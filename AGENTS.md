# OUT OF REACH agent rules

Read `PRODUCT.md`, `ARCHITECTURE.md`, `INTEGRATION.md`, `TASKS.md`, `NASA-DATA.md` and `TESTING.md` before changing the project.

## Engine and scope

Use **Unity 6000.3.28f1**, C#, URP, Windows x64. This is the user's explicit choice matching the currently running editor; ignore the older `6000.3.26f1` in preparatory material. Do not regenerate the Unity project or change engine/packages independently.

Build the one-mission vertical slice in `PRODUCT.md`. Implement one independently testable task at a time. Do not generate all systems in one pass or expand into an open world.

## Ownership is mandatory

**Latest explicit user override (14 Nov 2026, 13:02 UTC): Codex owns
`Assets/_OutOfReach/UI/**` again. Claude explicitly reported UI NOT STARTED at 13:00 UTC;
Alex authorized Codex to take it and root claimed the scope in issue #7 comment 6877645638.
Root assigns ONE UI writer. Claude must not start a parallel UI writer. Camera/lighting/audio,
composition wiring and integration remain with Codex.**

Follow the exact path map in `ARCHITECTURE.md`. Codex owns presentation, visuals, visual prefabs/scenes, UI and audio. Claude owns core, simulation, interaction, player, inventory and tests. The contracts, Bootstrap scene, package/project settings, documentation, shared datasets and repository tooling belong to the integrator.

The first shared baseline is authorized. **After that baseline, stop before changing a shared file and coordinate the exact change with the designated integrator.** Do not edit the teammate's files. Report a requested contract change with its reason, caller impact and migration steps. Do not bypass the boundary by copying the teammate's system into your folder.

Use separate checkouts and branches: `feature/visuals-cinematics` for Codex and `feature/survival-simulation` for Claude. Never edit one Unity scene from both branches simultaneously. Preserve all existing `.meta` GUIDs. Move assets together with their metadata. No destructive cleanup or reset of another developer's work.

## Integration and communication

Contracts under `Assets/_OutOfReach/Scripts/Contracts/` are canonical. Consume the actual signatures; do not infer alternate ones from a prompt. Domain state has one authority. UI and effects may issue commands through contracts, but never simulate a parallel outcome.

At each checkpoint report: task ID, branch/commit, changed paths, verification actually run, integration anchors, and known failures. Owners keep task evidence in their handoff; the integrator updates shared `TASKS.md` after coordination. Do not silently edit shared docs from a feature branch.

Do not merge branches automatically. The designated integrator must coordinate the merge, inspect conflicts and run the integration checks. Do not claim a passing Unity compile, test, build or playtest without evidence from that exact revision.

## Scientific and artistic rules

Use committed NASA data with source URLs, units, epoch and explicit assumptions. The spacecraft is fictional. Distinguish reference ephemeris, simplified simulation and cinematic shortcuts. A reply cannot precede an outbound and inbound light-travel interval. No instant rescue or instant hydroponic food. External space sounds must be suit/radio/contact-transmitted sound or a clearly non-diegetic score.

Use original assets or assets with verified compatible licenses and attribution. No copied game assets, unlicensed music, speculative large downloads or new dependencies without coordination. Keep the cinematic language graphite/amber/cyan/red with legible objectives and restrained effects.

## Delegation

The user authorized parallel agents for this setup. Every delegated task must specify disjoint files and a concrete deliverable. Shared chat context is not a file lock. No two agents may write the same file concurrently. Each branch has one lead writer; reviewers are read-only unless reassigned explicitly.

## Unrelated supplied rule

The user's Retellai Step One/Charon instruction concerns Retellai demonstrations. It does not apply to this Unity game; do not introduce Retellai voice assets or flows into OUT OF REACH.
