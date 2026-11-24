// Temporary: does an actor sprite standing in front of a wall get hidden by it? Built-in vs URP.
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
static class SpriteDepthCheck
{
    static SpriteDepthCheck()
    {
        if (File.Exists("Temp/run_sprite_depth")) EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        File.Delete("Temp/run_sprite_depth");
        var log = new StringBuilder();
        var urp = GraphicsSettings.defaultRenderPipeline;
        int oldAA = QualitySettings.antiAliasing;
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var camGo = new GameObject("Cam"); SceneManager.MoveGameObjectToScene(camGo, scene);
            var cam = camGo.AddComponent<Camera>(); cam.scene = scene; cam.cameraType = CameraType.Game;
            cam.fieldOfView = 40; cam.nearClipPlane = .1f; cam.farClipPlane = 100;
            cam.transform.SetPositionAndRotation(new Vector3(0, 7, -6), Quaternion.Euler(50, 0, 0));
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;

            Material Solid(Color c) { var m = new Material(Shader.Find("AzureDreams/TownInteriorUnlit")); m.SetColor("_Color", c); return m; }
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(floor, scene);
            floor.transform.position = new Vector3(0, -.5f, 0); floor.transform.localScale = new Vector3(10, 1, 10);
            floor.GetComponent<MeshRenderer>().sharedMaterial = Solid(Color.blue);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(wall, scene);
            wall.transform.position = new Vector3(0, .75f, 1.5f); wall.transform.localScale = new Vector3(4, 1.5f, 1); // front face at z=1
            wall.GetComponent<MeshRenderer>().sharedMaterial = Solid(Color.green);

            var tex = new Texture2D(8, 16); var px = new Color[128]; for (int i = 0; i < px.Length; i++) px[i] = Color.white; tex.SetPixels(px); tex.Apply();
            var spriteGo = new GameObject("Actor"); SceneManager.MoveGameObjectToScene(spriteGo, scene);
            spriteGo.transform.position = new Vector3(0, 0, .5f); // tile centre, half a tile in front of the wall
            spriteGo.transform.rotation = cam.transform.rotation; // camera-facing billboard
            var sr = spriteGo.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 8, 16), new Vector2(.5f, 0f), 8); // 1 x 2 units, pivot at the feet
            sr.sharedMaterial = new Material(Resources.Load<Shader>("Shaders/ActorSpriteDepth"));
            sr.color = Color.red;
            var mpb = new MaterialPropertyBlock();
            mpb.SetVector("_ActorDepthAnchor", new Vector4(0, 0, .5f, 0));
            mpb.SetVector("_ActorDepthNormal", Vector4.zero);
            mpb.SetVector("_ActorDepthFeet", new Vector4(0, 1, 0, 0));
            sr.SetPropertyBlock(mpb);

            foreach (var asset in new[] { urp, null })
            {
                GraphicsSettings.defaultRenderPipeline = asset;
                Render(cam); // warm-up after a pipeline switch
                var img = Render(cam);
                Vector3 head = cam.WorldToViewportPoint(spriteGo.transform.position + spriteGo.transform.up * 1.7f);
                Vector3 waist = cam.WorldToViewportPoint(spriteGo.transform.position + spriteGo.transform.up * 0.6f);
                Vector3 foot = cam.WorldToViewportPoint(spriteGo.transform.position + spriteGo.transform.up * 0.1f);
                string name = asset ? "URP" : "Built-in";
                log.AppendLine($"{name}: head={Px(img, head)} waist={Px(img, waist)} foot={Px(img, foot)} (red = sprite visible, green = wall)");
                File.WriteAllBytes($"Temp/depth_{name}.png", img.EncodeToPNG());
            }
        }
        catch (System.Exception e) { log.AppendLine("EXCEPTION " + e); }
        finally
        {
            GraphicsSettings.defaultRenderPipeline = urp;
            QualitySettings.antiAliasing = oldAA;
            EditorSceneManager.ClosePreviewScene(scene);
            File.WriteAllText("Temp/sprite_depth.txt", log.ToString());
        }
    }

    static string Px(Texture2D img, Vector3 vp) => img.GetPixel((int)(vp.x * img.width), (int)(vp.y * img.height)).ToString();

    static Texture2D Render(Camera cam)
    {
        var rt = RenderTexture.GetTemporary(400, 300, 24);
        cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var img = new Texture2D(400, 300, TextureFormat.RGB24, false); img.ReadPixels(new Rect(0, 0, 400, 300), 0, 0); img.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        return img;
    }
}
