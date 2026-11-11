"""Bake tower animation atlases from the NTSC-U DUNGEON.BIN and extracted sprite metadata."""

import argparse
import json
import struct
from dataclasses import dataclass
from pathlib import Path

from PIL import Image


PROJECT = Path(__file__).resolve().parents[1]
SPRITES = PROJECT / "Assets/AzureDreams/Sprites"
CELL = 96
ORIGIN = (CELL // 2, CELL - 12)

# Directional tables referenced by the retail damage-reaction functions.
HIT_TABLES = {
    36: (0xF572E4, bytes([58, 57, 56, 57, 58, 59, 60, 59])),
    38: (0xFAB30C, bytes([37, 36, 35, 36, 37, 38, 39, 38])),
}


@dataclass(frozen=True)
class ActionSet:
    name: str
    idle: int
    walk: int
    attack: tuple[int, ...]
    magic: int | None
    sleep: int | None
    magic_body: int | None = None
    shared_magic: bool = False
    retail_colors: bool = False


# First entry of each directional block, read from the overlay frame tables.
# The five source entries run from front through the left profile to back.
ACTIONS = {
    15: ActionSet("Death", 0, 0, (20, 25), 40, 10),
    16: ActionSet("Clown", 0, 0, (30,), 35, 20),
    24: ActionSet("UBoat", 0, 0, (35,), 65, 70, magic_body=0),
    27: ActionSet("Blume", 0, 0, (15, 40), 45, 30, magic_body=35),
    28: ActionSet("Volcano", 0, 5, (30,), 50, 25, magic_body=15),
    30: ActionSet("Manoeva", 0, 0, (30, 60), 59, 25, shared_magic=True),
    32: ActionSet("Picket", 0, 5, (15, 40), 45, 30, magic_body=5),
    33: ActionSet("Kraken", 0, 0, (20, 5), 26, 46),
}


# Additional bank poses. Missing actions reuse the species' own poses and the
# runtime supplies the motion. The Frog bank contains locomotion and a held pose.
ADDITIONAL_ACTIONS = {
    1: ActionSet("Hikewne", 0, 5, (15,), 40, 10, magic_body=15),
    2: ActionSet("Kewne", 0, 0, (17,), 66, 71),
    3: ActionSet("Dragon", 0, 5, (50,), 40, 25, magic_body=50),
    4: ActionSet("Kid", 0, 5, (50,), None, 10),
    5: ActionSet("Ifrit", 0, 5, (15, 20), 45, 10, magic_body=20),
    7: ActionSet("Grineut", 0, 5, (15,), 50, 30, magic_body=0),
    8: ActionSet("Griffon", 0, 5, (20,), 50, 30, magic_body=0),
    9: ActionSet("Saber", 0, 5, (15,), 45, 25, magic_body=0),
    10: ActionSet("Snowman", 0, 5, (15,), 45, 25, magic_body=40),
    11: ActionSet("Ashra", 0, 5, (15,), 45, 35, magic_body=30),
    12: ActionSet("Arachne", 0, 5, (15,), 45, 25, magic_body=35),
    13: ActionSet("Battnel", 0, 5, (40,), 45, 10, magic_body=25),
    14: ActionSet("Nyuel", 0, 5, (15,), 51, 41),
    17: ActionSet("Univern", 0, 5, (15,), 45, 10),
    18: ActionSet("Unicorn", 0, 5, (15,), 25, 10, magic_body=20),
    19: ActionSet("Metal", 0, 5, (15,), 40, 10),
    20: ActionSet("Block", 0, 5, (15,), 45, 25, magic_body=20),
    25: ActionSet("Baloon", 0, 5, (15,), 45, 10),
    26: ActionSet("Dreamin", 0, 5, (25,), 30, 10),
    31: ActionSet("Barong", 0, 5, (15,), 40, 20),
    34: ActionSet("Weadog", 0, 5, (15,), 40, 10),
    35: ActionSet("Stealth", 0, 5, (15,), 55, 10),
    36: ActionSet("Viper", 0, 5, (15,), 25, 30),
    37: ActionSet("Naplass", 0, 5, (15,), 30, 25, magic_body=20),
    38: ActionSet("Zu", 0, 5, (15,), 30, 10),
    39: ActionSet("Mandara", 0, 5, (15,), 35, 25, magic_body=30),
    40: ActionSet("Killer", 0, 5, (15,), 40, 30, magic_body=45),
    41: ActionSet("Garuda", 0, 5, (15,), 55, 35, magic_body=45),
    42: ActionSet("Glacier", 0, 5, (15,), 20, 35),
    43: ActionSet("Tyrant", 0, 5, (15,), 45, 25, magic_body=20),
    44: ActionSet("Golem", 0, 5, (15,), 50, 10, magic_body=30),
    45: ActionSet("Maximum", 0, 5, (15,), 45, 10, magic_body=20),
    46: ActionSet("Frog", 0, 5, (), None, None, retail_colors=True),
}


def frame_parts(parts, indices, words, omit_clown_markers=False):
    frame = Image.new("RGBA", (CELL, CELL))
    for part in reversed(parts):
        if part["is_callback"] or part["source_page"] != "resource_atlas" or part["texture_mode"] != 0:
            continue
        x, y, w, h = (part[key] for key in ("x", "y", "width", "height"))
        u, v, row, flags = (part[key] for key in ("u", "v", "clut_raw", "flags"))
        if row >= 4 or u + w > indices.width or v + h > indices.height or w == 0 or h == 0:
            continue
        start = row * 16
        texels = indices.crop((u, v, u + w, v + h))
        # The Clown's action banks include solid pink staging squares on CLUT 1.
        if omit_clown_markers and row == 1 and (w, h) == (6, 6) and len(texels.getcolors()) == 1:
            continue
        tile = Image.new("RGBA", (w, h))
        tile.putdata([(start + index, 0, 0, 255 if words[start + index] else 0)
                      for index in texels.get_flattened_data()])
        if flags & 1:
            x = -x - w
            tile = tile.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        if flags & 2:
            y = -y - h
            tile = tile.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
        frame.alpha_composite(tile, (ORIGIN[0] + x, ORIGIN[1] + y))
    return frame


def sample(sequence, count):
    if not sequence:
        raise ValueError("Animation contains no drawable local frames")
    if len(sequence) <= count:
        # Give short sequences an even share of the turn, including both endpoints.
        return [sequence[i * len(sequence) // count] for i in range(count)]
    return [sequence[round(i * (len(sequence) - 1) / (count - 1))] for i in range(count)]


def compress_pose(frame, height_scale):
    # Keep the contact point with the ground while shaping an unsupported pose.
    box = frame.getbbox()
    pose = frame.crop(box)
    pose = pose.resize((pose.width, max(1, round(pose.height * height_scale))), Image.Resampling.NEAREST)
    result = Image.new("RGBA", frame.size)
    result.paste(pose, (box[0], box[3] - pose.height))
    return result


def pixel_count(frame):
    return frame.getchannel("A").histogram()[255]


def bake(dungeon, species, actions):
    resource = f"resource_{species:03}"
    metadata = json.loads((SPRITES / "DungeonCreatures" / resource / "metadata.json").read_text())
    chains = {chain["pointer"]: chain["parts"] for chain in metadata["chains"]}
    indices = Image.open(SPRITES / "Indexed" / resource / "atlas_indices.png").convert("L")
    assert indices.getextrema()[1] <= 15

    # Dungeon actor sprite-bank table (ISO DUNGEON.BIN starts at LBA 12310).
    if species == 49:
        bank, clut_offset = 0x1768000, 0x15000
    elif species == 56:
        bank, clut_offset = 0x904800, 0x2F000
    else:
        bank = (0x56D6 - 12310 + (species - 1) * 43) * 2048
        clut_offset = 26 * 2048 + 0x8000
    header = struct.unpack_from("<10I", dungeon, bank)
    assert list(header) == metadata["header_words"] and header[9] == 0
    offsets = struct.unpack_from(f"<{header[1]}I", dungeon, bank + header[0]) + (header[2],)
    words = struct.unpack_from("<256H", dungeon, bank + clut_offset)

    def render(entry):
        frames = []
        if entry is None or entry >= header[1]:
            return frames
        for offset in range(offsets[entry], offsets[entry + 1], 8):
            _, kind, pointer = struct.unpack_from("<HHI", dungeon, bank + offset)
            if kind != 2:
                continue  # Callback and loop commands belong to the original player, not its sprite poses.
            frame = frame_parts(chains[pointer], indices, words, omit_clown_markers=species == 16 and entry >= 20)
            if frame.getbbox():
                frames.append(frame)
        return frames

    hit_table = None
    if species in HIT_TABLES:
        offset, hit_table = HIT_TABLES[species]
        assert dungeon[offset:offset + 8] == hit_table
    atlas = Image.new("RGBA", ((21 if hit_table else 20) * CELL, 5 * CELL))
    for row in range(5):
        idle = render(actions.idle + row)
        idle = idle or render(actions.idle)
        walk = render(actions.walk + row) or idle
        attack = [frame for entry in actions.attack for frame in render(entry + row)]
        magic = render(None if actions.magic is None else actions.magic + (0 if actions.shared_magic else row))
        sleep = render(None if actions.sleep is None else actions.sleep + row)
        if not sleep:
            sleep = [compress_pose(idle[0], height) for height in (0.64, 0.69)]

        body_area = pixel_count(idle[0])
        complete_attack = [frame for frame in attack if pixel_count(frame) >= body_area * 0.55]
        attack = complete_attack or idle
        if actions.magic_body is not None:
            body = render(actions.magic_body + row) or idle
            if magic:
                effects = sample(magic or idle, 6)
                magic = [fx if pixel_count(fx) >= body_area * 0.6 else Image.alpha_composite(base, fx)
                         for base, fx in zip(sample(body, 6), effects)]
            else:
                magic = body
        elif not actions.shared_magic:
            # Some action frames contain only a detached sprite part or effect.
            complete = [frame for frame in magic if pixel_count(frame) >= body_area * 0.55]
            if complete:
                magic = complete
            else:
                magic = [Image.alpha_composite(idle[0], fx) for fx in magic]

        if not magic:
            magic = [compress_pose(idle[0], height) for height in (1, 0.84, 0.78, 1.05, 1.08, 1)]
        frames = sample(idle, 2) + sample(walk, 5) + sample(attack, 5) + sample(magic, 6) + sample(sleep, 2)
        if species == 2:
            # Use the otherwise unused first 12 Kewne columns. The retail
            # direction tables are indexed from front to back at slots 2–6.
            overlay_base = 0x9BF000 - 0x80170800
            higher = render(dungeon[overlay_base + 0x80175E50 + 2 + row])
            lower = render(dungeon[overlay_base + 0x80175EA8 + 2 + row])
            # The downhill table stores detached parts. Keep Kewne's body
            # visible and composite any local parts the bank provides.
            lower = [Image.alpha_composite(idle[0], part) for part in lower] or idle
            frames[:12] = sample(higher or idle, 6) + sample(lower, 6)
        if hit_table:
            hit = render(hit_table[row + 2])
            # Several hit chains only draw a detached body part from the local page.
            # Keep the full body visible when the remaining original parts are unavailable.
            complete_hit = [frame for frame in hit if pixel_count(frame) >= body_area * 0.65]
            if not complete_hit:
                print(f"{actions.name}, direction {row}: using the complete idle pose for recoil")
            frames.append(complete_hit[len(complete_hit) // 2] if complete_hit else idle[0])
        for column, frame in enumerate(frames):
            atlas.paste(frame, (column * CELL, row * CELL))
    if actions.retail_colors:
        colors = [tuple(((word >> shift & 31) * 255 + 15) // 31 for shift in (0, 5, 10)) for word in words]
        atlas.putdata([(*colors[index], alpha) if alpha else (0, 0, 0, 0)
                       for index, _, _, alpha in atlas.get_flattened_data()])
    return atlas


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("dungeon", type=Path, help="NTSC-U DUNGEON.BIN")
    parser.add_argument("--output", type=Path, default=SPRITES / "Monsters/Community")
    parser.add_argument("--additional", action="store_true", help="Bake the remaining regular species")
    parser.add_argument("--beldo", action="store_true", help="Bake Beldo from the final event sprite bank")
    parser.add_argument("--ghosh", action="store_true", help="Bake Ghosh from the Tower sprite bank")
    parser.add_argument("--hit-reactions", action="store_true", help="Update atlases with confirmed hit frames")
    args = parser.parse_args()
    dungeon = args.dungeon.read_bytes()
    args.output.mkdir(parents=True, exist_ok=True)
    action_sets = ADDITIONAL_ACTIONS if args.additional else ACTIONS
    if args.hit_reactions:
        action_sets = {species: ADDITIONAL_ACTIONS[species] for species in HIT_TABLES}
    elif args.beldo:
        # Beldo stores eight directions per action. The renderer mirrors the last three.
        action_sets = {56: ActionSet("Beldo", 96, 96, (104,), 112, None, retail_colors=True)}
    elif args.ghosh:
        action_sets = {49: ActionSet("Ghosh", 0, 5, (25,), None, None, retail_colors=True)}
    for species, actions in action_sets.items():
        suffix = "Actions" if actions.retail_colors else "IndexedActions"
        bake(dungeon, species, actions).save(args.output / f"{actions.name}{suffix}.png")
        print(f"{actions.name}: 5 directions x {21 if species in HIT_TABLES else 20} frames")


if __name__ == "__main__":
    main()
