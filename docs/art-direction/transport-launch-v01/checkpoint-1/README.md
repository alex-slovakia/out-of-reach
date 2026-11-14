# TRANSPORT-LAUNCH-ART-V1 — checkpoint 1: measured block-out and anchor proposal

These are real Blender 5.3 Cycles views of a block-out with flat colours. Textures, labels and the detail pass come
later. The views show a proposed standalone kit: they are not Unity captures and not placed in any scene. The sky
and sea are render props.

Renders:
- `cp1-boarding-pov.png`: eye at 1.65 m on the access arm, hatch open.
- `cp1-exterior.png`: the whole kit on its barge.
- `cp1-interior.png`: from just inside the hatch.
- `cp1-seated-hatch-closed.png`: from `SeatedCamera` at a vertical FOV of 68°.

## Frame

Metres, Y up. The origin is on the capsule floor at the capsule axis. The player boards through the side hatch from
−Z and looks +Z.

## Proposed dimensions

| | Value |
|---|---|
| Capsule | Ø 5.00 m outer and Ø 4.76 m inner. The cylinder wall rises to Y 2.60 and the cone to Y 4.55; the docking port is at Y 4.95. The heat-shield base is at Y −0.55. |
| Hatch | Clear 1.40 m wide × 2.15 m high, with 0.16 m corner radii. The sill is level with the floor (Y 0). It is a plug door that follows the hull. |
| Hatch motion | Hinged on the −X jamb at `HatchPivot`, opening outwards to about 160°, where it lies against the hull to −X. Through that whole swing it stays clear of the arm's rails. |
| Central route | 1.30 m clear between the yellow edge lines, and about 1.43 m between the rack fronts. Interior height is 2.47 m under the light panels and 2.55 m at the wall. |
| Cargo racks | 4 cells on −X (oxygen, water, food, battery) and 3 on +X (solar, spares, science). The cell pitch is 0.55 m along Z. Each cell has 3 slots at Y 0.10, 0.46 and 0.82, 0.53 m deep and 0.36 m high. One game case (0.453 × 0.319 × 0.354 m) fits each slot, with its labelled face to the corridor. |
| Seat | One reclined launch seat (30°) with a harness. It is centred at Z 1.45 and faces −Z, so the player sees the hatch and the cargo, as in storyboard panel 1. A second seat beside it fits if you want two crew. |
| Windows | Two in the cylinder at the seated eye height, at ±60° from +Z. Four in the cone. |
| Below the capsule | Service section Ø 4.70 m down to Y −3.40, with stowed solar wings. Interstage to Y −4.40. Booster Ø 4.60 m down to Y −29.40, with two black bands and folded grid fins. Engine section and bell exit at Y −31.40. Four legs on hold-down pedestals. |
| Barge | 36 × 24 m (X × Z), deck at Y −32.60, with rails, bollards, fenders, deck lamps, a flame deflector and two small deck cabinets. Water is not modelled; it stays your runtime system. |
| Access mast | 3.2 × 3.2 m tower at Z −8.5, from the deck to Y 3.8. It has a landing at floor level (Y 0, 4.4 × 4.3 m) with a 2.2 m gap in the rail on its −Z edge for your staging deck. |
| Access arm | 4.35 m walkway from the mast to the hull, floor level with the capsule floor. It is 1.8 m wide with rails, narrowing to 1.5 m (no rails) for the last 0.55 m at the hatch. Its pivot is `AccessArmPivot`. |
| Total height | 37.6 m from the deck to the docking port. A fictional visual vehicle, not engineered. |

## Anchors (Unity metres; the direction is where the anchor points or faces)

| Anchor | Position | Direction / note |
|---|---|---|
| `BoardingThreshold` | (0, 0, −2.50) | +Z: the outer edge of the hatch sill |
| `CargoBay_oxygen` | (−0.985, 0.10, −1.025) | +X (case face to the corridor); slots at Y 0.10 / 0.46 / 0.82 |
| `CargoBay_water` | (−0.985, 0.10, −0.475) | +X |
| `CargoBay_food` | (−0.985, 0.10, 0.075) | +X |
| `CargoBay_battery` | (−0.985, 0.10, 0.625) | +X |
| `CargoBay_solar` | (0.985, 0.10, −1.025) | −X |
| `CargoBay_spares` | (0.985, 0.10, −0.475) | −X |
| `CargoBay_science` | (0.985, 0.10, 0.075) | −X |
| `SeatEntry` | (0, 0, 0.95) | the player stands here, then turns to sit facing −Z |
| `SeatedCamera` | (0, 1.16, 1.88) | looks −Z, slightly down (0, −0.17, −0.985) |
| `HatchPivot` | (−0.72, 0, −2.40) | the hinge axis is +Y; opens to about 160° |
| `HatchLatch` | (0.58, 1.05, −2.33) | inner latch handle on the closed door |
| `DockingPort` | (0, 4.95, 0) | +Y |
| `EngineEmitter` | (0, −31.40, 0) | −Y, at the bell exit |
| `AccessArmPivot` | (−0.90, 0, −6.90) | the hinge axis is +Y; swing the arm clear before ignition |

Exact numbers are in `checkpoint1_report.json`.

## Questions before the detail pass

- One seat, or two?
- Is one bay per type, with 3 stacked slots each, enough for your cargo counts?
- Is the stack height acceptable for the launch cinematic?
- Does the mast placement at −Z suit your staging deck?
