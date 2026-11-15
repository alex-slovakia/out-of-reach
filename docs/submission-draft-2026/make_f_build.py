"""Build the F set (F-build/), the set of the final candidate feature/claude-max-final-b@b2f7a89.

    python make_f_build.py            (rebuilds F-build/ completely, about 20 s)

It runs make_b_build.py with every B extra and the final deck (--deck-d), then writes the differences of the final
build into the texts: exact test counts, the demo field (video first, the deck as the second material), the star
sky sentence and a "SET F" section at the top of form-fields.md, credits-and-licenses.md and ai-disclosure.md.
The A and B sets are not changed. (The first version of this set was called D and matched f0ec271.)
Do not run it from a shell whose current folder is F-build: Windows cannot remove the folder then.
"""
from __future__ import annotations

import re
import shutil
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
OUT = HERE / "F-build"
SHA = "b2f7a89"
POST = "#7 6902220076"
ZIP = "OUT-OF-REACH_b2f7a89_Windows_x64.zip"
ZIP_SHA256 = "e3f5c8d4b0f15f5e9f70d1e6f19574d08c2ea5c21911bf24cd1112cadb947bfc"  # as given in the post above
TESTS = (544, 178)  # EditMode 544/544, PlayMode 178/178 on b2f7a89 (same post)

FORM_F = f"""## SET F (final): what differs from the B-visual set (read this first)

Checked against the final candidate `feature/claude-max-final-b@{SHA}` ({POST}; ZIP `{ZIP}`, SHA-256 `{ZIP_SHA256}` as given in that post; EditMode {TESTS[0]}/{TESTS[0]}, PlayMode {TESTS[1]}/{TESTS[1]}). The paste blocks below already hold everything of the B-visual set (Horizons range and signal delay on screen, radiation estimate from MSL RAD, Deep Space Network complex in view, Particle Pack launch effects, hull V2, voiced radio traffic). Set F adds:

| New against B-visual | Evidence on `{SHA}` | In this set |
|---|---|---|
| Launch vehicle "AURORA 7" (LAUNCH-COMPLEX-V01) and the reworked launch terrain V04 | `Assets/_OutOfReach/Visuals/Models/LaunchComplexV01/Provenance/ATTRIBUTION.md` (original geometry and textures, Blender + Python, no third-party meshes); installs `1e4fdd5` and `e53bce5` | Deck slide 3 picture (native QA run on `f0ec271`; the launch looks the same in `{SHA}`). Original team work: nothing to add to References |
| Spoken pre-launch briefing: five more Gemini takes (vo41 to vo45). 16 AI-generated radio clips in all (vo30 to vo45) | `Assets/_OutOfReach/Scripts/Presentation/Radio/Voices/voices.json` (16 clips: Orus 12, Iapetus 4, model `gemini-3.8-flash-lite-tts`) | Section 6 already names Gemini TTS and the voices Orus, Kore and Iapetus. Optional sentence F1 below. Deck slide 7 |
| The real NASA star sky (SVS Deep Star Maps 2020) is also seen during the spacewalk and in the Earth-orbit shot, not only in the ending | commit `8519490` (SPACE-SKY); Sources page, `UI/Report/Runtime/FlightRecorderPresenter.cs` lines 420 to 421 | Applied in section 7 (Deep Star Maps item). Deck slide 4, panel "ALSO FROM NASA" |
| The in-game Sources page names the voices Orus, Kore and Iapetus and the Unity Particle Pack | commit `cd14f4c`; same file, lines 428 to 431 | Matches sections 6 and 8 |
| Launching over the 600 kg limit is allowed and ends in an explained crash | commit `bfb7daa` (R27) | Optional sentence F2 below |
| Earth tablet on the wall, seated cockpit mounts, the seated body drawn in first person only, the radio prop is never shown | commits `715bd0c`, `2066761`, `cd84d67`, `c6ba6e0` | No text needed (the Poly Haven models are CC0 and already credited) |
| Reach fix for the antenna repair | commits `c8542a4`, `0d7c5d0` | No text needed; not on the slides |
| Test counts: EditMode {TESTS[0]}/{TESTS[0]}, PlayMode {TESTS[1]}/{TESTS[1]} | {POST} | Applied in section 5, the public README draft, README.txt and README.pdf; deck slide 6 |
| Demo | the video is the demo link, the deck is the second material | Section 3 below |

Optional sentences (add only if the field has room):

- **F1, section 6, after "no real person's voice was cloned.":** "This build adds 16 synthetic radio lines: the pre-launch briefing, the radio checks, the distress call, Mission Control's replies and the day-14 announcement."
- **F2, section 5, in the planning step:** "If you launch above the 600 kg payload limit, the game lets you go, the launch ends in a crash and the mission record explains why."
"""

DEMO_F = """## 3. Link to Project Demo

The form takes **one** link. **Set F: the demo is the VIDEO** (30 seconds or less, recorded from the final build). The Team Owner pastes the public link after he has uploaded the video. No link is written here because nothing was uploaded from our side.

```text
[PUBLIC LINK to the demo video of 30 seconds or less, added by the Team Owner after the upload. No login, permission or registration. Test it in a private browser window.]
```

**Second material, the 7-slide deck of this set:** [OUT-OF-REACH_SpaceApps2026_deck.pdf](OUT-OF-REACH_SpaceApps2026_deck.pdf) (editable source: [OUT-OF-REACH_SpaceApps2026_deck.pptx](OUT-OF-REACH_SpaceApps2026_deck.pptx)). Put the PDF into the public final-project folder of section 4, next to README.pdf, so the slides are reachable from the project link. If the video cannot be published in time, the link to the deck PDF goes into this field instead:

```text
[PUBLIC LINK to OUT-OF-REACH_SpaceApps2026_deck.pdf, 7 slides including the title. No login, permission or registration. Test it in a private browser window.]
```

Steps: [submission-checklist.md](../submission-checklist.md).

"""

CREDITS_F = f"""**Set F re-sweep, `9de3195..{SHA}`** (`feature/claude-max-final-b`, 15 Nov 2026, 22:22 UTC; `git diff --name-status 9de3195 {SHA}` filtered for attribution, provenance, notice, licence, readme and manifest files): **no new third-party content.**
- **Added:** `Visuals/Models/LaunchComplexV01/Provenance/ATTRIBUTION.md` and `README.md`, the launch vehicle "AURORA 7" of the launch cutscene. Geometry and textures are original (Blender 3.6 + Python scripts, numpy noise); no imported meshes, scans, photographs or third-party kits; no agency logo, flag or real programme marking (lettering `AURORA`, `7`, `LOX`, `LCH4` only). The letter outlines come from Blender's bundled font Bfont, a Bitstream Vera Sans derivative under the Bitstream Vera Fonts licence, which the permissions sentence already names; no font file ships.
- **Changed:** `Scripts/Presentation/Radio/Voices/voices.json` and `PROVENANCE.md`. The manifest lists 16 AI-generated radio clips (vo30 to vo45; new are the five pad briefing takes vo41 to vo45): Google Gemini TTS, model `gemini-3.8-flash-lite-tts`, voices Orus (Mission Control, Pad Control) and Iapetus (the player). The Gemini line in References already covers them.
- **Changed:** `Scripts/Presentation/LaunchGrassV04/Integration~/README.md` and `Scripts/Presentation/Rescue/README.md` (notes of original team work; the second one covers the star sky during the spacewalk and in the Earth-orbit shot, `8519490`, which uses the NASA SVS Deep Star Maps 2020 image that is already credited). The launch terrain V04 (`7d71461`, `7f998a2`, installed by `e53bce5`) brought no attribution or licence file; it is treated as original team work like terrain V03. Ask CLAUDE-MAX if in doubt.
- **In-game Sources page on `{SHA}`** (commit `cd14f4c`): it now credits "Launch effects — Particle Pack by Unity Technologies" and "Voices Orus, Kore and Iapetus. AI-generated audio." The texts of this set say the same.
- **No new asset source:** Earth tablet (`715bd0c`), seated cockpit mounts (`2066761`; Poly Haven models, CC0, already credited), launch seat (`426a92d`, `cd84d67`), overweight launch outcome (`bfb7daa`), antenna reach fix (`c8542a4`, `0d7c5d0`), radio prop hidden (`c6ba6e0`).
- Nothing was added under `Assets/StreamingAssets/ThirdParty`.
"""

AI_F = f"""## Set F (final build `feature/claude-max-final-b@{SHA}`): what changed for the SI disclosure

- **SI audio, 16 radio clips** (`Assets/_OutOfReach/Scripts/Presentation/Radio/Voices/`, vo30 to vo45): Google Gemini TTS, model `gemini-3.8-flash-lite-tts`, voices **Orus** (Mission Control, Pad Control; 12 clips) and **Iapetus** (the player's own calls; 4 clips). New against the B-visual set are the five pad briefing takes vo41 to vo45. Metadata label: `voices.json` (`"ai_generated": true` and a disclosure sentence for the whole manifest). Text label: the SI field of the form and the README name Gemini TTS and the voices Orus, Kore and Iapetus.
- **In-game Sources page on `{SHA}`** (`UI/Report/Runtime/FlightRecorderPresenter.cs`, line 431, commit `cd14f4c`): "Voices Orus, Kore and Iapetus. AI-generated audio." The game, the form text and the README now name the same three voices.
- **SI code and models:** the launch vehicle "AURORA 7" (`Visuals/Models/LaunchComplexV01`), the launch terrain V04, the Earth tablet, the overweight-launch outcome, the launch-seat change and the star sky during the spacewalk were written or generated with the same AI coding assistants as the rest of the project (inventory rows for code and procedural models). Labels unchanged: README, form text, provenance files. The star sky itself is the NASA SVS Deep Star Maps 2020 image, not an SI image.
- **No SI image or video** was added to the game or to the deck. The lift-off picture in the deck is a real game capture (native QA run on `f0ec271`; the launch looks the same in `{SHA}`).
- **Demo video:** made in another lane. A cut of real game captures needs no SI watermark; its synthetic voices are acknowledged in the form text. Check this line against the final cut.
"""

README_F = f"""
## Set F additions (this folder)

- This is the set of the final candidate `{SHA}` (ZIP `{ZIP}`). Differences against the B-visual set: the section "SET F" at the top of [form-fields.md](form-fields.md).
- Deck: lift-off picture with the AURORA 7 launch vehicle (slide 3), on-screen mission data block and the star sky line (slide 4), test counts {TESTS[0]}/{TESTS[0]} and {TESTS[1]}/{TESTS[1]} (slide 6), the 16 new radio lines (slide 7).
- [credits-and-licenses.md](credits-and-licenses.md) and [ai-disclosure.md](ai-disclosure.md) start with their own Set F section.
- Rebuild: `python make_f_build.py` in the folder above.
"""


def read(p: Path) -> str:
    return p.read_text(encoding="utf-8")


def write(p: Path, t: str):
    p.write_text(t, encoding="utf-8", newline="\n")


def insert_after(text: str, startswith: str, block: str) -> str:
    lines = text.split("\n")
    for i, line in enumerate(lines):
        if line.startswith(startswith):
            lines[i + 1:i + 1] = ["", *block.strip("\n").split("\n")]
            return "\n".join(lines)
    raise SystemExit(f"anchor not found: {startswith}")


def tests(text: str, name: str) -> str:
    text, n = re.subn(r"More than 460 automated(\s+)EditMode tests and 150 PlayMode scene tests pass on the "
                      r"release(\s+)candidate",
                      lambda m: f"All {TESTS[0]} automated{m.group(1)}EditMode tests and {TESTS[1]} PlayMode scene "
                                f"tests pass on the submitted{m.group(2)}build", text)
    if n < 1:  # form-fields.md has it twice (full and short description), the other files once
        raise SystemExit(f"{name}: test sentence not found")
    return text


def main() -> int:
    subprocess.run([sys.executable, str(HERE / "make_b_build.py"), "--out", "F-build", "--label", "F build",
                    "--sha", SHA, "--horizons", "--rad", "--dsn", "--particle", "--models", "--voices", "--deck-d"],
                   check=True)

    ff = OUT / "form-fields.md"
    t = tests(read(ff), ff.name)
    t, n = re.subn(r"## 3\. Link to Project Demo\n.*?(?=## 4\. Link to Final Project)", lambda m: DEMO_F, t, flags=re.S)
    if n != 1:
        raise SystemExit("form-fields.md: demo section not found")
    t, n = re.subn(r"Test counts: the texts say [^\n]*",
                   lambda m: f"Test counts in this set: EditMode {TESTS[0]}/{TESTS[0]} and PlayMode "
                             f"{TESTS[1]}/{TESTS[1]} on `{SHA}` ({POST}).", t)
    if n != 1:
        raise SystemExit("form-fields.md: test-count note not found")
    # the star sky is seen in three places of the final build (8519490)
    t, n = re.subn(r"ESA/Gaia/DPAC: the space sky\.",
                   "ESA/Gaia/DPAC: the space sky, seen during the spacewalk, in the Earth-orbit shot and in the "
                   "rescue finale.", t)
    print("star sky sentence applied:", n)
    t = insert_after(t, "**THIS IS THE SET FOR THE F BUILD**", FORM_F)
    write(ff, t)

    import make_b_build  # same folder; recount() rewrites the word and character counts above the paste blocks
    make_b_build.recount(ff)

    for name in ("public-README-draft.md", "final-project-folder/README.txt"):
        p = OUT / name
        write(p, tests(read(p), name))
    subprocess.run([sys.executable, str(HERE / "final-project-folder" / "make_readme_pdf.py"),
                    str(OUT / "final-project-folder" / "README.txt"),
                    str(OUT / "final-project-folder" / "README.pdf")], check=True)

    cr = OUT / "credits-and-licenses.md"
    write(cr, insert_after(read(cr), "# Credits and licences", CREDITS_F))

    ai = OUT / "ai-disclosure.md"
    shutil.copy2(HERE / "ai-disclosure.md", ai)
    write(ai, insert_after(read(ai), "# AI (", AI_F))

    rd = OUT / "README.md"
    write(rd, read(rd).rstrip("\n") + "\n" + README_F)
    print("F set ready:", OUT)
    return 0


if __name__ == "__main__":
    sys.exit(main())
