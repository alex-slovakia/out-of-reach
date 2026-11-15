"""LaunchTowerV03 procedural textures. Pure numpy + zlib (runs inside Blender 3.6's Python or any CPython with numpy).

Usage: python lt3_tex.py <out_dir>
Every texture is tileable, original and generated from fixed seeds, so a rebuild is byte-reproducible.
BaseColor PNGs are sRGB, MetallicSmoothness PNGs are linear (R = metallic, A = smoothness, URP Lit convention),
Normal PNGs are tangent-space OpenGL (+Y up), which is what Unity expects for a texture imported as Normal map.
"""
import os, sys, zlib, struct
import numpy as np

N = 1024


def write_png(path, rgba):
    a = np.clip(rgba * 255.0 + 0.5, 0, 255).astype(np.uint8)
    h, w, c = a.shape
    raw = b''.join(b'\x00' + a[y].tobytes() for y in range(h))
    color = {3: 2, 4: 6}[c]

    def chunk(t, d):
        return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    with open(path, 'wb') as f:
        f.write(b'\x89PNG\r\n\x1a\n')
        f.write(chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, color, 0, 0, 0)))
        f.write(chunk(b'IDAT', zlib.compress(raw, 9)))
        f.write(chunk(b'IEND', b''))


def vnoise(rng, size, cells):
    """Tileable value noise, smooth-step bilinear."""
    g = rng.random((cells, cells))
    t = np.arange(size) * cells / size
    i0 = np.floor(t).astype(int)
    f = t - i0
    f = f * f * (3 - 2 * f)
    i1 = (i0 + 1) % cells
    i0 = i0 % cells
    a = g[np.ix_(i0, i0)]; b = g[np.ix_(i0, i1)]
    c = g[np.ix_(i1, i0)]; d = g[np.ix_(i1, i1)]
    fx = f[None, :]; fy = f[:, None]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def fbm(seed, size=N, base=4, octaves=6, gain=0.5):
    rng = np.random.default_rng(seed)
    out = np.zeros((size, size)); amp = 1.0; tot = 0.0; cells = base
    for _ in range(octaves):
        out += vnoise(rng, size, cells) * amp
        tot += amp; amp *= gain; cells *= 2
        if cells > size:
            break
    out /= tot
    return (out - out.min()) / (out.max() - out.min())


def streaks(seed, size=N, density=90):
    """Vertical rain/rust streaks (v axis = down the member), tileable."""
    rng = np.random.default_rng(seed)
    img = np.zeros((size, size))
    cols = rng.integers(0, size, density)
    for c in cols:
        w = rng.integers(2, 9)
        start = rng.integers(0, size)
        length = rng.integers(size // 6, size // 2)
        strength = rng.uniform(0.25, 1.0)
        ys = (start + np.arange(length)) % size
        fade = np.linspace(1.0, 0.0, length) ** 1.5 * strength
        xs = (c + np.arange(-w, w + 1)) % size
        prof = np.exp(-np.linspace(-2, 2, 2 * w + 1) ** 2)
        img[np.ix_(ys, xs)] = np.maximum(img[np.ix_(ys, xs)], fade[:, None] * prof[None, :])
    blur = fbm(seed + 1, size, 16, 3)
    return np.clip(img * (0.6 + 0.6 * blur), 0, 1)


def smooth(x, a, b):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def normal_from_height(h, strength=4.0):
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * strength
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * strength
    n = np.dstack([-dx, dy, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return np.dstack([n * 0.5 + 0.5, np.ones_like(h)])


def rgb(c):
    return np.array(c, dtype=float)[None, None, :]


def painted(seed, paint, chip_amount, rust_amount, primer=(0.30, 0.29, 0.27)):
    """Weathered paint: colour variation, edge-like chips, rust spots and rain streaks."""
    var = fbm(seed, N, 4, 6)
    chips_n = fbm(seed + 11, N, 24, 4)
    chips = smooth(chips_n, 1.0 - chip_amount, 1.0 - chip_amount + 0.04)
    rust_n = fbm(seed + 23, N, 8, 5)
    rust = smooth(rust_n, 1.0 - rust_amount, 1.0 - rust_amount + 0.08) * (0.4 + 0.6 * chips)
    st = streaks(seed + 31)
    grime = fbm(seed + 41, N, 3, 4)
    col = rgb(paint) * (0.86 + 0.28 * var[..., None])
    col = col * (1 - 0.35 * st[..., None]) + rgb((0.28, 0.14, 0.06)) * (0.25 * st[..., None])
    col = col * (0.9 + 0.15 * grime[..., None])
    col = col * (1 - chips[..., None]) + rgb(primer) * (0.8 + 0.3 * var[..., None]) * chips[..., None]
    col = col * (1 - rust[..., None]) + rgb((0.30, 0.13, 0.05)) * (0.7 + 0.5 * rust_n[..., None]) * rust[..., None]
    metal = 0.05 + 0.65 * chips * (1 - rust)
    smooth_ = 0.38 + 0.12 * var - 0.18 * st - 0.25 * rust + 0.05 * chips
    height = 0.6 * (1 - chips) + 0.15 * var - 0.2 * rust
    base = np.dstack([np.clip(col, 0, 1), np.ones((N, N))])
    ms = np.dstack([metal, metal, metal, np.clip(smooth_, 0.03, 0.9)])
    return base, ms, normal_from_height(height, 3.0)


def galvanized(seed):
    var = fbm(seed, N, 6, 6)
    spangle = fbm(seed + 3, N, 48, 2)
    spangle = np.floor(spangle * 6) / 6
    st = streaks(seed + 5, density=50)
    c = 0.50 + 0.12 * var + 0.06 * spangle - 0.12 * st
    col = np.dstack([c * 0.98, c, c * 1.02])
    white_rust = smooth(fbm(seed + 9, N, 10, 4), 0.72, 0.8)
    col = col * (1 - white_rust[..., None] * 0.5) + 0.75 * white_rust[..., None] * 0.5
    metal = 0.85 - 0.5 * white_rust
    sm = 0.50 + 0.1 * spangle - 0.2 * st - 0.25 * white_rust
    base = np.dstack([np.clip(col, 0, 1), np.ones((N, N))])
    return base, np.dstack([metal, metal, metal, np.clip(sm, 0.05, 0.9)])


def grating(seed, size=512):
    """Bar grating: bearing bars along v every 32 px, cross rods every 96 px. Alpha holes for alpha clipping."""
    y, x = np.mgrid[0:size, 0:size]
    bear = (x % 32) < 5
    cross = (y % 96) < 6
    solid = bear | cross
    var = fbm(seed, size, 8, 4)
    st = streaks(seed + 2, size, 30)
    c = 0.42 + 0.14 * var - 0.15 * st
    edge = ((x % 32) == 0) | ((x % 32) == 4)
    c = c - 0.08 * edge
    col = np.dstack([c, c * 0.99, c * 0.96])
    alpha = solid.astype(float)
    base = np.dstack([np.clip(col, 0, 1), alpha])
    metal = np.full((size, size), 0.7)
    ms = np.dstack([metal, metal, metal, np.clip(0.35 + 0.1 * var - 0.2 * st, 0.05, 0.9)])
    return base, ms


def white_room(seed):
    """Off-white panels, 1 m modules at a 2 m tile: seams, fastener rows, grime gradient."""
    y, x = np.mgrid[0:N, 0:N]
    var = fbm(seed, N, 4, 6)
    st = streaks(seed + 4, density=40)
    seam = ((x % 512) < 4) | ((y % 512) < 4)
    fast = (((x % 512) - 24) % 64 < 6) & ((((y % 512) - 14) % 512) < 6) | (((y % 512) - 24) % 64 < 6) & ((((x % 512) - 14) % 512) < 6)
    c = 0.80 + 0.06 * var - 0.18 * st
    col = np.dstack([c, c * 0.99, c * 0.955])
    col = col * (1 - 0.55 * seam[..., None]) * (1 - 0.35 * fast[..., None])
    chips = smooth(fbm(seed + 7, N, 32, 3), 0.93, 0.96)
    col = col * (1 - chips[..., None]) + rgb((0.22, 0.22, 0.23)) * chips[..., None]
    metal = 0.02 + 0.5 * chips
    sm = 0.45 + 0.1 * var - 0.2 * st
    height = 0.5 - 0.5 * seam - 0.2 * fast + 0.1 * var - 0.3 * chips
    base = np.dstack([np.clip(col, 0, 1), np.ones((N, N))])
    return base, np.dstack([metal, metal, metal, np.clip(sm, 0.05, 0.9)]), normal_from_height(height, 2.0)


def hazard(seed):
    """45 degree amber / graphite stripes (0.6 m period at a 1.2 m tile), worn."""
    y, x = np.mgrid[0:N, 0:N]
    band = ((x + y) % 512) < 256
    base_amber, ms_a, _ = painted(seed, (0.86, 0.50, 0.06), 0.18, 0.05)
    base_dark, ms_d, _ = painted(seed + 1, (0.07, 0.072, 0.075), 0.18, 0.05)
    b = np.where(band[..., None], base_amber, base_dark)
    m = np.where(band[..., None], ms_a, ms_d)
    return b, m


def rubber(seed):
    var = fbm(seed, N, 8, 5)
    c = 0.045 + 0.03 * var
    base = np.dstack([c, c, c * 1.05, np.ones((N, N))])
    z = np.zeros((N, N))
    return base, np.dstack([z, z, z, 0.18 + 0.12 * var])


def main(out):
    os.makedirs(out, exist_ok=True)
    jobs = {}
    b, m, n = painted(101, (0.16, 0.165, 0.172), 0.14, 0.10)
    jobs['LT3_SteelGraphite'] = (b, m, n)
    b, m, n = painted(202, (0.84, 0.50, 0.07), 0.16, 0.06, primer=(0.12, 0.12, 0.13))
    jobs['LT3_SafetyAmber'] = (b, m, n)
    b, m, n = painted(303, (0.52, 0.075, 0.05), 0.10, 0.08)
    jobs['LT3_WarningRed'] = (b, m, n)
    b, m = galvanized(404)
    jobs['LT3_Galvanized'] = (b, m, None)
    b, m = grating(505)
    jobs['LT3_Grating'] = (b, m, None)
    b, m, n = white_room(606)
    jobs['LT3_WhiteRoom'] = (b, m, n)
    b, m = hazard(707)
    jobs['LT3_Hazard'] = (b, m, None)
    b, m = rubber(808)
    jobs['LT3_Rubber'] = (b, m, None)
    for name, (b, m, n) in jobs.items():
        write_png(os.path.join(out, name + '_BaseColor.png'), b)
        write_png(os.path.join(out, name + '_MetallicSmoothness.png'), m)
        if n is not None:
            write_png(os.path.join(out, name + '_Normal.png'), n)
        print('texture', name)


if __name__ == '__main__':
    main(sys.argv[-1])
