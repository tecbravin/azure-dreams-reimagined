using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using AzureDreamsReimagined.Rendering;

namespace AzureDreams.Reimagined.EditorTests
{
    public class PsxIndexedGpuRenderTests
    {
        private const int ATLAS_SIZE = 1;
        private const int CLUT_SIZE = 16;

        private Shader _shader;
        private Texture2D _indices;
        private Texture2D _clut;
        private Material _matOpaque;
        private Material _matStp;
        private RenderTexture _prevActive;

        [SetUp]
        public void Setup()
        {
            _shader = Shader.Find(PsxIndexedMaterial.ShaderName);
            Assert.IsNotNull(_shader, "Shader must compile and be importable for GPU tests");

            // 1x1 atlas: single pixel = index 0
            _indices = new Texture2D(ATLAS_SIZE, ATLAS_SIZE, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Idx"
            };
            _indices.SetPixel(0, 0, new Color32(0, 0, 0, 255));
            _indices.Apply(false, false);

            // CLUT 16x16: 256 LE words. Each entry is 2 bytes (R_low, G_high).
            // We set specific entries per test in the test methods.
            _clut = PsxIndexedMaterial.CreateClutTexture(new byte[512], "Test CLUT GPU");

            _matOpaque = new Material(_shader) { name = "Opaque" };
            _matStp = new Material(_shader) { name = "STP" };
        }

        [TearDown]
        public void TearDown()
        {
            if (_matOpaque != null) UnityEngine.Object.DestroyImmediate(_matOpaque);
            if (_matStp != null) UnityEngine.Object.DestroyImmediate(_matStp);
            if (_clut != null) UnityEngine.Object.DestroyImmediate(_clut);
            _matOpaque = null;
            _matStp = null;
            _clut = null;
        }

        [OneTimeTearDown]
        public void FinalCleanup()
        {
            if (_indices != null) UnityEngine.Object.DestroyImmediate(_indices);
            if (_clut != null) UnityEngine.Object.DestroyImmediate(_clut);
            _indices = null;
            _clut = null;
        }

        private Texture2D RenderToTexture2D(Material material)
        {
            // Linear buffer, clear to transparent black. Size 1x1 since atlas is 1x1.
            var rt = RenderTexture.GetTemporary(ATLAS_SIZE, ATLAS_SIZE, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            _prevActive = RenderTexture.active;
            try
            {
                RenderTexture.active = rt;
                GL.Clear(false, true, Color.clear);
                Graphics.Blit(Texture2D.whiteTexture, rt, material, -1); // all passes
                RenderTexture.active = rt;
                var img = new Texture2D(ATLAS_SIZE, ATLAS_SIZE, TextureFormat.RGBA32, false, true);
                img.ReadPixels(new Rect(0, 0, ATLAS_SIZE, ATLAS_SIZE), 0, 0);
                img.Apply(false, false);
                return img;
            }
            finally
            {
                RenderTexture.active = _prevActive;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static Color SampleCenter(Texture2D img)
        {
            int cx = img.width / 2;
            int cy = img.height / 2;
            return img.GetPixel(cx, cy);
        }

        private void SetClutEntry(byte[] clutBytes, int index, ushort word)
        {
            clutBytes[index * 2] = (byte)(word & 0xFF);
            clutBytes[index * 2 + 1] = (byte)((word >> 8) & 0xFF);
        }

        [Test]
        public void GpuRender_WordZero_IsTransparent()
        {
            // Index 0 -> word 0x0000 -> discarded by clip(word - 0.5) in both passes.
            _clut = PsxIndexedMaterial.CreateClutTexture(new byte[512], "Test CLUT zero");
            _matOpaque = new Material(_shader) { name = "Opaque" };
            PsxIndexedMaterial.Configure(_matOpaque, _indices, _clut, 0, false, PsxAbrMode.Half);

            var img = RenderToTexture2D(_matOpaque);
            var c = SampleCenter(img);
            Assert.AreEqual(0, c.a, 0.01f, "WORD==0 must be transparent; got alpha=" + c.a);
            UnityEngine.Object.DestroyImmediate(img);
        }

        [Test]
        public void GpuRender_WordNonZero_IsOpaque_NoStp()
        {
            // Index 0 -> word 0x001F (no STP, red=31/31=1.0).
            var clutBytes = new byte[512];
            SetClutEntry(clutBytes, 0, 0x001F);
            _clut = PsxIndexedMaterial.CreateClutTexture(clutBytes, "Test CLUT nonzero");

            _matOpaque = new Material(_shader) { name = "Opaque" };
            PsxIndexedMaterial.Configure(_matOpaque, _indices, _clut, 0, false, PsxAbrMode.Half);

            var img = RenderToTexture2D(_matOpaque);
            var c = SampleCenter(img);
            Assert.Greater(c.a, 0.9f, "Non-zero WORD must be opaque in OPAQUE pass; got alpha=" + c.a);
            Assert.AreEqual(1.0, c.r, 0.02f, "Red channel should be 1.0 for word 0x001F");
            UnityEngine.Object.DestroyImmediate(img);
        }

        [Test]
        public void GpuRender_StpPass_AlphaIsBlended()
        {
            // Index 0 -> word 0x801F (STP bit set, red=31/31=1.0).
            // STP pass with _SemiTransparency=1: blendPixel=1 -> alpha = _BlendAlpha (Half = 0.5).
            // For Half ABR, result red = src * 0.5 + dst * 0.5 = 0.5 over black.
            var clutBytes = new byte[512];
            SetClutEntry(clutBytes, 0, 0x801F);
            _clut = PsxIndexedMaterial.CreateClutTexture(clutBytes, "Test CLUT STP");

            _matStp = new Material(_shader) { name = "STP" };
            PsxIndexedMaterial.Configure(_matStp, _indices, _clut, 0, true, PsxAbrMode.Half);

            var img = RenderToTexture2D(_matStp);
            var c = SampleCenter(img);
            Assert.AreEqual(0.5f, c.a, 0.05f, "STP texel should be blended at Half alpha");
            Assert.AreEqual(0.5f, c.r, 0.02f, "STP red should be 0.5 for Half ABR over black");
            UnityEngine.Object.DestroyImmediate(img);
        }

        [Test]
        public void GpuRender_NonStpInOpaquePass_IsOpaque()
        {
            // Non-STP pixel is drawn in OPAQUE pass and stays opaque.
            // It is NOT clipped by the STP pass because STP pass only affects pixels with _SemiTransparency=1
            // and blendPixel=1; but the OPAQUE pass already wrote the pixel.
            var clutBytes = new byte[512];
            SetClutEntry(clutBytes, 0, 0x001F); // no STP
            _clut = PsxIndexedMaterial.CreateClutTexture(clutBytes, "Test CLUT no STP");

            _matOpaque = new Material(_shader) { name = "Opaque" };
            _matStp = new Material(_shader) { name = "STP" };
            PsxIndexedMaterial.Configure(_matOpaque, _indices, _clut, 0, false, PsxAbrMode.Half);
            PsxIndexedMaterial.Configure(_matStp, _indices, _clut, 0, true, PsxAbrMode.Half);

            // OPAQUE pass renders the non-STP pixel
            var imgOpaque = RenderToTexture2D(_matOpaque);
            var cOpaque = SampleCenter(imgOpaque);
            Assert.Greater(cOpaque.a, 0.9f, "Non-STP pixel must be opaque in OPAQUE pass");
            Assert.AreEqual(1.0, cOpaque.r, 0.02f, "Red should be 1.0 for word 0x001F");

            // STP pass alone on empty buffer would clip it, but combined with OPAQUE pass
            // the result is still opaque from the OPAQUE pass
            UnityEngine.Object.DestroyImmediate(imgOpaque);
        }

        [Test]
        public void GpuRender_AbrFourModes_AssertSrgb()
        {
            // Test all four ABR modes with a known color and STP enabled.
            // Word 0x801F = STP + red=31 (1.0). Background = transparent black.
            var clutBytes = new byte[512];
            SetClutEntry(clutBytes, 0, 0x801F);
            _clut = PsxIndexedMaterial.CreateClutTexture(clutBytes, "Test CLUT ABR modes");

            _matOpaque = new Material(_shader) { name = "Opaque" };
            _matStp = new Material(_shader) { name = "STP" };

            // Test each ABR mode
            foreach (PsxAbrMode mode in Enum.GetValues(typeof(PsxAbrMode)))
            {
                PsxIndexedMaterial.Configure(_matOpaque, _indices, _clut, 0, false, mode);
                PsxIndexedMaterial.Configure(_matStp, _indices, _clut, 0, true, mode);

                // Opaque pass: pixel must be opaque with full red
                var imgOpaque = RenderToTexture2D(_matOpaque);
                var cOpaque = SampleCenter(imgOpaque);
                Assert.Greater(cOpaque.a, 0.9f, $"Opaque pass must be opaque for ABR {mode}");
                Assert.AreEqual(1.0, cOpaque.r, 0.02f, $"Opaque red must be 1.0 for ABR {mode}");

                // STP pass: alpha depends on ABR mode
                var imgStp = RenderToTexture2D(_matStp);
                var cStp = SampleCenter(imgStp);
                float expectedAlpha = mode switch
                {
                    PsxAbrMode.Half => 0.5f,
                    PsxAbrMode.Add => 1.0f,
                    PsxAbrMode.Subtract => 1.0f, // Current shader keeps _BlendAlpha=1 for Subtract
                    PsxAbrMode.AddQuarter => 0.25f,
                    _ => 1.0f
                };
                float expectedRed = mode switch
                {
                    PsxAbrMode.Half => 0.5f,
                    PsxAbrMode.Add => 1.0f,
                    PsxAbrMode.Subtract => 0.0f, // COLOR channel is ReverseSubtract: dst - src = 0 - 1 = 0 over black (alpha is unaffected)
                    PsxAbrMode.AddQuarter => 0.25f,
                    _ => 1.0f
                };
                Assert.AreEqual(expectedAlpha, cStp.a, 0.05f, $"STP alpha for ABR {mode} should be {expectedAlpha}");
                Assert.AreEqual(expectedRed, cStp.r, 0.02f, $"STP red for ABR {mode} should be {expectedRed}");

                UnityEngine.Object.DestroyImmediate(imgOpaque);
                UnityEngine.Object.DestroyImmediate(imgStp);
            }
        }

        [Test]
        public void GpuRender_WordZero_IndexZero_CanBeValidColor()
        {
            // Index 0 with word != 0 should be visible. 0x0000 is transparent, not index rule.
            var clutBytes = new byte[512];
            SetClutEntry(clutBytes, 0, 0xFFFF); // word 0xFFFF = max red, no STP
            _clut = PsxIndexedMaterial.CreateClutTexture(clutBytes, "Test CLUT index0 color");

            _matOpaque = new Material(_shader) { name = "Opaque" };
            PsxIndexedMaterial.Configure(_matOpaque, _indices, _clut, 0, false, PsxAbrMode.Half);

            var img = RenderToTexture2D(_matOpaque);
            var c = SampleCenter(img);
            Assert.Greater(c.a, 0.9f, "Index0 with non-zero word must be opaque");
            Assert.AreEqual(1.0, c.r, 0.02f, "Red should be max for word 0xFFFF");
            UnityEngine.Object.DestroyImmediate(img);
        }

        [Test]
        public void GpuRender_Black0x8000_Opaque()
        {
            // Word 0x8000 = STP bit set, RGB=0. Should be opaque black, not transparent.
            var clutBytes = new byte[512];
            SetClutEntry(clutBytes, 0, 0x8000); // STP, black
            _clut = PsxIndexedMaterial.CreateClutTexture(clutBytes, "Test CLUT black STP");

            _matOpaque = new Material(_shader) { name = "Opaque" };
            _matStp = new Material(_shader) { name = "STP" };
            PsxIndexedMaterial.Configure(_matOpaque, _indices, _clut, 0, false, PsxAbrMode.Half);
            PsxIndexedMaterial.Configure(_matStp, _indices, _clut, 0, true, PsxAbrMode.Half);

            var imgOpaque = RenderToTexture2D(_matOpaque);
            var cOpaque = SampleCenter(imgOpaque);
            Assert.Greater(cOpaque.a, 0.9f, "0x8000 must be opaque");
            Assert.Less(cOpaque.r, 0.1f, "0x8000 must be black");

            var imgStp = RenderToTexture2D(_matStp);
            var cStp = SampleCenter(imgStp);
            Assert.AreEqual(0.5f, cStp.a, 0.05f, "0x8000 STP alpha must be Half");
            Assert.Less(cStp.r, 0.1f, "0x8000 STP must remain black");

            UnityEngine.Object.DestroyImmediate(imgOpaque);
            UnityEngine.Object.DestroyImmediate(imgStp);
        }
    }
}


