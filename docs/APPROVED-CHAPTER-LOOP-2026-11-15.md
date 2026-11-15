# Approved chapter loop — 15 November 2026

Alex explicitly approved this complete scope in chat annotation `7bcc7091-b361-434e-97e1-29069605250d`. Implementation required; this file is a specification, not delivery evidence. Coordination: [issue #7, 6893821535](https://github.com/alex-slovakia/out-of-reach/issues/7#issuecomment-6893821535).

## Playable beats

| Mission day | Required player experience |
|---|---|
| 1–2 | Survive the accident, repair essential systems, prepare the first packaged meal, secure a sleeping place. |
| 5 | Inspect repair consequences and actual reserves; decide which systems to shut down or sacrifice. |
| 9 | Finish the exterior intervention and communications; receive concrete hope of assistance. |
| 14 | Receive a causally delayed message that the first rescue mission failed; reassess reserves through day 30. |
| 20–30 | Make the final power/supply decision, prepare the beacon, and rendezvous with the rescue craft if survival conditions hold. |

The later rescue uses an already-in-transit nearby fictional craft. An Earth launch on day 14 must not be represented as reaching the game's Mars-vicinity location on day 30. Communications continue to obey the committed NASA/JPL light-time model.

## Routine between chapters

- Compress ordinary days into a brief montage of meals, checks and normal sleep. Display the elapsed days explicitly. A transition from day 2 to day 5 represents three days of life, not one three-day sleep.
- Advance the same authoritative calendar, food/water/oxygen/power ledger, needs, growth and communications. No second day counter and no replenishment at a chapter boundary.
- Stop at danger, death, a pending time-critical decision or unmet prerequisite. Report the reason and preserve the real stopping time. Never skip a failed survival boundary.
- Normal individual rest remains at most 8.5 simulated hours. Current sleep consumption accounting must not be silently repurposed as unlimited multi-day unattended routine; Claude must implement explicit supported routine behavior.
- Keep the approved compact mission duration target. Play consequential choices, not dozens of mandatory repeated meal/sleep clicks.

## Physical food and sleep

- Take a real stored food pouch, prepare it according to type (ready to eat, warmed or rehydrated), open and eat. Optional restrained character reaction to repetitive food; no claim that all astronaut food tastes bad.
- Consume an authoritative ration once: stored food decreases while fed status increases. Preparation animation cannot grant food or repeat the command on duplicate animation events. Water/power costs, if introduced, need explicit domain coordination.
- Growing plants remain an optional small supplement, not a replacement for packaged reserves or instant mature food.
- Enter the secured sleeping bag/berth, settle, fasten restraints, then close eyes. On waking release restraints and return to normal movement. Correct the prologue seat/harness sequence too.
- Both original-reference WORK and EVA suits require coherent body/hand animation. Pause, restart, early wake, hazard wake and mission loss must safely interrupt presentation.

## Verified real-world basis

- NASA describes an eight-hour sleep schedule and lightweight wall-attached sleeping bags: [Crew Systems](https://www.nasa.gov/reference/crew-systems/). Multi-day hibernation is not used as a factual NASA capability in this game.
- NASA describes ready-to-eat, rehydratable and thermostabilized foods, water dispensing and warming: [Artemis II menu](https://www.nasa.gov/missions/artemis/artemis-2/artemis-ii-whats-on-the-menu/) and [Food in Space](https://www.nasa.gov/ochmo/food-in-space/).
- NASA Veggie supplements the packaged diet and studies plant growth: [Growing Plants in Space](https://www.nasa.gov/exploration-research-and-technology/growing-plants-in-space/). It does not establish a complete autonomous food supply.

Sources checked 15 November 2026. The ship, rescue story and chapter schedule are fictional. Resource tuning and compressed routine remain disclosed game assumptions.

## Ownership and delivery gates

Claude owns Core/Simulation/Inventory, all movement, body/hand animations and its tests. Codex owns presentation/UI/audio/camera, scene wiring and integration. Contracts changes require an exact coordinated proposal; no duplicate simulation in presentation. Claude was asked for the smallest API needed to request routine advancement and read its next stopping point.

Current code at approval: `SurvivalRecovery.MaximumRestHours = 8.5`, rendezvous 12 days after contact, beacon requested 6 days after contact, missed beacon adds 3 days. This does NOT implement the approved fixed day-14 failure/day-30 rescue sequence. Real ration/drink commands and resource accounting exist. New chapter progression, meal animation and restraint animation are not yet verified deliveries.

Acceptance requires actual complete playthroughs reaching the beats and a failed-survival path, finite resource accounting through routine, causal communications, correct pause/restart/interruption, both-suit animations, and a Windows x64 build at the named integrated revision. Narrow UI tests cannot satisfy this gate.
