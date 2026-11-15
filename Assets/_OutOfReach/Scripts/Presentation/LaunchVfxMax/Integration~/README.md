# LAUNCH-VFX-MAX-V04: Particle Pack launch sequence

Branch `feature/claude-max-launch-vfx`, based on `feature/claude-max-final@5479e87`.

This is presentation only. It reads `TransportDeparturePresenter.PresentedSeconds`, `IsPlaying`, `IsSuspended` and `ReducedMotion`, plus the authored anchors on `I06MarineBindings`. It writes nothing back: no clock, flight, camera, audio or gameplay authority.

## What it adds

All layers are sampled from the departure playhead (ignition at 9.0 s, exterior pad shot from 9.75 s, liftoff at 10.5 s).

| Departure s | Layer | Pack source |
|---|---|---|
| 8.55–10.75, then a trickle | White deluge steam bursting up off the pad | `Steam` |
| 9.0 onward | Ignition flash (decays over about a second) and a flickering light that follows the plume | none (lights) |
| 9.0 until about 26 m of climb | Warm light under the clouds at the pad | none (light) |
| 9.0–21.8 | Jet fire and embers. They grow with thrust, widen with altitude and stop at the deflector plane while the vehicle is on the pad. | `FlameStream` › FlameStream, FireEmbers |
| 9.0–21.8, thinning to a trickle above about 26 m of climb | Exhaust billows rolling out both ways along the ground and down the trench: low fans, 8–15 s life, decelerating | `SmokeEffect` |
| 9.4–10.5 | Dust front racing out about 20 m across the pad, then stalling | `SmokeEffect` |
| 9.0 until about 26 m of climb | Rock debris off the two deflector flanks | `DustStorm` › RockDebris |
| 10.2–21.8 | Smoke column shed by the climbing nozzle; it stays in the world and drifts | `SmokeEffect` |

Off by default (toggles on the component):

- `sparks`: the pack's `SparksEffect` prefab loads without a material in this project, so the installer skips it and the toggle has nothing to show.
- `heatShimmer`: rendered once in the comparison pass with no visible difference; needs the URP opaque texture.
- `trenchJet`: rendered only in the first two passes, never judged on its own.
- `publishThrusterGust`: writes the `_ThrusterGust` global (pad xyz, strength). No shader on this base reads it; grass V04 writes its own `_OOR_GrassGust`.
- `groundShockwave` is on. An earlier user decision recorded in LaunchFxV03 cancelled radial bursts, and this request asked for the dust front again, so it is one switch.

## What it leaves alone

- **LaunchFxV03** stays: plume mesh, trench steam and pad light. The flat wide puff heading for the trench outlet in the frames is V03's own steam, not a layer from this folder.
- **Camera shake** is already in `TransportDeparturePresenter` and is zero with reduced motion.
- **Alex's uncommitted `ParticleLaunchPreview`** is deactivated by the installer (matched by type name), not deleted.

**Reduced effects** (`ReducedMotion`): emission and capacity at 25%, no flash, no flicker, no dust front, no debris, lights at 45%. Rendered: 115–151 live particles.

**Safety:** every pack reference is null-checked. On any exception the presenter logs one warning and switches itself off; the launch continues. Fixed seeds and fixed 1/30 s steps; pausing holds every particle; a rewind or skip clears and replays up to 3 s.

**Cost:** 590–725 live particles at full effects in the rendered replay (table in `Evidence~/V04-evidence.txt`). Frame time was not measured.

## Install (in Alex's project, which has the Particle Pack)

1. Merge the branch. The pack must exist at `Assets/UnityTechnologies/ParticlePack/` (untracked Asset Store content, never committed).
2. Open `Assets/_OutOfReach/Scenes/Bootstrap.unity`.
3. Run **Out of Reach › Launch › Install Launch VFX MAX (Particle Pack)**. It adds one root object, `Launch VFX MAX — Particle Pack (sampled, no authority)`, and saves the scene. Expect `LAUNCH_VFX_MAX_INSTALLED LAUNCH-VFX-MAX-V04 …` in the console.
4. To undo: **Out of Reach › Launch › Remove Launch VFX MAX**.

Without step 3 nothing changes in the game.

## Tuning (on the component, also live in Play mode)

`fireScale`, `smokeScale`, `billowScale`, `dustScale` (0–2); `flashIntensity`, `plumeLightIntensity`, `cloudGlowIntensity`; the tints; the toggles above.

## Evidence

`Evidence~/` holds 24 frames from **Out of Reach › Launch › Render Launch VFX MAX evidence (scene not saved)**, run in batch mode on this branch with the real LFS assets:

- `V04-game-t*.jpg`: the game's own pad, climb and upper-air shots at departure seconds 9.3, 10, 11, 12, 13, 14, 15.5, 17 and 19.
- `V04-wide-t*.jpg`: a fixed wide camera at the same times.
- `V04-game-reduced-t*.jpg`: reduced effects at 11 and 14 s.
- `V04-game-boost-t*.jpg`: a comparison pass (sparks and heat on, lights about 2.7× stronger). It is not the default and looks the same as the default frames.

How the frames are made: an edit-mode replay. The harness opens Bootstrap, installs without saving, moves the vehicle with the same formula as `TransportDeparturePresenter`, drives LaunchFxV03 and this presenter step by step, and renders with a copy of the player camera. It is not a Play-mode capture: camera shake, the departure's sky change above 17.5 s, UI and audio are absent.

## Verification actually run

- Unity 6000.3.28f1 batch mode on this branch compiled the whole project with these sources: 0 compiler errors (`OutOfReach.LaunchVfxMax.dll` and its Editor assembly built by Bee).
- The installer ran on Bootstrap inside that batch run with save off.
- Three render passes were inspected. The first showed scattered puffs and discs over the pad camera from the pack's DustStorm sprites; those layers were replaced. The third pass (this revision, real assets) shows one cohesive steam and exhaust cloud at the pad and a clean upper-air shot.

**Not run:** Play mode, the installer saving the real Bootstrap in Alex's project, a player build, a frame-time measurement.
