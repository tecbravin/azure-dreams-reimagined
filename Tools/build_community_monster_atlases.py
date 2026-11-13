"""Pack community poses into the tower renderer's five-direction action layout."""
import json
from pathlib import Path
from PIL import Image

PROJECT = Path(__file__).resolve().parents[1]
SOURCE = PROJECT.parent / "community" / "monster_animations"
OUTPUT = PROJECT / "Assets/AzureDreams/Resources/Monsters/Community"
PALETTES = json.loads((PROJECT.parent / "monsterPalettes.json").read_text())["monsters"]
FALLBACK = json.loads((PROJECT / "Assets/AzureDreams/Sprites/Monsters/Community/fullmonsters.json").read_text())["monsters"]
CHANNELS = (0, 23, 47, 63, 79, 95, 103, 111, 119, 127, 135, 143, 151, 159, 167, 175,
            183, 191, 199, 207, 211, 215, 219, 223, 227, 231, 235, 239, 243, 247, 251, 255)
CELL = 96

def palette_lookup(species):
    rows = PALETTES.get(str(species), FALLBACK[str(species)])
    lookup = {}
    for row in range(4):
        for index, color in enumerate(rows.get(str(row), [])[1:], 1):
            rgb = tuple(CHANNELS[int(color[i:i+2], 16) // 8] for i in (1, 3, 5))
            lookup.setdefault(rgb, row * 16 + index)
    return lookup

def frame(species, animation, index):
    path = SOURCE / f"monster_{species:02x}_animation_{animation:02d}_frame_{index:02d}.png"
    return Image.open(path).convert("RGBA") if path.exists() else None

def build(species):
    lookup = palette_lookup(species)
    palette_colors = list(lookup)
    atlas = Image.new("RGBA", (21 * CELL, 5 * CELL))
    for direction in range(5):
        # The extracted sequences are grouped in five camera-facing directions.
        for group, start, count in ((0, 0, 2), (1, 2, 5), (2, 7, 5), (3, 12, 6), (4, 18, 2)):
            animation = group * 5 + direction
            files = sorted(SOURCE.glob(f"monster_{species:02x}_animation_{animation:02d}_frame_*.png"))
            if not files:
                files = sorted(SOURCE.glob(f"monster_{species:02x}_animation_{direction:02d}_frame_*.png"))
            if not files:
                continue
            for column in range(count):
                image = Image.open(files[min(len(files)-1, column * len(files) // count)]).convert("RGBA")
                if image.width > CELL or image.height > CELL:
                    image.thumbnail((CELL, CELL), Image.Resampling.NEAREST)
                indexed = Image.new("RGBA", image.size)
                indexed.putdata([(lookup.get((r,g,b), lookup[min(palette_colors,
                    key=lambda c: (r-c[0])**2+(g-c[1])**2+(b-c[2])**2)]), 0, 0, a)
                                 if a else (0,0,0,0) for r,g,b,a in image.get_flattened_data()])
                atlas.alpha_composite(indexed, (start * CELL + column * CELL + (CELL-image.width)//2,
                                                direction * CELL + CELL-image.height))
        # Reserve the final column for the damage reaction used by the tower renderer.
        hit_files = sorted(SOURCE.glob(f"monster_{species:02x}_animation_{25+direction:02d}_frame_*.png"))
        if not hit_files:
            hit_files = sorted(SOURCE.glob(f"monster_{species:02x}_animation_{direction:02d}_frame_*.png"))
        if hit_files:
            image = Image.open(hit_files[0]).convert("RGBA")
            if image.width > CELL or image.height > CELL:
                image.thumbnail((CELL, CELL), Image.Resampling.NEAREST)
            indexed = Image.new("RGBA", image.size)
            indexed.putdata([(lookup.get((r,g,b), lookup[min(palette_colors,
                key=lambda c: (r-c[0])**2+(g-c[1])**2+(b-c[2])**2)]), 0, 0, a)
                             if a else (0,0,0,0) for r,g,b,a in image.get_flattened_data()])
            atlas.alpha_composite(indexed, (20 * CELL + (CELL-image.width)//2,
                                            direction * CELL + CELL-image.height))
    OUTPUT.mkdir(parents=True, exist_ok=True)
    atlas.save(OUTPUT / f"monster_{species:02x}.png", optimize=True)

if __name__ == "__main__":
    for species in range(1, 46):
        build(species)
    print("Packed 45 community monster atlases")
