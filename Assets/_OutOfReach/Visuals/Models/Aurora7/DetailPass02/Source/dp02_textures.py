"""dp02_textures - the small CKP-REFINE material set (original procedural tooling; numpy and Pillow only).

Writes two tiling texture sets in the AURORA-7 layout (BaseColor sRGB, Normal OpenGL-style, MODS = R metallic,
G occlusion, B unused, A smoothness) to ../Textures, and two URP Lit materials to ../Materials that point at them:
  DP02_PaintWorn    graphite paint on aluminium: orange peel, handling smudges (roughness variation), small chips and
                    fine scratches that expose bare metal (metallic and smoother where worn). Colour is in the map, so
                    the material tint is white.
  DP02_RubberKnurl  black rubber with a diamond knurl (about 2 mm pitch at the material's tiling of 16 per metre),
                    tops slightly worn and smoother, grooves occluded.
The .mat files copy the habitat's URP Lit material (Habitat/Materials/HB_BagQuilt.mat) with new texture GUIDs; the
GUIDs are the path-derived ones of make_metas.py, which writes the matching texture metas.

Run: python -B dp02_textures.py [--materials-only]
"""
import json
import math
import os
import re
import sys

import numpy as np
from PIL import Image, ImageDraw

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.insert(0, HERE)
from make_metas import HABITAT, guid_for  # noqa: E402

TEXTURES = os.path.join(ROOT, "Textures")
MATERIALS = os.path.join(ROOT, "Materials")
TEMPLATE_MAT = os.path.join(HABITAT, "Materials", "HB_BagQuilt.mat")


# ----------------------------------------------------------------------------- noise (same recipes as a7_textures.py)
def value_noise(n, cells_x, cells_y, rng):
    grid = rng.random((cells_y, cells_x))
    xs, ys = np.arange(n) * cells_x / n, np.arange(n) * cells_y / n
    x0, y0 = np.floor(xs).astype(int), np.floor(ys).astype(int)
    fx, fy = xs - x0, ys - y0
    sx, sy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    x1, y1 = (x0 + 1) % cells_x, (y0 + 1) % cells_y
    x0 %= cells_x
    y0 %= cells_y
    top = grid[np.ix_(y0, x0)] * (1 - sx)[None, :] + grid[np.ix_(y0, x1)] * sx[None, :]
    bottom = grid[np.ix_(y1, x0)] * (1 - sx)[None, :] + grid[np.ix_(y1, x1)] * sx[None, :]
    return top * (1 - sy)[:, None] + bottom * sy[:, None]


def fbm(n, cells, octaves, rng, persistence=0.5):
    total, amplitude, norm, c = np.zeros((n, n)), 1.0, 0.0, cells
    for _ in range(octaves):
        cc = max(1, min(n // 2, int(c)))
        total += amplitude * value_noise(n, cc, cc, rng)
        norm += amplitude
        amplitude *= persistence
        c *= 2
    return total / norm


def scratches(n, rng, count, max_len):
    image = Image.new("L", (n, n), 0)
    draw = ImageDraw.Draw(image)
    for _ in range(count):
        x, y, angle = rng.random() * n, rng.random() * n, rng.random() * math.tau
        length = (0.02 + rng.random() ** 2 * max_len) * n
        dx, dy = math.cos(angle) * length, math.sin(angle) * length
        shade = int(110 + rng.random() * 145)
        for ox in (-n, 0, n):
            for oy in (-n, 0, n):
                draw.line((x + ox, y + oy, x + dx + ox, y + dy + oy), fill=shade, width=1)
    return np.asarray(image, dtype=np.float64) / 255.0


def normal_from_height(height, strength):
    du = np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)
    dr = np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)
    nx, ny, nz = -du * strength, dr * strength, np.ones_like(height)
    length = np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.stack([nx / length, ny / length, nz / length], axis=-1) * 0.5 + 0.5


def u8(a):
    return (np.clip(a, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8)


def save_set(name, albedo_srgb, height, metallic, occlusion, smoothness, strength):
    os.makedirs(TEXTURES, exist_ok=True)
    n = height.shape[0]
    Image.fromarray(u8(albedo_srgb), "RGB").save(os.path.join(TEXTURES, name + "_BaseColor.png"), optimize=True)
    Image.fromarray(u8(normal_from_height(height, strength)), "RGB").save(os.path.join(TEXTURES, name + "_Normal.png"),
                                                                          optimize=True)
    mods = np.stack([np.broadcast_to(metallic, (n, n)), np.broadcast_to(occlusion, (n, n)), np.zeros((n, n)),
                     np.broadcast_to(smoothness, (n, n))], axis=-1)
    Image.fromarray(u8(mods), "RGBA").save(os.path.join(TEXTURES, name + "_MODS.png"), optimize=True)
    print("texture set", name, n, "px; smoothness %.2f-%.2f, metallic max %.2f" % (
        float(np.min(smoothness)), float(np.max(smoothness)), float(np.max(metallic))))


# ----------------------------------------------------------------------------- the two sets
def worn_paint(n=512):
    """Graphite (sRGB 0.17) paint on aluminium. One tile is 0.5 m at the material's tiling of 2."""
    rng = np.random.default_rng(2611)
    peel = fbm(n, 96, 3, rng)
    cloud = fbm(n, 4, 4, rng)
    smudge = fbm(n, 9, 4, rng)
    chip_detail = fbm(n, 64, 3, rng)
    chip_zone = fbm(n, 6, 3, rng)
    chips = np.clip((chip_detail - 0.66) * 14.0, 0.0, 1.0) * np.clip((chip_zone - 0.52) * 8.0, 0.0, 1.0)
    scr = scratches(n, rng, 70, 0.12) * 0.85
    bare = np.clip(np.maximum(chips, scr * 0.75), 0.0, 1.0)
    paint = 0.170 + 0.018 * (cloud - 0.5) + 0.010 * (smudge - 0.5)
    albedo = paint * (1.0 - bare) + 0.64 * bare
    albedo = np.stack([albedo, albedo, albedo * 1.01], axis=-1)
    metallic = bare
    smooth = (0.40 + 0.20 * (smudge - 0.5) - 0.04 * (peel - 0.5)) * (1.0 - bare) + 0.58 * bare
    occlusion = 1.0 - 0.10 * chips - 0.04 * (1.0 - cloud)
    height = 0.45 * peel - 0.9 * chips - 0.25 * scr
    save_set("DP02_PaintWorn", albedo, height, metallic, occlusion, smooth, 2.4)


def knurl(n=512, diamonds=32):
    """Diamond knurl, `diamonds` periods per tile along both diagonals, seamless."""
    rng = np.random.default_rng(1511)
    u = (np.arange(n) + 0.5) / n
    uu, vv = np.meshgrid(u, u)

    def tri(x):
        return 1.0 - 2.0 * np.abs(x - np.floor(x) - 0.5)

    h = np.minimum(tri((uu + vv) * diamonds), tri((uu - vv) * diamonds))
    grain = fbm(n, 128, 2, rng)
    cloud = fbm(n, 4, 3, rng)
    tops = np.clip((h - 0.55) * 3.0, 0.0, 1.0)
    tone = 0.105 + 0.012 * (cloud - 0.5) + 0.020 * tops
    albedo = np.stack([tone, tone, tone * 1.02], axis=-1)
    smooth = 0.22 + 0.10 * tops + 0.05 * (grain - 0.5)
    occlusion = 0.72 + 0.28 * h
    save_set("DP02_Knurl", albedo, h + 0.08 * grain, 0.0, occlusion, smooth, 3.0)


# ----------------------------------------------------------------------------- URP Lit materials
def material(name, texture_set, tiling, bump):
    text = open(TEMPLATE_MAT, encoding="utf-8").read()
    base = guid_for(os.path.join(TEXTURES, texture_set + "_BaseColor.png"))
    normal = guid_for(os.path.join(TEXTURES, texture_set + "_Normal.png"))
    mods = guid_for(os.path.join(TEXTURES, texture_set + "_MODS.png"))

    def tex(block, guid, scale):
        nonlocal text
        pattern = r"(    - %s:\n        m_Texture: )\{[^}]*\}(\n        m_Scale: )\{[^}]*\}" % block
        replacement = r"\g<1>{fileID: 2800000, guid: %s, type: 3}\g<2>{x: %s, y: %s}" % (guid, scale, scale)
        text, count = re.subn(pattern, replacement, text)
        assert count == 1, block

    assert "  m_Name: HB_BagQuilt\n" in text
    text = text.replace("  m_Name: HB_BagQuilt\n", "  m_Name: %s\n" % name, 1)     # the Material block, not the URP stub
    tex("_BaseMap", base, tiling)
    tex("_MainTex", base, tiling)
    tex("_BumpMap", normal, 1)
    tex("_MetallicGlossMap", mods, 1)
    tex("_OcclusionMap", mods, 1)
    text = re.sub(r"^    - _BumpScale: .*$", "    - _BumpScale: %s" % bump, text, count=1, flags=re.M)
    text = re.sub(r"^    - _BaseColor: .*$", "    - _BaseColor: {r: 1, g: 1, b: 1, a: 1}", text, count=1, flags=re.M)
    text = re.sub(r"^    - _Color: .*$", "    - _Color: {r: 1, g: 1, b: 1, a: 1}", text, count=1, flags=re.M)
    os.makedirs(MATERIALS, exist_ok=True)
    with open(os.path.join(MATERIALS, name + ".mat"), "w", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    print("material", name, "->", texture_set, "tiling", tiling, "bump", bump)


def main():
    if "--materials-only" not in sys.argv[1:]:
        worn_paint()
        knurl()
    with open(os.path.join(HERE, "dp02_materials.json"), encoding="utf-8") as handle:
        specs = json.load(handle)["materials"]
    for spec in specs:
        material(spec["name"], spec["set"], spec["tiling"], spec["normal"])


if __name__ == "__main__":
    main()
