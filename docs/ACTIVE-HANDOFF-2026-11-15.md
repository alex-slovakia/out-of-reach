# Active delivery checklist — 15 November 2026

Latest update: ASSET-01/GROUND-02/METAL-01 and REST-03 passed root native integration (13models/20placements;92rest/wake checks). NASA4Ksky imported/bound; clean beauty screenshot still pending. User rejected current terrain as a final look: generated coastal semi-arid landscape target is under `docs/art-direction/prologue-landscape-target-v01/`, not implemented. User has now explicitly reassigned the first movement-mechanics fix to one Codex worker; this supersedes the older Claude-only movement line below. Exact scope/coordination: `docs/MOVEMENT-HANDOVER-2026-11-15.md`. Claude body/character work remains queued; no fresh online acknowledgement.

Lead/integrator: Codex. Working checkout `.local/i01-integration`, branch `codex/i01-integration`; approved original baseline `732d7b0`. Unity 6000.3.28f1 only. Do not interrupt original Play mode or stage unrelated dirty files.

## Codex — actively assigned, disjoint writers

- [ ] **ASSET-01 / hydration_cargo:** import 13 curated Poly Haven models and two ground materials. Owns new `Visuals/ThirdParty/PolyHaven/**`, `Composition/Editor/PolyHavenAssetAuthoring.cs`, `tools/assets/polyhaven_prepare.py`, `docs/art-direction/polyhaven-import-20261115/**`. Raw downloads verified; prepared FBX/textures exist. Root must run Unity importer, inspect and integrate before claiming delivery.
- [ ] **GROUND-02 / music_samples:** ambientCG Ground109 + Ground110; blended near-ground material and <=2.5 cm visual relief outside protected launch surfaces. Owns new `Visuals/Environment/GroundFinishV02/**`, `Composition/Editor/I06GroundFinishAuthoring.cs`, `tools/assets/ambientcg_ground.py`, `docs/art-direction/ground-finish-20261115/**`. Entry `Apply(Transform site)`. No collider changes.
- [ ] **METAL-01 / music_samples, after ground:** two coherent CC0 metal finishes for new mounting hardware/service props. New `Visuals/Environment/SurfaceFinishV01/**` and `Composition/Editor/I06SurfaceFinishAuthoring.cs` only. Preserve Claude ship materials.
- [ ] **REST-03 / death_screen:** larger centered actual DAY N; downward settle/fade at confirmed wake; retain curved eyelid blinks and reduced-effects fade. Owns `UI/Runtime/SurvivalChapterPresenter.cs`, `SurvivalRestEyes.cs`, new `SurvivalWakeTitle.cs`, `UI/Styles/SurvivalChapter.uss`. Expose audio transition event. No body animation or artificial calendar advance.
- [ ] **Root:** new `Composition/Editor/I06PolyHavenSceneAuthoring.cs`; place props on actual surfaces, keep routes/interactions clear; combine ground finish, verify materials/scale, capture native screenshots and promote reviewed changes to original. Add wake audio through root-owned presentation wiring, not domain state.
- [ ] **Root:** show real closing-eyelid, asleep, paused and awake frames. Previous 69-check verification covers earlier rest implementation, not REST-03.
- [ ] **Root:** evaluate requested Grass Generic Small (Blendkit ID178a5199-4133-4670-b78d-bbfabec75bd0): free, Royalty Free commercial game use, listing19MiB/1615polygons. Appropriate sparse Earth launch-site perimeter grass; download/export/access and final size still unverified.
- [x] **Root:** HyperFrames trailer follow-up created as GitHub issue #10. Later trailer work; no installation or production trailer yet.

## Claude — queued; online acknowledgement still required

Do not describe these as running without a fresh acknowledgement in issue #7. Last measured quota was94%used/6%remaining at10:24CET; stale, not live status.

- [ ] **Priority1: movement:** reliable wall/ceiling push, repeated Space, understandable A/D steering, faster usable EVA, no ship clipping. One lead movement writer only; existing exact extra paths in ARCHITECTURE.md remain reserved.
- [ ] **Priority1: body/contact:** remove distorted belly/chest first-person view; convincing handholds/push/rope hand-over-hand animation; physical textured rope. Preserve canonical mechanics.
- [ ] **Priority2: characters:** use approved male cabin and EVA references in `docs/art-direction/astronaut-reference-v01`; rejected generated board characters must not be used.
- [ ] **Priority2: seated/rest embodiment:** entering restraint, fastening and releasing seatbelt, visible rest/wake body. Coordinate root's `CanPresentBody` gate and wake presentation event before touching root composition.
- [ ] **Later: spacecraft detail:** extend delivered DetailPass02 with purposeful restrained personality. Split asset work from movement into disjoint paths, use a cheaper suitable model for bounded mesh/material tasks and stronger review for movement. No duplicate Poly Haven/ground workers.
- [ ] Report online/accepting tasks, actual current quota and realistic milestone ETA. User14:00target is not a verified completion promise.

## Verification and preservation

- Source approval: both outcome cards and current clean Sources screen accepted; no orbit/planet visualization requested anymore.
- Music A/C approved, B rejected. Preserve Cobalt/Titan integration and original dirty voice-cues file.
- Scene only root writes; do not commit settings, temporary QA fixtures or unrelated avatar experiments.
- Every asset record: source, free price, commercial-compatible license, download bytes, prepared bytes, reductions, scene purpose. No claims of AAA quality or gameplay verification solely from an imported asset.
- Latest user instruction: prioritize actual playable scene integration over additional catalogs or duplicated sessions.
