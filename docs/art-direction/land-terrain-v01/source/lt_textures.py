"""lt_textures - LAND-TERRAIN-V01 maps, original and procedural (NumPy + Pillow, fixed seeds, no downloads, no AI images).

  LT_Terrain_Macro.png          2048 x 2048 sRGB, UV0 of the terrain (lt_height.uv_axis): 3.125 m per texel over
                                x, z in [-3000, 3000]; the outer 64 px band holds the far ring out to 9 km. Every texel
                                is coloured from its world position and the same height field the mesh uses: dry grass,
                                greener grass, coastal scrub, sand on dune crests and blowouts, darker damp soil in
                                swales and ditches, burn-scar mosaic, a hardstand at the facility, maintained (mowed) ground inside the fence and
                                along the corridors, a blasted sand and soot fan in the plume corridor, a light gravel
                                strip at the foundation's slope foot, sandy service tracks, and darker wooded ground on
                                the far ridge band.
  LT_TerrainDetail_Albedo.png   1024 x 1024 sRGB, tileable 5 m tile (URP Detail Albedo x2): neutral, mean linear 0.5
                                (sRGB 188) per channel, so it adds grain without shifting the macro colour: greener
                                tufts, pale bare sand, blades, grit, pebbles.
  LT_TerrainDetail_Normal.png   1024 x 1024, tileable, OpenGL-style (+Y), same 5 m tile.
  LT_TerrainTile_BaseColor.png  1024 x 1024 sRGB, tileable 16 m tile for the fallback material (tiled BaseMap only).

Run: python lt_textures.py <textures dir>
"""
import math
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True
sys.path.insert(0, HERE)
import a7_textures as tx  # noqa: E402
import lt_height as H  # noqa: E402

OUT = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else os.path.join(HERE, "..", "out", "textures")
ss = H.smoothstep


def lin(hexcode):
    c = np.array([int(hexcode[i:i + 2], 16) / 255.0 for i in (1, 3, 5)])
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def to_srgb(c):
    c = np.clip(c, 0.0, 1.0)
    return np.where(c <= 0.0031308, 12.92 * c, 1.055 * np.power(c, 1 / 2.4) - 0.055)


PAL = {k: lin(v) for k, v in {
    "grass_dry": "#A39A6B", "grass_green": "#7D8A52", "scrub": "#66714A", "scrub_dark": "#535E3A",
    "sand": "#D3C6A2", "sand_warm": "#BCA781", "soil": "#6E5E47", "wet_green": "#5E6D3F", "mowed": "#8C9A5E",
    "mowed_dry": "#A8A473", "burn": "#5C5849", "blast": "#C8BFA9", "soot": "#6C675F", "track": "#B9A780",
    "gravel": "#BDB6A3", "wood": "#4B5735", "dune_grass": "#B4AD86", "hardstand": "#A39C8A"}.items()}


def mix(a, b, w):
    w = np.asarray(w, float)[..., None]
    return a * (1.0 - w) + b * w


def polyline_distance(x, z, pts):
    d = np.full(x.shape, 1e9)
    for a, b in zip(pts, pts[1:]):
        d = np.minimum(d, H.segment_distance(x, z, a, b)[0])
    return d


TRACKS = [
    [(-209.0, -209.0), (209.0, -209.0), (209.0, 339.0), (-209.0, 339.0), (-209.0, -209.0)],   # fence patrol road
    [(124.0, -60.0), (124.0, -925.0)],                                                      # pole-line access
    [(209.0, 40.0), (900.0, 120.0), (2000.0, 60.0), (3000.0, -80.0)],                       # east service track
    [(-209.0, 300.0), (-900.0, 900.0), (-1700.0, 1900.0), (-2600.0, 2800.0)],               # south-west track
]


def burn_rects():
    rng = np.random.default_rng(706)
    out = []
    while len(out) < 9:
        r = 600.0 + 2000.0 * rng.random()
        a = rng.random() * 2 * math.pi
        cx, cz = r * math.cos(a), r * math.sin(a)
        if float(H.protected_distance(np.array([cx]), np.array([cz]))[0]) < 400.0:
            continue
        out.append((cx, cz, 180.0 + 340.0 * rng.random(), 120.0 + 260.0 * rng.random(), rng.random() * math.pi))
    return out


BURNS = burn_rects()


def colour(x, z):
    c = H.components(x, z)
    eps = 1.5
    hx = (H.height(x + eps, z) - H.height(x - eps, z)) / (2 * eps)
    hz = (H.height(x, z + eps) - H.height(x, z - eps)) / (2 * eps)
    slope = np.hypot(hx, hz)
    d, r, dune, coast, swale = c["d"], c["r"], c["dune"], c["coast"], c["swale"]
    # prairie: dry and greener grass at 380 m, drier flecks at 70 m
    prairie = mix(PAL["grass_dry"], PAL["grass_green"], ss(-0.2, 0.25, H.fbm(x, z, 380.0, 3, 701)))
    prairie = mix(prairie, PAL["mowed_dry"], 0.3 * ss(0.05, 0.35, H.fbm(x, z, 70.0, 2, 702)))
    # scrub communities at about 1 km, darker thickets and grass openings inside them, clumps in the prairie
    scrub_zone = ss(-0.02, 0.22, H.fbm(x, z, 1100.0, 3, 720))
    thick = ss(0.0, 0.3, H.fbm(x, z, 120.0, 3, 721))
    scrub_col = mix(PAL["scrub"], PAL["scrub_dark"], thick)
    scrub_body = mix(scrub_col, prairie, 0.75 * ss(0.12, 0.32, H.fbm(x, z, 160.0, 2, 722)))
    clumps = ss(0.24, 0.40, H.fbm(x, z, 70.0, 3, 703) + 0.15 * swale - 0.2 * dune)
    base = mix(mix(prairie, scrub_col, 0.85 * clumps), scrub_body, scrub_zone)
    # coastal dune band (+x): pale dune grass, sand on crests, blowouts and steeper faces
    base = mix(base, PAL["dune_grass"], 0.55 * coast * (1.0 - 0.6 * scrub_zone))
    crest = ss(0.35, 0.8, dune) * (0.15 + 0.85 * coast)
    blow = ss(0.25, 0.45, H.fbm(x, z, 60.0, 3, 704)) * ss(0.25, 0.55, dune) * (0.3 + 0.7 * coast)
    sand_w = np.clip(0.8 * crest + 0.85 * blow + 0.4 * ss(0.07, 0.16, slope), 0.0, 1.0)
    sand_w *= 1.0 - 0.6 * thick * scrub_zone
    sand = mix(PAL["sand"], PAL["sand_warm"], ss(-0.2, 0.3, H.fbm(x, z, 45.0, 2, 714)))
    # darker damp soil in swales and ditches
    wet = np.clip(0.7 * ss(0.45, 0.95, swale) + 0.9 * ss(0.15, 0.6, c["ditch"]), 0.0, 1.0)
    damp = mix(PAL["soil"], PAL["wet_green"], H.vnoise(x, z, 30.0, 30.0, 710))
    # burn-scar mosaic (old prescribed burns)
    burn = np.zeros_like(x)
    for cx, cz, w, h, a in BURNS:
        ca, sa = math.cos(a), math.sin(a)
        lx = ca * (x - cx) + sa * (z - cz)
        lz = -sa * (x - cx) + ca * (z - cz)
        burn = np.maximum(burn, ss(20.0, -20.0, H.rect_signed(lx, lz, (-w / 2, w / 2, -h / 2, h / 2))))
    fence_in = c["fence_in"]
    maint = np.maximum(0.75 * fence_in, ss(55.0, 18.0, d))
    base = mix(base, sand, sand_w * (1.0 - maint))
    base = mix(base, damp, 0.75 * wet)
    base = mix(base, PAL["burn"], 0.3 * burn * (0.65 + 0.35 * H.vnoise(x, z, 30.0, 30.0, 711)))
    # maintained ground: mowed grass (stripes inside the fence), a hardstand at the facility
    stripes = 1.0 + 0.045 * np.sin(2 * math.pi * (0.94 * x + 0.34 * z) / 12.5) * fence_in
    mowed = mix(PAL["mowed"], PAL["mowed_dry"], ss(-0.15, 0.25, H.fbm(x, z, 90.0, 2, 712))) * stripes[..., None]
    base = mix(base, mowed, maint)
    base = mix(base, PAL["hardstand"], ss(4.0, -4.0, H.rect_signed(x, z, (-50.0, 90.0, -1015.0, -905.0))))
    # plume corridor: blasted sand with a soot fan from the trench outlet
    plume_w = ss(12.0, -6.0, H.rect_signed(x, z, H.CORRIDORS[1][1]))
    soot = np.exp(-np.maximum(z - 72.0, 0.0) / 110.0) * ss(42.0, 0.0, np.abs(x)) * (
        0.55 + 0.45 * H.vnoise(x, z, 5.0, 60.0, 705))
    blast = mix(PAL["blast"], PAL["dune_grass"], 0.6 * ss(0.0, 0.3, H.fbm(x, z, 25.0, 2, 715)) * ss(110.0, 260.0, z))
    base = mix(base, mix(blast, PAL["soot"], 0.8 * soot), plume_w * ss(340.0, 170.0, z))
    # light gravel strip at the foundation's slope foot, sandy service tracks
    foot = H.rect_distance(x, z, H.FOOTPRINT)
    base = mix(base, PAL["gravel"], ss(9.0, 4.0, foot) * (1.0 - ss(0.0, 0.5, -H.rect_signed(x, z, H.FOOTPRINT))))
    track = np.zeros_like(x)
    for pts in TRACKS:
        track = np.maximum(track, ss(2.6, 1.2, polyline_distance(x, z, pts)))
    base = mix(base, PAL["track"], 0.5 * track * (0.7 + 0.3 * H.vnoise(x, z, 8.0, 8.0, 713)))
    # distant hammocks and the wooded far ridge band
    woods = ss(0.1, 0.3, H.fbm(x, z, 900.0, 2, 707)) * ss(1500.0, 4000.0, r)
    base = mix(base, PAL["wood"], np.clip(0.45 * woods + 0.55 * ss(6000.0, 8500.0, r), 0.0, 0.8))
    base = base * (1.0 + 0.08 * H.fbm(x, z, 14.0, 2, 708) + 0.05 * H.fbm(x, z, 40.0, 2, 709))[..., None]
    return base


def gen_macro(n=2048, rows=128):
    u = (np.arange(n) + 0.5) / n
    xs = H.uv_axis_inverse(u)
    zs = H.uv_axis_inverse(1.0 - u)            # image row 0 is v = 1
    img = np.zeros((n, n, 3))
    for r0 in range(0, n, rows):
        zz, xx = np.meshgrid(zs[r0:r0 + rows], xs, indexing="ij")
        img[r0:r0 + rows] = colour(xx, zz)
    srgb = to_srgb(img)
    Image.fromarray((srgb * 255.0 + 0.5).astype(np.uint8), "RGB").save(os.path.join(OUT, "LT_Terrain_Macro.png"),
                                                                        optimize=True)
    inner = srgb[64:-64, 64:-64]
    print("  wrote LT_Terrain_Macro", n, "mean sRGB", np.round(inner.reshape(-1, 3).mean(axis=0) * 255, 1))
    return srgb


def gen_detail(n=1024):
    rng = np.random.default_rng(811)
    clumps = tx.fbm(n, 20, 4, rng)
    tufts = ss(0.55, 0.70, tx.fbm(n, 14, 4, rng))
    bare = ss(0.55, 0.68, tx.fbm(n, 8, 3, rng))
    grain = tx.fbm(n, 260, 2, rng)
    blades = tx.fbm(n, 70, 3, rng, aniso=(1, 6))
    pebbles = ss(0.66, 0.74, tx.fbm(n, 110, 2, rng))
    dark = ss(0.66, 0.72, tx.fbm(n, 90, 2, rng))
    tone = 1.0 + 0.16 * (clumps - 0.5) + 0.16 * (grain - 0.5) + 0.16 * (blades - 0.5) + 0.20 * pebbles - 0.16 * dark
    rgb = tone[..., None] * mix(mix(np.ones(3), np.array([0.80, 0.92, 0.70]), tufts * (1 - bare)),
                                np.array([1.16, 1.10, 0.98]), bare * (1 - tufts))
    lin_rgb = 0.5 * rgb / rgb.reshape(-1, 3).mean(axis=0)
    srgb = to_srgb(lin_rgb)
    Image.fromarray((srgb * 255.0 + 0.5).astype(np.uint8), "RGB").save(
        os.path.join(OUT, "LT_TerrainDetail_Albedo.png"), optimize=True)
    height = 0.8 * tufts + 0.25 * clumps + 0.15 * grain + 0.3 * blades + 0.4 * pebbles - 0.15 * dark - 0.2 * bare
    nrm = tx.normal_from_height(height, 6.0)
    Image.fromarray(tx.to_u8(nrm), "RGB").save(os.path.join(OUT, "LT_TerrainDetail_Normal.png"), optimize=True)
    print("  wrote LT_TerrainDetail_Albedo/_Normal", n, "linear mean", np.round(lin_rgb.reshape(-1, 3).mean(axis=0), 4),
          "normal z mean %.3f" % (nrm[..., 2] * 2 - 1).mean())
    return grain, clumps


def gen_tile(n=1024):
    rng = np.random.default_rng(812)
    patches = tx.fbm(n, 4, 4, rng)
    sandy = tx.fbm(n, 6, 3, rng)
    scrubby = tx.fbm(n, 5, 3, rng)
    grain = tx.fbm(n, 80, 3, rng)
    fine = tx.fbm(n, 300, 2, rng)
    base = mix(PAL["grass_dry"], PAL["grass_green"], ss(0.35, 0.7, patches))
    base = mix(base, PAL["scrub"], 0.8 * ss(0.58, 0.7, scrubby))
    base = mix(base, PAL["sand"], 0.8 * ss(0.6, 0.72, sandy))
    base = base * (1.0 + 0.30 * (grain - 0.5) + 0.18 * (fine - 0.5))[..., None]
    srgb = to_srgb(base)
    Image.fromarray((srgb * 255.0 + 0.5).astype(np.uint8), "RGB").save(
        os.path.join(OUT, "LT_TerrainTile_BaseColor.png"), optimize=True)
    print("  wrote LT_TerrainTile_BaseColor", n, "mean sRGB", np.round(srgb.reshape(-1, 3).mean(axis=0) * 255, 1))


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    tx.OUT = OUT
    gen_detail()
    gen_tile()
    gen_macro()
