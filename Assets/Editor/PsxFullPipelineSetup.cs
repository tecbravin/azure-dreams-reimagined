using System.IO;
using UnityEditor;
using UnityEngine;

namespace AzureDreamsReimagined.Editor
{
    public static class PsxFullPipelineSetup
    {
        [MenuItem("Azure Dreams/Rendering/Full Pipeline Setup")]
        public static void FullPipelineSetup()
        {
            // 1. GPU Instancing (already in optimizer, re-run)
            PsxRenderPipelineOptimizer.OptimizePipeline();

            // 2. Camera settings
            SetupCamera();

            // 3. Create LOD groups for floor tileset
            SetupLODGroups();

            // 4. Create occlusion areas
            SetupOcclusionCulling();

            // 5. Create lightmap settings asset
            SetupLightmapSettings();

            // 6. Create texture streaming config
            SetupTextureStreaming();

            // 7. Create WebGL player settings (documentation)
            CreateWebGLSettingsDoc();

            Debug.Log("[PsxFullPipelineSetup] Full pipeline setup complete.");
        }

        private static void SetupCamera()
        {
            // Find main camera or create one
            var cam = Camera.main;
            if (cam)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;
                cam.fieldOfView = 60f;
                cam.depthTextureMode = DepthTextureMode.Depth;
                EditorUtility.SetDirty(cam);
                Debug.Log("Camera configured: skybox, 0.1-100 clip, 60 FOV");
            }
        }

        private static void SetupLODGroups()
        {
            // Find floor tileset prefab and add LOD group
            string prefabPath = "Assets/AzureDreams/Prefabs/Floor_Tileset.prefab";
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (go)
            {
                var lodGroup = go.GetComponent<LODGroup>();
                if (!lodGroup) lodGroup = go.AddComponent<LODGroup>();

                var renderers = go.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    var lods = new LOD[3];
                    lods[0] = new LOD(0.6f, renderers);  // Full detail
                    lods[1] = new LOD(0.3f, renderers);  // 50% detail
                    lods[2] = new LOD(0.1f, renderers);  // 25% detail
                    lodGroup.SetLODs(lods);
                    lodGroup.RecalculateBounds();
                    EditorUtility.SetDirty(go);
                    Debug.Log("LOD groups added to Floor_Tileset");
                }
            }
        }

        private static void SetupOcclusionCulling()
        {
            // Create occlusion area for tower floors
            var go = new GameObject("OcclusionArea_Tower");
            var area = go.AddComponent<OcclusionArea>();
            area.size = new Vector3(20, 10, 20);
            area.center = Vector3.zero;
            string prefabPath = "Assets/AzureDreams/Prefabs/OcclusionArea_Tower.prefab";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(Directory.GetParent(Application.dataPath).FullName, prefabPath)));
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log("Occlusion area created: " + prefabPath);
        }

        private static void SetupLightmapSettings()
        {
            // Configure lightmap settings via LightingSettings
            var settings = new LightingSettings();
            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.mixedBakeMode = MixedLightingMode.Subtractive;
            settings.lightmapMaxSize = 512; // WebGL friendly
            settings.lightmapCompression = LightmapCompression.LowQuality;
            settings.filteringMode = LightingSettings.FilterMode.Auto;
            settings.indirectResolution = 0.5f;
            settings.lightmapPadding = 2;
            string path = "Assets/AzureDreams/Lighting/TowerLightingSettings.lighting";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path)));
            AssetDatabase.CreateAsset(settings, path);
            Debug.Log("Lighting settings created: " + path);
        }

        private static void SetupTextureStreaming()
        {
            // Enable texture streaming on all textures
            string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/AzureDreams" });
            int streamed = 0;
            foreach (var guid in texGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer)
                {
                    importer.streamingMipmaps = true;
                    importer.SaveAndReimport();
                    streamed++;
                }
            }
            Debug.Log($"Texture streaming enabled on {streamed} textures");
        }

        private static void CreateWebGLSettingsDoc()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string docPath = Path.Combine(projectRoot, "Documentation", "WebGLSettings.md");
            File.WriteAllText(docPath, @"# WebGL Player Settings

## Required Settings (ProjectSettings/ProjectSettings.asset)
- m_ColorSpace: 0 (Gamma, for WebGL compatibility)
- m_GraphicsJobs: 0 (disabled for WebGL)
- m_MTRendering: 1 (enabled)

## WebGL Template
- Use Default template (not Minimal)
- Enable Data Caching
- Enable Threads (Unity 6)
- Memory Size: 512MB

## Build Settings
- Compression: Gzip (not Brotli for compatibility)
- Name Files As Hashes: Enabled
- Development Build: Disabled (for release)

## Quality Settings
- Default: Medium
- Very Low: for low-end devices
- High: for desktop browsers

## How to Apply
1. Edit > Project Settings > Player > WebGL
2. Set Memory Size to 512
3. Enable Data Caching
4. Enable Threads
5. Set Compression to Gzip
");
            Debug.Log("WebGL settings doc created: " + docPath);
        }
    }
}
