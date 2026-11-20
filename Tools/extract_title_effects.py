"""Extract title lens elements, rotating crystals and timing from MAIN.BIN."""
from pathlib import Path
import struct,json,re,uuid
from PIL import Image
import extract_vivian_town_sprites as v
from extract_town_character_packages import parse_package
P=Path(__file__).resolve().parents[1]
def meta(path,kind):
 v.write_meta(path,kind)
 m=Path(str(path)+'.meta')
 m.write_text(re.sub(r'guid: [0-9a-f]+','guid: '+uuid.uuid5(uuid.NAMESPACE_URL,'azure/title/'+m.name).hex,m.read_text()))
def main():
 b=(P.parent/'extracted_assets/full_disc/files/MAIN/MAIN.BIN').read_bytes();v.BASE=0x80066f60;v.CELL=64;v.ORIGIN_X=32;v.ORIGIN_Y=32
 scratch=P.parent/'Validation/Title/Menu';scratch.mkdir(parents=True,exist_ok=True);parse_package(b[0x257000:0x258800],scratch)
 vr=(scratch/'vram_words_le.bin').read_bytes();written=(scratch/'vram_written_mask.bin').read_bytes();out=P/'Assets/AzureDreams/Resources/Title/RetailEffects';out.mkdir(parents=True,exist_ok=True)
 pointers=struct.unpack_from('<10I',b,0x80088790-v.BASE);offsets=struct.unpack_from('<10h',b,0x800887b8-v.BASE);masks=struct.unpack_from('<10I',b,0x800887cc-v.BASE)
 for i,cp in enumerate(pointers):
  f=out/f'Flare{i}.png';v.render(v.read_chain(b,cp),vr,written).save(f);meta(f,'texture')
 clips=[];atlas=Image.new('RGBA',(7*64,2*64))
 for side,ptr in enumerate([0x8008b270,0x8008b230]):
  seq=v.read_sequence(b,ptr);assert len(seq['entries'])==7
  for i,e in enumerate(seq['entries']):atlas.alpha_composite(v.render(v.read_chain(b,int(e['chain'],16)),vr,written),(i*64,side*64))
  clips.append([e['ticks'] for e in seq['entries']])
 f=out/'Crystals.png';atlas.save(f);meta(f,'texture')
 f=out/'data.json';f.write_text(json.dumps(dict(offsets=offsets,masks=masks,crystalTicks=clips[0],confirmationTicks=96,initialAngle=3584,angleStep=-12)));meta(f,'text')
 print('10 native lens elements; two seven-frame crystal sequences; original offsets, palettes and durations.')
if __name__=='__main__':main()
