# HABITAT-DETAILS-V1 — inhabited-detail kit for AURORA-7

**Status:** a Blender art kit for review, authored by the Claude model writer on 14 November 2026.
- It is **not integrated in Unity** and has no animation or gameplay.
- Root imports the approved units and keeps their GUIDs.
- The renders below are genuine Blender 5.3 Cycles stills. They show **one proposed placement** in the Storage room, built from the ship's own generator; they are not a Unity capture.

![Storage room, proposed layout](renders/HB_blender_habitat_wide.png)

The wide view above has the crew berth and galley on the left wall (bays at z 14.75 and 16.25) and the grow cassette on the right wall (z 14.75). For this render they replace the stowage walls in those bays.

![Galley, first person](renders/HB_blender_habitat_galley_fp.png)

The first-person close-up above shows the galley at 1.62 m eye height.

More views:
- [berth front](renders/HB_blender_habitat_berth_fp.png)
- [grow cassette](renders/HB_blender_habitat_grow_fp.png)
- [view from the SleepCamera anchor](renders/HB_blender_habitat_sleep_camera.png)

## Units

| Unit (FBX) | Objects (pivot, motion) | Triangles | Envelope (W × H × D, m) | Anchors |
|---|---|---|---|---|
| `HB_CrewBerth.fbx` | `HB_CrewBerth` | 12,204 | 1.44 × 2.40 (2.55 with the duct) × 0.53 | SleepEntry, SleepCamera, PersonalStowage |
| `HB_Galley.fbx` | `HB_Galley`; `HB_GalleyTable` (hinge line y 0.97, z 0.46; modelled deployed, rotates 90° about local x to stow) | 13,180 | 1.44 × 2.40 × 0.46 (0.72 with the drawer open, 0.80 with the table out) | RationPickup, WaterDispenser |
| `HB_GrowCassette.fbx` | `HB_GrowCassette`; `HB_GrowLid` (hinge y 1.655, z 0.49; 0–100°); `HB_GrowTray` (slide along z, 0–0.30 m); `HB_GrowSeedlings` (same pivot as the tray, swap or hide per growth stage) | 11,826 | 1.00 × 1.65 (deck +0.40 to +2.05) × 0.51 | GrowInteraction, GrowTray |
| **Total** | 7 meshes, 7 anchor empties | **37,210** | | |

[`hb_manifest.json`](hb_manifest.json) lists:
- every anchor's point, its forward direction and its purpose;
- each unit's clearances and ventilation paths;
- every material with its maps.

[`hb_build_report.json`](hb_build_report.json) lists bounds and triangle counts per material.

## Coordinates and mounting

- **Unity space:** metres, y up.
- **Facing:** each unit's **forward (+z) points into the room**.
- **Origin:** on the **wall plane at deck level**, under the unit's centre line.
- **Mounting:** rotate about y only. For a −x wall use +90°, for a +x wall use −90°.
- **Blender ↔ Unity:** Blender (x, y, z) = Unity (x, z, y). Export uses `bake_space_transform`, axis_forward −Z and axis_up Y, the same as the AURORA-7 ship pipeline.
- **FBX contents:** meshes and anchor empties in a flat hierarchy. No cameras, lights or texture records; materials bind by name.
- **Rear faces:** closed by a graphite wall plate. Two cable and duct ends meet the wall plane; one goes 1.4 mm into it.
- **Collision:** none is included. Collision-safe placement is root's.

## Sleep brief applied (Codex 16:19 UTC, from NASA OCHMO-TB-041 and ESA)

- **Restraints:** the bag is secured to a quilted back pad by D-rings. Three restraint straps with amber **PULL quick-release tabs** sit at chest, waist and knees. **No strap crosses the eyes, face or neck.** The SleepCamera render shows the face is clear.
- **Ventilation stays on in quiet hours:**
  - a supply fan in the hood blows down past the face, fed by a visible duct from the wall;
  - two side vents on each side;
  - a return grille low in the plinth;
  - an "AIRFLOW · DO NOT BLOCK" label.
- **Light:** the reading lamp on a gooseneck is dim and warm (`HB_BerthLight`). The fan and light switches have a small cyan status light.
- **Head pad:** a small tan pad above the bag that the sleeper may use. It is **optional support**, not a required device.
- **Personal items are retained:**
  - a tablet in a holder with an elastic strap;
  - photos under an elastic band;
  - a headset on a hook;
  - a toiletry kit;
  - personal, clothing and linen lockers.
- **Not depicted:** no person is shown sleeping, no vacuum and no medication. The walkable ship simplifies gravity. This berth follows microgravity practice as requested and is **not** a validated spacecraft layout.

## Galley and grow cassette

- **Galley:**
  - An open ration drawer of labelled foil pouches.
  - A work recess with task light, bungee cords holding pouches, Velcro patches and tethered spoon, fork and scissors.
  - A **potable water dispenser** with HOT/AMB buttons, volume selector, caution label and status light. Below it is an open dispensing bay with the needle nozzle, a drip pad and a drink bag waiting in its clip.
  - A wet-trash bag on a rail and a strapped food warmer.
  - The fold-down table carries a tray with the pouches under bungees.
- **Grow cassette:**
  - A grow-light housing with heat sink, fan and "LIGHT 16 h · DARK 8 h" label.
  - Pleated reflective bellows and a hinged clear lid with an amber handle.
  - A slide-out tray A with six root pillows and young leafy seedlings.
  - A root/water reservoir with a level window, FILL port and DRAIN valve, plus a control panel with a status light.
  - No plant growth is implied by the model. Growth time and resources are gameplay's.

## Materials

- **Shared with the ship:** the 20 `A7_*` materials in [`source/hb_materials.json`](source/hb_materials.json) are the AURORA-7 materials with the same values and maps. Bind them to the existing ship materials; copies of their maps are in `textures/`, byte-identical.
- **New:** `HB_Labels` (label atlas, cutout), `HB_Leaf`, `HB_BagQuilt`, `HB_GrowLight`, `HB_BerthLight`, `HB_Water` and `HB_ScreenOff`.
- **Typography:** labels use a humanist sans-serif with matte, non-glowing text, in ivory, graphite and amber. Food labels carry lot codes, not brands. There is no insignia.

## Checks actually run

| Check | Result |
|---|---|
| Blender 5.3.0 build (`build_habitat.py`) | exit 0, 3 FBX files, `.blend` saved with relative texture paths |
| FBX round trip (`roundtrip_habitat.py`): every object and anchor re-imported | **PASS** for 14/14: exact triangle counts; pivots, bounds and anchors within 1 mm; material slots match ([`hb_roundtrip.json`](hb_roundtrip.json)) |
| Mirrored labels (`check_labels.py`), against the ship's label convention | 0 of 102 label triangles mirrored |
| Cycles renders (proposed placement) | 5 stills, inspected wide and first person |

**Not verified:** Unity import, collision, the final placement, performance in the game.

## Rebuild

```
python source/hb_textures.py <out>/textures <Aurora7/Textures>
blender -b --factory-startup --python source/build_habitat.py -- --out <out>
blender -b --factory-startup --python source/roundtrip_habitat.py -- --out <out>
python source/check_labels.py <out>
python source/make_manifest.py <out>
blender -b --factory-startup --python source/build_habitat.py -- --out <out> --render --ship-source <Aurora7/Source> --ship-textures <Aurora7/Textures>
```

`a7kit.py` and `a7_textures.py` are copied unchanged from `Assets/_OutOfReach/Visuals/Models/Aurora7/Source`. The Aurora7 source and every runtime, scene and collision file stay untouched.
