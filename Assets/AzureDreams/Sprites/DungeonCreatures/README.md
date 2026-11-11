# Azure Dreams Reimagined - Sprite Asset Integration

## Source
- Extracted from: DUNGEON.BIN (verified_decode/sprites/)
- Format: 4bpp indexed, 256x256 atlas, RGB555 CLUT (16 palettes x 16 colors)
- 51 valid resources (IDs 1-57, excluding quarantined 47, 48, 51-54)
- 119 colored atlas PNGs (RGBA, 256x256)
- 408 frame chain preview PNGs

## Unity Import Settings
- Texture Type: Default (Sprite for 2D, or Default for 3D)
- Max Size: 512 (256 source, allow headroom)
- Compression: (none for WebGL - use uncompressed or DXT)
- Mipmaps: Disabled (pixel art)
- Filter Mode: Point (nearest neighbor for pixel art)
- Wrap Mode: Clamp

## Quarantined (DO NOT IMPORT)
- resource_047: pending
- resource_048: out-of-bounds pointer
- resource_051-054: pending

## Notes
- STP (semi-transparency) preserved in palettes.json, not conventional alpha
- Atlas indices (atlas_indices.png) are raw 4-bit data, not for direct rendering
- Use atlas_palette_*.png for rendering (CLUT already baked)
- Frame chain previews are flat 2D composites, not runtime render order
