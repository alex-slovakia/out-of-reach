"""Private OUT OF REACH auditions. Existing NumPy + FFmpeg only; never edits Unity Assets."""
from pathlib import Path
import argparse, hashlib, json, re, subprocess, wave
import numpy as np

RATE = 48000
TAU = 2 * np.pi
HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[2]
STAGE = PROJECT / '.local/prologue-audio-20261115'
STAGE.mkdir(parents=True, exist_ok=True)

def ff(*args):
    return subprocess.run(['ffmpeg', '-hide_banner', '-nostdin', '-y', *map(str,args)], check=True, capture_output=True)

def decode(path, seconds=None, start=0):
    args=['-ss', str(start), '-i', str(path)]
    if seconds is not None: args += ['-t', str(seconds)]
    return np.frombuffer(ff(*args,'-ar',RATE,'-ac',2,'-f','f32le','pipe:1').stdout,dtype='<f4').reshape(-1,2).copy()

def rms(x): return float(np.sqrt(np.mean(np.square(x,dtype=np.float64))))
def unity(x): return x / max(rms(x),1e-9)
def fade(n,attack,release):
    t=np.arange(n,dtype=np.float32)/RATE
    return np.minimum(t/max(attack,.001),1)**1.6 * np.minimum((n/RATE-t)/max(release,.001),1)**1.6
def pan(x,p=0): return np.column_stack((x*np.sqrt((1-p)/2),x*np.sqrt((1+p)/2)))
def noise(seconds,lo,hi,seed):
    n=round(seconds*RATE); rng=np.random.default_rng(seed)
    freq=np.fft.rfftfreq(n,1/RATE)
    spec=np.fft.rfft(rng.standard_normal(n).astype(np.float32))
    shape=(1-np.exp(-(freq/max(lo,1))**4))*np.exp(-(freq/hi)**4)
    x=np.fft.irfft(spec*shape,n).astype(np.float32)
    return unity(x)
def wrap(x,seconds=6):
    n=round(seconds*RATE); w=(.5-.5*np.cos(np.linspace(0,np.pi,n))).astype(np.float32)[:,None]
    # Equal-gain tail/head bridge, followed by the untouched interior. Wrap is consecutive source samples.
    return np.concatenate((x[-n:]*(1-w)+x[:n]*w,x[n:-n]))
def rumble(seconds,seed,strength=1):
    t=np.arange(round(seconds*RATE),dtype=np.float32)/RATE
    slow=.76+.12*np.sin(TAU*t/17)+.08*np.sin(TAU*t/7.3)
    low=noise(seconds,22,125,seed)*slow
    mid=noise(seconds,100,780,seed+1)*(.54+.18*np.sin(TAU*t/11+.4))
    vibration=np.sin(TAU*(38*t+.22*np.sin(TAU*.11*t)))+.22*np.sin(TAU*77.1*t)
    return strength*(pan(low,.02)*.065+pan(mid,-.25)*.011+pan(vibration,.05)*.025)
def original_score(seconds):
    n=round(seconds*RATE); t=np.arange(n,dtype=np.float32)/RATE
    out=rumble(seconds,801,1.1)
    # Slow D-minor/add9, Bb-major/add9, G-minor/add9, A-suspended movement; no percussion.
    chords=[(38,45,50,57,64),(34,41,46,53,60),(31,38,43,50,57),(33,40,45,52,59)]
    for idx,start in enumerate(np.arange(-2,seconds,16)):
        for k,note in enumerate(chords[idx%4]):
            length=20.; nn=round(length*RATE); tt=np.arange(nn,dtype=np.float32)/RATE
            f=440*2**((note-69)/12)
            voice=np.zeros(nn,dtype=np.float32)
            for detune,weight in [(-.002,.3),(0,.45),(.0023,.25)]:
                phase=TAU*f*(1+detune)*tt + .045*np.sin(TAU*4.4*tt+k)
                for harmonic in range(1,7):
                    voice += weight*np.sin(phase*harmonic)/(harmonic**1.8)
            voice *= fade(nn,4.0,6.0)*(.65+.12*np.sin(TAU*.13*tt+k))
            left=max(0,round(start*RATE)); offset=max(0,-round(start*RATE)); count=min(n-left,nn-offset)
            if count>0: out[left:left+count] += pan(voice[offset:offset+count],(k-2)*.24)*(.028 if k<2 else .018)
    # Wide metallic swells; slowly developing, no impact or drum ending.
    sheen=noise(seconds,380,2200,886)
    breath=(.5+.5*np.sin(TAU*t/24-.7))**3
    out+=pan(sheen*breath,.65)*.008
    out+=pan(np.roll(sheen,round(.073*RATE))*breath,-.65)*.007
    return out
def write(path,x):
    peak=float(np.max(np.abs(x)))
    if peak>.97:x=x*(.97/peak)
    with wave.open(str(path),'wb') as w:
        w.setnchannels(2);w.setsampwidth(2);w.setframerate(RATE)
        w.writeframes(np.rint(x*32767).astype('<i2').tobytes())
def measured(path):
    result=ff('-i',path,'-af','loudnorm=I=-16:TP=-1.5:LRA=9:print_format=json','-f','null','-').stderr.decode()
    data=json.loads(re.search(r'\{\s*"input_i"[\s\S]*?\}',result).group())
    return {k:float(data[k]) for k in ['input_i','input_tp','input_lra','input_thresh']}
def export(name,x,kind,description,source):
    raw=STAGE/(name+'-raw.wav');write(raw,x)
    target=-16 if kind=='music' else -18
    levels=measured(raw)
    normalize=f"loudnorm=I={target}:TP=-1.5:LRA=9:measured_I={levels['input_i']}:measured_TP={levels['input_tp']}:measured_LRA={levels['input_lra']}:measured_thresh={levels['input_thresh']}:linear=true"
    master=STAGE/(name+'-master.wav')
    ff('-i',raw,'-af',normalize,'-ar',RATE,'-c:a','pcm_s16le',master)
    if kind=='music':
        preview_filters='atrim=duration=80,afade=t=in:st=0:d=1.2,afade=t=out:st=77:d=3'
    else:preview_filters='anull'
    out=HERE/(name+'.mp3')
    ff('-i',master,'-af',preview_filters,'-ar',RATE,'-c:a','libmp3lame','-b:a','192k',out)
    decoded=decode(out)
    probe=json.loads(subprocess.run(['ffprobe','-v','error','-show_entries','format=duration,size','-of','json',str(out)],capture_output=True,text=True,check=True).stdout)
    entry={'file':out.name,'kind':kind,'direction':description,'source':source,'duration_seconds':float(probe['format']['duration']),
           'bytes':int(probe['format']['size']),'sha256':hashlib.sha256(out.read_bytes()).hexdigest(),
           'post_encode_levels':measured(out),'decoded_peak':float(np.max(np.abs(decoded))),
           'decoded_samples_at_or_over_full_scale':int(np.sum(np.abs(decoded)>=1)),
           'pcm_master':str(master),'master_seconds':len(x)/RATE,
           'subjective_listening':'Not claimed; awaiting Alex approval','runtime_integrated':False}
    if kind=='music':
        core=decode(master); seam=float(np.max(np.abs(core[0]-core[-1])))
        entry['loop_boundary_sample_step']=seam
        entry['continuation']='120s continuous master without preview fades. Loop candidate; final musical seam listening and scene ducking remain to verify.'
    print(json.dumps({k:entry[k] for k in ['file','duration_seconds','post_encode_levels','decoded_samples_at_or_over_full_scale']}),flush=True)
    return entry

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--titan-source',type=Path,required=True)
    args=parser.parse_args()
    if hashlib.sha256(args.titan_source.read_bytes()).hexdigest()!='ce6d890556829b17152deb6216ceffa314469c09d858f07d0d302d81f2b48313':
        raise RuntimeError('Titan source hash does not match the approved licensed original')
    cobalt=PROJECT/'Assets/_OutOfReach/Audio/Music/mx01_isolation.wav'
    if hashlib.sha256(cobalt.read_bytes()).hexdigest()!='c9ad3c3db8446e3535f7f3a08a1c8cc230d29f37d0070f51ea50b0b75b048e0b':
        raise RuntimeError('Cobalt source differs from approved loop')
    entries=[]
    # Approved Titan phrase only: avoid introducing the later unwanted percussive ending.
    phrase=wrap(decode(args.titan_source,25,10.9),5)
    titan=np.tile(phrase,(6,1)); titan=unity(titan)*.13+wrap(rumble(126,101,1.35))
    entries.append(export('01-titan-deep-launch',titan,'music','Orchestral lift with deep continuous engine pressure; strongest cinematic option.',
        "Titan by Scott Buckley, CC BY 4.0, approved 10.9–35.9s phrase, 5s crossfade/20s phrase loop, original synthesized rumble added."))
    base=decode(cobalt); mix=unity(base)*.095+wrap(rumble(126,303,1.9))
    entries.append(export('02-cobalt-pressure',mix,'music','Slower, darker pressure under the approved Cobalt theme; wide continuous low roar.',
        'Cobalt by Scott Buckley, CC BY 4.0; approved 120s game loop with original synthesized rumble added.'))
    entries.append(export('03-threshold-original',wrap(original_score(126)),'music','Original deep harmonic tension: evolving minor chords, metallic air and sub pressure, no drums.',
        'Original deterministic synthesis by the OUT OF REACH team/Codex; no samples or third-party music.'))
    # Earth atmospheric launch: turbo-pump rise, ignition, turbulent engine sustain, smooth tail.
    seconds=18.;t=np.arange(round(seconds*RATE),dtype=np.float32)/RATE
    ramp=np.clip((t-.6)/4,0,1)**1.3
    engine=rumble(seconds,501,3.0)*ramp[:,None]
    engine+=pan(noise(seconds,180,2900,502)*(1-np.exp(-t/2)),-.1)*.075
    spin=np.sin(TAU*(55*t+105*(t-2+2*np.exp(-t/2))))
    engine+=pan(spin*(1-np.exp(-t/1.8))*np.exp(-t/7),.12)*.025
    engine*=fade(len(t),.25,2.5)[:,None]
    entries.append(export('sfx01-ignition-engine',engine,'sfx','18s ignition ramp into sustained Earth launch roar; no music.',
        'Original deterministic synthesis; Earth atmospheric launch sound, not vacuum propagation.'))
    seconds=7.;t=np.arange(round(seconds*RATE),dtype=np.float32)/RATE
    shock=pan(np.sin(TAU*(29*t+3.8*(1-np.exp(-t/.07))))*np.exp(-t/.95))*0.48
    shock+=pan(noise(seconds,45,1400,611)*np.exp(-t/1.15),-.2)*.25
    shock+=pan(noise(seconds,700,6500,612)*np.exp(-t/.055),.2)*.12
    shock+=pan(noise(seconds,25,180,613)*np.exp(-t/2.1),.1)*.14
    shock*=fade(len(t),.004,.7)[:,None]
    entries.append(export('sfx02-opening-shockwave',shock,'sfx','Low shock front, short crack and decaying structural rumble; no added drum.',
        'Original deterministic synthesis; atmospheric shockwave on Earth OR suit/ship-contact impact in space, never sound crossing vacuum.'))
    seconds=6.;t=np.arange(round(seconds*RATE),dtype=np.float32)/RATE
    pulse=np.zeros(len(t),dtype=np.float32)
    for a,b in [(0.2,.55),(1.7,2.35),(3.5,4.7)]:
        pulse+=np.clip((t-a)/.015,0,1)*np.clip((b-t)/.08,0,1)
    jet=pan(noise(seconds,450,5200,701)*pulse,-.15)*.12
    jet+=pan(noise(seconds,70,450,702)*pulse,.15)*.12
    jet+=pan(np.sin(TAU*122*t)*pulse)*.025
    jet+=np.roll(jet,round(.075*RATE))*.15
    entries.append(export('sfx03-thruster-contact',jet,'sfx','Three short valve/jet pulses, increasing duration; machinery and transmitted pressure.',
        'Original deterministic synthesis; cabin/suit/contact-transmitted thruster cue only in space. This does not add player thrusters or physics.'))
    (HERE/'manifest.json').write_text(json.dumps({'sample_rate':RATE,'channels':2,'generated_by':'build_previews.py','audition_only':True,'music_preview_seconds':80,'music_master_seconds':120,'no_added_drums':True,'items':entries},indent=2),encoding='utf-8')

if __name__=='__main__':main()
