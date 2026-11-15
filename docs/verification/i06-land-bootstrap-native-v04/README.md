# Land Bootstrap native4 — scoped acceptance archive

Run `20261115T003408587Z`, Unity6000.3.28f1: **225 assertions PASS, native process exit0**,13 genuine1280×720 PNGs. All13 images were reviewed. Functional loading/departure/arrival/restart is accepted for this checkpoint. **Landscape/rendering is visibly below the intended visual quality and is not final art or AAA acceptance.** No whole-game or human playthrough claim.

`run/` contains all44 original evidence files, copied byte-for-byte; `manifest.json` records each SHA-256, size and original path, plus native source pins. The private staging archive preserves the full native log. This repository archive includes the exact outcome and exit-code receipt, omitting unrelated Editor licensing/startup noise. [The separate full-row audit](../i06-status-audit-0140.md) is copied exactly; it is not rewritten by this archive.

## What this run actually proves

The saved Bootstrap was opened without authoring/scene saves. The actual Runtime.Update advanced the real60-second timed deadline and22-second departure. Actual UI, E/W/Escape and a continuous loaded route crossed dock, arm and hatch, then W approached the seat. Setup teleports were limited to bay/source cargo probes. A normal batch focus callback was disclosed. Arrival used actual W/Space foot-contact push and mouse/E, with no post-arrival pose writes or direct impulse. Pause held camera/transport/hatch/arm/world shot and playhead; transfer restored the recorded Aurora environment/camera/console; restart returned to actual Practice land state. One inventory and the546kg default were retained.

GPU captures include actual camera and UIDocuments. The screen-space-overlay fade is excluded from camera PNGs; its actual alpha was separately recorded/tested. A static frame does not establish motion quality, audio audibility, orbital realism or every collision path. No new run was performed for this review. Known Editor Search startup exception is preserved in `editor-environment-errors.txt` and the full log.

### Geometry checks: keep the counts distinct

- **11 imported land-kit modules:** `land-mesh-inventory.txt` and checks explicitly preserve the exact imported FBX/sharedMesh identities; no renamed substitute. Five structural MeshColliders and pad bounds/orientation are checked.
- **4 transient steam lobes:** Intro's15 MeshFilters comprise those11 imported modules plus exactly4 original owned lobes under the stationary deflector frame. Shared mesh metadata is153 vertices/672 triangle indices =224 triangles, correct shader/material and DontSave flags. No forbidden CPU readback was used. This proves ownership/geometry, not that the plume is visually convincing.
- **7 surroundings groups:** run asset hashes include the surroundings FBX and authorer. Source `I06LandSurroundingsAuthoring.Validate` requires7 mesh groups,7 anchors and17,032 triangles. **Native4's ValidateLand does not separately enumerate the seven surroundings groups.** Its explicit extra environment assertion covers the persisted passive horizon at ground datum without a support collider. Do not describe source validation as another completed native group-count assertion.
- All pre-existing Assets hashes, including Bootstrap, were checked unchanged at the start/end. Complete snapshot is `assets-before.json`; exact pin subset is in the manifest.

## All13 capture decisions

| Capture | Accepted for this scoped review | Specific limitation |
|---|---|---|
| 01-actual-dock | Objective/countdown and boarding route legible; carrier doorway identifiable. | Flat dark landscape and hard shadow edges; reused MARINE TRANSFER sign remains on land. Not final environment art. |
| 01a-land-from-deck-left | Cargo rack, rail and supported deck readable. | Small oblique physical labels are not a legibility pass; dark background terrain and sparse objects. |
| 01b-land-from-deck-right | Route marking, rails and service cabinet distinguishable. | Strong dark/jagged rail shadows; brown terrain lacks depth/detail. |
| 01c-land-facility-horizon | Distant facility and crawlerway establish location. | Very simple isolated facility silhouette, flat horizon and conspicuous large polygon patches; below intended landscape quality. |
| 02-actual-carried-cargo | Both hands visibly hold case;30kg, E action and11.2m destination cue readable. | Dark hands/case detail and tiny case labels; not a universal hand/material approval. |
| 03-cabin-before-deadline | Seat, countdown and genuine546/600kg,720/800-credit prompt readable. | Cabin is very dark; visible seat is not proof of harness/donning animation. |
| 04-cabin-launch-loaded-cases | Full-size loaded cases remain visible on racks; aisle and sealing subtitle readable. | Small bay labels and dark case faces; static image does not prove all retention animation. |
| 05-actual-paused-departure | Real cabin backdrop blur, left pause options and controls readable without clipping. | Native transforms establish freeze; one image does not prove transition feel or actual desktop quit. |
| 06-actual-exterior-ascent | Rocket silhouette and pad are clear; correct smaller craft/rocket context. | Broad flat dark terrain, sharp polygon patch boundaries and sparse facility detail dominate; short narrow visible exhaust is not final plume art. |
| 06b-actual-land-exterior-ascent | Second actual ascent view confirms same land setting. | Same landscape limitations; no apparent Earth-orbit docking claim. |
| 07-actual-aurora-return | Actual restored cockpit and objective readable. | **Recorded subtitle overlaps bottom-left Food stores bar.** Current Intro layout fails clearance despite readable text. |
| 07b-actual-arrival-foot-push | Actual cockpit E prompt after real contact push is readable. | **Same subtitle/Food bar overlap.** Does not establish full ship microgravity traversal. |
| 08-restarted-actual-land | Practice/no-deadline reset and dock return visible and coherent. | Same unfinished landscape; no new visual closure inferred from restart. |

The arrival overlap is concrete, not a landscape preference: in both07 views the first subtitle line runs through the Food stores gauge. Survival's separately tested subtitle offset does not cover Intro. Root was notified; a separate narrow subtitle candidate is being staged. The functional225 result remains valid within its declared assertions; it did not assert this Intro clearance.

## Source pins

Recorded from the run's `assets-before.json`, not assumed from current HEAD:

| Source | SHA-256 |
|---|---|
| Bootstrap.unity | `51e10801e89b470b795f14a865b3f5a1e916f895727b7f14eef0d2c72a93eeec` |
| LandBootstrapNativeQa.cs | `3ef211c9369a5c5cbb9c6ffc66618d6664308a62614ff2ff26da830ba5d62746` |
| I06MarineBindings.cs | `57a7e47625374e50f1464340afe0af07962ef4bd03ff49c36ffdd28c408c9bb6` |
| I06LandSiteAuthoring.cs | `57637d16174a7f6dc83136521024b6372dfa8dc9322d148342012e380cdddda0` |
| I06LandSurroundingsAuthoring.cs | `3e494a86bf2714c0c5fce721ce8c8228114ba24f710f7d4474b6473fe6bfd094` |
| LS_LandLaunchSite.fbx | `cabe9bf64c2996935b4944cbfd9bbe2258739f83c63bdf6f5d5e7f000fe12e6c` |
| LSS_LandSurroundings.fbx | `632a5c46f22e972fc41cbbbf1058efe2935e2bed68d076078d2bdbe86ce5fff4` |
| LandHorizon.shader | `0e78978c834674c08ecbe6ed285cade37fa1d136ebbe97e772603d9b1dab366d` |
| LandHorizonSurface.asset | `1f9f8186b4fdf7fc2f3ee6e0d0c7a0b21728473da80f07763b18f91ad9113e24` |

Land art source is pinned by SOURCE.json to `2f8dd22e6b026d7e4504efab4264df3f630c0553`; surroundings authorer pins `d3cf841`. These authoring references are not a claimed whole-runtime commit. Use the complete manifest/snapshot for all15 selected runtime/art pins and the remaining Assets hashes.

No source, scene, shared docs, Git state or Unity process was changed by this archive review.
