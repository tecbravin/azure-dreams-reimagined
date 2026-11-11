using AzureDreamsReimagined.Rendering;
using NUnit.Framework;
using UnityEngine;

public class PsxLowResLifecycleTests
{
    [Test]
    public void Rendering_PresentsIntoOriginalTarget()
    {
        var go = new GameObject("LowResPresentation");
        var camera = go.AddComponent<Camera>();
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.red;
        camera.cullingMask = 0;
        var output = new RenderTexture(64, 64, 16, RenderTextureFormat.ARGB32);
        output.Create();
        camera.targetTexture = output;
        var old = RenderTexture.active;
        var read = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = output; GL.Clear(true, true, Color.blue);
            var effect = go.AddComponent<PsxLowResRender>();
            typeof(PsxLowResRender).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).Invoke(effect, null);
            camera.Render();
            RenderTexture.active = output;
            read.ReadPixels(new Rect(32, 32, 1, 1), 0, 0); read.Apply();
            Assert.That(read.GetPixel(0, 0).r, Is.GreaterThan(0.9f), "Low-res scene must reach the real destination, not self-blit into its own RT");
        }
        finally
        {
            RenderTexture.active = old;
            Object.DestroyImmediate(go); Object.DestroyImmediate(read); Object.DestroyImmediate(output);
        }
    }

    [Test]
    public void Disabling_RestoresExistingCameraTarget()
    {
        var go = new GameObject("LowResLifecycle");
        var camera = go.AddComponent<Camera>();
        camera.enabled = false;
        var original = new RenderTexture(64, 64, 16);
        camera.targetTexture = original;
        try
        {
            var effect = go.AddComponent<PsxLowResRender>();
            // Runtime-only component: EditMode does not automatically call OnEnable.
            typeof(PsxLowResRender).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).Invoke(effect, null);
            Assert.That(camera.targetTexture, Is.SameAs(original), "The effect must not take ownership of the camera destination");
            effect.enabled = false;
            typeof(PsxLowResRender).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).Invoke(effect, null);
            Assert.That(camera.targetTexture, Is.SameAs(original));
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(original);
        }
    }
}
