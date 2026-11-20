"""Build Koh's house level-2 exterior (Construction/home_1.bytes) from the original tile data.

Run from anywhere:  python "Azure Dreams Reimagined/Tools/build_koh_house_lots.py"

Only two town captures exist (starting town and fully developed town), so the level-2 house was
previously improvised from the level-3 capture. The retail game builds it from tiles instead:
building record 2 (Koh's house level 2) applies tile stamp 28 to lot slot 11 at tile (50, 44)
(load_and_apply_town_building_tiles -> apply_building_tile_entry). This script rebuilds that lot
with tile_lot.py, which walks each tile's face list like render_shaded_map_tiles.

Validation: front-camera extraction of stamp 29 (level 3) reproduced the level-3 capture with
98.9% of triangles exactly on captured ones (median 0.000 tiles), identical UVs and textures.
The full-view union additionally preserves faces hidden in that frontal capture. The faces
the game draws depend on its backface test. Combine views around the entire house so rotating
the town camera also retains the rear and side walls, rather than baking only the front view.
"""
import shutil
import struct
import sys
import uuid
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from build_construction_lots import walkable_samples  # noqa: E402
from tile_lot import RETAIL, WORKSPACE, TownGeometry, lot_triangles, make_camera, page_texture  # noqa: E402

CONSTRUCTION = RETAIL / "Construction"
TEXTURES = CONSTRUCTION / "Textures"
VRAM = WORKSPACE / "extracted_assets/azure_dreams_3d_models/ad_model_town_max/vram.bin"
TEMPLATE_META = TEXTURES / "page_13_clut_30145.png.meta"

LOT_ORIGIN = (50, 44)            # lot slot 11
REPLACED = (50, 44, 62, 58)      # BuildingProject.ReplaceBounds: base town mesh removed here
LOTS = {"home_1": 28}            # level 2 = record 2 -> stamp 28 (level 3 keeps its exact capture)
CAMERA_YAWS = sorted(set(range(-180, 180, 15)) | {-20, -10, 10, 20})
CAMERAS = [make_camera(yaw, pitch) for yaw in CAMERA_YAWS for pitch in range(15, 90, 5)]


def build(geometry, stamp_id):
    """Union of faces visible from the camera spread, limited to cells the lot replaces.
    Cells outside REPLACED (row 58 path stubs) are identical in the base town and are skipped."""
    x0, z0, x1, z1 = REPLACED
    unique = {}
    for camera in CAMERAS:
        for material, triangle in lot_triangles(geometry, stamp_id, *LOT_ORIGIN, camera):
            unique.setdefault((material, tuple(round(c, 4) for p in triangle for c in p[:3])), (material, triangle))
    kept = []
    for material, triangle in unique.values():
        cx = min(p[0] for p in triangle)
        cz = min(p[2] for p in triangle)
        if z1 <= cz and min(p[1] for p in triangle) < 0.05 and max(p[1] for p in triangle) < 0.05:
            continue                     # flat ground of a cell beyond the replaced area
        kept.append((material, triangle))
    return kept


def write_lot(path, triangles, bounds):
    sections = {}
    for material, triangle in triangles:
        sections.setdefault(material, []).extend(triangle)
    with path.open("wb") as out:
        out.write(struct.pack("<i", len(sections)))
        for material, points in sorted(sections.items()):
            label = material.encode("ascii")
            out.write(bytes((len(label),)))
            out.write(label)
            out.write(struct.pack("<i", len(points)))
            for point in points:
                out.write(struct.pack("<5f", *point))
        heights = walkable_samples(bounds, sections)
        out.write(struct.pack("<i", len(heights)))
        for (x, z), height in sorted(heights.items()):
            out.write(struct.pack("<BBh", x, z, round(height * 256)))
    return len(sections), len(heights)


def ensure_textures(materials):
    vram = np.fromfile(VRAM, dtype="<u2")
    template = TEMPLATE_META.read_text()
    created = []
    for material in sorted(materials):
        png = TEXTURES / f"{material}.png"
        if png.exists():
            continue
        _, page, _, clut = material.split("_")
        page_texture(vram, int(page), int(clut)).save(png)
        meta = "\n".join(f"guid: {uuid.uuid4().hex}" if line.startswith("guid:") else line
                         for line in template.splitlines()) + "\n"
        (TEXTURES / f"{material}.png.meta").write_text(meta)
        created.append(material)
    return created


def main():
    geometry = TownGeometry()
    for lot, stamp_id in LOTS.items():
        triangles = build(geometry, stamp_id)
        target = CONSTRUCTION / f"{lot}.bytes"
        backup = WORKSPACE / "Old Files" / f"Construction {lot} (improvised before tile rebuild).bytes"
        if target.exists() and not backup.exists():
            backup.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(target, backup)
        created = ensure_textures({material for material, _ in triangles})
        sections, samples = write_lot(target, triangles, (48, 44, 63, 58))
        print(f"{lot}: stamp {stamp_id}, {len(triangles)} triangles, {sections} materials, "
              f"{samples} ground samples, new textures {created}")


if __name__ == "__main__":
    main()
