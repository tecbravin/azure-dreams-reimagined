using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AzureDreamsReimagined.Editor
{
    /// <summary>
    /// The project's shaders carry a URP SubShader above the Built-in one. Unity picks between them
    /// with Shader.globalRenderPipeline. If a URP pipeline was active earlier in this Editor session
    /// (for example a temporary URP validation), that tag can stay "UniversalPipeline" after going
    /// back to Built-in, and the Built-in camera then runs the URP SubShaders: full-screen effects
    /// read _BlitTexture instead of _MainTex and the Game view turns black. This clears the stale tag
    /// whenever no render pipeline asset is active.
    /// </summary>
    [InitializeOnLoad]
    static class BuiltinPipelineTagGuard
    {
        static BuiltinPipelineTagGuard()
        {
            Check();
            EditorApplication.playModeStateChanged += _ => Check();
            EditorApplication.update += DumpProbe; // TEMP diagnostic

        }

        static void DumpProbe()
        {
            if (!System.IO.File.Exists("Temp/dump_rp")) return;
            System.IO.File.Delete("Temp/dump_rp");
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("global='" + Shader.globalRenderPipeline + "' current=" + GraphicsSettings.currentRenderPipeline + " playing=" + Application.isPlaying);
            var m = typeof(ShaderUtil).GetMethod("GetShaderActiveSubshaderIndex", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            foreach (var n in new[] { "Hidden/AzureDreams/BuiltinPostProcess", "AzureDreams/PSX Color Quantization", "AzureDreams/TownCaptured" })
            {
                var sh = Shader.Find(n);
                sb.AppendLine(n + " active=" + (sh && m != null ? m.Invoke(null, new object[] { sh }) : "?"));
            }
            var ppType = System.Type.GetType("PostProcessingController, AzureDreams.Runtime") ?? System.Type.GetType("AzureDreamsReimagined.PostProcessingController, AzureDreams.Runtime");
            if (ppType != null)
                foreach (var o in Object.FindObjectsByType(ppType, FindObjectsInactive.Include))
                    foreach (var f in new[] { "_material", "_retroMaterial" })
                    {
                        var mat = ppType.GetField(f, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(o) as Material;
                        sb.AppendLine(o.name + "." + f + " = " + (mat ? mat.shader.name + " passes=" + mat.passCount + " pass0='" + mat.GetPassName(0) + "'" : "null"));
                    }
            else sb.AppendLine("PostProcessingController type not found");
            System.IO.File.WriteAllText("Temp/rp_dump.txt", sb.ToString());
        }

        static void Check()
        {
            if (GraphicsSettings.currentRenderPipeline != null) return;
            if (string.IsNullOrEmpty(Shader.globalRenderPipeline)) return;
            Debug.LogWarning($"[BuiltinPipelineTagGuard] Built-in is active but Shader.globalRenderPipeline was '{Shader.globalRenderPipeline}'. Cleared so the Built-in SubShaders are used.");
            Shader.globalRenderPipeline = string.Empty;
        }
    }
}
