#!/usr/bin/env python3
"""Procedural PBR texture set for the AURORA-7 exterior V2 (fictional vehicle, original art).

Run:  python a7x_textures.py            (needs numpy + Pillow; deterministic, no downloads)
Out:  ../Textures/A7X_<Set>_BaseColor.png  sRGB
      ../Textures/A7X_<Set>_Normal.png     tangent space, OpenGL (Y+), linear
      ../Textures/A7X_<Set>_Mask.png       R metallic, G ambient occlusion, B unused, A smoothness (URP Lit)
      ../Textures/A7X_Markings.png         RGBA, alpha-clipped marking atlas
      markings.json                        UV rectangles of the marking atlas (read by a7x_build.py)
Lettering uses Barlow Condensed Bold (SIL OFL 1.1), already committed under UI/Typography.
"""
import json
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "Textures"))
FONT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "..", "UI", "Typography", "BarlowCondensed-Bold.ttf"))
os.makedirs(OUT, exist_ok=True)


# ----------------------------------------------------------------------------- helpers
def fnoise(n, beta=2.0, seed=0, aniso=(1.0, 1.0)):
    """Periodic fractal noise in 0..1 (FFT filtered, so every tile is seamless)."""
    rng = np.random.default_rng(seed)
    f = np.fft.fft2(rng.standard_normal((n, n)))
    fy = np.fft.fftfreq(n)[:, None] * aniso[1]
    fx = np.fft.fftfreq(n)[None, :] * aniso[0]
    k = np.sqrt(fx * fx + fy * fy)
    k[0, 0] = 1.0
    f *= k ** (-beta / 2.0)
    f[0, 0] = 0.0
    r = np.real(np.fft.ifft2(f))
    r -= r.min()
    return r / r.max()


def blur(a, sigma):
    n0, n1 = a.shape
    fy = np.fft.fftfreq(n0)[:, None]
    fx = np.fft.fftfreq(n1)[None, :]
    g = np.exp(-2.0 * (math.pi * sigma) ** 2 * (fx * fx + fy * fy))
    return np.real(np.fft.ifft2(np.fft.fft2(a) * g))


def sstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def normal_map(h, strength):
    """Height (0..1, periodic) -> OpenGL tangent normal map. strength is in texels of relief."""
    nx = -(np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5 * strength
    ny = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5 * strength
    nz = np.ones_like(h)
    l = np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.stack([nx / l, ny / l, nz / l], -1) * 0.5 + 0.5


def cavity(h, sigma, k):
    return np.clip(1.0 - k * np.maximum(0.0, blur(h, sigma) - h), 0.0, 1.0)


def to8(a):
    return (np.clip(a, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8)


def lin2srgb(c):
    c = np.clip(c, 0.0, 1.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)


def save_set(name, base_lin, height, nstrength, metal, smooth, ao=None, ao_sigma=6.0, ao_k=3.0):
    """base_lin: linear RGB HxWx3. Writes the three maps of one set."""
    if ao is None:
        ao = cavity(height, ao_sigma, ao_k)
    Image.fromarray(to8(lin2srgb(base_lin)), "RGB").save(os.path.join(OUT, f"A7X_{name}_BaseColor.png"), optimize=True)
    Image.fromarray(to8(normal_map(height, nstrength)), "RGB").save(os.path.join(OUT, f"A7X_{name}_Normal.png"), optimize=True)
    mask = np.stack([np.broadcast_to(metal, height.shape), ao, np.zeros_like(height),
                     np.broadcast_to(smooth, height.shape)], -1)
    Image.fromarray(to8(mask), "RGBA").save(os.path.join(OUT, f"A7X_{name}_Mask.png"), optimize=True)
    print("wrote", name, height.shape)


def grid(n):
    v, u = np.mgrid[0:n, 0:n].astype(np.float64) / n
    return u, v


def rgb(c, like):
    return np.ones(like.shape + (3,)) * np.array(c, dtype=np.float64)


def lines(coord, period, width):
    """Mask of periodic lines (1 on the line), coord in 0..1, anti-aliased."""
    d = np.abs(((coord / period) + 0.5) % 1.0 - 0.5) * period
    return 1.0 - sstep(width * 0.5, width * 0.5 + 1.5 / coord.shape[0], d)


def discs(n, pts, radius):
    """Soft discs at uv points (periodic). Returns (mask, dome height)."""
    u, v = grid(n)
    m = np.zeros((n, n))
    dome = np.zeros((n, n))
    for (pu, pv) in pts:
        du = np.abs((u - pu + 0.5) % 1.0 - 0.5)
        dv = np.abs((v - pv + 0.5) % 1.0 - 0.5)
        d = np.sqrt(du * du + dv * dv)
        m = np.maximum(m, 1.0 - sstep(radius - 1.0 / n, radius + 1.0 / n, d))
        dome = np.maximum(dome, np.sqrt(np.clip(1.0 - (d / radius) ** 2, 0.0, 1.0)))
    return m, dome


# ----------------------------------------------------------------------------- sets
def blanket(n=1024):
    """Off-white beta-cloth thermal blanket, diamond quilting, 1 m tile."""
    u, v = grid(n)
    a = np.abs(np.sin(math.pi * (u + v) * 4.0))
    b = np.abs(np.sin(math.pi * (u - v) * 4.0))
    pillow = np.sqrt(a * b)
    stitch = 1.0 - sstep(0.0, 0.10, np.minimum(a, b))
    weave = 0.5 + 0.5 * np.sin(u * n * math.pi * 0.5) * np.sin(v * n * math.pi * 0.5)
    wr = fnoise(n, 2.4, 11)
    h = 0.62 * pillow ** 0.7 + 0.30 * wr + 0.03 * weave
    stain = fnoise(n, 2.8, 12)
    grime = fnoise(n, 1.6, 13)
    base = rgb((0.80, 0.79, 0.74), u)
    base *= (1.0 - 0.20 * stitch)[..., None]
    base *= (0.93 + 0.10 * stain)[..., None]
    base *= (1.0 - 0.05 * sstep(0.62, 0.9, grime))[..., None]
    base[..., 2] *= 0.985 - 0.03 * stain
    smooth = 0.22 + 0.10 * wr - 0.08 * stitch
    save_set("Blanket", base, h, 5.0, 0.0, smooth, ao_sigma=5.0, ao_k=2.2)


def whipple(n=2048, cells=4):
    """MMOD shield panel atlas: 4 x 4 panel variants, each cell is one complete panel."""
    c = n // cells
    cu, cv = grid(c)
    edge = np.minimum(np.minimum(cu, 1 - cu), np.minimum(cv, 1 - cv))
    bevel = sstep(0.0, 0.022, edge)
    ins = 0.065
    pos = [ins, 0.2825, 0.5, 0.7175, 1 - ins]
    pts = [(p, ins) for p in pos] + [(p, 1 - ins) for p in pos] + [(ins, p) for p in pos[1:-1]] + [(1 - ins, p) for p in pos[1:-1]]
    fm, fdome = discs(c, pts, 0.0125)
    fring, _ = discs(c, pts, 0.021)
    base = np.zeros((n, n, 3))
    height = np.zeros((n, n))
    metal = np.zeros((n, n))
    smooth = np.zeros((n, n))
    font = ImageFont.truetype(FONT, int(c * 0.055))
    rng = np.random.default_rng(71)
    tones = [(0.74, 0.74, 0.71), (0.77, 0.765, 0.73), (0.71, 0.715, 0.70), (0.79, 0.78, 0.745),
             (0.73, 0.725, 0.69), (0.76, 0.76, 0.74), (0.70, 0.70, 0.675), (0.775, 0.77, 0.75)]
    for j in range(cells):
        for i in range(cells):
            k = j * cells + i
            tone = np.array(tones[k % len(tones)]) * (0.97 + 0.06 * rng.random())
            if k in (5, 14):          # replaced panels: cooler, newer
                tone = np.array((0.80, 0.81, 0.82))
            if k == 10:               # a graphite service panel
                tone = np.array((0.30, 0.31, 0.33))
            streak = fnoise(c, 2.6, 100 + k, aniso=(1.0, 9.0))
            mott = fnoise(c, 2.2, 200 + k)
            fine = fnoise(c, 0.8, 300 + k)
            col = np.ones((c, c, 3)) * tone
            col *= (0.92 + 0.11 * mott)[..., None]
            col *= (1.0 - 0.10 * sstep(0.55, 0.95, streak))[..., None]
            col *= (0.80 + 0.20 * sstep(0.0, 0.05, edge))[..., None]          # soot towards the gap
            h = 0.55 * bevel + 0.05 * mott + 0.015 * fine
            # sub-tile seams on some panels (shield tiles), a hinge line on others
            seam = np.zeros((c, c))
            if k % 3 == 0:
                seam = np.maximum(lines(cu, 0.5, 0.006), lines(cv, 0.5, 0.006)) * sstep(0.03, 0.06, edge)
            elif k % 3 == 1:
                seam = lines(cv, 1.0 / 3.0, 0.005) * sstep(0.03, 0.06, edge)
            h -= 0.16 * seam
            col *= (1.0 - 0.38 * seam)[..., None]
            # micrometeoroid pits
            npit = int(rng.integers(3, 9))
            ppts = [(0.1 + 0.8 * rng.random(), 0.1 + 0.8 * rng.random()) for _ in range(npit)]
            for (pu, pv) in ppts:
                r = 0.004 + 0.007 * rng.random()
                pm, pd = discs(c, [(pu, pv)], r)
                rim, _ = discs(c, [(pu, pv)], r * 1.9)
                h -= 0.22 * pd
                h += 0.05 * (rim - pm)
                col *= (1.0 - 0.55 * pm)[..., None]
                col *= (1.0 + 0.10 * (rim - pm))[..., None]
            # fasteners
            h += 0.13 * fdome * fm - 0.05 * (fring - fm)
            col = col * (1 - fm[..., None]) + np.array((0.52, 0.53, 0.55)) * fm[..., None]
            col *= (1.0 - 0.30 * (fring - fm))[..., None]
            m = fm.copy()
            s = 0.34 + 0.10 * mott - 0.12 * sstep(0.55, 0.95, streak) + 0.30 * fm
            # stencilled panel id
            if k % 2 == 0:
                img = Image.new("L", (c, c), 0)
                d = ImageDraw.Draw(img)
                d.text((int(c * 0.115), int(c * 0.855)), "H-%02d-%02d" % (3 + k % 5, 11 + k), fill=255, font=font)
                if k % 4 == 0:
                    d.text((int(c * 0.60), int(c * 0.855)), "MMOD SHIELD", fill=255, font=font)
                t = np.asarray(img, dtype=np.float64) / 255.0
                ink = 0.42 if k != 10 else 2.2
                col = col * (1 - t[..., None]) + col * ink * t[..., None]
            y0, x0 = j * c, i * c
            base[y0:y0 + c, x0:x0 + c] = col
            height[y0:y0 + c, x0:x0 + c] = h
            metal[y0:y0 + c, x0:x0 + c] = m
            smooth[y0:y0 + c, x0:x0 + c] = s
    ao = np.clip(0.55 + 0.45 * sstep(0.0, 0.06, np.tile(edge, (cells, cells))), 0, 1) * cavity(height, 4.0, 2.5)
    save_set("Whipple", base, height, 9.0, metal, smooth, ao=ao)


def graphite(n=1024):
    """Worn graphite composite panels, 1 m tile: seams every 0.5 m, corner fasteners."""
    u, v = grid(n)
    seam = np.maximum(lines(u, 0.5, 0.008), lines(v, 0.5, 0.008))
    pts = [(x, y) for x in (0.035, 0.465, 0.535, 0.965) for y in (0.035, 0.465, 0.535, 0.965)]
    fm, fd = discs(n, pts, 0.009)
    mott = fnoise(n, 2.2, 21)
    brush = fnoise(n, 1.4, 22, aniso=(8.0, 1.0))
    wear = sstep(0.004, 0.03, np.minimum(np.abs(((u / 0.5) + 0.5) % 1 - 0.5), np.abs(((v / 0.5) + 0.5) % 1 - 0.5)) * 0.5)
    h = 0.6 - 0.5 * seam + 0.12 * fd * fm + 0.05 * mott + 0.03 * brush
    base = rgb((0.060, 0.064, 0.072), u)
    base *= (0.80 + 0.45 * mott)[..., None]
    base += (0.045 * (1 - wear) * (1 - seam))[..., None]              # bare edges
    base *= (1.0 - 0.55 * seam)[..., None]
    base = base * (1 - fm[..., None]) + np.array((0.34, 0.35, 0.37)) * fm[..., None]
    metal = 0.30 + 0.5 * (1 - wear) * (1 - seam) + 0.6 * fm
    smooth = 0.42 + 0.14 * brush - 0.2 * seam + 0.15 * fm
    save_set("Graphite", base, h, 7.0, np.clip(metal, 0, 1), smooth)


def radiator(n=1024):
    """White radiator face sheet, 1 m tile: coolant tube ribs along v every 12.5 cm."""
    u, v = grid(n)
    rib = np.clip(1.0 - np.abs(((u / 0.125) + 0.5) % 1.0 - 0.5) / 0.16, 0.0, 1.0) ** 1.5
    seam = lines(v, 0.5, 0.006)
    pts = [(x * 0.125 + 0.0625, y) for x in range(8) for y in (0.02, 0.48, 0.52, 0.98)]
    fm, fd = discs(n, pts, 0.006)
    mott = fnoise(n, 2.4, 31)
    h = 0.35 + 0.45 * rib - 0.3 * seam + 0.1 * fd * fm + 0.03 * mott
    base = rgb((0.82, 0.83, 0.84), u)
    base *= (0.94 + 0.08 * mott)[..., None]
    base *= (1.0 - 0.07 * (1 - rib))[..., None]
    base *= (1.0 - 0.45 * seam)[..., None]
    base = base * (1 - fm[..., None]) + np.array((0.5, 0.51, 0.53)) * fm[..., None]
    save_set("Radiator", base, h, 6.0, 0.12 + 0.7 * fm, 0.58 + 0.12 * mott - 0.25 * seam)


def solar_cells(n=2048):
    """Roll-out array cell side, 1 m tile: 8 x 8 cropped-corner cells on an amber polyimide blanket."""
    u, v = grid(n)
    cells = 8
    fu = (u * cells) % 1.0
    fv = (v * cells) % 1.0
    iu = np.floor(u * cells).astype(int)
    iv = np.floor(v * cells).astype(int)
    du = np.minimum(fu, 1 - fu)
    dv = np.minimum(fv, 1 - fv)
    gap = 0.022
    crop = 0.13
    inside = (du > gap) & (dv > gap) & ((du + dv) > crop)
    aa = sstep(0.0, 0.006, np.minimum(np.minimum(du, dv) - gap, (du + dv - crop) * 0.7071))
    cellm = np.where(inside, aa, 0.0)
    rng = np.random.default_rng(41)
    tone = rng.random((cells, cells))[iv, iu]
    tiltx = (rng.random((cells, cells))[iv, iu] - 0.5)
    tilty = (rng.random((cells, cells))[iv, iu] - 0.5)
    fingers = lines(fv, 1.0 / 14.0, 0.012) * cellm
    bus = np.maximum(lines(fu - 0.25, 1.0, 0.022), lines(fu - 0.75, 1.0, 0.022)) * cellm
    fold = np.maximum(lines(v, 0.5, 0.012), lines(u, 1.0, 0.010))        # module hinge lines
    cellcol = np.ones((n, n, 3)) * np.array((0.010, 0.022, 0.060))
    cellcol *= (0.75 + 0.6 * tone)[..., None]
    cellcol += fingers[..., None] * np.array((0.035, 0.05, 0.075))
    cellcol = cellcol * (1 - bus[..., None]) + bus[..., None] * np.array((0.55, 0.57, 0.60))
    kap = np.ones((n, n, 3)) * np.array((0.42, 0.235, 0.045))
    kap *= (0.8 + 0.4 * fnoise(n, 2.0, 42))[..., None]
    base = kap * (1 - cellm[..., None]) + cellcol * cellm[..., None]
    base *= (1.0 - 0.6 * fold)[..., None]
    h = 0.35 + 0.4 * cellm + 0.25 * cellm * (tiltx * (fu - 0.5) + tilty * (fv - 0.5)) + 0.06 * bus - 0.3 * fold
    h += 0.05 * fnoise(n, 2.6, 43)
    metal = 0.25 * cellm + 0.75 * bus + 0.35 * (1 - cellm)
    smooth = 0.88 * cellm + 0.55 * (1 - cellm) - 0.3 * fold
    save_set("SolarCells", base, h, 5.0, np.clip(metal, 0, 1), np.clip(smooth, 0, 1), ao_sigma=3.0, ao_k=2.0)


def solar_back(n=1024):
    """Array back side: amber polyimide blanket with printed harness, tension ripples, 1 m tile."""
    u, v = grid(n)
    rip = fnoise(n, 2.8, 51, aniso=(1.0, 6.0))
    cellshadow = np.maximum(lines(u, 0.125, 0.006), lines(v, 0.125, 0.006))
    tape = np.maximum(lines(v, 0.5, 0.030), lines(u, 1.0, 0.024))
    trace = lines(u - 0.31, 1.0, 0.010) + lines(u - 0.335, 1.0, 0.006) + lines(u - 0.69, 1.0, 0.010)
    trace = np.clip(trace, 0, 1)
    h = 0.4 + 0.35 * rip + 0.12 * tape + 0.05 * trace - 0.04 * cellshadow
    base = rgb((0.50, 0.275, 0.050), u)
    base *= (0.78 + 0.40 * rip)[..., None]
    base *= (1.0 - 0.22 * cellshadow)[..., None]
    base = base * (1 - tape[..., None]) + tape[..., None] * np.array((0.36, 0.20, 0.045))
    base = base * (1 - trace[..., None]) + trace[..., None] * np.array((0.10, 0.075, 0.04))
    save_set("SolarBack", base, h, 7.0, 0.55 - 0.3 * tape, 0.70 - 0.2 * tape + 0.1 * rip, ao_sigma=5.0, ao_k=1.5)


def mli_gold(n=1024):
    """Crinkled amber multi-layer insulation foil, 1 m tile, taped seams every 0.5 m."""
    u, v = grid(n)
    a = np.abs(fnoise(n, 2.2, 61) - 0.5) * 2
    b = np.abs(fnoise(n, 1.9, 62) - 0.5) * 2
    c = np.abs(fnoise(n, 1.5, 63) - 0.5) * 2
    cr = 0.55 * a + 0.30 * b + 0.15 * c
    tape = np.maximum(lines(u, 0.5, 0.030), lines(v, 0.5, 0.030))
    pts = [(x, y) for x in (0.0, 0.5) for y in (0.125, 0.375, 0.625, 0.875)] + [(x, y) for y in (0.0, 0.5) for x in (0.125, 0.375, 0.625, 0.875)]
    gm, gd = discs(n, pts, 0.008)
    h = cr * (1 - 0.75 * tape) + 0.25 * tape + 0.1 * gd * gm
    base = rgb((0.78, 0.50, 0.11), u)
    base *= (0.72 + 0.50 * cr)[..., None]
    base = base * (1 - tape[..., None]) + tape[..., None] * np.array((0.56, 0.36, 0.09))
    base = base * (1 - gm[..., None]) + gm[..., None] * np.array((0.6, 0.6, 0.62))
    save_set("MLIGold", base, h, 9.0, 0.95 - 0.35 * tape, 0.74 - 0.25 * cr + 0.0 * tape, ao_sigma=5.0, ao_k=2.0)


def metal(n=512):
    """Neutral brushed metal, 0.5 m tile; tinted per material (aluminium, amber anodise, dark anodise)."""
    u, v = grid(n)
    br = fnoise(n, 1.2, 81, aniso=(14.0, 1.0))
    mott = fnoise(n, 2.4, 82)
    scr = sstep(0.80, 0.86, fnoise(n, 0.9, 83, aniso=(1.0, 22.0)))
    h = 0.5 + 0.25 * (br - 0.5) + 0.08 * mott - 0.12 * scr
    base = rgb((0.80, 0.80, 0.80), u)
    base *= (0.86 + 0.20 * br)[..., None]
    base *= (0.95 + 0.08 * mott)[..., None]
    save_set("Metal", base, h, 3.5, 1.0, 0.50 + 0.22 * br - 0.2 * scr, ao=np.ones((n, n)))


def engine(n=1024):
    """Radiatively cooled nozzle: u wraps the bell (8 tiles), v runs throat (0) to exit (1)."""
    u, v = grid(n)
    vv = 1.0 - v                                    # image row 0 is v = 1
    streak = fnoise(n, 1.8, 91, aniso=(1.0, 12.0))
    mott = fnoise(n, 2.2, 92)
    weld = np.clip(lines(vv - 0.18, 1.0, 0.012) + lines(vv - 0.55, 1.0, 0.010) + lines(vv - 0.965, 1.0, 0.030), 0, 1)
    stiff = lines(u, 0.25, 0.012) * sstep(0.56, 0.60, vv)
    hot = np.exp(-vv * 5.5)
    c_hot = np.array((0.050, 0.040, 0.075))          # blued throat
    c_mid = np.array((0.30, 0.18, 0.10))             # straw / bronze
    c_cold = np.array((0.23, 0.235, 0.25))           # grey alloy
    t = sstep(0.05, 0.45, vv)
    t2 = sstep(0.35, 0.95, vv)
    base = (c_hot * (1 - t)[..., None] + c_mid * t[..., None]) * (1 - t2)[..., None] + c_cold * t2[..., None]
    base *= (0.75 + 0.5 * streak)[..., None]
    base *= (0.9 + 0.2 * mott)[..., None]
    base *= (1.0 - 0.35 * weld)[..., None]
    h = 0.5 + 0.2 * (streak - 0.5) + 0.25 * weld + 0.2 * stiff + 0.05 * mott
    save_set("Engine", base, h, 6.0, 0.9, 0.36 + 0.2 * streak + 0.12 * (1 - hot), ao_sigma=4.0, ao_k=2.0)


# ----------------------------------------------------------------------------- markings atlas
def markings(n=2048):
    """Alpha-clipped marking atlas. Insignia-free: fictional names, hazard bands, guide marks."""
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    rects = {}
    GRAPH = (26, 28, 32, 255)
    WHITE = (226, 224, 214, 255)
    AMBER = (232, 150, 22, 255)
    RED = (190, 34, 28, 255)
    CYAN = (40, 190, 215, 255)

    def put(name, x, y, w, h):
        rects[name] = [x / n, 1.0 - (y + h) / n, (x + w) / n, 1.0 - y / n, w / h]

    def text(name, s, x, y, size, fill, pad=8, track=0):
        font = ImageFont.truetype(FONT, size)
        if track:
            widths = [d.textlength(ch, font=font) for ch in s]
            w = int(sum(widths) + track * (len(s) - 1))
        else:
            w = int(d.textlength(s, font=font))
        bb = font.getbbox(s)
        h = bb[3]
        if track:
            cx = x + pad
            for ch, cw in zip(s, widths):
                d.text((cx, y + pad), ch, font=font, fill=fill)
                cx += cw + track
        else:
            d.text((x + pad, y + pad), s, font=font, fill=fill)
        put(name, x, y, w + 2 * pad, h + 2 * pad)
        return w + 2 * pad, h + 2 * pad

    y = 8
    w, h = text("name_dark", "AURORA-7", 8, y, 330, GRAPH, track=18)
    y += h + 12
    w, h = text("name_light", "AURORA-7", 8, y, 330, WHITE, track=18)
    y += h + 12
    for k, (key, s_, col_) in enumerate((("srv_dark", "SERVICE ACCESS", GRAPH), ("press", "PRESSURANT  N2", GRAPH),
                                         ("grap", "GRAPPLE FIXTURE", GRAPH), ("strk", "STAR TRACKER", GRAPH),
                                         ("lga", "LGA", GRAPH), ("keep", "KEEP OUT ZONE", RED),
                                         ("thr", "THRUST AXIS", WHITE), ("vent_d", "VENT", GRAPH))):
        text(key, s_, 1400, 12 + k * 92, 72, col_, pad=8, track=3)
    row = y
    w, h = text("reg_dark", "A7-EXP-07", 8, row, 150, GRAPH, track=8)
    w2, h = text("reg_light", "A7-EXP-07", 8 + w + 24, row, 150, WHITE, track=8)
    text("reg_amber", "A7-EXP-07", 8 + w + 24 + w2 + 24, row, 150, AMBER, track=8)
    y += h + 12
    small = [("dock", "DOCKING PORT AFT", GRAPH), ("rcs", "RCS  KEEP CLEAR", RED), ("nostep", "NO STEP", RED),
             ("tether", "TETHER POINT", GRAPH), ("hga", "HGA  RF HAZARD", AMBER), ("sada", "ARRAY DRIVE", GRAPH),
             ("rad", "RADIATOR  NO HANDHOLD", GRAPH), ("eng", "MAIN ENGINE  HOT", RED), ("eva", "EVA ROUTE", CYAN),
             ("cm", "CM", GRAPH), ("hab", "HAB", GRAPH), ("sm", "SM", WHITE), ("dockl", "DOCKING PORT AFT", WHITE),
             ("vent", "VENT", WHITE), ("srv", "SERVICE ACCESS", WHITE), ("latch", "LATCH 1-4", AMBER)]
    x = 8
    for key, s, col in small:
        font = ImageFont.truetype(FONT, 84)
        wpred = int(d.textlength(s, font=font)) + 20
        if x + wpred > n - 8:
            x = 8
            y += 108
        w, h = text(key, s, x, y, 84, col, pad=10, track=3)
        x += w + 18
    y += 116

    # hazard band amber / graphite (tileable along u)
    def band(name, y0, hh, c1, c2, period):
        for xx in range(-hh, n + hh, period):
            d.polygon([(xx, y0 + hh), (xx + period // 2, y0 + hh), (xx + period // 2 + hh, y0), (xx + hh, y0)], fill=c1)
            d.polygon([(xx + period // 2, y0 + hh), (xx + period, y0 + hh), (xx + period + hh, y0), (xx + period // 2 + hh, y0)], fill=c2)
        put(name, 0, y0, n, hh)

    band("hazard_amber", y, 96, AMBER, GRAPH, 128)
    y += 96 + 10
    band("hazard_red", y, 96, RED, WHITE, 128)
    y += 96 + 10
    # solid colour bars
    for key, col in (("bar_amber", AMBER), ("bar_cyan", CYAN), ("bar_red", RED), ("bar_graph", GRAPH), ("bar_white", WHITE)):
        d.rectangle([0, y, n, y + 40], fill=col)
        put(key, 4, y + 4, n - 8, 32)
        y += 46
    # chevrons (eva route arrows), docking target, tick ring segment
    x = 8
    for key, col in (("chev_cyan", CYAN), ("chev_amber", AMBER)):
        for k in range(3):
            ox = x + k * 70
            d.polygon([(ox, y), (ox + 40, y), (ox + 90, y + 60), (ox + 40, y + 120), (ox, y + 120), (ox + 50, y + 60)], fill=col)
        put(key, x, y, 250, 120)
        x += 270
    tx, ty, ts = x, y, 240
    d.rectangle([tx, ty, tx + ts, ty + ts], fill=GRAPH)
    d.ellipse([tx + 20, ty + 20, tx + ts - 20, ty + ts - 20], outline=WHITE, width=10)
    d.rectangle([tx + ts // 2 - 8, ty + 14, tx + ts // 2 + 8, ty + ts - 14], fill=WHITE)
    d.rectangle([tx + 14, ty + ts // 2 - 8, tx + ts - 14, ty + ts // 2 + 8], fill=WHITE)
    d.ellipse([tx + ts // 2 - 22, ty + ts // 2 - 22, tx + ts // 2 + 22, ty + ts // 2 + 22], fill=AMBER)
    put("target", tx, ty, ts, ts)
    x = tx + ts + 20
    # tick scale (for the array drive and the gimbal)
    for k in range(40):
        hh = 110 if k % 5 == 0 else 60
        d.rectangle([x + k * 22, y, x + k * 22 + 8, y + hh], fill=WHITE)
    put("ticks", x, y, 40 * 22, 110)
    img.save(os.path.join(OUT, "A7X_Markings.png"), optimize=True)
    with open(os.path.join(HERE, "markings.json"), "w") as f:
        json.dump(rects, f, indent=1, sort_keys=True)
    print("wrote Markings", len(rects), "rects, used rows to y =", y + 250)


if __name__ == "__main__":
    blanket()
    whipple()
    graphite()
    radiator()
    solar_cells()
    solar_back()
    mli_gold()
    metal()
    engine()
    markings()
