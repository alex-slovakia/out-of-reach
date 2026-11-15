# Continuous microgravity route V04 — failed checkpoint

Actual saved Bootstrap, Unity6000.3.28f1, run20261115T004154153Z. **FAIL after40 assertions**, process exit1. The driver used actual keyboard/mouse, actual surface queries and real Player/hand commands. No teleport or force fallback. This run predates the later directed-push mapping correction.

At the first portal it failed18seconds without measured approach progress. Best horizontal distance1.130m, actual feet(2.33,1.01,1.05), velocity(0,-.01,-.09). Geometrically unreachable hands were refused. It does not prove a permanent player softlock, but it fails whole-route acceptance. Earlier successful contact-fixture checks cannot substitute for it.

V04 remembered observed hand-specific geometric refusals to avoid repeating an impossible pose. That changed test selection only. The new test still failed and its record is intentionally retained. Full private diagnostics remain in the named local run; no frame is a generated concept image.
