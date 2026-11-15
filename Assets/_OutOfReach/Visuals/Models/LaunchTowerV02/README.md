# LaunchTowerV02 — passive exterior detail for the crew access tower

Task TOWER/BOARDING-BRIDGE-V02 (issue #7, scope correction 6894439167). This is an **exterior visual remodel only**:
a passive truss, rail, cable and pipe detail model fitted to the measured existing tower, deck module and access arm
of the I06 prologue. It does **not** replace, move or re-lay-out any existing building, hut, ship, platform, collider,
scene, prefab or script, and it adds no interior or cockpit work. Root (Codex) keeps the scene and integrates it.

## Files

| Path | What |
|---|---|
| `Models/LaunchTowerV02.fbx` | 4 mesh objects, 54,942 triangles, Git LFS |
| `Materials/TW_*.mat` | 8 URP Lit materials (remapped in the FBX `.meta`) |
| `Textures/TW_*.png` | procedural textures (BaseColor, MetallicSmoothness, grating cut-out, label atlas) |
| `anchors.json` | frame, importer settings, measured existing anchors, keep-clear rules, 17 named anchors, per-object bounds |
| `Models/LaunchTowerV02_report.json` | Blender build report (triangles per object and material, bounds in Unity metres) |
| `Models/LaunchTowerV02_clearance.json` | BVH clearance scan against the existing meshes, both arm poses, open hatch |
| `Previews~/*.png` | Cycles renders next to the existing structures (Unity ignores `~` folders) |
| `Source~/` | generator scripts (Blender 5.3 + Python), the clearance scan, the Unity import check and its last output |
| `Provenance/ATTRIBUTION.md` | rights |

## Frame and placement (for root)

- Unity metres, y up, the **same root frame as `MarinePrologue/Transport/TransportLaunch.fbx`** and `MarineLayout.json`
  (the `I06 isolated marine prologue` root). Instantiate the FBX at identity (position 0, rotation 0, scale 1) under that root.
- `LTV2_TowerFacade`, `LTV2_DeckSupport`, `LTV2_Deluge`: static, origin (0, 0, 0). They belong next to the stationary
  `Launch site - stationary` content.
- `LTV2_ArmDetail`: origin = **AccessArmPivot (-0.9, 0, -6.9)**. Make it a child of
  `Access arm control +Y 0 connected to -90 retracted` (keep world position, `SetParent(frame, true)`), so it swings with
  `AccessArm`. Checked in both poses (0 and -90 deg).
- No colliders anywhere. The existing `COL_*` boxes remain the only collision; nothing here is a walking surface.
- It assumes the 72 sea-level dock pieces stay disabled as in `I06MarineAuthoring.ApplyElevatedDeckVariant`.

Importer (already in `LaunchTowerV02.fbx.meta`): Bake Axis Conversion **ON**, Scale Factor 1, Use File Scale ON,
Preserve Hierarchy ON, no animation, no cameras or lights, mesh compression off, normals imported,
materials via material description with `externalObjects` remap to `Materials/TW_*.mat`.

## Objects

| Object | Triangles | What |
|---|---:|---|
| `LTV2_TowerFacade` | 38,384 | bolted corner-leg truss 0.5 m outside the shaft (4 legs, 11 ring beams, Warren diagonals with lacing struts, 88 gussets, leg splices, base plates with anchor bolts and stiffeners, hazard guards), caged ladder on -x, three grated rear landings with ochre rails, two cable trays with junction boxes, bolted cladding battens on the shaft, ochre top bands, AURORA-7 plates and level plates L1–L10 |
| `LTV2_DeckSupport` | 5,336 | knee braces with gussets under the deck overhang, outboard grated service ledge with ochre rails on +x, -x and -z, slew pedestal under the arm pivot |
| `LTV2_Deluge` | 5,588 | red DN200 deluge riser with flanged joints and standoffs, base valve station V-07 (handwheel, gauge, supports), drencher header under the deck with 14 downward heads and hanger rods, two scuppers with galvanised downpipes to grated catch basins on the pad |
| `LTV2_ArmDetail` | 5,634 | slew housing with bolted ring and drive, twin Warren trusses with gussets under the arm floor and plan bracing, energy chain plus galvanised and red fluid lines with a hazard coupling on the +x outside, crew-connection hood (portal, header with hazard strip, sill, rubber boot rings) with the AURORA-7 crew hatch plate |

Materials: `TW_SteelPainted` (graphite paint, chips), `TW_Galvanized`, `TW_OchrePaint`, `TW_HazardStripe`, `TW_PipeRed`,
`TW_Rubber`, `TW_Grating` (alpha clip 0.5, both faces), `TW_Labels`. Textures 1024 px (grating 512, label atlas 2048 x 1024).
Palette follows the graphite / amber / red direction; no agency logos, only fictional AURORA-7 marks.

## Fit to the existing structures (measured, read only)

Measured from `TransportLaunch.fbx`, `MarineDock.fbx` and `MarineLayout.json` at `codex/i01-integration@c772511`:
shaft 3.3 x 3.3 m (x +-1.65, z -10.15..-6.85) from the pad top y -32.6, top slab y -0.67..0.2; deck module x +-4,
z -12.9..-6.9, deck top y 0 (dock root (0, -0.021, -9.9)); access arm x +-0.98, z -6.9..-2.48, floor underside y -0.55,
rail top 1.128, pivot (-0.9, 0, -6.9); hatch pivot (-0.72, 0, -2.4), open 160 deg; capsule shell radius 2.577.

Keep-clear rules held (all checked, 0 vertices inside and 0 surfaces crossing):
arm walkway x +-0.93, y 0.02..2.2, z -6.9..-2.5; deck walking area x +-3.9, y 0.05..2.5, z -12.8..-7.0;
hatch doorway x +-0.95, y 0.02..2.2, z -3.4..-2.3. The facade keeps at least 3 m to the booster and capsule.
The rocket-side legs stop at y -1.45 because the arm trusses sweep through that corner when the arm retracts; the energy
chain and fluid lines sit on the +x side so a retracted arm does not push them into the deck edge rails.

## Verification actually run

1. `blender -b --factory-startup --python Source~/build_tower.py -- --export` (Blender 5.3.0): builds the four objects, exports
   the FBX (`apply_scale_options=FBX_SCALE_UNITS`, `bake_space_transform=True`, `-Z` forward, `Y` up, triangles, no texture
   paths: `grep -a alexp` finds nothing) and writes the report.
2. `blender -b --factory-startup --python Source~/check_clearance.py -- TransportLaunch.fbx MarineDock.fbx Models/LaunchTowerV02_clearance.json`:
   BVH overlap and nearest-distance scan against the existing meshes with the scene poses. The only overlapping triangle
   pairs are by-design attachment contacts (seat plates and flanges under the top slab and girders, the slew bearing ring
   between pedestal and housing, hanger rods, cladding battens over the shaft ribs, arm trusses under the arm floor).
   Nothing touches the capsule, booster or the open or closed hatch (arm detail min 0.37 m to the open hatch leaf,
   0.53 m to the shell). Static parts vs the arm detail in the retracted pose: 0 overlaps except the bearing ring.
3. Isolated Unity 6000.3.28f1 batch import in my own sparse checkout (`Source~/Unity/LaunchTowerV02ImportCheck.cs`, copied in
   temporarily, never committed into `Assets`): **PASS** — triangles per object equal the Blender report (54,942 total),
   bounds and origins within 0.04 mm of the report in Unity metres, lossy scale 1, local rotation 0, 8 materials bound
   (`Source~/unity_import_check.txt`).
4. `blender -b --factory-startup --python Source~/render_views.py -- ...`: eight Cycles renders in `Previews~/` (player walkway
   to the open hatch, view over the arm rail, under-deck and arm, distance with the rocket, rear landings, base and valve,
   worm's-eye from the pad, arm retracted). The existing structures use neutral stand-in materials there because their
   textures are not part of this delivery; the ground is a flat plane at the pad level.

Rebuild: `python -I Source~/tw_tex.py Textures <Inter-Regular.otf>` (textures), then step 1, 2, `python -I Source~/make_anchors.py .`.

## Limits (honest)

- No in-scene integration and no Unity URP capture of the final scene: root owns the scene; the Unity check proves the
  import, scale, axes, counts and material binding only. The renders are Blender Cycles.
- No LODs. 54,942 triangles, about 70 % of them bolt heads and gussets on the facade; if the far view needs it, root can
  disable the bolt-heavy facade beyond about 120 m (or ask me for a LOD1 without bolts).
- Bolts, gussets and cladding are geometry; the textures are tiling (paint chips, galvanised spangle, rain streaks), there
  is no baked ambient occlusion or unique grime.
- The retracted pose was checked as a -90 deg rotation about the pivot. The existing arm itself already clips the deck's
  boarding rails by about 7 cm in that pose; that is existing geometry and was not changed.
- Built by Claude (Apoliak7777): increments 1–2 on Claude Sonnet 5.5, the rest on Claude Opus 5.5.
