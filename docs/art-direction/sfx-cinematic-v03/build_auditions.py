"""V03: real recordings only; NumPy + installed FFmpeg. No Unity mutation."""
from pathlib import Path
import hashlib, json, re, subprocess
import numpy as np

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[2]
STAGE = PROJECT / '.local/sfx-cinematic-v03'
SOURCES = STAGE / 'sources'
RATE = 48000
DURATION = 6.5

def run(*args):
    return subprocess.run(['ffmpeg', '-nostdin', '-hide_banner', '-y', *map(str, args)], check=True, capture_output=True)

def source(name, start, duration, pitch=1, low=2400, high=30):
    filters = f'aresample={RATE},asetrate={RATE*pitch},aresample={RATE},highpass=f={high},lowpass=f={low}:p=2'
    raw = run('-ss',start,'-t',duration*pitch,'-i',SOURCES / (name+'.mp3'),'-ar',RATE,'-ac',2,
              '-af',filters,'-f','f32le','pipe:1').stdout
    x = np.frombuffer(raw,dtype='<f4').reshape(-1,2).copy()
    rms = np.sqrt(np.mean(x*x))
    return x * (.15/max(rms,.000001))

def shape(x, attack=.04, release=.3, decay=0):
    n=len(x); t=np.arange(n)/RATE
    env=np.minimum(t/max(attack,.0001),1)*np.minimum((n/RATE-t)/max(release,.0001),1)
    if decay: env*=np.exp(-t*decay)
    return x*env[:,None]

def blank(): return np.zeros((round(DURATION*RATE),2),dtype=np.float64)
def put(out,x,start,gain=1,pan=0):
    i=round(start*RATE); n=min(len(x),len(out)-i)
    if n>0: out[i:i+n]+=x[:n]*gain*np.array([1-max(0,pan)*.45,1+min(0,pan)*.45])

def master(x):
    x=x-np.mean(x,axis=0)
    x=shape(x,.012,.12)
    # One fixed gain, no brickwall limiting or noise generation.
    return (x*(.67/max(np.max(np.abs(x)),.000001))).astype('<f4')

def render(name,x):
    wav=STAGE/(name+'.wav'); raw=STAGE/(name+'.f32')
    raw.write_bytes(x.astype('<f4').tobytes())
    run('-f','f32le','-ar',RATE,'-ac',2,'-i',raw,'-c:a','pcm_s24le',wav)
    mp3=HERE/(name+'.mp3')
    run('-i',wav,'-c:a','libmp3lame','-b:a','320k','-id3v2_version','3',mp3)
    result=run('-i',mp3,'-af','ebur128=peak=true','-f','null','NUL').stderr.decode(errors='replace')
    (STAGE/(name+'-levels.txt')).write_text(result)
    peaks=re.findall(r'Peak:\s+([-\d.]+) dBFS',result)
    return {'file':mp3.name,'duration_seconds':len(x)/RATE,'sha256':hashlib.sha256(mp3.read_bytes()).hexdigest(),
            'decoded_true_peak_dbfs':float(peaks[-1]) if peaks else None,
            'pcm_peak_dbfs':float(20*np.log10(np.max(np.abs(x)))),
            'pcm_rms_dbfs':float(20*np.log10(np.sqrt(np.mean(x*x))))}

variants=[]
# A: expose J79 spool texture; B: much lower hull-coupled launch mass.
x=blank()
put(x,shape(source('j79',10,6.5,.85,2600,42),.15,.8),0,.8)
put(x,shape(source('j79',34,5.8,.44,240,27),1.6,1.0),.7,.85)
put(x,shape(source('metal',0,1.0,.6,1700,70),.002,.25,2.3),.55,.12)
variants.append(('01-engine-A-turbine-spool',master(x)))
x=blank()
put(x,shape(source('launch',4,6.4,.72,950,28),1.1,.6),.05,.9)
put(x,shape(source('j79',25,5.7,.6,1600,110),1.6,.7),.5,.45)
put(x,shape(source('j79',45,5.7,.35,170,25),.8,.7),.6,.8)
variants.append(('02-engine-B-hull-mass',master(x)))

# Impact: real metal body, stretched mechanical resonances and a recorded rocket tail.
x=blank()
put(x,shape(source('j79',12,1.3,.55,650,45)[::-1],.2,.06),.0,.18)
put(x,shape(source('metal',0,3.5,.38,1550,30),.002,.8,1.0),1.0,.95)
put(x,shape(source('shed',0,4.4,.55,850,28),.004,1.3,.7),1.02,.55)
put(x,shape(source('launch',8,4.8,.48,240,25),.03,1.3,.72),1.03,.6)
variants.append(('03-shock-A-structural-slam',master(x)))
x=blank()
put(x,shape(source('launch',5,1.0,.6,700,35)[::-1],.4,.02),.1,.22)
put(x,shape(source('shed',0,4.5,.4,1200,28),.002,1.2,.8),1.05,.8)
put(x,shape(source('metal',0,2,.72,2900,130),.002,.4,1.8),1.10,.36,.3)
put(x,shape(source('metal',0,3.6,.28,280,25),.002,1.3,.5),1.12,.8)
put(x,shape(source('shed',.2,2.8,.66,650,70),.004,1.4,1.3),1.36,.16,-.4)
variants.append(('04-shock-B-pressure-collapse',master(x)))

# Thrusters: three commanded burns, real valve-like metal attack, turbine body.
x=blank()
for t,d,g in [(0.6,.65,.7),(2.3,.95,.9),(4.3,1.5,1)]:
    put(x,shape(source('jet',2,d,.75,1450,65),.025,.13),t,g)
    put(x,shape(source('j79',48,d+.2,.40,220,27),.035,.23),t,g*.75)
    put(x,shape(source('metal',0,.16,1.2,2200,180),.001,.09,8),t,g*.12)
variants.append(('05-thruster-A-valve-bursts',master(x)))
x=blank()
for t,d,g,p in [(0.6,.8,.75,-.6),(2.5,1.1,.9,.6),(4.5,1.5,1,0)]:
    put(x,shape(source('j79',36,d,.65,950,45),.025,.16),t,g,p)
    put(x,shape(source('launch',7,d+.2,.42,180,25),.03,.25),t,g*.85,p)
    put(x,shape(source('shed',0,.25,.85,1200,150),.002,.16,8),t,g*.10,p)
variants.append(('06-thruster-B-deep-contact',master(x)))

records=[render(n,x) for n,x in variants]
gap=np.zeros((round(.5*RATE),2),dtype='<f4')
reel=np.concatenate([v for i,(_,x) in enumerate(variants) for v in ([x,gap] if i<5 else [x])])
records.append(render('00-comparison-six-variants',reel))
sources=[]
for name in ['j79','jet','metal','shed','launch','sofia','gear']:
    p=SOURCES/(name+'.mp3')
    sources.append({'file':p.name,'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),
                    'used_in_auditions':name in ['j79','jet','metal','shed','launch']})
(HERE/'manifest.json').write_text(json.dumps({'version':3,'sample_rate':RATE,'outputs':records,'sources':sources,
    'source_audio_bytes':sum(s['bytes'] for s in sources),'no_synthesized_noise_or_oscillators':True,
    'human_listening_approved':False},indent=2))
assert len(reel)/RATE<=45
assert all(r['decoded_true_peak_dbfs'] is not None and r['decoded_true_peak_dbfs']<0 for r in records)
print(json.dumps(records,indent=2))
