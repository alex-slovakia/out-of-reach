"""EVA-TETHER-01: original braided tether sheath textures (procedural, no external source).

One tile = once around the rope (U) x one repeat along it (V, about one circumference, 0.094 m).
Writes EvaTetherV02_Rope_BaseColor.png (sRGB) and EvaTetherV02_Rope_Normal.png (tangent space, OpenGL +Y)
next to ../Textures. Run: python -I make_rope_textures.py
"""
import os
import numpy as np
from PIL import Image

SIZE = 512
STRANDS = 8          # strands per family around the rope (16-carrier braid)
FIBRES = 7           # fibre lines across one strand
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Textures")

u, v = np.meshgrid((np.arange(SIZE) + 0.5) / SIZE, (np.arange(SIZE) + 0.5) / SIZE)
a = (u + v) * STRANDS           # family A runs one way round
b = (u - v) * STRANDS           # family B the other way
ia, ib = np.floor(a), np.floor(b)
fa, fb = a - ia, b - ib

# Plain (one-over-one) braid on the crossing grid: in each cell one strand lies on top, shaped like a grain:
# rounded across the strand and bulging along it between two crossings.
a_on_top = np.mod(ia + ib, 2) == 0
across_a = np.sin(np.pi * fa) ** 0.55
across_b = np.sin(np.pi * fb) ** 0.55
along_a = np.sin(np.pi * fb)      # A runs along the b direction inside a cell
along_b = np.sin(np.pi * fa)
height = np.where(a_on_top, across_a * (0.55 + 0.45 * along_a), across_b * (0.55 + 0.45 * along_b))
# Fine fibres along each strand and a little irregularity.
rng = np.random.default_rng(1511)
fibre_a = 0.5 + 0.5 * np.cos(2 * np.pi * fa * FIBRES)
fibre_b = 0.5 + 0.5 * np.cos(2 * np.pi * fb * FIBRES)
fibre = np.where(a_on_top, fibre_a, fibre_b)
noise = rng.normal(0.0, 1.0, (SIZE, SIZE))
for _ in range(2):  # cheap tileable blur
    noise = (noise + np.roll(noise, 1, 0) + np.roll(noise, -1, 0) + np.roll(noise, 1, 1) + np.roll(noise, -1, 1)) / 5.0
height = np.clip(height + 0.06 * (fibre - 0.5) + 0.03 * noise, 0.0, 1.0)

# Colours: off-white sheath, two amber tracer strands (family A) and one graphite strand (family B).
white = np.array([0.86, 0.85, 0.80])
amber = np.array([0.90, 0.53, 0.11])
graphite = np.array([0.20, 0.22, 0.25])
strand_a = np.mod(ia, STRANDS)
strand_b = np.mod(ib, STRANDS)
colour = np.empty((SIZE, SIZE, 3))
colour[:] = white
amber_mask = a_on_top & ((strand_a == 0) | (strand_a == 4))
graphite_mask = (~a_on_top) & (strand_b == 2)
colour[amber_mask] = amber
colour[graphite_mask] = graphite
tone = np.where(a_on_top, 1.0, 0.94)[..., None]          # the two families catch light slightly differently
ao = (0.50 + 0.50 * height)[..., None]                    # crevices between strands
fibre_shade = (0.93 + 0.07 * fibre)[..., None]
albedo = np.clip(colour * tone * ao * fibre_shade, 0.0, 1.0)
srgb = np.where(albedo <= 0.0031308, albedo * 12.92, 1.055 * np.power(albedo, 1 / 2.4) - 0.055)
Image.fromarray((srgb * 255 + 0.5).astype(np.uint8), "RGB").save(os.path.join(OUT, "EvaTetherV02_Rope_BaseColor.png"))

# Tangent-space normal map from the height field (tileable central differences).
strength = 3.2
du = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * 0.5 * SIZE / STRANDS / 8.0
dv = (np.roll(height, -1, 0) - np.roll(height, 1, 0)) * 0.5 * SIZE / STRANDS / 8.0
n = np.stack([-du * strength, -dv * strength, np.ones_like(height)], axis=-1)
n /= np.linalg.norm(n, axis=-1, keepdims=True)
Image.fromarray(((n * 0.5 + 0.5) * 255 + 0.5).astype(np.uint8), "RGB").save(os.path.join(OUT, "EvaTetherV02_Rope_Normal.png"))
print("wrote", OUT)
