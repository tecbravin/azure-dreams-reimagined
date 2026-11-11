using UnityEngine;
using UnityEditor;


public class TextureImportPostprocessor : AssetPostprocessor
{
    private const string AngelFramesPath = "Assets/AzureDreams/Resources/Title/AngelOpeningFrames.png";

    void OnPreprocessTexture()
    {
        TextureImporter importer = (TextureImporter)assetImporter;
        if (importer == null) return;

        if (assetPath == AngelFramesPath)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            return;
        }

        bool isAlbedo = assetPath.Contains("/Textures/");
        // PSX index/CLUT rules belong exclusively to PsxDataTextureImporter.
        if (!isAlbedo) return;

        // Small reconstructed Town materials are authored as PS1 pixel art.
        // Keep their palette blocks crisp; the general albedo rule below is
        // intended for larger modern-display textures and would blur them.
        if (assetPath.Contains("/Textures/TownGenerated/"))
        {
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.isReadable = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 32;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.compressionQuality = 0;
            foreach (string platformName in new[] { "WebGL", "Standalone" })
            {
                TextureImporterPlatformSettings platform = importer.GetPlatformTextureSettings(platformName);
                platform.overridden = true;
                platform.maxTextureSize = 32;
                platform.textureCompression = TextureImporterCompression.Uncompressed;
                platform.compressionQuality = 0;
                platform.format = TextureImporterFormat.RGBA32;
                importer.SetPlatformTextureSettings(platform);
            }
            return;
        }

        if (isAlbedo)
        {
            // Upscaled for modern displays: 1024 max, mipmaps for LOD
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = true;
            importer.isReadable = true;
            importer.npotScale = TextureImporterNPOTScale.ToNearest;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.compressionQuality = 100;

            // WebGL platform: 1024 max, compressed
            TextureImporterPlatformSettings webgl = importer.GetPlatformTextureSettings("WebGL");
            webgl.overridden = true;
            webgl.maxTextureSize = 1024;
            webgl.format = TextureImporterFormat.Automatic;
            webgl.compressionQuality = 100;
            webgl.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(webgl);

            // Standalone: 1024 max
            TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true;
            standalone.maxTextureSize = 1024;
            standalone.format = TextureImporterFormat.Automatic;
            standalone.compressionQuality = 100;
            standalone.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(standalone);
        }
    }

    void OnPostprocessTexture(Texture2D texture)
    {
        if (assetPath == AngelFramesPath)
        {
            // The upscaled sheet contains opaque near-black pixels in its transparent background.
            const byte backgroundThreshold = 10;
            Color32[] pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                if (pixel.r <= backgroundThreshold && pixel.g <= backgroundThreshold && pixel.b <= backgroundThreshold)
                    pixels[i].a = 0;
            }
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return;
        }

        if (assetPath.Contains("/Sprites/Indexed/") && assetPath.EndsWith("atlas_indices.png"))
        {
            // Validate that indexed atlas remains lossless and usable as lookup data.
            TextureImporter importer = (TextureImporter)assetImporter;
            if (importer != null)
            {
                var webgl = importer.GetPlatformTextureSettings("WebGL");
                bool isLossless = webgl.overridden &&
                                  webgl.format == TextureImporterFormat.R8 &&
                                  webgl.textureCompression == TextureImporterCompression.Uncompressed;
                if (!isLossless)
                {
                    Debug.LogError("[TextureImportPostprocessor] Indexed atlas is not lossless: " + assetPath +
                                   " format=" + webgl.format + " compression=" + webgl.textureCompression);
                }
            }

            // Color32 channels are bytes: a 0..255 range check cannot detect loss.
            // Regression tests compare exact source indices after import/GPU readback.
        }
    }
}
