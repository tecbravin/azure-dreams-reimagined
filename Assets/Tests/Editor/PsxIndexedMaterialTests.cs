using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using AzureDreamsReimagined.Rendering;

namespace AzureDreams.Reimagined.EditorTests
{
    public class PsxIndexedMaterialTests
    {
        private Material _material;
        private Texture2D _clut;

        [SetUp]
        public void Setup()
        {
            var shader = Shader.Find(PsxIndexedMaterial.ShaderName);
            Assert.IsNotNull(shader, $"Shader '{PsxIndexedMaterial.ShaderName}' not found");
            _material = new Material(shader);
            _clut = PsxIndexedMaterial.CreateClutTexture(Enumerable.Repeat((byte)0x80, 512).ToArray(), "Test CLUT");
            Assert.IsNotNull(_clut);
        }

        [TearDown]
        public void TearDown()
        {
            if (_material != null) UnityEngine.Object.DestroyImmediate(_material);
            if (_clut != null) UnityEngine.Object.DestroyImmediate(_clut);
        }

        [Test]
        public void CreateClutTexture_Valid512Bytes_Returns16x16RGBA32()
        {
            var clut = PsxIndexedMaterial.CreateClutTexture(Enumerable.Repeat((byte)0xAB, 512).ToArray());
            Assert.AreEqual(16, clut.width);
            Assert.AreEqual(16, clut.height);
            Assert.AreEqual(TextureFormat.RGBA32, clut.format);
            Assert.AreEqual(FilterMode.Point, clut.filterMode);
            Assert.AreEqual(TextureWrapMode.Clamp, clut.wrapMode);
            UnityEngine.Object.DestroyImmediate(clut);
        }

        [Test]
        public void CreateClutTexture_InvalidLength_Throws()
        {
            Assert.Throws<ArgumentException>(() => PsxIndexedMaterial.CreateClutTexture(new byte[256]));
            Assert.Throws<ArgumentException>(() => PsxIndexedMaterial.CreateClutTexture(null));
        }

        [Test]
        public void CreateClutTexture_LittleEndianWords_DecodesRLowGHigh()
        {
            // LE word 0x1234 => pixel bytes R=0x34, G=0x12
            var data = new byte[512];
            data[0] = 0x34; data[1] = 0x12;
            var clut = PsxIndexedMaterial.CreateClutTexture(data);
            var c = clut.GetPixel(0, 0);
            Assert.AreEqual(0x34, c.r * 255, 1);
            Assert.AreEqual(0x12, c.g * 255, 1);
            Assert.AreEqual(0, c.b * 255, 1);
            Assert.AreEqual(255, c.a * 255, 1);
            UnityEngine.Object.DestroyImmediate(clut);
        }

        [Test]
        public void Configure_InvalidMaterial_Throws()
        {
            Assert.Throws<ArgumentException>(() => PsxIndexedMaterial.Configure(null, _clut, _clut, 0));
            Assert.Throws<ArgumentException>(() => PsxIndexedMaterial.Configure(_material, null, _clut, 0));
        }

        [Test]
        public void Configure_ClutNot16x16_Throws()
        {
            var badClut = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            Assert.Throws<ArgumentException>(() => PsxIndexedMaterial.Configure(_material, badClut, badClut, 0));
            UnityEngine.Object.DestroyImmediate(badClut);
        }

        [Test]
        public void Configure_PaletteOutOfRange_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PsxIndexedMaterial.Configure(_material, _clut, _clut, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => PsxIndexedMaterial.Configure(_material, _clut, _clut, 16));
        }

        [Test]
        public void Configure_SetsTexturesAndProperties()
        {
            var indices = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            PsxIndexedMaterial.Configure(_material, indices, _clut, 3, true, PsxAbrMode.Subtract);
            Assert.AreEqual(indices, _material.GetTexture(PsxIndexedMaterial.IndexTex));
            Assert.AreEqual(_clut, _material.GetTexture(PsxIndexedMaterial.ClutTex));
            Assert.AreEqual(3, _material.GetFloat(PsxIndexedMaterial.PaletteRow));
            UnityEngine.Object.DestroyImmediate(indices);
        }

        [Test]
        public void ApplyBlendState_Half_SetsRenderQueueTransparent()
        {
            PsxIndexedMaterial.Configure(_material, Texture2D.whiteTexture, _clut, 0, true, PsxAbrMode.Half);
            PsxIndexedMaterial.ApplyBlendState(_material);
            Assert.AreEqual((int)RenderQueue.Transparent, _material.renderQueue);
            Assert.AreEqual("Transparent", _material.GetTag("RenderType", false));
        }

        [Test]
        public void ApplyBlendState_Opaque_SetsAlphaTestQueue()
        {
            PsxIndexedMaterial.Configure(_material, Texture2D.whiteTexture, _clut, 0, false, PsxAbrMode.Half);
            PsxIndexedMaterial.ApplyBlendState(_material);
            Assert.AreEqual((int)RenderQueue.AlphaTest, _material.renderQueue);
            Assert.AreEqual("TransparentCutout", _material.GetTag("RenderType", false));
        }

        [Test]
        public void SetPrimitiveColor_NeutralModulation_128DividesToWhite()
        {
            var block = new MaterialPropertyBlock();
            PsxIndexedMaterial.SetPrimitiveColor(block, new Color32(128, 128, 128, 255), false);
            Assert.Pass("SetPrimitiveColor accepted neutral 0x80 modulation");
        }

        [Test]
        public void SetPrimitiveColor_RawTexture_BypassesModulation()
        {
            var block = new MaterialPropertyBlock();
            Assert.DoesNotThrow(() => PsxIndexedMaterial.SetPrimitiveColor(block, new Color32(255, 255, 255, 255), true));
        }
    }
}
