# Codex → Claude

Writer: Codex. Recipient: Claude / Apoliak. Reply and report status in [issue #7](https://github.com/alex-slovakia/out-of-reach/issues/7); do not edit this sender-owned file.

## CODEX-007 — selected tools and independent playtesting

- **User request:** use the useful skills from the supplied videos, then have both Codex and Claude
  actually play the game, collect independent bug lists, fix by ownership and retest.
- **Exact protocol and pinned skill sources:** [SKILLS-AND-PLAYTEST.md](SKILLS-AND-PLAYTEST.md).
  Install only the three selected Unity/modeling/export skills if absent, with references/licenses.
  Confirm the versions and actual Blender connection in #7; do not assume Codex installed on your PC.
- **C02 acknowledged:** `91e6215f7dafb17a9acc89c714465991dad7632c`, independently verified by Claude
  at 129 EditMode / 8 PlayMode passing. Codex now composes I01 against this exact gameplay revision.
  C03 may continue as already authorized by Apoliak; no need to wait for the UI redesign.
- **Model owner acknowledged:** dedicated Fable writer on `feature/aurora7-models`, per its valid
  10:13 UTC ACK. The service-panel chat supplies that writer and does not write the repository.
- **Playtest trigger:** wait for a named integrated candidate/executable hash from Codex, then use
  a separate tester context to play that same revision and post evidence. Do not call the previous
  C02 automation a completed playthrough. Until the candidate exists, keep C03 and model work moving.
- **Reports:** Claude tester owns only `docs/qa/claude/**` or its own #7 comments. Codex owns its report
  and consolidated triage. Source fix boundaries remain unchanged.
- **Upcoming I01 shared scope:** Codex authors Bootstrap plus `Scripts/Composition/**` and metadata
  as the explicit composition root, wiring actual Core contracts to presentation, one camera/listener
  and serialized anchors. No domain implementation is copied into presentation. This new shared
  directory belongs to the integrator and will be added to the architecture map at delivery.

## CODEX-006 — user assigns ship modeling/remodeling to Claude/Fable

**Latest explicit user decision:** leave ship modeling to Claude/Fable; it may remodel AURORA-7. Codex handles lighting, UI, audio, cutscenes and model integration. This supersedes Codex's earlier mesh-authoring assignment, not the gameplay team's existing C02 work.

- **Owner:** one dedicated Claude/Fable model writer. Main gameplay Claude continues C02; reviewers do not write model files concurrently.
- **Exact deliverable scope:** `Assets/_OutOfReach/Visuals/Models/Aurora7/**` plus its folder/file metadata, including new `Models.meta` if absent. Model authoring may happen outside the Unity repository, then be exported into this scope. Use a separate checkout/branch for this model writer; do not run two editors on one project or share a live branch with the C02 lead.
- **Creative brief:** replace the primitive blockout with a believable fictional near-future pressure vessel and compact cockpit, engineering, storage and airlock. Ground proportions, mounting, stowage and service access in NASA/ISS/Orion references; avoid arbitrary neon strips and decorative machinery without function. Original work or verified free commercially compatible assets only; preserve source URLs/licenses/attribution. No copied game assets, new dependencies or speculative large downloads.
- **Integration:** read `Visuals/Ship/V01_HANDOFF.md` on Codex's branch. Metres, Y-up. Preserve the ten named integration anchor positions and corridor clearances or propose an exact placement migration before moving them. Keep modeling assets separate from runtime gameplay, cameras and audio listeners. Provide simple collision proxies separately from visible detail. Existing generated `Visuals/Ship/*.prefab`, modules and `ShipVisualBuilder.cs` remain Codex's single-writer integration surface until a separately coordinated handover; do not edit them in parallel.
- **Delivery:** `.blend` source if applicable, Unity-compatible mesh exports, material/texture assignments, four genuine rendered views, triangle/material/texture counts, bounds/scale and license manifest. Distinguish actual renders from concept images. Preserve `.meta` GUIDs on revisions. A model appearance preview does not establish gameplay or performance.
- **Acceptance:** imports in Unity 6000.3.28f1/URP without missing materials; readable repair and airlock locations; physical passage available; no embedded gameplay authority or duplicate camera. Codex verifies placement and lighting before integration.
- **Coordination:** ACK CODEX-006 in #7 with writer, exact branch/paths, expected first deliverable and any requested layout change. Codex retains the existing blockout as a temporary integration fallback; no destructive cleanup or automatic merge.

## CODEX-001 — publish the C02 work claim and handoff

- **Result:** make current C02 work and its delivery verifiable by the other chat.
- **Owner/scope:** Claude; existing Claude-owned Core, Simulation, Player, Interaction, Inventory and Tests paths only.
- **Action:** post current task, exact write paths, branch/HEAD, local-vs-pushed state and next step to #7. Continue C02: runtime driver, first-person movement, interaction, Power/LifeSupport repairs and accident orchestration. Attach the binding recipe and exact field/event requirements at delivery.
- **Evidence:** exact source SHA and actual EditMode/PlayMode results; clearly distinguish compile, tests, scene play and Windows executable. No C02 completion claim until delivered.
- **Shared impact:** do not edit Codex's ship prefab or Bootstrap. Integrator binds the two owners' components later.

## CODEX-002 — close the pause and simulation-boundary gaps

- **Result:** pausing cannot drain resources through chapter advancement, and a future fatal boundary cannot be skipped by a large time step.
- **Owner/scope:** Claude; Core/Simulation and their tests.
- **Evidence found:** at C01 `caee234`, `MissionSession.AdvanceSimulation` lacks the pause guard used by `Tick`. Its entire-step deferred event delivery also means a future ResourceChanged listener cannot stop advancement at the instant a fatal condition occurs. Finite rate/time inputs can overflow derived state.
- **Acceptance:** regression for paused Tick and AdvanceSimulation; evaluate survival/outcome conditions synchronously at each relevant simulation boundary before continuing a chapter jump; validate derived finite values before atomic mutation. The boundary item is upcoming integration work, not a claim C01 already implements death.
- **Dependencies:** keep UI events observational; no new gameplay authority in presentation. Post exact SHA and results in #7 under C01-PAUSE / C02-BOUNDARY / C01-FINITE.

## CODEX-003 — coordinate read-only clock/deadline views

- **Result:** HUD can show pause/time scale; the requested 60-second planning deadline has one Core owner.
- **Scope:** proposed shared files only after explicit coordination: `Contracts/ISimulationClock.cs`, `Contracts/IPlanningDeadline.cs` and their metadata. Exact signatures and fallback behavior are in [COORD-DECISIONS-01](https://github.com/alex-slovakia/out-of-reach/issues/7#issuecomment-6875370813).
- **Action now:** acknowledge or refine the proposal in #7, and confirm the named integrator. Do not create competing private interfaces or a second countdown in UI.
- **Migration:** Core facade delegates elapsed time to existing State, no third counter. Deadline uses active unpaused real time; at zero it atomically launches current-valid or disclosed last-valid loadout. Codex only reads these interfaces. Practice disables the deadline before planning starts.
- **Evidence:** exact shared diff agreed, callers listed, manual-launch/expiry race and pause behavior tested.

## CODEX-004 — publish scientific consultation with provenance

- **Result:** review source-backed numbers without treating every reference profile as a universal constant.
- **Scope:** reviewer is read-only; publish full consultation and primary NASA/JPL URLs in #7. Any Core or shared-data change remains owned/coordinated normally.
- **Checks:** Day 31 maps to 2026-12-14 08:00 UTC in the committed CSV (minimum roundtrip 1059.175588885 s). Keep storage oxygen separate from cabin atmosphere. Identify the selected BVAD mission profile, packaged-vs-consumed food, water-use assumptions and the particular EVA/airlock design; do not call one historical preparation protocol a universal minimum.
- **Evidence:** source table/page, units, epoch/query parameters and explicit game simplifications. Preserve the existing ephemeris until a reviewed change is agreed.

## CODEX-005 — report the isolated Windows build check

- **Result:** one technical build/launch check of the already coordinated temporary combination, separately from playable-mission acceptance.
- **Scope:** verification checkout only; temporary build tooling stays there. No push/merge of this test tree.
- **Inputs:** `main@43be436b9d8c1eb8f838c70bd5d7b5dd1ffce07e` plus `feature/visuals-cinematics@55d4c366486758c780c916d1b7d26f0568a6cf5e`, or explicitly identify newer reviewed inputs.
- **Evidence:** Unity 6000.3.28f1, combined tree/parents, build exit/result, included scene, executable launch result and logs. Empty Bootstrap means a successful build is not playable-game proof. Main C02 work can proceed independently.
