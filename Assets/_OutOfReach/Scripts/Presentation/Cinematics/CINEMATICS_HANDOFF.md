# V02 cinematic presenter source handoff

Owner: Codex presentation. Assembly: `OutOfReach.Cinematics`, referencing only `OutOfReach.Contracts` and Unity engine assemblies. Component: `OutOfReach.Presentation.Cinematics.MissionCinematicPresenter`.

## Explicit integration API

Call `Configure(IMissionEventBus events, IGameState state, ISimulationClock clock = null)` from the designated composition root after creating the authoritative mission session. No scene searches, service locator or domain implementation dependency is used.

- `bool SkipActive()` restores the active visual sequence and publishes its matching `CinematicFinished(..., WasSkipped: true)`. Returns false if idle.
- `bool ReducedEffects { get; set; }` immediately removes shake/blackout/pullback motion when enabled, including while paused. Captions and a gentle emergency tint remain.
- Read-only `IsPlaying`, `ActiveSequenceId` (zero when idle), and `CurrentCaption` expose presentation status.
- `event Action<CinematicRequested> SequenceStarted` fires after a request is accepted and its baseline is captured.
- `event Action<CinematicFinished> SequenceEnded` fires after restoration, before the matching event-bus acknowledgement. Audio adapters should match the sequence ID when stopping an effect.
- `event Action<string> CaptionChanged` carries English caption text; an empty string clears this presenter's caption. The presenter creates no UI or audio objects.

Outward listener exceptions are logged individually and do not prevent later listeners, restoration or acknowledgement. A fresh request declined for an incompatible phase receives a skipped acknowledgement without Started/Ended notifications. Duplicate/stale requests receive neither effects nor another acknowledgement.

## Inspector references

All references are optional. With none bound, sequences still provide captions, timing and acknowledgement.

| Field | Required integration treatment |
| --- | --- |
| `cameraOffsetPivot` | A dedicated presentation offset transform beneath the player/look rig and above the existing gameplay camera. Its local pose belongs exclusively to presentation. Do not assign the movement root or a camera transform simultaneously controlled by the player. |
| `endingFramingAnchor` | Optional target **world pose of that offset pivot**, not an additional camera. Author for the existing camera's offset. Missing reference retains the current frame. Reduced effects retains the current frame too. |
| `effectLights` | Optional lights reserved for temporary modulation during a sequence. Color, intensity and enabled state are snapshotted/restored. Avoid competing light writers; reapply authoritative damage lighting after SequenceEnded if needed. |
| `blackoutOverlay` | Optional black CanvasGroup artwork. Integrator sets `blocksRaycasts=false` and `interactable=false`; presenter only changes/restores alpha. |
| `emergencyOverlay` | Optional restrained red CanvasGroup artwork with the same non-interactive settings. Use a different group from blackout. |

No Camera, AudioListener, input lock, cursor change, FOV change or mesh is created. The gameplay camera's own local transform is never written; cinematic offsets are applied and restored on the dedicated pivot. Core/player code remains responsible for movement/input policy while an authored ending frame is active. This presenter must not become a competing movement authority.

## Confirmed request behavior

| Request | Accepted authoritative phase/outcome | Default duration | Presentation |
| --- | --- | --- | --- |
| `Impact` | Intro or Emergency, not Lost | 4.5 real seconds | One smooth dim/black fade, restrained decaying camera displacement, then emergency red tint. Reduced effects removes displacement and blackout/dip. No damage is applied here. |
| `FalseSignal` | Repair, Eva or Transmission, not Lost | 5 real seconds | Caption changes from local buffer playback to explicitly archived transmission; no false instantaneous Earth reply. Audio can attach through sequence events. |
| `CommunicationRestored` | Transmission, or Outcome with RecoveryPossible | 4 real seconds | Restrained cool light tint and confirmed-channel caption; recovery planning text requires authoritative RecoveryPossible. No reply timer is advanced. |
| `EndingPullback` | Outcome with an actual success/loss outcome | 8 real seconds | Smooth movement to optional framing anchor, or current-frame fallback. Caption follows the confirmed outcome. |

Durations are clamped to 0.25–40 real seconds, below the inspected C01 coordinator's default 45-second fallback. Invalid configured duration falls back to four seconds. No simulation-time acceleration is used. With `ISimulationClock` supplied, `IsPaused` freezes visual elapsed time and the current pose. Without that optional view, presentation follows unscaled real time and cannot infer domain pause. No elapsed time is accumulated while paused.

## Lifecycle and handshake

Only `CinematicFinished` is published to the mission bus. The presenter never calls state transitions, advances clocks, charges resources or publishes authoritative phase/outcome events.

Subscriptions are attached only while enabled/configured and removed on disable, destroy or reconfiguration. Generation-guarded delegates ignore callbacks retained by an older dispatch snapshot. A Configure call made synchronously by a completion listener supersedes the outer Configure without leaking the earlier bus subscriptions. Active state is cleared and all captured visual values are restored before outward completion/acknowledgement, allowing the coordinator to request a subsequent sequence safely. Skip, disable, destroy, reconfiguration, superseding requests, incompatible phase changes and newly confirmed loss all restore and acknowledge as skipped. Repeated completion paths do not acknowledge twice.

Any `MissionEnded` event cancels the old shot. For an outcome-specific ending, Core should publish the confirmed outcome first, then request EndingPullback in Outcome. Report/Planning transitions cancel any active shot. Re-enabling does not reconstruct an already-issued request; the canonical coordinator's fallback handles a missed request.

The inspected C01 coordinator issues positive process-wide increasing sequence IDs using `Interlocked.Increment`, including across sessions. This presenter retains its high-water mark across Configure so old-session requests cannot replay. It does not invent a separate sequence numbering scheme. If the coordinator's ID policy changes, coordinate this presentation assumption rather than silently resetting it.

## Verification actually performed

- Read canonical Contracts, including the newly added read-only ISimulationClock.
- Inspected the C01 `CinematicCoordinator`, `MissionSession` and queued `MissionEventBus` in the isolated verification copy for timeout, pause, dispatch and ID behavior.
- Static ownership/dependency review: only this Cinematics directory added; no Core, player, UI, Audio, shared assembly definition or scene edits.
- Static publication scan: only the typed CinematicFinished acknowledgement is published; no gameplay command calls, extra cameras/listeners or input changes.
- Source whitespace check performed. Runtime Unity verification is delegated to the parent integrator; no cinematic Unity compile/playtest result is claimed here.

## Required integration checks and limitations

Run actual request → completion, duplicate/stale ID, skip-twice, pause/resume, reduced-effects toggle, disable/re-enable, reconfigure and loss-during-shot checks. Compare pivot local pose, light values and overlay alphas against their pre-sequence snapshots. Verify one acknowledgement per accepted request, restored views on Report/Planning and no camera/listener duplication.

No prerecorded dialogue, accident audio, final camera path asset, overlay artwork or subtitle renderer is included. These are optional integration bindings. Tune configured durations against actual recorded cue lengths; audio does not extend the bounded cinematic timer. The presenter provides a bounded visual reaction rather than a new domain coordinator. Exact rendered motion and accessibility still need inspection in the integrated scene.
