using System.IO;
using UnityEditor;
using UnityEngine;

namespace AzureDreamsReimagined.Editor
{
    /// <summary>
    /// Imports quarantined sprite frames (resources 47, 51-54) into Unity.
    /// These are 8-bit indexed sprites that were quarantined during initial validation.
    /// Frame chains are already decoded as RGBA PNGs.
    /// </summary>
    public static class PsxQuarantinedSpritesImporter
    {
        [MenuItem("Azure Dreams/Rendering/Import Quarantined Sprites")]
        public static void ImportQuarantinedSprites()
        {
            string root = "Assets/AzureDreams/Sprites/Quarantined";
            
            if (!AssetDatabase.IsValidFolder(root))
            {
                Debug.LogError("[PsxQuarantinedSpritesImporter] Quarantined folder not found: " + root);
                return;
            }

            var resourceDirs = Directory.GetDirectories(root, "resource_*");
            Debug.Log($"[PsxQuarantinedSpritesImporter] Found {resourceDirs.Length} resource directories");

            int totalSprites = 0;
            int totalPrefabs = 0;

            foreach (var resDir in resourceDirs)
            {
                string resName = Path.GetFileName(resDir);
                string prefabDirBase = "Assets/AzureDreams/Prefabs/Quarantined";
                string prefabDir = $"{prefabDirBase}/{resName}";
                
                if (!AssetDatabase.IsValidFolder(prefabDirBase))
                {
                    AssetDatabase.CreateFolder("Assets/AzureDreams/Prefabs", "Quarantined");
                }
                if (!AssetDatabase.IsValidFolder(prefabDir))
                {
                    AssetDatabase.CreateFolder(prefabDirBase, resName);
                }

                var frames = Directory.GetFiles(resDir, "frame_chain_*.png");
                Debug.Log($"[PsxQuarantinedSpritesImporter] {resName}: {frames.Length} frames");

                foreach (var framePath in frames)
                {
                    string fileName = Path.GetFileNameWithoutExtension(framePath);
                    string texPath = framePath.Replace(Application.dataPath, "Assets").Replace('\\', '/');
                    
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                    if (!tex)
                    {
                        // Skip files that are not real PNGs (e.g. YAML dumps, placeholders).
                        if (!File.Exists(framePath) || new FileInfo(framePath).Length < 8 ||
                            !IsLikelyPng(framePath))
                        {
                            Debug.LogWarning($"[PsxQuarantinedSpritesImporter] Skipping non-PNG or empty asset: {texPath}");
                            continue;
                        }

                        AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceSynchronousImport);
                        tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                    }
                    if (!tex)
                    {
                        Debug.LogWarning($"[PsxQuarantinedSpritesImporter] Texture not found or not imported: {texPath}");
                        continue;
                    }

                    // Create sprite in-memory; do NOT save it as a .png asset.
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
                    if (!sprite) { Debug.LogWarning("Sprite subasset unavailable: " + texPath); continue; }

                    // Create prefab
                    var go = new GameObject(fileName);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    
                    string prefabPath = $"{prefabDir}/{fileName}.prefab";
                    PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                    UnityEngine.Object.DestroyImmediate(go);
                    
                    totalSprites++;
                    totalPrefabs++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[PsxQuarantinedSpritesImporter] Complete: {totalSprites} sprites, {totalPrefabs} prefabs");
        }

        private static bool IsLikelyPng(string path)
        {
            try
            {
                var span = File.ReadAllBytes(path);
                return span.Length >= 8 && span[0] == 0x89 && span[1] == 0x50 && span[2] == 0x4E && span[3] == 0x47;
            }
            catch
            {
                return false;
            }
        }
    }
}
