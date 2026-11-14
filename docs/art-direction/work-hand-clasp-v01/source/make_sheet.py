"""make_sheet - WORK-HAND-CLASP-01: one contact sheet of the 12 Blender stills (Pillow).

Run: python make_sheet.py <renders folder>
Rows: drink R, drink L, meal R, meal L. Columns: front (label side, thumb), back (fingers), top (the aperture).
"""
import os
import sys

from PIL import Image

folder = sys.argv[1]
names = ["WORK_Clasp_%s_%s_%s.png" % (k, s, v) for k in ("Drink", "Meal") for s in ("R", "L") for v in ("front", "back", "top")]
tiles = [Image.open(os.path.join(folder, n)).convert("RGB").resize((480, 360)) for n in names]
sheet = Image.new("RGB", (3 * 480, 4 * 360), (20, 20, 20))
for i, t in enumerate(tiles):
    sheet.paste(t, ((i % 3) * 480, (i // 3) * 360))
sheet.save(os.path.join(folder, "WORK_Clasp_sheet.png"))
print("sheet written")
