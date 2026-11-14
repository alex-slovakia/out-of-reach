# Page 2 — Schedule and next playable Unity update
Planning estimate made around **16:40 CET, 14 November 2026**. These are ranges, not verified completion times. Additional design changes or integration failures move them; report the cause rather than silently missing an ETA.

| Delivery | Estimate from this checkpoint | What Alex can expect |
|---|---|---|
| Next playable feedback build | **90–150 minutes**, approximately **18:10–19:10 CET today** | Sprint/exertion, forward/preflight bob, walkable first disturbance, cargo-label cleanup/guidance, pause-hand/readability fixes and menu feedback after verification. No promise of all new art, dock or final survival ending in this build. |
| Individual Blender art previews | **1–3 hours per bounded asset task**, in parallel | Dock, body/outfit prototype and repair kit rendered from actual Blender geometry. Quality may require another pass; a render is not Unity integration. |
| Coastal prologue + boarded departure | **3–5 active hours** including root scene/cinematic integration | Separate loading environment, truthful loading deadline, boarding/launch/orbital-transfer montage and carry choices preserved. Reuses one small playable area. |
| Survival, physical stations and real finale | **4–8 additional active hours** after first domain/UI binding | Food/water/rack, sleep/day flow, terminal/radio, scenario rescue timing and actual ending/record. Scope may be reduced if blockers emerge. |
| Combined one-mission release candidate | **Tomorrow, 15 November, afternoon/evening is the working target**, subject to the gates below | The complete chosen one-mission flow, not a promise of every animation reaching the image's fidelity. Keep at least the final two hours before midnight for packaging/testing. |
| Further model/animation polish | Usually **several more passes** | Exact 1:1 image quality across every gameplay angle is not established by any estimate above. |

Ranges overlap because art/domain work runs in parallel; do not sum them mechanically. Root integration and Unity verification remain serial bottlenecks. Full studio-scale AAA production is not a credible overnight promise; the aim is a coherent, polished, bounded slice.

## Next Unity promotion gates
1. Preserve Alex's running playtest until explicitly finished.
2. Compile/import exact candidate in the isolated checkout with Unity 6000.3.28f1.
3. Test sprint/carry/route/pause/restart and the modified opening with actual scene bindings; inspect readable prompts/screens and hands.
4. Record exact SHA, changed paths, tests and known gaps; build where the checkpoint requires it.
5. Update the original project safely after playtest release, open Bootstrap, and tell Alex exactly which features are included.
6. Publish the same candidate for Apoliak to fetch; his local open/build ACK is required before claiming both computers match.

## Who is doing what
- **Codex root:** integration, existing feedback fixes, UI/audio, one camera/input authority, Unity tests/build and GitHub handoffs.
- **Codex dock agent:** original Blender coastal loading deck/access/rack, separate assets.
- **Codex body agent:** first-person torso/legs and workwear/EVA variants, compatible with existing hands; actual render review first.
- **Codex repair-prop agent:** ratchet, locking hook, braided tether, grip/contact anchors.
- **Claude / Opus 5.5, requested not yet acknowledged:** redesign the physical terminal hardware against the approved earlier reference, retaining its anchors/size. A radio-desk art pass can follow after explicit ownership ACK; no concurrent Codex writer there.
- Sprint/domain agents have already handed over bounded tested source; they are not idle blockers to root integration.

No fresh model quota figures are available. Do not present old percentages as current.

