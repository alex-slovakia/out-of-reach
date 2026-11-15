# Claim check — play the submitted build once before pasting

**DRAFT.** Judges score *Validity*: every sentence we submit must be visible in the build we link. One person plays the final Windows x64 build once (about one session, network off) and ticks each line. A line that fails → use the fallback named in the last column, or delete the claim. The earlier, longer step-by-step checklist (marine-prologue era) is in git history at `bb4b883`.

| Field | Value |
|---|---|
| Build SHA / zip name | ________ / ________ |
| Tester, PC, date (UTC) | ________ |
| Network | ☐ off during the test (proves no online dependency) |

| # | Claim we make (where) | How to see it in the build | P / F | If it fails |
|---|---|---|---|---|
| C1 | Physical cargo loading at the launch pad with 600 kg, 800 credits, power, 30 crew-days, 60 s (summary, description 1, deck 2, video S2) | Timed start; carry a crate; the HUD/prompt shows mass and credits; the console reads "Begin mission: 546 of 600 kg, 720 of 800 credits"; overloading is refused with the exact violation | ☐ ☐ | Describe the list planner instead |
| C2 | A crate in your hands at zero stays on the pad; an invalid manifest falls back to the last valid one (description 1) | Let the timer expire while carrying a crate | ☐ ☐ | Delete that sentence |
| C3 | Vertical launch from the pad, then "LATER, ABOARD AURORA-7" (description 2, video S3) | Board, sit, watch the departure | ☐ ☐ | — |
| C4 | Radio countdown before ignition (NASA Data item 9, video S3) | Hear "…ignition sequence starts … zero" | ☐ ☐ | Leave out item 9 and the NASA-audio credit lines |
| C5 | Debris strike: power 20 %, life support and antenna down, CO₂ rising (summary, description 3, video S4) | Cockpit check → impact; Tab systems shows the damage | ☐ ☐ | — |
| C6 | Exact cost in every repair prompt; refusal spends nothing (description 4, video S5) | Aim at the life-support rack with too few parts, then with enough | ☐ ☐ | — |
| C7 | Spacewalk: 2 h 20 min preparation, airlock, tether, antenna, 3 h suit oxygen (description 5, deck 3/5) | Suit locker → airlock → outside → repair → return; watch the suit O₂ | ☐ ☐ | Keep "follow the tether"; drop "hand over hand" wording if the climb is missing |
| C8 | EPIC Earth on the terminal with observation time and image age (description 6, NASA Data 3, deck 4, video S6) | Open "AURORA / EARTH ARCHIVE" | ☐ ☐ | Remove the EPIC sentences and credit |
| C9 | One distress call; "Earth is 197,708,000 km away; earliest reply in 31 min 59 s"; reserves drain while waiting (description 6, deck 4) | Run the diagnostic, transmit on day 1, watch the reply timer | ☐ ☐ | Use the number the build actually shows |
| C10 | Day 1–30 chapters (days 1, 5, 9, 14, 20, 30), routine montage with elapsed days, stops at danger (summary, description 7, deck 3/5) | After contact: the chapter card, the montage, the day-5 and day-20 choices, the day-14 message | ☐ ☐ | Fallback texts in form-fields §2 and §5 |
| C11 | Meals from the actual stores and sleep up to 8.5 h in a secured sleeping bag (description 7, deck 4) | Eat, drink, sleep at the berth; check the HUD reserves change | ☐ ☐ | Say "rest" instead of "sleeping bag" if no bag is shown |
| C12 | Rescue window: power the beacon within 8 h on day 30 (description 7) | Reach the beacon request | ☐ ☐ | Fallback text of §5 item 7 |
| C13 | Outcome card with the cause; mission record; Sources page with NASA data / music & art / assumptions; restart (description 8, deck 5, video S8) | Lose once on purpose (e.g. skip the life-support repair) and read the card; open Sources | ☐ ☐ | — |
| C14 | In-game credits: Titan and Cobalt (CC BY 4.0), Mars imagery, fonts; **AI voices line**; NASA SVS sky (credits §C) | Sources → MUSIC & ART | ☐ ☐ | If the AI-voice line is missing, it must be added before submission (SI audio rule) |
| C15 | No NASA logo or insignia anywhere in the game | Look at the ship, suits, launch site, menus | ☐ ☐ | Report on #7 at once |
| C16 | Runs from a fresh unzip without admin rights; no network needed | Fresh folder on another PC | ☐ ☐ | Fix the zip |

Measured play time of one full mission: ______ min (put it into the public README).
