"""Keep the original child sheets at native size and remove their magenta key.

These runtime copies avoid the source sheets' power-of-two resampling, which
invalidates their directional sprite rectangles. Original sheets are preserved.
Requires Pillow.
"""
from pathlib import Path
from PIL import Image

project = Path(__file__).resolve().parents[1]
destination = project / "Assets/AzureDreams/Resources/Town/Arcade"
destination.mkdir(parents=True, exist_ok=True)
for name, source_name, size in [("Young Nico", "Young Nico", (230, 244)),
                                ("Young Koh", "Guy, Wreath & Young Koh", (399, 226))]:
    source = project / f"Assets/AzureDreams/SpritersResource/PlayStation - Azure Dreams - Characters - {source_name}.png"
    image = Image.open(source).convert("RGBA")
    if image.size != size:
        raise ValueError(f"Unexpected original {name} sheet dimensions: {image.size}")
    image.putdata([(r, g, b, 0 if r >= 245 and g <= 10 and b >= 245 else a)
                   for r, g, b, a in image.get_flattened_data()])
    target = destination / (name.replace(" ", "") + ".png")
    image.save(target)
    print(target)
