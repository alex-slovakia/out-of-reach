# Submission checklist — links, owners, timeline

**DRAFT for Alexander (Team Owner) and Alex/Apoliak.** Claude does not submit, publish, upload or change repository visibility; every action below is a human action unless it names a lane.

## Deadline and tonight's schedule

Times CET (UTC+1).

| CET | UTC | Gate |
|---|---|---|
| 22:30 | 21:30 | Submission pack ready against `feature/claude-max-final@5ea2c23` (this folder) |
| **23:00** | **22:00** | **Final build** from CLAUDE-MAX's final integration (final SHA, Windows ZIP, checksum, smoke). **Apoliak decides the demo: slides or video** |
| 23:00–23:15 | 22:00–22:15 | Last pass of the texts on the final SHA (`submit-pack` lane: switch sentences, test counts). Game done by 23:15 |
| **≈ 23:30** | **≈ 22:30** | **Team Owner submits** |
| 23:59 | 22:59 | Hard deadline — Universal Event = **Team Owner's** local time (Alexander Kozovy, Slovakia) |

Safety net (recommended): as soon as the public final-project link and the public deck PDF link exist, the Team Owner can submit a complete version, then edit and resubmit when the final build or a video is ready. The guide allows unlimited resubmissions until the deadline and judges only the last saved version.

Anything saved after 23:59 CET is not judged. The participant certificate appears in each member's profile only after the project is submitted.

## Eligibility facts already checked (15 Nov 2026, official pages)

- [x] Team **Polish Alex Goats**, Universal Event, challenge **Space Mission Design Game**; Members tab lists **2 members** (Universal Event teams need at least 2): Alexander Kozovy (Team Owner) and Alex (Apoliak). No new members can be added after submission closes.
- [x] Project tab exists ("Check back later to see this project take flight") — nothing submitted yet.
- [ ] The "About the team" text on the public team page says one member is 17. **No image, video or audio of anyone under 18 may appear in the demo or the final project** — keep every team member's face and voice out of the demo, the deck and the published files. (The game itself has only synthetic voices and NASA's historical launch commentary.)

## The two links the form requires (no password, permission or registration)

### 1. Link to Project Demo — Apoliak decides at 23:00 CET

| Variant | What | State | Notes |
|---|---|---|---|
| **A** | The 7-slide deck as **PDF**: [deck/OUT-OF-REACH_SpaceApps2026_deck.pdf](deck/OUT-OF-REACH_SpaceApps2026_deck.pdf) | **Ready now** | Put it on Google Drive ("Anyone with the link · Viewer") or in the public repo; 7 slides including the title; it names the team, both members and the challenge |
| B | A video of **30 seconds or less** | **Not approved, nothing uploaded** | Only on Apoliak's word. Gameplay only, nobody on camera, no voice-over by a team member, English captions if any speech; AI acknowledgement in the description ([ai-disclosure.md](ai-disclosure.md)) |

The form has one demo link. If a video is chosen, the deck goes into the final-project folder.

### 2. Link to Final Project

Do **not** flip `alex-slovakia/out-of-reach` to public: that would publish issue #7 and the other issues, third-party frames without a licence (`docs/references/tpbXZyRxWNY`) and unwatermarked AI concept boards (`docs/art-direction/…`) — see [credits-and-licenses.md](credits-and-licenses.md) §B10.

| Option | What | Owner | Effort |
|---|---|---|---|
| **B (fastest, recommended tonight)** | A **Google Drive folder**, "Anyone with the link · Viewer", with: the final Windows build ZIP, `README.pdf` or `README.txt` (from [public-README-draft.md](public-README-draft.md)), the deck PDF, `CREDITS.txt` (block A of the credits file) | Team Owner | ~15 min + upload time (the ZIP is about 530 MB) |
| A | A **new public GitHub repository** (for example `out-of-reach-space-apps-2026`) with: `README.md` from the public README draft plus 4–6 real captures; `CREDITS.md`; the game source (`Assets/_OutOfReach/**`, `Packages/`, `ProjectSettings/`) **without** `docs/references/`, `docs/art-direction/`, `.local/`; a **Release** with the Windows build ZIP (GitHub allows up to 2 GB per file) | Team Owner (owns the GitHub account) | ~30–45 min |

Build ZIP: use the one named in the final post on #7, check its SHA-256 against that post, and test it once on a PC: unzip, run `OutOfReach.exe` (no admin rights). Windows SmartScreen may warn about an unsigned game: the README says "More info → Run anyway". Leave out any `*_BurstDebugInformation_DoNotShip` folder.

## Who does what

| # | Item | Owner | When (CET) | Status |
|---|---|---|---|---|
| 1 | Final SHA + Windows ZIP + checksum + smoke | CLAUDE-MAX (final integration, #7 6901207238) | 23:00 | open |
| 1a | VO23 spoken day-neutral cut: approve or clear the binding | **Alex** | before the final build | open (texts are true either way) |
| 2 | Demo decision: slides (A) or video (B) | **Apoliak** | 23:00 | open |
| 3 | Last diff pass of the texts on the final SHA (checks A1–A3, switches S1/S5/S7/S8/S9, deck rebuild if needed) | `submit-pack` lane | 23:00–23:15 | waits for item 1 |
| 4 | Claim check of the final build ([playthrough-checklist.md](playthrough-checklist.md)): QA result from `qa-e2e` plus one human look | `qa-e2e` + Apoliak or Alexander | by 23:15 | open |
| 5 | Public final-project location (option B or A): build ZIP, README, credits, deck PDF | Team Owner | by 23:25 | open |
| 6 | Public demo link (deck PDF, or the video if Apoliak approved one) | Team Owner | by 23:25 | open |
| 7 | Paste the 8 fields from [form-fields.md](form-fields.md), tick both boxes, **Submit for Judging**, check the success banner | **Team Owner only** | ≈ 23:30 | open |
| 8 | Open both links in a private window and on a phone, without login | both | right after submitting | open |
| 9 | Last edits/resubmit if something changes | Team Owner | before 23:59 | — |

Already done in the game (no action): the in-game Sources page names the synthetic voices, the code-synthesized score and effects, the NASA launch commentary, the NASA SVS sky, EPIC and the GIBS image (`cf2224d`).

## What to copy where (Team Owner)

| Form field | Copy from |
|---|---|
| Project Name | [form-fields.md](form-fields.md) §1 |
| High-Level Project Summary | §2 (full block; the short one only if the form refuses it) |
| Link to Project Demo | your public link — §3 says which variant |
| Link to Final Project | your public link — §4 |
| Detailed Project Description | §5 (full block; short version only if refused), after the switch sentences are applied |
| Use of SI | §6 |
| NASA Data | §7 |
| Space Agency Partner Data and Other References | §8 |
| README of the public folder/repo | [public-README-draft.md](public-README-draft.md) + block A of [credits-and-licenses.md](credits-and-licenses.md) |

## Final check before clicking "Submit for Judging"

- [ ] All 8 fields filled, in English, no `[PLACEHOLDER]` or Markdown left.
- [ ] Demo: deck ≤ 7 slides incl. title, or video ≤ 30 s (file properties); it names the team, both members and the challenge.
- [ ] Both links open in a private browser window with no login, permission request or registration.
- [ ] NASA Data field lists at least one NASA dataset/resource (we list Horizons, BVAD, EPIC, GIBS, NASA audio and more).
- [ ] References field lists every third-party asset (paste from [form-fields.md](form-fields.md) §8).
- [ ] SI: the README has the AI section; no concept board is visible anywhere public; the in-game Sources page names the synthetic voices (it does at `cf2224d`; re-checked on the final SHA).
- [ ] No NASA logo, flag or insignia in the deck, the video (if any), a thumbnail, the README or the game.
- [ ] No image, video or voice of a person under 18.
- [ ] Every claim in the description is visible in the submitted build (*Validity*).
- [ ] Tick **"I have read and understand the project submission requirements … and I agree to the Participant Terms and Conditions and Privacy Policy"** and **"I confirm that the submitted project represents my team's original work and that all external resources … are listed in the References field …"**.
