"""Generate original contact sounds, candidate metadata and a 9-second mix audition. No live writes."""
from pathlib import Path
import array, hashlib, json, math, random, wave, argparse

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output-root',required=True)
options=parser.parse_args()
HERE=Path(options.output_root).resolve()
AUDIO = HERE / 'Assets/_OutOfReach/Audio'
RATE = 48000

def guid(name):
    return hashlib.sha256(('OUT-OF-REACH-original-AudioMixV02/' + name).encode()).hexdigest()[:32]

def meta(path, importer, settings=''):
    Path(str(path)+'.meta').write_text('fileFormatVersion: 2\nguid: '+guid(str(path.relative_to(HERE)))+'\n'+importer+':\n  externalObjects: {}\n'+settings+'  userData: Original OUT OF REACH authored asset; not a NASA recording\n  assetBundleName: \n  assetBundleVariant: \n')

def write_wav(path, samples, channels):
    with wave.open(str(path), 'wb') as w:
        w.setnchannels(channels); w.setsampwidth(2); w.setframerate(RATE)
        w.writeframes(array.array('h', [round(max(-1, min(1, x))*32767) for x in samples]).tobytes())

folder = AUDIO/'Original/DeckContacts'
folder.mkdir(parents=True, exist_ok=True)
Path(str(folder)+'.meta').write_text('fileFormatVersion: 2\nguid: '+guid('DeckContacts')+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
settings = '''  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 48000
    compressionFormat: 0
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {}
  forceToMono: 0
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 0
'''
clips = []
evidence = {'authorship':'Original deterministic rubber-sole/deck-contact synthesis. No third-party or NASA samples.',
    'scientific_disclosure':'Cabin sound is ordinary acoustic presentation. Grounded walking in a microgravity craft is a game simplification. No free-float or EVA footsteps.',
    'human_listening_verified':False,'clips':[]}
for index, (duration, pitch, peak_db) in enumerate([(0.24, 1.0, -10.0), (0.265, .94, -10.6), (.225, 1.065, -9.6)]):
    rng = random.Random(11052026 + index)
    low = high = 0.0; raw = []
    for i in range(round(duration*RATE)):
        t=i/RATE; noise=rng.uniform(-1, 1)
        low += .032*(noise-low); high += .24*(noise-high)
        band=high-low
        attack=1-math.exp(-t/.0018)
        thud=math.sin(2*math.pi*pitch*(72*t + .9*(1-math.exp(-t/.02))))*math.exp(-t/.038)
        modes=.17*math.sin(2*math.pi*219*pitch*t)*math.exp(-t/.025) + .06*math.sin(2*math.pi*437*pitch*t)*math.exp(-t/.018)
        sole=band*2.0*math.exp(-t/.041)
        scuff=band*.35*math.exp(-max(0,t-.025)/.055)*(1-math.exp(-t/.014))
        tail=min(1.0,max(0.0,(duration-t)/.025))**2
        raw.append(attack*(.5*thud+modes+sole+scuff)*tail)
    gain=10**(peak_db/20)/max(abs(v) for v in raw)
    samples=[v*gain for v in raw];samples[0]=samples[-1]=0
    name=f'deck-contact-{index+1:02d}.wav';dest=folder/name
    write_wav(dest,samples,1);meta(dest,'AudioImporter',settings)
    clips.append(samples)
    evidence['clips'].append({'name':name,'duration_seconds':duration,'sample_rate':RATE,'channels':1,
        'peak_dbfs':peak_db,'rms_dbfs':20*math.log10(math.sqrt(sum(v*v for v in samples)/len(samples))),
        'sha256':hashlib.sha256(dest.read_bytes()).hexdigest(),'first_sample':0,'last_sample':0,'clipped_samples':0})

# This is a timed LISTENING EXAMPLE only; runtime cadence uses actual travelled distance.
with wave.open(str(AUDIO/'Music/mx02_tension.wav'),'rb') as w:
    music=array.array('h',w.readframes(9*RATE))
mix=[v/32768*.8 for v in music]
for index, start in enumerate([.55,1.0,1.45,1.9,2.35,3.5,3.85,4.2,4.55,4.9,5.25,6.5,7.0,7.5]):
    clip=clips[index%3]
    for i,v in enumerate(clip):
        frame=round(start*RATE)+i
        if frame>=9*RATE:break
        # Centred contact placement, matching the single 2D playback owner.
        mix[frame*2]+=v*.55*.8
        mix[frame*2+1]+=v*.55*.8
peak=max(abs(v) for v in mix);assert peak < 1
write_wav(HERE/'deck-and-music-audition-9s.wav',mix,2)
evidence['audition']={'file':'deck-and-music-audition-9s.wav','duration_seconds':9,
    'sample_peak_dbfs':20*math.log10(peak),'clipped_samples':0,'music_gain':.8,'step_gain':.55,'sfx_gain':.8,
    'note':'Offline listening example, not recorded Unity output or proof of runtime cadence.'}
(HERE/'footstep-verification.json').write_text(json.dumps(evidence,indent=2)+'\n')

print(json.dumps(evidence,indent=2))
