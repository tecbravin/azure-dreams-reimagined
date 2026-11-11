using System.IO;
using UnityEditor;
using UnityEngine;

namespace AzureDreamsReimagined.Editor
{
    /// <summary>
    /// Generates monster prefabs from quarantined sprite frames.
    /// Groups frame chains by resource ID and creates animation-ready prefabs.
    /// </summary>
    public static class PsxQuarantinedMonsterPrefabBuilder
    {
        [MenuItem("Azure Dreams/Characters/Build Quarantined Monster Prefabs")]
        public static void BuildQuarantinedMonsterPrefabs()
        {
            string root = "Assets/AzureDreams/Sprites/Quarantined";
            string prefabRoot = "Assets/AzureDreams/Prefabs/Monsters";
            string quarantinedPrefabDirectory = $"{prefabRoot}/Quarantined";
            
            if (!AssetDatabase.IsValidFolder(root))
            {
                Debug.LogError("[PsxQuarantinedMonsterPrefabBuilder] Quarantined folder not found");
                return;
            }

            if (!AssetDatabase.IsValidFolder(quarantinedPrefabDirectory))
                AssetDatabase.CreateFolder(prefabRoot, "Quarantined");

            var resourceDirs = Directory.GetDirectories(root, "resource_*");
            Debug.Log($"[PsxQuarantinedMonsterPrefabBuilder] Building prefabs for {resourceDirs.Length} resources");

            int built = 0;
            foreach (var resDir in resourceDirs)
            {
                string resName = Path.GetFileName(resDir); // e.g., "resource_047"
                int resourceId = int.Parse(resName.Replace("resource_", ""));
                
                string monsterName = $"Monster_Quarantined_{resourceId:D3}";
                // Create root monster object
                var monsterRoot = new GameObject(monsterName);
                
                // Add sprite renderer
                var sr = monsterRoot.AddComponent<SpriteRenderer>();
                
                // Find first frame as default sprite
                var frames = Directory.GetFiles(resDir, "frame_chain_*.png");
                if (frames.Length > 0)
                {
                    string firstFramePath = frames[0].Replace(Application.dataPath, "Assets").Replace('\\', '/');
                    var defaultTex = AssetDatabase.LoadAssetAtPath<Texture2D>(firstFramePath);
                    if (!defaultTex)
                    {
                        AssetDatabase.ImportAsset(firstFramePath, ImportAssetOptions.ForceSynchronousImport);
                        defaultTex = AssetDatabase.LoadAssetAtPath<Texture2D>(firstFramePath);
                    }
                    if (defaultTex)
                    {
                        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(firstFramePath);
                        sr.sprite = sprite;
                    }
                }
                
                // Add components
                var animator = monsterRoot.AddComponent<Animator>();
                var collider = monsterRoot.AddComponent<BoxCollider2D>();
                var rb = monsterRoot.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0;
                
                // Create animation clips from frame chains
                var animCtrl = monsterRoot.AddComponent<PsxAnimationController>();
                
                string prefabPath = $"{quarantinedPrefabDirectory}/{monsterName}.prefab";
                PrefabUtility.SaveAsPrefabAsset(monsterRoot, prefabPath);
                UnityEngine.Object.DestroyImmediate(monsterRoot);
                
                Debug.Log($"  Built: {monsterName} ({frames.Length} frames)");
                built++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[PsxQuarantinedMonsterPrefabBuilder] Complete: {built} prefabs built");
        }
    }
}
