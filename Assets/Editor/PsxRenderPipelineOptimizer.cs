using System.IO;
using UnityEditor;
using UnityEngine;

namespace AzureDreamsReimagined.Editor
{
    public static class PsxRenderPipelineOptimizer
    {
        [MenuItem("Azure Dreams/Rendering/Optimize Pipeline")]
        public static void OptimizePipeline()
        {
            // 1. Enable GPU instancing on all materials
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/AzureDreams" });
            int instanced = 0;
            foreach (var guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat && !mat.enableInstancing)
                {
                    mat.enableInstancing = true;
                    EditorUtility.SetDirty(mat);
                    instanced++;
                }
            }
            Debug.Log($"GPU instancing enabled on {instanced} materials");

            // 2. Set texture import settings for WebGL
            string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/AzureDreams" });
            int optimized = 0;
            foreach (var guid in texGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer)
                {
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    importer.crunchedCompression = true;
                    importer.compressionQuality = 50;
                    importer.mipmapEnabled = true;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.SaveAndReimport();
                    optimized++;
                }
            }
            Debug.Log($"Texture import optimized: {optimized} textures");

            // 3. Mark environment objects as static
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/AzureDreams/Prefabs" });
            foreach (var guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go && (go.name.Contains("Floor") || go.name.Contains("Wall") || go.name.Contains("Ground")))
                {
                    go.isStatic = true;
                    EditorUtility.SetDirty(go);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[PsxRenderPipelineOptimizer] Pipeline optimized.");
        }
    }
}
