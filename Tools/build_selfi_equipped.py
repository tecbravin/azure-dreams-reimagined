"""Build Selfi's equipped field bank from her complete original dungeon part chains."""
from pathlib import Path
import json,struct,uuid,re
from PIL import Image
from build_retail_monster_actions import frame_parts,CELL,ORIGIN
from repair_resident_grounding import repair_bank
root=Path(__file__).resolve().parents[2];p=root/'Azure Dreams Reimagined';src=p/'Assets/AzureDreams/Sprites';out=p/'Assets/AzureDreams/Resources/Town/Residents/Retail'
b=(root/'DUNGEON/DUNGEON.BIN').read_bytes();bank=(0x56D6-12310+49*43)*2048
metadata=json.loads((src/'DungeonCreatures/resource_050/metadata.json').read_text());header=struct.unpack_from('<10I',b,bank);assert list(header)==metadata['header_words']
offsets=struct.unpack_from('<48I',b,bank+40)+(header[2],);chains={c['pointer']:c['parts'] for c in metadata['chains']};indices=Image.open(src/'Indexed/resource_050/atlas_indices.png').convert('L');words=struct.unpack_from('<256H',b,bank+26*2048+0x8000)
palette=[((w&31)*255//31,((w>>5)&31)*255//31,((w>>10)&31)*255//31,255 if w else 0) for w in words]
frames=[];clips=[]
for key,base in [('idle',33),('walk',18)]:
 directions=[]
 for direction in [2,0,2,4]:
  sequence=[];ticks=[];entry=base+direction
  for pos in range(offsets[entry],offsets[entry+1],8):
   duration,kind,ptr=struct.unpack_from('<HHI',b,bank+pos)
   if kind!=2:continue
   encoded=frame_parts(chains[ptr],indices,words);image=Image.new('RGBA',encoded.size);image.putdata([palette[v[0]] if v[3] else (0,0,0,0) for v in encoded.get_flattened_data()]);assert image.getbbox()
   sequence.append(len(frames));ticks.append(duration);frames.append(image)
  directions.append(dict(frames=sequence,ticks=ticks,loop=True))
 clips.append(dict(key=key,directions=directions))
atlas=Image.new('RGBA',(CELL*8,((len(frames)+7)//8)*CELL))
for i,im in enumerate(frames):atlas.alpha_composite(im,((i%8)*CELL,(i//8)*CELL))
name='selfy_equipped';atlas.save(out/(name+'.png'));data=dict(cellSize=CELL,columns=8,frameCount=len(frames),pivotX=ORIGIN[0]/CELL,pivotY=(CELL-ORIGIN[1])/CELL,ticksPerSecond=30,clips=clips,source=dict(resource=50,bankOffset=hex(bank),idle=33,walk=18));(out/(name+'.json')).write_text(json.dumps(data,indent=1)+'\n',encoding='utf-8');repair_bank(out/(name+'.json'))
for ext in ['png','json']:
 meta=out/(name+'.'+ext+'.meta')
 if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
print(len(frames),'complete equipped cels, original CLUTs and timings')


# Use the known field-sprite texture importer, not a generic DefaultImporter.
meta=out/(name+'.png.meta');guid=re.search(r'guid: (\w+)',meta.read_text()).group(1)
meta.write_text(re.sub(r'guid: \w+','guid: '+guid,(out/'selfy.png.meta').read_text()),encoding='utf-8')
