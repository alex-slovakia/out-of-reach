# Power Box 01 — OUT OF REACH visual adaptation

Source: https://polyhaven.com/a/power_box_01

Authors: Rico Cilliers (modeling and texturing), Yann Kervran (rigging).
License: CC0-1.0, https://polyhaven.com/license and https://creativecommons.org/publicdomain/zero/1.0/.
The source manifest preserves acquisition URLs, byte counts, provider checksums and SHA-256 values. The FBX and four supplied texture files are copied byte-for-byte; `ShipArtAssets.Prepare()` verifies them against that manifest before generation.

## Unity adaptation

- Existing Unity 6000.3.28f1 and URP 17.3.0 only; no source scripts, packages or dependencies are included.
- Source animation and automatic collision import are disabled. The static displayed instance uses the original mesh, UVs and supplied texture maps. Its maintenance-cover renderer is hidden to expose the breaker assembly. No gameplay state or repair behavior is implemented by this asset.
- Diffuse is imported as sRGB. OpenGL normal is imported as a normal map without a green-channel inversion. Metallic and roughness are read as linear data.
- The derived `power_box_01_metallic_smoothness.png` contains metallic in R, zero G/B, and `1 - roughness` in A, quantized to 8 bits. The two source EXR maps remain unchanged. `PowerBox01_URP.mat` binds the original diffuse/normal and derived packed map to URP/Lit.
- Model bounds determine scale: visible assembly height is 0.85 m. The power-rack authoring frame centers it at y=1.25 m and z=6.1 m, with its foremost surface at x=-1.70 m. Original integration anchors and cabinet collision envelope remain unchanged.
- This is a near-future fictional spacecraft prop derived from terrestrial electrical equipment. It is not sourced NASA hardware or a claim of flight-qualified design. Source labels remain artwork, not authoritative game instructions.

## Verification boundary

Copy-time SHA-256 checks passed for the original five binaries. Shader appearance, model-facing direction, source-cover removal, final lighting and player-path clearance require verification in the regenerated Unity scene. A source import or checksum pass is not a gameplay/build test.
