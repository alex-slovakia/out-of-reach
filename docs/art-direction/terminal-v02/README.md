# MissionTerminal V2 — physical touchscreen terminal art (TERMINAL-ART-V2)

This folder contains new hardware art for the AURORA-7 navigation/Earth terminal. It replaces the art of TERM-V1
(`docs/art-direction/terminal-v01-source`) and keeps its interface exactly: the active display, the five
integration anchors, the object names the V1 handoff uses, the axes and the FBX export settings.

- **Author:** the Claude prop chat (Apoliak's machine). The asset is original, built in Blender 5.3 from one script.
- **Request:** Codex's request in issue #7 (15:47 UTC, 14 Nov 2026).
- **Scope:** only this folder. Nothing under `Assets/` was touched: no prefab, presenter, input, UI, scene or contract.
- **Import:** root imports it into Unity, as for V1.

## Interface — unchanged from TERM-V1

| Fact | Value |
|---|---|
| Axes (Unity) | Origin is the display centre. X is screen right, Y is up, +Z goes into the case, and the screen faces −Z. Unit scale. |
| Active display | `ActiveDisplay`: one quad, 0.640 × 0.400 m on the plane Z = 0, facing −Z. UV 0..1 runs along screen right and up. Material slot `MT_Screen`. |
| `DisplayFrame` | (0, 0, 0) |
| `LeftGripFrame` | (−0.426, 0, −0.040). The 34 mm grip tube (radius 17 mm) runs along Y through this point, from Y −0.145 to +0.145, as in V1. |
| `InteractionAnchor` | (0, −0.100, −0.080) |
| `FocusCameraAnchor` | (0.012, 0.025, −0.660) |
| `WallMountAnchor` | (0, −0.038, 0.318). The back of the wall base lies exactly on this plane. |
| Anchor objects | Five empties with identity rotation, as in V1. The importer still sets the documented `LeftGripFrame` rotation itself. |
| Other V1 names kept | `Left handhold` (the grip tube) and `Status lamp` (same place as V1: X 0.300, Y −0.233, on the bezel face). |
| Envelope | The bezel is 0.766 × 0.510 m, the same as V1. The case runs from Z −0.012 (bezel face) to Z 0.080 (shell back), so V1's casing collider (0.766 × 0.510 × 0.080, centre Z 0.040) still fits. The grip, the controls and the mount stand outside it, as in V1. |
| FBX export | Same settings as V1's `build_terminal.py`: `FBX_SCALE_UNITS`, forward −Z, up Y, modifiers applied, face smoothing, no leaf bones, no animation, meshes and empties only. The faces are also triangulated, and there are no lights, cameras or armatures. |

## What changed (art)

- **Enclosure.** A deep warm-ivory powder-coated bezel (#D6CFBC) on a graphite rear shell (#2B2B2C, the ship's
  `A7_PaintGraphite`). A black rubber seal line runs between the two parts.
- **Recessed screen.** A machined aluminium chamfer, 16 mm wide, steps down from the bezel face to a black rubber
  glass seal. A 6 mm black mask border surrounds the active area, which sits 12 mm behind the bezel face, about as
  deep as V1's reveal so the finger keeps clear of it.
- **Purposeful fasteners.** Eight captive stainless screws in counterbores clamp the bezel to the seal. Four
  socket-head bolts hold the VESA adapter, four hex bolts with washers hold the wall base, and four quarter-turn
  fasteners hold the rear service cover.
- **Restrained tactile controls.** All controls are on the right bezel band:
  - a knurled dimmer knob (`BRT`);
  - four rectangular keys in pockets (`DISP`, `MODE`, `ACK`, `PWR`), with guard ribs on `PWR`;
  - the amber status lamp with `STATUS` beside it.

  There are no pill buttons and no glow.
- **Robust side grip.** The D-handle has a ribbed rubber sleeve and machined end bosses. Its standoff arms end in
  bolted graphite feet. The tube position and radius are exactly V1's, so the glove grip calibration stays valid.
- **Rear tilt mount.** The mount has these parts:
  - a VESA-style adapter plate;
  - a tilt yoke with two knurled friction knobs;
  - an arm with knuckles around the two hinge barrels;
  - lugs on an ivory wall base.
- **Rear details.**
  - a finned heat sink;
  - a service cover;
  - two circular connectors underneath (J1 with backshell, boot and service cable; J2 with a dust cap);
  - a strain relief where the cable enters the wall base.
- **Markings, all real words.**
  - The key legends, `BRT` and `STATUS`.
  - A dark unit plate reading `DU-2 NAV / COMM DISPLAY UNIT`.
  - `AURORA-7` with a short red-orange identity stripe.
  - On the back, an ID plate and an amber `SERVICE COVER / ISOLATE 28 V BEFORE OPENING` label.

  There is no NASA insignia, no logo and no pseudo-text.
- **Material variation.** The textures are baked from procedural sources:
  - light edge wear and darkening in recesses;
  - roughness variation;
  - bright machined faces against painted ones.

| | TERM-V1 | V2 |
|---|---|---|
| Triangles | 12,466 | 14,268: `TerminalBody` 9,806, `Left handhold` 4,352, `Status lamp` 108, `ActiveDisplay` 2 |
| Meshes | 41 separate meshes | 4: `TerminalBody`, `Left handhold`, `Status lamp`, `ActiveDisplay` |
| Materials | 6 flat values | 4: `MT_TerminalBody` (baked maps), `MT_Labels`, `MT_Screen`, `MT_Amber` |
| Bounds (Unity, m) | min (−0.443, −0.273, −0.057), max (0.383, 0.255, 0.3175) | min (−0.4456, −0.3685, −0.0596), max (0.383, 0.255, 0.3175). The lower Y comes from the service cable loop under the case. |

## Files

| Path | What |
|---|---|
| `MissionTerminal.fbx` | The model: four meshes and the five anchor empties. The file holds no texture records and no paths from the build machine. |
| `MissionTerminal.blend` | Editable source: the terminal collection only, as in V1, with textures linked relatively from `textures/`. The local save path was blanked from the file. |
| `materials.json` | Material manifest. It keeps V1's fields (`baseColorLinear`, `metallic`, `roughness`) and adds the URP Lit maps and keywords. |
| `textures/` | 2048² maps. `BaseColor` and `Labels` are sRGB. `MetallicSmoothness` (metallic in RGB, smoothness in A) and `Occlusion` are linear. Separate `Metallic` and `Roughness` maps are included for other tools. |
| `renders/` | Real Cycles renders: `01-front.png` and `02-rear.png` use V1's camera framings, plus `03-controls.png`, `04-grip.png` and `05-wire.png`. The screen content in them is preview only: a dim schematic globe outline drawn from scratch and V1's captions. It is not exported, because the runtime draws the display. |
| `model-report.json` | The build report: dimensions, triangles, materials, textures, and each anchor compared with V1. |
| `Source/build_mission_terminal_v2.py` | Rebuilds everything above. |
| `Source/verify_terminal_fbx.py` | Re-imports an FBX and reports what a consumer receives. |
| `evidence/` | Results of the checks below: Blender FBX round trips of V2 and V1, the Unity import comparison and three Unity captures. |

Rebuild (about one minute on an RTX 4070):
`blender -b --factory-startup --python Source/build_mission_terminal_v2.py -- --stage full --out <folder outside Assets>`.
`--stage geometry` is a 12-second shape check with flat colours.

## Verification actually performed (14 Nov 2026, Apoliak's machine)

- **Blender 5.3.0, Cycles/OptiX.** Full build exit 0. The script compares every anchor with the handoff values:
  all five are equal. The active display measures 0.640 × 0.400 m.
- **FBX round trip** (`Source/verify_terminal_fbx.py`). Both files were re-imported into empty scenes: the V2 FBX
  and a V1 FBX exported from the published V1 `.blend` with V1's own settings.
  - The five anchors land at the same positions in both.
  - `ActiveDisplay` is 0.640 × 0.400 m, faces −Z, and its UV is (0,0) at the bottom-left and (1,1) at the top-right.
  - V2 has 14,268 triangles, unit scale, no cameras or lights, and no drive-letter path.
  - Evidence: `evidence/fbx_roundtrip_v2.json` and `evidence/fbx_roundtrip_v1.json`.
- **`.blend` check.** The file reloads with the nine terminal objects and the `MissionTerminal` collection, and its
  image paths are relative. A byte search finds no path of the build machine in the `.blend` or the FBX.
- **Unity 6000.3.28f1, URP.** A batch run in a private clone of the repository (`evidence/unity_import.json`)
  imported the V1 FBX and the V2 FBX side by side with default import settings. In both files:
  - all five anchors sit at the handoff positions relative to the model root, within 0.1 mm;
  - they share the same local rotation, 270° about X from Blender's axis conversion;
  - `ActiveDisplay` is 0.640 × 0.400 m on Z = 0, faces −Z, and its UV runs 0..1 along screen right and up.

  V1 and V2 match on every anchor, every display corner and the facing. V2 has 4 meshes, 14,268 triangles and the
  materials `MT_TerminalBody`, `MT_Labels`, `MT_Screen` and `MT_Amber`. It has no camera or light. The run made
  three captures with URP Lit materials built from the delivered maps:
  - `evidence/MissionTerminal_unity_front.png`;
  - `evidence/MissionTerminal_unity_rear.png`;
  - `evidence/MissionTerminal_unity_focus_camera.png`, from `FocusCameraAnchor` at FOV 68.

  They use plain directional lights and preview materials, not the game's lighting or display shader. Exit 0,
  no compile error. The temporary check script lived only in that clone and is not part of this delivery.

## Limits and what is not verified

- **Not integrated.** Root's `PhysicalTerminalBuilder` is not in the repository, so it was not run against V2. No
  prefab was made. The display shader, finger contact, glove grip pose and placement in the ship have not been
  seen with V2.
- **Possible importer changes.** The importer may need two adjustments:
  - **Material assignment.** V1 had 41 meshes with six flat materials; V2 has four meshes. If the builder looks up
    V1 part names other than `ActiveDisplay`, `Left handhold` and `Status lamp`, those lookups need updating.
  - **Maps.** The builder must read the maps from `materials.json`.
- **Collider coverage.** V1's casing collider still covers the case. Like V1, the grip, controls and mount have no
  collision.
- **Not measured:** performance and label legibility at gameplay distance. The legends are 4.7 to 10.5 mm tall.
- **Fixed parts:** one LOD, no normal map, no lightmap UVs, no moving parts. The knob, keys and hinges are static
  geometry.

## Rights and attribution

| Item | Source | Licence |
|---|---|---|
| Geometry, UVs and baked textures | Generated by `Source/build_mission_terminal_v2.py`. No external model, texture or scan. | Original team work |
| Legend typeface | Inter, bundled with Blender 5.3 (`datafiles/fonts/Inter.woff2`), rasterised into `MissionTerminal_Labels.png` | SIL Open Font License 1.1. Credit: "Inter typeface © The Inter Project Authors" |
| Tool | Blender 5.3.0 (Cycles) | GPL tool; its output carries no licence obligation |

- **Design reference.** The design follows the team's own art direction: issue #7, `docs/playtest-review/01-feedback.md`
  (B22), and the review boards under `docs/art-direction/`. It was reworked from the dimensions and anchors of TERM-V1.
- **What is not used.** No NASA model, texture, drawing, photograph, logo or insignia is copied or included. No
  third-party game asset or downloaded texture is used. The renders contain no NASA imagery: the globe on the
  preview screen is a schematic outline drawn by the script.
- **Names.** `AURORA-7` is the game's fictional ship. `DU-2` and the serial number are fictional.
