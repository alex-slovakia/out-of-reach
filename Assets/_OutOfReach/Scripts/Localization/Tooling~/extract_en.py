"""LANG-01: extract the English player-facing strings and format patterns from the gameplay sources.

Output: a catalog JSON with
  exact   : plain sentences/labels (translated by exact match, also used recursively for embedded phrases)
  formats : patterns with positional placeholders {0}, {1:0.##} ... (from string.Format, interpolation, concatenation)
  sources : string -> ["file:line", ...] for translator context
"""
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(sys.argv[1])          # Assets/_OutOfReach/Scripts
OUT = pathlib.Path(sys.argv[2])
FOLDERS = ("Core", "Simulation", "Inventory", "Interaction")
SKIP_LINE = re.compile(r"throw new|Debug\.Log|Exception\(|nameof\(|\[Tooltip|\[Header|Assert\.|LogWarning|LogError|"
                       r"GetComponent|AddComponent|Shader\.|Resources\.Load|PlayerPrefs|\bQ\(|\.name\s*=|new GameObject")
PLACEHOLDER = re.compile(r"\{(\d+)(?:,[^}:]*)?(?::([^}]*))?\}")
IDENTIFIER_LIKE = re.compile(r"^[a-z0-9_.:/\-]+$")

# Composite messages built from several pieces of code that no single literal shows.
MANUAL_FORMATS = {
    "EVA suit on: {0} kg oxygen in the tank": "Core/MissionSession.cs (suit locker status)",
    "{0}: repaired": "Core/RepairInteraction.cs (Capitalize(DisplayName) + \": repaired\")",
    "{0}: nominal": "Core/RepairInteraction.cs (Capitalize(DisplayName) + \": nominal\")",
    "Debris impact. Condition after the strike: {0}.": "Core/MissionDirector.cs (journal)",
    "Insufficient reserves: {0}.": "Simulation/ResourceLedger.cs",
    "Spent on {0}": "Core/MissionReportBuilder.cs (report label)",
    "{0} back on the pallet.": "Core/CargoLoading.cs",
    "{0} loaded.": "Core/CargoLoading.cs",
}


# Single words inserted into sentences (resource names, purposes, fallbacks) that the literal filter skips.
MANUAL_EXACT = {
    "oxygen": "Simulation/HoldingEstimate.cs, ResourceLedger.cs", "water": "Simulation/HoldingEstimate.cs, ResourceLedger.cs",
    "food": "Simulation/HoldingEstimate.cs, ResourceLedger.cs", "repairs": "Core/MissionReportBuilder.cs",
    "transmission": "Core/MissionReportBuilder.cs", "nothing": "Core/MissionReportBuilder.cs, Simulation/ResourceLedger.cs",
    "unknown": "Simulation/CommunicationSystem.cs",
}
MANUAL_FORMATS["Preset '{0}'"] = "Core/MissionReportBuilder.cs (loadout line; the preset ID stays as is)"


def unescape(text):
    if "\\" not in text:
        return text
    return (text.replace("\\\\", "\u0000").replace('\\"', '"').replace("\\n", "\n").replace("\\t", "\t")
            .replace("\\'", "'").replace("\u0000", "\\"))


def scan_literals(line):
    """Yields (interpolated, body) for each C# string literal on the line, honouring quotes nested in {...}."""
    i = 0
    n = len(line)
    while i < n:
        c = line[i]
        if c == "/" and i + 1 < n and line[i + 1] == "/":
            return
        if c == "'":
            j = i + 1
            while j < n and line[j] != "'":
                j += 2 if line[j] == "\\" else 1
            i = j + 1
            continue
        interpolated = c == "$" and i + 1 < n and line[i + 1] == '"'
        if c == '"' or interpolated:
            j = i + (2 if interpolated else 1)
            body = []
            depth = 0
            while j < n:
                ch = line[j]
                if depth == 0:
                    if ch == "\\":
                        body.append(line[j:j + 2])
                        j += 2
                        continue
                    if ch == '"':
                        break
                    if interpolated and ch == "{":
                        if j + 1 < n and line[j + 1] == "{":
                            body.append("{{")
                            j += 2
                            continue
                        depth = 1
                        body.append(ch)
                        j += 1
                        continue
                    body.append(ch)
                    j += 1
                    continue
                # inside an interpolation expression: skip nested strings, track braces
                if ch == '"':
                    k = j + 1
                    while k < n and line[k] != '"':
                        k += 2 if line[k] == "\\" else 1
                    body.append(line[j:k + 1])
                    j = k + 1
                    continue
                if ch == "{":
                    depth += 1
                elif ch == "}":
                    depth -= 1
                body.append(ch)
                j += 1
            yield interpolated, "".join(body)
            i = j + 1
            continue
        i += 1


def interpolated_to_format(body):
    """$"A {x:0.0} B {y}" -> "A {0:0.0} B {1}" (expressions become positional placeholders)."""
    out = []
    i = 0
    n = 0
    while i < len(body):
        c = body[i]
        if c == "{" and i + 1 < len(body) and body[i + 1] == "{":
            out.append("{{")
            i += 2
            continue
        if c == "}" and i + 1 < len(body) and body[i + 1] == "}":
            out.append("}}")
            i += 2
            continue
        if c == "{":
            depth = 1
            j = i + 1
            in_string = False
            while j < len(body) and depth:
                ch = body[j]
                if ch == '"':
                    in_string = not in_string
                elif not in_string and ch == "{":
                    depth += 1
                elif not in_string and ch == "}":
                    depth -= 1
                j += 1
            expr = body[i + 1:j - 1]
            fmt = ""
            stripped = re.sub(r'"[^"]*"', '""', expr)
            colon = stripped.rfind(":")
            if colon > 0 and "?" not in stripped[:colon] and "(" not in stripped[colon:]:
                fmt = ":" + stripped[colon + 1:]
            out.append("{" + str(n) + fmt + "}")
            n += 1
            i = j
            continue
        out.append(c)
        i += 1
    return "".join(out)


def player_facing(text):
    if not text or not re.search(r"[A-Za-z]{2}", text):
        return False
    if IDENTIFIER_LIKE.match(text):
        return False
    if text.startswith(("OutOfReach", "Assets/", "http")) or ("/" in text and " " not in text):
        return False
    return True


exact = {}
formats = {}


def add(table, text, where):
    table.setdefault(text, [])
    if where not in table[text]:
        table[text].append(where)


CONCAT = re.compile(r'"((?:[^"\\\n]|\\.)*)"\s*\+\s*([^";+]+?)\s*\+\s*"((?:[^"\\\n]|\\.)*)"')
CONCAT_TAIL = re.compile(r'"((?:[^"\\\n]|\\.)*)"\s*\+\s*([A-Za-z_][\w.]*(?:\([^()"]*\))?)\s*(?=[;),])')

for folder in FOLDERS:
    for path in sorted((ROOT / folder).rglob("*.cs")):
        rel = str(path.relative_to(ROOT)).replace("\\", "/")
        for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            stripped = line.strip()
            if stripped.startswith("//") or stripped.startswith("///") or SKIP_LINE.search(line):
                continue
            where = f"{rel}:{number}"
            for interpolated, body in scan_literals(line):
                if interpolated:
                    pattern = unescape(interpolated_to_format(body))
                    if player_facing(re.sub(r"\{\d+[^}]*\}", "", pattern)):
                        add(formats, pattern, where)
                    continue
                text = unescape(body)
                if PLACEHOLDER.search(text):
                    if player_facing(PLACEHOLDER.sub("", text)):
                        add(formats, text, where)
                    continue
                if player_facing(text) and (" " in text.strip() or text[:1].isupper()):
                    add(exact, text, where)
            for a, _expr, b in CONCAT.findall(line):
                pattern = unescape(a) + "{0}" + unescape(b)
                if player_facing(unescape(a) + unescape(b)):
                    add(formats, pattern, where)
            for a, _expr in CONCAT_TAIL.findall(line):
                if a.strip() and player_facing(unescape(a)) and len(unescape(a).split()) >= 2:
                    add(formats, unescape(a) + "{0}", where)

for text, where in MANUAL_FORMATS.items():
    add(formats, text, "manual: " + where)
for text, where in MANUAL_EXACT.items():
    add(exact, text, "manual: " + where)

BAD = re.compile(r"Format\(|\{[A-Za-z_]|\)\}|=>|\bnew\b \w+\(|\?\?|\[\]|;\s*$")


def ok(s):
    return not BAD.search(s) and s.count("{") == s.count("}")


formats = {k: v for k, v in formats.items()
           if ok(k) and len(re.sub(r"[^A-Za-z]", "", PLACEHOLDER.sub("", k))) >= 3}
exact = {k: v for k, v in exact.items() if ok(k)}
catalog = {
    "exact": sorted(exact),
    "formats": sorted(formats),
    "sources": {k: v for k, v in sorted({**exact, **formats}.items())},
}
OUT.write_text(json.dumps(catalog, ensure_ascii=False, indent=1), encoding="utf-8")
print("exact", len(catalog["exact"]), "formats", len(catalog["formats"]))
