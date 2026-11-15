# Land launch visual handoff

![Approved launch concept and six detail crops](launch-approved.png)

This user-approved concept is the art target for ignition and early liftoff at the Earth launch pad. The image shows the crew transport’s booster rising beside its service mast, with the upper vehicle cropped out. Use the existing boardable transport, tower, access arm, pad and terrain as the basis. Keep their placement, silhouette, traversal clearances and operating sequence. AURORA-7 is the destination in the later orbital rendezvous. The concept gives visual direction for the pad shot and supplies no dimensions or flight performance data.

## Detail crops

| Crop | Modeling and surface work | Effects and placement |
| --- | --- | --- |
| **01 · Engine bell and plume** | Detail the existing bell with a thick rim, concentric ribs, mounting flange, fasteners and heat-stained dark metal. Concentrate modeled detail at the visible rim and fittings. | Use the vehicle-local `EngineEmitter` for a tight white-hot core and a wider amber turbulent envelope with short, soft shock bands. The plume follows the booster and meets the actual deflector or pad contact surface as it lifts. |
| **02 · Deluge and trench** | Keep the open trench and existing outlet. Give the deflector a curved, ribbed hot face, localized soot and wet concrete near the spray. Add visible feed pipes and spray heads where the tower meets the pad. | Mount water jets on the stationary pipe heads and aim them at the trench mouth. Use `LS_TrenchFloor_UnderNozzle` and `LS_DeflectorPeak` to locate the mixing region. Spawn rolling white-grey steam along the trench and carry it out through `LS_TrenchExit_Ground`. |
| **03 · Dust** | Keep the existing grass and gravel margins. Place loose grit and color variation at the apron edge where the outflow reaches exposed ground. | Emit tan dust from low, stationary ground anchors beyond the apron. Push it outward in a thin layer with slower curl and breakup. Let it linger briefly as the vehicle climbs. |
| **04 · Heat distortion** | Keep the bell lip and distant terrain legible. Their edges make the distortion visible. | Attach a narrow region of subtle refraction to the hot wake below the nozzle. Fade it at the edges and with distance. Suppress refraction in reduced-effects mode and keep subtitles clear. |
| **05 · Booster skin and service lines** | Retain the pale cylinder and dark bands. Use shallow panel seams, access plates, fastener rows, patchy roughness and restrained downward streaks. Model the service lines and brackets that break the silhouette. | Parent attached lines and fittings to `BoosterStack` and preserve separation and access-arm clearance. |
| **06 · Ramp, drains and rails** | Build flush galvanized drain grates, slab joints, anchored rail feet, amber rails and worn hazard stripes. Use roughness variation for damp channels and painted concrete. | Keep grates and rails on the stationary site. Keep the apron walk surface flush and the existing boarding route clear. |

Give the existing tower dark structural steel, faded corrugated cladding, amber handrails, visible stairs and landings, and a pipe run down to the deluge. Prioritize the bell-to-trench interaction and early-liftoff composition, then tower and booster detail, then foreground wear.

## Integration and review

Use `TransportDepartureTimeline` as the shared playhead for ignition, liftoff and departure audio. `MarineDepartureFxPresenter` already consumes that playhead. Bind pad emitters under `Launch site - stationary` and use the [land-site anchor map](../land-launch-site-v01/README.md) against the current scene placement. Bring in a short, restrained ignition rumble beneath the radio in the cabin and a brief deluge wash in the pad view. Check pause, skip and restart for lingering emitters and audio.

Compare actual Unity pad-camera frames before ignition, at first clearance and during the early climb. Check that steam and dust stay grounded, the booster and tower remain legible, and reduced effects preserve both the action and subtitle readability. This delivery contains the approved image and art notes. Unity verification is pending for the corresponding art pass.
