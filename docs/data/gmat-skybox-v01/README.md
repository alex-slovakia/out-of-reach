# GMAT skybox staging v01

Six 1024 x 1024 RGB PNG faces calculated from the committed GMAT catalogue. This is a **calculated star projection, not a Unity screenshot or a verified in-game render**. No Unity process, game asset, material, scene, `.meta`, Git state or original research package was changed by this work.

The numerical rasterizer uses the existing installed Python, NumPy and Pillow. Run `python -B generate_sky.py` for read-only regeneration, source/hash, double-precision mapping and pixel-seam verification. `--build` deliberately rewrites only this staging directory. The contact sheet uses the installed Segoe UI font for its labels; those labels are absent from the six textures.

## Copy plan

Copy **only** the six `textures/Stars*.png` files to their same-named destinations under `Assets/_OutOfReach/Visuals/Space/`. Keep every existing `.meta` and the existing `DistantStars.mat`; the destination GUIDs and observed hashes are in `input-provenance.json`. No new Unity GUIDs are supplied. Existing settings inspected: 1024 faces, sRGB, clamp, bilinear, no mipmaps; material exposure 1, neutral tint 0.5, rotation 0. These remain the intended interface.

| PNG | Material property | Celestial direction at center | Image right | Image top |
| --- | --- | --- | --- | --- |
| StarsFront.png | _FrontTex | +Z, RA 0 / Dec 0 | +X | +Y |
| StarsBack.png | _BackTex | -Z, RA 180 / Dec 0 | -X | +Y |
| StarsLeft.png | _LeftTex | +X, RA 90 / Dec 0 | -Z | +Y |
| StarsRight.png | _RightTex | -X, RA 270 / Dec 0 | +Z | +Y |
| StarsUp.png | _UpTex | +Y, north celestial pole | +X | -Z |
| StarsDown.png | _DownTex | -Y, south celestial pole | +X | +Z |

The counterintuitive Left/+X and Right/-X names are Unity's six-sided skybox convention. All face corner orientations were independently checked against 36 position/UV records from Unity 6000.3.28f1's installed `6FaceSkyboxMesh.mesh`; the reference hash and extracted numerical evidence are in `unity-orientation-evidence.json`. See the [Unity six-sided skybox reference](https://docs.unity.com/en-us/engine/6000.3/manual/creating-environments/sky/skybox-material-reference/shader-skybox-6sided).

**Integration hazard:** the existing `DistantSpaceAuthoring.Build()` still creates the former procedural random-star textures. Do not run that authoring command after copying these faces. Retiring or updating that command is a separate integrator-owned change.

Keep this as `RenderSettings.skybox`, with the existing skybox material, rather than a nearby sphere/particles attached inside the cabin. Texture sampling uses direction alone; walking cannot introduce geometric parallax into this data. The integrator must still verify the actual skybox camera behavior in Unity. No authoritative ship attitude or epoch was added, and no Earth/Mars discs are included.

## Source and artistic treatment

The input is `docs/data/gmat-starfield-v01/gmat-stars.json` from the isolated integration checkout. The captured Git revision, byte-exact input hash and upstream commit are recorded in `input-provenance.json`. A local input copy makes regeneration self-contained. The 2,797 rows become 2,785 distinct coordinate pairs by retaining the brightest source magnitude for exact duplicates; no random stars or HEASARC BSC values are added. Polaris remains source magnitude 6.0, without a special brightness override. All RGB channels are equal because the source has no color data.

Small angular Gaussian point-spread functions use source unit directions, relative flux `10^(-0.4*m)`, fixed artistic exposure 4, and `1-exp(-radiance)` response before sRGB encoding. The 0.0009 radian spread is an artistic raster footprint, not a physical stellar angular diameter. No glow halo, diffraction cross or star streak is added. The background is black. Stars near cube boundaries are evaluated on both adjoining faces; the exact shared edge directions have matching RGB texels. This avoids dropping a star merely because its center crosses a face boundary. The edge texel sample is snapped by at most half a texel to the exact cube edge for the existing clamp/bilinear interface.

GMAT's frame alignment with J2000 is inferred from its renderer, while its stellar observation epoch and underlying measurement provenance are unspecified. The magnitudes are coarse visualization values, not precision Johnson-V measurements. Proper motion, stellar parallax, aberration and epoch-of-date correction are not modeled. Texture pixel precision does not improve catalogue accuracy.

## Remaining actual-game checks

The root integrator should preserve the six `.meta` GUIDs, confirm material property bindings and rotation zero, inspect recognizable directions and all six transitions in Unity, and walk inside the cabin to check for translation parallax. Check conservative visibility against the ship's bright cockpit and the actual tonemapper. This staging verification cannot certify an actual GPU render, import settings after integration, or an in-game playtest.
