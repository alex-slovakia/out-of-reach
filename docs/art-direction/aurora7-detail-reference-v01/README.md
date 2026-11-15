# AURORA-7 whole-ship detail reference

Generated with built-in GPT Image on 15 November 2026 from genuine AURORA-7 Unity frames. These boards show materials, assembly seams and detail placement proposals. The FBX geometry, Bootstrap and the existing placement manifests are authoritative for measurements.

![Interior compartments and separate parts](aurora7-interior-reference.png)

![Exterior, aft EVA route and separate parts](aurora7-exterior-reference.png)

## Existing ship and mounting rules

The deck is at Y=0 m and the ceiling is Y=3.12 m. +Z runs aft. The room bounds below come from `a7_ship.py`. The real centered openings are 1.8 m wide and 2.5 m tall. The body capsule, contact rails, hardware reach and all current click zones must pass in the actual scene after mounting.

| Room | Z interval | Width | Current fixed geometry |
| --- | --- | --- | --- |
| Cockpit | −3 to 3 m | 6 m | Two seats on rails, three live displays, two inset windows, side lockers and forward bulkhead |
| Engineering | 3 to 11 m | 4.8 m | Port EPS repair rack, starboard life-support equipment, tool board, composite vessels, overhead conduits |
| Habitat and stowage | 11 to 17 m | 4.8 m | Port galley and berth, starboard grow cassette, existing cargo restraint and access aisles |
| Airlock | 17 to 21 m | 4.8 m | Two-leaf sliding pressure hatches, pressure station and starboard glazed suit locker |
| Exterior | Authored shell and EVA route | R3.55 m forward shell and R3 m aft shell | Paired visor windows, blankets, solar wings, radiators, aft porch, antenna gimbal and tether |

## Buildable interior models

| Image | Separate mesh | Mount and acceptance |
| --- | --- | --- |
| 01 | Three instances of a shallow display hood/bezel and one separate guarded switch cassette | Use the world-space bounds of the three cockpit displays. Preserve the active glass and every current click ray. Model the raised switch caps around their live hitboxes. Keep both seats, rails and portholes. |
| 02 | Service junction box, keyed plug body, braided loom and cushioned clamp | Use engineering ring-frame flanges and existing trough entries. Reuse the DP02 engineering loom wherever it already covers the intended mount. Leave the open EPS face, action targets and amber grips exposed. |
| 03 | Small manifold body with caps, captive couplers, 90-degree fittings and a short hose pair | Mount beside the existing grow cassette at its plumbing-side face or reuse the delivered DP02 kit. Keep the tray and access lid usable and pressure taps sealed. |
| 04 | Soft crew pouch with loop, webbing strap, cam buckle and D-ring | Reuse or refine DP02 at a habitat blanket wall. No detached props in the center aisle. Keep galley, berth and restraint rails reachable. |
| 05 | Ribbed hatch-track cover, edge seal strip and fastener strip | Use the existing stationary door frame near AirlockInner and AirlockOuter. Exclude the whole moving-leaf sweep and preserve the authored pivots, triggers and seals. |
| 06 | Pressure-station bezel, dial rim, glass, low-travel key caps and bracket | Dress the existing physical readout and switch surfaces at the aft bulkhead. Keep canonical pressure labels, driven pointer and collision. |

Start with **01 cockpit**, then **02 engineering** and **05 airlock**. DP02 engineering, habitat and cockpit are already delivered at `650db0c`. Compare them in-engine before assigning another writer or rebuilding similar parts.

## Buildable exterior models

| Image | Separate mesh | Mount and acceptance |
| --- | --- | --- |
| 01 | Thermal-blanket seam and webbing tensioner | Follow existing hull radius and strap intersections. Geometry belongs at the seam and buckle. Use material detail for weave at distance. |
| 02 | Curved flush service-panel skin with gasket lip and captive screws | Fit the authored panel aperture on the proper module radius. Preserve the amber edge, existing silhouette and all route clearances. |
| 03 | Shielded flight-service junction box, connectors and clamps | Mount beside its existing equipment interface. Use short secured cable runs to the authentic tray. Keep thruster plumes, radiator and solar-wing sweep clear. |
| 04 | Bolt-on handrail foot and existing tether eye detail | Fit around the real handrail shaft and end point. Preserve the real grab radius, anchor positions and EVA route. |
| 05 | Docking collar outer skirt, gasket, bolt ring and inspection lights | Fit around the cinematics' actual aft receiving frame. Keep the real opening, bolt plane, approach, EVA porch and hatch clearance. The two mating ports must still meet on camera. |

Use metre scale, stable names and pivots on actual mount faces. Supply one Blender source, FBX with stable metas, placement records, original/shared licensed materials and a genuine in-engine screenshot. Keep player-facing controls and labels on the established live assets. Recheck normal and emergency light at the eye position.

## References

- [AURORA-7 model handoff](../../../Assets/_OutOfReach/Visuals/Models/Aurora7/MODEL_HANDOFF.md)
- [Unity ship previews](../../../Assets/_OutOfReach/Visuals/Models/Aurora7/Previews)
- [Measured ship authoring source](../../../Assets/_OutOfReach/Visuals/Models/Aurora7/Source/a7_ship.py)
- [DP02 delivery and coordination](https://github.com/alex-slovakia/out-of-reach/issues/7)

The habitat panel also used the actual in-flight native capture `01-habitat-wide.png` from run `20261114T192757692Z`. Image labels and materials are visual drafts. Final HUD, door behavior and astronaut animation continue to come from Unity.
