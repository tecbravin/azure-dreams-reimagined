// Temporary: validates the URP post-process feature and camera stacking with URP active for a moment.
// Triggered by Temp/run_urp_phase3; writes Temp/urp_phase3.txt and PNGs in Temp/.
using System.IO;
using System.Text;
using AzureDreamsReimagined.Rendering;
using AzureDreamsReimagined.URP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
static class URPPhase3Validation
{
    const string Trigger = "Temp/run_urp_phase3";
    const string Folder = "Assets/_URPValidation";
    const int W = 640, H = 360;

    static URPPhase3Validation()
    {
        if (File.Exists(Trigger)) EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        File.Delete(Trigger);
        var log = new StringBuilder();
        var oldDefault = GraphicsSettings.defaultRenderPipeline;
        int oldAA = QualitySettings.antiAliasing;
        bool oldColorTemp = GraphicsSettings.lightsUseColorTemperature;
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var cam = BuildScene(scene);
            var effects = cam.gameObject.AddComponent<PostProcessingController>();
            effects.ambientOcclusion = false; // the Built-in reference below has no depth texture
            effects.vignette = true; effects.colorGrading = true; effects.saturation = 1.4f; effects.contrast = 1.2f;

            // Built-in reference: the same blit the Built-in OnRenderImage does.
            var builtinMat = new Material(Shader.Find("Hidden/AzureDreams/BuiltinPostProcess"));
            var retroBuiltin = new Material(Shader.Find("AzureDreams/PSX Color Quantization"));
            Save("Temp/p3_builtin_effect.png", BuiltinReference(cam, effects, builtinMat, null));
            effects.colorQuantization = true; effects.lowResolution = true; effects.pixelHeight = 120;
            Save("Temp/p3_builtin_retro.png", BuiltinReference(cam, effects, builtinMat, retroBuiltin));
            effects.colorQuantization = false; effects.lowResolution = false;

            // URP with the feature.
            var data = ScriptableObject.CreateInstance<UniversalRendererData>();
            var feature = ScriptableObject.CreateInstance<AzurePostProcessFeature>();
            feature.name = "AzurePostProcess";
            AssetDatabase.CreateAsset(data, Folder + "/P3Renderer.asset");
            AssetDatabase.AddObjectToAsset(feature, data);
            data.rendererFeatures.Add(feature);
            data.SetDirty();
            var asset = UniversalRenderPipelineAsset.Create(data);
            asset.supportsHDR = true;
            AssetDatabase.CreateAsset(asset, Folder + "/P3URP.asset");
            AssetDatabase.SaveAssets();
            GraphicsSettings.defaultRenderPipeline = asset;
            RenderToTexture(cam); // warm up: creates the pipeline instance
            log.AppendLine("pipeline=" + Shader.globalRenderPipeline);

            Save("Temp/p3_urp_effect.png", RenderToTexture(cam));
            effects.colorQuantization = true; effects.lowResolution = true; effects.pixelHeight = 120;
            Save("Temp/p3_urp_retro.png", RenderToTexture(cam));
            effects.colorQuantization = false; effects.lowResolution = false;
            effects.ambientOcclusion = true; effects.depthOfField = true;
            Save("Temp/p3_urp_depth.png", RenderToTexture(cam));
            effects.ambientOcclusion = false; effects.depthOfField = false;
            effects.enabled = false;
            Save("Temp/p3_urp_plain.png", RenderToTexture(cam));
            var low = cam.gameObject.AddComponent<PsxLowResRender>();
            low.targetWidth = 160; low.targetHeight = 90;
            Save("Temp/p3_urp_lowres.png", RenderToTexture(cam));
            Object.DestroyImmediate(low);
            effects.enabled = true;

            // Camera stacking hook.
            var overlayGo = new GameObject("Overlay"); SceneManager.MoveGameObjectToScene(overlayGo, scene);
            var overlay = overlayGo.AddComponent<Camera>(); overlay.CopyFrom(cam); overlay.scene = scene;
            overlay.clearFlags = CameraClearFlags.Depth;
            typeof(AzurePostProcessFeature).Assembly.GetType("AzureDreamsReimagined.URP.UrpCameraStacking")
                .GetMethod("Install", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, null);
            CameraStacking.AddOverlay(cam, overlay);
            Save("Temp/p3_urp_stack.png", RenderToTexture(cam));
            var overlayData = overlay.GetComponent<UniversalAdditionalCameraData>();
            var baseData = cam.GetComponent<UniversalAdditionalCameraData>();
            log.AppendLine($"stack: overlayType={(overlayData ? overlayData.renderType.ToString() : "none")} baseStackCount={(baseData ? baseData.cameraStack.Count : -1)}");
            CameraStacking.RemoveOverlay(cam, overlay);
            log.AppendLine($"stack after remove: {(baseData ? baseData.cameraStack.Count : -1)}");
        }
        catch (System.Exception e) { log.AppendLine("EXCEPTION " + e); }
        finally
        {
            GraphicsSettings.defaultRenderPipeline = oldDefault;
            QualitySettings.antiAliasing = oldAA;
            GraphicsSettings.lightsUseColorTemperature = oldColorTemp;
            EditorSceneManager.ClosePreviewScene(scene);
            AssetDatabase.DeleteAsset(Folder + "/P3URP.asset");
            AssetDatabase.DeleteAsset(Folder + "/P3Renderer.asset");
            AssetDatabase.SaveAssets();
            log.AppendLine("restored=" + (GraphicsSettings.currentRenderPipeline == null ? "Built-in" : GraphicsSettings.currentRenderPipeline.name));
            File.WriteAllText("Temp/urp_phase3.txt", log.ToString());
        }
    }

    static Camera BuildScene(Scene scene)
    {
        string[] names = { "AzureDreams/TownCaptured", "AzureDreams/PSX Indexed CLUT", "AzureDreams/TownInteriorUnlit", "AzureDreams/PoolBasin", "AzureDreams/FusionGlow", "AzureDreams/ArcadeCreature" };
        var byShader = new System.Collections.Generic.Dictionary<string, Material>();
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/AzureDreams" }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m && m.shader && !byShader.ContainsKey(m.shader.name)) byShader[m.shader.name] = m;
        }
        var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
        floor.transform.SetPositionAndRotation(new Vector3(0, -1, 2), Quaternion.Euler(90, 0, 0)); floor.transform.localScale = new Vector3(12, 8, 1);
        var floorMat = new Material(Shader.Find("AzureDreams/TownInteriorUnlit")); floorMat.SetColor("_Color", new Color(.2f, .5f, .25f));
        floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat; SceneManager.MoveGameObjectToScene(floor, scene);
        for (int i = 0; i < names.Length; i++)
        {
            var go = GameObject.CreatePrimitive(i % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Quad);
            go.transform.position = new Vector3(-3.75f + i * 1.5f, 0, 1 + (i % 3));
            go.transform.rotation = Quaternion.Euler(0, 25, 0);
            go.GetComponent<MeshRenderer>().sharedMaterial = byShader.TryGetValue(names[i], out var m) ? m : new Material(Shader.Find(names[i]));
            SceneManager.MoveGameObjectToScene(go, scene);
        }
        var camGo = new GameObject("P3Camera"); SceneManager.MoveGameObjectToScene(camGo, scene);
        var cam = camGo.AddComponent<Camera>();
        cam.scene = scene; cam.cameraType = CameraType.Game;
        cam.transform.SetPositionAndRotation(new Vector3(0, 2, -5), Quaternion.Euler(20, 0, 0));
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.1f, .1f, .2f);
        cam.fieldOfView = 40; cam.nearClipPlane = .1f; cam.farClipPlane = 50; cam.allowHDR = true;
        return cam;
    }

    static Texture2D BuiltinReference(Camera cam, PostProcessingController effects, Material effect, Material retro)
    {
        var scene = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.DefaultHDR);
        var dest = RenderTexture.GetTemporary(W, H, 0, RenderTextureFormat.ARGB32);
        cam.targetTexture = scene; cam.Render(); cam.targetTexture = null;
        effects.ApplyEffectParameters(effect);
        if (retro && effects.RetroActive)
        {
            effects.RetroTargetSize(W, H, out int w, out int h);
            var pixels = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.DefaultHDR);
            pixels.filterMode = FilterMode.Point;
            Graphics.Blit(scene, pixels, effect, 0);
            effects.ApplyRetroParameters(retro, h);
            Graphics.Blit(pixels, dest, retro, 0);
            RenderTexture.ReleaseTemporary(pixels);
        }
        else Graphics.Blit(scene, dest, effect, 0);
        var tex = Read(dest);
        RenderTexture.ReleaseTemporary(scene); RenderTexture.ReleaseTemporary(dest);
        return tex;
    }

    static Texture2D RenderToTexture(Camera cam)
    {
        var rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
        var tex = Read(rt);
        RenderTexture.ReleaseTemporary(rt);
        return tex;
    }

    static Texture2D Read(RenderTexture rt)
    {
        var previous = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
        RenderTexture.active = previous;
        return tex;
    }

    static void Save(string path, Texture2D tex)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }
}
