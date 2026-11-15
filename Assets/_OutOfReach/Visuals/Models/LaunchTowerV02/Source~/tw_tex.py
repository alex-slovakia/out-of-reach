"""Procedural textures for LaunchTowerV02 (numpy + Pillow, all original). python -I tw_tex.py <out_dir> <Inter-Regular.otf>"""
import sys, os
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tw_layout as L

OUT = sys.argv[1]
FONT = sys.argv[2] if len(sys.argv) > 2 else None
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(11)


def noise(n, scale, seed=0):
    r = np.random.default_rng(seed)
    out = np.zeros((n, n), np.float32)
    amp, tot = 1.0, 0.0
    s = scale
    while s <= n:
        g = r.random((s, s)).astype(np.float32)
        im = Image.fromarray((g * 255).astype(np.uint8)).resize((n, n), Image.BICUBIC)
        out += np.asarray(im, np.float32) / 255 * amp
        tot += amp
        amp *= 0.5
        s *= 2
    out /= tot
    return (out - out.min()) / (out.max() - out.min() + 1e-6)


def save_rgb(a, name):
    Image.fromarray(np.clip(a * 255, 0, 255).astype(np.uint8), 'RGB').save(os.path.join(OUT, name))


def save_rgba(a, name):
    Image.fromarray(np.clip(a * 255, 0, 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, name))


def tint(base, n):
    return np.array(base, np.float32)[None, None, :] * (0.78 + 0.44 * n[..., None])


N = 1024
# ---- painted structural steel (graphite, matching the graphite/amber/cyan/red art direction), chips and grime ----
n1, n2, n3 = noise(N, 4, 1), noise(N, 16, 2), noise(N, 64, 3)
col = tint((0.30, 0.315, 0.335), 0.6 * n1 + 0.4 * n2)
streak = noise(N, 2, 4)[:, :1] * np.ones((1, N), np.float32)          # vertical streaks
streak = np.asarray(Image.fromarray((noise(N, 2, 5) * 255).astype(np.uint8)).resize((N, N)).filter(ImageFilter.GaussianBlur(1)), np.float32) / 255
vs = np.tile(noise(N, 128, 6)[:1, :], (N, 1))                          # vertical rain streaks
col *= (1 - 0.18 * (vs ** 2))[..., None]
chips = (n3 > 0.80) & (n2 > 0.55)
col[chips] = np.array([0.36, 0.27, 0.22], np.float32) * 1.0              # rust under chipped paint
col *= (1 - 0.10 * (n2 > 0.9)[..., None])
save_rgb(col, 'TW_SteelPainted_BaseColor.png')
sm = 0.38 + 0.12 * n2 - 0.15 * chips
save_rgba(np.stack([np.full((N, N), 0.15, np.float32) + 0.5 * chips, np.zeros((N, N), np.float32), np.zeros((N, N), np.float32), sm], -1), 'TW_SteelPainted_MetallicSmoothness.png')

# ---- galvanised steel: spangle pattern ----
g1 = noise(N, 32, 7)
sp = noise(N, 256, 8)
col = tint((0.62, 0.64, 0.65), 0.5 * g1 + 0.5 * sp)
col *= (1 - 0.15 * (vs ** 2))[..., None]
save_rgb(col, 'TW_Galvanized_BaseColor.png')
save_rgba(np.stack([np.full((N, N), 0.8, np.float32), np.zeros((N, N), np.float32), np.zeros((N, N), np.float32), 0.45 + 0.2 * sp], -1), 'TW_Galvanized_MetallicSmoothness.png')

# ---- hazard stripes, ochre and near black, 8 diagonal periods per tile ----
yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
stripe = (((xx + yy) / (N / 8.0)) % 1.0) < 0.5
base = np.where(stripe[..., None], np.array([0.64, 0.42, 0.09], np.float32), np.array([0.07, 0.07, 0.06], np.float32))
base = base * (0.8 + 0.3 * noise(N, 16, 9)[..., None])
base[(noise(N, 64, 10) > 0.86)] *= 0.55
save_rgb(base, 'TW_HazardStripe_BaseColor.png')

# ---- fire-red pipe paint ----
col = tint((0.46, 0.07, 0.05), 0.6 * noise(N, 8, 12) + 0.4 * noise(N, 64, 13))
col[(noise(N, 64, 14) > 0.88)] = np.array([0.30, 0.20, 0.18], np.float32)
col *= (1 - 0.2 * (vs ** 2))[..., None]
save_rgb(col, 'TW_PipeRed_BaseColor.png')
save_rgba(np.stack([np.full((N, N), 0.05, np.float32), np.zeros((N, N), np.float32), np.zeros((N, N), np.float32), np.full((N, N), 0.45, np.float32)], -1), 'TW_PipeRed_MetallicSmoothness.png')

# ---- rubber ----
col = tint((0.045, 0.045, 0.05), noise(N, 32, 15))
save_rgb(col, 'TW_Rubber_BaseColor.png')

# ---- grating: bearing bars + cross bars, alpha cut-out (tile = 0.5 m) ----
G = 512
img = np.zeros((G, G, 4), np.float32)
pitch = G // 12            # 12 bars per 0.5 m -> 42 mm pitch
bar = 7
xs = np.arange(G)
bars = (xs % pitch) < bar
cross = ((np.arange(G) % (G // 4)) < 5)
mask = bars[None, :] | cross[:, None]
border = (xs < 8) | (xs > G - 9)
mask = mask | border[None, :] | border[:, None]
shade = 0.55 + 0.25 * noise(G, 16, 16)
img[..., 0] = shade * 0.92
img[..., 1] = shade * 0.94
img[..., 2] = shade * 0.96
img[..., 3] = mask.astype(np.float32)
save_rgba(img, 'TW_Grating_BaseColor.png')

# ---- label atlas ----
W, H = L.ATLAS_W, L.ATLAS_H
atlas = Image.new('RGB', (W, H), (24, 26, 28))
d = ImageDraw.Draw(atlas)
def font(sz):
    try:
        return ImageFont.truetype(FONT, sz)
    except Exception:
        return ImageFont.load_default()
def cell(key, lines, fg, bg, sizes, stripe=None, pad=12):
    r = L.LABELS[key]
    x0, y1 = int(r[0] * W), int((1 - r[1]) * H)
    x1, y0 = int(r[2] * W), int((1 - r[3]) * H)
    d.rectangle([x0, y0, x1 - 1, y1 - 1], fill=bg)
    d.rectangle([x0 + 6, y0 + 6, x1 - 7, y1 - 7], outline=fg, width=4)
    tot = sum(sizes) * 1.12
    y = y0 + ((y1 - y0) - tot) / 2
    for t, s in zip(lines, sizes):
        f = font(s)
        tw = d.textlength(t, font=f)
        d.text((x0 + ((x1 - x0) - tw) / 2, y), t, font=f, fill=fg)
        y += s * 1.12
    if stripe:
        d.rectangle([x0, y0, x1 - 1, y0 + 14], fill=stripe)
OCH = (190, 126, 34)
cell('tower_main', ['AURORA-7  ·  LAUNCH COMPLEX 1', 'CREW ACCESS TOWER'], (232, 226, 210), (30, 33, 36), [88, 104], OCH)
cell('crew_hood', ['AURORA-7  ·  CREW HATCH CONNECTION', 'WATCH HEAD  ·  MIND THE GAP'], (30, 28, 22), OCH, [56, 56])
cell('deluge', ['DELUGE RISER', 'DN200  ·  AUR-7'], (236, 226, 220), (120, 22, 16), [66, 62])
cell('cable', ['CABLE TRAY', 'C-1  ·  POWER / DATA'], (232, 226, 210), (30, 33, 36), [66, 52])
cell('valve', ['V-07', 'DELUGE ISOL.'], (236, 226, 220), (120, 22, 16), [96, 48])
for i, (u0, v0, u1, v1) in L.LEVELS.items():
    x0, y1 = int(u0 * W), int((1 - v0) * H)
    x1, y0 = int(u1 * W), int((1 - v1) * H)
    d.rectangle([x0, y0, x1 - 1, y1 - 1], fill=(30, 33, 36))
    d.rectangle([x0 + 5, y0 + 5, x1 - 6, y1 - 6], outline=OCH, width=4)
    f = font(120)
    t = 'L%d' % i
    tw = d.textlength(t, font=f)
    d.text((x0 + ((x1 - x0) - tw) / 2, y0 + 56), t, font=f, fill=(232, 226, 210))
atlas.save(os.path.join(OUT, 'TW_Labels_BaseColor.png'))
print('textures ok', sorted(os.listdir(OUT)))
