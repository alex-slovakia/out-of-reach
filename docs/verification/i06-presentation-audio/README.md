# I06 presentation and audio checkpoint — 14 November 2026, 19:46 CET

Verified in the isolated `codex/i01-integration` checkout using Unity **6000.3.28f1** on Alex's Windows machine. This commit contains the tested production sources; each run records their SHA-256 values. Base before these changes: `c7f9c39`. Alex's original open project is still `33f474f` and has **not** been updated by this checkpoint.

## Changes and actual verification

- Reworked, left-aligned end card over the real game scene, optional detailed record/sources, menu feedback and reduced-motion support. Removed the rejected centered boxed-button layout.
- Bound C06 survival needs/rest presentation to the actual services; genuine recovered and failed outcomes show distinct short summaries. Fixed a stale false-signal subtitle interrupting an actual received Earth reply.
- Four original melodic score moods, a hard crisis cut, dialogue ducking and three distance-driven deck contact sounds. No airborne space footsteps.
- Correct speaker identities: Houston is the player, AURORA-7 the ship, Mission Control on Earth the remote speaker. The approved Houston-to-Earth performance is bound separately from the ship warning and delayed Earth reply.
- Presentation runner: **270/270 assertions**, exit 0 (`i06-presentation-play-3.log`). Actual outcome, real bounded rests/beacon costs, pause, disposal/rebind, report navigation and restart were exercised. Ending presentation finished without a test skip in this runner.
- Audio runner: **209/209 assertions**, exit 0 (`i06-audio-play-5.log`). Real Unity listener PCM was nonzero in **8/10** sampled windows; the two intended silent windows were pause and loss. The actual W-input probe moved 0.740 m along the EVA route without deck footsteps. This is a bounded probe, not a complete manual spacewalk.
- Repository verifier: PASS, 762 asset GUIDs. NASA offline verifier: PASS, 32 samples and exact hashes/light-time regeneration. Unity scripting checker: 0 errors; six temporary Editor-driver naming warnings, whose native attachment was verified by the actual runs.

## Scope and limitations

C06 is enabled **in memory by the verification runner**. The saved Bootstrap still defaults to the existing contact-restored slice until physical habitat actions are integrated and verified. These tests issue domain setup/advance commands, and disclose fixture teleports and skips; they are not a human full-game playthrough. Native listener PCM is not proof of physical speaker audibility or human music approval. Three dialogue performances have user listening approval; the new score still needs listening review.

The screenshots here are real Unity captures, not target illustrations. The final rescue/Mars cinematics still use interim framing and have not achieved the approved full visual target. Microgravity, look-down body, marine boarding, habitat models and tool repair are separate integration work, not silently included in this checkpoint.

Earlier failed launches were diagnosed and retained locally: an Editor helper attachment error, outdated test copy assertions, an assertion 1.7 ms before a music fade completed, and a test fixture facing inward at the tether start. The final audio runner explicitly faces the real outward tangent without moving feet before testing W. No production movement/fade rule was changed to make those tests pass. Unity also logged the known Editor Search database exception; no clean-Editor-console claim is made.

## Reproduction

Temporary runners are preserved in `tools/verification/i06-presentation-audio/`. Copy each `.cs` to a separate folder under `Assets/Editor/` in an isolated checkout, let Unity import metadata, then run sequentially with graphics enabled:

`Unity.exe -batchmode -projectPath <isolated-checkout> -executeMethod I06PresentationQa.Run -logFile <log>`

`Unity.exe -batchmode -projectPath <isolated-checkout> -executeMethod I06AudioQa.Run -logFile <log>`

Do not add `-quit`; each runner terminates with its result. Do not open the same project twice. Raw logs and listener WAV recordings remain in ignored local verification output; curated checks and signal measurements are alongside this file. No organizer submission or public release was made.
