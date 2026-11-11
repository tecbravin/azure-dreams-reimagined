"""Extract named Qt resources from ADStateDisplay's native PE symbol tables."""
from pathlib import Path
import struct,json,hashlib,zlib
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]; exe=ROOT/'ADStateDisplay_v5.0/ADStateDisplay.exe'; b=exe.read_bytes(); out=ROOT/'extracted_assets/ad_state_display'; out.mkdir(parents=True,exist_ok=True)
pe=struct.unpack_from('<I',b,60)[0]; _,n,_,ptr,count,opt,_=struct.unpack_from('<HHIIIHH',b,pe+4); st=ptr+count*18; symbols={}
for i in range(count):
 p=ptr+i*18; name=b[p:p+8]
 if name[:4]==bytes(4):
  offset=struct.unpack_from('<I',name,4)[0]; stop=b.index(0,st+offset); name=b[st+offset:stop]
 else:name=name.rstrip(b'\0')
 if name in (b'_ZL16qt_resource_data',b'_ZL16qt_resource_name',b'_ZL18qt_resource_struct'):
  value,section=struct.unpack_from('<Ih',b,p+8); header=pe+24+opt+(section-1)*40; raw=struct.unpack_from('<I',b,header+20)[0]; symbols[name.decode()]=raw+value
names=symbols['_ZL16qt_resource_name']; tree=symbols['_ZL18qt_resource_struct']; data=symbols['_ZL16qt_resource_data']; records=[]; visited=set()
def visit(index,parent):
 if index in visited or index>10000: raise ValueError('Invalid resource tree')
 visited.add(index); p=tree+index*22; no,flags=struct.unpack_from('>IH',b,p)
 namepos=names+no; length=struct.unpack_from('>H',b,namepos)[0]; name=b[namepos+6:namepos+6+length*2].decode('utf-16be') if index else ''
 if name in ('.','..') or '/' in name or '\\' in name: raise ValueError('Unsafe resource path')
 path=parent/name if name else parent
 if flags&2:
  count,first=struct.unpack_from('>II',b,p+6)
  if count>10000:raise ValueError('Invalid child count')
  for child in range(first,first+count):visit(child,path)
 else:
  offset=struct.unpack_from('>I',b,p+10)[0]; size=struct.unpack_from('>I',b,data+offset)[0]; payload=b[data+offset+4:data+offset+4+size]
  if len(payload)!=size: raise ValueError('Truncated resource')
  if flags&1:
   expected=struct.unpack_from('>I',payload)[0]; payload=zlib.decompress(payload[4:]); assert len(payload)==expected
  if flags&4:raise ValueError('Zstd resource not yet supported')
  dest=out/'resources'/path; dest.parent.mkdir(parents=True,exist_ok=True); dest.write_bytes(payload)
  if dest.suffix.lower()=='.png':
   with Image.open(dest) as im: im.verify()
  records.append(dict(path=path.as_posix(),flags=flags,bytes=len(payload),sha256=hashlib.sha256(payload).hexdigest()))
visit(0,Path())
(out/'resource_manifest.json').write_text(json.dumps(dict(executable_sha256=hashlib.sha256(b).hexdigest(),resources=records,scope='Resources bundled by a third-party state display tool; not asserted byte-identical assets from the original disc.'),indent=2)+'\n')
from collections import Counter
print('Resources:',len(records),'Extensions:',dict(Counter(Path(r['path']).suffix for r in records)))
print('Folders:',dict(Counter(str(Path(r['path']).parent) for r in records)))
