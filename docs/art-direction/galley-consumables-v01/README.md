# GALLEY-PROPS-01 — meal pouch and drink pouch for the AURORA-7 galley

**Status:** an authored Blender art kit for review, by the Claude model writer on 14 November 2026 (#7, Codex offer 6882373318).
- It is **not integrated in Unity** and has no animation or gameplay.
- It serves the already-authorised ration and drink interactions; it is not a new mechanic.
- The existing habitat, the ship and every runtime file stay untouched.
- The renders below are genuine Blender 5.3 Cycles stills, not Unity captures.

![Lineup with 100 mm scale](renders/GC_blender_studio_lineup.png)

![On the habitat galley table, first person](renders/GC_blender_galley_table_fp.png)

Close-ups:
- [the opened meal pouch, mouth and stew](renders/GC_blender_studio_meal_open.png)
- [the drink pouch adapter, septum, straw, clamp and tethered cap](renders/GC_blender_studio_drink_detail.png)

## Props

Each prop has two variants. Both variants share the prop's origin (its centre), so they can be swapped in place.

| FBX | Variants (triangles) | Size (m) | Anchors |
|---|---|---|---|
| `GC_MealPouch.fbx` | `GC_MealPouch_Sealed` (1,736), `GC_MealPouch_Opened` (2,484) | 0.141 × 0.178 × 0.029; opened 0.141 × 0.139 | MealGrip, MealMouth |
| `GC_DrinkPouch.fbx` | `GC_DrinkPouch_Closed` (2,338), `GC_DrinkPouch_Open` (2,278) | 0.111 × 0.260 (with the straw) × 0.025 | DrinkGrip, DrinkStrawTip, DrinkFillPort |
| **Total** | 8,836 triangles | | |

**Sealed meal pouch.**
- A thermostabilised retort pouch in aluminium-laminate film.
- The front and back walls bulge and meet at flat heat seals.
- It has a crimped top seal, a "CUT HERE" scissors line, a printed label and a Velcro patch on the back.

**Opened meal pouch.**
- The top is cut off at 86 % of the body height.
- The walls end apart, so the mouth is a real opening.
- The inner foil and the stew surface show inside.

**Closed drink pouch.**
- A foil-laminate pouch with a rigid septum adapter and a FILL mark.
- A silicone septum sits on a boss.
- A polyethylene straw goes in through the septum.
- An amber pinch clamp squeezes the straw shut, and an amber cap sits on the tip, tethered to the adapter.

**Open drink pouch.**
- The clamp is slid down and sprung open.
- The cap hangs on its tether beside the adapter, and the straw end is free.

## Anchors

All anchors are in Unity metres, in the prop's own space. Forward and up are given in `gc_manifest.json`; the empties carry positions only.

| Anchor | Point | Forward | Meaning |
|---|---|---|---|
| MealGrip | (0, −0.020, 0) | +z | hand grip at the centre of the lower body |
| MealMouth | (0, 0.058, 0) | +y | centre of the cut opening (Opened) |
| DrinkGrip | (0, −0.020, 0) | +z | hand grip at the centre of the lower body |
| DrinkStrawTip | (−0.004, 0.160, 0.006) | along the straw | open straw end (Open; capped when Closed) |
| DrinkFillPort | (0, 0.0985, 0) | +y | silicone septum, where the dispenser needle injects water before the straw goes in |

## Coordinates and materials

- **Coordinates:** Unity metres, y up, label side +z, origin at the prop centre. Blender (x, y, z) = Unity (x, z, y), exported with `bake_space_transform`, axis_forward −Z and axis_up Y, the same as the AURORA-7 pipeline.
- **FBX contents:** meshes and anchor empties only, in a flat hierarchy. No cameras, lights or texture records.
- **Materials:** listed in `gc_manifest.json` with their maps and the suggested URP setup.
  - `A7_PlasticWhite` and `A7_Rubber` are the ship's own materials.
  - `GC_Foil` uses the ship's MLI maps at a finer tiling.
  - `GC_AmberPlastic` uses the paint maps.
  - `GC_Silicone`, `GC_StrawPE` (transparent) and `GC_Food` are flat materials.
  - `GC_Labels` is the label atlas with cutout alpha.
- **Labels:** printed onto the curved walls (grids that follow the bulge), not flat cards.

## Sources consulted for shape and use

No content was copied, and nothing here claims NASA endorsement.
- [NASA Facts, *Space Food*](https://www.nasa.gov/wp-content/uploads/2015/05/167750main_fs_spacefood508c.pdf): beverages in a foil-laminate pouch with a septum adapter; the straw goes in through the septum used for the water and is closed by a clamp.
- [NASA Facts, *Space Food* (JSC, 2002)](https://www.nasa.gov/wp-content/uploads/2016/07/71426main_fs-2002-10-079-jsc.pdf): thermostabilised food in flexible retort pouches.
- [M. Perchonok, *Feeding the Astronauts During Long Duration Missions*, NASA NTRS](https://ntrs.nasa.gov/api/citations/20100026032/downloads/20100026032.pdf): low-density polyethylene septum adapter, silicone septum, polyolefin/aluminium laminate.
- [NASA podcast *Houston We Have a Podcast — Space Food*](https://www.nasa.gov/podcasts/houston-we-have-a-podcast/space-food/): why the straw is clamped in microgravity.

The tethered amber cap is this kit's own addition, so that a resealed straw reads at a glance.

## Checks actually run

| Check | Result |
|---|---|
| Blender 5.3.0 build (`build_galley.py`) | exit 0, 2 FBX files, `.blend` with relative texture paths |
| FBX round trip (`roundtrip_habitat.py --report gc_build_report.json`) | **PASS 9/9**: exact triangle counts; pivots, bounds and anchors within 1 mm; material slots match |
| Unity 6000.3.28f1 import with the ship's importer settings (throwaway folder of the model clone, deleted afterwards) | all 4 variants and 5 anchors match the build report within 0.5 mm, with the same triangle counts and materials |
| Mirrored labels (`check_labels.py`) | 0 of 868 label triangles |
| Cycles renders | 4 stills, inspected |

**Not verified:** the in-game interaction, hand pose and performance. Those are Codex's integration.

## Rebuild

```
python source/gc_textures.py textures
blender -b --factory-startup --python source/build_galley.py -- --out .
blender -b --factory-startup --python source/roundtrip_habitat.py -- --out . --report gc_build_report.json
python source/check_labels.py textures/gc_labels.json
python source/make_manifest.py .
blender -b --factory-startup --python source/build_galley.py -- --out . --render --habitat-out <habitat build folder>
```

- **Copied unchanged:** `a7kit.py` and `a7_textures.py` come from the AURORA-7 source. `build_habitat.py`, `hb_kit.py` and `hb_materials.json` come from `docs/art-direction/habitat-v01/source` on `feature/habitat-details-v1`.
- **Why the habitat files are here:** they are shared helpers, and the galley context render needs them.
