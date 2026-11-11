using System.IO;
using UnityEditor;
using UnityEngine;

namespace AzureDreamsReimagined.Editor
{
    public static class PsxTowerAssetBuilder
    {
        [MenuItem("Azure Dreams/Rendering/Build Tower Assets")]
        public static void BuildTowerAssets()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;

            // 1. Floor tile material (vertex-lit, PS1 style)
            CreateMaterial(projectRoot, "Floor_Tile", "AzureDreams/PSX Vertex Lit",
                new Color(0.7f, 0.65f, 0.6f));

            // 2. Wall material (stone, vertex-lit)
            CreateMaterial(projectRoot, "Wall_Stone", "AzureDreams/PSX Vertex Lit",
                new Color(0.5f, 0.45f, 0.4f));

            // 3. Background skybox materials (one per tower section)
            string[] skyColors = { "0.1,0.1,0.2", "0.15,0.1,0.25", "0.2,0.15,0.3", "0.1,0.05,0.15" };
            string[] skyNames = { "Sky_Tower1", "Sky_Tower2", "Sky_Tower3", "Sky_Tower4" };
            for (int i = 0; i < skyColors.Length; i++)
            {
                var parts = skyColors[i].Split(',');
                var color = new Color(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]));
                CreateSkyboxMaterial(projectRoot, skyNames[i], color);
            }

            // 4. Ground fog material (for depth)
            CreateMaterial(projectRoot, "Ground_Fog", "AzureDreams/PSX Vertex Lit",
                new Color(0.4f, 0.4f, 0.5f));

            // 5. Floor tile prefab (from floor_tileset.obj)
            string modelPath = "Assets/AzureDreams/Models/floor_tileset.obj";
            if (File.Exists(Path.Combine(projectRoot, modelPath)))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (model)
                {
                    var groundMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/AzureDreams/Materials/Floor_Tile.mat");
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                    instance.name = "Floor_Tileset";
                    var renderer = instance.GetComponent<Renderer>();
                    if (renderer && groundMat) renderer.sharedMaterial = groundMat;
                    string prefabPath = "Assets/AzureDreams/Prefabs/Floor_Tileset.prefab";
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(projectRoot, prefabPath)));
                    PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                    UnityEngine.Object.DestroyImmediate(instance);
                    Debug.Log("Floor tileset prefab: " + prefabPath);
                }
            }

            // 6. Tower background prefab (skybox + fog)
            CreateBackgroundPrefab(projectRoot);

            Debug.Log("[PsxTowerAssetBuilder] Tower assets built.");
        }

        private static void CreateMaterial(string projectRoot, string name, string shaderName, Color color)
        {
            string matPath = "Assets/AzureDreams/Materials/" + name + ".mat";
            if (File.Exists(Path.Combine(projectRoot, matPath))) return;

            var shader = Shader.Find(shaderName);
            var mat = new Material(shader);
            mat.name = name;
            mat.color = color;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(projectRoot, matPath)));
            AssetDatabase.CreateAsset(mat, matPath);
        }

        private static void CreateSkyboxMaterial(string projectRoot, string name, Color color)
        {
            string matPath = "Assets/AzureDreams/Materials/" + name + ".mat";
            if (File.Exists(Path.Combine(projectRoot, matPath))) return;

            var shader = Shader.Find("Skybox/6 Sided");
            if (!shader) shader = Shader.Find("Skybox/Procedural");
            var mat = new Material(shader);
            mat.name = name;
            mat.SetColor("_Tint", color);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(projectRoot, matPath)));
            AssetDatabase.CreateAsset(mat, matPath);
        }

        private static void CreateBackgroundPrefab(string projectRoot)
        {
            var go = new GameObject("TowerBackground");
            var sky = go.AddComponent<Skybox>();
            var skyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/AzureDreams/Materials/Sky_Tower1.mat");
            if (skyMat) sky.material = skyMat;

            string prefabPath = "Assets/AzureDreams/Prefabs/TowerBackground.prefab";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(projectRoot, prefabPath)));
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log("Tower background prefab: " + prefabPath);
        }
    }
}
