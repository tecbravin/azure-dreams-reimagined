"""Reconstruct the VRAM writes made by scene_load_variant_texture_set."""
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


def recover(disc_path, variant, output):
    disc = DiscImage(disc_path)
    exe, _ = disc.read_file('SLUS_006.14')
    town, town_entry = disc.read_file('TOWN/TOWN.BIN')
    if hashlib.sha256(exe).hexdigest() != 'ae19d8d4bdd2db9a933b1e44d70dd61be670781e441d7cb37da85250a7aed31f':
        raise ValueError('Unrecognized resident executable')
    if hashlib.sha256(town).hexdigest() != EXPECTED_SHA256:
        raise ValueError('Unrecognized Town image')
    base = struct.unpack_from('<I', exe, 24)[0]
    spec = importlib.util.spec_from_file_location('verified_lz', WORKSPACE / 'extracted_assets/verified_decode/verify_container_reports.py')
    decoder = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(decoder)
    vram = bytearray(1024 * 512 * 2)
    written = bytearray(1024 * 512)
    reports = []

    def upload(x, y, w, h, data):
        if x < 0 or y < 0 or w < 1 or h < 1 or x + w > 1024 or y + h > 512 or len(data) < w*h*2:
            raise ValueError('Invalid VRAM upload')
        for row in range(h):
            start = (y+row)*1024+x
            vram[start*2:(start+w)*2] = data[row*w*2:(row+1)*w*2]
            written[start:start+w] = bytes([1])*w

    for address in [0x80080BB4, 0x80080BBC, 0x80080BC4, 0x80080BCC, 0x80080BD4+variant*8]:
        packed, lba = struct.unpack_from('<II', exe, address-base+2048)
        sectors = packed >> 23
        raw = disc.read_data(lba, sectors)
        offset_in_town = (lba-town_entry['lba'])*2048
        if raw != town[offset_in_town:offset_in_town+len(raw)]:
            raise ValueError('Texture bank differs from authenticated Town file')
        commands = []
        pos = 0
        while True:
            cmd, size, offset, x, y, w, h = struct.unpack_from('<HHI4h', raw, pos)
            if cmd < 1 or cmd > 9:
                break
            if offset >= len(raw):
                raise ValueError('Image command data offset outside bank')
            data = raw[offset:]
            rect = None
            flags = w
            if cmd in (1, 5):
                if cmd == 1:
                    data, consumed = decoder.decode_lz(data)
                rect = (x, y, w, h)
                upload(*rect, data)
            elif cmd in (2, 6, 7, 8):
                if cmd in (7, 8):
                    data = bytearray(data)
                    for index in range(1, y*16):
                        if cmd == 7 or index % 16:
                            color = struct.unpack_from('<H', data, index*2)[0]
                            struct.pack_into('<H', data, index*2, color | 0x8000)
                if cmd in (6, 8): flags |= 2
                if cmd == 7: flags |= 4
                if not flags & 1:
                    slot = (x-0x7000) >> 6
                    rect = (((slot & 0x40) << 1) + ((x & 63) << 4), (slot & 63)+0x1c0, y*16, 1)
                    upload(*rect, data)
            else:
                raise ValueError(f'Unexpected non-image command {cmd}; cannot reconstruct snapshot safely')
            commands.append(dict(command=cmd, command_offset=pos, data_offset=offset, upload_rect=rect, flags=flags))
            if size == 0: break
            if size < 16 or pos+size+16 > len(raw):
                raise ValueError('Invalid image command stride')
            pos += size
        reports.append(dict(descriptor=hex(address), lba=lba, sectors=sectors, sha256=hashlib.sha256(raw).hexdigest(), commands=commands))
    output.mkdir(parents=True, exist_ok=True)
    stem = f'town_vram_variant_{variant}'
    (output/(stem+'.bytes')).write_bytes(vram)
    (output/(stem+'_written.bytes')).write_bytes(written)
    report = dict(variant_texture_bank=variant, width_words=1024, height=512, byte_order='little',
                  town_sha256=EXPECTED_SHA256, decoded_sha256=hashlib.sha256(vram).hexdigest(),
                  written_words=sum(written), banks=reports,
                  limits='Only writes from the four shared banks and selected variant bank. Unwritten VRAM is unknown, not asserted black. Runtime palette animation and other upload paths are not included.')
    (output/(stem+'_metadata.json')).write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(dict(variant=variant, written_words=sum(written), commands=sum(len(b['commands']) for b in reports))))

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--disc', type=Path, default=WORKSPACE/'Azure Dreams.bin')
    parser.add_argument('--variant', type=int, choices=range(9), default=0)
    parser.add_argument('--output-dir', type=Path, default=PROJECT/'Assets/AzureDreams/Resources/Town/Retail')
    args = parser.parse_args()
    recover(args.disc, args.variant, args.output_dir)
