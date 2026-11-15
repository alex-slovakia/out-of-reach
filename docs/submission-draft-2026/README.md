# NASA Space Apps 2026 — OUT OF REACH submission pack

**DRAFTS ONLY. Nothing here was submitted, published, uploaded or shared.** The Team Owner submits. SUBMIT-PACK-02, final edition of 15 Nov 2026.

- **Team** Polish Alex Goats — Alexander Kozovy (Team Owner), Alex (Apoliak) · **Event** Universal Event · **Challenge** [Space Mission Design Game](https://www.spaceappschallenge.org/2026/challenges/space-mission-design-game/) · **Team page** https://www.spaceappschallenge.org/2026/find-a-team/polish-alex-goats/
- **Deadline:** Sun 15 Nov 2026, **23:59 Bratislava = 22:59 UTC** ("Submit for Judging" by the Team Owner). Tonight: Codex pushes the final build at **23:00 CET**, the game is done by **23:15**, the Team Owner submits around **23:30**; resubmits are allowed until the deadline.
- **Checked against the game that ships:** `codex/nasa-2330-staging` @ `cf2224d` (tested twin `8665183`: 470/470 EditMode, 151/151 PlayMode, Windows x64 ZIP and smoke PASS, #7 6900862397). Every paste block describes only what is in that tree. One last pass follows when Codex names the final SHA (section below).

## Start here (in this order)

| Step | File | For whom |
|---|---|---|
| 1 | [submission-checklist.md](submission-checklist.md) — tonight's timeline, the two required public links, who does what, final checks | Alexander, Alex |
| 2 | [form-fields.md](form-fields.md) — all 8 form fields, paste-ready, plus the switch sentences for the final SHA | Team Owner |
| 3 | [deck/OUT-OF-REACH_SpaceApps2026_deck.pdf](deck/OUT-OF-REACH_SpaceApps2026_deck.pdf) (+ `.pptx`, `build_deck.py`) — 7 slides including the title, real in-game captures only | the demo link (variant A), final-project folder |
| 4 | [credits-and-licenses.md](credits-and-licenses.md) — every third-party asset, licence, required attribution, what must stay private | Team Owner, README |
| 5 | [ai-disclosure.md](ai-disclosure.md) — the SI rules, our inventory, the labels in place | Team Owner, README |
| 6 | [public-README-draft.md](public-README-draft.md) — the project page for the public final-project link | Team Owner |
| 7 | [playthrough-checklist.md](playthrough-checklist.md) — one play of the final build to confirm every claim | whoever plays the build |

**Demo link:** the form takes one link, a video of 30 s or less **or** at most 7 slides. **No video is approved, uploaded or linked.** Apoliak decides at 23:00 CET; [form-fields.md](form-fields.md) §3 has both variants. The deck is ready now. (`demo-video-30s.md` and its `.srt` are an older script, kept only in case Apoliak chooses a video; nothing in this pack depends on them.)

## What changed in this edition (against `cf2224d`)

| Area | Change |
|---|---|
| Chapter loop | `Bootstrap.unity` has `chapterLoop: 1` → the day 1–30 texts are the main texts; the non-chapter fallback is kept only as a footnote |
| In the game now, so in the texts | NASA launch radio countdown (Apollo 11 commentary), tether rope, physical meal from a ration pouch, sleep strapped into the berth, the rescue ending with visible docking and the VO24a/b voices, ending themes, Slovakia inset (NASA GIBS, MODIS Terra) on the terminal, practice mode |
| Removed (not in the tree) | STS-131 launch sound and the Freesound recordings; the "sleeping bag" wording; every reference to a demo video as existing |
| Earth call | The call may be made on day 1 or left until day 9; the texts give the day-1 figures (197,707,976 km, 31 min 59 s) and say the day-9 wait is still over 30 minutes (187,437,326 km → about 30 min 51 s) |
| NASA Data field | 10 items: + NASA GIBS (item 4) and NASA audio (item 5); the sky is NASA SVS Deep Star Maps 2020, GMAT is "development only" |
| SI field | names the Gemini voices incl. the ending lines, the code-composed ending themes "Thirty-One Dawns" / "Last Carrier", and where each is acknowledged (Sources page text + manifests as metadata) |
| In-game Sources page | **already names** the synthetic voices and the NASA SVS sky at `cf2224d` → the two old gaps are closed, nothing to ask Codex for |
| Deck | still 7 slides; new pictures from the native QA run on `cf2224d` (launch, antenna repair, ration pouch) and the rescue docking; test counts 470 / 151; credits and AI line updated |
| Tests | 470 EditMode, 151 PlayMode (was "more than 370 / 100") |

## Last pass when Codex names the final SHA (≈10 min, `submit-pack` lane)

1. `git fetch origin`, then the three commands at the end of [form-fields.md](form-fields.md) (chapter loop, credits re-sweep `cf2224d..<SHA>`, Sources page lines).
2. Go through the **switch sentences** S1–S6 in [form-fields.md](form-fields.md): day-14 voice (VO23), handheld radio, finale stars, routine montage, day-14 console, tower V03 / new models. Add a sentence only if its check passes.
3. If VO23 ships: **listen to the day it names.** The approved recording says "mission day thirty-one", the game's rendezvous is day 30 (S1 tells what to change).
4. Test counts: take them from Codex's final post; update §5 of the form fields and `TESTS_EDITMODE, TESTS_PLAYMODE` in `deck/build_deck.py`, rebuild, export the PDF.
5. Optional: a final-build picture for deck slide 3 (`CAPTURES` in `deck/build_deck.py`).

## Official rules, verified on the official site on 15 Nov 2026

Sources: [Project Submission Guide](https://www.spaceappschallenge.org/resources/project-submission-guide/) and [Judging & Awards Guide](https://www.spaceappschallenge.org/resources/judging-awards-guide/) (both dated 13 Nov 2026), the [challenge page](https://www.spaceappschallenge.org/2026/challenges/space-mission-design-game/) and the [team page](https://www.spaceappschallenge.org/2026/find-a-team/polish-alex-goats/), read in a browser by Claude.

1. **Deadline:** Universal Event projects close Sun 15 Nov 11:59 p.m. in the local time of the **Team Owner's** area of residence. Anything saved after it is not judged. Only the last saved submission is reviewed.
2. **Fields:** Project Name; High-Level Project Summary; Link to Project Demo; Link to Final Project; Detailed Project Description; Use of Super Intelligence (SI); NASA Data; Space Agency Partner Data and Other References. All required fields must be filled; everything in **English**.
3. **Demo:** a video of **30 s or less** (another spoken language only with English subtitles) **or** a deck of **at most 7 slides including title and intro**. Over the limit = not eligible for Global Judging. Slides must show the team name, the members' names and the challenge, an image of the project, the user experience, and how NASA/space-agency data make it possible.
4. **Links:** demo and final project hosted externally (YouTube, Drive, GitHub…) with **no login, permission or registration**. The project page accepts JPG/PNG/GIF images up to 10 MB, but no videos or documents.
5. **SI:** permitted as an accelerator; not to come up with the idea. Generated content may not use or modify NASA branding (logos, flags, mission identifiers). **Images/videos: visible watermark. Audio, code, data: acknowledgement in descriptive text and metadata.** The SI field is not shown to judges; the indicators in the project are.
6. **Minors:** no images, video, audio or other likeness of persons **under 18** in demos or final projects. The public team page says one member is 17.
7. **Eligibility:** Universal Event teams need **at least 2** members on the Members tab (we have 2, no more can be added after the close); at least one NASA dataset/resource for Global Awards; two checkboxes (requirements + Terms and Privacy; original work with all external resources listed in References and no copyrighted material without permission).
8. **Judging:** Impact, Creativity, Validity, Relevance (incl. NASA data as a core component), Presentation (demo and full project), each 1–5.

## Status at the time of writing (15 Nov, 22:00 CET)

| Item | State |
|---|---|
| Form texts, credits, AI disclosure, checklists, public README draft | **Ready**, matched to `cf2224d` |
| 7-slide deck PPTX + PDF | **Ready** |
| In-game Sources lines for the AI voices and the NASA SVS sky | **Done in the game** (`cf2224d`) |
| Final SHA, final Windows build | Open — Codex, 23:00 CET |
| Last pass of this pack on the final SHA (switch sentences, test counts) | Open — `submit-pack` lane, right after the final SHA |
| Demo: slides or video | Open — **Apoliak decides at 23:00 CET** |
| Public final-project location and public demo link | Open — Team Owner |
| Submit for Judging | Open — Team Owner only, around 23:30 CET |
