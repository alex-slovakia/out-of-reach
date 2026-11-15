# Fresh lead handoff — 15 November 2026

## Mandate

Alex explicitly requested a fresh lead chat to avoid the very long conversation. **Target today 19:00 CET; HARD hackathon deadline today 23:59 CET.** Deliver the best feasible complete playable mission within that time. The earlier 3–5 day estimate was rejected and is superseded. Do not promise all AAA art ambitions fit today. Preserve the approved 13-stage story in `../03-walkthrough.md`, prioritize actual usability and end-to-end completion, then finish high-impact presentation. Ask only necessary questions. Use compact tools, disjoint delegated tasks and lighter GPT-6.1-sol for routine work. User explicitly authorizes a goal in the new chat.

## One integrator and safe working location

Codex is designated integrator; read AGENTS.md and the six named project docs. Original project is branch `feature/visuals-cinematics`, HEAD `710a99b`, Unity 6000.3.28f1 currently opened for Alex's playtest. **Do not modify or restart the original during that playtest.** Its sole intentional dirty file `Assets/_OutOfReach/Audio/Production/voice-cues.en.json` must remain byte-for-byte; backup exists under `.local/original-promotion-20261115-0548/`. That script edit is not new runtime audio.

Work in **`.local/i01-integration`**, branch `codex/i01-integration`. Parent HEAD before this docs checkpoint `c5296c9`, prior gameplay `3cb145a` and `710a99b`. New lead becomes sole lead writer after handoff; old lead stops all feature work. Coordinate exact shared changes through private GitHub issue7. GitHub connector previously returned404; authenticated `gh` works. No Claude UI/session control, no merge to main or NASA/public release without separate authorization. Private development pushes are authorized.

## Actual current state

Full catalog `01-feedback.md`:24 bugs +32 additions, NOT completed. Audit `docs/verification/i06-status-audit-0140.md` covers every row but is historical. Last strict original acceptance4/24 +3/32; additional individual checkpoints exist, not a fresh combined accepted total. Do not confuse test assertions, implemented source or generated art with delivered features. Complete morning feedback and eight screenshots are in this folder's README.

The original now has physical cargo, timed/practice start, basic departure, body and microgravity, repairs/EVA, continued survival/finale candidates. User rejects current terrain/launch/harness, missing departure VO, too-loud music, poor walking/float-body presentation and ceiling trapping. User corrected that menu sounds DO work and initial apparent input lock was unknown microgravity controls. The complete mission has NOT passed its latest uninterrupted test.

## Ready staged packages — integrate, don't regenerate

Paths below are relative to the original project root. Each package is isolated and existing `.meta` GUIDs must be preserved.

1. `.local/verification/GroundBodyV01/HANDOFF.md` +`staged-manifest.json`: displacement-driven walk/sprint on existing WORK/EVA rigs. EmbodiedBodyPresenter only, compile PASS; Blender480 poses PASS. Root must add `ConfigureGroundLocomotion(controller, CanPresentGroundedStepAudio, sprintPredicate)` and block orbital steps/gait via `!orbitalLocationReached`. Standing Z setback−.285m measured; current−.197. **Do not alter FloatV04 frame**: simple shin-forward fix failed geometry and contact alignment. Native test pending.
2. `.local/verification/DeparturePolishV02/HANDOFF.md` +`manifest.json`:10 files, compiled Cinematics/Marine; stronger plume/deck dust/shake,34s optional orbit sequence. Root binds ConfigureOrbitalViews, dust plane, transit sky/cover, actual docking receiver/camera anchors and visible seated/harness body. No native run yet. Sequence0boarding/3hatch/6arm/9interior ignition/9.75external pad/10.5ascent/13.25tracking/17.5atmosphere/22orbit/27.5docking/29.15capture/30boarding/33.5covered handoff/34end. Stop cabin sounds on first exterior9.75; don't play exterior vacuum engine audio.
3. `.local/verification/DepartureAudioV02/Assets/`: MissionAudioPresenter candidate and4 original WAVs/metas/provenance. Separate pause-aware ducked nonloop departure score; backgroundScoreTrim.44, departureMusicTrim.65. API TryPlayDepartureCabinCue, StopDepartureCabinAudio, PlayDepartureMusic(AudioClip,startSeconds), StopDepartureMusic. Bind clips and stop on all terminal/skip/cancel paths (typed DepartureEnded required). No root import/native listening yet. Earlier agent handoff is in local staged messages; inspect exact source, don't assume missing HANDOFF.md exists.
4. `.local/verification/MicroHumanPlayabilityV01/Ceiling01/import-manifest.json`: new bounded same-ceiling palm correction, I06MicrogravityBridge only. Source/compile checks passed; native pending. Short HANDOFF.md is being finalized by old child; it has been told to stop after writing fixture/handoff, no native imports. Current fixture lease only within this folder. New lead must confirm final completion before editing that folder.
5. Claude engineering art delivered in remote branch `feature/ship-detail-pass-02`, SHA `38cac1f591c06f938616e5a52628144fa55f76d0`. Only `Visuals/Models/Aurora7/DetailPass02/**`+folder meta. Author import/roundtrip passed; our material mappings and scene placement not verified. Issue7 comment6889596174. Habitat then cockpit in progress; ask fresh ETA, enforce incremental pushes/RESUME and WIP before quota stop.

## Latest actual blocker and needed coordination

Full saved-scene native run3 **FAILED** at05:11UTC,1118 checks: `.local/i01-integration/.local/full-mission-continuity-v01/20261115T050429450Z/result.json`. PID56736 is no longer running. No new Unity test should start without checking current processes.

Near ceiling feet(1.25,1.29,4.01), eye(1.2546,2.94,4.0654), target(1.2145,3.12,4.3599), downward normal. WORK elbow reach d.5678m versus total arm.4835m; refused, no route progress18s. Ceiling01 corrects same-plane contact by bounded25cm maximum, not reach inflation/teleport. Root should native-test this exact repro before another full1260s run. Earlier continuous EVA→return run passed but is not full survival success.

**Two-hand push remains open:** existing Player.TryPushOff stamps cooldown, so calling twice is wrong. Coordinate with Claude an additive atomic TryPushOffPair that validates both contacts before any mutation, sums capped contributions, respects MaxPushSpeed/MaxSpeed, releases hold/stamps cooldown/emits event once. Presentation must wait for BOTH visible contact callbacks. Existing hand presenters have one active hand. Suited AstronautHandsPresenter also rejects entire Eva phase even after inside return; narrow contact-only scope must be coordinated. Do not copy gameplay authority into presentation. Plain L/R must produce corresponding push; explicitly document any grip modifier rather than silently making default click hold. User wants both-buttons stronger push and reliable floor/wall/ceiling recovery.

## Dirty isolated work and exclusions

Intentional candidate WIP: I01MissionComposition + FlightBoardView + MissionFlightBoard (correct repair objective); I06MarineBindings (LandLightV01); BareArmContactPresenter/I06MicrogravityBridge/I06MicrogravityInputBindings and new BareRailGripLease/Pose/editor/assets (WorkBareGripV01 followup04). Saved Bootstrap workGripPose stillOFF/unbound. Native grip562 scoped checks passed in unsaved fixture; not in current playable scene. LandLightV01 native PASS5 images; candidate brighter/hazy but does NOT repair poor terrain geometry.

Never sweep-stage generated changes: Assets/Settings/**, ProjectSettings/**, Assets/Editor/** temporaryQA, StandingIdleV01 rejected assets, docs/art-direction/review-03. `docs/verification/i06-full-route-native-v02` untracked evidence needs review. Preserve all dirty work; no reset/cleanup.

## Audio/keys/licensing

Approved GAME male voice is **Orus**, unrelated Retell Charon rule does not apply. Existing private helper `.local/generate_game_voice.py` and `.local/generate_voice_batch.py` use approved credentials privately. Never print/copy credentials to docs/GitHub, never change original voice file to generate staged cues. Stop on429; no bypass rotation. Need spoken Secure for departure / Sealing cabin / Hatch secured / Ignition / Liftoff and concise docking cues. Review audibility, not only PCM/checksums.

Official licensed dramatic candidate: Scott Buckley **Titan**, https://www.scottbuckley.com.au/library/titan/ and licensing page `/library/licensing/`, CC-BY4.0 commercial permitted with attribution. MP3 `/library/wp-content/uploads/2018/06/sb_titan.mp3`. Researched only, not downloaded/listened/bound. Use bounded download, preserve license/credit and duck under VO. Music background must be quieter, user explicitly reported too loud.

## Verification and next steps

`.local/run-i06-native.ps1 -Method Class.Run -Name unique-name` guards isolated Unity and writes logs. `.local/import-i06-stage.py` validates manifests; never overwrite unexpected hashes. `.local/run-i06-candidate-checks.ps1` prepared for Edit/Play+build after the bounded feature checks. Original repoGUID/NASAoffline checks passed before new work, not proof of future revision. Test only changed risks, inspect actual rendered frames/audio, then one full mission and build. Promote a verified exact revision into original only after playtest ends; protect the original dirty voice script.

Immediate sequence: coordinate Claude pair API + terrain/ship increments; reproduce/apply/test Ceiling01 and onboarding; integrate grounded gait; integrate departure/audio/harness and actual orbital transition; verify survival→rescue/Mars/failure; use remaining time for shadows and art. Freeze content early enough for19:00 full build, keep23:59 hard limit. Communicate missed estimates immediately. Fresh lead may use narrowly scoped sol agents; do not create duplicate lead writers or import new packages.
