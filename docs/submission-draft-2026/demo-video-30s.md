# Demo video — 30 seconds, script and exact shot list

**DRAFT.** Nothing was recorded or uploaded. The team records it from the submitted Windows build, edits it and uploads it.

## Rules this video must meet (official guides, read 15 Nov 2026)

- **30 seconds or less.** Over the limit = not eligible for Global Judging. Export at **29.5 s**.
- **English.** On-screen text in English (we use no voice-over; if anyone speaks another language, English subtitles are mandatory).
- **Public link, no login** (YouTube *Public* or *Unlisted*, or Google Drive "Anyone with the link"). Test in a private window.
- **Nobody under 18**: no face, voice or photo of any person under 18. The team page says one member is under 18, so the video shows **no team member at all** and uses no human voice-over. Names appear only as text.
- **SI labelling**: SI images/videos need a visible watermark; SI audio needs text + metadata acknowledgement. Our footage is real gameplay (not generated video), but the game's dialogue voices are AI-generated and its assets were made with AI coding tools, so we put a small persistent label on screen and the acknowledgement in the description and in the file metadata. **Never** cut in a concept board from `docs/art-direction/` (those are AI-generated images).
- **No NASA branding.** No NASA logo, meatball, worm or flag anywhere, including the thumbnail. Say "NASA/JPL data" in text only.
- Suggested beats from the guide: 0:00 who you are and the team · 0:02 the challenge and why · 0:05 title and tagline · 0:10 how it works and how NASA data made it possible · 0:20–0:30 the future.

## Before recording

| Item | Setting |
|---|---|
| Build | The submitted Windows x64 build at the final integration SHA (write it here: `________`). Release build, no development overlay. |
| Resolution | 1920×1080, fullscreen, 60 fps; graphics at the highest preset that keeps 60 fps |
| Options | Reduced camera motion / light effects **off**; subtitles **on** (they are part of the experience) |
| Recorder | Windows Game Bar (Win + Alt + R) records the game window with its audio; OBS Studio if installed. Record each shot as its own clip of 6–15 s so you can choose the best 2–5 s |
| Audio | Keep the game audio in every clip. The music bed is laid in the editor (see Audio) |
| Editor | Clipchamp (built into Windows 11) or DaVinci Resolve; 1080p30 or 1080p60 MP4, H.264, AAC 48 kHz |

## Shot list (29.5 s)

Times are positions in the final edit. "Capture" says exactly where in the game to record. If a feature is not in the build, use the fallback in the last column; never fake a feature.

| # | Edit time | Beat | Capture (in game) | On-screen text (burned in, English) | Fallback |
|---|---|---|---|---|---|
| S1 | 0:00.0–0:02.0 | Who we are | From the elevated boarding deck at LC-7, slow look from the tower to the vehicle on the pad (Planning, before touching any crate). | **TEAM POLISH ALEX GOATS** / Alexander Kozovy · Alex Poliak | Any wide shot of the launch site |
| S2 | 0:02.0–0:05.0 | The challenge, why | First person carrying a crate across the deck; the white mm:ss loading countdown is visible at the top (timed mode, not Practice). | **NASA SPACE APPS 2026 · SPACE MISSION DESIGN GAME** / What if you had to survive your own mission design? | Cargo stacks with their placards (`SECURED CARGO`) |
| S3 | 0:05.0–0:10.0 | Title + tagline | Departure from the seat: last seconds of the radio countdown, ignition, liftoff (exterior shot from the departure cinematic, 8.5–12 s of the timeline). Title appears at liftoff. | **OUT OF REACH** (large) / The mission you design is the mission you must survive. | If the countdown is not in the build, start the clip at ignition |
| S4 | 0:10.0–0:13.0 | The accident | "Complete the cockpit check" at the console → the impact: shake, emergency light, damage prompts. | A debris strike. Power 20 %. Life support down. | Interior right after the impact |
| S5 | 0:13.0–0:16.0 | Engineering trade-off | Aim at the life-support rack until the prompt shows the exact cost (spare parts, kWh); press and show the repaired state. Alternative: the EVA at the antenna with the tether in view. | Every repair is paid from the cargo you packed. | Power repair at the EPS panel |
| S6 | 0:16.0–0:20.0 | NASA data | Open the "AURORA / EARTH ARCHIVE" terminal: the EPIC Earth with *OBSERVED UTC* and *IMAGE AGE*; then the console prompt *"Transmit distress call … Earth is 197,708,000 km away; earliest reply in 31 min 59 s."* | Real NASA/JPL distance: Earth's reply can't beat light. / small: *Earth image: NASA EPIC / NOAA DSCOVR* | Terminal only, no transmit prompt |
| S7 | 0:20.0–0:24.0 | Survival | The routine montage or a chapter card with the day number, then the HUD with O₂ reserve, water, food and battery bars; ideally the sleeping bag or a meal. | 30 days. NASA life-support rates drain every reserve. | "DAY 1" rest card over the cabin (survival chapter after contact) |
| S8 | 0:24.0–0:27.0 | Outcome | The SURVIVED card after the rescue, or a loss card with its cause (e.g. *"Carbon dioxide overwhelmed the cabin."*), then 1 s of the mission record scrolling. | Survive — or learn exactly why you didn't. | Loss card only |
| S9 | 0:27.0–0:29.5 | The future | Slow push-in on the cockpit window / Earth terminal; fade to black over the last 0.5 s. | **NEXT:** team mode for four · new destinations · classrooms / small: *Music: "Titan" by Scott Buckley (CC BY 4.0) · NASA/JPL data* | Black card with the same text |

Persistent label, all 29.5 s, bottom-right, small (≈1.5 % of the frame height), 70 % white:
`Real gameplay · AI-assisted development · details in description`

Safe area: keep all text inside the central 90 % of the frame; the bottom 12 % already holds in-game subtitles, so put the burned-in text in the upper or middle third.

## Audio

1. Mute the music in the clips (keep effects), then lay the approved opening master under the whole edit: `docs/art-direction/prologue-audio-20261115/v02/opening-titan-ascent-v02-master.wav` ("Titan" by Scott Buckley, CC BY 4.0, with the team's added percussion). Suggested in-point **0:45.0** of the master (measured drum pulse 42–60 s), 0.3 s fade-in, 1.0 s fade-out ending at 29.5 s. Choose the exact point by ear.
2. Keep from the clips: the radio countdown and the ignition in S3 (duck the music by about 8 dB under the countdown), the impact in S4, one interface confirmation in S5.
3. The in-game voices are synthetic (Google Gemini TTS). If any voice line is audible in the final cut, the description and the file metadata must say so (they already do below).
4. Never use the rejected SFX (v02, shock v03–v06) or any film/game audio.

## Burned-in captions (also as a file)

[demo-video-30s.en.srt](demo-video-30s.en.srt) holds the same text with the same timings. Upload it as English subtitles on YouTube too.

## File metadata (SI acknowledgement in metadata)

After export, write the metadata without re-encoding (FFmpeg is installed on the team PCs):

```bash
ffmpeg -i out-of-reach-demo.mp4 -c copy -metadata title="OUT OF REACH - NASA Space Apps 2026 demo" -metadata artist="Team Polish Alex Goats" -metadata comment="Real gameplay capture. In-game dialogue voices are AI-generated (Google Gemini text-to-speech). The game was developed with AI coding assistants (Claude Code, OpenAI Codex). Music: Titan by Scott Buckley, CC BY 4.0. NASA/JPL Horizons data; Earth image NASA EPIC/NOAA DSCOVR." out-of-reach-demo-final.mp4
```

## Video description (paste into YouTube / Drive)

```text
OUT OF REACH — NASA Space Apps Challenge 2026 · Space Mission Design Game · Team Polish Alex Goats (Alexander Kozovy, Alex Poliak)

The mission you design is the mission you must survive. Pack a fictional Mars research ship within real mass, budget, power and supply limits, then survive the accident your own choices shaped. NASA/JPL Horizons distances set the light-speed radio delay; NASA life-support baseline values drive oxygen, CO2, water and food; the Earth on the cockpit terminal is a real NASA EPIC / NOAA DSCOVR image.

Real gameplay capture from the Windows build.

AI use: the game was developed with AI coding assistants (Claude Code, OpenAI Codex) under the team's direction. In-game dialogue voices are AI-generated with Google Gemini text-to-speech. No AI-generated images or video appear in this demo.

Music: "Titan" by Scott Buckley — www.scottbuckley.com.au — licensed under CC BY 4.0 (https://creativecommons.org/licenses/by/4.0/). Edited excerpt with added percussion.
NASA data and media: NASA/JPL Horizons; NASA Life Support Baseline Values (BVAD); NASA EPIC Team / NOAA DSCOVR; NASA/GSFC Scientific Visualization Studio Deep Star Maps (Gaia DR2: ESA/Gaia/DPAC). No NASA endorsement implied; AURORA-7 is fictional.
Full credits: [FINAL PROJECT LINK]
```

Add "Launch countdown: NASA audio (Apollo 11 launch countdown), excerpt." to the description if the countdown is audible.

## Thumbnail

A frame from S3 (liftoff) or S6 (EPIC terminal) with the title "OUT OF REACH". No logos of any agency, no team member, no concept board.

## Final checks before upload

- [ ] Duration ≤ 29.5 s in the exported file (check the file properties, not the editor timeline).
- [ ] Every caption is English and readable on a phone.
- [ ] No person, face or human voice-over; no concept board; no NASA logo.
- [ ] The persistent AI label is visible; description and metadata carry the AI and music credits.
- [ ] Every shot shows the submitted build, and every claim on screen is visible in it.
- [ ] Link opened in a private browser window without login.

## Alternative if there is no time to edit a video

Submit the 7-slide deck as the Project Demo (PDF on Google Drive or GitHub, public link): [deck/OUT-OF-REACH_SpaceApps2026_deck.pdf](deck/OUT-OF-REACH_SpaceApps2026_deck.pdf). The guide allows either format; a strong demo raises the *Presentation* score, so the video is the first choice.
