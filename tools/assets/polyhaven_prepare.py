"""Curated Poly Haven CC0 assets for OUT OF REACH. No Unity scene mutation.
Download with --download; prepare with Blender --background --python THIS -- --blender.
Default verifies provenance hashes. Uses Python stdlib; Blender preparation uses its bundled numpy.
"""
from pathlib import Path
import argparse, hashlib, json, math, shutil, sys, uuid
from datetime import datetime, timezone
from urllib.request import Request,urlopen
from concurrent.futures import ThreadPoolExecutor
ROOT=Path(__file__).resolve().parents[2]
STAGE=ROOT/'.local/polyhaven-prepare'
OUT=ROOT/'Assets/_OutOfReach/Visuals/ThirdParty/PolyHaven'
DOC=ROOT/'docs/art-direction/polyhaven-import-20261115'
META=ROOT/'.local/asset-review'
MODELS=['flood_lights','portable_generator','long_life_food','coast_land_rocks_04','namaqualand_boulder_02','combination_wrench','adjustable_wrench','pliers','screwdrivers_02','vintage_spacecraft_instrument','medical_box','metal_toolbox','binoculars']
GROUNDS=['coast_sand_02','gravelly_sand']
UA='OutOfReachAssetReview/1.0'

def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def save(path,value):Path(path).parent.mkdir(parents=True,exist_ok=True);Path(path).write_text(json.dumps(value,indent=2)+'\n',encoding='utf-8')
def load(path):return json.loads(Path(path).read_text(encoding='utf-8-sig'))
def fetch(entry,path):
 path=Path(path);path.parent.mkdir(parents=True,exist_ok=True)
 if path.exists() and hashlib.md5(path.read_bytes()).hexdigest()==entry['md5']:return
 with urlopen(Request(entry['url'],headers={'User-Agent':UA}),timeout=90) as response,path.with_suffix(path.suffix+'.part').open('wb') as stream:shutil.copyfileobj(response,stream)
 tmp=path.with_suffix(path.suffix+'.part')
 if tmp.stat().st_size!=entry['size'] or hashlib.md5(tmp.read_bytes()).hexdigest()!=entry['md5']:raise ValueError('Source byte/hash mismatch '+str(path))
 tmp.replace(path)

def download():
 DOC.mkdir(parents=True,exist_ok=True);STAGE.mkdir(parents=True,exist_ok=True)
 tasks=[];assets=[]
 for id in MODELS+GROUNDS:
  info=load(META/(id+'-info.json'));files=load(META/(id+'-files.json'))
  res='2k' if id in GROUNDS+['coast_land_rocks_04','namaqualand_boulder_02'] else '1k'
  selected=[]
  if id in MODELS:
   package=files['fbx'][res]['fbx'];selected.append((Path(package['url']).name,package))
   selected.extend(package.get('include',{}).items())
  else:
   for kind,fmt in [('Diffuse','jpg'),('nor_gl','exr'),('Rough','exr')]:
    item=files[kind][res][fmt];selected.append(('textures/'+Path(item['url']).name,item))
  records=[]
  for relative,entry in selected:
   if '_spec_' in relative:continue
   path=STAGE/id/relative;tasks.append((entry,path));records.append(dict(relative=relative,url=entry['url'],source_md5=entry['md5'],bytes=entry['size']))
  assets.append(dict(id=id,resolution=res,kind='ground' if id in GROUNDS else 'model',source_url='https://polyhaven.com/a/'+id,authors=info.get('authors',{}),source_dimensions_mm=info.get('dimensions'),source_polycount=info.get('polycount'),files=records))
  save(DOC/'metadata'/(id+'-info.json'),info);save(DOC/'metadata'/(id+'-files.json'),files)
 print('Downloading',len(tasks),'files',round(sum(e['size'] for e,p in tasks)/1e6,2),'MB',flush=True)
 def work(pair):fetch(*pair)
 with ThreadPoolExecutor(max_workers=3) as pool:list(pool.map(work,tasks))
 for asset in assets:
  for f in asset['files']:f['sha256']=sha(STAGE/asset['id']/f['relative'])
 save(DOC/'source-manifest.json',dict(license='CC0-1.0',license_url='https://polyhaven.com/license',price_paid=0,currency='EUR',retrieved_utc=datetime.now(timezone.utc).isoformat(),api_user_agent=UA,assets=assets))
 print('Download verified',flush=True)

def blender_prepare(only=None):
 import bpy,numpy as np
 from mathutils import Vector
 OUT.mkdir(parents=True,exist_ok=True)
 for folder in ['Models','Textures','Materials','Prefabs']:(OUT/folder).mkdir(exist_ok=True)
 source=load(DOC/'source-manifest.json');prepared=[]
 if only:
  prepared=[a for a in load(OUT/'prepared-manifest.json')['assets'] if a['id']!=only]
  source['assets']=[a for a in source['assets'] if a['id']==only]
 def image(path,noncolor=True):
  im=bpy.data.images.load(str(path),check_existing=False)
  if noncolor:im.colorspace_settings.name='Non-Color'
  if max(im.size)>limit:
   factor=limit/max(im.size);im.scale(max(1,round(im.size[0]*factor)),max(1,round(im.size[1]*factor)))
  return im
 def pixels(im):
  a=np.empty(len(im.pixels),dtype=np.float32);im.pixels.foreach_get(a);return a.reshape((-1,4))
 def png(name,array,w,h,noncolor=True):
  im=bpy.data.images.new(name,width=w,height=h,alpha=True)
  im.colorspace_settings.name='Non-Color' if noncolor else 'sRGB';im.pixels.foreach_set(np.ascontiguousarray(array,dtype=np.float32).reshape(-1));im.filepath_raw=str(OUT/'Textures'/(name+'.png'));im.file_format='PNG';im.save();bpy.data.images.remove(im)
  return 'Assets/_OutOfReach/Visuals/ThirdParty/PolyHaven/Textures/'+name+'.png'
 for asset in source['assets']:
  id=asset['id'];limit=2048 if asset['resolution']=='2k' else 1024;directory=STAGE/id;files=asset['files'];textures=[f for f in files if f['relative'].startswith('textures/')]
  groups=['glass','main'] if id=='flood_lights' else ['main'];mats=[]
  for group in groups:
   def match(marker):
    candidates=[directory/f['relative'] for f in textures if marker in Path(f['relative']).name and (('_glass_' in f['relative'])==(group=='glass'))]
    return candidates[0] if candidates else None
   diff=match('_diff_');normal=match('_nor_gl_');rough=match('_rough_');metal=match('_metal_');alpha=match('_alpha_');emission=match('_emissive_')
   if not diff:continue
   key=id if group=='main' else id+'_glass';prefix='Assets/_OutOfReach/Visuals/ThirdParty/PolyHaven/Textures/'
   base=image(diff,False);w,h=base.size
   if alpha or max(base.size)>limit:
    rgb=pixels(base)
    if alpha:
     mask=image(alpha)
     if list(mask.size)!=[w,h]:mask.scale(w,h)
     rgb[:,3]=pixels(mask)[:,0];bpy.data.images.remove(mask)
    basepath=png(key+'_BaseColor',rgb,w,h,False)
   else:
    dest=OUT/'Textures'/(key+'_BaseColor'+diff.suffix);shutil.copyfile(diff,dest);basepath=prefix+dest.name
   bpy.data.images.remove(base)
   mat=dict(key=key,baseColor=basepath,normal='',metalSmooth='',emission='',alpha='',ground=asset['kind']=='ground',glass=group=='glass')
   if normal:
    im=image(normal);w,h=im.size;mat['normal']=png(key+'_Normal',pixels(im),w,h);bpy.data.images.remove(im)
   if rough:
    im=image(rough);w,h=im.size;arr=np.zeros((w*h,4),dtype=np.float32);arr[:,3]=1-np.clip(pixels(im)[:,0],0,1);bpy.data.images.remove(im)
    if metal:
     im=image(metal)
     if list(im.size)!=[w,h]:im.scale(w,h)
     arr[:,0]=np.clip(pixels(im)[:,0],0,1);bpy.data.images.remove(im)
    mat['metalSmooth']=png(key+'_MetalSmooth',arr,w,h)
   if emission:
    dest=OUT/'Textures'/(key+'_Emission'+emission.suffix);shutil.copyfile(emission,dest);mat['emission']=prefix+dest.name
   if alpha:
    dest=OUT/'Textures'/(key+'_Alpha'+alpha.suffix);shutil.copyfile(alpha,dest);mat['alpha']=prefix+dest.name
   mats.append(mat)
  entry=dict(id=id,kind=asset['kind'],materials=mats,objects=[],source_url=asset['source_url'],resolution=asset['resolution'])
  if asset['kind']=='model':
   bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
   fbx=next(directory/f['relative'] for f in files if f['relative'].endswith('.fbx'))
   bpy.ops.import_scene.fbx(filepath=str(fbx),use_anim=False)
   objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
   print('INSPECT',id,[(o.name,len(o.data.polygons)) for o in objects],flush=True)
   entry['source_objects']=[dict(name=o.name,polygons=len(o.data.polygons),dimensions=list(o.dimensions)) for o in objects]
   # LOD selection: retain one lowest provided level per mesh family when named.
   import re
   families={}
   for o in objects:
    match=re.search(r'(?i)(?:[_. -]lod)(\d+)',o.name)
    base=re.sub(r'(?i)(?:[_. -]lod)\d+','',o.name) if match else o.name
    level=int(match.group(1)) if match else 0
    if base not in families or level>families[base][0]:families[base]=(level,o)
   keep=[v[1] for v in families.values()]
   for o in objects:
    if o not in keep:bpy.data.objects.remove(o,do_unlink=True)
   objects=keep
   if id=='flood_lights':
    selected=[o for o in objects if o.name.startswith('flood_lights_standing_double_a_') and len(o.data.polygons)>0]
    if len(selected)<8:raise ValueError('Expected complete standing double-A fixture')
    for o in objects:
     if o not in selected:bpy.data.objects.remove(o,do_unlink=True)
    objects=selected
    entry['selection']='standing_double_a complete twin-lamp tripod, cable and brackets; other source variants omitted'
   for o in objects:
    matrix=o.matrix_world.copy();o.parent=None;o.matrix_world=matrix
   # An isolated, usable twin light fixture; prefer largest visible connected assembly if source pack has variants.
   # Per-object decimation preserves separate supplied objects and materials.
   total=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects)
   budget=5000 if id in ['combination_wrench','adjustable_wrench','pliers','screwdrivers_02'] else 30000
   if id=='long_life_food':budget=12000
   ratio=min(1.,budget/max(total,1))
   for o in objects:
    bpy.context.view_layer.objects.active=o;o.select_set(True)
    if ratio<.99:
     mod=o.modifiers.new('OutOfReach bounded mesh','DECIMATE');mod.ratio=ratio;bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    o.select_set(False)
   corners=[o.matrix_world@Vector(c) for o in objects for c in o.bound_box]
   minimum=Vector(tuple(min(v[i] for v in corners) for i in range(3)));maximum=Vector(tuple(max(v[i] for v in corners) for i in range(3)))
   shift=Vector(((minimum.x+maximum.x)*.5,(minimum.y+maximum.y)*.5,minimum.z))
   for o in objects:o.location-=shift
   for o in objects:
    for slot in o.material_slots:
     if slot.material:slot.material.name='PH_'+id+('_glass' if 'glass' in slot.material.name.lower() else '')
    o.select_set(True)
    triangles=sum(len(p.vertices)-2 for p in o.data.polygons)
    entry['objects'].append(dict(name=o.name,triangles=triangles))
   entry['triangles']=sum(o['triangles'] for o in entry['objects']);entry['dimensions_m_blender_xyz']=list(maximum-minimum)
   entry['dimensions_m_unity_xyz']=[maximum.x-minimum.x,maximum.z-minimum.z,maximum.y-minimum.y]
   entry['pivot']='bottom-centre of combined source pack';entry['scaleFactor']=1.0
   bpy.ops.export_scene.fbx(filepath=str(OUT/'Models'/(id+'.fbx')),use_selection=True,object_types={'MESH'},use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',global_scale=1.0,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',path_mode='STRIP',embed_textures=False)
   entry['model']='Assets/_OutOfReach/Visuals/ThirdParty/PolyHaven/Models/'+id+'.fbx';entry['prefab']='Assets/_OutOfReach/Visuals/ThirdParty/PolyHaven/Prefabs/PH_'+id+'.prefab'
  prepared.append(entry)
  save(OUT/'prepared-manifest.json',dict(assets=prepared))
  print('PREPARED',id,entry.get('triangles'),flush=True)
 save(DOC/'prepared-manifest.json',dict(assets=prepared))
 for p in OUT.rglob('*'):
  if p.suffix=='.meta':continue
  meta=Path(str(p)+'.meta')
  if meta.exists():continue
  meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'+('folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n' if p.is_dir() else ''),encoding='utf-8')
 save(DOC/'prepared-hashes.json',{str(p.relative_to(OUT)).replace('\\','/'):dict(bytes=p.stat().st_size,sha256=sha(p)) for p in OUT.rglob('*') if p.is_file() and p.suffix!='.meta'})

def verify():
 manifest=load(DOC/'source-manifest.json')
 for a in manifest['assets']:
  for f in a['files']:
   p=STAGE/a['id']/f['relative']
   if p.stat().st_size!=f['bytes'] or sha(p)!=f['sha256']:raise ValueError('Source mismatch '+str(p))
 hashes=load(DOC/'prepared-hashes.json')
 for relative,data in hashes.items():
  p=OUT/relative
  if p.stat().st_size!=data['bytes'] or sha(p)!=data['sha256']:raise ValueError('Prepared mismatch '+str(p))
 print('PASS source and prepared byte/SHA256 manifests; price0 CC0; '+str(len(manifest['assets']))+' curated assets')

if __name__=='__main__':
 args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else sys.argv[1:]
 if '--download' in args:download()
 elif '--blender' in args:blender_prepare(args[args.index('--only')+1] if '--only' in args else None)
 else:verify()
