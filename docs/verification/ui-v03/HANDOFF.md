# UiV03 — sparse HUD and planner

Task: V02 presentation refinement / UiV03. Sole UI writer; no Git index, branch, or commit mutation.
Checkout: `.local/i01-integration`, branch `codex/i01-integration`, base `38b2b09b9a9dbd00e9aacbee2cc6ece266679411`.

## Changes

- `Assets/_OutOfReach/UI/Runtime/FlightBoardView.cs`: removes decorative survival-panel heading/icons and repeated gauge explanations. Cabin defaults to oxygen reserve (kg) and battery (kWh); configured CO2 action/emergency state adds a third reading. Hold Tab or the existing Systems control reveals all six resources. Sealed suit or outside state replaces cabin readings with suit oxygen and Core-supplied remaining simulation duration. Open suit in a pressurized cabin retains cabin readings. One phase objective, focused action with a local E key, and a fixed subtitle region. Base HUD hides during Report and returns on a fresh mission.
- `Assets/_OutOfReach/UI/Styles/MissionFlightBoard.uss`: unboxed neutral 3px gauges, small contextual prompt, fixed two-line-sized subtitle region, flatter/narrower planner column, and inspection display below the objective. All text is English; existing licensed Source Sans typography retained.
- `Assets/_OutOfReach/UI/Runtime/MissionFlightBoard.cs`: additive optional `ConfigureCargoLoading(ICargoLoading)` refresh binding. A Changed event re-reads the existing authoritative planner draft only in Planning. Null detaches. Disable, rebind and dispose unsubscribe. No cargo implementation, inventory, prices, movement, or timer is introduced.

All existing public calls remain compatible. All asset GUIDs and metadata remain untouched. Existing generated prefab/style references remain valid; no scene or prefab regeneration was needed.

## Root composition integration

Root owns and changed `I01MissionComposition.cs` and `I01SessionControls.cs` during this checkpoint: a rejected interaction briefly replaces the focused prompt; focus changes/expiry restore the latest authoritative prompt; successful interaction feedback no longer creates a second banner; the permanent controls hint and duplicate outcome reason banner are removed. UI did not edit these files. The five exact source files and their hashes are preserved in `verified-source/` and `source-sha256.json`.

Keep the phase-based objective for this bounded pass, as root requested. It may still read “Repair the antenna” in a capture immediately after the antenna is repaired; no private mission progression was added to the view.

For C05, call `MissionFlightBoard.ConfigureCargoLoading` with the actual optional `ICargoLoading` instance when integration is enabled. C05 is not activated by this UI change, and the optional refresh path has not been exercised against a live C05 runtime yet.

## Verification

- Unity 6000.3.28f1, Windows, D3D12; existing saved Bootstrap; `I02PlayQa.Run` without `-quit`.
- First pass: exit 0, 198 checks passed; preserved in `first-pass/`. Actual scene captures inspected for planner, cabin, EVA, communications and report.
- First-pass report capture exposed the underlying HUD around the report edges. One small refinement hides HUD during Report; final confirmation passed: exit 0, 198 checks, zero game Error/Exception/Assert entries. Both final report captures visually confirm that the base HUD is hidden. Restart passes. Evidence: `after/` and `v03-ui-confirm.log`.
- `git diff --check`: pass.
- `python tools/verify_repository.py`: pass, 578 asset GUIDs.
- `python tools/nasa/horizons_snapshot.py`: pass, 32 samples and exact hashes/derivations.
- Mechanical UI detector: no findings (`[]`).

## Evidence and limits

`before/` preserves the earlier local QA captures (12:49–12:50 UTC) before this run. These are historical comparison material, not claimed as renders of the final source hashes. `first-pass/` and `after/` contain new actual scene camera + actual UIDocument captures at 1280×720, with state and capture-method manifests. Screenshots are not composited mockups.

The batch environment produces no ScreenCapture image, so the established harness renders the existing camera and actual UIDocument into a shared target. It excludes ScreenSpaceOverlay UGUI and captures outside impact. Do not claim an impact/blackout overlap capture or 1920×1080 validation. No interactive desktop walkthrough, comfort check, or standalone build was performed here. The smoke uses documented QA teleports inside the cabin; the exterior traversal uses real movement input.

The existing harness isolates an editor-only Unity SearchDatabase startup exception. `runtime-errors.txt` records game Error/Exception/Assert separately. Licensing handshake warnings recover before the run. No USS import warnings were found.

The gameplay repeat-rejection and restart paths run in the existing smoke. A dedicated empty-space E / focus-expiry visual assertion and a held-Tab screenshot are not present in that harness; those are not claimed as checked. The ordinary resource labels are verified against authoritative snapshots, including the values hidden behind inspection.
