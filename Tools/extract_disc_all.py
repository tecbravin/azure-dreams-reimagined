"""Inventory and losslessly extract every ISO file, preserving XA/Form-2 sectors."""
import hashlib
import json
from pathlib import Path
from psx_disc import DiscImage

ROOT = Path(__file__).resolve().parents[2]
DISC = ROOT / 'Azure Dreams.bin'
OUT = ROOT / 'extracted_assets/full_disc'

def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda: f.read(1024*1024), b''): h.update(block)
    return h.hexdigest()

def main():
    disc = DiscImage(DISC)
    root = disc._record(disc.read_data(16, 1), 156)
    entries = []
    visited = set()
    def walk(record, prefix):
        key = (record['lba'], record['bytes'])
        if key in visited: raise ValueError('Directory cycle or alias')
        visited.add(key)
        data = disc.read_data(record['lba'], (record['bytes']+2047)//2048)[:record['bytes']]
        pos = 0
        while pos < len(data):
            if data[pos] == 0:
                pos = (pos//2048+1)*2048
                continue
            child = disc._record(data, pos)
            pos += child['record_bytes']
            name = child['name']
            if name in ('\x00', '\x01'): continue
            if any(c in name for c in '/\\:') or name in ('.', '..'): raise ValueError('Unsafe ISO name')
            path = prefix / name
            if child['directory']: walk(child, path)
            else: entries.append((path, child))
    walk(root, Path())
    reports = []
    with DISC.open('rb') as source:
        for relative, record in entries:
            count = (record['bytes']+2047)//2048
            source.seek(record['lba']*2352)
            raw_path = OUT/'raw_sectors'/relative.with_name(relative.name+'.sectors')
            raw_path.parent.mkdir(parents=True, exist_ok=True)
            modes = {}; payload = bytearray(); raw_hash = hashlib.sha256()
            with raw_path.open('wb') as target:
                for _ in range(count):
                    sector = source.read(2352)
                    if len(sector)!=2352 or sector[:12] != b'\0'+b'\xff'*10+b'\0': raise ValueError('Invalid data sector')
                    target.write(sector); raw_hash.update(sector)
                    if sector[15] == 1: kind='mode1'; data=sector[16:2064]
                    elif sector[15] == 2 and sector[16:20] == sector[20:24]:
                        form2=bool(sector[18]&32)
                        kind='mode2_form2' if form2 else 'mode2_form1'
                        data=sector[24:2348] if form2 else sector[24:2072]
                    else: raise ValueError('Unsupported sector format')
                    modes[kind]=modes.get(kind,0)+1
                    payload.extend(data)
            form1_only='mode2_form2' not in modes
            destination=OUT/('files' if form1_only else 'xa_payloads')/relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            if form1_only: payload=payload[:record['bytes']]
            destination.write_bytes(payload)
            reports.append(dict(path=relative.as_posix(), **record, sectors=count, sector_modes=modes,
                                raw_sha256=raw_hash.hexdigest(), payload_sha256=hashlib.sha256(payload).hexdigest(),
                                payload_bytes=len(payload), payload_kind='iso_file' if form1_only else 'concatenated_XA_payloads'))
    manifest=dict(disc=DISC.name, disc_bytes=DISC.stat().st_size, disc_sha256=digest(DISC), files=len(reports), entries=reports,
                  limits='All ISO files and their original raw sector extents. XA payloads retain Form-2 data; packed game containers still require format-specific decoding. Non-file sectors and audio tracks remain in the source BIN.')
    (OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf8')
    print(json.dumps(dict(files=len(reports), output=str(OUT), sector_modes=sorted({k for r in reports for k in r['sector_modes']}))))

if __name__=='__main__': main()
