# Attribution

LAND-TERRAIN-V01 is original procedural work made with AI: Claude Opus 5.5 (Anthropic), working as the model writer for Apoliak7777's team, OUT OF REACH (NASA Space Apps 2026).

## How it was made

- All geometry and every texture are generated from the code in `source/`, with fixed seeds and a fixed integer hash.
- Tools: Python 3 with NumPy and Pillow; Blender 5.3 (bpy) for meshes, FBX export and preview renders.
- The pipeline helpers (`a7kit.py`, `a7_textures.py`, `build_habitat.py`, `hb_kit.py`, `roundtrip_habitat.py`) are copies of the team's own earlier original code, as shipped in the LAND kit and LAND-SURROUNDINGS packages.
- The rock material reuses the LAND kit's own procedural `LS_Concrete` maps.

## Not used

- no downloads, scans, photographs or satellite data;
- no AI image generators;
- no third-party models, textures, fonts or brushes;
- no agency logos, insignia or branding.

## Previews

The preview renders show the LAND kit rebuilt from its own source, with simple stand-in proxies (vehicle, mast, sky, sun) and a distance haze added in post. They are illustrations, not runtime captures.
