"""Recover Koh's directional weapon and shield sprites from the NTSC disc."""
import struct
import json

from PIL import Image, ImageOps

from extract_koh_locomotion import BANK_OFFSET, CELL, color, decode, parse_metadata, workspace
from psx_disc import DiscImage


def read_equipment(disc, executable, table, subtype):
    flags, lba = struct.unpack_from('<II', executable, table - 0x8002D000 + 0x800 + 8 * subtype)
    raw = disc.read_data(lba, flags >> 23)
    command, _, offset, vram_x, vram_y, words_per_row, height = struct.unpack_from('<HHI4h', raw)
    palette_command, _, palette_offset = struct.unpack_from('<HHI', raw, 16)
    assert command == 1 and palette_command == 2 and vram_x == 928
    stride = words_per_row * 2
    pixels, _ = decode(raw[offset:], stride * height)
    palette = [color(word) for word in struct.unpack_from('<16H', raw, palette_offset)]
    return pixels, palette, stride, height, vram_y


def equipment_tile(page, part):
    pixels, palette, stride, height, vram_y = page
    u, v = part['u'] - 128, part['v'] - vram_y
    width, rows = part['width'], part['height']
    assert 0 <= u <= stride * 2 - width and 0 <= v <= height - rows
    tile = Image.new('RGBA', (width, rows))
    tile.putdata([
        palette[(pixels[(v + y) * stride + x // 2] >> (4 * (x % 2))) & 15]
        for y in range(rows) for x in range(u, u + width)
    ])
    return ImageOps.mirror(tile) if part['flags'] & 1 else tile


def main():
    disc = DiscImage(workspace / 'Azure Dreams.bin')
    executable, _ = disc.read_file('SLUS_006.14')
    dungeon, _ = disc.read_file('DUNGEON/DUNGEON.BIN')
    bank = dungeon[BANK_OFFSET:]
    metadata = parse_metadata(bank)
    chains = {chain['pointer']: chain['parts'] for chain in metadata['chains']}
    views = []
    for group in range(174, 179):
        offset, = struct.unpack_from('<I', bank, metadata['animation_table_offset'] + 4 * group)
        _, kind, pointer = struct.unpack_from('<HHI', bank, offset)
        assert kind == 2
        views.append({part['clut_raw']: part for part in chains[pointer]})

    # Rows are the original subtype IDs, starting at 1, in sword/wand/shield order.
    atlas = Image.new('RGBA', (5 * CELL, 32 * CELL))
    row = 0
    for table, count, clut in ((0x800710F8, 12, 0x7A40), (0x800711C0, 9, 0x7A40), (0x80071160, 11, 0x7A41)):
        for subtype in range(1, count + 1):
            page = read_equipment(disc, executable, table, subtype)
            for column, parts in enumerate(views):
                part = parts[clut]
                tile = equipment_tile(page, part)
                assert tile.getbbox()
                x = -part['x'] - tile.width if part['flags'] & 1 else part['x']
                atlas.alpha_composite(tile, (column * CELL + 32 + x, row * CELL + 53 + part['y']))
            row += 1

    # Follow the exact weapon part in every body pose. Compact 32px cells keep
    # this animation atlas smaller than storing 900 mostly empty 64px cells.
    def sequence(group):
        offset, = struct.unpack_from('<I', bank, metadata['animation_table_offset'] + 4 * group)
        result = []
        while True:
            ticks, kind, pointer = struct.unpack_from('<HHI', bank, offset)
            if kind != 2 or ticks == 0:
                return result
            result.append(chains[pointer])
            offset += 8

    motion = Image.new('RGBA', (20 * 32, 9 * 5 * 32))
    offsets = []
    for subtype in range(1, 10):
        page = read_equipment(disc, executable, 0x800711C0, subtype)
        for direction in range(5):
            poses = [sequence(174 + direction)[0], *sequence(direction), *sequence(5 + direction),
                     *[sequence(128 + direction * 10 + phase)[0] for phase in range(3)]]
            assert len(poses) == 20
            for pose, parts in enumerate(poses):
                part = next(part for part in parts if part['clut_raw'] == 0x7A40)
                tile = equipment_tile(page, part)
                assert tile.width <= 32 and tile.height <= 32 and tile.getbbox()
                x = -part['x'] - tile.width if part['flags'] & 1 else part['x']
                # Center of the 32px cell relative to Koh's retail feet origin.
                offsets.append(dict(x=x + 16, y=-part['y'] - 16))
                row = (subtype - 1) * 5 + direction
                motion.alpha_composite(tile, (pose * 32, row * 32))
    folder = workspace / 'Azure Dreams Reimagined/Assets/AzureDreams/Resources/Tower'
    motion.save(folder / 'KohWandEquipment.png')
    (folder / 'KohWandEquipment.json').write_text(json.dumps(dict(offsets=offsets)) + '\n')

    target = workspace / 'Azure Dreams Reimagined/Assets/AzureDreams/Resources/Tower/KohEquipment.png'
    atlas.save(target)
    print('Exported 32 equipment sets and 900 original wand motion frames.')


if __name__ == '__main__':
    main()
