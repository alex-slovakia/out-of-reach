# Native6 bounded acceptance and visual review

**Accept the antenna sleeve/contact repair checkpoint for this exact saved scene.** Root's native run `.local/antenna-sleeve-continuity-qa/20261115T002610781Z` passed190 assertions. I inspected all seven genuine timed GPU frames, normal work, paused contact and repaired-state images. No new blocking sleeve discontinuity, floating rear cap, visible hand/tool separation or obvious mesh clipping appears in them. The wrench approach, turn and withdrawal remain readable; the gloved hand enters deep shadow in some poses, but the tool silhouette remains clear. No production change is requested.

The old black rear-cap disk is absent throughout the seven actual captures (actual times0.013–2.959s). The sleeve continues through the lower view edge. This is supported by physical geometry proof rather than framing alone: all91 exact source poses retain the fixed350/320mm segments, original16 rigid hand meshes, actual torso/sleeve surface intersection, empty full-scene padded-arm blockers, and at least11.436mm of actual cap enclosure plus mesh-ray coverage. Maximum exact-source contact error is0.000337mm; the continuously sampled real work maximum is0.000983mm, against the unchanged2mm limit. Maximum real runtime shape drift is0.002351mm, against0.020mm.

The paused screenshot retains the wrench and glove at the worksite behind the blur. Assertions verify sleeve/wrist/tool freeze **from before pause**, including the first paused frame, unchanged work/simulation clocks and resources, hidden progress row, and retained movement ownership on resume. The restored progress row remains unique. Look-away, focus/source loss, final-frame owner revocation and restart cancel without charges or stale leases.

The completed three-second attempt issues exactly one actual repair command. All five resource ledger deltas equal the current authoritative repair costs; repeated E spends nothing. The repaired connector visibly replaces the faulty one, and tool/progress/movement leases clear. Screenshot03 confirms that final state.

Acceptance is limited to the native tested scene/source hashes,1280×720 view, authored x3.75 route endpoint and current body fit. This is an automated actual W/E route/work test following disclosed direct-domain mission setup and cinematic skips, not an end-to-end manual mission. Resource driving is disabled during transaction isolation and enabled for the paused-clock test. The real work sampled78 of91 authored bins; the separate synchronous91-pose sweep provides complete source-pose coverage. Timed camera PNGs precede UI Toolkit overlay rendering; regular01/02/03 include actual UI. The known Unity Editor Search indexing exception is separately recorded, not a hidden gameplay exception.

Exact native pins: Bootstrap`664c5794`; Hands`a343cc2d`; sleeve`0d0590d6`; authoring`f8a928b1`; QA`dfe1fa72`. Full hashes are in the run's `source-sha256.txt`.

## Archive with the checkpoint

Keep the complete seven `work-00...work-06...png` files with `timed-captures.txt`; representative before/middle/withdrawal images are `work-00-requested-0.00s.png`, `work-03-requested-1.50s.png`, and `work-06-requested-2.95s.png`.

Also keep:

- `01-actual-eva-tool-contact.png`, `02-actual-paused-tool-contact.png`, `03-actual-eva-repaired-antenna.png`.
- `PASS.txt`, `result.json`, `checks.json`, `checks.txt`, `scope.txt`, `source-sha256.txt`, `capture-manifest.txt`.
- `sleeve-continuity.json`, `actual-work-shape-samples.json`, `actual-tool-acquisition.txt` and `editor-environment-errors.txt`.
- Root's `TestResults/i06-antenna-continuity-native-6.log` and exact committed scene/asset references at this checkpoint; source-code hashes alone must not substitute for saved body clip/offset identity.

This review made no live source edits and launched no Unity instance.
