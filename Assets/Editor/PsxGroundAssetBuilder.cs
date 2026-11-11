using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace AzureDreamsReimagined.Editor
{
    public static class PsxGroundAssetBuilder
    {
        [MenuItem("Azure Dreams/Rendering/Build Ground Assets")]
        public static void BuildGroundAssets()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;

            // 1. Ensure floor tileset model is imported
            string modelPath = "Assets/AzureDreams/Models/floor_tileset.obj";
            if (!File.Exists(Path.Combine(projectRoot, modelPath)))
            {
                Debug.LogError("Floor tileset model not found at " + modelPath);
                return;
            }

            // 2. Create ground material using vertex-lit shader
            string groundMatPath = "Assets/AzureDreams/Materials/Ground_Tileset.mat";
            Material groundMat;
            if (File.Exists(Path.Combine(projectRoot, groundMatPath)))
                groundMat = AssetDatabase.LoadAssetAtPath<Material>(groundMatPath);
            else
            {
                var shader = Shader.Find("AzureDreams/PSX Vertex Lit");
                groundMat = new Material(shader);
                groundMat.name = "Ground_Tileset";
                groundMat.color = new Color(0.8f, 0.8f, 0.8f);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(projectRoot, groundMatPath)));
                AssetDatabase.CreateAsset(groundMat, groundMatPath);
            }

            // 3. Create ground prefab from floor tileset
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model)
            {
                var prefabPath = "Assets/AzureDreams/Prefabs/Ground_Tileset.prefab";
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.name = "Ground_Tileset";
                var renderer = instance.GetComponent<Renderer>();
                if (renderer) renderer.sharedMaterial = groundMat;
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                UnityEngine.Object.DestroyImmediate(instance);
                Debug.Log("Ground prefab created: " + prefabPath);
            }

            // 4. Configure mipmaps on all textures
            ConfigureMipmaps(projectRoot);

            Debug.Log("[PsxGroundAssetBuilder] Ground assets built.");
        }

        private static void ConfigureMipmaps(string projectRoot)
        {
            string[] textureDirs = {
                "Assets/AzureDreams/Textures"
            };

            int configured = 0;
            foreach (var dir in textureDirs)
            {
                string fullDir = Path.Combine(projectRoot, dir);
                if (!Directory.Exists(fullDir)) continue;

                var files = Directory.GetFiles(fullDir, "*.png", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    string assetPath = file.Replace(projectRoot + "\\", "").Replace("\\", "/");
                    var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (importer != null)
                    {
                        importer.mipmapEnabled = true;
                        importer.filterMode = FilterMode.Bilinear;
                        importer.textureCompression = TextureImporterCompression.Compressed;
                        importer.SaveAndReimport();
                        configured++;
                    }
                }
            }
            Debug.Log("[PsxGroundAssetBuilder] Configured mipmaps on " + configured + " textures.");
        }
    }
}
