using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using AzureDreamsReimagined.Rendering;

namespace AzureDreamsReimagined.Editor
{
    /// <summary>One-shot, explicit import of the proven extraction. Never touches existing actor materials.</summary>
    [InitializeOnLoad]
    public static class PsxShaderIntegration
    {
        private const string Output = "Assets/AzureDreams/Sprites/Indexed";
        private const string Request = "Library/PsxShaderImport.request";
        private static double _nextCheck;
        [Serializable] private sealed class Manifest { public Resource[] resources; }
        [Serializable] private sealed class Resource { public int resource_id; public string directory; }
        [Serializable] private sealed class Report
        {
            public string status, unityVersion, graphicsDevice, colorSpace, sourceManifest, error;
            public int resourcesImported, materialsCreated, shaderErrors;
            public string[] shaderMessages;
            public string preview;
        }

        static PsxShaderIntegration() { EditorApplication.update += CheckRequest; }
        private static void CheckRequest()
        {
            if (EditorApplication.timeSinceStartup < _nextCheck) return;
            _nextCheck = EditorApplication.timeSinceStartup + 1;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (!File.Exists(Request)) return;
            File.Delete(Request);
            EditorApplication.update -= CheckRequest;
            ImportVerifiedSprites();
        }

        [MenuItem("Azure Dreams/Rendering/Import Verified Indexed Sprites")]
        public static void ImportVerifiedSprites()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string source = Path.Combine(Directory.GetParent(project).FullName, "extracted_assets/verified_decode/sprites");
            string reportDir = Path.Combine(project, "Logs/PsxShaders");
            Directory.CreateDirectory(reportDir);
            var report = new Report { status = "started", unityVersion = Application.unityVersion,
                graphicsDevice = SystemInfo.graphicsDeviceType.ToString(), colorSpace = QualitySettings.activeColorSpace.ToString(),
                sourceManifest = Path.Combine(source, "manifest.json") };
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/AzureDreams/Shaders/PsxIndexed.shader");
                var post = AssetDatabase.LoadAssetAtPath<Shader>("Assets/AzureDreams/Shaders/BuiltinPostProcess.shader");
                if (!shader || !post) throw new InvalidOperationException("Shader assets are not imported yet.");
                var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(report.sourceManifest));
                if (manifest.resources == null || manifest.resources.Length == 0)
                    throw new InvalidDataException("No verified resources in manifest.");
                Material firstMaterial = null;
                foreach (var entry in manifest.resources)
                {
                    string from = Path.Combine(source, entry.directory);
                    string folder = Output + "/resource_" + entry.resource_id.ToString("D3");
                    Directory.CreateDirectory(folder);
                    string indexPath = folder + "/atlas_indices.png";
                    File.Copy(Path.Combine(from, "atlas_indices.png"), indexPath, true);
                    AssetDatabase.ImportAsset(indexPath, ImportAssetOptions.ForceSynchronousImport);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(indexPath);
                    PsxDataTextureImporter.Configure(importer, true);
                    importer.SaveAndReimport();
                    var indices = AssetDatabase.LoadAssetAtPath<Texture2D>(indexPath);
                    var clut = PsxIndexedMaterial.CreateClutTexture(File.ReadAllBytes(Path.Combine(from, "clut_rgb555_le.bin")),
                        "clut_words");
                    string clutPath = folder + "/clut_words.asset";
                    var previous = AssetDatabase.LoadAssetAtPath<Texture2D>(clutPath);
                    if (previous)
                    {
                        EditorUtility.CopySerialized(clut, previous);
                        UnityEngine.Object.DestroyImmediate(clut);
                        clut = previous;
                        EditorUtility.SetDirty(previous);
                    }
                    else AssetDatabase.CreateAsset(clut, clutPath);
                    for (int state = -1; state < 4; state++)
                    {
                        string name = state < 0 ? "opaque" : "abr_" + state;
                        string matPath = folder + "/" + name + ".mat";
                        var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                        if (!material)
                        {
                            material = new Material(shader) { name = "PSX " + entry.resource_id + " " + name };
                            AssetDatabase.CreateAsset(material, matPath);
                        }
                        PsxIndexedMaterial.Configure(material, indices, clut, 0, state >= 0,
                            state < 0 ? PsxAbrMode.Half : (PsxAbrMode)state);
                        EditorUtility.SetDirty(material);
                        report.materialsCreated++;
                        if (!firstMaterial) firstMaterial = material;
                    }
                    report.resourcesImported++;
                }
                IncludeShader(shader);
                IncludeShader(post);
                CreatePostProcessingAssets(post, reportDir);
                AssetDatabase.SaveAssets();
                var messages = new List<string>();
                foreach (var s in new[] { shader, post })
                {
                    foreach (var message in ShaderUtil.GetShaderMessages(s))
                    {
                        messages.Add(s.name + ": " + message.severity + " " + message.message);
                        if (message.severity == ShaderCompilerMessageSeverity.Error) report.shaderErrors++;
                    }
                    if (!s.isSupported) throw new InvalidOperationException("Unsupported shader: " + s.name);
                }
                report.shaderMessages = messages.ToArray();
                report.preview = Path.Combine(reportDir, "resource_001_gpu.png");
                RenderAtlasPreview(firstMaterial, report.preview);
                report.status = report.shaderErrors == 0 ? "completed" : "shader_errors";
                Debug.Log("[PSX shaders] Imported " + report.resourcesImported + " banks; " +
                    report.materialsCreated + " materials. GPU preview: " + report.preview);
            }
            catch (Exception e)
            {
                report.status = "failed";
                report.error = e.ToString();
                Debug.LogException(e);
            }
            File.WriteAllText(Path.Combine(reportDir, "import-report.json"), JsonUtility.ToJson(report, true));
        }

        private static void CreatePostProcessingAssets(Shader shader, string backupDirectory)
        {
            const string profilePath = "Assets/AzureDreams/PostProcessing/AzureDreamsPostProcessProfile.asset";
            const string prefabPath = "Assets/AzureDreams/Prefabs/PostProcessingVolume.prefab";
            Directory.CreateDirectory(Path.GetDirectoryName(profilePath));
            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
            var profile = AssetDatabase.LoadAssetAtPath<BuiltinPostProcessProfile>(profilePath);
            if (!profile)
            {
                // Preserve the former hand-written URP stub, then create a real typed asset.
                if (File.Exists(profilePath))
                {
                    File.Copy(profilePath, Path.Combine(backupDirectory, "legacy-urp-profile.asset.txt"), true);
                    AssetDatabase.DeleteAsset(profilePath);
                }
                profile = ScriptableObject.CreateInstance<BuiltinPostProcessProfile>();
                profile.name = "AzureDreams Built-in Post Process Profile";
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            if (File.Exists(prefabPath))
                File.Copy(prefabPath, Path.Combine(backupDirectory, "previous-postprocessing-prefab.txt"), true);
            var root = new GameObject("PostProcessingCamera", typeof(Camera), typeof(PostProcessingController));
            try
            {
                var controller = root.GetComponent<PostProcessingController>();
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("_profile").objectReferenceValue = profile;
                serialized.FindProperty("_effectShader").objectReferenceValue = shader;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                // This is a camera prefab, not a URP global Volume. Never instantiate it into the user's scene.
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void IncludeShader(Shader shader)
        {
            var graphics = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (graphics.Length == 0) throw new InvalidOperationException("GraphicsSettings unavailable.");
            var serialized = new SerializedObject(graphics[0]);
            var included = serialized.FindProperty("m_AlwaysIncludedShaders");
            for (int i = 0; i < included.arraySize; i++)
                if (included.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;
            int at = included.arraySize;
            included.InsertArrayElementAtIndex(at);
            included.GetArrayElementAtIndex(at).objectReferenceValue = shader;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RenderAtlasPreview(Material material, string path)
        {
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(256, 256, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                GL.Clear(false, true, Color.clear);
                Graphics.Blit(Texture2D.whiteTexture, target, material, 0);
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}
