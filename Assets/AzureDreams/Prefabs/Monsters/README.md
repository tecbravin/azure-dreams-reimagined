# Azure Dreams Reimagined - Monster Prefabs

## Generation
Run `Azure Dreams/Rendering/Build Monster Prefabs` in Unity Editor after ImportVerifiedSprites() has completed.

## Prefabs
51 monster prefabs (excluding quarantined IDs 47, 48, 51-54) in `Assets/AzureDreams/Prefabs/Monsters/`.

Each prefab contains:
- Root GameObject (named after monster)
- Child Quad with PsxIndexed opaque material
- Material references the verified 4bpp atlas + CLUT

## Monster Names (from monster_ids.h)
Resource ID → Monster Name (1:1 mapping)
1: Hikewne, 2: Kewne (familiar), 3: Dragon, 4: Kid, 5: Ifrit, ...
57: Beldo

## Notes
- Quarantined resources (47, 48, 51-54) excluded
- Player character (Koh) not in monster sprite bank — separate asset needed
- Prefabs use 2D quads with PsxIndexed material (not 3D models)
- STP/ABR variants available as separate materials (abr_0 through abr_3)
