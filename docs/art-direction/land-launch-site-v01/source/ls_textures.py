"""ls_textures - LAND-LAUNCH-SITE-01: original procedural texture sets and the markings atlas.

- LS_Concrete_*  pad and apron concrete; one tile is 4 m with a 12 mm sawn control joint on its edges
- LS_Gravel_*    crawlerway and road aggregate; one tile is 2 m
- LS_Markings    1024 atlas (64 px per metre): hazard band, white and yellow lines, the fictional "LC-7" pad
                 number and the "AURORA-7" mission mark; no agency logos or insignia
Everything is generated here from noise and text (Inter, SIL OFL 1.1, as shipped with Blender; pixels only).

Run: python ls_textures.py <textures dir>
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import a7_textures as tx  # noqa: E402

tx.OUT = os.path.abspath(sys.argv[1])
WHITE = (236, 234, 228, 255)


def gen_concrete(n=1024):
    rng = np.random.default_rng(71)
    mott = tx.fbm(n, 6, 5, rng)
    fine = tx.fbm(n, 192, 2, rng)
    stain = tx.fbm(n, 3, 4, rng)
    u, v = tx.uv_grid(n)
    edge = np.minimum(np.minimum(u, 1 - u), np.minimum(v, 1 - v)) * 4.0
    joint = np.clip(1.0 - edge / 0.012, 0, 1)
    height = 0.6 * fine + 0.3 * mott - 1.4 * joint
    tone = 0.66 + 0.07 * (mott - 0.5) + 0.05 * (fine - 0.5) - 0.10 * np.clip(stain - 0.6, 0, 1) - 0.25 * joint
    smooth = 0.20 + 0.06 * (mott - 0.5)
    tx.save_set("LS_Concrete", tx.gray(tone), height, 0.0, 1.0 - 0.45 * joint, smooth, 2.4)


def gen_gravel(n=1024):
    rng = np.random.default_rng(72)
    stones = tx.fbm(n, 160, 2, rng)
    pebbles = tx.fbm(n, 60, 2, rng)
    tint = tx.fbm(n, 4, 4, rng)
    height = 0.8 * stones + 0.5 * pebbles
    tone = 0.55 + 0.18 * (stones - 0.5) + 0.06 * (tint - 0.5)
    albedo = np.stack([tone * 1.02, tone * 0.98, tone * 0.92], axis=-1)
    tx.save_set("LS_Gravel", albedo, height, 0.0, 0.8 + 0.2 * stones, 0.12 + 0.05 * pebbles, 3.0)


def bar(colour):
    def paint(tile, draw, w, h):
        draw.rectangle((0, 0, w, h), fill=colour)
    return paint


def build_markings():
    atlas = tx.Atlas(1024, 64)
    atlas.add("hazard_band", 4.0, 0.40, tx.hazard())
    atlas.add("line_white", 4.0, 0.15, bar(WHITE))
    atlas.add("line_yellow", 4.0, 0.15, bar(tx.YELLOW))
    atlas.add("pad_number", 9.0, 3.6, tx.stencil("LC-7", fill=WHITE))
    atlas.add("mission_mark", 12.0, 2.4, tx.stencil("AURORA-7", fill=WHITE))
    atlas.add("keep_clear", 7.0, 0.9, tx.stencil("BLAST AREA · KEEP CLEAR", fill=tx.YELLOW))
    atlas.save("LS_Markings")


if __name__ == "__main__":
    gen_concrete()
    gen_gravel()
    build_markings()
    print("font:", tx.INTER or "Pillow embedded Aileron (CC0)")
