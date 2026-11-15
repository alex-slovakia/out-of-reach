# Submission form fields — paste-ready

**DRAFT for the Team Owner.** Not submitted, not published. **Checked against the release candidate:** `feature/claude-max-final` @ `5479e87` (#7 6901310758; 15 Nov, 22:15 CET) = staging `022138d` (tower V03) + handheld radio + finale stars + routine montage + walk fixes, with the day-14 announcement as radio subtitles. Full suites on the RC: 499/499 EditMode, 155/155 PlayMode (#7 6901385202); its Windows build is running. If the RC ships, the paste blocks are final as they stand. If a B build ships instead, one short diff pass follows (last section). Field names and rules were checked on the official [Project Submission Guide](https://www.spaceappschallenge.org/resources/project-submission-guide/) (dated 13 Nov 2026) and [Judging & Awards Guide](https://www.spaceappschallenge.org/resources/judging-awards-guide/), both read on 15 Nov 2026.

How to use:
- Each field has a **paste block** in plain English (no Markdown, the form is plain text).
- The paste blocks describe only what is in the RC `5479e87`. Features offered for a possible B build have **switch sentences** in [the last section](#switch-sentences-for-the-final-sha): add one only if its check passes on the final SHA. Never describe what the build cannot show: judges score *Validity*.
- Counts are measured on the paste blocks. The guide gives no length limits; if the form refuses a block, use the short version.

What was verified in the tree at `5479e87` (saved `Bootstrap.unity` and its prefabs, by GUID; runtime-created parts in their source):

| Claim | In the RC `5479e87` | Evidence |
|---|---|---|
| Day 1–30 chapter loop | **Yes** | `Bootstrap.unity`: `chapterLoop: 1`; `MissionChapterFlow` days 1, 5, 9, 14, 20, 30 |
| Handheld radio with the story's radio traffic | **Yes** | `HandheldRadioPresenter` and `RadioV01.prefab` in Bootstrap (`3d619b6`, merge `3709b6e`) |
| Finale stars (NASA SVS sky at a brighter exposure in the ending) | **Yes** | `RescueEndingSky` used by `RescueEndingPresenter` (`12991d7`); runtime only |
| Routine montage with live station pictures | **Yes** | `RoutineStationMontage` creates itself after scene load (`d67eb8e`…`5ea2c23`) |
| Launch tower V03 | **Yes** | installed in Bootstrap (`022138d`); original work, no text claim |
| Day-14 announcement on the radio | **Yes, as subtitles** | `5479e87` clears the VO23 voice binding (neither the original clip nor the day-neutral cut is referenced by Bootstrap) until Alex approves the cut; the paste blocks say "announcement" and are true with or without the voice |
| NASA launch radio countdown | **Yes** | `Audio/LaunchRadio/launch-radio-countdown.wav` bound in `MissionAudioPresenter.prefab`; credited on the Sources page |
| Tether rope on the spacewalk | **Yes** | `EvaTetherRope` installed in Bootstrap (`cbb9140`) |
| Physical meal from a ration pouch | **Yes** | `MealPresenter` installed in Bootstrap |
| Sleep strapped into the berth | **Yes** | berth sequence (approach, straps, hold, wake, unbuckle) in `I01MissionComposition`; R22 sleep guard |
| Rescue ending: craft, visible docking, voices VO24a/b | **Yes** | `RescueEndingPresenter` + both clips bound in Bootstrap (`19105a3`, `d7d00e9`) |
| Ending themes "Thirty-One Dawns" / "Last Carrier" | **Yes** | bound in `MissionAudioPresenter.prefab` |
| EPIC globe on the terminal + Slovakia Terra MODIS inset | **Yes** | `MissionTerminal.prefab`: `staticImageMode: 0`, inset texture set |
| Sources page names the synthetic voices and the NASA SVS sky | **Yes** | `UI/Report/Runtime/FlightRecorderPresenter.cs` lines 410–423 |
| Horizons line during play, radiation estimate (MSL RAD), DSN complex in view, day-14 console reveal, stars on the spacewalk, hull V2 / capsules V02 / launch VFX | **Not in the RC** (offered for a B build) | switch sentences below |

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

Short version (60 words, 380 characters), if a limit applies:

```text
A first-person survival game where the mission you design is the mission you must survive. Pack a fictional Mars ship within mass, budget, power and supply limits, then survive the accident your choices shaped. NASA/JPL Horizons distances set the light-speed radio delay; NASA life-support values drive oxygen, CO2, water and food. A mission record explains why you lived or died.
```

## 3. Link to Project Demo

The form takes **one** link. Apoliak decides at 23:00 CET which variant is used. **No video is approved, uploaded or linked at this time.**

Variant A — slides (ready now, [deck/](deck/)):

```text
[PUBLIC LINK to OUT-OF-REACH_SpaceApps2026_deck.pdf — 7 slides including the title. No login, permission or registration. Test it in a private browser window.]
```

Variant B — video (only if Apoliak approves a cut at 23:00 CET; 30 seconds or less, nobody on camera, no voice-over by a team member):

```text
[PUBLIC LINK to the video of 30 seconds or less. No login, permission or registration. Test it in a private browser window.]
```

Steps for both: [submission-checklist.md](submission-checklist.md).

## 4. Link to Final Project

```text
[PUBLIC LINK — the playable Windows build plus README, credits and source. No login, permission or registration.]
```

Do **not** simply make the working repository public: it would expose issue #7 and unlicensed or unwatermarked material in `docs/` (see [credits-and-licenses.md](credits-and-licenses.md) §B10). Options are in [submission-checklist.md](submission-checklist.md).

## 5. Detailed Project Description

Guide questions: *What exactly does it do? How does it work? What benefits does it have? What do you hope to achieve? What tools, coding languages, hardware, or software did you use?* (1,425 words, 8,494 characters)

```text
THE IDEA
Mission design is usually taught as a spreadsheet: mass, power, budget, communications and supplies on paper. OUT OF REACH makes you live inside your own spreadsheet. You are Houston, the only crew member of AURORA-7, a small fictional research ship bound for Mars. You pack it yourself. Then something goes wrong, and the only resources you have are the ones you chose.

WHAT THE PLAYER DOES
1. Design the mission. At launch complex LC-7 you carry crates one at a time onto the transport: oxygen tanks, water tanks, food lockers, battery modules, solar wings, spare-parts kits and a 90 kg science spectrometer. The game enforces a 600 kg payload limit, an 800-credit budget, electrical load versus generation and supplies for 30 crew-days. Heavy crates slow you down. In the timed mode you have 60 seconds of real time; if your manifest is invalid when time runs out, the last valid one launches, and a crate still in your hands stays on the pad. A practice mode has no deadline. There is no single right answer: a science-focused load (546 kg, 720 credits) carries exactly the 7 spare parts the three repairs will need, while a redundancy load (591 kg, 690 credits) gives up the science objective for more battery, oxygen and 13 spare parts.
2. Launch. You board, strap in and ride a vertical launch from the pad while the radio carries a real NASA launch countdown, then the story cuts ahead to "LATER, ABOARD AURORA-7".
3. The accident. A routine cockpit check ends in a debris strike: power drops to 20 percent, life support and the antenna fail, and cabin CO2 starts to climb.
4. Stabilize. You repair power and life support with finite spare parts and battery energy. Every prompt states the exact cost, or the exact reason an action is refused, and a refused action spends nothing.
5. Spacewalk. You suit up (2 h 20 min of preparation, like the ISS prebreathe), seal the inner hatch and depressurize the airlock (the two hatches are never open together), follow the tether rope along the hull to the antenna, repair it and return before the suit's 3-hour oxygen supply runs out. A solo spacewalk is itself an emergency: real procedures require two crew members.
6. Call Earth. At the "Earth archive" terminal the globe you turn with your glove is a real NASA EPIC photograph from the DSCOVR spacecraft, labelled with its observation time and its age on the mission clock; an inset shows Slovakia, the team's home, as NASA's Terra satellite saw it on 14 November 2026. You get one distress call. The console shows Earth's real distance on that mission day and the earliest possible reply: on day 1 Earth is 197,707,976 km away, so the reply cannot arrive before 31 min 59 s of mission time; leave the call until the day-9 chapter and the wait is still more than 30 minutes. Your reserves keep draining while you wait.
7. Survive 30 days. The story runs in chapters on mission days 1, 5, 9, 14, 20 and 30. You eat a packaged ration from its pouch, drink from the actual water store and sleep up to 8.5 hours strapped into the crew berth. Ordinary days pass as a short montage that cuts between the galley, the water store and the berth, shows the elapsed days, spends real food, water, oxygen and power, and stops at the first danger. Radio traffic reaches you on a handheld radio that rises into view. On day 5 you inspect the damage and decide: if you carried the science instrument, salvage it for spare parts or preserve the science; otherwise build a small grow module or conserve its parts. On day 14 the radio calls you to the console for Mission Control's announcement: the first rescue attempt has failed. It was sent from Earth and is delayed by the real light time of that day. On day 20 you choose between keeping your crop growing and saving power. On day 30 a rescue craft that was already in transit nearby asks for your beacon, and you must power it within an 8-hour window.
8. Outcome and record. If you held on, you watch the rescue arrive in first person: the craft appears at the cabin window against NASA's Deep Star Maps sky, closes in on the aft docking camera, makes contact, captures and hard-docks, and the hatch opens. If not, the loss is stated with its cause, for example "Carbon dioxide overwhelmed the cabin". A mission record then rolls through your decisions and the science behind them, the Sources page separates NASA data, music and art, and game assumptions, and you can restart with a different loadout.

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
The fictional parts are labelled: AURORA-7, its location (the Mars-centre distance is a documented proxy, not a computed trajectory), the rescue story, the catalogue prices and the time compression. Space is silent outside: you hear only the radio and what the structure carries. No NASA logo or insignia appears anywhere.
More than 460 automated EditMode tests and 150 PlayMode scene tests pass on the release candidate (resource accounting, light-time timing, airlock safety, chapter routine stops, the rescue sequence, the radio beats, quit and pause), plus native Unity captures, scripted full-mission runs driven through the game's real input path, and repeated human playtests.

WHAT WE HOPE TO ACHIEVE
A complete mission that a student or a curious player can finish in about half an hour, lose for an understandable reason, and replay with a better design. Next we want to add team play for up to four people with separate roles (flight, engineering, communications, life support), new destinations and scenarios, a launch-date picker that refreshes the Horizons data, a side-by-side comparison of mission designs, and teacher notes. Spanish and French text already exists on a development branch.

TOOLS, LANGUAGES, HARDWARE, SOFTWARE
Unity 6000.3.28f1 with the Universal Render Pipeline, Input System, Timeline, UI Toolkit and the Unity Test Framework; C#; Windows x64. Blender 5.3 and 3.6 driven by Python for the original 3D models; Python 3 with NumPy and Pillow for procedural textures and data checks; FFmpeg for audio; Git, Git LFS and GitHub. AI coding assistants Claude Code (Anthropic) and OpenAI Codex helped write code, procedural assets, original score cues and documentation under our direction; all dialogue uses synthetic voices from Google Gemini text-to-speech. Third-party music, models, textures and fonts are credited in the references. Developed and tested on Windows PCs.
```

Short version (543 words, 3,227 characters), only if the form refuses the full text:

```text
WHAT IT DOES
OUT OF REACH is a first-person space-survival game in which the mission you design is the mission you must survive. You are Houston, the only crew member of AURORA-7, a fictional research ship bound for Mars.
1. Design: at launch complex LC-7 you carry crates onto the transport within real limits (600 kg payload, 800 credits, power load versus generation, supplies for 30 crew-days) in 60 seconds. A science load carries a 90 kg spectrometer and exactly the spare parts the repairs will need; a redundancy load trades science for battery, oxygen and parts.
2. Launch with a real NASA countdown on the radio, then later, deep in space, a debris strike: power 20 percent, life support and antenna down, CO2 rising.
3. Repair with the parts and energy you packed. Every prompt shows the exact cost; a refused action spends nothing.
4. Spacewalk to the antenna: preparation, airlock, tether rope, a 3-hour suit oxygen supply.
5. Call Earth once from a terminal that shows a real NASA EPIC photograph of Earth. On day 1 Earth is 197,707,976 km away, so no reply can arrive before 31 min 59 s of mission time (still over 30 minutes on day 9); reserves drain while you wait.
6. Survive 30 days in chapters (days 1, 5, 9, 14, 20, 30): meals, sleep in the berth, ordinary days as a short montage, hard choices, a day-14 announcement on your handheld radio delayed by real light time, and a rescue window you must power.
7. A rescue craft docks before your eyes under NASA's Deep Star Maps sky, or the loss is stated with its cause. A mission record explains why; restart with a different design.

WHAT PLAYERS LEARN
Mass, power, money and consumables compete. Communication is limited by light speed. Life support is several separate jobs: with scrubbers down, CO2 reaches the ISS emergency level in about 20.5 hours, long before the oxygen runs out. Spacewalks cost time, oxygen and risk. Time itself is a consumable.

HOW IT WORKS
A deterministic C# simulation owns one mission clock and one resource ledger in real units; spending is all-or-nothing. An offline NASA/JPL Horizons snapshot (32 daily Earth-Mars vectors, SHA-256 checked) gives distance / speed of light, both ways, plus 600 s of assumed processing. NASA BVAD rates (0.84 kg O2, 1.01 kg CO2, 2.5 kg water, 1.83 kg food per crew-day) drive consumption; cabin CO2 uses the ideal gas law; spacewalk costs follow BVAD EVA values. Time compression is always shown; the routine montage never skips a danger. AURORA-7, its position proxy, the rescue and the prices are labelled fiction. More than 460 automated EditMode tests and 150 PlayMode scene tests pass on the release candidate, plus human playtests.

WHAT WE HOPE TO ACHIEVE
A complete half-hour mission that students can lose for an understandable reason and replay with a better design; next, team play for up to four roles, new destinations, a launch-date picker with fresh Horizons data and teacher notes.

TOOLS
Unity 6000.3.28f1 (URP), C#, Windows x64; Blender and Python for original 3D models; NumPy, Pillow, FFmpeg, Git LFS. AI coding assistants Claude Code and OpenAI Codex under our direction; dialogue voices by Google Gemini text-to-speech. Third-party assets are credited in the references.
```

Sources: player flow, limits and presets — `Assets/_OutOfReach/Scripts/Core/README.md` ("C05-A", "AURORA-7 catalog and limits", "Emergency model", "C03", "C04", "CORE-CHAPTER-LOOP"); chapter days and the 8-hour beacon window — `Scripts/Core/MissionChapterFlow.cs`, `SurvivalRecovery.cs` (`BeaconWindowHours = 8`); practice/timed wording — `UI/Runtime` strings ("Practice has no deadline"); countdown — `Audio/LaunchRadio/README.md`; tether — `Visuals/Props/EvaTetherV02/README.md`; meal — `Scripts/Presentation/Meal/README.md`; berth — `Scripts/Composition/Runtime/I01MissionComposition.cs` (SLEEP-SEQ); rescue ending and its star field — `Scripts/Presentation/Rescue/README.md`, `RescueEndingSky.cs` (exposure 0.45 instead of the saved 0.15); handheld radio and day-14 announcement — `docs/art-direction/radio-v01/README.md`, `Scripts/Presentation/Radio/Voices/PROVENANCE.md`; routine montage — `Scripts/Presentation/Routine/RoutineStationMontage.cs`, #7 6901248994; EPIC globe and Slovakia inset — `Visuals/Props/MissionTerminal/Data/ATTRIBUTION.md`, `Scripts/Presentation/Terminal/PhysicalEarthTerminalPresenter.cs`; Sources page — `UI/Report/Runtime/FlightRecorderPresenter.cs`; Horizons — `NASA-DATA.md`; BVAD — `Scripts/Simulation/CrewMetabolicAssumptions.cs`; tests and build — #7 6900862397 (`8665183`); full native route with 0 game errors — #7 6900345249 (`cbb9140`); localization — `feature/localization` @ `1cc011d`.

## 6. Use of Super Intelligence (SI)

The guide says this answer is for data collection and is **not shown to judges**, but judges see the SI indicators inside the project (watermarks, descriptive text, metadata). It also says SI must not be used to come up with the idea. Full labelling plan: [ai-disclosure.md](ai-disclosure.md).

```text
Yes. We used SI tools as implementation accelerators under the team's direction. The idea, the story, the mission premise and every design decision came from the two team members, who directed, reviewed, playtested and approved all work.

Code: Claude Code (Anthropic; models Claude Opus 5.5, Claude Fable 5.2 and Claude Sonnet 5.5) wrote most gameplay and simulation code, movement, body animation, the chapter loop, the tether, the meal, the handheld radio, the routine montage, the rescue sequence and the tests, and did the final integration. OpenAI Codex wrote most presentation, UI, scene composition, audio runtime and build tooling, and most documentation. Claude-written commits carry a "Co-Authored-By: Claude" trailer in the git metadata; the project README states the rest.

3D models, textures and drawings: our original meshes, textures and the sketched title cards were produced by code and by Python scripts for Blender and NumPy/Pillow that these assistants wrote. No generative image or 3D model produced any in-game asset. Third-party CC0 models and textures are credited separately.

Audio: all spoken dialogue, including the Mission Control and rescue-crew radio lines, uses synthetic voices generated with Google Gemini text-to-speech (models gemini-3.8-flash-tts and gemini-3.8-flash-lite-tts, stock voices Orus and Kore); no real person's voice was cloned. This is acknowledged in text on the in-game Sources page ("Synthetic voices - Google Gemini text-to-speech ... AI-generated audio"), in the project credits and README, and in metadata: the audio manifests and provenance files that travel with the clips record the model, voice, transcript, edits and SHA-256 of every clip. The original score cues, including the two ending themes "Thirty-One Dawns" and "Last Carrier", and all synthesized sound effects (including the radio tones) were composed and generated by scripts written by the AI assistants (procedural synthesis, no samples), recorded in provenance files next to the audio and acknowledged on the Sources page. The licensed music is by the human composer Scott Buckley (CC BY 4.0); the percussion added to the opening mix was synthesized by such a script. The launch countdown is a real NASA recording, not generated.

Images: art-direction concept boards were generated with the built-in GPT image generator in Codex. They are not in the game, the slide deck or anything we publish.

Data: NASA data are used unmodified; the scripts that download, verify and convert them were written by the AI assistants and are recorded in the data manifests.

Submission materials: these texts and the slide deck were drafted with Claude Code from the project's own documentation and edited by the team.
```

## 7. NASA Data

Guide: *Provide specific details about what NASA data and resources you used. To be eligible for a Global Award, you must use at least one NASA data or resource.*

```text
1. NASA/JPL Horizons ephemeris (https://ssd-api.jpl.nasa.gov/doc/horizons.html). Query: Mars body centre (499) relative to Earth centre (500@399), VECTORS, km and km/s, ICRF, UT, geometric, daily 2026-11-14 to 2026-12-15 at 08:00 UTC (32 samples). The snapshot ships offline and is checked by SHA-256 at mission start. Mechanic: Earth's distance on the mission date gives the one-way light time; the earliest reply is twice that plus 600 s of game-assumed processing (197,707,976 km and 31 min 59 s on day 1). The same model times Earth's day-14 message. Reserves drain during every wait, so real orbital geometry decides whether you survive. The Mars-centre distance is a labelled proxy for the fictional ship, not its trajectory.

2. NASA Life Support Baseline Values and Assumptions Document, NASA/TP-2015-218570/REV2 (https://ntrs.nasa.gov/citations/20210024855). Per crew-day: oxygen 0.84 kg and CO2 1.01 kg (Table 3-23), water 2.5 kg (Table 4-20), food 1.83 kg (Table 4-40). Cabin CO2 and O2 levels (Table 4-1, section 4.1.1) anchor the CO2 and oxygen losses; EVA prebreathe, suit purge, suit oxygen use and airlock losses (section 4.6) set the spacewalk costs.

3. NASA EPIC / NOAA DSCOVR Earth image (https://epic.gsfc.nasa.gov/). The cockpit "Earth archive" terminal shows the unmodified natural-colour image epic_1b_20261111110606, observed 2026-11-11 11:01:17 UTC, with its credit, its observation time and its age computed from the mission clock. It is labelled as an archived observation, not a live feed or a view from the fictional ship.

4. NASA GIBS / ESDIS, MODIS on NASA's Terra satellite (https://worldview.earthdata.nasa.gov/): the corrected-reflectance true-colour image of Slovakia of 2026-11-14 (Terra pass 08:15-08:20 UTC), unmodified, shown as an inset on the same terminal with its pass time. We acknowledge the use of imagery provided by services from NASA's Global Imagery Browse Services (GIBS), part of NASA's Earth Science Data and Information System (ESDIS).

5. NASA audio: two unedited passages of the Apollo 11 launch commentary from nasa.gov ("Ignition sequence start ... We have a liftoff." and "Tower cleared. We got a roll program."), band-limited to radio quality, as the radio countdown of the fictional launch. The line naming the mission is omitted. No endorsement is implied.

6. NASA crew food and sleep practice: NASA Crew Systems (eight-hour sleep schedule, wall-attached sleeping bags), NASA Medical Operations (8.5-hour sleep opportunity = our maximum rest), OCHMO-TB-041 Sleep Accommodations (restraints, ventilation, CO2), NASA Food in Space and the Artemis II menu (ready-to-eat, rehydratable and thermostabilized foods), NASA Veggie (plants only supplement the packaged diet; nominal 33-day harvest age, about 70 W). These shape the meals, the berth with its restraints, the rest rules and the optional grow module.

7. NASA Scientific Visualization Studio, Deep Star Maps 2020 (https://svs.gsfc.nasa.gov/4851/), with Gaia DR2 star data from ESA/Gaia/DPAC: the space sky. The source image is unmodified; the game shows it at an authored exposure, brighter in the rescue finale so that the Milky Way stands behind the docking.

8. NASA/JPL-Caltech Mars colour map from Viking images processed by USGS (https://science.nasa.gov/3d-resources/mars/): Mars in the recovery cinematic.

9. NASA Environmental Control and Life Support Systems (ECLSS) and NASA "Mars Communications Disruption and Delay": background for treating oxygen supply, CO2 removal and water as separate jobs, and for refusing any instant reply or rescue.

10. NASA General Mission Analysis Tool (GMAT, Apache-2.0), a resource listed on this challenge: we converted its star catalogue into a six-face sky during development; the final build uses the SVS map instead.

AURORA-7, launch complex LC-7, the crew and the rescue are fictional. Mission time is compressed and always labelled. No NASA logo, flag or insignia appears in the game or in our demo.
```

Sources: [credits-and-licenses.md](credits-and-licenses.md) §B2, §B5; `NASA-DATA.md` (rows NASA-SVS, NASA-GMAT); `docs/data/horizons/manifest.json`; `docs/APPROVED-CHAPTER-LOOP-2026-11-15.md` ("Verified real-world basis"); `Visuals/Props/MissionTerminal/Data/ATTRIBUTION.md` (EPIC and GIBS); `Assets/StreamingAssets/ThirdParty/NASA-Audio/NOTICE.txt`; `Visuals/Models/Habitat/ATTRIBUTION.md`; `Visuals/Environment/NasaDeepSkyV01/NOTICE.txt`; `Visuals/Celestial/RecoveryMars/SOURCE.json`. Sky binding at `cf2224d`: `NasaDeepSky.mat` is referenced by Bootstrap, `DistantStars.mat` (GMAT) by nothing.

## 8. Space Agency Partner Data and Other References

Guide: *List all of the data, resources, and tools used … including any code, text, and images (even if they are open source or freely available)… If you are using any copyrighted materials, make sure you have permission.*

```text
SPACE AGENCY PARTNER DATA
- ESA Gaia DR2 (ESA/Gaia/DPAC), included in the NASA SVS Deep Star Maps 2020 sky.
- NOAA DSCOVR (the spacecraft carrying NASA's EPIC camera): the Earth image on the terminal.
- ESA, "An astronaut's rhythm" (https://www.esa.int/articles/an-astronaut-s-rhythm): light cues for the sleeping berth (reference only).
- ESA, "How loud is space?" (https://www.esa.int/ESA_Multimedia/Videos/2020/04/How_loud_is_space/%28lang%29): our rule that sound in vacuum is only heard through the suit, the hull or the radio (reference only).

MUSIC (CC BY 4.0, https://creativecommons.org/licenses/by/4.0/)
- "Titan" by Scott Buckley, https://www.scottbuckley.com.au/library/titan/ : adapted excerpt (source 48-132 s) with added percussion (synthesized by AI-written code), gain staging and mastering.
- "Cobalt" by Scott Buckley, https://www.scottbuckley.com.au/library/cobalt/ : edited 120-second loop.

SOUND RECORDINGS
- NASA audio: Apollo 11 launch commentary, two excerpts (https://www.nasa.gov/wp-content/uploads/2015/01/590320main_ringtone_apollo11_countdown.mp3). Not copyrighted in the United States; credit NASA; no endorsement implied. Every other sound effect is our own synthesis.

3D MODELS, SKIES AND TEXTURES (CC0 1.0)
- Poly Haven (https://polyhaven.com/license): Power Box 01 (Rico Cilliers, Yann Kervran); flood lights and portable generator (James Ray Cock); long-life food (Mia Pecina Zorko); coast land rocks 04 (Rob Tuytel, Rico Cilliers); Namaqualand boulder 02 (Greg Zaal, Rico Cilliers); combination wrench and screwdrivers 02 (BKS); adjustable wrench and metal toolbox (Mateusz Sadek); pliers (PierreB3D); vintage spacecraft instrument (Michał Wiśniewski); medical box (Ulan Cabanilla); binoculars (Derek Wight); coast sand 02 (Rob Tuytel); gravelly sand (Dario Barresi); grass medium 02 and shrub 04 (Rico Cilliers); Rocky Ridge Pure Sky HDRI (Greg Zaal, Jarod Guest).
- ambientCG (https://docs.ambientcg.com/license/): Ground109, Ground110, Metal009, Metal027.
- MakeHuman base mesh, skeleton and weights (https://www.makehumancommunity.org/about/license.html).

FONTS (SIL Open Font License 1.1 unless noted)
- Barlow Condensed (The Barlow Project Authors), Source Sans 3 (Adobe), Inter (The Inter Project Authors, rendered into textures), DejaVu Sans Mono (Bitstream, Inc. and DejaVu contributors, Bitstream Vera/DejaVu licence, rendered into textures).

AI SERVICES WHOSE OUTPUT IS IN THE PROJECT
- Google Gemini text-to-speech (gemini-3.8-flash-tts and gemini-3.8-flash-lite-tts; voices Orus, Kore), under the Gemini API Additional Terms: all dialogue.
- Claude Code (Anthropic) and OpenAI Codex: code, procedural asset scripts, original score cues and sound effects (synthesized by their scripts), documentation. Built-in GPT image generation: art-direction concept boards only (not in the game).

ENGINE, LIBRARIES AND TOOLS
Unity 6000.3.28f1 (Universal Render Pipeline 17.3.0, Input System 1.20.1, Timeline 1.8.13, Unity Test Framework 1.6.0, UI Toolkit), Blender 5.3 and 3.6, Python 3 (standard library, NumPy, Pillow), FFmpeg, Git, Git LFS, GitHub.

OTHER REFERENCES
- NASA Space Apps 2026 challenge "Space Mission Design Game" and its resources (GMAT).
- Real spacecraft (ISS, Orion, Skylab) and launch complexes were used as proportion and function references only; no NASA model, drawing, photograph, logo or insignia was copied.

PERMISSIONS
Every third-party asset above is CC0, CC BY 4.0 (attributed), SIL OFL 1.1, Bitstream Vera/DejaVu, Apache-2.0, a NASA, NOAA or ESA public-reuse release (credited, no endorsement implied) or an AI output used under the provider's terms. The game uses no copyrighted material without permission.
```

Sources: [credits-and-licenses.md](credits-and-licenses.md) (every row has its repository file).

---

## Switch sentences for the final SHA

`<SHA>` is the SHA that CLAUDE-MAX or Alex declares final on #7. Run the checks in any clone after `git fetch origin`. **If the release candidate `5479e87` ships unchanged, nothing below applies: paste the blocks as they are.**

### Already in the paste blocks (in the RC `5479e87`) — undo only if the final SHA lost the feature

| # | Feature in the texts | Check on the final SHA | Undo if the check fails |
|---|---|---|---|
| A1 | Handheld radio | `git grep -l 89f13dffac652ce458889e3acbbbc56b <SHA> -- "Assets/*.unity"` prints `Bootstrap.unity` | §5 item 7: delete *"Radio traffic reaches you on a handheld radio that rises into view."* and replace *"On day 14 the radio calls you to the console for Mission Control's announcement: the first rescue attempt has failed. It was sent from Earth and is delayed"* with *"On day 14 a message arrives that the first rescue attempt has failed; it was sent from Earth and is delayed"*. Short version item 6: delete *"on your handheld radio"*. |
| A2 | Finale stars | `git ls-tree -r --name-only <SHA> \| grep RescueEndingSky.cs` prints the file | §5 item 8: delete *" against NASA's Deep Star Maps sky"*; short version item 7: delete *" under NASA's Deep Star Maps sky"*; §7 item 7: delete its second sentence; deck slide 3, third caption → "Day 30: the rescue craft hard-docks on the aft hull camera". |
| A3 | Routine montage | `git ls-tree -r --name-only <SHA> \| grep RoutineStationMontage.cs` prints the file | §5 item 7: *"as a short montage that cuts between the galley, the water store and the berth, shows"* → *"as a compressed routine that shows"*; "HOW IT WORKS" and the short version: *"routine montage"* → *"daily routine"*; short version item 6: delete *"ordinary days as a short montage, "*; deck: `ROUTINE_MONTAGE_SHIPPED = False`. |
| A4 | Launch tower V03 | — | No text depends on it. Deck slide 3, first picture shows tower V03; fine either way (a real capture). |

### Offered for a possible B build — add only if the check passes on the final SHA

| # | Feature | Check on the final SHA | If it passes, edit |
|---|---|---|---|
| S1 | Day-14 announcement **spoken** (VO23, day-neutral cut). In the RC the binding is cleared: the announcement ships as radio subtitles until Alex approves the cut | `git grep -l c4a8e83073e24d17a5d9a27e3aceb9e8 <SHA> -- "Assets/*.unity"` prints `Bootstrap.unity` | **Nothing has to change:** the paste blocks say "Mission Control's announcement", true for a voice and for subtitles alike. Optional, only if bound: §5 item 7 *"for Mission Control's announcement"* → **"for Mission Control's spoken announcement"**. If the **original** clip is bound instead (`git grep -l 95620279f55e4dfda9df3f09c1503265 <SHA> -- "Assets/*.unity"`), it says "mission day thirty-one" while the rendezvous is day 30: report it on #7 before submitting. |
| S7 | **Horizons line during play** (`feature/apx-mission-data-2315`, commits `6b69313` + `c158a10`, #7 6901373730) | `git grep -n "ConfigureMissionData" <SHA> -- Assets/_OutOfReach/Scripts/Composition/Runtime/I01MissionComposition.cs` prints a line | §5 item 7, after the sentence that ends *"…and stops at the first danger."* add: **"Between chapters a line on the next-chapter strip keeps the NASA/JPL Horizons numbers in view: Earth's distance for that mission day and the one-way signal time."** §7 item 1, before *"The Mars-centre distance is a labelled proxy"* add: **"During play the same distance and one-way light time stay visible on the next-chapter strip, labelled NASA/JPL Horizons."** Deck slide 4, first block: add **"The same numbers stay on screen between chapters."** |
| S8 | **Radiation estimate line** (same branch, commit `119a083`) | `git grep -n "RadCruiseMilliSievertsPerDay" <SHA> -- Assets/_OutOfReach/UI` prints a line | New item in §7, after item 5 (renumber the rest): **"NASA/JPL Mars Science Laboratory, Radiation Assessment Detector (RAD): inside the spacecraft on its cruise to Mars, RAD measured an average of 1.8 millisieverts of galactic cosmic rays per day (NASA/JPL, 'Data From NASA Rover's Voyage to Mars Aids Planning', 30 May 2013, https://www.jpl.nasa.gov/news/data-from-nasa-rovers-voyage-to-mars-aids-planning/; Zeitlin et al., Science 340, 2013). The next-chapter strip shows a labelled estimate of the dose since day 1: elapsed mission days x 1.8 mSv per day. It is information only: no rule, resource or outcome reads it, and AURORA-7's shielding and the solar cycle are not modelled."** §5 "HOW IT WORKS", after the BVAD paragraph add: **"A radiation line estimates the dose since day 1 from the 1.8 mSv per day that NASA's RAD instrument measured on the Mars Science Laboratory cruise; it is information only."** Credits §B5: a row with the same source. Deck slide 4 "ALSO FROM NASA": add "Radiation estimate: NASA MSL RAD cruise measurement". **Also check that the in-game Sources page got its RAD entry** (the lane proposed the text); if not, say so on #7: a NASA number on screen needs its source in the game. |
| S9 | **DSN complex in view** (lane `dsn-calc`; not READY at 22:15 CET) | the lane's READY post, and its code is in the final SHA | New item in §7 (NASA Deep Space Network: which complex is in view, how it is computed, source URL from the lane's post). One sentence in §5 item 6. Credits §B5 row. Deck slide 4 "ALSO FROM NASA". Wording and numbers only from the lane's post and provenance file. |
| S5 | Day-14 console reveal | the integrator's final post lists it | §5 item 7, after the day-14 sentences add: **"The cockpit console shows when it was sent and how long it travelled."** Only if that is what the console shows; otherwise write what it shows, or leave it out. |
| S10 | Stars during the spacewalk and in the "LATER IN EARTH ORBIT" shot; hold-to-skip for cinematics; hull V2, capsules V02, launch VFX | the integrator's final post lists them | No text change. Re-check the credits re-sweep command below for new attribution files (new models). |
| S11 | Sources credit wording for the sky (`codex/nasa-2330-sky-credit@bb2dcec`) | — | No text change: §7 item 7 already says the image is shown at an authored exposure. |

### Always re-run on the final SHA (2 minutes)

```text
git show <SHA>:Assets/_OutOfReach/Scenes/Bootstrap.unity | grep -n "chapterLoop"        (must print "chapterLoop: 1")
git diff --name-status 5479e87 <SHA> | grep -iE "attribution|provenance|notice|licen|readme|SOURCE.json"   (new third-party items go to section 8 and credits block A)
git show <SHA>:Assets/_OutOfReach/UI/Report/Runtime/FlightRecorderPresenter.cs | grep -n "Synthetic voices\|Deep Star Maps"   (both must still be there)
```

Run on the RC `5479e87` at 22:15 CET: `chapterLoop: 1`; radio in Bootstrap; `RescueEndingSky.cs` and `RoutineStationMontage.cs` present; both Sources lines present; VO23 not bound (neither cut); no new attribution file since `5ea2c23`.

Test counts: the texts say "more than 460 EditMode and 150 PlayMode" (the RC has 499 and 155, #7 6901385202). They need a change only if the final counts are lower.

If `chapterLoop` is `0` (not expected): in §2 replace *"Across 30 compressed days you eat, sleep, decide and wait for a rescue that is never instant."* with *"After contact you ration food and water, rest and power a beacon for a rescue that is never instant."*; in §5 replace item 7 with *"7. Survive after contact. Contact is the midpoint, not the win: you eat and drink from the actual stores, can salvage the science instrument for spare parts, may build and harvest one small grow module, sleep up to 8.5 hours per rest, and power a recovery beacon when a fictional rescue craft that was already in transit asks for it."* and delete *"The same model times the day-14 message."* and the "six hours per second in the routine montage" clause.
