"""Extract original Aura Bike/booster facing tables, timed clips and VRAM colors.
The zero-sized flag-0x20 command is a dynamic rider marker, not a texture quad.
"""
from pathlib import Path
import struct,json
from PIL import Image
import extract_vivian_town_sprites as v
P=Path(__file__).resolve().parents[1]
def main():
 b=(P.parent/'extracted_assets/full_disc/files/TOWN/TOWN.BIN').read_bytes()
 vr=(P.parent/'extracted_assets/azure_dreams_3d_models/ad_model_town_min/vram.bin').read_bytes()
 v.CELL=128;v.ORIGIN_X=64;v.ORIGIN_Y=100
 out=P/'Assets/AzureDreams/Resources/Town/AuraBike';out.mkdir(parents=True,exist_ok=True)
 def chain(cp):
  parts=[]
  for i in range(32):
   flags,mode,x,y,page,clut,u,w,ww,hh=struct.unpack_from('<BBbbHHBBBB',b,cp-v.BASE+i*12)
   assert mode==0x2c
   if ww and hh:parts.append(dict(flags=flags,mode=mode,x=x,y=y,tpage=page,clut=clut,u=u,v=w,width=ww,height=hh))
   else:assert flags&0x20
   if flags&128:return parts
  raise ValueError(cp)
 for name,tables in [('Bike',[0x800d0cc4,0x800d0ce8]),('Booster',[0x800d0d0c,0x800d0d30])]:
  chains={};clips=[]
  for table in tables:
   seqs=[]
   for ptr in struct.unpack_from('<8I',b,table-v.BASE):
    seq=v.read_sequence(b,ptr);seqs.append(seq)
    for e in seq['entries']:
     cp=int(e['chain'],16);chains[cp]=chain(cp)
   clips.append(seqs)
  ids={cp:i for i,cp in enumerate(chains)};atlas=Image.new('RGBA',(1024,128*((len(ids)+7)//8)))
  for cp,i in ids.items():atlas.alpha_composite(v.render(chains[cp],vr,bytes([1])*524288),(i%8*128,i//8*128))
  f=out/(name+'.png');atlas.save(f);v.write_meta(f,'texture')
  data={'frameCount':len(ids),'clips':[{'directions':[{'frames':[ids[int(e['chain'],16)] for e in seq['entries']],'ticks':[e['ticks'] for e in seq['entries']]} for seq in seqs]} for seqs in clips]}
  f=out/(name+'.json');f.write_text(json.dumps(data));v.write_meta(f,'text')
if __name__=='__main__':main()
