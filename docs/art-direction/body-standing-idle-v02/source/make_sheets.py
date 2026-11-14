"""make_sheets - STANDING-IDLE-UNIT-02: the two evidence sheets from the renders (Python 3 + Pillow).

renders/STANDING_IDLE_sheet.png  Blender, same layout as STANDING-IDLE-01: rows = pose and setback, columns = eye view,
                                 region mask (red torso, yellow hips, blue legs, green boots), side view with the eye rays.
renders/UNITY_native_sheet.png   Unity 6000.3.28f1 captures of the imported clips on the canonical bodies: rows = pose and
                                 setback, columns = the same eye view and the side view; the last row is the delivered v01
                                 clip in the same project (the unit bug root found), for contrast.
Run: python make_sheets.py <delivery dir>
"""
import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

D = sys.argv[1]
R = os.path.join(D, "renders")
CELL, HEAD = (480, 270), 30
FONT = ImageFont.load_default(size=19)
TITLE = ImageFont.load_default(size=24)


def cell(path):
    im = Image.open(path).convert("RGBA")
    bg = Image.new("RGBA", im.size, (255, 255, 255, 255))
    bg.alpha_composite(im)
    return bg.convert("RGB").resize(CELL, Image.LANCZOS)


def sheet(title, rows, cols, out):
    w = CELL[0] * len(cols)
    h = 44 + len(rows) * (CELL[1] + HEAD)
    img = Image.new("RGB", (w, h), (246, 246, 244))
    d = ImageDraw.Draw(img)
    d.text((10, 9), title, fill=(20, 20, 20), font=TITLE)
    y = 44
    for label, stem in rows:
        d.text((10, y + 5), label, fill=(20, 20, 20), font=FONT)
        y += HEAD
        for i, suffix in enumerate(cols):
            img.paste(cell(os.path.join(R, stem + suffix)), (i * CELL[0], y))
        y += CELL[1]
    img.save(os.path.join(R, out), optimize=True)
    print("wrote", out, img.size)


blender = json.load(open(os.path.join(D, "standing_idle_report.json")))["evidence"]
unity = json.load(open(os.path.join(D, "unity_skin_check.json")))["outfits"]


def blabel(name, text):
    e = blender[name]
    return "%s - boots %d/%d visible, eye to skin %.0f mm" % (text, e["boots_sight"]["visible"],
                                                               e["boots_sight"]["boot_vertices"], e["eye_to_nearest_skin_mm"])


def ulabel(outfit, pose, setback, text):
    e = unity[outfit]["lookdown"][pose]["setback%03d" % setback]
    return "%s - boots %d/%d visible, eye to skin %.0f mm" % (text, e["boots_visible"], e["boot_vertices"],
                                                               e["eye_to_nearest_skin_mm"])


rows = []
for outfit, pose, text in (("WORK", "StandRelaxed", "WORK, published StandRelaxed"), ("WORK", "StandIdle", "WORK, StandIdle V02"),
                           ("EVA", "StandIdle", "EVA, StandIdle V02")):
    for sb in (330, 197):
        stem = "%s_%s_setback%03d" % (outfit, pose, sb)
        rows.append((blabel(stem, "%s, setback 0.%03d m" % (text, sb)), stem))
sheet("Blender 5.3, same eye 1.65 m, 80 deg down, vFOV 68 deg (columns: eye, regions, side)", rows,
      ["_eye.png", "_eye_regions.png", "_side.png"], "STANDING_IDLE_sheet.png")

rows = []
for outfit, pose, text in (("WORK", "StandRelaxed", "WORK, published StandRelaxed"), ("WORK", "V02", "WORK, BODY_StandIdle_V02"),
                           ("EVA", "V02", "EVA, BODY_StandIdle_V02")):
    for sb in (330, 197):
        rows.append((ulabel(outfit, pose, sb, "%s, setback 0.%03d m" % (text, sb)), "%s_%s_setback%03d" % (outfit, pose, sb)))
r = unity["WORK"]["meshes"]["A7_WORK_TorsoLegs"]["V01"]
rows.append(("WORK, delivered v01 clip in the same project: rig scale 100, TorsoLegs %.0f x %.0f x %.0f m (camera pulled back)"
             % tuple(r["unity_world_size"]), "WORK_V01_setback330"))
sheet("Unity 6000.3.28f1 native, canonical bodies (columns: same eye, side)", rows,
      ["_eye_unity.png", "_side_unity.png"], "UNITY_native_sheet.png")
