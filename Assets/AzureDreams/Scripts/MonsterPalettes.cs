using System;
using System.Collections.Generic;
using AzureDreamsReimagined.Rendering;
using UnityEngine;

namespace AzureDreamsReimagined
{
    internal enum MonsterPaletteVariant { Enemy = 0, Fire = 4, Water = 8, Wind = 12 }

    internal static class MonsterPalettes
    {
        [Serializable] private sealed class Catalog { public Entry[] entries; }
        [Serializable] private sealed class Entry
        {
            public int species;
            public int[] enemy, fire, water, wind, rgbIndices;

            public int[] Colors(MonsterPaletteVariant variant) => variant switch
            {
                MonsterPaletteVariant.Fire => fire,
                MonsterPaletteVariant.Water => water,
                MonsterPaletteVariant.Wind => wind,
                _ => enemy
            };
        }

        private static Dictionary<int, Entry> _entries;
        private static readonly Dictionary<(Texture2D, int, MonsterPaletteVariant, bool), Texture2D> Atlases = new();
        private static readonly Dictionary<(Sprite, Texture2D), Sprite> Sprites = new();

        internal static MonsterPaletteVariant ForElement(int element) => element switch
        {
            1 => MonsterPaletteVariant.Fire,
            2 => MonsterPaletteVariant.Water,
            4 => MonsterPaletteVariant.Wind,
            _ => MonsterPaletteVariant.Enemy
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            _entries = null;
            Atlases.Clear();
            Sprites.Clear();
        }

        private static Entry Find(int species)
        {
            if (_entries == null)
            {
                _entries = new Dictionary<int, Entry>();
                var asset = Resources.Load<TextAsset>("Town/Retail/monster_palettes");
                if (asset && JsonUtility.FromJson<Catalog>(asset.text)?.entries is { } entries)
                    foreach (var entry in entries) _entries[entry.species] = entry;
            }
            return _entries.TryGetValue(species, out var palette) ? palette : null;
        }

        internal static void Apply(GameObject view, int species, MonsterPaletteVariant variant)
        {
            if (Find(species) == null) return;
            if (view.TryGetComponent<KewneSpriteView>(out var kewne))
            {
                kewne.SetPalette(species, variant);
                return;
            }
            if (view.GetComponentInChildren<MonsterTurnSpriteView>() is { } animated)
            {
                animated.SetPalette(species, variant);
                return;
            }

            MaterialPropertyBlock properties = null;
            foreach (var renderer in view.GetComponentsInChildren<MeshRenderer>())
            {
                var material = renderer.sharedMaterial;
                if (!material || !material.HasProperty(PsxIndexedMaterial.PaletteRow)) continue;
                properties ??= new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                properties.SetFloat(PsxIndexedMaterial.PaletteRow, (int)variant);
                renderer.SetPropertyBlock(properties);
            }
            foreach (var renderer in view.GetComponentsInChildren<SpriteRenderer>())
            {
                var sprite = renderer.sprite;
                if (!sprite) continue;
                var texture = Recolor(sprite.texture, species, variant);
                if (texture == sprite.texture) continue;
                var key = (sprite, texture);
                if (!Sprites.TryGetValue(key, out var replacement))
                {
                    var rect = sprite.rect;
                    var pivot = new Vector2(sprite.pivot.x / rect.width, sprite.pivot.y / rect.height);
                    replacement = Sprite.Create(texture, rect, pivot, sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect, sprite.border);
                    Sprites.Add(key, replacement);
                }
                renderer.sprite = replacement;
            }
        }

        internal static Texture2D Recolor(Texture2D source, int species, MonsterPaletteVariant variant, bool indexed = false)
        {
            if (!source || !source.isReadable) return source;
            var key = (source, species, variant, indexed);
            if (Atlases.TryGetValue(key, out var cached)) return cached;
            var entry = Find(species);
            if (entry == null || (!indexed && entry.rgbIndices is not { Length: > 0 })) return source;
            var colors = entry.Colors(variant);
            var pixels = source.GetPixels32();
            if (indexed)
            {
                // Red stores the texel's palette subrow and index. Keep the baked coverage mask.
                var enemy = entry.enemy;
                for (int i = 0; i < pixels.Length; i++)
                {
                    var pixel = pixels[i];
                    if (pixel.a == 0 || pixel.r >= colors.Length) continue;
                    int color = colors[pixel.r];
                    if (color < 0) color = enemy[pixel.r];
                    if (color >= 0) pixels[i] = PalettePixel(color, pixel.a);
                }
            }
            else
            {
                var indices = entry.rgbIndices;
                var lookup = new Dictionary<int, int>();
                for (int i = 0; i < indices.Length; i += 2)
                {
                    int color = colors[indices[i + 1]];
                    if (color >= 0) lookup.Add(indices[i], color);
                }
                for (int i = 0; i < pixels.Length; i++)
                {
                    var pixel = pixels[i];
                    int rgb = pixel.r << 16 | pixel.g << 8 | pixel.b;
                    if (pixel.a != 0 && lookup.TryGetValue(rgb, out int color)) pixels[i] = PalettePixel(color, pixel.a);
                }
            }
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
            {
                name = $"{source.name} ({variant})",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Atlases.Add(key, texture);
            return texture;
        }

        private static Color32 PalettePixel(int rgb, byte alpha) =>
            new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, alpha);
    }
}
