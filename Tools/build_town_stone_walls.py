"""Export wall and prop collision from the captured Monsbaiya mesh."""
from collections import defaultdict
import json
import math
from pathlib import Path
import struct

project = Path(__file__).resolve().parents[1]
town = project / "Assets/AzureDreams"
capture = town / "Models/Town/CapturedScenes/ad_model_town_min"
materials = {
    "page_11_clut_28681", "page_11_clut_28682", "page_8_clut_29640",
    "page_8_clut_29768", "page_8_clut_29769", "page_8_clut_29772",
}
size = 128
vertices = []
tops = []
steps = []
bridge = []
canal_bridge_samples = set()
flowers = {"page_8_clut_29577", "page_8_clut_29711", "page_8_clut_29708", "page_8_clut_29709"}
wall_samples = defaultdict(set)
material = None
well_panels = []
trunks = {}
tree_tiles = defaultdict(list)
barrel_points = []

def cross(a, b, p):
    return (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0])

with (capture / "Monsbaiya.obj").open() as model:
    for line in model:
        parts = line.split()
        if not parts:
            continue
        if parts[0] == "v":
            x, y, z = map(float, parts[1:])
            # Original capture coordinates are quantized at 1/256 world units.
            vertices.append(tuple(round(value * 256) / 256 for value in (64 - 64*x, 64*y, 64 - 64*z)))
        elif parts[0] == "usemtl":
            material = parts[1]
        elif parts[0] == "f":
            a, b, c = (vertices[int(ref.split("/")[0]) - 1] for ref in parts[1:])
            points = (a, b, c)
            xs, ys, zs = zip(*points)
            if material == "page_8_clut_29641":
                barrel_points.extend(points)
            elif material == "page_137_clut_28808" and min(ys) == 0 and max(ys) == 0.5:
                well_panels.append(points)
            elif material == "page_12_clut_32389" and min(ys) == 0 and max(ys) >= 1.75:
                center = (round(sum(xs)/3), round(sum(zs)/3))
                trunks[center] = max(trunks.get(center, 0), max(ys))
            elif material == "page_12_clut_32388" and min(ys) == max(ys):
                center = (round(sum(xs)/3), round(sum(zs)/3))
                tree_tiles[center].append(points)
            pond_island = (material == "page_13_clut_30145" and set(ys) in ({-0.125}, {0.375}) and
                           57 <= min(xs) <= max(xs) <= 70 and 66 <= min(zs) <= max(zs) <= 77)
            stepping_stone = (material == "page_13_clut_32335" and set(ys) == {0.21875})
            small_bridge = (material == "page_11_clut_28682" and min(ys) >= 0.5 and
                            57 <= min(xs) <= max(xs) <= 60 and 75 <= min(zs) <= max(zs) <= 78)
            # The canal bridge uses separate materials for its approach ramps and crown.
            canal_bridge = (material in {"page_8_clut_29826", "page_8_clut_30018"} and
                            55 <= min(xs) <= max(xs) <= 62 and
                            any(lo <= min(zs) <= max(zs) <= hi
                                for lo, hi in ((82.5, 85.5), (100.5, 103.5))))
            bridge_deck = small_bridge or canal_bridge
            if not bridge_deck and material not in materials and material not in flowers and not pond_island and not stepping_stone:
                continue
            top = a[1]
            if not bridge_deck and ((not pond_island and not 0 < top <= 2) or b[1] != top or c[1] != top):
                continue
            a, b, c = [(x, z) for x, _, z in (a, b, c)]
            if material == "page_8_clut_29768":
                # The wooden block tapers from a one-unit base to a half-unit cap.
                # Use the widest footprint so walking Koh cannot enter its sides.
                a, b, c = [(x, 26.0 if z < 26.5 else 27.0) for x, z in (a, b, c)]
            if cross(a, b, c) == 0:
                continue
            surface = dict(a=dict(x=a[0], y=a[1]), b=dict(x=b[0], y=b[1]), c=dict(x=c[0], y=c[1]), top=top)
            if stepping_stone:
                steps.append(surface)
                continue
            if bridge_deck:
                surface.update(heightB=ys[1], heightC=ys[2])
                bridge.append(surface)
            else:
                tops.append(surface)
            if pond_island:
                continue
            for z in range(math.ceil(min(a[1], b[1], c[1])), math.floor(max(a[1], b[1], c[1])) + 1):
                for x in range(math.ceil(min(a[0], b[0], c[0])), math.floor(max(a[0], b[0], c[0])) + 1):
                    edges = [cross(a, b, (x, z)), cross(b, c, (x, z)), cross(c, a, (x, z))]
                    if 0 <= x < size and 0 <= z < size and (min(edges) >= 0 or max(edges) <= 0):
                        height = top
                        if bridge_deck:
                            area = cross(a, b, c)
                            height = (edges[1] * ys[0] + edges[2] * ys[1] + edges[0] * ys[2]) / area
                        wall_samples[(x, z)].add(round(height * 256))
                        if canal_bridge:
                            canal_bridge_samples.add((x, z))

def round_obstacle(x, z, radius, top):
    return dict(center=dict(x=x, y=z), radius=radius, top=top)

def components(faces):
    by_vertex = defaultdict(list)
    for i, face in enumerate(faces):
        for vertex in face:
            by_vertex[vertex].append(i)
    seen = set()
    for first in range(len(faces)):
        if first in seen:
            continue
        pending = [first]
        seen.add(first)
        for i in pending:
            for vertex in faces[i]:
                for neighbor in by_vertex[vertex]:
                    if neighbor not in seen:
                        pending.append(neighbor)
                        seen.add(neighbor)
        yield {vertex for i in pending for vertex in faces[i]}

props = []
for points in components(well_panels):
    x, _, z = zip(*points)
    radius = (max(x)-min(x))/2
    if radius == 0.75:
        props.append(round_obstacle((min(x)+max(x))/2, (min(z)+max(z))/2, radius, 0.5))
if barrel_points:
    x, y, z = zip(*barrel_points)
    props.append(round_obstacle((min(x)+max(x))/2, (min(z)+max(z))/2, (max(x)-min(x))/2, max(y)))

for (x, z), top in sorted(trunks.items()):
    # The trunk texture is on crossed cards. Its opaque stem occupies about
    # half their width, so do not use the canopy or entire quad as a collider.
    props.append(round_obstacle(x, z, 0.24 if top < 3 else 0.30, top))

low_tree_tiers = [triangle for faces in tree_tiles.values() for triangle in faces if 0.625 <= triangle[0][1] <= 1.25]
small_trees = {}
for points in components(low_tree_tiers):
    xs, ys, zs = zip(*points)
    if 0.7 <= max(xs)-min(xs) <= 1.01 and 0.7 <= max(zs)-min(zs) <= 1.01:
        x, z = (min(xs)+max(xs))/2, (min(zs)+max(zs))/2
        if (x, z) not in trunks:
            small_trees[(x, z)] = max(small_trees.get((x, z), 0), max(ys))
props.extend(round_obstacle(x, z, 0.30, top) for (x,z), top in sorted(small_trees.items()))

for faces in tree_tiles.values():
    # Remove canopy samples from the ground map. Low trunk/plant collision is
    # supplied separately, so walking past the leaves does not hit an invisible wall.
    for a,b,c in faces:
        top = a[1]
        a,b,c = [(p[0],p[2]) for p in (a,b,c)]
        if cross(a,b,c) == 0:
            continue
        for gz in range(math.ceil(min(a[1],b[1],c[1])), math.floor(max(a[1],b[1],c[1]))+1):
            for gx in range(math.ceil(min(a[0],b[0],c[0])), math.floor(max(a[0],b[0],c[0]))+1):
                edges = [cross(a,b,(gx,gz)),cross(b,c,(gx,gz)),cross(c,a,(gx,gz))]
                if 0 <= gx < size and 0 <= gz < size and (min(edges) >= 0 or max(edges) <= 0):
                    wall_samples[(gx,gz)].add(round(top*256))

heights = struct.unpack("<16384h", (capture / "MonsbaiyaHeights.bytes").read_bytes())
ground = []
for (x, z), top_heights in sorted(wall_samples.items()):
    index = z * size + x
    if heights[index] not in top_heights:
        continue
    samples = []
    for dz in range(-2, 3):
        for dx in range(-2, 3):
            nx, nz = x + dx, z + dz
            if 0 <= nx < size and 0 <= nz < size:
                candidate = heights[nz * size + nx]
                if -192 <= candidate <= 166 and candidate not in wall_samples.get((nx, nz), ()):
                    samples.append((dx*dx + dz*dz, -candidate))
    if samples:
        ground.append(dict(index=index, height=-min(samples)[1] / 256))
    elif (x, z) in canal_bridge_samples:
        # No exposed floor sample reaches the middle of the canal under the span.
        ground.append(dict(index=index, height=-0.5))

output = town / "Resources/Town/Retail/town_stone_walls.json"
output.write_text(json.dumps(dict(surfaces=tops, ground=ground, props=props, steps=steps, bridge=bridge), separators=(",", ":")) + "\n", encoding="utf-8")
print(f"{len(tops)} wall triangles, {len(ground)} underlying ground samples, {len(props)} round props")
