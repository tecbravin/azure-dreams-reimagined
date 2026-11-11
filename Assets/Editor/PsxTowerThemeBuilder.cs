using System.IO;
using UnityEditor;
using UnityEngine;

namespace AzureDreamsReimagined.Editor
{
    public static class PsxTowerThemeBuilder
    {
        [MenuItem("Azure Dreams/Rendering/Build Tower Themes")]
        public static void BuildTowerThemes()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string matDir = "Assets/AzureDreams/Materials";

            // Floor materials per theme
            CreateThemeMaterial(projectRoot, matDir, "Floor_StoneDungeon", new Color(0.6f, 0.55f, 0.5f));
            CreateThemeMaterial(projectRoot, matDir, "Floor_CrystalCave", new Color(0.4f, 0.5f, 0.7f));
            CreateThemeMaterial(projectRoot, matDir, "Floor_LavaChamber", new Color(0.7f, 0.3f, 0.15f));
            CreateThemeMaterial(projectRoot, matDir, "Floor_IcePalace", new Color(0.8f, 0.85f, 0.9f));
            CreateThemeMaterial(projectRoot, matDir, "Floor_DarkTower", new Color(0.2f, 0.18f, 0.22f));

            // Wall materials per theme
            CreateThemeMaterial(projectRoot, matDir, "Wall_StoneDungeon", new Color(0.5f, 0.45f, 0.4f));
            CreateThemeMaterial(projectRoot, matDir, "Wall_CrystalCave", new Color(0.3f, 0.4f, 0.6f));
            CreateThemeMaterial(projectRoot, matDir, "Wall_LavaChamber", new Color(0.6f, 0.25f, 0.1f));
            CreateThemeMaterial(projectRoot, matDir, "Wall_IcePalace", new Color(0.7f, 0.75f, 0.85f));
            CreateThemeMaterial(projectRoot, matDir, "Wall_DarkTower", new Color(0.15f, 0.13f, 0.18f));

            // Skybox materials per theme
            CreateSkyboxMaterial(projectRoot, matDir, "Sky_StoneDungeon", new Color(0.1f, 0.1f, 0.15f));
            CreateSkyboxMaterial(projectRoot, matDir, "Sky_CrystalCave", new Color(0.05f, 0.1f, 0.25f));
            CreateSkyboxMaterial(projectRoot, matDir, "Sky_LavaChamber", new Color(0.2f, 0.05f, 0.02f));
            CreateSkyboxMaterial(projectRoot, matDir, "Sky_IcePalace", new Color(0.3f, 0.4f, 0.5f));
            CreateSkyboxMaterial(projectRoot, matDir, "Sky_DarkTower", new Color(0.02f, 0.02f, 0.05f));

            Debug.Log("[PsxTowerThemeBuilder] Tower theme materials created.");
        }

        private static void CreateThemeMaterial(string projectRoot, string matDir, string name, Color color)
        {
            string matPath = matDir + "/" + name + ".mat";
            if (File.Exists(Path.Combine(projectRoot, matPath))) return;

            var shader = Shader.Find("AzureDreams/PSX Vertex Lit");
            if (!shader) shader = Shader.Find("Standard");
            var mat = new Material(shader);
            mat.name = name;
            mat.color = color;
            Directory.CreateDirectory(Path.Combine(projectRoot, matDir));
            AssetDatabase.CreateAsset(mat, matPath);
        }

        private static void CreateSkyboxMaterial(string projectRoot, string matDir, string name, Color color)
        {
            string matPath = matDir + "/" + name + ".mat";
            if (File.Exists(Path.Combine(projectRoot, matPath))) return;

            var shader = Shader.Find("Skybox/6 Sided");
            if (!shader) shader = Shader.Find("Skybox/Procedural");
            var mat = new Material(shader);
            mat.name = name;
            mat.SetColor("_Tint", color);
            Directory.CreateDirectory(Path.Combine(projectRoot, matDir));
            AssetDatabase.CreateAsset(mat, matPath);
        }
    }
}
