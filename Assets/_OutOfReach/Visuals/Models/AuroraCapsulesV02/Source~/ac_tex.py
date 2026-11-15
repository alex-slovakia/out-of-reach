"""Procedural PBR textures for the AURORA capsules (rescue craft RC, crew transport CT).

Everything is painted with numpy in UV space; no photographs, no third-party textures.
Arrays use row 0 = v 0 (bottom), the same order Blender images and UVs use.

Maps per material (URP Lit, metallic workflow):
  *_BaseColor.png  sRGB
  *_Normal.png     tangent space, OpenGL / Unity convention (+Y up)
  *_Mask.png       R metallic, G occlusion, A smoothness  (used as _MetallicGlossMap and _OcclusionMap)
"""
import math
import os

import numpy as np

TAU = 2.0 * math.pi
F32 = np.float32


# ----------------------------------------------------------------------------- helpers
def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def vnoise(h, w, cy, cx, seed):
    """Value noise, cy x cx cells, tileable in x and y."""
    rng = np.random.RandomState(seed)
    g = rng.rand(cy, cx).astype(F32)
    ys = np.linspace(0, cy, h, endpoint=False, dtype=F32)
    xs = np.linspace(0, cx, w, endpoint=False, dtype=F32)
    y0 = np.floor(ys).astype(np.int32)
    x0 = np.floor(xs).astype(np.int32)
    fy = ys - y0
    fx = xs - x0
    fy = fy * fy * (3 - 2 * fy)
    fx = fx * fx * (3 - 2 * fx)
    y1 = (y0 + 1) % cy
    x1 = (x0 + 1) % cx
    top = g[y0][:, x0] * (1 - fx) + g[y0][:, x1] * fx
    bot = g[y1][:, x0] * (1 - fx) + g[y1][:, x1] * fx
    return top * (1 - fy)[:, None] + bot * fy[:, None]


def fbm(h, w, cy, cx, seed, octaves=5, gain=0.5):
    out = np.zeros((h, w), F32)
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        c_y, c_x = cy * (2 ** o), cx * (2 ** o)
        if c_y > h // 2 or c_x > w // 2:
            break
        out += amp * vnoise(h, w, c_y, c_x, seed + 31 * o)
        tot += amp
        amp *= gain
    return out / max(tot, 1e-6)


def normal_from_height(hm, sx, sy, strength=1.0):
    """hm in metres; sx, sy = metres per pixel (sx may be an (H,1) array)."""
    dx = (np.roll(hm, -1, 1) - np.roll(hm, 1, 1)) / (2.0 * sx)
    dy = (np.roll(hm, -1, 0) - np.roll(hm, 1, 0)) / (2.0 * sy)
    nx = -dx * strength
    ny = -dy * strength
    inv = 1.0 / np.sqrt(nx * nx + ny * ny + 1.0)
    out = np.empty(hm.shape + (4,), F32)
    out[..., 0] = nx * inv * 0.5 + 0.5
    out[..., 1] = ny * inv * 0.5 + 0.5
    out[..., 2] = inv * 0.5 + 0.5
    out[..., 3] = 1.0
    return out


def rgba(rgb):
    out = np.empty(rgb.shape[:2] + (4,), F32)
    out[..., :3] = np.clip(rgb, 0, 1)
    out[..., 3] = 1.0
    return out


def mask(metal, ao, smoothness):
    out = np.empty(metal.shape + (4,), F32)
    out[..., 0] = np.clip(metal, 0, 1)
    out[..., 1] = np.clip(ao, 0, 1)
    out[..., 2] = 0.0
    out[..., 3] = np.clip(smoothness, 0, 1)
    return out


def lerp3(base, colour, m):
    """base (H,W,3) towards colour (3,) by mask m (H,W) in place."""
    for c in range(3):
        base[..., c] += (colour[c] - base[..., c]) * m


def save(path, arr, srgb):
    import bpy
    h, w = arr.shape[:2]
    name = os.path.basename(path)
    old = bpy.data.images.get(name)
    if old:
        bpy.data.images.remove(old)
    img = bpy.data.images.new(name, w, h, alpha=True)
    img.colorspace_settings.name = 'sRGB' if srgb else 'Non-Color'
    img.alpha_mode = 'CHANNEL_PACKED'
    img.pixels.foreach_set(np.ascontiguousarray(arr, dtype=F32).ravel())
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    return img


# ----------------------------------------------------------------------------- back shell
ROW_COLS = [48] * 8 + [48, 40, 40, 32, 32, 24, 24, 16]      # tile columns per full turn, 8 cylinder + 8 cone rows


def paint_hull(G, W, H, span, variant, pods=(), seed=7):
    """Back-shell tile field.

    u in [0,1) covers `span` radians of azimuth starting at G['TH_U0'] (a gore seam); v covers the profile
    arc 0..S_TOT (cylinder then cone). CT uses one 45 degree gore repeated 8 times; RC paints the whole turn
    so that soot, streaks and repairs are unique.
    """
    rc = variant == 'RC'
    S_CYL, CONE, S_TOT = G['S_CYL'], G['CONE_LEN'], G['S_TOT']
    R_CYL, R_TOP = G['R_CYL'], G['R_TOP']
    gores = int(round(span / (TAU / 8)))
    u = (np.arange(W, dtype=F32) + 0.5) / W
    v = (np.arange(H, dtype=F32) + 0.5) / H
    s = v * S_TOT
    on_cyl = s <= S_CYL
    r = np.where(on_cyl, R_CYL, R_CYL + (R_TOP - R_CYL) * (s - S_CYL) / CONE).astype(F32)
    rh_c, rh_k = S_CYL / 8.0, CONE / 8.0
    rowi = np.where(on_cyl, np.floor(s / rh_c), 8 + np.floor((s - S_CYL) / rh_k)).astype(np.int32)
    rowi = np.clip(rowi, 0, 15)
    rowf = np.where(on_cyl, (s / rh_c) % 1.0, ((s - S_CYL) / rh_k) % 1.0).astype(F32)
    rowh = np.where(on_cyl, rh_c, rh_k).astype(F32)
    ncols = (np.array(ROW_COLS)[rowi] * span / TAU).astype(F32)            # columns across this texture
    stag = (rowi % 2) * 0.5
    cu = u[None, :] * ncols[:, None] + stag[:, None]
    coli = np.floor(cu).astype(np.int32)
    colf = (cu - coli).astype(F32)
    tile_w = (span * r / ncols).astype(F32)
    dx = np.minimum(colf, 1 - colf) * tile_w[:, None]
    dy = (np.minimum(rowf, 1 - rowf) * rowh)[:, None]
    d = np.minimum(dx, dy)                                                 # metres to the nearest tile edge
    gap = 1.0 - smooth(0.0014, 0.0034, d)
    face = smooth(0.0034, 0.0130, d)

    # per-tile identity (wraps with the texture)
    rng = np.random.RandomState(seed)
    table = rng.rand(16, 64).astype(F32)
    table2 = rng.rand(16, 64).astype(F32)
    cmod = coli % np.maximum(ncols.astype(np.int32), 1)[:, None]
    tid = table[rowi[:, None], cmod]
    tid2 = table2[rowi[:, None], cmod]

    # gore (panel) seams every 45 degrees and three circumferential panel joints
    gf = u[None, :] * gores
    gfr = gf - np.round(gf)
    xg = gfr * (TAU / 8) * r[:, None]                                      # signed metres to the gore seam
    panel_v = np.abs(xg)
    joints = [S_CYL * 0.5, S_CYL, S_CYL + CONE * 0.5]
    panel_h = np.full(H, 9.0, F32)
    for j in joints:
        panel_h = np.minimum(panel_h, np.abs(s - j))
    panel_h = panel_h[:, None]
    pd = np.minimum(panel_v, panel_h)
    pgap = 1.0 - smooth(0.0030, 0.0060, pd)
    plip = smooth(0.0060, 0.0100, pd) * (1.0 - smooth(0.0100, 0.0200, pd))    # slightly raised closeout strip

    # fasteners beside the panel seams
    pitch = 0.13
    sy = (s % pitch) - pitch * 0.5
    fd_v = np.sqrt((np.abs(xg) - 0.032) ** 2 + (sy ** 2)[:, None])
    xm = (u[None, :] * span * r[:, None])
    fx = (xm % pitch) - pitch * 0.5
    fd_h = np.sqrt((panel_h - 0.032) ** 2 + fx ** 2)
    fd = np.minimum(fd_v, fd_h)
    fast = 1.0 - smooth(0.0050, 0.0070, fd)
    fring = (1.0 - smooth(0.0070, 0.0100, fd)) - fast

    # ---- colour
    n_lo = fbm(H, W, 3 * max(1, H // W), 3 * max(1, W // H), seed + 1, 5)
    n_hi = fbm(H, W, 96 * max(1, H // W), 96 * max(1, W // H), seed + 2, 3)
    if rc:
        tile_rgb = np.array([0.745, 0.742, 0.725], F32)
        tint_amp, gap_rgb = 0.050, np.array([0.085, 0.082, 0.080], F32)
    else:
        tile_rgb = np.array([0.868, 0.872, 0.868], F32)
        tint_amp, gap_rgb = 0.022, np.array([0.150, 0.158, 0.165], F32)
    val = 1.0 + (tid - 0.5) * 2.0 * tint_amp + (n_lo - 0.5) * 0.05 + (n_hi - 0.5) * 0.030
    base = np.empty((H, W, 3), F32)
    for c in range(3):
        base[..., c] = tile_rgb[c] * val
    # warm / cool drift between tiles (different firing batches)
    base[..., 0] *= 1.0 + (tid2 - 0.5) * (0.030 if rc else 0.012)
    base[..., 2] *= 1.0 - (tid2 - 0.5) * (0.030 if rc else 0.012)

    # painted livery bands (paint follows the tiles, the gaps stay dark)
    graphite = np.array([0.105, 0.115, 0.125], F32)
    band_lo = (smooth(-1.0, 0.0, s) * (1.0 - smooth(0.150, 0.158, s)))[:, None]
    nose = smooth(S_TOT - 0.335, S_TOT - 0.325, s)[:, None]
    if rc:
        accent = np.array([0.830, 0.250, 0.055], F32)                      # international orange rescue belt
        a0, a1 = 1.960, 2.480
    else:
        accent = np.array([0.905, 0.545, 0.085], F32)                      # amber cheat line
        a0, a1 = 2.395, 2.470
    acc = (smooth(a0 - 0.004, a0 + 0.004, s) * (1.0 - smooth(a1 - 0.004, a1 + 0.004, s)))[:, None]
    paint_var = 1.0 + (n_hi - 0.5) * 0.06 + (tid - 0.5) * 0.03
    lerp3(base, graphite, band_lo)
    lerp3(base, graphite, nose)
    lerp3(base, accent, acc)
    painted = np.clip(band_lo + nose + acc, 0, 1)
    for c in range(3):
        base[..., c] = np.where(painted > 0.5, base[..., c] * paint_var, base[..., c])
    if not rc:
        # thin graphite pinstripes either side of the amber line
        pin = np.zeros(H, F32)
        for z0 in (a0 - 0.030, a1 + 0.018):
            pin += smooth(z0 - 0.002, z0 + 0.002, s) * (1.0 - smooth(z0 + 0.010, z0 + 0.014, s))
        lerp3(base, graphite, np.clip(pin, 0, 1)[:, None])

    height = face * 0.0016 + (n_hi - 0.5) * 0.00035 + (tid - 0.5) * 0.0006 * face
    smoothn = 0.50 + (tid - 0.5) * 0.10 + (n_lo - 0.5) * 0.10 if not rc else 0.36 + (tid - 0.5) * 0.14 + (n_lo - 0.5) * 0.14
    smoothn = smoothn * (1.0 - 0.18 * painted)
    metal = np.zeros((H, W), F32)
    ao = np.ones((H, W), F32)

    # ---- weathering (rescue craft only)
    if rc:
        soot = np.zeros((H, W), F32)
        # recirculated plume soot above the shoulder ring, fading up the cylinder
        streak = fbm(H, W, 2, 220, seed + 5, 4)                             # long along the profile
        blot = fbm(H, W, 5, 10, seed + 6, 4)
        soot += (1.0 - smooth(0.0, 0.85, s))[:, None] * (0.30 + 0.55 * streak) * 0.55
        # ascent heating streaks running down the cone from the nose ring
        cone_t = np.clip((s - S_CYL) / CONE, 0, 1)[:, None]
        soot += smooth(0.15, 1.0, cone_t) * smooth(0.45, 0.80, streak) * (0.30 + 0.5 * blot) * 0.50
        soot += smooth(0.35, 0.9, blot) * 0.10
        # thruster plume blooms: (theta, s, direction) for each jet
        for (th, sc, dth, ds, reach) in pods:
            uu = ((th - G['TH_U0']) % TAU) / span
            du = u[None, :] - uu
            du = (du + 0.5) % 1.0 - 0.5
            px = du * span * r[:, None]
            py = (s - sc)[:, None]
            along = px * dth + py * ds
            across = -px * ds + py * dth
            w0 = 0.05 + 0.34 * np.clip(along / reach, 0, 1)
            bloom = np.exp(-(across / w0) ** 2) * smooth(-0.03, 0.05, along) * (1.0 - smooth(reach * 0.35, reach, along))
            soot += bloom * (0.55 + 0.35 * streak)
        soot = np.clip(soot, 0, 1) * (0.78 + 0.22 * tid)
        soot_rgb = np.array([0.150, 0.118, 0.092], F32)
        m = soot * (0.62 - 0.30 * painted)
        lerp3(base, soot_rgb, m)
        smoothn = smoothn * (1.0 - 0.55 * soot)
        # replaced tiles (clean, slightly brighter) and a few scorched ones
        fresh = (tid2 > 0.965) * face
        burnt = (tid2 < 0.030) * face
        for c in range(3):
            base[..., c] = base[..., c] * (1.0 + 0.16 * fresh * (1 - painted)) * (1.0 - 0.38 * burnt)
        smoothn = smoothn + 0.12 * fresh
        # micrometeoroid pits
        pit = vnoise(H, W, max(8, H // 6), max(8, W // 6), seed + 9)
        pits = smooth(0.985, 0.995, pit)
        lerp3(base, np.array([0.06, 0.06, 0.06], F32), pits * 0.8)
        height = height - pits * 0.0012
        gap_rgb = gap_rgb * 0.8

    # ---- gaps, panel joints, fasteners on top
    lerp3(base, gap_rgb, gap * 0.92)
    lerp3(base, gap_rgb * 0.7, pgap)
    edge_dirt = (1.0 - smooth(0.0034, 0.030, d)) * (0.16 if rc else 0.045)
    for c in range(3):
        base[..., c] *= 1.0 - edge_dirt
    alu = np.array([0.62, 0.63, 0.64], F32)
    lerp3(base, alu, fast)
    lerp3(base, gap_rgb, np.clip(fring, 0, 1) * 0.7)
    height = height * (1.0 - gap) - gap * 0.0016 - pgap * 0.0030 + plip * 0.0007 + fast * 0.0012
    smoothn = smoothn * (1.0 - gap * 0.75) * (1.0 - pgap * 0.8)
    smoothn = np.where(fast > 0.5, 0.62, smoothn)
    metal = np.maximum(metal, fast * 0.95)
    ao = ao * (1.0 - 0.50 * gap) * (1.0 - 0.60 * pgap) * (1.0 - 0.25 * np.clip(fring, 0, 1))
    ao *= 1.0 - 0.10 * (1.0 - smooth(0.0, 0.04, d))

    sx = (span * r / W)[:, None]
    sy_m = S_TOT / H
    return rgba(base), normal_from_height(height.astype(F32), sx, sy_m, 1.6), mask(metal, ao, smoothn)


# ----------------------------------------------------------------------------- ablative base heat shield
def paint_heatshield(N, variant, radius, seed=21):
    """Planar map, uv 0.5 = axis, the square spans 2*radius metres."""
    rc = variant == 'RC'
    c = (np.arange(N, dtype=F32) + 0.5) / N - 0.5
    X = c[None, :] * 2 * radius
    Y = c[:, None] * 2 * radius
    rho = np.sqrt(X * X + Y * Y)
    phi = np.arctan2(Y, X)
    ring_w = 0.44
    ring = np.floor(rho / ring_w)
    rf = rho / ring_w - ring
    nblk = np.maximum(1.0, np.round(ring * 6.0 + 1.0))
    pa = (phi / TAU + 0.5 + ring * 0.137) * nblk
    pf = pa - np.floor(pa)
    blk_w = TAU * np.maximum(rho, 0.05) / nblk
    d = np.minimum(np.minimum(rf, 1 - rf) * ring_w, np.minimum(pf, 1 - pf) * blk_w)
    d = np.where(ring < 1, np.minimum(rf, 1 - rf) * ring_w, d)
    gap = 1.0 - smooth(0.0020, 0.0050, d)
    rng = np.random.RandomState(seed)
    tb = rng.rand(16, 128).astype(F32)
    tid = tb[np.clip(ring, 0, 15).astype(np.int32), (np.floor(pa).astype(np.int32)) % 128]
    n1 = fbm(N, N, 6, 6, seed + 1, 5)
    n2 = fbm(N, N, 160, 160, seed + 2, 2)
    if rc:
        col = np.array([0.090, 0.078, 0.068], F32)
        gapc = np.array([0.30, 0.25, 0.20], F32)
    else:
        col = np.array([0.335, 0.285, 0.225], F32)
        gapc = np.array([0.52, 0.50, 0.46], F32)
    val = 1.0 + (tid - 0.5) * 0.16 + (n1 - 0.5) * 0.30 + (n2 - 0.5) * 0.22
    base = np.empty((N, N, 3), F32)
    for k in range(3):
        base[..., k] = col[k] * val
    if rc:
        # radial flow streaks and a paler, glassy rim where the char spalled
        radial = (np.sin(phi * 37.0 + n1 * 9.0) * 0.5 + 0.5) * smooth(0.5, radius, rho)
        lerp3(base, np.array([0.30, 0.24, 0.19], F32), radial * 0.35 * n1 * 1.6)
        rim = smooth(radius * 0.80, radius * 0.98, rho)
        lerp3(base, np.array([0.36, 0.33, 0.30], F32), rim * 0.45 * (0.5 + n1))
    lerp3(base, gapc, gap * 0.85)
    height = (1 - gap) * 0.002 + (n2 - 0.5) * 0.0016 + (n1 - 0.5) * 0.004
    smoothn = (0.16 if rc else 0.24) + (n1 - 0.5) * 0.1 - gap * 0.08
    metal = np.zeros((N, N), F32)
    ao = 1.0 - 0.45 * gap
    px = 2 * radius / N
    return rgba(base), normal_from_height(height.astype(F32), px, px, 1.5), mask(metal, ao, smoothn)


# ----------------------------------------------------------------------------- tiling trim surfaces (1 uv = 1 m)
def paint_metal(N, seed=41):
    """Satin machined aluminium: directional brush, faint tooling arcs, a few handling scuffs."""
    brush = fbm(N, N, 256, 2, seed, 3)                                      # streaks along u
    fine = fbm(N, N, 400, 12, seed + 1, 2)
    blot = fbm(N, N, 4, 4, seed + 2, 5)
    v = 0.66 + (brush - 0.5) * 0.10 + (fine - 0.5) * 0.05 + (blot - 0.5) * 0.06
    base = np.dstack([v * 0.985, v * 0.995, v * 1.010]).astype(F32)
    scuff = smooth(0.62, 0.80, fbm(N, N, 9, 9, seed + 3, 4)) * smooth(0.5, 0.7, fbm(N, N, 60, 3, seed + 4, 2))
    for c in range(3):
        base[..., c] *= 1.0 - 0.10 * scuff
    height = (brush - 0.5) * 0.00012 + (fine - 0.5) * 0.00008
    smoothn = 0.60 + (brush - 0.5) * 0.16 - scuff * 0.18 + (blot - 0.5) * 0.08
    metal = np.full((N, N), 0.96, F32)
    ao = np.ones((N, N), F32)
    return rgba(base), normal_from_height(height.astype(F32), 1.0 / N, 1.0 / N, 1.5), mask(metal, ao, smoothn)


def paint_graphite(N, seed=57):
    """Graphite epoxy / anodised trim: fine orange-peel, soft mottling, sparse bright nicks."""
    peel = fbm(N, N, 180, 180, seed, 2)
    blot = fbm(N, N, 5, 5, seed + 1, 5)
    v = 0.088 + (blot - 0.5) * 0.030 + (peel - 0.5) * 0.016
    base = np.dstack([v * 0.92, v * 1.00, v * 1.09]).astype(F32)
    nick = smooth(0.990, 0.997, vnoise(N, N, N // 3, N // 3, seed + 2))
    lerp3(base, np.array([0.42, 0.43, 0.44], F32), nick * 0.7)
    height = (peel - 0.5) * 0.00030
    smoothn = 0.46 + (blot - 0.5) * 0.14 + (peel - 0.5) * 0.06
    metal = np.full((N, N), 0.25, F32) + nick * 0.6
    ao = np.ones((N, N), F32)
    return rgba(base), normal_from_height(height.astype(F32), 1.0 / N, 1.0 / N, 1.5), mask(metal, ao, smoothn)


# ----------------------------------------------------------------------------- kit palette (flat swatches)
# name: (sRGB colour, metallic, smoothness, emission colour or None)
KIT = [
    ('amber',      (0.905, 0.545, 0.085), 0.00, 0.46, None),
    ('orange',     (0.830, 0.250, 0.055), 0.00, 0.42, None),
    ('yellow',     (0.870, 0.700, 0.120), 0.10, 0.50, None),
    ('red',        (0.720, 0.070, 0.050), 0.00, 0.45, None),
    ('white',      (0.880, 0.880, 0.860), 0.00, 0.48, None),
    ('marking',    (0.090, 0.100, 0.110), 0.00, 0.40, None),
    ('black',      (0.018, 0.018, 0.020), 0.00, 0.30, None),
    ('seal',       (0.030, 0.030, 0.034), 0.00, 0.22, None),
    ('glass',      (0.020, 0.032, 0.048), 0.55, 0.96, None),
    ('nozzle',     (0.215, 0.175, 0.150), 0.90, 0.42, None),
    ('nozzle_hot', (0.060, 0.050, 0.048), 0.60, 0.25, None),
    ('gold',       (0.800, 0.580, 0.220), 1.00, 0.66, None),
    ('silver',     (0.780, 0.790, 0.800), 1.00, 0.72, None),
    ('radiator',   (0.900, 0.905, 0.900), 0.00, 0.62, None),
    ('lamp_white', (0.950, 0.950, 0.900), 0.00, 0.80, (1.00, 0.96, 0.86)),
    ('lamp_red',   (0.600, 0.050, 0.030), 0.00, 0.80, (1.00, 0.10, 0.05)),
    ('lamp_green', (0.050, 0.500, 0.150), 0.00, 0.80, (0.10, 1.00, 0.30)),
    ('lamp_amber', (0.700, 0.380, 0.050), 0.00, 0.80, (1.00, 0.55, 0.10)),
    ('lamp_cyan',  (0.100, 0.500, 0.600), 0.00, 0.80, (0.20, 0.85, 1.00)),
    ('solar',      (0.030, 0.045, 0.110), 0.35, 0.86, None),
]
KIT_GRID = 8


def kit_uv(name):
    for i, k in enumerate(KIT):
        if k[0] == name:
            return ((i % KIT_GRID + 0.5) / KIT_GRID, (i // KIT_GRID + 0.5) / KIT_GRID)
    raise KeyError(name)


def paint_kit(N=256):
    cell = N // KIT_GRID
    base = np.zeros((N, N, 3), F32) + 0.5
    met = np.zeros((N, N), F32)
    smo = np.zeros((N, N), F32) + 0.5
    emi = np.zeros((N, N, 3), F32)
    for i, (name, col, m, sm, e) in enumerate(KIT):
        x0, y0 = (i % KIT_GRID) * cell, (i // KIT_GRID) * cell
        base[y0:y0 + cell, x0:x0 + cell] = col
        met[y0:y0 + cell, x0:x0 + cell] = m
        smo[y0:y0 + cell, x0:x0 + cell] = sm
        if e:
            emi[y0:y0 + cell, x0:x0 + cell] = e
    return rgba(base), mask(met, np.ones((N, N), F32), smo), rgba(emi)
