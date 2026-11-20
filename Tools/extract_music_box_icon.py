"""Recover Music Box's original inventory icon from the decoded PSX icons.

NTSC item kind 0x0D/subtype 4: record at executable file offset 0x45BFC,
name pointer 0x8002F738, description pointer 0x8002F6DC, descriptor index 20.
The existing ADStateDisplay extraction labels this icon 'chest'. Do not crop
GroundItemIcons at fixed 16px offsets: that atlas can contain redesigned art.
"""
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets/AzureDreams/Sprites/Items/ADStateDisplay/chest.png"
OUTPUT = ROOT / "Assets/AzureDreams/Resources/Tower/MusicBox.png"


def main():
    image = Image.open(SOURCE).convert("RGBA")
    if image.size != (16, 16) or not image.getbbox():
        raise ValueError("Expected the nonempty original 16x16 Music Box icon")
    image.save(OUTPUT)
    print(f"Extracted Music Box (kind 13, subtype 4): {OUTPUT}")


if __name__ == "__main__":
    main()
