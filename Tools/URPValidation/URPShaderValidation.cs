// Temporary: validates the URP SubShaders by activating URP for a moment, then restores Built-in.
// Triggered by the file Temp/run_urp_validation; results go to Temp/urp_validation.txt.
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
static class URPShaderValidation
{
    const string Trigger = "Temp/run_urp_validation";
    const string Output = "Temp/urp_validation.txt";
    const string Folder = "Assets/_URPValidation";

    static readonly string[] Shaders =
    {
        "AzureDreams/KohColorKey", "AzureDreams/PSX Color Quantization", "Hidden/AzureDreams/BuiltinPostProcess",
        "AzureDreams/PSX Indexed CLUT", "AzureDreams/PSX Vertex Lit", "AzureDreams/PSX Vertex Snap Affine",
        "AzureDreams/TownWater", "AzureDreams/ActorSpriteDepth", "AzureDreams/AuraBikeDust", "AzureDreams/FusionGlow",
        "AzureDreams/KohForegroundWall", "AzureDreams/PoolBasin", "AzureDreams/PoolResident",
        "AzureDreams/RetailTowerBackdrop", "AzureDreams/RetailTowerTile", "AzureDreams/RetailTowerTranslucent",
        "AzureDreams/TitleLens", "AzureDreams/ArcadeCreature",
    };
    static readonly string[] ShaderPaths =
    {
        "Assets/AzureDreams/Shaders/TownCaptured.shader", "Assets/AzureDreams/Shaders/TownInteriorUnlit.shader",
    };

    static URPShaderValidation()
    {
        if (File.Exists(Trigger)) EditorApplication.delayCall += Run;
        if (File.Exists("Temp/run_urp_restore")) EditorApplication.delayCall += () =>
        {
            File.Delete("Temp/run_urp_restore");
            // Activating URP synced these project settings; put the Built-in values back.
            QualitySettings.antiAliasing = 2;
            GraphicsSettings.lightsUseColorTemperature = false;
            AssetDatabase.SaveAssets();
            File.WriteAllText("Temp/urp_restore.txt", $"quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} aa={QualitySettings.antiAliasing} colorTemp={GraphicsSettings.lightsUseColorTemperature}");
        };
    }

    static void Run()
    {
        File.Delete(Trigger);
        var log = new StringBuilder();
        var oldDefault = GraphicsSettings.defaultRenderPipeline;
        var oldQuality = new RenderPipelineAsset[QualitySettings.names.Length];
        for (int i = 0; i < oldQuality.Length; i++) oldQuality[i] = QualitySettings.GetRenderPipelineAssetAt(i);
        bool oldAsync = ShaderUtil.allowAsyncCompilation;
        int oldAA = QualitySettings.antiAliasing;
        bool oldColorTemp = GraphicsSettings.lightsUseColorTemperature;
        for (int i = 0; i < oldQuality.Length; i++) if (oldQuality[i] != null) { File.WriteAllText(Output, "quality level " + i + " has its own pipeline; aborted"); return; }
        try
        {
            log.AppendLine("== Built-in (before) globalRenderPipeline='" + Shader.globalRenderPipeline + "'");
            Check(log);
            GridCapture("Temp/urp_grid_builtin.png", log);

            var data = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(data, Folder + "/ValidationRenderer.asset");
            var asset = UniversalRenderPipelineAsset.Create(data);
            AssetDatabase.CreateAsset(asset, Folder + "/ValidationURP.asset");
            GraphicsSettings.defaultRenderPipeline = asset;
            GridCapture("Temp/urp_grid_warmup.png", null);
            GridCapture("Temp/urp_grid_urp.png", log);
            log.AppendLine("== URP active: " + (GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "null") + " globalRenderPipeline='" + Shader.globalRenderPipeline + "'");
            Check(log);
        }
        catch (System.Exception e) { log.AppendLine("EXCEPTION " + e); }
        finally
        {
            GraphicsSettings.defaultRenderPipeline = oldDefault;
            ShaderUtil.allowAsyncCompilation = oldAsync;
            QualitySettings.antiAliasing = oldAA;
            GraphicsSettings.lightsUseColorTemperature = oldColorTemp;
            AssetDatabase.DeleteAsset(Folder + "/ValidationURP.asset");
            AssetDatabase.DeleteAsset(Folder + "/ValidationRenderer.asset");
            AssetDatabase.SaveAssets();
            log.AppendLine("== restored: " + (GraphicsSettings.currentRenderPipeline == null ? "Built-in" : GraphicsSettings.currentRenderPipeline.name));
            File.WriteAllText(Output, log.ToString());
        }
    }

    static void Check(StringBuilder log)
    {
        ShaderUtil.allowAsyncCompilation = false;
        foreach (var name in Shaders) CheckShader(log, Shader.Find(name), name);
        foreach (var path in ShaderPaths) CheckShader(log, AssetDatabase.LoadAssetAtPath<Shader>(path), path);
    }

    static void CheckShader(StringBuilder log, Shader shader, string label)
    {
        if (shader == null) { log.AppendLine("MISSING " + label); return; }
        var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        var passes = new StringBuilder();
        string[][] keywordSets = { new string[0], new[] { "PIXELSNAP_ON" }, new[] { "ETC1_EXTERNAL_ALPHA" } };
        foreach (var keywords in keywordSets)
        {
            material.shaderKeywords = keywords;
            for (int p = 0; p < material.passCount; p++)
            {
                ShaderUtil.CompilePass(material, p, true);
                if (keywords.Length == 0)
                    passes.Append(material.GetPassName(p)).Append('[').Append(material.GetTag("LightMode", false, "-")).Append("] ");
            }
        }
        int subshader = -1;
        var method = typeof(ShaderUtil).GetMethod("GetShaderActiveSubshaderIndex",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (method != null) subshader = (int)method.Invoke(null, new object[] { shader });
        var messages = ShaderUtil.GetShaderMessages(shader);
        log.AppendLine($"{(ShaderUtil.ShaderHasError(shader) ? "ERROR" : "ok   ")} {label} subshader={subshader}/{shader.subshaderCount} passes={material.passCount} {passes}");
        foreach (var m in messages)
            log.AppendLine($"    {m.severity} {m.platform} line {m.line}: {m.message} {m.messageDetails}".Replace("\n", " "));
        Object.DestroyImmediate(material);
    }

    static readonly string[] GridShaders =
    {
        "AzureDreams/KohColorKey", "AzureDreams/PSX Indexed CLUT", "AzureDreams/PSX Vertex Lit", "AzureDreams/PSX Vertex Snap Affine",
        "AzureDreams/TownWater", "AzureDreams/ActorSpriteDepth", "AzureDreams/AuraBikeDust", "AzureDreams/FusionGlow",
        "AzureDreams/KohForegroundWall", "AzureDreams/PoolBasin", "AzureDreams/PoolResident", "AzureDreams/RetailTowerBackdrop",
        "AzureDreams/RetailTowerTile", "AzureDreams/RetailTowerTranslucent", "AzureDreams/TitleLens", "AzureDreams/ArcadeCreature",
        "AzureDreams/TownCaptured", "AzureDreams/TownInteriorUnlit",
    };

    // One quad per shader, each with the first project material that uses it, rendered by an
    // orthographic camera in a preview scene. Tile order is GridShaders (6 columns x 3 rows).
    static void GridCapture(string path, StringBuilder log)
    {
        var byShader = new System.Collections.Generic.Dictionary<string, Material>();
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/AzureDreams" }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m != null && m.shader != null && !byShader.ContainsKey(m.shader.name)) byShader[m.shader.name] = m;
        }
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var objects = new System.Collections.Generic.List<GameObject>();
        try
        {
            var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            back.transform.position = new Vector3(2.5f, 1f, 1f); back.transform.localScale = new Vector3(7f, 4f, 1f);
            var backMat = new Material(Shader.Find("AzureDreams/TownInteriorUnlit")); backMat.SetColor("_Color", new Color(.25f, .25f, .3f, 1));
            back.GetComponent<MeshRenderer>().sharedMaterial = backMat;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(back, scene); objects.Add(back);
            for (int i = 0; i < GridShaders.Length; i++)
            {
                var name = GridShaders[i];
                Material material;
                if (!byShader.TryGetValue(name, out material)) { var sh = Shader.Find(name); if (sh == null) continue; material = new Material(sh); }
                if (log != null) log.AppendLine($"  tile {i} {name} <- {(AssetDatabase.GetAssetPath(material) is string ap && ap.Length > 0 ? ap : "new material")}");
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.transform.position = new Vector3(i % 6, 2 - i / 6, 0);
                quad.transform.localScale = Vector3.one * .9f;
                quad.GetComponent<MeshRenderer>().sharedMaterial = material;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(quad, scene); objects.Add(quad);
            }
            var camGo = new GameObject("ValidationCamera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camGo, scene); objects.Add(camGo);
            var camera = camGo.AddComponent<Camera>();
            camera.scene = scene; camera.orthographic = true; camera.orthographicSize = 1.6f;
            camera.transform.position = new Vector3(2.5f, 1f, -5f);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.magenta;
            camera.nearClipPlane = .1f; camera.farClipPlane = 20f; camera.cameraType = CameraType.Game;
            var rt = new RenderTexture(1200, 640, 24);
            camera.targetTexture = rt; camera.aspect = 1200f / 640f;
            camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex); camera.targetTexture = null; rt.Release();
        }
        finally
        {
            foreach (var o in objects) if (o != null) Object.DestroyImmediate(o);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static void Capture(string path)
    {
        var camera = Camera.main;
        if (camera == null) camera = Object.FindAnyObjectByType<Camera>();
        if (camera == null) return;
        var rt = new RenderTexture(960, 540, 24);
        var old = camera.targetTexture;
        camera.targetTexture = rt;
        camera.Render();
        camera.targetTexture = old;
        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        RenderTexture.active = previous;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
    }
}
