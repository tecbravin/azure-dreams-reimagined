"""Extract native Gym/Casino object face lists, UVs and CLUT pages.
Sources: initialize_gym_minigame (52/53), create_minigame_display_objects (44),
initialize_casino_minigame (45 and D_80024444:42/43/44). Native units:64/tile.
"""
from pathlib import Path
import json,struct,uuid,sys
from PIL import Image
P=Path(__file__).resolve().parents[1];W=P.parent
SRC=W/'extracted_assets/reimagined_pipeline/TownGeometry'
OUT=P/'Assets/AzureDreams/Resources/Town/Minigames'
JOBS={'GymEmblem':(29,52,'08_gym'),'GymDumbbell':(29,53,'08_gym'), 'SlotLever':(30,44,'2b_casino_1'), 'RouletteWheel':(31,45,'06_casino_2'), 'Roulette6':(31,42,'06_casino_2'),'Roulette3':(31,43,'06_casino_2'),'Roulette2':(31,44,'06_casino_2')}
def meta(p,texture=False):
 m=Path(str(p)+'.meta')
 if m.exists():return
 guid=uuid.uuid5(uuid.NAMESPACE_URL,'azure/native/minigames/'+p.name).hex
 if texture:
  import re
  s=(P/'Assets/AzureDreams/Resources/Tower/GroundItemIcons.png.meta').read_text()
  s=re.sub(r'guid: [a-f0-9]+','guid: '+guid,s)
  s=re.sub(r'textureCompression: \d+','textureCompression: 0',s)
 else:s=f'fileFormatVersion: 2\nguid: {guid}\nTextScriptImporter:\n  externalObjects: {{}}\n'
 m.write_text(s)
def main():
 OUT.mkdir(parents=True,exist_ok=True);report=[]
 for name,(typ,idx,scene) in JOBS.items():
  m=json.loads((SRC/f'town_geometry_type_{typ}_metadata.json').read_text());d=(SRC/f'town_geometry_type_{typ}.bytes').read_bytes();r=m['region_offsets']
  vs=[struct.unpack_from('<3h',d,r['vertices']+i*8) for i in range(m['vertex_records'])]
  pos=struct.unpack_from('<I',d,r['face_list_pointers']+idx*4)[0];groups={}
  vram=(W/f'extracted_assets/azure_dreams_3d_models/ad_model_building_{scene}/vram.bin').read_bytes()
  count=0
  while pos+24<=r['face_list_pointers']:
   vi=struct.unpack_from('<4H',d,pos);uv0,uv1,n,attr=struct.unpack_from('<4I',d,pos+8)
   if uv1:
    assert max(vi)<len(vs)
    page,clut=uv1>>16,uv0>>16;key=f'{name}_p{page}_c{clut}';g=groups.setdefault(key,dict(texture=key,vertices=[],uv=[],triangles=[]))
    base=len(g['vertices']);uvs=[(uv0&255,(uv0>>8)&255),(uv1&255,(uv1>>8)&255),(n>>16&255,n>>24&255),(attr&255,attr>>8&255)]
    for v,(u,t) in zip(vi,uvs):
     x,y,z=vs[v];g['vertices'].append(dict(x=x/64,y=-z/64,z=y/64));g['uv'].append(dict(x=(u+.5)/256,y=1-(t+.5)/256))
    g['triangles'] += [base,base+1,base+2,base+2,base+1,base+3];count+=1
    path=OUT/(key+'.png')
    if not path.exists():
     assert (page>>7)&3==0
     px=(page&15)*64;py=(page>>4&1)*256;cx=(clut&63)*16;cy=clut>>6
     pal=struct.unpack_from('<16H',vram,(cy*1024+cx)*2)
     pixels=[]
     for yy in range(256):
      for xx in range(256):
       word=struct.unpack_from('<H',vram,((py+yy)*1024+px+xx//4)*2)[0];c=pal[word>>(xx%4*4)&15]
       pixels.append(tuple(((c>>s&31)*255//31) for s in (0,5,10))+(255 if c else 0,))
     im=Image.new('RGBA',(256,256));im.putdata(pixels);im.save(path);meta(path,True)
   # Preserve every face in this model, including back-facing branches.
   if attr>>31:break
   pos+=24
  path=OUT/(name+'.json');path.write_text(json.dumps(dict(sections=list(groups.values())),separators=(',',':')));meta(path)
  report.append(dict(name=name,geometryType=typ,faceList=idx,faces=count,textures=list(groups),source=m['chunks']))
 # The thrown barbell is a native sprite chain, not one of the decorative meshes.
 import extract_vivian_town_sprites as resident
 resident.CELL=128;resident.ORIGIN_X=64;resident.ORIGIN_Y=100
 town=(W/'extracted_assets/full_disc/files/TOWN/TOWN.BIN').read_bytes()
 gym_vram=(W/'extracted_assets/azure_dreams_3d_models/ad_model_building_08_gym/vram.bin').read_bytes()
 image=resident.render(resident.read_chain(town,0x800F9B40),gym_vram,bytes([1])*524288)
 path=OUT/'GymBarbell.png';image.crop((32,84,96,108)).save(path);meta(path,True)
 # Casino minigame_pickup_sprite_sources (0x8002445C) references these resident descriptors.
 exe=(W/'SLUS_006.14').read_bytes()
 vr=(W/'extracted_assets/azure_dreams_3d_models/ad_model_building_06_casino_2/vram.bin').read_bytes()
 for denomination,ptr in enumerate([0x80077BF0,0x80077824,0x80077818]):
  flags,mode,x,y,page,clut,u,t,width,height=struct.unpack_from('<BBbbHH4B',exe,ptr-0x8002d000+0x800)
  assert (width,height)==(16,16)
  palette=struct.unpack_from('<16H',vr,((clut>>6)*1024+(clut&63)*16)*2)
  pixels=[]
  for yy in range(t,t+height):
   for xx in range(u,u+width):
    word=struct.unpack_from('<H',vr,((((page>>4)&1)*256+yy)*1024+(page&15)*64+xx//4)*2)[0]
    c=palette[word>>(xx%4*4)&15]
    pixels.append(tuple((c>>shift&31)*255//31 for shift in (0,5,10))+(255 if c else 0,))
  image=Image.new('RGBA',(16,16));image.putdata(pixels);assert image.getbbox()
  path=OUT/f'Coin{denomination}.png';image.save(path);meta(path,True)
 (W/'Validation/MinigameGraphics').mkdir(parents=True,exist_ok=True)
 (W/'Validation/MinigameGraphics/models.json').write_text(json.dumps(report,indent=2));print([(x['name'],x['faces']) for x in report])
if __name__=='__main__':main()
