"""Switch the submission pack from the release candidate (A build, 5479e87) to a B build.

Run only after the final SHA is declared and the checks in form-fields.md ("Switch sentences") pass:

    python apply_b_variant.py --horizons --rad            (the two approved NASA-data lines)
    python apply_b_variant.py --horizons --rad --dsn --particle --models

Flags (each only if the feature is really in the submitted build):
    --horizons   Horizons range and one-way delay on the next-chapter strip (6b69313 + c158a10)
    --rad        radiation estimate from NASA MSL RAD, 1.8 mSv/day (119a083, Sources entry eb890aa)
    --dsn        Deep Space Network complex in view, visible in the UI (8aea692 plus a UI hook)
    --particle   launch effects made with the Unity Particle Pack (1b733ac)
    --models     AURORA-7 exterior V2 and capsules V02 (original team work; credits list only)

Every replacement must match exactly once, otherwise nothing is written (run again after fixing the anchor).
Afterwards: python count_blocks.py form-fields.md (update the counts above the changed blocks),
python deck/build_deck.py, deck/export_pdf.ps1, python final-project-folder/make_readme_pdf.py, commit.
Use --root <folder> to try it on a copy of this folder first.
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

RAD_URL = "https://www.jpl.nasa.gov/news/data-from-nasa-rovers-voyage-to-mars-aids-planning/"

RAD_ITEM = (
    "NASA/JPL Mars Science Laboratory, Radiation Assessment Detector (RAD): inside the spacecraft on its "
    "cruise to Mars, RAD measured an average of 1.8 millisieverts of galactic cosmic rays per day (NASA/JPL, "
    "\"Data From NASA Rover's Voyage to Mars Aids Planning\", 30 May 2013, " + RAD_URL + "; Zeitlin et al., "
    "Science 340, 2013). The next-chapter strip shows a labelled estimate of the dose since day 1: elapsed "
    "mission days x 1.8 mSv per day. It is information only: no rule, resource or outcome reads it, and "
    "AURORA-7's shielding and the solar cycle are not modelled."
)
DSN_ITEM = (
    "NASA Deep Space Network: the three 70 m antenna sites DSS-14 Goldstone, DSS-63 Madrid and DSS-43 "
    "Canberra, with coordinates from NASA/JPL Horizons observer-site records. From the packaged Horizons "
    "vectors and Earth's rotation the game computes which complex has Mars at least 10 degrees above its "
    "horizon at the mission time; the result agrees with Horizons observer ephemerides to within 0.004 "
    "degrees at 24 checked values. It is the geometric visibility of Mars' centre, not the real DSN "
    "tracking schedule and not the position of the fictional ship."
)
NASA_CLOSING = ("\n\nAURORA-7, launch complex LC-7, the crew and the rescue are fictional. Mission time is "
                "compressed and always labelled.")

FF, CR, RT, PR, PT, DK, RM = ("form-fields.md", "credits-and-licenses.md", "final-project-folder/README.txt",
                              "public-README-draft.md", "playthrough-checklist.md", "deck/build_deck.py",
                              "README.md")

EDITS = {
    "horizons": [
        (FF, "and stops at the first danger. Radio traffic reaches you",
         "and stops at the first danger. During play a line on the next-chapter strip keeps the NASA/JPL "
         "Horizons numbers in view: the Earth-Mars range and the one-way signal time. Radio traffic reaches you"),
        (FF, "The same model times Earth's day-14 message. Reserves drain during every wait",
         "The same model times Earth's day-14 message. During play the same range and one-way light time stay "
         "on screen on the next-chapter strip, labelled NASA/JPL Horizons. Reserves drain during every wait"),
        (FF, "plus 600 s of assumed processing. NASA BVAD rates",
         "plus 600 s of assumed processing; the range and the one-way delay stay on screen during play. "
         "NASA BVAD rates"),
        (RT, "reply; it also times the day-14 message.",
         "reply; it also times the day-14 message, and the range and the one-way signal time stay on\n"
         "  screen during play."),
        (PR, "= the earliest reply; it also times the day-14 message |",
         "= the earliest reply; it also times the day-14 message, and the range and one-way delay stay on "
         "screen during play |"),
        (DK, "HORIZONS_LINE_SHIPPED = False", "HORIZONS_LINE_SHIPPED = True"),
        (PT, "| C17 | Runs from a fresh unzip",
         "| C18 | **Horizons line during play:** the next-chapter strip shows \"Earth <distance> · signal "
         "<delay> one way\", labelled NASA/JPL Horizons (description 7, NASA Data 1, deck 4) | Look at the strip "
         "bottom right between chapters | ☐ ☐ | Run the pack without `--horizons` (restore the files) |\n"
         "| C17 | Runs from a fresh unzip"),
    ],
    "rad": [
        (FF, "follow BVAD EVA values for a fictional suit.\nTime compression is always shown",
         "follow BVAD EVA values for a fictional suit.\nA radiation line on the next-chapter strip estimates "
         "the dose since day 1 from the 1.8 mSv per day that NASA's RAD instrument measured inside the Mars "
         "Science Laboratory spacecraft on its cruise to Mars; it is information only and changes no outcome."
         "\nTime compression is always shown"),
        (FF, "- NASA Space Apps 2026 challenge \"Space Mission Design Game\" and its resources (GMAT).",
         "- NASA Space Apps 2026 challenge \"Space Mission Design Game\" and its resources (GMAT).\n"
         "- Zeitlin et al., \"Measurements of Energetic Particle Radiation in Transit to Mars on the Mars "
         "Science Laboratory\", Science 340 (2013): the paper behind the NASA/JPL RAD cruise figure."),
        (FF, "spacewalk costs follow BVAD EVA values. Time compression is always shown",
         "spacewalk costs follow BVAD EVA values. A radiation line estimates the dose from NASA's MSL RAD "
         "cruise measurement (1.8 mSv per day), for information only. Time compression is always shown"),
        (CR, "- Earth–Mars distances: NASA/JPL Horizons. Life-support values: NASA Life Support Baseline Values "
             "and\n  Assumptions Document (NASA/TP-2015-218570/REV2).",
         "- Earth–Mars distances: NASA/JPL Horizons. Life-support values: NASA Life Support Baseline Values "
         "and\n  Assumptions Document (NASA/TP-2015-218570/REV2).\n"
         "- Radiation estimate: NASA/JPL Mars Science Laboratory, Radiation Assessment Detector (RAD), cruise\n"
         "  average of 1.8 mSv per day (NASA/JPL, 30 May 2013; Zeitlin et al., Science 340, 2013)."),
        (CR, "| GMAT star catalogue derivative |",
         "| Radiation estimate on the next-chapter strip (elapsed days × 1.8 mSv/day, display only) | NASA/JPL, "
         "\"Data From NASA Rover's Voyage to Mars Aids Planning\", 30 May 2013, " + RAD_URL + " ; Zeitlin et "
         "al., Science 340 (2013), PMID 23723233 | NASA/JPL news release (US Government work); the paper is "
         "cited, not copied | `UI/Chapter/**` (`ChapterCopy.RadCruiseMilliSievertsPerDay`), commit `119a083`; "
         "in-game Sources entry `eb890aa` | **Yes** (B build) | \"NASA/JPL, Mars Science Laboratory RAD: an "
         "average of 1.8 mSv of galactic cosmic rays per day on the cruise to Mars\" |\n"
         "| GMAT star catalogue derivative |"),
        (RT, "- NASA/JPL-Caltech Viking Mars map: Mars in the recovery cinematic.",
         "- NASA/JPL-Caltech Viking Mars map: Mars in the recovery cinematic.\n"
         "- NASA/JPL Mars Science Laboratory RAD (an average of 1.8 mSv per day measured on the cruise to\n"
         "  Mars): the radiation estimate shown during play, for information only."),
        (PR, "| NASA/JPL-Caltech Viking Mars map | Mars in the recovery cinematic |",
         "| NASA/JPL-Caltech Viking Mars map | Mars in the recovery cinematic |\n"
         "| NASA/JPL Mars Science Laboratory RAD (1.8 mSv per day on the cruise to Mars) | The radiation "
         "estimate shown during play, for information only |"),
        (DK, "RAD_SHIPPED = False", "RAD_SHIPPED = True"),
        (PT, "| C17 | Runs from a fresh unzip",
         "| C19 | **Radiation estimate:** \"Radiation since Day 1 ≈ … mSv (est. at 1.8 mSv/day, NASA MSL RAD "
         "cruise measurement)\" on the strip, and a RAD entry with its NASA/JPL link on the Sources page "
         "(NASA Data, deck 4/7) | Strip between chapters; Sources page | ☐ ☐ | No Sources entry → report on #7 "
         "before submitting; no line → run the pack without `--rad` |\n"
         "| C17 | Runs from a fresh unzip"),
    ],
    "dsn": [
        (FF, " Radio traffic reaches you on a handheld radio that rises into view.",
         " The strip also names the Deep Space Network complex (Goldstone, Madrid or Canberra) that has Mars "
         "above its horizon at that moment. Radio traffic reaches you on a handheld radio that rises into view."),
        (CR, "- Star data in the source package: NASA General Mission Analysis Tool (GMAT), Apache License 2.0.",
         "- Deep Space Network view: site coordinates of DSS-14 Goldstone, DSS-63 Madrid and DSS-43 Canberra\n"
         "  from NASA/JPL Horizons; the visibility of Mars is computed by the game.\n"
         "- Star data in the source package: NASA General Mission Analysis Tool (GMAT), Apache License 2.0."),
        (CR, "| Background references (no media copied) |",
         "| Deep Space Network complex in view (geometric visibility of Mars above a 10° mask) | NASA/JPL "
         "Horizons observer-site records of DSS-14, DSS-63 and DSS-43 (`CENTER='-14@399'`, `'-63@399'`, "
         "`'-43@399'`) and the packaged Earth–Mars vectors; checked against Horizons observer ephemerides | US "
         "Government work | `UI/Chapter/Runtime/DsnVisibility.cs`, `UI/Chapter/Integration~/DSN-README.md` "
         "(commit `8aea692`) | **Yes** (B build, if hooked to the UI) | \"DSN site coordinates: NASA/JPL "
         "Horizons\"; say it is not the real tracking schedule |\n"
         "| Background references (no media copied) |"),
        (RT, "- NASA/JPL-Caltech Viking Mars map: Mars in the recovery cinematic.",
         "- NASA/JPL-Caltech Viking Mars map: Mars in the recovery cinematic.\n"
         "- NASA Deep Space Network sites (Goldstone, Madrid, Canberra; coordinates from NASA/JPL Horizons):\n"
         "  the line that names the complex with Mars in view."),
        (PR, "| NASA/JPL-Caltech Viking Mars map | Mars in the recovery cinematic |",
         "| NASA/JPL-Caltech Viking Mars map | Mars in the recovery cinematic |\n"
         "| NASA Deep Space Network sites (coordinates from NASA/JPL Horizons) | The line that names the "
         "complex with Mars in view |"),
        (DK, "DSN_SHIPPED = False", "DSN_SHIPPED = True"),
        (PT, "| C17 | Runs from a fresh unzip",
         "| C20 | **DSN line:** \"DSN: <complex> has Mars in view\" during play (description 7, NASA Data) | "
         "Strip between chapters | ☐ ☐ | Run the pack without `--dsn` |\n"
         "| C17 | Runs from a fresh unzip"),
    ],
    "particle": [
        (FF, "Unity Test Framework 1.6.0, UI Toolkit), Blender 5.3 and 3.6",
         "Unity Test Framework 1.6.0, UI Toolkit), Unity Particle Pack by Unity Technologies (Unity Asset "
         "Store; the launch smoke and exhaust effects), Blender 5.3 and 3.6"),
        (FF, "Bitstream Vera/DejaVu, Apache-2.0, a NASA, NOAA or ESA public-reuse release",
         "Bitstream Vera/DejaVu, Apache-2.0, a Unity Asset Store licence (Particle Pack), a NASA, NOAA or ESA "
         "public-reuse release"),
        (CR, "- MakeHuman base mesh, skeleton and weights (makehumancommunity.org).",
         "- MakeHuman base mesh, skeleton and weights (makehumancommunity.org).\n\n"
         "Effects (Unity Asset Store licence)\n"
         "- Unity Particle Pack by Unity Technologies: launch smoke and exhaust effects."),
        (CR, "Blender 5.3.0 and 3.6.1, Python 3 with NumPy and Pillow, FFmpeg, Git, Git LFS, GitHub.",
         "Blender 5.3.0 and 3.6.1, Python 3 with NumPy and Pillow, FFmpeg, Git, Git LFS, GitHub. **Unity "
         "Particle Pack** (Unity Technologies, Unity Asset Store): the launch effects of the B build use it. "
         "Its content sits in `Assets/UnityTechnologies/ParticlePack/`, is not committed and must never be "
         "published as source (Asset Store licence); it ships only inside the built game."),
        (DK, "PARTICLE_PACK_SHIPPED = False", "PARTICLE_PACK_SHIPPED = True"),
    ],
    "models": [
        (CR, "launch towers V02 and V03, transport,",
         "launch towers V02 and V03, AURORA-7 exterior V2 and capsules V02 (scripted Blender builds, \"nothing "
         "is downloaded\", no agency logos), transport,"),
    ],
}


def next_nasa_number(text: str) -> int:
    """Highest numbered item of the NASA Data paste block + 1."""
    start = text.index("## 7. NASA Data")
    end = text.index("## 8. Space Agency Partner Data")
    numbers = [int(n) for n in re.findall(r"^(\d+)\. ", text[start:end], flags=re.M)]
    return max(numbers) + 1


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    for flag in EDITS:
        ap.add_argument("--" + flag, action="store_true")
    ap.add_argument("--root", default=str(Path(__file__).resolve().parent))
    args = ap.parse_args()
    root = Path(args.root)
    flags = [f for f in EDITS if getattr(args, f)]
    if not flags:
        ap.error("give at least one flag")
    texts: dict[str, str] = {}
    problems = []

    def load(name: str) -> str:
        if name not in texts:
            texts[name] = (root / name).read_text(encoding="utf-8")
        return texts[name]

    for flag in flags:
        for name, old, new in EDITS[flag]:
            text = load(name)
            n = text.count(old)
            if n != 1:
                problems.append(f"--{flag}: {name}: anchor found {n} times: {old[:70]!r}")
                continue
            texts[name] = text.replace(old, new)
    # numbered NASA Data items go to the end of the list, so nothing is renumbered
    for flag, item in (("rad", RAD_ITEM), ("dsn", DSN_ITEM)):
        if flag in flags:
            text = load(FF)
            if text.count(NASA_CLOSING) != 1:
                problems.append(f"--{flag}: {FF}: NASA Data closing paragraph not found exactly once")
                continue
            texts[FF] = text.replace(NASA_CLOSING, f"\n\n{next_nasa_number(text)}. {item}" + NASA_CLOSING)
    notice = ("**B-build variant applied (`apply_b_variant.py`):** " + ", ".join("--" + f for f in flags)
              + ". The paste blocks below describe the B build.\n\n")
    for name, anchor in ((FF, "How to use:"), (RM, "## Start here (in this order)")):
        text = load(name)
        if text.count(anchor) != 1:
            problems.append(f"{name}: notice anchor found {text.count(anchor)} times")
            continue
        texts[name] = text.replace(anchor, notice + anchor)
    if problems:
        print("NOTHING WRITTEN:\n  " + "\n  ".join(problems))
        return 1
    for name, text in texts.items():
        (root / name).write_text(text, encoding="utf-8", newline="\n")
        print("updated", name)
    # CREDITS.txt of the public folder = block A of the credits file
    credits = texts.get(CR) or (root / CR).read_text(encoding="utf-8")
    start = credits.index("CREDITS — OUT OF REACH")
    block = credits[start:credits.index("\n```", start)] + "\n"
    (root / "final-project-folder" / "CREDITS.txt").write_text(block, encoding="utf-8", newline="\n")
    print("updated final-project-folder/CREDITS.txt")
    print("NEXT: count_blocks.py form-fields.md -> update the counts; deck/build_deck.py + export_pdf.ps1; "
          "final-project-folder/make_readme_pdf.py; commit.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
