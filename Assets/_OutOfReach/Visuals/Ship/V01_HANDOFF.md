# V01: interior visual foundation

Owner: Codex. Branch: `feature/visuals-cinematics`. Baseline: `9dce09c3650e16f15796cc73ece51f82451b0a0e`.
Checkpoint revision: the commit containing this file. This is a visual foundation, not a playable mission or final art pass.

## Deliverables

- `Assets/_OutOfReach/Visuals/Ship/Aurora7Interior.prefab`: assembled, metre-scale interior at world origin.
- `Assets/_OutOfReach/Visuals/Ship/Modules/`: separate Cockpit, Engineering, Storage and Airlock prefabs at local origin.
- `Assets/_OutOfReach/Scenes/Visual/Aurora7VisualPreview.unity`: authoring scene with one temporary camera under an `EditorOnly` root; no audio listener.
- `Assets/_OutOfReach/Scripts/Presentation/ShipVisualAnchors.cs`: serialized integration references, no domain authority.
- `ShipLabelAtlasBinder` plus `Visuals/Ship/Shaders/ShipDepthLabel.shader`: depth-tested text with a live font atlas, including after a fresh editor launch.
- `Scripts/Presentation/Editor/ShipVisualBuilder.cs`: reproducible builder, scene validator and GPU still capture. Uses the existing URP package and Unity built-in font; no packages added.

All geometry is original primitive authoring geometry. It has no external model or texture dependency. Detailed licensed props and more directional lighting are a separate art pass. Player-facing labels are English; diagnostic placeholders explicitly disclose that they are unbound.

## Integration anchors

Bind references from `OutOfReach.Presentation.ShipVisualAnchors`; do not search names at runtime. Positions below are metres in the untransformed assembly. PlayerSpawn is the feet position; target eye height is 1.65 m above it.

| Reference | Position | Purpose |
|---|---|---|
| PlayerSpawn | (0, 0.05, 0.8), yaw 180 | Player spawn facing cockpit |
| CockpitConsole | (0, 1.1, -1.72) | Console interaction anchor |
| PowerRepair | (-1.65, 1.15, 6.1), yaw 90 | Power diagnostics/repair |
| LifeSupportRepair | (1.65, 1.15, 8.1), yaw -90 | Life-support diagnostics/repair |
| AirlockInner | (0, 0, 17) | Inner gate boundary |
| AirlockOuter | (0, 0, 21) | Outer gate boundary |
| EvaStart | (0, 0, 22) | Reserved for V03 |
| AntennaRepair | (3.2, 2.4, 13), yaw -90 | Reserved for V03 |
| EvaReturn | (0, 0, 21.7), yaw 180 | Reserved for V03 |
| FinaleCameraAnchor | (9, 5, 27), yaw 205 | Reserved final framing |

`InnerDoorLeft`, `InnerDoorRight`, `OuterDoorLeft` and `OuterDoorRight` are separate visual leaves. They have no colliders and cannot authorize EVA. The integrator supplies independent gameplay gates from Claude's systems. Structural walls and cabinets have colliders; decorative parts do not.

Import only the ship prefab into Bootstrap. The preview camera is not inside it. Bootstrap retains its single gameplay camera and audio listener. No gameplay state, player, resource counters, repair scripts, event subscriptions or service discovery are present here. Do not attach Claude components to this asset on both branches; bind them through the coordinated Bootstrap composition.

## Verification actually performed

Environment: Windows, Unity **6000.3.28f1**, existing URP settings, isolated copy `.local/unity-baseline-check` so the user's open editor was not reused by a second process.

- `ShipVisualBuilder.BuildAndValidate`: Unity exit 0, `V01_BUILD_COMPLETE` and `V01_VALIDATION_PASS`. Log: `.local/unity-baseline-check/v01-build-refined.log`.
- Validator: 10 uniquely named anchors, all 14 serialized references, no missing scripts/materials, no camera/listener inside ship, 405 interior standing-capsule/floor samples, repair/spawn standing areas, and collider-free visual door leaves.
- Fresh-process `ShipVisualBuilder.CapturePreview`: Unity exit 0 and five actual 1600 x 900 GPU renders. Log: `.local/unity-baseline-check/v01-render-refined.log`; images under that project's `.local/v01-captures/`.
- Corridor, cockpit and engineering captures visually inspected. Fixed text rendering through walls and overlapping front-cockpit signage. GPU stills demonstrate rendering, not interactive play or performance.
- `python tools/verify_repository.py`: PASS after import, 82 asset GUIDs before adding this handoff.

The verified source and generated scene/prefabs were copied back from that isolated project with metadata preserved. Run the same builder/validator after changing geometry. The builder asks before replacing an unsaved scene in interactive use.

## Remaining work and limits

This is still a primitive interior blockout. Lighting is uniform and surface detail is limited. The screens have no bound telemetry. No exterior walkable route, EVA gate, playable controller, cinematic sequencing, audio playback, UI, resource simulation or outcome is included. No Windows executable, gameplay tests, end-to-end run, performance measurement or second-machine launch has passed for this checkpoint.

V01 does not change contracts, Bootstrap, shared documentation, datasets, project settings or Claude-owned paths. Integration and merge remain subject to the coordinated process in INTEGRATION.md.
