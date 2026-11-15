"""Prepare tintable fabric textures from the MakeHuman CC0 system assets (output into <out dir>):
- cs02_gray.png: male_casualsuit02 diffuse with the orange MakeHuman logo/piping removed (colour mask + normalized-convolution
  fill), grayscale, normalized to mean 0.82; the jeans half gets half contrast (no stone-wash blotches on charcoal cargo).
- shoes06_gray.png, shoes03_gray.png: grayscale shoe textures.
python -I prep_textures.py <system assets x dir> <out dir>"""
import os, sys
import numpy as np
from PIL import Image, ImageFilter

SA, OUT = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)


def blur(a, r):
    im = Image.fromarray(np.clip(a * 255.0, 0, 255).astype(np.uint8)) if a.dtype != np.uint8 else Image.fromarray(a)
    return np.asarray(im.filter(ImageFilter.GaussianBlur(r))).astype(np.float64) / 255.0


def fill(rgb, mask, radii=(6, 14, 30, 60)):
    """replace masked pixels by a normalized-convolution average of the unmasked surroundings."""
    out = rgb.copy()
    valid = (~mask).astype(np.float64)
    todo = mask.copy()
    for r in radii:
        w = blur(valid, r)
        for c in range(3):
            num = blur(out[..., c] * valid, r)
            est = np.where(w > 1e-3, num / np.maximum(w, 1e-3), out[..., c])
            out[..., c] = np.where(todo & (w > 0.05), est, out[..., c])
        done = todo & (w > 0.05)
        valid = np.maximum(valid, done.astype(np.float64))
        todo = todo & ~done
    return out


def gray_norm(rgb, mean_target=0.82):
    g = 0.2126 * rgb[..., 0] + 0.7152 * rgb[..., 1] + 0.0722 * rgb[..., 2]
    return np.clip(g * (mean_target / max(1e-3, g.mean())), 0, 1)


# ---- casual suit 02
p = os.path.join(SA, "clothes/male_casualsuit02/male_casualsuit02_diffuse.png")
rgb = np.asarray(Image.open(p).convert("RGB")).astype(np.float64) / 255.0
H, W = rgb.shape[:2]
r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
orange = (r > b + 0.08) & (r > 0.30)
yy, xx = np.mgrid[0:H, 0:W]
lum = 0.2126 * r + 0.7152 * g + 0.0722 * b
# chest print (logo ring + figure) sits around 55.6 %/16.6 % of the atlas (u, 1-v); light pixels there are print
cx, cy, rad = 0.556 * W, 0.166 * H, 0.062 * W
inlogo = (xx - cx) ** 2 + (yy - cy) ** 2 < rad ** 2
med = np.median(lum[inlogo])
logo = inlogo & (lum > med + 0.06)
mask = orange | logo
mask = np.asarray(Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(7))) > 0
clean = fill(rgb, mask)
G = gray_norm(clean)
# jeans region (u < 0.73, v < 0.59 -> image y > 0.41 H): half contrast around its own mean
jeans = (xx < 0.73 * W) & (yy > 0.41 * H)
low = blur(G, 22)
Gj = 0.80 + 0.75 * (G - low) + 0.12 * (low - G[jeans].mean())     # keep weave + seams, kill stone-wash blotches
G = np.where(jeans, np.clip(Gj, 0, 1), G)
Image.fromarray((G * 255).astype(np.uint8)).save(os.path.join(OUT, "cs02_gray.png"))
print("cs02: masked px", int(mask.sum()), "mean", round(float(G.mean()), 3))
# EVA variant: bright (mean 0.95) and calmer (0.6 contrast), so a cream tint stays cream
Ge = np.clip(0.95 + 0.6 * (G - G.mean()), 0, 1)
Image.fromarray((Ge * 255).astype(np.uint8)).save(os.path.join(OUT, "cs02_gray_eva.png"))

for name in ("shoes06", "shoes03"):
    p = os.path.join(SA, "clothes/%s/%s_diffuse.png" % (name, name))
    im = Image.open(p)
    a = im.getchannel("A") if im.mode == "RGBA" else None
    rgb = np.asarray(im.convert("RGB")).astype(np.float64) / 255.0
    g0 = gray_norm(rgb, 0.78)
    if name == "shoes03":                     # EVA boot: bright, calm leather detail under a cream tint
        g0 = np.clip(0.93 + 0.45 * (g0 - g0.mean()), 0, 1)
    G = (g0 * 255).astype(np.uint8)
    out = Image.fromarray(G)
    if a is not None:
        out = Image.merge("LA", (out, a)).convert("RGBA")
    out.save(os.path.join(OUT, "%s_gray.png" % name))
    print(name, "ok")
