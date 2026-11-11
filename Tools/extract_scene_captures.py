"""Export supplied RAM+VRAM scene captures through virtual_monsbaia's native reader."""
from pathlib import Path
import os,subprocess,json,hashlib,struct
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]; OUT=ROOT/'extracted_assets/azure_dreams_3d_models'; OUT.mkdir(parents=True,exist_ok=True)
exe=ROOT/'virtual_monsbaia-main/virtual_monsbaia-main/batch/release/monsbaia_export.exe'
env=os.environ.copy(); env['PATH']=str(ROOT/'.tools/Qt/5.15.2/msvc2019_64/bin')+os.pathsep+env['PATH']
results=[]; yy,xx=np.indices((256,256))
for source in sorted((ROOT/'AzureDreams_3D_models').glob('*.3dm')):
 folder=OUT/source.stem; folder.mkdir(exist_ok=True); raw=source.read_bytes()
 if len(raw)!=3*1024*1024: raise ValueError('Invalid capture size')
 try: run=subprocess.run([str(exe),str(source),str(folder)],env=env,capture_output=True,text=True,timeout=90)
 except subprocess.TimeoutExpired:
  results.append(dict(source=source.name,status='timeout')); continue
 if run.returncode:
  results.append(dict(source=source.name,status='failed',error=run.stderr));continue
 faces=json.loads((folder/'faces.json').read_text()); pairs=sorted({(f['page'],f['clut']) for f in faces}); textures=folder/'textures';textures.mkdir(exist_ok=True)
 vram=np.frombuffer(raw[2*1024*1024:],dtype='<u2').reshape(512,1024)
 with (folder/'catalog.mtl').open('w') as m:
  for page,clut in pairs:
   name=f'page_{page}_clut_{clut}';depth=(page>>7)&3
   if depth==3: raise ValueError('Reserved texture depth')
   words=vram[(((page>>4)&1)*256+yy)&511,((page&15)*64+(xx>>(2-depth)))&1023]
   if depth<2:
    indices=(words>>((xx&3)*4))&15 if depth==0 else (words>>((xx&1)*8))&255
    words=vram[(clut>>6)&511,((clut&63)*16+indices)&1023]
   image=np.zeros((256,256,4),dtype=np.uint8)
   for channel,shift in enumerate((0,5,10)):
    color=(words>>shift)&31;image[:,:,channel]=(color<<3)|(color>>2)
   image[:,:,3]=np.where(words==0,0,255)
   Image.fromarray(image).save(textures/(name+'.png'))
   Image.fromarray(np.where(words&32768,255,0).astype(np.uint8)).save(textures/(name+'_stp.png'))
   m.write(f'newmtl {name}\nKd 1 1 1\nillum 0\nmap_Kd textures/{name}.png\nmap_d textures/{name}.png\n\n')
 gridptr,faceptr,vertexptr=struct.unpack_from('<III',raw,0x8333c);sx,sy,mx,my=struct.unpack_from('<4H',raw,0x83350)
 start=gridptr&0x1fffff;count=(my+1)*(1<<(sx&31));cells=list(struct.unpack_from('<'+'H'*count,raw,start))
 (folder/'scene_grid.json').write_text(json.dumps(dict(width=mx+1,height=my+1,row_stride=1<<(sx&31),grid_address=hex(gridptr),face_table_address=hex(faceptr),vertices_address=hex(vertexptr),cells=cells),indent=2)+'\n')
 vertices=uvs=triangles=0
 with (folder/'catalog.obj').open() as f:
  for line in f:
   if line.startswith('v '):vertices+=1
   elif line.startswith('vt '):uvs+=1
   elif line.startswith('f '):
    for ref in line.split()[1:]:
     vi,ti=map(int,ref.split('/')); assert 0<vi<=vertices and 0<ti<=uvs
    triangles+=1
 assert triangles==2*len(faces)
 for png in textures.glob('*.png'):
  with Image.open(png) as im:im.verify()
 results.append(dict(source=source.name,status='exported',source_sha256=hashlib.sha256(raw).hexdigest(),width=mx+1,height=my+1,quads=len(faces),triangles=triangles,vertices=vertices,texture_pairs=len(pairs),obj_sha256=hashlib.sha256((folder/'catalog.obj').read_bytes()).hexdigest()))
 print(source.name,len(faces),'quads',len(pairs),'textures',flush=True)
(OUT/'manifest.json').write_text(json.dumps(dict(results=results,limits='Supplied capture names retained as labels; disc equivalence and capture conditions not independently established. Native virtual_monsbaia sequential traversal retained; alternate faces can coexist. OBJ materials do not reproduce PS1 semitransparency blending. STP masks and face metadata preserve those bits.'),indent=2)+'\n')
print('SUMMARY',len(results),'scenes;',sum(r.get('triangles',0) for r in results),'triangles;',sum(r.get('texture_pairs',0) for r in results),'texture pairs')
