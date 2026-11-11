from pathlib import Path
import shutil,json,hashlib,os
root=Path.cwd(); project=root/'Azure Dreams Reimagined'; assets=project/'Assets'; target=assets/'AzureDreams'; records=[]
def copy(src,dst,transform=None):
 dst.parent.mkdir(parents=True,exist_ok=True)
 if transform: dst.write_text(transform(src.read_text()),encoding='utf8')
 else: shutil.copy2(src,dst)
 records.append(dict(source=str(src.relative_to(root)),asset=str(dst.relative_to(project)).replace('\\','/'),sha256=hashlib.sha256(dst.read_bytes()).hexdigest(),adapted=bool(transform)))
for f in (root/'extracted_assets/ad_resources_dumper').iterdir():
 if not f.is_file(): continue
 if f.name=='town_vram.png': dest=target/'Textures/Extracted/AdResourcesDumper/TownVRAM'/f.name
 elif f.suffix=='.png': dest=target/'Sprites/Portraits/AdResourcesDumper'/('Components' if '_element' in f.stem else 'Frames')/f.name
 else: dest=target/'Data/Extracted/AdResourcesDumper'/f.name
 copy(f,dest)
source=root/'extracted_assets/virtual_monsbaia'
for f in source.rglob('*'):
 if not f.is_file(): continue
 rel=f.relative_to(source)
 if rel.parts[0]=='textures': dest=target/'Textures/Extracted/VirtualMonsbaia'/f.name
 elif f.suffix in ('.obj','.mtl'): dest=target/'Models/Extracted/VirtualMonsbaia'/rel
 elif f.suffix in ('.3dm','.bin'): dest=assets/'StreamingAssets/Extracted/VirtualMonsbaia'/rel
 else: dest=target/'Data/Extracted/VirtualMonsbaia'/rel
 if f.suffix=='.mtl':
  texdir=target/'Textures/Extracted/VirtualMonsbaia'
  prefix=os.path.relpath(texdir,dest.parent).replace('\\','/')+'/'
  copy(f,dest,lambda s,prefix=prefix:s.replace('../textures/',prefix))
 else: copy(f,dest)
report=target/'Data/Extracted/import_manifest.json'; report.parent.mkdir(parents=True,exist_ok=True); report.write_text(json.dumps(records,indent=2)+'\n')
print('Imported',len(records),'files; bytes',sum((project/r['asset']).stat().st_size for r in records))
for m in (target/'Models/Extracted/VirtualMonsbaia').rglob('*.mtl'):
 for line in m.read_text().splitlines():
  if line.startswith(('map_Kd ','map_d ')): assert (m.parent/line.split(' ',1)[1]).is_file(),line
print('All OBJ material texture paths resolve')
