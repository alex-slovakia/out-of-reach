"""make_sheets - GALLEY-USE-MOTION-01: contact sheets from the Blender frames (Pillow).

Run: python make_sheets.py <renders folder>
Top row of each clip sheet: witness view beside the player (eye and assumed mouth as markers); bottom row: the
first-person view (game camera, vertical FOV 68). The study sheet shows the left index on the closed and on the
open clamp, and the rejected fist round the adapter.
"""
import os
import sys

from PIL import Image

folder = sys.argv[1]


def sheet(names, cols, size, out):
    tiles = [Image.open(os.path.join(folder, n)).convert("RGB").resize(size) for n in names]
    rows = (len(tiles) + cols - 1) // cols
    img = Image.new("RGB", (cols * size[0], rows * size[1]), (20, 20, 20))
    for i, t in enumerate(tiles):
        img.paste(t, ((i % cols) * size[0], (i // cols) * size[1]))
    img.save(os.path.join(folder, out))


for clip in ("meal", "drink"):
    sheet(["GU_%s_%s_%d.png" % (clip, v, k) for v in ("witness", "firstperson") for k in (1, 2, 3, 4)], 4, (480, 360),
          "GU_%s_sheet.png" % clip)
sheet(["GU_study_press_closed.png", "GU_study_press_open.png", "GU_study_rejected_fist.png"], 3, (640, 480),
      "GU_study_sheet.png")
print("sheets written")
