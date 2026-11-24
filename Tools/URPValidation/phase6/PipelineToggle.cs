// Temporary: Temp/toggle_rp flips between URP and Built-in while playing (for side-by-side checks).
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
static class PipelineToggle
{
    static RenderPipelineAsset s_urp;
    static PipelineToggle() { EditorApplication.update += Poll; }

    static void Poll()
    {
        if (File.Exists("Temp/dump_actor"))
        {
            File.Delete("Temp/dump_actor");
            var sb = new System.Text.StringBuilder();
            var cam = Camera.main;
            if (cam) sb.AppendLine($"camera pos={cam.transform.position} rot={cam.transform.eulerAngles} ortho={cam.orthographic} fov={cam.fieldOfView} near={cam.nearClipPlane} far={cam.farClipPlane}");
            var mpb = new MaterialPropertyBlock();
            foreach (var a in Object.FindObjectsByType<AzureDreamsReimagined.Rendering.ActorSpriteDepth>(FindObjectsInactive.Exclude))
            {
                sb.AppendLine($"{a.name} root={a.transform.position}");
                foreach (var sr in a.GetComponentsInChildren<SpriteRenderer>())
                {
                    sr.GetPropertyBlock(mpb);
                    sb.AppendLine($"   {sr.name} pos={sr.transform.position} rot={sr.transform.eulerAngles} scale={sr.transform.lossyScale} shader={(sr.sharedMaterial ? sr.sharedMaterial.shader.name : "-")} anchor={mpb.GetVector("_ActorDepthAnchor")} feet={mpb.GetVector("_ActorDepthFeet")} normal={mpb.GetVector("_ActorDepthNormal")} bounds={sr.bounds}");
                }
            }
            File.WriteAllText("Temp/dump_actor.txt", sb.ToString());
        }
        if (!File.Exists("Temp/toggle_rp")) return;
        File.Delete("Temp/toggle_rp");
        if (GraphicsSettings.defaultRenderPipeline) { s_urp = GraphicsSettings.defaultRenderPipeline; GraphicsSettings.defaultRenderPipeline = null; }
        else GraphicsSettings.defaultRenderPipeline = s_urp ? s_urp : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/AzureDreams/Settings/URP/AzureDreams_URP.asset");
        File.WriteAllText("Temp/toggle_rp.txt", GraphicsSettings.defaultRenderPipeline ? "URP" : "Built-in");
    }
}
