"""CARGO-CASES-V02 procedural textures (original, generated; no external images).

Run with system Python 3 (numpy + Pillow):  python cc2_tex.py
Writes tiling 1024 px BaseColor + Normal pairs and the 2048 px label atlas into ../Textures and the atlas
rectangles (UV space, v up) into cc2_atlas.json for build_cargo_cases_v02.py.
The only external input is the project's own OFL font Assets/_OutOfReach/UI/Typography/BarlowCondensed-Bold.ttf.
"""
import json
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "Textures"))
FONT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "..", "UI", "Typography", "BarlowCondensed-Bold.ttf"))
N = 1024
rng = np.random.default_rng(7031)


def spectral(n, beta, seed, aniso=(1.0, 1.0)):
    """Tileable fractal noise in 0..1 (random phase, 1/f^beta amplitude)."""
    r = np.random.default_rng(seed)
    fy = np.fft.fftfreq(n)[:, None] * aniso[1]
    fx = np.fft.fftfreq(n)[None, :] * aniso[0]
    f = np.sqrt(fx * fx + fy * fy)
    f[0, 0] = 1.0
    amp = 1.0 / f ** beta
    amp[0, 0] = 0.0
    spec = amp * np.exp(2j * np.pi * r.random((n, n)))
    img = np.real(np.fft.ifft2(spec))
    img -= img.min()
    return img / img.max()


def normal_from_height(h, strength):
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * strength
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * strength
    nz = np.ones_like(h)
    l = np.sqrt(dx * dx + dy * dy + nz * nz)
    n = np.stack([-dx / l, dy / l, nz / l], -1)  # image y is down: +G = up in UV space
    return (np.clip(n * .5 + .5, 0, 1) * 255).astype(np.uint8)


def save_rgb(name, rgb):
    Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8), "RGB").save(os.path.join(OUT, name + ".png"), optimize=True)


def save_pair(name, albedo, height, strength):
    if albedo.ndim == 2:
        albedo = np.repeat(albedo[:, :, None], 3, 2)
    save_rgb(name + "_BaseColor", albedo)
    Image.fromarray(normal_from_height(height, strength), "RGB").save(os.path.join(OUT, name + "_Normal.png"), optimize=True)


def wrapped_lines(count, seed, min_len, max_len, width=1, blur=.6):
    """Random thin scratches, drawn 9 times with offsets so the image tiles."""
    r = np.random.default_rng(seed)
    img = Image.new("L", (N, N), 0)
    d = ImageDraw.Draw(img)
    for _ in range(count):
        x, y = r.random(2) * N
        a = r.random() * np.pi
        if r.random() < .6:
            a = r.normal(0, .25)
        l = min_len + r.random() ** 2 * (max_len - min_len)
        dx, dy = np.cos(a) * l, np.sin(a) * l
        v = int(120 + r.random() * 135)
        for ox in (-N, 0, N):
            for oy in (-N, 0, N):
                d.line([(x + ox, y + oy), (x + dx + ox, y + dy + oy)], fill=v, width=width)
    return np.asarray(img.filter(ImageFilter.GaussianBlur(blur)), np.float32) / 255.0


def polymer():
    grain = spectral(N, .35, 11)
    pebble = spectral(N, 1.1, 12)
    mottle = spectral(N, 2.2, 13)
    scr = wrapped_lines(170, 14, 12, 150)
    scuff = np.clip(spectral(N, 1.7, 15) - .62, 0, 1) * 2.4 * spectral(N, .6, 16)
    h = grain * .5 + pebble * .5 - scr * .7
    a = .74 + (mottle - .5) * .12 + (grain - .5) * .05 + scr * .22 + scuff * .18
    save_pair("CC2_Polymer", a, h, 5.0)


def painted():
    peel = spectral(N, 1.6, 21)
    fine = spectral(N, .4, 22)
    mottle = spectral(N, 2.4, 23)
    chips = np.clip(spectral(N, 1.35, 24) - .7, 0, 1)
    chips = np.clip(chips * 14, 0, 1)
    scr = wrapped_lines(90, 25, 20, 260)
    h = peel * .8 + fine * .12 - chips * .9 - scr * .5
    a = .72 + (mottle - .5) * .14 + (fine - .5) * .03
    a = a * (1 - chips) + chips * .97 + scr * .16
    save_pair("CC2_Painted", a, h, 4.0)


def aluminium():
    streak = spectral(N, 1.0, 31, aniso=(.02, 3.0))
    fine = spectral(N, .2, 32, aniso=(.05, 1.0))
    mottle = spectral(N, 2.3, 33)
    pits = np.clip(spectral(N, .9, 34) - .78, 0, 1) * 5
    scr = wrapped_lines(120, 35, 30, 400)
    a = .80 + (streak - .5) * .16 + (fine - .5) * .06 + (mottle - .5) * .08 - pits * .25 + scr * .1
    h = streak * .5 + fine * .2 - pits - scr * .4
    save_pair("CC2_Aluminium", a, h, 2.2)


def quilt():
    y, x = np.mgrid[0:N, 0:N] / N
    cells = 8
    u = (x + y) * cells
    v = (x - y) * cells
    pu = np.abs(np.sin(np.pi * u))
    pv = np.abs(np.sin(np.pi * v))
    pillow = (pu * pv) ** .42
    seam = np.clip(1 - np.minimum(pu, pv) * 9, 0, 1)
    weave = (np.sin(x * N * np.pi / 2) * np.sin(y * N * np.pi / 2)) * .5 + .5
    wr = spectral(N, 1.4, 41)
    stitch = seam * ((np.sin((u + v) * np.pi * 14) > 0) | (np.sin((u - v) * np.pi * 14) > 0))
    h = pillow * 1.0 + weave * .05 + wr * .12 - seam * .25
    a = .70 + pillow * .14 + (weave - .5) * .05 + (wr - .5) * .1 - seam * .2 + stitch * .12
    save_pair("CC2_Quilt", a, h, 7.0)


def composite():
    y, x = np.mgrid[0:N, 0:N] / N
    b1 = np.sin((x * 3 + y * 6) * 2 * np.pi * 2)
    b2 = np.sin((x * 3 - y * 6) * 2 * np.pi * 2)
    tow1 = np.sin((x * 3 + y * 6) * 2 * np.pi * 32) * .5 + .5
    tow2 = np.sin((x * 3 - y * 6) * 2 * np.pi * 32) * .5 + .5
    over = (b1 * b2 > 0)
    fibre = np.where(over, tow1, tow2)
    resin = spectral(N, 1.8, 51)
    h = np.where(over, .55, .45) + fibre * .12 + resin * .15
    a = .80 + (fibre - .5) * .10 + np.where(over, .03, -.03) + (resin - .5) * .10
    save_pair("CC2_Composite", a, h, 3.0)


def webbing():
    y, x = np.mgrid[0:N, 0:N] / N
    rows = 48
    ry = y * rows
    twill = np.sin((x * 96 + np.floor(ry) * .5) * 2 * np.pi) * .5 + .5
    rib = np.abs(np.sin(ry * np.pi)) ** .6
    edge = np.clip(1 - np.abs((y * 4) % 1 - .5) * 2, 0, 1)  # four straps' worth of selvedge per tile
    stripe = (np.abs((y * 4) % 1 - .5) < .07).astype(np.float32)
    fuzz = spectral(N, .5, 61)
    h = rib * .7 + twill * .25 + fuzz * .1
    a = .70 + rib * .12 + (twill - .5) * .10 + (fuzz - .5) * .08 - stripe * .22 - (edge < .08) * .15
    save_pair("CC2_Webbing", a, h, 5.0)


def rubber():
    y, x = np.mgrid[0:N, 0:N] / N
    k = 40
    d1 = np.abs(np.sin((x + y) * k * np.pi))
    d2 = np.abs(np.sin((x - y) * k * np.pi))
    knurl = np.minimum(d1, d2) ** .7
    fine = spectral(N, .5, 71)
    dust = spectral(N, 2.0, 72)
    h = knurl * .9 + fine * .15
    a = .62 + knurl * .16 + (fine - .5) * .08 + np.clip(dust - .55, 0, 1) * .5
    save_pair("CC2_Rubber", a, h, 4.0)


def solar():
    cells_x, cells_y = 4, 8
    y, x = np.mgrid[0:N, 0:N] / N
    cx = (x * cells_x) % 1
    cy = (y * cells_y) % 1
    gap = (np.minimum(cx, 1 - cx) < .03) | (np.minimum(cy, 1 - cy) < .06)
    corner = (np.minimum(cx, 1 - cx) + np.minimum(cy, 1 - cy) * .5) < .09
    fingers = (np.abs((y * cells_y * 22) % 1 - .5) < .08)
    bus = (np.abs(cx - .3) < .012) | (np.abs(cx - .7) < .012)
    tint = spectral(N, 2.0, 81)
    rgb = np.zeros((N, N, 3), np.float32)
    base = np.stack([.035 + tint * .03, .06 + tint * .05, .20 + tint * .14], -1)
    rgb[:] = base
    rgb[fingers] = rgb[fingers] * .6 + np.array([.45, .47, .5]) * .4
    rgb[bus] = np.array([.72, .73, .75])
    rgb[gap | corner] = np.array([.78, .66, .36])  # polyimide substrate between cells
    h = np.where(gap | corner, 0.0, .6) + bus * .25 + fingers * .05
    save_rgb("CC2_SolarCell_BaseColor", rgb)
    Image.fromarray(normal_from_height(h.astype(np.float32), 3.0), "RGB").save(os.path.join(OUT, "CC2_SolarCell_Normal.png"), optimize=True)


# ---------------------------------------------------------------- label atlas
A = 2048
BRIDGED = set("ABDOPQR04689")


def stencil_text(text, px, ink):
    """RGBA image of bold condensed text with stencil bridges through closed counters."""
    font = ImageFont.truetype(FONT, px)
    widths = [font.getlength(ch) for ch in text]
    track = px * .09
    w = int(sum(widths) + track * (len(text) - 1) + px * .2)
    asc, desc = font.getmetrics()
    hgt = asc + desc
    mask = Image.new("L", (w, hgt), 0)
    d = ImageDraw.Draw(mask)
    x = px * .1
    for ch, cw in zip(text, widths):
        d.text((x, 0), ch, font=font, fill=255)
        if ch in BRIDGED:
            bx = x + cw * .5
            bw = max(3, px * .075)
            d.rectangle([bx - bw / 2, 0, bx + bw / 2, hgt], fill=0)
        x += cw + track
    bbox = mask.getbbox()
    mask = mask.crop(bbox)
    # paint: slight overspray softness and worn specks
    r = np.random.default_rng(len(text) * 131 + px)
    m = np.asarray(mask.filter(ImageFilter.GaussianBlur(px * .006)), np.float32) / 255
    wear = spectral(256, 1.2, 900 + len(text))
    wear = np.asarray(Image.fromarray((wear * 255).astype(np.uint8)).resize(mask.size, Image.BICUBIC), np.float32) / 255
    m = m * np.clip(1.25 - np.clip(wear - .62, 0, 1) * 3.5, 0, 1)
    img = np.zeros((mask.size[1], mask.size[0], 4), np.uint8)
    img[..., 0], img[..., 1], img[..., 2] = ink
    img[..., 3] = (np.clip(m, 0, 1) * 255).astype(np.uint8)
    return Image.fromarray(img, "RGBA")


def fit(img, w, h):
    s = min(w / img.width, h / img.height)
    return img.resize((max(1, int(img.width * s)), max(1, int(img.height * s))), Image.LANCZOS)


CASES = [  # id, short name, kg, ink
    ("water-tank", "WATER", 48, (244, 246, 240)),
    ("oxygen-tank", "OXYGEN", 30, (22, 26, 28)),
    ("food-locker", "FOOD", 35, (244, 246, 240)),
    ("battery-module", "BATTERY", 55, (20, 20, 20)),
    ("solar-wing", "SOLAR", 70, (244, 246, 240)),
    ("spare-parts-kit", "SPARES", 25, (244, 246, 240)),
    ("spectrometer", "SCIENCE", 90, (244, 246, 240)),
]


def labels():
    atlas = Image.new("RGBA", (A, A), (0, 0, 0, 0))
    inkmap = Image.new("RGB", (A, A), (128, 128, 128))
    rects = {}

    def place(name, img, x, y, w, h, ink=None):
        img = fit(img, w - 8, h - 8)
        if ink is None:
            px = np.asarray(img)
            ink = tuple(int(c) for c in px[px[..., 3] > 128][:, :3].mean(0)) if (px[..., 3] > 128).any() else (128, 128, 128)
        inkmap.paste(ink, (x, y, x + w, y + h))
        ox, oy = x + (w - img.width) // 2, y + (h - img.height) // 2
        atlas.alpha_composite(img, (ox, oy))
        # tight rect around the art (2 px margin) so decal quads can be sized from it
        x0, y0, x1, y1 = ox - 2, oy - 2, ox + img.width + 2, oy + img.height + 2
        rects[name] = {"u0": x0 / A, "v0": 1 - y1 / A, "u1": x1 / A, "v1": 1 - y0 / A, "aspect": (x1 - x0) / (y1 - y0)}

    # 7 name blocks: two lines, name and mass, 1016 x 330 each in two columns
    bw, bh = 1016, 330
    for i, (cid, name, kg, ink) in enumerate(CASES):
        top = stencil_text(name, 300, ink)
        bottom = stencil_text("%d kg" % kg, 190, ink)
        gap = 26
        top = fit(top, bw - 40, 190)
        bottom = fit(bottom, bw - 40, 96)
        block = Image.new("RGBA", (max(top.width, bottom.width), top.height + gap + bottom.height), ink + (0,))
        block.alpha_composite(top, ((block.width - top.width) // 2, 0))
        block.alpha_composite(bottom, ((block.width - bottom.width) // 2, top.height + gap))
        place("name:" + cid, block, 4 + (i % 2) * (bw + 8), 4 + (i // 2) * (bh + 6), bw, bh, ink)
        # single line variant for narrow bands (curved decals, rails)
        line = stencil_text("%s  %d kg" % (name, kg), 150, ink)
        place("line:" + cid, line, 4 + (i % 2) * (bw + 8), 1352 + (i // 2) * 84, bw, 80, ink)
    # the empty 8th name slot: generic handling stencil
    place("stencil:twohand", stencil_text("TWO-HAND LIFT", 150, (244, 246, 240)), 4 + bw + 8, 1352 + 3 * 84, bw, 80, (244, 246, 240))

    y0 = 1700
    white, black, amber, red = (244, 246, 240), (20, 22, 24), (236, 160, 32), (200, 40, 30)

    def sticker(w, h, fill, border=None):
        img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        d.rounded_rectangle([2, 2, w - 3, h - 3], radius=int(min(w, h) * .08), fill=fill + (255,), outline=(border or fill) + (255,), width=6)
        return img, d

    f_s = ImageFont.truetype(FONT, 44)
    f_m = ImageFont.truetype(FONT, 64)
    f_l = ImageFont.truetype(FONT, 96)

    # this side up: two arrows over a bar, white stencil
    img = Image.new("RGBA", (300, 300), white + (0,))
    d = ImageDraw.Draw(img)
    for cx in (90, 210):
        d.polygon([(cx, 20), (cx - 62, 120), (cx - 24, 120), (cx - 24, 226), (cx + 24, 226), (cx + 24, 120), (cx + 62, 120)], fill=white + (255,))
    d.rectangle([20, 250, 280, 280], fill=white + (255,))
    place("icon:up", img, 4, y0, 300, 300, white)

    # hazard triangle (battery)
    img = Image.new("RGBA", (300, 300), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.polygon([(150, 10), (292, 270), (8, 270)], fill=black + (255,))
    d.polygon([(150, 50), (258, 250), (42, 250)], fill=amber + (255,))
    d.polygon([(165, 90), (118, 172), (150, 172), (132, 236), (186, 148), (154, 148)], fill=black + (255,))
    place("icon:hazard", img, 310, y0, 300, 300)

    # pressure gauge dial (oxygen)
    img = Image.new("RGBA", (300, 300), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([4, 4, 296, 296], fill=black + (255,))
    d.ellipse([18, 18, 282, 282], fill=(238, 238, 230, 255))
    d.arc([40, 40, 260, 260], start=20, end=45, fill=red + (255,), width=18)
    d.arc([40, 40, 260, 260], start=250, end=20, fill=(60, 150, 70, 255), width=18)
    for i in range(13):
        a = np.radians(135 + i * 22.5)
        r0, r1 = (96, 126) if i % 2 == 0 else (110, 126)
        d.line([(150 + np.cos(a) * r0, 150 + np.sin(a) * r0), (150 + np.cos(a) * r1, 150 + np.sin(a) * r1)], fill=black + (255,), width=5)
    a = np.radians(318)
    d.line([(150 - np.cos(a) * 22, 150 - np.sin(a) * 22), (150 + np.cos(a) * 104, 150 + np.sin(a) * 104)], fill=red + (255,), width=8)
    d.ellipse([134, 134, 166, 166], fill=black + (255,))
    d.text((150, 214), "bar", font=f_s, fill=black + (255,), anchor="mm")
    place("icon:gauge", img, 616, y0, 300, 300)

    # data plate: white sticker with a code block and text lines (generic serial plate)
    img, d = sticker(420, 300, white, black)
    r = np.random.default_rng(404)
    for iy in range(12):
        for ix in range(12):
            if ix in (0,) or iy in (11,) or r.random() < .5:
                d.rectangle([24 + ix * 12, 30 + iy * 12, 24 + ix * 12 + 11, 30 + iy * 12 + 11], fill=black + (255,))
    d.text((184, 30), "FLIGHT CARGO", font=ImageFont.truetype(FONT, 50), fill=black + (255,))
    d.text((184, 92), "CHECK LATCHES", font=ImageFont.truetype(FONT, 38), fill=black + (255,))
    d.text((184, 138), "BEFORE LIFT", font=ImageFont.truetype(FONT, 38), fill=black + (255,))
    for i in range(34):
        wbar = int(r.integers(2, 8))
        d.rectangle([24 + i * 11, 206, 24 + i * 11 + wbar, 270], fill=black + (255,))
    place("icon:plate", img, 922, y0, 420, 300)

    # fragile optics (science)
    img, d = sticker(420, 300, red, white)
    d.text((210, 92), "FRAGILE", font=f_l, fill=white + (255,), anchor="mm")
    d.text((210, 196), "OPTICS  KEEP DRY", font=ImageFont.truetype(FONT, 52), fill=white + (255,), anchor="mm")
    place("icon:fragile", img, 1348, y0, 420, 300)

    # no oil (oxygen) / potable (water) small plates stacked
    img, d = sticker(270, 144, white, (30, 120, 60))
    d.text((135, 46), "OXYGEN", font=f_m, fill=(30, 120, 60, 255), anchor="mm")
    d.text((135, 104), "NO OIL  NO FLAME", font=ImageFont.truetype(FONT, 34), fill=black + (255,), anchor="mm")
    place("icon:nooil", img, 1774, y0, 270, 144)
    img, d = sticker(270, 144, (30, 110, 170), white)
    d.ellipse([22, 60, 70, 112], fill=white + (255,))
    d.polygon([(46, 22), (24, 78), (68, 78)], fill=white + (255,))
    d.text((168, 46), "POTABLE", font=ImageFont.truetype(FONT, 52), fill=white + (255,), anchor="mm")
    d.text((168, 104), "WATER  40 L", font=ImageFont.truetype(FONT, 36), fill=white + (255,), anchor="mm")
    place("icon:potable", img, 1774, y0 + 152, 270, 144)

    # fully transparent texels take their cell's ink colour so mips and bilinear taps never bleed black
    arr = np.asarray(atlas, np.uint8)
    ink = np.asarray(inkmap, np.uint8)
    rgb = np.where(arr[..., 3:4] > 0, arr[..., :3], ink)
    Image.fromarray(np.concatenate([rgb, arr[..., 3:4]], -1), "RGBA").save(os.path.join(OUT, "CC2_Labels_BaseColor.png"), optimize=True)
    with open(os.path.join(HERE, "cc2_atlas.json"), "w") as f:
        json.dump(rects, f, indent=1, sort_keys=True)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    polymer(); painted(); aluminium(); quilt(); composite(); webbing(); rubber(); solar(); labels()
    print("CC2 textures written to", OUT)
