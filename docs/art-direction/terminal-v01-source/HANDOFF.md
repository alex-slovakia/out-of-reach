# TERM-V1 — staged physical Earth terminal

Checkpoint: `feature/visuals-cinematics`, live baseline inspected at `c7cfb86`. All work is restricted to `.local/verification/PhysicalTerminalV01`. No original source, source glove, scene, ship lighting, Contracts, Player, Core, package, shared documentation or repository setting was changed. No Unity editor was launched. No commit or push. Root owns import/integration.

## Delivered

- `Source/MissionTerminal.blend`: editable original model, metre units, UVs, bevel/weighted-normal modifiers. `Source/build_terminal.py` rebuilds/exports it and makes genuine Blender studio renders.
- `Assets/_OutOfReach/Visuals/Props/MissionTerminal/Models/MissionTerminal.fbx`: 41 static mesh objects, 12,466 evaluated triangles, original material names, five reference empties. Rear mounting arm, sealed case, machined bezel, 34 mm diameter left grip, fasteners, service panel and cable. No ship geometry or NASA insignia.
- Original metallic/roughness material values in `materials.json`; importer builds matching URP materials. No downloaded model or new package.
- Exact unaltered NASA EPIC JPEG plus original source/licensing manifest/attribution in `Data/`. Observation is **2026-11-11 11:01:17 UTC**, not the different filename timestamp. SHA-256 `d5a5fc0ce1505848b9ddd8fd1e6b020d21d44d532d24f05ee083202c8d8c9463`.
- `PhysicalEarthTerminalPresenter`, internal `TerminalGlovePose`, isolated runtime/editor asmdefs, URP display shader and prefab builder under the two eventual owned paths. Stable metadata is present for both owned roots and every descendant. Existing parent `Props` metadata, if missing in the integration checkout, belongs to root.
- `Renders/01-front.png` and `Renders/02-rear.png`: actual Blender previews of exported geometry. Screen text there is preview-only; runtime text/shader are created by the Unity builder. These are **not Unity screenshots**.

## Public API / ownership

Namespace: `OutOfReach.Presentation.Terminal`.

```csharp
void ConfigureScene(Camera camera, Transform frame, Vector2 sizeMetres,
    Renderer screen, Transform gloveInstance, TextAsset gloveRig, Transform gripFrame);
void ConfigureSession(Func<bool> acquire, Action release, Func<bool> close,
    Func<DateTime> missionUtc);
void ConfigureDisplay(Texture image, TextMesh mission, TextMesh observed,
    TextMesh age, TextMesh status);
bool TryEnter();
void Exit(TerminalExitReason reason = TerminalExitReason.User);
void SubmitPointer(Vector2 screenPixels, bool pressed, bool isHeld, bool released);
bool TryMapPointer(Vector2 screenPixels, out Vector2 displayUv);

bool IsEntered { get; }
Transform DisplayFrame { get; }
Vector2 ActiveSizeMetres { get; }
Renderer DisplayRenderer { get; }
Transform LeftGripFrame { get; }
event Action Entered;
event Action<TerminalExitReason> Exited;
event Action<Vector2> ContactPressed;
```

`TerminalExitReason`: User, Escape, Pause, Restart, Disabled, Reconfigured, Invalidated, Fault.

Root owns the **only** E interaction target, player input lease, camera framing, cursor lock and event ordering. `acquire` must transactionally return true only after it acquired that lease; `release` restores root's camera/input/cursor snapshot. The presenter captures that exact release callback for the acquired session and releases it on exit, disable, reconfiguration or a caught failure. Failed acquisition does not call release. Configure calls use a generation guard so reentrant configuration from exit observers is not overwritten. Exit clears active state and restores visuals before callbacks; TryEnter is blocked during transitions. Observer exceptions are isolated.

Call `TryEnter` from root's E handler. Route absolute screen pixel coordinates into `SubmitPointer` only while entered. The presenter itself polls **no input**, uses no Player/Interaction/Contracts implementation, adds no camera/listener, writes no gameplay state and changes no cursor/camera transform. Root must suppress ordinary left/right-hand gestures inside this mode. Current terminal touches with the right index and grips with the left; choosing the touching hand by mouse button is intentionally a later extension.

Route Escape, pause and restart to `Exit` with the relevant reason before resetting the runtime. Supply `close` as a fallback predicate for pause/restart/invalid view/mission transition; if it returns true the presenter exits with Invalidated. Supply `missionUtc` from `runtime.Calendar.NowUtc`; it must be `DateTimeKind.Utc`. There is no PC clock or parallel mission timer. Unscaled time only animates contact/ripple while the session remains active.

The generated prefab already retains the display refs/data labels. Bind the operator without searching globally:

```csharp
presenter.ConfigureScene(playerCamera, presenter.DisplayFrame,
    presenter.ActiveSizeMetres, presenter.DisplayRenderer,
    dedicatedGloveInstance.transform, astronautHandsRigJson, presenter.LeftGripFrame);
presenter.ConfigureSession(TryAcquireTerminalLease, ReleaseTerminalLease,
    MustCloseTerminal, () => runtime.Calendar.NowUtc);
```

Use a **dedicated instance of the existing `AstronautHands.prefab`**, not its raw FBX and not the instance owned by EVA/cargo hands. Root passes its transform and existing rig JSON. No source mesh is copied or modified. Exactly 34 named joints and Press/Grip poses are validated; local transform/active-state snapshots are restored. Camera and glove/display hierarchies must use unit scale. Right Press tip calibration is `(-.0089,-.0690,.1864)` in R_Arm space; contact lies 1 mm in front of glass, hover 12 mm. The left grip frame's +X runs up the side rail. Final pose aesthetics still require an actual Unity view.

Reference rig SHA-256: `025b9e5c798b345979366d36f4904d51ceb138443dd20916a0b46e747a62dd3e`.
Reference prefab SHA-256: `33b5bb526ecc19769aa4a0ec0690066b671a5ad291dfdebd1711b398ec0c5a0c`.

## Anchors / geometry

Root origin is the display centre. Local X is screen-right, Y is up, **+Z goes into the case**, front outward is -Z. Active area is 0.64 × 0.40 m. Parent transforms must have unit scale.

| Direct prefab child | Local position (metres) | Use |
|---|---|---|
| DisplayFrame | (0, 0, 0) | Screen ray/UV plane, identity rotation |
| LeftGripFrame | (-0.426, 0, -0.040) | 90° about local Z, +X along grip tube |
| InteractionAnchor | (0, -0.100, -0.080) | Root-owned interaction binding |
| FocusCameraAnchor | (0.012, 0.025, -0.660) | Suggested existing-camera pose, looks +Z |
| WallMountAnchor | (0, -0.038, 0.318) | Mount plane; no wall is included |

Hardware FBX reference empties get a `Source_` prefix inside the prefab, preventing ambiguity with direct integration anchors. Builder adds one casing BoxCollider (0.766 × 0.510 × 0.080 m, centre z=0.040); it does not add a separate interaction script or grip/cable collision. Root must place the prop outside the walk corridor and route its collider to the existing interaction authority. No original ship anchors or colliders were altered.

## Display truth boundary

No licensed full-Earth surface texture existed in the inspected live Assets tree. Main visual is therefore explicitly labeled **SCHEMATIC DISC PROJECTION**: the archived disc is orthographically projected onto its observed front hemisphere; dragging rotates that projection. The reverse hemisphere is a neutral graticule and marked unobserved. It is not mapped as equirectangular, georeferenced or a current planet/weather simulation. A separate square inset shows the original JPEG with its original framing. One blue expanding ring marks contact; no repetitive flashing is used.

Mission UTC, observation UTC and computed image age are separate. Before the observation epoch, imagery is hidden and the screen reports unavailable at this mission time. Credit is `Image: NASA EPIC Team / NOAA DSCOVR`. `AURORA-7 position: not propagated` remains explicit. Archive source is NASA's own image-use policy, not an invented CC license; source link/epoch and the policy remain with the original data manifest. No NASA logo/endorsement is present.

## Root import / verification

Copy only the staged `Terminal` folder + its meta and `MissionTerminal` folder + its meta into the designated integration copy. Preserve source files and supplied GUIDs. Root can retain `.blend` outside Assets or archive it to avoid Unity's automatic Blender importer. Add `OutOfReach.Terminal` to the root composition assembly's references where required; this task intentionally did not touch that shared file.

Exact execute method:

`OutOfReach.Presentation.Terminal.Editor.PhysicalTerminalBuilder.BuildAndValidate`

Use Unity **6000.3.28f1** only, against the isolated import copy. The method imports the model/image, generates materials and `Assets/_OutOfReach/Visuals/Props/MissionTerminal/MissionTerminal.prefab`, validates image hash, metre screen bounds, UV data, triangle facing, five direct anchors, missing scripts and zero cameras/listeners. It never opens or changes a scene. It is idempotent for existing generated asset GUIDs. Source model Read/Write remains enabled for its small geometry validation. No shader or rendering success is claimed before root runs Unity.

Actually run:

- Hidden Blender 3.6.1 source build/export and two renders: completed; genuine previews visually inspected.
- Hidden Blender FBX reimport verification: **182 checks passed, process exit 0**. Metre dimensions, all five exact reference anchors, UVs/materials, 41 meshes, no camera/light/armature. Evidence `model-check.log`, `model-verification.json`, `model-report.json`.
- `verify_compile.ps1`: **exit 0**, C# 9 + exact Unity 6000.3.28f1 managed references, warnings as errors, including Editor importer code and the existing label binder dependency. This is a reference compilation, not a Unity import/assembly/shader/playtest.
- `package.py`: original NASA SHA-256 and stable, unique metadata for the two asset roots; `delivery-manifest.json` contains per-file hashes.

Required before shipping: exact Unity import/build method; shader compile and camera-facing render; finger contact at centre/corners; left grip orientation; drag/release/off-screen behavior; failed acquisition; Escape/pause/restart/disable/reconfigure release exactly once; restoration to ordinary mouse gestures/camera/input; dated image hiding before observation UTC; final physical placement without player obstruction. No live integration, Unity render, PlayMode test or gameplay claim is made by this handoff.

## Future scope advice only — no work started

A compact ground loading/access model could be a roughly 4 × 3 m platform plus a short crew-access arm with at least the existing 1.2 m clear gameplay passage. Keep its deck, railings, ship-end seal, ground gate and a small loading bay as separate meshes; proposed neutral anchors are GroundEntry, CargoSetDown, ShipMate and CrewEntry. Its ShipMate should fit the delivered ship airlock mating plane without editing ship meshes. Root should confirm the ship's launch attitude and mating transform before authoring the dock; a render concept alone cannot determine that interface. No launch tower, terrain or full spaceport is required for the first independent model checkpoint.

Existing reusable source assets are `Visuals/Props/PreflightCargo/Meshes/PreflightCargo.fbx`, `Models/Aurora7/Meshes/A7_Exterior.fbx`, `A7_ExteriorParts.fbx`, `A7_Airlock.fbx`, and `A7_AirlockParts.fbx`, plus the already verified PolyHaven PowerBox. Reuse their instances/materials and keep source ownership intact. Crates belong on that ground loading platform for the preflight task; this proposal does not fill the in-flight corridor with loose cargo. New interaction and launch transitions remain integrator/domain work, separate from that prop model.

Visible-body inventory: the current live Visuals tree contains **only the rigid AstronautHands first-person limbs**, not a full body character or Humanoid skeleton. Their 34-transform rig and pose JSON are compatible with this terminal's contact calibration, but do not constitute a reusable skinned full-body avatar. No workwear or complete EVA-body FBX was found in the inspected live asset paths. A later author should deliver one coherent body skeleton with workwear/EVA outfit variants, explicit wrist mating transforms, head/neck visibility masks and waist/leg proportions for look-down views; root can then bind a presentation body to the existing Player authority. Do not invent a second locomotion controller or assume the rigid hand hierarchy can simply drive a full-body skeleton. No new global rig, body or dock was created in TERM-V1.
