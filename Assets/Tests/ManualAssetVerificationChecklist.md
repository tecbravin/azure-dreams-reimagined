# Asset Integration Manual Verification Checklist

Use this while the automated PlayMode runner is unavailable on this machine.

## 1. Town Albedo Textures
Files:
- `Assets/AzureDreams/Textures/Player_Albedo.png`
- `Assets/AzureDreams/Textures/Townsperson_Albedo.png`
- `Assets/AzureDreams/Textures/Familiar_Albedo.png`

Checks:
- [ ] Each texture is 32×128 in the Inspector.
- [ ] Filter Mode is set correctly for the desired style:
  - Point for pixel-sharp PS1-style rendering.
  - Bilinear for smooth interpolation.
- [ ] Wrap Mode is Clamp or Repeat as intended.
- [ ] Read/Write is enabled if runtime modification is required.
- [ ] Max texture size does not downscale below 32×128.
- [ ] In-scene preview: assign to a prefab using `ActorView` and verify tint + albedo appear correctly on the mesh.

## 2. Audio Import Settings
Folder structure to create:
- `Assets/AzureDreams/Audio/bgm/`
- `Assets/AzureDreams/Audio/sfx/`

Checks per clip:
- [ ] Load Type: `Compressed In Memory` or `Streaming` depending on clip length.
- [ ] Compression Format: `Vorbis` for WebGL compatibility.
- [ ] Quality slider does not introduce audible artifacts.
- [ ] 3D Spatial Blend is 0 for 2D UI/bgm; set appropriately for positional SFX.
- [ ] No missing references on `AudioSource` components in `PlayerController`, `MonsterAI`, `NPCController`.

## 3. PlayMode Audio Smoke Test
- [ ] Enter Play mode.
- [ ] Trigger at least one BGM and one SFX.
- [ ] Confirm playback without errors in the Console.
- [ ] Confirm no missing `AudioClip` references in Inspector during play.

## 4. WebGL Build Readiness
- [ ] Switch platform to WebGL via Build Settings.
- [ ] Verify no unsupported audio codec warnings.
- [ ] Verify textures are within WebGL-friendly size limits.
- [ ] Run a development build and check browser console for asset load failures.

## 5. .meta Stability
- [ ] Reimport one texture and confirm its `.meta` GUID stays stable.
- [ ] Reimport one audio clip and confirm its `.meta` GUID stays stable.
- [ ] No duplicate `.meta` files appear in source control.
