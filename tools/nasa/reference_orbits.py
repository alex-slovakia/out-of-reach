"""Offline-verifiable, dated heliocentric reference for the Sources viewer.

Run --fetch to deliberately fetch two official Horizons tables and physical-radius
source pages; default verifies committed source hashes and exact normalization.
Python standard library only. This never changes mission/simulation data.
"""
from __future__ import annotations
import argparse, csv, hashlib, io, json, math, re
from datetime import datetime, timedelta, timezone
from pathlib import Path
from urllib.parse import urlencode
from urllib.request import Request, urlopen

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / 'docs/data/reference-orbits'
RUNTIME = ROOT / 'docs/data/reference-orbits/normalized.json'
START = datetime(2026, 11, 14, 8, tzinfo=timezone.utc)
DAYS = 700
END = START + timedelta(days=DAYS)
API = 'https://ssd.jpl.nasa.gov/api/horizons.api'
RADII = {'Sun': 695700.0, 'Earth': 6371.0084, 'Mars': 3389.5}
SOURCES = {
    'planet-physical-parameters.html': 'https://ssd.jpl.nasa.gov/planets/phys_par.html',
    'sun-fact-sheet.html': 'https://nssdc.gsfc.nasa.gov/planetary/factsheet/sunfact.html'
}

def require(value, message):
    if not value: raise ValueError(message)

def digest(data): return hashlib.sha256(data).hexdigest()
def request(url):
    with urlopen(Request(url, headers={'User-Agent': 'OUT-OF-REACH-reference-data/1.0'}), timeout=45) as response:
        return response.read()

def parameters(target):
    return dict(format='json', COMMAND=f"'{target}'", CENTER="'500@10'", MAKE_EPHEM="'YES'",
        EPHEM_TYPE="'VECTORS'", START_TIME=f"'{START:%Y-%m-%d %H:%M}'", STOP_TIME=f"'{END:%Y-%m-%d %H:%M}'",
        STEP_SIZE="'1 d'", TIME_TYPE="'UT'", OUT_UNITS="'KM-S'", REF_SYSTEM="'ICRF'",
        REF_PLANE="'ECLIPTIC'", VEC_CORR="'NONE'", VEC_TABLE="'3'", CSV_FORMAT="'YES'", OBJ_DATA="'YES'")

def extract(raw, name, target):
    response=json.loads(raw)
    require('error' not in response, response.get('error'))
    result=response.get('result','')
    for pattern in [rf'Target body name:\s+{name} \({target}\)', r'Center body name:\s+Sun \(10\)',
                    r'Center-site name:\s+BODY CENTER',r'Output units\s*:\s*KM-S',
                    r'Reference frame\s*:\s*Ecliptic of J2000',r'Geometric state vectors have NO corrections',
                    r'JDUT\s*,\s*Calendar Date \(UT\s*\),\s*X,\s*Y,\s*Z,\s*VX,\s*VY,\s*VZ,\s*LT,\s*RG,\s*RR']:
        require(re.search(pattern,result),f'{name}: unexpected metadata {pattern}')
    require(result.count('$$SOE')==1 and result.count('$$EOE')==1,'Missing ephemeris table')
    rows=[]
    for index,values in enumerate(csv.reader(io.StringIO(result.split('$$SOE')[1].split('$$EOE')[0].strip()))):
        values=[value.strip() for value in values]
        require(len(values)==12 and values[-1]=='','Unexpected vector columns')
        epoch=datetime.strptime(values[1], 'A.D. %Y-%b-%d %H:%M:%S.%f').replace(tzinfo=timezone.utc)
        require(epoch==START+timedelta(days=index),'Unexpected epoch/spacing')
        numbers=[float(values[n]) for n in [0,*range(2,11)]]
        require(all(map(math.isfinite,numbers)),'Nonfinite source')
        x,y,z=map(float,values[2:5]); distance=math.sqrt(x*x+y*y+z*z)
        require(math.isclose(distance,float(values[9]),rel_tol=1e-12),'Source RG mismatch')
        require(math.isclose(distance/299792.458,float(values[8]),rel_tol=1e-12),'Source LT mismatch')
        require(abs(float(values[0])-(2461358.833333333+index))<1e-7,'Unexpected Julian date')
        rows.append(dict(x=x,y=y,z=z))
    require(len(rows)==DAYS+1,'Expected701 daily samples')
    return rows,response.get('signature',{})

def normalized():
    bodies=[dict(name='Sun',horizonsId=10,radiusKm=RADII['Sun'],x=0.0,y=0.0,z=0.0,path=[])]
    for name,target in [('Earth',399),('Mars',499)]:
        rows,_=extract((DATA/f'{name.lower()}.raw.json').read_bytes(),name,target)
        bodies.append(dict(name=name,horizonsId=target,radiusKm=RADII[name],**rows[0],path=rows))
    return dict(schema=1,epochUtc=START.strftime('%Y-%m-%d %H:%M UTC'),traceEndUtc=END.strftime('%Y-%m-%d %H:%M UTC'),
        sampleStepDays=1,origin='Sun body centre (10)',referenceFrame='ICRF / mean ecliptic of J2000',
        units='km',correction='NONE',bodies=bodies)

def encoded(value):return (json.dumps(value,indent=2,ensure_ascii=False,allow_nan=False)+'\n').encode('utf-8')

def verify():
    manifest=json.loads((DATA/'manifest.json').read_bytes())
    for file,entry in manifest['sources'].items():
        require(digest((DATA/file).read_bytes())==entry['sha256'],f'Hash mismatch: {file}')
    expected=encoded(normalized())
    require(RUNTIME.read_bytes()==expected,'Runtime normalization differs from exact raw vectors')
    require(digest(expected)==manifest['runtime_sha256'],'Runtime hash mismatch')
    data=json.loads(expected); sun,earth,mars=data['bodies']
    require([sun[k] for k in ['x','y','z']]==[0,0,0],'Sun origin is not zero')
    for body in data['bodies']:require(body['radiusKm']==RADII[body['name']],'Radius mismatch')
    separation=math.dist([earth[k] for k in ['x','y','z']],[mars[k] for k in ['x','y','z']])
    xy=math.hypot(earth['x']-mars['x'],earth['y']-mars['y'])
    existing=ROOT/'docs/data/horizons/earth-mars-2026-11-14--2026-12-15.csv'
    if existing.exists():
        with existing.open(newline='',encoding='utf-8') as stream: row=next(csv.DictReader(stream))
        require(math.isclose(separation,float(row['earth_mars_distance_km']),abs_tol=.01,rel_tol=0),'Heliocentric3D separation disagrees with existing geocentric snapshot')
    report={'pass':True,'earth_samples':len(earth['path']),'mars_samples':len(mars['path']),
        'earth_sun_km':math.sqrt(sum(earth[k]**2 for k in ['x','y','z'])),
        'mars_sun_km':math.sqrt(sum(mars[k]**2 for k in ['x','y','z'])),
        'earth_mars_3d_km':separation,'earth_mars_projected_xy_km':xy,
        'sun_to_earth_mean_radius_ratio':RADII['Sun']/RADII['Earth'],
        'earth_to_mars_mean_radius_ratio':RADII['Earth']/RADII['Mars'],
        'runtime_sha256':digest(expected)}
    print(json.dumps(report,indent=2))
    return report

def fetch():
    DATA.mkdir(parents=True,exist_ok=True); RUNTIME.parent.mkdir(parents=True,exist_ok=True)
    sources={}
    for name,target in [('Earth',399),('Mars',499)]:
        params=parameters(target);url=API+'?'+urlencode(params);raw=request(url)
        _,signature=extract(raw,name,target)
        file=name.lower()+'.raw.json';(DATA/file).write_bytes(raw)
        sources[file]=dict(url=url,parameters=params,sha256=digest(raw),signature=signature,
            retrieved_utc=datetime.now(timezone.utc).isoformat())
    for file,url in SOURCES.items():
        raw=request(url);(DATA/file).write_bytes(raw)
        sources[file]=dict(url=url,sha256=digest(raw),retrieved_utc=datetime.now(timezone.utc).isoformat())
    data=encoded(normalized()); RUNTIME.write_bytes(data)
    (DATA/'manifest.json').write_bytes(encoded(dict(dataset_id='oor-heliocentric-reference-20261114-v1',
        units='km and seconds',time_scale='UT (UTC in this epoch; future leap seconds subject to Horizons caveat)',
        epoch_utc=START.isoformat(),trace_end_utc=END.isoformat(),samples_per_body=DAYS+1,mean_radii_km=RADII,
        radius_selection='NASA physical table volumetric mean Earth/Mars radii and NASA Sun fact-sheet mean radius, manually checked 2026-11-15.',
        runtime_path=str(RUNTIME.relative_to(ROOT)).replace('\\','/'),runtime_sha256=digest(data),sources=sources,
        assumptions=['Static dated reference, not mission live state or fictional spacecraft trajectory.',
            'Positions are geometric, heliocentric body centres, no aberration/light-time corrections.',
            'Top-down display projects ecliptic XY; Z remains in data and3D range readout.',
            'Daily points connected by straight segments approximate orbital traces over700 days.',
            'Bodies are mean-radius spheres; no terrain, atmosphere, oblateness or true-colour rendering.',
            'Same pixels-per-km scale for radii and position; optional locator marks are not physical size.'])))
    report=verify();(DATA/'verification.json').write_bytes(encoded(report))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--fetch',action='store_true');args=parser.parse_args()
    fetch() if args.fetch else verify()
