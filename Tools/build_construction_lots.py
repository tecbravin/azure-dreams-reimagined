"""Extract the constructed lots from the fully developed Monsbaiya capture."""

from collections import defaultdict
import math
from pathlib import Path
import shutil
import struct


PROJECT = Path(__file__).resolve().parents[1]
CAPTURE = PROJECT.parent / "extracted_assets/azure_dreams_3d_models/ad_model_town_max"
OUTPUT = PROJECT / "Assets/AzureDreams/Resources/Town/Retail/Construction"

# World-space lot boundaries shared with TowerGameController.Construction.cs.
LOTS = {
    "circuit": (7, 23, 25, 44),
    "theater": (26, 28, 38, 40),
    "alley": (42, 29, 54, 41),
    "temple": (64, 27, 84, 44),
    "library": (90, 30, 102, 41),
    "home_1": (48, 44, 63, 58),
    "home": (48, 44, 63, 58),
    "hospital": (8, 62, 23, 72),
    "fountain": (55, 64, 73, 80),
    "arcade": (104, 62, 119, 73),
    "gym": (25, 88, 38, 98),
    "casino": (91, 88, 102, 98),
}


def main():
    vertices, uv = [], []
    meshes = {name: defaultdict(list) for name in LOTS}
    with (CAPTURE / "catalog.obj").open() as obj:
        material = ""
        for line in obj:
            parts = line.split()
            if not parts:
                continue
            kind, *values = parts
            if kind == "v":
                x, y, z = map(float, values)
                vertices.append((64 - 64*x, 64*y, 64 - 64*z))
            elif kind == "vt":
                uv.append(tuple(map(float, values)))
            elif kind == "usemtl":
                material = values[0]
            elif kind == "f":
                refs = [tuple(int(i) - 1 for i in ref.split("/")[:2]) for ref in values]
                points = [vertices[i] for i, _ in refs]
                x = sum(p[0] for p in points) / len(points)
                z = sum(p[2] for p in points) / len(points)
                for name, (x0, z0, x1, z1) in LOTS.items():
                    if x0 <= x < x1 and z0 <= z < z1:
                        # No intermediate town capture was supplied. The
                        # developed capture has a main house and a later side
                        # wing. Keep its main house for rank 1, and remove
                        # the raised wing while preserving its ground.
                        if name == "home_1" and x >= 58 and z >= 50 and max(p[1] for p in points) > 0.8:
                            continue
                        meshes[name][material].extend((*vertices[i], *uv[j]) for i, j in refs)
                        if name != "home_1":
                            break

    # The removed side wing exposes ground that was hidden in the developed
    # capture. Continue the adjacent captured grass tile across that area.
    grass = meshes["home_1"]["page_13_clut_30145"]
    u0, v0, span = 0.126953, 0.751953, 0.121094
    for z in range(50, 57):
        for x in range(58, 62):
            corners = (
                (x, 0.01, z, u0, v0),
                (x + 1, 0.01, z, u0 + span, v0),
                (x, 0.01, z + 1, u0, v0 + span),
                (x + 1, 0.01, z + 1, u0 + span, v0 + span),
            )
            grass.extend((corners[0], corners[2], corners[1], corners[1], corners[2], corners[3]))

    textures = OUTPUT / "Textures"
    textures.mkdir(parents=True, exist_ok=True)
    for name, sections in meshes.items():
        if not sections:
            raise ValueError(f"No captured faces for {name}")
        with (OUTPUT / f"{name}.bytes").open("wb") as out:
            out.write(struct.pack("<i", len(sections)))
            for material, points in sorted(sections.items()):
                label = material.encode("ascii")
                if len(label) > 127 or len(points) % 3:
                    raise ValueError(f"Invalid geometry section {material}")
                out.write(bytes((len(label),)))
                out.write(label)
                out.write(struct.pack("<i", len(points)))
                for point in points:
                    if not all(map(math.isfinite, point)):
                        raise ValueError(f"Invalid vertex {point}")
                    out.write(struct.pack("<5f", *point))
                texture = textures / f"{material}.png"
                if not texture.exists():
                    shutil.copyfile(CAPTURE / "textures" / texture.name, texture)
            heights = walkable_samples(LOTS[name], sections)
            out.write(struct.pack("<i", len(heights)))
            for (x, z), height in sorted(heights.items()):
                out.write(struct.pack("<BBh", x, z, round(height * 256)))
        print(name, sum(len(points) // 3 for points in sections.values()), "triangles")


def walkable_samples(bounds, sections):
    x0, z0, x1, z1 = bounds
    heights = {}
    for vertices in sections.values():
        for i in range(0, len(vertices), 3):
            a, b, c = vertices[i:i + 3]
            if max(a[1], b[1], c[1]) > 0.65:
                continue
            bx, bz = b[0] - a[0], b[2] - a[2]
            cx, cz = c[0] - a[0], c[2] - a[2]
            area = bx * cz - bz * cx
            if abs(area) < 1e-6:
                continue
            xs = (a[0], b[0], c[0])
            zs = (a[2], b[2], c[2])
            for z in range(max(z0, math.ceil(min(zs))), min(z1 - 1, math.floor(max(zs))) + 1):
                for x in range(max(x0, math.ceil(min(xs))), min(x1 - 1, math.floor(max(xs))) + 1):
                    dx, dz = x - a[0], z - a[2]
                    w_b = (dx * cz - dz * cx) / area
                    w_c = (bx * dz - bz * dx) / area
                    if min(w_b, w_c, 1 - w_b - w_c) < -1e-5:
                        continue
                    height = a[1] + w_b * (b[1] - a[1]) + w_c * (c[1] - a[1])
                    if height > heights.get((x, z), -128):
                        heights[x, z] = height
    return heights


if __name__ == "__main__":
    main()
