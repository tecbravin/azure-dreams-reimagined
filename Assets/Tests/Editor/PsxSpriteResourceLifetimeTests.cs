using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using AzureDreamsReimagined.Rendering;

public class PsxSpriteResourceLifetimeTests
{
    GameObject a, b;
    Texture2D atlas;
    TextAsset meta, palettes;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp] public void Setup()
    {
        atlas = new Texture2D(3, 1, TextureFormat.RGBA32, false, true);
        atlas.SetPixels32(new[] { new Color32(0,0,0,255), new Color32(1,0,0,255), new Color32(2,0,0,255) });
        atlas.Apply();
        meta = new TextAsset("{\"chains\":[{\"pointer\":1,\"parts\":[{\"width\":3,\"height\":1}]}]}");
        palettes = new TextAsset("[{\"index\":0,\"rgba_preview\":[[0,0,0,255],[255,0,0,255],[0,255,0,128]]}]");
    }
    PsxSpriteRenderer Create(ref GameObject go)
    {
        go = new GameObject("resource test");
        var r = go.AddComponent<PsxSpriteRenderer>();
        r.atlas = atlas; r.metadataJson = meta; r.palettesJson = palettes;
        Build(r); return r;
    }
    static void Build(PsxSpriteRenderer r)
    {
        var type = typeof(PsxSpriteRenderer);
        string key = (string)type.GetMethod("ComputeResourceKey", Private).Invoke(r, null);
        var it = (IEnumerator)type.GetMethod("BuildFramesCoroutine", Private).Invoke(r, new object[] { key });
        while (it.MoveNext()) { }
    }
    // These are runtime-only behaviours. EditMode does not dispatch their
    // lifecycle callbacks; invoke the same seam used by the manual bake above.
    static void Disable(GameObject go)
    {
        if (!go) return;
        typeof(PsxSpriteRenderer).GetMethod("OnDisable", Private)
            .Invoke(go.GetComponent<PsxSpriteRenderer>(), null);
    }
    [Test] public void NestedPreviewColors_IncludeIndexZero_AndPreserveAlpha()
    {
        Create(ref a);
        var sprite = a.GetComponent<SpriteRenderer>().sprite;
        Assert.That(sprite, Is.Not.Null);
        var rt = RenderTexture.GetTemporary(3, 1, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var old = RenderTexture.active;
        var read = new Texture2D(3, 1, TextureFormat.RGBA32, false, true);
        try
        {
            Graphics.Blit(sprite.texture, rt); RenderTexture.active = rt;
            read.ReadPixels(new Rect(0,0,3,1),0,0); read.Apply();
            var p = read.GetPixels32();
            Assert.That(p[0], Is.EqualTo(new Color32(0,0,0,255)));
            Assert.That(p[1].r, Is.GreaterThan(250)); Assert.That(p[1].g, Is.LessThan(3));
            Assert.That(p[2].g, Is.GreaterThan(250)); Assert.That((int)p[2].a, Is.InRange(126,130));
            Assert.That(sprite.texture.isReadable, Is.False);
        }
        finally { RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(read); }
    }
    [Test] public void SharedFrames_LastReleaseDestroys_AndRestartRebuilds()
    {
        var first = Create(ref a); Create(ref b);
        var sprite = a.GetComponent<SpriteRenderer>().sprite;
        Assert.That(b.GetComponent<SpriteRenderer>().sprite, Is.SameAs(sprite));
        a.SetActive(false);
        Disable(a);
        Assert.That(sprite != null, Is.True);
        Disable(b);
        Object.DestroyImmediate(b); b = null;
        Assert.That(sprite == null, Is.True, "Last lease must destroy generated sprites");
        a.SetActive(true); Build(first);
        Assert.That(a.GetComponent<SpriteRenderer>().sprite, Is.Not.Null);
        Assert.That(a.GetComponent<SpriteRenderer>().sprite, Is.Not.SameAs(sprite));
    }
    [Test] public void RawWords_ZeroTransparent_StpBlackVisible()
    {
        Object.DestroyImmediate(palettes);
        palettes = new TextAsset("[{\"index\":0,\"words_rgb555_stp\":[32768,31,0]}]");
        Create(ref a);
        Assert.That(a.GetComponent<SpriteRenderer>().sprite, Is.Not.Null);
        // Color assertions use the same GPU readback path as the preview test.
        var tex = a.GetComponent<SpriteRenderer>().sprite.texture;
        var rt = RenderTexture.GetTemporary(3,1,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        var old = RenderTexture.active; var read = new Texture2D(3,1,TextureFormat.RGBA32,false,true);
        try { Graphics.Blit(tex,rt); RenderTexture.active=rt; read.ReadPixels(new Rect(0,0,3,1),0,0); read.Apply();
            var p=read.GetPixels32(); Assert.That(p[0].a,Is.EqualTo(255)); Assert.That(p[1].r,Is.GreaterThan(250)); Assert.That(p[2].a,Is.Zero); }
        finally { RenderTexture.active=old; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(read); }
    }
    [Test] public void InterleavedBuilds_SecondWaitsWithoutDuplicateBake()
    {
        a = new GameObject("first builder"); b = new GameObject("second builder");
        var first = a.AddComponent<PsxSpriteRenderer>(); var second = b.AddComponent<PsxSpriteRenderer>();
        foreach (var r in new[] { first, second })
        { r.atlas = atlas; r.metadataJson = meta; r.palettesJson = palettes; r.buildBatchSize = 1; }
        var type = typeof(PsxSpriteRenderer);
        string key = (string)type.GetMethod("ComputeResourceKey", Private).Invoke(first, null);
        var one = (IEnumerator)type.GetMethod("BuildFramesCoroutine", Private).Invoke(first, new object[] { key });
        var two = (IEnumerator)type.GetMethod("BuildFramesCoroutine", Private).Invoke(second, new object[] { key });
        try
        {
            Assert.That(one.MoveNext(), Is.True);
            Assert.That(two.MoveNext(), Is.True);
            Assert.That(type.GetField("_pendingFrames", Private).GetValue(second), Is.Null,
                "Second instance should wait for the first bake, not allocate duplicate textures");
            while (one.MoveNext()) { }
            while (two.MoveNext()) { }
            Assert.That(a.GetComponent<SpriteRenderer>().sprite, Is.SameAs(b.GetComponent<SpriteRenderer>().sprite));
        }
        finally { (one as System.IDisposable)?.Dispose(); (two as System.IDisposable)?.Dispose(); }
    }

    [TearDown] public void Cleanup()
    {
        Disable(a); Disable(b);
        if(a) Object.DestroyImmediate(a); if(b) Object.DestroyImmediate(b);
        Object.DestroyImmediate(atlas); Object.DestroyImmediate(meta); Object.DestroyImmediate(palettes);
    }
}
