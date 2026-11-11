using NUnit.Framework;
using UnityEngine;
using AzureDreamsReimagined.Rendering;
using System.IO;

public class PsxSpriteRendererEditorTests
{
    [Test]
    public void BuildFrames_Resource001_ProducesFrames()
    {
        UnityEditor.AssetDatabase.ImportAsset("Assets/AzureDreams/Sprites/Indexed/resource_001/atlas_index.png", UnityEditor.ImportAssetOptions.ForceUpdate);
        // Load PNG bytes directly to bypass import settings (guarantees readable texture)
        byte[] pngBytes = System.IO.File.ReadAllBytes(
            "Assets/AzureDreams/Sprites/Indexed/resource_001/atlas_index.png");
        var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        atlas.LoadImage(pngBytes);
        atlas.filterMode = FilterMode.Point;
        var meta = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(
            "Assets/AzureDreams/Sprites/DungeonCreatures/resource_001/metadata.json");
        var pals = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(
            "Assets/AzureDreams/Sprites/DungeonCreatures/resource_001/palettes.json");

        Assert.IsNotNull(atlas, "atlas_index.png not found");
        Assert.IsNotNull(meta, "metadata.json not found");
        Assert.IsNotNull(pals, "palettes.json not found");

        var go = new GameObject("SpriteTest");
        var sr = go.AddComponent<PsxSpriteRenderer>();
        sr.atlas = atlas;
        sr.metadataJson = meta;
        sr.palettesJson = pals;
        sr.buildBatchSize = 1000; // build all in one step
        // Trigger build by calling Start via reflection (Awake already ran)
        var startMethod = typeof(PsxSpriteRenderer).GetMethod("Start",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        startMethod.Invoke(sr, null);

        // Drive the build coroutine to completion (EditMode does not auto-run coroutines)
        var buildMethod = typeof(PsxSpriteRenderer).GetMethod("BuildFramesCoroutine",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var keyMethod = typeof(PsxSpriteRenderer).GetMethod("ComputeResourceKey",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        string key = (string)keyMethod.Invoke(sr, null);
        var enumerator = buildMethod.Invoke(sr, new object[] { key }) as System.Collections.IEnumerator;
        if (enumerator != null)
        {
            int guard = 0;
            while (enumerator.MoveNext() && guard < 1000000)
                guard++;
        }

        // Access _frames via reflection
        var framesField = typeof(PsxSpriteRenderer).GetField("_frames",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var frames = (System.Collections.IList)framesField.GetValue(sr);
        Assert.Greater(frames.Count, 0, "No frames built");

        Object.DestroyImmediate(go);
    }
}
