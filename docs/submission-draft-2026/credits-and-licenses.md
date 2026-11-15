# Credits and licences — OUT OF REACH

**DRAFT for the team.** Nothing here was published. **Re-swept on `codex/nasa-2330-staging` @ `cf2224d`** (15 Nov 2026, 20:35 UTC) with `git diff --name-status dc9ff8b cf2224d | grep -iE "attribution|provenance|notice|licen|readme|SOURCE.json"` and a GUID check of the saved `Bootstrap` scene and its prefabs. The first sweep was on `codex/i01-integration` @ `dc9ff8b`.

What the re-sweep changed:
- **New third-party item in the build:** NASA audio (Apollo 11 launch commentary) as the launch radio countdown — `Assets/StreamingAssets/ThirdParty/NASA-Audio/NOTICE.txt`, `Audio/LaunchRadio/README.md`.
- **New NASA image in the build:** the Slovakia inset on the terminal, NASA GIBS / ESDIS, MODIS Terra, 14 Nov 2026 — `Visuals/Props/MissionTerminal/Data/ATTRIBUTION.md`.
- **Titan** now ships as the approved v02 master (source 48–132 s, added percussion) — `StreamingAssets/ThirdParty/Titan/NOTICE.txt`.
- **New original team work (no third-party content):** rescue craft and ending (`Visuals/Props/RescueCraftV01/README.md`), tether rope (`Visuals/Props/EvaTetherV02/README.md`), ration pouch (`Visuals/Props/GalleyConsumablesV01/ATTRIBUTION.md`), launch tower V02, startup cards and their pencil sound (`UI/Startup/PENCIL-SOUND-PROVENANCE.md`), two ending themes (`Audio/Music/FinaleR31/provenance.json`), three new voice lines (`Audio/Voices/Story02/approved-voices.json`).
- **Not in the tree, removed from every paste block:** the Space Shuttle STS-131 launch sound and the four Freesound recordings (they were auditions only; `git grep -i "freesound\|STS-131" cf2224d -- Assets` finds nothing).
- **Gaps 1–4 of the first sweep are closed** in the game (section C).

Why this matters: the official submission form makes the Team Owner tick *"I confirm that the submitted project represents my team's original work and that all external resources including code, text and images (even if they are open source or freely available) used in the project are listed in the References field … my team has not used any copyrighted materials (i.e., music, images, text, etc.) that we don't have permission to use."* ([Judging & Awards Guide](https://www.spaceappschallenge.org/resources/judging-awards-guide/), read 15 Nov 2026). Every row below is either licensed for this use or excluded from what we publish.

---

## A. Paste-ready credits block

Use it in the public project README, the last slide (short form) and the References field. It matches `cf2224d`; after the final SHA only the lines named in [form-fields.md](form-fields.md) "Switch sentences" can change.

```text
CREDITS — OUT OF REACH (NASA Space Apps Challenge 2026, team Polish Alex Goats)

Music
- "Titan" by Scott Buckley — www.scottbuckley.com.au — licensed under CC BY 4.0
  (https://creativecommons.org/licenses/by/4.0/). Adapted excerpt (source 48–132 s); added percussion
  synthesized by AI-written code; gain staging and mastering.
- "Cobalt" by Scott Buckley — www.scottbuckley.com.au — licensed under CC BY 4.0. Edited 120-second loop.
- Ending themes "Thirty-One Dawns" and "Last Carrier" and the tension and emergency cues: original,
  composed and synthesized by scripts written with AI coding assistants.

NASA and space-agency media (no endorsement implied; no NASA logos or insignia are used)
- Launch radio countdown: NASA audio, Apollo 11 launch commentary (nasa.gov), two excerpts, radio
  band-limited; the mission name is omitted.
- Earth image: NASA EPIC Team / NOAA DSCOVR (epic.gsfc.nasa.gov), observed 2026-11-11 11:01:17 UTC.
- Slovakia image: NASA GIBS / ESDIS, MODIS Terra, 2026-11-14. We acknowledge the use of imagery provided
  by services from NASA's Global Imagery Browse Services (GIBS), part of NASA's Earth Science Data and
  Information System (ESDIS).
- Mars map: NASA/Jet Propulsion Laboratory & Caltech (Viking images processed by USGS).
- Space sky: NASA/Goddard Space Flight Center Scientific Visualization Studio, Deep Star Maps 2020;
  Gaia DR2: ESA/Gaia/DPAC.
- Earth–Mars distances: NASA/JPL Horizons. Life-support values: NASA Life Support Baseline Values and
  Assumptions Document (NASA/TP-2015-218570/REV2).
- Star data in the source package: NASA General Mission Analysis Tool (GMAT), Apache License 2.0.

3D models, skies and textures (CC0 1.0)
- Poly Haven (polyhaven.com): James Ray Cock, Mia Pecina Zorko, Rob Tuytel, Rico Cilliers, Greg Zaal,
  Jarod Guest, BKS, Mateusz Sadek, PierreB3D, Michał Wiśniewski, Ulan Cabanilla, Derek Wight, Dario Barresi,
  Yann Kervran.
- ambientCG (ambientcg.com): Ground109, Ground110, Metal009, Metal027.
- MakeHuman base mesh, skeleton and weights (makehumancommunity.org).

Fonts (SIL Open Font License 1.1 unless noted)
- Barlow Condensed — The Barlow Project Authors. Source Sans 3 — Adobe.
- Inter — The Inter Project Authors (rendered into textures). DejaVu Sans Mono — Bitstream, Inc. and
  DejaVu contributors, Bitstream Vera / DejaVu licence (rendered into textures).

AI-generated content
- All dialogue uses synthetic voices generated with Google Gemini text-to-speech (voices Orus and Kore).
- Original score cues and sound effects were synthesized by scripts written with AI coding assistants.
- Code, procedural 3D models and textures were created with AI coding assistants (Claude Code, OpenAI Codex)
  under the team's direction, review and playtesting.

Engine: Unity 6000.3.28f1 (Universal Render Pipeline). AURORA-7, its crew and its rescue are fictional.
```

---

## B. Full inventory

Column **In build** means referenced by the saved `Bootstrap` scene or by a prefab it uses at `cf2224d` (checked by GUID), unless noted.

### B1. Music

| Asset | Creator, source | Licence | Repo path / notice | In build | What attribution must say |
|---|---|---|---|---|---|
| "Titan" (opening: loading, launch, ascent) | Scott Buckley, https://www.scottbuckley.com.au/library/titan/ | CC BY 4.0 | `Assets/_OutOfReach/Audio/Music/titan-departure.wav` (v02 master: source 48–132 s, added low percussion and tonal vibration, mastering; faded, not looped); `Assets/StreamingAssets/ThirdParty/Titan/NOTICE.txt` | **Yes** (Bootstrap) | Title, author, link, licence link, "adapted" — the in-game Sources page says "Excerpt 48–132 s with added original percussion, gain staging and mastering" |
| "Cobalt" (exploration/rest loop) | Scott Buckley, https://www.scottbuckley.com.au/library/cobalt/ | CC BY 4.0 (full text shipped) | `Assets/_OutOfReach/Audio/Music/mx01_isolation.wav`; `Assets/StreamingAssets/ThirdParty/Cobalt/{NOTICE,LICENSE}.txt` | **Yes** (`MissionAudioPresenter.prefab`) | Same; in-game Sources page has it |
| "Thirty-One Dawns" (rescue ending, loop), "Last Carrier" (loss ending) | Original composition and procedural synthesis by OpenAI Codex for OUT OF REACH (`Audio/Music/FinaleR31/compose_endings.py`); no samples, no paid API, no third-party composition | Team work | `Assets/_OutOfReach/Audio/Music/FinaleR31/` (`provenance.json` with hashes and the user approval of 15 Nov) | **Yes** (`MissionAudioPresenter.prefab`) | SI acknowledgement (AI-written audio): see [ai-disclosure.md](ai-disclosure.md); covered by the Sources line "Original score cues … synthesized by scripts written with AI coding assistants" |
| mx02 Tension, mx03 Emergency | Original algorithmic scores, `Audio/Music/Editor/build_music_v2.py` (Codex audio agent) | Team work | `Assets/_OutOfReach/Audio/Music/` | **Yes** | Same SI acknowledgement |
| mx04 Recovery | same generator | Team work | same folder | **No** (replaced by "Thirty-One Dawns"; no scene or prefab reference) | — |
| "Resonance" | Scott Buckley | CC BY 4.0 | audition only in `docs/art-direction/music-audition-20261115/` | **No** (rejected) | Credit only if the docs folder is published |

Stale text, harmless for the submission (owner of `Audio/Music/**` = Codex): `Audio/Music/README.md` still describes mx01 as the original "Cabin Radio" cue and mx04 as the recovery theme; mx01 is Cobalt and the recovery theme is "Thirty-One Dawns".

### B2. NASA audio

| Asset | Source | Licence / basis | Repo path | In build | Attribution |
|---|---|---|---|---|---|
| Launch radio countdown ("Ignition sequence start … We have a liftoff." and "Tower cleared. We got a roll program.") | NASA, Apollo 11 launch commentary, https://www.nasa.gov/wp-content/uploads/2015/01/590320main_ringtone_apollo11_countdown.mp3 (400,398 bytes, SHA-256 `9f14d326…a6f0`); transcript https://www.nasa.gov/wp-content/uploads/2015/03/apollo-11_day-01-highlights.pdf | NASA-created audio; not copyrighted in the US under the [NASA media guidelines](https://www.nasa.gov/nasa-brand-center/images-and-media/); credit NASA; no endorsement | `Assets/_OutOfReach/Audio/LaunchRadio/launch-radio-countdown.wav` (17.66 s; cut list and word times in `launch-radio-cues.json`; processing in `README.md`); notice `Assets/StreamingAssets/ThirdParty/NASA-Audio/NOTICE.txt` | **Yes** (`MissionAudioPresenter.prefab`) | "NASA audio (Apollo 11 launch commentary), excerpt". The passage naming the mission ("Liftoff on Apollo 11") is cut out; the launch stays fictional. In-game Sources page: "Launch radio — NASA" with the source link |
| STS-131 launch sound, Freesound recordings (C-V, m_cel, dslrguide, Yoyodaman234), SOFIA takeoff, STS-132 gear drop | — | — | were auditions under `docs/art-direction/sfx-cinematic-v03/`, deleted from the tree | **No** | Do not credit; they are not in the project |

### B3. Original synthesized sound (team work, AI-written generators)

UI/alarm SFX `Audio/SFX/` (`finish_audio.py`), airlock cues `Audio/Original/Airlock/` (seed 740317), deck contacts `Audio/Original/DeckContacts/`, launch effects `Audio/Original/LaunchEffects/` (seat belt, hatch latch, ignition ramp, engine vibration, pad roar: "Original mathematical synthesis. No samples, recordings, or external dependencies"), rest/wake accent `Audio/Production/RestWakeV01/`, the startup-card pencil scratch (rendered in memory by `UI/Startup/Runtime/PencilScratchSynth.cs`, nothing stored), the docking clunks and hiss of the rescue ending (`Scripts/Presentation/Rescue/RescueEndingSfx.cs`). No samples or recordings. SI acknowledgement in text: in-game Sources page + README (see [ai-disclosure.md](ai-disclosure.md)).

### B4. Synthetic voices (SI-generated audio)

| Asset | Generator | Terms | Repo evidence (metadata) | In build |
|---|---|---|---|---|
| Clean/Radio dialogue vo01–vo12, Dramatic 02/03/05, Departure cues | Google Gemini TTS, models `gemini-3.8-flash-tts` and `gemini-3.8-flash-lite-tts`, stock voices **Orus** and **Kore**; no real person cloned | [Gemini API Additional Terms](https://aistudio.google.com/docs/terms) (Google claims no ownership of outputs; not CC0) | `Audio/Production/AUDIO_HANDOFF.md`, `clean-runtime-selection.json`, `Voices/Dramatic/voice-manifest.json`, `Voices/Departure/provenance.json` | **Yes** (`MissionAudioPresenter.prefab`; the spoken departure numbers were replaced by the NASA countdown) |
| Story02: **VO24a** "Aurora Seven, rescue crew. We have you in sight…" and **VO24b** "Contact. Capture confirmed. Hard dock…" | Google Gemini TTS, `gemini-3.8-flash-lite-tts`, voice **Orus**; exact approved bytes | same | `Audio/Voices/Story02/approved-voices.json` (cue, model, voice, transcript, duration, SHA-256, approval date), `README.md`, `*.txt` transcripts | **Yes** (bound in Bootstrap by the rescue-ending installer) |
| Story02: **VO23** "Aurora Seven… Mission Control. The rescue vehicle has crashed…" (day-14 message) | same | same | same manifest (`"runtime_wired": false`) | **Not yet** — file in the tree, no scene or prefab reference at `cf2224d` (switch S1 in [form-fields.md](form-fields.md)). Its text says "mission day thirty-one"; the game's rendezvous is day 30 |

Rule: SI audio must be acknowledged in descriptive text **and** metadata. Text: the in-game Sources page (`MUSIC & ART` → "Synthetic voices — Google Gemini text-to-speech. Voices Orus and Kore. AI-generated audio. Original score cues and sound effects synthesized by scripts written with AI coding assistants."), the README and the References field. Metadata: the manifests above.

### B5. NASA imagery and data

| Asset | Source | Basis | Repo path | In build | Credit line |
|---|---|---|---|---|---|
| Earth globe on the "AURORA / EARTH ARCHIVE" terminal | NASA EPIC / NOAA DSCOVR, image `epic_1b_20261111110606`, observed 2026-11-11 11:01:17 UTC, https://epic.gsfc.nasa.gov/archive/natural/2026/11/11/jpg/epic_1b_20261111110606.jpg | [EPIC image use policy](https://epic.gsfc.nasa.gov/about): reuse allowed with credit | `Visuals/Props/MissionTerminal/Data/` (unmodified JPEG + `ATTRIBUTION.md`, manifest) | **Yes** (default mode: `staticImageMode: 0`); the credit is printed on the terminal and on the Sources page | "Image: NASA EPIC Team / NOAA DSCOVR" |
| Slovakia inset on the same terminal | NASA GIBS / ESDIS, MODIS on Terra, layer `MODIS_Terra_CorrectedReflectance_TrueColor`, 2026-11-14, Terra pass 08:15–08:20 UTC, crop 46.90–50.45 N, 16.30–23.10 E, unmodified bytes (`modis_terra_slovakia_20261114_0815utc.jpg`) | [NASA Earth science data use guidance](https://www.earthdata.nasa.gov/engage/open-data-services-software-policies/data-use-guidance) (open, citation requested); [GIBS acknowledgement](https://nasa-gibs.github.io/gibs-api-docs/) | same folder (`ATTRIBUTION.md`, `slovakia-observation-manifest.json`) | **Yes** (inset texture set in `MissionTerminal.prefab`); credited on the terminal and on the Sources page ("Slovakia archive — Terra MODIS · 14 Nov 2026") | "We acknowledge the use of imagery provided by services from NASA's Global Imagery Browse Services (GIBS), part of NASA's Earth Science Data and Information System (ESDIS)." |
| Slovakia weather readout (MODIS land-surface temperature MOD11, cloud fraction MOD06) | NASA GIBS, same date | same | same folder, `Derivation~/` | **No** in the default mode (shown only in the look-only fallback `staticImageMode`, which is off) | Same acknowledgement if that mode is ever enabled |
| Mars colour map (recovery cinematic) | NASA 3D Resources, https://science.nasa.gov/3d-resources/mars/ | NASA/JPL media policy | `Visuals/Celestial/RecoveryMars/` (`SOURCE.json`) | **Yes**; credited in Sources | "NASA/Jet Propulsion Laboratory & Caltech; Viking images processed by USGS" |
| Space skybox | NASA SVS Deep Star Maps 2020, https://svs.gsfc.nasa.gov/4851/ (4096×2048 EXR, unmodified) | [SVS policy](https://svs.gsfc.nasa.gov/help/): public domain unless marked | `Visuals/Environment/NasaDeepSkyV01/` (`NOTICE.txt`) | **Yes** (`NasaDeepSky.mat` referenced by Bootstrap); credited in Sources | "NASA/Goddard Space Flight Center Scientific Visualization Studio; Gaia DR2: ESA/Gaia/DPAC" |
| Earth–Mars ephemeris, 32 daily vectors 14 Nov–15 Dec 2026 | NASA/JPL Horizons API, https://ssd-api.jpl.nasa.gov/doc/horizons.html | US Government work | `docs/data/horizons/`, `Scripts/Simulation/HorizonsEarthMars2026Snapshot.cs` | **Yes** (communications delay, day-14 dispatch) | "Earth–Mars reference ephemeris: NASA/JPL Horizons" |
| Life-support rates | NASA/TP-2015-218570/REV2 (BVAD), https://ntrs.nasa.gov/citations/20210024855 | US Government work | `Scripts/Simulation/CrewMetabolicAssumptions.cs` | **Yes** | cite in Sources/report |
| GMAT star catalogue derivative | NASA GMAT, https://github.com/nasa/GMAT @ `b8aec55` | Apache-2.0 (LICENSE + NOTICE shipped) | `Visuals/Space/Stars*.png`, `StreamingAssets/ThirdParty/NASA-GMAT/` | **Not bound** (`DistantStars.mat` has 0 scene/prefab references); the notice still ships in StreamingAssets. `NASA-DATA.md` and `STARFIELD_PROVENANCE.md` now say so (`7303687`) | Keep the NOTICE; list as "used in development" |
| Background references (no media copied) | NASA ECLSS, Mars communications delay PDF, Veggie fact sheet, Medical Operations (8.5 h sleep), OCHMO-TB-041 sleep accommodations, Crew Systems, Food in Space, Artemis II menu; ESA "An astronaut's rhythm", ESA "How loud is space?" | — | `NASA-DATA.md`, `docs/APPROVED-CHAPTER-LOOP-2026-11-15.md`, `Visuals/Models/Habitat/ATTRIBUTION.md` | n/a | List in References |

### B6. 3D models, HDRI skies and textures (CC0 1.0)

| Asset | Author(s) | Source | Repo path | In build |
|---|---|---|---|---|
| Power Box 01 (ship service cabinet) | Rico Cilliers (model/texture), Yann Kervran (rig) | https://polyhaven.com/a/power_box_01 | `Visuals/ThirdParty/PolyHaven/PowerBox01/` | TO CONFIRM (not referenced directly by Bootstrap; may sit inside a ship prefab) — credited anyway |
| flood_lights, portable_generator | James Ray Cock | polyhaven.com/a/… | `Visuals/ThirdParty/PolyHaven/` (`NOTICE.txt`) | Yes — the `PH_*` prefabs are referenced by Bootstrap |
| long_life_food | Mia Pecina Zorko | " | " | Yes |
| coast_land_rocks_04 | Rob Tuytel, Rico Cilliers | " | " | Yes |
| namaqualand_boulder_02 (also small stones) | Greg Zaal, Rico Cilliers | " | " | Yes |
| combination_wrench, screwdrivers_02 | BKS | " | " | Yes |
| adjustable_wrench, metal_toolbox | Mateusz Sadek | " | " | Yes |
| pliers | PierreB3D | " | " | Yes |
| vintage_spacecraft_instrument | Michał Wiśniewski | " | " | Yes |
| medical_box | Ulan Cabanilla | " | " | Yes |
| binoculars | Derek Wight | " | " | Yes |
| coast_sand_02 / gravelly_sand ground maps | Rob Tuytel / Dario Barresi | " | " | Materials prepared; scene use TO CONFIRM — credited anyway |
| grass_medium_02, shrub_04 (shrub_02 prepared, unused) | Rico Cilliers | polyhaven.com/a/… | `Visuals/LaunchVegetationV03/` | Yes |
| Rocky Ridge (Pure Sky) HDRI | Greg Zaal, Jarod Guest | https://polyhaven.com/a/rocky_ridge_puresky | `Visuals/Environment/LaunchSkyV03/` | Yes (launch site sky) |
| Industrial Sunset (Pure Sky) HDRI | Sergej Majboroda, Jarod Guest | polyhaven.com | Blender preview only | No |
| Ground109, Ground110 | ambientCG | https://ambientcg.com/a/Ground109 , /Ground110 | `Visuals/Environment/GroundFinishV02/` | Yes |
| Metal009, Metal027 | ambientCG | https://ambientcg.com/a/Metal009 , /Metal027 | `Visuals/Environment/SurfaceFinishV01/` | Yes (`BrushedSteel.mat`, `PowderCoat.mat` in Bootstrap) |
| MakeHuman base mesh, skeleton, weights | MakeHuman community, rev `a8bc2d5` | https://www.makehumancommunity.org/about/license.html (CC0 for graphical assets) | `Visuals/Character/FirstPersonBody/FitV02/Provenance/` | Yes (first-person body) |

Licence links: Poly Haven https://polyhaven.com/license · ambientCG https://docs.ambientcg.com/license/ · CC0 https://creativecommons.org/publicdomain/zero/1.0/ . Attribution is optional under CC0; we credit anyway.

### B7. Fonts

| Font | Licence | Shipped file / use |
|---|---|---|
| Barlow Condensed Bold — © 2017 The Barlow Project Authors | SIL OFL 1.1 | `UI/Typography/BarlowCondensed-Bold.ttf` + `BarlowCondensed-OFL.txt`; credited in Sources |
| Source Sans 3 Regular/Semibold — Adobe | SIL OFL 1.1 | `UI/Typography/SourceSans3-*.otf` + `OFL.txt`; also cargo case lettering |
| Inter — The Inter Project Authors | SIL OFL 1.1 | rasterised into ship, habitat, launch-site, transport and ration-pouch label textures; font file not shipped |
| DejaVu Sans Mono — Bitstream, Inc. and DejaVu contributors | Bitstream Vera / DejaVu licence | rasterised barcode/lot-code digits; font file not shipped |

### B8. Original team work (made with AI tools, see [ai-disclosure.md](ai-disclosure.md))

AURORA-7 ship, props (EPS-2 panel, ARS-1 rack, HGA antenna), astronaut hands, detail pass 02, habitat (berth, galley, grow module, ration labels), ration pouch (GalleyConsumablesV01), land launch site LC-7, surroundings, terrain, launch tower V02, transport, cargo case, WORK/EVA suits and first-person body animation, EVA tether rope V02 (procedural rope textures, meshes built in code), rescue craft V01 (built procedurally at runtime; "no copy of a real vehicle", no insignia), launch FX, land horizon, startup cards (vector drawings in code), all C# code and tests. Each package has its own `LICENSES.md`, `ATTRIBUTION.md`, `README.md` or `PROVENANCE` file. No NASA or agency insignia anywhere; "AURORA-7" and "LC-7" are fictional marks.

### B9. Engine and tools (not redistributed except the Unity runtime)

Unity 6000.3.28f1 and its packages (URP 17.3.0, Input System 1.20.1, Timeline 1.8.13, Test Framework 1.6.0, UI Toolkit, uGUI) under Unity's terms; the URP template content in `Assets/TutorialInfo`, `Assets/Settings` and `Assets/Scenes/SampleScene.unity`. Blender 5.3.0 and 3.6.1, Python 3 with NumPy and Pillow, FFmpeg, Git, Git LFS, GitHub.

### B10. In the repository but NOT for publication

| Item | Why it must stay out of any public link | Action |
|---|---|---|
| `docs/references/tpbXZyRxWNY/` (contact sheet and frames of a YouTube video by Brendan Jowett) | Third-party copyrighted frames, `"license": null` | Exclude from any public repository or archive |
| AI-generated concept boards: `docs/art-direction/{review-01,review-04,review-05-marine,review-06-final-overview,approved-hud-02,aurora7-detail-reference-v01,aurora7-personality-v01,hand-push-reference-v01,prologue-landscape-target-v01,outcome-sources-20261115}` and likely `launch-reference-v01`, `astronaut-reference-v01` (origin TO CONFIRM) | SI images need a visible watermark wherever judges can see them; none has one | Exclude from public links, or watermark "AI-generated concept — not gameplay" |
| `docs/art-direction/music-audition-20261115/` previews | Contain the rejected Resonance and unshipped auditions | Exclude, or keep with their attribution READMEs |
| `Audio/LaunchRadio/Previews/*.mp3` | Offline listening mixes, never used at runtime | Harmless (same sources as the game); may stay |
| Issue #7 and the other issues/PRs | Internal coordination history | Do not make the working repository public; publish a curated copy instead (see [submission-checklist.md](submission-checklist.md)) |
| Any demo video file | Not approved by Apoliak | Publish nothing unless Apoliak decides so at 23:00 CET |

---

## C. Gaps — state at `cf2224d`

| # | Gap from the first sweep | State |
|---|---|---|
| 1 | In-game Sources page lacks the AI voice notice | **Closed.** `FlightRecorderPresenter.cs` 422–423: "Synthetic voices — Google Gemini text-to-speech" / "Voices Orus and Kore. AI-generated audio. Original score cues and sound effects synthesized by scripts written with AI coding assistants." |
| 2 | Sources page lacks the NASA SVS sky credit | **Closed.** Lines 413–415: "Space sky — NASA/Goddard Space Flight Center Scientific Visualization Studio" / "Deep Star Maps 2020; Gaia DR2: ESA/Gaia/DPAC. Unmodified; public-domain reuse per NASA SVS." + link |
| 3 | Titan excerpt text on the Sources page | **Closed.** Line 406: "Excerpt 48–132 s with added original percussion, gain staging and mastering." |
| 4 | Countdown audio provenance next to the clip | **Closed.** `Audio/LaunchRadio/README.md`, `launch-radio-cues.json`, `StreamingAssets/ThirdParty/NASA-Audio/NOTICE.txt`; Sources page "Launch radio — NASA" |
| 5 | Stale texts | `STARFIELD_PROVENANCE.md` and `NASA-DATA.md` fixed (`7303687`). `Audio/Music/README.md` + `music-manifest.json` still stale (mx01 = Cobalt, recovery = "Thirty-One Dawns"). Not shown to players; fix only if Codex has spare minutes |
| 6 | Poly Haven, ambientCG, MakeHuman on the Sources page | CC0, no in-game credit required. They are credited in the References field and the README. Optional |
| 7 | **New, optional:** the Sources page does not name the two ending themes | Covered by the generic line in gap 1; nothing required. If Codex edits the page anyway, the exact wording is in [ai-disclosure.md](ai-disclosure.md) |
