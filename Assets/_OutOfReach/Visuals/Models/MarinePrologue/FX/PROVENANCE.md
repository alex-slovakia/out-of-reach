# Original departure effects

Original OUT OF REACH presentation code, parametric mesh shapes and procedural shader, authored for this project on14 November2026. No downloaded textures, image planes, copied game art, new packages or third-party runtime dependencies.

The flame/heat structure and four low-opacity ignition-steam lobes are authored cinematic approximations. They do not calculate combustion chemistry, thrust, fluid pressure, water displacement or a flight trajectory. Existing Core and TransportDepartureTimeline remain authoritative. No ocean behavior is changed.

The nozzle reference is the delivered transport's `EngineEmitter` neutral-axis integration frame: capsule-relative(0,−31.4,0), forward−Y, original bell exit inner radius0.90m/outer0.95m. Its stationary solid barge deck top is y−32.6m; sea level is y−34.6m. These are measured fictional art dimensions, not certified launch engineering. Use the actual supplied frame and actual collider plane rather than rebuilding the vehicle or assuming a world origin.

Geometry is allocated once when the first visible sample arrives: one closed720-triangle plume mesh and one shared closed224-triangle steam mesh instanced at most four times. Transparent surfaces are depth tested, backface culled and have no shadow/depth-writing pass. Each steam lobe has opacity at most0.13. Shared materials are never mutated; per-instance property blocks carry root-supplied time. No global light, grade, camera, physics or audio changes are included.
