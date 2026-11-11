"""Recover a Town geometry resource using the original resident lookup and chunk loader."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import struct

from psx_disc import DiscImage
from extract_town_selection_table import EXPECTED_SHA256

PROJECT = Path(__file__).resolve().parents[1]
WORKSPACE = PROJECT.parent
NATIVE_DELTA = 0x8007D760


def recover(disc_path, type_index, output):
    disc = DiscImage(disc_path)
    exe, exe_entry = disc.read_file('SLUS_006.14')
    town, town_entry = disc.read_file('TOWN/TOWN.BIN')
    if hashlib.sha256(exe).hexdigest() != 'ae19d8d4bdd2db9a933b1e44d70dd61be670781e441d7cb37da85250a7aed31f':
        raise ValueError('Unrecognized resident executable')
    if hashlib.sha256(town).hexdigest() != EXPECTED_SHA256:
        raise ValueError('Disc TOWN.BIN does not match the authenticated US image')
    if exe[:8] != b'PS-X EXE':
        raise ValueError('Invalid executable header')
    exe_base = struct.unpack_from('<I', exe, 24)[0]

    def exe_offset(address, count=4):
        offset = address - exe_base + 2048
        if offset < 2048 or offset + count > len(exe):
            raise ValueError('Resident lookup address outside the executable')
        return offset

    for address, word in {0x80046D70: 0x3C028007, 0x80046D74: 0x2442E7F0}.items():
        if struct.unpack_from('<I', exe, exe_offset(address))[0] != word:
            raise ValueError('Resident geometry lookup instructions differ')
    if type_index < 0 or type_index > 40:
        raise ValueError('This extractor is scoped to the observed Town entries 0..40')
    info_address = struct.unpack_from('<I', exe, exe_offset(0x8006E7F0 + type_index * 4))[0]
    info_offset = info_address - NATIVE_DELTA
    if info_offset < 0 or info_offset + 16 > len(town):
        raise ValueError('The selected geometry metadata is outside the Town image')
    descriptor, chunks, lists, *offset_words = struct.unpack_from('<IHH4H', town, info_offset)
    descriptor_offset = descriptor - NATIVE_DELTA
    packed, first_lba = struct.unpack_from('<II', town, descriptor_offset)
    first_sectors = packed >> 23
    if not 0 < chunks <= 64 or not 0 < lists <= 16384 or not 0 < first_sectors <= 511:
        raise ValueError('Invalid geometry load metadata')

    spec = importlib.util.spec_from_file_location('verified_container_decode',
        WORKSPACE / 'extracted_assets/verified_decode/verify_container_reports.py')
    decoder = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(decoder)
    parts = []
    chunk_reports = []
    # runtime_load_packed_resource_chunks advances the LBA by 0x10 each time,
    # repacking subsequent reads with a 16-sector extent. LZ output appends.
    for index in range(chunks):
        lba = first_lba + index * 16
        sectors = first_sectors if index == 0 else 16
        raw = disc.read_data(lba, sectors)
        source_offset = (lba - town_entry['lba']) * 2048
        if source_offset < 0 or raw != town[source_offset:source_offset + len(raw)]:
            raise ValueError('Geometry chunk differs from the authenticated Town file')
        decoded, consumed = decoder.decode_lz(raw)
        parts.append(decoded)
        chunk_reports.append(dict(lba=lba, sectors=sectors, town_file_offset=hex(source_offset),
                                  input_bytes_consumed=consumed, decoded_bytes=len(decoded),
                                  decoded_sha256=hashlib.sha256(decoded).hexdigest()))
    payload = b''.join(parts)
    face_table, vertices, normals, auxiliary = [word * 4 for word in offset_words]
    if not 0 < face_table < vertices <= normals <= len(payload):
        raise ValueError('Geometry regions are not ordered or exceed the decoded payload')
    if face_table + lists * 4 != vertices or (normals - vertices) % 8:
        raise ValueError('Geometry table/vertex extents differ from the resident layout contract')
    starts = struct.unpack_from('<' + 'I' * lists, payload, face_table)
    if any(value % 4 or value + 24 > face_table for value in starts):
        raise ValueError('Face-list pointer outside the face data region')
    report = dict(type_index=type_index, executable_entry=exe_entry,
                  executable_sha256=hashlib.sha256(exe).hexdigest(), town_entry=town_entry,
                  town_sha256=EXPECTED_SHA256, info_address=hex(info_address),
                  info_file_offset=hex(info_offset), descriptor_address=hex(descriptor),
                  chunks=chunk_reports, decoded_bytes=len(payload),
                  decoded_sha256=hashlib.sha256(payload).hexdigest(), list_count=lists,
                  region_offsets=dict(face_list_pointers=face_table, vertices=vertices,
                                      normals=normals, auxiliary=auxiliary),
                  vertex_records=(normals - vertices) // 8,
                  normal_record_capacity=((auxiliary if auxiliary > normals else len(payload)) - normals) // 8,
                  face_list_offsets=starts,
                  limits=('Raw geometry payload and resident region bindings only. Normal capacity is not an '
                          'asserted allocation count; auxiliary offset zero is retained. Face traversal, '
                          'axis conversion, texture/CLUT mapping and final Unity meshes remain separate.'))
    output.mkdir(parents=True, exist_ok=True)
    stem = f'town_geometry_type_{type_index}'
    (output / (stem + '.bytes')).write_bytes(payload)
    (output / (stem + '_metadata.json')).write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({key: report[key] for key in ('type_index', 'decoded_bytes', 'decoded_sha256',
                                                 'list_count', 'vertex_records', 'normal_record_capacity')}, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--disc', type=Path, default=WORKSPACE / 'Azure Dreams.bin')
    parser.add_argument('--type-index', type=int, default=0)
    parser.add_argument('--output-dir', type=Path, default=PROJECT / 'Assets/AzureDreams/Resources/Town/Retail')
    args = parser.parse_args()
    recover(args.disc, args.type_index, args.output_dir)
