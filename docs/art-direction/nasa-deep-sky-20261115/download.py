import datetime
import hashlib
import json
from pathlib import Path
import urllib.request

ROOT = Path(__file__).resolve().parent
SOURCES = {
    'starmap_2020_4k.exr': 'https://svs.gsfc.nasa.gov/vis/a000000/a004800/a004851/starmap_2020_4k.exr',
    'source-page.html': 'https://svs.gsfc.nasa.gov/4851/',
    'svs-reuse-policy.html': 'https://svs.gsfc.nasa.gov/help/',
    'nasa-media-policy.html': 'https://www.nasa.gov/nasa-brand-center/images-and-media/',
}
records = []
for filename, url in SOURCES.items():
    path = ROOT / filename
    if not path.exists():
        req = urllib.request.Request(url, headers={'User-Agent': 'OutOfReachScientificSkyReview/1.0'})
        with urllib.request.urlopen(req, timeout=90) as response:
            content = response.read(40_000_001)
            if len(content) > 40_000_000:
                raise ValueError('Bounded download exceeds 40 MB: ' + filename)
        path.write_bytes(content)
    raw = path.read_bytes()
    records.append(dict(file=filename, url=url, bytes=len(raw), sha256=hashlib.sha256(raw).hexdigest()))
    print(filename, len(raw), records[-1]['sha256'], flush=True)
(ROOT / 'retrieval.json').write_text(json.dumps(dict(retrieved_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), files=records), indent=2)+'\n')
