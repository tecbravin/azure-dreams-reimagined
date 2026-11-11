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
- Name Files As Hashes: Disabled for Cloudflare Pages
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

## Cloudflare Pages

In Unity, select the Web profile under **File > Build Profiles**, then choose
**Azure Dreams > Build for Cloudflare**. The command builds and packages the game,
then opens the new folder under `Builds/Cloudflare`. It requires Python 3.11 or
newer on PATH.

To package an existing build, use the Default template with Gzip compression,
decompression fallback disabled, and unhashed filenames.

    python Tools/package_webgl.py Build Build-Cloudflare

Use a new destination directory on each run. Publish the destination directory
as the site root. The service worker requires HTTPS or localhost and a browser
with gzip DecompressionStream support. The generated _headers file supplies
the response types and encodings for the Unity runtime files. The package
leaves extracted diagnostic files out of the site and checks the 25 MiB
per-file limit.
