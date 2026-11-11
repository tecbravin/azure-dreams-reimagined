using UnityEditor;
using UnityEngine;
using System.IO;

namespace AzureDreamsReimagined.Editor
{
    /// <summary>Overrides general art import rules only for PSX byte-data textures.</summary>
    public sealed class PsxDataTextureImporter : AssetPostprocessor
    {
        public override int GetPostprocessOrder() => 100;
        public override uint GetVersion() => 2;
        private void OnPreprocessTexture()
        {
            bool indexed = assetPath.Contains("/Sprites/Indexed/") ||
                Path.GetFileName(assetPath) == "atlas_index.png";
            bool verifiedArt = assetPath.Contains("/Sprites/DungeonCreatures/");
            if (!indexed && !verifiedArt) return;
            Configure((TextureImporter)assetImporter, indexed);
        }

        public static void Configure(TextureImporter importer, bool isByteData)
        {
            string filename = Path.GetFileName(importer.assetPath);
            bool cpuAtlas = isByteData && filename == "atlas_index.png";
            bool indexAtlas = isByteData && (cpuAtlas || filename == "atlas_indices.png");
            var format = indexAtlas ? TextureImporterFormat.R8 : TextureImporterFormat.RGBA32;
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = !isByteData;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 512;
            importer.alphaSource = isByteData ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false; // Edge dilation would corrupt data.
            // The CPU compositor needs its source indices. GPU-only lookup data
            // does not need a retained CPU copy. Both paths remain lossless.
            importer.isReadable = cpuAtlas;
            importer.anisoLevel = 0;
            var defaults = importer.GetDefaultPlatformTextureSettings();
            defaults.maxTextureSize = 512;
            defaults.format = format;
            defaults.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(defaults);
            foreach (string platform in new[] { "Standalone", "WebGL", "Android", "iPhone" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.overridden = true;
                settings.maxTextureSize = 512;
                settings.format = format;
                settings.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SetPlatformTextureSettings(settings);
            }
        }
    }
}
