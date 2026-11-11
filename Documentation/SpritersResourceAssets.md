# Azure Dreams Reimagined - Additional Assets

Last updated: After Spriters Resource integration

## Spriters Resource (109 PNGs)
- Location: Assets/AzureDreams/SpritersResource/
- Tower floors: 1F-2F through 38F-39F (20 textures, 256×256 RGBA)
- Characters: Koh, Selfi, Ghosh, Nico, Weedy, Jorda, Guy, Wreath, Young Koh, Young Nico, Cherrl, Chappie
- Monsters: Beldo, Kewne, Trolls
- UI: Fonts, Minimap, OpenBag
- Miscellaneous: Angel, Cat & Bird, Credits, Mugshots, Pots/Table, Dishes, Title Screen, Monster Portraits, Koh Status Faces, Tower Elevator
- Familiar Portraits: 1-50

## Tower Floor Themes (5 themes)
| Theme | Floors | Floor Material | Wall Material | Sky Material |
|-------|--------|----------------|---------------|--------------|
| StoneDungeon | 1-10 | Floor_StoneDungeon.mat | Wall_StoneDungeon.mat | Sky_StoneDungeon.mat |
| CrystalCave | 11-20 | Floor_CrystalCave.mat | Wall_CrystalCave.mat | Sky_CrystalCave.mat |
| LavaChamber | 21-30 | Floor_LavaChamber.mat | Wall_LavaChamber.mat | Sky_LavaChamber.mat |
| IcePalace | 31-40 | Floor_IcePalace.mat | Wall_IcePalace.mat | Sky_IcePalace.mat |
| DarkTower | 41+ | Floor_DarkTower.mat | Wall_DarkTower.mat | Sky_DarkTower.mat |

## Floor Tileset (proprietary 0x29 format)
- OBJ: Assets/AzureDreams/Models/floor_tileset.obj (165v, 22n, 582f)
- Prefab variants: 5 themes in Assets/AzureDreams/Prefabs/Ground/Floor_Tileset_*.prefab
- Raw data: extracted_assets/verified_decode/report_verification/floor_tileset_type29_decoded.bin (46,951 bytes)

## FX Effects (6 particle systems)
- Prefabs: Assets/AzureDreams/Prefabs/FX/FX_Fire.prefab, FX_Smoke.prefab, FX_Dust.prefab,
  FX_Lightning.prefab, FX_Water.prefab, FX_Magic.prefab
- Materials: Assets/AzureDreams/Materials/FX_*.mat (additive for fire/lightning/magic, alpha blend for smoke/dust/water)

## Environment Props (14 prefabs)
- Walls: Wall_Short, Wall_Long, Wall_Tall
- Pillars: Pillar_Small, Pillar_Large
- Stairs: Stairs_1x1, Stairs_2x2, Stairs_3x3
- DoorFrame, Torch_Holder, Chest, Pot
- Location: Assets/AzureDreams/Prefabs/Environment/

## Audio (WebGL-Ready OGG + Original WAV)
- Location: Assets/AzureDreams/Audio/bgm/ (91 files) and Assets/AzureDreams/Audio/sfx/ (79 files)
- Files: WAV + OGG (Vorbis compression, spatialized SFX, stereo BGM)
- Import settings: Vorbis, CompressedInMemory (via AudioImportPostprocessor)

## Video
- Location: Assets/AzureDreams/Videos/
- Files: 7 MP4 (H.264 + AAC) — JO001-003, KONAMI, LOP1, LOP2, TOP_WS, converted from AVI

## Shaders
- Location: Assets/AzureDreams/Shaders/
- Files: PsxIndexed.shader, PsxVertexSnapAffine.shader, PsxColorQuantization.shader,
         PsxVertexLit.shader, BuiltinPostProcess.shader, PsxIndexed.shader

## Spriters Resource (109 PNGs)
- Location: Assets/AzureDreams/SpritersResource/
- Tower floors: 1F-2F through 38F-39F (20 textures, 256×256 RGBA)
- Characters: Koh, Selfi, Ghosh, Nico, Weedy, Jorda, Guy, Wreath, Young Koh, Young Nico, Cherrl, Chappie
- Monsters: Beldo, Kewne, Trolls
- UI: Fonts, Minimap, OpenBag
- Miscellaneous: Angel, Cat & Bird, Credits, Mugshots, Pots/Table, Dishes, Title Screen, Monster Portraits, Koh Status Faces, Tower Elevator
- Familiar Portraits: 1-50

## Tower Floor Themes (5 themes)
| Theme | Floors | Floor Material | Wall Material | Sky Material |
|-------|--------|----------------|---------------|--------------|
| StoneDungeon | 1-10 | Floor_StoneDungeon.mat | Wall_StoneDungeon.mat | Sky_StoneDungeon.mat |
| CrystalCave | 11-20 | Floor_CrystalCave.mat | Wall_CrystalCave.mat | Sky_CrystalCave.mat |
| LavaChamber | 21-30 | Floor_LavaChamber.mat | Wall_LavaChamber.mat | Sky_LavaChamber.mat |
| IcePalace | 31-40 | Floor_IcePalace.mat | Wall_IcePalace.mat | Sky_IcePalace.mat |
| DarkTower | 41+ | Floor_DarkTower.mat | Wall_DarkTower.mat | Sky_DarkTower.mat |

## Floor Tileset (proprietary 0x29 format)
- OBJ: Assets/AzureDreams/Models/floor_tileset.obj (165v, 22n, 582f)
- Prefab variants: 5 themes in Assets/AzureDreams/Prefabs/Ground/Floor_Tileset_*.prefab
- Raw data: extracted_assets/verified_decode/report_verification/floor_tileset_type29_decoded.bin (46,951 bytes)

## FX Effects (6 particle systems)
- Prefabs: Assets/AzureDreams/Prefabs/FX/FX_Fire.prefab, FX_Smoke.prefab, FX_Dust.prefab,
  FX_Lightning.prefab, FX_Water.prefab, FX_Magic.prefab
- Materials: Assets/AzureDreams/Materials/FX_*.mat (additive for fire/lightning/magic, alpha blend for smoke/dust/water)

## Environment Props (14 prefabs)
- Walls: Wall_Short, Wall_Long, Wall_Tall
- Pillars: Pillar_Small, Pillar_Large
- Stairs: Stairs_1x1, Stairs_2x2, Stairs_3x3
- DoorFrame, Torch_Holder, Chest, Pot
- Location: Assets/AzureDreams/Prefabs/Environment/

## Audio (WebGL-Ready OGG + Original WAV)
- Location: Assets/AzureDreams/Audio/bgm/ (91 files) and Assets/AzureDreams/Audio/sfx/ (79 files)
- Files: WAV + OGG (Vorbis compression, spatialized SFX, stereo BGM)
- Import settings: Vorbis, CompressedInMemory (via AudioImportPostprocessor)

## Video
- Location: Assets/AzureDreams/Videos/
- Files: 7 MP4 (H.264 + AAC) — JO001-003, KONAMI, LOP1, LOP2, TOP_WS, converted from AVI

## Shaders
- Location: Assets/AzureDreams/Shaders/
- Files: PsxIndexed.shader, PsxVertexSnapAffine.shader, PsxColorQuantization.shader,
         PsxVertexLit.shader, BuiltinPostProcess.shader, PsxIndexed.shader

## Spriters Resource (109 PNGs)
- Location: Assets/AzureDreams/SpritersResource/
- Tower floors: 1F-2F through 38F-39F (20 textures, 256×256 RGBA)
- Characters: Koh, Selfi, Ghosh, Nico, Weedy, Jorda, Guy, Wreath, Young Koh, Young Nico, Cherrl, Chappie
- Monsters: Beldo, Kewne, Trolls
- UI: Fonts, Minimap, OpenBag
- Miscellaneous: Angel, Cat & Bird, Credits, Mugshots, Pots/Table, Dishes, Title Screen, Monster Portraits, Koh Status Faces, Tower Elevator
- Familiar Portraits: 1-50

## Tower Floor Themes (5 themes)
| Theme | Floors | Floor Material | Wall Material | Sky Material |
|-------|--------|----------------|---------------|--------------|
| StoneDungeon | 1-10 | Floor_StoneDungeon.mat | Wall_StoneDungeon.mat | Sky_StoneDungeon.mat |
| CrystalCave | 11-20 | Floor_CrystalCave.mat | Wall_CrystalCave.mat | Sky_CrystalCave.mat |
| LavaChamber | 21-30 | Floor_LavaChamber.mat | Wall_LavaChamber.mat | Sky_LavaChamber.mat |
| IcePalace | 31-40 | Floor_IcePalace.mat | Wall_IcePalace.mat | Sky_IcePalace.mat |
| DarkTower | 41+ | Floor_DarkTower.mat | Wall_DarkTower.mat | Sky_DarkTower.mat |

## Floor Tileset (proprietary 0x29 format)
- OBJ: Assets/AzureDreams/Models/floor_tileset.obj (165v, 22n, 582f)
- Prefab variants: 5 themes in Assets/AzureDreams/Prefabs/Ground/Floor_Tileset_*.prefab
- Raw data: extracted_assets/verified_decode/report_verification/floor_tileset_type29_decoded.bin (46,951 bytes)

## FX Effects (6 particle systems)
- Prefabs: Assets/AzureDreams/Prefabs/FX/FX_Fire.prefab, FX_Smoke.prefab, FX_Dust.prefab,
  FX_Lightning.prefab, FX_Water.prefab, FX_Magic.prefab
- Materials: Assets/AzureDreams/Materials/FX_*.mat (additive for fire/lightning/magic, alpha blend for smoke/dust/water)

## Environment Props (14 prefabs)
- Walls: Wall_Short, Wall_Long, Wall_Tall
- Pillars: Pillar_Small, Pillar_Large
- Stairs: Stairs_1x1, Stairs_2x2, Stairs_3x3
- DoorFrame, Torch_Holder, Chest, Pot
- Location: Assets/AzureDreams/Prefabs/Environment/

## Audio (WebGL-Ready OGG + Original WAV)
- Location: Assets/AzureDreams/Audio/bgm/ (91 files) and Assets/AzureDreams/Audio/sfx/ (79 files)
- Files: WAV + OGG (Vorbis compression, spatialized SFX, stereo BGM)
- Import settings: Vorbis, CompressedInMemory (via AudioImportPostprocessor)

## Video
- Location: Assets/AzureDreams/Videos/
- Files: 7 MP4 (H.264 + AAC) — JO001-003, KONAMI, LOP1, LOP2, TOP_WS, converted from AVI

## Shaders
- Location: Assets/AzureDreams/Shaders/
- Files: PsxIndexed.shader, PsxVertexSnapAffine.shader, PsxColorQuantization.shader,
         PsxVertexLit.shader, BuiltinPostProcess.shader, PsxIndexed.shader

## Spriters Resource (109 PNGs)
- Location: Assets/AzureDreams/SpritersResource/
- Tower floors: 1F-2F through 38F-39F (20 textures, 256×256 RGBA)
- Characters: Koh, Selfi, Ghosh, Nico, Weedy, Jorda, Guy, Wreath, Young Koh, Young Nico, Cherrl, Chappie
- Monsters: Beldo, Kewne, Trolls
- UI: Fonts, Minimap, OpenBag
- Miscellaneous: Angel, Cat & Bird, Credits, Mugshots, Pots/Table, Dishes, Title Screen, Monster Portraits, Koh Status Faces, Tower Elevator
- Familiar Portraits: 1-50

## Tower Floor Themes (5 themes)
| Theme | Floors | Floor Material | Wall Material | Sky Material |
|-------|--------|----------------|---------------|--------------|
| StoneDungeon | 1-10 | Floor_StoneDungeon.mat | Wall_StoneDungeon.mat | Sky_StoneDungeon.mat |
| CrystalCave | 11-20 | Floor_CrystalCave.mat | Wall_CrystalCave.mat | Sky_CrystalCave.mat |
| LavaChamber | 21-30 | Floor_LavaChamber.mat | Wall_LavaChamber.mat | Sky_LavaChamber.mat |
| IcePalace | 31-40 | Floor_IcePalace.mat | Wall_IcePalace.mat | Sky_IcePalace.mat |
| DarkTower | 41+ | Floor_DarkTower.mat | Wall_DarkTower.mat | Sky_DarkTower.mat |

## Floor Tileset (proprietary 0x29 format)
- OBJ: Assets/AzureDreams/Models/floor_tileset.obj (165v, 22n, 582f)
- Prefab variants: 5 themes in Assets/AzureDreams/Prefabs/Ground/Floor_Tileset_*.prefab
- Raw data: extracted_assets/verified_decode/report_verification/floor_tileset_type29_decoded.bin (46,951 bytes)

## FX Effects (6 particle systems)
- Prefabs: Assets/AzureDreams/Prefabs/FX/FX_Fire.prefab, FX_Smoke.prefab, FX_Dust.prefab,
  FX_Lightning.prefab, FX_Water.prefab, FX_Magic.prefab
- Materials: Assets/AzureDreams/Materials/FX_*.mat (additive for fire/lightning/magic, alpha blend for smoke/dust/water)

## Environment Props (14 prefabs)
- Walls: Wall_Short, Wall_Long, Wall_Tall
- Pillars: Pillar_Small, Pillar_Large
- Stairs: Stairs_1x1, Stairs_2x2, Stairs_3x3
- DoorFrame, Torch_Holder, Chest, Pot
- Location: Assets/AzureDreams/Prefabs/Environment/

## Audio (WebGL-Ready OGG + Original WAV)
- Location: Assets/AzureDreams/Audio/bgm/ (91 files) and Assets/AzureDreams/Audio/sfx/ (79 files)
- Files: WAV + OGG (Vorbis compression, spatialized SFX, stereo BGM)
- Import settings: Vorbis, CompressedInMemory (via AudioImportPostprocessor)

## Video
- Location: Assets/AzureDreams/Videos/
- Files: 7 MP4 (H.264 + AAC) — JO001-003, KONAMI, LOP1, LOP2, TOP_WS, converted from AVI

## Shaders
- Location: Assets/AzureDreams/Shaders/
- Files: PsxIndexed.shader, PsxVertexSnapAffine.shader, PsxColorQuantization.shader,
         PsxVertexLit.shader, BuiltinPostProcess.shader, PsxIndexed.shader

## Spriters Resource (109 PNGs)
- Location: Assets/AzureDreams/SpritersResource/
- Tower floors: 1F-2F through 38F-39F (20 textures, 256×256 RGBA)
- Characters: Koh, Selfi, Ghosh, Nico, Weedy, Jorda, Guy, Wreath, Young Koh, Young Nico, Cherrl, Chappie
- Monsters: Beldo, Kewne, Trolls
- UI: Fonts, Minimap, OpenBag
- Miscellaneous: Angel, Cat & Bird, Credits, Mugshots, Pots/Table, Dishes, Title Screen, Monster Portraits, Koh Status Faces, Tower Elevator
- Familiar Portraits: 1-50

## Tower Floor Themes (5 themes)
| Theme | Floors | Floor Material | Wall Material | Sky Material |
|-------|--------|----------------|---------------|--------------|
| StoneDungeon | 1-10 | Floor_StoneDungeon.mat | Wall_StoneDungeon.mat | Sky_StoneDungeon.mat |
| CrystalCave | 11-20 | Floor_CrystalCave.mat | Wall_CrystalCave.mat | Sky_CrystalCave.mat |
| LavaChamber | 21-30 | Floor_LavaChamber.mat | Wall_LavaChamber.mat | Sky_LavaChamber.mat |
| IcePalace | 31-40 | Floor_IcePalace.mat | Wall_IcePalace.mat | Sky_IcePalace.mat |
| DarkTower | 41+ | Floor_DarkTower.mat | Wall_DarkTower.mat | Sky_DarkTower.mat |

## Floor Tileset (proprietary 0x29 format)
- OBJ: Assets/AzureDreams/Models/floor_tileset.obj (165v, 22n, 582f)
- Prefab variants: 5 themes in Assets/AzureDreams/Prefabs/Ground/Floor_Tileset_*.prefab
- Raw data: extracted_assets/verified_decode/report_verification/floor_tileset_type29_decoded.bin (46,951 bytes)

## FX Effects (6 particle systems)
- Prefabs: Assets/AzureDreams/Prefabs/FX/FX_Fire.prefab, FX_Smoke.prefab, FX_Dust.prefab,
  FX_Lightning.prefab, FX_Water.prefab, FX_Magic.prefab
- Materials: Assets/AzureDreams/Materials/FX_*.mat (additive for fire/lightning/magic, alpha blend for smoke/dust/water)

## Environment Props (14 prefabs)
- Walls: Wall_Short, Wall_Long, Wall_Tall
- Pillars: Pillar_Small, Pillar_Large
- Stairs: Stairs_1x1, Stairs_2x2, Stairs_3x3
- DoorFrame, Torch_Holder, Chest, Pot
- Location: Assets/AzureDreams/Prefabs/Environment/

## Audio (WebGL-Ready OGG + Original WAV)
- Location: Assets/AzureDreams/Audio/bgm/ (91 files) and Assets/AzureDreams/Audio/sfx/ (79 files)
- Files: WAV + OGG (Vorbis compression, spatialized SFX, stereo BGM)
- Import settings: Vorbis, CompressedInMemory (via AudioImportPostprocessor)

## Video
- Location: Assets/AzureDreams/Videos/
- Files: 7 MP4 (H.264 + AAC) — JO001-003, KONAMI, LOP1, LOP2, TOP_WS, converted from AVI

## Shaders
- Location: Assets/AzureDreams/Shaders/
- Files: PsxIndexed.shader, PsxVertexSnapAffine.shader, PsxColorQuantization.shader,
         PsxVertexLit.shader, BuiltinPostProcess.shader, PsxIndexed.shader

## Spriters Resource (109 PNGs)
- Location: Assets/AzureDreams/SpritersResource/
- Tower floors: 1F-2F through 38F-39F (20 textures, 256×256 RGBA)
- Characters: Koh, Selfi, Ghosh, Nico, Weedy, Jorda, Guy, Wreath, Young Koh, Young Nico, Cherrl, Chappie
- Monsters: Beldo, Kewne, Trolls
- UI: Fonts, Minimap, OpenBag
- Miscellaneous: Angel, Cat & Bird, Credits, Mugshots, Pots/Table, Dishes, Title Screen, Monster Portraits, Koh Status Faces, Tower Elevator
- Familiar Portraits: 1-50

## Tower Floor Themes (5 themes)
| Theme | Floors | Floor Material | Wall Material | Sky Material |
|-------|--------|----------------|---------------|--------------|
| StoneDungeon | 1-10 | Floor_StoneDungeon.mat | Wall_StoneDungeon.mat | Sky_StoneDungeon.mat |
| CrystalCave | 11-20 | Floor_CrystalCave.mat | Wall_CrystalCave.mat | Sky_CrystalCave.mat |
| LavaChamber | 21-30 | Floor_LavaChamber.mat | Wall_LavaChamber.mat | Sky_LavaChamber.mat |
| IcePalace | 31-40 | Floor_IcePalace.mat | Wall_IcePalace.mat | Sky_IcePalace.mat |
| DarkTower | 41+ | Floor_DarkTower.mat | Wall_DarkTower.mat | Sky_DarkTower.mat |

## Floor Tileset (proprietary 0x29 format)
- OBJ: Assets/AzureDreams/Models/floor_tileset.obj (165v, 22n, 582f)
- Prefab variants: 5 themes in Assets/AzureDreams/Prefabs/Ground/Floor_Tileset_*.prefab
- Raw data: extracted_assets/verified_decode/report_verification/floor_tileset_type29_decoded.bin (46,951 bytes)

## FX Effects (6 particle systems)
- Prefabs: Assets/AzureDreams/Prefabs/FX/FX_Fire.prefab, FX_Smoke.prefab, FX_Dust.prefab,
  FX_Lightning.prefab, FX_Water.prefab, FX_Magic.prefab
- Materials: Assets/AzureDreams/Materials/FX_*.mat (additive for fire/lightning/magic, alpha blend for smoke/dust/water)

## Environment Props (14 prefabs)
- Walls: Wall_Short, Wall_Long, Wall_Tall
- Pillars: Pillar_Small, Pillar_Large
- Stairs: Stairs_1x1, Stairs_2x2, Stairs_3x3
- DoorFrame, Torch_Holder, Chest, Pot
- Location: Assets/AzureDreams/Prefabs/Environment/

## Audio (WebGL-Ready OGG + Original WAV)
- Location: Assets/AzureDreams/Audio/bgm/ (91 files) and Assets/AzureDreams/Audio/sfx/ (79 files)
- Files: WAV + OGG (Vorbis compression, spatialized SFX, stereo BGM)
- Import settings: Vorbis, CompressedInMemory (via AudioImportPostprocessor)

## Video
- Location: Assets/AzureDreams/Videos/
- Files: 7 MP4 (H.264 + AAC) — JO001-003, KONAMI, LOP1, LOP2, TOP_WS, converted from AVI

## Shaders
- Location: Assets/AzureDreams/Shaders/
- Files: PsxIndexed.shader, PsxVertexSnapAffine.shader, PsxColorQuantization.shader,
         PsxVertexLit.shader, BuiltinPostProcess.shader, PsxIndexed.shader

## Spriters Resource (109 PNGs)
- Location: Assets/AzureDreams/SpritersResource/
- Tower floors: 1F-2F through 38F-39F (20 textures, 256×256 RGBA)
- Characters: Koh, Selfi, Ghosh, Nico, Weedy, Jorda, Guy, Wreath, Young Koh, Young Nico, Cherrl, Chappie
- Monsters: Beldo, Kewne, Trolls
- UI: Fonts, Minimap, OpenBag
- Miscellaneous: Angel, Cat & Bird, Credits, Mugshots, Pots/Table, Dishes, Title Screen, Monster Portraits, Koh Status Faces, Tower Elevator
- Familiar Portraits: 1-50

## Tower Floor Themes (5 themes)
| Theme | Floors | Floor Material | Wall Material | Sky Material |
|-------|--------|----------------|---------------|--------------|
| StoneDungeon | 1-10 | Floor_StoneDungeon.mat | Wall_StoneDungeon.mat | Sky_StoneDungeon.mat |
| CrystalCave | 11-20 | Floor_CrystalCave.mat | Wall_CrystalCave.mat | Sky_CrystalCave.mat |
| LavaChamber | 21-30 | Floor_LavaChamber.mat | Wall_LavaChamber.mat | Sky_LavaChamber.mat |
| IcePalace | 31-40 | Floor_IcePalace.mat | Wall_IcePalace.mat | Sky_IcePalace.mat |
| DarkTower | 41+ | Floor_DarkTower.mat | Wall_DarkTower.mat | Sky_DarkTower.mat |

## Floor Tileset (proprietary 0x29 format)
- OBJ: Assets/AzureDreams/Models/floor_tileset.obj (165v, 22n, 582f)
- Prefab variants: 5 themes in Assets/AzureDreams/Prefabs/Ground/Floor_Tileset_*.prefab
- Raw data: extracted_assets/verified_decode/report_verification/floor_tileset_type29_decoded.bin (46,951 bytes)

## FX Effects (6 particle systems)
- Prefabs: Assets/AzureDreams/Prefabs/FX/FX_Fire.prefab, FX_Smoke.prefab, FX_Dust.prefab,
  FX_Lightning.prefab, FX_Water.prefab, FX_Magic.prefab
- Materials: Assets/AzureDreams/Materials/FX_*.mat (additive for fire/lightning/magic, alpha blend for smoke/dust/water)

## Environment Props (14 prefabs)
- Walls: Wall_Short, Wall_Long, Wall_Tall
- Pillars: Pillar_Small, Pillar_Large
- Stairs: Stairs_1x1, Stairs_2x2, Stairs_3x3
- DoorFrame, Torch_Holder, Chest, Pot
- Location: Assets/AzureDreams/Prefabs/Environment/

## Audio (WebGL-Ready OGG + Original WAV)
- Location: Assets/AzureDreams/Audio/bgm/ (91 files) and Assets/AzureDreams/Audio/sfx/ (79 files)
- Files: WAV + OGG (Vorbis compression, spatialized SFX, stereo BGM)
- Import settings: Vorbis, CompressedInMemory (via AudioImportPostprocessor)

## Video
- Location: Assets/AzureDreams/Videos/
- Files: 7 MP4 (H.264 + AAC) — JO001-003, KONAMI, LOP1, LOP2, TOP_WS, converted from AVI

## Shaders
- Location: Assets/AzureDreams/Shaders/
- Files: PsxIndexed.shader, PsxVertexSnapAffine.shader, PsxColorQuantization.shader,
         PsxVertexLit.shader, BuiltinPostProcess.shader, PsxIndexed.shader

## Spriters Resource (109 PNGs)
- Location: Assets/AzureDreams/SpritersResource/
- Tower floors: 1F-2F through 38F-39F (20 textures, 256×256 RGBA)
- Characters: Koh, Selfi, Ghosh, Nico, Weedy, Jorda, Guy, Wreath, Young Koh, Young Nico, Cherrl, Chappie
- Monsters: Beldo, Kewne, Trolls
- UI: Fonts, Minimap, OpenBag
- Miscellaneous: Angel, Cat & Bird, Credits, Mugshots, Pots/Table, Dishes, Title Screen, Monster Portraits, Koh Status Faces, Tower Elevator
- Familiar Portraits: 1-50

## Tower Floor Themes (5 themes)
| Theme | Floors | Floor Material | Wall Material | Sky Material |
|-------|--------|----------------|---------------|--------------|
| StoneDungeon | 1-10 | Floor_StoneDungeon.mat | Wall_StoneDungeon.mat | Sky_StoneDungeon.mat |
| CrystalCave | 11-20 | Floor_CrystalCave.mat | Wall_CrystalCave.mat | Sky_CrystalCave.mat |
| LavaChamber | 21-30 | Floor_LavaChamber.mat | Wall_LavaChamber.mat | Sky_LavaChamber.mat |
| IcePalace | 31-40 | Floor_IcePalace.mat | Wall_IcePalace.mat | Sky_IcePalace.mat |
| DarkTower | 41+ | Floor_DarkTower.mat | Wall_DarkTower.mat | Sky_DarkTower.mat |

## Floor Tileset (proprietary 0x29 format)
- OBJ: Assets/AzureDreams/Models/floor_tileset.obj (165v, 22n, 582f)
- Prefab variants: 5 themes in Assets/AzureDreams/Prefabs/Ground/Floor_Tileset_*.prefab
- Raw data: extracted_assets/verified_decode/report_verification/floor_tileset_type29_decoded.bin (46,951 bytes)

## FX Effects (6 particle systems)
- Prefabs: Assets/AzureDreams/Prefabs/FX/FX_Fire.prefab, FX_Smoke.prefab, FX_Dust.prefab,
  FX_Lightning.prefab, FX_Water.prefab, FX_Magic.prefab
- Materials: Assets/AzureDreams/Materials/FX_*.mat (additive for fire/lightning/magic, alpha blend for smoke/dust/water)

## Environment Props (14 prefabs)
- Walls: Wall_Short, Wall_Long, Wall_Tall
- Pillars: Pillar_Small, Pillar_Large
- Stairs: Stairs_1x1, Stairs_2x2, Stairs_3x3
- DoorFrame, Torch_Holder, Chest, Pot
- Location: Assets/AzureDreams/Prefabs/Environment/

## Audio (WebGL-Ready OGG + Original WAV)
- Location: Assets/AzureDreams/Audio/bgm/ (91 files) and Assets/AzureDreams/Audio/sfx/ (79 files)
- Files: WAV + OGG (Vorbis compression, spatialized SFX, stereo BGM)
- Import settings: Vorbis, CompressedInMemory (via AudioImportPostprocessor)

## Video
- Location: Assets/AzureDreams/Videos/
- Files: 7 MP4 (H.264 + AAC) — JO001-003, KONAMI, LOP1, LOP2, TOP_WS, converted from AVI

## Shaders
- Location: Assets/AzureDreams/Shaders/
- Files: PsxIndexed.shader, PsxVertexSnapAffine.shader, PsxColorQuantization.shader,
         PsxVertexLit.shader, BuiltinPostProcess.shader, PsxIndexed.shader

## Spriters Resource (109 PNGs)
- Location: Assets/AzureDreams/SpritersResource/
- Tower floors: 1F-2F through 38F-39F (20 textures, 256×256 RGBA)
- Characters: Koh, Selfi, Ghosh, Nico, Weedy, Jorda, Guy, Wreath, Young Koh, Young Nico, Cherrl, Chappie
- Monsters: Beldo, Kewne, Trolls
- UI: Fonts, Minimap, OpenBag
- Miscellaneous: Angel, Cat & Bird, Credits, Mugshots, Pots/Table, Dishes, Title Screen, Monster Portraits, Koh Status Faces, Tower Elevator
- Familiar Portraits: 1-50

## Tower Floor Themes (5 themes)
| Theme | Floors | Floor Material | Wall Material | Sky Material |
|-------|--------|----------------|---------------|--------------|
| StoneDungeon | 1-10 | Floor_StoneDungeon.mat | Wall_StoneDungeon.mat | Sky_StoneDungeon.mat |
| CrystalCave | 11-20 | Floor_CrystalCave.mat | Wall_CrystalCave.mat | Sky_CrystalCave.mat |
| LavaChamber | 21-30 | Floor_LavaChamber.mat | Wall_LavaChamber.mat | Sky_LavaChamber.mat |
| IcePalace | 31-40 | Floor_IcePalace.mat | Wall_IcePalace.mat | Sky_IcePalace.mat |
| DarkTower | 41+ | Floor_DarkTower.mat | Wall_DarkTower.mat | Sky_DarkTower.mat |

## Floor Tileset (proprietary 0x29 format)
- OBJ: Assets/AzureDreams/Models/floor_tileset.obj (165v, 22n, 582f)
- Prefab variants: 5 themes in Assets/AzureDreams/Prefabs/Ground/Floor_Tileset_*.prefab
- Raw data: extracted_assets/verified_decode/report_verification/floor_tileset_type29_decoded.bin (46,951 bytes)

## FX Effects (6 particle systems)
- Prefabs: Assets/AzureDreams/Prefabs/FX/FX_Fire.prefab, FX_Smoke.prefab, FX_Dust.prefab,
  FX_Lightning.prefab, FX_Water.prefab, FX_Magic.prefab
- Materials: Assets/AzureDreams/Materials/FX_*.mat (additive for fire/lightning/magic, alpha blend for smoke/dust/water)

## Environment Props (14 prefabs)
- Walls: Wall_Short, Wall_Long, Wall_Tall
- Pillars: Pillar_Small, Pillar_Large
- Stairs: Stairs_1x1, Stairs_2x2, Stairs_3x3
- DoorFrame, Torch_Holder, Chest, Pot
- Location: Assets/AzureDreams/Prefabs/Environment/

## Audio (WebGL-Ready OGG + Original WAV)
- Location: Assets/AzureDreams/Audio/bgm/ (91 files) and Assets/AzureDreams/Audio/sfx/ (79 files)
- Files: WAV + OGG (Vorbis compression, spatialized SFX, stereo BGM)
- Import settings: Vorbis, CompressedInMemory (via AudioImportPostprocessor)

## Video
- Location: Assets/AzureDreams/Videos/
- Files: 7 MP4 (H.264 + AAC) — JO001-003, KONAMI, LOP1, LOP2, TOP_WS, converted from AVI

## Shaders
- Location: Assets/AzureDreams/Shaders/
- Files: PsxIndexed.shader, PsxVertexSnapAffine.shader, PsxColorQuantization.shader,
         PsxVertexLit.shader, BuiltinPostProcess.shader, PsxIndexed.shader

## Spriters Resource (109 PNGs)
- Location: Assets/AzureDreams/SpritersResource/
- Tower floors: 1F-2F through 38F-39F (20 textures, 256×256 RGBA)
- Characters: Koh, Selfi, Ghosh, Nico, Weedy, Jorda, Guy, Wreath, Young Koh, Young Nico, Cherrl, Chappie
- Monsters: Beldo, Kewne, Trolls
- UI: Fonts, Minimap, OpenBag
- Miscellaneous: Angel, Cat & Bird, Credits, Mugshots, Pots/Table, Dishes, Title Screen, Monster Portraits, Koh Status Faces, Tower Elevator
- Familiar Portraits: 1-50

## Tower Floor Themes (5 themes)
| Theme | Floors | Floor Material | Wall Material | Sky Material |
|-------|--------|----------------|---------------|--------------|
| StoneDungeon | 1-10 | Floor_StoneDungeon.mat | Wall_StoneDungeon.mat | Sky_StoneDungeon.mat |
| CrystalCave | 11-20 | Floor_CrystalCave.mat | Wall_CrystalCave.mat | Sky_CrystalCave.mat |
| LavaChamber | 21-30 | Floor_LavaChamber.mat | Wall_LavaChamber.mat | Sky_LavaChamber.mat |
| IcePalace | 31-40 | Floor_IcePalace.mat | Wall_IcePalace.mat | Sky_IcePalace.mat |
| DarkTower | 41+ | Floor_DarkTower.mat | Wall_DarkTower.mat | Sky_DarkTower.mat |

## Floor Tileset (proprietary 0x29 format)
- OBJ: Assets/AzureDreams/Models/floor_tileset.obj (165v, 22n, 582f)
- Prefab variants: 5 themes in Assets/AzureDreams/Prefabs/Ground/Floor_Tileset_*.prefab
- Raw data: extracted_assets/verified_decode/report_verification/floor_tileset_type29_decoded.bin (46,951 bytes)

## FX Effects (6 particle systems)
- Prefabs: Assets/AzureDreams/Prefabs/FX/FX_Fire.prefab, FX_Smoke.prefab, FX_Dust.prefab,
  FX_Lightning.prefab, FX_Water.prefab, FX_Magic.prefab
- Materials: Assets/AzureDreams/Materials/FX_*.mat (additive for fire/lightning/magic, alpha blend for smoke/dust/water)

## Environment Props (14 prefabs)
- Walls: Wall_Short, Wall_Long, Wall_Tall
- Pillars: Pillar_Small, Pillar_Large
- Stairs: Stairs_1x1, Stairs_2x2, Stairs_3x3
- DoorFrame, Torch_Holder, Chest, Pot
- Location: Assets/AzureDreams/Prefabs/Environment/

## Audio (WebGL-Ready OGG + Original WAV)
- Location: Assets/AzureDreams/Audio/bgm/ (91 files) and Assets/AzureDreams/Audio/sfx/ (79 files)
- Files: WAV + OGG (Vorbis compression, spatialized SFX, stereo BGM)
- Import settings: Vorbis, CompressedInMemory (via AudioImportPostprocessor)

## Video
- Location: Assets/AzureDreams/Videos/
- Files: 7 MP4 (H.264 + AAC) — JO001-003, KONAMI, LOP1, LOP2, TOP_WS, converted from AVI

## Shaders
- Location: Assets/AzureDreams/Shaders/
- Files: PsxIndexed.shader, PsxVertexSnapAffine.shader, PsxColorQuantization.shader,
         PsxVertexLit.shader, BuiltinPostProcess.shader, PsxIndexed.shader

## Spriters Resource (109 PNGs)
- Location: Assets/AzureDreams/SpritersResource/
- Tower floors: 1F-2F through 38F-39F (20 textures, 256×256 RGBA)
- Characters: Koh, Selfi, Ghosh, Nico, Weedy, Jorda, Guy, Wreath, Young Koh, Young Nico, Cherrl, Chappie
- Monsters: Beldo, Kewne, Trolls
- UI: Fonts, Minimap, OpenBag
- Miscellaneous: Angel, Cat & Bird, Credits, Mugshots, Pots/Table, Dishes, Title Screen, Monster Portraits, Koh Status Faces, Tower Elevator
- Familiar Portraits: 1-50

## Tower Floor Themes (5 themes)
| Theme | Floors | Floor Material | Wall Material | Sky Material |
|-------|--------|----------------|---------------|--------------|
| StoneDungeon | 1-10 | Floor_StoneDungeon.mat | Wall_StoneDungeon.mat | Sky_StoneDungeon.mat |
| CrystalCave | 11-20 | Floor_CrystalCave.mat | Wall_CrystalCave.mat | Sky_CrystalCave.mat |
| LavaChamber | 21-30 | Floor_LavaChamber.mat | Wall_LavaChamber.mat | Sky_LavaChamber.mat |
| IcePalace | 31-40 | Floor_IcePalace.mat | Wall_IcePalace.mat | Sky_IcePalace.mat |
| DarkTower | 41+ | Floor_DarkTower.mat | Wall_DarkTower.mat | Sky_DarkTower.mat |

## Floor Tileset (proprietary 0x29 format)
- OBJ: Assets/AzureDreams/Models/floor_tileset.obj (165v, 22n, 582f)
- Prefab variants: 5 themes in Assets/AzureDreams/Prefabs/Ground/Floor_Tileset_*.prefab
- Raw data: extracted_assets/verified_decode/report_verification/floor_tileset_type29_decoded.bin (46,951 bytes)

## FX Effects (6 particle systems)
- Prefabs: Assets/AzureDreams/Prefabs/FX/FX_Fire.prefab, FX_Smoke.prefab, FX_Dust.prefab,
  FX_Lightning.prefab, FX_Water.prefab, FX_Magic.prefab
- Materials: Assets/AzureDreams/Materials/FX_*.mat (additive for fire/lightning/magic, alpha blend for smoke/dust/water)

## Environment Props (14 prefabs)
- Walls: Wall_Short, Wall_Long, Wall_Tall
- Pillars: Pillar_Small, Pillar_Large
- Stairs: Stairs_1x1, Stairs_2x2, Stairs_3x3
- DoorFrame, Torch_Holder, Chest, Pot
- Location: Assets/AzureDreams/Prefabs/Environment/

## Audio (WebGL-Ready OGG + Original WAV)
- Location: Assets/AzureDreams/Audio/bgm/ (91 files) and Assets/AzureDreams/Audio/sfx/ (79 files)
- Files: WAV + OGG (Vorbis compression, spatialized SFX, stereo BGM)
- Import settings: Vorbis, CompressedInMemory (via AudioImportPostprocessor)

## Video
- Location: Assets/AzureDreams/Videos/
- Files: 7 MP4 (H.264 + AAC) — JO001-003, KONAMI, LOP1, LOP2, TOP_WS, converted from AVI

## Shaders
- Location: Assets/AzureDreams/Shaders/
- Files: PsxIndexed.shader, PsxVertexSnapAffine.shader, PsxColorQuantization.shader,
         PsxVertexLit.shader, BuiltinPostProcess.shader, PsxIndexed.shader

## Spriters Resource (109 PNGs)
- Location: Assets/AzureDreams/SpritersResource/
- Tower floors: 1F-2F through 38F-39F (20 textures, 256×256 RGBA)
- Characters: Koh, Selfi, Ghosh, Nico, Weedy, Jorda, Guy, Wreath, Young Koh, Young Nico, Cherrl, Chappie
- Monsters: Beldo, Kewne, Trolls
- UI: Fonts, Minimap, OpenBag
- Miscellaneous: Angel, Cat & Bird, Credits, Mugshots, Pots/Table, Dishes, Title Screen, Monster Portraits, Koh Status Faces, Tower Elevator
- Familiar Portraits: 1-50

## Tower Floor Themes (5 themes)
| Theme | Floors | Floor Material | Wall Material | Sky Material |
|-------|--------|----------------|---------------|--------------|
| StoneDungeon | 1-10 | Floor_StoneDungeon.mat | Wall_StoneDungeon.mat | Sky_StoneDungeon.mat |
| CrystalCave | 11-20 | Floor_CrystalCave.mat | Wall_CrystalCave.mat | Sky_CrystalCave.mat |
| LavaChamber | 21-30 | Floor_LavaChamber.mat | Wall_LavaChamber.mat | Sky_LavaChamber.mat |
| IcePalace | 31-40 | Floor_IcePalace.mat | Wall_IcePalace.mat | Sky_IcePalace.mat |
| DarkTower | 41+ | Floor_DarkTower.mat | Wall_DarkTower.mat | Sky_DarkTower.mat |

## Floor Tileset (proprietary 0x29 format)
- OBJ: Assets/AzureDreams/Models/floor_tileset.obj (165v, 22n, 582f)
- Prefab variants: 5 themes in Assets/AzureDreams/Prefabs/Ground/Floor_Tileset_*.prefab
- Raw data: extracted_assets/verified_decode/report_verification/floor_tileset_type29_decoded.bin (46,951 bytes)

## FX Effects (6 particle systems)
- Prefabs: Assets/AzureDreams/Prefabs/FX/FX_Fire.prefab, FX_Smoke.prefab, FX_Dust.prefab,
  FX_Lightning.prefab, FX_Water.prefab, FX_Magic.prefab
- Materials: Assets/AzureDreams/Materials/FX_*.mat (additive for fire/lightning/magic, alpha blend for smoke/dust/water)

## Environment Props (14 prefabs)
- Walls: Wall_Short, Wall_Long, Wall_Tall
- Pillars: Pillar_Small, Pillar_Large
- Stairs: Stairs_1x1, Stairs_2x2, Stairs_3x3
- DoorFrame, Torch_Holder, Chest, Pot
- Location: Assets/AzureDreams/Prefabs/Environment/

## Audio (WebGL-Ready OGG + Original WAV)
- Location: Assets/AzureDreams/Audio/bgm/ (91 files) and Assets/AzureDreams/Audio/sfx/ (79 files)
- Files: WAV + OGG (Vorbis compression, spatialized SFX, stereo BGM)
- Import settings: Vorbis, CompressedInMemory (via AudioImportPostprocessor)

## Video
- Location: Assets/AzureDreams/Videos/
- Files: 7 MP4 (H.264 + AAC) — JO001-003, KONAMI, LOP1, LOP2, TOP_WS, converted from AVI

## Shaders
- Location: Assets/AzureDreams/Shaders/
- Files: PsxIndexed.shader, PsxVertexSnapAffine.shader, PsxColorQuantization.shader,
         PsxVertexLit.shader, BuiltinPostProcess.shader, PsxIndexed.shader

## Spriters Resource (109 PNGs)
- Location: Assets/AzureDreams/SpritersResource/
- Tower floors: 1F-2F through 38F-39F (20 textures, 256×256 RGBA)
- Characters: Koh, Selfi, Ghosh, Nico, Weedy, Jorda, Guy, Wreath, Young Koh, Young Nico, Cherrl, Chappie
- Monsters: Beldo, Kewne, Trolls
- UI: Fonts, Minimap, OpenBag
- Miscellaneous: Angel, Cat & Bird, Credits, Mugshots, Pots/Table, Dishes, Title Screen, Monster Portraits, Koh Status Faces, Tower Elevator
- Familiar Portraits: 1-50

## Tower Floor Themes (5 themes)
| Theme | Floors | Floor Material | Wall Material | Sky Material |
|-------|--------|----------------|---------------|--------------|
| StoneDungeon | 1-10 | Floor_StoneDungeon.mat | Wall_StoneDungeon.mat | Sky_StoneDungeon.mat |
| CrystalCave | 11-20 | Floor_CrystalCave.mat | Wall_CrystalCave.mat | Sky_CrystalCave.mat |
| LavaChamber | 21-30 | Floor_LavaChamber.mat | Wall_LavaChamber.mat | Sky_LavaChamber.mat |
| IcePalace | 31-40 | Floor_IcePalace.mat | Wall_IcePalace.mat | Sky_IcePalace.mat |
| DarkTower | 41+ | Floor_DarkTower.mat | Wall_DarkTower.mat | Sky_DarkTower.mat |

## Floor Tileset (proprietary 0x29 format)
- OBJ: Assets/AzureDreams/Models/floor_tileset.obj (165v, 22n, 582f)
- Prefab variants: 5 themes in Assets/AzureDreams/Prefabs/Ground/Floor_Tileset_*.prefab
- Raw data: extracted_assets/verified_decode/report_verification/floor_tileset_type29_decoded.bin (46,951 bytes)

## FX Effects (6 particle systems)
- Prefabs: Assets/AzureDreams/Prefabs/FX/FX_Fire.prefab, FX_Smoke.prefab, FX_Dust.prefab,
  FX_Lightning.prefab, FX_Water.prefab, FX_Magic.prefab
- Materials: Assets/AzureDreams/Materials/FX_*.mat (additive for fire/lightning/magic, alpha blend for smoke/dust/water)

## Environment Props (14 prefabs)
- Walls: Wall_Short, Wall_Long, Wall_Tall
- Pillars: Pillar_Small, Pillar_Large
- Stairs: Stairs_1x1, Stairs_2x2, Stairs_3x3
- DoorFrame, Torch_Holder, Chest, Pot
- Location: Assets/AzureDreams/Prefabs/Environment/

## Audio (WebGL-Ready OGG + Original WAV)
- Location: Assets/AzureDreams/Audio/bgm/ (91 files) and Assets/AzureDreams/Audio/sfx/ (79 files)
- Files: WAV + OGG (Vorbis compression, spatialized SFX, stereo BGM)
- Import settings: Vorbis, CompressedInMemory (via AudioImportPostprocessor)

## Video
- Location: Assets/AzureDreams/Videos/
- Files: 7 MP4 (H.264 + AAC) — JO001-003, KONAMI, LOP1, LOP2, TOP_WS, converted from AVI

## Shaders
- Location: Assets/AzureDreams/Shaders/
- Files: PsxIndexed.shader, PsxVertexSnapAffine.shader, PsxColorQuantization.shader,
         PsxVertexLit.shader, BuiltinPostProcess.shader, PsxIndexed.shader

## Spriters Resource (109 PNGs)
- Location: Assets/AzureDreams/SpritersResource/
- Tower floors: 1F-2F through 38F-39F (20 textures, 256×256 RGBA)
- Characters: Koh, Selfi, Ghosh, Nico, Weedy, Jorda, Guy, Wreath, Young Koh, Young Nico, Cherrl, Chappie
- Monsters: Beldo, Kewne, Trolls
- UI: Fonts, Minimap, OpenBag
- Miscellaneous: Angel, Cat & Bird, Credits, Mugshots, Pots/Table, Dishes, Title Screen, Monster Portraits, Koh Status Faces, Tower Elevator
- Familiar Portraits: 1-50

## Tower Floor Themes (5 themes)
| Theme | Floors | Floor Material | Wall Material | Sky Material |
|-------|--------|----------------|---------------|--------------|
| StoneDungeon | 1-10 | Floor_StoneDungeon.mat | Wall_StoneDungeon.mat | Sky_StoneDungeon.mat |
| CrystalCave | 11-20 | Floor_CrystalCave.mat | Wall_CrystalCave.mat | Sky_CrystalCave.mat |
| LavaChamber | 21-30 | Floor_LavaChamber.mat | Wall_LavaChamber.mat | Sky_LavaChamber.mat |
| IcePalace | 31-40 | Floor_IcePalace.mat | Wall_IcePalace.mat | Sky_IcePalace.mat |
| DarkTower | 41+ | Floor_DarkTower.mat | Wall_DarkTower.mat | Sky_DarkTower.mat |

## Floor Tileset (proprietary 0x29 format)
- OBJ: Assets/AzureDreams/Models/floor_tileset.obj (165v, 22n, 582f)
- Prefab variants: 5 themes in Assets/AzureDreams/Prefabs/Ground/Floor_Tileset_*.prefab
- Raw data: extracted_assets/verified_decode/report_verification/floor_tileset_type29_decoded.bin (46,951 bytes)

## FX Effects (6 particle systems)
- Prefabs: Assets/AzureDreams/Prefabs/FX/FX_Fire.prefab, FX_Smoke.prefab, FX_Dust.prefab,
  FX_Lightning.prefab, FX_Water.prefab, FX_Magic.prefab
- Materials: Assets/AzureDreams/Materials/FX_*.mat (additive for fire/lightning/magic, alpha blend for smoke/dust/water)

## Environment Props (14 prefabs)
- Walls: Wall_Short, Wall_Long, Wall_Tall
- Pillars: Pillar_Small, Pillar_Large
- Stairs: Stairs_1x1, Stairs_2x2, Stairs_3x3
- DoorFrame, Torch_Holder, Chest, Pot
- Location: Assets/AzureDreams/Prefabs/Environment/

## Audio (WebGL-Ready OGG + Original WAV)
- Location: Assets/AzureDreams/Audio/bgm/ (91 files) and Assets/AzureDreams/Audio/sfx/ (79 files)
- Files: WAV + OGG (Vorbis compression, spatialized SFX, stereo BGM)
- Import settings: Vorbis, CompressedInMemory (via AudioImportPostprocessor)

## Video
- Location: Assets/AzureDreams/Videos/
- Files: 7 MP4 (H.264 + AAC) — JO001-003, KONAMI, LOP1, LOP2, TOP_WS, converted from AVI

## Shaders
- Location: Assets/AzureDreams/Shaders/
- Files: PsxIndexed.shader, PsxVertexSnapAffine.shader, PsxColorQuantization.shader,
         PsxVertexLit.shader, BuiltinPostProcess.shader, PsxIndexed.shader

## Spriters Resource (109 PNGs)
- Location: Assets/AzureDreams/SpritersResource/
- Tower floors: 1F-2F through 38F-39F (20 textures, 256×256 RGBA)
- Characters: Koh, Selfi, Ghosh, Nico, Weedy, Jorda, Guy, Wreath, Young Koh, Young Nico, Cherrl, Chappie
- Monsters: Beldo, Kewne, Trolls
- UI: Fonts, Minimap, OpenBag
- Miscellaneous: Angel, Cat & Bird, Credits, Mugshots, Pots/Table, Dishes, Title Screen, Monster Portraits, Koh Status Faces, Tower Elevator
- Familiar Portraits: 1-50

## Tower Floor Themes (5 themes)
| Theme | Floors | Floor Material | Wall Material | Sky Material |
|-------|--------|----------------|---------------|--------------|
| StoneDungeon | 1-10 | Floor_StoneDungeon.mat | Wall_StoneDungeon.mat | Sky_StoneDungeon.mat |
| CrystalCave | 11-20 | Floor_CrystalCave.mat | Wall_CrystalCave.mat | Sky_CrystalCave.mat |
| LavaChamber | 21-30 | Floor_LavaChamber.mat | Wall_LavaChamber.mat | Sky_LavaChamber.mat |
| IcePalace | 31-40 | Floor_IcePalace.mat | Wall_IcePalace.mat | Sky_IcePalace.mat |
| DarkTower | 41+ | Floor_DarkTower.mat | Wall_DarkTower.mat | Sky_DarkTower.mat |

## Floor Tileset (proprietary 0x29 format)
- OBJ: Assets/AzureDreams/Models/floor_tileset.obj (165v, 22n, 582f)
- Prefab variants: 5 themes in Assets/AzureDreams/Prefabs/Ground/Floor_Tileset_*.prefab
- Raw data: extracted_assets/verified_decode/report_verification/floor_tileset_type29_decoded.bin (46,951 bytes)

## FX Effects (6 particle systems)
- Prefabs: Assets/AzureDreams/Prefabs/FX/FX_Fire.prefab, FX_Smoke.prefab, FX_Dust.prefab,
  FX_Lightning.prefab, FX_Water.prefab, FX_Magic.prefab
- Materials: Assets/AzureDreams/Materials/FX_*.mat (additive for fire/lightning/magic, alpha blend for smoke/dust/water)

## Environment Props (14 prefabs)
- Walls: Wall_Short, Wall_Long, Wall_Tall
- Pillars: Pillar_Small, Pillar_Large
- Stairs: Stairs_1x1, Stairs_2x2, Stairs_3x3
- DoorFrame, Torch_Holder, Chest, Pot
- Location: Assets/AzureDreams/Prefabs/Environment/

## Audio (WebGL-Ready OGG + Original WAV)
- Location: Assets/AzureDreams/Audio/bgm/ (91 files) and Assets/AzureDreams/Audio/sfx/ (79 files)
- Files: WAV + OGG (Vorbis compression, spatialized SFX, stereo BGM)
- Import settings: Vorbis, CompressedInMemory (via AudioImportPostprocessor)

## Video
- Location: Assets/AzureDreams/Videos/
- Files: 7 MP4 (H.264 + AAC) — JO001-003, KONAMI, LOP1, LOP2, TOP_WS, converted from AVI

## Shaders
- Location: Assets/AzureDreams/Shaders/
- Files: PsxIndexed.shader, PsxVertexSnapAffine.shader, PsxColorQuantization.shader,
         PsxVertexLit.shader, BuiltinPostProcess.shader, PsxIndexed.shader

## Spriters Resource (109 PNGs)
- Location: Assets/AzureDreams/SpritersResource/
- Tower floors: 1F-2F through 38F-39F (20 textures, 256×256 RGBA)
- Characters: Koh, Selfi, Ghosh, Nico, Weedy, Jorda, Guy, Wreath, Young Koh, Young Nico, Cherrl, Chappie
- Monsters: Beldo, Kewne, Trolls
- UI: Fonts, Minimap, OpenBag
- Miscellaneous: Angel, Cat & Bird, Credits, Mugshots, Pots/Table, Dishes, Title Screen, Monster Portraits, Koh Status Faces, Tower Elevator
- Familiar Portraits: 1-50

## Tower Floor Themes (5 themes)
| Theme | Floors | Floor Material | Wall Material | Sky Material |
|-------|--------|----------------|---------------|--------------|
| StoneDungeon | 1-10 | Floor_StoneDungeon.mat | Wall_StoneDungeon.mat | Sky_StoneDungeon.mat |
| CrystalCave | 11-20 | Floor_CrystalCave.mat | Wall_CrystalCave.mat | Sky_CrystalCave.mat |
| LavaChamber | 21-30 | Floor_LavaChamber.mat | Wall_LavaChamber.mat | Sky_LavaChamber.mat |
| IcePalace | 31-40 | Floor_IcePalace.mat | Wall_IcePalace.mat | Sky_IcePalace.mat |
| DarkTower | 41+ | Floor_DarkTower.mat | Wall_DarkTower.mat | Sky_DarkTower.mat |

## Floor Tileset (proprietary 0x29 format)
- OBJ: Assets/AzureDreams/Models/floor_tileset.obj (165v, 22n, 582f)
- Prefab variants: 5 themes in Assets/AzureDreams/Prefabs/Ground/Floor_Tileset_*.prefab
- Raw data: extracted_assets/verified_decode/report_verification/floor_tileset_type29_decoded.bin (46,951 bytes)

## FX Effects (6 particle systems)
- Prefabs: Assets/AzureDreams/Prefabs/FX/FX_Fire.prefab, FX_Smoke.prefab, FX_Dust.prefab,
  FX_Lightning.prefab, FX_Water.prefab, FX_Magic.prefab
- Materials: Assets/AzureDreams/Materials/FX_*.mat (additive for fire/lightning/magic, alpha blend for smoke/dust/water)

## Environment Props (14 prefabs)
- Walls: Wall_Short, Wall_Long, Wall_Tall
- Pillars: Pillar_Small, Pillar_Large
- Stairs: Stairs_1x1, Stairs_2x2, Stairs_3x3
- DoorFrame, Torch_Holder, Chest, Pot
- Location: Assets/AzureDreams/Prefabs/Environment/

## Audio (WebGL-Ready OGG + Original WAV)
- Location: Assets/AzureDreams/Audio/bgm/ (91 files) and Assets/AzureDreams/Audio/sfx/ (79 files)
- Files: WAV + OGG (Vorbis compression, spatialized SFX, stereo BGM)
- Import settings: Vorbis, CompressedInMemory (via AudioImportPostprocessor)

## Video
- Location: Assets/AzureDreams/Videos/
- Files: 7 MP4 (H.264 + AAC) — JO001-003, KONAMI, LOP1, LOP2, TOP_WS, converted from AVI

## Shaders
- Location: Assets/AzureDreams/Shaders/
- Files: PsxIndexed.shader, PsxVertexSnapAffine.shader, PsxColorQuantization.shader,
         PsxVertexLit.shader, BuiltinPostProcess.shader, PsxIndexed.shader

## Spriters Resource (109 PNGs)
- Location: Assets/AzureDreams/SpritersResource/
- Tower floors: 1F-2F through 38F-39F (20 textures, 256×256 RGBA)
- Characters: Koh, Selfi, Ghosh, Nico, Weedy, Jorda, Guy, Wreath, Young Koh, Young Nico, Cherrl, Chappie
- Monsters: Beldo, Kewne, Trolls
- UI: Fonts, Minimap, OpenBag
- Miscellaneous: Angel, Cat & Bird, Credits, Mugshots, Pots/Table, Dishes, Title Screen, Monster Portraits, Koh Status Faces, Tower Elevator
- Familiar Portraits: 1-50

## Tower Floor Themes (5 themes)
| Theme | Floors | Floor Material | Wall Material | Sky Material |
|-------|--------|----------------|---------------|--------------|
| StoneDungeon | 1-10 | Floor_StoneDungeon.mat | Wall_StoneDungeon.mat | Sky_StoneDungeon.mat |
| CrystalCave | 11-20 | Floor_CrystalCave.mat | Wall_CrystalCave.mat | Sky_CrystalCave.mat |
| LavaChamber | 21-30 | Floor_LavaChamber.mat | Wall_LavaChamber.mat | Sky_LavaChamber.mat |
| IcePalace | 31-40 | Floor_IcePalace.mat | Wall_IcePalace.mat | Sky_IcePalace.mat |
| DarkTower | 41+ | Floor_DarkTower.mat | Wall_DarkTower.mat | Sky_DarkTower.mat |

## Floor Tileset (proprietary 0x29 format)
- OBJ: Assets/AzureDreams/Models/floor_tileset.obj (165v, 22n, 582f)
- Prefab variants: 5 themes in Assets/AzureDreams/Prefabs/Ground/Floor_Tileset_*.prefab
- Raw data: extracted_assets/verified_decode/report_verification/floor_tileset_type29_decoded.bin (46,951 bytes)

## FX Effects (6 particle systems)
- Prefabs: Assets/AzureDreams/Prefabs/FX/FX_Fire.prefab, FX_Smoke.prefab, FX_Dust.prefab,
  FX_Lightning.prefab, FX_Water.prefab, FX_Magic.prefab
- Materials: Assets/AzureDreams/Materials/FX_*.mat (additive for fire/lightning/magic, alpha blend for smoke/dust/water)

## Environment Props (14 prefabs)
- Walls: Wall_Short, Wall_Long, Wall_Tall
- Pillars: Pillar_Small, Pillar_Large
- Stairs: Stairs_1x1, Stairs_2x2, Stairs_3x3
- DoorFrame, Torch_Holder, Chest, Pot
- Location: Assets/AzureDreams/Prefabs/Environment/

## Audio (WebGL-Ready OGG + Original WAV)
- Location: Assets/AzureDreams/Audio/bgm/ (91 files) and Assets/AzureDreams/Audio/sfx/ (79 files)
- Files: WAV + OGG (Vorbis compression, spatialized SFX, stereo BGM)
- Import settings: Vorbis, CompressedInMemory (via AudioImportPostprocessor)

## Video
- Location: Assets/AzureDreams/Videos/
- Files: 7 MP4 (H.264 + AAC) — JO001-003, KONAMI, LOP1, LOP2, TOP_WS, converted from AVI

## Shaders
- Location: Assets/AzureDreams/Shaders/
- Files: PsxIndexed.shader, PsxVertexSnapAffine.shader, PsxColorQuantization.shader,
         PsxVertexLit.shader, BuiltinPostProcess.shader, PsxIndexed.shader

## Spriters Resource (109 PNGs)
- Location: Assets/AzureDreams/SpritersResource/
- Tower floors: 1F-2F through 38F-39F (20 textures, 256×256 RGBA)
- Characters: Koh, Selfi, Ghosh, Nico, Weedy, Jorda, Guy, Wreath, Young Koh, Young Nico, Cherrl, Chappie
- Monsters: Beldo, Kewne, Trolls
- UI: Fonts, Minimap, OpenBag
- Miscellaneous: Angel, Cat & Bird, Credits, Mugshots, Pots/Table, Dishes, Title Screen, Monster Portraits, Koh Status Faces, Tower Elevator
- Familiar Portraits: 1-50

## Tower Floor Themes (5 themes)
| Theme | Floors | Floor Material | Wall Material | Sky Material |
|-------|--------|----------------|---------------|--------------|
| StoneDungeon | 1-10 | Floor_StoneDungeon.mat | Wall_StoneDungeon.mat | Sky_StoneDungeon.mat |
| CrystalCave | 11-20 | Floor_CrystalCave.mat | Wall_CrystalCave.mat | Sky_CrystalCave.mat |
| LavaChamber | 21-30 | Floor_LavaChamber.mat | Wall_LavaChamber.mat | Sky_LavaChamber.mat |
| IcePalace | 31-40 | Floor_IcePalace.mat | Wall_IcePalace.mat | Sky_IcePalace.mat |
| DarkTower | 41+ | Floor_DarkTower.mat | Wall_DarkTower.mat | Sky_DarkTower.mat |

## Floor Tileset (proprietary 0x29 format)
- OBJ: Assets/AzureDreams/Models/floor_tileset.obj (165v, 22n, 582f)
- Prefab variants: 5 themes in Assets/AzureDreams/Prefabs/Ground/Floor_Tileset_*.prefab
- Raw data: extracted_assets/verified_decode/report_verification/floor_tileset_type29_decoded.bin (46,951 bytes)

## FX Effects (6 particle systems)
- Prefabs: Assets/AzureDreams/Prefabs/FX/FX_Fire.prefab, FX_Smoke.prefab, FX_Dust.prefab,
  FX_Lightning.prefab, FX_Water.prefab, FX_Magic.prefab
- Materials: Assets/AzureDreams/Materials/FX_*.mat (additive for fire/lightning/magic, alpha blend for smoke/dust/water)

## Environment Props (14 prefabs)
- Walls: Wall_Short, Wall_Long, Wall_Tall
- Pillars: Pillar_Small, Pillar_Large
- Stairs: Stairs_1x1, Stairs_2x2, Stairs_3x3
- DoorFrame, Torch_Holder, Chest, Pot
- Location: Assets/AzureDreams/Prefabs/Environment/

## Audio (WebGL-Ready OGG + Original WAV)
- Location: Assets/AzureDreams/Audio/bgm/ (91 files) and Assets/AzureDreams/Audio/sfx/ (79 files)
- Files: WAV + OGG (Vorbis compression, spatialized SFX, stereo BGM)
- Import settings: Vorbis, CompressedInMemory (via AudioImportPostprocessor)

## Video
- Location: Assets/AzureDreams/Videos/
- Files: 7 MP4 (H.264 + AAC) — JO001-003, KONAMI, LOP1, LOP2, TOP_WS, converted from AVI

## Shaders
- Location: Assets/AzureDreams/Shaders/
- Files: PsxIndexed.shader, PsxVertexSnapAffine.shader, PsxColorQuantization.shader,
         PsxVertexLit.shader, BuiltinPostProcess.shader, PsxIndexed.shader

## Spriters Resource (109 PNGs)
- Location: Assets/AzureDreams/SpritersResource/
- Tower floors: 1F-2F through 38F-39F (20 textures, 256×256 RGBA)
- Characters: Koh, Selfi, Ghosh, Nico, Weedy, Jorda, Guy, Wreath, Young Koh, Young Nico, Cherrl, Chappie
- Monsters: Beldo, Kewne, Trolls
- UI: Fonts, Minimap, OpenBag
- Miscellaneous: Angel, Cat & Bird, Credits, Mugshots, Pots/Table, Dishes, Title Screen, Monster Portraits, Koh Status Faces, Tower Elevator
- Familiar Portraits: 1-50

## Tower Floor Themes (5 themes)
| Theme | Floors | Floor Material | Wall Material | Sky Material |
|-------|--------|----------------|---------------|--------------|
| StoneDungeon | 1-10 | Floor_StoneDungeon.mat | Wall_StoneDungeon.mat | Sky_StoneDungeon.mat |
| CrystalCave | 11-20 | Floor_CrystalCave.mat | Wall_CrystalCave.mat | Sky_CrystalCave.mat |
| LavaChamber | 21-30 | Floor_LavaChamber.mat | Wall_LavaChamber.mat | Sky_LavaChamber.mat |
| IcePalace | 31-40 | Floor_IcePalace.mat | Wall_IcePalace.mat | Sky_IcePalace.mat |
| DarkTower | 41+ | Floor_DarkTower.mat | Wall_DarkTower.mat | Sky_DarkTower.mat |

## Floor Tileset (proprietary 0x29 format)
- OBJ: Assets/AzureDreams/Models/floor_tileset.obj (165v, 22n, 582f)
- Prefab variants: 5 themes in Assets/AzureDreams/Prefabs/Ground/Floor_Tileset_*.prefab
- Raw data: extracted_assets/verified_decode/report_verification/floor_tileset_type29_decoded.bin (46,951 bytes)

## FX Effects (6 particle systems)
- Prefabs: Assets/AzureDreams/Prefabs/FX/FX_Fire.prefab, FX_Smoke.prefab, FX_Dust.prefab,
  FX_Lightning.prefab, FX_Water.prefab, FX_Magic.prefab
- Materials: Assets/AzureDreams/Materials/FX_*.mat (additive for fire/lightning/magic, alpha blend for smoke/dust/water)

## Environment Props (14 prefabs)
- Walls: Wall_Short, Wall_Long, Wall_Tall
- Pillars: Pillar_Small, Pillar_Large
- Stairs: Stairs_1x1, Stairs_2x2, Stairs_3x3
- DoorFrame, Torch_Holder, Chest, Pot
- Location: Assets/AzureDreams/Prefabs/Environment/

## Audio (WebGL-Ready OGG + Original WAV)
- Location: Assets/AzureDreams/Audio/bgm/ (91 files) and Assets/AzureDreams/Audio/sfx/ (79 files)
- Files: WAV + OGG (Vorbis compression, spatialized SFX, stereo BGM)
- Import settings: Vorbis, CompressedInMemory (via AudioImportPostprocessor)

## Video
- Location: Assets/AzureDreams/Videos/
- Files: 7 MP4 (H.264 + AAC) — JO001-003, KONAMI, LOP1, LOP2, TOP_WS, converted from AVI

## Shaders
- Location: Assets/AzureDreams/Shaders/
- Files: PsxIndexed.shader, PsxVertexSnapAffine.shader, PsxColorQuantization.shader,
         PsxVertexLit.shader, BuiltinPostProcess.shader, PsxIndexed.shader

## Spriters Resource (109 PNGs)
- Location: Assets/AzureDreams/SpritersResource/
- Tower floors: 1F-2F through 38F-39F (20 textures, 256×256 RGBA)
- Characters: Koh, Selfi, Ghosh, Nico, Weedy, Jorda, Guy, Wreath, Young Koh, Young Nico, Cherrl, Chappie
- Monsters: Beldo, Kewne, Trolls
- UI: Fonts, Minimap, OpenBag
- Miscellaneous: Angel, Cat & Bird, Credits, Mugshots, Pots/Table, Dishes, Title Screen, Monster Portraits, Koh Status Faces, Tower Elevator
- Familiar Portraits: 1-50

## Tower Floor Themes (5 themes)
| Theme | Floors | Floor Material | Wall Material | Sky Material |
|-------|--------|----------------|---------------|--------------|
| StoneDungeon | 1-10 | Floor_StoneDungeon.mat | Wall_StoneDungeon.mat | Sky_StoneDungeon.mat |
| CrystalCave | 11-20 | Floor_CrystalCave.mat | Wall_CrystalCave.mat | Sky_CrystalCave.mat |
| LavaChamber | 21-30 | Floor_LavaChamber.mat | Wall_LavaChamber.mat | Sky_LavaChamber.mat |
| IcePalace | 31-40 | Floor_IcePalace.mat | Wall_IcePalace.mat | Sky_IcePalace.mat |
| DarkTower | 41+ | Floor_DarkTower.mat | Wall_DarkTower.mat | Sky_DarkTower.mat |

## Floor Tileset (proprietary 0x29 format)
- OBJ: Assets/AzureDreams/Models/floor_tileset.obj (165v, 22n, 582f)
- Prefab variants: 5 themes in Assets/AzureDreams/Prefabs/Ground/Floor_Tileset_*.prefab
- Raw data: extracted_assets/verified_decode/report_verification/floor_tileset_type29_decoded.bin (46,951 bytes)

## FX Effects (6 particle systems)
- Prefabs: Assets/AzureDreams/Prefabs/FX/FX_Fire.prefab, FX_Smoke.prefab, FX_Dust.prefab,
  FX_Lightning.prefab, FX_Water.prefab, FX_Magic.prefab
- Materials: Assets/AzureDreams/Materials/FX_*.mat (additive for fire/lightning/magic, alpha blend for smoke/dust/water)

## Environment Props (14 prefabs)
- Walls: Wall_Short, Wall_Long, Wall_Tall
- Pillars: Pillar_Small, Pillar_Large
- Stairs: Stairs_1x1, Stairs_2x2, Stairs_3x3
- DoorFrame, Torch_Holder, Chest, Pot
- Location: Assets/AzureDreams/Prefabs/Environment/

## Audio (WebGL-Ready OGG + Original WAV)
- Location: Assets/AzureDreams/Audio/bgm/ (91 files) and Assets/AzureDreams/Audio/sfx/ (79 files)
- Files: WAV + OGG (Vorbis compression, spatialized SFX, stereo BGM)
- Import settings: Vorbis, CompressedInMemory (via AudioImportPostprocessor)

## Video
- Location: Assets/AzureDreams/Videos/
- Files: 7 MP4 (H.264 + AAC) — JO001-003, KONAMI, LOP1, LOP2, TOP_WS, converted from AVI

## Shaders
- Location: Assets/AzureDreams/Shaders/
- Files: PsxIndexed.shader, PsxVertexSnapAffine.shader, PsxColorQuantization.shader,
         PsxVertexLit.shader, BuiltinPostProcess.shader, PsxIndexed.shader

## Spriters Resource (109 PNGs)
- Location: Assets/AzureDreams/SpritersResource/
- Tower floors: 1F-2F through 38F-39F (20 textures, 256×256 RGBA)
- Characters: Koh, Selfi, Ghosh, Nico, Weedy, Jorda, Guy, Wreath, Young Koh, Young Nico, Cherrl, Chappie
- Monsters: Beldo, Kewne, Trolls
- UI: Fonts, Minimap, OpenBag
- Miscellaneous: Angel, Cat & Bird, Credits, Mugshots, Pots/Table, Dishes, Title Screen, Monster Portraits, Koh Status Faces, Tower Elevator
- Familiar Portraits: 1-50

## Tower Floor Themes (5 themes)
| Theme | Floors | Floor Material | Wall Material | Sky Material |
|-------|--------|----------------|---------------|--------------|
| StoneDungeon | 1-10 | Floor_StoneDungeon.mat | Wall_StoneDungeon.mat | Sky_StoneDungeon.mat |
| CrystalCave | 11-20 | Floor_CrystalCave.mat | Wall_CrystalCave.mat | Sky_CrystalCave.mat |
| LavaChamber | 21-30 | Floor_LavaChamber.mat | Wall_LavaChamber.mat | Sky_LavaChamber.mat |
| IcePalace | 31-40 | Floor_IcePalace.mat | Wall_IcePalace.mat | Sky_IcePalace.mat |
| DarkTower | 41+ | Floor_DarkTower.mat | Wall_DarkTower.mat | Sky_DarkTower.mat |

## Floor Tileset (proprietary 0x29 format)
- OBJ: Assets/AzureDreams/Models/floor_tileset.obj (165v, 22n, 582f)
- Prefab variants: 5 themes in Assets/AzureDreams/Prefabs/Ground/Floor_Tileset_*.prefab
- Raw data: extracted_assets/verified_decode/report_verification/floor_tileset_type29_decoded.bin (46,951 bytes)

## FX Effects (6 particle systems)
- Prefabs: Assets/AzureDreams/Prefabs/FX/FX_Fire.prefab, FX_Smoke.prefab, FX_Dust.prefab,
  FX_Lightning.prefab, FX_Water.prefab, FX_Magic.prefab
- Materials: Assets/AzureDreams/Materials/FX_*.mat (additive for fire/lightning/magic, alpha blend for smoke/dust/water)

## Environment Props (14 prefabs)
- Walls: Wall_Short, Wall_Long, Wall_Tall
- Pillars: Pillar_Small, Pillar_Large
- Stairs: Stairs_1x1, Stairs_2x2, Stairs_3x3
- DoorFrame, Torch_Holder, Chest, Pot
- Location: Assets/AzureDreams/Prefabs/Environment/

## Audio (WebGL-Ready OGG + Original WAV)
- Location: Assets/AzureDreams/Audio/bgm/ (91 files) and Assets/AzureDreams/Audio/sfx/ (79 files)
- Files: WAV + OGG (Vorbis compression, spatialized SFX, stereo BGM)
- Import settings: Vorbis, CompressedInMemory (via AudioImportPostprocessor)

## Video
- Location: Assets/AzureDreams/Videos/
- Files: 7 MP4 (H.264 + AAC) — JO001-003, KONAMI, LOP1, LOP2, TOP_WS, converted from AVI

## Shaders
- Location: Assets/AzureDreams/Shaders/
- Files: PsxIndexed.shader, PsxVertexSnapAffine.shader, PsxColorQuantization.shader,
         PsxVertexLit.shader, BuiltinPostProcess.shader, PsxIndexed.shader

## Spriters Resource (109 PNGs)
- Location: Assets/AzureDreams/SpritersResource/
- Tower floors: 1F-2F through 38F-39F (20 textures, 256×256 RGBA)
- Characters: Koh, Selfi, Ghosh, Nico, Weedy, Jorda, Guy, Wreath, Young Koh, Young Nico, Cherrl, Chappie
- Monsters: Beldo, Kewne, Trolls
- UI: Fonts, Minimap, OpenBag
- Miscellaneous: Angel, Cat & Bird, Credits, Mugshots, Pots/Table, Dishes, Title Screen, Monster Portraits, Koh Status Faces, Tower Elevator
- Familiar Portraits: 1-50
