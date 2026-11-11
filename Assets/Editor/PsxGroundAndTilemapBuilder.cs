using System.IO;
using UnityEditor;
using UnityEngine;

namespace AzureDreamsReimagined.Editor
{
    public static class PsxGroundAndTilemapBuilder
    {
        [MenuItem("Azure Dreams/Rendering/Build Ground and Tilemaps")]
        public static void BuildGroundAndTilemaps()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;

            // 1. Create ground plane material
            CreateGroundMaterial(projectRoot);

            // 2. Create floor tileset prefab variations (one per theme)
            CreateFloorTilesetVariants(projectRoot);

            // 3. Create background images for each tower theme
            CreateBackgroundImages(projectRoot);

            // 4. Create tilemap data assets
            CreateTilemapDataAssets(projectRoot);

            // 5. Create environment prop prefabs (pillars, walls, stairs)
            CreateEnvironmentProps(projectRoot);

            AssetDatabase.SaveAssets();
            Debug.Log("[PsxGroundAndTilemapBuilder] Ground and tilemaps built.");
        }

        private static void CreateGroundMaterial(string projectRoot)
        {
            string matPath = "Assets/AzureDreams/Materials/Ground_Plane.mat";
            if (File.Exists(Path.Combine(projectRoot, matPath))) return;

            var shader = Shader.Find("AzureDreams/PSX Vertex Lit");
            if (!shader) shader = Shader.Find("Standard");
            var mat = new Material(shader);
            mat.name = "Ground_Plane";
            mat.color = new Color(0.4f, 0.38f, 0.35f);
            AssetDatabase.CreateAsset(mat, matPath);
        }

        private static void CreateFloorTilesetVariants(string projectRoot)
        {
            string modelPath = "Assets/AzureDreams/Models/floor_tileset.obj";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (!model) return;

            string[] themes = { "StoneDungeon", "CrystalCave", "LavaChamber", "IcePalace", "DarkTower" };
            foreach (var theme in themes)
            {
                string matPath = $"Assets/AzureDreams/Materials/Floor_{theme}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (!mat) continue;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.name = $"Floor_Tileset_{theme}";
                var renderer = instance.GetComponent<Renderer>();
                if (renderer) renderer.sharedMaterial = mat;

                string prefabPath = $"Assets/AzureDreams/Prefabs/Ground/Floor_Tileset_{theme}.prefab";
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(projectRoot, prefabPath)));
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                UnityEngine.Object.DestroyImmediate(instance);
            }
            Debug.Log("Created floor tileset variants for 5 themes");
        }

        private static void CreateBackgroundImages(string projectRoot)
        {
            // Create background image prefabs from Spriters Resource tower textures
            string[] towerFiles = Directory.GetFiles(Path.Combine(projectRoot, "Assets/AzureDreams/SpritersResource"), "PlayStation - Azure Dreams - Tower - Monster Tower *.png");
            foreach (var file in towerFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                string cleanName = fileName.Replace("PlayStation - Azure Dreams - Tower - ", "").Replace(" ", "_");
                string prefabPath = $"Assets/AzureDreams/Prefabs/Backgrounds/BG_{cleanName}.prefab";

                var go = new GameObject($"BG_{cleanName}");
                var sr = go.AddComponent<SpriteRenderer>();
                string texPath = $"Assets/AzureDreams/SpritersResource/{Path.GetFileName(file)}";
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex)
                {
                    var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100);
                    sr.sprite = sprite;
                    sr.sortingOrder = -10; // Behind everything
                }

                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(projectRoot, prefabPath)));
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                UnityEngine.Object.DestroyImmediate(go);
            }
            Debug.Log($"Created {towerFiles.Length} background prefabs");
        }

        private static void CreateTilemapDataAssets(string projectRoot)
        {
            // Create ScriptableObject tilemap data for each theme
            foreach (TowerFloorTheme theme in System.Enum.GetValues(typeof(TowerFloorTheme)))
            {
                string assetPath = $"Assets/AzureDreams/Data/Tilemap_{theme}.asset";
                var data = ScriptableObject.CreateInstance<TilemapData>();
                data.theme = theme;
                data.floorMaterial = AssetDatabase.LoadAssetAtPath<Material>($"Assets/AzureDreams/Materials/Floor_{theme}.mat");
                data.wallMaterial = AssetDatabase.LoadAssetAtPath<Material>($"Assets/AzureDreams/Materials/Wall_{theme}.mat");
                data.backgroundMaterial = AssetDatabase.LoadAssetAtPath<Material>($"Assets/AzureDreams/Materials/Sky_{theme}.mat");
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(projectRoot, assetPath)));
                AssetDatabase.CreateAsset(data, assetPath);
            }
            Debug.Log("Created tilemap data assets for all themes");
        }

        private static void CreateEnvironmentProps(string projectRoot)
        {
            // Create simple pillar/wall/stairs prefabs
            string[] propNames = { "Pillar", "Wall_Section", "Stairs_Up", "Stairs_Down", "Elevator_Shaft" };
            foreach (var propName in propNames)
            {
                var go = GameObject.CreatePrimitive(propName.Contains("Pillar") ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                go.name = propName;
                var renderer = go.GetComponent<Renderer>();
                if (renderer)
                {
                    string matPath = "Assets/AzureDreams/Materials/Wall_StoneDungeon.mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (mat) renderer.sharedMaterial = mat;
                }
                if (propName.Contains("Stairs"))
                {
                    go.transform.rotation = Quaternion.Euler(0, 0, 45);
                    go.transform.localScale = new Vector3(2, 0.2f, 2);
                }
                if (propName.Contains("Pillar"))
                {
                    go.transform.localScale = new Vector3(0.5f, 3, 0.5f);
                }

                string prefabPath = $"Assets/AzureDreams/Prefabs/Environment/{propName}.prefab";
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(projectRoot, prefabPath)));
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                UnityEngine.Object.DestroyImmediate(go);
            }
            Debug.Log("Created environment prop prefabs");
        }
    }
}
