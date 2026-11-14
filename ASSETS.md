# Art direction, sources and budgets

## Visual language

AURORA-7 is a small research vessel with believable service access, worn graphite panels, practical lighting and readable equipment labels. Cyan marks ordinary information, amber marks action or caution, red marks emergency. Use darkness to frame the objective, not to hide the route. The user's supplied concept image is a direction reference, not a promise that a two-day prototype reproduces every illustration.

Begin with authored modular primitives: floor and ceiling strips, ribs, service panels, cockpit consoles, storage lockers, airlock and exterior handholds. Keep collision simple. Design the silhouette, composition and lighting before adding surface detail. Earth is a small distant point during the deep-space mission.

## Source register

| Asset group | Source / license status | Required action |
|---|---|---|
| Existing Unity URP template content | Already present in project | Retain provenance; no claim that it is original team art |
| New modular geometry and generated materials | Team-authored | Record creator/task at handoff |
| New UI text and simple graphic shapes | Team-authored | Keep legibility and readable units |
| NASA scientific dataset | See `NASA-DATA.md` | Include provenance in report and credits; do not imply NASA endorsement |
| External textures, models, music or voices | None approved by this document | Verify each specific item's license and attribution before import |

Candidate sources, not downloaded or approved assets: [NASA 3D resources](https://science.nasa.gov/3d-resources/), [Poly Haven](https://polyhaven.com/), [ambientCG](https://ambientcg.com/), [Kenney](https://kenney.nl/assets). Inspect the individual asset/license; NASA branding and third-party contributions may have separate restrictions. Prefer small CC0 assets when needed. Do not download packs merely because a source is listed.

For each imported asset record its exact URL, creator, license URL/text, date, modifications and in-game credit requirement. Do not use a game reference screenshot as a texture, extract assets from another game, or use commercial music without permission. Avoid voice cloning; use original text and licensed/generated voices only after a clear source decision.

## Provisional performance targets

These are engineering budgets to measure, not achieved results: 1080p, 60 fps target on an agreed ordinary Windows gaming PC; scalable effects for 30 fps hardware. Keep only a few active dynamic lights per room; start with one shadowed key light in view. Prefer instanced shared materials, modest texture sizes (usually 1–2K), limited transparent particles, and simple colliders. Avoid realtime GI, excessive volumetrics and cinematic-quality offline assets.

Measure actual CPU/GPU frame time, memory, startup and build size on a named machine before claiming performance. Reduce expensive effects before reducing objective readability. Package only necessary content.

## Cinematic accessibility

Provide reduced shake/flashing, subtitles for transmissions, separate audio level controls where feasible, clear pause/restart and no objective conveyed solely by color. Brief blackout must return control reliably. In EVA use breath, radio and suit/contact vibration; any music is non-diegetic. Finale camera movement must not interfere with the mission result.
