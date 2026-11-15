# B-build set — use ONLY if the B build ships

Built by `make_b_build.py` and checked against `feature/claude-max-final@f6f4f96`.
The A set (release candidate `5479e87`) is one folder up and is unchanged.

**What the B texts add to the A texts:**

- the NASA/JPL Horizons range and one-way signal time on the next-chapter strip during play
- the radiation estimate from NASA's MSL RAD cruise measurement (1.8 mSv per day), with its Sources entry

**Which set do I paste?** Look at the final post on #7:

| The build that ships | Use |
|---|---|
| A build = release candidate `5479e87` | the files one folder up: `../form-fields.md`, `../deck/…pdf`, `../final-project-folder/` |
| B build | the files in this folder: `form-fields.md`, `OUT-OF-REACH_SpaceApps2026_deck.pdf`, `final-project-folder/` |

If the final B build has more or fewer extras than listed above, this folder must be rebuilt first
(`python make_b_build.py --sha <final> <flags>`; the checks are at the end of `form-fields.md`).

Files here: `form-fields.md` (8 paste blocks), `OUT-OF-REACH_SpaceApps2026_deck.pdf` + `.pptx` (7 slides),
`final-project-folder/` (`README.pdf`, `README.txt`, `CREDITS.txt`), and the B versions of
`credits-and-licenses.md`, `public-README-draft.md` and `playthrough-checklist.md`.
The submission checklist, the AI disclosure and `UPLOAD-LIST.txt` are the same for both builds (one folder up).
