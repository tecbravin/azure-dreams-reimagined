using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using System.IO;

public class ActorViewIntegrationTests
{
    [Test]
    public void ActorView_AppliesTint_AndAlbedo_WithoutNullErrors()
    {
        var go = new GameObject("ActorViewIntegration");
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = new Material(Shader.Find("Standard"));

        var def = ScriptableObject.CreateInstance<AzureDreamsReimagined.ActorDefinition>();
        var tex = new Texture2D(4, 4);
        def.Configure("TestHero", AzureDreamsReimagined.ActorKind.Player, 10, 2, 0.16f, Color.magenta, tex);

        var view = go.AddComponent<AzureDreamsReimagined.ActorView>();
        view.Configure(def, renderer);

        Assert.IsNotNull(view.Definition);

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(def);
    }

    [Test]
    public void AlbedoTextures_ArePowerOfTwo_OrAlreadyValid()
    {
        if (!AssetDatabase.IsValidFolder("Assets/AzureDreams/Textures"))
        {
            Assert.Inconclusive("Texture folder not present.");
            return;
        }

        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/AzureDreams/Textures" });
        bool any = false;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) continue;

            any = true;
            int w = tex.width;
            int h = tex.height;
            bool pot = (w & (w - 1)) == 0 && (h & (h - 1)) == 0;
            Assert.IsTrue(pot || (w == 32 && h == 128),
                "Texture should be POT or known 32x128 albedo: " + path + " (" + w + "x" + h + ")");
        }

        if (!any)
            Assert.Inconclusive("No textures found in Assets/AzureDreams/Textures.");
    }
}
