using UnityEngine;
using UnityEditor;

public class AudioImportPostprocessor : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        AudioImporter importer = (AudioImporter)assetImporter;
        if (importer == null) return;

        // Web-friendly settings for Azure Dreams Reimagined (WebGL)
        importer.loadInBackground = true;

        // Apply via defaultSampleSettings (obsolete but still functional in Unity 6000)
#pragma warning disable CS0618
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.CompressedInMemory;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.8f; // 80% quality for better fidelity
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;

        // 3D spatial settings for SFX
        if (assetPath.Contains("/sfx/"))
        {
            settings.preloadAudioData = false;
            // Spatialization belongs to AudioSource, not AudioImporter.
        }
        else if (assetPath.Contains("/bgm/"))
        {
            settings.preloadAudioData = true;
            // Preserve the original stereo channels; source playback stays 2D.
        }
#pragma warning restore CS0618
        importer.defaultSampleSettings = settings;
    }
}
