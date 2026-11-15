"""V04 EVA textures: warm-ivory suit fabric on the CC0 casualsuit02 layout, worn ivory hard-goods, chest display.
python -I make_textures_eva.py <inputs dir> <out tex dir>"""
import sys, os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

INP, OUT = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)
S = 2048
cs = os.path.join(INP, 'mh_sa/clothes/male_casualsuit02')
ao = np.asarray(Image.open(os.path.join(cs, 'male_casualsuit02_ao.png')).convert('L').resize((S, S)), np.float32) / 255


def srgb(h):
    h = h.lstrip('#')
    return np.array([int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)], np.float32)


def noise(n, scale, seed):
    r = np.random.default_rng(seed)
    out = np.zeros((n, n), np.float32); amp = 1; tot = 0; s = scale
    while s <= n:
        g = r.random((s, s)).astype(np.float32)
        out += np.asarray(Image.fromarray((g * 255).astype(np.uint8)).resize((n, n), Image.BICUBIC), np.float32) / 255 * amp
        tot += amp; amp *= .5; s *= 2
    out /= tot
    return (out - out.min()) / (out.max() - out.min() + 1e-6)


# ---- suit fabric: warm ivory (FitV02 MAT_EVA_WarmIvory #D5D3CB), AO folds, mottling, light grime ----
base = srgb('#DAD5CA')
mott = noise(S, 8, 11)
grime = noise(S, 32, 12)
speck = noise(S, 256, 13)
col = np.ones((S, S, 3), np.float32) * base
col *= (0.60 + 0.40 * ao)[..., None]
col *= (0.95 + 0.07 * mott)[..., None]
g = np.clip((grime - 0.62) * 3.0, 0, 1) * 0.10 + np.clip((speck - 0.80) * 4, 0, 1) * 0.08
col *= 1 - g[..., None] * np.array([0.9, 1.0, 1.15], np.float32)
# panel seams along the CC0 garment's UV island borders: seam groove + dashed stitch line inside each panel
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mhio
_, UVc, Fc = mhio.read_obj(os.path.join(cs, 'male_casualsuit02.obj'))
parent = list(range(len(UVc)))


def find(a):
    while parent[a] != a:
        parent[a] = parent[parent[a]]; a = parent[a]
    return a


for vi, ti, g in Fc:
    for t in ti[1:]:
        ra, rb = find(ti[0]), find(t)
        if ra != rb:
            parent[ra] = rb
ids = {}
lab = Image.new('I', (S, S), 0); dr = ImageDraw.Draw(lab)
for vi, ti, g in Fc:
    r = find(ti[0]); ids.setdefault(r, len(ids) + 1)
    dr.polygon([(UVc[t][0] * S, (1 - UVc[t][1]) * S) for t in ti], fill=ids[r])
L = np.asarray(lab)
inside = L > 0
edge = np.zeros_like(inside)
for sh in ((0, 1), (0, -1), (1, 0), (-1, 0)):
    edge |= inside & (np.roll(L, sh, (0, 1)) != L)


def dil(mask, k):
    im = Image.fromarray((mask * 255).astype(np.uint8))
    for _ in range(k):
        im = im.filter(ImageFilter.MaxFilter(3))
    return np.asarray(im) > 127


yy, xx = np.mgrid[0:S, 0:S]
b2 = dil(edge, 2) & inside
b6 = dil(edge, 6) & inside
b8 = dil(edge, 8) & inside
stitch = b8 & ~b6 & (((xx + yy) // 5) % 2 == 0)
# ripstop grid and wear that grows toward the boots (height from the CC0 garment's own fitted positions)
rip = ((xx % 14) == 0) | ((yy % 14) == 0)
col *= np.where(rip & inside, 0.965, 1.0)[..., None]
Vg, _, _ = mhio.read_obj(os.path.join(cs, 'male_casualsuit02.obj'))
hgt = Image.new('L', (S, S), 120); dh = ImageDraw.Draw(hgt)
for vi, ti, g in Fc:
    hm = float(np.mean([Vg[v][1] for v in vi])) * 0.1065109473396234 + 0.875583853
    dh.polygon([(UVc[t][0] * S, (1 - UVc[t][1]) * S) for t in ti], fill=int(np.clip(hm * 120, 0, 255)))
hgt = np.asarray(hgt.filter(ImageFilter.BoxBlur(6)), np.float32) / 120
low = np.clip((0.50 - hgt) / 0.42, 0, 1); low = low * low * (3 - 2 * low)
dirt = low * (0.10 + 0.14 * grime)
col *= 1 - dirt[..., None] * np.array([0.75, 0.95, 1.25], np.float32)
col *= np.where(b2, 0.78, 1.0)[..., None]
col *= np.where(stitch, 0.86, 1.0)[..., None]
print('SEAM_PANELS', len(ids))
Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8), 'RGB').save(os.path.join(OUT, 'V04E_Suit_BaseColor.png'))

# ---- hard goods (PLSS, chest pack, helmet shell, boots): ivory with soft wear ----
H = 1024
m = noise(H, 6, 21); w = noise(H, 64, 22)
hard = np.ones((H, H, 3), np.float32) * srgb('#D3CEC3')
hard *= (0.93 + 0.08 * m)[..., None]
hard *= 1 - (np.clip((w - 0.70) * 3, 0, 1) * 0.12)[..., None]
im = Image.fromarray((np.clip(hard, 0, 1) * 255).astype(np.uint8), 'RGB')
d = ImageDraw.Draw(im)
r = np.random.default_rng(23)
for _ in range(140):                                   # fine scratches
    x, y = r.integers(0, H, 2); L = r.integers(8, 40); a = r.uniform(0, np.pi)
    c = int(r.integers(150, 185))
    d.line([(x, y), (x + L * np.cos(a), y + L * np.sin(a))], fill=(c, c - 4, c - 10), width=1)
im.save(os.path.join(OUT, 'V04E_Hard_BaseColor.png'))

# ---- chest display: dark screen with grid, trace and status bars (also used as emission) ----
Wd, Hd = 512, 320
im = Image.new('RGB', (Wd, Hd), (8, 18, 24))
d = ImageDraw.Draw(im)
for x in range(16, Wd - 16, 32):
    d.line([(x, 20), (x, Hd - 70)], fill=(14, 40, 44))
for y in range(20, Hd - 70, 32):
    d.line([(16, y), (Wd - 16, y)], fill=(14, 40, 44))
pts = [(16 + i * 4, 140 - 60 * np.sin(i / 9.0) * np.exp(-i / 160.0)) for i in range(0, 118)]
d.line(pts, fill=(90, 230, 160), width=3)
pts2 = [(16 + i * 4, 200 + 18 * np.sin(i / 4.0 + 1)) for i in range(0, 118)]
d.line(pts2, fill=(90, 180, 230), width=2)
for i, (lab, v) in enumerate((('O2', .78), ('CO2', .22), ('PWR', .64), ('H2O', .55))):
    y = Hd - 58 + i * 13
    d.rectangle([16, y, 16 + 40, y + 8], fill=(60, 120, 110))
    d.rectangle([64, y, 64 + int(380 * v), y + 8], fill=(90, 230, 160) if v > .3 else (240, 170, 60))
d.rectangle([2, 2, Wd - 3, Hd - 3], outline=(40, 70, 72), width=3)
im.save(os.path.join(OUT, 'V04E_Display.png'))
print('EVA_TEXTURES_OK')
