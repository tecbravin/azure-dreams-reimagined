# Azure Dreams Reimagined — Render Pipeline Improvements

## Current State
- Built-in Render Pipeline (not URP)
- Quality: Very Low (m_CurrentQuality: 5)
- Color Space: Gamma (need to verify)
- MT Rendering: Enabled
- No LOD groups configured
- No occlusion culling
- No lightmap baking

## Recommended Improvements (Unity 6 Best Practices)

### 1. Quality Settings
- **Current**: Very Low (pixelLightCount=0, shadows=0, no AA)
- **Recommended**: Create 3 quality tiers:
  - Low (WebGL fallback): 0 pixel lights, no shadows, 0 AA, 1 mip limit
  - Medium (default): 2 pixel lights, hard shadows, 2x MSAA, 0 mip limit
  - High (desktop): 4 pixel lights, soft shadows, 4x MSAA, streaming mips

### 2. Texture Compression (WebGL)
- Use **DXT5 (BC3)** for RGBA textures with alpha
- Use **DXT1 (BC1)** for opaque textures (ground, walls)
- Use **ASTC** for mobile/WebGL2 (better compression, higher quality)
- Set `m_TextureCompression` in Player Settings to ASTC for WebGL

### 3. Shader Optimization
- **Batching**: Use Static Batching for environment (ground, walls)
- **Dynamic Batching**: Enable for small objects (<300 vertices)
- **GPU Instancing**: Enable on all materials for repeated meshes (tiles, trees)
- **Shader keywords**: Minimize shader variants (use `#pragma multi_compile` sparingly)

### 4. LOD Groups
- Add LODGroup to:
  - Floor tileset (LOD0: full, LOD1: simplified 50%, LOD2: billboard)
  - Monster models (LOD0: full, LOD1: 50%, LOD2: 25%)
  - Environment objects (trees, rocks, buildings)
- Use `m_LODBias` = 1.0 (default) or 1.5 for better quality

### 5. Occlusion Culling
- Bake occlusion for tower floors (each floor is a closed room)
- Use `Occlusion Area` components per floor
- Enable `m_OcclusionCulling` in camera

### 6. Lightmaps
- Bake static lighting for tower interiors
- Use `Lightmap Parameters` for low-res (WebGL friendly)
- Enable `m_LightmapBakeQuality` = 1 (low, for WebGL)

### 7. Post-Processing (Already Created)
- Bloom, Color Grading, Vignette, DOF, Chromatic Aberration, Film Grain, AO
- Disable DOF on WebGL (performance)
- Use half-resolution for post-processing on low quality

### 8. Render Pipeline (Built-In vs URP)
- **Keep Built-In** for WebGL (URP has higher baseline overhead)
- Built-In is more compatible with WebGL 1.0/2.0
- Use `m_CustomRenderPipeline: {fileID: 0}` (current, correct)

### 9. Draw Call Optimization
- **Static Batching**: Mark environment objects as static
- **Dynamic Batching**: Enable in Player Settings
- **SRP Batcher**: Not available in Built-In (URP only)
- **GPU Instancing**: Enable on materials with repeated meshes

### 10. Memory Management
- **Texture Streaming**: Enable `m_TextureStreaming` for large textures
- **Audio Compression**: Vorbis 80% quality (already set)
- **Asset Bundles**: Consider for WebGL (load on demand)

### 11. WebGL-Specific
- **Memory Size**: Set to 512MB (default 256MB too small)
- **Compression**: Gzip (not Brotli for compatibility)
- **Data Caching**: Enable `m_WebGLDataCaching`
- **Threads**: Enable `m_WebGLThreads` for Unity 6
- **Name Files As Hashes**: Enable for caching

### 12. Camera Settings
- **Clear Flags**: Skybox (not solid color)
- **Clipping Planes**: Near=0.1, Far=100 (WebGL friendly)
- **Field of View**: 60 (default, good for PS1 style)
- **Viewport Rect**: Full screen

### Implementation Priority
1. Quality settings tiers (quick win)
2. GPU instancing on materials (quick win)
3. Static batching (moderate effort)
4. LOD groups (moderate effort)
5. Occlusion culling (moderate effort)
6. Lightmap baking (high effort)
7. Texture streaming (high effort)
8. WebGL memory/compression (deployment)
