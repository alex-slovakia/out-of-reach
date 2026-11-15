"""V04 textures from the MakeHuman CC0 casualsuit02 layout: navy knit shirt + charcoal twill trousers in one atlas.
python -I make_textures.py <inputs dir> <out tex dir>"""
import sys, os, json
import numpy as np
from PIL import Image, ImageFilter

INP, OUT = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)
S = 2048
rng = np.random.default_rng(4)
cs = os.path.join(INP, 'mh_sa/clothes/male_casualsuit02')
ao = np.asarray(Image.open(os.path.join(cs, 'male_casualsuit02_ao.png')).convert('L').resize((S, S)), np.float32) / 255


def srgb(h):
    h = h.lstrip('#')
    return np.array([int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)], np.float32)


def noise(scale, seed):
    r = np.random.default_rng(seed)
    out = np.zeros((S, S), np.float32); amp = 1; tot = 0; s = scale
    while s <= S:
        g = r.random((s, s)).astype(np.float32)
        out += np.asarray(Image.fromarray((g * 255).astype(np.uint8)).resize((S, S), Image.BICUBIC), np.float32) / 255 * amp
        tot += amp; amp *= .5; s *= 2
    out /= tot
    return (out - out.min()) / (out.max() - out.min() + 1e-6)


yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
# Per UV island classification (never cut through a panel): trousers = islands centred low-left/centre.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mhio
from PIL import ImageDraw
_, UVc, Fc = mhio.read_obj(os.path.join(cs, 'male_casualsuit02.obj'))
parent = list(range(len(UVc)))
def find(a):
    while parent[a] != a:
        parent[a] = parent[parent[a]]; a = parent[a]
    return a
for vi, ti, g in Fc:
    for t in ti[1:]:
        ra, rb = find(ti[0]), find(t)
        if ra != rb: parent[ra] = rb
isl = {}
for k in range(len(UVc)):
    isl.setdefault(find(k), []).append(k)
cls = {}
for r, ks in isl.items():
    c = UVc[ks].mean(0)
    cls[r] = 'pants' if (c[1] < .58 and c[0] < .74) else 'shirt'
mask_img = Image.new('L', (S, S), 0)
dr = ImageDraw.Draw(mask_img)
for vi, ti, g in Fc:
    if cls[find(ti[0])] == 'pants':
        dr.polygon([(UVc[t][0] * S, (1 - UVc[t][1]) * S) for t in ti], fill=255)
mask_img = mask_img.filter(ImageFilter.MaxFilter(5))
pants = np.asarray(mask_img) > 127
json.dump({'pants_islands': sum(1 for c in cls.values() if c == 'pants'), 'shirt_islands': sum(1 for c in cls.values() if c == 'shirt')}, open(os.path.join(OUT, 'suit_islands.json'), 'w'))
# navy knit: fine vertical knit ribs + heather noise
knit = .5 + .5 * np.sin(xx * 2 * np.pi / 3.0)
heather = noise(64, 1) * .6 + noise(256, 2) * .4
shirt = srgb('#1B2541')[None, None, :] * (0.86 + 0.10 * heather[..., None] + 0.05 * knit[..., None])
# charcoal twill: 45 degree fine diagonal lines + slight mottling
twill = .5 + .5 * np.sin((xx + yy) * 2 * np.pi / 5.0)
mott = noise(32, 3) * .5 + noise(128, 4) * .5
trousers = srgb('#2E2F32')[None, None, :] * (0.84 + 0.10 * mott[..., None] + 0.06 * twill[..., None])
col = np.where(pants[..., None], trousers, shirt)
col *= (0.55 + 0.45 * ao)[..., None]
# soft wear on knees and seat of the trousers (lighter, low frequency)
col = np.clip(col, 0, 1)
Image.fromarray((col * 255).astype(np.uint8), 'RGB').save(os.path.join(OUT, 'V04_Suit_BaseColor.png'))
Image.open(os.path.join(cs, 'male_casualsuit02_normal.png')).convert('RGB').resize((S, S)).save(os.path.join(OUT, 'V04_Suit_Normal.png'))
# shoes: keep the CC0 shoe detail as luminance, tint to dark grey work shoes
sh = np.asarray(Image.open(os.path.join(INP, 'mh_sa/clothes/shoes02/shoes02_diffuse.png')).convert('RGB'), np.float32) / 255
lum = sh @ np.array([.3, .59, .11], np.float32)
shoe = srgb('#26282B')[None, None, :] * (0.45 + 0.75 * lum[..., None])
Image.fromarray((np.clip(shoe, 0, 1) * 255).astype(np.uint8), 'RGB').save(os.path.join(OUT, 'V04_Shoes_BaseColor.png'))
print('textures', sorted(os.listdir(OUT)))
