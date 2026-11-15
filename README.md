# OUT OF REACH

**The mission you design is the mission you must survive.**

A first-person space-survival game for Windows, made during the **NASA Space Apps Challenge 2026** hackathon (14–15 November 2026), challenge [Space Mission Design Game](https://www.spaceappschallenge.org/2026/challenges/space-mission-design-game/), by team [Polish Alex Goats](https://www.spaceappschallenge.org/2026/find-a-team/polish-alex-goats/) (Alexander Kozovy, Alex (Apoliak)).

## Play it

1. Download `OUT-OF-REACH_b82c5e7_Windows_x64.zip` from the release [OUT OF REACH — Space Apps 2026 final build](https://github.com/alex-slovakia/out-of-reach/releases/tag/spaceapps-2026-final) and unzip it anywhere.
   SHA-256: `a5457672eed2462052e33880e57d94daff6d5caa4a4ea3c8fc0380b90c695462`
2. Run `OutOfReach.exe`. No installation, no account, no network connection needed. If Windows SmartScreen warns about an unsigned app, choose **More info → Run anyway**.
3. Controls are shown in the pause menu (**Esc**). On foot: **WASD** move, **Shift** sprint, **mouse** look, **E** interact, **Tab** ship systems. In zero gravity your hands and feet push you off surfaces (mouse buttons, **Space**).
4. A full mission takes about half an hour. A reduced camera-motion and light-effects option is in the pause menu.

The 30-second demo video, the 30-second trailer, the 7-slide deck, `README.pdf` and `CREDITS.txt` are in the release [Space Apps 2026 submission files](https://github.com/alex-slovakia/out-of-reach/releases/tag/spaceapps-2026-submission).

## The mission

You are Houston, the only crew member of **AURORA-7**, a small fictional research ship bound for Mars.

1. **Design** — at launch complex LC-7 you carry the cargo yourself: oxygen, water, food, batteries, solar wings, spare parts and a 90 kg science spectrometer. Limits: 600 kg payload, 800 credits, power load versus generation, supplies for 30 crew-days, and 60 seconds in the timed mode (a practice mode has no deadline).
2. **Launch** to a real NASA countdown on the radio, then later, deep in space, **a debris strike**: power 20 %, life support and antenna down.
3. **Repair** with the parts and energy you packed. Every prompt shows the exact cost; a refused action costs nothing.
4. **Spacewalk** to the antenna: suit, airlock, tether rope, 3 hours of suit oxygen.
5. **Call Earth** once. The reply cannot arrive faster than light: 31 min 59 s of mission time on day 1, and still over 30 minutes on day 9.
6. **Survive 30 days** in chapters (days 1, 5, 9, 14, 20, 30): ration pouches, sleep strapped into the berth, ordinary days as a short montage, radio traffic on a handheld radio, hard choices, and a rescue that is never instant.
7. **The outcome**: a rescue craft docks before your eyes under a real star field, or the loss names its cause.
8. **Read the record**: why you lived or died, with the science behind each consequence. Then try another design.

## NASA data inside the game

| Data | What it does in the game |
|---|---|
| NASA/JPL Horizons Earth–Mars ephemeris (14 Nov–15 Dec 2026, packaged offline, SHA-256 checked) | Distance ÷ speed of light, both ways, plus 10 min processing = the earliest reply; it also times the day-14 message, and the range and one-way delay stay on screen during play |
| NASA Life Support Baseline Values (BVAD, NASA/TP-2015-218570/REV2) | 0.84 kg O₂, 1.01 kg CO₂, 2.5 kg water, 1.83 kg food per crew-day; cabin CO₂ levels; spacewalk costs |
| NASA EPIC / NOAA DSCOVR | The real Earth image on the cockpit terminal, observed 11 Nov 2026, shown with its age |
| NASA GIBS / ESDIS, MODIS on Terra | The inset on the same terminal: Slovakia, the team's home, on 14 Nov 2026 |
| NASA audio, Apollo 11 launch commentary (two excerpts; the mission name is omitted) | The radio countdown of the fictional launch |
| NASA crew practice (Crew Systems, Medical Operations, Food in Space, Artemis II menu, Veggie) | Berth restraints, 8.5 h maximum rest, ready-to-eat / rehydratable / thermostabilized food, optional grow module |
| NASA SVS Deep Star Maps 2020 (Gaia DR2: ESA/Gaia/DPAC) | The space sky, shown brighter in the rescue finale |
| NASA/JPL-Caltech Viking Mars map | Mars in the recovery cinematic |
| NASA Deep Space Network sites (coordinates from NASA/JPL Horizons) | The line that names the complex with Mars in view |
| NASA/JPL Mars Science Laboratory RAD (1.8 mSv per day on the cruise to Mars) | The radiation estimate shown during play, for information only |

Sources, units, epochs and assumptions are in [NASA-DATA.md](NASA-DATA.md) and on the in-game **Sources** page.

What is fiction: AURORA-7, LC-7, the crew, the rescue, prices and the time compression (always shown on screen). The Mars-centre distance is a labelled stand-in for the ship's position, not a trajectory. This is an educational game, not NASA-certified software. No NASA logos or insignia are used and no endorsement is implied. We acknowledge the use of imagery provided by services from NASA's Global Imagery Browse Services (GIBS), part of NASA's Earth Science Data and Information System (ESDIS).

## Under the hood

Unity 6000.3.28f1 (URP), C#. One deterministic mission simulation: one clock, one resource ledger in real units, all-or-nothing spending; the visuals only show committed state. All 544 automated EditMode tests and 178 PlayMode scene tests pass on the submitted build, plus scripted full-mission runs and repeated human playtests. Original 3D models and textures are generated by Python scripts for Blender and NumPy/Pillow.

## AI use (NASA Space Apps "SI" disclosure)

- Idea, story and design decisions: the team.
- Code: written with AI coding assistants under our direction, review and playtesting — Claude Code (Anthropic; Claude Opus 5.5, Claude Fable 5.2, Claude Sonnet 5.5) and OpenAI Codex. Claude-written commits carry a `Co-Authored-By: Claude` trailer.
- 3D models and textures: original, produced by scripts that these assistants wrote. No generative image or 3D model produced any in-game asset.
- Audio: **all dialogue uses AI-generated synthetic voices** (Google Gemini text-to-speech, voices Orus, Kore and Iapetus); per-file records are in the voice manifests under `Assets/_OutOfReach`. The original score cues, including the ending themes "Thirty-One Dawns" and "Last Carrier", the synthesized sound effects and the percussion layer added to the opening "Titan" excerpt were composed and generated by scripts written with AI assistants. The licensed music by Scott Buckley is human-composed; the launch countdown is a real NASA recording.
- Images: AI-generated concept boards were used only for art direction and are not part of the game.
- Data: NASA data are unmodified; the download and verification scripts were written with AI assistants.

## Credits

Third-party music (Scott Buckley, CC BY 4.0), models and textures (Poly Haven, ambientCG, MakeHuman, CC0), fonts (SIL OFL), NASA, NOAA and ESA material and the Unity Particle Pack are listed with their licences in [CREDITS.txt](https://github.com/alex-slovakia/out-of-reach/releases/download/spaceapps-2026-submission/CREDITS.txt), in [ASSETS.md](ASSETS.md) and on the in-game **Sources** page.

## Team

- **Alexander Kozovy** (Team Owner) — creative direction, visuals, UI, audio, integration (with OpenAI Codex)
- **Alex (Apoliak)** — gameplay and simulation, movement, survival chapters, tests (with Claude Code)

## Build from source

Unity **6000.3.28f1**, Universal Render Pipeline, C#, Windows x64.

```powershell
git lfs install
git clone https://github.com/alex-slovakia/out-of-reach.git
cd out-of-reach
git lfs pull
```

In Unity Hub, add the folder as a project and open `Assets/_OutOfReach/Scenes/Bootstrap.unity`. Development notes: [PRODUCT.md](PRODUCT.md), [ARCHITECTURE.md](ARCHITECTURE.md), [INTEGRATION.md](INTEGRATION.md), [TESTING.md](TESTING.md), [TASKS.md](TASKS.md).
