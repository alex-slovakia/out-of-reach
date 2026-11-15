# Launch FX V03 — integration handoff

Owner: delegated Codex launch FX writer. New files only in `Scripts/Presentation/LaunchFXV03/` and `Visuals/LaunchFX_V03/`. Existing transport, tower, scenes, audio and gameplay are untouched. This is a proposed replacement for the old departure FX rendering, not a second concurrent launch system.

## Delivered behavior

- A narrow white-hot core inside a wider amber exhaust volume, with soft shock bands and turbulent edge breakup. Geometry follows the actual `EngineEmitter`; its length is clipped against the stationary solid deflector plane.
- A finite stream of rolling cream/tan particles flowing toward the actual trench outlet and spreading into two broad, low flanks. The previous radial dust system is removed entirely.
- One local, shadowless ignition bounce light with a smooth onset and decay. Engine-local orange illumination inside the dense cloud fades as the actual nozzle rises. Reduced effects suppress that illumination/light, reduce steam to 40 particles, and lower plume intensity.
- No luminous pressure ring, explosion, audio, camera shake, full-screen distortion, model changes or invented vacuum pressure. The plume's internal shock bands are a stylized engine exhaust feature. Ground dust is displaced material, not a claimed measurement of acoustic wave speed.

The approved [launch concept](../launch-reference-v01/launch-approved.png) informs the color, tight core, trench outflow and distinct dust layer. This delivery does not replace the existing deluge hardware, add water jets, or implement heat refraction. Those remain separate integration/art choices.

Latest user steering cancels shockwave effects entirely and supplies [NASASpaceflight's Starship ignition reference](https://www.youtube.com/shorts/x67ku2zLgTs). Root viewed the ignition/liftoff frames and relayed tight white/yellow exhaust, broad ground-hugging cream/tan clouds, localized orange illumination and readable vehicle/tower silhouettes. The subsequent source revision follows those observations; this worker does not claim a separate video playback or a matched Unity render.

Cloud dimensions use the actual delivered 4.7m booster diameter and 6m trench width. Individual billows begin roughly 4.4–6m wide, grow to a maximum 19m width, and remain roughly 2–6.8m tall. A center stream and two flanks spread downstream rather than radially. The effect has no shockwave mesh, dust ring, pressure burst or shockwave trigger. `PresentedDustCount` remains zero for existing diagnostic callers.

## Exact hookup

1. Run `LaunchFxV03Authoring.Build()` or **OUT OF REACH → Visuals → Build Launch FX V03 Prefab**. This writes only the two owned material assets and `Assets/_OutOfReach/Visuals/LaunchFX_V03/LaunchFX_V03.prefab`. It never changes the active scene or Bootstrap. Dedicated shader references are serialized into the resulting prefab materials, so runtime does not rely on `Shader.Find`.
2. Instantiate the prefab on a stable presentation owner. Call `Configure(actualNozzle, solidDeflectorPlane, stationaryDustPlane, actualTrenchExit)`. For the current land kit use `EngineEmitter`, `land.DeflectorContactPlane`, **`land.PadPlane`** and `land.TrenchExit`. The PadPlane is at prologue-local −32.6m; passing the lower GroundPlane (−36.6m) would bury the pad cloud. The exit at z=72 supplies the downstream direction. Alternatively add `LaunchFxV03Presenter` and pass the two dedicated materials to the optional fifth/sixth arguments. Nozzle +Z must point down; all input frames use unit world scale and the stationary ground plane is horizontal.
3. After `TransportDeparturePresenter` has moved the vehicle, call:

```csharp
launchFx.Present(
    PresentedSeconds,
    actualCue,
    IsDeparturePlaying,
    runtime.IsPaused,
    departure.ReducedMotion);
```

`actualCue` is the existing `TransportDepartureCue` from `CueRaised`, retained by the integrator; no new domain contract is required. The exact existing root call site is `I06MarineBindings.LateUpdate`, where the old `departureFx.Present(PresentedSeconds, IsDeparturePlaying, runtime.IsPaused, departure.ReducedMotion)` is currently called after departure updates. Timing uses the actual `TransportDepartureTimeline.IgnitionSeconds`, `LiftoffSeconds` and `OrbitSeconds` constants.

4. Call `ResetEffects()` on departure end/skip/cancel/restart. `ClearBinding()` also destroys the owned transient views and clears scene anchors. Disabling the component hides/clears effects while preserving a valid binding for re-enable. Destroying it releases all owned objects. Invalid/missing anchors, non-finite samples, inactive lease or orbital cues immediately clear effects.
5. Suppress/replace the old `MarineDepartureFxPresenter` rendering before enabling V03, to avoid two flame/cloud/light sets. Root owns that wiring. Be mindful that old `ClearBinding()` also destroys its procedurally created deluge hardware; V03 deliberately does not recreate that hardware or modify the site's model.

## Timing, pause and bounds

The component has no `Update`, coroutine, clock advance, emission accumulator or gameplay command. Every particle position, size, opacity, plume shader sample and light envelope comes from the supplied absolute playhead. Particle simulation is paused with emission disabled; `SetParticles` updates preallocated buffers. Repeated samples are deterministic; backward samples clear and resample. A paused call holds the previous frame without moving any owned transform, particle or shader time. Cancellation still clears immediately while paused.

Two plume mesh renderers share one 561-vertex/1,024-triangle mesh; steam uses at most 160 billboard particles (40 reduced), with no separate dust particle system. Buffers and property blocks are allocated when bound, not per sample. No collider, shadow caster, texture download or per-particle light is created. Expected upper rendering budget is three transparent draw groups plus one short-lived light; actual batching, overdraw and frame cost need native profiling.

## Verification and remaining review

Runtime and editor C# reference compilation passed against the installed **Unity 6000.3.28f1** managed DLLs. This is not a Unity import/build/playtest claim. Shader compilation, actual particle rendering, scene anchors, pause/skip/restart in the integrated scene and visual quality remain root's native verification steps. The root temporarily froze Assets while running a separate native landscape check; this worker did not launch Unity.

Native acceptance frames: before ignition (8.8s), first pad view (9.9s), early clearance (11.6s), climb (14s), orbital cutoff (22s). Check plume/deck contact, steam direction, low dust placement, tower readability, subtitle readability, freeze/resume with no catch-up, skip/restart with zero lingering particles, and reduced-effects mode.

All code, mesh and procedural shader patterns are original for OUT OF REACH. No movie/game audio, images, textures or third-party effect packages are included.
