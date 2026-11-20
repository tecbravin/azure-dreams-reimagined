"""Extract the retail tower floor data used by RetailTowerFloor (C#) from DUNGEON.BIN and SLUS_006.14.

Outputs (Assets/AzureDreams/Resources/Tower/Retail/):
  geometry_29/2a/2b.bytes  floor tilesets (resource types 0x29 random floors, 0x2A Beldo arena, 0x2B floors 1-2).
                           'ADG1', u16 lists, u16 0, u32 face_table, vertices, normals, aux, payload_length, payload.
                           Same CellFace/MapVertex/MapNormal format as render_map_cell_mesh.c.
  layout_NN.bytes          fixed floors (load_dungeon_layout): 'ADL1', rooms, markers, items, objects, 64x64x6 cells.
  floor_variants.bytes     byte 0x40 of each floor's spawn table (sector 0x3F5E + floor): the texture variant.
  tiles_<set>.png/.bytes   texture atlas per texture set: one 256x256 cell per (tpage, clut) the tileset uses.
                           .bytes: 'ADT1', u16 count, count * (u16 tpage, u16 clut, u16 flags).
  backdrop_NN.png          128x128 backdrop texture of package NN (create_default_state_object / D_800DEF90).

Sources: generate.c (setup_run_and_load_track, load_dungeon_layout, load_floor_spawn_tables),
engine/graphics/run_image_load_commands.c, dungeon/actor/depth_strip_projection_g00337.c.
"""
import importlib.util
import json
import struct
from pathlib import Path

import numpy as np
from PIL import Image

PROJECT = Path(__file__).resolve().parents[1]
WORKSPACE = PROJECT.parent
OUT = PROJECT / "Assets/AzureDreams/Resources/Tower/Retail"
DUNGEON = (WORKSPACE / "DUNGEON/DUNGEON.BIN").read_bytes()
EXE = (WORKSPACE / "SLUS_006.14").read_bytes()
DUNGEON_LBA = 12310
spec = importlib.util.spec_from_file_location("vlz", WORKSPACE / "extracted_assets/verified_decode/verify_container_reports.py")
lz = importlib.util.module_from_spec(spec)
spec.loader.exec_module(lz)


def exe(va, n):
    o = va - 0x8002D000 + 0x800
    return EXE[o:o + n]


def overlay(va, n):
    """Dungeon overlay data resident at 0x80016000 (DUNGEON.BIN 0x2A3000)."""
    o = va - 0x80016000 + 0x2A3000
    return DUNGEON[o:o + n]


def resident(va, n):
    """Dungeon resident image at 0x80088760 (DUNGEON.BIN 0xA3000)."""
    o = va - 0x80088760 + 0xA3000
    return DUNGEON[o:o + n]


def read_desc(va):
    packed, lba = struct.unpack("<II", exe(va, 8))
    sectors = packed >> 23
    o = (lba - DUNGEON_LBA) * 2048
    return DUNGEON[o:o + sectors * 2048]


def read_lba(lba, sectors):
    o = (lba - DUNGEON_LBA) * 2048
    return DUNGEON[o:o + sectors * 2048]


# ---------------------------------------------------------------- geometry
def geometry(type_id):
    record = struct.unpack("<I", exe(0x8006E7F0 + type_id * 4, 4))[0]
    desc, chunks, count, off8, offA, offC, offE = struct.unpack("<IHH4H", exe(record, 16))
    packed, lba = struct.unpack("<II", exe(desc, 8))
    payload = bytearray()
    raw = read_lba(lba, 64)
    pos = 0
    for _ in range(chunks):
        data, used = lz.decode_lz(raw[pos:])
        payload += data
        pos += used
    header = struct.pack("<4sHHIIIII", b"ADG1", count, 0, off8 * 4, offA * 4, offC * 4, offE * 4, len(payload))
    return bytes(payload), header + bytes(payload), dict(count=count, faces=off8 * 4, vertices=offA * 4, normals=offC * 4, aux=offE * 4)


def face_records(payload, info):
    """Every polygon record the renderer can reach for each list (both visibility branches)."""
    starts = struct.unpack_from(f"<{info['count']}I", payload, info["faces"])
    normals_count = (info["aux"] - info["normals"]) // 8
    normal_z = [struct.unpack_from("<h", payload, info["normals"] + i * 8 + 4)[0] for i in range(normals_count)]
    out = {}
    for tile, start in enumerate(starts):
        if tile == 0:
            continue
        seen, stack, faces = set(), [start], []
        while stack:
            pos = stack.pop()
            if pos in seen or pos + 24 > info["faces"]:
                continue
            seen.add(pos)
            v = struct.unpack_from("<4H", payload, pos)
            uv0, uv1, attr, inf = struct.unpack_from("<4I", payload, pos + 8)
            control = (inf >> 16) & 0xFF
            flag = (inf >> 24) & 0xFF
            flag_s = flag - 256 if flag >= 128 else flag
            normal = attr & 0xFFFF
            faces.append(pos)
            # drawn branch
            if (control & 0xF) == 1:
                if flag_s >= 0:
                    stack.append(pos + ((control >> 4) + 1) * 24)
            else:
                stack.append(pos + 24)
            # culled branch
            nz = normal_z[normal] if normal < normals_count else 0
            if nz < 0:
                if ((inf >> 16) & 0x80FF) != 0x8001:
                    stack.append(pos + 24)
            elif (control & 0xF0) or flag_s >= 0:
                stack.append(pos + max(1, control & 0xF) * 24)
        out[tile] = sorted(faces)
    return out


# ---------------------------------------------------------------- VRAM
class Vram:
    def __init__(self):
        self.v = np.zeros((512, 1024), np.uint16)

    def upload(self, x, y, w, h, data):
        data = bytes(data) + bytes(max(0, w * h * 2 - len(data)))
        self.v[y:y + h, x:x + w] = np.frombuffer(data[:w * h * 2], np.uint16).reshape(h, w)

    def load(self, raw):
        pos = 0
        while pos + 16 <= len(raw):
            cmd, size, off, x, y, w, h = struct.unpack_from("<HHI4h", raw, pos)
            if cmd < 1 or cmd > 9:
                break
            data = raw[off:]
            flags = w
            if cmd in (1, 5):
                if cmd == 1:
                    data, _ = lz.decode_lz(data)
                self.upload(x, y, w, h, data)
            elif cmd in (2, 6, 7, 8):
                if cmd in (7, 8):
                    data = bytearray(data[:y * 32])
                    for i in range(1, y * 16):
                        if cmd == 7 or i % 16:
                            c = struct.unpack_from("<H", data, i * 2)[0]
                            struct.pack_into("<H", data, i * 2, c | 0x8000)
                if cmd in (6, 8):
                    flags |= 2
                if cmd == 7:
                    flags |= 4
                if not flags & 1:
                    slot = (x - 0x7000) >> 6
                    self.upload(((slot & 0x40) << 1) + ((x & 63) << 4), (slot & 63) + 0x1C0, y * 16, 1, data)
            if size == 0:
                break
            pos += size

    def page(self, tpage, clut, scroll_rows=None):
        """256x256 RGBA of a texture page, decoded with a CLUT; colour 0 transparent (PSX rule)."""
        depth = (tpage >> 7) & 3
        x0 = (tpage & 15) * 64
        y0 = ((tpage >> 4) & 1) * 256
        cx, cy = (clut & 63) * 16, (clut >> 6) & 511
        u = np.arange(256)[None, :]
        rows = (y0 + np.arange(256)[:, None]) & 511
        if depth == 0:
            packed = self.v[rows, (x0 + u // 4) & 1023]
            idx = (packed >> ((u & 3) * 4)) & 15
            colors = self.v[cy, (cx + idx) & 1023]
        elif depth == 1:
            packed = self.v[rows, (x0 + u // 2) & 1023]
            idx = (packed >> ((u & 1) * 8)) & 255
            colors = self.v[cy, (cx + idx) & 1023]
        else:
            colors = self.v[rows, (x0 + u) & 1023]
        r, g, b = colors & 31, (colors >> 5) & 31, (colors >> 10) & 31
        rgb = np.stack(((r << 3) | (r >> 2), (g << 3) | (g >> 2), (b << 3) | (b >> 2)), -1).astype(np.uint8)
        alpha = np.where(colors == 0, 0, 255).astype(np.uint8)[..., None]
        return np.concatenate((rgb, alpha), -1)


SHARED = [0x80080AE0, 0x80080AE8, 0x80080E18, 0x80080E20, 0x80080EE8, 0x80080E30]


def texture_set(desc_va):
    vram = Vram()
    for va in SHARED:
        vram.load(read_desc(va))
    vram.load(read_desc(desc_va))
    return vram


def atlas(vram, combos, path):
    columns = 4
    rows = (len(combos) + columns - 1) // columns
    image = np.zeros((rows * 256, columns * 256, 4), np.uint8)
    for i, (tpage, clut) in enumerate(combos):
        r, c = divmod(i, columns)
        image[r * 256:(r + 1) * 256, c * 256:(c + 1) * 256] = vram.page(tpage, clut)
    Image.fromarray(image, "RGBA").save(path.with_suffix(".png"))
    path.with_suffix(".bytes").write_bytes(struct.pack("<4sH", b"ADT1", len(combos)) +
                                          b"".join(struct.pack("<HH", t, c) for t, c in combos))


def water_cluts(payload, info):
    """CLUTs of faces sampling the band update_wrapped_texture_scroll animates (page 0x99, u 96-127, v 0-63)."""
    found = set()
    for faces in face_records(payload, info).values():
        for pos in faces:
            uv0, uv1, attr, inf = struct.unpack_from("<4I", payload, pos + 8)
            if uv0 >> 31 or not uv1 or ((uv1 >> 16) & 0x1F) != 0x19:
                continue
            us = [uv0 & 255, uv1 & 255, (attr >> 16) & 255, inf & 255]
            vs = [(uv0 >> 8) & 255, (uv1 >> 8) & 255, (attr >> 24) & 255, (inf >> 8) & 255]
            if min(us) >= 96 and max(vs) < 64:
                found.add((uv1 >> 16, (uv0 >> 16) & 0x7FFF))
    return sorted(found)


def water(vram, cluts, set_name):
    for tpage, clut in cluts:
        band = vram.page(tpage, clut)[64:96, 96:128]
        Image.fromarray(band, "RGBA").save(OUT / f"water_{set_name}_{clut:04x}.png")


def combos_of(payload, info):
    found = set()
    for faces in face_records(payload, info).values():
        for pos in faces:
            uv0, uv1 = struct.unpack_from("<II", payload, pos + 8)
            if uv0 >> 31 or not uv1:
                continue
            found.add((uv1 >> 16, uv0 >> 16))
    return sorted(found)


# ---------------------------------------------------------------- layouts
def layout(number):
    desc = struct.unpack("<I", overlay(0x8001F604 + (number - 1) * 4, 4))[0]
    if desc < 0x80030000:
        packed, lba = struct.unpack("<II", overlay(desc, 8))
    else:
        packed, lba = struct.unpack("<II", exe(desc, 8))
    data = read_lba(lba, max(1, packed >> 23) + 16)
    shift_x, shift_y = struct.unpack_from("<HH", data, 0)
    pos = 4
    rooms = []
    while struct.unpack_from("<H", data, pos + 4)[0] != 0:
        x, y, w, h = struct.unpack_from("<4H", data, pos)
        pos += 8
        cells = []
        while data[pos] != 0:
            cells.append((data[pos], data[pos + 1], data[pos + 2]))
            pos += 4
        pos += 2
        rooms.append((x, y, w, h, cells))
    pos += 8
    markers = []
    while data[pos] != 0:
        markers.append((data[pos + 2] + 2, data[pos], data[pos + 1]))
        pos += 4
    pos += 2
    items = []
    while data[pos] != 0:
        if data[pos] == 99:
            pos += 6
            break
        items.append(tuple(data[pos:pos + 6]))
        pos += 6
    pos += 2
    objects = []
    while data[pos] != 0:
        if data[pos] == 99:
            pos += 6
            break
        objects.append(tuple(data[pos:pos + 6]))
        pos += 6
    pos += 2
    extra = []
    while data[pos] != 0:
        extra.append(tuple(data[pos:pos + 6]))
        pos += 6
    pos += 2
    cells, _ = lz.decode_lz(data[pos:])
    if len(cells) < 64 * 64 * 6:
        cells += bytes(64 * 64 * 6 - len(cells))
    out = bytearray(b"ADL1")
    out += struct.pack("<HHB", shift_x, shift_y, len(rooms))
    for x, y, w, h, room_cells in rooms:
        out += struct.pack("<4H", x, y, w, h)
    out += struct.pack("<B", len(markers))
    for kind, x, y in markers:
        out += struct.pack("<3B", kind, x, y)
    out += struct.pack("<B", len(items)) + b"".join(bytes(i) for i in items)
    out += struct.pack("<B", len(objects)) + b"".join(bytes(o) for o in objects)
    out += struct.pack("<B", len(extra)) + b"".join(bytes(o) for o in extra)
    out += cells[:64 * 64 * 6]
    return bytes(out), dict(rooms=[r[:4] for r in rooms], markers=markers, items=items, objects=objects, extra=extra)


# ---------------------------------------------------------------- backdrops
def backdrop(package_index):
    va = struct.unpack("<I", exe(0x8006CDC8 + package_index * 4, 4))[0]
    vram = Vram()
    for shared in SHARED:
        vram.load(read_desc(shared))
    vram.load(read_desc(va))
    entry = resident(0x800DEF90, 12)
    tpage, clut = struct.unpack_from("<HH", entry, 4)
    page = vram.page(tpage, clut)
    Image.fromarray(page[0:128, 0:128], "RGBA").save(OUT / f"backdrop_{package_index:02d}.png")


def write_metas():
    """Unity import settings: point filtered, uncompressed, no mipmaps (cloned from Tower/ElevatorSheet.png.meta)."""
    import uuid
    template = (OUT.parent / "ElevatorSheet.png.meta").read_text()
    template = template.replace("enableMipMap: 1", "enableMipMap: 0")
    for path in sorted(OUT.iterdir()):
        meta = path.with_name(path.name + ".meta")
        if meta.exists() or path.suffix == ".meta":
            continue
        guid = uuid.uuid5(uuid.NAMESPACE_URL, "azure-dreams/tower-retail/" + path.name).hex
        if path.suffix == ".png":
            text = template
            old_guid = text.split("guid: ")[1].split()[0]
            meta.write_text(text.replace(old_guid, guid))
        else:
            meta.write_text(f"fileFormatVersion: 2\nguid: {guid}\nTextScriptImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    report = {}
    tilesets = {}
    for type_id in (0x29, 0x2A, 0x2B):
        payload, blob, info = geometry(type_id)
        (OUT / f"geometry_{type_id:02x}.bytes").write_bytes(blob)
        tilesets[type_id] = (payload, info)
        report[f"geometry_{type_id:02x}"] = info
    floors = bytearray(100)
    for floor in range(100):
        floors[floor] = read_lba(0x3F5E + floor, 1)[0x40]
    (OUT / "floor_variants.bytes").write_bytes(bytes(floors))
    report["floor_variants"] = list(floors)
    # Random floors: 24 variants on tileset 0x29. Track sets: table index 1 (Beldo, 0x2A), 2 (floors 1-2, 0x2B).
    payload, info = tilesets[0x29]
    combos = combos_of(payload, info)
    report["combos_29"] = combos
    wcluts = water_cluts(payload, info)
    for variant in range(24):
        desc = struct.unpack("<I", overlay(0x8001F594 + variant * 4, 4))[0]
        vram = texture_set(desc)
        atlas(vram, combos, OUT / f"tiles_v{variant:02d}")
        water(vram, wcluts, f"tiles_v{variant:02d}")
    for table_index, type_id in ((1, 0x2A), (2, 0x2B)):
        desc = struct.unpack("<I", overlay(0x8001F588 + table_index * 4, 4))[0]
        payload, info = tilesets[type_id]
        combos = combos_of(payload, info)
        report[f"combos_{type_id:02x}"] = combos
        vram = texture_set(desc)
        atlas(vram, combos, OUT / f"tiles_t{table_index}")
        water(vram, water_cluts(payload, info), f"tiles_t{table_index}")
    for number in range(1, 11):
        if number == 4:
            continue  # descriptor 0x80080E98 is never selected by a track record and does not decode
        blob, meta = layout(number)
        (OUT / f"layout_{number:02d}.bytes").write_bytes(blob)
        report[f"layout_{number:02d}"] = meta
    for package in range(1, 27):
        backdrop(package)
    write_metas()
    (WORKSPACE / "extracted_assets/tower_research/extract_report.json").write_text(json.dumps(report, indent=1, default=str))
    print("ok")


if __name__ == "__main__":
    main()
