# AntennaRepair V1 — antenna repair art kit (ANTENNA-REPAIR-ART-01)

This folder contains a standalone art kit for the antenna repair task: a bulkhead data connector in a damaged and a
repaired state, and a tethered service wrench. It answers Codex's request in issue #7 (18:35 UTC, 14 Nov 2026).

- **Author:** the Claude prop chat (Apoliak's machine). The kit is original, built in Blender 5.3 from one script.
- **Scope:** only this folder. The delivered ship and antenna, the scenes, gameplay source, UI and contracts are
  unchanged.
- **What it is:** art with anchors and object origins for root's repair interaction. It has no logic, prefab or
  collider.

## Interface

| Fact | Value |
|---|---|
| Axes (Unity) | Origin = `ConnectorMount`, the wall face the connector flange is screwed to (plane Z = 0). Y is up. The technician works from −Z and faces +Z. Unit scale. |
| Objects | `ConnectorReceptacle` (static: flange, receptacle and the cable grommet below it), `ConnectorDamaged`, `ConnectorRepaired` and `ServiceWrench`, plus eight anchor empties. |
| Swapping states | Show exactly one of `ConnectorDamaged` and `ConnectorRepaired` over `ConnectorReceptacle`. All three have their origin on `ConnectorMount`, so the swap needs no offset. Each state carries its own cable, which ends in the shared grommet. |
| Wrench pivot | `ServiceWrench` has its origin on `WrenchGrip`, the centre of the 35 mm glove grip; the grip axis is +X. The wrench pose in the file is a display pose only. |
| Engaging the wrench | Put `WrenchTip` (the centre of the C-jaw, jaw axis +Y as modelled) on `PlugGripBackedOut` for the damaged plug or on `PlugGrip` for the seated one, and turn the jaw axis onto the connector axis (Z). The 34 mm jaw opening passes over the 17 mm cable. Seating the plug is a 0.012 m move along +Z with a 24° turn. |
| FBX export | Same convention and settings as TERMINAL-ART-V2 and RADIO-DESK-V1: Blender = (−X, −Z, Y) of Unity, `FBX_SCALE_UNITS`, forward −Z, up Y, triangulated, meshes and empties only. In Unity each object shows `localEuler (270.02, 0, 0)` from the axis conversion (float rounding of −90°); positions come out right. |

Anchors in Unity metres. The empties have identity rotation; the directions below are also in `model-report.json`.

| Anchor | Position | Direction | Meaning |
|---|---|---|---|
| `ConnectorMount` | (0, 0, 0) | (0, 0, -1) | Wall face under the flange; origin of the three connector objects. Direction = out of the wall. |
| `CableEntry` | (0, 0, -0.156) | (0, 0, -1) | Where the repaired cable leaves the boot. |
| `CableGrommet` | (0.015, -0.24, 0) | (0, 0, -1) | Where the cable enters the wall (grommet centre). |
| `PlugGrip` | (0, 0, -0.045) | (0, 0, 1) | Coupling-nut centre, plug seated. Direction = push to seat. |
| `PlugGripBackedOut` | (0, 0, -0.057) | (0, 0, 1) | Coupling-nut centre in the damaged state (0.012 m further out). |
| `WrenchGrip` | (0.38, -0.03, -0.2) | (1, 0, 0) | Hand pivot: grip centre, origin of `ServiceWrench`. Direction = grip axis. |
| `WrenchTip` | (0.2, -0.03, -0.2) | (0, 1, 0) | C-jaw centre. Direction = jaw axis as modelled. |
| `WrenchTether` | (0.475, -0.03, -0.2) | (1, 0, 0) | Tether ring centre. |

## What it contains

- **Receptacle (static).** A 90 mm cream flange with four screws, an aluminium receptacle barrel with a key ridge and
  six contact pins, and a white alignment tick at 12 o'clock. A 48 mm grommet plate with a rubber grommet sits
  0.24 m below; the cable enters the wall there.
- **Damaged state.**
  - The plug is backed out 12 mm and turned 24°: its white lock mark no longer lines up with the flange tick, and the
    contact pins show in the gap.
  - The coupling nut and backshell are scorched, the boot is melted short and a blotchy soot halo marks the flange.
  - The cable jacket is torn open: nine copper strands are bird-caged out of it and two broken strands stick out.
- **Repaired state.** The plug is seated and locked with the marks aligned. A steel safety wire runs from the nut to a
  flange screw, and a yellow ID sleeve marks the new cable, which loops into the grommet.
- **Service wrench.** An original tethered C-jaw connector wrench, 0.33 m long:
  - a 60 mm steel jaw lined with a rubber soft jaw, open on one side so it slips over the cable;
  - a ratchet lever with a knob and a flat steel shaft;
  - a 120 mm ribbed rubber grip with aluminium end collars;
  - a yellow tether ring.
- **Colours.** Cream panel, aluminium, steel, black rubber, dark anodised nut, copper, soot and safety yellow, baked
  into one material.

Triangles: 7,174 (`ConnectorReceptacle` 1,114, `ConnectorRepaired` 1,846, `ConnectorDamaged` 3,006,
`ServiceWrench` 1,208). Material: `AR_Kit` (baked maps).

## Files

| Path | What |
|---|---|
| `AntennaRepair.fbx` | The kit: four meshes and eight anchor empties. The file holds no texture records and no paths from the build machine. |
| `AntennaRepair.blend` | Editable source: the kit collection only, with textures linked relatively from `textures/`. The local paths were blanked from the file. |
| `materials.json` | Material manifest for URP Lit: maps, colour spaces and keywords. |
| `model-report.json` | Anchors with directions, object descriptions, bounds, dimensions, triangles and materials. |
| `textures/` | 2048² maps. `BaseColor` is sRGB. `MetallicSmoothness` (metallic in RGB, smoothness in A) and `Occlusion` are linear. |
| `renders/` | Real Cycles renders: `01-overview-repaired`, `02-connector-damaged`, `03-connector-repaired`, `04-service-wrench` and `05-wrench-on-backed-out-nut` (the wrench engaged by the rule above). The grey wall is a render prop and is not exported. |
| `Source/build_antenna_repair.py` | Rebuilds everything above. |
| `Source/verify_kit_fbx.py` | Re-imports an FBX and reports what a consumer receives. |
| `evidence/` | The Blender FBX round trip, the Unity import result and two Unity captures. |

Rebuild (about 70 s on an RTX 4070):
`blender -b --factory-startup --python Source/build_antenna_repair.py -- --stage full --out <folder outside Assets>`.

## Verification actually performed (14 Nov 2026, Apoliak's machine)

- **Blender 5.3.0, Cycles/OptiX.** Full build exit 0.
- **FBX round trip** (`evidence/fbx_roundtrip.json`). The FBX was re-imported into an empty scene.
  - All eight anchors land where the build report says, within 0.1 mm.
  - The three connector objects have their origin on `ConnectorMount`, and the wrench on `WrenchGrip`.
  - There are 7,174 triangles, one UV map per mesh, no cameras or lights, and no drive-letter path.
- **Unity 6000.3.28f1, URP.** A batch import in a private clone of the repository (`evidence/unity_import.json`) gave
  `AR_CHECK_PASS`, exit 0, no compile error.
  - All eight anchors and the four object origins are at their values within 0.1 mm.
  - With `WrenchTip` on `PlugGripBackedOut` and the jaw axis turned onto Z, 0 wrench vertices lie inside the coupling nut (radius 0.0285 m). The closest wrench vertex in the nut's length is 0.0300 m from the connector axis, and 174 jaw vertices lie on the 0.030 m circle around it.
  - There is no camera or light. Two captures were made with URP Lit materials built from the delivered maps:
    `evidence/AntennaRepair_unity_engaged_damaged.png` and `evidence/AntennaRepair_unity_repaired_overview.png`.

  Unity reports 7,158 triangles in 4 meshes; Blender counts 7,174. The difference was not traced and is most likely degenerate triangles that the importer drops.
- **No local paths.** A byte search finds no path of the build machine in the FBX or the `.blend`.

## Limits and what is not verified

- **Not integrated.** The kit is not placed on the ship or the antenna, so the fit against the real mounting surface
  is unchecked. The receptacle needs a flat area of about 0.09 × 0.29 m (flange plus grommet).
- **Static parts.** Nothing is animated or rigged. The cables are rigid, and the swap between the states is instant.
- **No colliders or prefab.** Root decides collision and interaction.
- **The wrench check** proves only that the jaw clears the coupling nut when engaged by the anchor rule. It says
  nothing about the hand pose.
- **Not measured:** performance and readability at gameplay distance.

## Rights and attribution

| Item | Source | Licence |
|---|---|---|
| Geometry, UVs and baked textures | Generated by `Source/build_antenna_repair.py`. No external model, texture or scan. | Original team work |
| Tool | Blender 5.3.0 (Cycles) | GPL tool; its output carries no licence obligation |

- **What is not used.** No NASA model, texture, drawing, photograph, logo or insignia is copied or included. No
  third-party game asset or downloaded texture is used.
- **Hardware.** The connector and the wrench are generic fictional hardware, not copies of a specific flight part.
