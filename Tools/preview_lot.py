"""Oblique textured preview of construction-lot triangles (software affine texture mapping)."""
import numpy as np
from PIL import Image, ImageDraw

SCALE = 40.0          # pixels per tile
TILT = 0.62           # ground depth foreshortening
LIFT = 0.80           # screen pixels per height unit (in tiles)


def project(p, x0, z0):
    x, h, z = p[0], p[1], p[2]
    return ((x - x0) * SCALE, (z - z0) * SCALE * TILT - h * SCALE * LIFT)


def render(triangles, texture_for, bounds, title=""):
    x0, z0, x1, z1 = bounds
    width = int((x1 - x0) * SCALE)
    top = int(6 * SCALE * LIFT)
    height = int((z1 - z0) * SCALE * TILT) + top
    image = Image.new("RGBA", (width, height), (40, 44, 60, 255))
    order = sorted(triangles, key=lambda t: (sum(p[2] for p in t[1]) / 3, sum(p[1] for p in t[1]) / 3))
    for material, tri in order:
        tex = texture_for(material)
        dst = [project(p, x0, z0) for p in tri]
        dst = [(x, y + top) for x, y in dst]
        src = [(p[3] * 256.0, (1.0 - p[4]) * 256.0) for p in tri]
        # affine: screen -> texture
        A = np.array([[dst[i][0], dst[i][1], 1.0] for i in range(3)])
        if abs(np.linalg.det(A)) < 1e-6:
            continue
        cu = np.linalg.solve(A, [s[0] for s in src])
        cv = np.linalg.solve(A, [s[1] for s in src])
        warped = tex.transform(image.size, Image.AFFINE, (cu[0], cu[1], cu[2], cv[0], cv[1], cv[2]), Image.NEAREST)
        mask = Image.new("L", image.size, 0)
        ImageDraw.Draw(mask).polygon(dst, fill=255)
        alpha = Image.fromarray(np.minimum(np.array(mask), np.array(warped.getchannel("A"))))
        image.paste(warped, (0, 0), alpha)
    if title:
        ImageDraw.Draw(image).text((6, 4), title, fill=(255, 255, 255, 255))
    return image
