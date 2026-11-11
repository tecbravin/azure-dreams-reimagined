using System.IO;
using UnityEditor;
using UnityEngine;

namespace AzureDreamsReimagined.Editor
{
    public static class PsxSpritersResourceImporter
    {
        [MenuItem("Azure Dreams/Rendering/Import Spriters Resource")]
        public static void ImportSpritersResource()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string srcDir = Path.Combine(Directory.GetParent(projectRoot).FullName, "extracted_assets", "spriters_resource");
            string destDir = "Assets/AzureDreams/SpritersResource";
            Directory.CreateDirectory(Path.Combine(projectRoot, destDir));

            // Copy all PNGs
            string[] files = Directory.GetFiles(srcDir, "*.png");
            int copied = 0;
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                string destPath = Path.Combine(projectRoot, destDir, fileName);
                File.Copy(file, destPath, true);
                copied++;
            }
            AssetDatabase.Refresh();
            Debug.Log($"[PsxSpritersResourceImporter] Copied {copied} files to {destDir}");

            // Create tower floor materials (one per floor pair)
            CreateTowerFloorMaterials(projectRoot, destDir);

            // Create character/monster sprite materials
            CreateSpriteMaterials(projectRoot, destDir);

            Debug.Log("[PsxSpritersResourceImporter] Import complete.");
        }

        private static void CreateTowerFloorMaterials(string projectRoot, string destDir)
        {
            string[] towerFiles = Directory.GetFiles(Path.Combine(projectRoot, destDir), "PlayStation - Azure Dreams - Tower - Monster Tower *.png");
            foreach (var file in towerFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                string matName = fileName.Replace("PlayStation - Azure Dreams - Tower - ", "").Replace(" ", "_");
                string matPath = $"Assets/AzureDreams/Materials/TowerFloor_{matName}.mat";

                if (File.Exists(Path.Combine(projectRoot, matPath))) continue;

                var texPath = destDir + "/" + Path.GetFileName(file);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (!tex) continue;

                var shader = Shader.Find("AzureDreams/PSX Vertex Lit");
                if (!shader) shader = Shader.Find("Standard");
                var mat = new Material(shader);
                mat.name = "TowerFloor_" + matName;
                mat.mainTexture = tex;
                mat.color = Color.white;
                AssetDatabase.CreateAsset(mat, matPath);
            }
            Debug.Log($"Created {towerFiles.Length} tower floor materials");
        }

        private static void CreateSpriteMaterials(string projectRoot, string destDir)
        {
            string[] spriteFiles = Directory.GetFiles(Path.Combine(projectRoot, destDir), "PlayStation - Azure Dreams - *.png");
            foreach (var file in spriteFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.Contains("Tower - Monster Tower")) continue; // Skip tower floors (handled above)

                string matName = fileName.Replace("PlayStation - Azure Dreams - ", "").Replace(" ", "_").Replace("-", "");
                string matPath = $"Assets/AzureDreams/Materials/Sprite_{matName}.mat";

                if (File.Exists(Path.Combine(projectRoot, matPath))) continue;

                var texPath = destDir + "/" + Path.GetFileName(file);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (!tex) continue;

                var shader = Shader.Find("Sprites/Default");
                var mat = new Material(shader);
                mat.name = "Sprite_" + matName;
                mat.mainTexture = tex;
                AssetDatabase.CreateAsset(mat, matPath);
            }
        }
    }
}
