"""make_manifest - writes hb_manifest.json (materials, maps, objects, pivots, motions, anchors, clearances) from the
build report and the material list. Plain Python. Run: python make_manifest.py <out dir>"""
import json
import os
import sys

OUT = os.path.abspath(sys.argv[1])
HERE = os.path.dirname(os.path.abspath(__file__))
report = json.load(open(os.path.join(OUT, "hb_build_report.json"), encoding="utf-8"))
specs = json.load(open(os.path.join(HERE, "hb_materials.json"), encoding="utf-8"))["materials"]
roundtrip = json.load(open(os.path.join(OUT, "hb_roundtrip.json"), encoding="utf-8"))

PURPOSE = {
    "SleepEntry": "Deck point 0.80 m in front of the berth centre where the player stands to get in; forward faces the berth.",
    "SleepCamera": "Eye point inside the bag opening, 1.96 m above the deck and 0.24 m off the wall; forward looks out of "
                   "the berth. Nothing crosses the face from here (restraints sit at chest, waist and knees).",
    "PersonalStowage": "Paddle latch of the PERSONAL locker door; forward faces the user.",
    "RationPickup": "Above the pouches in the open ration drawer; forward faces the user.",
    "WaterDispenser": "Tip of the needle nozzle in the dispensing bay; forward faces the user (the nozzle points down).",
    "GrowInteraction": "Amber handle of the hinged clear lid; forward faces the user.",
    "GrowTray": "Top centre of the six root pillows on the tray; forward is up.",
}
CLEARANCE = {
    "HB_CrewBerth": {
        "envelope_m": "1.44 W x 2.40 H (2.55 with the supply duct to the wall) x 0.53 D",
        "berth_opening_m": "0.94 W x 2.10 H, bag 0.72 x 1.78 m on a quilted back pad",
        "front_access_m": "keep 0.90 m deep x 1.00 m wide free in front of the berth (SleepEntry at 0.80 m)",
        "ventilation": "supply fan in the hood above the face, two side vents per side, return grille in the plinth; "
                       "the duct leaves the hood top towards the wall. Do not block the hood, vents or plinth grille.",
    },
    "HB_Galley": {
        "envelope_m": "1.44 W x 2.40 H x 0.46 D; 0.72 with the ration drawer open, 0.80 with the table deployed",
        "front_access_m": "keep 0.90 m in front of the table edge when deployed",
        "work_heights_m": "table top 1.00, dispensing bay floor 0.99, nozzle tip 1.13, open drawer pouches 1.67-1.76",
    },
    "HB_GrowCassette": {
        "envelope_m": "1.00 W x 1.65 H (deck +0.40 to +2.05 incl. heat sink) x 0.51 D; tray slides out 0.30 more",
        "front_access_m": "keep 0.80 m in front of the lid for opening and tray service",
        "mounting": "rear plate to the wall, bottom 0.40 m above the deck; space below is free. A cable end enters "
                    "the wall plane by 1.4 mm.",
    },
}

maps = sorted(f for f in os.listdir(os.path.join(OUT, "textures")) if f.endswith((".png", ".json")))
materials = []
for spec in specs:
    entry = dict(spec)
    files = []
    if "set" in spec:
        files = [spec["set"] + s for s in ("_BaseColor.png", "_Normal.png", "_MODS.png")]
    if "atlas" in spec:
        files = [spec["atlas"] + ".png", "hb_labels.json"]
    entry["files"] = ["textures/" + f for f in files]
    missing = [f for f in files if f not in maps]
    if missing:
        raise SystemExit("material %s: missing maps %s" % (spec["name"], missing))
    entry["used"] = spec["name"] in report["materials"]
    materials.append(entry)

units = {}
for unit_name, info in report["units"].items():
    units[unit_name] = {
        "fbx": info["fbx"],
        "triangles": info["triangles"],
        "bounds_min_m": info["bounds_min"],
        "bounds_max_m": info["bounds_max"],
        "objects": {n: {"triangles": o["triangles"], "pivot_m": o["origin"], "motion": o.get("motion"),
                        "materials": sorted(o["per_material"])} for n, o in info["objects"].items()},
        "anchors": {n: dict(a, purpose=PURPOSE[n]) for n, a in info["anchors"].items()},
        "clearance": CLEARANCE[unit_name],
    }

manifest = {
    "kit": "HABITAT-DETAILS-V1",
    "status": "authored Blender art kit for review; not integrated in Unity, no animation or gameplay",
    "coordinates": "Unity space, metres, y up. Each unit's forward (+z) points into the room; its origin is on the "
                   "wall plane at deck level under the unit centre line. Mount by rotating about y only. Blender "
                   "(x, y, z) = Unity (x, z, y); exported with bake_space_transform, axis_forward -Z, axis_up Y.",
    "fbx_contents": "meshes and anchor empties only, flat hierarchy, no cameras, lights or texture records; "
                    "materials bind by name",
    "material_binding": "Materials marked shared_with_ship are the AURORA-7 ship materials (same values and maps): "
                        "bind them to the existing A7_* materials. HB_* are new.",
    "total_triangles": report["total_triangles"],
    "roundtrip": {"pass": roundtrip["pass"], "tolerance_m": roundtrip["tolerance_m"], "tool": "Blender " + roundtrip["blender"]},
    "units": units,
    "materials": materials,
}
with open(os.path.join(OUT, "hb_manifest.json"), "w", encoding="utf-8") as handle:
    json.dump(manifest, handle, indent=1)
print("hb_manifest.json:", len(units), "units,", sum(len(u["anchors"]) for u in units.values()), "anchors,",
      len(materials), "materials,", report["total_triangles"], "triangles")
