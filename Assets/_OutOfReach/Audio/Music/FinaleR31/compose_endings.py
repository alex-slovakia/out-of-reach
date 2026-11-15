"""Two original offline synth-score auditions; no samples, APIs or game changes."""
from pathlib import Path
import hashlib, json, math, re, subprocess, wave
import numpy as np

OUT = Path(__file__).resolve().parent
SR = 48000
RNG = np.random.default_rng(3114)

def envelope(t, length, attack, release):
    return np.sin(np.minimum(t / attack, 1) * np.pi / 2) ** 2 * np.sin(np.clip((length-t)/release, 0, 1)*np.pi/2)**2

def instrument(note, length, kind):
    t = np.arange(round(length*SR))/SR
    f = 440*2**((note-69)/12)
    phase = 2*np.pi*f*t
    if kind == 'piano':
        y = np.zeros(len(t))
        for k in range(1,9):
            y += (1/k**1.6)*np.sin(phase*k*np.sqrt(1+.00013*k*k)+.04*k)*np.exp(-t*(.6+.34*k))
        y *= envelope(t,length,.007,.5)
    elif kind == 'strings':
        y = np.zeros(len(t))
        for cents in [-6,-2,3,7]:
            p = phase*2**(cents/1200) + .06*np.sin(2*np.pi*4.9*t+cents)
            for k in range(1,7):
                y += np.sin(p*k+.07*cents)/(4*k**1.65)
        y *= envelope(t,length, min(1.2,length/4), min(1.8,length/3))
    elif kind == 'horn':
        env = envelope(t,length,.26,.65)
        y = (np.sin(phase+.04*np.sin(2*np.pi*4.4*t))+.28*np.sin(2*phase)+.13*np.sin(3*phase)+.05*np.sin(4*phase))*env
    else:
        y = (np.sin(phase)+.24*np.sin(2*phase))*envelope(t,length,.08,.45)*np.exp(-t*.5)
    return y

def place(mix, t, note, length, gain, kind='piano', pan=0):
    y = instrument(note,length,kind)*gain
    start = round(t*SR)
    n = min(len(y),len(mix)-start)
    if n <= 0: return
    angle = (pan+1)*np.pi/4
    mix[start:start+n,0] += y[:n]*np.cos(angle)
    mix[start:start+n,1] += y[:n]*np.sin(angle)

def drum(mix,start,gain):
    t = np.arange(SR*2)/SR
    y = np.sin(2*np.pi*(44*t+22*.055*(1-np.exp(-t/.055))))*np.exp(-t*4)
    y += RNG.normal(0,1,len(t))*.025*np.exp(-t*23)
    y *= envelope(t,2,.006,.1)*gain
    i=round(start*SR); n=min(len(y),len(mix)-i)
    mix[i:i+n] += y[:n,None]*.707

def render(name,mix,description):
    # Diffuse stereo tail, built from original audio only; independent early reflections.
    dry=mix.copy()
    for delay,gain in [(.071,.12),(.113,.10),(.179,.09),(.263,.08),(.397,.075),(.587,.065),(.821,.05),(1.117,.04),(1.571,.03),(2.071,.02)]:
        k=round(delay*SR)
        mix[k:] += dry[:-k,::-1]*gain
    # Warm high frequency rolloff and DC/sub-bass rejection.
    freq=np.fft.rfftfreq(len(mix),1/SR)
    filt=(freq/np.maximum(freq,28))**2/(1+(freq/6500)**4)
    for c in range(2): mix[:,c]=np.fft.irfft(np.fft.rfft(mix[:,c])*filt,n=len(mix))
    fade=round(3.5*SR)
    mix[-fade:] *= np.linspace(1,0,fade)[:,None]**1.5
    mix[:round(.05*SR)] *= np.linspace(0,1,round(.05*SR))[:,None]
    mix *= .68/max(np.abs(mix).max(),1e-9)
    assert np.isfinite(mix).all()
    pcm=np.round(mix*32767).astype('<i2')
    path=OUT/(name+'.wav')
    with wave.open(str(path),'wb') as w:
        w.setnchannels(2);w.setsampwidth(2);w.setframerate(SR);w.writeframes(pcm.tobytes())
    measure=subprocess.run(['ffmpeg','-hide_banner','-nostats','-i',str(path),'-af','ebur128=peak=true','-f','null','-'],capture_output=True,check=True).stderr.decode(errors='replace').rsplit('Summary:',1)[-1]
    report={'title':name,'description':description,'authorship':'Original notes, arrangement and procedural synthesis authored by Codex for OUT OF REACH. No external samples, borrowed composition or generative audio API.','duration_seconds':len(mix)/SR,'sample_rate':SR,'channels':2,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'clipped_samples':int(np.sum(np.abs(pcm.astype(np.int32))>=32767)),'loudness_measurement':measure.strip(),'user_approved':False,'listening_verified':False,'game_wiring_changed':False}
    path.with_suffix('.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    subprocess.run(['ffmpeg','-y','-hide_banner','-loglevel','error','-i',str(path),'-codec:a','libmp3lame','-q:a','2',str(path.with_suffix('.mp3'))],check=True)
    print(json.dumps(report),flush=True)

def victory():
    beat=60/80; bar=beat*4
    mix=np.zeros((SR*57,2))
    # D minor suspense opens into D major at the docking beat, then a full final cadence.
    chords=[[50,57,65,69],[46,53,62,65],[43,50,58,65],[45,52,61,67],
            [50,57,66,69],[47,54,62,66],[43,50,59,66],[45,52,61,67],
            [50,57,66,69],[47,54,62,66],[43,50,59,66],[45,52,61,67],
            [43,50,59,66],[45,52,61,67],[50,57,66,69],[50,57,66,74]]
    for b,chord in enumerate(chords):
        t=1+b*bar
        strength=.28 if b<4 else .60 if b<12 else .75
        for j,n in enumerate(chord):
            place(mix,t,n,bar+1.6,.045*strength,'strings',-.7+j*.45)
            place(mix,t+.03*j,n,3,.055 if b<4 else .07,'piano',-.4+j*.23)
        place(mix,t,chord[0]-12,bar+.3,.08*strength,'bass')
        if 4<=b<14:
            drum(mix,t,.055*strength)
            for step in range(8):
                place(mix,t+step*beat/2,chord[[0,1,2,1,3,2,1,2][step]]+12,1.5,.025,'piano',(-1)**step*.35)
        if b in (4,8,12,14):
            for off,n,dur in [(0,69,1.4),(1.5,74,1.1),(2.75,78,2.5)]:
                place(mix,t+off*beat,n,dur,.09 if b<14 else .065,'horn',.1)
    render('Thirty-One-Dawns-v1',mix,'57-second victory audition: intimate piano, ascending three-note synth-brass motif, warm string pads, restrained low drums, D-minor to D-major release and a held ending.')

def loss():
    mix=np.zeros((SR*45,2))
    chords=[[38,45,53,64],[34,41,50,57],[31,38,46,57],[33,40,49,55],[38,45,53,64]]
    melody=[(1.4,76,3),(4.5,74,4),(9.2,69,4),(13,65,4),(17.4,67,3),(20.8,65,4),(25.5,64,4),(29,61,4),(34.5,62,6)]
    for b,chord in enumerate(chords):
        t=b*7+.5
        for j,n in enumerate(chord):
            place(mix,t,n,9,.038,'strings',-.65+j*.4)
        place(mix,t,chord[0],6,.09,'piano',-.2)
        place(mix,t+2.1,chord[1]+12,5,.045,'piano',.25)
    for t,n,d in melody: place(mix,t,n,d,.12 if t<30 else .08,'piano',.12)
    for i,t in enumerate([2,5,8,11.5,15,19,23.5]): drum(mix,t,.022*(1-i/9))
    place(mix,35,50,7,.035,'strings',-.15)
    render('Last-Carrier-v1',mix,'45-second loss audition: sparse descending felt-style synthesized piano, low bowed-style pads, fading pulse and unresolved suspended ending. No vocals or shock effects.')

victory()
loss()
