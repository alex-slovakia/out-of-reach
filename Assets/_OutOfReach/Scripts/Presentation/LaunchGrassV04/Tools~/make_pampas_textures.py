"""LAUNCH-GRASS-V04: original procedural textures for the pampas field (no source imagery).

Draws every plume from scratch as fine strokes: a curved rachis, drooping branchlets and short spikelets,
accumulated as soft coverage so the core is dense and the rim stays wispy. Deterministic (fixed seeds).

Outputs (next to the shaders, sRGB, alpha = coverage):
  PampasPlumesV04.png  1024x512   4 plume heads (256x512 cells), base at the bottom centre of each cell
  PampasClumpsV04.png  1024x1024  4 whole clumps (512x512 cells): leaf fountain, bare stalks, plume heads
  PampasTopsV04.png    512x512    4 clumps seen from above (256x256 cells): radiating leaves and seed heads

Usage:  python make_pampas_textures.py <output folder>
Needs numpy and Pillow only.
"""
import sys
import numpy as np
from PIL import Image

SS = 3  # supersampling


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.W, self.H = w * SS, h * SS
        self.acc = np.zeros((self.H, self.W), np.float32)
        self.col = np.zeros((self.H, self.W, 3), np.float32)

    def stroke(self, pts, width0, width1, alpha0, alpha1, col0, col1):
        """Polyline in cell pixels (y up). Width in cell pixels; alpha is opacity inside the stroke."""
        pts = np.asarray(pts, np.float32) * SS
        seg = np.linalg.norm(np.diff(pts, axis=0), axis=1)
        length = float(seg.sum())
        if length < 1e-3:
            return
        n = max(2, int(length / .6))
        s = np.linspace(0, 1, n)
        cum = np.concatenate([[0], np.cumsum(seg)]) / length
        x = np.interp(s, cum, pts[:, 0]); y = np.interp(s, cum, pts[:, 1])
        tx = np.gradient(x); ty = np.gradient(y)
        norm = np.maximum(np.hypot(tx, ty), 1e-6)
        nx, ny = -ty / norm, tx / norm
        if np.ndim(width0):   # per-point widths (interpolated along the stroke)
            width = np.interp(s, cum, np.asarray(width0, np.float32)) * SS
        else:
            width = (width0 + (width1 - width0) * s) * SS
        alpha = alpha0 + (alpha1 - alpha0) * s
        across = int(max(1, np.ceil(width.max() / .8)))
        step = length / n
        col0 = np.asarray(col0, np.float32); col1 = np.asarray(col1, np.float32)
        colour = col0[None, :] + (col1 - col0)[None, :] * s[:, None]
        for k in range(across):
            off = ((k + .5) / across - .5) * width
            px = x + nx * off; py = y + ny * off
            ix = np.round(px).astype(np.int32); iy = np.round(py).astype(np.int32)
            ok = (ix >= 0) & (ix < self.W) & (iy >= 0) & (iy < self.H)
            dep = (alpha * step * np.maximum(width, .35) / across)[ok]
            np.add.at(self.acc, (iy[ok], ix[ok]), dep)
            np.add.at(self.col, (iy[ok], ix[ok]), colour[ok] * dep[:, None])

    def finish(self):
        """Returns RGBA float (h, w, 4), top row first."""
        a = 1 - np.exp(-self.acc * 1.6)
        rgb = self.col / np.maximum(self.acc, 1e-6)[..., None]
        pre = rgb * a[..., None]
        a_lo = a.reshape(self.h, SS, self.w, SS).mean(axis=(1, 3))
        pre_lo = pre.reshape(self.h, SS, self.w, SS, 3).mean(axis=(1, 3))
        rgb_lo = pre_lo / np.maximum(a_lo, 1e-4)[..., None]
        # Fill transparent texels with the row's mean colour so mip-maps and bilinear taps never darken the rim.
        weight = a_lo[..., None]
        row = (rgb_lo * weight).sum(axis=1) / np.maximum(weight.sum(axis=1), 1e-4)
        have = weight.sum(axis=1)[:, 0] > 1e-3
        idx = np.where(have)[0]
        if len(idx):
            nearest = idx[np.abs(np.arange(self.h)[:, None] - idx[None, :]).argmin(axis=1)]
            row = row[nearest]
        blend = np.clip(a_lo * 6, 0, 1)[..., None]
        rgb_lo = rgb_lo * blend + row[:, None, :] * (1 - blend)
        out = np.concatenate([np.clip(rgb_lo, 0, 1), a_lo[..., None]], axis=2)
        return out[::-1]


CREAM_CORE = (0.86, 0.74, 0.52)
CREAM_TIP = (0.99, 0.93, 0.78)
STRAW = (0.80, 0.66, 0.40)
OLIVE_DARK = (0.30, 0.29, 0.13)
OLIVE_MID = (0.50, 0.45, 0.22)


def plume(cv, rng, x0, y0, length, half_width, lean, droop, density=1.0, fine=1.0):
    """A feathery panicle. Angles are radians from vertical, positive to the right."""
    n = 70
    t = np.linspace(0, 1, n)
    ang = lean + droop * t ** 1.9
    xs = x0 + np.cumsum(np.sin(ang)) * length / n
    ys = y0 + np.cumsum(np.cos(ang)) * length / n
    shade = rng.uniform(.9, 1.05)
    core = np.array(CREAM_CORE) * shade; tip = np.array(CREAM_TIP) * min(1.0, shade + .03)
    # Soft translucent body (so the head has volume and survives mip-mapping), then the rachis.
    body = 2 * half_width * .36 * np.sin(np.pi * np.clip(t * 1.04 - .04, 0, 1) ** .7) ** .9 * (1 - .42 * t)
    cv.stroke(np.stack([xs, ys], 1), np.maximum(body, .6), None, .3, .2, tip * .96, tip)
    cv.stroke(np.stack([xs, ys], 1), 2.2 * fine, .8 * fine, .95, .8, core * .82, core)
    side_of_droop = 1 if droop >= 0 else -1
    for _ in range(int(430 * density)):
        tb = rng.uniform(.0, .985)
        i = min(n - 1, int(tb * n))
        side = 1 if rng.random() < .5 else -1
        env = np.sin(np.pi * tb ** .55) ** .85 * (1 - .42 * tb)
        blen = half_width * env * rng.uniform(.5, 1.05) * (1.15 if side == side_of_droop else .9)
        if blen < 1.5:
            continue
        a0 = ang[i] + side * np.radians(rng.uniform(10, 30))
        curl = side * np.radians(rng.uniform(30, 85)) + side_of_droop * np.radians(rng.uniform(0, 28))
        m = 12
        s = np.linspace(0, 1, m)
        a = a0 + curl * s ** 1.6
        bx = xs[i] + np.cumsum(np.sin(a)) * blen / m
        by = ys[i] + np.cumsum(np.cos(a)) * blen / m
        tone = rng.uniform(.86, 1.06)
        cv.stroke(np.stack([bx, by], 1), 1.5 * fine, .5 * fine, .72, .3, core * tone, tip * min(1.0, tone))
        for _ in range(int(rng.integers(7, 13))):
            j = int(rng.integers(1, m))
            sl = rng.uniform(5, 14) * fine * (half_width / 60)
            sa = a[j] + rng.choice([-1, 1]) * np.radians(rng.uniform(18, 55))
            cv.stroke([(bx[j], by[j]), (bx[j] + np.sin(sa) * sl, by[j] + np.cos(sa) * sl)],
                      .9 * fine, .4 * fine, .5, .2, tip * tone, tip)
    return xs[-1], ys[-1]


def plume_cell(seed):
    rng = np.random.default_rng(seed)
    cv = Canvas(256, 512)
    side = 1 if seed % 2 else -1
    droop = side * np.radians(rng.uniform(10, 24))
    lean = -side * np.radians(rng.uniform(0, 4))
    plume(cv, rng, 128, 4, rng.uniform(455, 485), rng.uniform(70, 84), lean, droop,
          density=1.25)
    return cv.finish()


def clump_cell(seed):
    rng = np.random.default_rng(seed)
    cv = Canvas(512, 512)
    cx = 256
    # Leaf fountain: long thin leaves arching outwards and drooping at the tips (kept below the seed heads).
    for _ in range(140):
        side = rng.choice([-1, 1])
        a0 = side * np.radians(rng.uniform(2, 52))
        curl = side * np.radians(rng.uniform(50, 140))
        length = rng.uniform(130, 255)
        m = 22
        s = np.linspace(0, 1, m)
        a = a0 + curl * s ** 1.5
        x = cx + rng.uniform(-24, 24) + np.cumsum(np.sin(a)) * length / m
        y = 2 + np.cumsum(np.cos(a)) * length / m
        tone = rng.uniform(.8, 1.1)
        cv.stroke(np.stack([x, np.maximum(y, 1)], 1), rng.uniform(3.2, 5.0), 1.0, .95, .85,
                  np.array(OLIVE_DARK) * tone, np.array(STRAW if rng.random() < .45 else OLIVE_MID) * tone)
    # Many bare stalks fanning out, each with one large seed head: from a distance the heads are the plant.
    count = int(rng.integers(9, 13))
    order = rng.permutation(count)
    for k in range(count):
        f = (order[k] + .5) / count * 2 - 1                      # -1 .. 1 across the fan
        lean = np.radians(f * 27 + rng.uniform(-4, 4))
        height = rng.uniform(235, 318) * (1 - .12 * abs(f))
        m = 18
        s = np.linspace(0, 1, m)
        a = lean * (.7 + .5 * s)
        x = cx + f * 14 + np.cumsum(np.sin(a)) * height / m
        y = 4 + np.cumsum(np.cos(a)) * height / m
        cv.stroke(np.stack([x, y], 1), 3.2, 2.0, .97, .97, np.array(OLIVE_MID) * .9, STRAW)
        side = 1 if f + rng.uniform(-.3, .3) > 0 else -1
        droop = side * np.radians(rng.uniform(12, 32))
        plume(cv, rng, x[-1], y[-1] - 6, rng.uniform(165, 192), rng.uniform(40, 50), a[-1], droop, density=.5, fine=1.1)
    return cv.finish()


def top_cell(seed):
    """A clump seen from straight above: leaves radiating from the centre, seed heads fanning outwards."""
    rng = np.random.default_rng(seed)
    cv = Canvas(256, 256)
    cx = cy = 128
    for _ in range(120):
        ang = rng.uniform(0, 2 * np.pi)
        length = rng.uniform(55, 116)
        m = 14
        s = np.linspace(0, 1, m)
        a = ang + np.radians(rng.uniform(-22, 22)) * s
        x = cx + rng.uniform(-6, 6) + np.cumsum(np.sin(a)) * length / m
        y = cy + rng.uniform(-6, 6) + np.cumsum(np.cos(a)) * length / m
        tone = rng.uniform(.8, 1.1)
        cv.stroke(np.stack([x, y], 1), rng.uniform(2.6, 4.2), .9, .95, .8,
                  np.array(OLIVE_DARK) * tone, np.array(STRAW if rng.random() < .45 else OLIVE_MID) * tone)
    count = int(rng.integers(9, 13))
    for k in range(count):
        ang = (k + rng.uniform(.1, .9)) / count * 2 * np.pi
        r0 = rng.uniform(14, 52)
        x0, y0 = cx + np.sin(ang) * r0, cy + np.cos(ang) * r0
        plume(cv, rng, x0, y0, rng.uniform(52, 70), rng.uniform(15, 19), ang + np.radians(rng.uniform(-18, 18)),
              np.radians(rng.uniform(-14, 14)), density=.22, fine=1.0)
    return cv.finish()


def save(cells, cols, path):
    rows = [np.concatenate(cells[r * cols:(r + 1) * cols], axis=1) for r in range(len(cells) // cols)]
    img = np.concatenate(rows, axis=0)
    Image.fromarray((np.clip(img, 0, 1) * 255 + .5).astype(np.uint8), 'RGBA').save(path, optimize=True)
    print(path, img.shape, 'coverage %.3f' % img[..., 3].mean())


if __name__ == '__main__':
    out = sys.argv[1]
    save([plume_cell(s) for s in (11, 12, 13, 14)], 4, out + '/PampasPlumesV04.png')
    save([clump_cell(s) for s in (21, 22, 23, 24)], 2, out + '/PampasClumpsV04.png')
    save([top_cell(s) for s in (31, 32, 33, 34)], 2, out + '/PampasTopsV04.png')
