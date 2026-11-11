# Azure Dreams Reimagined - FX Prefabs

## Generation
Run `Azure Dreams/Rendering/Build FX Prefabs` in Unity Editor.

## Available Effects
| Prefab | Type | Blend Mode | Use Case |
|--------|------|------------|----------|
| FX_Fire | Particle System | Additive | Fire spells, torches, lava |
| FX_Smoke | Particle System | Alpha Blend | Smoke trails, dust clouds |
| FX_Dust | Particle System | Alpha Blend | Footsteps, ground impacts |
| FX_Lightning | Particle System | Additive | Lightning spells, electric attacks |
| FX_Water | Particle System | Alpha Blend | Water spells, splashes |
| FX_Magic | Particle System | Additive | Generic magic, spell effects |

## Materials
Located in `Assets/AzureDreams/Materials/`:
- FX_Fire.mat (orange, additive)
- FX_Smoke.mat (gray, alpha blend)
- FX_Dust.mat (brown, alpha blend)
- FX_Lightning.mat (blue/white, additive)
- FX_Water.mat (blue, alpha blend)
- FX_Magic.mat (purple, additive)

## Notes
- All effects use Unity's built-in Particle System (no VFX Graph required)
- Additive blending for emissive effects (fire, lightning, magic)
- Alpha blending for opaque effects (smoke, dust, water)
- Gravity and size-over-lifetime configured per effect
- Color gradients match PS1-era color palette
- For PS1 authenticity, consider adding vertex snapping via PsxVertexSnapAffine shader
