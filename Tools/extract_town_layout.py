"""Extract authenticated Town tile blocks and plot records, without guessing building identities."""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import xml.etree.ElementTree as ET

from extract_town_selection_table import EXPECTED_SHA256

PROJECT = Path(__file__).resolve().parents[1]
WORKSPACE = PROJECT.parent
NATIVE_DELTA = 0x8007D760


def extract(source, disc, output):
    town = source.read_bytes()
    if hashlib.sha256(town).hexdigest() != EXPECTED_SHA256:
        raise ValueError('TOWN.BIN does not match the authenticated US source')
    # Native context initializer stores the plot table into context +0x3C.
    for offset, word in {0x1FF20: 0x3C02800D, 0x1FF24: 0x24422EA4,
                         0x1FF30: 0xAE02003C}.items():
        if struct.unpack_from('<I', town, offset)[0] != word:
            raise ValueError(f'Plot table reference mismatch at {offset:#x}')
    packed, lba = struct.unpack_from('<II', town, 0x800D1B64 - NATIVE_DELTA)
    sectors = packed >> 23
    target = 0x80000000 | (packed & 0x7FFFFF)
    if (sectors, target, lba) != (27, 0x8011AC98, 4126):
        raise ValueError('Unexpected native tile-bank load descriptor')
    chunks = []
    with disc.open('rb') as handle:
        handle.seek(lba * 2352)
        for index in range(sectors):
            raw = handle.read(2352)
            if len(raw) != 2352 or raw[:12] != b'\0' + b'\xff' * 10 + b'\0':
                raise ValueError(f'Invalid raw CD sector at {lba + index}')
            if raw[15] != 2 or raw[16:20] != raw[20:24] or raw[18] & 0x20:
                raise ValueError('Expected a Mode-2 Form-1 data sector')
            chunks.append(raw[24:24 + 2048])
    bank = b''.join(chunks)
    if bank != town[0x20C800:0x20C800 + len(bank)]:
        raise ValueError('Raw disc sectors differ from the authenticated TOWN slice')

    patches = []
    # Export the non-null prefix, not an inferred total allocation bound.
    for index in range(256):
        pointer = struct.unpack_from('<I', town, 0x800D17D8 - NATIVE_DELTA + index * 4)[0]
        if pointer == 0:
            break
        offset = pointer - target
        if offset < 0 or offset + 4 > len(bank):
            raise ValueError('Tile pointer outside the loaded bank')
        width, height = struct.unpack_from('<HH', bank, offset)
        size = 4 + width * height * 2
        if width > 128 or height > 128 or offset + size > len(bank):
            raise ValueError('Invalid or truncated tile block')
        values = struct.unpack_from('<' + 'H' * (width * height), bank, offset + 4)
        patches.append(dict(entry_id=index, address=hex(pointer), byte_offset=offset,
                            width=width, height=height, nonzero_cells=sum(v != 0 for v in values),
                            sha256=hashlib.sha256(bank[offset:offset + size]).hexdigest()))
    else:
        raise ValueError('No null pointer found in the inspected tile-table prefix')

    plots = []
    defaults = []
    for index in range(34):
        offset = 0x800D2EA4 - NATIVE_DELTA + index * 8
        x, y, width, height, kind, unknown, primary, secondary = town[offset:offset + 8]
        defaults.append(dict(index=index, primary=primary, secondary=secondary))
        if index < 33:
            if x + width > 128 or y + height > 128:
                raise ValueError('Plot footprint outside the Town grid')
            plots.append(dict(slot_id=index, x=x, y=y, width=width, height=height,
                              slot_kind=kind, unknown_byte_5=unknown,
                              default_primary=primary, default_secondary=secondary))
    records = []
    for identifier in sorted({value for row in defaults for value in (row['primary'], row['secondary'])}):
        offset = 0x800D2644 - NATIVE_DELTA + identifier * 32
        raw = town[offset:offset + 32]
        records.append(dict(record_id=identifier, file_offset=hex(offset), raw_hex=raw.hex(),
                            kind=raw[3], width=raw[4], height=raw[5],
                            availability_flag=struct.unpack_from('<h', raw, 8)[0]))
    report = dict(source_sha256=EXPECTED_SHA256, disc_sectors_verified=sectors,
                  bank=dict(lba=lba, sectors=sectors, load_address=hex(target),
                            town_file_offset='0x20c800', bytes=len(bank),
                            sha256=hashlib.sha256(bank).hexdigest()),
                  grid=dict(width=128, height=128, bytes_per_cell=2, tile_id_mask=0x3FFF),
                  tile_table_address='0x800d17d8', tile_patch_records=patches,
                  plot_table_address='0x800d2ea4', plots=plots,
                  initial_assignments=defaults, referenced_building_records=records,
                  limits=('Slots 0..32 are used by the region loop; reset copies 34 assignment pairs. '
                          'Record 33 supplies the special assignment at save +0x33E6, not another spatial plot. '
                          'Building identities, complete tile composition order, geometry and textures remain unbound.'))
    output.mkdir(parents=True, exist_ok=True)
    (output / 'town_layout_bank.bytes').write_bytes(bank)
    (output / 'town_layout_metadata.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')

    svg = ET.Element('svg', xmlns='http://www.w3.org/2000/svg', width='768', height='804', viewBox='0 0 128 134')
    ET.SubElement(svg, 'rect', x='0', y='0', width='128', height='128', fill='#eef0ed')
    for row in plots:
        if row['width'] == 0 or row['height'] == 0:
            continue
        x, y = row['x'], row['y']
        ET.SubElement(svg, 'rect', x=str(x), y=str(y), width=str(row['width']), height=str(row['height']),
                      fill='#d0ddea', stroke='#47657c', **{'stroke-width': '.25'})
        label = ET.SubElement(svg, 'text', x=str(x + .6), y=str(y + 3.2),
                              **{'font-family': 'sans-serif', 'font-size': '2.4', 'fill': '#1e3344'})
        label.text = f"{row['slot_id']} : {row['default_primary']}"
    label = ET.SubElement(svg, 'text', x='2', y='132', **{'font-family': 'sans-serif', 'font-size': '2.5'})
    label.text = 'Source tile coordinates; labels = slot : default record ID. No building identity inferred.'
    ET.ElementTree(svg).write(PROJECT / 'Documentation/TownRetailPlotLayout.svg', encoding='utf-8', xml_declaration=True)
    print(f"Verified {sectors} disc sectors; exported {len(patches)} tile-table prefix entries and {len(plots)} slot records.")


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, default=WORKSPACE / 'extracted_assets/files/TOWN/TOWN.BIN')
    parser.add_argument('--disc', type=Path, default=WORKSPACE / 'Azure Dreams.bin')
    parser.add_argument('--output-dir', type=Path, default=PROJECT / 'Assets/AzureDreams/Resources/Town/Retail')
    args = parser.parse_args()
    extract(args.source, args.disc, args.output_dir)
