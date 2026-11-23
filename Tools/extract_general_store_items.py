"""Extract authenticated NTSC-U General Store text records and their native menu icons.
No guessed furniture models or redesigned images are used. Most furniture shares icon 63.
"""
from pathlib import Path
import hashlib,json,struct,unicodedata,uuid
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[1];WORK=ROOT.parent
OUT=ROOT/'Assets/AzureDreams/Resources/Town/GeneralStore'
DELTA=0x8007D760
IDS=['bed','canopy','table','bonsai','plant','carpet_1','carpet_2','carpet_3','tiger_fur','stuffed_deer','wallpaper_1','wallpaper_2','wallpaper_3','painting','scrolls','poster','sculpture','something','tv','fridge','washer','range','light','vest','jacket','trousers','shoes','aura_bike','booster','telephone','trophy','remove']
def exeoff(p):return p-0x8002D000+0x800
def text(data,p):
 raw=data[p:data.index(0,p)]
 return unicodedata.normalize('NFKC',raw.decode('shift_jis')).replace('\u2019',"'")
def meta(p):
 m=Path(str(p)+'.meta')
 if m.exists():return
 guid=uuid.uuid5(uuid.NAMESPACE_URL,'azure/retail/general-store/'+p.name).hex
 if p.suffix=='.png':
  import re
  s=(ROOT/'Assets/AzureDreams/Resources/Tower/MusicBox.png.meta').read_text()
  s=re.sub(r'guid: [a-f0-9]+','guid: '+guid,s)
 else:s=f'fileFormatVersion: 2\nguid: {guid}\nTextScriptImporter:\n  externalObjects: {{}}\n'
 m.write_text(s)
def icon(exe,vram,index,dest):
 pointer=struct.unpack_from('<I',exe,exeoff(0x8007361C)+index*4)[0]
 flags,mode,x,y,page,clut,u,v,w,h=struct.unpack_from('<BBbbHH4B',exe,exeoff(pointer))
 assert mode==0x2c and flags&0x80 and (page>>7)&3==0 and (w,h)==(16,16)
 palette=struct.unpack_from('<16H',vram,((clut>>6)*1024+(clut&63)*16)*2)
 pixels=[]
 for yy in range(v,v+h):
  for xx in range(u,u+w):
   word=struct.unpack_from('<H',vram,((((page>>4)&1)*256+yy)*1024+(page&15)*64+xx//4)*2)[0]
   c=palette[word>>(xx%4*4)&15]
   pixels.append(tuple((c>>k&31)*255//31 for k in (0,5,10))+(255 if c else 0,))
 im=Image.new('RGBA',(w,h));im.putdata(pixels);assert im.getbbox(),index
 im.save(dest);meta(dest)
 return dict(index=index,descriptor=hex(pointer),tpage=page,clut=clut,u=u,v=v,width=w,height=h)
def main():
 town=(WORK/'TOWN/TOWN.BIN').read_bytes();exe=(WORK/'SLUS_006.14').read_bytes()
 assert hashlib.sha256(town).hexdigest()=='907725012d8d9e1deab18c28b62f4d304057e86a6415df605b9ffb87cfb2fe18'
 assert hashlib.sha256(exe).hexdigest()=='ae19d8d4bdd2db9a933b1e44d70dd61be670781e441d7cb37da85250a7aed31f'
 vram=(WORK/'extracted_assets/azure_dreams_3d_models/ad_model_building_16_general_store/vram.bin').read_bytes()
 OUT.mkdir(parents=True,exist_ok=True);entries=[]
 for i,id in enumerate(IDS):
  off=0x573E4+i*20
  key,idx,name,desc,unused,price,percent=struct.unpack_from('<HHIIIHH',town,off)
  assert 0x80089000<name<0x8008b000 and percent==100
  entry=dict(id=id,nativeIndex=i,name=text(town,name-DELTA),description=text(town,desc-DELTA),price=price,service=id=='remove',recordOffset=hex(off),nameOffset=hex(name-DELTA),descriptionOffset=hex(desc-DELTA),recordHex=town[off:off+20].hex(),icon=icon(exe,vram,idx,OUT/(id+'.png')))
  entries.append(entry)
 # Separate inventory merchandise: the Music Box is not a furniture record.
 entries.append(dict(id='music_box',nativeIndex=-1,name=text(exe,exeoff(0x8002F738)),description=text(exe,exeoff(0x8002F6DC)),price=700,priceSource='existing GeneralStock; not asserted from this inventory record',service=False,recordOffset='SLUS_006.14:0x45BFC',nameOffset='SLUS_006.14:'+hex(exeoff(0x8002F738)),descriptionOffset='SLUS_006.14:'+hex(exeoff(0x8002F6DC)),icon=icon(exe,vram,20,OUT/'music_box.png')))
 data=dict(source='NTSC-U TOWN.BIN + SLUS_006.14 + general-store VRAM',townSha256=hashlib.sha256(town).hexdigest(),executableSha256=hashlib.sha256(exe).hexdigest(),note='Original menu icons. Shared icon indices are intentional; these are not individual 3D furniture models. Fullwidth Shift-JIS normalized to Unicode.',entries=entries)
 p=OUT/'catalog.json';p.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8');meta(p)
 preview=Image.new('RGB',(640,((len(entries)+3)//4)*72),(30,30,40));draw=ImageDraw.Draw(preview)
 for i,e in enumerate(entries):
  x=(i%4)*160;y=(i//4)*72;im=Image.open(OUT/(e['id']+'.png')).resize((32,32),Image.Resampling.NEAREST);preview.paste(im,(x+6,y+4),im);draw.text((x+4,y+40),e['id'],fill='white')
 target=WORK/'Validation/GeneralStore';target.mkdir(parents=True,exist_ok=True);preview.save(target/'original-icons.png')
 print('Extracted',len(entries),'records/icons;',len(set(e['icon']['index'] for e in entries)),'native icon descriptors. Output:',OUT)
if __name__=='__main__':main()
