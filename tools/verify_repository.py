"""Offline source hygiene checks. Does not replace a Unity compile or playtest."""
from pathlib import Path
import json
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
errors = []


def require(condition, message):
    if not condition:
        errors.append(message)


version = (ROOT / "ProjectSettings/ProjectVersion.txt").read_text()
require("m_EditorVersion: 6000.3.28f1\n" in version,
        "Both developers must use Unity 6000.3.28f1.")
require("m_SerializationMode: 2" in (ROOT / "ProjectSettings/EditorSettings.asset").read_text(),
        "Force Text serialization is required.")
require("m_Mode: Visible Meta Files" in (ROOT / "ProjectSettings/VersionControlSettings.asset").read_text(),
        "Visible Meta Files is required.")

guids = {}
for path in (ROOT / "Assets").rglob("*"):
    if path.name.startswith(".") or path.suffix == ".meta":
        continue
    meta = Path(str(path) + ".meta")
    require(meta.exists(), f"Missing meta: {path.relative_to(ROOT)}")
    if meta.exists():
        match = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(), re.M)
        require(match is not None, f"Invalid GUID: {meta.relative_to(ROOT)}")
        if match:
            guid = match.group(1)
            require(guid not in guids, f"Duplicate GUID: {meta.relative_to(ROOT)} / {guids.get(guid)}")
            guids[guid] = str(meta.relative_to(ROOT))

for folder in ("Assets", "Packages"):
    for path in (ROOT / folder).rglob("*"):
        if path.suffix in (".json", ".asmdef"):
            try:
                json.loads(path.read_text(encoding="utf-8-sig"))
            except (ValueError, UnicodeError) as error:
                errors.append(f"Invalid JSON: {path.relative_to(ROOT)}: {error}")

for folder in ("Library", "Temp", "Logs", "UserSettings", "obj", "Builds", ".local"):
    result = subprocess.run(["git", "check-ignore", "-q", f"{folder}/verification.tmp"], cwd=ROOT)
    require(result.returncode == 0, f"Generated directory not ignored: {folder}")

tracked = subprocess.check_output(["git", "ls-files"], cwd=ROOT, text=True).splitlines()
for name in tracked:
    require(name.split("/")[0].lower() not in ("library", "temp", "logs", "usersettings", "obj", "builds", ".local"),
            f"Generated file is tracked: {name}")

if errors:
    raise SystemExit("\n".join(errors))
print(f"PASS: Unity version, serialization, {len(guids)} asset GUIDs, JSON, generated-file exclusions.")
