"""Measure resident foot pixels below the authored pivot; change metadata only."""
import json
from pathlib import Path
from PIL import Image

def repair_bank(path):
 bank=json.loads(path.read_text(encoding='utf-8'))
 if not all(k in bank for k in ('cellSize','columns','pivotY','clips')):return None
 frames=set()
 for clip in bank['clips']:
  if clip['key'] in ('idle','walk'):
   for direction in clip['directions']:frames.update(direction['frames'])
 if not frames:return None
 with Image.open(path.with_suffix('.png')) as image:
  alpha=image.convert('RGBA').getchannel('A');cell=bank['cellSize'];bottom=cell*(1-bank['pivotY']);clearance=0
  for frame in frames:
   x=frame%bank['columns']*cell;y=frame//bank['columns']*cell
   bounds=alpha.crop((x,y,x+cell,y+cell)).getbbox()
   if bounds:clearance=max(clearance,bounds[3]-bottom)
 bank['groundingPixels']=round(max(0,clearance),4)
 path.write_text(json.dumps(bank,ensure_ascii=True,indent=1)+'\n',encoding='utf-8')
 return bank['groundingPixels']

if __name__=='__main__':
 root=Path(__file__).resolve().parents[1]/'Assets/AzureDreams/Resources/Town/Residents/Retail'
 results={p.stem:repair_bank(p) for p in root.glob('*.json') if p.with_suffix('.png').exists()}
 print(json.dumps(results,sort_keys=True))
