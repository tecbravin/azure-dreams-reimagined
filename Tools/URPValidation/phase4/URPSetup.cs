// Temporary one-shot: phase 4 of the URP migration. Trigger file Temp/run_urp_setup; log Temp/urp_setup.txt.
using System.IO;
using System.Linq;
using System.Text;
using AzureDreamsReimagined.URP;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
static class URPSetup
{
    const string Folder = "Assets/AzureDreams/Settings/URP";

    static URPSetup()
    {
        if (File.Exists("Temp/run_urp_setup")) EditorApplication.delayCall += Run;
        if (File.Exists("Temp/run_urp_reimport")) EditorApplication.delayCall += ReimportModels;
    }

    static void Run()
    {
        File.Delete("Temp/run_urp_setup");
        var log = new StringBuilder();
        try
        {
            if (!AssetDatabase.IsValidFolder("Assets/AzureDreams/Settings")) AssetDatabase.CreateFolder("Assets/AzureDreams", "Settings");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/AzureDreams/Settings", "URP");

            // Renderer: Forward, with the post-process feature that replaces OnRenderImage.
            var data = ScriptableObject.CreateInstance<UniversalRendererData>();
            data.renderingMode = RenderingMode.Forward;
            AssetDatabase.CreateAsset(data, Folder + "/AzureDreams_Renderer.asset");
            var feature = ScriptableObject.CreateInstance<AzurePostProcessFeature>();
            feature.name = "AzureDreams Post Process";
            AssetDatabase.AddObjectToAsset(feature, data);
            var fso = new SerializedObject(feature);
            fso.FindProperty("_effectShader").objectReferenceValue = Shader.Find("Hidden/AzureDreams/URP/PostProcess");
            fso.FindProperty("_retroShader").objectReferenceValue = Shader.Find("Hidden/AzureDreams/URP/PSX Color Quantization");
            fso.ApplyModifiedPropertiesWithoutUndo();
            data.rendererFeatures.Add(feature);
            var dso = new SerializedObject(data);
            var map = dso.FindProperty("m_RendererFeatureMap");
            map.arraySize = 1;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
            map.GetArrayElementAtIndex(0).longValue = localId;
            dso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);

            // Lean asset for WebGL: no HDR, MSAA, shadows, extra lights, depth or opaque copies by default.
            var asset = UniversalRenderPipelineAsset.Create(data);
            AssetDatabase.CreateAsset(asset, Folder + "/AzureDreams_URP.asset");
            var aso = new SerializedObject(asset);
            void Set(string name, int value) { var p = aso.FindProperty(name); if (p == null) log.AppendLine("missing " + name); else if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value != 0; else p.intValue = value; }
            Set("m_SupportsHDR", 0);
            Set("m_MSAA", 1);
            Set("m_RequireDepthTexture", 0);
            Set("m_RequireOpaqueTexture", 0);
            Set("m_MainLightShadowsSupported", 0);
            Set("m_AdditionalLightsRenderingMode", 0);
            Set("m_UseSRPBatcher", 1);
            aso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            GraphicsSettings.defaultRenderPipeline = asset;
            log.AppendLine("assigned " + AssetDatabase.GetAssetPath(asset) + " features=" + data.rendererFeatures.Count);
            var names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
                log.AppendLine($"quality {i} {names[i]} = {(QualitySettings.GetRenderPipelineAssetAt(i) ? QualitySettings.GetRenderPipelineAssetAt(i).name : "inherits Graphics")}");

            ConvertMaterials(log);
            AssetDatabase.SaveAssets();
        }
        catch (System.Exception e) { log.AppendLine("EXCEPTION " + e); }
        File.WriteAllText("Temp/urp_setup.txt", log.ToString());
    }

    // Materials embedded in imported models cannot be edited; reimporting under URP regenerates
    // them with the pipeline's default shader (URP Lit), keeping the imported textures and colours.
    static void ReimportModels()
    {
        File.Delete("Temp/run_urp_reimport");
        var log = new StringBuilder();
        int count = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/AzureDreams" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                bool legacy = false;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is Material m && m.shader && (m.shader.name == "Standard" || m.shader.name.StartsWith("Legacy Shaders"))) { legacy = true; break; }
                if (!legacy) continue;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                count++;
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/AzureDreams" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Material m && m.shader && !m.shader.name.StartsWith("Universal Render Pipeline") && !m.shader.name.StartsWith("AzureDreams"))
                    log.AppendLine("still legacy: " + path + " " + m.name + " " + m.shader.name);
        }
        log.AppendLine("reimported " + count);
        File.WriteAllText("Temp/urp_reimport.txt", log.ToString());
    }

    static void ConvertMaterials(StringBuilder log)
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Assets/bkp")) continue;
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m || !m.shader) continue;
            string shader = m.shader.name;
            if (shader.StartsWith("AzureDreams/") || shader.StartsWith("Hidden/AzureDreams") || shader.StartsWith("TextMeshPro") || shader.StartsWith("Universal Render Pipeline")) continue;

            Texture tex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            Vector2 scale = m.HasProperty("_MainTex") ? m.GetTextureScale("_MainTex") : Vector2.one;
            Vector2 offset = m.HasProperty("_MainTex") ? m.GetTextureOffset("_MainTex") : Vector2.zero;
            Color color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            float mode = m.HasProperty("_Mode") ? m.GetFloat("_Mode") : -1;
            float cutoff = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f;
            string action = "kept (renders in URP as SRPDefaultUnlit)";

            if (shader == "Standard" && lit)
            {
                Texture normal = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
                Texture emission = m.HasProperty("_EmissionMap") ? m.GetTexture("_EmissionMap") : null;
                Color emissionColor = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
                float metallic = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0;
                float smooth = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.5f;
                bool emissive = m.IsKeywordEnabled("_EMISSION");
                m.shader = lit;
                m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", scale); m.SetTextureOffset("_BaseMap", offset);
                m.SetColor("_BaseColor", color);
                m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smooth);
                if (normal) m.SetTexture("_BumpMap", normal);
                if (emissive) { m.SetTexture("_EmissionMap", emission); m.SetColor("_EmissionColor", emissionColor); m.EnableKeyword("_EMISSION"); }
                // Standard _Mode: 0 opaque, 1 cutout, 2 fade, 3 transparent.
                m.SetFloat("_Surface", mode >= 2 ? 1 : 0);
                m.SetFloat("_AlphaClip", mode == 1 ? 1 : 0);
                m.SetFloat("_Cutoff", cutoff);
                m.SetFloat("_Blend", mode == 3 ? 1 : 0);
                BaseShaderGUI.SetMaterialKeywords(m);
                action = "-> URP Lit";
            }
            else if (shader == "Particles/Standard Unlit" && particles)
            {
                m.shader = particles;
                m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", scale); m.SetTextureOffset("_BaseMap", offset);
                m.SetColor("_BaseColor", color);
                // Particles _Mode: 0 opaque, 1 cutout, 2 fade, 3 transparent, 4 additive, 5 subtractive, 6 modulate.
                m.SetFloat("_Surface", mode >= 2 ? 1 : 0);
                m.SetFloat("_AlphaClip", mode == 1 ? 1 : 0);
                m.SetFloat("_Cutoff", cutoff);
                m.SetFloat("_Blend", mode == 4 ? 2 : mode == 6 ? 3 : mode == 3 ? 1 : 0);
                BaseShaderGUI.SetMaterialKeywords(m);
                action = "-> URP Particles/Unlit";
            }
            EditorUtility.SetDirty(m);
            log.AppendLine($"{path} | {shader} | mode={mode} tex={(tex ? tex.name : "-")} color={color} | {action}");
        }
    }
}
