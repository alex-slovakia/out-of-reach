# Credits and licences — OUT OF REACH

**DRAFT for the team.** Nothing here was published. Swept from the committed attribution, licence and provenance files on `codex/i01-integration` @ `dc9ff8b` (15 Nov 2026, 13:26 UTC), plus the lanes still in flight today (marked **PENDING**). Re-checked on the first `claude/integration` SHA `30055e5` (14:05 UTC): its only new attribution file is `Visuals/Models/LaunchTowerV02/Provenance/ATTRIBUTION.md` (original geometry and textures, Inter label pixels; listed in B8). The PENDING rows already carry their final wording: keep a PENDING line only if that asset is in the build you submit.

Why this matters: the official submission form makes the Team Owner tick *"I confirm that the submitted project represents my team's original work and that all external resources including code, text and images (even if they are open source or freely available) used in the project are listed in the References field … my team has not used any copyrighted materials (i.e., music, images, text, etc.) that we don't have permission to use."* ([Judging & Awards Guide](https://www.spaceappschallenge.org/resources/judging-awards-guide/), read 15 Nov 2026). Every row below is either licensed for this use or excluded from what we publish.

---

## A. Paste-ready credits block

Use it in the public project README, the video description, the last slide (short form) and the References field. Delete a line only if its asset is not in the submitted build.

```text
CREDITS — OUT OF REACH (NASA Space Apps Challenge 2026, team Polish Alex Goats)

Music
- "Titan" by Scott Buckley — www.scottbuckley.com.au — licensed under CC BY 4.0
  (https://creativecommons.org/licenses/by/4.0/). Edited excerpt; added percussion synthesized by AI-written
  code; mastering.
- "Cobalt" by Scott Buckley — www.scottbuckley.com.au — licensed under CC BY 4.0. Edited 120-second loop.

NASA and space-agency media (no endorsement implied; no NASA logos or insignia are used)
- Launch countdown radio: NASA audio, Apollo 11 launch countdown (nasa.gov), excerpt.      [PENDING: only if shipped]
- Launch rumble layer: NASA, Space Shuttle Discovery STS-131 launch sound (nasa.gov).     [PENDING: only if shipped]
- Earth image: NASA EPIC Team / NOAA DSCOVR (epic.gsfc.nasa.gov), observed 2026-11-11 11:01:17 UTC.
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

Sound recordings (CC0 1.0, freesound.org)                                                [PENDING: only if shipped]
- C-V, m_cel, dslrguide, Yoyodaman234.

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

Column **In build** means referenced by the saved `Bootstrap` scene or its prefabs at `dc9ff8b` (checked by GUID), unless noted.

### B1. Music

| Asset | Creator, source | Licence | Repo path / notice | In build | What attribution must say |
|---|---|---|---|---|---|
| "Titan" (departure/opening score) | Scott Buckley, https://www.scottbuckley.com.au/library/titan/ | CC BY 4.0 | `Assets/_OutOfReach/Audio/Music/titan-departure.wav`; notice `Assets/StreamingAssets/ThirdParty/Titan/NOTICE.txt`; approved master `docs/art-direction/prologue-audio-20261115/v02/opening-titan-ascent-v02-master.wav` (84 s, source 48–132 s + original percussion) | Yes (old 10.9–45.4 s cut). **PENDING:** `audio-opening` replaces its PCM with the approved v02 master under the same GUID | Title, author, link, licence link, "edited" — already in the in-game Sources page; update the excerpt text when v02 lands |
| "Cobalt" (exploration/rest loop) | Scott Buckley, https://www.scottbuckley.com.au/library/cobalt/ | CC BY 4.0 (full text shipped) | `Assets/_OutOfReach/Audio/Music/mx01_isolation.wav`; `Assets/StreamingAssets/ThirdParty/Cobalt/{NOTICE,LICENSE}.txt` | Yes (`MissionAudioPresenter.prefab`) | Same; in-game Sources page has it |
| "Resonance" | Scott Buckley | CC BY 4.0 | audition only in `docs/art-direction/music-audition-20261115/` | **No** (rejected) | Credit only if the docs folder is published |
| mx02 Tension, mx03 Emergency, mx04 Recovery | Original algorithmic scores, `Audio/Music/Editor/build_music_v2.py` (Codex audio agent) | Team work | `Assets/_OutOfReach/Audio/Music/` | Yes | SI acknowledgement (code-generated audio): see [ai-disclosure.md](ai-disclosure.md) |

Stale text to fix (owner of `Audio/Music/**` = `audio-opening` lane): `Audio/Music/README.md` and `music-manifest.json` still describe mx01 as the original "Cabin Radio" cue; mx01 is now Cobalt.

### B2. NASA audio and sound recordings

| Asset | Source | Licence / basis | Repo path | In build | Attribution |
|---|---|---|---|---|---|
| Launch countdown radio ("…ignition sequence starts…zero") | NASA, Apollo 11 countdown, https://www.nasa.gov/wp-content/uploads/2015/01/590320main_ringtone_apollo11_countdown.mp3 ; transcript https://www.nasa.gov/wp-content/uploads/2015/03/apollo-11_day-01-highlights.pdf | NASA-created audio; not copyrighted in the US under the [NASA media guidelines](https://www.nasa.gov/nasa-brand-center/images-and-media/); no endorsement | **PENDING** `Assets/_OutOfReach/Audio/Radio/**` (lane `audio-opening`, #7 6895117642) | PENDING | "NASA audio (Apollo 11 launch countdown), excerpt". Root's rule: no "Apollo 11" spoken line in the cut; the mission stays fictional |
| STS-131 launch sound (layer in engine audition v03 #02) | NASA Historical Sounds, https://www.nasa.gov/historical-sounds/ | as above | `docs/art-direction/sfx-cinematic-v03/` (audition) | PENDING (engine #02 and thruster #06 approved as directions) | "NASA (STS-131 launch sound)" |
| Jet Engine Start Cart | C-V, https://freesound.org/people/C-V/sounds/704945/ | CC0 1.0 | same audition folder | PENDING | Optional; listed |
| Jet Engine | m_cel, https://freesound.org/people/m_cel/sounds/534856/ | CC0 1.0 | same | PENDING | Optional; listed |
| Metal Hit 2 | dslrguide, https://freesound.org/people/dslrguide/sounds/321482/ | CC0 1.0 | same | PENDING | Optional; listed |
| Metal Impact 1 | Yoyodaman234, https://freesound.org/people/Yoyodaman234/sounds/334680/ | CC0 1.0 | same | PENDING | Optional; listed |
| SOFIA takeoff, STS-132 gear drop | NASA | as above | downloaded for selection, **not used** | No | — |

### B3. Original synthesized sound (team work, AI-written generators)

UI/alarm SFX `Audio/SFX/` (`finish_audio.py`), airlock cues `Audio/Original/Airlock/` (seed 740317), deck contacts `Audio/Original/DeckContacts/`, launch effects `Audio/Original/LaunchEffects/` (`departure-liftoff-shock` is cancelled by the user), rest/wake accent `Audio/Production/RestWakeV01/`. No samples or recordings. Need an SI acknowledgement in text (see [ai-disclosure.md](ai-disclosure.md)).

### B4. Synthetic voices (SI-generated audio)

| Asset | Generator | Terms | Repo evidence | In build |
|---|---|---|---|---|
| Clean/Radio dialogue vo01–vo12, Dramatic 02/03/05, Departure cues | Google Gemini TTS, models `gemini-3.8-flash-tts` and `gemini-3.8-flash-lite-tts`, stock voices **Orus** and **Kore**; no real person cloned | [Gemini API Additional Terms](https://aistudio.google.com/docs/terms) (Google claims no ownership of outputs; not CC0) | `Audio/Production/AUDIO_HANDOFF.md`, `clean-runtime-selection.json`, `Voices/Dramatic/voice-manifest.json`, `Voices/Departure/provenance.json` | Yes (7 Clean + 3 Dramatic bound; Departure cues partly removed by `audio-opening`) |

Rule: SI audio must be acknowledged in descriptive text **and** metadata. The manifests are the metadata; the in-game Sources page does not mention the synthetic voices yet (gap 1 below).

### B5. NASA imagery and data

| Asset | Source | Basis | Repo path | In build | Credit line |
|---|---|---|---|---|---|
| Earth disc on the "AURORA / EARTH ARCHIVE" terminal | NASA EPIC / NOAA DSCOVR, image `epic_1b_20261111110606`, observed 2026-11-11 11:01:17 UTC, https://epic.gsfc.nasa.gov/archive/natural/2026/11/11/jpg/epic_1b_20261111110606.jpg | [EPIC image use policy](https://epic.gsfc.nasa.gov/about): reuse allowed with credit | `Visuals/Props/MissionTerminal/Data/` (unmodified JPEG + `ATTRIBUTION.md`, manifest) | Yes; the credit is printed on the terminal | "Image: NASA EPIC Team / NOAA DSCOVR" |
| Mars colour map (recovery cinematic) | NASA 3D Resources, https://science.nasa.gov/3d-resources/mars/ | NASA/JPL media policy | `Visuals/Celestial/RecoveryMars/` (`SOURCE.json`) | Yes; credited in Sources | "NASA/Jet Propulsion Laboratory & Caltech; Viking images processed by USGS" |
| Space skybox | NASA SVS Deep Star Maps 2020, https://svs.gsfc.nasa.gov/4851/ (4096×2048 EXR, unmodified) | [SVS policy](https://svs.gsfc.nasa.gov/help/): public domain unless marked | `Visuals/Environment/NasaDeepSkyV01/` (`NOTICE.txt`) | Yes (`RenderSettings.skybox` = `NasaDeepSky.mat`) | "NASA/Goddard Space Flight Center Scientific Visualization Studio; Gaia DR2: ESA/Gaia/DPAC" — **not yet in the in-game Sources page** (gap 2) |
| Earth–Mars ephemeris, 32 daily vectors 14 Nov–15 Dec 2026 | NASA/JPL Horizons API, https://ssd-api.jpl.nasa.gov/doc/horizons.html | US Government work | `docs/data/horizons/`, `Scripts/Simulation/HorizonsEarthMars2026Snapshot.cs` | Yes (communications delay, day-14 dispatch) | "Earth–Mars reference ephemeris: NASA/JPL Horizons" |
| Life-support rates | NASA/TP-2015-218570/REV2 (BVAD), https://ntrs.nasa.gov/citations/20210024855 | US Government work | `Scripts/Simulation/CrewMetabolicAssumptions.cs` | Yes | cite in Sources/report |
| GMAT star catalogue derivative | NASA GMAT, https://github.com/nasa/GMAT @ `b8aec55` | Apache-2.0 (LICENSE + NOTICE shipped) | `Visuals/Space/Stars*.png`, `StreamingAssets/ThirdParty/NASA-GMAT/` | **No longer bound** (`DistantStars.mat` has 0 scene/prefab references); notice still ships in StreamingAssets | Keep the NOTICE; list as "used in development" |
| Background references (no media copied) | NASA ECLSS, Mars communications delay PDF, Veggie fact sheet, Medical Operations (8.5 h sleep), OCHMO-TB-041 sleep accommodations, Crew Systems, Food in Space, Artemis II menu; ESA "An astronaut's rhythm", ESA "How loud is space?" | — | `NASA-DATA.md`, `docs/APPROVED-CHAPTER-LOOP-2026-11-15.md`, `Visuals/Models/Habitat/ATTRIBUTION.md` | n/a | List in References |

### B6. 3D models, HDRI skies and textures (CC0 1.0)

| Asset | Author(s) | Source | Repo path | In build |
|---|---|---|---|---|
| Power Box 01 (ship service cabinet) | Rico Cilliers (model/texture), Yann Kervran (rig) | https://polyhaven.com/a/power_box_01 | `Visuals/ThirdParty/PolyHaven/PowerBox01/` | TO CONFIRM (not referenced directly by Bootstrap; may sit inside a ship prefab) |
| flood_lights, portable_generator | James Ray Cock | polyhaven.com/a/… | `Visuals/ThirdParty/PolyHaven/` (`NOTICE.txt`) | Yes — all 13 `PH_*` prefabs are referenced by Bootstrap |
| long_life_food | Mia Pecina Zorko | " | " | Yes |
| coast_land_rocks_04 | Rob Tuytel, Rico Cilliers | " | " | Yes (terrain-v03 may deactivate clutter) |
| namaqualand_boulder_02 (also small stones) | Greg Zaal, Rico Cilliers | " | " | Yes (same caveat) |
| combination_wrench, screwdrivers_02 | BKS | " | " | Yes |
| adjustable_wrench, metal_toolbox | Mateusz Sadek | " | " | Yes |
| pliers | PierreB3D | " | " | Yes |
| vintage_spacecraft_instrument | Michał Wiśniewski | " | " | Yes |
| medical_box | Ulan Cabanilla | " | " | Yes |
| binoculars | Derek Wight | " | " | Yes |
| coast_sand_02 / gravelly_sand ground maps | Rob Tuytel / Dario Barresi | " | " | Materials prepared; scene use TO CONFIRM |
| grass_medium_02, shrub_04 (shrub_02 prepared, unused) | Rico Cilliers | polyhaven.com/a/… | `Visuals/LaunchVegetationV03/` | Yes (terrain-v03 reworks it) |
| Rocky Ridge (Pure Sky) HDRI | Greg Zaal, Jarod Guest | https://polyhaven.com/a/rocky_ridge_puresky | `Visuals/Environment/LaunchSkyV03/` | Yes (launch site sky) |
| Industrial Sunset (Pure Sky) HDRI | Sergej Majboroda, Jarod Guest | polyhaven.com | Blender preview only | No |
| Ground109, Ground110 | ambientCG | https://ambientcg.com/a/Ground109 , /Ground110 | `Visuals/Environment/GroundFinishV02/` | Yes |
| Metal009, Metal027 | ambientCG | https://ambientcg.com/a/Metal009 , /Metal027 | `Visuals/Environment/SurfaceFinishV01/` | Yes (`BrushedSteel.mat`, `PowderCoat.mat` in Bootstrap) |
| MakeHuman base mesh, skeleton, weights; head with eyes/lashes (suits V03) | MakeHuman community, rev `a8bc2d5` | https://www.makehumancommunity.org/about/license.html (CC0 for graphical assets) | `Visuals/Character/FirstPersonBody/FitV02/Provenance/`; `ReferenceSuitsV03/**` (PENDING integration) | Yes (body) / PENDING (V03 suits) |

Licence links: Poly Haven https://polyhaven.com/license · ambientCG https://docs.ambientcg.com/license/ · CC0 https://creativecommons.org/publicdomain/zero/1.0/ . Attribution is optional under CC0; we credit anyway.

### B7. Fonts

| Font | Licence | Shipped file / use |
|---|---|---|
| Barlow Condensed Bold — © 2017 The Barlow Project Authors | SIL OFL 1.1 | `UI/Typography/BarlowCondensed-Bold.ttf` + `BarlowCondensed-OFL.txt`; credited in Sources |
| Source Sans 3 Regular/Semibold — Adobe | SIL OFL 1.1 | `UI/Typography/SourceSans3-*.otf` + `OFL.txt`; also cargo case lettering |
| Inter — The Inter Project Authors | SIL OFL 1.1 | rasterised into ship, habitat, launch-site and transport label textures; font file not shipped |
| DejaVu Sans Mono — Bitstream, Inc. and DejaVu contributors | Bitstream Vera / DejaVu licence | rasterised barcode/lot-code digits; font file not shipped |

### B8. Original team work (made with AI tools, see [ai-disclosure.md](ai-disclosure.md))

AURORA-7 ship, props (EPS-2 panel, ARS-1 rack, HGA antenna), astronaut hands, detail pass 02, habitat (berth, galley, grow module, ration labels), land launch site LC-7, surroundings, terrain v01, launch tower v02, transport, cargo case, WORK/EVA suits, launch FX, ocean/dock (legacy marine prologue), land horizon, all C# code and tests. Each package has its own `LICENSES.md`, `ATTRIBUTION.md` or `PROVENANCE` file. No NASA or agency insignia anywhere; "AURORA-7" and "LC-7" are fictional marks.

### B9. Engine and tools (not redistributed except the Unity runtime)

Unity 6000.3.28f1 and its packages (URP 17.3.0, Input System 1.20.1, Timeline 1.8.13, Test Framework 1.6.0, UI Toolkit, uGUI) under Unity's terms; the URP template content in `Assets/TutorialInfo`, `Assets/Settings` and `Assets/Scenes/SampleScene.unity`. Blender 5.3.0 and 3.6.1, Python 3 with NumPy and Pillow, FFmpeg, Git, Git LFS, GitHub.

### B10. In the repository but NOT for publication

| Item | Why it must stay out of any public link | Action |
|---|---|---|
| `docs/references/tpbXZyRxWNY/` (contact sheet and frames of a YouTube video by Brendan Jowett) | Third-party copyrighted frames, `"license": null` | Exclude from any public repository or archive |
| AI-generated concept boards: `docs/art-direction/{review-01,review-04,review-05-marine,review-06-final-overview,approved-hud-02,aurora7-detail-reference-v01,aurora7-personality-v01,hand-push-reference-v01,prologue-landscape-target-v01,outcome-sources-20261115}` and likely `launch-reference-v01`, `astronaut-reference-v01` (origin TO CONFIRM) | SI images need a visible watermark wherever judges can see them; none has one | Exclude from public links, or watermark "AI-generated concept — not gameplay" |
| `docs/art-direction/music-audition-20261115/` and `sfx-cinematic-v03/` previews | Contain the rejected Resonance and unshipped auditions | Exclude, or keep with their attribution READMEs |
| Issue #7 and the other issues/PRs | Internal coordination history | Do not make the working repository public; publish a curated copy instead (see [submission-checklist.md](submission-checklist.md)) |

---

## C. Gaps to close before submission

1. **In-game Sources page lacks the AI voice notice** (SI audio needs text + metadata). Owner of `UI/Report/Runtime/FlightRecorderPresenter.cs` (Claude integration assembler, per #7 6895055267): add to `MUSIC & ART`: *"Synthetic voices — Google Gemini text-to-speech (Orus, Kore). AI-generated audio."* and *"Original score cues and sound effects synthesized by scripts written with AI coding assistants."*
2. **Sources page lacks the NASA SVS sky credit** (same owner): *"Space sky — NASA/Goddard Space Flight Center Scientific Visualization Studio, Deep Star Maps 2020; Gaia DR2: ESA/Gaia/DPAC."*
3. **Titan excerpt text** on the Sources page says "10.9–45.4 seconds"; when `audio-opening` ships the v02 master, change it to the new excerpt (48–132 s, original percussion added) — lane `audio-opening`.
4. **Countdown audio provenance** file next to the clip, with the URL, excerpt times and "no endorsement" — lane `audio-opening`.
5. **Stale texts:** `Audio/Music/README.md` + `music-manifest.json` (mx01 is Cobalt), `Scripts/Presentation/Space/STARFIELD_PROVENANCE.md` (the GMAT sky is no longer bound; the SVS map is). Owners: `audio-opening`; integration assembler.
6. **Poly Haven, ambientCG and MakeHuman** are CC0 — no in-game credit is legally required. A one-line "3D models and textures: Poly Haven, ambientCG (CC0)" on the Sources page is courteous; optional.
