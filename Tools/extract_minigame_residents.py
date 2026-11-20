"""Reassemble Gym/Casino residents from native slot state -> direction -> frame tables.
The state tables (field 0x7C) contain direction-table pointers; they are NOT
sprite chains. Palette overrides come from each original constructor.
"""
from pathlib import Path
import sys,struct,json
from PIL import Image,ImageDraw
import extract_vivian_town_sprites as v
P=Path(__file__).resolve().parents[1];W=P.parent
JOBS={'Kim':(0x800D5EF4,'08_gym',0),'CasinoGambler':(0x800D5DD4,'06_casino_2',0),'CasinoBroke':(0x800D5774,'06_casino_2',0),'RouletteAttendant':(0x800D6C4C,'06_casino_2',0x2c0),'CasinoLoser':(0x800D7238,'06_casino_2',0),'SlotAttendant':(0x800D5240,'2b_casino_1',0),'CasinoWoman':(0x800D71C4,'2b_casino_1',0),'CasinoOldMan':(0x800D6C4C,'2b_casino_1',0)}
JOBS.update({"ArcadeBoy":(0x800D63BC,"09_arcade_1",0), "ArcadeGirl":(0x800D5E80,"09_arcade_1",0)})
def main():
 b=(W/'extracted_assets/full_disc/files/TOWN/TOWN.BIN').read_bytes();v.CELL=128;v.ORIGIN_X=64;v.ORIGIN_Y=100
 out=P/'Assets/AzureDreams/Resources/Town/Residents';out.mkdir(parents=True,exist_ok=True)
 report=[];preview=Image.new('RGBA',(640,len(JOBS)*128),(50,50,60,255));draw=ImageDraw.Draw(preview)
 for row,(name,(table,scene,clut_delta)) in enumerate(JOBS.items()):
  vr=(W/f'extracted_assets/azure_dreams_3d_models/ad_model_building_{scene}/vram.bin').read_bytes()
  written=bytes([1])*524288
  # Native base state table orders stand, walk, stop. All four facings survive.
  state=struct.unpack_from('<3I',b,table-v.BASE)
  clips=[];seqs={};chains={}
  for key,tb in [('idle',state[0]),('walk',state[1]),('stand',state[2])]:
   ps=struct.unpack_from('<5I',b,tb-v.BASE);assert ps[4]==0
   for ptr in ps[:4]:
    seqs[ptr]=v.read_sequence(b,ptr)
    for entry in seqs[ptr]['entries']:
     cp=int(entry['chain'],16);chains[cp]=[dict(p,clut=p['clut']+clut_delta) for p in v.read_chain(b,cp)]
   clips.append((key,ps[:4]))
  frames=sorted(chains);ids={p:i for i,p in enumerate(frames)}
  atlas=Image.new('RGBA',(8*128,((len(frames)+7)//8)*128))
  for cp,i in ids.items():
   frame=v.render(chains[cp],vr,written);atlas.alpha_composite(frame,(i%8*128,i//8*128))
  path=out/(name+'.png');atlas.save(path);v.write_meta(path,'texture')
  runtime=[]
  for key,ps in clips:
   runtime.append(dict(key=key,directions=[dict(frames=[ids[int(e['chain'],16)] for e in seqs[p]['entries']],ticks=[e['ticks'] for e in seqs[p]['entries']],loop=seqs[p]['loop']) for p in ps]))
  data=dict(cellSize=128,columns=8,frameCount=len(frames),pivotX=.5,pivotY=28/128,ticksPerSecond=60,clips=runtime)
  path=out/(name+'.json');path.write_text(json.dumps(data,separators=(',',':')));v.write_meta(path,'text')
  for i,p in enumerate(clips[0][1]):
   frame=v.render(chains[int(seqs[p]['entries'][0]['chain'],16)],vr,written);preview.alpha_composite(frame,(i*128,row*128))
  draw.text((515,row*128+40),name,fill='white')
  report.append(dict(name=name,stateTable=hex(table),directionTables=[hex(x) for x in state],clutOffset=clut_delta,frames=len(frames),sequences=len(seqs),sceneVram=scene))
 dest=W/'Validation/MinigameGraphics';dest.mkdir(exist_ok=True,parents=True);preview.save(dest/'residents.png');(dest/'residents.json').write_text(json.dumps(report,indent=2));print([(x['name'],x['frames']) for x in report])
if __name__=='__main__':main()
