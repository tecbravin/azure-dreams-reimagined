using AzureDreamsReimagined.Rendering;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Small, camera-facing swimsuit layer for the pool-only town residents.
    /// The retail field sprites are shared by the normal town actors, so this
    /// layer keeps the original sprites intact everywhere outside the pool.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PoolAttireView : MonoBehaviour
    {
        public enum Style
        {
            OnePiece,
            Trunks
        }

        private const float PixelsPerUnit = 32f;
        private Texture2D _texture;
        private Sprite _sprite;
        private Transform _visual;
        private Texture2D _waterTexture;
        private Sprite _waterSprite;
        private Transform _waterVisual;
        private float _scale = 1f;

        public static void Apply(NPCController npc, Style style, Color primary, Color accent)
        {
            if (!npc) return;
            Apply(npc.gameObject, style, primary, accent);
        }

        /// <summary>Applies a temporary pool outfit without modifying the shared actor sprite sheet.</summary>
        public static void Apply(GameObject wearer, Style style, Color primary, Color accent, float scale = 1f)
        {
            if (!wearer) return;
            var attire = wearer.GetComponent<PoolAttireView>();
            if (!attire) attire = wearer.AddComponent<PoolAttireView>();
            attire.Rebuild(style, primary, accent, scale);
        }

        public static void Remove(GameObject wearer)
        {
            if (!wearer) return;
            var attire = wearer.GetComponent<PoolAttireView>();
            if (attire)
            {
                if (attire._visual) attire._visual.gameObject.SetActive(false);
                if (attire._waterVisual) attire._waterVisual.gameObject.SetActive(false);
                Destroy(attire);
            }
        }

        /// <summary>Shows the waterline only while the wearer is actually in the pool.</summary>
        public static void SetSwimming(GameObject wearer, bool swimming)
        {
            if (!wearer) return;
            var attire = wearer.GetComponent<PoolAttireView>();
            if (attire) attire.PresentWaterline(swimming);
        }

        private void Rebuild(Style style, Color primary, Color accent, float scale)
        {
            _scale = Mathf.Max(0.1f, scale);
            if (_sprite) Destroy(_sprite);
            if (_texture) Destroy(_texture);

            _texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
            {
                name = "Pool attire",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var clear = new Color(0f, 0f, 0f, 0f);
            var pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            if (style == Style.OnePiece)
            {
                // Straps, a high-cut one-piece and a thin contrasting belt.
                Paint(pixels, 5, 13, 2, 3, primary);
                Paint(pixels, 9, 13, 2, 3, primary);
                Paint(pixels, 4, 5, 8, 8, primary);
                Paint(pixels, 5, 3, 2, 2, primary);
                Paint(pixels, 9, 3, 2, 2, primary);
                Paint(pixels, 4, 8, 8, 1, accent);
            }
            else
            {
                // Wotta and the anonymous swimmer wear short swimming trunks.
                Paint(pixels, 3, 4, 10, 5, primary);
                Paint(pixels, 4, 2, 3, 2, primary);
                Paint(pixels, 9, 2, 3, 2, primary);
                Paint(pixels, 3, 8, 10, 1, accent);
            }

            _texture.SetPixels(pixels);
            _texture.Apply(false, true);
            _sprite = Sprite.Create(_texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0f), PixelsPerUnit, 0,
                SpriteMeshType.FullRect);

            if (!_visual)
            {
                var child = new GameObject("Pool Attire");
                _visual = child.transform;
                _visual.SetParent(transform, false);
                child.AddComponent<CameraFacingSprite>();
            }
            var renderer = _visual.GetComponent<SpriteRenderer>();
            if (!renderer) renderer = _visual.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = _sprite;
            renderer.sortingOrder = 8;

            _visual.localPosition = style == Style.OnePiece
                ? new Vector3(0f, 0.24f * _scale, -0.02f)
                : new Vector3(0f, 0.16f * _scale, -0.02f);
            _visual.localScale = style == Style.OnePiece
                ? new Vector3(1.12f * _scale, 1.12f * _scale, 1f)
                : new Vector3(1.18f * _scale, 0.78f * _scale, 1f);
        }

        private void PresentWaterline(bool swimming)
        {
            if (!swimming)
            {
                if (_waterVisual) _waterVisual.gameObject.SetActive(false);
                return;
            }

            if (!_waterSprite) BuildWaterline();
            if (!_waterVisual)
            {
                var child = new GameObject("Pool Waterline");
                _waterVisual = child.transform;
                _waterVisual.SetParent(transform, false);
                child.AddComponent<CameraFacingSprite>();
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sprite = _waterSprite;
                renderer.sortingOrder = 10;
            }

            _waterVisual.localPosition = new Vector3(0f, 0.31f * _scale, -0.05f);
            _waterVisual.localScale = new Vector3(1.9f * _scale, 0.72f * _scale, 1f);
            _waterVisual.gameObject.SetActive(true);
        }

        private void BuildWaterline()
        {
            _waterTexture = new Texture2D(24, 8, TextureFormat.RGBA32, false)
            {
                name = "Pool waterline",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var clear = new Color(0f, 0f, 0f, 0f);
            var pixels = new Color[24 * 8];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            Color dark = new(0.05f, 0.35f, 0.74f, 0.88f);
            Color light = new(0.34f, 0.86f, 1f, 0.94f);
            for (int x = 0; x < 24; x++)
            {
                int crest = 4 + ((x + 1) % 6 < 3 ? 1 : 0);
                for (int y = 0; y <= crest; y++) pixels[y * 24 + x] = dark;
                pixels[crest * 24 + x] = light;
            }
            _waterTexture.SetPixels(pixels);
            _waterTexture.Apply(false, true);
            _waterSprite = Sprite.Create(_waterTexture, new Rect(0f, 0f, 24f, 8f), new Vector2(0.5f, 0f), PixelsPerUnit,
                0, SpriteMeshType.FullRect);
        }

        private static void Paint(Color[] pixels, int x, int y, int width, int height, Color color)
        {
            for (int yy = Mathf.Max(0, y); yy < Mathf.Min(16, y + height); yy++)
                for (int xx = Mathf.Max(0, x); xx < Mathf.Min(16, x + width); xx++)
                    pixels[yy * 16 + xx] = color;
        }

        private void OnDestroy()
        {
            if (_sprite) Destroy(_sprite);
            if (_texture) Destroy(_texture);
            if (_waterSprite) Destroy(_waterSprite);
            if (_waterTexture) Destroy(_waterTexture);
        }
    }
}
