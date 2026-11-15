# Overnight integration checkpoint — 15 November 2026, 01:05 CET

Branch: `codex/i01-integration`. This is an integration work-in-progress, not the original open/playable checkout and not a release. The original remains `33f474f`; the running editor and unrelated local changes are preserved. Feature freeze is04:00 CET and the target handoff is05:00 CET. No80–90% completion claim follows from the work below.

## Verified component evidence

- Standing body:158 native assertions, exact metre-scale idle source, full source skin comparison, standing/downward/EVA/float views. [Evidence](i06-body-standing-unit02-native/README.md).
- Session menus:82 native assertions with actual UI navigation, restart, two-step Quit and cancellation on context change. The test intercepts actual application termination; packaged-player Quit remains pending. [Evidence](i06-session-menus-native-v01/README.md).
- Recovery:5932 scoped native assertions with the real delayed recovery state, approach sequence and clearly later Mars view. [Evidence](i06-recovery-native-v03/README.md). The source Mars map has NASA/JPL credit. This is not the whole game played continuously by a human.
- Claude C06 audit imported: registered domain interactions, clock and ledger tests plus a fix preventing an immediate new rest from sleeping through the beacon window. Author reports323 EditMode and72 PlayMode on9e313ed; combined integration tests still pending.
- Land-site source uses exact11-module FBX and16anchors. Unity welds104 measured degenerate pole faces:6458 native triangles versus6562 source, with source/triangle correspondence recorded by the author. No importer/count tolerance is relaxed. An optional-ocean null call in the land scene was fixed. The continuous loading/departure test is pending; its first run exposed an outdated exact-target assertion for the intentionally wrapped antenna interaction.

## Still unaccepted and actively investigated

- Continuous microgravity traversal: basic input/contact/lifecycle passes do not yet establish a complete path through the ship. The latest whole-route run stalled before the first doorway. New exact refusal diagnostics are installed; source anatomy and collision guards remain unchanged.
- Antenna repair: the three-second work gate and wrench contact were demonstrated, but actual native images exposed a disconnected forearm cap. A fixed-length350/320mm continuous sleeve and closer collision-checked stance are now candidates. First candidate rejected the initial wrist angle; the downward-elbow correction is being retested with the original80-degree and2mm limits. [Earlier failing visual evidence](i06-antenna-work-native-v02/README.md) is retained deliberately.
- Earth terminal: approved V2 physical hardware/data/scripts imported; scene binding and native input/finger/occlusion/lifecycle checks are pending. It displays an archived NASAEPIC observation, not live weather. No terminal visual acceptance claimed.
- Land/loading and full microgravity route, terminal acceptance, exact committed-candidate suite, Windows player build, second-PC verification and original-project promotion remain required.

Only source/art/evidence intended for integration are included. Temporary native QA fixtures, generated settings, the rejected standing V1 export and unrelated review folders are excluded. Detailed evidence folders retain their own earlier source hashes; a pass there applies to that measured version, not silently to every later edit.
