# I06 habitat and contact native checkpoint

Unity 6000.3.28f1, isolated `codex/i01-integration` checkout, GPU batch execution. The user-facing original editor/project was not changed.

## Actual habitat result

`HabitatNativeQa.Run`, log `TestResults/i06-habitat-native-2.log`, exit **0**, **579 assertions passed**. Saved Bootstrap at **85b0994** with C06 enabled in the saved scene. The exact input hashes, real interaction rays, full checks and scope are retained below. The untracked Marine package was also present/imported, but was not instantiated in this scene.

- All seven real cargo types: unload/return/pick up/stow, default manifest restored.
- Real scene resolver and commands for ration, water, bed, payload salvage, build/plant/harvest and recovery beacon.
- Default repair costs leave no spare parts: actual engineering payload salvage supplies three parts once, then construction consumes them. No inventory grant.
- Bounded rests advance the real calendar and reserves. Crop is not instant. Pause, wrong-phase visibility, occlusion, restart and all28 target identities checked.
- Source/scene setup is partly scripted: aisle teleports, earlier domain repair/EVA/communication commands and cinematic skips. This is not a complete manual input playthrough or an approved final visual.
- Root inspected native wide, galley and growing-crop frames. The fixtures retain ten GPU captures locally; four are attached here. They are real Unity renders, not generated concepts.

**Asset side effect correction:** the runner's original scope says no imported asset writes. That means the runner does not explicitly write them; Unity/URP nevertheless normalized `HB_Labels.mat` (removed the disabled SHADOWCASTER pass) and `HB_Water.mat` (enabled premultiply keyword / changed SrcBlend5→1) on exit. This checkpoint records that observed difference; it does not claim byte-identical materials after native import. The saved Bootstrap and production C# were not rewritten by this runner. The initial `-1` launch was a root manifest-copy error (relative source path), not a failed gameplay check; `-2` is the actual complete run.

## Actual contact defect and fix

The earlier uninstrumented right-grip failure could not establish its cause. A dedicated real-frame reproducer now confirms a specific defect independently:

- **Before:** `ContactNativeQa.RunHitchHeld`, exit **1**, static left rail remains the authoritative held anchor. A real175.7ms frame changes the glove from Holding to Returning despite accepted geometry; callback count stays1 and player position stays unchanged. See `hitch-before/glove-Left-hitch-held-hitch-frame.json`.
- **Patch:** only the two presentation timing guards in `AstronautHandsPresenter.cs` and `BareArmContactPresenter.cs`. A finite frame over100ms advances zero animation time while retaining every geometry/ownership validation. Nonfinite/negative time still cancels. No collider, reach, palm tolerance or Player physics changes.
- **After:** `ContactNativeQa.RunHitch`, exit **0**, **114 checks**, both held grips and partial reaches survive the deliberate stall. Actual target jump and obstruction still cancel. Two post-stall GPU captures retained locally.
- **Regression:** ordinary `ContactNativeQa.Run`, exit **0**, **179 checks**, six contact-action captures plus two look-down captures. This includes pause/release/removed surface/blocked path/reset/duplicate-action checks.

These bounded fixtures do not bind click-to-float to Bootstrap. Microgravity remains off in the saved playable scene. Whole-palm penetration, floating body framing and the glove forearm's visible cut end remain separate art/integration work; passing a palm reference-point test does not certify complete hand anatomy.

## Limits

UnityEditor.Search.SearchDatabase produced its previously observed editor-only ArgumentOutOfRangeException during runs; it is retained in the environment error logs, not concealed as a clean console. No Windows player build, complete human playthrough, final rescue film, marine prologue integration or original-project promotion is claimed here.
