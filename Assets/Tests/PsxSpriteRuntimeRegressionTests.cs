using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AzureDreamsReimagined.Rendering;

public class PsxSpriteRuntimeRegressionTests
{
    const string Metadata = "{\"chains\":[{\"pointer\":1,\"parts\":[{\"width\":1,\"height\":1}]}]}";
    const string Palettes = "[{\"index\":0,\"words_rgb555_stp\":[0,31]}]";

    static IEnumerator WaitForSprite(GameObject go)
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while (go.GetComponent<SpriteRenderer>().sprite == null && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(go.GetComponent<SpriteRenderer>().sprite, Is.Not.Null, "Sprite build/load did not finish");
    }

    [UnityTest]
    public IEnumerator SharedFrames_RuntimeDisableAndDestroy_ReleaseLastLease()
    {
        var atlas = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
        atlas.SetPixels32(new[] { new Color32(1, 0, 0, 255) });
        atlas.Apply();
        var meta = new TextAsset(Metadata);
        var palettes = new TextAsset(Palettes);
        var a = new GameObject("shared sprite A");
        var b = new GameObject("shared sprite B");
        try
        {
            foreach (var go in new[] { a, b })
            {
                var renderer = go.AddComponent<PsxSpriteRenderer>();
                renderer.atlas = atlas;
                renderer.metadataJson = meta;
                renderer.palettesJson = palettes;
                renderer.buildBatchSize = 1;
            }
            yield return WaitForSprite(a);
            yield return WaitForSprite(b);
            var original = a.GetComponent<SpriteRenderer>().sprite;
            var texture = original.texture;
            Assert.That(b.GetComponent<SpriteRenderer>().sprite, Is.SameAs(original));
            a.SetActive(false);
            Assert.That(original != null, Is.True);
            Object.Destroy(b);
            yield return null;
            yield return null;
            Assert.That(original == null, Is.True, "Last lease must release the sprite");
            Assert.That(texture == null, Is.True, "Last lease must release the baked texture");
            a.SetActive(true);
            yield return WaitForSprite(a);
            Assert.That(a.GetComponent<SpriteRenderer>().sprite, Is.Not.SameAs(original));
        }
        finally
        {
            Object.DestroyImmediate(a);
            if (b != null) Object.DestroyImmediate(b);
            Object.DestroyImmediate(atlas);
            Object.DestroyImmediate(meta);
            Object.DestroyImmediate(palettes);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator StreamingAssets_LoadsWithoutAssignedAssets_AndReleasesOwnedSources()
    {
        const string folder = "PsxSpriteRepairFixture";
        string directory = Path.Combine(Application.streamingAssetsPath, folder);
        Directory.CreateDirectory(directory);
        var source = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
        var go = new GameObject("streamed sprite");
        try
        {
            source.SetPixels32(new[] { new Color32(1, 0, 0, 255) });
            source.Apply();
            File.WriteAllBytes(Path.Combine(directory, "atlas_index.png"), source.EncodeToPNG());
            File.WriteAllText(Path.Combine(directory, "metadata.json"), Metadata);
            File.WriteAllText(Path.Combine(directory, "palettes.json"), Palettes);
            var renderer = go.AddComponent<PsxSpriteRenderer>();
            renderer.streamingAtlasRelativePath = folder + "/atlas_index.png";
            renderer.buildBatchSize = 1;
            yield return WaitForSprite(go);
            Assert.That(renderer.atlas, Is.Not.Null);
            Assert.That(renderer.metadataJson, Is.Not.Null);
            Assert.That(renderer.palettesJson, Is.Not.Null);
            Assert.That(renderer.atlas.GetPixels32()[0].r, Is.EqualTo(1));
            var atlas = renderer.atlas;
            var meta = renderer.metadataJson;
            var palettes = renderer.palettesJson;
            go.SetActive(false);
            yield return null;
            Assert.That(atlas == null, Is.True);
            Assert.That(meta == null, Is.True);
            Assert.That(palettes == null, Is.True);
            Assert.That(renderer.atlas == null, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(source);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            if (File.Exists(directory + ".meta")) File.Delete(directory + ".meta");
        }
        yield return null;
    }
}
