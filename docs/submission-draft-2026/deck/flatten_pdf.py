"""Make the deck PDF look the same in every viewer.

    python flatten_pdf.py <deck.pdf> <folder with slide-01.png ... from export_pdf.ps1 -PngDir>

Why: the headline font (Bahnschrift, a variable font) is embedded by PowerPoint's PDF export with its default
outlines, so browsers and other PDF viewers draw thin, crowded headlines and the decimal points of the big
numbers nearly vanish ("2.5" reads like "25"). PowerPoint's own slide pictures are correct. This script lays
each slide picture over its PDF page. The original text stays underneath, so the PDF remains searchable and
selectable; what the reader sees is exactly the PowerPoint rendering.
"""
from __future__ import annotations

import io
import sys
from pathlib import Path

import fitz  # PyMuPDF
from PIL import Image


def main() -> int:
    pdf, pngdir = Path(sys.argv[1]), Path(sys.argv[2])
    doc = fitz.open(str(pdf))
    meta = dict(doc.metadata)
    for i, page in enumerate(doc):
        png = pngdir / f"slide-{i + 1:02d}.png"
        im = Image.open(png).convert("RGB")
        buf = io.BytesIO()
        im.save(buf, "JPEG", quality=92, subsampling=0, optimize=True)
        page.insert_image(page.rect, stream=buf.getvalue(), overlay=True)
    doc.set_metadata(meta)
    tmp = pdf.with_name(pdf.stem + ".flat.tmp")
    doc.save(str(tmp), garbage=3, deflate=True)
    pages = doc.page_count
    doc.close()
    tmp.replace(pdf)
    print(f"flattened {pdf.name}: {pages} pages, {pdf.stat().st_size:,} bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
