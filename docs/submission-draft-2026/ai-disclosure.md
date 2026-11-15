# AI ("SI") disclosure and labelling plan

**DRAFT.** Space Apps calls AI tools "Super Intelligence (SI)". This file lists exactly what the official rules require, where AI was used in OUT OF REACH, how each use is labelled, and what is still missing.

**State at `feature/claude-max-final` @ `5ea2c23` (15 Nov 2026, 21:05 UTC; first checked at staging `cf2224d`): every required label is in place.** SI audio is acknowledged in text (in-game Sources page, README, References field) and in metadata (per-file manifests); no SI image or video is in the game, the deck or anything we publish, so no watermark is needed. Open only: the team must keep the concept boards out of every public link (row 6).

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
| 1 | Gameplay, simulation, movement, body animation, chapter loop, tests; 15 Nov lanes: tether rope, meal, berth sleep, rescue ending and its star field, handheld radio, routine montage, startup cards, terrain, launch towers; the final integration | Claude Code — Claude Opus 5.5, Claude Fable 5.2, Claude Sonnet 5.5 | `Assets/_OutOfReach/Scripts/**`, `Tests/**`, `UI/Startup/**` | Code: text + metadata | Metadata: Claude-written commits carry `Co-Authored-By: Claude …` trailers | Text: the "AI use" section below in the public README (team) |
| 2 | Presentation, UI, scenes, audio runtime, build and QA tooling, most docs | OpenAI Codex (recommended model GPT-6 Astra, High; exact model per session not recorded) | `Scripts/{Presentation,Composition}`, `UI/**`, `Audio/Runtime`, `tools/**`, `docs/**` | Code: text + metadata | No commit trailers | Text: README section below covers it (team); the exact model is optional |
| 3 | Original 3D models and procedural textures | Python/Blender and NumPy/Pillow scripts written by Claude Code and Codex agents; no generative image or 3D model | `Visuals/Models/**`, `Visuals/Props/**`, `Visuals/Character/**` | Code/data: text + metadata | Most packages' `ATTRIBUTION.md`/`README.md` already say "made by AI (Claude Opus 5.5)" or name the Codex agent | None required beyond the README statement |
| 4 | **All dialogue voices**: vo01–vo12, Dramatic 02/03/05, Departure cues, and Story02 **VO24a/VO24b** (rescue-crew radio lines of the ending; in the build) and **VO23** (day-14 Mission Control announcement: its **day-neutral cut** is bound to the handheld radio at `5ea2c23`, pending Alex's approval; the cut removes one sentence with ffmpeg and adds no generated audio) | Google Gemini TTS (`gemini-3.8-flash-tts`, `gemini-3.8-flash-lite-tts`), voices Orus, Kore; Story02 = `gemini-3.8-flash-lite-tts`, Orus. No voice cloning | `Audio/Voices/**`, `Scripts/Presentation/Radio/Voices/` | **Audio: text + metadata** | **Text in the game:** Sources → MUSIC & ART: "Synthetic voices — Google Gemini text-to-speech" / "Voices Orus and Kore. AI-generated audio. …" (`FlightRecorderPresenter.cs` 422–423). **Metadata:** `clean-runtime-selection.json`, `Voices/Dramatic/voice-manifest.json`, `Voices/Departure/provenance.json`, `Voices/Story02/approved-voices.json` and `Presentation/Radio/Voices/PROVENANCE.md` record model, voice, transcript, edits and SHA-256 per file | None. Text outside the game: README and References field (done in drafts) |
| 5 | Original score cues: mx02 Tension, mx03 Emergency, and the ending themes **"Thirty-One Dawns"** (rescue) and **"Last Carrier"** (loss); all synthesized SFX (UI, alarms, airlock, deck contacts, launch effects, rest/wake, docking clunks, radio tones, pencil scratch); the added percussion in the Titan opening mix | Composed and synthesized by scripts written by Codex agents (`build_music_v2.py`, `FinaleR31/compose_endings.py`, `generate_*.py`) and by Claude (`RescueEndingSfx.cs`, `RadioSynth.cs`, `PencilScratchSynth.cs`); procedural synthesis, no samples, no generative-audio service | `Audio/Music/`, `Audio/Music/FinaleR31/`, `Audio/SFX/`, `Audio/Original/**`, `Audio/Production/RestWakeV01/`, `UI/Startup/Runtime/`, `Scripts/Presentation/Rescue/` | Audio: text + metadata | **Text in the game:** the same Sources entry ends "Original score cues and sound effects synthesized by scripts written with AI coding assistants." **Metadata:** `Music/FinaleR31/provenance.json` ("Original composition and procedural synthesis by Codex"), `Original/**/audio-provenance.json`, `UI/Startup/PENCIL-SOUND-PROVENANCE.md` | None |
| 6 | Concept boards (art direction) | Built-in GPT image generation in Codex | `docs/art-direction/{review-01,review-04,review-05-marine,review-06-final-overview,approved-hud-02,aurora7-detail-reference-v01,aurora7-personality-v01,hand-push-reference-v01,prologue-landscape-target-v01,outcome-sources-20261115}` (+ check `launch-reference-v01`, `astronaut-reference-v01`) | **Images: visible watermark** | None | Keep them out of every public link (recommended), or stamp "AI-generated concept — not gameplay" on each (team) |
| 7 | NASA data processing | Fetch/verify/convert scripts written by the agents | `tools/nasa/`, `docs/data/**` | Data: text + metadata | Manifests record request, hashes, derivation | README statement |
| 8 | Submission texts and the slide deck | Claude Code (Opus 5.5, Fable 5.2) | `docs/submission-draft-2026/**` | Text | Deck metadata (`comments`) and slide 7 say so | Team edits; nothing else |
| 9 | Startup title cards (pencil-sketch look) | Vector drawings defined in code written by Claude (`UI/Startup/Runtime/VectorArt.cs`, `CalligraphicStroke.cs`); not produced by an image generator | `UI/Startup/**` | Code: text + metadata | README statement; commit trailers | None |

What is **not** AI-generated: the licensed music by Scott Buckley, the NASA images, data and the launch-countdown recording, the CC0 assets from Poly Haven/ambientCG/MakeHuman, the fonts, and all gameplay captures (rendered by Unity, not generated).

No generated content uses NASA branding: no logo, flag or insignia. The synthetic voices name only the fictional ship and its fictional crew, and the passage of the NASA recording that names the real mission is cut out.

## Paste-ready texts

### For the public project README ("AI use" section)

```text
AI use (NASA Space Apps "SI" disclosure)
- Idea, story and design decisions: the team (Alexander Kozovy, Alex (Apoliak)).
- Code: written with AI coding assistants under our direction, review and playtesting — Claude Code (Anthropic; Claude Opus 5.5, Claude Fable 5.2, Claude Sonnet 5.5) and OpenAI Codex. Claude-written commits carry a "Co-Authored-By: Claude" trailer.
- 3D models and textures: original, produced by Python scripts for Blender and NumPy/Pillow that these assistants wrote. No generative image or 3D model produced any in-game asset.
- Audio: all dialogue uses AI-generated synthetic voices (Google Gemini text-to-speech, voices Orus and Kore); per-file records are in Assets/_OutOfReach/Audio (voice manifests). The original score cues, including the ending themes "Thirty-One Dawns" and "Last Carrier", the synthesized sound effects and the percussion layer added to the opening "Titan" excerpt were composed and generated by scripts written with AI assistants. The licensed music by Scott Buckley (CC BY 4.0) is human-composed; the launch countdown is a real NASA recording.
- Images: AI-generated concept boards were used only for art direction; none is in the game.
- Data: NASA data are unmodified; the download and verification scripts were written with AI assistants.
```

### In-game Sources page (`MUSIC & ART`) — present at `cf2224d` and unchanged at `5ea2c23`

```text
Synthetic voices — Google Gemini text-to-speech
Voices Orus and Kore. AI-generated audio. Original score cues and sound effects synthesized by scripts written with AI coding assistants.
```

Nothing has to change. Only if Codex edits `FlightRecorderPresenter.cs` for another reason (R41 is Codex's file), this optional wording would also name the ending themes:

```text
Voices Orus and Kore. AI-generated audio. Original score cues, the ending themes "Thirty-One Dawns" and "Last Carrier", and sound effects synthesized by scripts written with AI coding assistants.
```

### Deck and video

The deck uses only real in-game captures, says so in every footer, and carries the AI statement on slide 7 and in its file properties. **No demo video is approved or published.** If Apoliak decides for a video at 23:00 CET, it needs: gameplay footage only (no concept board), the acknowledgement in its description ("In-game dialogue voices are AI-generated (Google Gemini text-to-speech). Developed with AI coding assistants (Claude Code, OpenAI Codex).") and in the file metadata; a watermark is needed only if any generated picture or clip is shown.

## Do not

- Do not show any concept board, generated poster or generated thumbnail without a visible "AI-generated" watermark.
- Do not put a NASA logo, flag or insignia into anything we generate (deck, thumbnail, video, game).
- Do not describe the voices as recordings of people, or the gameplay captures as AI-generated.
