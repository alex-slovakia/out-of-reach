from pathlib import Path
import json,subprocess,zipfile,io,hashlib,datetime
from PIL import Image
import numpy as np
root=Path('.')
doc=root/'docs/art-direction/ground-finish-20261115'
cache=doc/'source-cache'
asset=root/'Assets/_OutOfReach/Visuals/Environment/SurfaceFinishV01/Textures'
asset.mkdir(parents=True,exist_ok=True)
metadata=json.loads((doc/'metal-search.json').read_text())
receipt={'retrieved_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'license':'CC0 1.0 Universal','license_policy':'https://docs.ambientcg.com/license/','license_url':'https://creativecommons.org/publicdomain/zero/1.0/','assets':[]}
used=0
for a in metadata['foundAssets']:
 if a['assetId'] not in ['Metal009','Metal027']:continue
 ident=a['assetId']
 d=next(x for x in a['downloadFolders']['default']['downloadFiletypeCategories']['zip']['downloads'] if x['attribute']=='1K-JPG')
 used+=d['size']
 if used>20_000_000:raise RuntimeError('Metal budget exceeded')
 zpath=cache/d['fileName']
 if not zpath.exists():subprocess.run(['curl','-sSfL','--max-time','55','--max-filesize',str(d['size']+1),'-o',str(zpath),d['downloadLink']],check=True)
 if zpath.stat().st_size!=d['size']:raise RuntimeError('Download size mismatch')
 row={'id':ident,'page':a['shortLink'],'source_url':d['downloadLink'],'source_size':d['size'],'source_sha256':hashlib.sha256(zpath.read_bytes()).hexdigest(),'maps':[]}
 with zipfile.ZipFile(zpath) as z:
  print(ident,z.namelist(),flush=True)
  def load(kind):
   name=ident+'_1K-JPG_'+kind+'.jpg'
   if name not in z.namelist():return None
   data=z.read(name);row['maps'].append({'name':name,'sha256':hashlib.sha256(data).hexdigest()})
   return Image.open(io.BytesIO(data))
  for kind in ['Color','NormalGL']:
   im=load(kind).convert('RGB');assert im.size==(1024,1024)
   im.save(asset/(ident+'_'+kind+'.jpg'),quality=95,subsampling=0)
  rough=np.array(load('Roughness').convert('L'))
  metal=load('Metalness')
  if metal is None:metal=np.full_like(rough,255 if ident=='Metal009' else 0)
  else:metal=np.array(metal.convert('L'))
  packed=np.stack([metal,np.full_like(rough,255),np.zeros_like(rough),255-rough],axis=-1)
  Image.fromarray(packed,'RGBA').save(asset/(ident+'_MetallicSmoothness.png'))
 row['products']=[{'file':str(p).replace('\\','/'),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in asset.glob(ident+'*')]
 receipt['assets'].append(row)
receipt['source_bytes_total']=used
(doc/'metal-source-manifest.json').write_text(json.dumps(receipt,indent=2),encoding='utf-8')
print('Metal source bytes',used)
