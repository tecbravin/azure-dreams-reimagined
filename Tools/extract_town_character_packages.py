"""Decode the retail packages queued by ``reserve_twch_load``.

The town executable selects these resources by the 13-entry descriptor table
loaded with TOWN.BIN.  Each package is a sequence of original PlayStation GPU
upload commands.  This tool preserves the package bytes and rebuilds only the
VRAM writes that those commands explicitly describe; it does not assign
character names or fabricate animation metadata.

Outputs are deliberately kept under Extracted/, outside Unity's Assets tree.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import struct
import sys

from PIL import Image

HERE = Path(__file__).resolve().parent
WORKSPACE = HERE.parents[1]
sys.path[:0] = [str(HERE), str(WORKSPACE / "azure-dreams-decomp-master/tools/decoded")]

from lz import decode as decode_lz
from psx_disc import DiscImage


EXPECTED_TOWN_SHA = "907725012d8d9e1deab18c28b62f4d304057e86a6415df605b9ffb87cfb2fe18"
EXPECTED_SLUS_SHA = "ae19d8d4bdd2db9a933b1e44d70dd61be670781e441d7cb37da85250a7aed31f"
EXE_BASE = 0x8002D000
TOWN_DESCRIPTOR_TABLE_OFFSET = 0x520D8
TOWN_DESCRIPTOR_TABLE_VA = 0x800CF838
VRAM_WIDTH_WORDS = 1024
VRAM_HEIGHT = 512
COMMAND_SIZE = 16


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def write_json(path: Path, value: object) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def exe_offset(address: int) -> int:
    offset = address - EXE_BASE + 0x800
    if offset < 0:
        raise ValueError(f"Descriptor address precedes executable base: {address:#x}")
    return offset


def rgba(word: int) -> tuple[int, int, int, int]:
    return tuple((value << 3) | (value >> 2) for value in
                 ((word >> shift) & 31 for shift in (0, 5, 10))) + (0 if word == 0 else 255,)


def write_vram(vram: bytearray, written: bytearray, x: int, y: int, width: int, height: int,
               payload: bytes) -> None:
    expected = width * height * 2
    if not (0 <= x < VRAM_WIDTH_WORDS and 0 <= y < VRAM_HEIGHT and width > 0 and height > 0
            and x + width <= VRAM_WIDTH_WORDS and y + height <= VRAM_HEIGHT):
        raise ValueError(f"Invalid VRAM rectangle ({x}, {y}, {width}, {height})")
    if len(payload) != expected:
        raise ValueError(f"VRAM payload is {len(payload)} bytes; expected {expected}")
    for row in range(height):
        destination = ((y + row) * VRAM_WIDTH_WORDS + x) * 2
        mask_destination = (y + row) * VRAM_WIDTH_WORDS + x
        source = row * width * 2
        vram[destination:destination + width * 2] = payload[source:source + width * 2]
        written[mask_destination:mask_destination + width] = b"\1" * width


def palette_rect(vram_offset: int, palette_count: int) -> tuple[int, int, int, int]:
    slot = (vram_offset - 0x7000) >> 6
    if slot < 0:
        raise ValueError(f"Palette VRAM offset is outside the GPU palette region: {vram_offset:#x}")
    return ((slot & 0x40) << 1) + ((vram_offset & 63) << 4), (slot & 63) + 0x1C0, palette_count << 4, 1


def mirror_palette_stp(payload: bytes, palette_count: int, every_color: bool) -> bytes:
    words = list(struct.unpack(f"<{len(payload) // 2}H", payload))
    for palette in range(palette_count):
        start = palette * 16
        for index in range(16):
            if every_color or index != 0:
                words[start + index] |= 0x8000
    return struct.pack(f"<{len(words)}H", *words)


def indexed_texture(vram: bytes, x: int, y: int, width_words: int, height: int) -> Image.Image:
    pixels = bytearray(width_words * 4 * height)
    for row in range(height):
        for column in range(width_words):
            word = struct.unpack_from("<H", vram, ((y + row) * VRAM_WIDTH_WORDS + x + column) * 2)[0]
            index = (row * width_words + column) * 4
            pixels[index:index + 4] = bytes((word & 15, (word >> 4) & 15,
                                              (word >> 8) & 15, (word >> 12) & 15))
    image = Image.frombytes("P", (width_words * 4, height), bytes(pixels))
    image.putpalette([value for color in range(16) for value in (color * 17,) * 3] + [0] * (256 - 16) * 3)
    return image


def written_crop(written: bytes) -> tuple[int, int, int, int] | None:
    points = [index for index, value in enumerate(written) if value]
    if not points:
        return None
    xs = [point % VRAM_WIDTH_WORDS for point in points]
    ys = [point // VRAM_WIDTH_WORDS for point in points]
    return min(xs), min(ys), max(xs) + 1, max(ys) + 1


def table_entries(exe: bytes, town: bytes) -> list[dict]:
    entries = []
    for resource_id in range(1, 14):
        descriptor_va = struct.unpack_from("<I", town, TOWN_DESCRIPTOR_TABLE_OFFSET + resource_id * 4)[0]
        packed, lba = struct.unpack_from("<II", exe, exe_offset(descriptor_va))
        sectors = packed >> 23
        if sectors == 0:
            raise ValueError(f"Town resource {resource_id} has an empty descriptor")
        entries.append(dict(resource_id=resource_id, descriptor_va=descriptor_va, packed_word=packed,
                            lba=lba, sectors=sectors))
    return entries


def parse_package(raw: bytes, output: Path) -> dict:
    vram = bytearray(VRAM_WIDTH_WORDS * VRAM_HEIGHT * 2)
    written = bytearray(VRAM_WIDTH_WORDS * VRAM_HEIGHT)
    texture_blocks: list[dict] = []
    palettes: list[dict] = []
    commands: list[dict] = []
    offset = 0
    terminator = None

    while offset + COMMAND_SIZE <= len(raw):
        command, size, data_offset, x, y, width, height = struct.unpack_from("<HHI4h", raw, offset)
        if command < 1 or command > 9:
            terminator = dict(file_offset=offset, command=command, size=size)
            break
        record = dict(file_offset=offset, command=command, size=size, data_offset=data_offset,
                      arguments=dict(x=x, y=y, width=width, height=height))
        if data_offset >= len(raw):
            raise ValueError(f"Command at {offset:#x} points outside its package: {data_offset:#x}")

        if command in (1, 5):
            bytes_expected = width * height * 2
            if bytes_expected <= 0:
                raise ValueError(f"Image command at {offset:#x} has a non-positive rectangle")
            if command == 1:
                payload, consumed = decode_lz(raw[data_offset:], bytes_expected)
                record.update(compression="lz", compressed_bytes=consumed)
            else:
                payload = raw[data_offset:data_offset + bytes_expected]
                record.update(compression="none", compressed_bytes=bytes_expected)
            if len(payload) != bytes_expected:
                raise ValueError(f"Image command at {offset:#x} decoded {len(payload)} bytes; expected {bytes_expected}")
            write_vram(vram, written, x, y, width, height, payload)
            block = dict(command_index=len(commands), command_file_offset=offset,
                         vram_word_rect=[x, y, width, height], texel_dimensions=[width * 4, height],
                         bit_depth=4, source="decoded_lz" if command == 1 else "uncompressed")
            texture_blocks.append(block)
            record["vram_word_rect"] = block["vram_word_rect"]
        elif command in (2, 6, 7, 8):
            palette_count = y
            if palette_count <= 0:
                raise ValueError(f"Palette command at {offset:#x} has no palettes")
            required = palette_count * 16 * 2
            payload = raw[data_offset:data_offset + required]
            if len(payload) != required:
                raise ValueError(f"Palette command at {offset:#x} is truncated")
            flags = width
            if command == 6:
                flags |= 2
            elif command == 7:
                payload = mirror_palette_stp(payload, palette_count, every_color=False)
                flags |= 4
            elif command == 8:
                payload = mirror_palette_stp(payload, palette_count, every_color=True)
                flags |= 2
            rect = palette_rect(x, palette_count)
            record.update(vram_word_rect=list(rect), upload_flags=flags,
                          palette_stp_adjustment={2: "none", 6: "none", 7: "colors_1_to_15", 8: "all_colors"}[command])
            if not flags & 1:
                write_vram(vram, written, *rect, payload)
            for palette_index in range(palette_count):
                words = struct.unpack_from("<16H", payload, palette_index * 32)
                palettes.append(dict(command_index=len(commands), command_file_offset=offset,
                                     palette_index=palette_index, vram_word_origin=[rect[0] + palette_index * 16, rect[1]],
                                     rgb555_stp_words=list(words), rgba_preview=[list(rgba(word)) for word in words]))
        elif command == 3:
            payload, consumed = decode_lz(raw[data_offset:], 0x10000)
            record.update(operation="decode_to_runtime_scratch", compression="lz", decoded_bytes=len(payload),
                          compressed_bytes=consumed, vram_write=False)
        elif command == 4:
            count = max(0, width + height)
            if data_offset + count > len(raw):
                raise ValueError(f"Scratch copy command at {offset:#x} is truncated")
            record.update(operation="copy_to_runtime_scratch", copied_bytes=count, vram_write=False)
        elif command == 9:
            record.update(operation="relocate_runtime_pointers", relocation_count=x, vram_write=False)
        commands.append(record)
        if size == 0:
            terminator = dict(file_offset=offset, command=command, size=size, reason="zero_command_stride")
            break
        if size < COMMAND_SIZE or offset + size > len(raw):
            raise ValueError(f"Invalid command stride {size} at {offset:#x}")
        offset += size
    else:
        raise ValueError("Package ends before a command terminator")

    if terminator is None:
        raise ValueError("Package has no command terminator")
    if not commands:
        raise ValueError("Package contains no GPU commands")

    block_dir = output / "texture_blocks"
    block_dir.mkdir(exist_ok=True)
    for index, block in enumerate(texture_blocks):
        x, y, width, height = block["vram_word_rect"]
        filename = f"block_{index:02d}_indices.png"
        indexed_texture(vram, x, y, width, height).save(block_dir / filename)
        block["indexed_png"] = f"texture_blocks/{filename}"
    write_json(output / "commands.json", dict(commands=commands, terminator=terminator))
    write_json(output / "palettes.json", palettes)
    (output / "vram_words_le.bin").write_bytes(vram)
    (output / "vram_written_mask.bin").write_bytes(written)
    Image.frombytes("L", (VRAM_WIDTH_WORDS, VRAM_HEIGHT), bytes(value * 255 for value in written)).save(
        output / "vram_written_mask.png")
    crop = written_crop(written)
    return dict(command_count=len(commands), terminator=terminator, texture_blocks=texture_blocks,
                palettes=palettes, written_word_count=sum(written), written_word_crop=list(crop) if crop else None,
                vram_snapshot="vram_words_le.bin", vram_written_mask="vram_written_mask.bin",
                vram_written_mask_png="vram_written_mask.png")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--disc", type=Path, default=WORKSPACE / "Azure Dreams.bin")
    parser.add_argument("--output", type=Path,
                        default=WORKSPACE / "extracted_assets/reimagined_pipeline/TownCharacterPackages")
    args = parser.parse_args()
    disc = DiscImage(args.disc)
    exe, exe_record = disc.read_file("SLUS_006.14")
    town, town_record = disc.read_file("TOWN/TOWN.BIN")
    if sha256(exe) != EXPECTED_SLUS_SHA or sha256(town) != EXPECTED_TOWN_SHA:
        raise ValueError("Disc executable or TOWN.BIN does not match the authenticated retail edition")

    output = args.output
    output.mkdir(parents=True, exist_ok=True)
    entries = table_entries(exe, town)
    by_descriptor: dict[int, list[dict]] = {}
    for entry in entries:
        by_descriptor.setdefault(entry["descriptor_va"], []).append(entry)
    manifest = dict(schema="azure-town-reserved-resource-decode-v1",
                    source=dict(disc=str(args.disc), executable=dict(**exe_record, sha256=sha256(exe)),
                                town_file=dict(**town_record, sha256=sha256(town))),
                    selector_table=dict(runtime_address=f"{TOWN_DESCRIPTOR_TABLE_VA:#x}",
                                        town_file_offset=f"{TOWN_DESCRIPTOR_TABLE_OFFSET:#x}",
                                        selector_ids=list(range(1, 14))),
                    identity_note="Selector IDs are original reserve_twch_load table indices. Character names are intentionally not inferred by this decoder.",
                    packages=[])
    for descriptor_va, aliases in by_descriptor.items():
        entry = aliases[0]
        raw = disc.read_data(entry["lba"], entry["sectors"])
        name = f"resource_{min(item['resource_id'] for item in aliases):02d}"
        directory = output / name
        directory.mkdir(exist_ok=True)
        (directory / "package.bin").write_bytes(raw)
        decoded = parse_package(raw, directory)
        package = dict(name=name, aliases=[item["resource_id"] for item in aliases],
                       descriptor_va=f"{descriptor_va:#x}", packed_word=f"{entry['packed_word']:#x}",
                       lba=entry["lba"], sectors=entry["sectors"], package_bytes=len(raw),
                       package_sha256=sha256(raw), raw_package="package.bin", **decoded)
        write_json(directory / "metadata.json", package)
        manifest["packages"].append(dict(name=name, aliases=package["aliases"],
                                         descriptor_va=package["descriptor_va"], lba=package["lba"],
                                         sectors=package["sectors"], package_sha256=package["package_sha256"],
                                         metadata=f"{name}/metadata.json",
                                         command_count=package["command_count"],
                                         texture_block_count=len(package["texture_blocks"]),
                                         palette_count=len(package["palettes"])))
        print(f"{name}: selectors {package['aliases']}; {decoded['command_count']} commands; "
              f"{len(decoded['texture_blocks'])} texture blocks; {len(decoded['palettes'])} palettes")
    write_json(output / "manifest.json", manifest)


if __name__ == "__main__":
    main()
