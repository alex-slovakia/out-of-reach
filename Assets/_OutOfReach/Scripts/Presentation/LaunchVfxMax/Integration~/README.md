# LAUNCH-VFX-MAX-V01: Particle Pack launch sequence

Branch `feature/claude-max-launch-vfx`, cut from `origin/codex/nasa-2330-staging`.

This is presentation only. It reads `TransportDeparturePresenter.PresentedSeconds`, `IsPlaying`, `IsSuspended` and `ReducedMotion`, plus the authored anchors on `I06MarineBindings`. It writes nothing back: no clock, flight, camera, audio or gameplay authority.

## What it adds and what it leaves alone

| Beat (departure s) | Layer | Pack source |
|---|---|---|
| 9.00 ignition | Amber flash light (about 0.5 s decay) plus a fire burst | none (light) / `FlameStream` |
| 9.0–21.8 | Turbulent fire and embers in the jet. Size and speed grow with thrust and climb. | `FlameStream` › FlameStream, FireEmbers |
| 9.0–about 14 (until about 26 m of climb) | Dust clouds, sand and rock debris thrown sideways along the **two deflector flanks** (no radial ring; the user cancelled shockwaves) | `DustStorm` › DustStorm, SandParticles, RockDebris |
| 9.0–about 14 | Sparks thrown up and out from the deflector impingement point | `SparksEffect` |
| 9.1–about 14 | Heat shimmer rising off the pad. Only if the active URP asset has the opaque texture on (PC_RPAsset yes, Mobile_RPAsset no). | `HeatDistortion` |
| 9.35–about 14 | Pressurised steam venting from the real `launchTrenchExit` along the flow | `PressurisedSteam` |
| 10.2–21.8 | **Lingering smoke column.** World-space smoke shed by the climbing nozzle that stays where it was born, 9–13 s life. | `RocketTrail` › Smoke |
| 9.0–21.8 | Point light following the plume, with Perlin flicker | none |

These are **kept or not duplicated**:

- **LaunchFxV03:** the plume mesh, the trench steam billows and the pad bounce light stay as they are. Their envelopes (fade at 21.1 s, ground contact fading by about 26 m) are matched exactly.
- **Camera shake:** already in `TransportDeparturePresenter.AddCameraVibration`, and zero with reduced motion. No new hook is needed.
- **Grass flattening:** `LaunchGrassGustPresenter` (grass V04, `feature/claude-max-grass`) already writes `_OOR_GrassGust` from the same playhead. That lane stays the only writer.
- **Alex's uncommitted `ParticleLaunchPreview`** (`FlameStream` ×3, `SmokeEffect` ×3, `GroundFog`) is **superseded**. It doubled V03's trench steam. The installer deactivates it by type name and does not delete it.

**Reduced effects** (`departure.ReducedMotion`): emission and capacity run at 25%. There is no flash spike, no flicker, no sparks, no debris and no heat shimmer, and the plume light runs at half intensity.

**Determinism:** fixed seeds and fixed 1/30 s sub-steps driven only by the playhead. Pausing holds every particle. Rewinding or a skip clears the particles and replays up to 3 s.

**Budget:** about 2,000 particles at the full-effects peak (the sum of the capacities). `LiveParticleCount` exposes the live figure.

## Install (coordinator, in Alex's project, which has the Particle Pack)

1. Merge or cherry-pick this branch. The pack must exist at `Assets/UnityTechnologies/ParticlePack/` (untracked Asset Store content; never committed).
2. Open `Assets/_OutOfReach/Scenes/Bootstrap.unity` and wait for the compile.
3. Run the menu item **Out of Reach › Launch › Install Launch VFX MAX (Particle Pack)**.
   - Expect the console line `LAUNCH_VFX_MAX_INSTALLED LAUNCH-VFX-MAX-V01 … Retired ParticleLaunchPreview owners: N`.
   - The installer saves the scene. It adds one root object, `Launch VFX MAX — Particle Pack (sampled, no authority)`, and deactivates the preview object if one exists.
4. Undo the change with **Out of Reach › Launch › Remove Launch VFX MAX**, or delete that one object.

Safety:

- Every pack reference is null-checked. If the pack is missing, the presenter quietly renders nothing. The required prefabs are FlameStream, RocketTrail and DustStorm; Sparks, Heat and Steam are optional.
- The assembly `OutOfReach.LaunchVfxMax` depends only on `OutOfReach.Cinematics`.

## Tuning live (Play mode, on the component)

- Scale fire, smoke and dust with `fireScale`, `smokeScale` and `dustScale` (0–2).
- Set the light with `flashIntensity` and `plumeLightIntensity`.
- Toggle layers with `sparks`, `heatShimmer` and `trenchJet`.
- Set the palette with `smokeTint`, `dustTint`, `steamTint` and `lightColour` (graphite and amber by default).

## Render to run (not run here: no Editor slot was available)

1. Enter Play mode, then start the departure. Use the same path as LaunchFxV03 acceptance, so that `TransportDeparturePresenter.IsPlaying` is true.
2. Capture the Game view at departure seconds:
   - **9.05:** flash and seat-shot exterior light.
   - **9.9:** pad shot at ignition. Check the flank dust, sparks and shimmer.
   - **11.0:** liftoff. Check the fire, the trench jet and the start of the column.
   - **13.0:** end of the pad shot.
   - **15.5:** climb shot. Check that the column lingers below the vehicle.
   - **19.0:** upper air.
3. Repeat 9.9 and 11.0 with reduced motion on.
4. Things to check:
   - No pink materials.
   - No radial ring at the pad.
   - Dust reads tan or graphite, not white.
   - The fire sits inside the V03 envelope rather than replacing it.
   - The column stays put in the world while the camera tracks the vehicle.

## Verification actually run

- Roslyn compile, using Unity 6000.3.28f1 `csc.dll` with the reference set from Alex's `Library/Bee` `OutOfReach.LaunchFXV03.rsp` and `OutOfReach.Composition.Editor.rsp`:
  - `OutOfReach.LaunchVfxMax.dll` built with 0 errors.
  - `OutOfReach.LaunchVfxMax.Editor.dll` built with 0 errors.
- **Not run:** a Unity import, Play mode, renders, the installer on Bootstrap, or a player build.
