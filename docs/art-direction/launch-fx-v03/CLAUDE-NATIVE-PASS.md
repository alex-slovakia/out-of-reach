# LAUNCH-FX-V03 — Claude native pass (15 Nov 2026)

Writer: Claude (Apoliak7777, tower session), the one LaunchFX writer (#7 re-claim 6895332040 after the lead's
resolution 6895301156). Branch `feature/launch-fx-v03` from `codex/i01-integration@b5dc6f8` (the owned files and the
patched root files are identical at `19c86c4`). Root's V03 (`29cd95c`) is reused, not restarted.
Committed paths: `Scripts/Presentation/LaunchFXV03/**`, `Visuals/LaunchFX_V03/**`, `docs/art-direction/launch-fx-v03/**`.
Root's `I06MarineBindings.cs`, `I01BootstrapBuilder.cs` and the two Composition asmdefs are **not committed**: the exact
changes are in `integration/*.patch` for the integrator (CLAUDE-INTEGRATION) and were applied only in my clone.

## What changed in the owned files

| File | Change | Why |
|---|---|---|
| `Visuals/LaunchFX_V03/LaunchPlumeV03.shader` | fog attenuation only under `FOG_LINEAR/EXP/EXP2`; hotter, whiter core colours | **Native bug:** URP's `ComputeFogIntensity` returns 0 when fog is off and the departure disables fog for the upper-atmosphere shot, so the plume vanished there ([before](native-captures/before-root29cd95c-t19.5-no-plume.png), [after](native-captures/full-t19.5.png)) |
| `Visuals/LaunchFX_V03/LaunchCloudV03.shader` | sun-lit billows: sphere-impostor normal from the camera-facing quad, the scene's actual main light (wrapped, half its tint so the steam stays cream) plus the ambient probe; stronger orange contact glow | volume and integration with root's lighting; orange illumination near the flame |
| `Scripts/Presentation/LaunchFXV03/LaunchFxV03Presenter.cs` | new steam schedule (below); tighter white core (0.34 r, 0.62 length); shadowless point light 60 at 3 m above the deflector, range 30, subtle deterministic flicker, `lightIntensity` serialized; `maxParticleSize` 0.7 | brief: tight white/yellow core, large cream/tan ground-hugging steam rolling sideways, orange light near the flame |
| `Visuals/LaunchFX_V03/LaunchFX_V03.prefab`, `LaunchPlumeV03.mat`, `LaunchCloudV03.mat` | built by root's unchanged `LaunchFxV03Authoring.Build()` | the asset the builder instantiates |

Steam (still 160 particles, 40 in reduced effects; every position from the absolute playhead, no emission accumulator):
- an ignition burst (28 particles in 0.5 s), then steady production that **fades with the actual timeline height**
  (`TransportDepartureTimeline.Evaluate(t).HeightMetres`, 0 by about 26 m) because the jet no longer reaches the deflector;
- three groups: a stream down the open trench towards `LS_TrenchExit_Ground` rising out of the slot, two ground-hugging
  flanks rolling sideways off the deflector, and irregular billows on a wide arc around the engine bay (never a closed ring);
- decelerating roll-out `1 - exp(-age / 1.7 s)` like a turbulent ground jet; billows grow to 24 m wide and 13–14 m tall,
  calibrated to the 4.7 m booster and 6 m trench (scales if either serialized value changes); cream with tan flanks;
- no shockwave ring, burst, explosion, distortion or camera effect.

The vehicle ascent is untouched: the timeline already climbs 0 → 23 m between 10.5 s and 13.25 s from a zero start rate
(about 0.9 m at 11.0 s, 4.1 m at 11.6 s), then accelerates. No timeline, camera, caption or audio file was changed.

## Native verification actually run

Own full clone `NASA\out-of-reach-launchfx` (own worktree) (never `out-of-reach-verify`), Unity 6000.3.28f1 batch mode
**with GPU** (Intel UHD, Direct3D12), the actual saved `Bootstrap.unity`, real Practice UI, real walking and E on the seat,
the real 34 s departure. Tool: `native-qa/LaunchFxV03NativeQa.cs` + asmdef (copied into `Assets/_LaunchFxQa/Editor/` only
for the runs). The only non-player step is binding V03 through the patched `I06MarineBindings.ConfigureLaunchFxV03`,
which is what the builder patch does at authoring. Final runs on the committed files: `full-20261115T135749Z` and
`reduced-skip-20261115T135846Z` (both PASS; evidence in `native-captures/`).

- Anchors bound: `EngineEmitter` (forward = down), `Actual deflector contact plane`, `Land pad top plane`
  (prologue-local y -32.6 confirmed), `LS_TrenchExit_Ground`.
- Pause at 11.0 s: playhead, plume length, particle count and every particle position held for 1.2 s; resume continues
  without catch-up (11.00 → 11.09 s).
- After the orbit cut (22 s) and after the departure end: 0 particles, plume and light disabled.
- Reduced effects: 21 particles at 11.7 s (≤ 40), no light. Skip at 12.2 s: 0 particles, no plume, no light.
- No runtime errors from game code (only the known `UnityEditor.Search` indexing exception, filed separately).
- The builder patch compiled with the whole project in the same clone; Bootstrap was **not** rebuilt here.
- Captures (1280 x 720, game camera with the UI): `native-captures/full-*.png` at 8.8 (seat), 9.9 (first pad view), 10.6,
  11.6, 12.6, 14.0, 16.0, 19.5, 21.7 s and the paused frame; `reduced-t11.6.png`; root's baseline for comparison
  `before-root29cd95c-*.png`.

## Integration (CLAUDE-INTEGRATION)

1. `git apply docs/art-direction/launch-fx-v03/integration/I06MarineBindings-LaunchFxV03.patch` (runtime + asmdef reference;
   natively tested) and `.../I01BootstrapBuilder-LaunchFxV03.patch` (builder + editor asmdef reference; compile-tested).
   Both apply on `b5dc6f8`, `cfd9f38` and `19c86c4` (those four files are unchanged there).
2. Rebuild Bootstrap with the builder, or do the same as a scene edit: instantiate `LaunchFX_V03.prefab` under the
   presentation root and call `ConfigureLaunchFxV03(presenter, land.DeflectorContactPlane, land.PadPlane, land.TrenchExit)`
   on `I06MarineBindings`; the nozzle is its existing `nozzleFrame` (`EngineEmitter`). `ConfigureLaunchFxV03` stores
   serialized references when called at authoring and binds immediately when called while bound (not during a departure).
3. With V03 bound, the old `MarineDepartureFxPresenter` stays bound but is not presented, so its flame, steam, pressure ring
   **and its procedural deluge nozzles/water jets** are not shown. Keeping the water jets is a separate choice.
4. Rerun `native-qa/LaunchFxV03NativeQa.cs` (`RunFull`, `RunReducedSkip`) on the combined branch.

## Limits

- Captures come from the Intel UHD batch run at `b5dc6f8`; final look depends on root's newer terrain, lighting and
  post-processing. No bloom-specific tuning was done.
- The departure only has the seat, pad, climb and upper-air shots; the steam is seen from the pad camera at about 46 m.
- Bootstrap was not rebuilt here; the builder patch is compile-checked, not executed.
