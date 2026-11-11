using UnityEngine;

/// <summary>Reusable settings for the project's actual Built-in camera renderer.</summary>
[CreateAssetMenu(menuName = "Azure Dreams/Rendering/Built-in Post Process Profile")]
public sealed class BuiltinPostProcessProfile : ScriptableObject
{
    public bool fxaa = true, bloom = true, colorGrading = true, vignette = true;
    public bool ambientOcclusion = true, depthOfField, chromaticAberration, filmGrain;
    [Range(0,10)] public float bloomIntensity = 1;
    [Range(0,1)] public float bloomThreshold = 0.9f;
    [Range(-100,100)] public float temperature, tint;
    [Range(0,2)] public float saturation = 1, contrast = 1;
    [Range(0,1)] public float vignetteIntensity = 0.3f, aoIntensity = 0.5f;
    [Range(0.1f,100)] public float focalDistance = 5;
    [Range(0,1)] public float chromaticIntensity = 0.1f, grainIntensity = 0.2f;

    public void ApplyTo(PostProcessingController target)
    {
        if (!target) return;
        target.fxaa = fxaa; target.bloom = bloom; target.colorGrading = colorGrading;
        target.vignette = vignette; target.ambientOcclusion = ambientOcclusion;
        target.depthOfField = depthOfField; target.chromaticAberration = chromaticAberration;
        target.filmGrain = filmGrain; target.bloomIntensity = bloomIntensity;
        target.bloomThreshold = bloomThreshold; target.temperature = temperature; target.tint = tint;
        target.saturation = saturation; target.contrast = contrast;
        target.vignetteIntensity = vignetteIntensity; target.aoIntensity = aoIntensity;
        target.focalDistance = focalDistance; target.chromaticIntensity = chromaticIntensity;
        target.grainIntensity = grainIntensity;
    }
}
