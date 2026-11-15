# LaunchTowerV03 — umbilical tower and crew access arm with white room

Original replacement for the launch tower and the walkway to the capsule in the starting (marine prologue) scene.
It replaces the *visuals* of `TransportLaunch.fbx` `AccessMastTower` + `AccessArm` and of the `LaunchTowerV02` detail
overlay. It is a drop-in: same frame, same arm pivot, same walking heights; no existing collider, anchor, control frame,
script or scene is edited by this delivery. **`Bootstrap.unity` is untouched — the integrator runs the installer.**

## Install (integrator)

1. Open `Assets/_OutOfReach/Scenes/Bootstrap.unity` (not in Play mode; access arm saved in its connected pose).
2. Menu **OUT OF REACH ▸ Launch Tower V03 ▸ Install into open scene**. Read the Console report, look at it, save the scene.
   Batch alternative: `-executeMethod OutOfReach.Visuals.LaunchTowerV03.Editor.LaunchTowerV03Installer.InstallIntoBootstrapBatch`.
3. Undo with Ctrl+Z or **… ▸ Uninstall from open scene (restore V02)**.

What the installer does (additive, reversible, idempotent):
- instantiates `Prefabs/LaunchTowerV03.prefab` at identity under `I06 isolated marine prologue`, keeps it with
  `Launch site - stationary`, and re-parents `LTV3_AccessArm` under `Access arm control +Y 0 connected to -90 retracted`
  (world pose kept) so it swings with the existing arm animation — the same wiring as the V02 `LTV2_ArmDetail`;
- copies the layer of the existing `COL_ArmFloor` onto the V03 colliders;
- deactivates `Launch tower V02 detail - b330226` and `LTV2_ArmDetail` (not deleted, so
  `ClaudeIntegrationAuthoring.Validate` still finds them) and disables only the **renderers** under `AccessMastTower`
  and `AccessArm`; all `COL_*` boxes stay.

**Rebuild Materials and Prefab** re-runs the texture/model import settings, the 12 URP Lit materials and the prefab.

## Files

| Path | What |
|---|---|
| `Models/LaunchTowerV03.fbx` | 2 mesh objects, **28,046 triangles** (tower 25,468, arm 2,578), no LODs |
| `Prefabs/LaunchTowerV03.prefab` | `LTV3_Tower`, `LTV3_AccessArm` (origin = arm pivot), 8 box colliders, 3 anchors |
| `Materials/LT3_*.mat` | 12 URP Lit materials (graphite steel, safety amber, warning red, galvanised, grating cut-out, white room, hazard, rubber, dark glass, warm/red/cyan emissive lamps) |
| `Textures/LT3_*.png` | 12 procedural tileable textures, 1024 px (grating 512): BaseColor, Normal. Metallic/smoothness are material scalars |
| `Editor/` | builder + installer (own Editor-only asmdef, no project references) |
| `Models/LaunchTowerV03_report.json` | Blender build report: triangles per object/material, bounds, keep-clear vertex counts |
| `Source~/` | `build_tower_v03.py`, `lt3_tex.py`, `LaunchTowerV03.blend`, the Unity scratch check and its output |
| `Previews~/` | Blender Eevee renders (`LTV3_*.png`) and Unity URP captures from the scratch check (`urp_*.png`) |

## What is modelled

- **Tower** (5 x 6 m, pad y -32.6 to the deck underside): wide-flange corner legs on grouted base plates with anchor
  bolts and hazard collars, 11 braced panels per face (horizontals, X-diagonals, gussets, splice plates), plan bracing,
  three grated service levels with amber guard rails and toe boards, deck-bearing head girders with knee braces.
- **Stair tower** on +x: 21 switchback flights with grating treads, amber nosings and handrails, braced frame, bridges
  to the service levels. Caged ladder and vertical cable tray with cable bundle on -x.
- **Services**: red fire-water riser with flanges, valve station and drencher header; two retracted umbilical swing
  arms with hose loops and hazard carrier plates (parked 1.2 m off the booster, nothing attached to the vehicle).
- **Lightning / lighting pole** at the rear +x corner beyond the deck (x 4.5, z -13.4) up to y +23.6: tapered steel
  pole with flanged base on an outrigger girder, floodlight crossarm, air terminal, red obstruction lights. Lamps are emissive meshes only — no `Light` components (the lighting lane decides).
- **Crew access arm**: slewing drive at the pivot, grated walkway on cross beams, Pratt side trusses with portals,
  standing-seam roof, amber handrails, walkway lamps, energy chain and service lines on +x, hazard thresholds.
- **White room** at the hatch end (z -4.75..-2.74): white panelled walls with windows, walk-through door with hazard
  header and cyan status lamps, ceiling light strip, grab bars, rubber boot seal toward the capsule.

## Frame, pivots, corridors (unchanged from the existing scene)

Unity metres in the prologue root frame (same as `TransportLaunch.fbx` / `MarineLayout.json`). Tower origin (0,0,0);
arm origin = `AccessArmPivot` (-0.9, 0, -6.9). Walkway top y = 0. Keep-clear boxes hold with **0 vertices inside**:
arm walkway x ±0.93, y 0.02..2.2, z -6.9..-2.5; hatch doorway x ±0.95, y 0.02..2.2, z -3.4..-2.3; crew deck
x ±3.9, y 0.05..2.5, z -12.8..-7.0. Colliders: floor (top y 0, coincides with `COL_ArmFloor`), two rails, white-room
walls, door jambs, ceiling (underside y 2.38).

## Verification actually run

1. `blender --background --factory-startup --python Source~/build_tower_v03.py -- <dir> --save --render`, then the same
   with `--export` only (Blender 3.6.1). Export runs separately so the FBX carries no local path.
2. Unity **6000.3.28f1** batch run in an empty scratch URP 17.3.0 project (not the game project):
   `Source~/Unity/LaunchTowerV03ScratchCheck.cs`, output in `Source~/Unity/unity_scratch_check.txt` — **RESULT PASS**:
   both scripts compile, 28,046 triangles after import, tower at (0,0,0), arm at (-0.9,0,-6.9), all materials URP Lit,
   installer run twice against a stand-in hierarchy with the Bootstrap object names (rotated, offset root): arm under
   the control frame at the pivot, V02 deactivated, blockout renderers off, layer copied, floor ray hit at y 0.0000,
   walkway corridor free of colliders. URP captures (`urp_*.png`) use read-only copies of the real
   `TransportLaunch.fbx` and `MarineDock.fbx` in their Bootstrap poses (neutral grey), incl. the player's view from
   `GroundEntry` (prologue (0, 1.7, -12.34)) toward the arm.

## Fix 1 after the first playtest (release session, 15 Nov)

- **Cyan box at the top**: the 5 m-wide lattice mast directly behind the spawn is gone (it towered over the arrival
  platform). Lamp materials now share the single URP Lit keyword set of the other LT3 materials (`_NORMALMAP` +
  `_EMISSION`), so no separate shader variant is compiling (editor cyan placeholder) on first view, and emission is lower.
- **Pitch-black lattice**: the graphite was metallic map driven and very dark; with no reflection probe a metallic
  surface only reflects the dark ambient. Now non-metallic paint (metallic 0, smoothness 0.38), base colour lifted to
  a mid graphite, no metallic/smoothness maps at all. Galvanised and grating stay metallic 0.5.
- **Placement**: the walkway is unchanged (same pivot, floor y 0, z -6.9..-2.62, meets the crew-deck edge at
  z -6.9); the only structure above deck level is now the slim pole outside the deck at the rear +x corner.
- Installer re-runs the builder when a prefab material lost its base texture (e.g. an un-smudged LFS checkout).

## Limits (honest)

- **Not installed in and not tested against the real `Bootstrap.unity`**; no playtest; no compile in the game project
  itself. The installer was only exercised on a stand-in hierarchy with the same object names.
- `AccessMastTower` renderers are switched off wholesale. If that mesh also carries something that should stay visible
  at deck level, re-enable that renderer (the Console report lists each one).
- Retract pose: like the existing arm (reach 4.80 m from the pivot), the V03 arm head (boot-seal corner, about 4.73 m)
  passes through the capsule cylinder envelope (r 2.577) mid-swing. Existing behaviour, not made worse; the swing
  itself was not simulated against the real capsule mesh.
- `ClaudeIntegrationAuthoring.Validate` keeps passing only because V02 stays in the scene (inactive).
- No LODs, no baked AO; UVs are box-projected tiling. Stair flights and ladder have no colliders (not a player route).
