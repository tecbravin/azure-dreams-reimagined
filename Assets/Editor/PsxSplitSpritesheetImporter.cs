using System.IO;
using UnityEditor;
using UnityEngine;

namespace AzureDreamsReimagined.Editor
{
    public static class PsxSplitSpritesheetImporter
    {
        [MenuItem("Azure Dreams/Rendering/Import Split Spritesheets")]
        public static void ImportSplitSpritesheets()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string srcDir = "Assets/AzureDreams/SpritersResource";
            string prefabRoot = "Assets/AzureDreams/Prefabs";

            // Categories and their patterns
            var categories = new[]
            {
                new { Prefix = "MonsterPortrait", Folder = "MonsterPortraits", Pattern = "MonsterPortrait_{0:D2}.png" },
                new { Prefix = "Mugshot", Folder = "Mugshots", Pattern = "Mugshot_{0:D2}.png" },
                new { Prefix = "KohFace", Folder = "KohFaces", Pattern = "KohFace_{0:D2}.png" },
                new { Prefix = "Font", Folder = "Fonts", Pattern = "Font_{0:D2}.png" },
                new { Prefix = "Pot", Folder = "Props", Pattern = "Pot_{0:D2}.png" },
                new { Prefix = "Dish", Folder = "Props", Pattern = "Dish_{0:D2}.png" }
            };

            int total = 0;
            foreach (var cat in categories)
            {
                string folderPath = Path.Combine(projectRoot, prefabRoot, cat.Folder);
                Directory.CreateDirectory(folderPath);

                int count = 0;
                for (int i = 1; i <= 999; i++)
                {
                    string fileName = string.Format(cat.Pattern, i);
                    string texPath = srcDir + "/" + fileName;
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                    if (!tex) break;

                    // Create sprite
                    var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100);
                    string spritePath = srcDir + "/" + Path.GetFileNameWithoutExtension(fileName) + ".asset";
                    var existing = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                    if (existing) { UnityEngine.Object.DestroyImmediate(sprite); sprite = existing; }
                    else AssetDatabase.CreateAsset(sprite, spritePath);

                    // Create prefab
                    var go = new GameObject(fileName.Replace(".png", ""));
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    string prefabPath = $"{prefabRoot}/{cat.Folder}/{fileName.Replace(".png", "")}.prefab";
                    PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                    UnityEngine.Object.DestroyImmediate(go);
                    count++;
                }
                Debug.Log($"[PsxSplitSpritesheetImporter] {cat.Folder}: {count} sprites/prefabs");
                total += count;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[PsxSplitSpritesheetImporter] Total imported: {total} sprites");
        }
    }
}
