# NASA deep sky v01 — dated celestial reference

Source: [NASA SVS Deep Star Maps 2020](https://svs.gsfc.nasa.gov/4851/), released 9 September 2020. The selected 4096×2048 celestial OpenEXR plots catalogue stars in ICRF/J2000 geocentric right ascension/declination. It includes the Milky Way's dense stellar background; it is not a spacecraft photograph or a 2026 observation. Colour balancing in the source includes artistic choices. No constellation overlays are included.

The source is an unmodified 35,997,085-byte download, SHA-256 `69b841cd048c2ef35543eb1c5819b322530e78aa4b0cab97ec1de3ef56a4ddcd`.
Exact URL: https://svs.gsfc.nasa.gov/vis/a000000/a004800/a004851/starmap_2020_4k.exr

[SVS reuse policy](https://svs.gsfc.nasa.gov/help/) places its content in the public domain unless otherwise marked. No separate restriction is displayed for this star-map file. Keep the credit “NASA/Goddard Space Flight Center Scientific Visualization Studio; Gaia DR2: ESA/Gaia/DPAC.” This use does not imply NASA/ESA review or endorsement. [NASA media guidance](https://www.nasa.gov/nasa-brand-center/images-and-media/) also applies. There are no logos, astronaut likenesses, music or constellation figure artwork in this texture.

## Unity integration

Call `OutOfReach.Composition.Editor.I06NasaDeepSkyAuthoring.EnsureAssets()` in Unity 6000.3.28f1 Edit mode. The builder verifies the source hash and dimensions, imports it as a **Texture2D**, linear-light HDR (Standalone BC6H), horizontal repeat/vertical clamp, 4K maximum, trilinear filtering and mipmaps. It generates two `Skybox/Panoramic` materials:

- `Assets/_OutOfReach/Visuals/Environment/NasaDeepSkyV01/NasaDeepSky.mat`: exposure multiplier 0.15, the restrained bright-ship/EVA preset.
- `Assets/_OutOfReach/Visuals/Environment/NasaDeepSkyV01/NasaDeepSky_DeepView.mat`: multiplier 0.45, a dark-cabin/cinematic preset.

The builder never changes RenderSettings or scenes. Root owns the `I01BootstrapBuilder.ConfigureEnvironment` sky binding and `LeadDepartureAuthoring.Stars` binding feeding `I06MarineBindings.orbitalSky`. The existing GMAT cubemap, licences, provenance and its standalone validator remain untouched; this is an explicitly selected replacement sky, not a rewrite of that dataset.

Both presets preserve one common map orientation. Source horizontal centre is RA 0°, increasing leftward; top is declination +90°. With the built-in panoramic mapping at rotation zero, the intended sky frame is +X = RA 0°, +Z = RA 90°, +Y = celestial north. This differs from the older GMAT presentation frame; there is no claimed measured spacecraft attitude tying either frame to the fictional ship. Root's native screenshot review must check the celestial orientation and seams before asserting visual acceptance.

The sky is at infinity, with no translation parallax. There is no stellar proper-motion update to mission epoch, aberration, Solar-System parallax correction or radiometric calibration. ICRF/J2000 coordinate orientation is not an assertion that all catalogue stars were measured in 2000. Sun, Earth and Mars are not painted into this texture; any nearby bodies require independent scene placement and scale. No atmospheric cloud or terrestrial horizon panorama is used in space.

## Exposure and visibility

The two exposure values are explicit art direction, not physical eye adaptation or camera EV estimates. They do not change automatically with the view. Root may select the dark-view preset for deliberate dark-cabin/cinematic shots; adaptive exposure would require a separately reviewed runtime change. Current game grade is ACES with +0.45 EV post exposure, so native viewing under that exact grade remains required.

NASA's [Cassini explanation](https://science.nasa.gov/photojournal/not-really-starless-at-saturn/) describes bright rings/moons requiring short camera exposures that omit faint stars. A richer dark-view sky is therefore a controlled cinematic choice; the Milky Way should not remain equally bright beside a sunlit hull. Earth-surface/cloud HDRIs are appropriate only for atmospheric launch scenes.

`pixel-inspection.json` records Blender 3.6.1 inspection: finite, nonnegative RGB, 4096×2048, linear EXR; median luminance 0.007651, 95th percentile 0.055678, maximum 1.0. These are source pixel values, not measured radiance. No tone mapping, resampling or image-generation step was applied to source bytes.

## Evidence

`retrieval.json` records exact URLs, retrieval timestamp, bytes and SHA-256 for the map and the saved source/reuse-policy pages. `download.py` repeats the bounded acquisition into its own directory (40 MB ceiling per file). `inspect_exr.py` runs in the already installed Blender and checks source pixels. Native Unity import/material/scene verification belongs to root; preparation alone is not a passing Unity render or playtest.
