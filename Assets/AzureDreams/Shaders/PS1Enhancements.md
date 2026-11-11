# PS1 Graphics Enhancement Implementation

## New Shaders Created

### 1. `Assets/AzureDreams/Shaders/PsxVertexSnapAffine.shader`
- **Vertex Snapping**: Rounds vertex positions to screen grid (fixed-point precision, PS1 style)
- **Affine Texture Mapping**: Drops perspective correction, causes texture warping on oblique surfaces
- Properties: `_SnapResolution` (default 320), `_AffineWarp` (0-1), `_Color` (tint)

### 2. `Assets/AzureDreams/Shaders/PsxColorQuantization.shader`
- **15-bit Color Quantization**: Reduces to 5 bits per channel (32 levels, matching PS1)
- **Bayer 4x4 Dithering**: Ordered dither pattern for smooth gradients
- Properties: `_ColorDepth` (3-8 bits), `_DitherStrength` (0-1), `_DitherScale`

## Existing Assets (Already Implemented by Coder)
- `PsxIndexed.shader` — 4bpp indexed atlas + CLUT (256×256, 16 palettes × 16 colors)
- `BuiltinPostProcess.shader` — Built-in post-processing
- `PostProcessingController.cs` — Toggleable effects (Bloom, Vignette, AO, etc.)

## Recommended Usage
1. Apply `PsxVertexSnapAffine` to 3D models for authentic vertex jitter + texture warping
2. Apply `PsxColorQuantization` as a full-screen post-process for 15-bit color + dithering
3. Keep `PsxIndexed` for sprite rendering (already integrated)

## Unity Built-In Pipeline Notes
- Built-In is deprecated in Unity 6.7 LTS but still supported
- For full STP upscaling, migrate to URP (recommended for new projects)
- SRP Batcher requires URP/HDRP (not available in Built-In)
- GPU occlusion culling requires URP/HDRP

## Next Steps (Coder's Domain)
- Integrate vertex snapping into the sprite rendering pipeline
- Add color quantization to the post-processing chain
- Consider URP migration for STP upscaling and SRP Batcher
