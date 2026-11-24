// Temporary diagnostics: MaterialPropertyBlock on sprites under URP, and the fusion glow camera.
using System.IO;
using System.Reflection;
using System.Text;
using AzureDreamsReimagined;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
static class URPPhase5Check
{
    static URPPhase5Check()
    {
        if (File.Exists("Temp/run_urp_p5")) EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        File.Delete("Temp/run_urp_p5");
        var log = new StringBuilder();
        log.AppendLine("pipeline=" + (GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.name : "Built-in") + " srpBatcher=" + GraphicsSettings.useScriptableRenderPipelineBatching);
        var scene = EditorSceneManager.NewPreviewScene();
        bool batcher = GraphicsSettings.useScriptableRenderPipelineBatching;
        try
        {
            var camGo = new GameObject("Cam"); SceneManager.MoveGameObjectToScene(camGo, scene);
            var cam = camGo.AddComponent<Camera>(); cam.scene = scene; cam.cameraType = CameraType.Game;
            cam.orthographic = true; cam.orthographicSize = 1; cam.transform.position = new Vector3(0, 0, -5);
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.gray;

            var tex = new Texture2D(4, 4); var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = Color.white; tex.SetPixels(px); tex.Apply();
            var spriteGo = new GameObject("Sprite"); SceneManager.MoveGameObjectToScene(spriteGo, scene);
            spriteGo.transform.position = new Vector3(-1f, 0, 0); spriteGo.transform.localScale = Vector3.one * 4;
            var sr = spriteGo.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 8);
            sr.sharedMaterial = new Material(Resources.Load<Shader>("Shaders/ActorSpriteDepth"));
            var mpb = new MaterialPropertyBlock(); mpb.SetColor("_Color", Color.red); sr.SetPropertyBlock(mpb);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); SceneManager.MoveGameObjectToScene(quad, scene);
            quad.transform.position = new Vector3(1f, 0, 0); quad.transform.localScale = Vector3.one;
            var qr = quad.GetComponent<MeshRenderer>(); qr.sharedMaterial = new Material(Shader.Find("AzureDreams/TownInteriorUnlit"));
            var mpb2 = new MaterialPropertyBlock(); mpb2.SetColor("_Color", Color.green); qr.SetPropertyBlock(mpb2);

            foreach (bool on in new[] { true, false })
            {
                GraphicsSettings.useScriptableRenderPipelineBatching = on;
                var img = Render(cam);
                log.AppendLine($"batcher={on}: sprite(expect red)={img.GetPixel(img.width / 4, img.height / 2)} quad(expect green)={img.GetPixel(img.width * 3 / 4, img.height / 2)}");
                File.WriteAllBytes($"Temp/p5_mpb_{on}.png", img.EncodeToPNG());
            }
            GraphicsSettings.useScriptableRenderPipelineBatching = batcher;

            // Fusion glow camera path, as the game builds it.
            var urp = System.Type.GetType("AzureDreamsReimagined.URP.UrpCameraStacking, AzureDreams.URP");
            urp?.GetMethod("Install", BindingFlags.NonPublic | BindingFlags.Static)?.Invoke(null, null);
            var t = typeof(TowerGameController);
            var darkness = (GameObject)t.GetMethod("CreateFusionDarkness", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { cam });
            var glow = (Camera)t.GetMethod("CreateFusionGlowCamera", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { cam });
            log.AppendLine("darkness=" + (darkness ? darkness.name : "null") + " glow=" + (glow ? glow.name : "null"));
            if (glow)
            {
                var data = glow.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                log.AppendLine("glow type=" + (data ? data.renderType.ToString() : "none") + " scene=" + glow.gameObject.scene.name);
            }
            var effects = cam.gameObject.AddComponent<PostProcessingController>();
            effects.vignette = true; effects.vignetteIntensity = 1f;
            var img2 = Render(cam);
            log.AppendLine("with overlay + effect: corner(expect dark from vignette)=" + img2.GetPixel(2, 2) + " sprite=" + img2.GetPixel(img2.width / 4, img2.height / 2));
            File.WriteAllBytes("Temp/p5_stack.png", img2.EncodeToPNG());
        }
        catch (System.Exception e) { log.AppendLine("EXCEPTION " + (e.InnerException ?? e)); }
        finally
        {
            GraphicsSettings.useScriptableRenderPipelineBatching = batcher;
            EditorSceneManager.ClosePreviewScene(scene);
            File.WriteAllText("Temp/urp_p5.txt", log.ToString());
        }
    }

    static Texture2D Render(Camera cam)
    {
        var rt = RenderTexture.GetTemporary(256, 128, 24);
        cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var img = new Texture2D(256, 128, TextureFormat.RGB24, false); img.ReadPixels(new Rect(0, 0, 256, 128), 0, 0); img.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        return img;
    }
}
