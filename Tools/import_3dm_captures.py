"""Convert decoded Azure Dreams .3dm scene captures to portable glTF 2.0 GLB.

Noesis 4.474 can read the virtual_monsbaia OBJ output, but this installation
does not register a .3dm importer or export glTF. This bridge uses the existing
capture decoder and packages its geometry and original VRAM textures as GLB.
"""

import argparse
import json
import struct
import subprocess
import sys
from collections import defaultdict
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
CAPTURES = ROOT / "AzureDreams_3D_models"
CACHE = ROOT / "extracted_assets" / "azure_dreams_3d_models"
OUTPUT = ROOT / "extracted_assets" / "reimagined_pipeline" / "CapturedGLB"
DECODER = Path(__file__).with_name("extract_scene_captures.py")


def cached_obj(source):
    obj = CACHE / source.stem / "catalog.obj"
    if not obj.is_file() or not obj.with_suffix(".mtl").is_file():
        if source.parent.resolve() != CAPTURES.resolve():
            raise ValueError("Place the .3dm file in AzureDreams_3D_models before decoding it")
        subprocess.run([sys.executable, str(DECODER)], check=True)
    if not obj.is_file():
        raise FileNotFoundError(obj)
    return obj


def read_materials(path):
    materials = {}
    name = None
    for line in path.read_text(encoding="utf-8").splitlines():
        parts = line.split(maxsplit=1)
        if len(parts) != 2:
            continue
        if parts[0] == "newmtl":
            name = parts[1]
            materials[name] = None
        elif parts[0] == "map_Kd" and name:
            # The source MTL uses forward slashes on every platform.
            materials[name] = path.parent.joinpath(*parts[1].split("/"))
    return materials


def read_obj(path):
    vertices = []
    uvs = []
    faces = defaultdict(list)
    material = "default"
    for line in path.read_text(encoding="utf-8").splitlines():
        fields = line.split()
        if not fields:
            continue
        if fields[0] == "v" and len(fields) >= 4:
            vertices.append(tuple(float(value) for value in fields[1:4]))
        elif fields[0] == "vt" and len(fields) >= 3:
            uvs.append((float(fields[1]), 1.0 - float(fields[2])))
        elif fields[0] == "usemtl" and len(fields) >= 2:
            material = fields[1]
        elif fields[0] == "f" and len(fields) >= 4:
            corners = []
            for entry in fields[1:]:
                fields_at_corner = entry.split("/")
                v = int(fields_at_corner[0])
                t = int(fields_at_corner[1]) if len(fields_at_corner) > 1 and fields_at_corner[1] else 0
                vi = v - 1 if v > 0 else len(vertices) + v
                ti = t - 1 if t > 0 else len(uvs) + t if t < 0 else -1
                if vi < 0 or vi >= len(vertices) or ti >= len(uvs):
                    raise ValueError("OBJ face index outside vertex/UV table")
                corners.append((vi, ti))
            for index in range(1, len(corners) - 1):
                faces[material].extend((corners[0], corners[index], corners[index + 1]))
    return vertices, uvs, faces


def convert(source, output):
    if source.stat().st_size != 3 * 1024 * 1024:
        raise ValueError("Expected a 3 MiB RAM/VRAM .3dm capture: " + str(source))
    obj = cached_obj(source)
    vertices, uvs, groups = read_obj(obj)
    textures = read_materials(obj.with_suffix(".mtl"))
    document = {"asset": {"version": "2.0", "generator": "Azure Dreams 3DM importer"},
                "scenes": [{"nodes": [0]}], "scene": 0, "nodes": [{"mesh": 0, "name": source.stem}],
                "meshes": [{"name": source.stem, "primitives": []}], "buffers": [{"byteLength": 0}],
                "bufferViews": [], "accessors": [], "images": [], "textures": [], "materials": [],
                "extras": {"sourceCapture": source.name, "decoder": "virtual_monsbaia"}}
    binary = bytearray()

    def append_data(payload, target=None):
        while len(binary) % 4:
            binary.append(0)
        view = {"buffer": 0, "byteOffset": len(binary), "byteLength": len(payload)}
        if target is not None:
            view["target"] = target
        document["bufferViews"].append(view)
        binary.extend(payload)
        return len(document["bufferViews"]) - 1

    def accessor(values, components, fmt, component_type, type_name, target, bounds=False):
        flat = [value for row in values for value in row]
        payload = struct.pack("<" + fmt * len(flat), *flat)
        result = {"bufferView": append_data(payload, target), "componentType": component_type,
                  "count": len(values), "type": type_name}
        if bounds and values:
            result["min"] = [min(row[i] for row in values) for i in range(components)]
            result["max"] = [max(row[i] for row in values) for i in range(components)]
        document["accessors"].append(result)
        return len(document["accessors"]) - 1

    triangles = 0
    for name, corners in groups.items():
        if not corners:
            continue
        texture_path = textures.get(name)
        material = {"name": name, "doubleSided": True,
                    "alphaMode": "MASK", "alphaCutoff": 0.5,
                    "pbrMetallicRoughness": {"metallicFactor": 0.0,
                    "roughnessFactor": 1.0}}
        if texture_path and texture_path.is_file():
            image_view = append_data(texture_path.read_bytes())
            document["images"].append({"bufferView": image_view, "mimeType": "image/png", "name": texture_path.stem})
            document["textures"].append({"source": len(document["images"]) - 1})
            material["pbrMetallicRoughness"]["baseColorTexture"] = {"index": len(document["textures"]) - 1}
        material_index = len(document["materials"])
        document["materials"].append(material)
        unique = {}
        positions = []
        coordinates = []
        indices = []
        for corner in corners:
            if corner not in unique:
                unique[corner] = len(positions)
                positions.append(vertices[corner[0]])
                coordinates.append(uvs[corner[1]] if corner[1] >= 0 else (0.0, 0.0))
            indices.append((unique[corner],))
        primitive = {"attributes": {
            "POSITION": accessor(positions, 3, "f", 5126, "VEC3", 34962, True),
            "TEXCOORD_0": accessor(coordinates, 2, "f", 5126, "VEC2", 34962)},
            "indices": accessor(indices, 1, "I", 5125, "SCALAR", 34963), "material": material_index}
        document["meshes"][0]["primitives"].append(primitive)
        triangles += len(indices) // 3
    if not triangles:
        raise ValueError("No geometry in " + str(obj))
    document["buffers"][0]["byteLength"] = len(binary)
    encoded = json.dumps(document, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    encoded += b" " * (-len(encoded) % 4)
    binary.extend(b"\0" * (-len(binary) % 4))
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("wb") as dest:
        dest.write(struct.pack("<4sII", b"glTF", 2, 12 + 8 + len(encoded) + 8 + len(binary)))
        dest.write(struct.pack("<I4s", len(encoded), b"JSON"))
        dest.write(encoded)
        dest.write(struct.pack("<I4s", len(binary), b"BIN\0"))
        dest.write(binary)
    return {"capture": source.name, "output": str(output), "triangles": triangles,
            "materials": len(document["materials"]), "bytes": output.stat().st_size}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("captures", nargs="*", type=Path, help=".3dm captures; omit for all 44 supplied captures")
    parser.add_argument("--output-dir", type=Path, default=OUTPUT)
    args = parser.parse_args()
    sources = args.captures or sorted(CAPTURES.glob("*.3dm"))
    results = []
    for source in sources:
        result = convert(source.resolve(), args.output_dir / (source.stem + ".glb"))
        results.append(result)
        print("{}: {} triangles, {} textures, {:.1f} MiB".format(source.name, result["triangles"],
              result["materials"], result["bytes"] / 1048576), flush=True)
    if not results:
        raise SystemExit("No .3dm captures found")
    (args.output_dir / "import_manifest.json").write_text(json.dumps(results, indent=2) + "\n", encoding="utf-8")
    print("Converted {} captures".format(len(results)))


if __name__ == "__main__":
    main()
