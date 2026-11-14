"""Bounded extraction of NASA GMAT's Apache-2.0 star graphics asset.

No BSC data is copied into this output. Magnitudes are source visualization data.
--fetch re-fetches pinned source files, --build regenerates compact data; default verifies.
"""
import sys
sys.dont_write_bytecode = True
import argparse
import csv
import hashlib
import io
import json
import math
from pathlib import Path
import urllib.request

ROOT = Path(__file__).resolve().parent


def sha(data):
    return hashlib.sha256(data).hexdigest()


def direction(ra_deg, dec_deg):
    """Unity celestial basis: +Z=RA0, +X=RA90, +Y=north pole."""
    a, d = math.radians(ra_deg), math.radians(dec_deg)
    return (math.cos(d) * math.sin(a), math.sin(d), math.cos(d) * math.cos(a))


def fetch():
    provenance=json.loads((ROOT/'gmat-retrieval.json').read_bytes())
    for f in provenance['files']:
        with urllib.request.urlopen(f['url'],timeout=45) as response:
            raw=response.read(1_500_001)
        if len(raw)>1_500_000 or sha(raw)!=f['sha256']:
            raise ValueError('Pinned source changed or exceeded cap: '+f['filename'])
        (ROOT/f['filename']).write_bytes(raw)
    print('Pinned NASA GMAT source bytes verified and retrieved')


def products():
    rows=[]
    for line_no, line in enumerate((ROOT/'gmat-stars.raw.txt').read_text().splitlines(),1):
        ra,dec,mag=map(float,line.split()[:3])
        if not all(math.isfinite(v) for v in (ra,dec,mag)) or not (0<=ra<360 and -90<=dec<=90):
            raise ValueError('Invalid source row '+str(line_no))
        # Keep Polaris source row as-is, explicitly flagged. Do NOT apply upstream mag=-2 override.
        if mag>5.5 and line_no!=1:
            continue
        x,y,z=direction(ra,dec)
        rows.append({'source_line':line_no,'ra_deg':ra,'dec_deg':dec,'source_visual_magnitude':mag,
                     'bv_color':None,'unity_x':round(x,12),'unity_y':round(y,12),'unity_z':round(z,12),
                     'relative_flux_from_source_mag':round(10**(-0.4*mag),12),
                     'special_source_row':line_no==1})
    out=io.StringIO(newline='')
    out.write('# Modified NASA GMAT star asset: magnitude subset plus Polaris; added Unity directions and derived flux. See THIRD-PARTY-NOTICE.md and gmat-LICENSE.txt.\n')
    writer=csv.DictWriter(out,fieldnames=list(rows[0]),lineterminator='\n')
    writer.writeheader(); writer.writerows(rows)
    wrapper={'dataset_id':'nasa-gmat-stars-v01','source_commit':'b8aec552a8f34a9212063adc3f249bd2315397a3',
             'modification_notice':'Modified NASA GMAT star asset: magnitude subset plus Polaris; added Unity directions and derived flux. See THIRD-PARTY-NOTICE.md and gmat-LICENSE.txt.',
             'frame':'RA/Dec degrees; J2000 alignment inferred from upstream renderer, original observation epoch unspecified',
             'photometry':'coarse source visualization magnitudes; no precision Johnson-V assertion; Polaris source mag 6.0 retained',
             'color':'B-V absent; null. Neutral-white display is an artistic choice.',
             'columns':list(rows[0]),'stars':[list(r.values()) for r in rows]}
    return rows,{'gmat-stars.csv':out.getvalue().encode(),
                 'gmat-stars.json':(json.dumps(wrapper,separators=(',',':'),allow_nan=False)+'\n').encode()}


def verify(rows):
    for f in json.loads((ROOT/'gmat-retrieval.json').read_bytes())['files']:
        assert sha((ROOT/f['filename']).read_bytes())==f['sha256']
    source_rows=len((ROOT/'gmat-stars.raw.txt').read_text().splitlines())
    assert source_rows==42100
    assert len(rows)==2797
    for r in rows:
        assert abs(math.sqrt(sum(r['unity_'+k]**2 for k in 'xyz'))-1)<1e-11
        assert math.isclose(r['relative_flux_from_source_mag'],10**(-.4*r['source_visual_magnitude']),rel_tol=1e-9)
    # Independent, hand-selected anchors from the pinned upstream asset, not BSC.
    anchors=[(1,37.95,89.267,6.0,'Polaris'),(2,101.289,-16.713,-1.1,'Sirius'),
             (3,95.988,-52.696,-.6,'Canopus'),(4,279.234,38.783,.1,'Vega'),
             (6,213.918,19.187,.2,'Arcturus'),(12,88.793,7.407,.8,'Betelgeuse')]
    for line,ra,dec,mag,name in anchors:
        r=next(r for r in rows if r['source_line']==line)
        assert (r['ra_deg'],r['dec_deg'],r['source_visual_magnitude'])==(ra,dec,mag),name
    duplicates=len(rows)-len({(r['ra_deg'],r['dec_deg']) for r in rows})
    assert duplicates==12
    return {'result':'PASS','source_rows':source_rows,'selected_rows':len(rows),'duplicate_exact_positions':duplicates,
            'checks':['pinned raw/license/parser SHA-256','all unit vectors','source flux mapping','six source anchors','byte-identical regeneration'],
            'limitations':['J2000 frame inferred, not explicit source epoch metadata','coarse magnitudes','no color data','12 exact duplicate positions retained; do not sum duplicate brightness','Polaris magnitude 6.0 source visualization value, not physical V'],'Unity':'NOT RUN'}


if __name__=='__main__':
    if sys.flags.optimize:
        raise RuntimeError('Verification requires assertions; do not run Python with -O.')
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--fetch',action='store_true'); parser.add_argument('--build',action='store_true'); args=parser.parse_args()
    if args.fetch: fetch()
    rows, files=products()
    for name,data in files.items():
        if args.build: (ROOT/name).write_bytes(data)
        else: assert (ROOT/name).read_bytes()==data,name
    print(json.dumps(verify(rows),indent=2))
