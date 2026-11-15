"""make_sheet - contact sheet of the SHIP-DETAIL-02 preview renders (original tooling; Pillow's built-in font).

Run: python -B make_sheet.py      (reads ../Previews~/DP02_*.png, writes ../Previews~/DP02_Sheet.png)
     python -B make_sheet.py --refine   (CKP-REFINE: writes ../Previews~/DP02_CKR_Sheet.png from DP02_CKR_*.png)
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
PREVIEWS = os.path.join(os.path.dirname(HERE), "Previews~")
TILES = [
    ("DP02_Engineering.png", "ENGINEERING  frame loom R z9.4 (P-clamps, junction box, backshells), aft access panel"),
    ("DP02_Habitat.png", "HABITAT  strapped bags, ration/drink packs, grow-cassette plumbing"),
    ("DP02_HabitatGrowPlumbing.png", "HABITAT  potable lines, clamp blocks, capped valves, flex hoses"),
    ("DP02_HabitatCrewKit.png", "HABITAT  rolled sleeping bag tie-down, notebook, photo, headphones"),
    ("DP02_Cockpit.png", "COCKPIT  frame loom L z0.0, crew notes board behind the commander's seat"),
    ("DP02_CockpitAft.png", "COCKPIT  aft access panel on the starboard wing of the z3 portal (cockpit face)"),
]
W, H, BAR, COLS = 800, 450, 34, 2


def main():
    tiles = [(name, text) for name, text in TILES if os.path.exists(os.path.join(PREVIEWS, name))]
    rows = (len(tiles) + COLS - 1) // COLS
    sheet = Image.new("RGB", (COLS * W, rows * (H + BAR) + 40), (18, 19, 22))
    draw = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.load_default(size=18)
        title = ImageFont.load_default(size=22)
    except TypeError:
        font = title = ImageFont.load_default()
    draw.text((12, 9), "AURORA-7  SHIP-DETAIL-02  Blender Cycles previews inside the generated ship (original work)",
              font=title, fill=(230, 176, 74))
    for i, (name, text) in enumerate(tiles):
        x, y = (i % COLS) * W, 40 + (i // COLS) * (H + BAR)
        image = Image.open(os.path.join(PREVIEWS, name)).convert("RGB").resize((W, H), Image.LANCZOS)
        sheet.paste(image, (x, y))
        draw.text((x + 10, y + H + 7), text, font=font, fill=(200, 205, 210))
    out = os.path.join(PREVIEWS, "DP02_Sheet.png")
    sheet.save(out, optimize=True)
    print("sheet", len(tiles), "tiles ->", os.path.basename(out), sheet.size)


REFINE = [  # (file, caption, column span)
    ("DP02_CKR_FirstPerson.png", "FIRST PERSON  standing at the console (1.65 m eye, 80 deg), generated ship, Cycles", 2),
    ("DP02_CKR_Front.png", "ISOLATED FRONT  console + CKP-REFINE (orthographic)", 1),
    ("DP02_CKR_Side.png", "ISOLATED SIDE  starboard end (orthographic)", 1),
    ("DP02_CKR_Before.png", "BEFORE  existing console", 1),
    ("DP02_CKR_After.png", "AFTER  with CKP-REFINE", 1),
]


def refine_sheet():
    """FIRST PERSON over the full width, then front | side and before | after."""
    cell_w, cell_h = W, H
    rows = [[REFINE[0]], REFINE[1:3], REFINE[3:5]]
    heights = [(2 * cell_h if row[0][2] == 2 else cell_h) + BAR for row in rows]
    sheet = Image.new("RGB", (2 * cell_w, sum(heights) + 40), (18, 19, 22))
    draw = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.load_default(size=18)
        title = ImageFont.load_default(size=22)
    except TypeError:
        font = title = ImageFont.load_default()
    draw.text((12, 9), "AURORA-7  SHIP-DETAIL-02  CKP-REFINE: close-range cockpit console and seats (original work)",
              font=title, fill=(230, 176, 74))
    y = 40
    for row, height in zip(rows, heights):
        x = 0
        for name, text, span in row:
            w, h = span * cell_w, (2 * cell_h if span == 2 else cell_h)
            image = Image.open(os.path.join(PREVIEWS, name)).convert("RGB").resize((w, h), Image.LANCZOS)
            sheet.paste(image, (x, y))
            draw.text((x + 10, y + h + 7), text, font=font, fill=(200, 205, 210))
            x += w
        y += height
    out = os.path.join(PREVIEWS, "DP02_CKR_Sheet.png")
    sheet.save(out, optimize=True)
    print("refine sheet ->", os.path.basename(out), sheet.size)


if __name__ == "__main__":
    if "--refine" in sys.argv[1:]:
        refine_sheet()
    else:
        main()
