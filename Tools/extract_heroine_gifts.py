"""Extract the retail seven-heroine gift matrices and response scripts (no invented responses)."""
from pathlib import Path
import struct,json,unicodedata,uuid
root=Path(__file__).resolve().parents[2];project=root/'Azure Dreams Reimagined'
t=(root/'TOWN/TOWN.BIN').read_bytes();exe=(root/'extracted_assets/files/SLUS_006.14').read_bytes()
tbase=0x8007d760;ebase=struct.unpack_from('<I',exe,0x18)[0]-0x800
names=['nico','selfi','fur','mia','patty','vivian','cherrl'];groups=list(struct.unpack_from('<7h',t,0x800d1054-tbase));flags=list(struct.unpack_from('<7h',t,0x800d1064-tbase))
matrices=[list(struct.unpack_from('<16I',t,p-tbase)) for p in struct.unpack_from('<7I',t,0x800d1038-tbase)]
def response(a):
 pos=a-ebase;start=pos;pages=[];chars=[];accept=False;increment=False
 while pos<start+1024:
  c=exe[pos]
  if c==1:break
  if c==8 and exe[pos+1]==0x15:pos+=6;continue
  if c==0x4c:
   call=struct.unpack_from('<I',exe,pos+1)[0];accept |= call==0x800a9d00;increment |= call==0x800a9c18;pos+=5;continue
  if c==0x34:pos+=6;continue
  if c in (0x11,8):
   if chars:pages.append(''.join(chars));chars=[]
   pos+=1;continue
  if c==10:chars.append('\n');pos+=1;continue
  if 0x81<=c<=0x9f or 0xe0<=c<=0xef:
   chars.append(unicodedata.normalize('NFKC',exe[pos:pos+2].decode('shift_jis')));pos+=2;continue
  raise ValueError((hex(a),hex(pos+ebase),hex(c)))
 if chars:pages.append(''.join(chars))
 return dict(address=a,pages=pages,accepted=accept,increment=increment)
addresses=sorted(set(sum(matrices,[])+[0x8006bc7a]))
data=dict(source='SLUS_006.14 gift scripts 0x8006B32C–0x8006C894; TOWN.BIN matrices 0x800D0E78; item_query_checks_g01145.c',heroes=[dict(id=n,group=g,loveFlag=f,matrix=m) for n,g,f,m in zip(names,groups,flags,matrices)],responses=[response(a) for a in addresses])
translations=json.loads((Path(__file__).parent/'heroine_gifts_pt.json').read_text(encoding='utf-8'))
for r in data['responses']:
 r['portuguese']=translations[str(r['address'])]
 assert len(r['portuguese'])==len(r['pages'])
p=project/'Assets/AzureDreams/Resources/Town/Retail/heroine-gifts.json';p.write_text(json.dumps(data,ensure_ascii=True,indent=2)+'\n',encoding='utf-8')
if not Path(str(p)+'.meta').exists():Path(str(p)+'.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
print(len(data['heroes']),'heroines;',len(data['responses']),'native response scripts')

