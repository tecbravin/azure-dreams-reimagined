using UnityEngine;

/// <summary>Built-in camera effects; no dependency on an uninstalled URP package.</summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public sealed partial class PostProcessingController : MonoBehaviour
{
    [Header("Post-Processing Toggles")]
    public bool enablePostProcessing = true;
    [Header("Anti-Aliasing")]
    public bool fxaa = true;
    [Tooltip("Legacy option: uses FXAA fallback, not a SMAA implementation.")]
    public bool smaa;
    [Tooltip("Allows camera MSAA. Sample count is controlled by Quality Settings.")]
    public bool msaa;
    [Header("Bloom")]
    public bool bloom = true;
    [Range(0,10)] public float bloomIntensity = 1;
    [Range(0,1)] public float bloomThreshold = 0.9f;
    [Header("Color Grading")]
    public bool colorGrading = true;
    [Range(-100,100)] public float temperature;
    [Range(-100,100)] public float tint;
    [Range(0,2)] public float saturation = 1;
    [Range(0,2)] public float contrast = 1;
    [Header("Vignette")]
    public bool vignette = true;
    [Range(0,1)] public float vignetteIntensity = 0.3f;
    [Header("Depth-only Ambient Occlusion")]
    public bool ambientOcclusion = true;
    [Range(0,1)] public float aoIntensity = 0.5f;
    [Header("Depth of Field")]
    public bool depthOfField;
    [Range(0.1f,100)] public float focalDistance = 5;
    [Header("Chromatic Aberration")]
    public bool chromaticAberration;
    [Range(0,1)] public float chromaticIntensity = 0.1f;
    [Header("Film Grain")]
    public bool filmGrain;
    [Range(0,1)] public float grainIntensity = 0.2f;
    [Header("Pixel Art")]
    public bool lowResolution;
    [Range(120, 480)] public int pixelHeight = 240;
    public bool colorQuantization;
    [Range(3, 8)] public int colorBits = 5;
    [Range(0, 1)] public float ditherStrength = 0.5f;
    public bool crtScanlines;
    [Range(0, 0.6f)] public float scanlineStrength = 0.22f;
    [Header("PS1 Vertex Wobble")]
    [Tooltip("Snaps vertex screen positions to a coarse grid, like the PS1's fixed-point transform. Applied globally to every PsxVertexLit/PsxIndexed surface, not just this camera's render.")]
    public bool psxVertexSnap;
    [Range(60, 480)] public int vertexSnapResolution = 160;
    [Tooltip("Drops perspective-correct texturing on PsxVertexLit/PsxIndexed surfaces, warping textures on oblique geometry like the PS1's affine texture mapping.")]
    public bool psxAffineWarp;
    [Range(0, 1)] public float affineWarpStrength = 1f;
    [SerializeField] private Shader _retroShader;

    [Tooltip("Optional preset, copied once on enable; runtime fields remain independently adjustable.")]
    [SerializeField] private BuiltinPostProcessProfile _profile;
    [SerializeField] private Shader _effectShader;
    private Camera _camera;
    private Material _material;
    private Material _retroMaterial;
    private static readonly int Retro = Shader.PropertyToID("_Retro");
    private static readonly int AAAndBloom = Shader.PropertyToID("_AAAndBloom");
    private static readonly int Grade = Shader.PropertyToID("_Grade");
    private static readonly int Balance = Shader.PropertyToID("_Balance");
    private static readonly int Depth = Shader.PropertyToID("_Depth");
    private static readonly int PsxSnapResolution = Shader.PropertyToID("_PSXVertexSnapResolution");
    private static readonly int PsxAffineWarp = Shader.PropertyToID("_PSXAffineWarpStrength");

    private void OnEnable()
    {
        _camera = GetComponent<Camera>();
        if (_profile) _profile.ApplyTo(this);
        RebuildMaterial();
        RebuildRetroMaterial();
        ApplyPsxVertexGlobals();
    }

    // PsxVertexLit.shader and PsxIndexed.shader read these as global shader properties (not
    // per-material) so every surface using them reacts to a single graphics-menu toggle at once,
    // instead of needing every material swapped or every renderer tracked individually.
    public void ApplyPsxVertexGlobals()
    {
        Shader.SetGlobalFloat(PsxSnapResolution, psxVertexSnap ? vertexSnapResolution : 0f);
        Shader.SetGlobalFloat(PsxAffineWarp, psxAffineWarp ? affineWarpStrength : 0f);
    }

    internal bool PreserveBlackBackground { get; set; }

    public bool EffectSupported => _material;
    public bool RetroSupported => _retroMaterial;

    // Reuse the prefab's profile and shader on the gameplay camera.
    public void ApplyPrefabPreset(PostProcessingController preset)
    {
        if (!_camera) _camera = GetComponent<Camera>();
        ResetToDefaults();
        if (preset && preset != this)
        {
            enablePostProcessing = preset.enablePostProcessing;
            msaa = preset.msaa;
            smaa = preset.smaa;
            fxaa = preset.fxaa;
            bloom = preset.bloom; bloomIntensity = preset.bloomIntensity; bloomThreshold = preset.bloomThreshold;
            colorGrading = preset.colorGrading; temperature = preset.temperature; tint = preset.tint;
            saturation = preset.saturation; contrast = preset.contrast;
            vignette = preset.vignette; vignetteIntensity = preset.vignetteIntensity;
            ambientOcclusion = preset.ambientOcclusion; aoIntensity = preset.aoIntensity;
            depthOfField = preset.depthOfField; focalDistance = preset.focalDistance;
            chromaticAberration = preset.chromaticAberration; chromaticIntensity = preset.chromaticIntensity;
            filmGrain = preset.filmGrain; grainIntensity = preset.grainIntensity;
            lowResolution = preset.lowResolution; pixelHeight = preset.pixelHeight;
            colorQuantization = preset.colorQuantization; colorBits = preset.colorBits;
            ditherStrength = preset.ditherStrength;
            crtScanlines = preset.crtScanlines; scanlineStrength = preset.scanlineStrength;
            psxVertexSnap = preset.psxVertexSnap; vertexSnapResolution = preset.vertexSnapResolution;
            psxAffineWarp = preset.psxAffineWarp; affineWarpStrength = preset.affineWarpStrength;
            _retroShader = preset._retroShader;
            _profile = preset._profile;
            _effectShader = preset._effectShader;
        }
        if (_profile) _profile.ApplyTo(this);
        if (isActiveAndEnabled)
        {
            RebuildMaterial();
            RebuildRetroMaterial();
            ApplyPsxVertexGlobals();
        }
    }

    private void RebuildMaterial()
    {
        Shader shader = _effectShader ? _effectShader : Shader.Find("Hidden/AzureDreams/BuiltinPostProcess");
        if (_material && _material.shader == shader) return;
        ReleaseMaterial(ref _material);
        if (shader && shader.isSupported)
            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
    }

    private void RebuildRetroMaterial()
    {
        Shader shader = _retroShader ? _retroShader : Shader.Find("AzureDreams/PSX Color Quantization");
        if (_retroMaterial && _retroMaterial.shader == shader) return;
        ReleaseMaterial(ref _retroMaterial);
        if (shader && shader.isSupported)
            _retroMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
    }

    private void OnDisable()
    {
        ReleaseMaterial(ref _material);
        ReleaseMaterial(ref _retroMaterial);
    }

    private static void ReleaseMaterial(ref Material material)
    {
        if (!material) return;
        if (Application.isPlaying) Destroy(material);
#if UNITY_EDITOR
        else DestroyImmediate(material);
#endif
        material = null;
    }

    private void OnPreCull()
    {
        _camera.allowMSAA = msaa;
        if (enablePostProcessing && (ambientOcclusion || depthOfField))
            _camera.depthTextureMode |= DepthTextureMode.Depth;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!enablePostProcessing || !_material)
        {
            Graphics.Blit(source, destination);
            return;
        }
        ApplyEffectParameters(_material);
        if (!_retroMaterial || !RetroActive)
        {
            Graphics.Blit(source, destination, _material, 0);
            return;
        }

        // Preserve the aspect ratio and shade the final pass on the same grid as the point upscale.
        RetroTargetSize(source.width, source.height, out int width, out int height);
        var pixels = RenderTexture.GetTemporary(width, height, 0, source.format);
        try
        {
            pixels.filterMode = FilterMode.Point;
            pixels.wrapMode = TextureWrapMode.Clamp;
            Graphics.Blit(source, pixels, _material, 0);
            ApplyRetroParameters(_retroMaterial, height);
            Graphics.Blit(pixels, destination, _retroMaterial, 0);
        }
        finally
        {
            RenderTexture.ReleaseTemporary(pixels);
        }
    }

    public void ApplyPixelArtPreset()
    {
        enablePostProcessing = true;
        lowResolution = colorQuantization = true;
        pixelHeight = 240;
        colorBits = 5;
        ditherStrength = 0.5f;
        fxaa = smaa = msaa = depthOfField = chromaticAberration = filmGrain = false;
        psxVertexSnap = psxAffineWarp = true;
        vertexSnapResolution = 160;
        affineWarpStrength = 1f;
    }

    public void ToggleAll(bool value)
    {
        enablePostProcessing = value;
        fxaa = bloom = colorGrading = vignette = ambientOcclusion = value;
    }

    public void ResetToDefaults()
    {
        enablePostProcessing = true;
        fxaa = true; smaa = false; msaa = false;
        bloom = true; bloomIntensity = 1; bloomThreshold = 0.9f;
        colorGrading = true; temperature = tint = 0; saturation = contrast = 1;
        vignette = true; vignetteIntensity = 0.3f;
        ambientOcclusion = true; aoIntensity = 0.5f;
        depthOfField = false; focalDistance = 5;
        chromaticAberration = false; chromaticIntensity = 0.1f;
        filmGrain = false; grainIntensity = 0.2f;
        lowResolution = colorQuantization = crtScanlines = false;
        pixelHeight = 240; colorBits = 5; ditherStrength = 0.5f; scanlineStrength = 0.22f;
        psxVertexSnap = false; vertexSnapResolution = 160;
        psxAffineWarp = false; affineWarpStrength = 1f;
    }
}
