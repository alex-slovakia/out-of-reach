# AURORA-7 visual review 01

**Decision:** Alex accepted the vessel direction in boards 01/02, conditional on turning it into actual Blender models. Team approval is not yet confirmed. These eight views are generated concept targets, not existing 3D assets or Unity captures. No completion percentage follows from them.

| Board | Views | Review state |
|---|---|---|
| 01 — The ship | R01 cockpit, R02 engineering, R03 stowage, R04 airlock | Vessel direction accepted conditionally; model verification pending |
| 02 — The silence | R05 exterior, R06 EVA, R07 impact, R08 finale | Same conditional acceptance; cinematic/gameplay verification pending |
| 03 v2 — Player view | R09–R12, revised physical terminal/minimal HUD | Generated and inspected; user/team approval pending |
| [Original 03](03-player-view.png) | Superseded R09–R12 | **Rejected:** AI/web-form appearance. Retained as history; not canonical |

![Board 01: accepted vessel direction, awaiting actual models](01-ship-interiors.png)

![Board 02: accepted exterior and cinematic direction, awaiting actual models](02-eva-cinematics.png)

![Board 03 v2: proposed physical terminal and minimal HUD, approval pending](03-player-view-v2.png)

The v2 board replaces the floating form with a modeled terminal, local instruments and restrained subtitles. Its asset associations remain provisional until approval. These images were made with the built-in GPT image generator; the complete prompts remain in this directory, including the rejected first UI attempt. In-game text, seven catalog items, resource values, controls and reply timing must come from actual gameplay state; simplified mockup labels are not new contracts or verified readouts.

## From references to assets

[asset-map.json](asset-map.json) records view IDs, assigned paths, owners, source/license, scale and the first verification for each asset. Assigned destinations do not imply that files exist.

| ID | Asset / responsibility |
|---|---|
| A01 | Modular pressure-shell/interior kit — Claude/Fable original models |
| A02 | Life-support hardware — Claude/Fable original model |
| A03 | Power cabinet — existing Poly Haven Power Box 01, or pending Claude EPS-2 custom model; neither is automatically the exact generated cabinet |
| A04 | Cargo bags and straps — Claude/Fable original, pending |
| A05 | Pressure hatch and suit stowage — Claude/Fable original |
| A06 | Exterior and solar assembly — Claude/Fable original |
| A07 | Antenna — Claude/Fable original |
| A08 | First-person gloves and tether — original models pending; no full character required |
| A09 | Practical lights and restrained sparks — Codex Unity presentation |
| A10 | Physical display frame — Claude/Fable; UI rendering — Codex |

First delivery: **one actual representative engineering bay, rendered in Unity 6000.3.28f1/URP, before multiplying the kit**. Supply Blender source/export, named anchors, measured metre scale, and a genuine Unity capture with revision evidence. Inspect it at 1.65 m eye height, preserve at least 1.2 m clear passage, and verify materials, pivots, collisions and repair-target readability. Concept approval does not approve invented geometry or gameplay behavior.

## Provenance and workflow

[Power Box 01](https://polyhaven.com/a/power_box_01) is CC0-1.0; the [local manifest](../../../Assets/_OutOfReach/Visuals/ThirdParty/PolyHaven/PowerBox01/manifest.json) records source and verified file hashes. [Adobe Source Sans 3](https://github.com/adobe-fonts/source-sans/tree/87b37a2daaed80fcb8e8ccb0085c4d72ddade12e) uses [SIL OFL 1.1](https://github.com/adobe-fonts/source-sans/blob/87b37a2daaed80fcb8e8ccb0085c4d72ddade12e/LICENSE.md). NASA references inform fictional engineering; no NASA model is imported by these boards. Exterior sound must be suit/radio/contact-transmitted or clearly non-diegetic score.

Seven videos were researched locally. Their useful contribution is reference selection, modular asset breakdown and repeated in-engine inspection. Four selected skills are installed, including official [ui-uitk](https://github.com/Unity-Technologies/skills/blob/e5956e7e34a125982550de15a9e87f8e66af7d86/skills/ui-uitk/SKILL.md); installing guidance does not itself deliver visual quality. Claude was instructed in [CODEX-009](https://github.com/alex-slovakia/out-of-reach/issues/7#issuecomment-6876209557) to use [Blender MCP](https://github.com/ahujasid/mcp-for-blender) and remain on Fable. [CODEX-010](https://github.com/alex-slovakia/out-of-reach/issues/7#issuecomment-6876232143) supersedes the reset instruction: preserve the manual reset for tomorrow and verify the reported automatic reset. Connector installation/connection remains unverified.
