"""Extract retail swimming: SSTP_PLT_DM_SWIM -> D_800D0158 -> D_800CFFC0.
Alternate bundle groups 50..54, seven 6-tick frames in each direction.
"""
from pathlib import Path
import json,uuid
import struct,zlib

def read_rgba(path):
 data=path.read_bytes();pos=8;packed=b''
 while pos<len(data):
  n=struct.unpack_from('>I',data,pos)[0];kind=data[pos+4:pos+8];chunk=data[pos+8:pos+8+n];pos+=12+n
  if kind==b'IHDR':w,h,depth,color,_,_,interlace=struct.unpack('>IIBBBBB',chunk);assert depth==8 and color==6 and interlace==0
  if kind==b'IDAT':packed+=chunk
 raw=zlib.decompress(packed);stride=w*4;result=bytearray();previous=bytearray(stride)
 for y in range(h):
  kind=raw[y*(stride+1)];row=bytearray(raw[y*(stride+1)+1:(y+1)*(stride+1)])
  for x in range(stride):
   a=row[x-4] if x>=4 else 0;b=previous[x];c=previous[x-4] if x>=4 else 0
   if kind==1:predict=a
   elif kind==2:predict=b
   elif kind==3:predict=(a+b)//2
   elif kind==4:
    v=a+b-c;da,db,dc=abs(v-a),abs(v-b),abs(v-c);predict=a if da<=db and da<=dc else b if db<=dc else c
   else:predict=0
   row[x]=(row[x]+predict)&255
  result+=row;previous=row
 return w,h,result

def write_rgba(path,w,h,pixels):
 def chunk(kind,data):return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
 raw=b''.join(b'\0'+pixels[y*w*4:(y+1)*w*4] for y in range(h))
 path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(raw))+chunk(b'IEND',b''))

P=Path(__file__).resolve().parents[1];s=P.parent/'extracted_assets/reimagined_pipeline/TownSpriteBanks/town_koh_alternate'
g=json.loads((s/'animations.json').read_text())['groups'];f={e['pointer']:e for e in json.loads((s/'frames.json').read_text())};atlas=bytearray(7*64*5*64*4);ticks=[]
for row in range(5):
 entries=[e for e in g[50+row]['entries'] if 'frame_png' in e];assert len(entries)==7;ticks.append([e['ticks_raw'] for e in entries])
 for col,e in enumerate(entries):
  w,h,pixels=read_rgba(s/e['frame_png']);x,y=f[e['pointer']]['origin']
  for iy in range(h):
   dest=((row*64+y+iy)*448+col*64+x)*4;atlas[dest:dest+w*4]=pixels[iy*w*4:(iy+1)*w*4]
out=P/'Assets/AzureDreams/Resources/Town/Retail';write_rgba(out/'koh-swimming.bytes',448,320,atlas);(out/'koh-swimming-timings.json').write_text(json.dumps(dict(columns=7,ticks=ticks[0],groups=list(range(50,55)),source='SSTP_PLT_DM_SWIM 41 -> TOWN 800992F8 -> D_800D0158 -> alternate D_800CFFC0')))
for n in ('koh-swimming.bytes','koh-swimming-timings.json'):
 meta=out/(n+'.meta')
 if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'azure-retail/'+n).hex+'\n')
print('35 original swim frames extracted')

