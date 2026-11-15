# Set for the B-visual build — use ONLY if the B-visual build ships

Built by `make_b_build.py` and checked against `feature/claude-max-final@05d7a83`.
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
| B-visual build | the files in this folder: `form-fields.md`, `OUT-OF-REACH_SpaceApps2026_deck.pdf`, `final-project-folder/` |

If the build that ships has more or fewer extras than listed above, this folder must be rebuilt first
(`python make_b_build.py --out B-visual-build --label "B-visual build" --sha <final> <flags>`; the checks are at the end of `form-fields.md`).

Files here: `form-fields.md` (8 paste blocks), `OUT-OF-REACH_SpaceApps2026_deck.pdf` + `.pptx` (7 slides),
`final-project-folder/` (`README.pdf`, `README.txt`, `CREDITS.txt`), and the B versions of
`credits-and-licenses.md`, `public-README-draft.md` and `playthrough-checklist.md`.
The submission checklist, the AI disclosure and `UPLOAD-LIST.txt` are the same for both builds (one folder up).
