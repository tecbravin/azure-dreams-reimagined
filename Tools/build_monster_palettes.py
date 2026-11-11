"""Build the runtime palette map from fullmonsters.json and the indexed PS1 assets."""

import json
import re
from pathlib import Path

from PIL import Image


ASSETS = Path(__file__).resolve().parents[1] / "Assets/AzureDreams"
SOURCE = ASSETS / "Sprites/Monsters/Community/fullmonsters.json"
DESTINATION = ASSETS / "Resources/Town/Retail/monster_palettes.json"
ROLES = {"enemy": 0, "fire": 4, "water": 8, "wind": 12}

# The community frame exporter uses a display curve over the 5-bit channels.
COMMUNITY_CHANNELS = (0, 23, 47, 63, 79, 95, 103, 111, 119, 127, 135, 143, 151, 159, 167, 175,
                      183, 191, 199, 207, 211, 215, 219, 223, 227, 231, 235, 239, 243, 247, 251, 255)
RGB_SOURCES = {
    2: ("kewne-locomotion", "kewne-actions"),
    6: ("FlameActions", "monster_06_animation_01_frame_00"),
    21: ("PulunpaActions", "monster_15_animation_01_frame_00"),
    22: ("TrollActions", "monster_16_animation_01_frame_00"),
    23: ("NoiseActions", "monster_17_animation_01_frame_00"),
    25: ("monster_19_animation_01_frame_00",),
    29: ("CycloneActions", "CycloneRetail"),
    31: ("monster_1f_animation_01_frame_00",),
}


def pack(rgb):
    return int.from_bytes(bytes(rgb), "big")


def source_color_indices(species, rows, texture_paths):
    lookup = {}
    # Identical baked colors can represent different indices. Alignment with the
    # original indexed poses picks the lowest slot for the shared body colors.
    for row in range(4):
        for index, raw in enumerate(rows.get(row, ())[1:], 1):
            rgb = raw.to_bytes(3, "big")
            if species == 29:  # CycloneRetail uses normal RGB555 bit expansion.
                encoded = pack(channel | (channel >> 5) for channel in rgb)
            else:
                encoded = pack(COMMUNITY_CHANNELS[channel >> 3] for channel in rgb)
            lookup.setdefault(encoded, row * 16 + index)
            if species == 21:  # Pulunpa's action atlas also uses literal CLUT colors.
                lookup.setdefault(raw, row * 16 + index)

    used = set()
    for path in texture_paths:
        with Image.open(path) as image:
            used.update(pack((r, g, b)) for r, g, b, a in image.convert("RGBA").get_flattened_data() if a)
    if missing := used - lookup.keys():
        raise ValueError(f"Species {species}: no indexed match for {sorted(hex(rgb) for rgb in missing)}")
    return [value for rgb in sorted(used) for value in (rgb, lookup[rgb])]


def build():
    monsters = json.loads(SOURCE.read_text(encoding="utf-8"))["monsters"]
    registry = json.loads((ASSETS / "Resources/Town/Retail/egg_catalog.json").read_text(encoding="utf-8"))
    names = {entry["id"]: entry["name"] for entry in registry["species"]}
    assert set(map(int, monsters)) == set(range(1, 46))
    entries = []
    for key, source_rows in sorted(monsters.items(), key=lambda item: int(item[0])):
        species = int(key)
        rows = {int(row): [int(color.removeprefix("#"), 16) for color in colors] for row, colors in source_rows.items()}
        assert set(ROLES.values()) <= rows.keys() and all(len(colors) == 16 for colors in rows.values())
        clut = ASSETS / f"Sprites/Indexed/resource_{species:03}/clut_words.asset"
        words = bytes.fromhex(re.search(r"_typelessdata: (\w+)", clut.read_text(encoding="utf-8"))[1])
        for row, colors in rows.items():
            for index, rgb in enumerate(colors):
                offset = (row * 16 + index) * 4
                word = int.from_bytes(words[offset:offset + 2], "little")
                assert rgb == pack(((word >> shift) & 31) << 3 for shift in (0, 5, 10)), (species, row, index)

        paths = [next((ASSETS / "Sprites").rglob(name + ".png")) for name in RGB_SOURCES.get(species, ())]
        entries.append({"species": species, "name": names.get(species, "Hikewne" if species == 1 else "Kewne"),
                        **{role: [rgb for row in range(start, start + 4) for rgb in rows.get(row, [-1] * 16)]
                           for role, start in ROLES.items()},
                        "rgbIndices": source_color_indices(species, rows, paths) if paths else []})

    DESTINATION.write_text('{"entries":[\n' + ',\n'.join(json.dumps(e, separators=(",", ":")) for e in entries)
                           + '\n]}\n', encoding="utf-8")
    print(f"Mapped {len(entries)} species, {len(entries) * len(ROLES)} palette variants, and {sum(map(len, RGB_SOURCES.values()))} RGB textures.")


if __name__ == "__main__":
    build()
