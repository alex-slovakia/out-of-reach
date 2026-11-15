# Mission terminal V02 runtime hardware

Hardware is original team art delivered by the Claude prop chat on Apoliak's machine in commit `0fd8cad4c9ef639785eb22a0f01205928b9b65b4`, under `docs/art-direction/terminal-v02/`. The runtime package retains the exact approved FBX and four supplied PNG maps, verified against Git LFS SHA-256 identifiers. No geometry or texture pixels were changed here. Editable Blender source and construction scripts remain in that source commit; the `.blend` is intentionally not imported into Unity.

The source reports four static meshes, 14,268 triangles, four materials / five mesh material slots, metre scale and a 0.640 × 0.400 m active display. The importer validates those counts and the actual screen winding/corners/UVs. This delivery has only managed-reference compilation; an integrated native import/render/contact test is still required.

Geometry, UVs and baked maps: original team work. Printed legends use rasterized Inter, SIL Open Font License 1.1. Credit: **Inter typeface © The Inter Project Authors**. No font binary is redistributed in this package. No NASA insignia, third-party model, game asset or downloaded texture is used in the hardware. Fictional AURORA-7 identity does not imply agency endorsement.

URP materials: `MT_TerminalBody` uses sRGB BaseColor, linear MetallicSmoothness (RGB metallic, A smoothness), and linear Occlusion (G). `MT_Labels` uses the sRGB label atlas and is non-emissive. `MT_Amber` is a small non-emissive status lens by default; its source emission setting is intentionally not enabled before native exposure review. No normal map was delivered. All four supplied maps are 2048 square and initially imported uncompressed with mipmaps.

The separate NASA JPEG in `Data/` is unmodified. Its existing `ATTRIBUTION.md` and `earth-observation-manifest.json` define reuse basis, source and observation UTC. This note does not relicense NASA material. Preserve **NASA EPIC Team / NOAA DSCOVR** credit, **2026-11-11 11:01:17 UTC** and archived status. The inherited data manifest's `image/file` is its original source-package path; the runtime JPEG is beside the manifest in `Data/`.

Display shader and presentation runtime remain byte-identical to staged PhysicalTerminalV01. They show one archived full-disc observation as an explicitly schematic orthographic projection, with neutral unobserved reverse and original-image inset. It is not a georeferenced full-Earth atlas, current weather, a live feed, spacecraft GPS or navigation authority. No runtime downloads occur.

The supplied artist Unity focus-camera image is source-art evidence with different lighting and no runtime screen. It is not evidence that this terminal is integrated or that glove contact has passed.
