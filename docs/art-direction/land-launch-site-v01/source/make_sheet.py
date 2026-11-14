"""make_sheet - 2 x 2 contact sheet of the four review renders (LS_sheet.png, 1280 x 720).

Run: python make_sheet.py <renders dir>
"""
import os
import sys

from PIL import Image, ImageDraw

VIEWS = ["overview", "crew_deck", "ground", "towards_building"]
d = os.path.abspath(sys.argv[1])
sheet = Image.new("RGB", (1280, 720), (18, 18, 20))
draw = ImageDraw.Draw(sheet)
for i, name in enumerate(VIEWS):
    im = Image.open(os.path.join(d, "LS_%s.png" % name)).convert("RGB").resize((640, 360), Image.LANCZOS)
    x, y = (i % 2) * 640, (i // 2) * 360
    sheet.paste(im, (x, y))
    draw.rectangle((x + 8, y + 8, x + 8 + 7 * len(name) + 10, y + 26), fill=(0, 0, 0))
    draw.text((x + 13, y + 11), name, fill=(240, 240, 236))
sheet.save(os.path.join(d, "LS_sheet.png"))
print("sheet", sheet.size)
