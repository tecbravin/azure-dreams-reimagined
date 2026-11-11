using System.IO;
using UnityEditor;
using UnityEngine;

namespace AzureDreamsReimagined.Editor
{
    public static class PsxEnvironmentPropBuilder
    {
        [MenuItem("Azure Dreams/Rendering/Build Environment Props")]
        public static void BuildEnvironmentProps()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;

            // Wall segments (3 sizes)
            CreateProp(projectRoot, "Wall_Short", PrimitiveType.Cube, new Vector3(4, 3, 0.5f), "Wall_StoneDungeon");
            CreateProp(projectRoot, "Wall_Long", PrimitiveType.Cube, new Vector3(8, 3, 0.5f), "Wall_StoneDungeon");
            CreateProp(projectRoot, "Wall_Tall", PrimitiveType.Cube, new Vector3(4, 5, 0.5f), "Wall_StoneDungeon");

            // Pillars
            CreateProp(projectRoot, "Pillar_Small", PrimitiveType.Cylinder, new Vector3(0.5f, 2, 0.5f), "Wall_StoneDungeon");
            CreateProp(projectRoot, "Pillar_Large", PrimitiveType.Cylinder, new Vector3(0.8f, 4, 0.8f), "Wall_StoneDungeon");

            // Stairs
            CreateProp(projectRoot, "Stairs_1x1", PrimitiveType.Cube, new Vector3(1, 0.3f, 1), "Floor_StoneDungeon");
            CreateProp(projectRoot, "Stairs_2x2", PrimitiveType.Cube, new Vector3(2, 0.3f, 2), "Floor_StoneDungeon");
            CreateProp(projectRoot, "Stairs_3x3", PrimitiveType.Cube, new Vector3(3, 0.3f, 3), "Floor_StoneDungeon");

            // Door frames
            CreateProp(projectRoot, "DoorFrame", PrimitiveType.Cube, new Vector3(1.5f, 2.5f, 0.3f), "Wall_StoneDungeon");

            // Torch holders
            CreateProp(projectRoot, "Torch_Holder", PrimitiveType.Cylinder, new Vector3(0.2f, 0.5f, 0.2f), "Wall_StoneDungeon");

            // Chest
            CreateProp(projectRoot, "Chest", PrimitiveType.Cube, new Vector3(0.8f, 0.6f, 0.5f), "Floor_StoneDungeon");

            // Pot
            CreateProp(projectRoot, "Pot", PrimitiveType.Cylinder, new Vector3(0.4f, 0.5f, 0.4f), "Floor_StoneDungeon");

            Debug.Log("[PsxEnvironmentPropBuilder] Environment props built.");
        }

        private static void CreateProp(string projectRoot, string name, PrimitiveType type, Vector3 scale, string materialName)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.localScale = scale;

            var renderer = go.GetComponent<Renderer>();
            if (renderer)
            {
                string matPath = $"Assets/AzureDreams/Materials/{materialName}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat) renderer.sharedMaterial = mat;
            }

            string prefabPath = $"Assets/AzureDreams/Prefabs/Environment/{name}.prefab";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(projectRoot, prefabPath)));
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
