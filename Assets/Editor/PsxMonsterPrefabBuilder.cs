using System;
using System.IO;
using UnityEditor;
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
                    if (existing && existing.GetComponentInChildren<SpriteRenderer>()) continue;
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BarongSpritePath);
                    if (!sprite) throw new InvalidDataException("Missing Barong sprite at " + BarongSpritePath);
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
    }
}
