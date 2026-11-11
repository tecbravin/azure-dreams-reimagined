using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using AzureDreamsReimagined.Rendering;

namespace AzureDreamsReimagined.Editor
{
    public static class PsxIndexedShowcaseBuilder
    {
        private const string ScenePath = "Assets/AzureDreams/Scenes/PsxIndexedShowcase.unity";
        private const string IndexedRoot = "Assets/AzureDreams/Sprites/Indexed";
        private const string ReportPath = "Logs/PsxShaders/import-report.json";
        private const string CapturePath = "Logs/PsxShaders/showcase.png";
        private const int StPalette = 1; // Palette 1 has 15 STP words (palette 0 has none)

        [Serializable]
        private sealed class ShowcaseStatus
        {
            public string status;
            public string detail;
        }

        [Serializable]
        private sealed class ImportReport
        {
            public string status;
            public int shaderErrors;
        }

        [MenuItem("Azure Dreams/Rendering/Build PsxIndexed Showcase (safe)")]
        public static void BuildShowcase()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string reportFile = Path.Combine(projectRoot, ReportPath);

            if (!File.Exists(reportFile))
            {
                Debug.LogError("[PsxIndexedShowcaseBuilder] BLOCKED: Missing " + reportFile);
                var status = new ShowcaseStatus { status = "blocked_no_import_report", detail = "ImportVerifiedSprites() has not run." };
                UpdateStatus(JsonUtility.ToJson(status, true));
                return;
            }

            var importReport = JsonUtility.FromJson<ImportReport>(File.ReadAllText(reportFile));
            bool completed = importReport != null && importReport.status == "completed";
            bool noErrors = importReport != null && importReport.shaderErrors == 0;
            if (!completed)
            {
                Debug.LogError("[PsxIndexedShowcaseBuilder] BLOCKED: import report status is not 'completed'.");
                var s1 = new ShowcaseStatus { status = "blocked_import_not_completed", detail = "Import report status is not 'completed'." };
                UpdateStatus(JsonUtility.ToJson(s1, true));
                return;
            }
            if (!noErrors)
            {
                Debug.LogError("[PsxIndexedShowcaseBuilder] BLOCKED: import report has shader errors.");
                var s2 = new ShowcaseStatus { status = "blocked_shader_errors", detail = "Import report has shader errors." };
                UpdateStatus(JsonUtility.ToJson(s2, true));
                return;
            }

            string resourceFolder = IndexedRoot + "/resource_001";
            string opaqueMatPath = resourceFolder + "/opaque.mat";
            string fullOpaquePath = Path.Combine(projectRoot, opaqueMatPath);
            if (!File.Exists(fullOpaquePath))
            {
                Debug.LogError("[PsxIndexedShowcaseBuilder] BLOCKED: opaque.mat not found.");
                var s3 = new ShowcaseStatus { status = "blocked_no_materials", detail = "opaque.mat not found at " + opaqueMatPath };
                UpdateStatus(JsonUtility.ToJson(s3, true));
                return;
            }

            for (int i = 0; i < 4; i++)
            {
                string abrPath = resourceFolder + "/abr_" + i + ".mat";
                if (!File.Exists(Path.Combine(projectRoot, abrPath)))
                {
                    Debug.LogError("[PsxIndexedShowcaseBuilder] BLOCKED: Missing " + abrPath);
                    var s4 = new ShowcaseStatus { status = "blocked_missing_materials", detail = "Missing " + abrPath };
                    UpdateStatus(JsonUtility.ToJson(s4, true));
                    return;
                }
            }

            var alreadyOpen = SceneManager.GetSceneByPath(ScenePath);
            if (alreadyOpen.IsValid() && alreadyOpen.isLoaded)
            {
                var blocked = new ShowcaseStatus { status = "blocked_showcase_already_open",
                    detail = "The output scene is open. Its contents and unsaved changes will not be overwritten." };
                UpdateStatus(JsonUtility.ToJson(blocked, true));
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            Scene showcaseScene = default;
            bool sceneCreated = false;
            try
            {
                showcaseScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                sceneCreated = true;
                EditorSceneManager.SetActiveScene(showcaseScene);
                ulong originalSceneMask = EditorSceneManager.GetSceneCullingMask(showcaseScene);
                ulong isolatedSceneMask = EditorSceneManager.CalculateAvailableSceneCullingMask();
                if (isolatedSceneMask == 0)
                    throw new InvalidOperationException("No isolated Scene culling bit is available.");
                EditorSceneManager.SetSceneCullingMask(showcaseScene, isolatedSceneMask);

                var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
                bg.name = "KnownBackground";
                string bgMatPath = "Assets/AzureDreams/Materials/ShowcaseBackground.mat";
                Material bgMat;
                if (File.Exists(Path.Combine(projectRoot, bgMatPath)))
                    bgMat = AssetDatabase.LoadAssetAtPath<Material>(bgMatPath);
                else
                {
                    bgMat = new Material(Shader.Find("Unlit/Color"));
                    bgMat.color = new Color(0.3f, 0.5f, 0.8f);
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(projectRoot, bgMatPath)));
                    AssetDatabase.CreateAsset(bgMat, bgMatPath);
                }
                bg.transform.position = new Vector3(0, 0, 2);
                bg.transform.localScale = new Vector3(10, 10, 1);
                bg.GetComponent<Renderer>().sharedMaterial = bgMat;

                // Calculate framing from bounds (quads span x[-3.25..4.6], y[-1.8..1.5] with labels)
                float minX = -3.25f, maxX = 4.6f, minY = -1.8f, maxY = 1.5f;
                float centerX = (minX + maxX) / 2f;
                float centerY = (minY + maxY) / 2f;
                float width = maxX - minX;
                float height = maxY - minY;
                float aspect = 1f; // 512x512 capture
                float orthoSize = Mathf.Max(width / (2f * aspect), height / 2f) * 1.15f;

                var cameraGo = new GameObject("ShowcaseCamera");
                var cam = cameraGo.AddComponent<Camera>();
                cam.enabled = false; // Manual capture only while the user's other Scenes are open.
                cam.overrideSceneCullingMask = isolatedSceneMask;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
                cam.orthographic = true;
                cam.orthographicSize = orthoSize;
                cam.transform.position = new Vector3(centerX, centerY, -5);
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;

                // Opaque atlas quad with STP palette copy (palette 1 has STP words)
                var opaqueMatBase = AssetDatabase.LoadAssetAtPath<Material>(opaqueMatPath);
                var indices = AssetDatabase.LoadAssetAtPath<Texture2D>(resourceFolder + "/atlas_indices.png");
                var clut = AssetDatabase.LoadAssetAtPath<Texture2D>(resourceFolder + "/clut_words.asset");
                string opaqueDemoPath = resourceFolder + "/opaque_stp_demo.mat";
                var opaqueMat = CreateDemoMaterial(opaqueMatBase, indices, clut, false,
                    PsxAbrMode.Half, opaqueDemoPath);

                var opaqueQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                opaqueQuad.name = "Resource001_Opaque";
                opaqueQuad.transform.position = new Vector3(-2.5f, 0, 0);
                opaqueQuad.transform.localScale = new Vector3(1.5f, 1.5f, 1);
                opaqueQuad.GetComponent<Renderer>().sharedMaterial = opaqueMat;

                // ABR quads (4 states) with STP palette copies
                for (int state = 0; state < 4; state++)
                {
                    string abrPath = resourceFolder + "/abr_" + state + ".mat";
                    var abrMatBase = AssetDatabase.LoadAssetAtPath<Material>(abrPath);
                    string abrDemoPath = resourceFolder + "/abr_" + state + "_stp_demo.mat";
                    var abrMat = CreateDemoMaterial(abrMatBase, indices, clut, true,
                        (PsxAbrMode)state, abrDemoPath);

                    var abrQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    abrQuad.name = "Resource001_ABR_" + state;
                    abrQuad.transform.position = new Vector3(0.5f + state * 1.2f, 0, 0);
                    abrQuad.transform.localScale = new Vector3(1f, 1f, 1);
                    abrQuad.GetComponent<Renderer>().sharedMaterial = abrMat;
                }

                CreateLabel("Opaque", new Vector3(-2.5f, -1.5f, 0));
                for (int i = 0; i < 4; i++)
                    CreateLabel("ABR " + i, new Vector3(0.5f + i * 1.2f, -1.5f, 0));

                CaptureGPU(cam, Path.Combine(projectRoot, CapturePath));

                // The temporary isolation mask must not break the saved, reusable scene.
                cam.overrideSceneCullingMask = 0;
                cam.enabled = true;
                EditorSceneManager.SetSceneCullingMask(showcaseScene, originalSceneMask);
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                if (!EditorSceneManager.SaveScene(showcaseScene, ScenePath))
                    throw new IOException("Failed to save showcase scene: " + ScenePath);

                var s5 = new ShowcaseStatus { status = "completed", detail = "Scene created and GPU captured with STP palette " + StPalette + "." };
                UpdateStatus(JsonUtility.ToJson(s5, true));
                Debug.Log("[PsxIndexedShowcaseBuilder] Scene: " + ScenePath + " | Capture: " + CapturePath);
            }
            catch (Exception e)
            {
                var s6 = new ShowcaseStatus { status = "failed", detail = e.ToString() };
                UpdateStatus(JsonUtility.ToJson(s6, true));
                Debug.LogException(e);
            }
            finally
            {
                if (sceneCreated && showcaseScene.IsValid() && showcaseScene != activeScene)
                {
                    EditorSceneManager.CloseScene(showcaseScene, true);
                }
                if (activeScene.IsValid())
                    EditorSceneManager.SetActiveScene(activeScene);
            }
        }

        private static Material CreateDemoMaterial(Material source, Texture indices, Texture clut,
            bool semiTransparency, PsxAbrMode abr, string path)
        {
            if (!source) throw new InvalidDataException("Imported base material is unavailable: " + path);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = !material;
            if (isNew) material = new Material(source);
            else material.CopyPropertiesFromMaterial(source);
            material.name = Path.GetFileNameWithoutExtension(path);
            PsxIndexedMaterial.Configure(material, indices, clut, StPalette, semiTransparency, abr);
            if (isNew) AssetDatabase.CreateAsset(material, path);
            else
            {
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
            }
            return material;
        }

        private static void CreateLabel(string text, Vector3 pos)
        {
            var go = new GameObject("Label_" + text);
            go.transform.position = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 24;
            tm.color = Color.white;
        }

        private static void CaptureGPU(Camera cam, string path)
        {
            var previous = RenderTexture.active;
            var previousTarget = cam.targetTexture;
            var target = RenderTexture.GetTemporary(512, 512, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D image = null;
            try
            {
                cam.targetTexture = target;
                cam.Render();
                RenderTexture.active = target;
                image = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                image.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, image.EncodeToPNG());
                Debug.Log("[PsxIndexedShowcaseBuilder] GPU capture saved: " + path);
            }
            finally
            {
                cam.targetTexture = previousTarget;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                if (image) UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static void UpdateStatus(string json)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string statusPath = Path.Combine(projectRoot, "Logs/PsxShaders/showcase-builder-status.json");
            Directory.CreateDirectory(Path.GetDirectoryName(statusPath));
            File.WriteAllText(statusPath, json);
        }
    }
}
