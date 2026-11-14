# Preflight repair-supply case — staged handoff

Task: **PREFLIGHT-CARGO-01**. Owner: Codex prop subagent. Date: 2026-11-14.
Branch/commit: no Git operation or production edit was made; this is an isolated staged asset, not a committed integration.
All writes are under `.local/verification/PreflightCargoProp/`. Intended destination after integrator review: `Assets/_OutOfReach/Visuals/Props/PreflightCargo/`.

## Delivered

- `Source/PreflightCargo.blend`: editable Blender 3.6.1 source. Live shell-recess Boolean, bevels and weighted normals; separate labeled components, handle and latches. Source Sans font packed into the source for portability.
- `Meshes/PreflightCargo.fbx`: seven mesh objects, four materials, **7,638 triangles**. Fixed geometry consolidated by material. Handle and two latch levers remain separate with mechanical pivots.
- `Meshes/PreflightCargo_Collision.fbx`: separate **12-triangle box**. Prefer a primitive Unity BoxCollider using the measurements below.
- `Previews/PreflightCargo_Hero.png`: genuine 1400 × 1050 Cycles render of this geometry, not generated concept art.
- `Previews/PreflightCargo_Angle_000.png` through `315.png`: eight genuine renders at 45-degree increments.
- `build_preflight_cargo.py`, `verify_fbx.py`, `materials.json`: reproducible authoring, FBX validation and material mapping.
- `Evidence/geometry_report.json`, `Evidence/fbx_roundtrip_report.json`, `Evidence-build.log`, `Evidence-verify.log`: actual measurements and process evidence.
- `Evidence/delivery_sha256.json`: hashes identifying this handoff.

The automatically retained `.blend1` is a local backup, not part of the intended delivery.

## Measured scale and integration

One Blender metre equals one Unity metre. Authoring front is Blender **-Y**; FBX conversion is **Y up**, with the labeled face toward **Unity +Z**. Export scale factor is 1.0; encoded FBX `UnitScaleFactor=100` means one unit is 100 cm. Root `PreflightCargo` is at the floor centre, with all mesh scales positive and equal to one. Do not add a compensating 100× or 0.01× scale.

| Measurement | Metres |
| --- | --- |
| Nominal shell width × depth × height | 0.450 × 0.320 × 0.260 |
| Overall width × depth × height, including latches and raised handle | **0.452600 × 0.354138 × 0.319000** |
| Expected Unity mesh bounds size X × Y × Z | **0.452600 × 0.319000 × 0.354138** |
| Recommended BoxCollider size X × Y × Z | **0.454 × 0.264 × 0.356** |
| Recommended BoxCollider centre X × Y × Z | **0 × 0.132 × 0.005** |

The body collider intentionally excludes the raised carry handle so it does not snag during pickup. The separate proxy FBX is reference geometry, not an automatically configured Unity collider.

Named exported anchors in Unity coordinates, relative to the root:

| Anchor/object | Local position X, Y, Z | Purpose |
| --- | --- | --- |
| `FloorOrigin` | 0, 0, 0 | Floor placement |
| `CarryGrip` | 0, 0.312, 0 | Carry attachment authoring aid |
| `InteractionAnchor` | 0, 0.145, 0.185 | Front-face interaction authoring aid |
| `CarryHandle_Bail_EXPORT` pivot | 0, 0.272, 0 | Separate bail; hinge axis X |
| `Latch_Left_Lever_EXPORT` pivot | -0.145, 0.168, 0.173 | Separate left lever; hinge axis X |
| `Latch_Right_Lever_EXPORT` pivot | 0.145, 0.168, 0.173 | Separate right lever; hinge axis X |

These are visual anchors only. The integrator binds actual references and Claude-owned pickup/put-down behavior. No resource count, weight, availability, timer, interaction state or gameplay authority is encoded. No scripts, camera, light, animation, Rigidbody or audio component is shipped in the FBX. The static lid is not an opening inventory implementation.

## URP material mapping

Map the four named materials to URP/Lit. Colours below are sRGB; smoothness equals 1 minus Blender roughness. The model uses geometry labels and no texture maps. Mesh UVs are present but are simple authoring unwraps, not a unique baked texture atlas.

| Material | Colour | Metallic | Smoothness |
| --- | --- | --- | --- |
| `MAT_Cargo_OffWhite` | `#CCCFC7` | 0.05 | 0.52 |
| `MAT_Cargo_Graphite` | `#262B2F` | 0.05 | 0.42 |
| `MAT_Cargo_Aluminium` | `#A6AEB5` | 0.82 | 0.72 |
| `MAT_Cargo_Amber` | `#CF8E23` | 0.05 | 0.54 |

The studio floor, lights and camera live only in `PREVIEW_ONLY_Studio` inside the source. The editable cutter and collision proxy are also excluded from the visual FBX. Do not place the `.blend` into a runtime model folder merely to use its automatic import; import the selected FBX instead.

## Verification actually run

- Built in an isolated **Blender 3.6.1 background Python** process. Blender connector discovery returned no available Blender MCP tool in this runtime. No MCP use is claimed. No configuration or open Blender/Unity process was changed.
- Actual visual FBX reimport passed triangle-count equality, fewer than 8,000 triangles, seven meshes, four materials, exact source bounds within 0.01 mm, floor origin, positive unit scales, anchors and separate moving parts.
- Encoded FBX axis and unit properties checked; raw exported front anchor is on +Z. Reimported geometry agrees with the Blender source.
- Collision FBX independently reimported: one box, 12 triangles, expected dimensions.
- Inspected the final hero and eight 45-degree silhouette renders. The case has a readable solid silhouette, physically distinct lid/gasket, two front draw latches, rear hinges and separate carry bail. No visible missing geometry or floating assembly was found in those views.

**Not run:** Unity import, URP remapping, Unity compile/build, pickup behavior or in-game playtest. The no-Unity scope left those to the designated integrator. This is a reviewed prop delivery, not a claim of a completed gameplay feature.

## Provenance

All container geometry, arrangement, material values, labels and authoring code were created for OUT OF REACH in this task. No external mesh, game asset, texture, NASA model/logo or AI bitmap was used. The accepted project concept and the delivered Aurora7 storage preview were visual references only; their files were read, never changed or copied into this asset.

Label typeface: **Adobe Source Sans 3 Semibold**, already present in the project and redistributed unchanged under **SIL Open Font License 1.1**. The complete copyright notice and licence are included at `Source/Typography/OFL.txt`; the original font is at `Source/Typography/SourceSans3-Semibold.otf`. Upstream source recorded by the project: https://github.com/adobe-fonts/source-sans/tree/87b37a2daaed80fcb8e8ccb0085c4d72ddade12e . This attribution applies to the font, not the case geometry. The spacecraft and case are fictional, with no NASA certification claim.

## Reproduction

Run the existing Blender executable in background mode with `--factory-startup --python-exit-code 1 --python build_preflight_cargo.py`. It rebuilds only this package directory. Add `-- --export-only` to skip rendering. Then run the same executable with `--background --factory-startup --python-exit-code 1 --python verify_fbx.py`.

Keep `Source/Typography/` beside the scripts when relocating the package. Do not run this builder through an actively edited interactive Blender document; the dedicated background process is intentional.
