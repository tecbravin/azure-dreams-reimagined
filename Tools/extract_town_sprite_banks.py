"""Decode the retail town sprite bundles directly from the PS1 disc image.

Exports lossless source metadata, palettes, frame timings, and flat PNG previews.
PNG compositing is for inspection: PS1 ordering, blending and shared VRAM overlays
still require the original renderer. No assets in the Unity project are replaced.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import struct
import sys

from PIL import Image, ImageOps

HERE = Path(__file__).resolve().parent
WORKSPACE = HERE.parents[1]
sys.path[:0] = [str(WORKSPACE / "extracted_assets/verified_decode"),
                str(WORKSPACE / "azure-dreams-decomp-master/tools/decoded")]

from extract_verified_sprites import color, detile_4bpp, parse_metadata
from lz import decode as decode_lz
from psx_disc import DiscImage

EXPECTED_TOWN_SHA = "907725012d8d9e1deab18c28b62f4d304057e86a6415df605b9ffb87cfb2fe18"
EXPECTED_SLUS_SHA = "ae19d8d4bdd2db9a933b1e44d70dd61be670781e441d7cb37da85250a7aed31f"
EXE_BASE = 0x8002D000
INDEX_VA = 0x80071210
CELL = 64


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def write_json(path: Path, value) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def exe_offset(va: int) -> int:
    return va - EXE_BASE + 0x800


def bundle_locations(exe: bytes) -> list[dict]:
    result = []
    for index, name in [(1, "town_koh"), (2, "town_koh_alternate")]:
        descriptor, destination = struct.unpack_from("<II", exe, exe_offset(INDEX_VA) + index * 8)
        packed, lba = struct.unpack_from("<II", exe, exe_offset(descriptor))
        sectors = packed >> 23
        if destination != 0x80175800 or not sectors:
            raise ValueError(f"Unexpected town bundle descriptor {index}: {packed:#x}")
        result.append(dict(name=name, lba=lba, sectors=sectors, descriptor_va=descriptor,
                           descriptor_raw=[packed, lba], executable_table_index=index,
                           image_lba=None))
    if [(r["lba"], r["sectors"]) for r in result] != [(6803, 135), (6985, 68)]:
        raise ValueError("Retail executable town bundle locations differ")
    for resource_id, name in [(0x3A, "town_kewne"), (0x3B, "town_resource_3b")]:
        lba = 0x1B8D + (resource_id - 0x3A) * 0x2B
        result.append(dict(name=name, lba=lba, sectors=26, image_lba=lba + 26,
                           image_sectors=17, resource_id=resource_id,
                           location_rule="metadata=0x1B8D+(id-0x3A)*0x2B; image=metadata+0x1A"))
    return result


def animations(bank: bytes, meta: dict, frame_names: set[int]) -> list[dict]:
    groups = []
    anchor = meta["anchor"]
    for group in range(meta["animation_count"]):
        offset = struct.unpack_from("<I", bank, meta["animation_table_offset"] + group * 4)[0] - anchor
        start = offset
        entries = []
        for _ in range(512):
            if not 0 <= offset <= len(bank) - 8:
                raise ValueError(f"Animation {group} leaves bundle at {offset:#x}")
            ticks, kind, pointer = struct.unpack_from("<HHI", bank, offset)
            entry = dict(metadata_offset=offset, ticks_raw=ticks, kind=kind, pointer=pointer)
            if kind == 2:
                if pointer not in frame_names:
                    raise ValueError(f"Animation {group} references absent chain {pointer:#x}")
                entry["frame_png"] = f"frames/chain_{pointer:08x}.png"
            entries.append(entry)
            offset += 8
            if kind != 2 or ticks == 0:
                break
        else:
            raise ValueError(f"Animation {group} has no terminator")
        groups.append(dict(index=group, sequence_offset=start, entries=entries))
    return groups


def palette_record(words: tuple[int, ...]) -> dict:
    return dict(rgb555_stp_words=list(words), rgba_preview=[list(color(w)) for w in words],
                note="PS1 bit 15 is semi-transparency, not ordinary alpha; zero word is transparent in previews")


def body_tile(bank: bytes, texture_offset: int, pointer: int, part_index: int, part: dict,
              palette: tuple[int, ...]) -> Image.Image:
    compressed_offset = struct.unpack_from("<I", bank, pointer - (part_index + 1) * 4)[0]
    if texture_offset + compressed_offset >= len(bank):
        raise ValueError(f"Compressed frame offset outside bundle: {pointer:#x}")
    stream, _ = decode_lz(bank[texture_offset + compressed_offset:], 8192)
    pixels = bytearray()
    for token in stream:
        pixels.extend(bytes([token & 63]) * (1 + (token >> 6)))
    width, height = part["width"], part["height"]
    stride = (width + 1) // 2 * 2
    if len(pixels) < stride * height:
        raise ValueError(f"Incomplete RLE body tile: {pointer:#x}")
    tile = Image.new("RGBA", (width, height))
    tile.putdata([color(palette[pixels[y * stride + x]]) for y in range(height) for x in range(width)])
    return tile


def render_body_chain(bank: bytes, meta: dict, chain: dict, palette: tuple[int, ...]) -> tuple[Image.Image, dict]:
    parts = [part for part in chain["parts"] if part["texture_mode"] == 1
             and part["source_page"] == "alternate_shared_page" and not part["is_callback"]]
    if not parts:
        raise ValueError(f"No compressed body part in {chain['pointer']:#x}")
    tiles = []
    for index in reversed(range(len(parts))):
        part = parts[index]
        tile = body_tile(bank, meta["header_words"][3], chain["pointer"], index, part, palette)
        x, y = CELL // 2 + part["x"], 53 + part["y"]
        tiles.append((tile, x, y))
    left = min(0, *(x for _, x, _ in tiles))
    top = min(0, *(y for _, _, y in tiles))
    right = max(CELL, *(x + tile.width for tile, x, _ in tiles))
    bottom = max(CELL, *(y + tile.height for tile, _, y in tiles))
    canvas = Image.new("RGBA", (right - left, bottom - top))
    for tile, x, y in tiles:
        canvas.alpha_composite(tile, (x - left, y - top))
    omitted = [part["metadata_offset"] for part in chain["parts"] if part not in parts]
    return canvas, dict(body_parts=len(parts), origin=[left, top],
                        omitted_shared_or_equipment_parts=omitted,
                        status="body_only_flat_preview" if omitted else "flat_preview")


def render_atlas_chain(chain: dict, atlases: dict[int, Image.Image]) -> tuple[Image.Image, dict]:
    boxes = []
    rendered = []
    omitted = []
    for part in chain["parts"]:
        if (part["is_callback"] or part["source_page"] != "resource_atlas"
                or part["texture_mode"] != 0 or part["clut_raw"] not in atlases
                or part["u"] + part["width"] > 256 or part["v"] + part["height"] > 256):
            omitted.append(part["metadata_offset"])
            continue
        x = -part["x"] - part["width"] if part["flags"] & 1 else part["x"]
        y = -part["y"] - part["height"] if part["flags"] & 2 else part["y"]
        boxes.append((x, y, x + part["width"], y + part["height"]))
        rendered.append(part)
    if not rendered:
        return Image.new("RGBA", (1, 1)), dict(origin=[0, 0], omitted_parts=omitted,
                                                  status="source_page_unresolved")
    left, top = min(b[0] for b in boxes), min(b[1] for b in boxes)
    right, bottom = max(b[2] for b in boxes), max(b[3] for b in boxes)
    canvas = Image.new("RGBA", (right - left, bottom - top))
    for part, box in zip(rendered, boxes):
        tile = atlases[part["clut_raw"]].crop((part["u"], part["v"],
                                                   part["u"] + part["width"], part["v"] + part["height"]))
        if part["flags"] & 1:
            tile = ImageOps.mirror(tile)
        if part["flags"] & 2:
            tile = ImageOps.flip(tile)
        canvas.alpha_composite(tile, (box[0] - left, box[1] - top))
    return canvas, dict(origin=[left, top], omitted_parts=omitted,
                        status="partial_flat_preview" if omitted else "flat_preview")


def export_bank(disc: DiscImage, location: dict, output: Path) -> dict:
    bank = disc.read_data(location["lba"], location["sectors"])
    metadata = parse_metadata(bank)
    directory = output / location["name"]
    frames_dir = directory / "frames"
    frames_dir.mkdir(parents=True, exist_ok=True)
    write_json(directory / "metadata.json", metadata)
    palette_data = {}
    atlases = {}
    if location["image_lba"] is None:
        words = struct.unpack_from("<64H", bank, metadata["header_words"][7])
        palette_data["body_64_color"] = palette_record(words)
    else:
        image = disc.read_data(location["image_lba"], location["image_sectors"])
        indices = detile_4bpp(image[:0x8000])
        words = struct.unpack_from("<256H", image, 0x8000)
        palette_dir = directory / "palettes"
        palette_dir.mkdir(exist_ok=True)
        Image.frombytes("L", (256, 256), indices).save(palette_dir / "atlas_indices.png")
        for i in range(16):
            palette_data[str(i)] = palette_record(words[i * 16:(i + 1) * 16])
            atlas = Image.frombytes("RGBA", (256, 256),
                                    b"".join(bytes(color(words[i * 16 + value])) for value in indices))
            atlas.save(palette_dir / f"atlas_palette_{i:02d}.png")
            atlases[i] = atlas
    write_json(directory / "palettes.json", palette_data)

    frame_meta = []
    frames = {}
    for chain in metadata["chains"]:
        pointer = chain["pointer"]
        if location["image_lba"] is None:
            image, details = render_body_chain(bank, metadata, chain, words)
        else:
            image, details = render_atlas_chain(chain, atlases)
        image.save(frames_dir / f"chain_{pointer:08x}.png")
        frames[pointer] = image
        frame_meta.append(dict(pointer=pointer, png=f"frames/chain_{pointer:08x}.png",
                               size=list(image.size), **details))
    write_json(directory / "frames.json", frame_meta)
    groups = animations(bank, metadata, set(frames))
    write_json(directory / "animations.json", {
        "timing": "raw frame ticks from retail sequence; runtime divides by animation_tick_scale, which can vary by context",
        "groups": groups,
    })
    strips_dir = directory / "animation_strips"
    strips_dir.mkdir(exist_ok=True)
    for group in groups:
        sequence = [e for e in group["entries"] if e["kind"] == 2 and e["ticks_raw"]]
        if not sequence:
            continue
        cell_width = max(64, *(frames[e["pointer"]].width for e in sequence))
        cell_height = max(64, *(frames[e["pointer"]].height for e in sequence))
        strip = Image.new("RGBA", (cell_width * len(sequence), cell_height))
        for n, entry in enumerate(sequence):
            sprite = frames[entry["pointer"]]
            strip.alpha_composite(sprite, (n * cell_width + (cell_width - sprite.width) // 2,
                                           (cell_height - sprite.height) // 2))
        strip.save(strips_dir / f"animation_{group['index']:03d}.png")
    return dict(**location, bundle_sha256=sha(bank), metadata_header=metadata["header_words"],
                animation_groups=len(groups), unique_chains=len(frames),
                source="Azure Dreams.bin, PS1 Mode-2 Form-1 user data",
                frame_preview_limitations="flat render; shared VRAM overlays and PS1 GPU blending are not reconstructed")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--disc", type=Path, default=WORKSPACE / "Azure Dreams.bin")
    parser.add_argument("--output", type=Path,
                        default=WORKSPACE / "extracted_assets/reimagined_pipeline/TownSpriteBanks")
    args = parser.parse_args()
    disc = DiscImage(args.disc)
    exe, exe_record = disc.read_file("SLUS_006.14")
    town, town_record = disc.read_file("TOWN/TOWN.BIN")
    if sha(exe) != EXPECTED_SLUS_SHA or sha(town) != EXPECTED_TOWN_SHA:
        raise ValueError("Disc executable or town file does not match the decoded retail edition")
    manifest = dict(executable=dict(**exe_record, sha256=sha(exe)),
                    town_file=dict(**town_record, sha256=sha(town)),
                    banks=[])
    for location in bundle_locations(exe):
        result = export_bank(disc, location, args.output)
        manifest["banks"].append(result)
        print(f"{result['name']}: {result['animation_groups']} groups, {result['unique_chains']} frames")
    write_json(args.output / "manifest.json", manifest)


if __name__ == "__main__":
    main()
