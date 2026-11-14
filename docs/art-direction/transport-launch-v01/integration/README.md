# TRANSPORT-FIT-02 — derivative for root's staging module

This is a derivative of TRANSPORT-LAUNCH-ART-V1 (`../`, delivered at `6df81d3`). It fits root's 8 × 6 m staging
module, as Codex asked in issue #7 at 17:49 UTC. The original package in `../` is unchanged. Everything here comes from
the same script, `../Source/build_transport_launch.py`, run with `--fit02`. It is a fictional visual vehicle, not
engineering.

## What changed against `6df81d3`

- **Mast split.**
  - `AccessMast` is now `AccessMastTower`. The tower ends at Y −0.59, under the module, with no landing and no top
    light.
  - The old floor-level landing and its rails are kept as a separate optional object, `AccessMastLanding`. Omit it
    when you use your module. It is hidden in the renders.
- **Module support.** Restrained steel beams with their tops at Y −0.35 run under your module footprint (x ±4,
  z −12.9 to −6.9): two longitudinal beams at x ±2.4 and three cross beams. Six braces run down to the tower columns.
  There is no new base. If your module's underside sits deeper than 0.33 m below its root, lower `BEAM_TOP` in the
  script and rebuild.
- **`StagingModuleRoot`.** An anchor empty at (0, −0.021, −9.90), facing +Z, where your module root goes. Its forward
  edge meets the arm at Z −6.9.
- **Edge guards.**
  - Fixed guard stubs (posts and two rails) at Z −6.88 close the gaps between your 2.2 m exit (x ±1.10) and the 1.8 m
    arm (x ±0.90). They stand on the front cross beam, and the arm swings away from them.
  - A visible rail covers the +X side of the arm throat (Z −3.10 to −2.64).
  - The −X side of the throat has a proxy only: a fixed rail there would cut through the hatch while it swings past
    90°. When the hatch is fully open (160°), it lies across that side.
- **Proxies (`model-report.json` → `colliders`).**
  - The sill filler you specified: centre (0, −0.05, −2.425), size (1.4, 0.10, 0.35), parent `CapsuleShell`.
  - `ThroatGuardL` and `ThroatGuardR`, parent `AccessArm`, so they move with it.
  - `ModuleEdgeGuardL` and `ModuleEdgeGuardR`, parent `AccessMastTower`.
  - Six `LandingRail*` boxes, parent `AccessMastLanding`, used only if the landing is kept.
  - `MastTower` is shortened to the new tower top.
  - `HatchDoor` stays on `Hatch`, and `ArmFloor` and `ArmRail*` stay on `AccessArm`.
- **Clear route** from your module to the hatch: 2.2 m at your exit, 1.8 m on the arm, 1.5 m in the throat and 1.40 m
  at the hatch. It is never under 1.2 m.

Everything else is identical to `6df81d3`: the capsule, interior, hatch, stack, barge, the 15 anchors, materials and
moving-part ranges.

## Files

| Path | What |
|---|---|
| `TransportLaunch.fbx` | The derived kit. Meshes and 16 anchor empties (the 15 originals plus `StagingModuleRoot`). |
| `TransportLaunch.blend` | Editable source; textures are relative and the local paths were blanked. |
| `model-report.json` | Anchors, moving parts, colliders (corrected and new), dimensions and bounds. |
| `materials.json` | Same material set-up as `../materials.json`, with the maps in `textures/` here. |
| `textures/` | Re-baked atlases, because the UVs of the changed objects differ from `../textures/`. |
| `renders/` | Real Cycles renders: `fit_01-boarding-from-module` (eye at 1.65 m, 0.8 m behind your module's front edge), `fit_02-module-support` and `fit_03-launch-ready-arm-retracted`. The grey plate is a **render-only stand-in for your module**, not your model. |
| `evidence/` | The Blender FBX round trip, the Unity import check and its captures. |

Triangles: 24,614 in total (`CapsuleShell` 5,844, `CapsuleInterior` 5,960, `Hatch` 616, `BoosterStack` 2,276, `LaunchBarge` 5,818, `AccessArm` 1,320, `AccessMastTower` 1,444, `AccessMastLanding` 812, `CabinLights` 220, `HatchStatusLamp` 84, `DeckLamps` 220).

## Verification actually performed (14 Nov 2026, Apoliak's machine)

- **Blender 5.3.0, Cycles/OptiX.** Full build with `--fit02` exit 0. Three atlases baked, labels rendered, three renders written.
- **FBX round trip** (`evidence/fbx_roundtrip.json`): all 16 anchors land where the report says (within 0.1 mm), including `StagingModuleRoot` at (0, -0.021, -9.9); the `Hatch` and `AccessArm` origins sit on their pivots; no cameras or lights; a byte search finds no user, folder or file path.
- **Collider manifest:** 42 boxes; new: `HatchSillFiller`, `ThroatGuardL`, `ModuleEdgeGuardL`, `ThroatGuardR`, `ModuleEdgeGuardR`.
- **Unity 6000.3.28f1, URP**, batch import in a private clone (`evidence/unity_import.json`): `TRANSPORT_CHECK_PASS`, exit 0. The 15 brief anchors present, `Hatch` and `AccessArm` origins 0 m from their pivots, 11 meshes, 24,566 triangles, no camera or light. Captures with plain lights: `evidence/TransportLaunch_unity_boarding.png`, `_exterior.png`.

## Limits

- Your real module was not available here, so its fit on the beams is checked against the footprint and root height
  you gave, not against its mesh.
- Not integrated, no prefab. Performance is not measured.
