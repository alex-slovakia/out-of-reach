# NASA Space Apps 2026 — OUT OF REACH submission pack

**DRAFTS ONLY. Nothing here was submitted, published, uploaded or shared.** The Team Owner submits. SUBMIT-PACK-02, final edition of 15 Nov 2026.

- **Team** Polish Alex Goats — Alexander Kozovy (Team Owner), Alex (Apoliak) · **Event** Universal Event · **Challenge** [Space Mission Design Game](https://www.spaceappschallenge.org/2026/challenges/space-mission-design-game/) · **Team page** https://www.spaceappschallenge.org/2026/find-a-team/polish-alex-goats/
- **Deadline:** Sun 15 Nov 2026, **23:59 Bratislava = 22:59 UTC** ("Submit for Judging" by the Team Owner). Tonight: the final build around **23:00 CET**, the game done by **23:15**, the Team Owner submits around **23:30**; resubmits are allowed until the deadline.
- **Checked against the release candidate:** `feature/claude-max-final` @ `5479e87` (#7 6901310758, 22:15 CET) = staging `022138d` (tower V03) + handheld radio + finale stars + routine montage + walk fixes; the day-14 announcement ships as radio subtitles. Full suites on the RC: 499/499 EditMode, 155/155 PlayMode (#7 6901385202). Alex decided that CLAUDE-MAX does the final integration (#7 6901207238). Every paste block describes only what is in the RC. **If the RC ships, the pack is final as it stands.** If a B build ships (extra NASA data lines, new models), one short diff pass follows (section below).

## Start here (in this order)

| Step | File | For whom |
|---|---|---|
| 1 | [submission-checklist.md](submission-checklist.md) — tonight's timeline, the two required public links, who does what, final checks | Alexander, Alex |
| 2 | [form-fields.md](form-fields.md) — all 8 form fields, paste-ready, plus the switch sentences for the final SHA | Team Owner |
| 3 | [deck/OUT-OF-REACH_SpaceApps2026_deck.pdf](deck/OUT-OF-REACH_SpaceApps2026_deck.pdf) (+ `.pptx`, `build_deck.py`, `export_pdf.ps1`) — 7 slides including the title, real in-game captures only | the demo link (variant A), final-project folder |
| 4 | [credits-and-licenses.md](credits-and-licenses.md) — every third-party asset, licence, required attribution, what must stay private | Team Owner, README |
| 5 | [ai-disclosure.md](ai-disclosure.md) — the SI rules, our inventory, the labels in place | Team Owner, README |
| 6 | [final-project-folder/](final-project-folder/) — **ready to upload**: `README.pdf` (two pages with four real captures), `README.txt`, `CREDITS.txt`, and `UPLOAD-LIST.txt` saying what else goes into the public folder. [public-README-draft.md](public-README-draft.md) is the same content as Markdown for a public repository | Team Owner |
| 7 | [playthrough-checklist.md](playthrough-checklist.md) — one play of the final build to confirm every claim | whoever plays the build |

**Demo link:** the form takes one link, a video of 30 s or less **or** at most 7 slides. **No video is approved, uploaded or linked.** Apoliak decides at 23:00 CET; [form-fields.md](form-fields.md) §3 has both variants. The deck is ready now. (`demo-video-30s.md` and its `.srt` are an older script, kept only in case Apoliak chooses a video; nothing in this pack depends on them.)

## What the texts say now (matched to the RC `5479e87`)

| Area | State |
|---|---|
| Chapter loop | `Bootstrap.unity` has `chapterLoop: 1` → the day 1–30 texts are the main texts |
| In the game, so in the texts | NASA launch radio countdown (Apollo 11 commentary), tether rope, physical meal from a ration pouch, sleep strapped into the berth, **handheld radio**, **routine montage with live station pictures**, the rescue ending with visible docking **under the NASA SVS star field**, VO24a/b voices, ending themes, Slovakia inset (NASA GIBS, MODIS Terra) on the terminal, practice mode |
| Day-14 announcement | In the RC it arrives on the handheld radio as subtitles (the VO23 voice binding is cleared until Alex approves the day-neutral cut). The texts say "announcement" and are true with or without the voice |
| Not in the tree, not in the texts | STS-131 launch sound and the Freesound recordings; any demo video |
| Earth call | May be made on day 1 or left until day 9; the texts give the day-1 figures (197,707,976 km, 31 min 59 s) and say the day-9 wait is still over 30 minutes (187,437,326 km → about 30 min 51 s) |
| NASA Data field | 10 items incl. NASA GIBS and NASA audio; the sky is NASA SVS Deep Star Maps 2020 (brighter exposure in the finale), GMAT is "development only" |
| SI field | names the Gemini voices incl. Mission Control and rescue-crew lines, the code-composed ending themes, the radio tones, and where each is acknowledged (Sources page text + manifests/provenance as metadata) |
| In-game Sources page | names the synthetic voices and the NASA SVS sky; unchanged between `cf2224d` and the RC → nothing to ask the integrator for |
| Deck | 7 slides; pictures: tower V03 liftoff, antenna repair, docking under the stars, routine montage ledger; "more than 460 / 150 tests" |
| Tests | "More than 460 EditMode and 150 PlayMode" (the RC has 499 and 155) → stays true unless the final counts drop |

## Last pass only if the final SHA is not the RC (≈10 min, `submit-pack` lane)

1. `git fetch origin`, then the three commands at the end of [form-fields.md](form-fields.md) (chapter loop, credits re-sweep `5479e87..<SHA>`, Sources page lines).
2. Checks A1–A3 (radio, stars, montage still in) and switches S1, S7, S8, S9, S5, S10 (spoken VO23, Horizons line during play, MSL RAD radiation estimate, DSN complex in view, day-14 console, new models) in [form-fields.md](form-fields.md). The exact sentences for S7 and S8 are already written there.
3. New NASA sources (MSL RAD, DSN): numbers and URLs only from the lanes' posts and provenance files; check that the in-game Sources page credits them too.
4. If a paste block changed: `python count_blocks.py form-fields.md` and update the counts above the block.
5. If the deck changed: `python deck/build_deck.py`, then `powershell -File deck\export_pdf.ps1 -Pptx <full path>.pptx -Pdf <full path>.pdf` (PowerPoint, no window); check that the PDF has 7 pages.

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

## Status at the time of writing (15 Nov, 22:15 CET)

| Item | State |
|---|---|
| Form texts, credits, AI disclosure, checklists, public README draft | **Ready**, matched to the release candidate `feature/claude-max-final@5479e87` |
| 7-slide deck PPTX + PDF | **Ready** |
| In-game Sources lines for the AI voices and the NASA SVS sky | **Done in the game** |
| Windows build of the RC (ZIP, SHA-256, smoke) | Open — CLAUDE-MAX, ETA about 22:30 CET |
| RC ships, or a B build with extras | Open — CLAUDE-MAX / Alex, by 23:00 CET |
| VO23 spoken day-neutral cut | Open — Alex's approval; the texts are true either way |
| Diff pass of this pack | Needed **only** if the final SHA differs from the RC |
| Demo: slides or video | Open — **Apoliak decides at 23:00 CET** |
| Public final-project location and public demo link | Open — Team Owner |
| Submit for Judging | Open — Team Owner only, around 23:30 CET |
