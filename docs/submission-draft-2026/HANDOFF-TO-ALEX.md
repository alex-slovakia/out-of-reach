# Hand-off to Alex (Team Owner): the submission pack is yours from here

15 Nov 2026, 22:50 CET. The submit-pack lane on Apoliak's side is **closed**. No further edits come from us, and nothing in this pack needs us any more. Everything below can be done by you alone.

## Do this, in order

1. **Pick the build you submit and open its set.**

   | Build | Set (texts, deck PDF, upload files) |
   |---|---|
   | **A** = `feature/claude-max-final@5479e87` (ZIP verified) | this folder: [form-fields.md](form-fields.md), [deck PDF](deck/OUT-OF-REACH_SpaceApps2026_deck.pdf), [final-project-folder/](final-project-folder/) |
   | **B-code** = `feature/claude-max-final@f6f4f96` | [B-build/](B-build/) |
   | **B-visual** = `feature/claude-max-final-b` (checked on `9de3195`) | [B-visual-build/](B-visual-build/) |

2. **Public final-project folder** (Google Drive, "Anyone with the link · Viewer"): the build ZIP, plus `README.pdf`, `README.txt`, `CREDITS.txt` from the set's `final-project-folder/`, plus the set's deck PDF. Exact list: [final-project-folder/UPLOAD-LIST.txt](final-project-folder/UPLOAD-LIST.txt). Its link is the form's **Link to Final Project**. Do not make the working repository public.
3. **Link to Project Demo:** the video, if Apoliak's side hands you an approved cut by 23:35 CET (#7 6901852158); you upload it to a public link. Otherwise the link to the set's deck PDF (7 slides). The form field takes one link.
4. **Paste the 8 fields** from the set's `form-fields.md`. Each field is one plain-text block. Tick both boxes and press **Submit for Judging** (planned 23:40 CET; hard limit 23:59 CET; the saved submission can be edited until then).
5. **Open both links in a private browser window.** They must open without a login.

## If the build you submit is not exactly one of the three

- Each set folder has a `README.md` with a short list: "this extra did not ship → delete these sentences". No script is needed.
- The **description and NASA Data of the A set are true for every build**, because B only adds things.
- For the **B-visual** build two fields must come from the B-visual set, not from A: **Use of SI** (the player's radio lines use the extra Gemini voice Iapetus) and **References** (the launch effects use the Unity Particle Pack by Unity Technologies, a third-party Asset Store package that must be listed and must never be published as source).
- Test counts in the texts are floors ("more than 460 EditMode and 150 PlayMode"); they stay true for A (499 / 155) and B (508 / 168).

## Open in the game itself (your side)

- **B-visual only:** the in-game Sources line still says "Voices Orus and Kore." The four player takes use Iapetus. One string in `UI/Report/Runtime/FlightRecorderPresenter.cs`: "Voices Orus, Kore and Iapetus."
- **B-visual only:** QA reported a visual defect in the first minute (#7 6901871581). Whether B-visual ships is your call; A is verified end to end.

## What was checked, so you do not have to

- Every paste block against the saved Bootstrap scene and prefabs of its build (chapter loop on, NASA launch countdown, tether rope, pouch meal, berth sleep, handheld radio, routine montage, rescue docking under the NASA SVS stars, Slovakia inset from NASA GIBS).
- Credits: nothing third-party is missing from References. In the game: Scott Buckley (CC BY 4.0), NASA audio and imagery, Poly Haven, ambientCG, MakeHuman (CC0), fonts (OFL). B-visual adds the Unity Particle Pack.
- AI disclosure: all dialogue is Google Gemini text-to-speech, acknowledged on the in-game Sources page and in the manifests; score cues and effects are code-synthesized. No AI image is in the game, the deck or the upload files.
- Deck PDFs: 7 pages, correct in browsers (the font fix of `941da59`). Do not use older PDF copies.
- No video is linked anywhere in the pack. No NASA logo. Nobody under 18 appears.
