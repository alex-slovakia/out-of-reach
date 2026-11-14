"""hb_textures - textures for HABITAT-DETAILS-V1 (original work, generated).

Writes into <out>:
- HB_Labels.png + hb_labels.json   label atlas of the kit (functional service labels, ration labels, photos)
- HB_Leaf_* set                     young leaf (albedo, normal, MODS) for the seedlings
- copies of the ship's tiling sets the kit shares (A7_Paint, A7_Fabric, A7_Quilt, A7_Rubber, A7_Anodized,
  A7_Brushed, A7_Grate, A7_MLI), so the package is complete on its own. They come from the same generator
  (a7_textures.py, copied here unchanged) and are byte-identical to the ship's maps.

Text is drawn with Inter (SIL Open Font License 1.1), the font Blender ships; only the rendered pixels are used.

Run: python hb_textures.py <out dir> <ship Textures dir>
"""
import math
import os
import shutil
import sys

import numpy as np
from PIL import Image, ImageDraw

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import a7_textures as tx  # noqa: E402

OUT = os.path.abspath(sys.argv[1])
SHIP_TEXTURES = os.path.abspath(sys.argv[2])
tx.OUT = OUT
os.makedirs(OUT, exist_ok=True)

IVORY = (236, 233, 222, 255)
INK = tx.INK
GRAPHITE = (40, 42, 46, 255)
AMBER = (214, 150, 40, 255)
CYAN = (40, 170, 200, 255)


# ----------------------------------------------------------------------------- label painters
def ivory_plate(text, weight="SemiBold", max_height=0.56):
    return tx.plate(IVORY, INK, text, weight, None, 0.10, max_height)


def graphite_plate(text, weight="SemiBold", max_height=0.56):
    return tx.plate(GRAPHITE, IVORY, text, weight, None, 0.10, max_height)


def amber_tab(text):
    """Quick-release pull tab: amber with dark text, the colour the brief asks to read at a glance."""
    def paint(tile, draw, w, h):
        draw.rounded_rectangle((0, 0, w - 1, h - 1), radius=int(min(w, h) * 0.30), fill=AMBER)
        tx.centered(draw, text, w, h, INK, "Bold", margin=0.10, max_height=0.60)
    return paint


def ration(title, subtitle, code, band):
    """Ration pouch label: coloured band, name, preparation line and a lot code. No brand, no insignia."""
    def paint(tile, draw, w, h):
        draw.rounded_rectangle((0, 0, w - 1, h - 1), radius=int(w * 0.05), fill=IVORY)
        draw.rectangle((0, 0, w - 1, int(h * 0.22)), fill=band)
        f1, b1 = tx.fit_text(draw, title, w * 0.86, h * 0.26, "Bold")
        draw.text(((w - (b1[2] - b1[0])) / 2 - b1[0], h * 0.32 - b1[1]), title, font=f1, fill=INK)
        f2, b2 = tx.fit_text(draw, subtitle, w * 0.86, h * 0.14, "Medium")
        draw.text(((w - (b2[2] - b2[0])) / 2 - b2[0], h * 0.62 - b2[1]), subtitle, font=f2, fill=INK)
        f3, b3 = tx.fit_text(draw, code, w * 0.60, h * 0.11, "Regular", mono=True)
        draw.text((w * 0.08 - b3[0], h * 0.82 - b3[1]), code, font=f3, fill=(70, 72, 76, 255))
        draw.rounded_rectangle((1, 1, w - 2, h - 2), radius=int(w * 0.05), outline=(150, 150, 146, 255), width=2)
    return paint


def level_strip(tile, draw, w, h):
    """Water level scale for the reservoir window: ticks every tenth, MAX and MIN."""
    draw.rectangle((0, 0, w - 1, h - 1), fill=(0, 0, 0, 0))
    for k in range(11):
        y = h * 0.08 + (h * 0.84) * k / 10
        length = w * (0.55 if k % 5 == 0 else 0.30)
        draw.rectangle((0, y - 2, length, y + 2), fill=INK)
    f, b = tx.fit_text(draw, "MAX", w * 0.9, h * 0.06, "Bold")
    draw.text((w * 0.05 - b[0], h * 0.005 - b[1]), "MAX", font=f, fill=INK)
    draw.text((w * 0.05 - b[0], h * 0.94 - b[1]), "MIN", font=f, fill=INK)


def photo(seed, sky, ground, feature):
    """A small printed photo with a white border: a plain landscape, no people, no recognisable place."""
    def paint(tile, draw, w, h):
        rng = np.random.default_rng(seed)
        draw.rectangle((0, 0, w - 1, h - 1), fill=(244, 242, 236, 255))
        b = int(min(w, h) * 0.07)
        iw, ih = w - 2 * b, h - 2 * b
        top = np.array(sky[0], dtype=float)
        bottom = np.array(sky[1], dtype=float)
        horizon = int(ih * (0.55 + 0.1 * rng.random()))
        for y in range(horizon):
            c = top + (bottom - top) * y / max(1, horizon - 1)
            draw.line((b, b + y, b + iw - 1, b + y), fill=tuple(int(v) for v in c) + (255,))
        g0, g1 = np.array(ground[0], dtype=float), np.array(ground[1], dtype=float)
        for y in range(horizon, ih):
            c = g0 + (g1 - g0) * (y - horizon) / max(1, ih - horizon - 1)
            draw.line((b, b + y, b + iw - 1, b + y), fill=tuple(int(v) for v in c) + (255,))
        if feature == "hills":
            pts = [(b, b + horizon)]
            for k in range(9):
                pts.append((b + iw * k / 8, b + horizon - ih * (0.08 + 0.18 * rng.random())))
            pts.append((b + iw - 1, b + horizon))
            draw.polygon(pts, fill=tuple(int(v * 0.8) for v in ground[0]) + (255,))
        elif feature == "sun":
            r = ih * 0.12
            cx, cy = b + iw * (0.3 + 0.4 * rng.random()), b + horizon - r * 0.4
            draw.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(250, 214, 150, 255))
            draw.rectangle((b, b + horizon, b + iw - 1, b + ih - 1), fill=tuple(int(v) for v in ground[0]) + (255,))
            for k in range(6):
                y = b + horizon + (ih - horizon) * (k + 1) / 7
                draw.line((cx - r * (1.6 - k * 0.2), y, cx + r * (1.6 - k * 0.2), y), fill=(240, 196, 140, 255), width=2)
        elif feature == "trees":
            for k in range(14):
                x = b + iw * rng.random()
                s = ih * (0.10 + 0.12 * rng.random())
                y = b + horizon + ih * 0.05 * rng.random()
                draw.polygon([(x, y - s), (x - s * 0.35, y), (x + s * 0.35, y)], fill=(38, 74, 52, 255))
    return paint


# ----------------------------------------------------------------------------- atlas
def build_labels():
    atlas = tx.Atlas(2048, tx.PPM)
    add = atlas.add
    # crew berth
    add("berth_name", 0.30, 0.05, graphite_plate("CREW BERTH 1"))
    add("airflow", 0.20, 0.032, ivory_plate("AIRFLOW · DO NOT BLOCK"))
    add("fan", 0.05, 0.02, ivory_plate("FAN", "Bold"))
    add("light", 0.05, 0.02, ivory_plate("LIGHT", "Bold"))
    add("release", 0.045, 0.022, amber_tab("PULL"))
    add("stow_clothing", 0.16, 0.034, ivory_plate("CLOTHING"))
    add("stow_personal", 0.16, 0.034, ivory_plate("PERSONAL"))
    add("stow_linen", 0.16, 0.034, ivory_plate("LINEN · SPARES"))
    add("head_pad", 0.14, 0.026, ivory_plate("HEAD PAD · OPTIONAL"))
    # galley
    add("galley_name", 0.24, 0.05, graphite_plate("GALLEY"))
    add("rations_meals", 0.22, 0.036, ivory_plate("RATIONS · MEALS"))
    add("rations_drinks", 0.22, 0.036, ivory_plate("DRINKS · SNACKS"))
    add("potable", 0.20, 0.034, graphite_plate("POTABLE WATER"))
    add("hot", 0.05, 0.022, tx.plate(AMBER, INK, "HOT", "Bold"))
    add("amb", 0.05, 0.022, tx.plate(IVORY, INK, "AMB", "Bold"))
    add("volume", 0.10, 0.02, ivory_plate("50 · 100 · 250 ml"))
    add("caution_hot", 0.11, 0.024, tx.plate(tx.YELLOW, INK, "CAUTION HOT", "Bold"))
    add("wet_trash", 0.16, 0.034, ivory_plate("WET TRASH"))
    add("food_warmer", 0.16, 0.03, ivory_plate("FOOD WARMER"))
    # grow cassette
    add("grow_name", 0.22, 0.045, graphite_plate("GROW UNIT"))
    add("grow_light", 0.16, 0.03, ivory_plate("GROW LIGHT"))
    add("light_cycle", 0.20, 0.03, ivory_plate("LIGHT 16 h · DARK 8 h"))
    add("fill", 0.06, 0.024, ivory_plate("FILL", "Bold"))
    add("drain", 0.07, 0.024, ivory_plate("DRAIN", "Bold"))
    add("tray", 0.08, 0.024, ivory_plate("TRAY A", "Bold"))
    add("level", 0.03, 0.20, level_strip)
    # rations and personal items
    add("pouch_meal", 0.10, 0.07, ration("MEAL 04", "REHYDRATE · 120 ml HOT", "LOT 26-031-04", (160, 92, 40, 255)))
    add("pouch_breakfast", 0.10, 0.07, ration("BREAKFAST 02", "THERMOSTABILISED", "LOT 26-031-02", (200, 160, 60, 255)))
    add("pouch_snack", 0.08, 0.06, ration("SNACK 07", "READY TO EAT", "LOT 26-030-07", (110, 130, 90, 255)))
    add("pouch_drink", 0.08, 0.06, ration("DRINK BAG", "400 ml · POTABLE", "LOT 26-029-11", CYAN))
    add("velcro_dark", 0.05, 0.03, tx.velcro((44, 46, 50, 255)))
    add("velcro_light", 0.05, 0.03, tx.velcro((205, 202, 192, 255)))
    add("photo_sea", 0.09, 0.065, photo(3, ((120, 150, 190), (236, 190, 150)), ((70, 100, 130), (40, 60, 90)), "sun"))
    add("photo_hills", 0.09, 0.065, photo(5, ((150, 190, 225), (215, 230, 240)), ((110, 140, 80), (70, 100, 60)), "hills"))
    add("photo_forest", 0.065, 0.09, photo(9, ((190, 210, 230), (230, 236, 240)), ((96, 120, 84), (60, 84, 60)), "trees"))
    atlas.save("HB_Labels")


# ----------------------------------------------------------------------------- leaf set
def gen_leaf(n=512):
    """Young leafy-green leaf: lighter midrib and veins, soft mottling; one leaf fills the 0..1 UV square."""
    rng = np.random.default_rng(41)
    u, v = tx.uv_grid(n)
    mid = np.exp(-((u - 0.5) / 0.012) ** 2)
    veins = np.zeros((n, n))
    for k in range(1, 9):
        y0 = k / 9.0
        for side in (-1, 1):
            dist = np.abs((v - y0) - side * (u - 0.5) * 0.55)
            veins = np.maximum(veins, np.exp(-(dist / 0.006) ** 2) * (np.abs(u - 0.5) < 0.45))
    mottling = tx.fbm(n, 6, 4, rng)
    base = np.array([0.24, 0.46, 0.16])
    light = np.array([0.52, 0.72, 0.34])
    t = np.clip(0.25 * mottling + 0.55 * mid + 0.30 * veins + 0.25 * v, 0, 1)
    albedo = base[None, None, :] * (1 - t[..., None]) + light[None, None, :] * t[..., None]
    height = 0.6 * mid + 0.35 * veins + 0.1 * mottling
    tx.save_set("HB_Leaf", albedo, height, 0.0, 1.0, 0.40 + 0.1 * mid, 2.5)


def copy_ship_sets():
    for name in ("A7_Paint", "A7_Fabric", "A7_Quilt", "A7_Rubber", "A7_Anodized", "A7_Brushed", "A7_Grate", "A7_MLI"):
        for suffix in ("_BaseColor.png", "_Normal.png", "_MODS.png"):
            shutil.copyfile(os.path.join(SHIP_TEXTURES, name + suffix), os.path.join(OUT, name + suffix))
    print("  copied 8 shared ship sets")


if __name__ == "__main__":
    print("font:", tx.INTER or "Pillow embedded Aileron (CC0)")
    build_labels()
    gen_leaf()
    copy_ship_sets()
    print("done ->", OUT)
