"""Fit town door leaves to the original capture and recover their artwork from retail footage."""
from collections import defaultdict
import json
from pathlib import Path
import re
import subprocess
import sys
import uuid

from PIL import Image

project = Path(__file__).resolve().parents[1]
assets = project / "Assets/AzureDreams"
output = assets / "Resources/Town/Retail/Doors"
output.mkdir(parents=True, exist_ok=True)

# Each quadrilateral is the interior of a closed leaf in community/town.mp4,
# ordered top-left, bottom-left, bottom-right, top-right.
artwork = {
    "Residence": ("384", (290, 28, 290, 105, 335, 103, 335, 28)),
    "MonsterShop": ("386", (518, 78, 513, 157, 564, 155, 565, 77)),
    "Blacksmith": ("663", (132, 116, 136, 197, 192, 201, 191, 114)),
    "Furniture": ("564", (492, 131, 487, 221, 542, 221, 550, 132)),
    "Fortune": ("506.4", (44, 96, 50, 187, 98, 187, 94, 93)),
}

def write_meta(path, template):
    meta = path.with_suffix(path.suffix + ".meta")
    if not meta.exists():
        meta.write_text(re.sub(r"(?m)^guid: \w+", f"guid: {uuid.uuid4().hex}", template), encoding="utf-8")

if len(sys.argv) > 1:
    ffmpeg = sys.argv[1]
    video = project.parent / "community/town.mp4"
    import io
    template = (assets / "Sprites/Characters/Koh/koh-retail-lift.png.meta").read_text()
    template = re.sub(r"(?m)^  spriteMode: \d+", "  spriteMode: 0", template)
    template = re.sub(r"(?m)^  textureType: \d+", "  textureType: 0", template)
    for name, (timestamp, corners) in artwork.items():
        frame = subprocess.check_output([
            ffmpeg, "-hide_banner", "-loglevel", "error", "-ss", timestamp,
            "-i", str(video), "-an", "-frames:v", "1", "-f", "image2pipe",
            "-vcodec", "png", "-"
        ])
        source = Image.open(io.BytesIO(frame)).convert("RGB")
        target = output / f"{name}.png"
        source.transform((32, 64), Image.Transform.QUAD, corners,
                         Image.Resampling.BILINEAR).save(target)
        write_meta(target, template)

obj = assets / "Models/Town/CapturedScenes/ad_model_town_min/Monsbaiya.obj"
vertices, doorway_faces = [], []
material = None
for line in obj.read_text().splitlines():
    if line.startswith("v "):
        x, y, z = map(float, line.split()[1:])
        vertices.append(tuple(round(v * 256) / 256 for v in (64 - 64*x, 64*y, 64 - 64*z)))
    elif line.startswith("usemtl "):
        material = line.split()[1]
    elif line.startswith("f ") and material == "page_12_clut_29443":
        face = [vertices[int(ref.split("/")[0]) - 1] for ref in line.split()[1:]]
        if max(vertex[1] for vertex in face) <= 2.5:
            doorway_faces.append(face)

by_vertex = defaultdict(list)
for index, face in enumerate(doorway_faces):
    for vertex in face:
        by_vertex[vertex].append(index)

# The captured doorway recess has 22 distinct vertices and a 1 x 1 floor footprint.
# The four shop façades below are identified by their signs in the retail video.
shop_styles = {
    (30, 68): "MonsterShop",
    (44, 68): "Blacksmith",
    (44, 80): "Furniture",
    (29, 80): "Fortune",
    (84, 68): "Furniture",
    (98, 80): "Furniture",
}
# Match the manual's numbered buildings to their captured doorway recesses.
# The home destination is resolved from the player's upgrade rank in Unity.
interiors = {
    (44, 68): "Interior_15_blacksmith",
    (30, 68): "Interior_14_monster_shop",
    (16, 68): "Interior_0f_hospital",
    (29, 80): "Interior_17_fortune_house",
    (44, 80): "Interior_16_general_store",
    (56, 56): "KohHome",
    (72, 38): "Interior_11_temple",
    (72, 56): "Interior_22_nico_house",
    (85, 51): "Interior_21_rhodes_house",
    (84, 68): "Interior_13_carpenter",
    (98, 80): "Interior_20_bar",
    (84, 80): "Interior_0e_restaurant",
}
seen, doors = set(), []
for index in range(len(doorway_faces)):
    if index in seen:
        continue
    queue, points = [index], set()
    seen.add(index)
    for face_index in queue:
        for vertex in doorway_faces[face_index]:
            points.add(vertex)
            for neighbor in by_vertex[vertex]:
                if neighbor not in seen:
                    seen.add(neighbor)
                    queue.append(neighbor)
    x, y, z = zip(*points)
    if len(points) != 22 or min(y) != 0 or max(y) != 1.75 or max(x)-min(x) != 1 or max(z)-min(z) != 1:
        continue
    center = (min(x)+max(x))/2, max(z)
    door = dict(x=center[0], z=center[1], style=shop_styles.get(center, "Residence"))
    if center in interiors:
        door["interior"] = interiors[center]
    doors.append(door)

assert len(doors) == 22
assert sum("interior" in door for door in doors) == len(interiors)
catalog = output / "placements.json"
catalog.write_text(json.dumps({"doors": doors}, indent=2) + "\n", encoding="utf-8")
write_meta(catalog, (assets / "Resources/Town/Retail/town_stone_walls.json.meta").read_text())
