"""EVA chest display texture (original, fictional AURORA-7 UI). python make_display.py <out.png>"""
import sys, math
from PIL import Image, ImageDraw

W, H = 512, 256
im = Image.new("RGB", (W, H), (6, 16, 24))
d = ImageDraw.Draw(im)
cy, cd = (64, 214, 240), (28, 110, 130)
d.rectangle([6, 6, W - 7, H - 7], outline=cd, width=3)
d.rectangle([6, 6, W - 7, 44], fill=(10, 34, 46))
d.text((18, 14), "AURORA-7  SUIT 02   O2 PRI", fill=cy)
d.text((W - 110, 14), "MET 31:17:39", fill=(240, 190, 80))
# oxygen / pressure bars
for i, (lab, v, col) in enumerate((("O2", 0.82, cy), ("PRS", 0.64, cy), ("CO2", 0.18, (240, 190, 80)), ("BAT", 0.71, cy))):
    y = 60 + i * 34
    d.text((18, y + 4), lab, fill=cd)
    d.rectangle([70, y, 250, y + 20], outline=cd, width=2)
    d.rectangle([73, y + 3, 73 + int(174 * v), y + 17], fill=col)
# trend graph
gx0, gy0, gx1, gy1 = 270, 56, W - 18, 196
d.rectangle([gx0, gy0, gx1, gy1], outline=cd, width=2)
for k in range(1, 4):
    yy = gy0 + k * (gy1 - gy0) // 4
    d.line([gx0, yy, gx1, yy], fill=(14, 44, 56), width=1)
pts = [(gx0 + i * (gx1 - gx0) / 60.0, gy1 - 18 - (gy1 - gy0 - 40) * (0.55 + 0.3 * math.sin(i / 7.0) * math.exp(-i / 70.0))) for i in range(61)]
d.line(pts, fill=cy, width=3)
d.text((18, 206), "TETHER OK   COMM LOS   HDG 214", fill=cd)
d.rectangle([W - 120, 204, W - 20, 230], outline=(200, 70, 50), width=2)
d.text((W - 108, 210), "CAUTION 0", fill=(220, 90, 70))
im.save(sys.argv[1])
print("display", sys.argv[1])
