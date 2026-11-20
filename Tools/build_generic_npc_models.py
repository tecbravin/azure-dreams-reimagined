"""Complete the existing town meshes using the recovered NPC clothing and face textures."""
from pathlib import Path
import re
import struct
import subprocess
import uuid
import zlib

PROJECT = Path(__file__).resolve().parents[1]
ASSETS = PROJECT / "Assets/AzureDreams"
MODEL_DIR = ASSETS / "Models/TownResidents"
TEXTURE_DIR = ASSETS / "Resources/Town/Residents/Models"


def metadata(path, template=None):
    target = Path(str(path) + ".meta")
    if target.exists():
        return
    guid = uuid.uuid4().hex
    if template:
        content = re.sub(r"(?m)^guid: .*$", "guid: " + guid, template.read_text())
        target.write_text(content)
    else:
        target.write_text(f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n")


def png_chunk(kind, data):
    return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))


def write_png(path, pixels, width=128, height=128):
    data = b"".join(b"\0" + bytes(pixels[row * width * 4:(row + 1) * width * 4]) for row in range(height))
    path.write_bytes(b"\x89PNG\r\n\x1a\n" +
        png_chunk(b"IHDR", struct.pack(">2I5B", width, height, 8, 6, 0, 0, 0)) +
        png_chunk(b"IDAT", zlib.compress(data)) + png_chunk(b"IEND", b""))


def connected_parts(vertices, faces):
    neighbors = {}
    for face in faces:
        points = {vertices[index] for index in face}
        for vertex in points:
            neighbors.setdefault(vertex, set()).update(points)
    groups = {}
    while neighbors:
        first = next(iter(neighbors))
        queue = [first]
        group = set()
        while queue:
            vertex = queue.pop()
            if vertex in group:
                continue
            group.add(vertex)
            queue.extend(neighbors.pop(vertex, ()))
        low = min(v[1] for v in group)
        high = max(v[1] for v in group)
        center_x = (min(v[0] for v in group) + max(v[0] for v in group)) / 2
        height = max(v[1] for v in vertices)
        part = "boots" if high < height * .13 else "pants" if high < height * .45 else \
            "head" if low > height * .68 else "skin" if low > height * .60 else \
            "arms" if abs(center_x) > height * .12 else "torso"
        for vertex in group:
            groups[vertex] = part
    return groups


def build(name, source, head_box, palette, width_scale=1.0):
    base = ASSETS / "Models/Hunter.obj"
    lines = base.read_text().splitlines()
    vertices = [tuple(map(float, row.split()[1:])) for row in lines if row.startswith("v ")]
    faces = [[int(ref.split("/")[0]) - 1 for ref in row.split()[1:]] for row in lines if row.startswith("f ")]
    parts = connected_parts(vertices, faces)
    ffmpeg = next((PROJECT.parent / "Tools/ffmpeg-portable").rglob("ffmpeg.exe"))
    source_bytes = source.read_bytes()
    source_width, source_height = struct.unpack(">II", source_bytes[16:24])
    raw = subprocess.check_output([str(ffmpeg), "-v", "error", "-i", str(source),
        "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgba", "-"])
    pixels = bytearray(palette["skin"] + (255,)) * (128 * 128)
    x0, y0, head_width, head_height = head_box
    for y in range(64):
        source_y = min(source_height - 1, y0 + y * head_height // 64)
        for x in range(64):
            source_x = min(source_width - 1, x0 + x * head_width // 64)
            offset = (source_y * source_width + source_x) * 4
            color = raw[offset:offset + 4]
            if color[3] < 128:
                color = bytes(palette["skin"] + (255,))
            destination = (y * 128 + x) * 4
            pixels[destination:destination + 4] = color
    regions = {}
    for i, (key, color) in enumerate(palette.items()):
        px, py = 80 + i % 3 * 16, i // 3 * 16
        regions[key] = ((px + 8) / 128, 1 - (py + 8) / 128)
        for y in range(py, py + 16):
            for x in range(px, px + 16):
                at = (y * 128 + x) * 4
                pixels[at:at + 4] = bytes(color + (255,))
    texture = TEXTURE_DIR / (name + ".png")
    write_png(texture, pixels)
    metadata(texture, ASSETS / "Resources/Town/Residents/Barten.png.meta")
    output = ["# Reconstructed complete town character, recovered PS1 face and clothing palette.", "o " + name]
    for x, y, z in vertices:
        part = parts[(x, y, z)]
        shape = width_scale * (1.24 if part == "head" else 1)
        output.append(f"v {x * shape:.7f} {y:.7f} {z:.7f}")
    uv_index = 0
    for face in faces:
        coords = [vertices[i] for i in face]
        part = parts[coords[0]]
        avg_x = sum(v[0] for v in coords) / len(coords)
        avg_y = sum(v[1] for v in coords) / len(coords)
        avg_z = sum(v[2] for v in coords) / len(coords)
        region = part
        if part == "torso":
            region = "shirt" if avg_z > 0 and abs(avg_x) < .075 else "vest"
        elif part == "head":
            region = "hair" if avg_z < 0 or avg_y > 1.07 else "skin"
        if region == "arms":
            region = "sleeves" if "sleeves" in palette else "skin"
        indices = []
        for vertex_index, (x, y, z) in zip(face, coords):
            if part == "head" and avg_z >= .01:
                u = min(.98, max(.02, (x + .116) / .232)) * .5
                v = 1 - min(.98, max(.02, (1.12 - y) / .336)) * .5
            else:
                u, v = regions[region]
            output.append(f"vt {u:.7f} {v:.7f}")
            uv_index += 1
            indices.append(f"{vertex_index + 1}/{uv_index}")
        output.append("f " + " ".join(indices))
    model = MODEL_DIR / (name + ".obj")
    model.write_text("\n".join(output) + "\n")
    metadata(model, ASSETS / "Models/Hunter.obj.meta")
    meta = Path(str(model) + ".meta")
    text = meta.read_text().replace("isReadable: 0", "isReadable: 1").replace("importAnimation: 1", "importAnimation: 0")
    meta.write_text(text)


def main():
    for directory in (MODEL_DIR, TEXTURE_DIR):
        directory.mkdir(exist_ok=True, parents=True)
        metadata(directory)
    chars = ASSETS / "Sprites/Characters"
    recovered = ASSETS / "Resources/Town/Residents"
    skin = (211, 160, 122)
    build("Bet", chars / "Town_Bet.png", (7, 1, 23, 24),
        dict(skin=skin, hair=(22, 34, 29), vest=(25, 43, 35), shirt=(197, 204, 201),
             pants=(83, 88, 48), boots=(53, 42, 32)), 1.16)
    build("Barten", recovered / "Barten.png", (7, 0, 23, 24),
        dict(skin=skin, hair=(67, 67, 75), vest=(16, 33, 27), shirt=(210, 213, 207),
             pants=(22, 38, 32), boots=(22, 23, 24), sleeves=(210, 213, 207)), .92)
    build("NicoDad", recovered / "NicoDad.png", (10, 0, 27, 25),
        dict(skin=skin, hair=(112, 58, 56), vest=(131, 65, 32), shirt=(208, 210, 197),
             pants=(55, 58, 79), boots=(78, 79, 76)), 1.12)
    build("RestaurantDar", chars / "Town_Dar.png", (1, 0, 27, 23),
        dict(skin=skin, hair=(78, 76, 64), vest=(127, 73, 28), shirt=(197, 196, 171),
             pants=(83, 77, 39), boots=(41, 37, 31)), .96)
    build("Tonka", chars / "Town_Tonka.png", (2, 0, 17, 16),
        dict(skin=skin, hair=(52, 47, 27), vest=(43, 60, 72), shirt=(177, 157, 104),
             pants=(51, 56, 62), boots=(60, 43, 29)), 1.10)
    build("Coppe", chars / "Town_Coppe.png", (4, 0, 25, 28),
        dict(skin=skin, hair=(185, 186, 180), vest=(66, 68, 77), shirt=(182, 181, 177),
             pants=(67, 65, 68), boots=(36, 35, 35)), 1.10)


if __name__ == "__main__":
    main()
