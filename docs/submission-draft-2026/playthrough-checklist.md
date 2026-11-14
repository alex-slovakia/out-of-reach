# Manual playthrough checklist — Windows build

**DRAFT.** For a human tester playing the Windows x64 executable. No human standalone playthrough of the current candidate is recorded in the repository: earlier evidence is scripted scene checks with QA teleports [docs/verification/i05-3032686/README.md; docs/verification/i06-habitat-contact-native/README.md]. Expected results below come from committed code and docs; where a time is derived rather than measured it says so. Mark each step ☐ P (pass) / ☐ F (fail) and write what you saw when it fails.

## 0. Test record

| Field | Value |
|---|---|
| Build source commit SHA | ________ (survival chapter needs `85b0994` or later with `survivalExtension` on [Assets/_OutOfReach/Scenes/Bootstrap.unity]; the latest **recorded** Windows build `3032686` does not contain it [docs/verification/i05-3032686/README.md]) |
| Build type | ☐ Development ☐ Release (committed entry point builds Development [Assets/_OutOfReach/Scripts/Composition/Editor/I01BootstrapBuilder.cs]) |
| Executable SHA-256 / folder size | ________ / ________ |
| Tester | [TESTER] |
| Date, time, timezone | ________ |
| Machine name | ________ |
| OS version | ________ |
| CPU / GPU + driver / RAM | ________ / ________ / ________ |
| Display: resolution, refresh, fullscreen or windowed | ________ |
| Input: keyboard+mouse / gamepad | ________ |
| Network during test | ☐ Disconnected (recommended: proves no live NASA/API dependency [TESTING.md A14]) |

Controls (from code): WASD or arrows, mouse look, **E** interact (left mouse also listed), **Esc** pause, **Tab** hold to show all reserves (click "Tab Systems" to pin), **Shift** sprint; gamepad sticks/south/Start [Assets/_OutOfReach/Scripts/Core/README.md "No-code wiring"; Assets/_OutOfReach/Scripts/Composition/Runtime/I01SessionControls.cs; Assets/_OutOfReach/UI/Runtime/FlightBoardView.cs; docs/verification/i05-3032686/README.md]. Gamepad support at this revision: TO CONFIRM.

## 1. Launch

| ID | Action | Expected result | Result |
|---|---|---|---|
| L1 | Extract the build into a fresh, empty folder; disconnect the network | Nothing to install or download; the Horizons data is compiled in [Assets/_OutOfReach/Scripts/Core/README.md "Packaged copy"] | ☐ P ☐ F |
| L2 | Start a stopwatch, double-click the executable; stop when the first menu responds | "Choose planning mode" with **Start timed · 60 s** and **Practice · no deadline** over the real scene [I01SessionControls.cs]. Load time: ____ s | ☐ P ☐ F |
| L3 | Look for error dialogs, missing textures, black screen | None | ☐ P ☐ F |

## 2. Planning, cargo and the 60 s loading deadline

| ID | Action | Expected result | Result |
|---|---|---|---|
| P1 | Click **Start timed · 60 s** | White mm:ss countdown of 60 **active** seconds, red for the last 10 s; text says at zero the valid or last valid loadout launches and carried cargo is not aboard [I01SessionControls.cs; docs/verification/i04-c7cfb86/README.md; docs/PLAYER-FEEDBACK-2026-11-14.md] | ☐ P ☐ F |
| P2 | Walk to a supply pallet, press E | Both hands hold one crate; prompt like "Pick up oxygen tank: 30 kg, 40 credits, +12 kg oxygen (4 on the pallet)"; walking slows with crate mass [Assets/_OutOfReach/Scripts/Core/README.md "C05-A"] | ☐ P ☐ F |
| P3 | Follow the bay marker, press E at the bay stack | Crate loaded; payload/credits update; marker points to the bay [docs/verification/i05-3032686/README.md] | ☐ P ☐ F |
| P4 | Load one battery module on top of the default 546 kg | "payload 546 -> 601 of 600 kg (over the limit)"; launch refused with the exact violation [Core README "C05-A"; docs/verification/i04-c7cfb86/README.md] | ☐ P ☐ F |
| P5 | Press Esc mid-countdown for 10 s, resume | Countdown frozen while paused [docs/verification/i04-c7cfb86/README.md] | ☐ P ☐ F |
| P6 | Unload back to a valid loadout; let the timer expire while holding a crate (or launch at the cockpit console: "Begin mission: … kg, … credits") | One launch; the held crate stays behind; cargo targets read "The cargo bay is sealed after launch." [Core README "C05-A"] | ☐ P ☐ F |
| P7 | Boarding window (only if the build has the marine prologue with Board After Loading Deadline on) | Let the 60 s loading timer expire away from the seat: no teleport; cargo prompts read "Loading closed. Board and secure your seat."; a crate in hands is left behind; E on the transport seat ("Secure your seat and launch: ...") launches once [C05-BOARDING-01, `8995f64`; Assets/_OutOfReach/Scripts/Core/README.md]. If the build has no prologue: N/A | ☐ N/A ☐ P ☐ F |
| P8 | (Second run) choose **Practice · no deadline** | No countdown [I01SessionControls.cs] | ☐ P ☐ F |

## 3. Accident

| ID | Action | Expected result | Result |
|---|---|---|---|
| A1 | Cockpit console, E: "Run cockpit systems check" | Impact happens once; alarms; ship line "Power fault. Life support degraded. Communications offline. Check engineering diagnostics." with subtitle; control returns (Core fallback 45 s) [Core README "Accident"; Assets/_OutOfReach/Audio/Voices/Dramatic/voice-manifest.json] | ☐ P ☐ F |
| A2 | Try walking during the first impact | Movement allowed [docs/playtest-review/06-status-1845.md B02] | ☐ P ☐ F |
| A3 | Read the HUD | Time scale shown (×300 = 1 s is 5 min) [Core README "Clock"] | ☐ P ☐ F |

## 4. Repairs

| ID | Action | Expected result | Result |
|---|---|---|---|
| R1 | Aim at the power cabinet, E | Prompt states cost (2 spare parts); after repair parts −2, power restored, voice "Power system restored…" [Core README; Assets/_OutOfReach/Audio/Production/audio-manifest.json] | ☐ P ☐ F |
| R2 | Life-support rack, E | 2 spare parts + 1 kWh; life support restored; CO2 stops rising [Core README "Scene integration", "Emergency model"] | ☐ P ☐ F |
| R3 | Repeat either repair | Refused; nothing spent [Core README "Repairs"] | ☐ P ☐ F |
| R4 | Hold Tab | Expanded reserves including Water and Spare parts [FlightBoardView.cs] | ☐ P ☐ F |

## 5. EVA

| ID | Action | Expected result | Result |
|---|---|---|---|
| E1 | Suit locker | Needs both repairs; 0.84 kg stored oxygen; 2 h 20 min simulated preparation [Core README "C03"] | ☐ P ☐ F |
| E2 | Airlock controls: "Seal inner hatch and depressurize" | 0.11 kg O2 + 0.2 kWh; wall display counts down; 10 simulated min (≈10 real s at ×60) [Core README "C03"; docs/verification/i04-c7cfb86/README.md] | ☐ P ☐ F |
| E3 | Outer hatch: "Clip tether and go outside" | On the route; W moves along the tether the way you face, S back, A/D nothing, both ends stop you [Core README "Traversal"] | ☐ P ☐ F |
| E4 | At the antenna, E | 3 spare parts + 0.5 kWh; antenna restored [Core README "C03"] | ☐ P ☐ F |
| E5 | "Enter the airlock", then "Close outer hatch and repressurize" | Refused while outside; after 10 simulated min the inner hatch opens, suit off, phase Transmission [Core README "C03"] | ☐ P ☐ F |
| E6 | Listen and watch throughout | Hatches never both open; outside only suit/radio/contact sounds, no deck footsteps [Core README "Guarantees"; docs/verification/i06-presentation-audio/README.md] | ☐ P ☐ F |

## 6. Transmission and delayed reply

| ID | Action | Expected result | Result |
|---|---|---|---|
| T1 | Cockpit console: "Run communications diagnostic" | Old local recording labelled "LOCAL BUFFER — OLD OUTBOUND RECORDING"; not a reply; no cost or timer [Core README "C04"; Assets/_OutOfReach/Audio/Production/AUDIO_HANDOFF.md] | ☐ P ☐ F |
| T2 | Read the console prompt | "Transmit distress call: 0.5 kWh. Earth is 197,708,000 km away; earliest reply in 31 min 59 s." on mission day 1 (later dates use later rows) [Core README "C04"]. Shown: ________ | ☐ P ☐ F |
| T3 | Transmit; time the wait | "Awaiting reply from Earth: … (simulated)"; ≈32 real s at ×60; reserves keep draining [Core README "C04"]. Real seconds: ____ | ☐ P ☐ F |
| T4 | Reply | Only after the countdown ends; Earth voice "Aurora Seven, we received your position… Rescue is not yet confirmed." [voice-manifest.json; NASA-DATA.md] | ☐ P ☐ F |
| T5 | Use the console again | No second transmission [Core README "One transmission"] | ☐ P ☐ F |

## 7. Survival chapter (post-contact)

Order follows the automated end-to-end test [Assets/_OutOfReach/Tests/PlayMode/C06EndToEndPlayTests.cs; commit `b4fa7a7`]. Time scale ×300 awake, one simulated hour per real second while resting [Assets/_OutOfReach/Scripts/Core/SurvivalExtensionSpec.cs; SurvivalRecovery.cs]. Real chapter duration: ____ min.

| ID | Action | Expected result | Result |
|---|---|---|---|
| V1 | After the reply, read the HUD | "Survive until rescue" / "Manage your food, water and ship systems."; Hunger and Hydration bars [FlightBoardView.cs; SurvivalChapterPresenter.cs] | ☐ P ☐ F |
| V2 | Engineering science bay: salvage | "Dismantle science instrument: +3 spare parts for 0.2 kWh. The science objective is abandoned for good." (default repairs leave 0 parts) [Core README "C05-B"; docs/verification/i06-habitat-contact-native/README.md] | ☐ P ☐ F |
| V3 | Grow module: build | "Build grow module: 1 spare part(s), 0.2 kWh" [Assets/_OutOfReach/Scripts/Core/SurvivalExtension.cs] | ☐ P ☐ F |
| V4 | Plant | "Plant 25-day seedlings: 1 L; 8 further days, 70 W and 0.05 L/day"; not instant [SurvivalExtension.cs; SurvivalExtensionSpec.cs] | ☐ P ☐ F |
| V5 | Galley: eat, drink | "Eat stored ration" / "Drink stored water", or "No ration is needed yet." / "No water portion is needed yet." Food/water stores fall (water only visible with Tab) [SurvivalExtension.cs; FlightBoardView.cs] | ☐ P ☐ F |
| V6 | Berth: rest | Prompt "Rest up to 8.5 h: about … kg food and … L water at the BVAD crew-day rates; any danger wakes the crew"; panel "DAY N", "Resting · …", Wake button [SurvivalRecovery.cs; SurvivalChapterPresenter.cs] | ☐ P ☐ F |
| V7 | Press Wake; rest again several times | Manual wake works; repeated rests advance DAY N; nothing charged twice [SurvivalRecovery.cs; docs/C06-REST-RECOVERY-CONTRACT.md] | ☐ P ☐ F |
| V8 | Day 6 after contact | Rest stops; beacon request — **visible only with Tab**: "Beacon window: … (simulated)" [SurvivalRecovery.cs; SurvivalChapterPresenter.cs] | ☐ P ☐ F |
| V9 | Cockpit beacon within the 8 h window (≈96 real s) | "Power the recovery beacon: 0.4 kWh; …" → "Recovery beacon powered: the rendezvous stays on schedule." [SurvivalRecovery.cs] | ☐ P ☐ F |
| V10 | When the crop is ready | "Harvest small supplement: 0.05 kg ration-equivalent game credit" [SurvivalExtension.cs] | ☐ P ☐ F |
| V11 | Keep eating, drinking, resting | Rendezvous on contact + 12 simulated days [SurvivalRecovery.cs; C06EndToEndPlayTests.cs] | ☐ P ☐ F |

## 8. RECOVERED ending, report, restart

| ID | Action | Expected result | Result |
|---|---|---|---|
| X1 | Reach the rendezvous | "The recovery craft docked on mission day N; the crew transfers aboard."; RecoveryArrival then MarsDeparture sequences (interim framing) [SurvivalRecovery.cs; docs/verification/i06-presentation-audio/README.md] | ☐ P ☐ F |
| X2 | End card | "CREW RECOVERED" · "You made it." · "Rescue reached Aurora-7. Your next destination: Mars." · elapsed time [Assets/_OutOfReach/UI/Report/Runtime/FlightRecorderPresenter.cs] | ☐ P ☐ F |
| X3 | Open the mission record | Sections: outcome, mission design, resources, systems, science, communication, timeline, NASA data, derived physics, fictional scenario, time compression; NASA-01–03 links, BVAD line, dataset ID and SHA-256; numbers match what you saw [Core README "Report"; MissionReportBuilder.cs] | ☐ P ☐ F |
| X4 | "Restart mission" | Fresh planning with the default cargo; no salvage, crop, rest or beacon carried over; one set of sounds [C06EndToEndPlayTests.cs; Core README] | ☐ P ☐ F |

## 9. Loss paths (play at least one)

| ID | Action | Expected result | Result |
|---|---|---|---|
| D1 | During the EVA, stay outside | Suit tank 3 simulated h (≈3 real min at ×60, derived) → Lost, reason "outside the ship"; end card "FLIGHT ENDED" · "Out of time." + cause [Core README "C03"; FlightRecorderPresenter.cs] | ☐ P ☐ F |
| D2 | After the impact repair nothing and wait | Lost by cabin CO2 about 20.5 simulated h after scrubbers stop (≈4 real min at ×300, derived, TO CONFIRM) [Core README "Endings in C02"] | ☐ P ☐ F |
| D3 | In the survival chapter stop drinking | Lost at the 24 h thirst limit before rescue (≈4.8 real min awake at ×300, derived) [SurvivalExtensionSpec.cs; C06EndToEndPlayTests.cs] | ☐ P ☐ F |
| D4 | (Not a loss) ignore the beacon window | Rendezvous slips once by 3 days, to contact + 15 days [SurvivalRecovery.cs; C06EndToEndPlayTests.cs] | ☐ P ☐ F |
| D5 | After any loss | Cause is understandable, no negative numbers, Restart works [TESTING.md A10, A13] | ☐ P ☐ F |

## 10. Pause and settings

| ID | Action | Expected result | Result |
|---|---|---|---|
| K1 | Esc during play | "Paused": Resume, Restart mission, "Reduced camera motion and light effects" toggle, controls hint [I01SessionControls.cs] | ☐ P ☐ F |
| K2 | Stay paused 30 real s, resume | No resource or timer change; no catch-up drain [TESTING.md A07] | ☐ P ☐ F |
| K3 | Toggle reduced effects, trigger motion/flash scenes | Less camera motion and flashing; objectives still clear [TESTING.md A15] | ☐ P ☐ F |
| K4 | Language choice | Not in `8bb53aa`. LANG-01 (English, Español, Français) is on `feature/localization` and appears only if root adds the picker before the freeze; if present: switch language, check a prompt, an E refusal and the report change language [Assets/_OutOfReach/Scripts/Core/README.md LANG-01 on that branch] | ☐ N/A ☐ P ☐ F |
| K5 | Volume settings | No level controls in the pause menu at this revision [I01SessionControls.cs] | ☐ N/A |
| K6 | Hold Shift while walking | Sprint with exertion [docs/verification/i05-3032686/README.md] | ☐ P ☐ F |
| K7 | Mute system audio during a voice line | Subtitles still shown and timed [Assets/_OutOfReach/Audio/Production/AUDIO_HANDOFF.md; ASSETS.md] | ☐ P ☐ F |
| K8 | Close the game (window close / Alt+F4) | Process exits; cursor restored [TESTING.md] | ☐ P ☐ F |

## 11. Performance capture (named machine)

Targets are budgets, not results: 1080p 60 fps target, scalable to 30 fps [ASSETS.md]. Use an FPS overlay already available on the machine (for example the Windows Game Bar performance widget); install nothing new without team approval.

| Scene (30 s each) | Avg fps | Min fps / worst hitch | Notes |
|---|---|---|---|
| Menu / planning with cargo | | | |
| Cabin during emergency lighting | | | |
| EVA exterior at the antenna | | | |
| Survival habitat, resting | | | |
| End card + mission record | | | |

Also record: load time (L2) ____ s; peak memory (Task Manager) ____ MB; build folder size ____ (earlier I05 build reported 369,312,768 bytes [docs/verification/i05-3032686/README.md]).

## Known issues to watch

Review findings (confirm, and note when they confuse you):

1. **Beacon request only in Tab telemetry.** The beacon line is added to the expanded-telemetry panel, so it is invisible unless Tab is held or pinned [Assets/_OutOfReach/UI/Runtime/SurvivalChapterPresenter.cs, beacon label placed in `expanded-telemetry`].
2. **Automatic rest stop shows no reason.** The contract exposes `LastRestStop` [Assets/_OutOfReach/Scripts/Contracts/ISurvivalRecovery.cs] and Core composes "Rest ended on mission day … after … h: …" [SurvivalRecovery.cs], but no UI code reads the reason; the rest panel simply disappears [SurvivalChapterPresenter.cs].
3. **Water store hidden by default in Survival.** Water is only in the expanded telemetry; oxygen, food and battery are primary [Assets/_OutOfReach/UI/Runtime/FlightBoardView.cs].
4. **Hunger and hydration bars have opposite polarity.** Hunger: empty bar = satisfied, rising = food needed; Hydration: full bar = satisfied [SurvivalChapterPresenter.cs tooltips].

Other documented limits:

5. Rescue and Mars sequences use interim framing [docs/verification/i06-presentation-audio/README.md].
6. B24 cyan-view bug unreproduced/open; B23 hull containment not fully proven [docs/verification/i05-3032686/README.md; docs/playtest-review/06-status-1845.md].
7. Floating/microgravity is off in the playable scene; the floating body view and the glove forearm cut end are known art defects [docs/verification/i06-body-habitat-candidate/README.md; docs/verification/i06-habitat-contact-native/README.md].
8. Input can stay locked up to 45 s if the false-signal sequence is not acknowledged [Assets/_OutOfReach/Scripts/Core/README.md "Open points" 4].
9. With life support repaired but power never repaired, the loss takes about 10 real minutes [Core README "Open points" 2].
10. No transmission is possible after mission date 2026-12-15 08:00 UTC (end of the NASA snapshot) [Core README "Open points" 3].
11. Development-build overlay or console, if any (TO CONFIRM).

## Summary and sign-off

| Area | Pass | Fail | Blocking for submission? |
|---|---|---|---|
| Launch / planning / accident / repairs | | | |
| EVA / transmission | | | |
| Survival / RECOVERED / report / restart | | | |
| Loss path(s) | | | |
| Pause / settings / exit | | | |
| Performance recorded | | | |

Tester: [TESTER] · Date: ________ · Build SHA: ________ · Notes for the form text ([form-fields.md](form-fields.md)): ________
