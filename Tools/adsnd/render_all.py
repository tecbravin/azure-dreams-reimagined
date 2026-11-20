"""Render every Azure Dreams music sequence from the disc with adsnd.

Run from the workspace root (Projetos Pessoais/Azure):
    python "Azure Dreams Reimagined/Tools/adsnd/render_all.py" <adsnd-binary> <out-dir> [passes] [fade]

Town songs use the town music bank (TOWN.BIN VAB at sector 1245); tower songs use the dungeon bank
(DUNGEON.BIN VAB at sector 3780). Both pairings were derived from the programs each KDT sequence uses.
Writes <out-dir>/<area>_<sector>.wav plus songs.json with the timing reported by adsnd.
"""
import json
import struct
import subprocess
import sys
from pathlib import Path

BANKS = {"TOWN/TOWN.BIN": 1245 * 2048, "DUNGEON/DUNGEON.BIN": 3780 * 2048}


def find_sequences(data):
    """Every 'KDT ' sequence. A KCET > TSDB container can hold more than one (e.g. Entrance and Exit of
    the comedians share a container), so the whole file is scanned. Returns (name, offset)."""
    found = []
    start = 0
    while True:
        offset = data.find(b"KDT ", start)
        if offset < 0:
            return found
        size, division, tracks = struct.unpack_from("<III", data, offset + 4)
        if division in (0x30, 0x60, 0xC0, 0xF0, 0x120, 0x168, 0x180, 0x1E0) and 0 < tracks <= 32 and size < 0x20000:
            sector, rest = divmod(offset, 2048)
            found.append((f"{sector:04d}" if rest == 0x24 else f"{sector:04d}b", offset))
        start = offset + 4


def main():
    binary, out_dir = sys.argv[1], Path(sys.argv[2])
    passes = sys.argv[3] if len(sys.argv) > 3 else "2"
    fade = sys.argv[4] if len(sys.argv) > 4 else "8"
    out_dir.mkdir(parents=True, exist_ok=True)
    songs = []
    for disc_file, bank in BANKS.items():
        data = Path(disc_file).read_bytes()
        area = Path(disc_file).stem.lower()
        for name, kdt in find_sequences(data):
            if area == "dungeon" and name == "0000":
                continue  # same sequence as town sector 0
            wav = out_dir / f"{area}_{name}.wav"
            result = subprocess.run([binary, disc_file, hex(bank), hex(kdt), str(wav), passes, fade],
                                    check=True, capture_output=True, text=True)
            info = json.loads(result.stdout)
            info.update(file=wav.name, disc=disc_file, name=name, kdt=kdt, bank=bank)
            songs.append(info)
            print(f"{wav.name}: {info['duration']:.1f}s loop={info['loop']:.1f}s")
    (out_dir / "songs.json").write_text(json.dumps(songs, indent=2))


if __name__ == "__main__":
    main()
