using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using AzureDreamsReimagined.Rendering;

namespace AzureDreamsReimagined.Editor
{
    public static class PsxMonsterPrefabBuilder
    {
        private static readonly string[] MonsterNames = new string[]
        {
            "", // 0
            "Hikewne", "Kewne", "Dragon", "Kid", "Ifrit", "Flame", "Grineut", "Griffon",
            "Saber", "Snowman", "Ashra", "Arachne", "Battnel", "Nyuel", "Death", "Clown",
            "Univern", "Unicorn", "Metal", "Block", "Pulunpa", "Troll", "Noise", "U_Boat",
            "Baloon", "Dreamin", "Blume", "Volcano", "Cyclone", "Manoeva", "Barong", "Picket",
            "Kraken", "Weadog", "Stealth", "Viper", "Naplass", "Zu", "Mandara", "Killer",
            "Garuda", "Glacier", "Tyrant", "Golem", "Maximum", "Frog", "Salamander", "Guy",
            "Ghosh", "Selfi", "WallOfFire", "WallOfIce", "WallOfStorm", "Rock", "Beldo"
        };

        private static readonly int[] Quarantined = { 47, 48, 51, 52, 53, 54 };
        private const string BarongSpritePath = "Assets/AzureDreams/Sprites/Monsters/Community/monster_1f/animation_01/monster_1f_animation_01_frame_00.png";

        [MenuItem("Azure Dreams/Rendering/Build Monster Prefabs")]
        public static void BuildMonsterPrefabs()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string indexedRoot = "Assets/AzureDreams/Sprites/Indexed";
            string prefabDir = "Assets/AzureDreams/Prefabs/Monsters";
            Directory.CreateDirectory(Path.Combine(projectRoot, prefabDir));

            int created = 0;
            for (int rid = 1; rid <= 57; rid++)
            {
                if (Array.IndexOf(Quarantined, rid) >= 0) continue;

                string resourceFolder = indexedRoot + "/resource_" + rid.ToString("D3");
                string opaqueMatPath = resourceFolder + "/opaque.mat";
                string name = rid < MonsterNames.Length ? MonsterNames[rid] : "Monster_" + rid;
                string prefabPath = prefabDir + "/" + name + ".prefab";
                var animated = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (animated && animated.GetComponentInChildren<MonsterTurnSpriteView>()) continue;
                if (name == "Barong")
                {
                    var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (existing && existing.GetComponentInChildren<SpriteRenderer>() is { sprite: var existingSprite } && existingSprite) continue;
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BarongSpritePath);
                    if (!sprite && File.Exists(Path.Combine(projectRoot, BarongSpritePath)))
                    {
                        AssetDatabase.ImportAsset(BarongSpritePath,
                            ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BarongSpritePath);
                    }
                    if (!sprite)
                    {
                        Debug.LogError("Barong sprite could not be imported: " + BarongSpritePath);
                        continue;
                    }
                    var barong = new GameObject(name);
                    var visual = new GameObject("Sprite");
                    visual.transform.SetParent(barong.transform, false);
                    visual.transform.localPosition = Vector3.up * (sprite.bounds.extents.y - 0.55f);
                    visual.AddComponent<SpriteRenderer>().sprite = sprite;
                    visual.AddComponent<CameraFacingSprite>();
                    PrefabUtility.SaveAsPrefabAsset(barong, prefabPath);
                    UnityEngine.Object.DestroyImmediate(barong);
                    created++;
                    continue;
                }
                if (!File.Exists(Path.Combine(projectRoot, opaqueMatPath))) continue;
                if (name == "Pulunpa" || name == "Kewne" || name == "Noise" || name == "Troll" || name == "Flame" || name == "Cyclone" || name == "Baloon")
                {
                    var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (existing && (existing.GetComponentInChildren<CameraFacingSprite>() ||
                        existing.GetComponent<KewneSpriteView>())) continue;
                }

                var go = new GameObject(name);
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Sprite";
                quad.transform.SetParent(go.transform);
                quad.transform.localPosition = Vector3.zero;
                quad.transform.localScale = new Vector3(1f, 1f, 1f);

                var mat = AssetDatabase.LoadAssetAtPath<Material>(opaqueMatPath);
                if (mat)
                    quad.GetComponent<Renderer>().sharedMaterial = mat;

                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                UnityEngine.Object.DestroyImmediate(go);
                created++;
            }

            Debug.Log("[PsxMonsterPrefabBuilder] Created " + created + " monster prefabs in " + prefabDir);
        }

        [MenuItem("Azure Dreams/Rendering/Audit Scene Sprites")]
        private static void AuditSceneSprites()
        {
            int missing = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                    foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        var sprite = new SerializedObject(renderer).FindProperty("m_Sprite");
                        if (sprite == null || sprite.objectReferenceValue ||
                            sprite.objectReferenceEntityIdValue == default(UnityEngine.EntityId)) continue;
                        Debug.LogWarning($"{path}: {renderer.name} has a broken sprite reference.");
                        missing++;
                    }
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }
            Debug.Log($"Scene sprite audit found {missing} broken SpriteRenderer references.");
        }
    }
}
