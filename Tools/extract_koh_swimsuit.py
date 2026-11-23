"""Extract original Koh swimsuit locomotion from indexed retail town bundle 2."""
import json, uuid
from pathlib import Path
from PIL import Image
from extract_town_sprite_banks import DiscImage, bundle_locations, export_bank, sha, EXPECTED_SLUS_SHA

ROOT = Path(__file__).resolve().parents[2]
disc = DiscImage(ROOT / 'Azure Dreams.bin')
exe, _ = disc.read_file('SLUS_006.14')
assert sha(exe) == EXPECTED_SLUS_SHA
location = next(p for p in bundle_locations(exe) if p['name'] == 'town_koh_alternate')
source_root = ROOT / 'extracted_assets/reimagined_pipeline/TownSpriteBanks'
result = export_bank(disc, location, source_root)
source = source_root / location['name']
groups = json.loads((source / 'animations.json').read_text())['groups']
frames = {f['pointer']: f for f in json.loads((source / 'frames.json').read_text())}
atlas = Image.new('RGBA', (17 * 64, 5 * 64))
timing = []
for direction in range(5):
    def seq(group):
        return [e for e in groups[group]['entries'] if 'frame_png' in e]
    idle, walk, run = seq(direction), seq(5 + direction), seq(10 + direction)
    assert (len(idle), len(walk), len(run)) == (1, 8, 8)
    for column, entry in enumerate(idle + walk + run):
        image = Image.open(source / entry['frame_png']).convert('RGBA')
        assert image.getbbox()
        ox, oy = frames[entry['pointer']]['origin']
        assert 0 <= ox and 0 <= oy and ox + image.width <= 64 and oy + image.height <= 64
        atlas.alpha_composite(image, (column * 64 + ox, direction * 64 + oy))
    timing.append(dict(idle=[e['ticks_raw'] for e in idle], walk=[e['ticks_raw'] for e in walk], run=[e['ticks_raw'] for e in run]))
folder = ROOT / 'Azure Dreams Reimagined/Assets/AzureDreams/Resources/Town/Retail'
image_path = folder / 'koh-swimsuit.bytes'
atlas.save(image_path, format='PNG')
info = folder / 'koh-swimsuit-source.json'
info.write_text(json.dumps(dict(bundle=2, lba=location['lba'], sectors=location['sectors'], bundle_sha256=result['bundle_sha256'], directions=5, frames=85, cellSize=64, columns=17, ticks=timing, evidence='initialize_scene_for_mode.c F_player_mizugi; load_alternate_town_sprite_bundle -> resource_load_indexed_sprite_bundle(2)'))+'\n')
for path in (image_path, info):
    meta = path.with_name(path.name + '.meta')
    if not meta.exists(): meta.write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid5(uuid.NAMESPACE_URL, 'azure-retail/' + path.name).hex + '\n')
print('Extracted 85 authenticated Koh swimsuit idle/walk/run frames, five directional views.')