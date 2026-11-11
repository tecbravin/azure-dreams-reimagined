from pathlib import Path
import sys,json,zipfile,xml.etree.ElementTree as ET,hashlib,email.policy,email,re
ROOT=Path.cwd();sys.path.insert(0,str(ROOT/'.tools/community_readers'))
from pypdf import PdfReader
from bs4 import BeautifulSoup
from PIL import Image
out=ROOT/'Azure Dreams Reimagined/Documentation/Community';out.mkdir(parents=True,exist_ok=True)
manifest=[]
for f in (ROOT/'community').iterdir():
 if not f.is_file():continue
 manifest.append(dict(file=f.name,bytes=f.stat().st_size,sha256=hashlib.sha256(f.read_bytes()).hexdigest()))
 if f.suffix=='.pdf':
  pages=[dict(page=i+1,text=p.extract_text(extraction_mode='layout')) for i,p in enumerate(PdfReader(f).pages)]
  (out/(f.stem+'.json')).write_text(json.dumps(pages,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  print(f.name,'pages',len(pages));print('\n'.join(p['text'][:500] for p in pages[:2]))
 if f.suffix=='.ods':
  ns={'t':'urn:oasis:names:tc:opendocument:xmlns:table:1.0','x':'urn:oasis:names:tc:opendocument:xmlns:text:1.0'}
  with zipfile.ZipFile(f) as z:root=ET.fromstring(z.read('content.xml'))
  sheets=[]
  for tab in root.findall('.//t:table',ns):
   rows=[];ri=1
   for row in tab.findall('t:table-row',ns):
    ci=1;cells=[]
    for cell in row:
     text='\n'.join(''.join(p.itertext()) for p in cell.findall('x:p',ns));repeat=int(cell.get('{'+ns['t']+'}number-columns-repeated','1'))
     if text:cells.append(dict(column=ci,text=text,repeat=repeat))
     ci+=repeat
    repeat=int(row.get('{'+ns['t']+'}number-rows-repeated','1'))
    if cells:rows.append(dict(row=ri,cells=cells,repeat=repeat))
    ri+=repeat
   sheets.append(dict(sheet=tab.get('{'+ns['t']+'}name'),rows=rows))
  (out/'GameObjectives.json').write_text(json.dumps(sheets,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  for s in sheets:print('SHEET',s['sheet'],'rows',len(s['rows']));print(str(s['rows'][:3])[:1400])
 if f.suffix=='.mhtml':
  msg=email.message_from_bytes(f.read_bytes(),policy=email.policy.default)
  html=next(p.get_content() for p in msg.walk() if p.get_content_type()=='text/html');soup=BeautifulSoup(html,'html.parser')
  pre=soup.find('pre');text=pre.get_text() if pre else soup.get_text('\n',strip=True)
  (out/'ItemsGuide.txt').write_text(text,encoding='utf8');print('GUIDE',len(text),text[:600])
(out/'source_manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
