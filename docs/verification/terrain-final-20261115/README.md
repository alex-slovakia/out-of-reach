# Terrain final — 15 November

Native Unity 6000.3.28f1 GPU run of saved Bootstrap, 13:01:53 UTC. Evidence is this workspace state (source hashes included), not a claim that unrelated dirty movement or the complete game is verified.

Added photographed dry grass, shrubs and 10–40 cm scanned stones; varied repeated photographic soil sampling; warm outdoor light and CC0 sky. 963 grass tufts, 45 shrubs, 291 stones; 2,297,479 aggregate LOD0 / 426,700 LOD1 triangles. Maximum simultaneous frame cost has not been benchmarked. No plants in protected footprints; no gameplay collider changes. Existing buildings, ship and upper platform preserved. Small stones have no gameplay collision.

Inspected actual D/E rendered screenshots. The cameras were positioned by the QA harness; these are not a manual walking playthrough. Saved-scene Planning, real transport skip/transfer, and repeated environment restoration passed. Shader/import checks produced no reported error. A previous isolated diagnostic crashed during D3D12 editor startup before test code; final run used D3D11 with one job worker and completed normally. No original user editor was stopped.

Generated vegetation meshes explicitly refresh GPU vertex buffers while preserving existing GUIDs. Source licenses/provenance are in docs/art-direction/launch-landscape-v03 and launch-sky-v03. Terrain is ready for user visual review; no AAA-quality or complete-game claim.