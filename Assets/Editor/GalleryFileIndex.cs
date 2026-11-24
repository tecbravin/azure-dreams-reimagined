using System;
using System.IO;
using System.Linq;
using UnityEditor;

namespace AzureDreamsReimagined.Editor
{
    internal sealed class GalleryFileIndex : AssetPostprocessor
    {
        private const string Media = "Assets/StreamingAssets/Media/";
        private const string Index = "Assets/AzureDreams/Resources/GalleryFiles.txt";
        private static readonly string[] Folders =
            { "Reimagined/Portraits/Dialogue", "Portraits/Dialogue", "Portraits/Book", "Fanart" };

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] oldPaths)
        {
            if (imported.Concat(deleted).Concat(moved).Concat(oldPaths).Any(path =>
                    Folders.Any(folder => path.StartsWith(Media + folder + "/", StringComparison.Ordinal))))
                EditorApplication.delayCall += Rebuild;
        }

        [MenuItem("Tools/Azure Dreams/Rebuild Gallery Index")]
        private static void Rebuild()
        {
            string content = string.Join("\n", Folders.Where(folder => Directory.Exists(Media + folder))
                .SelectMany(folder => Directory.EnumerateFiles(Media + folder)
                    .Where(path => Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg")
                    .Select(path => folder + "/" + Path.GetFileName(path))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))) + "\n";
            if (File.Exists(Index) && File.ReadAllText(Index).Replace("\r\n", "\n") == content) return;
            File.WriteAllText(Index, content);
            AssetDatabase.ImportAsset(Index);
        }
    }
}
