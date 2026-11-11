using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AzureDreamsReimagined.Rendering
{
    public enum PsxAbrMode { Half = 0, Add = 1, Subtract = 2, AddQuarter = 3 }

    /// <summary>Data textures and fixed-function PS1 blend states. STP is not alpha.</summary>
    public static class PsxIndexedMaterial
    {
        public const string ShaderName = "AzureDreams/PSX Indexed CLUT";
        public static readonly int IndexTex = Shader.PropertyToID("_IndexTex");
        public static readonly int ClutTex = Shader.PropertyToID("_ClutTex");
        public static readonly int PaletteRow = Shader.PropertyToID("_PaletteRow");
        private static readonly int Semi = Shader.PropertyToID("_SemiTransparency");
        private static readonly int Abr = Shader.PropertyToID("_AbrMode");
        private static readonly int Src = Shader.PropertyToID("_SrcBlend");
        private static readonly int Dst = Shader.PropertyToID("_DstBlend");
        private static readonly int Op = Shader.PropertyToID("_BlendOp");
        private static readonly int Alpha = Shader.PropertyToID("_BlendAlpha");
        private static readonly int Modulation = Shader.PropertyToID("_Color");

        public static Texture2D CreateClutTexture(byte[] littleEndianWords, string name = "PSX CLUT")
        {
            if (littleEndianWords == null || littleEndianWords.Length != 512)
                throw new ArgumentException("A sprite CLUT must contain 256 LE halfwords (512 bytes).", nameof(littleEndianWords));
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false, true)
            {
                name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0
            };
            var pixels = new Color32[256];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(littleEndianWords[i * 2], littleEndianWords[i * 2 + 1], 0, 255);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        public static void Configure(Material material, Texture indices, Texture clut, int palette,
            bool semiTransparency = false, PsxAbrMode abrMode = PsxAbrMode.Half)
        {
            if (!material || !material.shader || material.shader.name != ShaderName)
                throw new ArgumentException("Expected a PSX Indexed CLUT material.", nameof(material));
            if (!indices || !clut || clut.width != 16 || clut.height != 16)
                throw new ArgumentException("Index atlas and a 16x16 raw-word CLUT texture are required.");
            if (palette < 0 || palette > 15) throw new ArgumentOutOfRangeException(nameof(palette));
            material.SetTexture(IndexTex, indices);
            material.SetTexture(ClutTex, clut);
            material.SetFloat(PaletteRow, palette);
            material.SetFloat(Semi, semiTransparency ? 1 : 0);
            material.SetFloat(Abr, (float)abrMode);
            ApplyBlendState(material);
        }

        public static void ApplyBlendState(Material material)
        {
            if (!material) throw new ArgumentNullException(nameof(material));
            var mode = (PsxAbrMode)Mathf.RoundToInt(material.GetFloat(Abr));
            var source = BlendMode.One;
            var destination = BlendMode.One;
            var operation = BlendOp.Add;
            float alpha = 1;
            switch (mode)
            {
                case PsxAbrMode.Half:
                    source = BlendMode.SrcAlpha; destination = BlendMode.OneMinusSrcAlpha; alpha = 0.5f;
                    break;
                case PsxAbrMode.Add: break;
                case PsxAbrMode.Subtract: operation = BlendOp.ReverseSubtract; break;
                case PsxAbrMode.AddQuarter: source = BlendMode.SrcAlpha; alpha = 0.25f; break;
                default: throw new ArgumentOutOfRangeException(nameof(mode));
            }
            material.SetInt(Src, (int)source);
            material.SetInt(Dst, (int)destination);
            material.SetInt(Op, (int)operation);
            material.SetFloat(Alpha, alpha);
            bool semiTransparency = material.GetFloat(Semi) >= 0.5f;
            material.renderQueue = semiTransparency ? (int)RenderQueue.Transparent : (int)RenderQueue.AlphaTest;
            material.SetOverrideTag("RenderType", semiTransparency ? "Transparent" : "TransparentCutout");
        }

        /// <summary>GPU 0x80 modulation is neutral, not 0xFF. A raw-texture command bypasses it.</summary>
        public static void SetPrimitiveColor(MaterialPropertyBlock properties, Color32 color, bool rawTexture)
        {
            if (properties == null) throw new ArgumentNullException(nameof(properties));
            properties.SetColor(Modulation, rawTexture ? Color.white :
                new Color(color.r / 128f, color.g / 128f, color.b / 128f, 1));
        }
    }
}
