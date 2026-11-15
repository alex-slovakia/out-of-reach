"""lss_textures - LAND-SURROUNDINGS-V01: one original procedural vegetation set (LSS_Vegetation_*), 1024 px for a
4 m tile. Neutral leaf/grass mottling; the materials tint it (dry grass, scrub, canopy).

Run: python lss_textures.py <textures dir>
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import a7_textures as tx  # noqa: E402

tx.OUT = os.path.abspath(sys.argv[1])


def gen_vegetation(n=1024):
    rng = np.random.default_rng(311)
    leaves = tx.fbm(n, 220, 3, rng)
    clumps = tx.fbm(n, 28, 4, rng)
    patches = tx.fbm(n, 5, 4, rng)
    height = 0.7 * leaves + 0.6 * clumps
    tone = 0.56 + 0.16 * (leaves - 0.5) + 0.12 * (clumps - 0.5) + 0.06 * (patches - 0.5)
    albedo = np.stack([tone * 0.97, tone * 1.0, tone * 0.86], axis=-1)
    tx.save_set("LSS_Vegetation", albedo, height, 0.0, 0.75 + 0.25 * clumps, 0.10 + 0.05 * leaves, 2.2)


if __name__ == "__main__":
    os.makedirs(tx.OUT, exist_ok=True)
    gen_vegetation()
