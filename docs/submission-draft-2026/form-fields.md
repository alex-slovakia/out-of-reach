# Submission form fields — paste-ready drafts

**DRAFT ONLY.** Not submitted, not published. Source snapshot: `915a194` [git log]. Each field has a paste block (plain English, no repository tags) followed by the committed sources for every factual claim in it. Replace every `[PLACEHOLDER]`; remove or fix any sentence the human playthrough ([playthrough-checklist.md](playthrough-checklist.md)) does not confirm.

## Confirmed rules (from the official guide)

Official Project Submission Guide, https://www.spaceappschallenge.org/resources/project-submission-guide/ , dated **13 Nov 2026**, read **14 Nov 2026** (relayed by the team coordinator; not re-fetched for this draft).

- Fields: Project Name; High-Level Project Summary; Link to Project Demo; Link to Final Project; Detailed Project Description (what it does, how it works, benefits, what you hope to achieve, tools/coding languages/hardware/software); Use of Super Intelligence (SI) (data collection only, not shown to judges; judges see SI indicators in the project); NASA Data (at least one NASA data/resource for Global Award eligibility); Space Agency Partner Data and Other References (all data, resources and tools including code, text and images, even if open source; permission for copyrighted material).
- Everything in English. Demo: video ≤30 s (other spoken language only with English subtitles) or ≤7 English slides including title; exceeding the limit makes the project ineligible for Global Judging. Demo and final project on a public link with no login, permission or registration.
- SI: visible watermark on SI-generated images/videos; SI-generated audio, code and data acknowledged in descriptive text and metadata; no use or modification of NASA branding; no images/video/audio of persons under 18.
- Deadline: Sunday 15 Nov 2026 23:59, Team Owner's local time of residence; "Submit for Judging" plus the "Terms and Conditions" and "Confirm All Work is Original" boxes; edits/resubmits allowed until the deadline.

## Assumptions / team decisions

- Both links are **team-owner decisions**. The repository is private [APOLIAK-CLAUDE-MASTER-PROMPT.md §1; TASKS.md F00]; this draft does not propose hosting, visibility changes or uploads.
- Field length limits are not part of the relayed rules: TO CONFIRM on the form itself.
- The survival chapter and RECOVERED ending are enabled in the saved scene from `85b0994` [docs/verification/i06-habitat-contact-native/README.md] and covered by automated tests, but no human playthrough of them is recorded. Keep the sentences that mention them only if the playthrough confirms them in the submitted build.
- Team member names: [MEMBER 1], [MEMBER 2] (team decides how to credit).

---

## 1. Project Name

```text
OUT OF REACH
```

Sources: [README.md; PRODUCT.md].

## 2. High-Level Project Summary (148 words)

```text
OUT OF REACH is a cinematic first-person survival game for the Space Mission Design Game challenge. You plan AURORA-7, a fictional research spacecraft on a Mars mission, by physically loading cargo within mass, budget, power and supply limits before a 60-second deadline. A debris strike then damages power, life support and the antenna. Your loadout decides what you can repair, whether you survive a tethered spacewalk, and how long you can wait for Earth. NASA/JPL Horizons Earth–Mars distances, used as a labelled proxy for the ship's range, set the radio delay: no reply before light makes the round trip, plus processing. NASA life-support baseline values drive oxygen, CO2, water and food use. After contact you ration supplies, grow one crop module, rest and power a beacon until a fictional rescue arrives, or the mission is lost. A final report separates NASA data, derived physics, fiction and time compression.
```

Sources:
- Genre, challenge, fictional AURORA-7, Mars mission [README.md; PRODUCT.md; APOLIAK-CLAUDE-MASTER-PROMPT.md §3].
- Physical cargo, mass/budget/power/supply limits, 60-second deadline with last-valid fallback [Assets/_OutOfReach/Scripts/Core/README.md "C05-A" and "AURORA-7 catalog and limits"; docs/verification/i04-c7cfb86/README.md].
- Debris strike damaging power, life support, antenna [PRODUCT.md; Assets/_OutOfReach/Scripts/Core/README.md "Accident"].
- Loadout consequences for repairs, EVA suit oxygen and waiting [Assets/_OutOfReach/Scripts/Core/README.md "Emergency model", "C03", "C04"].
- Horizons distance as labelled proxy; reply ≥ round-trip light time + processing [NASA-DATA.md; Assets/_OutOfReach/Scripts/Core/README.md "How the delay is computed"].
- NASA BVAD rates for O2/CO2/water/food [Assets/_OutOfReach/Scripts/Simulation/CrewMetabolicAssumptions.cs].
- Post-contact rations, one grow module, rest, beacon, fictional rescue or loss [Assets/_OutOfReach/Scripts/Core/SurvivalRecovery.cs; Assets/_OutOfReach/Scripts/Core/SurvivalExtensionSpec.cs; docs/C06-REST-RECOVERY-CONTRACT.md; Assets/_OutOfReach/Tests/PlayMode/C06EndToEndPlayTests.cs].
- Report separation [NASA-DATA.md §"Required gameplay integration" item 6; Assets/_OutOfReach/Scripts/Core/README.md "Report"].

## 3. Link to Project Demo

```text
[DEMO LINK — team-owner decision; must be publicly accessible with no login, permission or registration]
```

## 4. Link to Final Project

```text
[FINAL PROJECT LINK — team-owner decision; must be publicly accessible with no login, permission or registration. The repository is currently private.]
```

Sources: repository private [APOLIAK-CLAUDE-MASTER-PROMPT.md §1; TASKS.md F00].

## 5. Detailed Project Description

```text
WHAT IT DOES
OUT OF REACH turns mission design, mission management and mission evaluation into one first-person mission for Windows. You play HOUSTON, the lone astronaut of AURORA-7, a fictional research spacecraft on a Mars mission.

1. Plan. Carry cargo crates one at a time into the cargo bay: oxygen tanks, water tanks, food lockers, battery modules, solar wings, spare-parts kits and a science spectrometer. The game enforces a 600 kg payload limit, a fictional 800-credit budget, electrical load versus generation, and supplies for 30 crew-days. Heavy crates slow you down. In timed mode you have 60 active seconds; at zero the valid loadout, or the last valid one, launches. A crate still in your hands stays behind.
2. Lose contact. A routine cockpit check ends in an authored debris strike: power drops to 20 %, life support and the antenna fail.
3. Stabilize. Repair power and life support with finite spare parts and battery energy. Every prompt states the real cost or the exact reason an action is refused, and a refused action spends nothing.
4. Spacewalk. Put on the suit, seal the inner hatch, depressurize the airlock (the two hatches are never open together), clip the tether, follow the hull route to the antenna and repair it before the suit's oxygen runs out, then return and repressurize.
5. Call Earth. Transmit one distress call and wait. The wait is set by real NASA/JPL data and by how much power and life support you have left.
6. Survive after contact. Contact is the midpoint, not the win. Eat and drink from your actual stores, salvage the science instrument for spare parts if you need them, build, plant and harvest one grow module, rest in a berth, and power the recovery beacon when a fictional rescue craft asks for it.
7. Outcome and record. The crew is recovered or the mission is lost with its recorded cause. A short end card leads to an optional mission record with the sources, and you can restart with a different loadout.

HOW IT WORKS
A deterministic C# simulation owns the mission: one mission clock, one resource ledger (oxygen in kg, water in litres, food in kg, battery in kWh, power in W, cabin CO2 in ppm, integer spare parts) and one event bus. Spending is all-or-nothing and the presentation only displays committed state.
Time compression is explicit and shown on the HUD: ×300 during the emergency, repairs and survival, ×60 during the spacewalk and the wait for Earth, and one simulated hour per real second while resting. Pausing freezes every timer.
Communications use a packaged, offline NASA/JPL Horizons snapshot: 32 daily Earth–Mars vectors from 14 November to 15 December 2026 at 08:00 UTC. The game checks its SHA-256 at mission start, takes the last sample at or before the mission date, divides the distance by the speed of light (299,792.458 km/s) and adds 600 s of Earth processing, a labelled game assumption. On mission day 1 Earth is 197,707,976 km away: 10 min 59 s one way, earliest reply 31 min 59 s of simulated time. Reserves keep draining during the wait; a failure during the wait ends the mission. The Mars-centre distance is a documented proxy for the fictional ship's position, not a computed trajectory.
Life support uses NASA Life Support Baseline Values (BVAD REV2): 0.84 kg oxygen, 1.01 kg CO2, 2.5 kg water and 1.83 kg food per crew-day. Cabin CO2 is derived with the ideal gas law for an assumed 20 m3 cabin; loss levels follow cited ISS flight-rule and cabin oxygen levels as game endings, not medical thresholds. Suit preparation, purge oxygen, suit oxygen use and airlock losses follow BVAD EVA values, labelled as assumptions for a fictional suit.
The post-contact chapter reuses the same clock and ledger. A rest lasts at most 8.5 simulated hours (NASA's sleep opportunity) and stops at the first danger. A fictional growing kit begins with 25-day-old plants; eight further simulated days reach a nominal harvest age informed by the NASA Veggie reference, with an approximately 70 W reference equipment load. The starting age, growth success, recipe and yield remain game assumptions. The rescue is explicitly fictional: a craft already in transit arrives 12 days after contact, requests the beacon 6 days after contact, and a missed 8-hour beacon window delays it by 3 days.
The star sky uses star directions from NASA's open-source General Mission Analysis Tool (GMAT) star data (Apache-2.0). The mission record keeps NASA/JPL data, derived physics, the fictional scenario and time compression in separate sections.
The code is covered by automated EditMode and PlayMode tests and scripted full-mission scene checks (for example 274 of 274 full-mission checks on the I05 build source, 309 EditMode plus 50 PlayMode tests on a later source revision, and 579 assertions for the physical survival habitat). These are automated checks, not a substitute for human play.

BENEFITS
Engineering trade-offs become felt, not read: a science-focused loadout leaves exactly the spare parts for three repairs, while a redundancy loadout gives up the science objective for more battery, oxygen and parts. Players without aerospace training learn that deep-space communication is limited by distance and light speed, that life support is a set of separate jobs with finite reserves, and that rescue is not instantaneous. The game is honest about what is real data, what is derived physics and what is fiction. Prompts state exact costs, transmissions are subtitled, and a reduced camera-motion and light-effects option is available.

WHAT WE HOPE TO ACHIEVE
A complete, causally valid mission that a new player can launch, understand, finish with either outcome and replay with a different loadout. Next steps the team has designed but not finished: a sea-side loading and launch prologue, tool-based repair animations and wider playtesting.

TOOLS, LANGUAGES, HARDWARE AND SOFTWARE
Unity 6000.3.28f1 with the Universal Render Pipeline 17.3.0, Input System 1.20.1, Timeline 1.8.13, Unity Test Framework 1.6.0 and UI Toolkit; C#; Windows x64. Blender 5.3.0 and 3.6.1 driven by Python scripts for the 3D models; Python 3 with numpy and Pillow for procedural textures and the star sky; ffmpeg for audio finishing; the Python standard library for offline NASA data verification. Git, Git LFS and GitHub. AI tools: Claude Code (Claude Opus 5.5, Claude Fable 5.2), OpenAI Codex, built-in GPT image generation for concept boards (not shipped in the game) and Google Gemini text-to-speech for the synthetic voices. Automated verification ran on Windows PCs, including one with an NVIDIA RTX 3070.
```

Sources by paragraph:
- What it does — HOUSTON as player name, AURORA-7 fictional [docs/playtest-review/06-status-1845.md "New approved steering"; docs/research/player-facing-terminology.md; APOLIAK-CLAUDE-MASTER-PROMPT.md §3].
- 1 Plan — crate types, limits, slow-down, 60 active seconds, last-valid fallback, crate in hands not aboard [Assets/_OutOfReach/Scripts/Core/README.md "C05-A", "AURORA-7 catalog and limits", "Planning deadline"; Assets/_OutOfReach/Scripts/Composition/Runtime/I01SessionControls.cs (mode text); docs/verification/i04-c7cfb86/README.md].
- 2 Accident — cockpit check, conditions Power 0.2, LifeSupport 0, Antenna 0 [Assets/_OutOfReach/Scripts/Core/README.md "Accident"; PRODUCT.md].
- 3 Repairs — costs, prompts, atomic refusal [Assets/_OutOfReach/Scripts/Core/README.md "Repairs", "Prompts"].
- 4 EVA — suit locker, airlock, hatches never open together, tether route, suit tank, return [Assets/_OutOfReach/Scripts/Core/README.md "C03"].
- 5 Call Earth [Assets/_OutOfReach/Scripts/Core/README.md "C04"; NASA-DATA.md].
- 6 Survive — rations, water, salvage, grow module, berth, beacon [Assets/_OutOfReach/Scripts/Core/InteractionIds.cs; Assets/_OutOfReach/Scripts/Core/SurvivalExtension.cs; Assets/_OutOfReach/Scripts/Core/SurvivalRecovery.cs; docs/verification/i06-habitat-contact-native/README.md; docs/PLAYER-FEEDBACK-2026-11-14.md "contact is the midpoint"].
- 7 Outcome — end card and record, restart [Assets/_OutOfReach/UI/Report/Runtime/FlightRecorderPresenter.cs; docs/verification/i06-presentation-audio/README.md].
- How it works, simulation authority and units [Assets/_OutOfReach/Scripts/Core/README.md; APOLIAK-CLAUDE-MASTER-PROMPT.md §6–7; ARCHITECTURE.md].
- Time scales ×300/×60, pause [Assets/_OutOfReach/Scripts/Core/README.md "Clock"]; survival ×300 and rest ×3600 [Assets/_OutOfReach/Scripts/Core/SurvivalExtensionSpec.cs; Assets/_OutOfReach/Scripts/Core/SurvivalRecovery.cs]; pause freezes timers [TESTING.md A07; docs/PLAYER-FEEDBACK-2026-11-14.md].
- Horizons snapshot, SHA-256 check, sample selection, 600 s processing, day-1 values [NASA-DATA.md; Assets/_OutOfReach/Scripts/Core/README.md "How the delay is computed", "Communication assumptions"].
- BVAD values, 20 m³ cabin, ideal gas law, loss levels, EVA values [Assets/_OutOfReach/Scripts/Simulation/CrewMetabolicAssumptions.cs; Assets/_OutOfReach/Scripts/Core/README.md "Emergency model", "Sources and labels", "EVA assumptions"].
- Rest 8.5 h, Veggie values, rescue timing 12/6/8 h/3 d [Assets/_OutOfReach/Scripts/Core/SurvivalRecovery.cs; Assets/_OutOfReach/Scripts/Core/SurvivalExtensionSpec.cs]. Note: docs/C06-REST-RECOVERY-CONTRACT.md still says a 2-hour window; commit `e119907` changed it to 8 h in code.
- GMAT sky [Assets/StreamingAssets/ThirdParty/NASA-GMAT/NOTICE.txt; docs/verification/i03-fb9703b/README.md; in-game `Stars*.png` hashes equal docs/data/gmat-skybox-v01/manifest.json].
- Report sections [Assets/_OutOfReach/Scripts/Core/README.md "Report"; Assets/_OutOfReach/Scripts/Core/MissionReportBuilder.cs].
- Test counts: 274/274 [docs/verification/i05-3032686/README.md]; 309 EditMode + 50 PlayMode at `4a34a6b` [docs/verification/i06-body-habitat-candidate/README.md]; 579 assertions at `85b0994` [docs/verification/i06-habitat-contact-native/README.md].
- Benefits — loadout presets and parts [Assets/_OutOfReach/Scripts/Core/README.md "Emergency model", presets table]; audience [PRODUCT.md]; honesty labels [NASA-DATA.md]; prompts, subtitles, reduced effects [Assets/_OutOfReach/Scripts/Core/README.md "Prompts"; Assets/_OutOfReach/Audio/Production/AUDIO_HANDOFF.md; Assets/_OutOfReach/Scripts/Composition/Runtime/I01SessionControls.cs].
- Goals — definition of done [PRODUCT.md]; designed-not-finished items [docs/art-direction/review-06-final-overview/README.md; docs/playtest-review/03-walkthrough.md; docs/playtest-review/06-status-1845.md]. These are team goals, not delivered features.
- Tools — [ProjectSettings/ProjectVersion.txt; Packages/manifest.json; README.md; Assets/_OutOfReach/Visuals/Models/Aurora7/LICENSES.md; Assets/_OutOfReach/Visuals/Props/PreflightCargo/PROVENANCE.md; Assets/_OutOfReach/Audio/Production/audio-manifest.json; NASA-DATA.md; docs/prompts/README.md; git log co-author trailers; Assets/_OutOfReach/Audio/Production/AUDIO_HANDOFF.md; docs/art-direction/review-01/README.md; docs/verification/i03-fb9703b/README.md; docs/verification/i04-375c535/README.md].
- TO CONFIRM before pasting: measured play length (PRODUCT.md targets 15–20 minutes for the original slice; the survival chapter extends it); that the submitted build contains the survival chapter.

## 6. Use of Super Intelligence (SI)

Not shown to judges (data collection), but judges see SI indicators in the project [guide]. Be complete.

```text
SI was used extensively. Two AI coding agents did most of the implementation under human direction, review, playtesting and approval.

Code. Gameplay and simulation C# (mission state, planner, resources, repairs, EVA, communications, survival, report data) and its EditMode/PlayMode tests were written with Claude Code. 33 of the 95 commits in the repository carry an AI co-author trailer: 18 Claude Fable 5.2 and 15 Claude Opus 5.5. Presentation, UI, audio runtime, scene composition, build and verification harnesses, data tooling and most documentation were written by OpenAI Codex acting as the team's integrator (recommended model GPT-6 Astra, High; the exact model per session is to be confirmed). Those commits carry no AI trailer.

3D models and textures. All original meshes, UVs and textures (the AURORA-7 ship and props, astronaut hands, habitat, cargo case, first-person body garments) were produced by Python scripts in Blender and numpy/Pillow that were written by the AI agents (Claude chats for the ship, props, hands and habitat; Codex sub-agents for the cargo case and body). No generative image or 3D model is recorded for any in-game asset. The first-person body builds on the CC0 MakeHuman base mesh and rig.

Audio. All spoken dialogue uses synthetic voices generated with Google Gemini text-to-speech (model gemini-3.8-flash-tts, stock voices Orus and Kore). No real person was cloned. Music, sound effects, airlock and deck-contact sounds are original algorithmic synthesis from Python generator scripts written by the AI agents; no generative-audio model or sample library was used.

Images. Concept and target boards in the repository documentation were generated with the built-in GPT image generator. They are design references only and are not used as in-game assets.

Data. NASA/JPL Horizons values and NASA GMAT star data are unmodified NASA outputs; the scripts that fetch, verify, normalize and project them were written by the AI agents.

Other. Local speech-recognition models checked voice transcripts (model to be confirmed). A Krea text-to-3D attempt returned no output and nothing from Krea is used.

Labelling. AI voice provenance is recorded in the audio manifests. Remaining labelling work (audio file metadata, demo watermark, in-game credits) is tracked by the team.
```

Sources:
- Role split: Developer A (visual production, UI, environment, audio, cinematics, integration) with OpenAI Codex; Developer B (gameplay, simulation, tests) with Claude Code [APOLIAK-CLAUDE-MASTER-PROMPT.md §1]; Codex as integrator [TASKS.md; AGENTS.md; docs/DEVELOPMENT-OWNERSHIP-C06.md].
- 95 commits; 33 trailers (18 Fable 5.2, 15 Opus 5.5) [git log at `915a194`]. Recommended models [docs/prompts/README.md]. Exact per-session Codex model: TO CONFIRM.
- Models by scripts in Blender; Claude prop chat "Blender fork"; Claude/Fable model writer [Assets/_OutOfReach/Visuals/Models/Aurora7/LICENSES.md; .../MODEL_HANDOFF.md; .../Props/*/README.md]; habitat by scripts, no AI images [Assets/_OutOfReach/Visuals/Models/Habitat/ATTRIBUTION.md]; cargo case by Codex prop subagent [Assets/_OutOfReach/Visuals/Props/PreflightCargo/PROVENANCE.md]; body by Codex body agent on MakeHuman CC0 [docs/DEVELOPMENT-OWNERSHIP-C06.md; Assets/_OutOfReach/Visuals/Character/FirstPersonBody/FitV02/Provenance/BODY-FIT-02-README.md].
- Gemini TTS, voices, no cloning [Assets/_OutOfReach/Audio/Production/AUDIO_HANDOFF.md; Assets/_OutOfReach/Audio/Production/audio-manifest.json; Assets/_OutOfReach/Audio/Voices/Dramatic/voice-manifest.json]. Music/SFX procedural [Assets/_OutOfReach/Audio/MUSIC_HANDOFF.md; Assets/_OutOfReach/Audio/Music/README.md; Assets/_OutOfReach/Audio/Original/Airlock/audio-provenance.json; Assets/_OutOfReach/Audio/Original/DeckContacts/Editor/build_deck_contacts.py].
- Concept boards: GPT built-in generator [docs/art-direction/review-01/README.md; docs/art-direction/review-04/prompts.md; docs/art-direction/review-05-marine/README.md; docs/art-direction/review-06-final-overview/prompts.md]. Generator of docs/art-direction/approved-hud-02 board: TO CONFIRM (README says "generated concept art").
- Data scripts [NASA-DATA.md; tools/nasa/horizons_snapshot.py; docs/data/gmat-skybox-v01/README.md].
- ASR review [Assets/_OutOfReach/Audio/Production/AUDIO_HANDOFF.md] — model TO CONFIRM. Krea [docs/art-direction/review-05-marine/README.md].
- TO CONFIRM: how the body's garment texture (`Assets/_OutOfReach/Visuals/Character/FirstPersonBody/FitV02/Textures/Body_Weave_Normal.png`, "original project contribution") was made; which agent wrote the airlock cue generator (`Assets/_OutOfReach/Audio/Original/Airlock/generate_cues.py`; provenance says only "OUT OF REACH"); author of the dialogue script text; the generator of the `clean-v3` voice takes (Assets/_OutOfReach/Audio/Production/clean-runtime-selection.json lists voice Orus but no model); whether Blender MCP was used (docs/art-direction/review-01/README.md says connection unverified).

## 7. NASA Data

```text
1. NASA/JPL Horizons API — https://ssd-api.jpl.nasa.gov/doc/horizons.html
Exact query: Mars body centre (499) relative to Earth centre (500@399), VECTORS, KM-S, ICRF, UT, no light-time correction, daily 2026-11-14 to 2026-12-15 at 08:00 UTC (32 samples). Mechanic: the game packages this snapshot offline and verifies its SHA-256 at mission start. The distance on the mission date gives the one-way light time (distance / 299,792.458 km/s); the earliest reply from Earth is twice that plus 600 s of game-assumed processing (31 min 59 s on mission day 1, 197,707,976 km). Reserves drain during the wait, so the data decides whether you survive until the reply. The Mars-centre range is a labelled proxy for the fictional ship's position, not a trajectory.

2. NASA Life Support Baseline Values and Assumptions Document, NASA/TP-2015-218570/REV2 — https://ntrs.nasa.gov/citations/20210024855
Mechanic: per-crew-day oxygen 0.84 kg and CO2 1.01 kg (Table 3-23), water 2.5 kg (Table 4-20), food 1.83 kg (Table 4-40) drive consumption, cabin CO2 rise and survival; cabin CO2/O2 levels (Table 4-1, section 4.1.1) anchor the loss conditions; EVA prebreathe, suit purge, suit oxygen use and airlock losses (sections 4.6.2–4.6.3) set spacewalk costs. Rates during rest also use these values.

3. NASA Environmental Control and Life Support Systems (ECLSS) — https://www.nasa.gov/reference/environmental-control-and-life-support-systems-eclss/
Mechanic: oxygen supply, CO2 removal and water are modelled as separate jobs; a damaged life-support loop stops CO2 scrubbing and leaks oxygen. Background only; it does not calibrate our damage rates.

4. NASA Mars Communications Disruption and Delay — https://www.nasa.gov/wp-content/uploads/2024/01/mars-communications-disruption-and-delay.pdf
Mechanic: signal delay depends on distance; no instantaneous reply or rescue.

5. NASA General Mission Analysis Tool (GMAT), star graphics data — https://github.com/nasa/GMAT (commit b8aec552a8f34a9212063adc3f249bd2315397a3), Apache-2.0
Mechanic: star directions (visual magnitude up to 5.5) are projected into the six in-game sky faces; artistic exposure and point spread; stellar epoch unspecified.

6. NASA Veggie fact sheet — https://science.nasa.gov/wp-content/uploads/2023/04/veggie_fact_sheet_508.pdf
Mechanic: the nominal 33-day harvest age and approximately 70 W reference equipment load inform the fictional module. Starting with 25-day-old plants, eight further simulated days, water use, growth success and yield are game assumptions.

7. NASA Medical Operations reference — https://www.nasa.gov/reference/6-0-medical-operations/
Mechanic: one rest lasts at most the 8.5-hour sleep opportunity, on the same mission clock.

8. NASA OCHMO-TB-041 Sleep Accommodations — https://www.nasa.gov/wp-content/uploads/2023/12/ochmo-tb-041-sleep-accommodations.pdf
Use: design reference for the sleep berth (secured bag, restraints, ventilation, CO2).

The spacecraft, its rescue and its credits are fictional. Mission timing is compressed and labelled. No NASA logo or insignia is used.
```

Sources: 1 [NASA-DATA.md; docs/data/horizons/manifest.json; Assets/_OutOfReach/Scripts/Core/README.md "How the delay is computed"]; 2 [Assets/_OutOfReach/Scripts/Simulation/CrewMetabolicAssumptions.cs; Assets/_OutOfReach/Scripts/Core/README.md "Sources and labels", "EVA assumptions"; Assets/_OutOfReach/Scripts/Core/SurvivalRecovery.cs]; 3–4 [NASA-DATA.md; Assets/_OutOfReach/Scripts/Core/MissionReportBuilder.cs]; 5 [Assets/StreamingAssets/ThirdParty/NASA-GMAT/NOTICE.txt; docs/data/gmat-starfield-v01/README.md; docs/data/gmat-skybox-v01/README.md]; 6 [Assets/_OutOfReach/Scripts/Core/SurvivalExtensionSpec.cs]; 7 [Assets/_OutOfReach/Scripts/Core/SurvivalRecovery.cs; docs/C06-REST-RECOVERY-CONTRACT.md]; 8 [Assets/_OutOfReach/Visuals/Models/Habitat/ATTRIBUTION.md]; no NASA insignia [Assets/_OutOfReach/Visuals/Models/Aurora7/LICENSES.md; Assets/_OutOfReach/Visuals/Models/Habitat/ATTRIBUTION.md]. The in-game report currently cites NASA-01–03 and BVAD but not GMAT, Veggie or the sleep reference [Assets/_OutOfReach/Scripts/Core/MissionReportBuilder.cs] (gap noted in the inventory). Optional background resources (research notes only): NASA sound monitoring spec, NASA-STD-3001 Vol. 2, NASA Spaceflight Q&A, NASA spacewalk pages [docs/research/cabin-audio-and-microgravity.md; docs/research/player-facing-terminology.md]; include them only if the team wants them listed.

## 8. Space Agency Partner Data and Other References

```text
SPACE AGENCY PARTNER REFERENCES
- ESA, "An astronaut's rhythm" — https://www.esa.int/articles/an-astronaut-s-rhythm — local light cues in the sleep-berth design (reference only, no content copied).
- ESA, "How loud is space?" — https://www.esa.int/ESA_Multimedia/Videos/2020/04/How_loud_is_space/%28lang%29 — supports our audio rule: cabin sound is real, no airborne sound outside in vacuum (reference only).

THIRD-PARTY ASSETS IN THE GAME
- Poly Haven "Power Box 01" by Rico Cilliers (modelling and texturing) and Yann Kervran (rigging) — https://polyhaven.com/a/power_box_01 — CC0-1.0 (https://polyhaven.com/license); used as a ship service cabinet with a derived URP material.
- MakeHuman core base mesh, skeleton and weights, revision a8bc2d54ff0ac92e78ff71431b1023eda42bf482 — https://www.makehumancommunity.org/about/license.html — CC0-1.0 (graphical assets); basis of the first-person body.
- NASA GMAT star graphics data — Apache-2.0, notice and licence shipped with the game (see NASA Data).
- Adobe Source Sans 3 (Regular, Semibold), revision 87b37a2daaed80fcb8e8ccb0085c4d72ddade12e — https://github.com/adobe-fonts/source-sans — SIL Open Font License 1.1; UI and cargo labels.
- Inter typeface, © The Inter Project Authors — SIL Open Font License 1.1; rasterised into ship and prop label textures (font file not distributed).
- DejaVu Sans Mono, © Bitstream, Inc. and DejaVu contributors — Bitstream Vera / DejaVu licence; rasterised digits on labels (font file not distributed).

AI SERVICES WHOSE OUTPUT IS IN THE GAME
- Google Gemini text-to-speech (gemini-3.8-flash-tts, voices Orus and Kore) — Gemini API Additional Terms https://aistudio.google.com/docs/terms (reviewed 14 Nov 2026) — all dialogue.
- Claude Code (Claude Opus 5.5, Claude Fable 5.2) and OpenAI Codex — code, procedural asset scripts and documentation. Built-in GPT image generation — documentation concept boards only.

ENGINE, LIBRARIES AND TOOLS
Unity 6000.3.28f1 (Universal Render Pipeline 17.3.0, Input System 1.20.1, Timeline 1.8.13, Test Framework 1.6.0, UI Toolkit, uGUI 2.0.0; the project started from Unity's URP template), Blender 5.3.0 and 3.6.1, Python 3 (standard library, numpy, Pillow), ffmpeg 5.1.2, Git, Git LFS, GitHub. Unity's official ui-uitk agent skill (Unity-Technologies/skills) was used as guidance.

OTHER REFERENCES (DESIGN DISCUSSION ONLY, NOT IN THE GAME)
- Brendan Jowett, "I Spent 100 Hours Making a Game With Claude" — https://www.youtube.com/watch?v=tpbXZyRxWNY — selected frames reviewed for criticism and reference; no assets, music, models or text reused.
- ISS, Orion and Skylab served as proportion and function references only; no NASA model, texture, drawing, photograph, logo or insignia is included.
- NASA Space Apps 2026 challenge page — https://www.spaceappschallenge.org/2026/challenges/space-mission-design-game/

PERMISSIONS
All third-party assets above are CC0, SIL OFL 1.1, Bitstream Vera/DejaVu or Apache-2.0 licensed, or AI outputs used under the provider's terms. No copyrighted third-party media is used in the game. [TO CONFIRM: permission status for the reference frames kept in the repository documentation.]
```

Sources: ESA [Assets/_OutOfReach/Visuals/Models/Habitat/ATTRIBUTION.md; docs/research/cabin-audio-and-microgravity.md]; Poly Haven [Assets/_OutOfReach/Visuals/ThirdParty/PolyHaven/PowerBox01/ADAPTATION.md; .../manifest.json; .../LICENSE-SOURCE-EVIDENCE.txt]; MakeHuman [Assets/_OutOfReach/Visuals/Character/FirstPersonBody/FitV02/Provenance/MakeHumanCC0-manifest.json; .../MakeHuman_LICENSE.ASSETS.md]; GMAT [Assets/StreamingAssets/ThirdParty/NASA-GMAT/NOTICE.txt; .../LICENSE.txt]; Source Sans 3 [Assets/_OutOfReach/UI/Typography/FONT-PROVENANCE.json; Assets/_OutOfReach/UI/Typography/OFL.txt; Assets/_OutOfReach/Visuals/Props/PreflightCargo/PROVENANCE.md]; Inter, DejaVu [Assets/_OutOfReach/Visuals/Models/Aurora7/LICENSES.md; Assets/_OutOfReach/Visuals/Models/Habitat/ATTRIBUTION.md]; Gemini terms [Assets/_OutOfReach/Audio/Production/AUDIO_HANDOFF.md]; tools [Packages/manifest.json; ProjectSettings/ProjectVersion.txt; ASSETS.md (URP template content); Assets/_OutOfReach/Visuals/Models/Aurora7/LICENSES.md; Assets/_OutOfReach/Audio/Production/audio-manifest.json; docs/art-direction/review-01/README.md (ui-uitk skill)]; YouTube reference [docs/references/tpbXZyRxWNY/README.md; docs/references/tpbXZyRxWNY/source-metadata.json (`"license": null`)]; ISS/Orion/Skylab [Assets/_OutOfReach/Visuals/Models/Aurora7/LICENSES.md].

TO CONFIRM for this field: licences of Unity packages and the URP template content (not stated in committed files; ASSETS.md only says "retain provenance"); the project's own licence (no LICENSE file in the repository); whether the "seven videos researched locally" [docs/art-direction/review-01/README.md] beyond the one committed reference must be listed; whether Blender MCP was used; whether the challenge resource "Design and Develop Science Missions" [docs/data/gmat-starfield-v01/challenge-resources.evidence.json] informed the design.
