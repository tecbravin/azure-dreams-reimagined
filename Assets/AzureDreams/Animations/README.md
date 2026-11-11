# Azure Dreams Reimagined - Animation System

## Generation
Run `Azure Dreams/Rendering/Build Animation Controllers` in Unity Editor.

## Animation Controller
`Assets/AzureDreams/Animations/PsxActor.controller`

### States
- **Idle** — Default state, character standing
- **Walk** — Movement animation (triggered by IsWalking bool)
- **Attack** — Attack animation (triggered by Attack trigger)
- **Death** — Death animation (triggered by Death trigger)
- **Cast** — Spell casting (triggered by Cast trigger)

### Parameters
- `IsWalking` (Bool) — Toggles walk/idle
- `Attack` (Trigger) — Triggers attack animation
- `Cast` (Trigger) — Triggers cast animation
- `Death` (Trigger) — Triggers death animation

## Animation Clips
Empty placeholder clips created in `Assets/AzureDreams/Animations/`:
- Idle.anim, Walk.anim, Attack.anim, Death.anim, Cast.anim

## Next Steps (Needs Coder)
The animation clips need to be populated with actual sprite frame data from:
- `verified_decode/sprites/resource_XXX/frame_chain_*.png` (flat previews)
- Sprite metadata (chain pointers, origins, sizes from metadata.json)
- The runtime assembles frames from part chains at runtime

The PS1 game uses a custom animation system (not Unity's) that:
- Uses part chains (12-byte descriptors with UV, position, flags)
- Supports callbacks (0x20 flag) and shared pages (0x40 flag)
- Has flip bits (0/1) that change origin
- Semi-transparency (STP) affects rendering

## PS1 Animation Characteristics
- Frame-based (not skeletal)
- Sprites assembled from multiple parts (quads)
- Each part has: flags, gpu_code, x, y, tpage, clut, u, v, width, height
- Animation speed tied to game frame rate (not time-based)
- Direction changes affect sprite flipping
