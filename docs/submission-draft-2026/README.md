# NASA Space Apps 2026 — OUT OF REACH submission drafts

**Status: DRAFT ONLY. Nothing here has been submitted, published, uploaded or shared.** These files prepare text and checks for the team owner. Every publication, upload, visibility or submission step is a **decision for the team owner** and is not performed or recommended by these drafts.

- Source snapshot: integration candidate `915a194` (14 Nov 2026, 20:37 +0100) [git log].
- Updated for `codex/i01-integration` @ `8bb53aa`: C05-BOARDING-01 (sealed loading deadline, launch from the transport seat) is integrated as `8995f64` [git log]; LANG-01 (English/Spanish/French text) exists only on the separate branch `feature/localization` and ships only if root wires the language picker before the 04:00 CET freeze [#7 comment 6883820270].
- Team: Polish Alex Goats [README.md]. Challenge: [Space Mission Design Game](https://www.spaceappschallenge.org/2026/challenges/space-mission-design-game/) [README.md]. Event: [Universal Event](https://www.spaceappschallenge.org/2026/local-events/universal-event/) [README.md]. Team page: https://www.spaceappschallenge.org/2026/find-a-team/polish-alex-goats/ [APOLIAK-CLAUDE-MASTER-PROMPT.md §1].
- Square-bracket tags such as [NASA-DATA.md] name the committed file that supports a claim. "TO CONFIRM" marks anything the committed files do not establish.

## Files

| File | Purpose | Audience |
|---|---|---|
| [form-fields.md](form-fields.md) | Paste-ready drafts for every submission form field, each followed by its sources | Team owner filling the form |
| [si-and-credits-inventory.md](si-and-credits-inventory.md) | Asset groups with origin, licence, SI involvement, required SI labelling, gaps and actions | Team; source for the SI and references fields |
| [demo-storyboard.md](demo-storyboard.md) | 30-second video plan (shot list, on-screen text, English SRT) and a 7-slide alternative | Whoever records/edits the demo |
| [playthrough-checklist.md](playthrough-checklist.md) | Manual Windows-build test script with pass/fail boxes, loss paths, performance capture and known issues | Human tester on a Windows PC |

## Reported rules — direct source check pending

Source: **Official Project Submission Guide**, https://www.spaceappschallenge.org/resources/project-submission-guide/ , dated **13 Nov 2026**, read **14 Nov 2026**. The rules below were relayed to this draft by the team coordinator; the guide was not re-fetched while writing (no network was used).

Integrator verification on 14 November: the direct official-page fetch returned HTTP 502. The claims below remain coordinator-reported, not independently confirmed by this draft. Recheck the live official guide before using its deadline, eligibility, length, labelling or form requirements for submission.

1. **Deadline (Universal Event):** Sunday **15 Nov 2026, 23:59 in the Team Owner's local time of residence**. The team owner clicks **"Submit for Judging"** and ticks **"Terms and Conditions"** and **"Confirm All Work is Original"**. Edits and resubmissions are allowed until the deadline.
2. **Form fields:** Project Name; High-Level Project Summary; Link to Project Demo; Link to Final Project; Detailed Project Description (what it does, how it works, benefits, what you hope to achieve, tools/coding languages/hardware/software); Use of Super Intelligence (SI) (data collection only, not shown to judges; judges do see SI indicators in the project); NASA Data (at least one NASA data/resource for Global Award eligibility); Space Agency Partner Data and Other References (all data, resources and tools including code, text and images, even if open source; permission for copyrighted material).
3. **Language:** everything in English.
4. **Demo:** a video of **30 seconds or less** (another spoken language only with English subtitles) **or** at most **7 slides** in English including the title/intro. Exceeding these limits makes the project ineligible for Global Judging.
5. **Links:** demo and final project must be on a **publicly accessible link with no login, permission or registration** (for example YouTube, Google Drive, GitHub).
6. **SI rules:** SI-generated images and videos carry a **visible watermark** indicating SI generation; SI-generated audio, code and data **acknowledge SI generation in descriptive text and metadata**; generated content may not use or modify **NASA branding**; no images, video or audio of **persons under 18**.
7. **Suggested 30 s video structure:** [0:00] who you are and team name; [0:02] which challenge and why; [0:05] project title and tagline; [0:10] how it works, the prototype, how NASA data made it possible; [0:20–0:30] the future.
8. **Judging criteria:** Impact, Creativity, Validity, Relevance, Presentation.

## Assumptions / team decisions

These are not rules from the guide. Each needs the team owner's decision or confirmation.

| Topic | What the committed files say | Decision / confirmation needed |
|---|---|---|
| Deadline conversion | Repository docs speak of the "team creator's local timezone"; if that is Europe/Bratislava the deadline is 22:59 UTC; reserve at least two hours for packaging and submission [README.md; TASKS.md]. The guide says **Team Owner's local time of residence**. | TO CONFIRM: who the Team Owner is and their timezone of residence. |
| Final-project link | The repository is **PRIVATE** [APOLIAK-CLAUDE-MASTER-PROMPT.md §1; TASKS.md F00]. The guide requires a public, no-login link. | **Team-owner decision.** These drafts do not propose a hosting route, visibility change or upload. |
| Publication timing | A 14 Nov instruction says "do not publish or submit to organizers yet; internal private collaboration continues" [TASKS.md R01]. | **Team-owner decision** when (and whether) to submit. |
| Demo format | Video (≤30 s) or slides (≤7) [guide]. Both are drafted in [demo-storyboard.md](demo-storyboard.md). | Team choice. |
| Build to show and test | Latest **recorded** Windows build is I05 at `3032686`; it does **not** contain the post-contact survival chapter [docs/verification/i05-3032686/README.md]. The saved Bootstrap enables the chapter from `85b0994` on (`survivalExtension: 1`) [docs/verification/i06-habitat-contact-native/README.md; Assets/_OutOfReach/Scenes/Bootstrap.unity]. No Windows build of `915a194` is recorded. | TO CONFIRM: which build is demonstrated and tested; the integrator records its SHA. |
| Development build | The committed build entry point uses `BuildOptions.Development` [Assets/_OutOfReach/Scripts/Composition/Editor/I01BootstrapBuilder.cs]; TESTING.md says to test the intended submission build separately [TESTING.md]. | TO CONFIRM whether a non-development build is produced for recording/judging. |
| SI interpretation | Many models, textures, music, SFX and the star sky are outputs of scripts written by AI coding agents (see inventory). | TO CONFIRM: whether the team labels these procedural outputs as SI-generated (the drafts take the conservative route). |
| Member names | Committed files name the team; member identities appear only as roles/handles [README.md; APOLIAK-CLAUDE-MASTER-PROMPT.md §1]. | Fill [MEMBER 1], [MEMBER 2] as the team prefers. |

## Human checklist (team owner and team)

All items are actions or decisions for humans. Nothing here was done by these drafts.

- [ ] Confirm the Team Owner and their timezone of residence; write the exact local deadline (15 Nov 2026 23:59 local) and an internal cutoff at least two hours earlier [guide; README.md; TASKS.md].
- [ ] Decide which build is the submission candidate and record its commit SHA and build type [TESTING.md "Release evidence"].
- [ ] Run [playthrough-checklist.md](playthrough-checklist.md) on a named Windows PC; correct any field text in [form-fields.md](form-fields.md) that the playthrough does not confirm (especially the survival chapter and the RECOVERED ending).
- [ ] **Team-owner decision:** the publicly accessible, no-login location of the final project (the repository is private).
- [ ] Record the demo video (≤30 s, English or English subtitles) **or** prepare ≤7 English slides, following [demo-storyboard.md](demo-storyboard.md); apply the SI watermark/acknowledgement decisions from [si-and-credits-inventory.md](si-and-credits-inventory.md).
- [ ] **Team-owner decision:** the publicly accessible, no-login location of the demo.
- [ ] Resolve or consciously accept the gaps listed at the end of [si-and-credits-inventory.md](si-and-credits-inventory.md) (SI metadata on audio, stale provenance files, in-game credits, third-party reference frames).
- [ ] Fill every field on the team page form from [form-fields.md](form-fields.md); replace all `[PLACEHOLDER]` and `TO CONFIRM` items.
- [ ] Team owner ticks **"Terms and Conditions"** and **"Confirm All Work is Original"** only after reviewing the SI and third-party disclosures.
- [ ] Team owner clicks **"Submit for Judging"** before 15 Nov 2026 23:59 (Team Owner's local time); edits/resubmits remain possible until then [guide].
- [ ] After any submission, open both links in a private browser window without logging in to confirm they are reachable [guide: no login/permission/registration].

## Open TO CONFIRM items (summary)

Detailed per file. Main ones: Team Owner timezone; member names; both links; build SHA/type actually tested; exact Codex model per session; human vs AI authorship split; dialogue script author; generator of the `clean-v3` voice takes; embedded audio metadata; NASA insignia in concept boards; licences of Unity template/package content and of the project itself; permission for third-party reference frames; measured performance.
