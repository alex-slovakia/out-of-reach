from pathlib import Path
import json, subprocess
import numpy as np
HERE=Path(__file__).resolve().parent
RATE=48000
rows=[]
svg=['<svg xmlns="http://www.w3.org/2000/svg" width="1100" height="1050" viewBox="0 0 1100 1050">',
     '<rect width="1100" height="1050" fill="#11171d"/>',
     '<text x="30" y="30" fill="#eee" font-size="18">V03 recorded-source auditions: frequency energy (30 Hz–12 kHz, log axis)</text>']
for index,p in enumerate(sorted(HERE.glob('0[1-6]-*.mp3'))):
    r=subprocess.run(['ffmpeg','-v','error','-i',str(p),'-ar',str(RATE),'-ac','2','-f','f32le','pipe:1'],capture_output=True,check=True)
    x=np.frombuffer(r.stdout,dtype='<f4').reshape(-1,2)
    size=4096; mono=x.mean(axis=1); frames=np.stack([mono[i:i+size] for i in range(0,len(mono)-size,size//2)])
    power=np.mean(abs(np.fft.rfft(frames*np.hanning(size),axis=1))**2,axis=0)
    f=np.fft.rfftfreq(size,1/RATE); total=power.sum()
    bands={f'{a}-{b} Hz':float(power[(f>=a)&(f<b)].sum()/total) for a,b in [(0,80),(80,250),(250,1500),(1500,4000),(4000,24001)]}
    peak=float(abs(x).max())
    rows.append({'file':p.name,'decoded_seconds':len(x)/RATE,'sample_peak_dbfs':float(20*np.log10(peak)),
                 'clipped_samples':int((abs(x)>=1).sum()),'energy_fraction':bands})
    base=65+index*160
    svg.append(f'<text x="30" y="{base}" fill="#ddd" font-size="15">{p.stem}</text>')
    db=10*np.log10(np.maximum(power,1e-12)); db-=db.max()
    points=[]
    for freq,level in zip(f,db):
        if 30<=freq<=12000:
            px=30+1040*np.log(freq/30)/np.log(12000/30); py=base+125-min(max(level+65,0),65)*1.6
            points.append(f'{px:.1f},{py:.1f}')
    svg.append(f'<polyline points="{" ".join(points)}" fill="none" stroke="#74d3dd" stroke-width="1.4"/>')
    for tick in [30,80,250,1500,4000,12000]:
        px=30+1040*np.log(tick/30)/np.log(12000/30)
        svg.append(f'<text x="{px:.1f}" y="{base+145}" fill="#87949c" font-size="10">{tick}</text>')
svg.append('</svg>')
(HERE/'spectral-comparison.svg').write_text('\n'.join(svg))
(HERE/'decoded-quality.json').write_text(json.dumps(rows,indent=2))
assert all(row['clipped_samples']==0 for row in rows)
print(json.dumps(rows,indent=2))
