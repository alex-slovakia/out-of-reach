"""Fetch only approved ambientCG 2K members, bounded to50MB, then prepare Unity maps.
Run at repository root. Reuses installed curl, Pillow and NumPy. No Unity or scene writes.
"""
from pathlib import Path
import io, json, subprocess, zipfile, hashlib, datetime, uuid
from PIL import Image
import numpy as np
ROOT=Path(__file__).resolve().parents[2]
DOC=ROOT/'docs/art-direction/ground-finish-20261115'
ASSET=ROOT/'Assets/_OutOfReach/Visuals/Environment/GroundFinishV02'
CACHE=DOC/'source-cache'
BUDGET=50_000_000
used=0

def curl(url,byte_range=None,maximum=2_000_000):
 global used
 args=['curl','-sSfL','--max-time','55','--max-filesize',str(maximum)]
 if byte_range:args+=['--range',byte_range]
 data=subprocess.run(args+[url],capture_output=True,check=True).stdout
 used+=len(data)
 if used>BUDGET:raise RuntimeError('50MB network budget exceeded')
 return data

class RemoteZip(io.RawIOBase):
 def __init__(self,url,size):self.url=url;self.size=size;self.pos=0
 def seekable(self):return True
 def seek(self,n,w=0):self.pos=n if w==0 else self.pos+n if w==1 else self.size+n;return self.pos
 def tell(self):return self.pos
 def read(self,n=-1):
  if n<0:n=self.size-self.pos
  n=min(n,self.size-self.pos)
  if n<=0:return b''
  if used+n>BUDGET:raise RuntimeError('50MB network budget would be exceeded')
  data=curl(self.url,f'{self.pos}-{self.pos+n-1}',n+1)
  if len(data)!=n:raise RuntimeError('Server did not honor bounded byte range')
  self.pos+=n;return data

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def meta(p,folder=False):
 m=Path(str(p)+'.meta')
 if m.exists():return
 m.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'+('folderAsset: yes\n' if folder else '')+'DefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n',encoding='utf-8')

def run():
 DOC.mkdir(parents=True,exist_ok=True);CACHE.mkdir(exist_ok=True)
 (DOC/'.gitignore').write_text('source-cache/\n',encoding='utf-8')
 url='https://ambientcg.com/api/v2/full_json?id=Ground109,Ground110&include=downloadData,dimensionsData,displayData'
 metadata_path=DOC/'selected-assets-api.json'
 if not metadata_path.exists():metadata_path.write_bytes(curl(url))
 metadata=json.loads(metadata_path.read_text())
 out=ASSET/'Textures';out.mkdir(parents=True,exist_ok=True)
 receipt={'retrieved_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'api_url':url,'license':'CC0 1.0 Universal','license_url':'https://creativecommons.org/publicdomain/zero/1.0/','license_policy':'https://docs.ambientcg.com/license/','assets':[]}
 for a in sorted(metadata['foundAssets'],key=lambda x:x['assetId']):
  ident=a['assetId']
  d=next(d for d in a['downloadFolders']['default']['downloadFiletypeCategories']['zip']['downloads'] if d['attribute']=='2K-JPG')
  sources={}
  with zipfile.ZipFile(RemoteZip(d['downloadLink'],d['size'])) as z:
   for kind in ['Color','NormalGL','Roughness','AmbientOcclusion','Displacement']:
    member=f'{ident}_2K-JPG_{kind}.jpg';p=CACHE/member
    if not p.exists():p.write_bytes(z.read(member))
    image=Image.open(p)
    sources[kind]={'member':member,'source_sha256':sha(p),'source_bytes':p.stat().st_size,'source_dimensions':list(image.size),'zip_crc32':f'{z.getinfo(member).CRC:08x}'}
  products=[]
  for kind in ['Color','NormalGL']:
   im=Image.open(CACHE/f'{ident}_2K-JPG_{kind}.jpg').convert('RGB').resize((2048,2048),Image.Resampling.LANCZOS)
   p=out/f'{ident}_{kind}.jpg';im.save(p,quality=95,subsampling=0)
   products.append({'file':str(p.relative_to(ROOT)).replace('\\','/'),'sha256':sha(p),'channels':'sRGB albedo' if kind=='Color' else 'linear OpenGL tangent normal'})
  channels=[]
  for kind in ['AmbientOcclusion','Displacement','Roughness']:
   im=Image.open(CACHE/f'{ident}_2K-JPG_{kind}.jpg').convert('L').resize((2048,2048),Image.Resampling.LANCZOS)
   channels.append(np.asarray(im))
  mask=np.stack([channels[0],channels[1],channels[2],np.full_like(channels[0],255)],axis=-1)
  p=out/f'{ident}_Mask.png';Image.fromarray(mask,'RGBA').save(p)
  products.append({'file':str(p.relative_to(ROOT)).replace('\\','/'),'sha256':sha(p),'channels':'R=AO,G=height,B=roughness,A=255;linear'})
  receipt['assets'].append({'id':ident,'page':a['shortLink'],'source_package_url':d['downloadLink'],'source_package_bytes':d['size'],'physical_width_cm':a.get('dimensionX'),'method':a.get('creationMethod'),'sources':sources,'products':products})
  print(ident,'prepared2K maps',flush=True)
 receipt['downloaded_bytes_this_run']=used
 receipt['source_map_bytes_total']=sum(s['source_bytes'] for a in receipt['assets'] for s in a['sources'].values())
 (DOC/'source-manifest.json').write_text(json.dumps(receipt,indent=2),encoding='utf-8')
 print('Downloaded',used,'bytes; source map bytes',receipt['source_map_bytes_total'])
if __name__=='__main__':run()
