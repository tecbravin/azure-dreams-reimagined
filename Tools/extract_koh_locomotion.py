"""Recover Koh's unequipped body sprites from the retail dungeon sprite bank."""
from pathlib import Path
import json
import struct
import sys

from PIL import Image

workspace = Path(__file__).resolve().parents[2]
sys.path[:0] = [
    str(workspace / 'extracted_assets/verified_decode'),
    str(workspace / 'azure-dreams-decomp-master/tools/decoded'),
]
from extract_verified_sprites import color, parse_metadata
from lz import decode

BANK_OFFSET = 0x4DB800
CELL = 64


def opaque_bottoms(atlas):
    """Return the bottom edge of each pose's opaque pixels, in atlas order."""
    bottoms = []
    for y in range(0, atlas.height, CELL):
        for x in range(0, atlas.width, CELL):
            bounds = atlas.crop((x, y, x + CELL, y + CELL)).getbbox()
            bottoms.append(bounds[3] if bounds else 0)
    return bottoms


def main(hit_only=False):
    dungeon = (workspace / 'DUNGEON/DUNGEON.BIN').read_bytes()
    assert dungeon[0xF7858:0xF7868] == bytes([2, 1, 0, 1, 2, 3, 4, 3, 7, 6, 5, 6, 7, 8, 9, 8])
    bank = dungeon[BANK_OFFSET:]
    meta = parse_metadata(bank)
    assert meta['animation_count'] == 216
    texture_offset = meta['header_words'][3]
    palette_offset = meta['header_words'][7]
    palette = [color(word) for word in struct.unpack_from('<64H', bank, palette_offset)]
    chains = {chain['pointer']: chain['parts'] for chain in meta['chains']}

    def frames(group):
        offset = struct.unpack_from('<I', bank, meta['animation_table_offset'] + group * 4)[0]
        while True:
            ticks, kind, pointer = struct.unpack_from('<HHI', bank, offset)
            if kind != 2 or ticks == 0:
                break
            yield pointer
            offset += 8

    def body(pointer):
        # These groups contain one compressed body tile and optional shared
        # equipment parts. Koh's unequipped appearance uses the body alone.
        parts = [part for part in chains[pointer] if not part['flags'] & 0x60]
        assert parts
        for part_index in reversed(range(len(parts))):
            part = parts[part_index]
            assert part['clut_raw'] == 0x7A00 and part['texture_mode'] == 1
            width, height = part['width'], part['height']
            offset = struct.unpack_from('<I', bank, pointer - (part_index + 1) * 4)[0]
            stream, _ = decode(bank[texture_offset + offset:], 8192)
            indices = bytearray()
            for i, token in enumerate(stream):
                if token == 1 and stream[i + 1:i + 2] == b'\x01':
                    break
                indices.extend(bytes([token & 63]) * (1 + (token >> 6)))
            stride = (width + 1) // 2 * 2
            assert len(indices) >= stride * height
            tile = Image.new('RGBA', (width, height))
            tile.putdata([palette[indices[y * stride + x]] for y in range(height) for x in range(width)])
            yield tile, CELL // 2 + part['x'], 53 + part['y']

    # The retail damage table stores front through back at indices 2-6.
    damage_groups = dungeon[0xF7888:0xF7890]
    assert damage_groups == bytes([44, 43, 42, 43, 44, 45, 46, 45])
    hit = Image.new('RGBA', (CELL * 9, CELL * 5))
    for row, group in enumerate(damage_groups[2:7]):
        sequence = list(frames(group))
        assert len(sequence) == 9
        for column, pointer in enumerate(sequence):
            for tile, x, y in body(pointer):
                hit.alpha_composite(tile, (column * CELL + x, row * CELL + y))
    hit_target = workspace / 'Azure Dreams Reimagined/Assets/AzureDreams/Resources/Tower/koh-retail-hit.bytes'
    hit.save(hit_target, format='PNG')
    if hit_only:
        return

    attack = Image.new('RGBA', (CELL * 3, CELL * 5))
    for row, group in enumerate([128, 138, 148, 158, 168]):
        for phase in range(3):
            for tile, x, y in body(next(frames(group + phase))):
                attack.alpha_composite(tile, (phase * CELL + x, row * CELL + y))
    attack.save(workspace / 'Azure Dreams Reimagined/Assets/AzureDreams/Sprites/Characters/Koh/koh-retail-attack.png')

    atlas = Image.new('RGBA', (CELL * 17, CELL * 5))
    for row in range(5):
        walk = list(frames(row))
        run = list(frames(row + 5))
        assert len(walk) == len(run) == 8
        for column, pointer in enumerate([next(frames(174 + row)), *walk, *run]):
            for tile, x, y in body(pointer):
                assert 0 <= x <= CELL - tile.width and 0 <= y <= CELL - tile.height
                atlas.alpha_composite(tile, (column * CELL + x, row * CELL + y))
    target = workspace / 'Azure Dreams Reimagined/Assets/AzureDreams/Sprites/Characters/Koh/koh-retail-locomotion.png'
    atlas.save(target)
    # NTSC lift and carry groups, ordered front to back.
    carrying = Image.new('RGBA', (CELL * 19, CELL * 5))
    for row in range(5):
        lift = list(frames(15 + row))
        held = list(frames(179 + row))
        walk = list(frames(184 + row))
        assert (len(lift), len(held), len(walk)) == (6, 1, 5)
        # NTSC push preparation, contact, and sustained pose. Each group is
        # selected from the corresponding directional table in DUNGEON.BIN.
        push = [next(frames(group + row * 6)) for group in (61, 62, 64)]
        for column, pointer in [*enumerate([*lift, *held, *walk]), *enumerate(push, 16)]:
            for tile, x, y in body(pointer):
                carrying.alpha_composite(tile, (column * CELL + x, row * CELL + y))
    carrying.save(target.with_name('koh-retail-lift.png'))
    # player_sleep_frames (0x800DD0F0) selects these groups by facing.
    assert dungeon[0xF7890:0xF7898] == bytes([51, 50, 49, 50, 51, 52, 53, 52])
    sleep = Image.new('RGBA', (CELL * 8, CELL * 5))
    for row, group in enumerate(range(49, 54)):
        sequence = list(frames(group))
        assert len(sequence) == 8
        for column, pointer in enumerate(sequence):
            for tile, x, y in body(pointer):
                assert 0 <= x <= CELL - tile.width and 0 <= y <= CELL - tile.height
                sleep.alpha_composite(tile, (column * CELL + x, row * CELL + y))
    sleep_target = workspace / 'Azure Dreams Reimagined/Assets/AzureDreams/Resources/Tower/koh-retail-sleep.bytes'
    sleep.save(sleep_target, format='PNG')
    grounding = {name: opaque_bottoms(sheet) for name, sheet in
                 (('locomotion', atlas), ('attack', attack), ('lift', carrying), ('sleep', sleep))}
    sleep_target.with_name('koh-retail-grounding.json').write_text(json.dumps(grounding) + '\n', encoding='utf-8')
    print('Exported original Koh locomotion, attack, lift, and sleep frames.')


if __name__ == '__main__':
    main(hit_only=sys.argv[1:] == ['--hit-only'])


