"""Extract retail Tower ground item graphics from the NTSC executable and a PS1 VRAM capture."""
from pathlib import Path
from struct import unpack_from
import sys
from uuid import uuid4

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Assets/AzureDreams/Resources/Tower"
CATEGORY_TABLE = 0x80073414
ICON_TABLE = 0x8007361C
ICON_SIZE = 16


def psx_offset(address):
    return address - 0x8002D000 + 0x800


def read_item_map(executable):
    mapping = bytearray(19 * 256)
    for kind in range(1, 18):  # Eggs have their own renderer.
        layout, _, count, _, _, _, records, _ = unpack_from(
            "<4B4I", executable, psx_offset(CATEGORY_TABLE) + kind * 20)
        for subtype in range(1, count):
            record = psx_offset(records) + subtype * (12 if layout else 20)
            icon, = unpack_from("<b", executable, record + 2)
            if icon >= 0:
                mapping[kind * 256 + subtype] = icon + 1

    # Project catalog extensions use their retail equipment family's icon.
    mapping[15 * 256 + 16] = mapping[15 * 256 + 1]
    mapping[16 * 256 + 10] = mapping[16 * 256 + 1]
    return mapping


def read_icon(executable, vram, index):
    descriptor, = unpack_from("<I", executable, psx_offset(ICON_TABLE) + 4 * index)
    page, clut, u, v, width, height = unpack_from("<HH4B", executable, psx_offset(descriptor) + 4)
    flags = executable[psx_offset(descriptor)]
    if flags != 0xC0 or (page >> 7) & 3 or (width, height) != (ICON_SIZE, ICON_SIZE):
        raise ValueError(f"Unsupported retail ground icon descriptor {index}")
    page_x, page_y = (page & 15) * 64, ((page >> 4) & 1) * 256
    palette = ((clut >> 6) * 1024 + (clut & 63) * 16) * 2
    colors = unpack_from("<16H", vram, palette)

    def rgba(word):
        channels = [(word >> shift) & 31 for shift in (0, 5, 10)]
        return (*(channel << 3 | channel >> 2 for channel in channels), 255 if word else 0)

    pixels = []
    for y in range(v, v + height):
        for x in range(u, u + width):
            offset = ((page_y + y) * 1024 + page_x + x // 4) * 2
            word, = unpack_from("<H", vram, offset)
            pixels.append(rgba(colors[(word >> (x % 4 * 4)) & 15]))
    if not any(pixel[3] for pixel in pixels):
        raise ValueError(f"VRAM capture is missing retail ground icon {index}")
    image = Image.new("RGBA", (width, height))
    image.putdata(pixels)
    return image


def ensure_meta(path, settings):
    meta = path.with_name(path.name + ".meta")
    if not meta.exists():
        meta.write_text(f"fileFormatVersion: 2\nguid: {uuid4().hex}\n{settings}\n", encoding="utf-8")


def main():
    executable, vram = (Path(arg).read_bytes() for arg in sys.argv[1:3])
    if executable[:8] != b"PS-X EXE" or len(vram) != 1024 * 512 * 2:
        raise ValueError("Expected the NTSC PS-X executable and an uncompressed PS1 VRAM capture.")
    mapping = read_item_map(executable)
    icons = sorted(set(mapping) - {0})
    atlas = Image.new("RGBA", (16 * ICON_SIZE, 8 * ICON_SIZE))
    for slot, descriptor in enumerate(icons, 1):
        atlas.paste(read_icon(executable, vram, descriptor - 1),
                    (slot % 16 * ICON_SIZE, slot // 16 * ICON_SIZE))
    slots = {descriptor: slot for slot, descriptor in enumerate(icons, 1)}
    mapping = bytes(slots.get(descriptor, 0) for descriptor in mapping)

    OUTPUT.mkdir(parents=True, exist_ok=True)
    atlas_path = OUTPUT / "GroundItemIcons.png"
    map_path = OUTPUT / "GroundItemMap.bytes"
    atlas.save(atlas_path)
    map_path.write_bytes(mapping)
    ensure_meta(OUTPUT, "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}")
    ensure_meta(map_path, "TextScriptImporter:\n  externalObjects: {}")
    ensure_meta(atlas_path, """TextureImporter:
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    textureFormat: -1
    textureCompression: 0""")
    print(f"Extracted {len(icons)} ground icons for {sum(bool(slot) for slot in mapping)} item IDs.")


if __name__ == "__main__":
    main()
