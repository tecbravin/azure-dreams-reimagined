"""Assemble the Troll body from verified retail sprite commands, omitting the separate equipment CLUT."""
from pathlib import Path
import sys,struct,json,re,uuid
r=Path(__file__).resolve().parents[2]
from PIL import Image,ImageOps
p=r/'Azure Dreams Reimagined';m=json.loads((r/'extracted_assets/verified_decode/sprites/resource_022/metadata.json').read_text());d=(r/'DUNGEON/DUNGEON.BIN').read_bytes();bank=(0x56d6-12310+21*43)*2048
assert list(struct.unpack_from('<10I',d,bank))==m['header_words']
idx=Image.open(r/'extracted_assets/verified_decode/sprites/resource_022/atlas_indices.png').convert('L');words=struct.unpack('<256H',(r/'extracted_assets/verified_decode/sprites/resource_022/clut_rgb555_le.bin').read_bytes());chains={c['pointer']:c['parts'] for c in m['chains']}
def rgb(w):return tuple((((w>>s)&31)<<3)|(((w>>s)&31)>>2) for s in [0,5,10])+(255 if w else 0,)
def frame(ptr):
 out=Image.new('RGBA',(64,64))
 for a in reversed(chains[ptr]):
  if a['is_callback'] or a['source_page']!='resource_atlas' or a['clut_raw']!=0:continue
  x,y,w,h=[a[k] for k in ['x','y','width','height']];tile=idx.crop((a['u'],a['v'],a['u']+w,a['v']+h));pic=Image.new('RGBA',(w,h));pic.putdata([rgb(words[i]) for i in tile.get_flattened_data()])
  if a['flags']&1:x=-x-w;pic=ImageOps.mirror(pic)
  if a['flags']&2:y=-y-h;pic=ImageOps.flip(pic)
  out.alpha_composite(pic,(32+x,60+y))
 return out
atlas=Image.new('RGBA',(1024,320));header=m['header_words'];offs=struct.unpack_from('<158I',d,bank+header[0])+(header[2],)
for row in range(5):
 seq=[]
 for off in range(offs[row],offs[row+1],8):
  tick,kind,ptr=struct.unpack_from('<HHI',d,bank+off)
  if kind==2:seq.append(frame(ptr))
 assert seq
 for col in range(16):atlas.alpha_composite(seq[min(col,len(seq)-1)%len(seq)],(col*64,row*64))
f=p/'Assets/AzureDreams/Resources/Tower/TrollUnarmedActions.png';atlas.save(f)
meta=Path(str(f)+'.meta')
if not meta.exists():meta.write_text(re.sub(r'(?m)^guid: .*','guid: '+uuid.uuid4().hex,(p/'Assets/AzureDreams/Resources/Tower/TrollSwordActions.png.meta').read_text()))
print('Rebuilt unarmed Troll from the original body CLUT, excluding equipment parts')
