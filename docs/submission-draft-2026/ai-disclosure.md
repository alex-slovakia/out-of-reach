# AI ("SI") disclosure and labelling plan

**DRAFT.** Space Apps calls AI tools "Super Intelligence (SI)". This file lists exactly what the official rules require, where AI was used in OUT OF REACH, how each use is labelled, and what is still missing.

## The rules (Project Submission Guide, dated 13 Nov 2026, read 15 Nov 2026)

- SI tools (machine learning, chatbots, image generation, LLMs) **are permitted** to accelerate innovation. SI **should not be used to come up with the idea** of how to solve the challenge.
- Generated content may not use, contain or modify **NASA branding or design elements** (NASA logos, flags, mission identifiers).
- Indicate clearly where and how SI was used:
  - **Images and videos:** a **visible watermark** indicating SI generation.
  - **Audio:** acknowledge SI generation in **descriptive text and metadata**.
  - **Code and data:** acknowledge SI generation in **descriptive text and metadata**.
- The form's SI answer is for data collection and **is not shown to judges**; judges **do see** the SI indicators inside the project (watermarks, descriptive text, metadata) and judge originality, intent and execution with them.

## Where the idea came from (keep this true in every text)

The premise (a lone astronaut whose own mission design decides survival), the Day 1/3/7/18/31 story progression, the choice of a NASA-style land launch instead of a sea launch, the approved chapter loop, the music direction and every acceptance decision came from the two team members (see `PRODUCT.md` "The user's Day 1/3/7/18/31 story", `docs/APPROVED-CHAPTER-LOOP-2026-11-15.md` "Alex explicitly approved", and the playtest records in `docs/playtest-review/`). AI agents implemented, researched sources and documented under that direction.

## Inventory and labels

| # | Content | SI tool / model | Where | Required label | Label in place | Still to do (owner) |
|---|---|---|---|---|---|---|
| 1 | Gameplay, simulation, movement, body animation, chapter loop, tests; 15 Nov launch/audio/terrain/tether lanes | Claude Code — Claude Opus 5.5, Claude Fable 5.2, Claude Sonnet 5.5 | `Assets/_OutOfReach/Scripts/**`, `Tests/**` | Code: text + metadata | Metadata: 140+ commits carry `Co-Authored-By: Claude …` (119 Opus 5.5, 21 Fable 5.2, 6 Sonnet 5.5 across the remote branches) | Text: the "AI use" section below in the public README (team) |
| 2 | Presentation, UI, scenes, audio runtime, build and QA tooling, most docs | OpenAI Codex (recommended model GPT-6 Astra, High; exact model per session not recorded) | `Scripts/{Presentation,Composition}`, `UI/**`, `Audio/Runtime`, `tools/**`, `docs/**` | Code: text + metadata | No commit trailers | Text: README section below covers it (team); the exact model is optional |
| 3 | Original 3D models and procedural textures | Python/Blender and NumPy/Pillow scripts written by Claude Code and Codex agents; no generative image or 3D model | `Visuals/Models/**`, `Visuals/Props/**`, `Visuals/Character/**` | Code/data: text + metadata | Most packages' `ATTRIBUTION.md`/`README.md` already say "made by AI (Claude Opus 5.5)" or name the Codex agent | None required beyond the README statement |
| 4 | **All dialogue voices** | Google Gemini TTS (`gemini-3.8-flash-tts`, `gemini-3.8-flash-lite-tts`), voices Orus, Kore | `Audio/Voices/**` | **Audio: text + metadata** | Metadata: `clean-runtime-selection.json`, `Voices/Dramatic/voice-manifest.json`, `Voices/Departure/provenance.json` record source, model, voice and SHA-256 per file | **Text in the game:** add the line below to the in-game Sources page (integration assembler; `credits-and-licenses.md` gap 1). Text outside: README, video description (done in drafts) |
| 5 | Original score cues mx02–mx04, all synthesized SFX, the added percussion in the Titan opening mix | Generator scripts written by Codex agents | `Audio/Music/`, `Audio/SFX/`, `Audio/Original/**`, `Audio/Production/RestWakeV01/` | Audio: text + metadata | Manifests/provenance JSON exist; they say "original synthesis" but not "AI-written generator" | Text: README + Sources line below |
| 6 | Concept boards (art direction) | Built-in GPT image generation in Codex | `docs/art-direction/{review-01,review-04,review-05-marine,review-06-final-overview,approved-hud-02,aurora7-detail-reference-v01,aurora7-personality-v01,hand-push-reference-v01,prologue-landscape-target-v01,outcome-sources-20261115}` (+ check `launch-reference-v01`, `astronaut-reference-v01`) | **Images: visible watermark** | None | Keep them out of every public link (recommended), or stamp "AI-generated concept — not gameplay" on each (team) |
| 7 | NASA data processing | Fetch/verify/convert scripts written by the agents | `tools/nasa/`, `docs/data/**` | Data: text + metadata | Manifests record request, hashes, derivation | README statement |
| 8 | Submission texts, deck, video script | Claude Code (Opus 5.5) | `docs/submission-draft-2026/**` | Text | Deck metadata (`comments`) and slide 7 say so | Team edits; nothing else |

What is **not** AI-generated: the licensed music by Scott Buckley, the NASA images/data/audio, the CC0 assets from Poly Haven/ambientCG/MakeHuman, the fonts, and all gameplay captures (rendered by Unity, not generated).

## Paste-ready texts

### For the public project README ("AI use" section)

```text
AI use (NASA Space Apps "SI" disclosure)
- Idea, story and design decisions: the team (Alexander Kozovy, Alex (Apoliak)).
- Code: written with AI coding assistants under our direction, review and playtesting — Claude Code (Anthropic; Claude Opus 5.5, Claude Fable 5.2, Claude Sonnet 5.5) and OpenAI Codex. Claude-written commits carry a "Co-Authored-By: Claude" trailer.
- 3D models and textures: original, produced by Python scripts for Blender and NumPy/Pillow that these assistants wrote. No generative image or 3D model produced any in-game asset.
- Audio: all dialogue uses AI-generated synthetic voices (Google Gemini text-to-speech, voices Orus and Kore); per-file records are in Assets/_OutOfReach/Audio (voice manifests). Three original score cues, the synthesized sound effects and the percussion layer added to the opening "Titan" excerpt were generated by scripts written with AI assistants. The licensed music by Scott Buckley (CC BY 4.0) is human-composed.
- Images: AI-generated concept boards were used only for art direction; none is in the game.
- Data: NASA data are unmodified; the download and verification scripts were written with AI assistants.
```

### For the in-game Sources page (`MUSIC & ART`)

```text
Synthetic voices — Google Gemini text-to-speech (Orus, Kore). AI-generated audio.
Original score cues and sound effects — synthesized by scripts written with AI coding assistants.
```

### Video and deck

The video plan puts a persistent label on screen and the acknowledgement in the description and in the MP4 metadata ([demo-video-30s.md](demo-video-30s.md)). The deck uses only real in-game captures, says so in every footer, and carries the AI statement on slide 7 and in its file properties.

## Do not

- Do not show any concept board, generated poster or generated thumbnail without a visible "AI-generated" watermark.
- Do not put a NASA logo, flag or insignia into anything we generate (deck, thumbnail, video, game).
- Do not describe the voices as recordings of people, or the gameplay captures as AI-generated.
