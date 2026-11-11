"""Build enemy Troll atlases from the three extracted five-direction pose banks."""
from pathlib import Path
import re
import uuid

from PIL import Image

assets = Path(__file__).resolve().parents[1] / "Assets/AzureDreams"
source = assets / "Sprites/Monsters/Community"
output = assets / "Resources/Tower"
template = (source / "TrollActions.png.meta").read_text(encoding="utf-8")
sword_atlas = Image.open(source / "TrollActions.png").convert("RGBA")
frame_size = 64
sequence = (0, 1, 0, 1, 3, 3, 1, 0, 2, 4, 2, 1, 0, 2)

for weapon, bank in (("Sword", 0), ("Hammer", 5), ("Bow", 10)):
    atlas = Image.new("RGBA", sword_atlas.size)
    for row in range(5):
        number = bank + row
        frames = [
            Image.open(path).convert("RGBA")
            for path in sorted((source / f"monster_16/animation_{number:02}").glob("*.png"))
        ]
        if len(frames) != 5:
            raise ValueError(f"Expected five extracted Troll frames for animation {number:02}")
        for col, index in enumerate(sequence):
            frame = frames[index]
            atlas.paste(frame, (col * frame_size + (frame_size - frame.width) // 2,
                                row * frame_size + 60 - frame.height))
        if bank == 0:
            box = (14 * frame_size, row * frame_size, 16 * frame_size, (row + 1) * frame_size)
            atlas.paste(sword_atlas.crop(box), box[:2])
        else:
            # Use the same resting silhouette for this weapon's two breathing poses.
            lying = frames[0].rotate(78, resample=Image.Resampling.NEAREST, expand=True)
            lying = lying.crop(lying.getbbox())
            for col, lift in ((14, 0), (15, 2)):
                atlas.paste(lying, (col * frame_size + (frame_size - lying.width) // 2,
                                    row * frame_size + 60 - lying.height - lift))

    path = output / f"Troll{weapon}Actions.png"
    atlas.save(path)
    meta = path.with_suffix(".png.meta")
    if not meta.exists():
        meta.write_text(re.sub(r"(?m)^guid: \w+", f"guid: {uuid.uuid4().hex}", template), encoding="utf-8")
