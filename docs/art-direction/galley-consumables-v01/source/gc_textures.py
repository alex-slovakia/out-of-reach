"""gc_textures - label atlas for GALLEY-PROPS-01 (original work, generated).

Writes <out>/GC_Labels.png and gc_labels.json: the meal and drink pouch labels, the scissors cut line, a FILL mark for
the septum adapter, Velcro patches and a 100 mm scale label for the review renders. Generic names and lot codes only,
no brand, no agency insignia. Text uses Inter (SIL OFL 1.1) shipped with Blender; only rendered pixels are used.

Run: python gc_textures.py <out dir>
"""
import os
import sys

from PIL import ImageDraw

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import a7_textures as tx  # noqa: E402

OUT = os.path.abspath(sys.argv[1])
tx.OUT = OUT
os.makedirs(OUT, exist_ok=True)
PPM = 3000.0   # finer than the ship's 1800 px/m: these labels are read at hand distance

IVORY = (236, 233, 222, 255)
INK = tx.INK
GRAPHITE = (40, 42, 46, 255)
AMBER = (214, 150, 40, 255)
CYAN = (40, 170, 200, 255)
STEW = (150, 84, 40, 255)


def ration(title, line1, line2, code, band):
    def paint(tile, draw, w, h):
        draw.rounded_rectangle((0, 0, w - 1, h - 1), radius=int(w * 0.05), fill=IVORY)
        draw.rectangle((0, 0, w - 1, int(h * 0.20)), fill=band)
        f1, b1 = tx.fit_text(draw, title, w * 0.86, h * 0.22, "Bold")
        draw.text(((w - (b1[2] - b1[0])) / 2 - b1[0], h * 0.27 - b1[1]), title, font=f1, fill=INK)
        for text, y, size in ((line1, 0.53, 0.12), (line2, 0.68, 0.10)):
            f2, b2 = tx.fit_text(draw, text, w * 0.86, h * size, "Medium")
            draw.text(((w - (b2[2] - b2[0])) / 2 - b2[0], h * y - b2[1]), text, font=f2, fill=INK)
        f3, b3 = tx.fit_text(draw, code, w * 0.60, h * 0.09, "Regular", mono=True)
        draw.text((w * 0.07 - b3[0], h * 0.85 - b3[1]), code, font=f3, fill=(70, 72, 76, 255))
        draw.rounded_rectangle((1, 1, w - 2, h - 2), radius=int(w * 0.05), outline=(150, 150, 146, 255), width=2)
    return paint


def cut_line(tile, draw, w, h):
    """Dashed cut line with a small scissors mark and CUT HERE, printed on the foil above the label."""
    y = h * 0.62
    x = w * 0.31
    while x < w * 0.98:
        draw.rectangle((x, y - 1.5, min(x + w * 0.035, w * 0.98), y + 1.5), fill=INK)
        x += w * 0.06
    f, b = tx.fit_text(draw, "CUT HERE", w * 0.17, h * 0.70, "Bold")
    draw.text((w * 0.02 - b[0], (h - (b[3] - b[1])) / 2 - b[1]), "CUT HERE", font=f, fill=INK)
    cx, cy, r = w * 0.285, y, h * 0.22
    for dy in (-1, 1):
        draw.ellipse((cx - 2.4 * r, cy + dy * r * 0.9 - r * 0.7, cx - 1.0 * r, cy + dy * r * 0.9 + r * 0.7),
                     outline=INK, width=2)
        draw.line((cx - 1.0 * r, cy + dy * r * 0.6, cx + 0.8 * r, cy - dy * r * 0.3), fill=INK, width=2)


def scale_bar(tile, draw, w, h):
    draw.rectangle((0, 0, w - 1, h - 1), fill=GRAPHITE)
    for k in range(11):
        x = (w - 1) * k / 10
        draw.rectangle((x - 1, 0, x + 1, h * (0.55 if k % 5 == 0 else 0.30)), fill=IVORY)
    f, b = tx.fit_text(draw, "100 mm", w * 0.3, h * 0.36, "Bold")
    draw.text(((w - (b[2] - b[0])) / 2 - b[0], h * 0.58 - b[1]), "100 mm", font=f, fill=IVORY)


def build():
    atlas = tx.Atlas(1024, PPM)
    atlas.add("meal_front", 0.100, 0.072, ration("MEAL 04", "VEGETABLE STEW", "THERMOSTABILISED · EAT WARM",
                                                 "LOT 26-031-04 · NET 227 g", STEW))
    atlas.add("meal_cut", 0.110, 0.010, cut_line)
    atlas.add("drink_front", 0.078, 0.064, ration("DRINK 07", "CITRUS", "ADD 240 ml AMB", "LOT 26-029-07", CYAN))
    atlas.add("fill", 0.026, 0.009, tx.plate(IVORY, INK, "FILL", "Bold", None, 0.25, 0.70))
    atlas.add("velcro", 0.040, 0.028, tx.velcro((205, 202, 192, 255)))
    atlas.add("scale_100", 0.100, 0.012, scale_bar)
    atlas.save("GC_Labels")


if __name__ == "__main__":
    print("font:", tx.INTER or "Pillow embedded Aileron (CC0)")
    build()
