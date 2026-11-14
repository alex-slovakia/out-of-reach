# TransportLaunch V1 — boardable transport, booster, launch barge and access arm (TRANSPORT-LAUNCH-ART-V1)

This kit is the art for the prologue: the small Earth-to-orbit transport the player loads and boards, standing on its
booster on a launch barge, with an access mast and a swing arm. It was requested by Codex in issue #7 (17:01 UTC,
14 Nov 2026).

- **Author:** the Claude prop chat (Apoliak's machine). The kit is original, built in Blender 5.3 from one script.
- **Scope:** only this folder. Nothing was changed in AURORA-7, the staging deck, Assets, Bootstrap, scenes or runtime.
- **What it is:** a **fictional visual launch vehicle**. Proportions are kept plausible, and the interior is the
  inside of the same hull. Masses, structure, propulsion and launch clearances are not engineered.
- **Checkpoint 1** (block-out, measured dimensions, anchor proposal) is in `checkpoint-1/`.
- **Changed since checkpoint 1:** `SeatEntry` moved from Z 0.95 to Z 0.80. It is now 0.34 m in front of the seat pan,
  so a 0.30 m player radius fits. This follows root's finding on RADIO-DESK-V1.

## Frame

Metres, Y up. The origin is on the capsule floor at the capsule axis. The player boards through the side hatch from
−Z and looks +Z. The FBX uses the same convention and export settings as TERMINAL-ART-V2 and RADIO-DESK-V1: Blender
coordinates are (−X, −Z, Y) of Unity. It holds meshes and empties only, with no cameras, lights or texture records.
In Unity each object comes in with `localEuler (270, 0, 0)` from the axis conversion, and the positions come out
right.

## Anchors (Unity metres)

| Anchor | Position | Direction / note |
|---|---|---|
| `BoardingThreshold` | (0, 0, -2.5) | (0, 0, 1), outer edge of the hatch sill |
| `SeatEntry` | (0, 0, 0.8) | (0, 0, -1), stand here, then sit facing -Z |
| `SeatedCamera` | (0, 1.16, 1.88) | (0, -0.17, -0.985), eye of the seated player |
| `HatchPivot` | (-0.72, 0, -2.4) | (0, 1, 0), hinge axis +Y; the Hatch origin |
| `HatchLatch` | (0.58, 1.05, -2.33) | (0, 0, 1), inner latch handle, door closed |
| `DockingPort` | (0, 4.95, 0) | (0, 1, 0), docking ring centre |
| `EngineEmitter` | (0, -31.4, 0) | (0, -1, 0), bell exit centre |
| `AccessArmPivot` | (-0.9, 0, -6.9) | (0, 1, 0), hinge axis +Y; the AccessArm origin |
| `CargoBay_oxygen` | (-0.985, 0.1, -1.025) | (1, 0, 0), bottom slot of the bay; the case face turns to the corridor |
| `CargoBay_water` | (-0.985, 0.1, -0.475) | (1, 0, 0), bottom slot of the bay; the case face turns to the corridor |
| `CargoBay_food` | (-0.985, 0.1, 0.075) | (1, 0, 0), bottom slot of the bay; the case face turns to the corridor |
| `CargoBay_battery` | (-0.985, 0.1, 0.625) | (1, 0, 0), bottom slot of the bay; the case face turns to the corridor |
| `CargoBay_solar` | (0.985, 0.1, -1.025) | (-1, 0, 0), bottom slot of the bay; the case face turns to the corridor |
| `CargoBay_spares` | (0.985, 0.1, -0.475) | (-1, 0, 0), bottom slot of the bay; the case face turns to the corridor |
| `CargoBay_science` | (0.985, 0.1, 0.075) | (-1, 0, 0), bottom slot of the bay; the case face turns to the corridor |

Anchor empties have identity rotation; the directions above are in `model-report.json`. Each cargo bay has three
slots at Y 0.10, 0.46 and 0.82, at the same X and Z. The game case (0.453 × 0.319 × 0.354 m) fits each slot, with its
labelled face (+Z of the case) turned to the corridor.

## Moving parts

| Object | Pivot | Axis | Range | Unity call |
|---|---|---|---|---|
| `Hatch` | `HatchPivot`, the object's origin | +Y | 0° closed (as modelled) to 160° open | `hatch.RotateAround(pivot, Vector3.up, +160)` opens it. The free edge moves out towards −Z, then −X. At 160° the door lies against the hull and clears the arm rails through the whole swing. |
| `AccessArm` | `AccessArmPivot`, the object's origin | +Y | 0° connected (as modelled) to 90° retracted | `arm.RotateAround(pivot, Vector3.up, −90)` retracts it. The hatch end swings towards −X. No ignition while it is connected. |

## Dimensions and clearances

- Capsule: Ø 5.00 m outer, Ø 4.76 m inner, floor to docking port 4.95 m.
- Hatch clear 1.40 × 2.15 m (corner radius 0.16 m), sill level with the floor.
- Central route 1.30 m clear; 2.47 m under the light panels.
- Rack cells 0.55 m pitch; slot floors at Y 0.10, 0.46, 0.82.
- SeatEntry is 0.34 m in front of the seat pan (player radius 0.30 m).
- Stack 37.5 m from deck to docking port; booster Ø 4.60 m; barge 36 × 24 m, deck at Y -32.60.
- Access arm 4.35 m long, 1.70 m clear, floor level with the capsule floor.

## Collider proxies

`model-report.json` → `colliders` lists the box proxies (Unity metres: centre, size and yaw about Y, plus the object
each belongs to). They cover:
- the cabin floor and ceiling;
- 15 wall segments around the inner hull, leaving the hatch sector open;
- the hatch jambs and header;
- both rack blocks and the seat;
- the hatch door;
- the arm floor and rails;
- the mast landing and tower;
- the stack and the barge deck.

The FBX contains no collider meshes; root creates the colliders.

## Objects and materials

| Object | Material(s) |
|---|---|
| `CapsuleShell` (hull with the hatch and window openings, trims, stripe and seams), `Hatch`, `AccessArm` | `TL_Exterior` (baked atlas) and `TL_Labels` |
| `CapsuleInterior` (floor, racks, seat, wall frames, cable trays, vents, extinguisher, hatch status panel) | `TL_Interior` (baked atlas) and `TL_Labels` |
| `BoosterStack`, `LaunchBarge`, `AccessMast` | `TL_Site` (baked atlas); `LaunchBarge` also has `TL_Labels` |
| `CabinLights`, `HatchStatusLamp`, `DeckLamps` | `TL_LightPanel`, `TL_StatusGreen`, `TL_DeckLamp` (emissive; drive or swap them as needed) |

`materials.json` lists every map with its colour space and the URP Lit set-up. Each atlas has `BaseColor` (sRGB),
`MetallicSmoothness` (metallic in RGB, smoothness in A, linear) and `Occlusion` (linear), all 2048². `Labels.png`
holds the legends.

The markings are real words only:
- `OXYGEN`, `WATER`, `FOOD`, `BATTERY`, `SOLAR`, `SPARES` and `SCIENCE` on the racks;
- `HATCH` / `LIFT HANDLE AND PULL TO SECURE` and `HATCH SECURE` inside;
- `EMERGENCY HATCH RELEASE` on the door;
- `TV-1` on the hull;
- `KEEP CLEAR OF LAUNCH MOUNT` on the deck.

There is no NASA insignia. Red-orange appears on the identity stripe and the extinguisher.

Triangles: 23,810 in total (`CapsuleShell` 5,844, `CapsuleInterior` 5,960, `Hatch` 616, `BoosterStack` 2,276, `LaunchBarge` 5,818, `AccessMast` 1,644, `AccessArm` 1,128, `CabinLights` 220, `HatchStatusLamp` 84, `DeckLamps` 220).

## Files

| Path | What |
|---|---|
| `TransportLaunch.fbx` | The kit, with 15 anchor empties. |
| `TransportLaunch.blend` | Editable source: the kit collection, with textures linked relatively from `textures/`. The local paths were blanked from the file. |
| `materials.json` | Material and map manifest. |
| `model-report.json` | Dimensions, anchors with directions, moving parts, collider proxies, bounds, triangles and materials. |
| `textures/` | Three atlases × three maps, plus `Labels.png`. |
| `renders/` | Real Cycles renders: `01-boarding-pov`, `02-exterior`, `03-interior`, `04-seated-hatch-closed`, `05-cargo-bays` and `06-launch-ready-arm-retracted`. The sky, sea and lights are render props. The crates in them are the game's existing `PreflightCargo` case, imported only to show the fit; they are not part of this kit. |
| `checkpoint-1/` | The block-out views and the anchor proposal posted in #7. |
| `Source/build_transport_launch.py` | Rebuilds everything above. `--cargo <PreflightCargo.fbx>` adds the preview crates. |
| `Source/verify_kit_fbx.py` | Re-imports an FBX and reports what a consumer receives. |
| `evidence/` | The Blender FBX round trip, the Unity import result and three Unity captures. |

Rebuild (a few minutes on an RTX 4070):
`blender -b --factory-startup --python Source/build_transport_launch.py -- --stage full --out <folder outside Assets>`.

## Verification actually performed (14 Nov 2026, Apoliak's machine)

- **Blender 5.3.0, Cycles/OptiX.** Full build exit 0. Three atlases baked, labels rendered, six renders written.
- **FBX round trip** (`evidence/fbx_roundtrip.json`): all 15 anchors land where the build report says (within 0.1 mm), the `Hatch` and `AccessArm` origins sit exactly on their pivots, 23,810 triangles, no cameras or lights. The coarse drive-letter scan flags one match that is binary data inside a compressed array, not a path; a byte search finds no user, folder or file path in the FBX or the `.blend`.
- **Unity 6000.3.28f1, URP**, batch import in a private clone (`evidence/unity_import.json`): `TRANSPORT_CHECK_PASS`, exit 0, no compile error. All 15 anchors present; `Hatch` and `AccessArm` origins on their pivots in Unity; 10 meshes, 23,762 triangles; no camera or light. Three captures with URP Lit materials from the delivered maps and plain lights, the hatch opened in Unity with `RotateAround(HatchPivot, up, +160)`: `evidence/TransportLaunch_unity_boarding.png`, `_interior.png`, `_exterior.png`.

## Limits and what is not verified

- **Not integrated.** The kit is not placed or integrated. The staging-deck join, the water, the launch effects,
  cargo stowing, input, time and cinematics are root's and gameplay's. The real AURORA-7 is used for the rendezvous;
  it is not part of this kit.
- **Detail.** One LOD, no normal maps. The capsule interior and exterior share the hull mesh. The barge and the stack
  have a coarse texel density, because they are meant to be seen from a distance.
- **Not measured:** performance, and label legibility at gameplay distance.

## Rights and attribution

| Item | Source | Licence |
|---|---|---|
| Geometry, UVs and baked textures | Generated by `Source/build_transport_launch.py`. No external model, texture or scan. | Original team work |
| Legend typeface | Inter, bundled with Blender 5.3, rasterised into `TransportLaunch_Labels.png` | SIL Open Font License 1.1. Credit: "Inter typeface © The Inter Project Authors" |
| Preview crates in the renders only | The team's own `Visuals/Props/PreflightCargo` (Codex) | Team asset; not redistributed in this folder |
| Tool | Blender 5.3.0 (Cycles) | GPL tool; its output carries no licence obligation |

- **Design reference.** The design follows the team's approved art direction
  (`docs/art-direction/review-05-marine/`, boards 02 and 03) and Codex's brief. No generated reference image was
  traced or used as a texture.
- **What is not used.** No NASA model, texture, drawing, photograph, logo or insignia, and no third-party game asset.
- **Names.** `TV-1` is fiction.
