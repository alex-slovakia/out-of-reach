# LaunchComplexV01: AURORA launch vehicle and launch pad dressing

Original drop-in visuals for the opening (prologue) scene:

- **(A) Launch vehicle**: the booster stack the crew capsule sits on, under
  `I06 isolated marine prologue/Transport vehicle - root controls launch`. It replaces only the *visual* of
  `BoosterStack`. The capsule, hatch, seats, anchors, `EngineEmitter`, `COL_Booster` and the launch root are untouched.
- **(B) Pad dressing** under `I06 isolated marine prologue/Launch site - stationary`: launch mount with hold-downs,
  tail service masts and rainbird deluge heads, flame trench lining, water-deluge tower with its ground main,
  propellant farm (LOX sphere, two fuel bullets, pipe rack, pad terminal, cable tray), two floodlight towers, four
  lightning masts with a catenary ring, four service vehicles, barriers, bollards, marker lights and pad markings.

`Bootstrap.unity` is **not modified by this delivery**. The integrator runs the installer and saves.

## Install (integrator)

1. Open `Assets/_OutOfReach/Scenes/Bootstrap.unity` (not in Play mode).
2. Menu **OUT OF REACH > Launch Complex V01 > Install into open scene**. Read the Console report, look, save.
3. Undo with Ctrl+Z or **... > Uninstall from open scene (restore old visuals)**.

Batch (release coordinator; these two open Bootstrap, install and **save** it):

- rocket only: `-executeMethod OutOfReach.Visuals.LaunchComplexV01.Editor.LaunchComplexV01Installer.InstallIntoBootstrap`
- rocket + pad dressing: `-executeMethod OutOfReach.Visuals.LaunchComplexV01.Editor.LaunchComplexV01Installer.InstallWithPadDressingIntoBootstrap`

The menu also has **Install launch vehicle only into open scene**.

What the menu installer does (additive, reversible, idempotent, never saves):

- instantiates `Prefabs/LC1_LaunchVehicle.prefab` at identity under `Transport vehicle - root controls launch`
  (so it lifts off with the existing root) and disables **only the MeshRenderer** of `BoosterStack`;
- instantiates `Prefabs/LC1_PadDressing.prefab` at identity under `Launch site - stationary` and disables **only the
  MeshRenderers** of the four land-kit modules it replaces in place: `LS_LaunchMount`, `LS_Umbilicals`,
  `LS_WaterTower`, `LS_FloodTowers`. Their objects, mesh colliders, anchors and names stay, so
  `I06LandSiteAuthoring.Validate` still finds 11 renderers and 5 colliders;
- adds no collider, light, script or camera. Nothing is deleted, moved or renamed.

**Rebuild Materials and Prefabs** re-runs the texture/model import settings, the URP Lit materials and both prefabs.

Order with the other launch-site deliveries does not matter: LaunchTowerV03 and LaunchComplexV01 touch disjoint
objects. If the grass installer keeps its field clear of standing renderers, re-run it after this one (all new
objects stand on the concrete apron, x within +-58 m, z -50..63 m).

## Files

| Path | What |
|---|---|
| `Models/LC1_LaunchVehicle.fbx` | `LC1_Vehicle_LOD0/1/2` |
| `Models/LC1_PadDressing.fbx` | 7 modules x `LOD0/LOD1` + `LC1_Markings_LOD0` |
| `Prefabs/LC1_LaunchVehicle.prefab` | LODGroup (0.18 / 0.05 / 0.004), 3 anchors |
| `Prefabs/LC1_PadDressing.prefab` | one LODGroup per module (0.09 / 0.0015), 4 anchors |
| `Materials/LC1_*.mat` | 17 URP Lit materials (13 textured, 3 emissive lamps, dark glass) |
| `Textures/LC1_*.png` | 25 procedural tileable 1024 px textures: BaseColor + Normal. Metallic / smoothness are material scalars |
| `Editor/` | builder, installer, evidence capture (own Editor-only asmdef, references URP only) |
| `Models/LaunchComplexV01_report.json` | Blender build report: triangles per object and material, bounds, keep-clear vertex counts |
| `Source~/` | `build_launch_complex_v01.py`, `lc1_tex.py` |
| `Previews~/` | Blender Eevee previews (flat preview colours, grey stand-ins for tower and capsule) |
| `Evidence~/` | Unity URP captures from the real Bootstrap scene, before and after, and `unity_install_check.txt` |

## Frame and interfaces (unchanged from the existing scene)

Unity metres in the prologue root frame (same as `TransportLaunch.fbx`, `LS_LandLaunchSite.fbx`, LaunchTowerV03).

- Vehicle: axis x 0, z 0. Nozzle exit plane y -31.62 (`EngineEmitter` stays at y -31.4, inside the centre bell).
  Base ring y -30.50, core radius 2.30, spacecraft adapter radius 2.54, top ring y -0.55 radius 2.40: the same
  interface the old `BoosterStack` gave the capsule. Fins reach radius 3.80 on the diagonals only, below y -26.85.
- Pad top y -32.6. The new launch mount has the footprint and heights of `LS_LaunchMount` (plinths x +-(3.1..4.7),
  z +-4.6, top y -31.0), so the existing mesh collider still matches what is drawn.
- Cable tray and pipe rack keep the old routes and stop at the rear face of LaunchTowerV03 (z -13.95).
- Floodlight lamp banks sit on the old `LS_FloodLamp_East/West` anchors (+-46, -4, 45.7).
- Nothing is above y -0.55 within 30 m of the crew deck, the access arm or the hatch; the Blender report counts
  **0 vertices** in the crew deck / arm / hatch box, the tower base box, the swing-arm zone, the capsule volume and
  the crawlerway gate lane.

## What is modelled

- **AURORA launch vehicle**: five regeneratively cooled bells (centre + four) with heat-tinted walls, stiffener
  bands and turbine exhaust ducts under a gold-blanket base closeout; graphite aft skirt with four swept fins and
  hold-down lugs; white fuel and LOX tanks with a graphite stringer intertank; LOX feedline in gold insulation on
  brackets; two systems tunnels; interstage with vent ports and an amber separation ring; second stage with a foil
  equipment bay, RCS pods, hazard-framed umbilical plates with cyan status lights, access doors; flared spacecraft
  adapter with panel seams and separation fittings. `AURORA` down both flanks of the LOX tank, `7` on the fuel tank.
- **Launch mount**: concrete plinths with steel deck, hazard edges, four hold-down arms with clamp housings and
  actuators, two tail service masts with umbilical carriers and hoses (0.3 m off the skirt), four rainbird heads
  on a fire-water header, guard rails, stairs, scorched refractory lining of the flame hole.
- **Flame trench**: buttress ribs, steel curbs, scorched lining near the deflector, deflector cladding, spray
  headers with nozzles, a service bridge beyond the last barrier.
- **Deluge**: 59 m water tower in the old footprint (braced legs, riser, banded tank, balcony with rail, ladder,
  obstruction lights), pump skid, 0.6 m ground main on sleepers with flanges and an isolation valve to the header.
- **Propellant farm**: LOX sphere on eight braced legs with ring girder, spiral stair, top platform and vent
  stack; two fuel bullets on saddles inside a containment wall; three-line pipe rack; pad terminal; cable tray.
- **Lights and lightning**: two lattice floodlight towers with platforms, ladders and twelve-lamp banks aimed at
  the vehicle; four 73 m lattice lightning masts with insulated air terminals joined by a sagging catenary ring
  (no wire crosses above the vehicle).
- **Service vehicles** (original designs): cryo tanker, crew van, knuckle-boom service truck, red fire tender,
  each in a painted bay.
- **Markings and barriers**: dashed amber keep-clear ring with cyan marker lights, service lanes, hazard hatch,
  concrete impact barriers, amber/white gate chicane, striped bollards, warm gate lights.

Lamps are emissive meshes only: no `Light` components (the lighting lane decides).

## Limits (honest)

- Not saved into `Bootstrap.unity`, not playtested, launch sequence not run with the new vehicle. The installer was
  run on the real Bootstrap scene in memory only (see `Evidence~/unity_install_check.txt`).
- The capsule is another session's delivery. This vehicle keeps the old interface ring (y -0.55, r 2.40); if the new
  capsule base differs, the adapter top ring is the one number to change (`vehicle()` profile, last two points).
- Engine plume FX stay on the single `EngineEmitter`; the four outer bells have no separate emitters.
- UVs are cylindrical on revolved surfaces and box-projected elsewhere; no baked AO, no lightmap UVs.
- Stairs, ladders and vehicles have no colliders (the player never reaches the pad surface in the prologue).
- The old `DeckLamps` mesh and the land-kit barriers, markings, trench and foundation are left as they are.
