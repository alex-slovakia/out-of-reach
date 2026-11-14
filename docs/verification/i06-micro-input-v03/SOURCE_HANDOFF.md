# MICRO-INPUT-ROUTING-V03 — same-wall WORK palm placement

Released staged candidate. **Default remains off pending actual-device native acceptance and saved-Bootstrap route proof.** No live changes, Git mutation or Unity launch by this task.

## Exact cause and correction

Native5 result `20261114T215207836Z` proves a real wrist-limit refusal, not an input failure: eye(0,1.66999984,−.03) means Player/body feet(0,.01999984,−.03). The actual aimed wall point(0,1.50499988,.30) is therefore body-relative(0,1.485,.33), while the earlier centered Blender probe used(0,1.50,.30). The capsule's real wall separation adds30mm of forward reach and changes the aim intersection height15mm. Re-running the exact native-relative geometry reproduced wrist80.214° atblend.97 and84.688° at endpoint. Native reported80.942° atblend.97532. No limit should be increased.

The native calibration uses7322 imported material skin vertices, support1673 and100 patch vertices; Blender's mesh uses6855 material vertices, support7712 and83 patch vertices. **Do not equate those indices/counts.** Native local support point(−.016804539,.046692055,−.012352623) agrees with the source point after the expected local-X reflection to below0.3µm. Splitting/reindexing at import is consistent with the count difference, but a complete vertex map was not fabricated. Runtime continues to measure its complete actual material set. The new native callback assertion independently bakes and checks every imported material skin vertex and records count, micrometre-deduplicated position count, support and patch counts.

Root approved natural hand-aware placement: the crosshair identifies the surface, while a WORK palm chooses a point6cm toward that hand in the aimed surface plane. This candidate changes **only** bridge target selection for WORK Push. It projects unbobbed eye.right onto the actual hit plane, selects±.06m, and casts again from that same authoritative eye. The new hit must be within the original reach, on the same actual collider, have normal dot>.999 with the original hit and be within the existing4mm point tolerance of the proposed point. If the patch is missing/occluded/off-edge, it refuses. No second-object search, center fallback or force fallback. Gloves and Grip are unchanged.

The original production BareArmContactPresenter remains **byte-identical**, SHA256 `bee19b82c549527345a4d7443ba8a8a4bd4c68e7f65c863467c0b582f1bf2d94`. No anatomy, weights, wrist80°, forearm75°, elbow25–175°, arm lengths, skin tolerance0.1mm, reach or capsule changes.

## Import

Use `import-manifest.json`: V03 bridge, exact already-reviewed V02 router and V03 temporary native runner. Preserve all existing metas. The bridge is separately patchable from either original V01 (`bridge-v01-to-v03.patch`) or released V02 (`bridge-v02-to-v03.patch`); baseline hashes are explicit in manifest.json. V03 includes V02's directional push API and actual-inside-Eva guards. Do not also apply the V02 bridge over V03 afterward.

Restore/remove the temporary DiagnosticBareArm candidate before acceptance; production bare owner must retain the original hash above. V03 native runner does not require the diagnostic flag.

## Actual evidence

- Source compile PASS: full current Composition with V03 bridge + exact V02 router, then the additive native runner, against installed Unity6000.3.28f1 and actual domain assemblies. No Unity launched.
- Frozen original Blender source, exact native-relative target with selected palm atx±.06,y1.485,z.33: **178 approach/return/mid-cancel samples per arm**, all original geometry gates pass. Max wrist75.0012°, correction≤24.0012mm; worst intermediate plane clearance−0.07944mm stays within the unchanged0.1mm tolerance. Unchanged lengths/scales, endpoint residual<0.3µm. See Geometry/hand-aware-contact-proof.json. These are Blender mesh checks, not claims about all7322 imported vertices.
- Prior V02 direction math remains700cases PASS. The first-portal38mm analytical clearance remains provisional, not full-route/native acceptance.
- Native V03 is compiled but **not run**. It preserves all existing input/lease checks and V02 directional/EVA/foot/portal assertions; adds actual L/R shifted-wall-point proof, independent native skin measurement at the physical callback, and L/R edge rejection on a real8cm-wide panel whose central crosshair still hits. Original center refusal is retained separately as native5 evidence and exact-source reproduction, not relabeled a success.

Complete-ship physical route, stopping/recovery around the console and low obstacles, actual rail registration, EVA route transfer/reentry and visible animation quality remain root acceptance work. No claim of safe default floating yet.
