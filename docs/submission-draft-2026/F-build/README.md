# Set for the F build — use ONLY if the F build ships

Built by `make_b_build.py` and checked against `b2f7a89`.
The A set (release candidate `5479e87`) is one folder up and is unchanged.

**What these texts add to the A texts:**

- the NASA/JPL Horizons range and one-way signal time on the next-chapter strip during play
- the radiation estimate from NASA's MSL RAD cruise measurement (1.8 mSv per day), with its Sources entry
- the line naming the Deep Space Network complex that has Mars in view
- launch effects made with the Unity Particle Pack (third-party, credited in References)
- AURORA-7 exterior V2, capsules V02, terrain V03 and grass V04 (original team work, credits list only)
- eleven new Gemini radio takes (vo30-vo40): the radio traffic and the day-14 announcement are spoken; the SI texts name the extra voice Iapetus

**Which set do I paste?** Look at the final post on #7:

| The build that ships | Use |
|---|---|
| A build = release candidate `5479e87` | the files one folder up: `../form-fields.md`, `../deck/…pdf`, `../final-project-folder/` |
| F build | the files in this folder: `form-fields.md`, `OUT-OF-REACH_SpaceApps2026_deck.pdf`, `final-project-folder/` |

## If one of the extras did NOT make it into the build (no script, two minutes by hand)

Paste from this folder's `form-fields.md` and change only the parts listed for the missing extra:

- **The Horizons line on the strip did not ship:**
  - section 5, item 7: delete the sentence that starts "During play a line on the next-chapter strip keeps the NASA/JPL Horizons numbers in view"
  - section 5, short version: delete the words "; the range and the one-way delay stay on screen during play"
  - section 7, item 1: delete the sentence that starts "During play the same range and one-way light time stay on screen"
- **The radiation estimate did not ship:**
  - section 5, HOW IT WORKS: delete the sentence that starts "A radiation line on the next-chapter strip estimates the dose"
  - section 5, short version: delete the sentence that starts "A radiation line estimates the dose"
  - section 7: delete the whole numbered item that starts "NASA/JPL Mars Science Laboratory, Radiation Assessment Detector (RAD)"
  - section 8, OTHER REFERENCES: delete the line that starts "- Zeitlin et al."
- **The DSN line did not ship:**
  - section 5, item 7: delete the sentence that starts "The strip also names the Deep Space Network complex"
  - section 7: delete the whole numbered item that starts "NASA Deep Space Network"
- **The Particle Pack launch effects did not ship:**
  - section 8, ENGINE: delete the words "Unity Particle Pack by Unity Technologies (Unity Asset Store; the launch smoke and exhaust effects), "
  - section 8, PERMISSIONS: delete the words "a Unity Asset Store licence (Particle Pack), "
- **The new voice takes did not ship:**
  - section 5, item 7: "Voiced radio traffic" becomes "Radio traffic", "spoken announcement" becomes "announcement"
  - section 6: "Orus, Kore and Iapetus" becomes "Orus and Kore"; delete " and the player's own radio calls"
  - section 8, AI SERVICES: "voices Orus, Kore, Iapetus" becomes "voices Orus, Kore"

For the deck take the set whose deck does not mention the missing extra: slide 4 ("ALSO FROM NASA") and
slide 7 name the radiation estimate, the DSN line and the Particle Pack only in the sets that have them.
The A deck (`../deck/`) mentions none of them and is always safe.

With the tools installed (python-pptx, Pillow, PyMuPDF, PowerPoint) the whole set can be rebuilt instead:
`python make_b_build.py --out F-build --label "F build" --sha <final> <flags>`.

Files here: `form-fields.md` (8 paste blocks), `OUT-OF-REACH_SpaceApps2026_deck.pdf` + `.pptx` (7 slides),
`final-project-folder/` (`README.pdf`, `README.txt`, `CREDITS.txt`), and this build's versions of
`credits-and-licenses.md`, `public-README-draft.md` and `playthrough-checklist.md`.
The submission checklist, the AI disclosure and `UPLOAD-LIST.txt` are the same for every build (one folder up).

## Set F additions (this folder)

- This is the set of the final candidate `b2f7a89` (ZIP `OUT-OF-REACH_b2f7a89_Windows_x64.zip`). Differences against the B-visual set: the section "SET F" at the top of [form-fields.md](form-fields.md).
- Deck: lift-off picture with the AURORA 7 launch vehicle (slide 3), on-screen mission data block and the star sky line (slide 4), test counts 544/544 and 178/178 (slide 6), the 16 new radio lines (slide 7).
- [credits-and-licenses.md](credits-and-licenses.md) and [ai-disclosure.md](ai-disclosure.md) start with their own Set F section.
- Rebuild: `python make_f_build.py` in the folder above.
