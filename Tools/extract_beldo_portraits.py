"""Extract Beldo's three retail portrait variants, including timed animation frames.
Follows the original speaker table and resource descriptors; never replaces redesigns.
"""
from pathlib import Path
import sys,struct,json,hashlib,uuid
from PIL import Image
P=Path(__file__).resolve().parents[1];W=P.parent
sys.path.insert(0,str(W/'azure-dreams-decomp-master/tools/decoded'))
from lz import decode
OUT=P/'Assets/StreamingAssets/Media/Portraits/Dialogue'
def main():
 exe=(W/'SLUS_006.14').read_bytes();town=(W/'TOWN/TOWN.BIN').read_bytes()
 assert hashlib.sha256(exe).hexdigest()=='ae19d8d4bdd2db9a933b1e44d70dd61be670781e441d7cb37da85250a7aed31f'
 assert hashlib.sha256(town).hexdigest()=='907725012d8d9e1deab18c28b62f4d304057e86a6415df605b9ffb87cfb2fe18'
 eo=lambda a:a-0x8002d000+0x800
 vram=bytearray(1024*512*2);written=bytearray(1024*512)
 def load(data,rect):
  x,y,w,h=rect;assert len(data)==w*h*2,(len(data),rect)
  for row in range(h):
   start=(y+row)*1024+x;vram[start*2:(start+w)*2]=data[row*w*2:(row+1)*w*2];written[start:start+w]=bytes([1])*w
 def pack(descriptor,portrait=False):
  flags,lba=struct.unpack_from('<II',exe,eo(descriptor));sectors=flags>>23;start=(lba-3077)*2048
  raw=town[start:start+sectors*2048];headers=[];pos=0
  while True:
   typ,size,dataoff=struct.unpack_from('<HHI',raw,pos)
   if not typ:break
   assert size>=8;headers.append((typ,pos,dataoff));pos+=size
  for i,(typ,p,o) in enumerate(headers):
   end=headers[i+1][2] if i+1<len(headers) else len(raw)
   if typ==1 or (typ==3 and portrait):
    rect=struct.unpack_from('<4H',exe,eo(0x8006b220)+i*8) if portrait else struct.unpack_from('<4H',raw,p+8)
    decoded,consumed=decode(raw[o:end],rect[2]*rect[3]*2);load(decoded,rect)
   elif typ==2:
    loc,count,fl=struct.unpack_from('<HHH',raw,p+8)
    if not fl&1:load(raw[o:o+count*32],((loc&63)*32,448+((loc>>6)&63),count*16,1))
   else:raise ValueError(('resource',typ))
  return dict(descriptor=hex(descriptor),lba=lba,sectors=sectors,townOffset=hex(start))
 pack(0x80080ea0)
 ids=exe[eo(0x8006b1ec):eo(0x8006b1ec)+17];index=ids.index(17)
 table=struct.unpack_from('<I',exe,eo(0x8006b1a8)+index*4)[0];assert table==0x8006b0c8
 report=dict(speakerId=17,speakerTable='0x8006B1EC',variantTable=hex(table),variants=[])
 all_images=[]
 for variant in range(3):
  resource,anim=struct.unpack_from('<II',exe,eo(table)+variant*8);origin=pack(resource,True);frames=[];p=eo(anim)
  for frame in range(256):
   ticks,kind,chain=struct.unpack_from('<HHI',exe,p);p+=8
   if kind!=2:break
   parts=[];q=eo(chain)
   for _ in range(32):
    flags,mode,x,y,page,clut,u,v,w,h=struct.unpack_from('<BBbbHH4B',exe,q);q+=12
    assert mode==0x2c and (page>>7)&3==0
    cx,cy=(clut&63)*16,clut>>6
    assert all(written[cy*1024+cx:cy*1024+cx+16]),hex(clut)
    pal=struct.unpack_from('<16H',vram,(cy*1024+cx)*2);pixels=[]
    for yy in range(v,v+h):
     for xx in range(u,u+w):
      at=(((page>>4)&1)*256+yy)*1024+(page&15)*64+xx//4;assert written[at]
      word=struct.unpack_from('<H',vram,at*2)[0];c=pal[(word>>(xx%4*4))&15]
      pixels.append(tuple((c>>k&31)*255//31 for k in (0,5,10))+(255 if c else 0,))
    im=Image.new('RGBA',(w,h));im.putdata(pixels)
    if flags&1:im=im.transpose(Image.Transpose.FLIP_LEFT_RIGHT);x=-x-w
    if flags&2:im=im.transpose(Image.Transpose.FLIP_TOP_BOTTOM);y=-y-h
    parts.append((im,x,y))
    if flags&128:break
   left=min(x for im,x,y in parts);top=min(y for im,x,y in parts);right=max(x+im.width for im,x,y in parts);bottom=max(y+im.height for im,x,y in parts)
   result=Image.new('RGBA',(right-left,bottom-top))
   for im,x,y in reversed(parts):result.alpha_composite(im,(x-left,y-top))
   assert result.getbbox()
   name=f'Beldo_variant{variant}_frame{frame}.png';dest=OUT/name
   if dest.exists():assert dest.read_bytes() or False
   else:result.save(dest)
   meta=Path(str(dest)+'.meta')
   if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'azure/portrait/'+name).hex+'\n')
   frames.append(dict(file=name,ticks=ticks,chain=hex(chain),width=result.width,height=result.height,anchor=[-left,-top]))
   all_images.append(result)
  report['variants'].append(dict(variant=variant,animation=hex(anim),resource=origin,frames=frames,control=dict(kind=kind,pointer=hex(chain))))
 target=W/'Validation/BeldoPortraits';target.mkdir(parents=True,exist_ok=True)
 (target/'manifest.json').write_text(json.dumps(report,indent=2))
 project_manifest=P/'Assets/AzureDreams/Data/Extracted/BeldoPortraits/manifest.json'
 project_manifest.parent.mkdir(parents=True,exist_ok=True)
 project_manifest.write_text(json.dumps(report,indent=2))
 canvas=Image.new('RGBA',(len(all_images)*144,160),(35,35,45,255))
 for i,im in enumerate(all_images):canvas.alpha_composite(im,(i*144+(144-im.width)//2,160-im.height))
 canvas.save(target/'portraits.png')
 print([(v['variant'],len(v['frames']),[f['ticks'] for f in v['frames']]) for v in report['variants']])
if __name__=='__main__':main()
