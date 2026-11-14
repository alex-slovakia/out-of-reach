# Coordinated integration — GPT-6 Astra High

Use a separate integrator checkout and choose **GPT-6 Astra / High** if available. This prompt prepares an integration; it does not itself approve merging feature branches.

First read AGENTS.md, ARCHITECTURE.md, INTEGRATION.md, TASKS.md, TESTING.md and the two owners' handoffs. Confirm the humans have named the integrator and explicitly coordinated the exact branches/commit SHAs, target checkpoint and shared paths to change. If that coordination is missing, perform read-only compatibility review and return the concrete merge/wiring plan; do not merge or edit shared files.

When coordination exists:

1. Verify clean checkout and remote/base SHA. Create an integration branch for the specific checkpoint. Inspect changed-file ownership and contract compatibility before merging the agreed commits. Never overwrite another owner's file to resolve a conflict; consult the owner.
2. Compose Bootstrap from authoritative Claude services, one player/camera/audio listener, Codex visual prefabs and views. Bind serialized references explicitly. Exclude preview mocks and preview cameras. Preserve all existing GUIDs. Add only approved build-setting changes.
3. Verify initial snapshots, subscription lifetime, command rejection feedback, cinematic sequence completion/skip/timeout, room/door collision and game-phase gates.
4. Run repository and offline NASA verification, exact-editor compile and relevant EditMode/PlayMode tests. For I01, prove planner → accident → interior repair. For I02, prove success and failure from planner through EVA/communications/report/restart.
5. Build Windows x64 and test the executable when the checkpoint requires it. Record exact SHA, Unity version, test counts, machine, logs and manual scenarios. Do not label a build playable from compile alone.

Update shared status/evidence only within the coordinated scope. Stop before promotion to main with the integration branch, evidence and remaining failures. The humans coordinate the final merge after seeing that concrete result. Do not force-push, delete branches, rewrite history or silently change interfaces.
