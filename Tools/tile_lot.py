"""Build a town lot exterior from the original tile data (TOWN.BIN), the way the retail game does.

The retail town is a 128x128 grid of tile ids (copy_variant_building_tiles / apply_building_tile_entry).
Each tile id selects a face list of the type-0 town geometry, in tile-local coordinates (0..64 per
tile, negative z = up). A building record names the tile stamp applied to its lot when the town
loads (load_and_apply_town_building_tiles): e.g. Koh's house record 2 (level 2) -> stamp 28,
record 3 (level 3) -> stamp 29, both at lot slot 11 = tile (50, 44).

Output uses the construction-lot format read by TowerGameController.CreateConstructionMesh:
int sections; per section: string material, int vertexCount, vertexCount * (x, height, z, u, v);
then int samples, samples * (byte x, byte z, short height*256).
Coordinates are in tiles: x = map X, z = map Y, height = -z_local / 64.
"""
import json
import struct
from pathlib import Path

import numpy as np
from PIL import Image

PROJECT = Path(__file__).resolve().parents[1]
WORKSPACE = PROJECT.parent
RETAIL = PROJECT / "Assets/AzureDreams/Resources/Town/Retail"
GEOMETRY = WORKSPACE / "extracted_assets/reimagined_pipeline/TownGeometry/town_geometry_type_0"
TILE = 64.0


def signed(value, bits):
    return value - (1 << bits) if value >= 1 << (bits - 1) else value


class TownGeometry:
    def __init__(self, path=GEOMETRY):
        meta = json.loads(Path(str(path) + "_metadata.json").read_text())
        self.data = Path(str(path) + ".bytes").read_bytes()
        regions = meta["region_offsets"]
        self.face_table = regions["face_list_pointers"]
        count = meta["list_count"]
        self.starts = struct.unpack_from(f"<{count}I", self.data, self.face_table)
        base = regions["vertices"]
        self.vertices = []
        for n in range(meta["vertex_records"]):
            xy, z, _ = struct.unpack_from("<ihH", self.data, base + n * 8)
            self.vertices.append((signed(xy & 0xFFFF, 16), signed((xy >> 16) & 0xFFFF, 16), z))
        self.normals = []
        normal_base = regions["normals"]
        for n in range(meta["normal_record_capacity"]):
            if normal_base + n * 8 + 8 > len(self.data):
                break
            xy, z, _ = struct.unpack_from("<ihh", self.data, normal_base + n * 8)
            self.normals.append((signed(xy & 0xFFFF, 16), signed((xy >> 16) & 0xFFFF, 16), z))

    def faces(self, tile_id, camera=None):
        """Face records of one tile, walked exactly like render_shaded_map_tiles (shaded_tile_rendering_g00594).

        Each polygon is tested with the GTE winding (nclip) as seen from `camera` (a function mapping a
        local point to screen x, y). A drawn polygon with control low nibble 1 skips its alternates
        ((control >> 4) + 1 records); a rejected one either steps to the next record or jumps
        (control & 0xF) records, depending on its normal and flags. Control and flag are the two high
        bytes of the fourth word.
        """
        out = []
        if tile_id >= len(self.starts):
            return out
        camera = camera or DEFAULT_CAMERA
        pos = self.starts[tile_id]
        for _ in range(4096):
            if pos + 24 > self.face_table:
                break
            v = struct.unpack_from("<4H", self.data, pos)
            uv0, uv1, normal_word, attr = struct.unpack_from("<4I", self.data, pos + 8)
            control = (attr >> 16) & 0xFF
            flag = signed((attr >> 24) & 0xFF, 8)
            if not uv1:                                   # non-polygon record
                if control != 1 or flag >= 0:
                    pos += 24
                    continue
                break
            if any(i >= len(self.vertices) for i in v):
                break
            points = [self.vertices[i] for i in v]
            (x0, y0), (x1, y1), (x2, y2) = (camera(p) for p in points[:3])
            if (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0) >= 0:   # nclip: front facing, drawn
                uvs = [(uv0 & 255, (uv0 >> 8) & 255), (uv1 & 255, (uv1 >> 8) & 255),
                       ((normal_word >> 16) & 255, (normal_word >> 24) & 255), (attr & 255, (attr >> 8) & 255)]
                out.append((points, uvs, uv1 >> 16, uv0 >> 16))
                if (control & 0xF) == 1:
                    if flag >= 0:
                        pos += ((control >> 4) + 1) * 24
                        continue
                    break
                pos += 24
                continue
            normal = normal_word & 0xFFFF
            normal_z = self.normals[normal][2] if normal < len(self.normals) else 0
            if normal_z < 0:
                if control != 1 or flag >= 0:
                    pos += 24
                    continue
                break
            if (control & 0xF0) or flag >= 0:
                pos += max(1, control & 0xF) * 24
                continue
            break
        return out


def make_camera(yaw_degrees, pitch_degrees, mirror=False):
    """Orthographic stand-in for the town camera: returns local point -> screen (x, y)."""
    import math
    yaw, pitch = math.radians(yaw_degrees), math.radians(pitch_degrees)
    cy, sy, cp, sp = math.cos(yaw), math.sin(yaw), math.cos(pitch), math.sin(pitch)
    sign = -1.0 if mirror else 1.0

    def camera(point):
        x, y, z = point                     # map X, map Y, height (negative = up)
        rx = cy * x - sy * y
        ry = sy * x + cy * y
        return sign * rx, ry * sp + z * cp
    return camera


DEFAULT_CAMERA = make_camera(0.0, 45.0)


def variant_tile(value, x, y):
    """copy_variant_building_tiles: ground tiles are replaced by the variant matching map parity."""
    odd_y, odd_x = y & 1, x & 1
    groups = [
        ({0x135, 0x137, 0x154, 0x15E}, ((0x135, 0x154), (0x137, 0x15E))),
        ({0x136, 0x138, 0x153, 0x15D}, ((0x153, 0x136), (0x15D, 0x138))),
        ({0x143, 0x144, 0x15B, 0x15C}, ((0x15B, 0x15C), (0x143, 0x144))),
        ({0x13D, 0x13E, 0x13F, 0x140}, ((0x13D, 0x13E), (0x13F, 0x140))),
        ({0x149, 0x14A, 0x14B, 0x14C}, ((0x149, 0x14A), (0x14B, 0x14C))),
    ]
    for members, table in groups:
        if value in members:
            return table[odd_y][odd_x]
    if value in (0x141, 0x142):
        return 0x142 if odd_x else 0x141
    return value


def stamp(entry_id):
    meta = json.loads((RETAIL / "town_layout_metadata.json").read_text())
    bank = (RETAIL / "town_layout_bank.bytes").read_bytes()
    record = next(p for p in meta["tile_patch_records"] if p["entry_id"] == entry_id)
    offset = record["byte_offset"]
    width, height = struct.unpack_from("<HH", bank, offset)
    values = struct.unpack_from(f"<{width * height}H", bank, offset + 4)
    return width, height, values


def lot_triangles(geometry, entry_id, origin_x, origin_y, camera=None):
    """World-space triangles (material, [(x, h, z, u, v) * 3]) for one stamp placed at a tile origin."""
    width, height, values = stamp(entry_id)
    triangles = []
    for row in range(height):
        for col in range(width):
            raw = values[row * width + col]
            if not raw:
                continue
            x, y = origin_x + col, origin_y + row
            tile = variant_tile(raw & 0x3FFF, x, y)
            for points, uvs, page, clut in geometry.faces(tile, camera):
                corners = [(x + px / TILE, -pz / TILE, y + py / TILE, (u + 0.5) / 256.0, 1.0 - (v + 0.5) / 256.0)
                           for (px, py, pz), (u, v) in zip(points, uvs)]
                material = f"page_{page}_clut_{clut}"
                # PS1 quads are drawn as triangles (0, 1, 2) and (1, 3, 2).
                triangles.append((material, (corners[0], corners[1], corners[2])))
                triangles.append((material, (corners[1], corners[3], corners[2])))
    return triangles


def read_lot(path):
    """Triangles of an existing construction-lot file."""
    data = Path(path).read_bytes()
    pos = 0
    sections = struct.unpack_from("<i", data, pos)[0]
    pos += 4
    triangles = []
    for _ in range(sections):
        length = data[pos]
        material = data[pos + 1:pos + 1 + length].decode()
        pos += 1 + length
        count = struct.unpack_from("<i", data, pos)[0]
        pos += 4
        points = [struct.unpack_from("<5f", data, pos + i * 20) for i in range(count)]
        pos += count * 20
        triangles += [(material, tuple(points[i:i + 3])) for i in range(0, count, 3)]
    return triangles


def page_texture(vram, page, clut):
    """256x256 RGBA image of a texture page decoded with a CLUT (RGB555, colour 0 transparent)."""
    u = np.arange(256)[None, :]
    v = np.arange(256)[:, None]
    depth = (page >> 7) & 3
    x0 = (page & 15) * 64
    y0 = ((page >> 4) & 1) * 256
    if depth == 0:
        packed = vram[((y0 + v) & 511) * 1024 + ((x0 + u // 4) & 1023)]
        colors = vram[(((clut >> 6) & 511) * 1024 + (clut & 63) * 16 + ((packed >> ((u & 3) * 4)) & 15)) % vram.size]
    elif depth == 1:
        packed = vram[((y0 + v) & 511) * 1024 + ((x0 + u // 2) & 1023)]
        colors = vram[(((clut >> 6) & 511) * 1024 + (clut & 63) * 16 + ((packed >> ((u & 1) * 8)) & 255)) % vram.size]
    else:
        colors = vram[((y0 + v) & 511) * 1024 + ((x0 + u) & 1023)]
    r, g, b = colors & 31, (colors >> 5) & 31, (colors >> 10) & 31
    rgb = np.stack(((r << 3) | (r >> 2), (g << 3) | (g >> 2), (b << 3) | (b >> 2)), axis=-1).astype(np.uint8)
    alpha = np.where(colors == 0, 0, 255).astype(np.uint8)[..., None]
    return Image.fromarray(np.concatenate((rgb, alpha), axis=-1), "RGBA")
