# Marine Ocean V01

Original project C# authoring/presentation and HLSL shaders. No downloaded texture, copied water package, generated-image texture, runtime network request or new dependency. Consumes existing Unity 6000.3.28f1, URP 17.3.0 and built-in ParticleSystem APIs through their installed packages.

The three sinusoidal waves, procedural normal detail, radial contact foam and small particle bursts are authored visual approximations. They do not compute wave spectra, fluid flow, buoyancy, structure loads, physically coupled splashes or vessel motion. The water is not a collider or survival boundary.

The built prefab's four default contact positions match the original MarineLoadingDockV02 support centres in XZ, but references remain explicitly replaceable by composition. That deck source is not modified or embedded.

The supplied code has static managed-reference compilation evidence only. Root must import and render the shader in the actual marine scene before claiming visual/runtime acceptance. No screenshot of runtime water is included in this staging handoff.
