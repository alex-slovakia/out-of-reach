"""
OUT OF REACH - AURORA-7 procedural textures (original work, generated from code).

Plain Python 3 with numpy and Pillow; no Blender needed and nothing is downloaded.

    python a7_textures.py                 (writes ../Textures and ../Textures/*.json)
    python a7_textures.py --only decals   (one group: tiling | decals | screens)

Outputs
- Tiling PBR sets, 1 UV unit = 1 tile, all seamless:
    <Name>_BaseColor.png  sRGB, tint-neutral where a material colour multiplies it
    <Name>_Normal.png     tangent space, OpenGL / Unity convention (Y+)
    <Name>_MODS.png       R metallic, G occlusion, B unused, A smoothness (URP Lit packing)
- A7_Decals.png + a7_decals.json: label / placard atlas with physical sizes in metres.
- A7_Screens.png + a7_screens.json: emissive display faces.

Label font: Inter (SIL Open Font License 1.1), read from the Blender installation's bundled fonts.
The font file is not copied into the repository; only rendered label pixels are.
"""
import json
import math
import os
import sys
import zlib

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "Textures"))
ARGS = sys.argv[1:]
ONLY = ARGS[ARGS.index("--only") + 1] if "--only" in ARGS else None

FONT_CANDIDATES = [
    r"C:\Program Files\Blender Foundation\Blender 5.3\5.3\datafiles\fonts\Inter.woff2",
    "/usr/share/blender/datafiles/fonts/Inter.woff2",
]
MONO_CANDIDATES = [
    r"C:\Program Files\Blender Foundation\Blender 5.3\5.3\datafiles\fonts\DejaVuSansMono.woff2",
]


def first_existing(paths):
    for path in paths:
        if os.path.exists(path):
            return path
    return None


INTER = first_existing(FONT_CANDIDATES)
MONO = first_existing(MONO_CANDIDATES)
_FONT_CACHE = {}


def font(size, weight="Regular", mono=False):
    """Inter at a pixel size and named weight; falls back to Pillow's embedded Aileron (CC0)."""
    key = (int(size), weight, mono)
    if key in _FONT_CACHE:
        return _FONT_CACHE[key]
    path = MONO if mono else INTER
    if path is None:
        result = ImageFont.load_default(size=int(size))
    else:
        result = ImageFont.truetype(path, int(size))
        if not mono:
            try:
                result.set_variation_by_name(weight.encode("ascii"))
            except (OSError, ValueError):
                pass
    _FONT_CACHE[key] = result
    return result


# --------------------------------------------------------------------------- noise helpers
def value_noise(n, cells_x, cells_y, rng):
    """Seamless value noise, n x n, with an integer number of cells along each axis."""
    grid = rng.random((cells_y, cells_x))
    xs = np.arange(n) * cells_x / n
    ys = np.arange(n) * cells_y / n
    x0 = np.floor(xs).astype(int)
    y0 = np.floor(ys).astype(int)
    fx = xs - x0
    fy = ys - y0
    sx = fx * fx * (3 - 2 * fx)
    sy = fy * fy * (3 - 2 * fy)
    x1 = (x0 + 1) % cells_x
    y1 = (y0 + 1) % cells_y
    x0 %= cells_x
    y0 %= cells_y
    top = grid[np.ix_(y0, x0)] * (1 - sx)[None, :] + grid[np.ix_(y0, x1)] * sx[None, :]
    bottom = grid[np.ix_(y1, x0)] * (1 - sx)[None, :] + grid[np.ix_(y1, x1)] * sx[None, :]
    return top * (1 - sy)[:, None] + bottom * sy[:, None]


def fbm(n, cells, octaves, rng, persistence=0.5, aniso=(1, 1)):
    total = np.zeros((n, n))
    amplitude = 1.0
    norm = 0.0
    c = cells
    for _ in range(octaves):
        cx = max(1, min(n // 2, int(c * aniso[0])))
        cy = max(1, min(n // 2, int(c * aniso[1])))
        total += amplitude * value_noise(n, cx, cy, rng)
        norm += amplitude
        amplitude *= persistence
        c *= 2
    return total / norm


def scratch_layer(n, rng, count, max_len, width=1, horizontal_bias=0.0):
    """0..1 mask of thin scratches, drawn with wrap-around so the tile stays seamless."""
    image = Image.new("L", (n, n), 0)
    draw = ImageDraw.Draw(image)
    for _ in range(count):
        x = rng.random() * n
        y = rng.random() * n
        angle = rng.random() * math.tau
        if horizontal_bias > 0:
            angle = (rng.random() - 0.5) * math.pi * (1 - horizontal_bias)
        length = (0.02 + rng.random() ** 2 * max_len) * n
        dx = math.cos(angle) * length
        dy = math.sin(angle) * length
        shade = int(90 + rng.random() * 165)
        for ox in (-n, 0, n):
            for oy in (-n, 0, n):
                draw.line((x + ox, y + oy, x + dx + ox, y + dy + oy), fill=shade, width=width)
    return np.asarray(image, dtype=np.float64) / 255.0


def normal_from_height(height, strength):
    """Height (rows down) to an OpenGL-style tangent-space normal map in 0..1."""
    du = np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)
    dr = np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)
    nx = -du * strength
    ny = dr * strength
    nz = np.ones_like(height)
    length = np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.stack([nx / length, ny / length, nz / length], axis=-1) * 0.5 + 0.5


def to_u8(array):
    return (np.clip(array, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8)


def save_set(name, albedo, height, metallic, occlusion, smoothness, normal_strength):
    os.makedirs(OUT, exist_ok=True)
    n = height.shape[0]
    Image.fromarray(to_u8(albedo), "RGB").save(os.path.join(OUT, name + "_BaseColor.png"), optimize=True)
    Image.fromarray(to_u8(normal_from_height(height, normal_strength)), "RGB").save(
        os.path.join(OUT, name + "_Normal.png"), optimize=True)
    mods = np.stack([np.broadcast_to(metallic, (n, n)), np.broadcast_to(occlusion, (n, n)),
                     np.zeros((n, n)), np.broadcast_to(smoothness, (n, n))], axis=-1)
    Image.fromarray(to_u8(mods), "RGBA").save(os.path.join(OUT, name + "_MODS.png"), optimize=True)
    print("  wrote", name, n)


def gray(value):
    return np.stack([value, value, value], axis=-1)


def uv_grid(n):
    coords = (np.arange(n) + 0.5) / n
    return np.meshgrid(coords, coords)


# --------------------------------------------------------------------------- tiling sets
def gen_paint(n=1024):
    """Painted aluminium panel: faint orange peel, handling smudges, a few fine scratches."""
    rng = np.random.default_rng(11)
    peel = fbm(n, 110, 3, rng)
    cloud = fbm(n, 3, 5, rng)
    smudge = fbm(n, 7, 4, rng)
    scratches = scratch_layer(n, rng, 40, 0.10)
    height = 0.55 * peel - 0.28 * scratches
    tone = 0.945 + 0.045 * (cloud - 0.5) - 0.010 * scratches
    albedo = gray(tone) * np.array([1.0, 1.0, 0.992])
    smooth = 0.47 + 0.16 * (smudge - 0.5) - 0.05 * scratches
    save_set("A7_Paint", albedo, height, 0.0, 1.0, smooth, 2.2)


def gen_anodized(n=1024):
    """Anodised aluminium: fine grain with a light directional finish."""
    rng = np.random.default_rng(12)
    grain = fbm(n, 180, 2, rng)
    brush = fbm(n, 6, 5, rng, aniso=(1, 40))
    cloud = fbm(n, 4, 4, rng)
    scratches = scratch_layer(n, rng, 60, 0.18)
    height = 0.35 * grain + 0.5 * brush - 0.6 * scratches
    tone = 0.93 + 0.05 * (cloud - 0.5) + 0.03 * (brush - 0.5)
    smooth = 0.60 + 0.12 * (cloud - 0.5) - 0.08 * grain - 0.15 * scratches
    save_set("A7_Anodized", gray(tone), height, 1.0, 1.0, smooth, 1.4)


def gen_brushed(n=1024):
    """Bare machined aluminium with visible brushing (structure, ring frames, fasteners)."""
    rng = np.random.default_rng(13)
    brush = fbm(n, 4, 6, rng, aniso=(1, 64))
    fine = fbm(n, 2, 3, rng, aniso=(1, 256))
    cloud = fbm(n, 3, 4, rng)
    scratches = scratch_layer(n, rng, 90, 0.3, horizontal_bias=0.6)
    height = 0.6 * brush + 0.5 * fine - 0.5 * scratches
    tone = 0.90 + 0.07 * (cloud - 0.5) + 0.05 * (brush - 0.5)
    smooth = 0.66 + 0.10 * (cloud - 0.5) - 0.10 * fine
    save_set("A7_Brushed", gray(tone), height, 1.0, 1.0, smooth, 1.8)


def weave_height(n, threads):
    u, v = uv_grid(n)
    su = np.sin(math.pi * threads * u)
    sv = np.sin(math.pi * threads * v)
    over = (np.floor(threads * u) + np.floor(threads * v)) % 2 < 1
    return np.where(over, np.abs(su) ** 0.6, np.abs(sv) ** 0.6 * 0.82)


def gen_fabric(n=1024):
    """Plain-weave Nomex / beta cloth for stowage bags, straps and seat covers."""
    rng = np.random.default_rng(14)
    weave = weave_height(n, 128)
    fuzz = fbm(n, 160, 2, rng)
    cloud = fbm(n, 3, 5, rng)
    crease = fbm(n, 6, 4, rng)
    height = 0.75 * weave + 0.18 * fuzz + 0.9 * crease
    tone = 0.90 + 0.06 * (weave - 0.6) + 0.06 * (cloud - 0.5)
    occlusion = 0.80 + 0.20 * weave
    smooth = 0.16 + 0.06 * (cloud - 0.5)
    save_set("A7_Fabric", gray(tone), height, 0.0, occlusion, smooth, 2.6)


def gen_quilt(n=1024):
    """Quilted insulation blanket: diamond stitching over a fine weave."""
    rng = np.random.default_rng(15)
    u, v = uv_grid(n)
    k = 4
    a = np.abs(np.sin(math.pi * k * (u + v)))
    b = np.abs(np.sin(math.pi * k * (u - v)))
    puff = (a * b) ** 0.38
    stitch = np.minimum(a, b)
    seam = np.clip(1.0 - stitch / 0.045, 0.0, 1.0)
    weave = weave_height(n, 160)
    wrinkle = fbm(n, 10, 4, rng)
    height = 1.0 * puff + 0.10 * weave + 0.20 * wrinkle - 0.25 * seam
    tone = 0.90 + 0.05 * (puff - 0.5) - 0.10 * seam + 0.03 * (weave - 0.6)
    occlusion = 0.62 + 0.38 * puff
    smooth = 0.20 + 0.05 * (wrinkle - 0.5)
    save_set("A7_Quilt", gray(tone), height, 0.0, occlusion, smooth, 5.0)


def gen_deck(n=1024):
    """Non-slip deck coating with scuffed traffic wear."""
    rng = np.random.default_rng(16)
    grit = fbm(n, 256, 2, rng)
    grit2 = fbm(n, 96, 2, rng)
    wear = fbm(n, 5, 5, rng)
    scuffs = scratch_layer(n, rng, 70, 0.35, width=2)
    height = 0.9 * grit + 0.5 * grit2 - 0.4 * scuffs
    tone = 0.80 + 0.10 * (wear - 0.5) + 0.06 * (grit - 0.5) + 0.10 * scuffs
    smooth = 0.22 + 0.16 * np.clip(wear - 0.45, 0, 1) + 0.10 * scuffs
    save_set("A7_Deck", gray(tone), height, 0.0, 0.85 + 0.15 * grit, smooth, 3.2)


def gen_grate(n=1024):
    """Perforated anti-slip deck plate: rounded-square holes on a 15.6 mm pitch (one tile is 0.25 m)."""
    rng = np.random.default_rng(23)
    u, v = uv_grid(n)
    cells = 16
    fx = (u * cells) % 1.0 - 0.5
    fy = (v * cells) % 1.0 - 0.5
    distance = (np.abs(fx) ** 4 + np.abs(fy) ** 4) ** 0.25
    bar = 1.0 - np.clip((0.33 - distance) / 0.045, 0.0, 1.0)
    rim = np.clip(1.0 - np.abs(distance - 0.365) / 0.035, 0.0, 1.0) * bar
    wear = fbm(n, 6, 4, rng)
    grit = fbm(n, 200, 2, rng)
    height = bar * (0.82 + 0.18 * grit)
    tone = 0.03 + bar * (0.36 + 0.12 * (wear - 0.5) + 0.05 * (grit - 0.5)) + 0.12 * rim * np.clip(wear + 0.15, 0, 1)
    smooth = bar * (0.36 + 0.20 * np.clip(wear - 0.4, 0, 1) + 0.14 * rim)
    save_set("A7_Grate", gray(tone), height, bar * 0.7, 0.2 + 0.8 * bar, smooth, 6.0)


def gen_rubber(n=512):
    rng = np.random.default_rng(17)
    grain = fbm(n, 96, 3, rng)
    cloud = fbm(n, 4, 4, rng)
    tone = 0.86 + 0.10 * (cloud - 0.5)
    save_set("A7_Rubber", gray(tone), grain, 0.0, 1.0, 0.30 + 0.10 * (cloud - 0.5), 1.6)


def gen_carbon(n=1024):
    """Filament-wound carbon overwrap for pressure tanks (2/2 twill read)."""
    rng = np.random.default_rng(18)
    u, v = uv_grid(n)
    f = 48
    iu = np.floor(u * f)
    iv = np.floor(v * f)
    horizontal = ((iu + iv) % 4) < 2
    tow_h = np.abs(np.sin(math.pi * f * v))
    tow_v = np.abs(np.sin(math.pi * f * u))
    tow = np.where(horizontal, tow_h, tow_v) ** 0.5
    resin = fbm(n, 6, 4, rng)
    tone = 0.10 + 0.10 * tow + 0.03 * np.where(horizontal, 1.0, 0.0)
    albedo = gray(tone) * np.array([1.0, 1.0, 1.04])
    smooth = 0.62 + 0.20 * tow + 0.08 * (resin - 0.5)
    save_set("A7_Carbon", albedo, 0.8 * tow + 0.2 * resin, 0.15, 0.85 + 0.15 * tow, smooth, 2.4)


def gen_mli(n=1024):
    """Crinkled multilayer insulation foil (tint-neutral; material colour makes it gold or silver)."""
    rng = np.random.default_rng(19)
    ridged = np.zeros((n, n))
    amplitude = 1.0
    cells = 9
    for _ in range(6):
        layer = value_noise(n, cells, cells, rng)
        ridged += amplitude * (1.0 - np.abs(2.0 * layer - 1.0)) ** 2.2
        amplitude *= 0.62
        cells *= 2
    ridged /= ridged.max()
    cloud = fbm(n, 4, 4, rng)
    tone = 0.86 + 0.14 * (ridged - 0.5)
    smooth = 0.80 - 0.25 * (1.0 - ridged) + 0.05 * (cloud - 0.5)
    save_set("A7_MLI", gray(tone), ridged, 1.0, 0.75 + 0.25 * ridged, smooth, 14.0)


def gen_solar(n=1024):
    """Solar cell string: dark cells, fine fingers, silver bus bars. One tile = 8 x 4 cells."""
    rng = np.random.default_rng(20)
    u, v = uv_grid(n)
    cu = (u * 8) % 1.0
    cv = (v * 4) % 1.0
    gap = (cu < 0.035) | (cu > 0.965) | (cv < 0.03) | (cv > 0.97)
    bus = (np.abs(cv - 0.25) < 0.012) | (np.abs(cv - 0.75) < 0.012)
    fingers = (np.sin(math.tau * 40 * cu) > 0.86)
    cloud = fbm(n, 8, 3, rng)
    cell = np.stack([0.030 + 0.01 * cloud, 0.045 + 0.02 * cloud, 0.11 + 0.05 * cloud], axis=-1)
    silver = np.array([0.72, 0.74, 0.76])
    substrate = np.array([0.55, 0.50, 0.38])
    albedo = cell.copy()
    albedo[fingers] = albedo[fingers] * 0.55 + silver * 0.45
    albedo[bus] = silver
    albedo[gap] = substrate
    metallic = np.where(bus | fingers, 1.0, 0.0)
    metallic[gap] = 0.0
    smooth = np.where(gap, 0.35, 0.86)
    height = np.where(gap, 0.0, 1.0) + 0.3 * bus
    save_set("A7_SolarCell", albedo, height, metallic, 1.0, smooth, 1.2)


# --------------------------------------------------------------------------- decal atlas
PPM = 1800.0  # pixels per metre in the decal atlas

WHITE = (236, 237, 232, 255)
INK = (22, 23, 26, 255)
GRAPHITE = (30, 32, 36, 255)
ORANGE = (240, 150, 50, 255)
YELLOW = (244, 196, 48, 255)
RED = (196, 38, 30, 255)
BLUE = (28, 78, 150, 255)
GREEN = (120, 205, 120, 255)
CLEAR = (0, 0, 0, 0)


class Atlas:
    """Shelf packer. Entries are collected first and packed tallest-first; each keeps its physical size."""

    def __init__(self, size, ppm, background=CLEAR, pad=6):
        self.size = size
        self.ppm = ppm
        self.pad = pad
        self.background = background
        self.pending = []
        self.entries = {}

    def add(self, key, width_m, height_m, painter):
        w = max(4, int(round(width_m * self.ppm)))
        h = max(4, int(round(height_m * self.ppm)))
        self.pending.append((key, width_m, height_m, w, h, painter))

    def save(self, name):
        image = Image.new("RGBA", (self.size, self.size), self.background)
        x = y = self.pad
        row = 0
        for key, width_m, height_m, w, h, painter in sorted(self.pending, key=lambda item: (-item[4], -item[3], item[0])):
            if x + w + self.pad > self.size:
                x = self.pad
                y += row + self.pad
                row = 0
            if y + h + self.pad > self.size:
                raise RuntimeError("atlas %s is full at %s (y=%d)" % (name, key, y))
            tile = Image.new("RGBA", (w, h), CLEAR)
            painter(tile, ImageDraw.Draw(tile), w, h)
            image.paste(tile, (x, y))
            s = float(self.size)
            self.entries[key] = {
                "u0": x / s, "v0": 1.0 - (y + h) / s, "u1": (x + w) / s, "v1": 1.0 - y / s,
                "w": width_m, "h": height_m,
            }
            x += w + self.pad
            row = max(row, h)
        os.makedirs(OUT, exist_ok=True)
        image.save(os.path.join(OUT, name + ".png"), optimize=True)
        with open(os.path.join(OUT, name.lower() + ".json"), "w", encoding="utf-8") as handle:
            json.dump({"ppm": self.ppm, "size": self.size, "entries": self.entries}, handle, indent=1)
        print("  wrote", name, len(self.entries), "entries, used height", y + row, "of", self.size)


def fit_text(draw, text, max_w, max_h, weight="SemiBold", mono=False, start=None):
    size = int(start or max_h)
    while size > 6:
        f = font(size, weight, mono)
        box = draw.textbbox((0, 0), text, font=f)
        if box[2] - box[0] <= max_w and box[3] - box[1] <= max_h:
            return f, box
        size -= 1
    f = font(6, weight, mono)
    return f, draw.textbbox((0, 0), text, font=f)


def centered(draw, text, w, h, fill, weight="SemiBold", margin=0.12, max_height=0.56, dy=0.0, mono=False):
    f, box = fit_text(draw, text, w * (1 - 2 * margin), h * max_height, weight, mono)
    tw = box[2] - box[0]
    th = box[3] - box[1]
    draw.text(((w - tw) / 2 - box[0], (h - th) / 2 - box[1] + dy * h), text, font=f, fill=fill)


def plate(fill, text_fill, text, weight="SemiBold", border=None, radius=0.10, max_height=0.56):
    def paint(tile, draw, w, h):
        r = int(min(w, h) * radius)
        draw.rounded_rectangle((0, 0, w - 1, h - 1), radius=r, fill=fill)
        if border:
            inset = max(2, int(min(w, h) * 0.06))
            draw.rounded_rectangle((inset, inset, w - 1 - inset, h - 1 - inset), radius=max(1, r - inset),
                                   outline=border, width=max(2, int(min(w, h) * 0.035)))
        centered(draw, text, w, h, text_fill, weight, max_height=max_height)
    return paint


def stencil(text, fill=INK, weight="Bold"):
    def paint(tile, draw, w, h):
        centered(draw, text, w, h, fill, weight, margin=0.02, max_height=0.86)
    return paint


def two_line(fill, text_fill, top, bottom, border=None, accent=None):
    def paint(tile, draw, w, h):
        r = int(min(w, h) * 0.08)
        draw.rounded_rectangle((0, 0, w - 1, h - 1), radius=r, fill=fill)
        if accent:
            draw.rectangle((0, 0, int(w * 0.035), h - 1), fill=accent)
        if border:
            draw.rounded_rectangle((3, 3, w - 4, h - 4), radius=max(1, r - 3), outline=border, width=3)
        f1, b1 = fit_text(draw, top, w * 0.84, h * 0.40, "Bold")
        f2, b2 = fit_text(draw, bottom, w * 0.84, h * 0.22, "Medium")
        draw.text(((w - (b1[2] - b1[0])) / 2 - b1[0], h * 0.14 - b1[1]), top, font=f1, fill=text_fill)
        draw.text(((w - (b2[2] - b2[0])) / 2 - b2[0], h * 0.66 - b2[1]), bottom, font=f2, fill=text_fill)
    return paint


def placard(title, lines, title_fill=ORANGE, body_fill=WHITE, ink=INK):
    def paint(tile, draw, w, h):
        draw.rounded_rectangle((0, 0, w - 1, h - 1), radius=int(w * 0.03), fill=body_fill)
        band = int(h * 0.20)
        draw.rounded_rectangle((0, 0, w - 1, band), radius=int(w * 0.03), fill=title_fill)
        draw.rectangle((0, band // 2, w - 1, band), fill=title_fill)
        ft, bt = fit_text(draw, title, w * 0.88, band * 0.60, "Bold")
        draw.text(((w - (bt[2] - bt[0])) / 2 - bt[0], (band - (bt[3] - bt[1])) / 2 - bt[1]), title, font=ft,
                  fill=INK if title_fill in (ORANGE, YELLOW, GREEN) else WHITE)
        size = max(8, int((h - band) / (len(lines) + 1.2) * 0.62))
        f = font(size, "Medium")
        y = band + size * 0.7
        for index, line in enumerate(lines):
            draw.text((w * 0.07, y), "%d" % (index + 1), font=font(size, "Bold"), fill=ink)
            draw.text((w * 0.16, y), line, font=f, fill=ink)
            y += (h - band) / (len(lines) + 0.6)
        draw.rounded_rectangle((2, 2, w - 3, h - 3), radius=int(w * 0.03), outline=ink, width=3)
    return paint


def hazard(colour_a=YELLOW, colour_b=INK):
    def paint(tile, draw, w, h):
        draw.rectangle((0, 0, w, h), fill=colour_a)
        step = h * 1.0
        x = -h
        while x < w + h:
            draw.polygon([(x, h), (x + step * 0.5, h), (x + step * 0.5 + h, 0), (x + h, 0)], fill=colour_b)
            x += step
    return paint


def velcro(colour):
    def paint(tile, draw, w, h):
        draw.rounded_rectangle((0, 0, w - 1, h - 1), radius=int(w * 0.08), fill=colour)
        rng = np.random.default_rng(w * 7 + h)
        darker = tuple(int(c * 0.72) for c in colour[:3]) + (255,)
        for _ in range(int(w * h / 14)):
            x = int(rng.random() * w)
            y = int(rng.random() * h)
            draw.point((x, y), fill=darker)
        draw.rounded_rectangle((1, 1, w - 2, h - 2), radius=int(w * 0.08), outline=darker, width=2)
    return paint


def barcode(code):
    def paint(tile, draw, w, h):
        draw.rectangle((0, 0, w - 1, h - 1), fill=WHITE)
        rng = np.random.default_rng(zlib.crc32(code.encode("ascii")))
        x = w * 0.06
        while x < w * 0.94:
            bar = max(1, int(rng.integers(1, 4) * w / 160))
            if rng.random() > 0.45:
                draw.rectangle((x, h * 0.12, x + bar, h * 0.62), fill=INK)
            x += bar + max(1, int(w / 220))
        f, box = fit_text(draw, code, w * 0.9, h * 0.22, "Medium", mono=True)
        draw.text(((w - (box[2] - box[0])) / 2 - box[0], h * 0.70 - box[1]), code, font=f, fill=INK)
    return paint


def arrow_label(text, fill, text_fill, direction=1):
    def paint(tile, draw, w, h):
        draw.rounded_rectangle((0, 0, w - 1, h - 1), radius=int(h * 0.14), fill=fill)
        ax = w * (0.86 if direction > 0 else 0.14)
        s = h * 0.26 * direction
        draw.polygon([(ax + s, h / 2), (ax - s, h / 2 - abs(s)), (ax - s, h / 2 + abs(s))], fill=text_fill)
        f, box = fit_text(draw, text, w * 0.60, h * 0.52, "Bold")
        tx = w * (0.10 if direction > 0 else 0.30)
        draw.text((tx - box[0], (h - (box[3] - box[1])) / 2 - box[1]), text, font=f, fill=text_fill)
    return paint


def gauge_face(title, unit, maximum, redline):
    def paint(tile, draw, w, h):
        cx, cy, r = w / 2, h / 2, min(w, h) / 2 - 2
        draw.ellipse((cx - r, cy - r, cx + r, cy + r), fill=WHITE, outline=INK, width=max(2, int(r * 0.03)))
        for i in range(0, 41):
            a = math.radians(225 - i * 270 / 40)
            inner = r * (0.78 if i % 5 == 0 else 0.86)
            colour = RED if i / 40 * maximum >= redline else INK
            draw.line((cx + math.cos(a) * inner, cy - math.sin(a) * inner, cx + math.cos(a) * r * 0.94,
                       cy - math.sin(a) * r * 0.94), fill=colour, width=max(1, int(r * (0.02 if i % 5 == 0 else 0.01))))
            if i % 10 == 0:
                label = "%g" % (i / 40 * maximum)
                f = font(int(r * 0.15), "SemiBold")
                box = draw.textbbox((0, 0), label, font=f)
                draw.text((cx + math.cos(a) * r * 0.62 - (box[2] - box[0]) / 2, cy - math.sin(a) * r * 0.62
                           - (box[3] - box[1]) / 2 - box[1]), label, font=f, fill=INK)
        f1 = font(int(r * 0.13), "Bold")
        b1 = draw.textbbox((0, 0), title, font=f1)
        draw.text((cx - (b1[2] - b1[0]) / 2, cy + r * 0.30), title, font=f1, fill=INK)
        f2 = font(int(r * 0.11), "Medium")
        b2 = draw.textbbox((0, 0), unit, font=f2)
        draw.text((cx - (b2[2] - b2[0]) / 2, cy + r * 0.50), unit, font=f2, fill=INK)
    return paint


def emblem():
    """Original AURORA-7 mission emblem: no agency insignia, flags or third-party marks."""
    def paint(tile, draw, w, h):
        cx, cy, r = w / 2, h / 2, min(w, h) / 2 - 3
        draw.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(18, 30, 58, 255), outline=(226, 228, 222, 255),
                     width=max(3, int(r * 0.045)))
        draw.ellipse((cx - r * 0.80, cy - r * 0.80, cx + r * 0.80, cy + r * 0.80), outline=(226, 228, 222, 255),
                     width=max(2, int(r * 0.012)))
        rng = np.random.default_rng(7)
        for _ in range(70):
            a = rng.random() * math.tau
            d = rng.random() ** 0.5 * r * 0.76
            s = 1 + rng.random() * r * 0.012
            draw.ellipse((cx + math.cos(a) * d - s, cy + math.sin(a) * d - s, cx + math.cos(a) * d + s,
                          cy + math.sin(a) * d + s), fill=(230, 232, 240, 255))
        # Mars limb (clipped to the inner disc) and a transfer arc
        limb = Image.new("RGBA", tile.size, CLEAR)
        ImageDraw.Draw(limb).ellipse((cx - r * 1.25, cy + r * 0.28, cx + r * 0.20, cy + r * 1.73), fill=(176, 84, 52, 255))
        mask = Image.new("L", tile.size, 0)
        ImageDraw.Draw(mask).ellipse((cx - r * 0.79, cy - r * 0.79, cx + r * 0.79, cy + r * 0.79), fill=255)
        tile.paste(limb, (0, 0), Image.composite(limb.getchannel("A"), Image.new("L", tile.size, 0), mask))
        draw.arc((cx - r * 0.62, cy - r * 0.50, cx + r * 0.70, cy + r * 0.62), 190, 350,
                 fill=(240, 150, 50, 255), width=max(3, int(r * 0.03)))
        # seven-point dawn star
        sx, sy = cx + r * 0.42, cy - r * 0.28
        points = []
        for i in range(14):
            a = -math.pi / 2 + i * math.pi / 7
            rr = r * (0.17 if i % 2 == 0 else 0.065)
            points.append((sx + math.cos(a) * rr, sy + math.sin(a) * rr))
        draw.polygon(points, fill=(244, 232, 190, 255))
        f = font(int(r * 0.17), "Black")
        text = "AURORA-7"
        box = draw.textbbox((0, 0), text, font=f)
        draw.text((cx - (box[2] - box[0]) / 2 - box[0], cy + r * 0.06 - box[1]), text, font=f, fill=(236, 237, 232, 255))
        f2 = font(int(r * 0.075), "SemiBold")
        text2 = "DEEP-SPACE RESEARCH VESSEL"
        box2 = draw.textbbox((0, 0), text2, font=f2)
        draw.text((cx - (box2[2] - box2[0]) / 2 - box2[0], cy + r * 0.30 - box2[1]), text2, font=f2,
                  fill=(200, 205, 214, 255))
    return paint


def switch_legend(labels):
    def paint(tile, draw, w, h):
        draw.rounded_rectangle((0, 0, w - 1, h - 1), radius=int(h * 0.10), fill=GRAPHITE)
        cell = w / len(labels)
        for index, label in enumerate(labels):
            f, box = fit_text(draw, label, cell * 0.86, h * 0.50, "SemiBold")
            draw.text((cell * index + (cell - (box[2] - box[0])) / 2 - box[0], (h - (box[3] - box[1])) / 2 - box[1]),
                      label, font=f, fill=WHITE)
            if index:
                draw.line((cell * index, h * 0.2, cell * index, h * 0.8), fill=(90, 94, 100, 255), width=2)
    return paint


def stencil_lines(lines, fill=(58, 60, 63, 255), weight="Bold"):
    """Left-aligned stencilled lines in one size, as painted on a rack fascia."""
    def paint(tile, draw, w, h):
        row = h / len(lines)
        size = min(fit_text(draw, text, w * 0.98, row * 0.72, weight)[0].size for text in lines)
        f = font(size, weight)
        for index, text in enumerate(lines):
            box = draw.textbbox((0, 0), text, font=f)
            draw.text((w * 0.01 - box[0], row * index + (row - (box[3] - box[1])) / 2 - box[1]), text, font=f, fill=fill)
    return paint


def breaker_tags(codes, ratings):
    """One strip for a row of breakers: a yellow rating tag and a white circuit number per module."""
    def paint(tile, draw, w, h):
        cell = w / len(codes)
        for index, (code, rating) in enumerate(zip(codes, ratings)):
            x = cell * index
            draw.rounded_rectangle((x + cell * 0.12, h * 0.03, x + cell * 0.88, h * 0.50), radius=max(2, int(h * 0.06)), fill=YELLOW)
            f, box = fit_text(draw, rating, cell * 0.62, h * 0.34, "Bold")
            draw.text((x + (cell - (box[2] - box[0])) / 2 - box[0], h * 0.265 - (box[3] - box[1]) / 2 - box[1]), rating, font=f, fill=INK)
            draw.rectangle((x + cell * 0.12, h * 0.60, x + cell * 0.88, h * 0.97), fill=WHITE)
            f, box = fit_text(draw, code, cell * 0.66, h * 0.25, "SemiBold")
            draw.text((x + (cell - (box[2] - box[0])) / 2 - box[0], h * 0.785 - (box[3] - box[1]) / 2 - box[1]), code, font=f, fill=INK)
    return paint


def isolator_face():
    """Position marks around a rotary isolator handle: O (off) at nine o'clock, I (on) at twelve."""
    def paint(tile, draw, w, h):
        cx, cy = w / 2, h * 0.46
        radius = w * 0.37
        line = max(2, int(w * 0.014))
        draw.ellipse((cx - radius, cy - radius, cx + radius, cy + radius), outline=INK, width=line)
        for text, (px, py) in (("O", (0.075, 0.46)), ("I", (0.5, 0.045))):
            f, box = fit_text(draw, text, w * 0.12, h * 0.11, "Black")
            draw.text((w * px - (box[2] - box[0]) / 2 - box[0], h * py - (box[3] - box[1]) / 2 - box[1]), text, font=f, fill=INK)
        for text, px in (("OFF", 0.13), ("ON", 0.87)):
            f, box = fit_text(draw, text, w * 0.22, h * 0.065, "Bold")
            draw.text((w * px - (box[2] - box[0]) / 2 - box[0], h * 0.07 - (box[3] - box[1]) / 2 - box[1]), text, font=f, fill=INK)
        f, box = fit_text(draw, "MAIN ISOLATOR", w * 0.92, h * 0.075, "Bold")
        draw.text(((w - (box[2] - box[0])) / 2 - box[0], h * 0.945 - (box[3] - box[1]) / 2 - box[1]), "MAIN ISOLATOR", font=f, fill=INK)
    return paint


def canister_label(bed, serial):
    """Wrap-around label of a replaceable sorbent canister."""
    def paint(tile, draw, w, h):
        draw.rounded_rectangle((0, 0, w - 1, h - 1), radius=int(w * 0.04), fill=WHITE)
        band = int(h * 0.20)
        draw.rounded_rectangle((0, 0, w - 1, band), radius=int(w * 0.04), fill=YELLOW)
        draw.rectangle((0, band // 2, w - 1, band), fill=YELLOW)
        f, box = fit_text(draw, "CO2 SORBENT", w * 0.86, band * 0.52, "Bold")
        draw.text(((w - (box[2] - box[0])) / 2 - box[0], (band - (box[3] - box[1])) / 2 - box[1]), "CO2 SORBENT", font=f, fill=INK)
        f, box = fit_text(draw, "BED " + bed, w * 0.70, h * 0.13, "Black")
        draw.text((w * 0.08 - box[0], h * 0.25 - box[1]), "BED " + bed, font=f, fill=INK)
        size = max(7, int(h * 0.048))
        for index, line in enumerate(("LiOH CARTRIDGE  2.4 kg", "P/N 7A-2210-04", "S/N " + serial, "REPLACE AT 0.7 kPa ppCO2",
                                      "CLOSE VALVE V-3 FIRST")):
            draw.text((w * 0.08, h * (0.43 + 0.075 * index)), line, font=font(size, "Medium"), fill=INK)
        x = w * 0.08
        code_rng = np.random.default_rng(zlib.crc32(serial.encode("ascii")))
        while x < w * 0.92:
            bar = max(1, int(code_rng.integers(1, 4) * w * 0.006))
            draw.rectangle((x, h * 0.84, x + bar, h * 0.95), fill=INK)
            x += bar + max(1, int(code_rng.integers(1, 4) * w * 0.006))
        draw.rounded_rectangle((1, 1, w - 2, h - 2), radius=int(w * 0.04), outline=INK, width=2)
    return paint


def build_decals():
    atlas = Atlas(4096, PPM)
    add = atlas.add

    # Service rack fascias and the hardware inside the two repair bays (assets A02 and A03)
    add("fascia_power", 0.62, 0.20, stencil_lines(["POWER", "DISTRIBUTION"]))
    add("fascia_scrubber", 0.62, 0.20, stencil_lines(["LIFE-SUPPORT", "SCRUBBER"]))
    add("brk_main", 0.10, 0.034, breaker_tags(["MAIN  Q1"], ["63 A"]))
    add("brk_row_a", 0.40, 0.034, breaker_tags(["CB-%02d" % i for i in range(1, 9)],
                                                ["C16", "C16", "C10", "C10", "C10", "B6", "B6", "C20"]))
    add("brk_row_b", 0.45, 0.034, breaker_tags(["CB-%02d" % i for i in range(9, 18)],
                                                ["C10", "C10", "B6", "B6", "C16", "C16", "B10", "B10", "C6"]))
    add("isolator_face", 0.19, 0.19, isolator_face())
    for bed, serial in (("A", "0417"), ("B", "0418"), ("C", "0422")):
        add("can_label_" + bed.lower(), 0.115, 0.16, canister_label(bed, serial))

    # Compartment and portal signage
    for key, number, name in (("sign_flightdeck", "01", "FLIGHT DECK"), ("sign_engineering", "02", "ENGINEERING"),
                              ("sign_logistics", "03", "LOGISTICS"), ("sign_airlock", "04", "AIRLOCK")):
        add(key, 0.62, 0.10, two_line(GRAPHITE, WHITE, number + " / " + name, "AURORA-7  PRESSURISED VOLUME",
                                      accent=ORANGE))
    add("stencil_aurora", 0.90, 0.13, stencil("AURORA-7", (40, 44, 50, 255), "Black"))
    add("stencil_vessel", 0.80, 0.045, stencil("DEEP-SPACE RESEARCH VESSEL", (60, 64, 70, 255), "SemiBold"))
    add("emblem", 0.36, 0.36, emblem())

    # Rack identifiers (location codes follow compartment / side / bay)
    for index, code in enumerate(["FD-P1", "FD-P2", "FD-S1", "FD-S2", "EN-P1", "EN-P2", "EN-P3", "EN-P4", "EN-P5",
                                  "EN-S1", "EN-S2", "EN-S3", "EN-S4", "EN-S5", "LG-P1", "LG-P2", "LG-P3", "LG-P4",
                                  "LG-S1", "LG-S2", "LG-S3", "LG-S4", "AL-P1", "AL-S1"]):
        add("rack_%02d" % index, 0.16, 0.045, plate(WHITE, INK, code, "Bold", border=INK))

    titles = [
        ("t_eps", "EPS-2  POWER DISTRIBUTION"), ("t_battery", "BATTERY ORU  28 V DC"), ("t_bus_a", "MAIN BUS A"),
        ("t_bus_b", "MAIN BUS B"), ("t_eclss", "ECLSS-1  AIR REVITALISATION"), ("t_co2", "CO2 REMOVAL BED"),
        ("t_o2", "OXYGEN SUPPLY"), ("t_h2o", "POTABLE WATER"), ("t_thermal", "THERMAL CONTROL LOOP"),
        ("t_avionics", "AVIONICS BAY"), ("t_comm", "COMM / TRACKING"), ("t_gnc", "GUIDANCE AND NAVIGATION"),
        ("t_stow", "STOWAGE"), ("t_spares", "SPARE PARTS"), ("t_food", "FOOD PROVISIONS"), ("t_med", "CREW MEDICAL"),
        ("t_tools", "IVA TOOLS"), ("t_eva", "EVA EQUIPMENT"), ("t_suit", "SUIT STOWAGE"), ("t_uia", "UMBILICAL INTERFACE"),
        ("t_hatch", "EVA HATCH"), ("t_depress", "DEPRESS VALVE"), ("t_science", "SCIENCE PAYLOAD"),
        ("t_sample", "SAMPLE STOWAGE"), ("t_fan", "CABIN FAN"), ("t_smoke", "SMOKE DETECTOR"),
        ("t_lighting", "GENERAL LIGHTING"), ("t_o2_stored", "STORED OXYGEN  kg"), ("t_n2", "NITROGEN"),
        ("t_filter", "HEPA FILTER"), ("t_scrub", "LiOH CANISTER"), ("t_pump", "COOLANT PUMP"),
    ]
    for key, text in titles:
        add(key, 0.34, 0.042, plate(GRAPHITE, WHITE, text, "SemiBold"))

    cautions = [
        ("c_hv", "CAUTION", "120 V DC BUS"), ("c_pinch", "CAUTION", "PINCH POINT"),
        ("c_hot", "CAUTION", "HOT SURFACE"), ("c_press", "CAUTION", "PRESSURISED LINE"),
        ("c_o2", "CAUTION", "OXYGEN  NO OIL OR GREASE"), ("c_rotate", "CAUTION", "ROTATING MACHINERY"),
        ("c_esd", "CAUTION", "STATIC SENSITIVE"), ("c_keepclear", "CAUTION", "KEEP HATCH PATH CLEAR"),
    ]
    for key, top, bottom in cautions:
        add(key, 0.20, 0.12, two_line(ORANGE, INK, top, bottom, border=INK))
    warnings = [("w_fire", "FIRE PORT", "INSERT EXTINGUISHER NOZZLE"), ("w_emerg", "EMERGENCY", "PORTABLE BREATHING APPARATUS"),
                ("w_depress", "DANGER", "VACUUM BEYOND THIS HATCH"), ("w_extinguisher", "FIRE", "PORTABLE EXTINGUISHER")]
    for key, top, bottom in warnings:
        add(key, 0.22, 0.10, two_line(RED, WHITE, top, bottom, border=WHITE))

    add("hazard_long", 0.60, 0.03, hazard())
    add("hazard_short", 0.24, 0.03, hazard())
    add("hazard_red", 0.40, 0.03, hazard(RED, WHITE))
    for key, text, direction in (("exit_right", "EXIT", 1), ("exit_left", "EXIT", -1)):
        add(key, 0.22, 0.07, arrow_label(text, GREEN, INK, direction))
    add("blue_handrail", 0.20, 0.035, plate(BLUE, WHITE, "TRANSLATION AID", "SemiBold"))
    add("blue_tether", 0.20, 0.035, plate(BLUE, WHITE, "TETHER POINT", "SemiBold"))
    add("velcro_blue", 0.045, 0.045, velcro((34, 78, 148, 255)))
    add("velcro_white", 0.045, 0.045, velcro((226, 226, 220, 255)))
    add("velcro_tan", 0.07, 0.035, velcro((176, 160, 128, 255)))

    placards = [
        ("p_hatch", "HATCH OPERATION", ["Verify delta-P below limit", "Unlock latch drive", "Rotate crank to OPEN",
                                        "Stow hatch clear of path"], ORANGE),
        ("p_egress", "EMERGENCY EGRESS", ["Don breathing apparatus", "Follow green markers", "Close hatch behind you",
                                          "Report on voice loop"], RED),
        ("p_eva", "EVA PREPARATION", ["Suit integrity check", "Verify tether attached", "Depress to vacuum",
                                      "Confirm go from cabin"], BLUE),
        ("p_repair", "ORU REPLACEMENT", ["Open circuit protection", "Release captive fasteners", "Demate connectors",
                                         "Install spare, torque, verify"], ORANGE),
        ("p_fire", "FIRE RESPONSE", ["Stop ventilation", "Locate source with detector", "Discharge at fire port",
                                     "Monitor cabin atmosphere"], RED),
        ("p_stow", "STOWAGE RULES", ["Restrain every item", "Scan barcode on move", "Keep vents unobstructed",
                                     "Hatch path stays clear"], BLUE),
    ]
    for key, title, lines, colour in placards:
        add(key, 0.26, 0.19, placard(title, lines, colour))

    add("gauge_cabin", 0.11, 0.11, gauge_face("CABIN", "kPa", 120, 108))
    add("gauge_o2", 0.11, 0.11, gauge_face("O2 TANK", "MPa", 40, 34))
    add("gauge_airlock", 0.14, 0.14, gauge_face("AIRLOCK", "kPa", 120, 108))
    add("gauge_coolant", 0.09, 0.09, gauge_face("COOLANT", "kPa", 600, 520))

    legends = [("leg_power", ["BUS A", "BUS B", "BATT", "SOLAR", "XFEED"]), ("leg_eclss", ["FAN", "CDRA", "O2", "H2O", "VENT"]),
               ("leg_comm", ["HGA", "LGA", "XPNDR", "REC", "TEST"]), ("leg_light", ["FWD", "MID", "AFT", "LOCK", "EMER"]),
               ("leg_gnc", ["IMU", "STAR", "RCS", "MAIN", "AUTO"]), ("leg_airlock", ["PUMP", "EQUAL", "VENT", "LIGHT", "COMM"])]
    for key, labels in legends:
        add(key, 0.30, 0.026, switch_legend(labels))

    connectors = ["J1 PWR IN", "J2 PWR OUT", "J3 DATA A", "J4 DATA B", "J5 1553", "J6 ETHERNET", "J7 VIDEO", "J8 AUDIO",
                  "QD-1 O2", "QD-2 N2", "QD-3 H2O", "QD-4 COOLANT", "GND", "TP-1", "TP-2", "VENT"]
    for index, text in enumerate(connectors):
        add("conn_%02d" % index, 0.085, 0.022, plate(WHITE, INK, text, "SemiBold"))

    rng = np.random.default_rng(99)
    for index in range(24):
        code = "A7%05d" % int(rng.integers(100, 99999))
        add("barcode_%02d" % index, 0.07, 0.035, barcode(code))
    for index in range(28):
        text = "P/N 7A-%04d-%02d   S/N %04d" % (int(rng.integers(1000, 9999)), int(rng.integers(1, 30)), int(rng.integers(1, 400)))
        add("pn_%02d" % index, 0.13, 0.018, plate(WHITE, INK, text, "Medium"))

    bags = ["CTB 01  FOOD", "CTB 02  FOOD", "CTB 03  CLOTHING", "CTB 04  HYGIENE", "CTB 05  SPARES", "CTB 06  FILTERS",
            "CTB 07  CABLES", "CTB 08  TOOLS", "CTB 09  MEDICAL", "CTB 10  SCIENCE", "CTB 11  EVA", "CTB 12  TRASH",
            "CTB 13  WATER", "CTB 14  LIOH", "CTB 15  SAMPLES", "CTB 16  OFFICE"]
    for index, text in enumerate(bags):
        add("bag_%02d" % index, 0.20, 0.06, two_line(WHITE, INK, text, "STOWAGE  LG-%s%d" % ("PS"[index % 2], index // 2 + 1),
                                                     border=INK))

    deck = ["A7-D01", "A7-D02", "A7-D03", "A7-D04", "A7-D05", "A7-D06", "A7-D07", "A7-D08"]
    for index, text in enumerate(deck):
        add("deck_%02d" % index, 0.20, 0.05, stencil(text, (70, 74, 80, 255), "Bold"))
    add("stencil_noslip", 0.50, 0.05, stencil("TRANSLATION PATH  KEEP CLEAR", (70, 74, 80, 255), "SemiBold"))
    add("stencil_notstep", 0.22, 0.045, stencil("NO STEP", (160, 40, 34, 255), "Black"))
    add("stencil_eva", 0.50, 0.08, stencil("EVA HATCH", (40, 44, 50, 255), "Black"))
    add("seat_cmd", 0.16, 0.04, plate(GRAPHITE, WHITE, "SEAT 1  COMMANDER", "SemiBold"))
    add("seat_fe", 0.16, 0.04, plate(GRAPHITE, WHITE, "SEAT 2  FLIGHT ENG", "SemiBold"))
    add("fastener_note", 0.16, 0.022, plate(YELLOW, INK, "CAPTIVE FASTENERS  4 PL", "SemiBold"))
    add("torque_note", 0.16, 0.022, plate(YELLOW, INK, "TORQUE 5.6 N-m", "SemiBold"))
    add("handle_note", 0.12, 0.022, plate(YELLOW, INK, "HANDLE  PULL", "SemiBold"))
    add("airflow", 0.16, 0.035, arrow_label("AIRFLOW", WHITE, INK, 1))
    atlas.save("A7_Decals")


# --------------------------------------------------------------------------- display faces
CYAN = (110, 215, 235, 255)
AMBER = (245, 170, 60, 255)
SOFT = (150, 160, 172, 255)
DIM = (60, 68, 80, 255)
SCREEN_BG = (6, 9, 13, 255)


def screen_frame(draw, w, h, title, page):
    draw.rectangle((0, 0, w, h), fill=SCREEN_BG)
    draw.rectangle((0, 0, w, int(h * 0.075)), fill=(14, 20, 28, 255))
    draw.text((w * 0.02, h * 0.016), title, font=font(h * 0.042, "SemiBold"), fill=CYAN)
    draw.text((w * 0.80, h * 0.016), page, font=font(h * 0.036, "Medium"), fill=SOFT)
    draw.line((0, int(h * 0.075), w, int(h * 0.075)), fill=DIM, width=2)
    keys = ["FLT", "SYS", "EPS", "ECLS", "COMM", "NAV", "CWS", "MENU"]
    cell = w / len(keys)
    for index, key in enumerate(keys):
        x0 = cell * index + cell * 0.08
        draw.rounded_rectangle((x0, h * 0.925, x0 + cell * 0.84, h * 0.985), radius=4, outline=DIM, width=2)
        f = font(h * 0.032, "SemiBold")
        box = draw.textbbox((0, 0), key, font=f)
        draw.text((x0 + (cell * 0.84 - (box[2] - box[0])) / 2 - box[0], h * 0.937 - box[1] + 2), key, font=f, fill=SOFT)


def screen_flight(tile, draw, w, h):
    screen_frame(draw, w, h, "AURORA-7  FLIGHT", "PAGE 1/3")
    cx, cy, r = w * 0.30, h * 0.50, h * 0.30
    draw.ellipse((cx - r, cy - r, cx + r, cy + r), outline=SOFT, width=3)
    for deg in range(0, 360, 10):
        a = math.radians(deg)
        inner = r * (0.88 if deg % 30 else 0.80)
        draw.line((cx + math.cos(a) * inner, cy + math.sin(a) * inner, cx + math.cos(a) * r, cy + math.sin(a) * r),
                  fill=SOFT, width=2)
    draw.line((cx - r * 0.72, cy + r * 0.10, cx + r * 0.72, cy - r * 0.10), fill=CYAN, width=4)
    draw.line((cx - r * 0.16, cy, cx - r * 0.05, cy), fill=AMBER, width=5)
    draw.line((cx + r * 0.05, cy, cx + r * 0.16, cy), fill=AMBER, width=5)
    draw.ellipse((cx - 5, cy - 5, cx + 5, cy + 5), fill=AMBER)
    draw.text((cx - r, cy + r * 1.08), "ATTITUDE  LVLH HOLD", font=font(h * 0.034, "Medium"), fill=SOFT)
    x0 = w * 0.58
    rows = [("MODE", "CRUISE"), ("GUIDANCE", "INERTIAL"), ("RCS", "ARMED"), ("STAR TRACKER", "TRACKING"),
            ("IMU", "ALIGNED"), ("MAIN ENGINE", "SAFED")]
    for index, (label, value) in enumerate(rows):
        y = h * (0.14 + index * 0.115)
        draw.text((x0, y), label, font=font(h * 0.033, "Medium"), fill=SOFT)
        draw.text((x0, y + h * 0.04), value, font=font(h * 0.050, "SemiBold"), fill=CYAN)
        draw.line((x0, y + h * 0.105, w * 0.96, y + h * 0.105), fill=DIM, width=1)


def screen_systems(tile, draw, w, h):
    screen_frame(draw, w, h, "AURORA-7  SYSTEMS", "SYNOPTIC")
    boxes = [("SOLAR", 0.08, 0.16), ("BATT", 0.08, 0.40), ("BUS A", 0.36, 0.16), ("BUS B", 0.36, 0.40),
             ("ECLSS", 0.66, 0.12), ("AVIONICS", 0.66, 0.30), ("COMM", 0.66, 0.48), ("THERMAL", 0.36, 0.66),
             ("PAYLOAD", 0.66, 0.66)]
    centres = {}
    for name, bx, by in boxes:
        x0, y0, x1, y1 = w * bx, h * by, w * (bx + 0.22), h * (by + 0.12)
        draw.rounded_rectangle((x0, y0, x1, y1), radius=6, outline=CYAN, width=3)
        f = font(h * 0.040, "SemiBold")
        box = draw.textbbox((0, 0), name, font=f)
        draw.text(((x0 + x1) / 2 - (box[2] - box[0]) / 2 - box[0], (y0 + y1) / 2 - (box[3] - box[1]) / 2 - box[1]),
                  name, font=f, fill=CYAN)
        centres[name] = ((x0 + x1) / 2, (y0 + y1) / 2, x0, x1)
    links = [("SOLAR", "BUS A"), ("BATT", "BUS B"), ("BUS A", "ECLSS"), ("BUS A", "AVIONICS"), ("BUS B", "COMM"),
             ("BUS B", "THERMAL"), ("BUS B", "PAYLOAD")]
    for a, b in links:
        ax, ay, _, ax1 = centres[a]
        bx, by, bx0, _ = centres[b]
        if abs(ax - bx) < 1.0:
            draw.line((ax, ay + h * 0.06, bx, by - h * 0.06), fill=SOFT, width=3)
            continue
        mid = (ax1 + bx0) / 2
        draw.line((ax1, ay, mid, ay, mid, by, bx0, by), fill=SOFT, width=3)
    draw.text((w * 0.08, h * 0.84), "SCHEMATIC ONLY  -  LIVE VALUES ON HUD", font=font(h * 0.030, "Medium"), fill=DIM)


def screen_comm(tile, draw, w, h):
    screen_frame(draw, w, h, "AURORA-7  COMM / TRACKING", "LINK")
    rng = np.random.default_rng(5)
    x0, x1, y0, y1 = w * 0.06, w * 0.94, h * 0.16, h * 0.52
    draw.rectangle((x0, y0, x1, y1), outline=DIM, width=2)
    for i in range(1, 8):
        gx = x0 + (x1 - x0) * i / 8
        draw.line((gx, y0, gx, y1), fill=(22, 30, 40, 255), width=1)
    points = []
    for i in range(160):
        t = i / 159
        value = 0.5 + 0.10 * math.sin(t * 34) + 0.05 * (rng.random() - 0.5) + 0.22 * math.exp(-((t - 0.52) / 0.03) ** 2)
        points.append((x0 + (x1 - x0) * t, y1 - (y1 - y0) * value))
    draw.line(points, fill=CYAN, width=3)
    rows = [("HIGH GAIN ANTENNA", "POINTING"), ("LOW GAIN ANTENNA", "STANDBY"), ("TRANSPONDER", "RECEIVE"),
            ("RECORDER", "BUFFERING")]
    for index, (label, value) in enumerate(rows):
        y = h * (0.58 + index * 0.08)
        draw.text((w * 0.06, y), label, font=font(h * 0.036, "Medium"), fill=SOFT)
        draw.text((w * 0.60, y), value, font=font(h * 0.038, "SemiBold"), fill=CYAN)


def screen_eclss(tile, draw, w, h):
    screen_frame(draw, w, h, "AURORA-7  LIFE SUPPORT", "LOOP")
    names = ["CABIN AIR", "CO2 BED A", "CO2 BED B", "O2 SUPPLY", "WATER", "CABIN FAN"]
    for index, name in enumerate(names):
        col, row = index % 3, index // 3
        x0, y0 = w * (0.06 + col * 0.31), h * (0.16 + row * 0.36)
        x1, y1 = x0 + w * 0.27, y0 + h * 0.30
        draw.rounded_rectangle((x0, y0, x1, y1), radius=8, outline=DIM, width=2)
        draw.text((x0 + w * 0.015, y0 + h * 0.02), name, font=font(h * 0.034, "SemiBold"), fill=SOFT)
        bx0, bx1, by = x0 + w * 0.015, x1 - w * 0.015, y0 + h * 0.20
        draw.rounded_rectangle((bx0, by, bx1, by + h * 0.035), radius=4, outline=CYAN, width=2)
        draw.text((x0 + w * 0.015, y0 + h * 0.10), "SEE HUD", font=font(h * 0.050, "SemiBold"), fill=CYAN)


def screen_small(text_top, text_bottom, colour):
    def paint(tile, draw, w, h):
        draw.rectangle((0, 0, w, h), fill=SCREEN_BG)
        draw.rectangle((0, 0, w, int(h * 0.20)), fill=(14, 20, 28, 255))
        draw.text((w * 0.04, h * 0.03), text_top, font=font(h * 0.13, "SemiBold"), fill=SOFT)
        f, box = fit_text(draw, text_bottom, w * 0.90, h * 0.34, "SemiBold")
        draw.text(((w - (box[2] - box[0])) / 2 - box[0], h * 0.50 - box[1]), text_bottom, font=f, fill=colour)
        for i in range(12):
            x = w * (0.06 + i * 0.075)
            draw.rectangle((x, h * 0.86, x + w * 0.05, h * 0.92), fill=colour if i < 8 else DIM)
    return paint


def build_screens():
    atlas = Atlas(2048, 1.0, background=(0, 0, 0, 255), pad=8)
    # Sizes here are pixels (ppm = 1); physical size is decided by the mesh that shows them.
    atlas.add("mfd_flight", 1000, 625, lambda t, d, w, h: screen_flight(t, d, w, h))
    atlas.add("mfd_systems", 1000, 625, lambda t, d, w, h: screen_systems(t, d, w, h))
    atlas.add("mfd_comm", 1000, 625, lambda t, d, w, h: screen_comm(t, d, w, h))
    atlas.add("mfd_eclss", 1000, 625, lambda t, d, w, h: screen_eclss(t, d, w, h))
    small = [("sm_power", "EPS-2", "BUS STATUS", CYAN), ("sm_eclss", "ECLSS-1", "AIR LOOP", CYAN),
             ("sm_airlock", "AIRLOCK", "PRESSURE", AMBER), ("sm_stow", "LOGISTICS", "INVENTORY", CYAN),
             ("sm_thermal", "THERMAL", "LOOP A", CYAN), ("sm_comm", "COMM", "LINK", AMBER)]
    for key, top, bottom, colour in small:
        atlas.add(key, 480, 300, screen_small(top, bottom, colour))
    atlas.save("A7_Screens")


def main():
    print("font:", INTER or "Pillow embedded Aileron (CC0)")
    if ONLY in (None, "tiling"):
        print("tiling sets")
        gen_paint()
        gen_anodized()
        gen_brushed()
        gen_fabric()
        gen_quilt()
        gen_deck()
        gen_grate()
        gen_rubber()
        gen_carbon()
        gen_mli()
        gen_solar()
    if ONLY in (None, "decals"):
        print("decal atlas")
        build_decals()
    if ONLY in (None, "screens"):
        print("screen atlas")
        build_screens()
    print("done ->", OUT)


if __name__ == "__main__":
    main()
