# Set for the B-code build — use ONLY if the B-code build ships

Built by `make_b_build.py` and checked against `feature/claude-max-final@f6f4f96`.
The A set (release candidate `5479e87`) is one folder up and is unchanged.

**What these texts add to the A texts:**

- the NASA/JPL Horizons range and one-way signal time on the next-chapter strip during play
- the radiation estimate from NASA's MSL RAD cruise measurement (1.8 mSv per day), with its Sources entry

**Which set do I paste?** Look at the final post on #7:

| The build that ships | Use |
|---|---|
| A build = release candidate `5479e87` | the files one folder up: `../form-fields.md`, `../deck/…pdf`, `../final-project-folder/` |
| B-code build | the files in this folder: `form-fields.md`, `OUT-OF-REACH_SpaceApps2026_deck.pdf`, `final-project-folder/` |

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

For the deck take the set whose deck does not mention the missing extra: slide 4 ("ALSO FROM NASA") and
slide 7 name the radiation estimate, the DSN line and the Particle Pack only in the sets that have them.
The A deck (`../deck/`) mentions none of them and is always safe.

With the tools installed (python-pptx, Pillow, PyMuPDF, PowerPoint) the whole set can be rebuilt instead:
`python make_b_build.py --out B-build --label "B-code build" --sha <final> <flags>`.

Files here: `form-fields.md` (8 paste blocks), `OUT-OF-REACH_SpaceApps2026_deck.pdf` + `.pptx` (7 slides),
`final-project-folder/` (`README.pdf`, `README.txt`, `CREDITS.txt`), and this build's versions of
`credits-and-licenses.md`, `public-README-draft.md` and `playthrough-checklist.md`.
The submission checklist, the AI disclosure and `UPLOAD-LIST.txt` are the same for every build (one folder up).
