# WebGL Player Settings

## Required Settings (ProjectSettings/ProjectSettings.asset)
- m_ColorSpace: 0 (Gamma, for WebGL compatibility)
- m_GraphicsJobs: 0 (disabled for WebGL)
- m_MTRendering: 1 (enabled)

## WebGL Template
- Use Default template (not Minimal)
- Enable Data Caching
- Enable Threads (Unity 6)
- Memory Size: 512MB

## Build Settings
- Compression: Gzip (not Brotli for compatibility)
- Name Files As Hashes: Enabled
- Development Build: Disabled (for release)

## Quality Settings
- Default: Medium
- Very Low: for low-end devices
- High: for desktop browsers

## How to Apply
1. Edit > Project Settings > Player > WebGL
2. Set Memory Size to 512
3. Enable Data Caching
4. Enable Threads
5. Set Compression to Gzip
