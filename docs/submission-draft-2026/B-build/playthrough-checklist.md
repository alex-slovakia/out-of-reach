# Claim check — play the submitted build once before pasting

**DRAFT.** Judges score *Validity*: every sentence we submit must be visible in the build we link. One person plays the final Windows x64 build once (network off) and ticks each line. A line that fails → use the action in the last column, or delete the claim. The claims below were checked in the trees at `cf2224d` and `feature/claude-max-final@5ea2c23` (scene and prefab bindings) and against the native QA runs; this sheet is the human look at the final build.

| Field | Value |
|---|---|
| Build SHA / zip name | ________ / ________ |
| Tester, PC, date (UTC) | ________ |
| Network | ☐ off during the test (proves no online dependency) |

| # | Claim we make (where) | How to see it in the build | P / F | If it fails |
|---|---|---|---|---|
| C1 | Physical cargo loading at the launch pad with 600 kg, 800 credits, power, 30 crew-days; 60 s in the timed mode, a practice mode without deadline (summary, description 1, deck 2) | Choose the timed start; carry a crate; the HUD/prompt shows mass and credits; the console reads "Begin mission: 546 of 600 kg, 720 of 800 credits"; overloading is refused with the exact violation | ☐ ☐ | Describe the list planner instead |
| C2 | A crate in your hands at zero stays on the pad; an invalid manifest falls back to the last valid one (description 1) | Let the timer expire while carrying a crate | ☐ ☐ | Delete that sentence |
| C3 | Vertical launch from the pad, then "LATER · ABOARD AURORA-7" (description 2, deck 3) | Board, sit, watch the departure | ☐ ☐ | — |
| C4 | Real NASA countdown on the radio (description 2, NASA Data 5, deck 3/4/7) | Hear "Ignition sequence start … zero … We have a liftoff … Tower cleared" | ☐ ☐ | Delete NASA Data item 5, the SOUND RECORDINGS block of §8 and "while the radio carries a real NASA launch countdown"; set `COUNTDOWN_SHIPPED = False` in the deck |
| C5 | Debris strike: power 20 %, life support and antenna down, CO₂ rising (summary, description 3) | Cockpit check → impact; Tab systems shows the damage | ☐ ☐ | — |
| C6 | Exact cost in every repair prompt; refusal spends nothing (description 4) | Aim at the life-support rack with too few parts, then with enough | ☐ ☐ | — |
| C7 | Spacewalk: 2 h 20 min preparation, airlock, **tether rope**, antenna, 3 h suit oxygen (description 5, deck 3/5) | Suit locker → airlock → outside along the rope → repair → return; watch the suit O₂ | ☐ ☐ | Say "tether" instead of "tether rope" if no rope is drawn |
| C8 | EPIC globe on the terminal with observation time and image age; **Slovakia inset from NASA's Terra** (description 6, NASA Data 3–4, deck 4) | Open "AURORA / EARTH ARCHIVE"; look for the inset captioned "SLOVAKIA / TERRA 14 NOV" | ☐ ☐ | No inset → delete the Slovakia clause of description 6, NASA Data item 4 and the GIBS credit line |
| C9 | One distress call; day 1: "197,708,000 km … earliest reply in 31 min 59 s"; on day 9 still over 30 min; reserves drain while waiting (description 6, deck 4) | Run the diagnostic, transmit, watch the reply timer. Note the day and the numbers shown | ☐ ☐ | Use the numbers the build actually shows |
| C10 | Chapters on days 1, 5, 9, 14, 20, 30; ordinary days pass as a **montage** that cuts between the galley, the water store and the berth, shows the elapsed days and stops at danger (summary, description 7, deck 5/6) | After the first meal and rest press R: the routine card with its live station picture and tally, the day-5 and day-20 choices | ☐ ☐ | No station pictures → undo A3 in form-fields.md. No chapters at all → the non-chapter fallback there |
| C10a | **Handheld radio** rises into view for radio traffic; on day 14 it calls you to the console for Mission Control's announcement that the first rescue failed (description 7) | Boarding radio check after arrival; day 14: the call, then the announcement at the cockpit console (voice or subtitles) | ☐ ☐ | No radio → undo A1 in form-fields.md |
| C11 | A meal from a ration pouch in your hands; water from the store; sleep up to 8.5 h **strapped into the berth** (description 7, deck 4/6) | Eat at the galley, drink, go to the berth; watch the straps and the HUD reserves | ☐ ☐ | Say "rest in the berth" if no straps are visible |
| C12 | Day 30: the rescue craft asks for the beacon; power it within 8 h (description 7) | Reach the beacon request | ☐ ☐ | — |
| C13 | **Rescue ending in first person:** craft at the cabin window → aft docking camera → contact, capture, hard dock → hatch opens, with the rescue-crew radio; **stars and the Milky Way behind it** (description 8, NASA Data 7, deck 3) | Survive to day 30 with the beacon powered | ☐ ☐ | No stars → undo A2 in form-fields.md. No docking sequence → replace the first sentence of description 8 with "The crew is rescued or lost, with the cause stated…" and swap the third picture of deck slide 3 |
| C14 | Loss card with the cause; mission record; Sources page with NASA data / music & art / assumptions; restart (description 8, deck 5) | Lose once on purpose (e.g. skip the life-support repair) and read the card; open Sources | ☐ ☐ | — |
| C15 | In-game credits on Sources → MUSIC & ART: Titan and Cobalt (CC BY 4.0), launch radio (NASA), space sky (NASA SVS), EPIC, Terra MODIS, **synthetic voices (Google Gemini) + AI-synthesized score and effects**, Mars imagery, font | Sources → MUSIC & ART | ☐ ☐ | The AI-voice line must be there before submission (SI audio rule) — report on #7 at once |
| C16 | No NASA logo or insignia anywhere in the game | Look at the ship, suits, launch site, rescue craft, menus | ☐ ☐ | Report on #7 at once |
| C18 | **Horizons line during play:** the next-chapter strip shows "Earth <distance> · signal <delay> one way", labelled NASA/JPL Horizons (description 7, NASA Data 1, deck 4) | Look at the strip bottom right between chapters | ☐ ☐ | Run the pack without `--horizons` (restore the files) |
| C19 | **Radiation estimate:** "Radiation since Day 1 ≈ … mSv (est. at 1.8 mSv/day, NASA MSL RAD cruise measurement)" on the strip, and a RAD entry with its NASA/JPL link on the Sources page (NASA Data, deck 4/7) | Strip between chapters; Sources page | ☐ ☐ | No Sources entry → report on #7 before submitting; no line → run the pack without `--rad` |
| C17 | Runs from a fresh unzip without admin rights; no network needed | Fresh folder on another PC | ☐ ☐ | Fix the zip |

Measured play time of one full mission: ______ min (put it into the public README).
