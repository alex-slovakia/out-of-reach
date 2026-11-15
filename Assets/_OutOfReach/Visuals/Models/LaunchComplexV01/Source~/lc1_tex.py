"""LaunchComplexV01 procedural textures. Pure numpy + zlib (runs inside Blender 3.6's Python or any CPython with numpy).

Usage: blender --background --python lc1_tex.py -- <Textures dir>      (or: python lc1_tex.py <Textures dir>)
Every texture is tileable, original and generated from fixed seeds, so a rebuild is byte-reproducible.
BaseColor PNGs are sRGB (metallic / smoothness are material scalars, no map),
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
    """Vertical rain / condensation streaks (image y = down the surface), tileable."""
    rng = np.random.default_rng(seed)
    img = np.zeros((size, size))
    for c in rng.integers(0, size, density):
        w = rng.integers(2, 9)
        start = rng.integers(0, size)
        length = rng.integers(size // 6, size // 2)
        strength = rng.uniform(0.25, 1.0)
        ys = (start + np.arange(length)) % size
        fade = np.linspace(1.0, 0.0, length) ** 1.5 * strength
        xs = (c + np.arange(-w, w + 1)) % size
        prof = np.exp(-np.linspace(-2, 2, 2 * w + 1) ** 2)
        img[np.ix_(ys, xs)] = np.maximum(img[np.ix_(ys, xs)], fade[:, None] * prof[None, :])
    return np.clip(img * (0.6 + 0.6 * fbm(seed + 1, size, 16, 3)), 0, 1)


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


def opaque(col):
    return np.dstack([np.clip(col, 0, 1), np.ones(col.shape[:2])])


def painted(seed, paint, chip_amount, rust_amount, primer=(0.30, 0.29, 0.27), streak=0.35):
    """Weathered paint: colour variation, chips, rust spots and rain streaks."""
    var = fbm(seed, N, 4, 6)
    chips_n = fbm(seed + 11, N, 24, 4)
    chips = smooth(chips_n, 1.0 - chip_amount, 1.0 - chip_amount + 0.04)
    rust_n = fbm(seed + 23, N, 8, 5)
    rust = smooth(rust_n, 1.0 - rust_amount, 1.0 - rust_amount + 0.08) * (0.4 + 0.6 * chips)
    st = streaks(seed + 31)
    grime = fbm(seed + 41, N, 3, 4)
    col = rgb(paint) * (0.88 + 0.24 * var[..., None])
    col = col * (1 - streak * st[..., None]) + rgb((0.28, 0.14, 0.06)) * (0.6 * streak * st[..., None])
    col = col * (0.9 + 0.15 * grime[..., None])
    col = col * (1 - chips[..., None]) + rgb(primer) * (0.8 + 0.3 * var[..., None]) * chips[..., None]
    col = col * (1 - rust[..., None]) + rgb((0.30, 0.13, 0.05)) * (0.7 + 0.5 * rust_n[..., None]) * rust[..., None]
    height = 0.6 * (1 - chips) + 0.15 * var - 0.2 * rust
    return opaque(col), normal_from_height(height, 3.0)


def hull_white(seed):
    """Insulated tank skin, 4 m tile: circumferential weld seams every 2 m, staggered vertical seams, rivet rows,
    condensation streaks and a faint spray-foam orange peel."""
    y, x = np.mgrid[0:N, 0:N]
    var = fbm(seed, N, 4, 6)
    peel = fbm(seed + 2, N, 96, 2)
    st = streaks(seed + 4, density=70)
    row = (y // 512)
    hseam = (y % 512) < 5
    vseam = ((x + row * 128) % 256) < 3
    rivet = ((((y % 512) - 16) % 512) < 5) & ((x % 32) < 5) | ((((y % 512) - 500) % 512) < 5) & ((x % 32) < 5)
    grime = fbm(seed + 6, N, 3, 4)
    c = 0.86 + 0.04 * var - 0.09 * st * (0.5 + 0.5 * grime) + 0.02 * peel
    col = np.dstack([c, c * 0.992, c * 0.965])
    col = col * (1 - 0.42 * hseam[..., None]) * (1 - 0.30 * vseam[..., None]) * (1 - 0.25 * rivet[..., None])
    # soot wash rising from seams
    soot = smooth(fbm(seed + 8, N, 6, 4), 0.62, 0.9) * 0.04
    col = col * (1 - soot[..., None])
    height = 0.5 - 0.6 * hseam - 0.4 * vseam + 0.25 * rivet + 0.06 * peel + 0.05 * var
    return opaque(col), normal_from_height(height, 2.4)


def hull_graphite(seed):
    """Graphite stringer skin, 2 m tile: 16 raised stringers per tile (0.125 m pitch) and ring frames every 1 m."""
    y, x = np.mgrid[0:N, 0:N]
    var = fbm(seed, N, 4, 6)
    st = streaks(seed + 3, density=60)
    rib = 0.5 + 0.5 * np.cos(x * (2 * np.pi / 64.0))
    rib = smooth(rib, 0.55, 0.95)
    frame = ((y % 512) < 10)
    chips = smooth(fbm(seed + 7, N, 28, 4), 0.86, 0.9) * (0.4 + 0.6 * rib)
    c = 0.23 + 0.06 * var + 0.05 * rib - 0.07 * st
    col = np.dstack([c * 0.97, c, c * 1.06])
    col = col * (1 - 0.3 * frame[..., None])
    col = col * (1 - chips[..., None]) + rgb((0.50, 0.49, 0.47)) * chips[..., None]
    height = 0.75 * rib + 0.3 * frame + 0.08 * var - 0.2 * chips
    return opaque(col), normal_from_height(height, 3.2)


def galvanized(seed):
    var = fbm(seed, N, 6, 6)
    spangle = np.floor(fbm(seed + 3, N, 48, 2) * 6) / 6
    st = streaks(seed + 5, density=50)
    c = 0.55 + 0.12 * var + 0.06 * spangle - 0.12 * st
    col = np.dstack([c * 0.98, c, c * 1.02])
    white_rust = smooth(fbm(seed + 9, N, 10, 4), 0.72, 0.8)
    col = col * (1 - white_rust[..., None] * 0.5) + 0.75 * white_rust[..., None] * 0.5
    return opaque(col), normal_from_height(0.3 * spangle + 0.2 * var, 1.2)


def concrete(seed, tone=0.55, soot_amount=0.0):
    """Cast concrete, 4 m tile: 2 m form panels, tie holes, pour lines, stains, optional scorch."""
    y, x = np.mgrid[0:N, 0:N]
    var = fbm(seed, N, 4, 7)
    fine = fbm(seed + 1, N, 128, 2)
    st = streaks(seed + 2, density=60)
    seam = ((x % 512) < 4) | ((y % 512) < 4)
    tie = ((((x % 512) - 128) % 256) < 9) & ((((y % 512) - 128) % 256) < 9)
    pour = smooth(np.abs(((y + 40 * fbm(seed + 3, N, 3, 3)) % 256) - 128), 0, 6)
    cracks = smooth(np.abs(fbm(seed + 5, N, 5, 5) - 0.5), 0.0, 0.012)
    c = tone + 0.10 * (var - 0.5) + 0.05 * (fine - 0.5) - 0.12 * st
    c = c * (0.92 + 0.08 * pour) * (0.75 + 0.25 * cracks)
    col = np.dstack([c * 1.01, c, c * 0.965])
    col = col * (1 - 0.35 * seam[..., None]) * (1 - 0.5 * tie[..., None])
    if soot_amount > 0:
        soot = smooth(fbm(seed + 9, N, 3, 5), 0.25, 0.8) * soot_amount
        soot = np.clip(soot + 0.35 * soot_amount * st, 0, 0.92)
        col = col * (1 - soot[..., None]) + rgb((0.035, 0.032, 0.03)) * soot[..., None]
    height = 0.5 + 0.12 * var + 0.10 * fine - 0.5 * seam - 0.7 * tie - 0.35 * (1 - cracks)
    return opaque(col), normal_from_height(height, 2.6)


def refractory(seed):
    """Scorched refractory brick lining, 2 m tile: 0.25 x 0.125 m bricks in running bond, heavy soot."""
    y, x = np.mgrid[0:N, 0:N]
    row = y // 64
    xo = (x + (row % 2) * 64) % 128
    mortar = ((y % 64) < 5) | (xo < 5)
    cell = (row * 131 + ((x + (row % 2) * 64) // 128) * 17) % 23 / 23.0
    var = fbm(seed, N, 4, 6)
    soot = smooth(fbm(seed + 3, N, 3, 5), 0.2, 0.75)
    c = 0.21 + 0.07 * cell + 0.05 * var
    col = np.dstack([c * 1.08, c * 0.97, c * 0.88])
    col = col * (1 - 0.5 * mortar[..., None])
    col = col * (1 - 0.8 * soot[..., None]) + rgb((0.03, 0.028, 0.027)) * (0.8 * soot[..., None])
    height = 0.6 - 0.6 * mortar + 0.15 * cell + 0.1 * var
    return opaque(col), normal_from_height(height, 3.0)


def hazard(seed):
    """45 degree amber / graphite stripes (0.6 m period at a 1.2 m tile), worn."""
    y, x = np.mgrid[0:N, 0:N]
    band = ((x + y) % 512) < 256
    a, _ = painted(seed, (0.86, 0.50, 0.06), 0.16, 0.04)
    d, _ = painted(seed + 1, (0.085, 0.087, 0.09), 0.16, 0.04)
    return np.where(band[..., None], a, d)


def nozzle(seed):
    """Regeneratively cooled nozzle wall. v (image y, top = throat end) carries the heat tint from dark steel through
    blue / violet to straw at the exit; u carries 64 coolant tubes per tile."""
    y, x = np.mgrid[0:N, 0:N]
    t = y / (N - 1.0)
    stops = [(0.00, (0.30, 0.31, 0.33)), (0.30, (0.26, 0.29, 0.37)), (0.52, (0.25, 0.27, 0.48)),
             (0.68, (0.42, 0.28, 0.44)), (0.82, (0.62, 0.42, 0.24)), (0.93, (0.60, 0.52, 0.33)), (1.00, (0.36, 0.34, 0.32))]
    col = np.zeros((N, N, 3))
    for (t0, c0), (t1, c1) in zip(stops[:-1], stops[1:]):
        w = np.clip((t - t0) / (t1 - t0), 0, 1)
        m = (t >= t0) & (t <= t1)
        col[m] = (np.array(c0)[None, :] * (1 - w[m])[:, None] + np.array(c1)[None, :] * w[m][:, None])
    var = fbm(seed, N, 6, 5)
    tube = 0.5 + 0.5 * np.cos(x * (2 * np.pi / 16.0))
    band = ((y % 128) < 6)
    col = col * (0.85 + 0.3 * var[..., None]) * (0.86 + 0.14 * tube[..., None]) * (1 - 0.25 * band[..., None])
    height = 0.6 * tube + 0.5 * band + 0.05 * var
    return opaque(col), normal_from_height(height, 2.2)


def foil(seed):
    """Crinkled gold thermal blanket, 1.5 m tile: quilting seams and creases."""
    y, x = np.mgrid[0:N, 0:N]
    a = fbm(seed, N, 12, 5)
    b = fbm(seed + 5, N, 20, 4)
    crease = np.abs(a - 0.5) * 2
    crease2 = np.abs(b - 0.5) * 2
    h = 0.6 * crease + 0.4 * crease2
    quilt = ((x % 256) < 4) | ((y % 256) < 4)
    c = 0.88 + 0.5 * (h - 0.4)
    col = np.dstack([c * 0.96, c * 0.70, c * 0.24])
    col = col * (1 - 0.45 * quilt[..., None])
    return opaque(col), normal_from_height(h - 0.5 * quilt, 5.0)


def rubber(seed):
    var = fbm(seed, N, 8, 5)
    y, x = np.mgrid[0:N, 0:N]
    tread = ((x % 64) < 26)
    c = 0.06 + 0.035 * var + 0.02 * tread
    return opaque(np.dstack([c, c, c * 1.05])), normal_from_height(0.5 * tread + 0.1 * var, 2.5)


def main(out):
    os.makedirs(out, exist_ok=True)
    jobs = {
        'LC1_HullWhite': hull_white(11),
        'LC1_HullGraphite': hull_graphite(22),
        'LC1_Amber': painted(33, (0.84, 0.50, 0.07), 0.10, 0.03, primer=(0.14, 0.14, 0.15), streak=0.16),
        'LC1_Red': painted(44, (0.56, 0.085, 0.055), 0.10, 0.05, streak=0.2),
        'LC1_Steel': painted(55, (0.36, 0.38, 0.41), 0.12, 0.09, primer=(0.50, 0.48, 0.45)),
        'LC1_Galvanized': galvanized(66),
        'LC1_Concrete': concrete(77, 0.56),
        'LC1_ConcreteSoot': refractory(88),
        'LC1_Hazard': (hazard(99), None),
        'LC1_Nozzle': nozzle(110),
        'LC1_Foil': foil(121),
        'LC1_Rubber': rubber(132),
        'LC1_MarkWhite': painted(143, (0.80, 0.80, 0.78), 0.22, 0.0, primer=(0.50, 0.49, 0.47), streak=0.15),
    }
    for name, (b, n) in jobs.items():
        write_png(os.path.join(out, name + '_BaseColor.png'), b)
        if n is not None:
            write_png(os.path.join(out, name + '_Normal.png'), n)
        print('texture', name)


if __name__ == '__main__':
    main(sys.argv[-1])
