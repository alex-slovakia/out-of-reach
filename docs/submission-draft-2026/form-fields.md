# Submission form fields — paste-ready

**DRAFT for the Team Owner.** Not submitted, not published. Written for the build the team is finishing on 15 Nov 2026 (`codex/i01-integration` @ `dc9ff8b` plus the Claude lanes listed on #7 6895044220). Field names and rules were checked on the official [Project Submission Guide](https://www.spaceappschallenge.org/resources/project-submission-guide/) (dated 13 Nov 2026) and [Judging & Awards Guide](https://www.spaceappschallenge.org/resources/judging-awards-guide/), both read on 15 Nov 2026.

How to use:
- Each field has a **paste block** in plain English (no Markdown, the form is plain text).
- A feature that is still being finished today has a **fallback** below the block. Use the fallback if that feature is not in the build you submit. Never describe what the build cannot show: judges score *Validity*.
- Counts are measured on the paste blocks. The guide gives no length limits; if the form refuses a block, use the short version.

---

## 1. Project Name

```text
OUT OF REACH
```

## 2. High-Level Project Summary

Guide: *What did you develop? How does it address the challenge? Why is it important?* (159 words, 967 characters)

```text
OUT OF REACH is a first-person space-survival game in which the mission you design is the mission you must survive. At the launch pad you physically load the cargo for AURORA-7, a fictional Mars research ship, against a 60-second deadline and real engineering limits: a 600 kg payload, an 800-credit budget, electrical power and supplies for 30 crew-days. Science or safety margin? Every crate is a trade-off. Later, deep in space, a debris strike cripples power, life support and the antenna, and every repair, spacewalk and meal is paid for with what you packed. NASA/JPL Horizons distances set the radio delay, so no answer from Earth arrives faster than light, and NASA life-support baseline values drive oxygen, CO2, water and food. Across 30 compressed days you eat, sleep, decide and wait for a rescue that is never instant. A mission record then explains why you lived or died, so players without aerospace training feel the trade-offs mission designers face.
```

Short version (61 words), if a limit applies:

```text
A first-person survival game where the mission you design is the mission you must survive. Pack a fictional Mars ship within mass, budget, power and supply limits, then survive the accident your choices shaped. NASA/JPL Horizons distances set the light-speed radio delay; NASA life-support values drive oxygen, CO2, water and food. A mission record explains why you lived or died.
```

Fallback if the day 1–30 chapter loop is not in the build: replace *"Across 30 compressed days you eat, sleep, decide and wait for a rescue that is never instant."* with *"After contact you ration food and water, rest and power a beacon for a rescue that is never instant."*

## 3. Link to Project Demo

```text
[PUBLIC LINK — 30-second video (YouTube) or the 7-slide deck as PDF. No login, permission or registration. Test it in a private browser window.]
```

Recommendation and steps: [submission-checklist.md](submission-checklist.md). Video plan: [demo-video-30s.md](demo-video-30s.md). Deck: [deck/](deck/).

## 4. Link to Final Project

```text
[PUBLIC LINK — the playable Windows build plus README, credits and source. No login, permission or registration.]
```

Do **not** simply make the working repository public: it would expose issue #7 and unlicensed or unwatermarked material in `docs/` (see [credits-and-licenses.md](credits-and-licenses.md) §B10). Options are in [submission-checklist.md](submission-checklist.md).

## 5. Detailed Project Description

Guide questions: *What exactly does it do? How does it work? What benefits does it have? What do you hope to achieve? What tools, coding languages, hardware, or software did you use?* (1,257 words, 7,538 characters)

```text
THE IDEA
Mission design is usually taught as a spreadsheet: mass, power, budget, communications and supplies on paper. OUT OF REACH makes you live inside your own spreadsheet. You are Houston, the only crew member of AURORA-7, a small fictional research ship bound for Mars. You pack it yourself. Then something goes wrong, and the only resources you have are the ones you chose.

WHAT THE PLAYER DOES
1. Design the mission. At launch complex LC-7 you carry crates one at a time onto the transport: oxygen tanks, water tanks, food lockers, battery modules, solar wings, spare-parts kits and a 90 kg science spectrometer. The game enforces a 600 kg payload limit, an 800-credit budget, electrical load versus generation and supplies for 30 crew-days. Heavy crates slow you down. You have 60 seconds of real time; if your manifest is invalid when time runs out, the last valid one launches, and a crate still in your hands stays on the pad. There is no single right answer: a science-focused load (546 kg, 720 credits) carries exactly the 7 spare parts the three repairs will need, while a redundancy load (591 kg, 690 credits) gives up the science objective for more battery, oxygen and 13 spare parts.
2. Launch. You board, strap in and ride a vertical launch from the pad, then the story cuts to "LATER, ABOARD AURORA-7".
3. The accident. A routine cockpit check ends in a debris strike: power drops to 20 percent, life support and the antenna fail, and cabin CO2 starts to climb.
4. Stabilize. You repair power and life support with finite spare parts and battery energy. Every prompt states the exact cost, or the exact reason an action is refused, and a refused action spends nothing.
5. Spacewalk. You suit up (2 h 20 min of preparation, like the ISS prebreathe), seal the inner hatch and depressurize the airlock (the two hatches are never open together), follow the tether along the hull to the antenna, repair it and return before the suit's 3-hour oxygen supply runs out. A solo spacewalk is itself an emergency: real procedures require two crew members.
6. Call Earth. At the "Earth archive" terminal the planet you see is a real NASA EPIC photograph from the DSCOVR spacecraft, labelled with its observation time and its age on the mission clock. You get one distress call. The console shows Earth's real distance and the earliest possible reply: on mission day 1 Earth is 197,707,976 km away, so the reply cannot arrive before 31 min 59 s of mission time. Your reserves keep draining while you wait.
7. Survive 30 days. The story runs in chapters on mission days 1, 5, 9, 14, 20 and 30. You eat packaged rations, drink from the actual water store and sleep up to 8.5 hours in a secured sleeping bag. Ordinary days pass as a short routine montage that shows the elapsed days, spends real food, water, oxygen and power, and stops at the first danger. On day 5 you inspect the damage and decide: if you carried the science instrument, salvage it for spare parts or preserve the science; otherwise build a small grow module or conserve its parts. On day 14 a message arrives that the first rescue attempt has failed; it was sent from Earth and is delayed by the real light time of that day. On day 20 you choose between keeping your crop growing and saving power. On day 30 a rescue craft that was already in transit nearby asks for your beacon, and you must power it within an 8-hour window.
8. Outcome and record. The crew is rescued or lost, with the cause stated, for example "Carbon dioxide overwhelmed the cabin". A mission record rolls through your decisions and the science behind them, the Sources page separates NASA data, music and art, and game assumptions, and you can restart with a different loadout.

WHAT PLAYERS LEARN
- Mass, power, money and consumables compete; every extra instrument costs margins somewhere else.
- Deep-space communication is limited by physics: distance divided by the speed of light, both ways, plus processing. Nobody can help you instantly.
- Life support is several separate jobs. With scrubbers down, CO2 reaches the ISS emergency level about 20.5 hours later, long before the stored oxygen runs out.
- Spacewalks have real costs (preparation time, purge oxygen, airlock losses) and real risks.
- People need food and sleep; time itself is a consumable.
- The post-mission record connects each choice to the outcome, so a lost mission teaches as much as a survived one.

HOW IT WORKS
A deterministic C# simulation owns the mission: one mission clock, one resource ledger (oxygen in kg, water in litres, food in kg, battery in kWh, power in W, cabin CO2 in ppm, integer spare parts) and one event bus. Spending is all-or-nothing, and the visuals only display committed state.
Communications use a packaged, offline NASA/JPL Horizons snapshot: 32 daily Earth-Mars vectors from 14 November to 15 December 2026 (ICRF, geometric). The game verifies its SHA-256 at mission start, selects the last sample at or before the mission date, divides the distance by 299,792.458 km/s and adds 600 seconds of Earth processing, a labelled game assumption. The same model times the day-14 message.
Consumption uses NASA's Life Support Baseline Values (BVAD, NASA/TP-2015-218570/REV2): 0.84 kg oxygen, 1.01 kg CO2, 2.5 kg water and 1.83 kg food per crew-day. Cabin CO2 is derived with the ideal gas law for an assumed 20 m3 cabin; the losses use cited ISS flight-rule levels as game endings, not medical thresholds. Spacewalk preparation, suit purge, suit oxygen use and airlock losses follow BVAD EVA values for a fictional suit.
Time compression is always shown: x300 during the emergency, x60 during the spacewalk and the wait for Earth, one simulated hour per real second while sleeping and six hours per second in the routine montage. Pause freezes everything; the routine never skips a danger, a decision or a death.
The fictional parts are labelled: AURORA-7, its location (the Mars-centre distance is a documented proxy, not a computed trajectory), the rescue story, the catalogue prices and the time compression. No NASA logo or insignia appears anywhere.
The project is covered by more than 320 automated EditMode tests plus PlayMode scene tests (resource accounting, light-time timing, airlock safety, chapter routine stops, quit and pause), native Unity captures and repeated human playtests.

WHAT WE HOPE TO ACHIEVE
A complete mission that a student or a curious player can finish in about half an hour, lose for an understandable reason, and replay with a better design. Next we want to add team play for up to four people with separate roles (flight, engineering, communications, life support), new destinations and scenarios, a launch-date picker that refreshes the Horizons data, a side-by-side comparison of mission designs, and teacher notes. Spanish and French text already exists on a development branch.

TOOLS, LANGUAGES, HARDWARE, SOFTWARE
Unity 6000.3.28f1 with the Universal Render Pipeline, Input System, Timeline, UI Toolkit and the Unity Test Framework; C#; Windows x64. Blender 5.3 and 3.6 driven by Python for the original 3D models; Python 3 with NumPy and Pillow for procedural textures and data checks; FFmpeg for audio; Git, Git LFS and GitHub. AI coding assistants Claude Code (Anthropic) and OpenAI Codex helped write code, procedural assets and documentation under our direction; all dialogue uses synthetic voices from Google Gemini text-to-speech. Third-party music, models, textures and fonts are credited in the references. Developed and tested on Windows PCs.
```

Short version (≈560 words), only if the form refuses the full text:

```text
WHAT IT DOES
OUT OF REACH is a first-person space-survival game in which the mission you design is the mission you must survive. You are Houston, the only crew member of AURORA-7, a fictional research ship bound for Mars.
1. Design: at launch complex LC-7 you carry crates onto the transport within real limits (600 kg payload, 800 credits, power load versus generation, supplies for 30 crew-days) in 60 seconds. A science load carries a 90 kg spectrometer and exactly the spare parts the repairs will need; a redundancy load trades science for battery, oxygen and parts.
2. Launch, then later, deep in space, a debris strike: power 20 percent, life support and antenna down, CO2 rising.
3. Repair with the parts and energy you packed. Every prompt shows the exact cost; a refused action spends nothing.
4. Spacewalk to the antenna: preparation, airlock, tether, a 3-hour suit oxygen supply.
5. Call Earth once from a terminal that shows a real NASA EPIC photograph of Earth. On day 1 Earth is 197,707,976 km away, so no reply can arrive before 31 min 59 s of mission time; reserves drain while you wait.
6. Survive 30 days in chapters (days 1, 5, 9, 14, 20, 30): meals, sleep, hard choices, a day-14 message delayed by real light time, and a rescue window you must power.
7. A mission record explains why you lived or died; restart with a different design.

WHAT PLAYERS LEARN
Mass, power, money and consumables compete. Communication is limited by light speed. Life support is several separate jobs: with scrubbers down, CO2 reaches the ISS emergency level in about 20.5 hours, long before the oxygen runs out. Spacewalks cost time, oxygen and risk. Time itself is a consumable.

HOW IT WORKS
A deterministic C# simulation owns one mission clock and one resource ledger in real units; spending is all-or-nothing. An offline NASA/JPL Horizons snapshot (32 daily Earth-Mars vectors, SHA-256 checked) gives distance / speed of light, both ways, plus 600 s of assumed processing. NASA BVAD rates (0.84 kg O2, 1.01 kg CO2, 2.5 kg water, 1.83 kg food per crew-day) drive consumption; cabin CO2 uses the ideal gas law; spacewalk costs follow BVAD EVA values. Time compression is always shown; the routine montage never skips a danger. AURORA-7, its position proxy, the rescue and the prices are labelled fiction. More than 320 automated EditMode tests plus PlayMode scene tests and human playtests.

WHAT WE HOPE TO ACHIEVE
A complete half-hour mission that students can lose for an understandable reason and replay with a better design; next, team play for up to four roles, new destinations, a launch-date picker with fresh Horizons data and teacher notes.

TOOLS
Unity 6000.3.28f1 (URP), C#, Windows x64; Blender and Python for original 3D models; NumPy, Pillow, FFmpeg, Git LFS. AI coding assistants Claude Code and OpenAI Codex under our direction; dialogue voices by Google Gemini text-to-speech. Third-party assets are credited in the references.
```

Fallbacks (use the matching one if a feature is missing from the build you submit):
- **No day 1–30 chapter loop:** replace item 7 with *"7. Survive after contact. Contact is the midpoint, not the win: you eat and drink from the actual stores, can salvage the science instrument for spare parts, may build and harvest one small grow module, sleep up to 8.5 hours per rest, and power a recovery beacon when a fictional rescue craft that was already in transit asks for it."* Also delete *"The same model times the day-14 message."* and the "six hours per second in the routine montage" clause.
- **No visible tether climbing:** item 5 already says only "follow the tether"; keep it.
- **No launch cinematic changes:** item 2 stays true for the current departure sequence.

Sources: player flow, limits and presets — `Assets/_OutOfReach/Scripts/Core/README.md` ("C05-A", "AURORA-7 catalog and limits", "Emergency model", "C03", "C04"); chapter beats, routine and day-14 dispatch — same README "CORE-CHAPTER-LOOP" on `feature/chapter-loop-core` @ `c85bd33` and `docs/APPROVED-CHAPTER-LOOP-2026-11-15.md`; EPIC terminal — `Visuals/Props/MissionTerminal/Data/ATTRIBUTION.md`, capture `docs/verification/i06-earth-terminal-native-v05/Evidence/01-actual-v2-focused.png`; outcome card — `docs/verification/i06-player-feedback-20261115/death-verified-1280.png`; Sources page — `UI/Report/Runtime/FlightRecorderPresenter.cs`; Horizons — `NASA-DATA.md`; BVAD — `Scripts/Simulation/CrewMetabolicAssumptions.cs`; tests — 324/324 EditMode on `cae1c97` + MOVE-MECH-01 (lead's run), 347/347 EditMode at `c85bd33` (#7 6895109340), so "more than 320" holds with or without the chapter branch; localization — `feature/localization` @ `1cc011d`.

## 6. Use of Super Intelligence (SI)

The guide says this answer is for data collection and is **not shown to judges**, but judges see the SI indicators inside the project (watermarks, descriptive text, metadata). It also says SI must not be used to come up with the idea. Full labelling plan: [ai-disclosure.md](ai-disclosure.md).

```text
Yes. We used SI tools as implementation accelerators under the team's direction. The idea, the story, the mission premise and every design decision came from the two team members, who directed, reviewed, playtested and approved all work.

Code: Claude Code (Anthropic; models Claude Opus 5.5, Claude Fable 5.2 and Claude Sonnet 5.5) wrote most gameplay and simulation code, movement, body animation, the chapter loop, the tests and, on the last day, parts of the launch, audio and terrain integration. OpenAI Codex wrote most presentation, UI, scene composition, audio runtime and build tooling, and most documentation. Claude-written commits carry a "Co-Authored-By: Claude" trailer in the git metadata; the project README states the rest.

3D models and textures: our original meshes and textures were produced by Python scripts for Blender and NumPy/Pillow that these assistants wrote. No generative image or 3D model produced any in-game asset. Third-party CC0 models and textures are credited separately.

Audio: all spoken dialogue uses synthetic voices generated with Google Gemini text-to-speech (stock voices Orus and Kore); no real person's voice was cloned. This is acknowledged in the project credits and README and in the audio manifests that travel with the files. Three original score cues and all synthesized sound effects were generated by scripts written by the AI assistants. The licensed music is by the human composer Scott Buckley (CC BY 4.0); the percussion added to the opening mix was synthesized by such a script.

Images: art-direction concept boards were generated with the built-in GPT image generator in Codex. They are not in the game, the demo video or the slide deck.

Data: NASA data are used unmodified; the scripts that download, verify and convert them were written by the AI assistants and are recorded in the data manifests.

Submission materials: these texts, the slide deck and the video script were drafted with Claude Code from the project's own documentation and edited by the team.
```

When the in-game Sources page gets the synthetic-voice line ([credits-and-licenses.md](credits-and-licenses.md) gap 1), extend the Audio sentence to *"…in the project credits and README, on the in-game Sources page and in the audio manifests…"*.

## 7. NASA Data

Guide: *Provide specific details about what NASA data and resources you used. To be eligible for a Global Award, you must use at least one NASA data or resource.*

```text
1. NASA/JPL Horizons ephemeris (https://ssd-api.jpl.nasa.gov/doc/horizons.html). Query: Mars body centre (499) relative to Earth centre (500@399), VECTORS, km and km/s, ICRF, UT, geometric, daily 2026-11-14 to 2026-12-15 at 08:00 UTC (32 samples). The snapshot ships offline and is checked by SHA-256 at mission start. Mechanic: Earth's distance on the mission date gives the one-way light time; the earliest reply is twice that plus 600 s of game-assumed processing (197,707,976 km and 31 min 59 s on day 1). The same model times Earth's day-14 message. Reserves drain during every wait, so real orbital geometry decides whether you survive. The Mars-centre distance is a labelled proxy for the fictional ship, not its trajectory.

2. NASA Life Support Baseline Values and Assumptions Document, NASA/TP-2015-218570/REV2 (https://ntrs.nasa.gov/citations/20210024855). Per crew-day: oxygen 0.84 kg and CO2 1.01 kg (Table 3-23), water 2.5 kg (Table 4-20), food 1.83 kg (Table 4-40). Cabin CO2 and O2 levels (Table 4-1, section 4.1.1) anchor the CO2 and oxygen losses; EVA prebreathe, suit purge, suit oxygen use and airlock losses (section 4.6) set the spacewalk costs.

3. NASA EPIC / NOAA DSCOVR Earth image (https://epic.gsfc.nasa.gov/). The cockpit "Earth archive" terminal shows the unmodified natural-colour image epic_1b_20261111110606, observed 2026-11-11 11:01:17 UTC, with its credit, its observation time and its age computed from the mission clock. It is labelled as an archived observation, not a live feed or a view from the fictional ship.

4. NASA crew food and sleep practice: NASA Crew Systems (eight-hour sleep schedule, wall-attached sleeping bags), NASA Medical Operations (8.5-hour sleep opportunity = our maximum rest), OCHMO-TB-041 Sleep Accommodations (secured bag, restraints, ventilation, CO2), NASA Food in Space and the Artemis II menu (ready-to-eat, rehydratable and thermostabilized foods), NASA Veggie (plants only supplement the packaged diet; nominal 33-day harvest age, about 70 W). These shape the meals, the sleeping bag, the rest rules and the optional grow module.

5. NASA Scientific Visualization Studio, Deep Star Maps 2020 (https://svs.gsfc.nasa.gov/4851/), with Gaia DR2 star data from ESA/Gaia/DPAC: the space sky.

6. NASA/JPL-Caltech Mars colour map from Viking images processed by USGS (https://science.nasa.gov/3d-resources/mars/): Mars in the recovery cinematic.

7. NASA Environmental Control and Life Support Systems (ECLSS) and NASA "Mars Communications Disruption and Delay": background for treating oxygen supply, CO2 removal and water as separate jobs, and for refusing any instant reply or rescue.

8. NASA General Mission Analysis Tool (GMAT, Apache-2.0), a resource listed on this challenge: we converted its star catalogue into a six-face sky during development; the final build uses the SVS map instead.

AURORA-7, launch complex LC-7, the crew and the rescue are fictional. Mission time is compressed and always labelled. No NASA logo, flag, insignia or mission identifier appears in the game, the deck or the video.
```

If the radio countdown ships (lane `audio-opening`), add before the last paragraph:

```text
9. NASA audio: an excerpt of the Apollo 11 launch countdown from nasa.gov, used as the radio countdown of the fictional launch, and (if shipped) the Space Shuttle Discovery STS-131 launch sound as one layer of the engine rumble. No endorsement is implied.
```

Sources: [credits-and-licenses.md](credits-and-licenses.md) §B5; `NASA-DATA.md`; `docs/data/horizons/manifest.json`; `docs/APPROVED-CHAPTER-LOOP-2026-11-15.md` ("Verified real-world basis"); `Visuals/Models/Habitat/ATTRIBUTION.md`; `Visuals/Environment/NasaDeepSkyV01/NOTICE.txt`; `Visuals/Celestial/RecoveryMars/SOURCE.json`; GMAT binding check: `DistantStars.mat` has no scene or prefab reference at `dc9ff8b`, `RenderSettings.skybox` is `NasaDeepSky.mat`.

## 8. Space Agency Partner Data and Other References

Guide: *List all of the data, resources, and tools used … including any code, text, and images (even if they are open source or freely available)… If you are using any copyrighted materials, make sure you have permission.*

```text
SPACE AGENCY PARTNER DATA
- ESA Gaia DR2 (ESA/Gaia/DPAC), included in the NASA SVS Deep Star Maps 2020 sky.
- ESA, "An astronaut's rhythm" (https://www.esa.int/articles/an-astronaut-s-rhythm): light cues for the sleeping berth (reference only).
- ESA, "How loud is space?" (https://www.esa.int/ESA_Multimedia/Videos/2020/04/How_loud_is_space/%28lang%29): our rule that sound in vacuum is only heard through the suit, the hull or the radio (reference only).

MUSIC (CC BY 4.0, https://creativecommons.org/licenses/by/4.0/)
- "Titan" by Scott Buckley, https://www.scottbuckley.com.au/library/titan/ : edited excerpt with original added percussion and mastering.
- "Cobalt" by Scott Buckley, https://www.scottbuckley.com.au/library/cobalt/ : edited 120-second loop.

3D MODELS, SKIES AND TEXTURES (CC0 1.0)
- Poly Haven (https://polyhaven.com/license): Power Box 01 (Rico Cilliers, Yann Kervran); flood lights and portable generator (James Ray Cock); long-life food (Mia Pecina Zorko); coast land rocks 04 (Rob Tuytel, Rico Cilliers); Namaqualand boulder 02 (Greg Zaal, Rico Cilliers); combination wrench and screwdrivers 02 (BKS); adjustable wrench and metal toolbox (Mateusz Sadek); pliers (PierreB3D); vintage spacecraft instrument (Michał Wiśniewski); medical box (Ulan Cabanilla); binoculars (Derek Wight); coast sand 02 (Rob Tuytel); gravelly sand (Dario Barresi); grass medium 02 and shrub 04 (Rico Cilliers); Rocky Ridge Pure Sky HDRI (Greg Zaal, Jarod Guest).
- ambientCG (https://docs.ambientcg.com/license/): Ground109, Ground110, Metal009, Metal027.
- MakeHuman base mesh, skeleton and weights (https://www.makehumancommunity.org/about/license.html).

FONTS (SIL Open Font License 1.1 unless noted)
- Barlow Condensed (The Barlow Project Authors), Source Sans 3 (Adobe), Inter (The Inter Project Authors, rendered into textures), DejaVu Sans Mono (Bitstream, Inc. and DejaVu contributors, Bitstream Vera/DejaVu licence, rendered into textures).

AI SERVICES WHOSE OUTPUT IS IN THE PROJECT
- Google Gemini text-to-speech (gemini-3.8-flash-tts and gemini-3.8-flash-lite-tts; voices Orus, Kore), under the Gemini API Additional Terms: all dialogue.
- Claude Code (Anthropic) and OpenAI Codex: code, procedural asset scripts, documentation. Built-in GPT image generation: art-direction concept boards only (not in the game).

ENGINE, LIBRARIES AND TOOLS
Unity 6000.3.28f1 (Universal Render Pipeline 17.3.0, Input System 1.20.1, Timeline 1.8.13, Unity Test Framework 1.6.0, UI Toolkit), Blender 5.3 and 3.6, Python 3 (standard library, NumPy, Pillow), FFmpeg, Git, Git LFS, GitHub.

OTHER REFERENCES
- NASA Space Apps 2026 challenge "Space Mission Design Game" and its resources (GMAT).
- Real spacecraft (ISS, Orion, Skylab) and launch complexes were used as proportion and function references only; no NASA model, drawing, photograph, logo or insignia was copied.

PERMISSIONS
Every third-party asset above is CC0, CC BY 4.0 (attributed), SIL OFL 1.1, Bitstream Vera/DejaVu, Apache-2.0, a NASA or ESA public-reuse release (credited, no endorsement implied) or an AI output used under the provider's terms. The game uses no copyrighted material without permission.
```

If the countdown and recorded launch sounds ship, add under a new heading:

```text
SOUND RECORDINGS
- NASA audio: Apollo 11 launch countdown excerpt; Space Shuttle Discovery STS-131 launch sound (nasa.gov). No endorsement implied.
- Freesound (CC0 1.0): Jet Engine Start Cart by C-V; Jet Engine by m_cel; Metal Hit 2 by dslrguide; Metal Impact 1 by Yoyodaman234.
```

Sources: [credits-and-licenses.md](credits-and-licenses.md) (every row has its repository file).
