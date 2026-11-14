"""Original OUT OF REACH instrumental scores. Existing numpy/ffmpeg only; explicit staging output."""
from pathlib import Path
import argparse, hashlib, json, math, subprocess, re, wave
import numpy as np

RATE=48000
TAU=2*np.pi
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output-root',required=True)
args=parser.parse_args(); ROOT=Path(args.output_root).resolve()
MUSIC=ROOT/'Assets/_OutOfReach/Audio/Music'; MUSIC.mkdir(parents=True,exist_ok=True)
STEMS=ROOT/'Stems';STEMS.mkdir(exist_ok=True)
PREVIEWS=ROOT/'Previews';PREVIEWS.mkdir(exist_ok=True)

def hz(note): return 440*2**((note-69)/12)
def pan(x,p): return np.column_stack((x*np.sqrt((1-p)/2),x*np.sqrt((1+p)/2)))

def piano(note,duration,velocity=1):
    # Original electric-piano FM/tine model: clear pitched attacks and warm decaying bodies, not a pad.
    t=np.arange(round((duration+1.05)*RATE))/RATE; f=hz(note)
    gate=np.where(t<duration,1,np.exp(-(t-duration)/.27))
    env=(1-np.exp(-t/.003))*np.exp(-t/(.95+duration*.25))*gate
    tine=np.sin(TAU*f*t + 1.15*np.exp(-t/.16)*np.sin(TAU*f*2*t))
    body=np.sin(TAU*f*t+.08*np.sin(TAU*4.5*t))
    bell=.11*np.sin(TAU*f*3.002*t)*np.exp(-t/.23)
    x=(.64*body+.29*tine+bell)*env*velocity
    x[-min(600,len(x)):]*=np.linspace(1,0,min(600,len(x)))
    return x

def lead(note,duration,velocity=1):
    t=np.arange(round((duration+.65)*RATE))/RATE;f=hz(note)
    gate=np.where(t<duration,1,np.exp(-(t-duration)/.15))
    env=(1-np.exp(-t/.022))*np.exp(-t/2.2)*gate
    x=(np.sin(TAU*f*t+.012*np.sin(TAU*5.2*t))+.18*np.sin(TAU*f*2*t)+.045*np.sin(TAU*f*3*t))*env*velocity
    x[-600:]*=np.linspace(1,0,600)
    return x

def bass(note,duration,velocity=1):
    t=np.arange(round((duration+.25)*RATE))/RATE;f=hz(note)
    env=(1-np.exp(-t/.006))*np.exp(-t/.55)*np.where(t<duration,1,np.exp(-(t-duration)/.065))
    x=(np.sin(TAU*f*t)+.25*np.sin(TAU*f*2*t)+.08*np.sin(TAU*f*3*t))*env*velocity
    x[-400:]*=np.linspace(1,0,400)
    return x

def drum(kind,seed):
    rng=np.random.default_rng(seed)
    duration={'kick':.23,'rim':.14,'hat':.07,'brush':.17}[kind]
    t=np.arange(round(duration*RATE))/RATE;n=rng.normal(0,.35,len(t))
    if kind=='kick':
        x=np.sin(TAU*(48*t+1.6*(1-np.exp(-t/.018))))*np.exp(-t/.055)+n*.035*np.exp(-t/.009)
    elif kind=='rim':
        x=(np.sin(TAU*910*t)*.3+np.sin(TAU*1430*t)*.13+n*.5)*np.exp(-t/.018)
    elif kind=='hat':
        x=np.concatenate(([0],np.diff(n)))*np.exp(-t/.015)*.4
    else:
        x=np.concatenate(([0],np.diff(n)))*np.exp(-t/.045)*.28
    x*=1-np.exp(-t/.0008);x[-250:]*=np.linspace(1,0,250)
    return x

def add(stem,sound,at,p=0,gain=1):
    stereo=pan(sound,p)*gain;start=round(at*RATE)%len(stem)
    take=min(len(stereo),len(stem)-start);stem[start:start+take]+=stereo[:take]
    if take<len(stereo):stem[:len(stereo)-take]+=stereo[take:]

def room(stem):
    # Circular short room/delay: tails from the end are genuinely carried into the loop start.
    wet=stem.copy()
    for delay,gain in [(.087,.10),(.139,.08),(.223,.055),(.337,.035)]:
        wet+=np.roll(stem[:,::-1],round(delay*RATE),axis=0)*gain
    return wet

def write(path,samples):
    quant=np.rint(samples*32767).astype('<i2')
    with wave.open(str(path),'wb') as w:
        w.setnchannels(2);w.setsampwidth(2);w.setframerate(RATE);w.writeframes(quant.tobytes())

def measure(path):
    p=subprocess.run(['ffmpeg','-hide_banner','-nostats','-i',str(path),'-af','loudnorm=I=-20:TP=-3:LRA=11:print_format=json','-f','null','-'],capture_output=True,text=True,check=True)
    data=json.loads(re.search(r'\{\s*"input_i"[\s\S]*?\}',p.stderr).group(0))
    return {k:float(data[k]) for k in ['input_i','input_tp','input_lra']}

SCORES=[
    ('mx01_isolation','Cabin Radio - Small Blue Light',80,'calm-radio',
     [[50,57,61,64],[47,54,57,62],[43,54,57,59],[45,52,59,61]], [38,35,31,33]),
    ('mx02_tension','Signal Break - Hold the Line',100,'crisis-tension',
     [[47,54,57,61],[43,50,54,57],[40,47,54,55],[42,49,55,58]], [35,31,28,30]),
    ('mx04_recovery','Another Dawn - Recovery',90,'hopeful-recovery',
     [[50,57,61,66],[45,52,57,61],[47,54,57,62],[43,50,54,59]], [38,33,35,31])]

manifest={'authorship':'Original algorithmically performed scores, composed for OUT OF REACH. No copied melody, third-party samples or NASA recordings.',
 'music_intent':'Pitched electric piano/chord accompaniment, original melodic motifs, bass and light percussion. Distinct from the prior ambient drone.',
 'mix':{'master':1,'music':.8,'dialogue_duck_db':-8},'human_listening_approved':False,'tracks':[]}
for number,(file,title,tempo,tag,chords,roots) in enumerate(SCORES):
    beat=60/tempo;duration=64*beat;frames=round(duration*RATE)
    stems={name:np.zeros((frames,2),dtype=np.float64) for name in ['keys','melody','bass','percussion']}
    for bar in range(16):
        chord=chords[bar%4];at=bar*4
        # Rhythmic voicings and restrained upper arpeggios establish actual harmonic motion.
        strikes=[(0,1.55,.43),(2.5,.85,.29)] if tag!='crisis-tension' else [(0,.7,.34),(1.5,.45,.25),(2.5,.65,.29)]
        for onset,length,level in strikes:
            for voice,note in enumerate(chord):
                add(stems['keys'],piano(note,length*beat), (at+onset)*beat+voice*.013, -.32+voice*.18,level/len(chord))
        if bar%2==1:
            for i,note in enumerate(chord[-3:]):add(stems['keys'],piano(note+12,.32*beat), (at+3+i*.25)*beat,.24,.026)
        for onset,note in [(0,roots[bar%4]),(2,roots[bar%4]+(7 if bar%2 else 12))]:
            add(stems['bass'],bass(note,1.15*beat),(at+onset)*beat,0,.10 if tag!='crisis-tension' else .135)
        for onset in ([0,2.5] if tag=='calm-radio' else [0,2,3.5]):
            add(stems['percussion'],drum('kick',1000+bar),(at+onset)*beat,0,.065 if tag=='calm-radio' else .085)
        for onset in [1,3]:add(stems['percussion'],drum('rim',2000+bar),(at+onset)*beat,.15,.075 if tag!='crisis-tension' else .10)
        for i in range(8):
            add(stems['percussion'],drum('hat' if tag=='crisis-tension' else 'brush',3000+bar*8+i),(at+i*.5)*beat,-.18 if i%2 else .18,.045 if i%2 else .032)
        if tag=='crisis-tension':
            # Original two-note ostinato with changing harmony, not a sustained drone.
            for i in range(8):
                note=[chord[1]+12,chord[-1]+12,chord[2]+12,chord[-1]+12][i%4]
                add(stems['melody'],piano(note,.22*beat),(at+i*.5)*beat,-.12 if i%2 else .12,.075 if i%2 else .10)
            if bar%4==3:add(stems['melody'],lead([66,65,62,61][bar//4],1.3*beat),(at+2.5)*beat,.12,.045)
        else:
            motifs = [
                [(.5,66,.7),(1.5,69,.5),(2.5,73,.7),(3.5,71,.45)],
                [(.5,69,.9),(2,66,.5),(3,64,.7)],
                [(.75,66,.5),(1.5,69,.9),(3,71,.7)],
                [(.25,73,1.1),(2,69,.7),(3.25,64,.5)]] if tag=='calm-radio' else [
                [(0,69,.7),(1,73,.7),(2,74,1.35)],[(.5,73,.8),(1.75,71,.6),(3,69,.7)],
                [(0,66,.7),(1,69,.7),(2,73,.65),(3,74,.7)],[(0,71,.9),(1.5,69,.7),(2.75,66,.9)]]
            for onset,note,length in motifs[bar%4]:
                # Later phrases answer the original motif with alternate ending tones.
                if bar>=8 and onset>=3:note=62 if tag=='calm-radio' else 74
                add(stems['melody'],lead(note,length*beat),(at+onset)*beat,.08,.075 if tag=='calm-radio' else .095)
    stems={k:room(v) for k,v in stems.items()}
    total=sum(stems.values());total-=np.mean(total,axis=0)
    raw_peak=float(np.max(np.abs(total)));pre=.65/max(raw_peak,.00001)
    dest=MUSIC/(file+'.wav');write(dest,total*pre)
    before=measure(dest);gain=pre*10**((-20-before['input_i'])/20)
    gain=min(gain,10**(-3.5/20)/raw_peak)
    total*=gain;assert np.max(np.abs(total))<1
    write(dest,total);measured=measure(dest)
    # Source stems use the same gain and circular room; kept as FLAC outside the Unity asset tree.
    for name,a in stems.items():
        temp=STEMS/(file+'-'+name+'.wav');write(temp,a*gain)
        subprocess.run(['ffmpeg','-y','-hide_banner','-loglevel','error','-i',str(temp),'-c:a','flac',str(temp.with_suffix('.flac'))],check=True)
        temp.unlink()  # Explicit generated file under the assigned staging directory only.
    preview=PREVIEWS/(tag+'-15s.wav')
    preview_audio=total[:15*RATE]*.8
    preview_audio[:round(.035*RATE)]*=np.linspace(0,1,round(.035*RATE))[:,None]
    preview_audio[-round(.35*RATE):]*=np.linspace(1,0,round(.35*RATE))[:,None]
    write(preview,preview_audio)
    slopes=np.diff(total,axis=0);jump=total[0]-total[-1]
    row={'asset':str(dest.relative_to(ROOT)).replace('\\','/'),'title':title,'tag':tag,'tempo_bpm':tempo,'bars':16,
       'duration_seconds':duration,'sha256':hashlib.sha256(dest.read_bytes()).hexdigest(),'loudness':measured,
       'clipped_samples':int(np.count_nonzero(np.abs(total)>=1)),'loop_wrap_jump':[float(v) for v in jump],
       'max_internal_sample_jump':[float(v) for v in np.max(np.abs(slopes),axis=0)],
       'wrap_slope_mismatch':[float(v) for v in slopes[0]-slopes[-1]],
       'preview':str(preview.relative_to(ROOT)).replace('\\','/'),
       'stems':['Stems/'+file+'-'+k+'.flac' for k in stems]}
    row['numerical_seam_pass']=all(abs(jump[c])<=np.max(np.abs(slopes[:,c]))*1.05 for c in range(2))
    manifest['tracks'].append(row);print(json.dumps(row),flush=True)
# Emergency retains its canonical meaning/GUID; this bounded delivery shares the new crisis arrangement.
import copy, shutil
shutil.copyfile(MUSIC/'mx02_tension.wav', MUSIC/'mx03_emergency.wav')
emergency=copy.deepcopy(manifest['tracks'][1])
emergency.update(asset='Assets/_OutOfReach/Audio/Music/mx03_emergency.wav',title='Signal Break - Emergency',tag='emergency-crisis',arrangement_shared_with='mx02_tension')
manifest['tracks'].insert(2,emergency)
manifest['phase_policy']='Isolation=calm cabin/radio; Tension=EVA/transmission/repair tension; Emergency=actual crisis; Recovery (new ordinal 4)=genuine recovered ending. Mere Earth contact is not recovery.' 
(ROOT/'composition-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
