import sys
from PIL import Image, ImageDraw
ref, f, b, out = sys.argv[1:5]
r = Image.open(ref).convert("RGB"); r = r.resize((int(r.width * 1024 / r.height), 1024))
a = Image.open(f).convert("RGB"); c = Image.open(b).convert("RGB")
W = r.width + a.width + c.width
im = Image.new("RGB", (W, 1024)); im.paste(r, (0, 0)); im.paste(a, (r.width, 0)); im.paste(c, (r.width + a.width, 0))
d = ImageDraw.Draw(im); d.text((10, 10), "REFERENCE (left) | MODEL front, back (Blender, FBX re-import, rest pose)", fill=(0, 0, 0))
im.save(out)
