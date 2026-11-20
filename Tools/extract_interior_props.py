"""Recover small interior props and their native 20-byte placement records.
Callback 92 = bottle, 155 = glass, 164 = small tabletop object.
Coordinates use the original interior grid origin (8,8), 64 units per tile.
Source scene identity is resolved through the native area-12 CD descriptors.
"""
from pathlib import Path
import sys,struct,json,uuid
from PIL import Image,ImageDraw
import extract_vivian_town_sprites as v
P=Path(__file__).resolve().parents[1];W=P.parent
SOURCES={92:('Bottle',0x800F8A4C),155:('Glass',0x800F8B6C),164:('TableProp',0x800F8A58)}
RECORDS=[('0e_restaurant',0x556234,164),('0e_restaurant',0x556248,92),('10_hospital_2',0x597E00,164),('18_cherrl_house',0x648444,164),('18_cherrl_house',0x6485D4,155),('19_generic_building',0x64AC50,92),('1a_generic_building',0x64CAE4,164),('1b_generic_building',0x64EA90,164),('1c_generic_building',0x6508A0,92),('1d_generic_building',0x6526C0,164),('20_bar',0x692930,92),('20_bar',0x692944,155)]
def support(room,x,z):
 p=P/'Assets/AzureDreams/Resources/Town/Interiors'/('Interior_'+room)
 heights=(p/'heights.bytes').read_bytes();ground=struct.unpack_from('<h',heights,(int(z)*128+int(x))*2)[0]/256
 d=json.loads((p/'geometry.json').read_text());vs=d['vertices'];ys=[ground]
 for section in d['surfaces']:
  ts=section['triangles']
  for i in range(0,len(ts),3):
   a,b,c=[vs[j] for j in ts[i:i+3]];den=(b['z']-c['z'])*(a['x']-c['x'])+(c['x']-b['x'])*(a['z']-c['z'])
   if abs(den)<1e-7:continue
   u=((b['z']-c['z'])*(x-c['x'])+(c['x']-b['x'])*(z-c['z']))/den
   w=((c['z']-a['z'])*(x-c['x'])+(a['x']-c['x'])*(z-c['z']))/den
   if min(u,w,1-u-w)<-1e-5:continue
   y=u*a['y']+w*b['y']+(1-u-w)*c['y']
   if ground-.05<=y<=ground+1:ys.append(y)
 return max(ys)
def main():
 b=(W/'extracted_assets/full_disc/files/TOWN/TOWN.BIN').read_bytes();out=P/'Assets/AzureDreams/Resources/Town/InteriorProps';out.mkdir(parents=True,exist_ok=True)
 v.CELL=128;v.ORIGIN_X=64;v.ORIGIN_Y=80
 vr=(W/'extracted_assets/azure_dreams_3d_models/ad_model_building_19_generic_building/vram.bin').read_bytes()
 lifts={}
 for cb,(name,ptr) in SOURCES.items():
  image=v.render(v.read_chain(b,ptr),vr,bytes([1])*524288)
  lifts[cb]=max(0,(image.getbbox()[3]-80)/48)
  path=out/(name+'.png');image.save(path);v.write_meta(path,'texture')
 placements=[]
 for room,offset,callback in RECORDS:
  assert b[offset]==0 and b[offset+1]&0xc0==0x40
  assert struct.unpack_from('<I',b,offset+12)[0]==callback
  x,z=struct.unpack_from('<HH',b,offset+16);x=x/64+8;z=z/64+8
  y=support(room,x,z)
  placements.append(dict(room='Interior_'+room,asset=SOURCES[callback][0],position=dict(x=x,y=y+lifts[callback],z=z),recordOffset=hex(offset),callback=callback,spriteSource=hex(SOURCES[callback][1])))
 path=out/'placements.json';path.write_text(json.dumps(dict(entries=placements),indent=2));v.write_meta(path,'text')
 print([(x['room'],x['asset'],x['position']) for x in placements])
if __name__=='__main__':main()

