"""Build a second (or third) set of deliverables next to the A set (release candidate 5479e87).

    python make_b_build.py --out B-build --label "B-code build" --sha f6f4f96 --horizons --rad
    python make_b_build.py --out B-visual-build --label "B-visual build" --sha <final> \
        --horizons --rad --dsn --particle --models --voices
    python make_b_build.py --readme-only --out <folder> --label "<name>" --sha <sha> <flags>

The A files in this folder are not changed. The set folder is rebuilt from them every time:
copy -> apply_b_variant.py (same flags) -> recount the paste blocks -> deck (PPTX + flattened PDF) -> README.pdf.
Needs: python-pptx, Pillow, PyMuPDF, PowerPoint (PDF export without a window).
"""
from __future__ import annotations

import argparse
import os
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
OUT = HERE / "B-build"
FLAGS = ("horizons", "rad", "dsn", "particle", "models", "voices")
COPIED_MD = ("form-fields.md", "credits-and-licenses.md", "public-README-draft.md", "playthrough-checklist.md")
WHAT = {
    "horizons": "the NASA/JPL Horizons range and one-way signal time on the next-chapter strip during play",
    "rad": "the radiation estimate from NASA's MSL RAD cruise measurement (1.8 mSv per day), with its Sources entry",
    "dsn": "the line naming the Deep Space Network complex that has Mars in view",
    "particle": "launch effects made with the Unity Particle Pack (third-party, credited in References)",
    "models": "AURORA-7 exterior V2, capsules V02, terrain V03 and grass V04 (original team work, credits list only)",
    "voices": "eleven new Gemini radio takes (vo30-vo40): the radio traffic and the day-14 announcement are spoken; "
              "the SI texts name the extra voice Iapetus",
}
SHORT = {
    "horizons": "The Horizons line on the strip",
    "rad": "The radiation estimate",
    "dsn": "The DSN line",
    "particle": "The Particle Pack launch effects",
    "models": "The new models",
    "voices": "The new voice takes",
}
# What to change by hand in this set's form-fields.md if an extra is NOT in the build that ships (no script needed).
REMOVE = {
    "horizons": [
        'section 5, item 7: delete the sentence that starts "During play a line on the next-chapter strip keeps the '
        'NASA/JPL Horizons numbers in view"',
        'section 5, short version: delete the words "; the range and the one-way delay stay on screen during play"',
        'section 7, item 1: delete the sentence that starts "During play the same range and one-way light time stay '
        'on screen"',
    ],
    "rad": [
        'section 5, HOW IT WORKS: delete the sentence that starts "A radiation line on the next-chapter strip '
        'estimates the dose"',
        'section 5, short version: delete the sentence that starts "A radiation line estimates the dose"',
        'section 7: delete the whole numbered item that starts "NASA/JPL Mars Science Laboratory, Radiation '
        'Assessment Detector (RAD)"',
        'section 8, OTHER REFERENCES: delete the line that starts "- Zeitlin et al."',
    ],
    "dsn": [
        'section 5, item 7: delete the sentence that starts "The strip also names the Deep Space Network complex"',
        'section 7: delete the whole numbered item that starts "NASA Deep Space Network"',
    ],
    "particle": [
        'section 8, ENGINE: delete the words "Unity Particle Pack by Unity Technologies (Unity Asset Store; the '
        'launch smoke and exhaust effects), "',
        'section 8, PERMISSIONS: delete the words "a Unity Asset Store licence (Particle Pack), "',
    ],
    "voices": [
        'section 5, item 7: "Voiced radio traffic" becomes "Radio traffic", "spoken announcement" becomes '
        '"announcement"',
        'section 6: "Orus, Kore and Iapetus" becomes "Orus and Kore"; delete " and the player\'s own radio calls"',
        'section 8, AI SERVICES: "voices Orus, Kore, Iapetus" becomes "voices Orus, Kore"',
    ],
    "models": [],
}


def write_readme(out: Path, out_name: str, label: str, sha: str, flags: list[str]):
    lines = [
        f"# Set for the {label} — use ONLY if the {label} ships",
        "",
        f"Built by `make_b_build.py` and checked against `{sha}`.",
        "The A set (release candidate `5479e87`) is one folder up and is unchanged.",
        "",
        "**What these texts add to the A texts:**",
        "",
    ] + [f"- {WHAT[f]}" for f in flags] + [
        "",
        "**Which set do I paste?** Look at the final post on #7:",
        "",
        "| The build that ships | Use |",
        "|---|---|",
        "| A build = release candidate `5479e87` | the files one folder up: `../form-fields.md`, `../deck/…pdf`, "
        "`../final-project-folder/` |",
        f"| {label} | the files in this folder: `form-fields.md`, `OUT-OF-REACH_SpaceApps2026_deck.pdf`, "
        "`final-project-folder/` |",
        "",
        "## If one of the extras did NOT make it into the build (no script, two minutes by hand)",
        "",
        "Paste from this folder's `form-fields.md` and change only the parts listed for the missing extra:",
        "",
    ]
    for f in flags:
        if REMOVE.get(f):
            lines.append(f"- **{SHORT[f]} did not ship:**")
            lines += [f"  - {r}" for r in REMOVE[f]]
    lines += [
        "",
        'For the deck take the set whose deck does not mention the missing extra: slide 4 ("ALSO FROM NASA") and',
        "slide 7 name the radiation estimate, the DSN line and the Particle Pack only in the sets that have them.",
        "The A deck (`../deck/`) mentions none of them and is always safe.",
        "",
        "With the tools installed (python-pptx, Pillow, PyMuPDF, PowerPoint) the whole set can be rebuilt instead:",
        f'`python make_b_build.py --out {out_name} --label "{label}" --sha <final> <flags>`.',
        "",
        "Files here: `form-fields.md` (8 paste blocks), `OUT-OF-REACH_SpaceApps2026_deck.pdf` + `.pptx` (7 slides),",
        "`final-project-folder/` (`README.pdf`, `README.txt`, `CREDITS.txt`), and this build's versions of",
        "`credits-and-licenses.md`, `public-README-draft.md` and `playthrough-checklist.md`.",
        "The submission checklist, the AI disclosure and `UPLOAD-LIST.txt` are the same for every build (one "
        "folder up).",
        "",
    ]
    (out / "README.md").write_text("\n".join(lines), encoding="utf-8", newline="\n")


def run(cmd, **kw):
    print("  $", " ".join(str(c) for c in cmd))
    subprocess.run(cmd, check=True, **kw)


def recount(path: Path):
    """Rewrite the '(N words, M characters)' notes above the two description blocks."""
    text = path.read_text(encoding="utf-8")

    def block_after(marker: str) -> str:
        start = text.index("```text\n", text.index(marker)) + len("```text\n")
        return text[start:text.index("\n```", start)]

    def note(block: str) -> str:
        words = len(re.findall(r"\S+", block))
        return f"({words:,} words, {len(block):,} characters)"

    short_marker = "Short version ("
    idx_short = text.index(short_marker, text.index("## 5. Detailed Project Description"))
    full = block_after("## 5. Detailed Project Description")
    short = block_after(text[idx_short:idx_short + 40])
    text = re.sub(r"did you use\?\* \([\d,]+ words, [\d,]+ characters\)",
                  "did you use?* " + note(full), text, count=1)
    head, tail = text[:idx_short], text[idx_short:]
    tail = re.sub(r"Short version \([\d,]+ words, [\d,]+ characters\)", "Short version " + note(short), tail, count=1)
    path.write_text(head + tail, encoding="utf-8", newline="\n")
    print("  counts:", note(full), "/ short", note(short))


def relink(path: Path):
    """Links to files that are not copied into the set folder point one folder up."""
    text = path.read_text(encoding="utf-8")

    def fix(m: re.Match) -> str:
        target = m.group(2)
        if re.match(r"^(https?:|#|\.\./)", target):
            return m.group(0)
        file_part = target.split("#")[0]
        if (OUT / file_part).exists():
            return m.group(0)
        return f"{m.group(1)}(../{target})"

    path.write_text(re.sub(r"(\[[^\]]+\])\(([^)]+)\)", fix, text), encoding="utf-8", newline="\n")


def main() -> int:
    global OUT
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    for flag in FLAGS:
        ap.add_argument("--" + flag, action="store_true")
    ap.add_argument("--sha", required=True, help="the SHA these texts were checked against")
    ap.add_argument("--out", default="B-build", help="output folder name next to the A set (default B-build)")
    ap.add_argument("--label", default="B build", help="how the README calls this build")
    ap.add_argument("--readme-only", action="store_true", help="rewrite only README.md of an existing set")
    ap.add_argument("--deck-d", action="store_true",
                    help="build the deck of the D build (new launch capture, data-lines block, exact test counts, "
                         "16 new radio lines); see D_BUILD in deck/build_deck.py")
    args = ap.parse_args()
    OUT = HERE / args.out
    flags = [f for f in FLAGS if getattr(args, f)]
    if not flags:
        ap.error("give at least one flag")
    if args.readme_only:
        write_readme(OUT, args.out, args.label, args.sha, flags)
        print("README rewritten:", OUT / "README.md")
        return 0

    with tempfile.TemporaryDirectory() as tmp:
        work = Path(tmp) / "pack"
        shutil.copytree(HERE, work, ignore=shutil.ignore_patterns("B-*build", "D-build", ".build", "captures", "*.pptx", "*.pdf",
                                                                 "__pycache__"))
        run([sys.executable, str(HERE / "apply_b_variant.py"), "--root", str(work)] + ["--" + f for f in flags])
        recount(work / "form-fields.md")
        if OUT.exists():
            shutil.rmtree(OUT)
        (OUT / "final-project-folder").mkdir(parents=True)
        for name in COPIED_MD:
            shutil.copy2(work / name, OUT / name)
        for name in ("README.txt", "CREDITS.txt"):
            shutil.copy2(work / "final-project-folder" / name, OUT / "final-project-folder" / name)

    # the notice apply_b_variant.py put into form-fields.md names the script; say what this folder is instead
    ff = OUT / "form-fields.md"
    text = ff.read_text(encoding="utf-8")
    text = re.sub(r"\*\*B-build variant applied \(`apply_b_variant\.py`\):\*\*[^\n]*\n",
                  f"**THIS IS THE SET FOR THE {args.label.upper()}** (checked against `{args.sha}`; extras: "
                  + ", ".join(flags) + f"). Use it only if the {args.label} is the one that ships. "
                  "The A set for the release candidate `5479e87` is one folder up.\n", text, count=1)
    # in this folder the deck is the deck of this set next to the texts, not ../deck/
    text = text.replace("(ready now, [deck/](deck/))",
                        "(ready now: [OUT-OF-REACH_SpaceApps2026_deck.pdf](OUT-OF-REACH_SpaceApps2026_deck.pdf) "
                        "in this folder, the deck of this set)")
    text = text.replace("| **Not in the RC** (offered for a B build) | switch sentences below |",
                        "| **In this set:** " + ", ".join(flags) + ". The rest: not included | switch sentences "
                        "below |")
    ff.write_text(text, encoding="utf-8", newline="\n")
    for name in COPIED_MD:
        relink(OUT / name)

    pptx = OUT / "OUT-OF-REACH_SpaceApps2026_deck.pptx"
    pdf = OUT / "OUT-OF-REACH_SpaceApps2026_deck.pdf"
    env = dict(os.environ, OOR_DECK_FLAGS=",".join(f for f in flags if f not in ("models", "voices")),
               OOR_DECK_OUT=str(pptx))
    if args.deck_d:
        env["OOR_DECK_D"] = "1"
    run([sys.executable, str(HERE / "deck" / "build_deck.py")], env=env)
    with tempfile.TemporaryDirectory() as pngs:
        run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
             str(HERE / "deck" / "export_pdf.ps1"), "-Pptx", str(pptx), "-Pdf", str(pdf), "-PngDir", pngs])
        # same look in every PDF viewer (see deck/flatten_pdf.py)
        run([sys.executable, str(HERE / "deck" / "flatten_pdf.py"), str(pdf), pngs])
    run([sys.executable, str(HERE / "final-project-folder" / "make_readme_pdf.py"),
         str(OUT / "final-project-folder" / "README.txt"), str(OUT / "final-project-folder" / "README.pdf")])

    write_readme(OUT, args.out, args.label, args.sha, flags)
    print("set ready:", OUT)
    return 0


if __name__ == "__main__":
    sys.exit(main())
