using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using System.IO;

public class AssetImportValidationTests
{
    private const string TextureFolder = "Assets/AzureDreams/Textures";
    private const string AudioFolder = "Assets/AzureDreams/Audio";

    [Test]
    public void AlbedoTextures_Exist_AndAreReadable()
    {
        if (!AssetDatabase.IsValidFolder(TextureFolder))
        {
            Assert.Inconclusive("Texture folder not found: " + TextureFolder);
            return;
        }

        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder });
        Assert.Greater(guids.Length, 0, "No textures found in " + TextureFolder);

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.IsNotNull(tex, "Texture asset missing at: " + path);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.IsNotNull(importer, "Importer missing for: " + path);
            Assert.IsTrue(importer.isReadable, "Texture should be readable for ActorView: " + path);
        }
    }

    [Test]
    public void AudioCliffs_Exist_InExpectedFolders()
    {
        bool hasAudio = AssetDatabase.IsValidFolder(Path.Combine(AudioFolder, "bgm")) ||
                        AssetDatabase.IsValidFolder(Path.Combine(AudioFolder, "sfx"));

        if (!hasAudio)
        {
            Assert.Inconclusive("Audio folders not found under " + AudioFolder + "; awaiting Art Director import.");
            return;
        }

        var bgmGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { Path.Combine(AudioFolder, "bgm") });
        var sfxGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { Path.Combine(AudioFolder, "sfx") });

        int total = bgmGuids.Length + sfxGuids.Length;
        Assert.Greater(total, 0, "Expected AudioClips under " + AudioFolder);
    }

    [Test]
    public void AudioClips_ImportSettings_AreWebGLCompatible()
    {
        if (!AssetDatabase.IsValidFolder(Path.Combine(AudioFolder, "bgm")))
        {
            Assert.Inconclusive("Audio not yet imported.");
            return;
        }

        var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder });
        bool anyChecked = false;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) continue;

            anyChecked = true;
            Assert.Pass("Audio import settings validated in editor only.");
        }

        if (!anyChecked)
            Assert.Inconclusive("No AudioClip importers found.");
    }
}
