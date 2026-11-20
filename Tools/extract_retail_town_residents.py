"""Recover every retail town resident field sprite from TOWN.BIN.

Method (generalises extract_vivian_town_sprites.py):
* Every person initializer calls register_person (jal 0x800C3050) with
  a1 = person slot, a2/a3 = private tables, stack+16 = idle/walk clip tables,
  stack+20 = action clip tables. Slot = F_act_<name> - 62 (script_symbols.h).
* Some initializers then store SpriteFrameState.clut (+0x12); the renderer
  adds it to every command CLUT (sprite_projection.c), giving per-character
  palettes for shared bodies (e.g. Hush/Tonka, Silver/Dar, Hal/Grandpa).
* Clip table = 4 direction sequence pointers + 0; sequence = 8-byte entries
  (ticks, kind 2, chain); chain = 12-byte mode 0x2C sprite commands.
* Textures/palettes: TownCharacterPackages resource_06 (rows 256-383 + all
  person CLUTs) plus resource_01 (rows 384-511); Guy uses resource_03.
Requires Pillow and extract_town_character_packages.py output.
"""
from __future__ import annotations
import argparse, json, re, shutil, struct, sys, uuid
from pathlib import Path
from PIL import Image, ImageOps

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from psx_disc import DiscImage
from extract_town_character_packages import EXPECTED_TOWN_SHA, sha256

ROOT = HERE.parents[1]
PKG = ROOT / "extracted_assets/reimagined_pipeline/TownCharacterPackages"
SYMBOLS = ROOT / "azure-dreams-decomp-master/include/script_symbols.h"
BASE = 0x8007D760
REGISTER_JAL = 0x0C030C14
EXCLUDE = {1, 2, 7, 8, 9, 10, 11, 12, 13, 17, 29, 51, 81, 75, 76, 77, 78}  # Koh, Kewne, heroines, Beldo, angel, unresolved, horses
PACKAGES = {3: (6, 3)}
# Pool scene: initializers at file offset 0x51600+ (swimsuit bodies, CLUT +0x40) use the
# person palettes of resource_06 plus the swimsuit texture package stored in TOWN.BIN.
POOL_FIRST_INITIALIZER = 0x51600
POOL_PACKAGE_OFFSET = 0x8B8000


def sx(v): return v - 0x10000 if v & 0x8000 else v


class Town:
    def __init__(self, data: bytes): self.t = data
    def u32(self, a): return struct.unpack_from("<I", self.t, a - BASE)[0]
    @staticmethod
    def valid(p): return BASE <= p < 0x80100000 and p % 4 == 0

    def registrations(self):
        out = []
        for off in range(0, len(self.t) - 4, 4):
            if struct.unpack_from("<I", self.t, off)[0] != REGISTER_JAL: continue
            regs, stack = {}, {}
            for o in range(max(0, off - 0x60), off + 8, 4):
                w = struct.unpack_from("<I", self.t, o)[0]; op, rs, rt, imm = w >> 26, (w >> 21) & 31, (w >> 16) & 31, w & 0xFFFF
                if op == 15: regs[rt] = imm << 16
                elif op == 9: regs[rt] = ((regs.get(rs, 0) if rs else 0) + sx(imm)) & 0xFFFFFFFF
                elif op == 43 and rs == 29: stack[sx(imm)] = regs.get(rt)
            clut, regs2 = 0, {}
            for o in range(off + 8, off + 0x60, 4):
                w = struct.unpack_from("<I", self.t, o)[0]; op, rs, rt, imm = w >> 26, (w >> 21) & 31, (w >> 16) & 31, w & 0xFFFF
                if op == 9 and rs == 0: regs2[rt] = sx(imm)
                if op == 41 and sx(imm) == 18: clut = regs2.get(rt, 0) if rt else 0; break
                if w == 0x03E00008: break
            out.append(dict(file_offset=off, slot=regs.get(5), s16=stack.get(16), s20=stack.get(20), clut_offset=clut))
        return out

    def chain(self, a):
        parts = []
        for i in range(48):
            fl, mode, x, y, page, clut, u, v, w, h = struct.unpack_from("<BBbbHHBBBB", self.t, a - BASE + i * 12)
            if mode != 0x2C or not w or not h: return None
            parts.append(dict(flags=fl, x=x, y=y, tpage=page, clut=clut, u=u, v=v, width=w, height=h))
            if fl & 0x80: return parts
        return None

    def sequence(self, a):
        ents = []
        for i in range(128):
            ticks, kind, ptr = struct.unpack_from("<HHI", self.t, a - BASE + i * 8)
            if kind != 2: return dict(entries=ents, loop=kind == 1) if kind in (0, 1) and ticks == 0 else None
            if not (0 < ticks <= 240) or not self.valid(ptr): return None
            ents.append((ticks, ptr))
        return None

    def clip(self, a):
        if not self.valid(a): return None
        dirs = []
        for i in range(5):
            p = self.u32(a + 4 * i)
            if p == 0: break
            s = self.sequence(p) if self.valid(p) else None
            if s is None or any(self.chain(c) is None for _, c in s["entries"]): return None
            dirs.append(p)
        return dirs or None

    def clips(self, s16, s20):
        out = []
        for base, n in ((s16, 2), (s20, 40)):
            for i in range(n):
                if base == s16 and base + 4 * i >= s20: break
                d = self.clip(self.u32(base + 4 * i))
                if d is None: break
                out.append(d)
        return out


def load_vram(names):
    vram, wr = bytearray(1024 * 512 * 2), bytearray(1024 * 512)
    for n in names:
        folder = PKG / (n if isinstance(n, str) else f"resource_{n:02d}")
        d = (folder / "vram_words_le.bin").read_bytes(); m = (folder / "vram_written_mask.bin").read_bytes()
        for i, e in enumerate(m):
            if e: vram[i * 2:i * 2 + 2] = d[i * 2:i * 2 + 2]; wr[i] = 1
    return vram, wr


def rgba(w): return tuple((v << 3) | (v >> 2) for v in ((w >> s) & 31 for s in (0, 5, 10))) + ((0 if w == 0 else 255),)


def render(parts, vram, wr, cell, ox, oy):
    res = Image.new("RGBA", (cell, cell))
    for p in reversed(parts):  # addPrim prepends: first part on top
        px, py = (p["tpage"] & 15) * 64, ((p["tpage"] >> 4) & 1) * 256
        cx, cy = (p["clut"] & 63) * 16, p["clut"] >> 6
        pal = []
        for i in range(16):
            idx = cy * 1024 + cx + i
            if not wr[idx]: raise ValueError(f"missing CLUT {p['clut']:#x}")
            pal.append(rgba(struct.unpack_from("<H", vram, idx * 2)[0]))
        pix = []
        for y in range(p["height"]):
            for x in range(p["width"]):
                tx, ty = p["u"] + x, p["v"] + y; idx = (py + ty) * 1024 + px + tx // 4
                if not wr[idx]: raise ValueError("missing texture word")
                pix.append(pal[(struct.unpack_from("<H", vram, idx * 2)[0] >> ((tx % 4) * 4)) & 15])
        tile = Image.new("RGBA", (p["width"], p["height"])); tile.putdata(pix)
        x, y = p["x"], p["y"]
        if p["flags"] & 1: tile = ImageOps.mirror(tile); x = -x - p["width"]
        if p["flags"] & 2: tile = ImageOps.flip(tile); y = -y - p["height"]
        if x + ox < 0 or y + oy < 0 or x + ox + tile.width > cell or y + oy + tile.height > cell: raise ValueError("cropped")
        res.alpha_composite(tile, (x + ox, y + oy))
    return res


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--disc", type=Path, default=ROOT / "Azure Dreams.bin")
    ap.add_argument("--output", type=Path, default=ROOT / "extracted_assets/reimagined_pipeline/TownResidents/Retail")
    ap.add_argument("--install", action="store_true")
    a = ap.parse_args()
    town_bytes, _ = DiscImage(a.disc).read_file("TOWN/TOWN.BIN")
    if sha256(town_bytes) != EXPECTED_TOWN_SHA: raise ValueError("TOWN.BIN is not the authenticated NTSC-U file")
    town = Town(town_bytes)
    names = {int(m[2]) - 62: m[1] for m in re.finditer(r"#define F_act_(\w+) (\d+)", SYMBOLS.read_text())}
    from extract_town_character_packages import parse_package
    pool_dir = PKG / "pool_swimsuits"
    pool_dir.mkdir(parents=True, exist_ok=True)
    parse_package(town_bytes[POOL_PACKAGE_OFFSET:POOL_PACKAGE_OFFSET + 0x40000], pool_dir)
    best = {}
    for r in town.registrations():
        pool = r["file_offset"] >= POOL_FIRST_INITIALIZER
        if r["slot"] in EXCLUDE: continue
        if pool and r["file_offset"] >= 0x51FA8: continue  # 0x51FA8 registers Chero outside the pool set
        clips = town.clips(r["s16"], r["s20"])
        if not clips: continue
        key = ("pool", r["slot"]) if pool else r["slot"]
        n = len({c for d in clips for p in d for _, c in town.sequence(p)["entries"]})
        if key not in best or n > best[key][0]: best[key] = (n, r, clips)
    a.output.mkdir(parents=True, exist_ok=True)
    cache = {}
    for key, (_, r, clips) in sorted(best.items(), key=lambda item: str(item[0])):
        pool = isinstance(key, tuple)
        slot = key[1] if pool else key
        combo = (6, "pool_swimsuits") if pool else PACKAGES.get(slot, (6, 1)); vram, wr = cache.setdefault(combo, load_vram(combo))
        ptrs = sorted({c for d in clips for p in d for _, c in town.sequence(p)["entries"]})
        xs, ys = [], []
        for c in ptrs:
            for p in town.chain(c):
                x = -p["x"] - p["width"] if p["flags"] & 1 else p["x"]; y = -p["y"] - p["height"] if p["flags"] & 2 else p["y"]
                xs += [x, x + p["width"]]; ys += [y, y + p["height"]]
        cell = max(64, ((max(max(xs) - min(xs), max(ys) - min(ys)) + 15) // 16) * 16)
        ox = cell // 2 if min(xs) + cell // 2 >= 0 and max(xs) + cell // 2 <= cell else -min(xs)
        oy = -min(ys)
        ids = {p: i for i, p in enumerate(ptrs)}
        atlas = Image.new("RGBA", (8 * cell, ((len(ptrs) + 7) // 8) * cell))
        for p, i in ids.items():
            parts = [dict(q, clut=q["clut"] + r["clut_offset"]) for q in town.chain(p)]
            atlas.alpha_composite(render(parts, vram, wr, cell, ox, oy), ((i % 8) * cell, (i // 8) * cell))
        keys = ["idle", "walk"] + [f"action_{k:02d}" for k in range(len(clips) - 2)]
        jc = []
        for key, dirs in zip(keys, clips):
            ds = []
            for d in (dirs * 4)[:4]:
                s = town.sequence(d)
                ds.append(dict(frames=[ids[c] for _, c in s["entries"]], ticks=[t for t, _ in s["entries"]], loop=s["loop"]))
            jc.append(dict(key=key, directions=ds))
        name = ("pool_" if pool else "") + names.get(slot, f"slot{slot}")
        atlas.save(a.output / f"{name}.png")
        (a.output / f"{name}.json").write_text(json.dumps(dict(cellSize=cell, columns=8, frameCount=len(ptrs), pivotX=ox / cell,
            pivotY=(cell - oy) / cell, ticksPerSecond=60, clips=jc, source=dict(slot=slot, act_symbol=f"F_act_{name}",
            initializer_file_offset=hex(r["file_offset"]), tables=[hex(r["s16"]), hex(r["s20"])], clut_offset=r["clut_offset"],
            packages=list(combo))), indent=1), encoding="utf-8")
        print(f"{slot:3d} {name:10s} {len(ptrs):3d} frames  clut+{r['clut_offset']}")
    if a.install:
        dest = HERE.parent / "Assets/AzureDreams/Resources/Town/Residents/Retail"; dest.mkdir(parents=True, exist_ok=True)
        src_meta = HERE.parent / "Assets/AzureDreams/Resources/Town/Residents"
        for f in a.output.glob("*.*"):
            if f.suffix not in (".png", ".json"): continue
            shutil.copyfile(f, dest / f.name)
            meta = dest / (f.name + ".meta")
            if not meta.exists():
                tmpl = (src_meta / ("Vivian" + f.suffix + ".meta")).read_text()
                meta.write_text(re.sub(r"(?m)^guid: .*", "guid: " + uuid.uuid5(uuid.NAMESPACE_URL, "azure-dreams/retail-resident/" + f.name).hex, tmpl))


if __name__ == "__main__":
    main()
