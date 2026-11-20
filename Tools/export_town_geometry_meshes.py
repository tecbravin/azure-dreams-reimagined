"""Convert verified raw Town geometry payloads into source-indexed OBJ meshes.

Uses the 24-byte polygon records and skip/stop conditions from the original
town shaded tile renderer. Exports geometry without claiming actor identity,
UV reconstruction, or PS1 lighting.
"""
import json
from pathlib import Path
import struct
import numpy as np
from PIL import Image

PROJECT = Path(__file__).resolve().parents[1]
SOURCE = PROJECT.parent / "extracted_assets/reimagined_pipeline/TownGeometry"
OUTPUT = PROJECT.parent / "extracted_assets/reimagined_pipeline/TownGeometry/meshes"
CAPTURE_VRAM = PROJECT.parent / "extracted_assets/azure_dreams_3d_models/ad_model_building_01_koh_house_1/vram.bin"
MAX_FACES_PER_LIST = 20000
VRAM_WORDS = 1024 * 512
_vram = None
_textures = {}


def texture_for(page, clut):
    key = (page, clut)
    if key in _textures:
        return _textures[key]
    words = _vram
    u = np.arange(256, dtype=np.uint32)[None, :]
    v = np.arange(256, dtype=np.uint32)[:, None]
    depth = (page >> 7) & 3
    if depth == 3:
        raise ValueError(f"Reserved PS1 texture depth on page {page:#x}")
    x = (page & 15) * 64
    y = ((page >> 4) & 1) * 256
    if depth == 0:
        packed = words[((y + v) & 511) * 1024 + ((x + u // 4) & 1023)]
        indices = (packed >> ((u & 3) * 4)) & 15
        palette = ((clut >> 6) * 1024 + (clut & 63) * 16 + indices) % VRAM_WORDS
        colors = words[palette]
    elif depth == 1:
        packed = words[((y + v) & 511) * 1024 + ((x + u // 2) & 1023)]
        indices = (packed >> ((u & 1) * 8)) & 255
        palette = ((clut >> 6) * 1024 + (clut & 63) * 16 + indices) % VRAM_WORDS
        colors = words[palette]
    else:
        colors = words[((y + v) & 511) * 1024 + ((x + u) & 1023)]
    r = colors & 31; g = (colors >> 5) & 31; b = (colors >> 10) & 31
    rgb = np.stack(((r << 3) | (r >> 2), (g << 3) | (g >> 2),
                    (b << 3) | (b >> 2)), axis=-1).astype(np.uint8)
    alpha = np.where(colors == 0, 0, 255).astype(np.uint8)[..., None]
    rgba = np.concatenate((rgb, alpha), axis=-1)
    stp = np.where((colors & 0x8000) != 0, 255, 0).astype(np.uint8)
    name = f"page_{page:04x}_clut_{clut:04x}"
    texture_dir = OUTPUT / "textures"
    texture_dir.mkdir(parents=True, exist_ok=True)
    Image.fromarray(rgba, "RGBA").save(texture_dir / f"{name}.png", optimize=True)
    Image.fromarray(stp, "L").save(texture_dir / f"{name}_stp.png", optimize=True)
    _textures[key] = name
    return name


def signed_byte(value):
    return value - 256 if value >= 128 else value


def export_one(index):
    stem = f"town_geometry_type_{index}"
    meta = json.loads((SOURCE / f"{stem}_metadata.json").read_text(encoding="utf-8"))
    data = (SOURCE / f"{stem}.bytes").read_bytes()
    regions = meta["region_offsets"]
    face_table = regions["face_list_pointers"]
    vertex_start = regions["vertices"]
    normal_start = regions["normals"]
    vertex_count = meta["vertex_records"]
    normal_count = meta["normal_record_capacity"]
    if not (0 <= face_table < vertex_start <= normal_start <= len(data)):
        raise ValueError(f"Type {index}: invalid section bounds")
    list_count = meta["list_count"]
    starts = struct.unpack_from("<" + "I" * list_count, data, face_table)
    if vertex_start - face_table != list_count * 4:
        raise ValueError(f"Type {index}: face list extent mismatch")

    vertices = []
    for n in range(vertex_count):
        xy, z, _pad = struct.unpack_from("<ihH", data, vertex_start + n * 8)
        x = xy & 0xffff
        y = (xy >> 16) & 0xffff
        if x >= 0x8000: x -= 0x10000
        if y >= 0x8000: y -= 0x10000
        vertices.append((x, y, z))
    normals = []
    for n in range(normal_count):
        xy, z, _pad = struct.unpack_from("<ihh", data, normal_start + n * 8)
        x = xy & 0xffff
        y = (xy >> 16) & 0xffff
        if x >= 0x8000: x -= 0x10000
        if y >= 0x8000: y -= 0x10000
        normals.append((x, y, z))

    rendered = []
    invalid = []
    visited = set()
    for list_id, start in enumerate(starts):
        if start >= face_table or start % 4:
            invalid.append({"list": list_id, "offset": start, "reason": "start outside face region"})
            continue
        pos = start
        for _ in range(MAX_FACES_PER_LIST):
            if pos + 24 > face_table:
                break
            v = struct.unpack_from("<4H", data, pos)
            uv0, uv1, normal_word, attr = struct.unpack_from("<4I", data, pos + 8)
            normal = normal_word & 0xffff
            nonzero = uv1 != 0
            if nonzero:
                if any(i >= vertex_count for i in v) or normal >= normal_count:
                    invalid.append({"list": list_id, "offset": pos, "vertices": list(v),
                                    "normal": normal, "reason": "index outside vertex/normal tables"})
                    break
                rendered.append((list_id, pos, v, normal, normal_word, uv0, uv1, attr))
                visited.add(pos)
            # The skip nibble and signed direction/termination byte live at
            # polygon+0x16 and +0x17, i.e. the upper halfword of unk_14.
            control = (attr >> 16) & 0xffff
            flag = signed_byte((attr >> 24) & 0xff)
            if not nonzero:
                if (control & 0xff) != 1 or flag >= 0:
                    pos += 24
                    continue
                break
            if (control & 0x0f) == 1:
                if flag >= 0:
                    pos += ((control >> 4) + 1) * 24
                    continue
                break
            if (control & 0xf0) or flag >= 0:
                advance = (control & 0x0f) * 24
                if advance == 0:
                    invalid.append({"list": list_id, "offset": pos, "reason": "zero face advance"})
                    break
                pos += advance
                continue
            break
        else:
            invalid.append({"list": list_id, "offset": pos, "reason": "list walk safety limit"})

    OUTPUT.mkdir(parents=True, exist_ok=True)
    obj = OUTPUT / f"town_geometry_type_{index:02d}.obj"
    lines = [f"# Retail Azure Dreams Town geometry type {index}",
             "# Face records traversed with original town renderer stop/skip rules.",
             "# RGB555 textures use the full 1 MiB retail Town VRAM capture stated in the MTL.",
             f"mtllib {obj.stem}.mtl"]
    lines.extend(f"v {x} {y} {z}" for x, y, z in vertices)
    lines.extend(f"vn {x} {y} {z}" for x, y, z in normals)
    material_names = set()
    for _list_id, _pos, _face, _normal, _normal_word, uv0, uv1, _attr in rendered:
        material_names.add(texture_for(uv1 >> 16, uv0 >> 16))
    mtl_lines = ["# Textures decoded from the full Town VRAM snapshot."]
    for name in sorted(material_names):
        mtl_lines.extend([f"newmtl {name}", "Ka 1 1 1", "Kd 1 1 1", "d 1",
                          f"map_Kd textures/{name}.png", ""])
    (OUTPUT / f"{obj.stem}.mtl").write_text("\n".join(mtl_lines), encoding="utf-8")
    uv_index = 1
    last_list = None
    for list_id, _pos, face, normal, normal_word, uv0, uv1, attr in rendered:
        if list_id != last_list:
            lines.append(f"g face_list_{list_id:03d}")
            last_list = list_id
        name = texture_for(uv1 >> 16, uv0 >> 16)
        lines.append(f"usemtl {name}")
        uv_pairs = [(uv0 & 255, (uv0 >> 8) & 255), (uv1 & 255, (uv1 >> 8) & 255),
                    ((normal_word >> 16) & 255, (normal_word >> 24) & 255)]
        # UV2 is stored in the low bytes of the normal/UV word; UV3 in control.
        uv_pairs.append((attr & 255, (attr >> 8) & 255))
        for u, v in uv_pairs:
            lines.append(f"vt {(u + 0.5) / 256:.8f} {1.0 - (v + 0.5) / 256:.8f}")
        # The retail renderer emits the first three vertices through GTE and
        # the fourth via the quad packet. OBJ preserves the full quad.
        ids = " ".join(f"{v + 1}/{uv_index + corner}/{normal + 1}" for corner, v in enumerate(face))
        lines.append(f"f {ids}")
    obj.write_text("\n".join(lines) + "\n", encoding="utf-8")
    report = {"type_index": index, "source_metadata": f"../{stem}_metadata.json",
              "obj": obj.name, "vertex_count": len(vertices), "normal_count": len(normals),
              "rendered_face_records": len(rendered), "unique_face_records": len(visited),
              "invalid_records": invalid[:100], "invalid_count": len(invalid),
              "face_layout": "24-byte records: four u16 vertex indices, three u32 render fields, u32 normal/color/skip flags",
              "traversal": "town shaded_tile_rendering_g00594 stop/skip control flow",
              "materials": sorted(material_names), "texture_source": str(CAPTURE_VRAM),
              "limitations": ["model identity is not inferred", "selected capture is Koh house level 1",
                              "PS1 lighting, culling, and runtime transforms are not reproduced"]}
    (OUTPUT / f"town_geometry_type_{index:02d}.json").write_text(
        json.dumps(report, indent=2) + "\n", encoding="utf-8")
    return report


def main():
    global _vram
    raw_vram = CAPTURE_VRAM.read_bytes()
    if len(raw_vram) != VRAM_WORDS * 2:
        raise ValueError("Expected a full 1 MiB Town VRAM capture")
    _vram = np.frombuffer(raw_vram, dtype="<u2")
    reports = [export_one(i) for i in range(41)]
    summary = {"geometry_types": len(reports), "total_vertices": sum(r["vertex_count"] for r in reports),
               "total_rendered_faces": sum(r["rendered_face_records"] for r in reports),
               "types_with_faces": sum(r["rendered_face_records"] > 0 for r in reports),
               "types_with_invalid_records": sum(r["invalid_count"] > 0 for r in reports),
               "unique_page_clut_textures": len(_textures),
               "models": reports}
    (OUTPUT / "manifest.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({k: v for k, v in summary.items() if k != "models"}, indent=2))


if __name__ == "__main__":
    main()
