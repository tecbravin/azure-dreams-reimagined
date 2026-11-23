"""Recover the retail bath poses (SSTP_PLT_DM_BATH / BATH_TURN).
TOWN D_800D01E0 -> D_800D0060 selects groups 240/241; 204/205/207 are pot entry.
The original poses are static. Scene holds/turn timing below is staging, not PS1 ticks.
"""
from pathlib import Path
import json
from PIL import Image
PROJECT=Path(__file__).resolve().parents[1]
source=PROJECT.parent/'extracted_assets/reimagined_pipeline/TownSpriteBanks/town_koh'
groups=json.loads((source/'animations.json').read_text())['groups']
frames={f['pointer']:f for f in json.loads((source/'frames.json').read_text())}
sequences=[[240],[240,241],[241]]
atlas=Image.new('RGBA',(192,288))
for row,seq in enumerate(sequences):
 for col,g in enumerate(seq):
  e=next(e for e in groups[g]['entries'] if 'frame_png' in e)
  im=Image.open(source/e['frame_png']).convert('RGBA'); origin=frames[e['pointer']]['origin']
  atlas.alpha_composite(im,(col*96+16+origin[0],row*96+16+origin[1]))
dest=PROJECT/'Assets/AzureDreams/Resources/Town/Retail'
atlas.save(dest/'koh-bath.bytes',format='PNG')
(dest/'koh-bath-timings.json').write_text(json.dumps(dict(columns=2,enter=[24],soak=[90,90],exit=[24],groups=[240,241],source='TOWN D_800D01E0 -> D_800D0060; static bath/front and bath/turned poses; holds are reconstructed staging'))+'\n')
print('Recovered native bath poses 240/241')
