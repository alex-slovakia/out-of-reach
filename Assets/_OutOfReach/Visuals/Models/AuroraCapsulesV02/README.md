# AURORA capsules V02: rescue craft and crew transport capsule

Two original, fully scripted hero vehicles. Both are **mesh-only drop-in replacements** for the delivered
`TransportLaunch.fbx` parts `CapsuleShell` and `Hatch`. No GameObject, anchor, collider, hinge frame, seat,
cargo bay or script reference is moved, renamed or removed. `CapsuleInterior` is left as delivered.

| Vehicle | Scene object | FBX | Look |
|---|---|---|---|
| Rescue craft (RC) | `AURORA-7 delivered Blender models/RecoveryArrival - owned visual group/RecoveryArrival_Courier` (and the same craft as `MarsDeparture_Courier`) | `Models/AuroraRescueCraft.fbx` | flown: soot above the shoulder ring, plume blooms at every jet, heating streaks down the cone, replaced tiles, orange RESCUE belt, optional service module |
| Crew transport (CT) | `I06 isolated marine prologue/Transport vehicle - root controls launch` | `Models/AuroraCrewTransport.fbx` | pad-fresh white tile, amber cheat line |

## What is modelled

Tiled back shell with gore seams and fasteners, ablative base and shoulder ring, hatch leaf with porthole,
seal, three hinges, outer emergency release and an inner latch lever on the delivered `HatchLatch` anchor,
hatch casing and step plate, two rendezvous windows in a trimmed surround, two side portholes, four RCS
quads with keep-out frames, umbilical plate with two fluid ports and two connectors, grab bars and EVA
rails, blade/patch/dome antennas, star-tracker apertures, navigation and docking lamps (emissive), and a
docking system: tunnel, actuator housings, hard-dock ring with twelve hooks, two seals and guide pins, a
retracted soft-capture ring with **three guide petals** and capture latches, and the tunnel hatch.
Lettering: AURORA, RESCUE, RV-02 / CT-07, CREW HATCH, UMBILICAL, PULL / EMERGENCY RELEASE. No agency logos.

The rescue craft's service module (radiators, foil bays, four more RCS quads, tanks, main engine, four
verniers, high-gain dish) is a separate mesh (`ServiceModule_LOD*`), so the capsule itself keeps the
delivered envelope. Install it or leave it out from the menu.

## Kept from the delivered capsule

- Blender frame: metres, Z up, hatch on +Y, cabin floor at z 0 (Unity: -90 degrees about X on the node).
- Hatch hinge origin (0.72, 2.4, 0); door opening x +-0.70, z 0..2.15; leaf x +-0.68, z 0.02..2.13.
- Docking datum z 4.95, top of bounds z 5.10 (the petal tips), which `LeadDepartureAuthoring.ValidateOrbit` checks.
- Outer mould line: cylinder r 2.50 to z 2.60, cone to r 0.93 at z 4.42. Nothing near the hatch reaches
  past y 2.62, the boot-seal face of LaunchTowerV03's white room.

## Triangles (as imported by Unity 6000.3.28f1)

| Mesh | LOD0 | LOD1 | LOD2 |
|---|---:|---:|---:|
| RC `CapsuleShell` | 35,142 | 18,095 | 6,991 |
| RC `ServiceModule` (optional) | 11,960 | 6,730 | 2,556 |
| RC `Hatch` | 2,565 | 788 | uses LOD1 |
| **Rescue craft drawn** | **49,667** | **25,613** | **10,335** |
| CT `CapsuleShell` | 31,996 | 15,782 | 6,010 |
| CT `Hatch` | 2,565 | 788 | uses LOD1 |
| **Crew transport drawn** | **34,561** | **16,570** | **6,798** |

LOD switches (screen-relative height): shell LOD0 from 12 %, LOD1 from 4 %, LOD2 down to 0.2 %; hatch LOD0 from 6 %.
`Models/AuroraCapsulesV02_report.json` holds the generator's own counts and bounds (hatch bounds there are
relative to the hinge origin). Rescue craft bounds with the service module: z -2.86..5.10 m, dish to x -2.96 m;
without it the capsule keeps the delivered -0.55..5.10 m envelope.

## Materials (URP Lit, metallic workflow, one shared keyword set)

`RC_Hull`, `RC_HeatShield`, `CT_Hull`, `CT_HeatShield`, `CAP_Metal`, `CAP_Graphite`, `CAP_Kit`.
Each has `_BaseColor`, `_Normal` and `_Mask` (R metallic, G occlusion, A smoothness); `CAP_Kit` is a flat
swatch palette with an emission map for the lamps. `RC_Hull` is a unique 4096x2048 unwrap (so the soot is
placed around the real jets); `CT_Hull` is one 45 degree gore at 2048x4096 repeated eight times, about
1 mm per texel for the close view at the hatch.

## Install

1. Open `Bootstrap.unity`.
2. `OUT OF REACH > Aurora Capsules V02 > Install into open scene (transport + rescue craft)`.
   Separate items install only one vehicle, or the rescue craft without its service module.
3. Check it, then save the scene yourself. The installer marks the scene dirty and never saves.

`Uninstall (restore TransportLaunch meshes)` puts the delivered meshes and `TL_Exterior`/`TL_Labels` back
and removes every object the installer added (they are all named `* (AC2)`).

The installer adds LOD children under `CapsuleShell` and `Hatch` and one `LODGroup` on each. It prints the
same docking-datum measure `LeadDepartureAuthoring` uses.

## Verified, and not verified

Run on this branch's own Editor (Unity 6000.3.28f1, batch, 15 Nov 2026 21:29 CEST), logs in `Previews~/`:

- `unity_build_report.txt`: textures, materials, both FBX and both prefabs build; the new meshes share the
  delivered node transform (rotation 270/0/0, scale 1) and mesh frame; nose datum 5.1000 on both.
- `unity_install_check_before_reinstall_fix.txt`: install into Bootstrap (in memory, not saved) succeeded on
  the transport and on both recovery couriers; docking datum 0.150 m past `DockingPort`: PASS.
  The same run then failed on a **second** install (the query touched objects the re-install had destroyed).
  That is fixed in `AuroraCapsulesV02Installer.Targets`; the fix compiles (Unity Roslyn, exit 0) but the
  re-install, the uninstall check and the Unity "after" captures were **not** re-run, because the machine was
  already at its Editor limit. No Play mode, build or in-game check was run.
- Blender renders of the final meshes: `Previews~/AuroraRescueCraft_*.png`, `Previews~/AuroraCrewTransport_*.png`.

## Rebuild

```
blender --background --python Source~/build_capsules.py -- --out <this folder>
```

then `OUT OF REACH > Aurora Capsules V02 > Rebuild Materials and Prefabs`.
Verification without saving: `-executeMethod OutOfReach.Visuals.AuroraCapsulesV02.Editor.AuroraCapsulesV02Evidence.RunBatch`
(writes `Previews~/unity_*.png` and `Previews~/unity_check.txt`).
