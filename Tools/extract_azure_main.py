"""Recover verified data using azure-main's spec and splat boundaries."""
from pathlib import Path
import sys,json,hashlib,re,unicodedata
import yaml
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Azure Dreams Reimagined/Tools'))
from psx_disc import DiscImage
repo=ROOT/'azure-main'; out=ROOT/'extracted_assets/azure_main'; out.mkdir(parents=True,exist_ok=True)
disc=DiscImage(ROOT/'Azure Dreams.bin'); spec=yaml.safe_load((repo/'spec.yaml').read_text()); reports=[]; blobs={}
for record in spec['files']:
 data=disc.read_data(record['sector'],(record['size']+2047)//2048)[:record['size']]
 actual=hashlib.sha1(data).hexdigest()
 if actual!=record['shasum']: raise ValueError('Source differs: '+record['name'])
 path=out/'verified_files'/record['name']; path.parent.mkdir(parents=True,exist_ok=True); path.write_bytes(data)
 blobs[Path(record['name']).name]=data
 reports.append(dict(file=record['name'],bytes=len(data),sha1=actual,status='verified'))
exe=blobs['slus_006.14']
for name,start,end,expected in [('items_and_spells',0x2810,0x660c,405),('credits',0x2184,0x27dc,53)]:
 rows=[]; pos=start
 while pos<end:
  stop=exe.index(0,pos,end); raw=exe[pos:stop]; text=raw.decode('shift_jis')
  rows.append(dict(file_offset=hex(pos),resident_address=hex(pos+0x8002c800),text=text,normalized_text=unicodedata.normalize('NFKC',text),raw_hex=raw.hex()))
  pos=(stop+4)&~3
 if pos!=end or len(rows)!=expected: raise ValueError('String pool contract differs: '+name)
 (out/(name+'.json')).write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
sections=[]
for config in sorted((repo/'splat').glob('*.yaml')):
 desc=yaml.safe_load(config.read_text(encoding="utf8")); name=desc.get('name')
 if name not in blobs: continue
 data=blobs[name]
 if hashlib.sha1(data).hexdigest()!=desc['sha1']: raise ValueError('Splat source differs')
 segments=desc['segments']
 for i,seg in enumerate(segments):
  start=seg.get('start') if isinstance(seg,dict) else seg[0]
  if i+1==len(segments): continue
  nxt=segments[i+1]; end=nxt.get('start') if isinstance(nxt,dict) else nxt[0]
  if not isinstance(start,int) or not isinstance(end,int) or not 0<=start<=end<=len(data): continue
  kind=seg.get('type') if isinstance(seg,dict) else seg[1]
  label=seg.get('name',kind) if isinstance(seg,dict) else (seg[2] if len(seg)>2 else kind)
  label=re.sub(r'[^A-Za-z0-9_.-]','_',str(label))
  folder=out/'mapped_sections'/name; folder.mkdir(parents=True,exist_ok=True)
  file=folder/f'{start:08x}_{end:08x}_{label}.bin'; file.write_bytes(data[start:end])
  sections.append(dict(container=name,start=hex(start),end=hex(end),kind=kind,label=label,bytes=end-start,sha256=hashlib.sha256(data[start:end]).hexdigest(),vram=hex(seg['vram']) if isinstance(seg,dict) and isinstance(seg.get('vram'),int) else None))
(out/'manifest.json').write_text(json.dumps(dict(verified_files=reports,mapped_sections=sections,string_pools={'items_and_spells':405,'credits':53},limits='Raw source-map sections are not decoded textures or models. Symbol and overlay mappings are decompilation evidence, not complete event specifications.'),indent=2)+'\n')
print(json.dumps(dict(verified_files=len(reports),mapped_sections=len(sections),item_spell_strings=405,credit_strings=53)))
