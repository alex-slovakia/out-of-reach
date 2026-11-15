"""make_sheet - finishes the review renders and builds a 2 x 2 contact sheet (1280 x 720).

Each view was rendered twice by build_terrain.py: <P>_<view>_raw.png (Cycles, AgX) and <P>_<view>_depth.png (haze
fraction 1 - exp(-distance / 10 km) as linear grey, alpha = geometry coverage). The preview haze is mixed in display
space towards a light sky grey; the sky itself is left as rendered. Output: <P>_<view>.png and <P>_sheet.png.
Preview only: Unity uses root's own fog and lighting.

Run: python make_sheet.py <renders dir> [prefix LT|LT2]
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

VIEWS = ["deck_north", "deck_east", "aerial", "ground"]
HAZE = np.array([203.0, 212.0, 220.0])
d = os.path.abspath(sys.argv[1])
prefix = sys.argv[2] if len(sys.argv) > 2 else "LT"
sheet = Image.new("RGB", (1280, 720), (18, 18, 20))
draw = ImageDraw.Draw(sheet)
for i, name in enumerate(VIEWS):
    raw = np.asarray(Image.open(os.path.join(d, "%s_%s_raw.png" % (prefix, name))).convert("RGB"), float)
    dep = np.asarray(Image.open(os.path.join(d, "%s_%s_depth.png" % (prefix, name))).convert("RGBA"), float) / 255.0
    g = dep[..., 0]
    f = (np.where(g <= 0.04045, g / 12.92, ((g + 0.055) / 1.055) ** 2.4) * dep[..., 3])[..., None]
    out = Image.fromarray(np.clip(raw * (1.0 - f) + HAZE * f + 0.5, 0, 255).astype(np.uint8), "RGB")
    out.save(os.path.join(d, "%s_%s.png" % (prefix, name)), optimize=True)
    im = out.resize((640, 360), Image.LANCZOS)
    x, y = (i % 2) * 640, (i // 2) * 360
    sheet.paste(im, (x, y))
    draw.rectangle((x + 8, y + 8, x + 8 + 7 * len(name) + 10, y + 26), fill=(0, 0, 0))
    draw.text((x + 13, y + 11), name, fill=(240, 240, 236))
sheet.save(os.path.join(d, "%s_sheet.png" % prefix))
print("sheet", sheet.size)
