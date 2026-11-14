# BODY-FIT-02 — separate body and arms, baked fitting poses

Staged art derived from the reviewed `EmbodiedAvatarV01/ContinuousV1/Source/A7_ContinuousBody.blend`. The input is immutable; its SHA-256 is recorded before/after the build. No game source, Assets, player/controller, camera, glove asset, or composition is changed. No Unity process was launched.

## Usable minimum

| File | Skin renderers | Geometry |
|---|---|---|
| `Exports/A7_BodyFit02_WORK.fbx` | `A7_WORK_TorsoLegs`, `A7_WORK_Arm_L`, `A7_WORK_Arm_R` | 26,065 vertices / 51,650 triangles |
| `Exports/A7_BodyFit02_EVA.fbx` | `A7_EVA_TorsoLegs`, `A7_EVA_Arm_L`, `A7_EVA_Arm_R` | 12,761 vertices / 25,132 triangles |

Each outfit uses the same unchanged 164-bone bind skeleton, including `CharacterRoot` at the feet. WORK's real bare hands are part of its corresponding arm renderer. EVA's arm renderers end at the existing glove interface; **no glove geometry is exported here**. The original glove meshes/hierarchy/Neutral finger rotations remain reference-only in the derived source and full-arm EVA renders.

To retain current camera/cargo hands, show only `TorsoLegs` and disable both arm **renderers**. Keep the skeleton active; do not disable/remove its arm bones or reparent the original glove roots. To show full WORK arms for a static review, enable both arm renderers and hide other hand instances. Full EVA arms require the separate existing gloves to be fitted by root's future hand owner. This package does not perform that integration.

The split follows upper-arm skin influence boundaries. Shared boundary vertex positions, skin weights and original surface normals are retained. The torso includes shallow inward shoulder caps, so hiding the arms does not expose holes. This is a first-person visibility interface: an armless hero render is a closure diagnostic, not a normal third-person appearance. Torso and legs remain one continuous renderer; no waist seam was introduced.

## Three actual baked poses

Both model FBXs contain three one-second Generic animation takes, sampled at 30 fps. The same takes are also supplied as armature-only files in `Animations/`:

| Clip suffix / standalone filename | Meaning | Visual root offset |
|---|---|---|
| `BODY_StandRelaxed` | Reviewed relaxed arms-low standing pose | Source CharacterRoot unchanged; minimum sole approximately −1.51 mm, within the 2 mm fitting tolerance |
| `BODY_SeatedFit` | Existing bent-hip/knee fitting reference, no chair/control contact claim | CharacterRoot includes the original −0.3734443 m drop; do not apply it twice |
| `BODY_FloatNeutral` | Original restrained bent-knee, hands-forward visual floating posture | CharacterRoot includes +0.10 m; boot clearance above review floor is approximately 0.081 m |

The clip interval is frame 1–31 at 30 fps; first and last deformation match. These are held fitting poses, **not transition or locomotion animations**. Use Generic skinning initially. No tested Humanoid Avatar is claimed. Disable Animator root-motion application: these transforms pose the visual skeleton and must not move the real player or authorize floating/sitting. Root chooses the pose from the existing game state. The floating pose is artistic and does not simulate microgravity forces.

`poses.json` also contains all bind matrices and every bone's complete local basis, parent-relative posed matrix and armature-space posed matrix for each pose. Expected character-basis conjugation is provided explicitly, but these are **not raw Unity Transform locals**: the FBX importer may encode bone axes via PreRotation. Prefer the supplied baked FBX clips. Head/tail coordinates alone are insufficient to reconstruct bone roll.

## Source, camera and axis mapping

`Source/A7_BodyFit02.blend` is editable and self-contained with packed textures. `SOURCE_WORK` / `SOURCE_EVA` preserve the prior garment objects hidden for editing; `EXPORT_WORK` / `EXPORT_EVA` contain the three final parts. The source also contains the unchanged glove reference collection and a render-only floor/lights/cameras; export selection excludes them.

Metres, global export scale 1, Y-up / −Z-forward FBX convention, no extra leaf bones, same existing 164 bind bones. Both exports keep unit object scale. Blender character forward is −Y, with anatomical left +X. Expected Unity character coordinates are `(-x, z, -y)`; verify upright +Z facing and left/right once in Unity. Preserve imported bind/axis transforms; use an external placement wrapper rather than zeroing imported armature transforms. Fresh Blender roundtrip checks actual animated surface/bone positions against the source, not just bone names.

New look-down renders retain the earlier exact positions and field of view, with **0.05 m near clip** matching the current game:

- Standing / floating camera: Blender `(0, -0.330, 1.651)`, down 80°, vertical FOV 68°.
- Seated camera: same X/Y, Z reduced by 0.3734443 m, down 65°, vertical FOV 68°.
- If the authoritative Unity player eye is at `(0,1.65,0)` and the imported character faces +Z, an initial render-matching visual feet-root position is approximately `(0,0,-0.330)` relative to that player. This is a presentation fit, not a requested controller change.
- The earlier 0.197 m value means additional anatomical-eye setback; it is not the absolute feet-root offset. Fit visuals to the real camera, keeping movement, interaction-ray origins and collision unchanged.

Body should follow real player feet position/yaw, not camera pitch or camera bob. Current pose dimensions can exceed the collision capsule visually; no new colliders are supplied or warranted by the asset. Test wall/seat/cargo proximity in the real scene before promoting it.

## Material setup and provenance

Materials/maps are copied unchanged from ContinuousV1. Create new URP Lit materials with exact slot names; `materials.json` gives colors, roughness and metallic values. Smoothness = 1−roughness. The reviewed Blender look uses solid base colors plus the weave normal at strength 0.18, tiling 14 for cloth. It does not use the supplied weave base-color and metallic/smoothness maps in those nodes; blindly assigning all maps will change appearance. Import normal as Normal Map, metallic/smoothness linear, base color sRGB if used. Keep `.001` slot-name mappings explicit for EVA shared materials. No emission or global lighting change is needed.

The anatomical mesh/rig/initial weights remain MakeHuman CC0 core data pinned to `a8bc2d54ff0ac92e78ff71431b1023eda42bf482`; copied license texts are in `Source/`. Exact original provenance is retained in `Provenance/ContinuousV1-manifest.json` and `Provenance/MakeHumanCC0-manifest.json`. Previous garment/texture assembly and this split/pose work are original project contributions. Existing glove references remain the existing project author's work and are not relicensed as CC0. No new asset download or package was used.

Suggested eventual root-owned asset destination: `Assets/_OutOfReach/Visuals/Character/FirstPersonBody/FitV02/` for FBX/maps/new materials, with new metadata. Preserve source/provenance outside Assets to avoid automatic `.blend` import of render/reference objects. Do not import the rejected top-level segmented study.

## Evidence and limitations

See `Validation/build-report.json`, `Validation/fbx-roundtrip.json`, `Validation/split-boundaries.json` and `Validation/render-manifest.json`. Build, independent fresh import and render logs identify their exact outcomes. All three baked poses are tested against source surfaces and bones, both standalone and embedded clips are checked, skin weight sums/influence count/degenerate triangles are checked, and shared shoulder vertices must remain coincident. Real renders include full arms and body-only from the same camera plus shoulder-cap diagnostics.

No Unity import, URP capture, real player/cargo/terminal fit, Humanoid mapping, walking or transitions have been verified. The material/detail level remains the reviewed simple V1; this checkpoint addresses controllability and pose delivery, not premium character fidelity. No provisional walk cycle is included: convincing foot contact and stride matching require a separate bounded movement pass rather than labeling a static or sliding pose as walking.

Reproduce in installed Blender 3.6.1 background mode, in order: `Source/build_fit.py`, `Source/verify_fit.py`, `Source/render_fit.py`. Use `--python-exit-code 1` so failed assertions produce a failing process status. These scripts write only to this staging directory and read the immutable prior source. The rendering helper also supports `-- --float-only` for the two floating-camera captures after a pose-only correction.
