"""Original, deterministic cabin/contact/interface cues; Python standard library only."""
from pathlib import Path
import array
import hashlib
import json
import math
import random
import sys
import wave

ROOT = Path(__file__).resolve().parent
RATE = 48000
PEAK = 10**(-10/20)


def write(name, samples):
    samples = list(samples)
    mean = sum(samples)/len(samples)
    samples = [x-mean for x in samples]
    for i in range(len(samples)):
        fade = min(1., i/(RATE*.012), (len(samples)-1-i)/(RATE*.018))
        samples[i] *= max(0, fade)
    maximum = max(abs(x) for x in samples)
    pcm = array.array('h', (round(x/maximum*PEAK*32767) for x in samples))
    if sys.byteorder != 'little': pcm.byteswap()
    path = ROOT/'Audio'/name
    path.parent.mkdir(exist_ok=True)
    with wave.open(str(path),'wb') as out:
        out.setnchannels(1); out.setsampwidth(2); out.setframerate(RATE); out.writeframes(pcm.tobytes())
    with wave.open(str(path),'rb') as source:
        assert (source.getnchannels(),source.getsampwidth(),source.getframerate()) == (1,2,RATE)
        values=array.array('h',source.readframes(source.getnframes()))
        if sys.byteorder != 'little': values.byteswap()
    peak=max(abs(x) for x in values)/32768
    assert peak<=10**(-8/20) and len(values)/RATE<=1.2
    assert values[0]==values[-1]==0
    return {'filename':str(path.relative_to(ROOT)).replace('\\','/'),'duration_seconds':len(values)/RATE,
            'channels':1,'sample_rate_hz':RATE,'pcm_bits':16,'peak_dbfs':20*math.log10(peak),
            'rms_dbfs':20*math.log10(math.sqrt(sum((x/32768)**2 for x in values)/len(values))),
            'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}


rng=random.Random(740317)
motor=[]
filtered=0.
for i in range(round(1.05*RATE)):
    t=i/RATE
    filtered=.92*filtered+.08*rng.uniform(-1,1)
    window=math.sin(math.pi*min(1,max(0,(t-.06)/.86)))**2 if .06<t<.92 else 0
    phase=2*math.pi*(145*t+25*t*t)
    value=window*(.18*math.sin(phase)+.06*math.sin(2.04*phase)+.12*filtered)
    for start in (.026,.925):
        dt=t-start
        if dt>=0:
            value+=math.exp(-dt/.022)*(.35*math.sin(2*math.pi*190*dt)+.16*math.sin(2*math.pi*1370*dt))
    motor.append(value)
chirp=[]
for i in range(round(.26*RATE)):
    t=i/RATE
    window=math.sin(math.pi*t/.26)**2
    value=window*(math.sin(2*math.pi*(620*t+160*t*t))+.18*math.sin(2*math.pi*1240*t))
    chirp.append(value)
report={'result':'PASS','creator':'OUT OF REACH, original mathematical synthesis, 2026-11-14',
        'source':'No recordings, voice model, sample library or external assets used.',
        'synthesis':'Sines, deterministic low-pass noise and smooth envelopes; seed 740317; normalized to -10 dBFS peak.',
        'use_limit':'Cabin mechanism/contact-transmitted sound or explicit suit/intercom feedback. Never exterior airborne space sound.',
        'listening_and_Unity_playback':'NOT VERIFIED',
        'files':[write('airlock-latch-motor.wav',motor),write('airlock-safe-chirp.wav',chirp)]}
(ROOT/'audio-provenance.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,indent=2))
