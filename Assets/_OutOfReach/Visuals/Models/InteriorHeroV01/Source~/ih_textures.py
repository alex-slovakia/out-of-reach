"""
ih_textures - procedural textures for the AURORA-7 interior hero props (INTERIOR-HERO-V01, original work).

Plain Python 3 with numpy and Pillow (no Blender needed):   python ih_textures.py
Writes ../Textures/IH_*.png and ../Textures/ih_labels.json / ih_screens.json (atlas maps read by the Blender build).

Everything is generated here: tiling PBR sets (BaseColor, Normal, MODS = metallic / occlusion / detail / smoothness),
one label atlas with alpha (placards, stencils, hazard bands, hook-and-loop patches, cue cards) and one emissive
display atlas. Lettering uses the two OFL fonts already shipped in Assets/_OutOfReach/UI/Typography.
Display pages are deliberately non-authoritative: schematics, plots and mode legends, never a simulated value.
"""
import json
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "Textures"))
FONTS = os.path.normpath(os.path.join(HERE, "..", "..", "..", "..", "UI", "Typography"))
BOLD = os.path.join(FONTS, "BarlowCondensed-Bold.ttf")
SEMI = os.path.join(FONTS, "SourceSans3-Semibold.otf")
REG = os.path.join(FONTS, "SourceSans3-Regular.otf")
RNG = np.random.default_rng(7)

INK = (22, 24, 27)
PUTTY = (203, 197, 184)
AMBER = (232, 158, 28)
CYAN = (36, 186, 204)
RED = (196, 44, 32)
WHITE = (236, 234, 228)
GRAPHITE = (44, 47, 52)


def font(path, px):
    return ImageFont.truetype(path, max(6, int(round(px))))


# ----------------------------------------------------------------------------- tileable noise helpers
def fractal(size, beta=2.0, seed=0, aniso=(1.0, 1.0)):
    """Tileable fractal noise in 0..1 from a filtered random spectrum."""
    rng = np.random.default_rng(seed)
    fx = np.fft.fftfreq(size)[:, None] * aniso[0]
    fy = np.fft.fftfreq(size)[None, :] * aniso[1]
    f = np.sqrt(fx * fx + fy * fy)
    f[0, 0] = 1.0
    spectrum = (rng.normal(size=(size, size)) + 1j * rng.normal(size=(size, size))) / f ** (beta / 2.0)
    spectrum[0, 0] = 0
    n = np.real(np.fft.ifft2(spectrum))
    n = (n - n.min()) / (n.max() - n.min())
    return n


def normal_from_height(h, strength=2.0):
    dx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * 0.5 * strength
    dy = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * 0.5 * strength
    nz = np.ones_like(h)
    length = np.sqrt(dx * dx + dy * dy + nz * nz)
    n = np.stack([-dx / length, dy / length, nz / length], axis=-1)   # +Y up (OpenGL / Unity)
    return ((n * 0.5 + 0.5) * 255).clip(0, 255).astype(np.uint8)


def save_rgb(name, a):
    Image.fromarray(a.clip(0, 255).astype(np.uint8), "RGB").save(os.path.join(OUT, name), optimize=True)


def save_mods(name, metallic, occlusion, smooth):
    z = np.zeros_like(metallic)
    a = np.stack([metallic, occlusion, z, smooth], axis=-1) * 255
    Image.fromarray(a.clip(0, 255).astype(np.uint8), "RGBA").save(os.path.join(OUT, name), optimize=True)


def tint(base, colour):
    return base[..., None] * np.asarray(colour, dtype=np.float64)[None, None, :]


def scratches(size, count, seed, length=(40, 220), width=1):
    img = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(img)
    rng = np.random.default_rng(seed)
    for _ in range(count):
        x, y = rng.integers(0, size, 2)
        a = rng.uniform(0, math.pi)
        ln = rng.uniform(*length)
        dx, dy = math.cos(a) * ln, math.sin(a) * ln
        for ox in (-size, 0, size):
            for oy in (-size, 0, size):
                d.line((x + ox, y + oy, x + dx + ox, y + dy + oy), fill=int(rng.uniform(90, 255)), width=width)
    return np.asarray(img, dtype=np.float64) / 255.0


# ----------------------------------------------------------------------------- tiling sets
def paint_set(size=1024):
    """Warm powder coat: mottling, handling scuffs, a few fine scratches, orange-peel normal."""
    mottle = fractal(size, 2.6, 11)
    peel = fractal(size, 0.9, 12)
    scuff = np.clip(fractal(size, 2.2, 13) * 1.9 - 1.15, 0, 1)
    scr = scratches(size, 46, 14)
    scr = np.asarray(Image.fromarray((scr * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.6)), dtype=np.float64) / 255
    value = 0.93 + 0.07 * mottle - 0.05 * scuff + 0.05 * scr
    base = tint(value, (255, 255, 255))
    base[..., 2] *= 0.985 + 0.015 * mottle            # slight warm drift
    save_rgb("IH_Paint_BaseColor.png", base)
    height = peel * 0.35 + mottle * 0.25 - scr * 0.5
    Image.fromarray(normal_from_height(height, 5.0), "RGB").save(os.path.join(OUT, "IH_Paint_Normal.png"), optimize=True)
    smooth = 0.42 + 0.10 * mottle - 0.16 * scuff + 0.08 * scr
    save_mods("IH_Paint_MODS.png", np.zeros((size, size)), 0.92 + 0.08 * mottle, smooth)


def chipped_set(size=1024):
    """Edge wear for the bevel strips: paint chipped to bare metal in irregular flakes. One normal / MODS set,
    two base colours with the paint colour baked in (putty powder coat and graphite anodise)."""
    n = fractal(size, 1.7, 21) * 0.65 + fractal(size, 0.8, 22) * 0.35
    metal = (n > 0.585).astype(np.float64)
    metal = np.asarray(Image.fromarray((metal * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8)), dtype=np.float64) / 255
    brushed = fractal(size, 1.6, 23, (0.05, 1.0))
    shade = 0.93 + 0.07 * fractal(size, 2.4, 24)
    alu = (0.62 + 0.25 * brushed)[..., None] * np.array([222.0, 224.0, 228.0])[None, None, :]
    for name, colour in (("Putty", (203, 197, 184)), ("Graphite", (48, 51, 58))):
        paint = shade[..., None] * np.asarray(colour, dtype=np.float64)[None, None, :]
        save_rgb("IH_Chipped%s_BaseColor.png" % name, paint * (1 - metal[..., None]) + alu * metal[..., None])
    Image.fromarray(normal_from_height(metal * 0.6 + brushed * 0.15, 4.0), "RGB").save(os.path.join(OUT, "IH_Chipped_Normal.png"), optimize=True)
    save_mods("IH_Chipped_MODS.png", metal * 0.95, np.ones((size, size)), 0.40 * (1 - metal) + 0.66 * metal)


def metal_set(size=1024):
    """Brushed aluminium (tinted by the material for anodised colours)."""
    brushed = fractal(size, 1.5, 31, (0.035, 1.0))
    blot = fractal(size, 2.5, 32)
    scr = scratches(size, 30, 33, (60, 300))
    value = 0.80 + 0.14 * brushed + 0.05 * blot + 0.06 * scr
    save_rgb("IH_Metal_BaseColor.png", tint(value, (255, 255, 255)))
    Image.fromarray(normal_from_height(brushed * 0.5 - scr * 0.3, 3.0), "RGB").save(os.path.join(OUT, "IH_Metal_Normal.png"), optimize=True)
    save_mods("IH_Metal_MODS.png", np.ones((size, size)), 0.95 + 0.05 * blot, 0.52 + 0.22 * brushed - 0.1 * scr)


def fabric_set(size=1024):
    """Plain-weave flight fabric, near white so materials tint it (seat, bag, strap, beta cloth)."""
    y, x = np.mgrid[0:size, 0:size].astype(np.float64)
    period = size / 96.0
    warp = np.sin(x / period * 2 * math.pi) * 0.5 + 0.5
    weft = np.sin(y / period * 2 * math.pi) * 0.5 + 0.5
    over = ((np.floor(x / period) + np.floor(y / period)) % 2)
    weave = np.where(over > 0.5, warp ** 0.6 * (0.55 + 0.45 * weft), weft ** 0.6 * (0.55 + 0.45 * warp))
    fuzz = fractal(size, 1.0, 41)
    wear = fractal(size, 2.6, 42)
    value = 0.78 + 0.16 * weave + 0.05 * fuzz - 0.08 * np.clip(wear * 2 - 1.1, 0, 1)
    save_rgb("IH_Fabric_BaseColor.png", tint(value, (255, 255, 255)))
    Image.fromarray(normal_from_height(weave * 0.9 + fuzz * 0.25, 6.0), "RGB").save(os.path.join(OUT, "IH_Fabric_Normal.png"), optimize=True)
    save_mods("IH_Fabric_MODS.png", np.zeros((size, size)), 0.72 + 0.28 * weave, 0.10 + 0.10 * weave)


def quilt_set(size=1024):
    """Channel-quilted upholstery: stitched seams every 1/8 tile, a fine weave on top."""
    y, x = np.mgrid[0:size, 0:size].astype(np.float64)
    cells = 8
    u = (y / size * cells) % 1.0
    puff = np.sin(u * math.pi) ** 0.55
    cross = (x / size * 2) % 1.0
    puff *= 0.9 + 0.1 * np.sin(cross * math.pi) ** 0.3
    period = size / 128.0
    weave = (np.sin(x / period * 2 * math.pi) * np.sin(y / period * 2 * math.pi)) * 0.5 + 0.5
    stitch_row = np.abs(u - 0.5) > 0.47
    dash = ((x / (size / 96.0)) % 1.0) < 0.6
    stitch = (stitch_row & dash).astype(np.float64)
    wrinkle = fractal(size, 2.2, 51, (1.0, 0.35))
    value = 0.74 + 0.16 * puff + 0.05 * weave - 0.10 * (np.abs(u - 0.5) > 0.44) + 0.10 * stitch
    save_rgb("IH_Quilt_BaseColor.png", tint(value, (255, 255, 255)))
    Image.fromarray(normal_from_height(puff * 9.0 + weave * 0.5 + wrinkle * 2.5 + stitch * 0.8, 3.2), "RGB").save(
        os.path.join(OUT, "IH_Quilt_Normal.png"), optimize=True)
    save_mods("IH_Quilt_MODS.png", np.zeros((size, size)), 0.55 + 0.45 * puff, 0.14 + 0.08 * puff)


def leaf_soil_set(size=512):
    """Grow medium: dark expanded-clay pellets under the plants."""
    n = fractal(size, 1.2, 61)
    cells = fractal(size, 0.4, 62)
    value = 0.25 + 0.5 * n * (0.6 + 0.4 * cells)
    rgb = tint(value, (150, 104, 78))
    save_rgb("IH_Medium_BaseColor.png", rgb)
    Image.fromarray(normal_from_height(n * 2 + cells, 8.0), "RGB").save(os.path.join(OUT, "IH_Medium_Normal.png"), optimize=True)


# ----------------------------------------------------------------------------- label atlas
LABEL_PPM = 4000.0     # pixels per metre on the label atlas
ATLAS = 4096

# key, style, width m, height m, lines
LABELS = [
    # ---- seats
    ("seat_cdr", "plate", 0.16, 0.05, ["SEAT 1  CDR", "AUR7-SE-0101  ·  STROKE LOCK BEFORE EGRESS"]),
    ("seat_fe", "plate", 0.16, 0.05, ["SEAT 2  FE", "AUR7-SE-0102  ·  STROKE LOCK BEFORE EGRESS"]),
    ("seat_harness", "white", 0.11, 0.03, ["5-PT RESTRAINT", "TURN TO RELEASE"]),
    ("seat_o2", "cyan", 0.075, 0.032, ["O2", "SUIT LOOP"]),
    ("seat_comm", "white", 0.075, 0.032, ["COMM", "J-14"]),
    ("no_step", "red", 0.13, 0.036, ["NO STEP"]),
    ("foot_restraint", "stencil_light", 0.14, 0.022, ["FOOT RESTRAINT"]),
    ("thc", "black", 0.06, 0.016, ["THC  TRANSLATE"]),
    ("rhc", "black", 0.06, 0.016, ["RHC  ROTATE"]),
    ("card_ascent", "card", 0.105, 0.150, ["CUE CARD 3 · DEPARTURE BURN", "1  HARNESS ........ LOCKED", "2  VISOR .......... AS REQD",
                                             "3  ATT REF ........ INERTIAL", "4  RCS QUADS ...... ARM", "5  GUIDANCE ...... AUTO",
                                             "6  CALL  \"GO FOR BURN\"", "7  MONITOR  CHAMBER P", "8  AT CUTOFF: RCS SAFE",
                                             "", "IF NO CUTOFF: MANUAL", "ENGINE STOP (GUARDED)"]),
    ("card_comm", "card", 0.105, 0.150, ["CUE CARD 7 · LONG-RANGE COMM", "SIGNALS TRAVEL AT LIGHT SPEED.", "A REPLY CANNOT ARRIVE SOONER",
                                           "THAN OUT + BACK DELAY.", "", "1  HGA ............ TRACK", "2  CARRIER ........ LOCK",
                                           "3  SEND · LOG TIME SENT", "4  DO NOT RESEND EARLY", "5  WORK THE PROBLEM", "",
                                           "KEEP MESSAGES SHORT."]),
    ("tape_mira", "tape", 0.09, 0.022, ["STICKS — CHECK TWICE"]),
    ("tape_spare", "tape", 0.075, 0.020, ["SPARE PENCILS"]),
    # ---- console
    ("con_title_l", "stencil_light", 0.30, 0.026, ["GUIDANCE  NAVIGATION  CONTROL"]),
    ("con_title_c", "stencil_light", 0.22, 0.026, ["FLIGHT  AVIONICS"]),
    ("con_title_r", "stencil_light", 0.30, 0.026, ["COMM  AND  DATA  HANDLING"]),
    ("leg_power", "legend", 0.40, 0.030, ["BUS A", "BUS B", "FC 1", "FC 2", "INV", "ESS"]),
    ("leg_eclss", "legend", 0.40, 0.030, ["FAN 1", "FAN 2", "CO2", "H2O", "N2", "O2"]),
    ("leg_rcs", "legend", 0.40, 0.030, ["QUAD A", "QUAD B", "QUAD C", "QUAD D", "ISOL", "XFEED"]),
    ("leg_light", "legend", 0.40, 0.030, ["CABIN", "PANEL", "FLOOD", "EXT", "NAV", "STROBE"]),
    ("on_off", "stencil_light", 0.03, 0.05, ["ON", "", "", "OFF"]),
    ("eng_stop", "red", 0.15, 0.036, ["ENGINE STOP"]),
    ("eng_stop_guard", "amber", 0.15, 0.030, ["LIFT GUARD · PULL TO ARM"]),
    ("haz_band", "hazard", 0.30, 0.022, []),
    ("haz_band_short", "hazard", 0.16, 0.016, []),
    ("haz_sq", "hazard", 0.20, 0.20, []),
    ("knob_att", "stencil_light", 0.11, 0.016, ["ATT REF  ·  INRTL  LVLH  SUN"]),
    ("knob_dim", "stencil_light", 0.07, 0.016, ["PANEL DIM"]),
    ("drawer_gnc", "plate", 0.20, 0.036, ["IMU 2 · STAR TRACKER ELECTRONICS", "ORU  AUR7-GN-2210   18 kg"]),
    ("drawer_av", "plate", 0.20, 0.036, ["FLIGHT COMPUTER B", "ORU  AUR7-AV-1040   ESD SENSITIVE"]),
    ("drawer_comm", "plate", 0.20, 0.036, ["X-BAND TRANSPONDER 1", "ORU  AUR7-CM-3105   RF HAZARD"]),
    ("esd", "amber", 0.10, 0.045, ["CAUTION", "ESD SENSITIVE", "GROUND STRAP REQD"]),
    ("rf_haz", "amber", 0.10, 0.045, ["CAUTION", "RF RADIATION", "SAFE BEFORE DEMATE"]),
    ("j_row_a", "legend", 0.36, 0.022, ["J1", "J2", "J3", "J4", "J5", "J6"]),
    ("j_row_b", "legend", 0.36, 0.022, ["P1 PWR", "P2 DATA", "P3 1553", "P4 VID", "P5 AUD", "P6 GND"]),
    ("cap_tether", "white", 0.07, 0.018, ["REPLACE DUST CAPS"]),
    ("pn_a", "barcode", 0.09, 0.03, ["AUR7-CN-0450-03"]),
    ("pn_b", "barcode", 0.09, 0.03, ["AUR7-CN-0450-07"]),
    ("pn_c", "barcode", 0.09, 0.03, ["AUR7-HB-0220-11"]),
    ("pn_d", "barcode", 0.09, 0.03, ["AUR7-EC-0610-02"]),
    ("handhold", "stencil_dark", 0.10, 0.016, ["HANDHOLD"]),
    ("velcro_hook", "velcro_hook", 0.05, 0.05, []),
    ("velcro_loop", "velcro_loop", 0.05, 0.05, []),
    ("velcro_strip", "velcro_loop", 0.16, 0.025, []),
    ("clock_frame", "black", 0.10, 0.014, ["MISSION ELAPSED TIME"]),
    ("notebook", "note", 0.14, 0.20, ["FLIGHT LOG", "", "Burn 1 nominal.", "HGA gimbal noisy at", "low elevation - watch.",
                                        "Seedlings in tray 2.", "Scrubber B swap due.", "", "Call home takes a while", "now. Be patient."]),
    # ---- grow unit
    ("grow_title", "plate", 0.30, 0.05, ["VEG-2  PLANT GROWTH UNIT", "ROOT MODULE · LED LIGHT CAP · CONDENSATE RETURN"]),
    ("grow_water", "cyan", 0.11, 0.034, ["WATER FILL", "POTABLE ONLY"]),
    ("grow_no_instant", "white", 0.16, 0.034, ["CROP TIME IS REAL TIME", "DO NOT HARVEST BEFORE READY"]),
    ("grow_light", "amber", 0.11, 0.045, ["CAUTION", "BRIGHT LED", "DO NOT STARE"]),
    ("grow_tray1", "black", 0.07, 0.016, ["ROOT MAT  A"]),
    ("grow_tray2", "black", 0.07, 0.016, ["ROOT MAT  B"]),
    ("grow_bellows", "stencil_dark", 0.12, 0.016, ["BELLOWS  ·  LIFT TO OPEN"]),
    ("grow_fan", "stencil_dark", 0.08, 0.016, ["AIRFLOW  >>"]),
    ("grow_nutrient", "white", 0.085, 0.030, ["NUTRIENT", "SOLUTION  B"]),
    # ---- berth
    ("berth_title", "plate", 0.24, 0.045, ["CREW QUARTERS 1", "SLEEP RESTRAINT · PERSONAL STOWAGE"]),
    ("berth_vent", "stencil_dark", 0.12, 0.016, ["KEEP VENT CLEAR"]),
    ("berth_light", "black", 0.06, 0.016, ["READING LT"]),
    ("berth_name", "tape", 0.10, 0.026, ["MIRA  ·  BUNK 1"]),
    ("berth_bag", "white", 0.12, 0.030, ["SLEEP RESTRAINT", "SIZE M · AUR7-CQ-0040"]),
    ("berth_strap", "stencil_light", 0.10, 0.014, ["CINCH  >>"]),
    ("berth_egress", "red", 0.14, 0.036, ["EMERG EGRESS", "PULL PANEL"]),
    ("berth_photo", "photo", 0.10, 0.075, []),
    ("berth_sched", "card", 0.105, 0.150, ["CREW DAY PLAN", "06:00  WAKE · HYGIENE", "07:00  BREAKFAST", "08:00  SYSTEMS WALK",
                                             "10:00  MAINTENANCE", "12:30  MEAL", "14:00  EXERCISE", "16:00  COMM WINDOW",
                                             "18:30  MEAL", "20:00  LOG · PLANTS", "21:30  SLEEP", "8 h REST = CLEAR HEAD"]),
    # ---- airlock panel
    ("alk_title", "plate", 0.30, 0.05, ["AIRLOCK CONTROL PANEL", "DEPRESS · REPRESS · HATCH INTERLOCK"]),
    ("alk_leg", "legend", 0.34, 0.030, ["INNER", "OUTER", "EQUAL", "PUMP", "VENT"]),
    ("alk_depress", "red", 0.13, 0.040, ["DEPRESS", "SUIT CHECK FIRST"]),
    ("alk_repress", "cyan", 0.13, 0.040, ["REPRESS", "HATCH CLOSED"]),
    ("alk_warn", "amber", 0.20, 0.07, ["CAUTION", "VERIFY SUIT PRESSURE AND TETHER", "BEFORE OPENING OUTER HATCH"]),
    ("alk_steps", "card", 0.105, 0.150, ["EVA EGRESS", "1  SUIT · SEALS CHECK", "2  TETHER TO HARD POINT", "3  INNER HATCH CLOSE",
                                           "4  DEPRESS · WAIT ZERO", "5  OUTER HATCH OPEN", "", "EVA INGRESS", "1  OUTER HATCH CLOSE",
                                           "2  REPRESS · WAIT EQUAL", "3  INNER HATCH OPEN", "NEVER RUSH THE GAUGE"]),
    ("alk_equal", "black", 0.07, 0.016, ["EQUALISE VALVE"]),
    ("alk_gauge", "gauge", 0.11, 0.11, []),
    ("alk_o2", "cyan", 0.075, 0.032, ["O2", "RECHARGE"]),
    # ---- ECLSS rack
    ("ecl_title", "plate", 0.34, 0.05, ["ARS-1  AIR REVITALISATION", "CO2 SORBENT BEDS · TRACE CONTAMINANT · FAN PACK"]),
    ("ecl_flow", "stencil_dark", 0.14, 0.018, ["CABIN AIR  >  BEDS  >  RETURN"]),
    ("ecl_bed", "white", 0.10, 0.030, ["SORBENT BED", "SWAP WHEN INDICATOR RED"]),
    ("ecl_hot", "amber", 0.10, 0.045, ["CAUTION", "HOT SURFACE", "DURING REGEN"]),
    ("ecl_valve", "black", 0.07, 0.016, ["ISOL VALVE  V-12"]),
    ("ecl_fan", "black", 0.07, 0.016, ["FAN PACK  A / B"]),
    ("ecl_filter", "plate", 0.22, 0.036, ["HEPA / CHARCOAL FILTER DRAWER", "PULL TO SERVICE · AUR7-EC-0610"]),
    ("ecl_sample", "cyan", 0.075, 0.032, ["AIR SAMPLE", "PORT"]),
    ("ecl_leg", "legend", 0.30, 0.030, ["BED A", "BED B", "BED C", "FAN", "HTR"]),
    # ---- galley: water and food
    ("wat_title", "plate", 0.24, 0.045, ["POTABLE WATER DISPENSER", "AMBIENT · HOT · METERED FILL"]),
    ("wat_hot", "red", 0.065, 0.026, ["HOT"]),
    ("wat_amb", "cyan", 0.065, 0.026, ["AMBIENT"]),
    ("wat_steps", "white", 0.13, 0.05, ["1  SEAT POUCH ON NEEDLE", "2  SELECT VOLUME", "3  PRESS AND HOLD FILL"]),
    ("wat_iodine", "amber", 0.10, 0.045, ["CAUTION", "HOT WATER", "82 °C"]),
    ("wat_tank", "stencil_dark", 0.12, 0.016, ["TANK  2  OF  2"]),
    ("food_title", "plate", 0.24, 0.045, ["FOOD STOWAGE", "RATIONS BY MEAL · LOG EVERY POUCH"]),
    ("food_a", "white", 0.10, 0.030, ["MEALS  A", "REHYDRATABLE"]),
    ("food_b", "white", 0.10, 0.030, ["MEALS  B", "THERMOSTABILISED"]),
    ("food_c", "white", 0.10, 0.030, ["SNACKS", "DRINK POWDER"]),
    ("food_count", "tape", 0.11, 0.024, ["COUNT WHAT YOU EAT"]),
    ("food_net", "stencil_dark", 0.10, 0.014, ["RESTRAINT NET"]),
    ("trash", "black", 0.07, 0.016, ["WET TRASH"]),
    ("util_arrow", "arrow", 0.05, 0.025, []),
    ("fire_port", "red", 0.07, 0.07, ["FIRE", "PORT"]),
]


def rounded_mask(w, h, r):
    m = Image.new("L", (w, h), 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, w - 1, h - 1), radius=r, fill=255)
    return m


def fit_font(path, text, max_w, max_h, start):
    size = start
    while size > 6:
        f = font(path, size)
        box = f.getbbox(text)
        if box[2] - box[0] <= max_w and box[3] - box[1] <= max_h:
            return f
        size -= 1
    return font(path, 6)


def centred(d, text, f, cx, cy, fill):
    box = d.textbbox((0, 0), text, font=f)
    d.text((cx - (box[2] + box[0]) / 2.0, cy - (box[3] + box[1]) / 2.0), text, font=f, fill=fill)


def grime(img, seed, amount=10):
    a = np.asarray(img, dtype=np.float64)
    h, w = a.shape[:2]
    rng = np.random.default_rng(seed)
    n = rng.normal(0, amount * 0.35, (h, w, 1))
    yy, xx = np.mgrid[0:h, 0:w]
    edge = np.minimum.reduce([xx, yy, w - 1 - xx, h - 1 - yy]).astype(np.float64)
    shade = np.clip(edge / (0.06 * max(w, h) + 1), 0, 1)[..., None]
    a[..., :3] = a[..., :3] * (0.90 + 0.10 * shade) + n
    return Image.fromarray(a.clip(0, 255).astype(np.uint8), "RGBA")


def draw_lines(d, lines, f_head, f_body, w, h, ink, pad, head_fill=None):
    """First line is the heading; remaining lines share the rest of the height."""
    if not lines:
        return
    rows = len(lines)
    if rows == 1:
        f = fit_font(f_head, lines[0], w - 2 * pad, h - 2 * pad, h * 0.62)
        centred(d, lines[0], f, w / 2, h / 2, head_fill or ink)
        return
    head_h = (h - 2 * pad) * (0.56 if rows == 2 else 0.40)
    f = fit_font(f_head, lines[0], w - 2 * pad, head_h, head_h * 0.95)
    centred(d, lines[0], f, w / 2, pad + head_h / 2, head_fill or ink)
    rest = lines[1:]
    row_h = (h - 2 * pad - head_h) / len(rest)
    longest = max(rest, key=len)
    fb = fit_font(f_body, longest, w - 2 * pad, row_h * 0.86, row_h * 0.80)
    for i, text in enumerate(rest):
        centred(d, text, fb, w / 2, pad + head_h + row_h * (i + 0.5), ink)


def make_label(key, style, wm, hm, lines, seed):
    w, h = int(round(wm * LABEL_PPM)), int(round(hm * LABEL_PPM))
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    r = int(0.003 * LABEL_PPM)
    pad = int(0.0028 * LABEL_PPM)
    lw = max(2, int(0.0006 * LABEL_PPM))
    if style in ("plate", "white", "black", "red", "cyan", "amber", "barcode", "tape", "card", "note", "photo", "gauge"):
        bg = {"plate": (214, 211, 202), "white": WHITE, "black": (26, 28, 31), "red": RED, "cyan": (228, 232, 230),
              "amber": AMBER, "barcode": WHITE, "tape": (226, 214, 176), "card": (240, 236, 222), "note": (238, 232, 208),
              "photo": (238, 236, 230), "gauge": (232, 230, 222)}[style]
        d.rounded_rectangle((0, 0, w - 1, h - 1), radius=0 if style in ("tape", "card", "note", "photo") else r, fill=bg + (255,))
    ink = INK
    if style == "plate":
        d.rounded_rectangle((pad * 0.45, pad * 0.45, w - 1 - pad * 0.45, h - 1 - pad * 0.45), radius=r, outline=INK + (255,), width=lw)
        draw_lines(d, lines, BOLD, SEMI, w, h, ink, pad * 1.6)
    elif style == "white":
        draw_lines(d, lines, BOLD, SEMI, w, h, ink, pad)
    elif style == "black":
        draw_lines(d, lines, BOLD, SEMI, w, h, (238, 236, 228), pad)
    elif style == "red":
        d.rounded_rectangle((pad * 0.4, pad * 0.4, w - 1 - pad * 0.4, h - 1 - pad * 0.4), radius=r, outline=(245, 240, 232, 255), width=lw)
        draw_lines(d, lines, BOLD, SEMI, w, h, (248, 244, 236), pad * 1.5)
    elif style == "cyan":
        band = int(h * 0.48) if len(lines) > 1 else h
        d.rounded_rectangle((0, 0, w - 1, band), radius=r, fill=CYAN + (255,))
        if len(lines) > 1:
            d.rectangle((0, band - r, w - 1, band), fill=CYAN + (255,))
            f = fit_font(BOLD, lines[0], w - 2 * pad, band - pad, band * 0.86)
            centred(d, lines[0], f, w / 2, band / 2, INK)
            f = fit_font(SEMI, lines[1], w - 2 * pad, h - band - pad, (h - band) * 0.66)
            centred(d, lines[1], f, w / 2, band + (h - band) / 2, INK)
        else:
            draw_lines(d, lines, BOLD, SEMI, w, h, INK, pad)
    elif style == "amber":
        if len(lines) > 1:
            band = int(h * 0.34)
            d.rounded_rectangle((0, 0, w - 1, band), radius=r, fill=INK + (255,))
            d.rectangle((0, band - r, w - 1, band), fill=INK + (255,))
            f = fit_font(BOLD, lines[0], w - 2 * pad, band - pad * 0.6, band * 0.9)
            centred(d, lines[0], f, w / 2, band / 2, AMBER)
            rest = lines[1:]
            row_h = (h - band - pad) / len(rest)
            fb = fit_font(BOLD, max(rest, key=len), w - 2 * pad, row_h * 0.9, row_h * 0.86)
            for i, text in enumerate(rest):
                centred(d, text, fb, w / 2, band + pad * 0.3 + row_h * (i + 0.5), INK)
        else:
            draw_lines(d, lines, BOLD, SEMI, w, h, INK, pad)
    elif style in ("stencil_light", "stencil_dark"):
        fill = (232, 230, 222) if style == "stencil_light" else (30, 32, 36)
        rows = len(lines)
        row_h = h / rows
        f = fit_font(BOLD, max(lines, key=len), w - 4, row_h * 0.92, row_h * 0.9)
        for i, text in enumerate(lines):
            centred(d, text, f, w / 2, row_h * (i + 0.5), fill + (255,))
    elif style == "legend":
        cells = len(lines)
        cw = w / cells
        f = fit_font(BOLD, max(lines, key=len), cw - pad, h * 0.62, h * 0.6)
        for i, text in enumerate(lines):
            cx = cw * (i + 0.5)
            centred(d, text, f, cx, h * 0.40, (232, 230, 222, 255))
            y = h * 0.86
            d.line((cx - cw * 0.36, y, cx + cw * 0.36, y), fill=(232, 230, 222, 255), width=lw)
            d.line((cx - cw * 0.36, y, cx - cw * 0.36, y - h * 0.12), fill=(232, 230, 222, 255), width=lw)
            d.line((cx + cw * 0.36, y, cx + cw * 0.36, y - h * 0.12), fill=(232, 230, 222, 255), width=lw)
    elif style == "hazard":
        d.rectangle((0, 0, w - 1, h - 1), fill=AMBER + (255,))
        stripe = max(8, int(0.011 * LABEL_PPM))
        for x in range(-h, w + h, stripe * 2):
            d.polygon([(x, h), (x + stripe, h), (x + stripe + h, 0), (x + h, 0)], fill=INK + (255,))
    elif style in ("velcro_hook", "velcro_loop"):
        rng = np.random.default_rng(seed)
        base = (176, 172, 160) if style == "velcro_hook" else (70, 72, 74)
        n = rng.normal(0, 16 if style == "velcro_hook" else 11, (h, w, 1))
        fz = np.asarray(Image.fromarray(rng.integers(0, 255, (h, w), dtype=np.uint8)).filter(ImageFilter.GaussianBlur(1.2)), dtype=np.float64)[..., None]
        a = np.asarray(base, dtype=np.float64)[None, None, :] + n + (fz - 128) * 0.35
        rgba = np.concatenate([a, np.full((h, w, 1), 255.0)], axis=-1).clip(0, 255).astype(np.uint8)
        img = Image.fromarray(rgba, "RGBA")
        d = ImageDraw.Draw(img)
        for i in range(2):   # stitched border
            inset = int(0.0022 * LABEL_PPM)
            for x in range(inset, w - inset, int(0.0030 * LABEL_PPM)):
                d.line((x, inset, x + int(0.0018 * LABEL_PPM), inset), fill=(40, 40, 42, 255), width=lw)
                d.line((x, h - inset, x + int(0.0018 * LABEL_PPM), h - inset), fill=(40, 40, 42, 255), width=lw)
            for y in range(inset, h - inset, int(0.0030 * LABEL_PPM)):
                d.line((inset, y, inset, y + int(0.0018 * LABEL_PPM)), fill=(40, 40, 42, 255), width=lw)
                d.line((w - inset, y, w - inset, y + int(0.0018 * LABEL_PPM)), fill=(40, 40, 42, 255), width=lw)
        return img
    elif style == "barcode":
        rng = np.random.default_rng(seed)
        x = pad
        bar_h = int(h * 0.52)
        while x < w - pad:
            bw = int(rng.integers(2, 7))
            if rng.random() < 0.55:
                d.rectangle((x, pad, min(w - pad, x + bw), pad + bar_h), fill=INK + (255,))
            x += bw
        f = fit_font(SEMI, lines[0], w - 2 * pad, h - bar_h - 2 * pad, h * 0.3)
        centred(d, lines[0], f, w / 2, pad + bar_h + (h - bar_h - pad) / 2, INK)
    elif style == "tape":
        f = fit_font(BOLD, lines[0], w - 3 * pad, h - pad, h * 0.62)
        centred(d, lines[0], f, w / 2, h / 2, (34, 46, 120, 255))
        for x in range(0, w, 5):   # torn ends
            cut = int(RNG.integers(0, int(0.0016 * LABEL_PPM)))
            d.rectangle((0, x % h, cut, x % h + 5), fill=(0, 0, 0, 0))
    elif style in ("card", "note"):
        d.rectangle((0, 0, w - 1, h - 1), outline=(150, 146, 132, 255), width=lw)
        rows = len(lines)
        top = int(h * 0.06)
        row_h = (h - top - pad) / rows
        if style == "card":
            d.rectangle((0, 0, w - 1, int(top + row_h * 1.05)), fill=GRAPHITE + (255,))
            # spiral binding holes along the top edge
            for x in range(int(w * 0.06), int(w * 0.95), int(w * 0.075)):
                d.ellipse((x, 2, x + int(w * 0.028), 2 + int(w * 0.028)), fill=(0, 0, 0, 0))
        else:
            for i in range(1, rows + 1):
                yy = top + row_h * i
                d.line((pad, yy, w - pad, yy), fill=(150, 170, 200, 255), width=1)
            d.line((int(w * 0.14), 0, int(w * 0.14), h), fill=(200, 110, 110, 255), width=2)
        longest = max(lines, key=len)
        fb = fit_font(SEMI if style == "card" else REG, longest, w - 2.4 * pad - (w * 0.14 if style == "note" else 0), row_h * 0.9, row_h * 0.78)
        fh = fit_font(BOLD, lines[0], w - 2.4 * pad, row_h * 0.95, row_h * 0.86)
        for i, text in enumerate(lines):
            y = top + row_h * (i + 0.5)
            if i == 0 and style == "card":
                centred(d, text, fh, w / 2, y, (236, 232, 220, 255))
            else:
                box = d.textbbox((0, 0), text, font=fb)
                x0 = pad * 1.2 + (w * 0.15 if style == "note" else 0)
                d.text((x0, y - (box[3] + box[1]) / 2.0), text, font=fb, fill=(34, 46, 120, 255) if style == "note" else INK)
    elif style == "photo":
        # a small printed picture of home: sky, hills, a house - flat original shapes
        inner = (pad, pad, w - pad, h - int(pad * 2.4))
        d.rectangle(inner, fill=(126, 178, 214, 255))
        hy = int(h * 0.56)
        d.polygon([(inner[0], hy), (int(w * 0.3), int(h * 0.36)), (int(w * 0.55), hy), (int(w * 0.78), int(h * 0.42)), (inner[2], hy),
                   (inner[2], inner[3]), (inner[0], inner[3])], fill=(86, 132, 84, 255))
        d.rectangle((int(w * 0.40), int(h * 0.50), int(w * 0.56), int(h * 0.66)), fill=(222, 206, 180, 255))
        d.polygon([(int(w * 0.38), int(h * 0.50)), (int(w * 0.48), int(h * 0.40)), (int(w * 0.58), int(h * 0.50))], fill=(160, 70, 54, 255))
        d.ellipse((int(w * 0.72), int(h * 0.14), int(w * 0.80), int(h * 0.14) + int(w * 0.08)), fill=(250, 232, 170, 255))
    elif style == "gauge":
        cx, cy, rad = w / 2, h / 2, w * 0.46
        d.ellipse((cx - rad, cy - rad, cx + rad, cy + rad), fill=(238, 236, 228, 255), outline=INK + (255,), width=lw * 2)
        for i in range(0, 41):
            a = math.radians(225 - i * 270 / 40)
            r0 = rad * (0.80 if i % 5 == 0 else 0.87)
            d.line((cx + math.cos(a) * r0, cy - math.sin(a) * r0, cx + math.cos(a) * rad * 0.94, cy - math.sin(a) * rad * 0.94),
                   fill=INK + (255,), width=lw * (2 if i % 5 == 0 else 1))
        d.arc((cx - rad * 0.74, cy - rad * 0.74, cx + rad * 0.74, cy + rad * 0.74), 135, 190, fill=RED + (255,), width=int(rad * 0.07))
        d.arc((cx - rad * 0.74, cy - rad * 0.74, cx + rad * 0.74, cy + rad * 0.74), 350, 405, fill=(60, 150, 90, 255), width=int(rad * 0.07))
        f = font(BOLD, rad * 0.17)
        centred(d, "CHAMBER", f, cx, cy + rad * 0.34, INK)
        centred(d, "PRESSURE", f, cx, cy + rad * 0.52, INK)
    elif style == "arrow":
        d.polygon([(0, h * 0.3), (w * 0.6, h * 0.3), (w * 0.6, 0), (w - 1, h / 2), (w * 0.6, h - 1), (w * 0.6, h * 0.7), (0, h * 0.7)],
                  fill=AMBER + (255,))
        return img
    if style in ("plate", "white", "black", "red", "cyan", "amber", "barcode", "card", "note", "photo", "gauge", "tape"):
        img = grime(img, seed)
    return img


def build_labels():
    atlas = Image.new("RGBA", (ATLAS, ATLAS), (0, 0, 0, 0))
    entries = {}
    margin = 10
    x = y = margin
    row_h = 0
    order = sorted(range(len(LABELS)), key=lambda i: -LABELS[i][3])
    for n, index in enumerate(order):
        key, style, wm, hm, lines = LABELS[index]
        img = make_label(key, style, wm, hm, lines, 100 + index)
        w, h = img.size
        if x + w + margin > ATLAS:
            x = margin
            y += row_h + margin
            row_h = 0
        if y + h + margin > ATLAS:
            raise RuntimeError("label atlas full at " + key)
        atlas.paste(img, (x, y))
        entries[key] = {"u0": x / ATLAS, "u1": (x + w) / ATLAS, "v0": 1.0 - (y + h) / ATLAS, "v1": 1.0 - y / ATLAS, "w": wm, "h": hm}
        x += w + margin
        row_h = max(row_h, h)
    # bleed colour into transparent texels so mip-mapped cutouts do not fringe dark
    a = np.asarray(atlas).copy()
    rgb = Image.fromarray(a[..., :3], "RGB")
    alpha = a[..., 3]
    spread = np.asarray(rgb.filter(ImageFilter.MaxFilter(9)))
    a[..., :3] = np.where(alpha[..., None] > 0, a[..., :3], spread)
    Image.fromarray(a, "RGBA").save(os.path.join(OUT, "IH_Labels.png"), optimize=True)
    with open(os.path.join(OUT, "ih_labels.json"), "w") as f:
        json.dump({"size": ATLAS, "pixels_per_metre": LABEL_PPM, "entries": entries}, f, indent=1)
    print("labels", len(entries), "used rows to y =", y + row_h)


# ----------------------------------------------------------------------------- display atlas (emissive)
SCREEN_W, SCREEN_H = 4096, 2048
BG = (5, 14, 20)
LINE = (70, 212, 226)
DIM = (34, 100, 112)
WARM = (240, 170, 52)
SOFT = (170, 214, 220)

# key, x, y, w, h in pixels
SCREENS = {
    "mfd_flight": (0, 0, 1280, 800), "mfd_systems": (1300, 0, 1280, 800), "mfd_comm": (2600, 0, 1280, 800),
    "ann_left": (0, 820, 420, 1160), "ann_right": (440, 820, 420, 1160),
    "grow_status": (880, 820, 640, 400), "airlock_status": (1540, 820, 640, 400), "eclss_status": (2200, 820, 640, 400),
    "water_status": (2860, 820, 480, 300), "berth_status": (3360, 820, 480, 300),
    "clock": (880, 1240, 640, 160), "keys_lit": (1540, 1240, 640, 220), "kbd": (2200, 1240, 1216, 480),
    "led_strip": (880, 1420, 640, 60),
}


def frame_page(d, x, y, w, h, title, tag):
    d.rectangle((x, y, x + w - 1, y + h - 1), fill=BG)
    d.rectangle((x + 8, y + 8, x + w - 9, y + h - 9), outline=DIM, width=2)
    d.rectangle((x + 8, y + 8, x + w - 9, y + 8 + int(h * 0.085)), fill=(10, 40, 48))
    f = font(BOLD, h * 0.06)
    d.text((x + 24, y + 10), title, font=f, fill=LINE)
    box = d.textbbox((0, 0), tag, font=f)
    d.text((x + w - 24 - box[2], y + 10), tag, font=f, fill=WARM)


def grid(d, box, nx, ny):
    x0, y0, x1, y1 = box
    d.rectangle(box, outline=DIM, width=2)
    for i in range(1, nx):
        xx = x0 + (x1 - x0) * i / nx
        d.line((xx, y0, xx, y1), fill=(14, 46, 54), width=1)
    for i in range(1, ny):
        yy = y0 + (y1 - y0) * i / ny
        d.line((x0, yy, x1, yy), fill=(14, 46, 54), width=1)


def page_flight(d, x, y, w, h):
    frame_page(d, x, y, w, h, "GNC  ·  TRAJECTORY", "NAV INERTIAL")
    box = (x + 40, y + 110, x + int(w * 0.62), y + h - 50)
    grid(d, box, 8, 6)
    cx, cy = (box[0] + box[2]) / 2, (box[1] + box[3]) / 2
    # two heliocentric arcs and the transfer between them (schematic, no scale)
    for rad, col in ((0.26, DIM), (0.42, DIM)):
        r = (box[2] - box[0]) * rad
        d.ellipse((cx - r, cy - r * 0.72, cx + r, cy + r * 0.72), outline=col, width=3)
    pts = []
    for i in range(0, 60):
        a = math.radians(200 - i * 2.6)
        r = (box[2] - box[0]) * (0.26 + 0.16 * i / 59.0)
        pts.append((cx + math.cos(a) * r, cy - math.sin(a) * r * 0.72))
    d.line(pts, fill=WARM, width=5)
    px, py = pts[34]
    d.polygon([(px, py - 14), (px + 12, py + 10), (px - 12, py + 10)], fill=LINE)
    d.ellipse((cx - 10, cy - 10, cx + 10, cy + 10), fill=WARM)
    f = font(SEMI, h * 0.036)
    d.text((px + 18, py - 16), "AURORA-7", font=f, fill=LINE)
    d.text((pts[0][0] - 60, pts[0][1] + 12), "DEPART", font=f, fill=SOFT)
    d.text((pts[-1][0] + 10, pts[-1][1] - 30), "ARRIVE", font=f, fill=SOFT)
    # attitude ball
    bx, by, br = x + int(w * 0.81), y + int(h * 0.36), int(h * 0.20)
    d.ellipse((bx - br, by - br, bx + br, by + br), outline=LINE, width=4)
    d.chord((bx - br, by - br, bx + br, by + br), 10, 170, fill=(12, 58, 70))
    d.line((bx - br * 1.15, by, bx - br * 0.4, by), fill=WARM, width=5)
    d.line((bx + br * 0.4, by, bx + br * 1.15, by), fill=WARM, width=5)
    d.ellipse((bx - 7, by - 7, bx + 7, by + 7), fill=WARM)
    for i in range(-2, 3):
        yy = by + i * br * 0.3
        d.line((bx - br * 0.25, yy, bx + br * 0.25, yy), fill=SOFT, width=2)
    f2 = font(BOLD, h * 0.045)
    for i, text in enumerate(("ATT REF   INERTIAL", "GUIDANCE  COAST", "RCS       SAFE", "STAR TRK  A + B")):
        d.text((x + int(w * 0.66), y + int(h * 0.64) + i * int(h * 0.062)), text, font=f2, fill=SOFT if i else LINE)


def page_systems(d, x, y, w, h):
    frame_page(d, x, y, w, h, "SYSTEMS  ·  POWER AND THERMAL", "SCHEMATIC")
    f = font(BOLD, h * 0.04)
    boxes = [("ARRAY A", 0.06, 0.20), ("ARRAY B", 0.06, 0.44), ("BATTERY", 0.06, 0.68), ("BUS A", 0.36, 0.26), ("BUS B", 0.36, 0.58),
             ("AVIONICS", 0.68, 0.18), ("ECLSS", 0.68, 0.40), ("COMM", 0.68, 0.62), ("THERMAL", 0.68, 0.84)]
    pos = {}
    for name, fx, fy in boxes:
        bx, by = x + int(w * fx), y + int(h * fy)
        bw, bh = int(w * 0.22), int(h * 0.12)
        d.rectangle((bx, by, bx + bw, by + bh), outline=LINE, width=3)
        centred(d, name, f, bx + bw / 2, by + bh / 2, SOFT)
        pos[name] = (bx, by, bw, bh)
    def link(a, b, col=LINE):
        ax, ay, aw, ah = pos[a]
        bx, by, bw, bh = pos[b]
        mx = (ax + aw + bx) / 2
        d.line((ax + aw, ay + ah / 2, mx, ay + ah / 2, mx, by + bh / 2, bx, by + bh / 2), fill=col, width=4)
    link("ARRAY A", "BUS A"); link("ARRAY B", "BUS A"); link("ARRAY B", "BUS B"); link("BATTERY", "BUS B", WARM)
    link("BUS A", "AVIONICS"); link("BUS A", "ECLSS"); link("BUS B", "COMM"); link("BUS B", "THERMAL")


def page_comm(d, x, y, w, h):
    frame_page(d, x, y, w, h, "COMM  ·  HIGH-GAIN LINK", "CARRIER")
    box = (x + 40, y + 110, x + int(w * 0.56), y + h - 50)
    grid(d, box, 10, 6)
    rng = np.random.default_rng(5)
    pts = []
    for i in range(0, 200):
        fx = i / 199.0
        peak = math.exp(-((fx - 0.5) / 0.035) ** 2) * 0.72 + math.exp(-((fx - 0.5) / 0.18) ** 2) * 0.16
        v = 0.10 + peak + rng.uniform(0, 0.05)
        pts.append((box[0] + (box[2] - box[0]) * fx, box[3] - (box[3] - box[1]) * v))
    d.line(pts, fill=LINE, width=3)
    f = font(SEMI, h * 0.034)
    d.text((box[0] + 12, box[1] + 8), "SPECTRUM  ·  X-BAND DOWNLINK", font=f, fill=SOFT)
    # light-time explainer: schematic only
    ox, oy = x + int(w * 0.62), y + int(h * 0.24)
    f2 = font(BOLD, h * 0.046)
    d.text((ox, oy - int(h * 0.08)), "SIGNAL PATH", font=f2, fill=LINE)
    d.ellipse((ox, oy + 30, ox + 44, oy + 74), outline=LINE, width=4)
    d.text((ox - 4, oy + 84), "SHIP", font=f, fill=SOFT)
    ex = x + w - 110
    d.ellipse((ex, oy + 22, ex + 60, oy + 82), fill=(18, 70, 110), outline=LINE, width=4)
    d.text((ex - 6, oy + 90), "EARTH", font=f, fill=SOFT)
    for i in range(7):
        sx = ox + 70 + i * (ex - ox - 90) / 7.0
        d.arc((sx, oy + 26, sx + 30, oy + 78), 300, 60, fill=WARM, width=4)
    for i, text in enumerate(("OUT   =  light-time", "BACK  =  light-time", "REPLY = OUT + BACK", "NO FASTER PATH")):
        d.text((ox, y + int(h * 0.58) + i * int(h * 0.066)), text, font=f2, fill=SOFT if i < 3 else WARM)


def page_ann(d, x, y, w, h, names, lit):
    d.rectangle((x, y, x + w - 1, y + h - 1), fill=(8, 9, 10))
    cols, rows = 2, len(names) // 2
    cw, ch = w / cols, h / rows
    for i, name in enumerate(names):
        f = fit_font(BOLD, max(name.split("|"), key=len), cw - 34, ch * 0.34, ch * 0.34)
        cx, cy = x + (i % cols) * cw, y + (i // cols) * ch
        on = lit.get(name)
        fill = {None: (20, 22, 24), "c": (22, 120, 132), "a": (176, 116, 26), "w": (150, 150, 140)}[on]
        d.rounded_rectangle((cx + 8, cy + 8, cx + cw - 8, cy + ch - 8), radius=10, fill=fill, outline=(46, 50, 54), width=3)
        parts = name.split("|")
        for j, part in enumerate(parts):
            centred(d, part, f, cx + cw / 2, cy + ch / 2 + (j - (len(parts) - 1) / 2.0) * ch * 0.34,
                    (236, 244, 244) if on else (74, 80, 84))


def page_small(d, x, y, w, h, title, tag, kind):
    frame_page(d, x, y, w, h, title, tag)
    f = font(BOLD, h * 0.075)
    if kind == "grow":
        # light cap, canopy and root module as a diagram; the authoritative crop state is drawn by the game beside it
        d.rectangle((x + 60, y + 90, x + w - 60, y + 110), fill=(190, 80, 200))
        for i in range(9):
            sx = x + 90 + i * (w - 180) / 8.0
            d.line((sx, y + 112, sx, y + 150), fill=(110, 50, 120), width=3)
        for i in range(6):
            lx = x + 110 + i * (w - 220) / 5.0
            d.ellipse((lx - 26, y + 210, lx + 26, y + 250), outline=(90, 200, 110), width=4)
            d.line((lx, y + 250, lx, y + 290), fill=(90, 200, 110), width=4)
        d.rectangle((x + 60, y + 290, x + w - 60, y + 330), outline=LINE, width=3)
        d.text((x + 70, y + h - 62), "LIGHT · WATER · AIR  —  SEE CROP STATE", font=font(SEMI, h * 0.055), fill=SOFT)
    elif kind == "airlock":
        cx = x + w / 2
        d.rectangle((cx - 110, y + 110, cx + 110, y + 290), outline=LINE, width=4)
        centred(d, "CHAMBER", f, cx, y + 200, SOFT)
        d.rectangle((x + 50, y + 150, cx - 110, y + 250), outline=DIM, width=3)
        centred(d, "CABIN", f, (x + 50 + cx - 110) / 2, y + 200, SOFT)
        d.rectangle((cx + 110, y + 150, x + w - 50, y + 250), outline=DIM, width=3)
        centred(d, "VACUUM", f, (cx + 110 + x + w - 50) / 2, y + 200, WARM)
        d.text((x + 50, y + h - 66), "READ THE GAUGE  ·  NO VALUE SHOWN HERE", font=font(SEMI, h * 0.05), fill=SOFT)
    elif kind == "eclss":
        names = ("CABIN", "FAN", "BED A", "BED B", "BED C", "RETURN")
        bw = (w - 100) / len(names)
        for i, name in enumerate(names):
            bx = x + 50 + i * bw
            d.rectangle((bx + 6, y + 150, bx + bw - 6, y + 250), outline=LINE if 1 < i < 5 else DIM, width=3)
            centred(d, name, font(BOLD, h * 0.058), bx + bw / 2, y + 200, SOFT)
            if i:
                d.polygon([(bx - 4, y + 200), (bx + 8, y + 190), (bx + 8, y + 210)], fill=WARM)
        d.text((x + 50, y + h - 66), "AIR LOOP SCHEMATIC  ·  SEE RACK INDICATORS", font=font(SEMI, h * 0.05), fill=SOFT)
    elif kind == "water":
        cx = x + w * 0.22
        d.polygon([(cx, y + 90), (cx + 46, y + 180), (cx - 46, y + 180)], fill=LINE)
        d.ellipse((cx - 46, y + 140, cx + 46, y + 232), fill=LINE)
        for i, text in enumerate(("POTABLE WATER", "SELECT · HOLD FILL")):
            d.text((x + w * 0.40, y + 110 + i * 62), text, font=font(BOLD, h * 0.11 if i == 0 else h * 0.085), fill=SOFT if i else LINE)
    elif kind == "berth":
        for i, text in enumerate(("CREW QUARTERS 1", "VENT ON  ·  LIGHT DIM")):
            d.text((x + 40, y + 100 + i * 70), text, font=font(BOLD, h * 0.12 if i == 0 else h * 0.09), fill=LINE if i == 0 else SOFT)


def build_screens():
    img = Image.new("RGB", (SCREEN_W, SCREEN_H), (0, 0, 0))
    d = ImageDraw.Draw(img)
    page_flight(d, *SCREENS["mfd_flight"])
    page_systems(d, *SCREENS["mfd_systems"])
    page_comm(d, *SCREENS["mfd_comm"])
    page_ann(d, *SCREENS["ann_left"], ["NAV|INRTL", "ATT|HOLD", "RCS|ARM", "RCS|SAFE", "GUID|AUTO", "GUID|MAN", "STAR|TRK A", "STAR|TRK B",
                                       "IMU|1", "IMU|2", "MAIN|ENG", "ABORT"],
             {"NAV|INRTL": "c", "ATT|HOLD": "c", "RCS|SAFE": "w", "GUID|AUTO": "c", "STAR|TRK A": "w", "IMU|1": "w"})
    page_ann(d, *SCREENS["ann_right"], ["HGA|TRACK", "HGA|STOW", "XPDR|1", "XPDR|2", "REC|ON", "DATA|DUMP", "CABIN|LT", "PANEL|LT",
                                        "FLOOD", "EXT|LT", "UPLINK", "DOWNLINK"],
             {"HGA|TRACK": "c", "XPDR|1": "w", "REC|ON": "a", "CABIN|LT": "w", "PANEL|LT": "w", "DOWNLINK": "c"})
    page_small(d, *SCREENS["grow_status"], "VEG-2  ·  GROWTH UNIT", "LIGHT CAP", "grow")
    page_small(d, *SCREENS["airlock_status"], "AIRLOCK  ·  CHAMBER", "INTERLOCK", "airlock")
    page_small(d, *SCREENS["eclss_status"], "ARS-1  ·  AIR LOOP", "FLOW", "eclss")
    page_small(d, *SCREENS["water_status"], "GALLEY", "H2O", "water")
    page_small(d, *SCREENS["berth_status"], "CQ-1", "REST", "berth")
    x, y, w, h = SCREENS["clock"]
    d.rectangle((x, y, x + w - 1, y + h - 1), fill=(6, 8, 8))
    centred(d, "MET   - - : - - : - -", font(BOLD, h * 0.62), x + w / 2, y + h / 2, WARM)
    x, y, w, h = SCREENS["keys_lit"]
    d.rectangle((x, y, x + w - 1, y + h - 1), fill=(8, 9, 10))
    names = ["EXEC", "CLR", "ENTR", "PROC", "MSG", "ACK", "PAGE", "MENU"]
    lit = {"ACK": WARM, "MENU": LINE}
    for i, name in enumerate(names):
        cx, cy = x + (i % 4) * w / 4, y + (i // 4) * h / 2
        col = lit.get(name, (30, 33, 36))
        d.rounded_rectangle((cx + 8, cy + 8, cx + w / 4 - 8, cy + h / 2 - 8), radius=10, fill=col, outline=(60, 64, 68), width=3)
        centred(d, name, font(BOLD, h * 0.2), cx + w / 8, cy + h / 4, (240, 240, 232) if name in lit else (120, 126, 130))
    # keyboard: dim legends on dark caps (back-lit)
    x, y, w, h = SCREENS["kbd"]
    d.rectangle((x, y, x + w - 1, y + h - 1), fill=(7, 8, 9))
    rows = ["1234567890-=<", "QWERTYUIOP[]\\", "ASDFGHJKL;'  ", "ZXCVBNM,./   "]
    kw, kh = w / 14.0, h / 5.0
    fk = font(BOLD, kh * 0.42)
    for r, row in enumerate(rows):
        for c, ch in enumerate(row):
            kx, ky = x + (c + 0.5 + r * 0.25) * kw, y + (r + 0.5) * kh
            d.rounded_rectangle((kx - kw * 0.44, ky - kh * 0.42, kx + kw * 0.44, ky + kh * 0.42), radius=8, fill=(24, 26, 29), outline=(50, 54, 58), width=2)
            if ch.strip():
                centred(d, ch, fk, kx, ky, (150, 200, 206))
    d.rounded_rectangle((x + kw * 3.2, y + kh * 4.1, x + kw * 10.2, y + kh * 4.9), radius=8, fill=(24, 26, 29), outline=(50, 54, 58), width=2)
    x, y, w, h = SCREENS["led_strip"]
    d.rectangle((x, y, x + w - 1, y + h - 1), fill=(255, 190, 235))
    img.save(os.path.join(OUT, "IH_Screens.png"), optimize=True)
    entries = {}
    for key, (x, y, w, h) in SCREENS.items():
        entries[key] = {"u0": (x + 2) / SCREEN_W, "u1": (x + w - 2) / SCREEN_W, "v0": 1.0 - (y + h - 2) / SCREEN_H, "v1": 1.0 - (y + 2) / SCREEN_H,
                        "w": w, "h": h}
    with open(os.path.join(OUT, "ih_screens.json"), "w") as f:
        json.dump({"size": [SCREEN_W, SCREEN_H], "entries": entries}, f, indent=1)
    print("screens", len(entries))


def main():
    os.makedirs(OUT, exist_ok=True)
    paint_set()
    chipped_set()
    metal_set()
    fabric_set()
    quilt_set()
    leaf_soil_set()
    build_labels()
    build_screens()


if __name__ == "__main__":
    main()
