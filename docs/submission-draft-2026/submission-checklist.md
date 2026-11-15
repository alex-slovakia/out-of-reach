# Submission checklist — links, owners, timeline

**DRAFT for Alexander (Team Owner) and Alex/Apoliak.** Claude does not submit, publish, upload or change repository visibility; every action below is a human action unless it names a Claude lane.

## Deadline and today's schedule

Binding schedule for the Claude lanes: #7 6895582351 (the lead). Times CET (UTC+1).

| CET | UTC | Gate |
|---|---|---|
| 18:30 | 17:30 | Feature freeze: the integrator merges every lane that is DONE |
| 19:00 | 18:00 | Playable combined SHA, QA full run 1, Windows x64 build 1 |
| 21:00 | 20:00 | **Code freeze**: final combined SHA, final Windows build, final QA run |
| 21:00–22:15 | 20:00–21:15 | Final demo video capture and cut (`demo-video` session); submission texts and slides updated to the final features (`submit-pack-02`) |
| **22:30** | **21:30** | **Submission package ready**: video, slides, texts, credits, public links |
| **by 23:30** | **by 22:30** | **Team Owner submits** (30 min buffer) |
| 23:59 | 22:59 | Hard deadline — Universal Event = **Team Owner's** local time (Alexander Kozovy, Slovakia) |

Safety net (recommended): once the public final-project link and the deck PDF exist, the Team Owner can already submit a complete version (deck as the demo), then edit and resubmit with the video before 23:30. The guide allows unlimited resubmissions until the deadline and judges only the last saved version.

Anything saved after 23:59 CET is not judged. The participant certificate appears in each member's profile only after the project is submitted.

## Eligibility facts already checked (15 Nov 2026, official pages)

- [x] Team **Polish Alex Goats**, Universal Event, challenge **Space Mission Design Game**; Members tab lists **2 members** (Universal Event teams need at least 2): Alexander Kozovy (Team Owner) and Alex Poliak. No new members can be added after submission closes.
- [x] Project tab exists ("Check back later to see this project take flight") — nothing submitted yet.
- [ ] The "About the team" text on the public team page says one member is 17. **No image, video or audio of anyone under 18 may appear in the demo or the final project** — keep every team member's face and voice out of the video, the deck and the published files.

## The two links the form requires (no password, permission or registration)

### 1. Link to Project Demo

| Option | What | Owner | Notes |
|---|---|---|---|
| **A (recommended)** | 30-second video on **YouTube** (Public or Unlisted) | Capture + cut: `demo-video` session (automatic, from the frozen build); upload: Team Owner | Script, shot list, captions, description, metadata: [demo-video-30s.md](demo-video-30s.md), [demo-video-30s.en.srt](demo-video-30s.en.srt) |
| B (fallback) | The 7-slide deck as **PDF** | Team Owner | [deck/OUT-OF-REACH_SpaceApps2026_deck.pdf](deck/OUT-OF-REACH_SpaceApps2026_deck.pdf) on Google Drive ("Anyone with the link · Viewer") or in the public repo |

The form has one demo link; if both exist, use the video and put the deck in the final-project folder.

### 2. Link to Final Project

Do **not** flip `alex-slovakia/out-of-reach` to public: that would publish issue #7 and the other issues, third-party frames without a licence (`docs/references/tpbXZyRxWNY`) and unwatermarked AI concept boards (`docs/art-direction/…`) — see [credits-and-licenses.md](credits-and-licenses.md) §B10.

| Option | What | Owner | Effort |
|---|---|---|---|
| **A (recommended)** | A **new public GitHub repository** (for example `out-of-reach-space-apps-2026`) with: `README.md` from [public-README-draft.md](public-README-draft.md) plus 4–6 real captures; `CREDITS.md` (block A of the credits file); the game source (`Assets/_OutOfReach/**`, `Packages/`, `ProjectSettings/`) **without** `docs/references/`, `docs/art-direction/`, `.local/`; a **Release** with the Windows build zip (GitHub allows up to 2 GB per file) | Team Owner (owns the GitHub account) | ~30–45 min |
| B (fastest) | A **Google Drive folder**, "Anyone with the link · Viewer", with: the build zip, `README.pdf` (from the public README draft), the deck PDF, `CREDITS.txt` | Team Owner | ~15 min + upload time |

Build zip rules: from the release build folder only; leave out `*_BurstDebugInformation_DoNotShip`; name it `OUT-OF-REACH_Windows_x64.zip`; test it on another PC: unzip, run `OutOfReach.exe` (no admin rights). Windows SmartScreen may warn about an unsigned game: say in the README "More info → Run anyway".

Optional: the free `outofreach.earth` domain (if its registration finished) can point to the public repo/page; the form still needs the direct link.

## Who does what

| # | Item | Owner | Due (UTC) | Status |
|---|---|---|---|---|
| 1 | Final integrated build SHA + Windows build | Claude integration assembler (`claude/integration`), root acceptance | 21:00 CET | in progress (#7 6895321605) |
| 2 | In-game Sources: add the AI-voice and AI-synth lines and the NASA SVS sky credit ([credits-and-licenses.md](credits-and-licenses.md) §C gaps 1–2) | Claude integration assembler | before the 21:00 CET code freeze | open |
| 3 | Countdown audio provenance next to the clip; music README/manifest fixed (mx01 = Cobalt); Titan excerpt text on the Sources page | `audio-opening` lane | with its DONE | open |
| 4 | Claim check of the final build ([playthrough-checklist.md](playthrough-checklist.md)): QA results from `qa-e2e` plus one human look at anything QA cannot show | `qa-e2e` + Apoliak or Alexander | 21:15 CET | open |
| 5 | Final video ≤ 30 s, captions, metadata (nobody on camera, no voice-over) | `demo-video` session | 22:00 CET | in progress (#7 6895463698) |
| 6 | Texts, slides and credits updated to the final features (slide-3 launch picture swapped for a final-build frame) | `submit-pack-02` | 22:15 CET | after the code freeze |
| 7 | Upload the video; set Public/Unlisted; paste the description; add the SRT | Team Owner | 22:30 CET | open |
| 8 | Create the public final-project location (option A or B), README, credits, build zip | Team Owner | 22:30 CET | open |
| 9 | Fallback lines in [form-fields.md](form-fields.md) chosen to match the final build (chapter loop, tether, countdown, meal) | `submit-pack-02`, confirmed by Apoliak | 22:15 CET | after the code freeze |
| 10 | Paste the 8 fields, tick both boxes, **Submit for Judging**, check the success banner | **Team Owner only** | by 23:30 CET | open |
| 11 | Open both links in a private window and on a phone, without login | both | right after submitting | open |
| 12 | Last edits/resubmit if something changes | Team Owner | before 22:59 | — |

## Final check before clicking "Submit for Judging"

- [ ] All 8 fields filled, in English, no `[PLACEHOLDER]` or Markdown left.
- [ ] Demo: video ≤ 30 s (file properties), or deck ≤ 7 slides incl. title; it names the team, both members and the challenge.
- [ ] Both links open in a private browser window with no login, permission request or registration.
- [ ] NASA Data field lists at least one NASA dataset/resource (we list Horizons, BVAD, EPIC and more).
- [ ] References field lists every third-party asset (paste from [form-fields.md](form-fields.md) §8; add the sound-recordings block if the countdown ships).
- [ ] SI: the README has the AI section; the video has its label, description and metadata; no concept board is visible anywhere public; the in-game Sources page names the synthetic voices (item 2).
- [ ] No NASA logo, flag or insignia in the video, deck, thumbnail, README or game.
- [ ] No image, video or voice of a person under 18.
- [ ] Every claim in the description is visible in the submitted build (*Validity*).
- [ ] Tick **"I have read and understand the project submission requirements … and I agree to the Participant Terms and Conditions and Privacy Policy"** and **"I confirm that the submitted project represents my team's original work and that all external resources … are listed in the References field …"**.
