"""Pack community poses into tower fallback and full streaming action atlases."""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image

PROJECT = Path(__file__).resolve().parents[1]
SOURCE = PROJECT.parent / "community" / "monster_animations"
OUTPUT = PROJECT / "Assets/AzureDreams/Resources/Monsters/Community"
STREAMING_OUTPUT = PROJECT / "Assets/StreamingAssets/Monsters/Community"
PALETTES = json.loads((PROJECT.parent / "monsterPalettes.json").read_text())["monsters"]
FALLBACK = json.loads((PROJECT / "Assets/AzureDreams/Sprites/Monsters/Community/fullmonsters.json").read_text())["monsters"]
CHANNELS = (0, 23, 47, 63, 79, 95, 103, 111, 119, 127, 135, 143, 151, 159, 167, 175,
            183, 191, 199, 207, 211, 215, 219, 223, 227, 231, 235, 239, 243, 247, 251, 255)
CELL = 96
# The source groups are five camera-facing directions per action.
ACTION_GROUPS = (
    ("idle", 0), ("walk", 5), ("attack", 10), ("magic", 15),
    ("sleep", 20), ("hit", 25), ("special_30", 30), ("defeated", 35),
    ("special_40", 40), ("special_45", 45),
)
# Every tower-rendered monster must have these states.  The community dump is
# not uniform: several retail creatures only expose a subset of action banks.
# When that happens, keep the creature's own extracted pose sequence instead
# of falling back to a generic or another creature's artwork.
RUNTIME_ACTIONS = ("idle", "walk", "attack", "magic", "sleep", "hit", "defeated")
ACTION_FALLBACKS = {
    "idle": ("walk", "attack", "magic", "sleep", "hit", "defeated"),
    "walk": ("idle", "attack", "magic", "sleep", "hit", "defeated"),
    "attack": ("magic", "hit", "walk", "idle", "sleep", "defeated"),
    "magic": ("attack", "hit", "idle", "walk", "sleep", "defeated"),
    "sleep": ("idle", "walk", "hit", "attack", "magic", "defeated"),
    "hit": ("attack", "magic", "idle", "walk", "sleep", "defeated"),
    "defeated": ("hit", "attack", "sleep", "idle", "walk", "magic"),
}
# Cyclone's community dump contains only partial directional data. Its captured
# retail atlas is authoritative and must not be replaced by guessed fallbacks.
RETAIL_ACTION_ONLY = {29}

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


def indexed(image, lookup, palette_colors):
    if image.width > CELL or image.height > CELL:
        image.thumbnail((CELL, CELL), Image.Resampling.NEAREST)
    result = Image.new("RGBA", image.size)
    result.putdata([(lookup.get((red, green, blue), lookup[min(palette_colors,
        key=lambda color: (red-color[0])**2+(green-color[1])**2+(blue-color[2])**2)]), 0, 0, alpha)
                    if alpha else (0, 0, 0, 0)
                    for red, green, blue, alpha in image.get_flattened_data()])
    return result


def source_frames(species, animation):
    return sorted(SOURCE.glob(f"monster_{species:02x}_animation_{animation:02d}_frame_*.png"))


def closest_direction(sequences, direction):
    """Return a same-action sequence nearest to the requested facing."""
    available = [(index, sequence) for index, sequence in enumerate(sequences) if sequence]
    if not available:
        return []
    return min(available, key=lambda pair: (abs(pair[0] - direction), pair[0]))[1]


def action_sequence(action, direction, extracted):
    """Find a directional sequence, retaining the monster's original poses.

    A missing directional bank is filled from the closest available bank of the
    same action first.  If a whole action is absent from the source dump, use a
    semantically nearby action from the same species.  This makes every state
    renderable without inventing pixels or borrowing another monster's art.
    """
    direct = extracted.get(action, [[], [], [], [], []])
    if direct[direction]:
        return direct[direction]
    sequence = closest_direction(direct, direction)
    if sequence:
        return sequence
    for fallback in ACTION_FALLBACKS.get(action, ()):
        candidate = extracted.get(fallback, [[], [], [], [], []])
        if candidate[direction]:
            return candidate[direction]
        sequence = closest_direction(candidate, direction)
        if sequence:
            return sequence
    for candidate in extracted.values():
        sequence = closest_direction(candidate, direction)
        if sequence:
            return sequence
    return []


def build_streaming_actions(species):
    """Write lossless indexed sheets with complete runtime action coverage."""
    lookup = palette_lookup(species)
    palette_colors = list(lookup)
    extracted = {
        action: [source_frames(species, first_animation + direction) for direction in range(5)]
        for action, first_animation in ACTION_GROUPS
    }
    available_actions = [action for action, _ in ACTION_GROUPS if any(extracted[action])]
    action_names = available_actions + [action for action in RUNTIME_ACTIONS if action not in available_actions]
    actions = []
    for action in action_names:
        sequences = [action_sequence(action, direction, extracted) for direction in range(5)]
        columns = max((len(sequence) for sequence in sequences), default=0)
        if columns == 0:
            continue
        atlas = Image.new("RGBA", (columns * CELL, 5 * CELL))
        for direction, sequence in enumerate(sequences):
            for column, path in enumerate(sequence):
                image = indexed(Image.open(path).convert("RGBA"), lookup, palette_colors)
                atlas.alpha_composite(image, (column * CELL + (CELL - image.width) // 2,
                                              direction * CELL + CELL - image.height))
        filename = f"monster_{species:02x}_{action}.png"
        atlas.save(STREAMING_OUTPUT / filename, optimize=True)
        actions.append({
            "name": action,
            "file": filename,
            "columns": columns,
            "frame_counts": [len(sequence) for sequence in sequences],
            "source_sha256": [
                [hashlib.sha256(path.read_bytes()).hexdigest() for path in sequence]
                for sequence in sequences
            ],
        })
    return {"species": species, "cell_size": CELL, "actions": actions}

def build(species):
    lookup = palette_lookup(species)
    palette_colors = list(lookup)
    extracted = {
        action: [source_frames(species, first_animation + direction) for direction in range(5)]
        for action, first_animation in ACTION_GROUPS
    }
    atlas = Image.new("RGBA", (21 * CELL, 5 * CELL))
    for direction in range(5):
        # The extracted sequences are grouped in five camera-facing directions.
        for group, start, count in ((0, 0, 2), (1, 2, 5), (2, 7, 5), (3, 12, 6), (4, 18, 2)):
            action = ("idle", "walk", "attack", "magic", "sleep")[group]
            files = action_sequence(action, direction, extracted)
            if not files:
                continue
            for column in range(count):
                pose = indexed(Image.open(files[min(len(files)-1, column * len(files) // count)]).convert("RGBA"),
                               lookup, palette_colors)
                atlas.alpha_composite(pose, (start * CELL + column * CELL + (CELL-pose.width)//2,
                                             direction * CELL + CELL-pose.height))
        # Reserve the final column for the damage reaction used by the tower renderer.
        hit_files = action_sequence("hit", direction, extracted)
        if hit_files:
            pose = indexed(Image.open(hit_files[0]).convert("RGBA"), lookup, palette_colors)
            atlas.alpha_composite(pose, (20 * CELL + (CELL-pose.width)//2,
                                         direction * CELL + CELL-pose.height))
    OUTPUT.mkdir(parents=True, exist_ok=True)
    atlas.save(OUTPUT / f"monster_{species:02x}.png", optimize=True)

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--start", type=int, default=1, help="first decimal monster ID")
    parser.add_argument("--end", type=int, default=45, help="last decimal monster ID")
    parser.add_argument("--skip-fallback", action="store_true", help="do not rebuild legacy Resources sheets")
    parser.add_argument("--manifest", type=Path, default=STREAMING_OUTPUT / "manifest.json")
    parser.add_argument("--combine-manifests", type=Path, nargs="*", default=None,
                        help="combine already-written partial manifests without repacking images")
    args = parser.parse_args()
    STREAMING_OUTPUT.mkdir(parents=True, exist_ok=True)
    if args.combine_manifests is not None:
        combined = {}
        for partial_path in args.combine_manifests:
            partial = json.loads(partial_path.read_text(encoding="utf-8"))
            for monster in partial.get("monsters", []): combined[monster["species"]] = monster
        output = {"schema": "azure-community-monster-actions-v2",
                  "monsters": [combined[species] for species in sorted(combined)]}
        args.manifest.parent.mkdir(parents=True, exist_ok=True)
        args.manifest.write_text(json.dumps(output, indent=2) + "\n", encoding="utf-8")
        print(f"Combined {len(output['monsters'])} community monster action manifests")
        raise SystemExit(0)
    manifest = {"schema": "azure-community-monster-actions-v2", "monsters": []}
    for species in range(max(1, args.start), min(45, args.end) + 1):
        if not args.skip_fallback: build(species)
        if species not in RETAIL_ACTION_ONLY:
            manifest["monsters"].append(build_streaming_actions(species))
    args.manifest.parent.mkdir(parents=True, exist_ok=True)
    args.manifest.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Packed {len(manifest['monsters'])} community monster action manifests")
