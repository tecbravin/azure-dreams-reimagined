from pathlib import Path
import json,struct
from PIL import Image
P=Path(__file__).resolve().parents[1];root=P.parent
s=root/'extracted_assets/reimagined_pipeline/TownSpriteBanks/town_koh';g=json.loads((s/'animations.json').read_text())['groups'];frames={f['pointer']:f for f in json.loads((s/'frames.json').read_text())}
b=(root/'extracted_assets/full_disc/files/TOWN/TOWN.BIN').read_bytes();ids=list(b[0x800d0c50-0x8007d760:0x800d0c50-0x8007d760+8]);entries=[];directions=[]
for group in ids:
 seq=[e for e in g[group]['entries'] if 'frame_png' in e];start=len(entries);entries+=seq;directions.append(dict(frames=list(range(start,len(entries))),ticks=[max(1,e['ticks_raw']) for e in seq]))
atlas=Image.new('RGBA',(8*96,((len(entries)+7)//8)*96))
for i,e in enumerate(entries):
 im=Image.open(s/e['frame_png']).convert('RGBA');x,y=frames[e['pointer']]['origin'];atlas.alpha_composite(im,(i%8*96+16+x,i//8*96+16+y))
out=P/'Assets/AzureDreams/Resources/Town/AuraBike';atlas.save(out/'Rider.bytes',format='PNG');(out/'RiderTimings.json').write_text(json.dumps(dict(frameCount=len(entries),directions=directions,groups=ids,source='TOWN D_800D0C50')))
print('Rider groups',ids,'frames',len(entries))

