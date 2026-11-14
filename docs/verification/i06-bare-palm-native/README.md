# I06 actual bare-palm native checkpoint

14 November 2026, Unity **6000.3.28f1**, isolated integration checkout. Native
`BarePalmPlaneQa.Run` exited **0** with **184 checks** and **443 actual rendered
frames** independently sampled. Original editor/project were not modified.

This replaces the old triangle-centroid contact test with actual skin support.
Both hands approach a real wall, make contact, issue one delayed real Player push,
return, cancel midway, pause, reject occlusion/removal, and handle an actual
slow frame and a real surface jump. The original V04 relaxed pose and rig lengths
remain unchanged. Only bounded finger/arm rotations and a measured wrist path
are borrowed during the action and restored afterward.

Measured worst whole-skin clearance was **-0.02599 mm** (native numerical error,
within the declared0.1 mm tolerance); maximum bounded wrist-path correction was
**34.1323 mm**. Source hashes, independent per-render skin samples, runtime checks
and genuine GPU screenshots are retained alongside this note. The exact temporary
native runner is retained as text, not shipped as a player asset.

## Actual scope and remaining work

- The screenshots show the real imported WORK hand touching a plain test wall.
  They are mechanism evidence, not the finished ship's artistic presentation.
- Actual joint/local scale preservation and restored V04 baseline passed.
- This does **not** certify arbitrary concave collisions, whole upper-arm collision
  avoidance, production input routing, registered ship handholds, or a complete
  microgravity gameplay loop. Those require the separate composition bridge pass.
- The longstanding UnityEditor.Search indexing exception is retained in
  `editor-environment-errors.txt`; no claim of a completely clean Editor console.
- Body technical import passed separately at26d0ee0. Look-down framing remains
  visually imperfect and is not promoted to an artistic acceptance by these checks.

Integration anchors: actual `BareArmContactPresenter` and `BarePalmCalibration`,
V04 floating clip with `preserveHierarchy=true`, zero floating body offset, real
Player and authoritative session. No new packages, inventory or clock.
