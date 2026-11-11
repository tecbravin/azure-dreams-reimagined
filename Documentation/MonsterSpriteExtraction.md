# Monster Sprite Extraction Report

## Validated Sprites from BIN
- **51 valid sprite resources** extracted from DUNGEON.BIN
- Format: 4bpp indexed, 256×256 atlas, RGB555 CLUT, 16 palettes × 16 colors
- Location: `Assets/AzureDreams/Sprites/DungeonCreatures/resource_001` through `resource_051`
- Prefabs: `Assets/AzureDreams/Prefabs/Monsters/` (51 prefabs)

## Quarantined Resources (Additional Sprites)
- **5 resources** with partial data: 47, 51, 52, 53, 54
- **40 frame_chain sprites** extracted (8 per resource)
- Format: Already decoded RGBA PNGs, various sizes (30×39 to 46×47)
- Location: `Assets/AzureDreams/Sprites/Quarantined/resource_047/` etc.
- Prefabs: `Assets/AzureDreams/Prefabs/Quarantined/` (5 prefabs, one per resource)

## Resource 48
- No sprite data available (references outside buffer at 0xf0704f7+0xc/0xd000)
- Cannot be recovered

## Total Monster Sprites Available
- 51 validated BIN sprites
- 40 quarantined frame_chain sprites
- 3 Spriters Resource monster PNGs (Beldo, Kewne, Trolls)
- **Total: 94 sprite frames**

## Monster Count from Decomp
- `monster_ids.h` defines 55 monsters (MONSTER_HIKEWNE = 1 to MONSTER_BELDO = 55)
- Some monsters may share sprites or use palette swaps
- 51 validated sprites likely cover the main unique monster designs
- Quarantined resources may be palette-swap variants or special effects

## Import Instructions
1. Run `Azure Dreams/Rendering/Import Split Spritesheets` for Spriters Resource sprites
2. Run `Azure Dreams/Characters/Build Quarantined Monster Prefabs` for quarantined sprites
3. Run `Azure Dreams/Rendering/Build ALL Assets` to generate everything

## Limitations
- BIN extraction is complete (710 dungeon slots audited)
- No additional sprite data found beyond 51 validated + 5 quarantined resources
- Resource 48 is unrecoverable
- Some monsters may share sprites (palette swaps not yet implemented)
