# NASA Space Apps 2026 — OUT OF REACH submission pack

**DRAFTS ONLY. Nothing here was submitted, published, uploaded or shared.** The Team Owner submits. SUBMIT-PACK-02 (15 Nov 2026) supersedes SUBMIT-PACK-01 (`bb4b883`, written for the marine-prologue build).

- **Team** Polish Alex Goats — Alexander Kozovy (Team Owner), Alex (Apoliak) · **Event** Universal Event · **Challenge** [Space Mission Design Game](https://www.spaceappschallenge.org/2026/challenges/space-mission-design-game/) · **Team page** https://www.spaceappschallenge.org/2026/find-a-team/polish-alex-goats/
- **Deadline:** Sun 15 Nov 2026, **23:59 Bratislava = 22:59 UTC** ("Submit for Judging" by the Team Owner). Today's schedule (#7 6895582351): code freeze 21:00 CET, package ready **22:30 CET**, Team Owner submits **by 23:30 CET**; resubmits are allowed until the deadline.
- **Written for:** `codex/i01-integration` @ `dc9ff8b` plus the Claude lanes running today (#7 6895044220); credits re-swept on the first `claude/integration` SHA `30055e5` (movement, Core chapter loop, tower, body, WORK grip). Fallback lines cover features that may miss the final build.

## Start here (in this order)

| Step | File | For whom |
|---|---|---|
| 1 | [submission-checklist.md](submission-checklist.md) — deadline timeline, the two required public links, who does what, final checks | Alexander, Alex |
| 2 | [playthrough-checklist.md](playthrough-checklist.md) — one play of the final build to confirm every claim (picks the fallbacks) | whoever plays the build |
| 3 | [form-fields.md](form-fields.md) — all 8 form fields, paste-ready, with fallbacks and counts | Team Owner |
| 4 | [demo-video-30s.md](demo-video-30s.md) + [demo-video-30s.en.srt](demo-video-30s.en.srt) — 29.5 s script, exact shot list, captions, description, metadata | video editor |
| 5 | [deck/OUT-OF-REACH_SpaceApps2026_deck.pdf](deck/OUT-OF-REACH_SpaceApps2026_deck.pdf) (+ `.pptx`, `build_deck.py`) — 7 slides, real in-game captures only | demo fallback / final-project folder |
| 6 | [credits-and-licenses.md](credits-and-licenses.md) — every third-party asset, licence, required attribution, what must stay private | Team Owner, README |
| 7 | [ai-disclosure.md](ai-disclosure.md) — the SI rules, our inventory, the labels in place and missing | Team Owner, README |
| 8 | [public-README-draft.md](public-README-draft.md) — the project page for the public final-project link | Team Owner |

## After the 21:00 CET code freeze — finalize in this order (≈30 min)

1. **Final SHA.** Read the integrator's final `claude/integration` SHA and the `qa-e2e` final run on #7.
2. **Chapter loop on or off?** In the saved Bootstrap at that SHA: `git show <SHA>:Assets/_OutOfReach/Scenes/Bootstrap.unity | grep -n "chapterLoop"`. If `0`, paste the non-chapter fallbacks of [form-fields.md](form-fields.md) §2 and §5 and set the deck's slide 3 step 7 / slide 5 last lesson to the fallback wording (edit the PPTX directly, or `deck/build_deck.py` and re-export).
3. **Shipped or not:** radio countdown (`Audio/Radio/**`), hand-over-hand tether (`EvaTether*`), physical meal (`Presentation/Meal/**`), sleeping-bag straps (BODY-REST-HOOK). Keep or drop the matching lines: NASA Data item 9 and the sound-recordings block (countdown), "follow the tether" wording, "eat packaged rations", "secured sleeping bag".
4. **Credits re-sweep:** `git diff --name-status dc9ff8b <SHA> | grep -iE "attribution|provenance|notice|licen|readme|SOURCE.json"` and add any new third-party item to [credits-and-licenses.md](credits-and-licenses.md) block A and [form-fields.md](form-fields.md) §8. Confirm the in-game Sources page now names the synthetic voices (gap 1).
5. **Video:** take duration, file path and captions from the `demo-video` final post; check duration ≤ 30 s; the team uploads it and pastes the link into field 3.
6. **Deck picture:** optionally swap the slide-3 launch picture for a final-build frame (`CAPTURES["launch"]` in `deck/build_deck.py`), rebuild, export the PDF.

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

## Status at the time of writing

| Item | State |
|---|---|
| Form texts, credits, AI disclosure, video script, checklists, public README draft | **Ready** (drafts) |
| 7-slide deck PPTX + PDF | **Ready**; optional: swap the slide-3 launch picture for a final-build capture |
| Final integrated build, its SHA, the human claim check | Open (integration assembler, then a human) |
| In-game Sources lines for the AI voices and the NASA SVS sky | Open (integration assembler), see credits §C |
| Video capture and cut | In progress: `demo-video` session (#7 6895463698), final cut ~22:00 CET from the frozen build |
| Public final-project location and demo upload | Open (Team Owner) |
| Submit for Judging | Open (Team Owner only) |
