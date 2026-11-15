"""Print word and character counts of every ```text block in a Markdown file, with the heading above it."""
import re
import sys
from pathlib import Path

src = Path(sys.argv[1]).read_text(encoding="utf-8")
heading = ""
lines = src.splitlines()
i = 0
n = 0
while i < len(lines):
    line = lines[i]
    if line.startswith("#"):
        heading = line.strip()
    if line.strip() == "```text":
        j = i + 1
        block = []
        while j < len(lines) and lines[j].strip() != "```":
            block.append(lines[j])
            j += 1
        text = "\n".join(block)
        n += 1
        words = len(re.findall(r"\S+", text))
        non_ascii = sorted({c for c in text if ord(c) > 127})
        print(f"{n:2d}. {heading[:48]:48s} words={words:5d} chars={len(text):6d} first={text[:40]!r} non_ascii={''.join(non_ascii)!r}")
        i = j
    i += 1
