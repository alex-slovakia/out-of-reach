# Product: OUT OF REACH

## User-approved extension, November 14 afternoon

The latest user playtest changes the desired ending: restored contact becomes the midpoint, followed by bounded food/water/oxygen survival, actual rations, one crafted growing module, then a time-separated rescue and departure-for-Mars cinematic (or survival failure). This supersedes the earlier final-report endpoint as the final product target. The existing contact-restored slice remains the tested checkpoint while the extension is implemented. No instant crops, instant rescue or unaccounted time skips. Exact requested changes and delivery distinctions: [live playtest record](docs/PLAYER-FEEDBACK-2026-11-14.md).

## Player promise

You planned AURORA-7's mission. After a debris strike damages its systems, the equipment and reserves you selected determine your options. Alone in a small spacecraft, you stabilize life support, repair an exterior antenna, and survive long enough for Earth to respond.

The experience should feel like a tense, cinematic space thriller with physical exploration and understandable engineering trade-offs. Intended audience: judges and players without aerospace training. Explain consequences through readable instruments, environmental changes and a short debrief.

## One mission, beginning to end

| Beat | Player action | Cinematic presentation | Engineering consequence |
|---|---|---|---|
| Mission planning | Choose research payload, reserves and equipment within mass, budget and power limits | Restrained ship schematic and launch briefing | Extra science capability competes with redundancy and reserves |
| Last signal | Complete a cockpit check; hear a previously transmitted Mission Control message | Distant Earth, machinery hum, impact, brief blackout, emergency light | One scripted accident damages power, life support and communications |
| First night | Explore cockpit, engineering, storage and airlock; repair power and life support | Local practical light, quiet fans, stressed breathing, readable damage | Parts and energy spent on one repair are unavailable elsewhere |
| Prepare to endure | Activate one modular survival improvement or conserve its parts | Small physical installation and diagnostic feedback | Supplies remain finite; hydroponics is a long-term investment |
| The silence outside | Suit up, cycle the airlock and follow a short tethered route to repair the antenna | Breath, radio and contact vibration; expansive exterior view | Suit reserves, airlock losses and repair costs matter |
| A signal in the darkness | Diagnose a corrupt local communications-buffer replay | Brief hope, then recognition of an old recording | A local replay is not an impossible instantaneous Earth echo |
| Last transmission | Allocate remaining power, transmit position data, wait for the reply | Signal countdown, sparse sound, delayed voice, distant final camera | Distance determines minimum round-trip time; recovery is possible, not immediate |
| Debrief | Review outcome and restart with a different loadout | Short mission report | Connect choices to survival, science return, resources and NASA sources |

The user's Day 1/3/7/18/31 story supplies the emotional progression. The hackathon slice condenses it into **15–20 minutes**. Chapter cards explicitly state elapsed simulation time. A skip must advance the same resource and communications model, or disclose a narrative-only time jump; it must never silently grant supplies or skip a death condition.

## Mandatory slice

- One compact ship with cockpit, engineering bay, storage and airlock.
- One mission planner with at least two viable loadouts and explicit mass, budget, power and supplies limits.
- First-person movement and one consistent interaction action.
- One accident cinematic; camera shake and flashing can be reduced or disabled.
- Three repairable systems: power, life support, antenna.
- One short EVA using a constrained route and a safe return path.
- One communications finale with NASA-derived distance, delay and provenance visible.
- Two outcomes: **contact restored / recovery planning possible**, or **mission lost**.
- One mission report explaining the player's decisions, scientific trade-offs and simplifications.

A survivable holding state may appear inside the success ending; it is not a third required ending. Navigation repair is a final planning choice or report result, not a full orbital mechanics implementation.

## Science and challenge alignment

The [official challenge](https://www.spaceappschallenge.org/2026/challenges/space-mission-design-game/) asks for integrated mission design, management and evaluation. The planner must therefore affect the survival sequence and report. A cinematic corridor with unrelated resource bars is insufficient. Multiplayer and multiple missions are optional in the challenge and excluded from this slice.

Use at least one real NASA/JPL dataset in a consequential mechanic: a committed Horizons snapshot drives communications delay. A Mars-centre distance is only a documented proxy for a fictional vessel near Mars; it is not a measured AURORA-7 trajectory. See [NASA-DATA.md](NASA-DATA.md). NASA ECLSS references inform oxygen, carbon-dioxide removal and water-system relationships. Tuning values remain labeled game assumptions. Real data use does not guarantee award eligibility.

Earth is a distant point during deep-space gameplay. Any large planetary limb belongs to an explicitly earlier scene or a disclosed artistic view. No instantly mature hydroponics, instant rescue, real-time interplanetary conversation, or claims of high-fidelity orbital simulation.

## Excluded until the slice passes

Open-world travel, procedural galaxies, multiplayer, full n-body dynamics, large crafting trees, NPC AI, complex character animation, new networking, large asset downloads and speculative package additions.

## Definition of done

A new player can launch a Windows x64 build, understand the planner, experience the accident, explore, repair all three systems, complete an EVA, receive a causally valid outcome, read the report, and restart. The success and failure paths both work. No missing scripts, broken references or blocking exceptions. The data source, asset credits, controls and time-compression assumptions are included in the build and submission materials. Performance and limitations are measured and reported on a named machine.
