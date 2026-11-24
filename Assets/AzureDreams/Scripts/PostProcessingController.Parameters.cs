using UnityEngine;

// Shader parameters shared by the Built-in OnRenderImage path and the URP renderer feature
// (AzureDreams.URP), so both pipelines grade the image the same way.
public sealed partial class PostProcessingController
{
    /// <summary>The colour quantization / low-resolution / scanline pass runs after the main effect.</summary>
    public bool RetroActive => lowResolution || colorQuantization || crtScanlines;

    /// <summary>Ambient occlusion and depth of field read the camera depth texture.</summary>
    public bool NeedsDepth => enablePostProcessing && (ambientOcclusion || depthOfField);

    public void ApplyEffectParameters(Material material)
    {
        material.SetVector(AAAndBloom, new Vector4(fxaa || smaa ? 1 : 0, bloom ? 1 : 0, bloomIntensity, bloomThreshold));
        float cameraContrast = colorGrading ? contrast : 1f;
        if (PreserveBlackBackground) cameraContrast = Mathf.Max(1f, cameraContrast);
        material.SetVector(Grade, new Vector4(colorGrading ? saturation : 1, cameraContrast,
            vignette ? vignetteIntensity : 0, filmGrain && !PreserveBlackBackground ? grainIntensity : 0));
        material.SetVector(Balance, new Vector4(colorGrading ? temperature / 100 : 0,
            colorGrading ? tint / 100 : 0, chromaticAberration ? chromaticIntensity : 0, Time.unscaledTime));
        material.SetVector(Depth, new Vector4(aoIntensity, Mathf.Max(focalDistance, 0.1f),
            depthOfField ? 1 : 0, ambientOcclusion ? 1 : 0));
    }

    /// <summary>Preserves the aspect ratio; the retro pass shades on the same grid as the point upscale.</summary>
    public void RetroTargetSize(int sourceWidth, int sourceHeight, out int width, out int height)
    {
        height = lowResolution ? Mathf.Clamp(pixelHeight, 1, sourceHeight) : sourceHeight;
        width = Mathf.Max(1, Mathf.RoundToInt((float)sourceWidth / sourceHeight * height));
    }

    public void ApplyRetroParameters(Material material, int height)
    {
        material.SetVector(Retro, new Vector4(colorQuantization ? Mathf.Clamp(colorBits, 3, 8) : 0,
            ditherStrength, crtScanlines ? scanlineStrength : 0, lowResolution ? height : 240));
    }
}
