"""Build the 7-slide OUT OF REACH Space Apps 2026 deck (PPTX).

Run from anywhere:  python docs/submission-draft-2026/deck/build_deck.py
Needs: python-pptx, Pillow, and the Git LFS captures listed in CAPTURES
(git lfs pull --include "docs/verification/**,docs/playtest-review/**").

Every picture in the deck is a real in-game capture: docs/verification (earlier builds) and deck/captures
(frames of the native QA runs on cf2224d and 022138d and of the R20 / R18 lane runs). No AI-generated image
is used, so no SI watermark is needed on the pictures; the footer still says the project is AI-assisted.
Swap a capture by editing CAPTURES (e.g. after the final build is recorded).

The deck has exactly 7 slides including the title (the Space Apps limit). Do not add a slide.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter
from pptx import Presentation
from pptx.dml.color import RGBColor
from pptx.enum.shapes import MSO_SHAPE
from pptx.enum.text import MSO_ANCHOR, PP_ALIGN
from pptx.oxml.ns import qn
from pptx.util import Emu, Inches, Pt

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
OUT = HERE / "OUT-OF-REACH_SpaceApps2026_deck.pptx"
TMP = HERE / ".build"  # ignored scratch for cropped JPEGs (see .gitignore)

# Switches for the final SHA (see form-fields.md "Switch sentences for the final SHA").
# The NASA radio countdown is in cf2224d (Audio/LaunchRadio, bound in MissionAudioPresenter.prefab).
COUNTDOWN_SHIPPED = True
# The routine montage with live station pictures (R18) is in feature/claude-max-final@5ea2c23.
# Set False only if it is missing from the submitted build (the plain routine card then shows the same ledger).
ROUTINE_MONTAGE_SHIPPED = True
# Floors, not exact counts: 470/151 on 8665183 (#7 6900862397), 483/151 and more on the later lanes.
# The slide says "more than"; raise the floors only if the final counts are far higher, lower them if they drop.
TESTS_EDITMODE, TESTS_PLAYMODE = 460, 150
# B-build extras (not in the release candidate 5479e87). Set True only if the feature is in the submitted
# build; apply_b_variant.py flips them together with the texts.
HORIZONS_LINE_SHIPPED = False   # Horizons range and one-way delay on the next-chapter strip (6b69313 + c158a10)
RAD_SHIPPED = False             # radiation estimate from NASA MSL RAD (119a083)
DSN_SHIPPED = False             # Deep Space Network complex in view, shown in the UI (8aea692 + a UI hook)
PARTICLE_PACK_SHIPPED = False   # launch effects built with the Unity Particle Pack (1b733ac)
# D build (feature/claude-max-final-b@f0ec271): launch picture with the new AURORA 7 launch vehicle and pad,
# a data-lines block on slide 4 (Horizons range and delay, DSN complex in view, MSL RAD dose), exact test counts,
# the 16 new Gemini radio lines on slide 7. make_b_build.py --deck-d sets OOR_DECK_D=1.
D_BUILD = False
TESTS_D = (542, 174)            # EditMode 542/542, PlayMode 174/174 on f0ec271 (#7 6902121785)

_DECK = "docs/submission-draft-2026/deck/captures/"
CAPTURES = {
    "title": "docs/verification/i06-land-bootstrap-native-v04/run/07-actual-aurora-return.png",
    "cargo": "docs/verification/i06-hud-cargo-20261115/00-cargo-placards.png",
    "launch": _DECK + "022138d-06-liftoff-tower-v03.png",           # native QA run on 022138d (tower V03)
    "eva": _DECK + "cf2224d-20-antenna-work.png",                   # native QA run on cf2224d
    "rescue": _DECK + "r20-08-docking-camera-hard-dock-stars.png",  # R42 ending with the R20 star field
    "epic": "docs/verification/i06-earth-terminal-native-v05/Evidence/01-actual-v2-focused.png",
    "death": "docs/verification/i06-player-feedback-20261115/death-verified-1280.png",
    "sources": "docs/verification/i06-player-feedback-20261115/sources-v03-native.png",
    "hud": _DECK + "r18-routine-montage-day4.png",                  # R18 routine montage, native run
    "meal": _DECK + "cf2224d-27-meal-in-progress.png",              # spare: ration pouch, native QA run on cf2224d
    "launch_d": _DECK + "f0ec271-06-liftoff-aurora7.png",           # native QA run on f0ec271 (D build): new launch vehicle and pad
}

# Palette: graphite / amber / cyan (+ red only for danger), matching the game's UI language.
BG = RGBColor(0x10, 0x13, 0x17)
PANEL = RGBColor(0x1A, 0x1F, 0x24)
LINE = RGBColor(0x2C, 0x33, 0x3A)
TXT = RGBColor(0xEE, 0xF0, 0xF2)
TXT2 = RGBColor(0xB4, 0xBC, 0xC4)
MUTED = RGBColor(0x75, 0x7F, 0x89)
AMBER = RGBColor(0xF2, 0xA3, 0x3A)
CYAN = RGBColor(0x4F, 0xD1, 0xE8)
RED = RGBColor(0xE5, 0x48, 0x4D)

HEAD = "Bahnschrift SemiBold Condensed"
HEAD_LIGHT = "Bahnschrift Light Condensed"
BODY = "Segoe UI"
BODY_SEMI = "Segoe UI Semibold"
BODY_LIGHT = "Segoe UI Light"

W, H = Inches(13.333), Inches(7.5)
TOTAL = 7


def capture(key: str) -> Path:
    path = ROOT / CAPTURES[key]
    with path.open("rb") as fh:
        head = fh.read(64)
    if head.startswith(b"version https://git-lfs"):
        sys.exit(f"{path} is a Git LFS pointer; run git lfs pull for it first.")
    return path


def cropped(key: str, w_in: float, h_in: float, focus=(0.5, 0.5), darken_left: float = 0.0,
            darken_all: float = 0.0, darken_bottom: float = 0.0) -> str:
    """Crop the capture to the frame aspect ratio around `focus`, optionally bake a left-to-right darkening."""
    TMP.mkdir(exist_ok=True)
    im = Image.open(capture(key)).convert("RGB")
    target = w_in / h_in
    iw, ih = im.size
    if iw / ih > target:
        nw = int(ih * target)
        x0 = int(min(max(focus[0] * iw - nw / 2, 0), iw - nw))
        im = im.crop((x0, 0, x0 + nw, ih))
    else:
        nh = int(iw / target)
        y0 = int(min(max(focus[1] * ih - nh / 2, 0), ih - nh))
        im = im.crop((0, y0, iw, y0 + nh))
    if darken_left or darken_all or darken_bottom:
        overlay = Image.new("L", im.size, 0)
        px = overlay.load()
        iw2, ih2 = im.size
        for x in range(iw2):
            t = x / max(iw2 - 1, 1)
            a_x = darken_all + darken_left * max(0.0, 1.0 - t / 0.85) ** 1.2
            for y in range(ih2):
                u = y / max(ih2 - 1, 1)
                a = a_x + darken_bottom * max(0.0, (u - 0.62) / 0.38) ** 1.3
                px[x, y] = int(255 * min(a, 0.97))
        overlay = overlay.filter(ImageFilter.GaussianBlur(6))
        black = Image.new("RGB", im.size, (11, 13, 16))
        im = Image.composite(black, im, overlay)
    out = TMP / f"{key}_{w_in:.2f}x{h_in:.2f}.jpg"
    im.save(out, quality=90, subsampling=0)
    return str(out)


def set_spacing(run, pts: float):
    run._r.get_or_add_rPr().set("spc", str(int(pts * 100)))


def text(slide, x, y, w, h, paragraphs, anchor=MSO_ANCHOR.TOP, wrap=True):
    """paragraphs: list of (runs, align, space_after_pt, line_spacing); runs: list of dicts."""
    box = slide.shapes.add_textbox(Inches(x), Inches(y), Inches(w), Inches(h))
    tf = box.text_frame
    tf.word_wrap = wrap
    tf.vertical_anchor = anchor
    for side in ("margin_left", "margin_right", "margin_top", "margin_bottom"):
        setattr(tf, side, 0)
    first = True
    for para in paragraphs:
        runs, align, after, spacing = (para + (None, None, None))[:4] if isinstance(para, tuple) else (para, None, None, None)
        p = tf.paragraphs[0] if first else tf.add_paragraph()
        first = False
        if align is not None:
            p.alignment = align
        if after is not None:
            p.space_after = Pt(after)
        if spacing is not None:
            p.line_spacing = spacing
        for r in runs:
            run = p.add_run()
            run.text = r["t"]
            f = run.font
            f.name = r.get("font", BODY)
            f.size = Pt(r.get("size", 14))
            f.bold = r.get("bold", False)
            f.color.rgb = r.get("color", TXT)
            if r.get("spc"):
                set_spacing(run, r["spc"])
    return box


UNITS = re.compile(r"(?<=\d)\s(?=(kg|h|s|min|%|kWh|W|L|m|km|credits|crew-days|seconds|hours|days|spare)\b)")


def nb(t: str) -> str:
    """Keep numbers and their units on one line (non-breaking space)."""
    return UNITS.sub("\u00a0", t)


def R(t, size=14, color=TXT, font=BODY, bold=False, spc=None):
    t = nb(t)
    d = {"t": t, "size": size, "color": color, "font": font, "bold": bold}
    if spc:
        d["spc"] = spc
    return d


def rect(slide, x, y, w, h, fill=None, line=None, line_w=0.75, shape=MSO_SHAPE.RECTANGLE):
    s = slide.shapes.add_shape(shape, Inches(x), Inches(y), Inches(w), Inches(h))
    if fill is None:
        s.fill.background()
    else:
        s.fill.solid()
        s.fill.fore_color.rgb = fill
    if line is None:
        s.line.fill.background()
    else:
        s.line.color.rgb = line
        s.line.width = Pt(line_w)
    s.shadow.inherit = False
    return s


def picture(slide, key, x, y, w, h, focus=(0.5, 0.5), tag="IN-GAME CAPTURE", darken_all=0.0):
    slide.shapes.add_picture(cropped(key, w, h, focus, darken_all=darken_all), Inches(x), Inches(y), Inches(w), Inches(h))
    rect(slide, x, y, w, h, line=LINE, line_w=1)
    if tag:
        tw = 0.24 + 0.078 * len(tag)
        rect(slide, x + w - tw - 0.08, y + 0.08, tw, 0.24, fill=RGBColor(0x0B, 0x0D, 0x10))
        text(slide, x + w - tw - 0.08, y + 0.08, tw, 0.24,
             [([R(tag, 7.5, CYAN, BODY_SEMI, spc=1)], PP_ALIGN.CENTER)], anchor=MSO_ANCHOR.MIDDLE, wrap=False)


def background(slide):
    rect(slide, 0, 0, 13.333, 7.5, fill=BG)


def chrome(slide, idx, kicker):
    rect(slide, 0.6, 0.47, 0.07, 0.3, fill=AMBER)
    text(slide, 0.78, 0.45, 8.0, 0.34, [[R(kicker, 11, AMBER, BODY_SEMI, spc=2.5)]], anchor=MSO_ANCHOR.MIDDLE)
    text(slide, 11.2, 0.45, 1.53, 0.34, [([R(f"{idx} / {TOTAL}", 11, MUTED, BODY_SEMI, spc=1)], PP_ALIGN.RIGHT)],
         anchor=MSO_ANCHOR.MIDDLE)
    text(slide, 0.6, 7.06, 8.2, 0.25,
         [[R("OUT OF REACH  ·  Team Polish Alex Goats  ·  NASA Space Apps Challenge 2026  ·  Space Mission Design Game",
             8.5, MUTED)]])
    text(slide, 8.4, 7.06, 4.33, 0.25,
         [([R("AI-assisted project (see slide 7)  ·  all pictures are real game captures", 8.5, MUTED)], PP_ALIGN.RIGHT)])


def heading(slide, t, y=0.98, size=34, w=12.1):
    text(slide, 0.6, y, w, 0.8, [[R(t, size, TXT, HEAD)]])


def slide_title(prs):
    s = prs.slides.add_slide(prs.slide_layouts[6])
    background(s)
    s.shapes.add_picture(cropped("title", 13.333, 7.5, (0.55, 0.5), darken_left=0.95, darken_all=0.3, darken_bottom=1.15), 0, 0, W, H)
    rect(s, 0.6, 1.02, 0.07, 0.3, fill=AMBER)
    text(s, 0.78, 1.0, 9, 0.34, [[R("NASA SPACE APPS CHALLENGE 2026  ·  SPACE MISSION DESIGN GAME", 12, AMBER, BODY_SEMI, spc=2.5)]],
         anchor=MSO_ANCHOR.MIDDLE)
    text(s, 0.55, 1.55, 9, 1.6, [[R("OUT OF REACH", 88, TXT, HEAD, spc=1)]])
    text(s, 0.62, 3.15, 10.5, 0.7, [([R("The mission you design is the mission you must survive.", 26, TXT, BODY_LIGHT)], None, 0, 1.05)])
    text(s, 0.62, 4.05, 7.6, 0.9,
         [([R("A first-person space-survival game for Windows. Pack a fictional Mars ship within real limits, "
              "then survive the accident your own choices shaped — with real NASA data deciding the odds.", 14, TXT2)],
           None, 0, 1.15)])
    rect(s, 0.62, 5.52, 0.5, 0.035, fill=CYAN)
    text(s, 0.62, 5.68, 8, 0.3, [[R("TEAM POLISH ALEX GOATS", 11, CYAN, BODY_SEMI, spc=2.5)]])
    text(s, 0.62, 6.0, 9, 0.4, [[R("Alexander Kozovy", 17, TXT, BODY_SEMI), R("  (Team Owner)", 13, TXT2),
                                  R("   ·   ", 17, MUTED), R("Alex", 17, TXT, BODY_SEMI), R(" (Apoliak)", 13, TXT2)]])
    text(s, 0.62, 7.0, 9, 0.3, [[R("Universal Event  ·  Unity 6  ·  Windows x64", 9, MUTED)]])
    tw = 2.55
    rect(s, 13.333 - tw - 0.35, 6.95, tw, 0.27, fill=RGBColor(0x0B, 0x0D, 0x10))
    text(s, 13.333 - tw - 0.35, 6.95, tw, 0.27, [([R("IN-GAME CAPTURE · AURORA-7 COCKPIT", 7.5, CYAN, BODY_SEMI, spc=1)], PP_ALIGN.CENTER)],
         anchor=MSO_ANCHOR.MIDDLE)


def slide_challenge(prs):
    s = prs.slides.add_slide(prs.slide_layouts[6])
    background(s)
    chrome(s, 2, "THE CHALLENGE  ·  OUR ANSWER")
    heading(s, "Mission trade-offs are easy to read and hard to feel.", w=12.2)
    text(s, 0.6, 1.78, 5.9, 1.1,
         [([R("The challenge asks for a game in which players design, manage and simulate a complete mission, "
              "make engineering decisions with limited resources and see how each choice shapes its success.", 14, TXT2)],
           None, 0, 1.12)])
    items = [
        ("1", "Pack the ship by hand.", "Carry every crate yourself: 600 kg payload, 800 credits, power budget, "
                                        "30 crew-days of supplies, 60 seconds."),
        ("2", "Live with it.", "A debris strike turns your manifest into your only toolbox."),
        ("3", "Learn why.", "A mission record ties each choice to the outcome, with sources."),
    ]
    y = 3.05
    for n, head, body in items:
        text(s, 0.6, y, 0.5, 0.6, [[R(n, 30, AMBER, HEAD)]])
        text(s, 1.12, y + 0.04, 5.3, 1.0,
             [([R(head + " ", 17, TXT, BODY_SEMI), R(body, 14, TXT2)], None, 0, 1.1)])
        y += 1.12
    picture(s, "cargo", 6.85, 1.78, 5.88, 3.31, focus=(0.5, 0.45))
    cards = [
        (6.85, CYAN, "SCIENCE FOCUS", "546 kg · 720 credits", "Spectrometer aboard; 7 spare parts,",
         "exactly the 3 repairs · power out ~39 h after the strike"),
        (9.84, AMBER, "REDUNDANCY", "591 kg · 690 credits", "No science; 13 spare parts,",
         "more oxygen and battery · power lasts ~90 h"),
    ]
    for x, col, name, l1, l2, l3 in cards:
        rect(s, x, 5.3, 2.89, 1.5, fill=PANEL, line=col, line_w=1.25)
        text(s, x + 0.18, 5.4, 2.6, 1.35,
             [([R(name, 11, col, BODY_SEMI, spc=2)], None, 3),
              ([R(l1, 15, TXT, BODY_SEMI)], None, 2),
              ([R(l2, 10.5, TXT2)], None, 1),
              ([R(l3, 10.5, TXT2)], None, 0)])
    text(s, 0.6, 6.55, 6.0, 0.35, [[R("Two valid designs, two different missions — and no single right answer.", 12, CYAN, BODY_SEMI)]])


def slide_loop(prs):
    s = prs.slides.add_slide(prs.slide_layouts[6])
    background(s)
    chrome(s, 3, "HOW IT PLAYS")
    heading(s, "One mission, from the launch pad to day 30")
    steps = [
        ("PLAN", "Carry crates within the limits in 60 s"),
        ("LAUNCH", "Strap in; lift off from LC-7 to a real NASA countdown"),
        ("ACCIDENT", "Debris strike: power 20 %, life support and antenna down"),
        ("REPAIR", "Spend parts and kWh; a refused action costs nothing"),
        ("SPACEWALK", "Suit, airlock, tether rope, antenna; 3 h of suit oxygen"),
        ("CALL EARTH", "One distress call; the reply waits for light"),
        ("SURVIVE", "Days 1–30: meals, sleep, decisions, rescue window"),
        ("OUTCOME", "A rescue craft docks, or the loss names its cause; then the record"),
    ]
    x0, gap = 0.6, 0.09
    cw = (12.13 - gap * 7) / 8
    for i, (name, desc) in enumerate(steps):
        x = x0 + i * (cw + gap)
        col = RED if name == "ACCIDENT" else (CYAN if name in ("CALL EARTH", "OUTCOME") else AMBER)
        rect(s, x, 1.85, cw, 0.05, fill=col)
        text(s, x, 1.97, cw, 0.32, [[R(f"{i + 1}  ", 11, col, HEAD), R(name, 12.5, TXT, BODY_SEMI, spc=0.5)]])
        text(s, x, 2.34, cw - 0.05, 1.0, [([R(desc, 10.5, TXT2)], None, 0, 1.05)])
    shots = [(("launch_d", "Lift-off: the AURORA 7 launch vehicle leaves the pad", (0.5, 0.5)) if D_BUILD else
              ("launch", "Vertical launch from launch complex LC-7", (0.5, 0.5))),
             ("eva", "Spacewalk: the antenna repaired by hand", (0.5, 0.5)),
             ("rescue", "Day 30: the rescue craft hard-docks under NASA's Deep Star Maps sky", (0.5, 0.5))]
    pw = (12.13 - 2 * 0.2) / 3
    for i, (key, cap, focus) in enumerate(shots):
        x = 0.6 + i * (pw + 0.2)
        picture(s, key, x, 3.45, pw, 2.83, focus=focus)
        text(s, x, 6.36, pw, 0.4, [[R(cap, 11.5, TXT2)]])


def slide_data(prs):
    s = prs.slides.add_slide(prs.slide_layouts[6])
    background(s)
    chrome(s, 4, "NASA DATA INSIDE THE MECHANICS")
    heading(s, "Real NASA data decides whether you make it")
    blocks = [
        ("31 min 59 s", "earliest reply from Earth on mission day 1",
         "NASA/JPL Horizons: Earth is 197,707,976 km away → 10 min 59 s of light time each way, "
         "+10 min processing (game assumption). "
         + ("Still over 30 minutes on day 9. Range and delay stay on screen during play."
            if HORIZONS_LINE_SHIPPED else
            "Call on day 9 and it is still over 30 minutes. The day-14 message obeys the same delay.")),
        ("0.84 · 1.01 · 2.5 · 1.83 kg", "oxygen used · CO₂ produced · water · food per crew-day",
         "NASA Life Support Baseline Values (BVAD, NASA/TP-2015-218570/REV2) drive every reserve, "
         "the cabin CO₂ and the spacewalk costs."),
        ("8.5 h", "the longest sleep, strapped into the crew berth",
         "NASA crew practice for sleep and for ready-to-eat, rehydratable and thermostabilized food "
         "shapes the meals and the rest rules."),
    ]
    if D_BUILD:  # the data lines the D build keeps on screen replace the sleep block (the sleep fact moves to the panel)
        blocks[2] = ("1.8 mSv per day", "radiation estimate on screen, from NASA MSL RAD cruise data",
                     "During play the game keeps its mission data in view: Earth range and one-way signal delay from "
                     "NASA/JPL Horizons, the Deep Space Network complex that has Mars in view (geometry from the "
                     "same vectors, not the DSN schedule) and the estimated dose since day 1.")
    y = 1.85
    for big, label, body in blocks:
        text(s, 0.6, y, 6.2, 0.6, [[R(big, 30, CYAN, HEAD)]])
        text(s, 0.6, y + 0.58, 6.2, 0.3, [[R(label.upper(), 9.5, AMBER, BODY_SEMI, spc=1.2)]])
        text(s, 0.6, y + 0.88, 6.0, 0.7, [([R(body, 12, TXT2)], None, 0, 1.08)])
        y += 1.62
    picture(s, "epic", 7.05, 1.85, 5.68, 3.2, focus=(0.5, 0.5))
    text(s, 7.05, 5.13, 5.68, 0.62,
         [([R("Earth on the cockpit terminal is a real NASA EPIC / NOAA DSCOVR image, observed 11 Nov 2026 "
              "and shown with its age on the mission clock — never as a live feed.", 11, TXT2)], None, 0, 1.06)])
    rect(s, 7.05, 5.83, 5.68, 1.0, fill=PANEL)
    also = ("Space sky: NASA SVS Deep Star Maps (ESA Gaia DR2)  ·  Mars: NASA/JPL-Caltech Viking map  ·  "
            "Slovakia inset: NASA GIBS, MODIS Terra")
    if COUNTDOWN_SHIPPED:
        also += "  ·  Launch radio: NASA audio, Apollo 11 commentary"
    if D_BUILD:  # RAD and DSN have their own block on the left; the sleep fact moves here
        also += "  ·  Meals and sleep (up to 8.5 h): NASA crew practice  ·  Development: NASA GMAT star catalogue"
    else:
        if RAD_SHIPPED:
            also += "  ·  Radiation estimate: NASA MSL RAD cruise data"
        if DSN_SHIPPED:
            also += "  ·  DSN complex in view: NASA Deep Space Network sites"
        if not (RAD_SHIPPED and DSN_SHIPPED):  # keep the panel to four lines
            also += "  ·  Development: NASA GMAT star catalogue"
    text(s, 7.22, 5.89, 5.4, 0.9,
         [([R("ALSO FROM NASA  ", 9, AMBER, BODY_SEMI, spc=1.2)], None, 2),
          ([R(also, 10, TXT2)], None, 0, 1.04)])


def slide_learn(prs):
    s = prs.slides.add_slide(prs.slide_layouts[6])
    background(s)
    chrome(s, 5, "WHAT PLAYERS LEARN")
    heading(s, "Every choice has a price, and the game shows the bill")
    lessons = [
        ("Life support is several jobs.", "With the scrubbers down, cabin CO₂ reaches the ISS emergency level in about "
                                          "20.5 hours, long before the stored oxygen runs out."),
        ("Help is never instant.", "No answer can arrive faster than light, both ways. Your reserves drain while you wait."),
        ("Spacewalks cost.", "2 h 20 min of preparation, purge oxygen, airlock losses and a 3-hour suit supply. "
                             "A solo spacewalk is itself an emergency."),
        ("Time is a consumable.", ("Ordinary days pass as a montage" if ROUTINE_MONTAGE_SHIPPED
                                   else "Ordinary days pass in a compressed routine")
                                  + " that spends real food, water, oxygen and power, and stops at the first danger."),
    ]
    y = 1.9
    for head, body in lessons:
        rect(s, 0.6, y + 0.06, 0.05, 0.86, fill=AMBER)
        text(s, 0.82, y, 5.9, 1.05, [([R(head, 16, TXT, BODY_SEMI)], None, 2),
                                     ([R(body, 12.5, TXT2)], None, 0, 1.08)])
        y += 1.18
    picture(s, "death", 7.05, 1.9, 5.68, 2.25, focus=(0.5, 0.52))
    text(s, 7.05, 4.19, 5.68, 0.3, [[R("The outcome names its cause: “Carbon dioxide overwhelmed the cabin.”", 11, TXT2)]])
    picture(s, "sources", 7.05, 4.6, 5.68, 1.85, focus=(0.5, 0.35))
    text(s, 7.05, 6.5, 5.68, 0.45,
         [([R("The record separates NASA data, derived physics, fiction and time compression.", 11, TXT2)], None, 0, 1.05)])


def slide_built(prs):
    s = prs.slides.add_slide(prs.slide_layouts[6])
    background(s)
    chrome(s, 6, "BUILT AND VERIFIED  ·  TEAM")
    heading(s, "A real simulation under the cinematic surface")
    points = [
        ("One authority.", "One mission clock and one ledger in real units (kg, L, kWh, W, ppm, parts); "
                           "spending is all-or-nothing and the visuals only show committed state."),
        ("Data you can check.", "The NASA/JPL snapshot ships offline and is verified by SHA-256 at mission start."),
        ("Tested.", (f"EditMode {TESTS_D[0]}/{TESTS_D[0]} and PlayMode {TESTS_D[1]}/{TESTS_D[1]} tests pass on the "
                     "submitted build, plus native full-mission runs, native Unity captures and human playtests.")
                    if D_BUILD else
                    (f"More than {TESTS_EDITMODE} EditMode and {TESTS_PLAYMODE} PlayMode tests pass on the release "
                     "candidate, plus scripted full-mission runs, native Unity captures and human playtests.")),
        ("Honest fiction.", "AURORA-7, the rescue, prices and time compression are labelled. No NASA logos."),
    ]
    y = 1.86
    for head, body in points:
        text(s, 0.6, y, 6.35, 0.8, [([R(head + " ", 14, TXT, BODY_SEMI), R(body, 12, TXT2)], None, 0, 1.06)])
        y += 0.8
    picture(s, "hud", 7.3, 1.86, 5.43, 3.05, focus=(0.5, 0.5))
    text(s, 7.3, 4.97, 5.43, 0.45,
         [([R("Ordinary days pass over the ship's real ledger: the same numbers the simulation spends."
              if ROUTINE_MONTAGE_SHIPPED else
              "The routine card shows the ship's real ledger: the same numbers the simulation spends.",
              11, TXT2)], None, 0, 1.05)])
    rect(s, 0.6, 5.5, 12.13, 1.38, fill=PANEL)
    text(s, 0.82, 5.6, 6, 0.28, [[R("TEAM POLISH ALEX GOATS", 9.5, CYAN, BODY_SEMI, spc=2)]])
    members = [
        (0.82, "Alexander Kozovy", "  Team Owner",
         "Creative direction, visuals, UI, audio and integration — with OpenAI Codex"),
        (6.85, "Alex", "  Apoliak",
         "Gameplay and simulation, movement, survival chapters and tests — with Claude Code"),
    ]
    for x, name, role, body in members:
        text(s, x, 5.92, 5.75, 0.62, [([R(name, 14, TXT, BODY_SEMI), R(role, 11, AMBER, BODY_SEMI)], None, 1),
                                       ([R(body, 11, TXT2)], None, 0, 1.04)])
    text(s, 0.82, 6.56, 11.7, 0.26,
         [[R("Unity 6000.3.28f1 (URP) · C# · Blender + Python · NumPy/Pillow · FFmpeg · Git LFS · Windows x64", 9.5, MUTED)]])


def slide_next(prs):
    s = prs.slides.add_slide(prs.slide_layouts[6])
    background(s)
    chrome(s, 7, "WHAT'S NEXT  ·  CREDITS  ·  AI USE")
    heading(s, "From one mission to a classroom of mission designers", w=12.2)
    nexts = [
        "Team mode for up to four roles: flight, engineering, communications, life support",
        "New destinations and scenarios, with a launch-date picker that refreshes NASA/JPL Horizons",
        "Side-by-side comparison of mission designs, and teacher notes",
        "Spanish and French text (already on a development branch)",
    ]
    y = 1.95
    for t in nexts:
        rect(s, 0.6, y + 0.12, 0.13, 0.13, fill=CYAN)
        text(s, 0.92, y, 5.2, 0.9, [([R(t, 14.5, TXT)], None, 0, 1.08)])
        y += 0.98
    rect(s, 6.55, 1.88, 6.18, 4.95, fill=PANEL)
    nasa_media = ("NASA/JPL Horizons · NASA BVAD · NASA EPIC Team / NOAA DSCOVR · NASA GIBS / ESDIS (MODIS Terra) · "
                  "NASA/GSFC SVS Deep Star Maps (ESA/Gaia/DPAC) · NASA/JPL-Caltech Viking Mars map")
    if COUNTDOWN_SHIPPED:
        nasa_media += " · NASA audio (Apollo 11 launch commentary)"
    if RAD_SHIPPED:
        nasa_media += " · NASA/JPL MSL RAD"
    if DSN_SHIPPED:
        nasa_media += " · NASA DSN sites"
    assets = ("Poly Haven, ambientCG and MakeHuman (CC0). Fonts: Barlow Condensed, Source Sans 3, Inter "
              "(SIL OFL 1.1), DejaVu Sans Mono.")
    if PARTICLE_PACK_SHIPPED:
        assets += " Launch effects: Unity Particle Pack (Unity Technologies)."
    rows = [
        ("MUSIC", "“Titan” and “Cobalt” by Scott Buckley (scottbuckley.com.au), CC BY 4.0, adapted excerpts. "
                  "Ending themes and score cues: original."),
        ("NASA DATA AND MEDIA", nasa_media + ". No endorsement implied."),
        ("ASSETS", assets),
        ("AI USE", "Idea, story and design decisions: the team. Code, procedural models and textures, original score "
                   "cues and sound effects: written with AI coding assistants (Claude Code, OpenAI Codex) under our "
                   "direction. All dialogue: AI-generated synthetic voices, Google Gemini text-to-speech"
                   + (" (voices Orus, Kore and Iapetus), incl. 16 new radio lines. " if D_BUILD else ". ")
                   + "No AI-generated pictures in this deck."),
        ("FICTION", "AURORA-7, LC-7, the crew and the rescue are fictional. No NASA logos or insignia."),
    ]
    heights = {"MUSIC": 0.8, "NASA DATA AND MEDIA": 1.14, "ASSETS": 0.82, "AI USE": 1.36, "FICTION": 0.62}
    if PARTICLE_PACK_SHIPPED:  # the ASSETS row needs a third line
        heights.update({"ASSETS": 1.0, "AI USE": 1.3, "FICTION": 0.56})
    y = 2.02
    for head, body in rows:
        text(s, 6.78, y, 5.75, 0.26, [[R(head, 9, AMBER, BODY_SEMI, spc=1.5)]])
        text(s, 6.78, y + 0.26, 5.75, heights[head] - 0.26, [([R(body, 10.5, TXT2)], None, 0, 1.05)])
        y += heights[head]
    text(s, 0.6, 6.1, 5.6, 0.7,
         [([R("Full credits, licences and the AI statement: README of the public project.", 11, MUTED)], None, 0, 1.05)])


def main():
    global OUT, HORIZONS_LINE_SHIPPED, RAD_SHIPPED, DSN_SHIPPED, PARTICLE_PACK_SHIPPED, D_BUILD
    import os
    if os.environ.get("OOR_DECK_D") == "1":  # deck of the D build (see D_BUILD above)
        D_BUILD = True
    if os.environ.get("OOR_DECK_PREVIEW_B") == "1":  # layout preview with every B-build extra switched on
        HORIZONS_LINE_SHIPPED = RAD_SHIPPED = DSN_SHIPPED = PARTICLE_PACK_SHIPPED = True
        TMP.mkdir(exist_ok=True)
        OUT = TMP / "preview-b.pptx"
    # make_b_build.py builds the B deck without touching this file:
    #   OOR_DECK_FLAGS=horizons,rad[,dsn][,particle]  OOR_DECK_OUT=<path to .pptx>
    for flag in filter(None, os.environ.get("OOR_DECK_FLAGS", "").split(",")):
        if flag == "horizons":
            HORIZONS_LINE_SHIPPED = True
        elif flag == "rad":
            RAD_SHIPPED = True
        elif flag == "dsn":
            DSN_SHIPPED = True
        elif flag == "particle":
            PARTICLE_PACK_SHIPPED = True
    if os.environ.get("OOR_DECK_OUT"):
        OUT = Path(os.environ["OOR_DECK_OUT"])
    prs = Presentation()
    prs.slide_width, prs.slide_height = W, H
    for build in (slide_title, slide_challenge, slide_loop, slide_data, slide_learn, slide_built, slide_next):
        build(prs)
    core = prs.core_properties
    core.title = "OUT OF REACH — NASA Space Apps Challenge 2026"
    core.author = "Team Polish Alex Goats"
    core.subject = "Space Mission Design Game — 7-slide project demo"
    core.keywords = "NASA Space Apps 2026; Space Mission Design Game; AI-assisted; synthetic voices: Google Gemini TTS"
    core.comments = ("All pictures are real in-game captures. Project built with AI coding assistants (Claude Code, "
                     "OpenAI Codex); deck drafted with Claude Code and edited by the team.")
    prs.save(OUT)
    print(OUT)


if __name__ == "__main__":
    main()
