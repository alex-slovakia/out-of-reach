"""Build README.pdf for the public final-project folder from README.txt plus four real game captures.

Run:  python make_readme_pdf.py        (needs PyMuPDF; reads README.txt and ../deck/captures/*.png)
The pictures are real in-game captures (no AI-generated image), the same ones the slide deck uses.
"""
from __future__ import annotations

import html
import re
from pathlib import Path

import fitz  # PyMuPDF

HERE = Path(__file__).resolve().parent
CAPS = HERE.parent / "deck" / "captures"
OUT = HERE / "README.pdf"
TMP = HERE / ".build"

PICTURES = [
    ("022138d-06-liftoff-tower-v03.png", "Launch from complex LC-7"),
    ("cf2224d-20-antenna-work.png", "Spacewalk: repairing the antenna"),
    ("r18-routine-montage-day4.png", "Ordinary days: the ship's real ledger"),
    ("r20-08-docking-camera-hard-dock-stars.png", "Day 30: the rescue craft docks"),
]

CSS = """
body { font-family: sans-serif; font-size: 9.6pt; line-height: 1.32; color: #16191d; }
h1 { font-size: 25pt; margin: 0 0 2pt 0; }
p.tag { font-size: 12.5pt; color: #8a5200; margin: 0 0 8pt 0; }
h2 { font-size: 10.5pt; color: #8a5200; margin: 11pt 0 3pt 0; letter-spacing: 1pt; }
p { margin: 0 0 4pt 0; }
p.item { margin: 0 0 3pt 14pt; }
p.cap { font-size: 8pt; color: #5b6570; margin: 1pt 0 7pt 0; }
p.note { font-size: 8pt; color: #5b6570; margin: 8pt 0 0 0; }
"""


def blocks(text: str):
    """Yield ('h2'|'p'|'item', text) from the plain README: blank-line blocks, wrapped lines joined."""
    for raw in re.split(r"\n\s*\n", text.strip()):
        lines = raw.splitlines()
        if not lines:
            continue
        first = lines[0].strip()
        if len(lines) == 1 and first.isupper() and len(first) < 60:
            yield "h2", first
            continue
        start = 0
        if first.isupper() and len(first) < 60:
            yield "h2", first
            start = 1
        cur_kind, cur = None, []
        for line in lines[start:]:
            is_item = re.match(r"^(\d+\.|-)\s", line) is not None
            if is_item or (not line.startswith(" ") and cur_kind == "item"):
                if cur:
                    yield cur_kind, " ".join(cur)
                cur_kind, cur = ("item" if is_item else "p"), [line.strip()]
            else:
                if cur_kind is None:
                    cur_kind = "p"
                cur.append(line.strip())
        if cur:
            yield cur_kind, " ".join(cur)


def grid_picture() -> str:
    """One 2 x 2 JPEG of the four captures, so the PDF stays small and the layout is fixed."""
    from PIL import Image

    TMP.mkdir(exist_ok=True)
    w, h, gap = 800, 450, 12
    sheet = Image.new("RGB", (2 * w + gap, 2 * h + gap), (255, 255, 255))
    for i, (name, _cap) in enumerate(PICTURES):
        im = Image.open(CAPS / name).convert("RGB").resize((w, h), Image.LANCZOS)
        sheet.paste(im, ((i % 2) * (w + gap), (i // 2) * (h + gap)))
    dst = TMP / "captures-grid.jpg"
    sheet.save(dst, quality=88)
    return dst.name


def main():
    global OUT
    import sys
    src = HERE / "README.txt"
    if len(sys.argv) == 3:  # make_readme_pdf.py <README.txt> <README.pdf>, used by make_b_build.py
        src, OUT = Path(sys.argv[1]), Path(sys.argv[2])
    text = src.read_text(encoding="utf-8")
    head, _, rest = text.partition("\n\n")
    title, tagline = head.splitlines()[0], head.splitlines()[1]
    parts = [f"<h1>{html.escape(title)}</h1>", f'<p class="tag">{html.escape(tagline)}</p>']
    body = list(blocks(rest))
    # first paragraph (what the game is), then the pictures, then the rest
    kind, first = body[0]
    parts.append(f"<p>{html.escape(first)}</p>")
    caption = " · ".join(cap for _name, cap in PICTURES)
    parts.append(f'<p><img src="{grid_picture()}" width="499"/></p>'
                 f'<p class="cap">In-game captures: {html.escape(caption)}.</p>')
    for kind, value in body[1:]:
        if kind == "h2":
            parts.append(f"<h2>{html.escape(value)}</h2>")
        elif kind == "item":
            parts.append(f'<p class="item">{html.escape(value)}</p>')
        else:
            parts.append(f"<p>{html.escape(value)}</p>")
    parts.append('<p class="note">All pictures are real in-game captures. No AI-generated image is used.</p>')
    story = fitz.Story(html="".join(parts), user_css=CSS, archive=fitz.Archive(str(TMP)))
    box = fitz.paper_rect("a4")
    where = box + (48, 44, -48, -44)
    writer = fitz.DocumentWriter(str(OUT))
    more = 1
    while more:
        dev = writer.begin_page(box)
        more, _ = story.place(where)
        story.draw(dev)
        writer.end_page()
    writer.close()
    doc = fitz.open(str(OUT))
    doc.set_metadata({
        "title": "OUT OF REACH - README",
        "author": "Team Polish Alex Goats",
        "subject": "NASA Space Apps Challenge 2026 - Space Mission Design Game",
        "keywords": "AI-assisted project; synthetic voices: Google Gemini TTS; real in-game captures",
    })
    doc.saveIncr()
    print(OUT, doc.page_count, "pages")


if __name__ == "__main__":
    main()
