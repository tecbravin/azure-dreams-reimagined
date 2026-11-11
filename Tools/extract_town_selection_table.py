"""Recover the selection tables referenced by the TOWN 0x3E5000 module."""
import argparse
import hashlib
import json
from pathlib import Path
import struct

EXPECTED_SHA256 = '907725012d8d9e1deab18c28b62f4d304057e86a6415df605b9ffb87cfb2fe18'


def extract(source):
    data = source.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if digest != EXPECTED_SHA256:
        raise ValueError('TOWN.BIN differs from the authenticated US source')
    # Check the actual LUI/ADDIU references, not merely a guessed data range.
    references = {
        0x1FEF4: 0x3C02800D, 0x1FEF8: 0x24423CC8, 0x1FEFC: 0xAE020020,
        0x56754: 0x800C23DC, 0x5675C: 0x8008B550, 0x56860: 0x800C17A0,
        0x3E8B1C: 0x244278FC, 0x3E8B30: 0x24427918,
        0x3E8B44: 0x244278F8, 0x3E8B5C: 0x244278F8,
        0x3E9304: 0x3C028001, 0x3E9308: 0x2442791C,
        0x3E9448: 0x3C028001, 0x3E944C: 0x2454791C,
        0x3E9450: 0x3C028001, 0x3E9454: 0x24537BB0,
        0x3E94F0: 0x3C038001, 0x3E94F4: 0x90627BC0,
        0x3ED578: 0x1080001B, 0x3ED5E0: 0x00621825,
        0x3ED5E4: 0xACC30000, 0x3ED5F0: 0x24020001,
        0x3ED5F4: 0x1082001C, 0x3ED654: 0x00031827,
        0x3ED660: 0x00621824, 0x3ED670: 0x10800018,
        0x3ED678: 0x10820014,
    }
    for offset, expected in references.items():
        if struct.unpack_from('<I', data, offset)[0] != expected:
            raise ValueError(f'MIPS table reference mismatch at {offset:#x}')

    def read_order(offset):
        order = []
        for entry in data[offset:offset + 32]:
            order.append(entry)
            if entry == 0:
                return order
            if entry > 7:
                raise ValueError('Entry ID outside the observed records 1..7')
        raise ValueError('Missing entry-order terminator')

    records = []
    for entry in range(8):
        offset = 0x3E691C + entry * 12
        value, enabled, paired, tail = struct.unpack_from('<IhhI', data, offset)
        records.append(dict(entry_id=entry, file_offset=hex(offset), value_word=value,
                            enabled_flag=enabled, paired_flag=paired, tail_word=tail))
    return dict(source_sha256=digest, module_file_offset='0x3e5000',
                module_load_address='0x80016000', records=records,
                cycling_order=read_order(0x3E6BB0), priority_order=read_order(0x3E6BC0),
                service_argument_pairs=[list(struct.unpack_from('<hh', data, 0x3E68F8 + i * 4))
                                        for i in range(9)],
                references={hex(k): hex(v) for k, v in references.items()},
                flag_helpers={'0x3ed578': 'set bit; skip ID 0',
                              '0x3ed5f0': 'clear bit; skip ID 1',
                              '0x3ed670': 'query mask; ID 0 false, ID 1 true'},
                native_audio_service={'table_address': '0x800d3cc8', 'table_file_offset': '0x56568',
                                      'root_service_field': '0x20', 'slot': '0x2f8',
                                      'native_call': 190, 'handler': 'town_seq_reserve',
                                      'handler_address': '0x800c17a0', 'handler_file_offset': '0x44040'},
                limits='Scene-specific table. Audio reservation callback resolved; audio asset IDs and narrative script execution are separate unresolved bindings.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, default=Path(__file__).resolve().parents[2]
                        / 'extracted_assets/files/TOWN/TOWN.BIN')
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    report = json.dumps(extract(args.source), indent=2) + '\n'
    if args.output:
        args.output.write_text(report, encoding='utf-8')
    else:
        print(report, end='')
