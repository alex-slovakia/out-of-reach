# RecoveryNativeQa3 — visual review

**Visual checkpoint accepted at the captured revision; no must-fix art defect found in these eight stills.** This is approval of the actual 1280×720 framing/readability shown here, not a claim of photorealism, trajectory realism, full input acceptance or complete temporal/audio QA. No production edits or Unity launch were made for this review.

Reviewed UTC: 2026-11-14T22:56:34.156765+00:00. Exact run: `.local/i01-integration/.local/recovery-native-qa/20261114T224725353Z`. Root run result: PASS, exit 0, 5,932 assertions; that count is not a bug count. `shader-messages.txt` is empty and checks report zero ShaderUtil errors.

## All eight captures

- **01-normal-recovery-approach.png** — ACCEPT. AURORA and the detached recovery capsule are recognisable, wholly framed and clearly separated. Neutral exterior light preserves panel detail; dark solar panels remain discernible. Caption states approach and preparation, not completed docking. No HUD/body/hands intrusion.
- **02-paused-recovery-approach.png** — ACCEPT. Same world framing is held behind a legible pause menu. The paused shot is not replaced by a cockpit or neutral camera. The generic movement help remains a minor menu convention, not evidence that controls are active.
- **03-normal-later-mars.png** — ACCEPT. Historical surface features now read clearly on a spherical planet, with a clean limb and restrained terminator. No previous blurry procedural appearance, cyan plane, visible texture seam or body/HUD leakage. Foreground capsule is intact and readable; LATER · MARS TRANSFER makes the ellipse explicit.
- **04-actual-recovered-report.png** — ACCEPT. You made it / CREW RECOVERED, the explanation and all three actions are readable over the darkened cockpit. Next destination: Mars avoids claiming an instantaneous completed landing. Elapsed simulation time is legible but subordinate.
- **05-reduced-recovery-approach.png** — ACCEPT. Static/reduced framing still includes both vessels and the same readable recovery caption; silhouette, separation and information survive the reduced-effects option.
- **06-reduced-later-mars.png** — ACCEPT. Mapped Mars and the full capsule remain framed in the reduced shot, with clear LATER caption. No new clipping or overlay obstruction compared with the normal shot.
- **07-actual-failure-report.png** — ACCEPT. Out of time / FLIGHT ENDED is unambiguous. The actual carbon-dioxide cause is readable; the restrained red cabin state supports loss without washing out text. No success/Mars imagery remains.
- **08-actual-mars-source-credit.png** — ACCEPT. Credit title, exact NASA/Jet Propulsion Laboratory & Caltech attribution, Viking/USGS history, authored-lighting/time-jump disclosure and announced browser action all fit and are readable. This is a scrolled Sources view; Back and the scrollbar are visible. No clipped credit or duplicated section is visible.

## Geometry, lighting and chronology

No detached hull sections, visible through-wall labels, giant cyan surfaces or accidental arm/body foreground were observed. The capsule and AURORA remain recognisably modest existing game assets; hard panel edges and the sparsely dressed black space are acceptable checkpoint limits. The Mars map is materially more readable than the previous procedural sphere. Its baked historical image detail is not elevation data.

The sequence log records real `RecoveryArrival` followed by `MarsDeparture`; screenshots caption the latter **LATER · MARS TRANSFER**. The actual simulation timestamp remains the same across those authored visual chapters, so the scene is a disclosed cinematic ellipse, not computed interplanetary travel. The end card correctly describes recovery and a next destination. No docking, transfer animation or Mars landing is proved by these frames.

Capture metadata retains the last received-reply subtitle string, but `audioCue` is empty and the visible labels/actual PNGs contain only the chapter caption. It is not a visible or established audible contradictory rescue line. `received-dialogue.txt` and scope record the real earlier reply finishing before accelerated survival time; this review did not audition audio.

## Evidence inventory and freeze limits

All eight expected PNGs and matching JSON sidecars exist. Present alongside them: exact pre-run asset hashes, per-check log, result/PASS, sequence log, paid time-input log, received-dialogue log and shader log. Critical saved Bootstrap, cinematic presenter/provider, Mars shader/texture, authorer, report presenter and temporary runner hashes still matched this run when read for this review; future changes must not inherit this approval without an appropriate check.

Missing from this run, so do not claim them as tested:

- A continuous video/audio recording across approach → fade → LATER → report. The snapshots cannot establish transition smoothness, flicker freedom, delivery pacing or sound quality. Lifecycle timing is separately checked by the native probe.
- A physical pointer/keyboard acceptance run. The fixture used public APIs and batch-focus delivery, as disclosed in `scope.txt`.
- An actual external-browser navigation from the NASA link: the check explicitly says **no browser launched by this probe**. The button and exact attribution are visually verified; explicit user activation is still a separate final check.
- Narrow-window, ultrawide or DPI-scaled captures. Only 1280×720 is established here.
- Screenshot proof of restored gameplay after skip/restart. Restoration is assertion/log evidence in this run, not an additional captured view.

The recorded editor-environment error is an `ArgumentOutOfRangeException` from `UnityEditor.Search.SearchDatabase` during editor startup. It is logged separately; the native recovery probe completed and the actual images show no associated rendering failure. Do not describe the entire editor log as error-free.

## Optional deferred work

No art redesign is recommended before the current route/cinematic/land checkpoint. If later polish is scheduled, review the full moving sequence and capsule orientation/transfer storytelling in motion before adding detail. The hard low-poly panel styling and plain Sources scrollbar are optional polish, not blockers. Keep the real-map attribution and LATER disclosure.

## Exact image hashes

| File | SHA-256 |
|---|---|
| 01-normal-recovery-approach.png | `935d3a06b4496389ef2d187211994f2e246cfcbb7befc65db59a2f68240eb2cb` |
| 02-paused-recovery-approach.png | `38dc75b8afd785099fa689efcb4e641e9edbccef841c33c3ef38ae802f0d3392` |
| 03-normal-later-mars.png | `90465038318bbb48187ffe9c4d789517d171e8657a719dc3ebfb81086751c07b` |
| 04-actual-recovered-report.png | `89f4b83cc9c1b739cd3f46fbbb7bfe3ec7cffc32b908357165a4df5d6409e453` |
| 05-reduced-recovery-approach.png | `4d7b7537dc9a9235d372a1f7dafd4488a8645b44afe953a8ff95e8a33bf93cf4` |
| 06-reduced-later-mars.png | `2d4e34e8461c21fd10dfccc3bfd4221e92f3c239080ad87c452b6479c2f20717` |
| 07-actual-failure-report.png | `4238113f407e37171908dbef3d7e5df82212ca93537f8d748e7c3ee3038d07c7` |
| 08-actual-mars-source-credit.png | `4011fa9c75f4c58df8a772787e913e64f2d690276c3d04ba26630e074b9b903e` |
