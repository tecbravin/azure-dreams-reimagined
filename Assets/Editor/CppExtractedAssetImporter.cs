using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AzureDreamsReimagined.Editor
{
    public sealed class CppExtractedAssetImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            bool portrait = assetPath.StartsWith("Assets/AzureDreams/Sprites/Portraits/AdResourcesDumper/", StringComparison.Ordinal);
            bool extractedTexture = assetPath.StartsWith("Assets/AzureDreams/Textures/Extracted/", StringComparison.Ordinal);
            if (!portrait && !extractedTexture) return;
            var texture = (TextureImporter)assetImporter;
            texture.textureType = portrait ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (portrait) { texture.spriteImportMode = SpriteImportMode.Single; texture.spritePixelsPerUnit = 64; }
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.mipmapEnabled = false;
            texture.textureCompression = TextureImporterCompression.Uncompressed;
            texture.maxTextureSize = 4096;
            bool coverage = assetPath.EndsWith("_coverage.png", StringComparison.Ordinal);
            texture.sRGBTexture = !coverage;
            texture.alphaIsTransparency = !coverage;
            if (coverage) texture.alphaSource = TextureImporterAlphaSource.None;
        }

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/AzureDreams/Models/Extracted/VirtualMonsbaia/", StringComparison.Ordinal)) return;
            var model = (ModelImporter)assetImporter;
            model.importAnimation = false;
            model.importCameras = false;
            model.importLights = false;
            model.importNormals = ModelImporterNormals.Calculate;
        }

        public static void Verify()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            int sprites = 0, models = 0, textures = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/AzureDreams/Sprites/Portraits/AdResourcesDumper" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null) throw new InvalidOperationException("Sprite import failed: " + path);
                sprites++;
            }
            foreach (string path in Directory.GetFiles("Assets/AzureDreams/Models/Extracted/VirtualMonsbaia", "*.obj", SearchOption.AllDirectories))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
                if (model == null || model.GetComponentsInChildren<MeshFilter>(true).Length == 0)
                    throw new InvalidOperationException("Model import failed: " + path);
                models++;
            }
            textures = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/AzureDreams/Textures/Extracted" }).Length;
            if (sprites != 1669 || models != 41 || textures != 1267)
                throw new InvalidOperationException($"Unexpected import counts: {sprites} sprites, {models} models, {textures} textures");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/CppExtractedAssetImport.json", $"{{\"sprites\":{sprites},\"models\":{models},\"textures\":{textures},\"status\":\"passed\"}}");
            Debug.Log($"CPP_EXPORT_IMPORT_OK: {sprites} sprites, {models} OBJ models, {textures} textures.");
        }
    }
}
