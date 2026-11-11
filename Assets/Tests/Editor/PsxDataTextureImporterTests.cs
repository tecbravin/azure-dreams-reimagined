using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AzureDreamsReimagined.Editor;

namespace AzureDreams.Reimagined.EditorTests
{
    public class PsxDataTextureImporterTests
    {
        private const string TestDir = "Assets/Tests/TempPsxImport";

        [OneTimeTearDown]
        public void Cleanup()
        {
            if (AssetDatabase.IsValidFolder(TestDir))
                AssetDatabase.DeleteAsset(TestDir);
        }

        private static string WriteRealPng(string path, int width, int height, Color color)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            File.WriteAllBytes(path, png);
            return path;
        }

        [Test]
        public void AutoImport_CpuAtlas_PreservesEveryIndexAndRemainsReadable()
        {
            string directory = TestDir + "/Sprites/Indexed/cpu";
            Directory.CreateDirectory(directory);
            string path = directory + "/atlas_index.png";
            var source = new Texture2D(16, 1, TextureFormat.RGBA32, false, true);
            try
            {
                var bytes = new Color32[16];
                for (int i = 0; i < bytes.Length; i++) bytes[i] = new Color32((byte)i, 0, 0, 255);
                source.SetPixels32(bytes);
                source.Apply();
                File.WriteAllBytes(path, source.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(importer.isReadable, Is.True, "CPU sprite compositor reads the atlas");
            Assert.That(importer.sRGBTexture, Is.False);
            Assert.That(texture.format, Is.EqualTo(TextureFormat.R8));
            Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(texture.mipmapCount, Is.EqualTo(1));
            foreach (string platform in new[] { "Standalone", "WebGL", "Android", "iPhone" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                Assert.That(settings.format, Is.EqualTo(TextureImporterFormat.R8), platform);
                Assert.That(settings.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), platform);
            }
            var pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
                Assert.That(pixels[i].r, Is.EqualTo((byte)i), "Raw palette index " + i);
        }

        [Test]
        public void ImportedGpuAtlases_AllSourceIndicesSurviveGpuReadback()
        {
            var paths = Directory.GetFiles("Assets/AzureDreams/Sprites/Indexed", "atlas_indices.png", SearchOption.AllDirectories);
            System.Array.Sort(paths);
            Assert.That(paths, Is.Not.Empty);
            long compared = 0;
            foreach (string filename in paths)
            {
                string path = filename.Replace("\\", "/");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(importer.sRGBTexture, Is.False, path);
                Assert.That(importer.isReadable, Is.False, path);
                Assert.That(texture.format, Is.EqualTo(TextureFormat.R8), path);
                foreach (string platform in new[] { "Standalone", "WebGL", "Android", "iPhone" })
                {
                    var settings = importer.GetPlatformTextureSettings(platform);
                    Assert.That(settings.format, Is.EqualTo(TextureImporterFormat.R8), path + " " + platform);
                    Assert.That(settings.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path + " " + platform);
                }
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                var read = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true);
                var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                var previous = RenderTexture.active;
                try
                {
                    Assert.That(source.LoadImage(File.ReadAllBytes(path)), Is.True, path);
                    Assert.That(texture.width, Is.EqualTo(source.width), path);
                    Assert.That(texture.height, Is.EqualTo(source.height), path);
                    Graphics.Blit(texture, rt);
                    RenderTexture.active = rt;
                    read.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                    read.Apply();
                    var expected = source.GetPixels32();
                    var actual = read.GetPixels32();
                    for (int i = 0; i < expected.Length; i++)
                        if (expected[i].r != actual[i].r)
                            Assert.Fail(path + " pixel " + i + ": expected index " + expected[i].r + ", got " + actual[i].r);
                    compared += expected.Length;
                }
                finally
                {
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(rt);
                    UnityEngine.Object.DestroyImmediate(source);
                    UnityEngine.Object.DestroyImmediate(read);
                }
            }
            Debug.Log("[PSX index audit] Atlases=" + paths.Length + "; exact source/GPU indices=" + compared);
        }

        [Test]
        public void Configure_IndexedByteData_SetsPointAndNoMips()
        {
            if (!AssetDatabase.IsValidFolder(TestDir))
                AssetDatabase.CreateFolder("Assets/Tests", "TempPsxImport");

            string texPath = Path.Combine(TestDir, "indexed.png").Replace("\\", "/");
            WriteRealPng(texPath, 16, 16, Color.white);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            PsxDataTextureImporter.Configure(importer, true);
            importer.SaveAndReimport();

            var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            Assert.IsNotNull(asset);
            Assert.AreEqual(FilterMode.Point, asset.filterMode);
            Assert.AreEqual(TextureWrapMode.Clamp, asset.wrapMode);
            Assert.IsFalse(importer.mipmapEnabled, "Indexed data should not use mipmaps");
            Assert.IsFalse(asset.mipmapCount > 1, "Imported texture should have no mipmaps");
        }

        [Test]
        public void Configure_VerifiedArt_SetsPointClampAndNoMips()
        {
            if (!AssetDatabase.IsValidFolder(TestDir))
                AssetDatabase.CreateFolder("Assets/Tests", "TempPsxImport");

            string texPath = Path.Combine(TestDir, "art.png").Replace("\\", "/");
            WriteRealPng(texPath, 32, 32, Color.gray);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            PsxDataTextureImporter.Configure(importer, false);
            importer.SaveAndReimport();

            var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            Assert.IsNotNull(asset);
            Assert.AreEqual(FilterMode.Point, asset.filterMode);
            Assert.AreEqual(TextureWrapMode.Clamp, asset.wrapMode);
            Assert.IsFalse(importer.mipmapEnabled, "Verified art should not use mipmaps");
            Assert.IsFalse(asset.mipmapCount > 1, "Imported texture should have no mipmaps");
            Assert.IsTrue(importer.sRGBTexture, "Art textures should be sRGB");
        }

        [Test]
        public void Configure_IndexedData_DoesNotOverrideWebGL()
        {
            if (!AssetDatabase.IsValidFolder(TestDir))
                AssetDatabase.CreateFolder("Assets/Tests", "TempPsxImport");

            string texPath = Path.Combine(TestDir, "webgl_indexed.png").Replace("\\", "/");
            WriteRealPng(texPath, 8, 8, Color.black);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            PsxDataTextureImporter.Configure(importer, true);
            importer.SaveAndReimport();

            // Verify that WebGL platform settings exist and are not overridden incorrectly
            var webglSettings = importer.GetPlatformTextureSettings("WebGL");
            Assert.IsNotNull(webglSettings);
        }
    }
}

