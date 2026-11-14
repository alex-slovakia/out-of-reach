# Demo storyboard — 30-second video (with 7-slide alternative)

**DRAFT ONLY.** Recording, hosting and uploading are team-owner decisions; nothing here was recorded or uploaded.

## Confirmed rules (from the official guide)

Guide https://www.spaceappschallenge.org/resources/project-submission-guide/ , dated 13 Nov 2026, read 14 Nov 2026 (relayed by the coordinator): video **30 seconds or less** in English (another spoken language only with English subtitles) **or** at most **7 English slides** including title/intro; exceeding the limit makes the project ineligible for Global Judging. Suggested structure: [0:00] who you are and team name; [0:02] challenge and why; [0:05] title and tagline; [0:10] how it works, prototype, how NASA data made it possible; [0:20–0:30] the future. SI-generated images/videos need a visible watermark; SI audio needs text + metadata acknowledgement; no NASA branding; nobody under 18.

## Assumptions / team decisions

- Footage must come from the **actual build** being submitted, ideally captured during the [playthrough checklist](playthrough-checklist.md). If a shot cannot be captured, use the fallback listed or drop the claim from narration.
- The survival chapter and RECOVERED ending exist in the saved scene from `85b0994` [docs/verification/i06-habitat-contact-native/README.md]; the latest recorded Windows build (`3032686`) does not contain them [docs/verification/i05-3032686/README.md]. Shots S7–S8 need a build that includes them (TO CONFIRM).
- The committed build entry point produces a Development build [Assets/_OutOfReach/Scripts/Composition/Editor/I01BootstrapBuilder.cs]; whether a development overlay appears in captures is TO CONFIRM.
- TESTING.md requires that edits condensing time **caption simulated time** and do not misrepresent the playable flow [TESTING.md "Release evidence"]; NASA-DATA.md forbids labelling compressed real seconds as simulated minutes without disclosing the scale [NASA-DATA.md "Chapter compression"].
- Export target: **29.5 s** or shorter, to keep a margin under 30 s.

## SI watermark and acknowledgement plan

| Element in the video | Rule | Plan |
|---|---|---|
| Game voices (Google Gemini TTS) heard in footage | SI audio: text + metadata | Put "Voices: synthetic, Google Gemini TTS" in the video description **and** in the video file's metadata (title/comment field) [Assets/_OutOfReach/Audio/Production/AUDIO_HANDOFF.md] |
| Game visuals made by AI-written scripts | Team interpretation TO CONFIRM | Conservative: persistent small corner label for all 29.5 s: `AI-assisted · synthetic voices (Google Gemini TTS)` |
| Any concept board from `docs/art-direction/**` | SI image: visible watermark | Not used in this plan. If added, overlay `AI-generated concept art — not gameplay` for its whole duration |
| Narration | — | If a team member speaks: no SI label needed. If narration is synthesized: acknowledge it like the game voices |
| Logos | No NASA branding | No NASA or agency logo/insignia anywhere, including thumbnail; say "NASA/JPL data" in plain text only |

## Shot list (29.5 s)

| Time | Guide beat | Shot (capture from the build) | Exists — evidence | On-screen text (overlay) |
|---|---|---|---|---|
| 0:00–0:02 | Who you are | S1. Live menu "Choose planning mode" over the real ship scene | Mode menu text in [Assets/_OutOfReach/Scripts/Composition/Runtime/I01SessionControls.cs]; menu presentation motion in [docs/verification/i05-3032686/README.md] | `Polish Alex Goats` · `[MEMBER 1] · [MEMBER 2]` |
| 0:02–0:05 | Challenge and why | S2. First-person two-hand crate carry, white mm:ss countdown at top | Physical cargo, two-hand grip, white/red deadline [docs/verification/i04-c7cfb86/README.md; captures `cargo-hands.png`, `cargo-countdown.png` there]; 60 active s [Assets/_OutOfReach/Scripts/Core/README.md "Planning deadline"] | `NASA Space Apps 2026 · Space Mission Design Game` |
| 0:05–0:10 | Title and tagline | S3. Cockpit check → impact, emergency light; hold on the cabin, then plain title card | Accident sequence [Assets/_OutOfReach/Scripts/Core/README.md "Accident"]; full-mission checks [docs/verification/i05-3032686/README.md] | Title card: `OUT OF REACH` / `Plan the mission. Lose contact. Engineer your way home.` |
| 0:10–0:13 | How it works | S4. Crosshair on life-support rack, prompt with real cost (e.g. "Repair life support: 2 spare parts, 1 kWh.") then repaired | Prompt format and costs [Assets/_OutOfReach/Scripts/Core/README.md "Prompts", "Scene integration"] | `Every part and kWh comes from what you packed` |
| 0:13–0:16 | How it works | S5. EVA: tethered route along the hull to the antenna; repair | EVA route and antenna repair [Assets/_OutOfReach/Scripts/Core/README.md "C03"]; real captures [docs/verification/i03-fb9703b/03-eva-start-1280.png; docs/verification/i04-375c535/04-antenna-restored-1280.png] | `Suit oxygen: 3 h (game assumption)` — use only if the HUD in the capture shows it |
| 0:16–0:20 | NASA data | S6. Cockpit console prompt "Transmit distress call … Earth is … km away; earliest reply in …", then "Awaiting reply from Earth … (simulated)" with the time scale | Console texts, day-1 values 197,707,976 km / 659.483 s / 1,918.966 s [Assets/_OutOfReach/Scripts/Core/README.md "C04"]; scripted delayed-reply checks [docs/verification/i05-3032686/README.md] | `NASA/JPL Horizons distance ÷ speed of light × 2 + 10 min processing (game assumption)` · `Simulated time shown at ×60` — copy the actual numbers visible in the capture |
| 0:20–0:25 | The future (bridge) | S7. Survival chapter: hunger/hydration bars, grow module, rest panel "DAY N" | Survival UI and rest panel [Assets/_OutOfReach/UI/Runtime/SurvivalChapterPresenter.cs]; real captures [docs/verification/i06-presentation-audio/01-survival.png, 02-rest.png]; habitat interactions 579 assertions [docs/verification/i06-habitat-contact-native/README.md] | `NASA BVAD crew-day rates · rest ≤ 8.5 h · fictional rescue` · `Edited; simulated time compressed` |
| 0:25–0:29.5 | The future | S8. End card "CREW RECOVERED — You made it." then the mission record's sources | End card copy [Assets/_OutOfReach/UI/Report/Runtime/FlightRecorderPresenter.cs]; capture [docs/verification/i06-presentation-audio/03-recovered.png]; report sections [Assets/_OutOfReach/Scripts/Core/README.md "Report"] | `Next: sea-launch prologue · wider playtests` (team goal, not delivered) |

Fallbacks if S7–S8 are not in the submitted build: S7 → the delayed Earth reply arriving; S8 → the legacy end card "EARTH LINK RESTORED — A voice from Earth." [FlightRecorderPresenter.cs] and change subtitle 6 to "Reserves keep draining while you wait." Do not show the survival chapter if the build cannot play it.

Persistent overlay (conservative SI label, all shots): `AI-assisted · synthetic voices (Google Gemini TTS)`.

## Narration / subtitles (English SRT, ends at 29.5 s)

```srt
1
00:00:00,000 --> 00:00:02,000
We are team Polish Alex Goats.

2
00:00:02,000 --> 00:00:05,000
Space Mission Design Game:
your plan must matter.

3
00:00:05,000 --> 00:00:10,000
OUT OF REACH. Plan the mission,
lose contact, engineer your way home.

4
00:00:10,000 --> 00:00:16,000
The cargo you load decides which repairs
and spacewalk you can afford.

5
00:00:16,000 --> 00:00:20,000
NASA/JPL Horizons distance
sets the reply delay.

6
00:00:20,000 --> 00:00:25,000
NASA life-support rates drain oxygen,
water and food until rescue.

7
00:00:25,000 --> 00:00:29,500
Next: the sea-launch prologue
and wider playtests.
```

Fact sources for the subtitles: team name [README.md]; challenge [README.md]; loadout decides repairs/EVA [Assets/_OutOfReach/Scripts/Core/README.md "Emergency model", "C03"]; Horizons drives delay [NASA-DATA.md; Assets/_OutOfReach/Scripts/Core/README.md "How the delay is computed"]; BVAD rates [Assets/_OutOfReach/Scripts/Simulation/CrewMetabolicAssumptions.cs; Assets/_OutOfReach/Scripts/Core/SurvivalRecovery.cs]; "until rescue" is the fictional rescue [Assets/_OutOfReach/Scripts/Core/SurvivalRecovery.cs]; future items are team goals [docs/art-direction/review-05-marine/README.md; docs/playtest-review/06-status-1845.md].

Pre-export checks: duration ≤ 29.5 s; subtitles legible at 1080p; all text English; no NASA logo; SI label visible; description and file metadata contain the Gemini TTS acknowledgement; numbers on screen match the capture.

## Alternative: 7 slides (English, including title)

Use real captures only (from the playthrough or from `docs/verification/i04-c7cfb86/`, `i05-3032686/`, `i06-presentation-audio/`). Avoid `docs/verification/i02-7e41699/` captures: they show the rejected interim UI [docs/verification/i02-7e41699/README.md]. Any concept board needs the SI watermark; review-06 boards also contain Slovak text.

1. **Title / intro** — OUT OF REACH · Polish Alex Goats · [MEMBER 1], [MEMBER 2] · Space Mission Design Game · tagline · one real capture (cargo carry). Footer: `AI-assisted · synthetic voices (Google Gemini TTS)` [README.md].
2. **Challenge and why** — The challenge asks for integrated mission design, management and evaluation; the planner must change the survival sequence and the report; audience: players without aerospace training [PRODUCT.md "Science and challenge alignment", "Player promise"].
3. **How it plays** — Plan (physical cargo, limits, 60 s) → accident → repairs → EVA → one transmission → survival → outcome and record; three captures [Assets/_OutOfReach/Scripts/Core/README.md; docs/verification/i04-c7cfb86/README.md].
4. **NASA data in the mechanics** — Horizons: distance ÷ c × 2 + 600 s processing = 31 min 59 s on day 1 (197,707,976 km); BVAD crew-day O2 0.84 kg, CO2 1.01 kg, water 2.5 kg, food 1.83 kg; GMAT star directions; Veggie grow timing; 8.5 h rest [NASA-DATA.md; Assets/_OutOfReach/Scripts/Core/README.md; CrewMetabolicAssumptions.cs; SurvivalExtensionSpec.cs; SurvivalRecovery.cs; Assets/StreamingAssets/ThirdParty/NASA-GMAT/NOTICE.txt].
5. **Survival and outcomes** — rest/grow/beacon, CREW RECOVERED vs FLIGHT ENDED; record separates NASA data, derived physics, fiction, time compression; captures `01-survival`, `02-rest`, `03-recovered`, `05-lost` [docs/verification/i06-presentation-audio/README.md; FlightRecorderPresenter.cs].
6. **Validity** — deterministic single-authority simulation, all-or-nothing spending, labelled assumptions; automated evidence (274/274 full-mission checks at I05 source; 309 EditMode + 50 PlayMode at `4a34a6b`; 579 habitat assertions) plus the human playthrough result once done [docs/verification/i05-3032686/README.md; docs/verification/i06-body-habitat-candidate/README.md; docs/verification/i06-habitat-contact-native/README.md].
7. **Impact, future, credits** — benefits; next steps (team goals); credits: Poly Haven Power Box 01 (CC0), MakeHuman (CC0), Source Sans 3/Inter (OFL 1.1), DejaVu, NASA GMAT (Apache-2.0); SI statement (Claude Code, OpenAI Codex, Gemini TTS) [si-and-credits-inventory.md].
