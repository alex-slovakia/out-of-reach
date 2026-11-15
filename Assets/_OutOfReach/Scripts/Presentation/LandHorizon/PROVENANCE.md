# Original land horizon V01

Original procedural geometry and shader source authored for OUT OF REACH by the Codex presentation team, 14 November 2026. No source photos, downloaded textures, third-party meshes, generated bitmap imagery or new dependencies. Project may use, modify and distribute this original source and its generated assets, including commercial distribution. No NASA imagery, NASA logo, geographic survey or real launch-site engineering claim.

The shader links to the project's existing Unity URP 17.3.0 package includes under its existing Unity package terms; no package source is copied into this asset. Standard gradient noise, interpolation and PBR equations are algorithms, not sampled visual assets. The broad surface and tiny roughness variation are static and use no time or simulation input.

Generated assets stay under this folder's `Generated/` directory. Mesh/material asset GUIDs are preserved on regeneration through in-place serialization. The generator never saves a scene and creates only one passive renderer under the explicitly supplied stationary SiteRoot.

Visible material: original muted sage/soil/dry grass. Four decorrelated, derivative-filtered noise bands; opaque back-face-culled depth-tested URP PBR, zero metallic, zero emission, smoothness limited to 0.04–0.20. Main directional light/shadow, ambient SH, optional SSAO, depth/normal passes and existing fog are consumed. No added/global light, sky, volume or water changes. No painted roads, actual grass blades, terrain collision or gameplay claims.

Whole kit footprint x[-72,72], z[-62,72] is a precise mesh hole. Native root must enable the horizon only with the covering launch kit and disable all marine water. Ground datum is site-local -36.6m, with at least200m flat clearance; bounded distant relief <=0.85m. Any optional outlet notch is supplied explicitly from the final kit manifest.

Native import/shader/GPU appearance pending root review. Offline geometry checks are not an art acceptance result.
