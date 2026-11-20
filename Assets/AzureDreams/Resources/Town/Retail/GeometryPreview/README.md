# Town geometry preview assets

These 41 OBJ files are generated from the authenticated Town geometry
resources. Vertex coordinates are signed map X, map Y and height. Polygon
traversal follows `shaded_tile_rendering_g00594`; OBJ faces preserve source
vertex order and carry all four source UV coordinates. MTL materials are keyed
by the retail TPAGE and CLUT words.

The 530 RGB textures were sampled from the complete 1 MiB VRAM capture at
`extracted_assets/azure_dreams_3d_models/ad_model_building_01_koh_house_1/vram.bin`.
That capture exactly matches all 110,336 known words from authenticated Town
variant 0, making it a source-backed full VRAM baseline. RGB555 colors and
transparency are decoded into PNGs; `_stp.png` files preserve the original
semi-transparency bit separately.

The 41 entries are Town map/environment geometry, not identified resident
characters. Six geometry payloads contain some malformed or out-of-range
face records; those records are omitted and counted in the JSON reports in
`Extracted/TownGeometry/meshes`. The selected full VRAM snapshot is the Koh
house level 1 capture; variant-specific or animated texture uploads may change
appearance in other Town scenes. PS1 lighting, blend modes, projection, and
runtime scene composition are not reproduced by these static previews.

Regenerate from the workspace root with:

```powershell
python 'Azure Dreams Reimagined/Tools/export_town_geometry_meshes.py'
```
