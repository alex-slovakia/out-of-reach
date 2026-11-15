"""V02 private auditions: existing NumPy and FFmpeg only. No Unity assets modified."""
from pathlib import Path
import hashlib, json, re, subprocess, wave
import numpy as np

RATE = 48000
TAU = 2 * np.pi
HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[3]
STAGE = PROJECT / '.local/prologue-audio-20261115/v02'
STAGE.mkdir(parents=True, exist_ok=True)
TITAN = PROJECT.parent.parent / '.local/music-staging/sb_titan.mp3'

def ff(*args):
    return subprocess.run(['ffmpeg','-hide_banner','-nostdin','-y',*map(str,args)], check=True, capture_output=True)

def decode(path, rate=RATE):
    return np.frombuffer(ff('-i',path,'-ar',rate,'-ac',2,'-f','f32le','pipe:1').stdout,dtype='<f4').reshape(-1,2).copy()

def pan(x,p=0):
    return np.column_stack((x*np.sqrt((1-p)/2),x*np.sqrt((1+p)/2))).astype(np.float32)

def put(out,x,start,gain=1,p=0):
    offset=round(start*RATE)
    if x.ndim==1: x=pan(x,p)
    count=min(len(x),len(out)-offset)
    if count>0: out[offset:offset+count]+=x[:count]*gain

def drum(freq=55,length=2.4,weight=1):
    """Membrane modes and pitch relaxation, with no noise-based attack."""
    t=np.arange(round(length*RATE),dtype=np.float32)/RATE
    attack=1-np.exp(-t/.0035)
    phase=TAU*(freq*t+freq*.065*(1-np.exp(-t/.045)))
    body=np.sin(phase)*np.exp(-t/.62)
    body+=.31*np.sin(phase*1.57+.2)*np.exp(-t/.26)
    body+=.17*np.sin(phase*2.12)*np.exp(-t/.10)
    body+=.09*np.sin(TAU*freq*4.21*t)*np.exp(-t/.025)
    return np.tanh(body*1.25)*attack*weight*np.minimum((length-t)/.15,1)

def modal_click(length=.28,base=290):
    t=np.arange(round(length*RATE),dtype=np.float32)/RATE
    x=np.zeros(len(t),dtype=np.float32)
    for f,a,decay in [(base,1,.027),(base*1.61,.45,.016),(base*2.37,.19,.009)]:
        x+=a*np.sin(TAU*f*t)*np.exp(-t/decay)
    return x*(1-np.exp(-t/.0015))

def room(x,delays=((.061,.13),(.113,.09),(.197,.05))):
    out=x.copy()
    for delay,gain in delays:
        n=round(delay*RATE)
        out[n:,0]+=x[:-n,1]*gain
        out[n:,1]+=x[:-n,0]*gain
    return out

def low_texture(seconds,seed=100):
    n=round(seconds*RATE)
    rng=np.random.default_rng(seed)
    f=np.fft.rfftfreq(n,1/RATE)
    spectrum=np.fft.rfft(rng.standard_normal(n))
    shape=(1-np.exp(-(f/26)**4))*np.exp(-(f/120)**6)
    x=np.fft.irfft(spectrum*shape,n).astype(np.float32)
    return x/max(float(np.sqrt(np.mean(x*x))),1e-9)

def music(source):
    # Continuous licensed crescendo, not a repeated intro or an abruptly replaced track.
    start=48.; seconds=84.
    out=source[round(start*RATE):round((start+seconds)*RATE)].copy()
    assert len(out)==round(seconds*RATE), 'Source too short for selected passage'
    t=np.arange(len(out),dtype=np.float32)/RATE
    out*=np.interp(t,[0,20,45,64,84],[.78,.86,.96,1,1])[:,None]
    # Sparse low membrane accents become a measured 80 BPM pulse and tom response.
    # Phase follows prominent low-frequency transients at source 109.64/113.77/115.64s.
    for beat in np.arange(24.14,79,.75):
        k=round((beat-24.14)/.75)
        if beat<42 and k%4: continue
        if beat<60 and k%2: continue
        gain=np.interp(beat,[24,42,60,79],[.014,.022,.033,.045])
        put(out,drum(47 if k%4==0 else 57),beat,gain,p=(-.13 if k%2 else .13))
        if beat>=63 and k%4==2:
            put(out,drum(84,1.4),beat+.375,gain*.6,p=-.28)
        if beat>=72 and k%4==3:
            put(out,drum(108,1),beat+.375,gain*.38,p=.3)
    for onset in [0.15,24.14,48.14,60.14,72.14]:
        put(out,drum(33,4.2),onset,.04 if onset>=48 else .024)
    # Mechanical support stays below the music, with no broadband whoosh.
    vibration=(np.sin(TAU*(35*t+.10*np.sin(TAU*.21*t)))+.2*np.sin(TAU*70.2*t))
    out+=pan(vibration,.04)*.0025*(.8+.2*np.sin(TAU*t/19))[:,None]
    out*=np.minimum(t/.45,1)[:,None]
    return out

def engine():
    seconds=14.; t=np.arange(round(seconds*RATE),dtype=np.float32)/RATE
    power=np.interp(t,[0,.4,3,9.5,11,14],[0,.07,1,1,.75,0])
    rpm=TAU*(38*t+5*(t-2+2*np.exp(-t/2)))
    x=np.sin(rpm)+.37*np.sin(rpm*2+.12)+.17*np.sin(rpm*3.03)
    x*=.83+.07*np.sin(TAU*6.3*t)+.05*np.sin(TAU*10.7*t)
    x+=.10*np.sin(TAU*137*t+.3*np.sin(TAU*2.3*t))
    x+=.025*low_texture(seconds)
    out=pan(np.tanh(x*.8)*power,.02)*.22
    put(out,modal_click(.3,215),.08,.08,-.24)
    put(out,drum(61,1.5),.36,.07)
    put(out,modal_click(.3,185),10.6,.045,.18)
    return room(out)

def shockwave():
    out=np.zeros((round(7*RATE),2),dtype=np.float32)
    put(out,drum(31,5.4),.16,.32)
    put(out,drum(74,2.5),.168,.12,-.11)
    put(out,modal_click(.3,176),.155,.095,.05)
    # Secondary structural responses, not a sustained air blast in vacuum.
    put(out,drum(102,1.2),.23,.035,.28)
    put(out,drum(61,2.2),.37,.025,-.25)
    return room(out,((.038,.19),(.092,.12),(.171,.07),(.29,.04)))

def thruster():
    out=np.zeros((round(7*RATE),2),dtype=np.float32)
    for start,duration,p in [(1,.24,-.2),(2.6,.40,.2),(4.4,.65,-.1)]:
        t=np.arange(round(duration*RATE),dtype=np.float32)/RATE
        env=np.minimum(t/.024,1)*np.minimum((duration-t)/.06,1)
        body=(np.sin(TAU*59*t)+.28*np.sin(TAU*119*t)+.13*np.sin(TAU*181*t))*env
        put(out,body,start,.15,p)
        put(out,modal_click(.17,260),start,.045,p)
        put(out,modal_click(.20,205),start+duration-.025,.032,-p)
        put(out,drum(43,.8),start+duration,.026,p)
    return room(out,((.037,.08),(.079,.04)))

def write(path,x):
    peak=float(np.max(np.abs(x)))
    if peak>.97: x=x*.97/peak
    with wave.open(str(path),'wb') as w:
        w.setnchannels(2);w.setsampwidth(2);w.setframerate(RATE)
        w.writeframes(np.rint(x*32767).astype('<i2').tobytes())

def measure(path):
    result=ff('-i',path,'-af','loudnorm=I=-16:TP=-2:LRA=12:print_format=json','-f','null','-').stderr.decode()
    d=json.loads(re.search(r'\{\s*"input_i"[\s\S]*?\}',result).group())
    return {k:float(d[k]) for k in ['input_i','input_tp','input_lra','input_thresh']}

def export(name,x,kind,direction):
    raw=STAGE/(name+'-raw.wav'); write(raw,x)
    m=measure(raw); target=-16 if kind=='music' else -20
    filt=f"loudnorm=I={target}:TP=-2:LRA=12:measured_I={m['input_i']}:measured_TP={m['input_tp']}:measured_LRA={m['input_lra']}:measured_thresh={m['input_thresh']}:linear=true"
    master=STAGE/(name+'-master.wav')
    ff('-i',raw,'-af',filt,'-ar',RATE,'-c:a','pcm_s16le',master)
    output=HERE/(name+'.mp3')
    ff('-i',master,'-af','afade=t=out:st=81.5:d=2.5' if kind=='music' else 'anull','-ar',RATE,'-c:a','libmp3lame','-b:a','192k',output)
    y=decode(output); spec=np.fft.rfft(y.mean(axis=1)); freq=np.fft.rfftfreq(len(y),1/RATE)
    high_share=float(np.sum(np.abs(spec[freq>2500])**2)/np.sum(np.abs(spec)**2))
    return dict(file=output.name,kind=kind,direction=direction,duration_seconds=len(y)/RATE,
        bytes=output.stat().st_size,sha256=hashlib.sha256(output.read_bytes()).hexdigest(),
        post_encode_levels=measure(output),decoded_peak=float(np.max(np.abs(y))),
        decoded_samples_at_or_over_full_scale=int(np.sum(np.abs(y)>=1)),
        spectral_energy_above_2500hz_fraction=high_share,pcm_master=str(master))

def main():
    expected='ce6d890556829b17152deb6216ceffa314469c09d858f07d0d302d81f2b48313'
    actual=hashlib.sha256(TITAN.read_bytes()).hexdigest()
    assert actual==expected, f'Unverified Titan source: {actual}'
    source=decode(TITAN)
    items=[]
    for name,x,kind,description in [
        ('opening-titan-ascent-v02',music(source),'music','84s continuous Titan crescendo, original deep membrane accents becoming drums in final third; no repeated intro phrase'),
        ('sfx01-engine-structure-v02',engine(),'sfx','14s ignition, rising pump/engine vibration, sustained power, controlled rundown; cabin/contact perspective'),
        ('sfx02-shockwave-structure-v02',shockwave(),'sfx','7s low structural impact and resonant decay; no hiss tail; impact received through spacecraft structure'),
        ('sfx03-thruster-contact-v02',thruster(),'sfx','7s three valve/pressure pulses from cabin/contact perspective; no player movement system implied')]:
        items.append(export(name,x,kind,description))
        print(json.dumps(items[-1]))
    result=dict(version='v02',audition_only=True,sample_rate=RATE,channels=2,
        generated_by='build_previews.py',subjective_listening='Not claimed; user listening and approval pending',
        source=dict(title='Titan',artist='Scott Buckley',license='CC BY 4.0',
            url='https://www.scottbuckley.com.au/library/titan/',license_url='https://creativecommons.org/licenses/by/4.0/',
            local_path=str(TITAN),sha256=actual,decoded_seconds=len(source)/RATE,
            header_duration_seconds=175.804082,excerpt_seconds=[48,132],
            modifications='Continuous excerpt, gentle gain staging, original synthesized percussion and very quiet tonal vibration, mastering, 2.5s audition fade'),
        original_sfx='Original deterministic NumPy synthesis for OUT OF REACH; no third-party recordings or film audio',
        physical_context='Music is nondiegetic. SFX are cabin/contact-transmitted stylizations, not sound propagating through external vacuum.',
        integration='No runtime or Unity assets changed. Master has no audition fade; current excerpt is not a seamless loop. Runtime transition/ducking requires separate integration.',items=items)
    (HERE/'manifest.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')

if __name__=='__main__': main()
