from pathlib import Path
import json,re,hashlib,collections
from PIL import Image
ROOT=Path.cwd();doc=ROOT/'Azure Dreams Reimagined/Documentation/Community';sheets=json.loads((doc/'GameObjectives.json').read_text(encoding='utf8'))
buildings=[];events=[]
for sheet in sheets:
 if sheet['sheet']=='Buildings score':
  for row in sheet['rows'][1:]:
   c={v['column']:v['text'] for v in row['cells']}
   buildings.append(dict(source_sheet=sheet['sheet'],source_row=row['row'],building_id=int(c[1]),building_id_hex=c[2],name=c[3],score=int(c[4]),validation='community_reference_not_retail_verified'))
 if sheet['sheet']=='Girlfriends':
  day=None;header={c['column']:c['text'] for c in sheet['rows'][0]['cells']};routes={c['column']:c['text'] for c in sheet['rows'][1]['cells']}
  for row in sheet['rows'][2:]:
   c={v['column']:v['text'] for v in row['cells']}
   if 1 in c:day=c[1]
   for col,text in c.items():
    if col<=2:continue
    events.append(dict(source_row=row['row'],source_column=col,day_label=day,location=c.get(2),column_label=header.get(col),route_label=routes.get(col),text=text,validation='community_reference_not_retail_verified'))
(doc/'building_scores.json').write_text(json.dumps(buildings,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
(doc/'town_event_reference.json').write_text(json.dumps(events,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
animations=collections.defaultdict(list);images=[]
for f in (ROOT/'community').rglob('*.png'):
 with Image.open(f) as im:
  size=im.size;im.verify()
 entry=dict(source=str(f.relative_to(ROOT)).replace('\\','/'),width=size[0],height=size[1],sha256=hashlib.sha256(f.read_bytes()).hexdigest());images.append(entry)
 match=re.fullmatch(r'monster_([0-9a-f]+)_animation_([0-9a-f]+)_frame_([0-9a-f]+)',f.stem)
 if match:animations[(int(match[1],16),int(match[2]))].append(int(match[3]))
groups=[dict(monster_id=k[0],animation_id=k[1],frames=sorted(v),contiguous=sorted(v)==list(range(max(v)+1)),timing='unknown',direction_semantics='unverified') for k,v in sorted(animations.items())]
(doc/'sprite_inventory.json').write_text(json.dumps(dict(images=images,animations=groups),indent=2)+'\n')
print(dict(building_scores=len(buildings),event_cells=len(events),pngs=len(images),animation_groups=len(groups),monster_ids=len({k[0] for k in animations}),noncontiguous=sum(not r['contiguous'] for r in groups)))
print('Town score records:',[(b['building_id_hex'],b['name'],b['score']) for b in buildings])
