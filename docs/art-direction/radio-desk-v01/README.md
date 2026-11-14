# RadioDesk V1 — communications desk art kit (RADIO-DESK-V1)

This folder contains a standalone art kit for the AURORA-7 communications desk, which root places in the cockpit.
It answers Codex's request in issue #7 (16:28 UTC, 14 Nov 2026).

- **Author:** the Claude prop chat (Apoliak's machine). The kit is original, built in Blender 5.3 from one script.
- **Scope:** only this folder. Nothing was changed in the ship, the scenes, runtime, UI or contracts.
- **What it is:** an art kit with passive geometry, anchors and contact points. It is not a working seated radio
  interaction.

## Interface

| Fact | Value |
|---|---|
| Axes (Unity) | Origin is the floor under the desk centre. X is width, Y is up, Z is depth. The seated player is on −Z and faces +Z. Unit scale. |
| Envelope | The desk is 1.60 m (X) × 0.65 m (Z), plus a 12 mm rubber bumper on the front edge. The worktop is at Y 0.75. Desk bounds: min (-0.804, 0.000, -0.337), max (0.804, 1.451, 0.325). |
| Brief anchors, exact | `SeatEntry` (0, 0, −1.00), `SeatedCamera` (0, 1.20, −0.70), `HandsetRest` (−0.52, 0.83, −0.04), `HandsetGrip` (−0.52, 0.86, −0.04), `ScreenCenter` (0.20, 1.10, 0.12) |
| Added anchors | `LampEmitter` and one `ControlContact_*` per physical control: `Power`, `Transmit`, `Monitor`, `Lamp`, `PTT`, `Distress`, `Volume`, `Squelch` and `Channel`. `model-report.json` gives each position and its outward direction in Unity axes. |
| Anchor objects | Empties with identity rotation. The directions are in the report, not in the empty rotations. |
| Screen | `ScreenSurface`: one quad, 0.44 × 0.26 m on Z = 0.12, centred on `ScreenCenter`, facing −Z. Corners: (−0.02, 0.97, 0.12), (0.42, 0.97, 0.12), (0.42, 1.23, 0.12), (−0.02, 1.23, 0.12). Normal (0, 0, −1). UV (0,0) at the bottom-left and (1,1) at the top-right, as seen from the seat. Slot `RD_Screen`. |
| Objects | `DeskBody`, `Handset` (pivot at `HandsetGrip`), `HandsetCord`, `Seat` (pivot at the floor-plate centre, Unity (0, 0, −0.66)), `Indicator_PWR`, `Indicator_TX`, `LampLens` and `ScreenSurface`, plus the anchor empties. |
| FBX export | Same convention and settings as TERMINAL-ART-V2 and TERM-V1. Blender coordinates are (−X, −Z, Y) of Unity. `FBX_SCALE_UNITS`, forward −Z, up Y, modifiers applied, triangulated. Meshes and empties only: no cameras, lights, armatures or texture records. In Unity each object comes in with `localEuler (270, 0, 0)` from the axis conversion, and the positions come out right. |

## Clearances

These values come from `model-report.json`.

- **Leg room:** 0.94 m wide between the pedestals. The worktop underside is at 0.712 m. The space reaches 0.607 m back from the front edge to the modesty panel.
- **Seat, occupied:** bounds min (-0.250, 0.000, -0.902), max (0.230, 0.957, -0.460). The seat front is 0.123 m from the desk bumper. `SeatEntry` is 0.098 m behind the backrest.
- **Seat, stowed:** move the `Seat` object +0.22 m along Z. Its bounds are then min (-0.250, 0.000, -0.682), max (0.230, 0.957, -0.240). The cushion slides under the worktop and between the pedestals: the cushion top is at 0.47 m, below the 0.712 m underside, and it is 0.40 m wide, inside the 0.94 m leg room.
- **Eye to screen:** the distance from `SeatedCamera` to `ScreenCenter` is 0.85 m.
- **Aisle:** root chooses the placement after import. The kit claims nothing about the cockpit walls.

## What it contains

- **Desk.** A scratched graphite worktop with a rubber front bumper. Two cream drawer pedestals hold quarter-turn
  latches and aluminium pulls; their drawers are labelled `SPARES`, `MANUALS`, `CABLES` and `TOOLS`. Graphite plinths
  and a modesty panel complete the desk.
- **Sloped control shelf.**
  - Toggles: `POWER` (guarded), `TX`, `MON` and `LAMP`.
  - Amber `PWR` and `TX` lamps, as separate objects so each can be driven on its own.
  - A `PTT` push button.
  - A red-covered `DISTRESS` switch. Red is used only for this danger control.
- **Rack units.**
  - A handset unit with a cord grommet.
  - A transceiver with an analogue `SIGNAL` meter and `VOLUME`, `SQUELCH` and `CHANNEL` knobs.
  - A monitor with a recessed, gasketed screen and the plate `COMM UNIT CU-1 AURORA-7` with the red-orange identity
    stripe.
  - A slotted `SPEAKER` grille.
- **Handset.** It rests on a cradle with a hook plunger, and a coiled cord runs to its port.
- **Lamp.** A warm desk lamp with an articulated arm and a knurled elbow knob. Its lens is a separate object.
- **Seat.** A small restrained seat: floor plate, gas-spring post, fabric cushion, backrest and lap-belt straps.
- **Colours.** Ship palette: graphite `A7_PaintGraphite`, cream as on TERMINAL-ART-V2, `A7_Aluminum`, rubber, and
  `A7_FabricDark` and `A7_FabricStrap` for the seat. All markings are real words, with no NASA insignia and no glow.

Triangles: 22,632. Materials: `RD_DeskKit` (baked maps), `RD_Labels`, `RD_Screen`, `RD_Amber`, `RD_LampWarm`.

## Files

| Path | What |
|---|---|
| `RadioDesk.fbx` | The kit: eight meshes and 15 anchor empties. The file holds no texture records and no paths from the build machine. |
| `RadioDesk.blend` | Editable source: the kit collection only, with textures linked relatively from `textures/`. The local paths were blanked from the file. |
| `materials.json` | Material manifest: V1-style fields, plus maps, colour spaces and URP keywords. |
| `model-report.json` | Bounds, clearances, the screen corners, every anchor with its direction, triangles and materials. |
| `textures/` | 2048² maps. `BaseColor` and `Labels` are sRGB. `MetallicSmoothness` and `Occlusion` are linear. Separate `Metallic` and `Roughness` maps are included for other tools. |
| `renders/` | Real Cycles renders: `01-wide`, `02-seated-view` (from `SeatedCamera` at a vertical FOV of 68°), `03-controls`, `04-handset`, `05-rear` and `06-wire`. The screen content and the desk-lamp light in them are preview only and are not exported. The floor plane is a render prop. |
| `checkpoints/` | Checkpoint 1: the first geometry views, posted in #7. |
| `Source/build_radio_desk.py` | Rebuilds everything above. |
| `Source/verify_kit_fbx.py` | Re-imports an FBX and reports what a consumer receives. |
| `evidence/` | The Blender FBX round trip, the Unity import result and two Unity captures. |

Rebuild (about 75 s on an RTX 4070):
`blender -b --factory-startup --python Source/build_radio_desk.py -- --stage full --out <folder outside Assets>`.

## Verification actually performed (14 Nov 2026, Apoliak's machine)

- **Blender 5.3.0, Cycles/OptiX.** Full build exit 0. All five brief anchors are equal to the brief values.
- **FBX round trip** (`evidence/fbx_roundtrip.json`). The FBX was re-imported into an empty scene.
  - All 15 anchors land where the build report says, within 0.1 mm.
  - `ScreenSurface` is 0.44 × 0.26 m and faces −Z.
  - There are 22,632 triangles, no cameras or lights, and no drive-letter path.
- **Unity 6000.3.28f1, URP.** A batch import in a private clone of the repository
  (`evidence/unity_import.json`) gave `DESK_CHECK_PASS`, exit 0, no compile error.
  - The five brief anchors are at their values within 0.1 mm.
  - All 15 anchors match the build report.
  - `ScreenSurface` is 0.44 × 0.26 m, centred on `ScreenCenter`, faces −Z, and its UV runs 0..1.
  - There is no camera or light.
  - Two captures were made with URP Lit materials built from the delivered maps, under plain directional lights:
    `evidence/RadioDesk_unity_wide.png` and `evidence/RadioDesk_unity_seated_view.png`.

  Unity reports 22,592 triangles in 8 meshes; Blender counts 22,632. The difference was not traced and is most likely degenerate triangles that the importer drops.
- **No local paths.** A byte search finds no path of the build machine in the FBX or the `.blend`.

## Limits and what is not verified

- **Not integrated.**
  - Not placed in the cockpit, so the clearances against the real walls and aisle are unchecked.
  - No prefab and no collider; root decides the collision.
- **Static parts.** The seat's stowed position is a documented offset, not a second pose in the file. Nothing is
  animated. The handset cord is a static coil, so lifting the handset needs root's presentation, such as hiding or
  replacing the cord.
- **Display.** `ScreenSurface` is passive. Root owns the screen content, input and UI.
- **Not measured:** label legibility at gameplay distance and performance.

## Rights and attribution

| Item | Source | Licence |
|---|---|---|
| Geometry, UVs and baked textures | Generated by `Source/build_radio_desk.py`. No external model, texture or scan. | Original team work |
| Legend typeface | Inter, bundled with Blender 5.3 (`datafiles/fonts/Inter.woff2`), rasterised into `RadioDesk_Labels.png` | SIL Open Font License 1.1. Credit: "Inter typeface © The Inter Project Authors" |
| Tool | Blender 5.3.0 (Cycles) | GPL tool; its output carries no licence obligation |

- **Design reference.** The design follows the team's own art direction: board 03 under `docs/art-direction/`, A20 and
  Codex's brief in issue #7.
- **What is not used.** No NASA model, texture, drawing, photograph, logo or insignia is copied or included. No
  third-party game asset or downloaded texture is used.
- **Names.** `AURORA-7`, `CU-1` and the drawer contents are the game's own fiction.
