"""Recover Vivian's field character from the authenticated retail disc.

The assembly is resident in TOWN.BIN, not in the portrait resource. Command
chains contain signed offsets, TPAGE, CLUT, UV rectangles, flips and ordering;
eight-byte animation entries retain the original ticks and loop/hold controls.
Requires Pillow and the adjacent extract_town_character_packages.py decoder.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import struct
import uuid

from PIL import Image, ImageDraw, ImageOps
from extract_town_character_packages import (
    EXPECTED_SLUS_SHA, EXPECTED_TOWN_SHA, parse_package, rgba, table_entries,
)
from psx_disc import DiscImage

PROJECT = Path(__file__).resolve().parents[1]
ROOT = PROJECT.parent
BASE = 0x8007D760
CELL, ORIGIN_X, ORIGIN_Y = 64, 32, 56
TABLES = {
    "stand": 0x800D6ED4,
    "idle": 0x800D6EE8,
    "turn_right": 0x800D6EFC,
    "turn_left": 0x800D6F10,
    "walk": 0x800D6F24,
    "gesture": 0x800D6F38,
    "step_in": 0x800D6F4C,
    "step_out": 0x800D6F60,
}


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def save_json(path: Path, data) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def read_chain(town: bytes, address: int) -> list[dict]:
    parts = []
    for index in range(32):
        offset = address - BASE + index * 12
        flags, mode, x, y, page, clut, u, v, width, height = struct.unpack_from(
            "<BBbbHHBBBB", town, offset)
        if mode != 0x2C or flags & 0x20 or (page >> 7) & 3 or not width or not height:
            raise ValueError(f"Unresolved sprite command at {BASE + offset:#x}")
        parts.append(dict(address=hex(BASE + offset), flags=flags, mode=mode, x=x, y=y,
                          tpage=page, clut=clut, u=u, v=v, width=width, height=height))
        if flags & 0x80:
            return parts
    raise ValueError(f"Unterminated chain {address:#x}")


def read_sequence(town: bytes, address: int) -> dict:
    entries = []
    for index in range(128):
        offset = address - BASE + index * 8
        ticks, kind, pointer = struct.unpack_from("<HHI", town, offset)
        if kind != 2:
            if kind not in (0, 1) or pointer != address or ticks != 0:
                raise ValueError(f"Unexpected control entry at {BASE + offset:#x}")
            return dict(address=hex(address), entries=entries, loop=kind == 1,
                        control=dict(address=hex(BASE + offset), kind=kind, pointer=hex(pointer)))
        if not 0 < ticks <= 120:
            raise ValueError(f"Invalid timing at {BASE + offset:#x}")
        entries.append(dict(address=hex(BASE + offset), ticks=ticks, chain=hex(pointer)))
    raise ValueError(f"Unterminated animation {address:#x}")


def render(parts: list[dict], vram: bytes, written: bytes) -> Image.Image:
    result = Image.new("RGBA", (CELL, CELL))
    # addPrim prepends to the PS1 ordering table: the first part is on top.
    for part in reversed(parts):
        px = (part["tpage"] & 15) * 64
        py = ((part["tpage"] >> 4) & 1) * 256
        cx, cy = (part["clut"] & 63) * 16, part["clut"] >> 6
        palette = []
        for i in range(16):
            index = cy * 1024 + cx + i
            if not written[index]:
                raise ValueError(f"Missing CLUT word {part['clut']:#x}:{i}")
            palette.append(rgba(struct.unpack_from("<H", vram, index * 2)[0]))
        pixels = []
        for y in range(part["height"]):
            for x in range(part["width"]):
                tx, ty = part["u"] + x, part["v"] + y
                index = (py + ty) * 1024 + px + tx // 4
                if not written[index]:
                    raise ValueError(f"Missing texture word for {part['address']}")
                word = struct.unpack_from("<H", vram, index * 2)[0]
                pixels.append(palette[(word >> ((tx % 4) * 4)) & 15])
        tile = Image.new("RGBA", (part["width"], part["height"]))
        tile.putdata(pixels)
        x, y = part["x"], part["y"]
        if part["flags"] & 1:
            tile = ImageOps.mirror(tile)
            x = -x - part["width"]
        if part["flags"] & 2:
            tile = ImageOps.flip(tile)
            y = -y - part["height"]
        x, y = x + ORIGIN_X, y + ORIGIN_Y
        if x < 0 or y < 0 or x + tile.width > CELL or y + tile.height > CELL:
            raise ValueError(f"Frame would be cropped: {part['address']}")
        result.alpha_composite(tile, (x, y))
    if result.getbbox() is None:
        raise ValueError("Empty assembled frame")
    return result


def write_meta(path: Path, kind: str) -> str:
    meta = Path(str(path) + ".meta")
    if meta.exists():
        return next(line.split(": ", 1)[1] for line in meta.read_text().splitlines() if line.startswith("guid: "))
    guid = uuid.uuid5(uuid.NAMESPACE_URL, "azure-dreams/retail-vivian/" + path.name).hex
    if kind == "texture":
        # Same importer family as the other runtime-created town sprites.
        template = PROJECT / "Assets/AzureDreams/Resources/Town/Arcade/YoungKoh.png.meta"
        text = template.read_text(encoding="utf-8")
        import re
        text = re.sub(r"(?m)^guid: .*", "guid: " + guid, text)
        text = re.sub(r"(?m)^(\s*)textureCompression: \d+", r"\g<1>textureCompression: 0", text)
        text = re.sub(r"(?m)^(\s*)spriteMode: \d+", r"\g<1>spriteMode: 0", text)
        text = re.sub(r"(?m)^(\s*)textureType: \d+", r"\g<1>textureType: 0", text)
    else:
        text = f"fileFormatVersion: 2\nguid: {guid}\nTextScriptImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    meta.write_text(text, encoding="utf-8")
    return guid


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--disc", type=Path, default=ROOT / "Azure Dreams.bin")
    parser.add_argument("--output", type=Path, default=ROOT / "extracted_assets/reimagined_pipeline/TownResidents/Vivian")
    parser.add_argument("--install", action="store_true", help="Copy the atlas and playback data into Unity; never touches portraits")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    disc = DiscImage(args.disc)
    exe, _ = disc.read_file("SLUS_006.14")
    town, _ = disc.read_file("TOWN/TOWN.BIN")
    if digest(town) != EXPECTED_TOWN_SHA or digest(exe) != EXPECTED_SLUS_SHA:
        raise ValueError("Original files do not match the authenticated NTSC-U edition")
    # Native 0x800CCA64 registers person 12; its four private handlers are the
    # developer's SSTP_VIVIAN_TURN_R/L_INIT and DANCE_A/B_INIT (actions 22..25).
    assert struct.unpack_from("<I", town, 0x800CCA84 - BASE)[0] == 0x2405000C
    private_handlers = struct.unpack_from("<4I", town, 0x800D6FA8 - BASE)
    assert private_handlers == (0x800CCAAC, 0x800CCB0C, 0x800CCB6C, 0x800CCBD0)

    vram, written = bytearray(1024 * 512 * 2), bytearray(1024 * 512)
    sources = []
    descriptors = {entry["resource_id"]: entry for entry in table_entries(exe, town)}
    for selector in (6, 1):
        entry = descriptors[selector]
        raw = disc.read_data(entry["lba"], entry["sectors"])
        folder = args.output / f"source_package_{selector:02d}"
        folder.mkdir(exist_ok=True)
        decoded = parse_package(raw, folder)
        data = (folder / "vram_words_le.bin").read_bytes()
        mask = (folder / "vram_written_mask.bin").read_bytes()
        for index, exists in enumerate(mask):
            if exists:
                vram[index*2:index*2+2] = data[index*2:index*2+2]
                written[index] = 1
        sources.append(dict(selector=selector, lba=entry["lba"], sectors=entry["sectors"], sha256=digest(raw),
                            descriptor=hex(entry["descriptor_va"]), command_count=decoded["command_count"]))

    clips, sequences, chains = [], {}, {}
    for key, address in TABLES.items():
        pointers = struct.unpack_from("<5I", town, address - BASE)
        assert pointers[-1] == 0
        directions = []
        for pointer in pointers[:4]:
            sequence = sequences.setdefault(pointer, read_sequence(town, pointer))
            for entry in sequence["entries"]:
                ptr = int(entry["chain"], 16)
                chains.setdefault(ptr, read_chain(town, ptr))
            directions.append(pointer)
        clips.append(dict(key=key, table=hex(address), direction_pointers=directions))

    frame_pointers = sorted(chains)
    frame_ids = {ptr: index for index, ptr in enumerate(frame_pointers)}
    rows = (len(frame_pointers) + 7) // 8
    atlas = Image.new("RGBA", (8 * CELL, rows * CELL))
    frames_dir = args.output / "frames"
    frames_dir.mkdir(exist_ok=True)
    for ptr, index in frame_ids.items():
        frame = render(chains[ptr], vram, written)
        frame.save(frames_dir / f"{ptr:08x}.png")
        atlas.alpha_composite(frame, ((index % 8) * CELL, (index // 8) * CELL))
    atlas.save(args.output / "Vivian.png")
    runtime_clips = []
    for clip in clips:
        directions = []
        for pointer in clip["direction_pointers"]:
            sequence = sequences[pointer]
            directions.append(dict(frames=[frame_ids[int(e["chain"], 16)] for e in sequence["entries"]],
                                   ticks=[e["ticks"] for e in sequence["entries"]], loop=sequence["loop"]))
        runtime_clips.append(dict(key=clip["key"], directions=directions))
    save_json(args.output / "Vivian.json", dict(cellSize=CELL, columns=8, frameCount=len(frame_ids),
        pivotX=ORIGIN_X / CELL, pivotY=(CELL - ORIGIN_Y) / CELL, ticksPerSecond=60,
        # Native side chain faces left; the opposite side reuses it mirrored.
        clips=runtime_clips))
    palettes = []
    for clut in sorted({p["clut"] for parts in chains.values() for p in parts}):
        x,y=(clut&63)*16,clut>>6
        words=list(struct.unpack_from("<16H",vram,(y*1024+x)*2))
        palettes.append(dict(clut=hex(clut),vram_word_origin=[x,y],rgb555_stp_words=words,
                             rgba=[list(rgba(word)) for word in words]))
    save_json(args.output / "manifest.json", dict(schema="azure-retail-town-resident-v1", character="Vivian",
        source_town_sha256=digest(town), source_exe_sha256=digest(exe), resident_load_delta=hex(BASE),
        identity=dict(person_slot=12,initializer=hex(0x800CCA64),private_action_table=hex(0x800D6FA8),
                      private_actions=dict(zip(["22: TURN_R_INIT","23: TURN_L_INIT","24: DANCE_A_INIT","25: DANCE_B_INIT"],map(hex,private_handlers)))),
        source_packages=sources, palettes=palettes, frame_count=len(frame_ids),
        animation_tables=[dict(key=c["key"],address=c["table"]) for c in clips],
        sequences=list(sequences.values()),frames=[dict(index=frame_ids[ptr],address=hex(ptr),parts=chains[ptr]) for ptr in frame_pointers],
        note="Source assembly, palettes, ticks and loop/hold controls. Private dance movement controllers are separate from these sprite clips; no portrait pixels or invented body parts are used."))

    preview=Image.new("RGB",(8*96,rows*108),(46,48,57));draw=ImageDraw.Draw(preview)
    for ptr,index in frame_ids.items():
        frame=Image.open(frames_dir/f"{ptr:08x}.png").resize((96,96),Image.Resampling.NEAREST)
        x,y=(index%8)*96,(index//8)*108
        preview.paste(frame,(x,y),frame);draw.text((x+8,y+96),f"{ptr:08x}",fill="white")
    preview.save(args.output/"contact.png")
    if args.install:
        target=PROJECT/"Assets/AzureDreams/Resources/Town/Residents"
        target.mkdir(parents=True,exist_ok=True)
        for name,kind in [("Vivian.png","texture"),("Vivian.json","text")]:
            dest=target/name;shutil.copyfile(args.output/name,dest);write_meta(dest,kind)
    print(f"Vivian: {len(frame_ids)} complete frames, {len(sequences)} timed sequences, {len(palettes)} original CLUTs; all texture/palette words resolved")


if __name__ == "__main__":
    main()
