"""End-to-end check of the celestial sky chain on the committed NASA SVS EXR, run inside Blender (bundled numpy).

    blender -b --factory-startup --python Assets/_OutOfReach/Scripts/Presentation/CelestialSky/Tools~/verify_sky_chain_blender.py -- <path to starmap_2020_4k.exr>

For each reference star it takes the world direction the presenter assigns (inverse material rotation of the
catalogue RA/Dec), runs it through the exact CelestialPanoramic.shader lookup (quaternion rotate, atan2/acos,
Unity v = 1 at the image top) and requires a saturated star pixel there. The old unrotated material is the
negative control. It also checks that the default sunlight direction samples the Horizons Sun position.
Read-only: nothing is written.
"""
import math
import sys

import bpy
import numpy as np

Q = np.array([-0.425012484, 0.269377633, -0.238544697, 0.830600088])  # NasaDeepSky_Celestial.mat _SkyRotation
SUN_WORLD = np.array([0.679108, 0.559193, -0.475516])                   # -forward of Euler(34, -55, 0)
SUN_RADEC = (288.340, -23.935)                                          # Horizons, Sun from Mars, day 0
STARS = {"Sirius": (101.2872, -16.7161), "Canopus": (95.9880, -52.6957), "Arcturus": (213.9153, 19.1824),
         "Vega": (279.2347, 38.7837), "Rigel": (78.6345, -8.2016), "Betelgeuse": (88.7929, 7.4071),
         "Polaris": (37.9529, 89.2641), "Capella": (79.172, 45.998), "Procyon": (114.825, 5.225)}

path = sys.argv[sys.argv.index("--") + 1]
image = bpy.data.images.load(path, check_existing=False)
W, H = image.size
pixels = np.empty(W * H * 4, dtype=np.float32)
image.pixels.foreach_get(pixels)
lum = (pixels.reshape(H, W, 4)[::-1, :, :3] @ np.array([.2126, .7152, .0722], np.float32))  # row 0 = image top


def rot(q, v):
    u, w = q[:3], q[3]
    t = 2 * np.cross(u, v)
    return v + w * t + np.cross(u, t)


def inverse(q):
    return np.array([-q[0], -q[1], -q[2], q[3]])


def shader_pixel(q, world):
    d = rot(q, np.asarray(world, float))
    d /= np.linalg.norm(d)
    u = 0.5 - math.atan2(d[2], d[0]) * (0.5 / math.pi)
    v = 1.0 - math.acos(max(-1.0, min(1.0, d[1]))) / math.pi
    return int((u % 1.0) * W), min(H - 1, int((1.0 - v) * H))


def peak(x, y, r=3):
    return float(lum[max(0, y - r):y + r + 1, :].take(range(x - r, x + r + 1), axis=1, mode="wrap").max())


def sky(ra, dec):
    ra, dec = math.radians(ra), math.radians(dec)
    return np.array([math.cos(dec) * math.cos(ra), math.sin(dec), math.cos(dec) * math.sin(ra)])


hits = misses = 0
for name, (ra, dec) in STARS.items():
    world = rot(inverse(Q), sky(ra, dec))
    hits += peak(*shader_pixel(Q, world)) > 0.9
    misses += peak(*shader_pixel(np.array([0, 0, 0, 1.0]), world)) < 0.5
x, y = shader_pixel(Q, SUN_WORLD)
sun_ra, sun_dec = ((0.5 - (x + 0.5) / W) * 360) % 360, 90 - (y + 0.5) / H * 180
sun_error = math.degrees(math.acos(min(1.0, float(sky(sun_ra, sun_dec) @ sky(*SUN_RADEC)))))
print(f"stars recovered {hits}/{len(STARS)}; unrotated control missed {misses}/{len(STARS)}; "
      f"sunlight samples RA {sun_ra:.2f} Dec {sun_dec:.2f}, {sun_error:.3f} deg from Horizons", flush=True)
ok = hits == len(STARS) and misses == len(STARS) and sun_error < 0.1
print("CELESTIAL_SKY_CHAIN_PASS" if ok else "CELESTIAL_SKY_CHAIN_FAIL", flush=True)
sys.exit(0 if ok else 1)
