import bpy
import hashlib
import json
import numpy as np
from pathlib import Path

root = Path(__file__).resolve().parent
path = root / 'starmap_2020_4k.exr'
im = bpy.data.images.load(str(path), check_existing=False)
assert tuple(im.size) == (4096, 2048), tuple(im.size)
pixels = np.empty(4096*2048*4, dtype=np.float32)
im.pixels.foreach_get(pixels)
pixels = pixels.reshape(2048,4096,4)
rgb = pixels[:,:,:3]
assert np.isfinite(rgb).all()
lum = rgb @ np.array([.2126,.7152,.0722], dtype=np.float32)
result = dict(width=4096, height=2048, channels=im.channels, colorspace=im.colorspace_settings.name,
    finite=True, negative_components=int((rgb<0).sum()),
    rgb_min=float(rgb.min()), rgb_max=float(rgb.max()),
    luminance_percentiles={str(q):float(np.percentile(lum,q)) for q in [0,25,50,75,90,95,99,99.9,100]},
    source_sha256=hashlib.sha256(path.read_bytes()).hexdigest())
(root/'pixel-inspection.json').write_text(json.dumps(result,indent=2)+'\n')
print('NASA_EXR_INSPECTION',json.dumps(result),flush=True)
