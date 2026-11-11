using System.IO;
using UnityEditor;
using UnityEngine;

namespace AzureDreamsReimagined.Editor
{
    public static class PsxSpritersResourceIntegrator
    {
        [MenuItem("Azure Dreams/Rendering/Integrate Spriters Resource")]
        public static void IntegrateSpritersResource()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string srcDir = "Assets/AzureDreams/SpritersResource";

            // 1. Create tower floor prefabs (one per floor, using Spriters Resource textures)
            CreateTowerFloorPrefabs(projectRoot, srcDir);

            // 2. Create character prefabs
            CreateCharacterPrefabs(projectRoot, srcDir);

            // 3. Create monster prefabs
            CreateMonsterPrefabs(projectRoot, srcDir);

            // 4. Create UI prefabs
            CreateUIPrefabs(projectRoot, srcDir);

            // 5. Create miscellaneous prefabs
            CreateMiscPrefabs(projectRoot, srcDir);

            AssetDatabase.SaveAssets();
            Debug.Log("[PsxSpritersResourceIntegrator] Integration complete.");
        }

        private static void CreateTowerFloorPrefabs(string projectRoot, string srcDir)
        {
            string[] towerFiles = Directory.GetFiles(Path.Combine(projectRoot, srcDir), "PlayStation - Azure Dreams - Tower - Monster Tower *.png");
            int count = 0;
            foreach (var file in towerFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                string matName = "TowerFloor_" + fileName.Replace("PlayStation - Azure Dreams - Tower - ", "").Replace(" ", "_");
                string matPath = "Assets/AzureDreams/Materials/" + matName + ".mat";

                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (!mat) continue;

                // Create a quad prefab with the floor texture
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = matName;
                var renderer = go.GetComponent<Renderer>();
                renderer.sharedMaterial = mat;
                go.transform.rotation = Quaternion.Euler(90, 0, 0); // Flat on ground

                string prefabPath = "Assets/AzureDreams/Prefabs/TowerFloors/" + matName + ".prefab";
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(projectRoot, prefabPath)));
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                UnityEngine.Object.DestroyImmediate(go);
                count++;
            }
            Debug.Log($"Created {count} tower floor prefabs");
        }

        private static void CreateCharacterPrefabs(string projectRoot, string srcDir)
        {
            string[] charFiles = new[] {
                "PlayStation - Azure Dreams - Characters - Cherrl & Chappie.png",
                "PlayStation - Azure Dreams - Characters - Guy, Wreath & Young Koh.png",
                "PlayStation - Azure Dreams - Characters - Nico, Weedy & Jorda.png",
                "PlayStation - Azure Dreams - Characters - Selfi & Ghosh.png",
                "PlayStation - Azure Dreams - Characters - Young Nico.png"
            };

            foreach (var fileName in charFiles)
            {
                string matName = "Sprite_" + fileName.Replace("PlayStation - Azure Dreams - ", "").Replace(" ", "_").Replace("-", "").Replace("&", "").Replace(",", "");
                string matPath = "Assets/AzureDreams/Materials/" + matName + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (!mat) continue;

                var go = new GameObject(matName);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.material = mat;
                string texPath = srcDir + "/" + fileName;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex)
                {
                    // Create sprite from texture
                    var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100);
                    sr.sprite = sprite;
                }

                string prefabPath = "Assets/AzureDreams/Prefabs/Characters/" + matName + ".prefab";
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(projectRoot, prefabPath)));
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                UnityEngine.Object.DestroyImmediate(go);
            }
            Debug.Log("Created character prefabs");
        }

        private static void CreateMonsterPrefabs(string projectRoot, string srcDir)
        {
            string[] monsterFiles = new[] {
                "PlayStation - Azure Dreams - Monsters - Beldo.png",
                "PlayStation - Azure Dreams - Monsters - Kewne.png",
                "PlayStation - Azure Dreams - Monsters - Trolls.png"
            };

            foreach (var fileName in monsterFiles)
            {
                string matName = "Sprite_" + fileName.Replace("PlayStation - Azure Dreams - ", "").Replace(" ", "_").Replace("-", "");
                string matPath = "Assets/AzureDreams/Materials/" + matName + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (!mat) continue;

                var go = new GameObject(matName);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.material = mat;
                string texPath = srcDir + "/" + fileName;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex)
                {
                    var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100);
                    sr.sprite = sprite;
                }

                string prefabPath = "Assets/AzureDreams/Prefabs/Monsters/" + matName + ".prefab";
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(projectRoot, prefabPath)));
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                UnityEngine.Object.DestroyImmediate(go);
            }
            Debug.Log("Created monster prefabs");
        }

        private static void CreateUIPrefabs(string projectRoot, string srcDir)
        {
            string[] uiFiles = new[] {
                "PlayStation - Azure Dreams - UI - Fonts.png",
                "PlayStation - Azure Dreams - UI - Builders Mini Map.png",
                "PlayStation - Azure Dreams - Tower - OpenBag.png"
            };

            foreach (var fileName in uiFiles)
            {
                string matName = "Sprite_" + fileName.Replace("PlayStation - Azure Dreams - ", "").Replace(" ", "_").Replace("-", "");
                string matPath = "Assets/AzureDreams/Materials/" + matName + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (!mat) continue;

                var go = new GameObject(matName);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.material = mat;
                string texPath = srcDir + "/" + fileName;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex)
                {
                    var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100);
                    sr.sprite = sprite;
                }

                string prefabPath = "Assets/AzureDreams/Prefabs/UI/" + matName + ".prefab";
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(projectRoot, prefabPath)));
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                UnityEngine.Object.DestroyImmediate(go);
            }
            Debug.Log("Created UI prefabs");
        }

        private static void CreateMiscPrefabs(string projectRoot, string srcDir)
        {
            string[] miscFiles = new[] {
                "PlayStation - Azure Dreams - Miscellaneous - Angel.png",
                "PlayStation - Azure Dreams - Miscellaneous - Cat & Bird.png",
                "PlayStation - Azure Dreams - Miscellaneous - Koh Status Faces.png",
                "PlayStation - Azure Dreams - Miscellaneous - Monster Portraits.png",
                "PlayStation - Azure Dreams - Miscellaneous - Mugshots.png",
                "PlayStation - Azure Dreams - Miscellaneous - Pots and Table Decorations.png",
                "PlayStation - Azure Dreams - Miscellaneous - Restaurant Dishes.png",
                "PlayStation - Azure Dreams - Miscellaneous - Title Screen Background.png",
                "PlayStation - Azure Dreams - Tower - Elevator.png"
            };

            foreach (var fileName in miscFiles)
            {
                string matName = "Sprite_" + fileName.Replace("PlayStation - Azure Dreams - ", "").Replace(" ", "_").Replace("-", "");
                string matPath = "Assets/AzureDreams/Materials/" + matName + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (!mat) continue;

                var go = new GameObject(matName);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.material = mat;
                string texPath = srcDir + "/" + fileName;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex)
                {
                    var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100);
                    sr.sprite = sprite;
                }

                string prefabPath = "Assets/AzureDreams/Prefabs/Misc/" + matName + ".prefab";
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(projectRoot, prefabPath)));
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                UnityEngine.Object.DestroyImmediate(go);
            }
            Debug.Log("Created misc prefabs");
        }
    }
}
