# I06-DAY-UPPER-MIDDLE

Runtime change: codex/i01-integration @ 6e3b2d7. Only Assets/_OutOfReach/UI/Styles/SurvivalChapter.uss changes presentation: rest and wake containers start 22% down the viewport. Existing eyelids, 3-second wake animation, audio and domain calendar are unchanged.

Unity 6000.3.28f1 isolated saved-Bootstrap run completed: 92 assertions passed (result.json, 2026-11-15T11:35:48Z). Native 1280x720 camera + UIDocument captures visually inspected: DAY 1 now occupies the upper middle during rest, eyelid opening and reduced-motion wake. No overlap with top corner HUD in the inspected wake capture. Reused RestWaterQa fixture, unchanged from docs/verification/i06-rest-wake-cinematic-20261115/RestWaterQa.cs. Local log: TestResults/rest-upper-middle-01.log. Process ended normally; process exit code was not captured.

Repository verifier passed (1440 GUIDs); NASA offline verifier passed (32 samples). Existing dirty movement experiments, settings, materials, avatar work and FlightBoardView were retained. source-sha256.txt identifies the actual tested runtime files; this is a scoped presentation check in the existing dirty integration checkout, not proof that movement WIP is correct or a clean standalone build passes. The visible controls hint still belongs to the previous unverified input experiment and is not the newly requested control contract.

Integration anchor: SurvivalChapterPresenter presentation layer, existing SurvivalChapter.uss selectors. No scene, public API, body animation or mechanic changed. Original user-operated checkout was not changed by this increment. No merge performed.

Coordination: the movement handoff now includes contextual mouse grip, diagonal/prop contact, clipping classification and polished ordinary walking in both original-reference suits. Claude art ACK is distinct from pending gameplay ACK. Exact separate LaunchFX_V02 and LaunchTowerV02 scopes were provided in issue #7 comment 6893713995; art self-reports resuming after 18:00 UTC / 19:00 CET. Cockpit 11ebd9c is delivered by its author but not yet reviewed in the game's URP scene.
