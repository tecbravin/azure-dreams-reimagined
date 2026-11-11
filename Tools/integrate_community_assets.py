from pathlib import Path
import json,hashlib,shutil,os,re,collections
ROOT=Path.cwd();P=ROOT/'Azure Dreams Reimagined';A=P/'Assets';BASE=A/'AzureDreams';D=BASE/'Data/Community';D.mkdir(parents=True,exist_ok=True);records=[]
def sha(f):return hashlib.sha256(f.read_bytes()).hexdigest()
existing=collections.defaultdict(list)
for f in A.rglob('*.png'):existing[sha(f)].append(f)
def put(src,dst,transform=None,dedup=False):
 h=sha(src)
 if dedup and h in existing:
  match=existing[h][0];records.append(dict(source=str(src.relative_to(ROOT)),asset=str(match.relative_to(P)).replace('\\','/'),sha256=h,status='reused_exact_bytes'));return
 dst.parent.mkdir(parents=True,exist_ok=True)
 if transform:dst.write_text(transform(src.read_text()),encoding='utf8')
 else:shutil.copy2(src,dst)
 records.append(dict(source=str(src.relative_to(ROOT)),asset=str(dst.relative_to(P)).replace('\\','/'),sha256=sha(dst),status='copied_with_mtl_paths' if transform else 'copied'))
 if dst.suffix=='.png':existing[h].append(dst)
for f in (ROOT/'community').rglob('*.png'):
 if 'monster_animations' in f.parts:
  m=re.match(r'monster_([0-9a-f]+)_animation_([0-9a-f]+)_frame_',f.stem);dest=BASE/'Sprites/Monsters/Community'/f'monster_{m[1]}'/f'animation_{m[2]}'/f.name
 elif f.name=='koh-walking-animation.png':dest=BASE/'Sprites/Characters/Koh/Community'/f.name
 elif f.name in ('guy.png','kewne.png'):dest=BASE/'Sprites/Characters'/f.stem.title()/'Community'/f.name
 else:dest=BASE/'Sprites/Monsters/Community/Sheets'/f.name
 put(f,dest,dedup=True)
for f in (P/'Documentation/Community').glob('*.json'):put(f,D/f.name)
put(P/'Documentation/Community/ItemsGuide.txt',BASE/'Data/Items/Community/ItemsGuide.txt')
for f in (ROOT/'community').iterdir():
 if f.suffix in ('.pdf','.ods','.mhtml'):put(f,P/'Documentation/Community/Sources'/f.name)
state=ROOT/'extracted_assets/ad_state_display/resources/resources'
for f in state.rglob('*'):
 if not f.is_file():continue
 kind=f.parent.name
 if kind=='fonts':dest=BASE/'Fonts/Community/ADStateDisplay'/f.name
 elif kind=='portraits':dest=BASE/'Sprites/Portraits/ADStateDisplay'/f.name
 elif kind=='items':dest=BASE/'Sprites/Items/ADStateDisplay'/f.name
 else:dest=BASE/'Sprites/UI/ADStateDisplay'/kind/f.name
 put(f,dest,dedup=f.suffix=='.png')
scenes=ROOT/'extracted_assets/azure_dreams_3d_models'
for f in scenes.rglob('*'):
 if not f.is_file():continue
 rel=f.relative_to(scenes)
 if f.suffix=='.png':dest=BASE/'Textures/Town/CapturedScenes'/rel
 elif f.suffix in ('.obj','.mtl'):dest=BASE/'Models/Town/CapturedScenes'/rel
 elif f.suffix=='.bin':dest=A/'StreamingAssets/Extracted/CapturedScenes'/rel
 else:dest=BASE/'Data/Town/CapturedScenes'/rel
 if f.suffix=='.mtl':
  prefix=os.path.relpath(BASE/'Textures/Town/CapturedScenes'/rel.parent/'textures',dest.parent).replace('\\','/')+'/'
  put(f,dest,lambda t,prefix=prefix:t.replace('textures/',prefix))
 else:put(f,dest)
for name in ('items_and_spells.json','credits.json'):
 put(ROOT/'extracted_assets/azure_main'/name,BASE/('Data/Items/Retail' if name.startswith('items') else 'Data/Credits/Retail')/name)
# Source record IDs and capture filename IDs are deliberately separate.
scores=json.loads((D/'building_scores.json').read_text(encoding='utf8'));layout=json.loads((BASE/'Resources/Town/Retail/town_layout_metadata.json').read_text())
rawrecords={r['record_id']:r for r in layout['referenced_building_records']} if 'referenced_building_records' in layout else {}
town_blob=(ROOT/'extracted_assets/files/TOWN/TOWN.BIN').read_bytes()
town_hash=hashlib.sha256(town_blob).hexdigest()
assert town_hash=='907725012d8d9e1deab18c28b62f4d304057e86a6415df605b9ffb87cfb2fe18'
def source_building_record(record_id):
 offset=0x800d2644-0x8007d760+record_id*32
 return dict(record_id=record_id,file_offset=hex(offset),raw_hex=town_blob[offset:offset+32].hex(),source_sha256=town_hash,semantic_identity='not_proven_by_numeric_id')
links=[]
for score in scores:
 candidates=[]
 for folder in scenes.glob('ad_model_building_*'):
  name=score['name'].lower();stem=folder.name
  alias={'koh\'s house lv 1':'01_koh_house_1','koh\'s house lv 2':'02_koh_house_2','monster hut lv 1':'26_monster_hut_3x3','monster hut lv 2':'27_monster_hut_4x4','monster hut lv 3':'28_monster_hut_6x6','monster hut lv 4':'29_monster_hut_8x8'}
  key=alias.get(name,name.replace('racetrack','racetrack_entrance').replace(' ','_'))
  if key in stem:candidates.append(dict(capture=folder.name,model_asset=str((BASE/'Models/Town/CapturedScenes'/folder.name/'catalog.obj').relative_to(P)).replace('\\','/'),capture_id=int(stem.split('_')[3],16),match_basis='supplied names; upgrade/size correspondence requires retail verification'))
 links.append(dict(community_record=score,retail_record=source_building_record(score['building_id']),capture_candidates=candidates,validation='reference_crosswalk_only_not_runtime_binding'))
(D/'building_crosswalk.json').write_text(json.dumps(links,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
(D/'integration_manifest.json').write_text(json.dumps(records,indent=2)+'\n')
for r in records:assert sha(P/r['asset'])==r['sha256']
for f in (BASE/'Models/Town/CapturedScenes').rglob('*.mtl'):
 for line in f.read_text().splitlines():
  if line.startswith(('map_Kd ','map_d ')):assert (f.parent/line.split(' ',1)[1]).is_file()
print('FILES',len(records),'STATUS',dict(collections.Counter(r['status'] for r in records)),'CROSSWALKS',len(links),'raw record fields',list(layout.keys()))
