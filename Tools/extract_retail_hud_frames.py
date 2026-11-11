"""Decode the PS1 window texture page from a raw retail VRAM capture."""

import argparse
from pathlib import Path
from struct import unpack

from PIL import Image


def decode_atlas(raw_vram):
    if len(raw_vram) != 1024 * 512 * 2:
        raise ValueError("Expected a 1024 by 512 PS1 VRAM capture.")
    words = unpack("<524288H", raw_vram)
    # The window draw-mode code selects the 4bpp page at (448, 256).
    # Retail window sprite descriptors reference CLUT row 499.
    palettes = (0, 1, 2, 4, 7, 10)
    atlas = Image.new("RGBA", (128, 128 * len(palettes)))
    for row, palette_index in enumerate(palettes):
        start = 499 * 1024 + palette_index * 16
        palette = [
            (*(word >> shift & 31 for shift in (0, 5, 10)), 255 if word else 0)
            for word in words[start:start + 16]
        ]
        palette = [(r * 255 // 31, g * 255 // 31, b * 255 // 31, a) for r, g, b, a in palette]
        if not any(color[3] for color in palette):
            raise ValueError(f"Capture is missing window palette {palette_index}.")
        pixels = [
            palette[(words[(256 + y) * 1024 + 448 + x // 4] >> (x % 4 * 4)) & 15]
            for y in range(128)
            for x in range(128)
        ]
        band = Image.new("RGBA", (128, 128))
        band.putdata(pixels)
        atlas.paste(band, (0, row * 128))
    return atlas


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("vram", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    decode_atlas(args.vram.read_bytes()).save(args.output)
