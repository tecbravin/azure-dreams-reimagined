using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        [SerializeField] private PostProcessingController _postProcessPrefab;
        private PostProcessingController _cameraEffects;
        private Vector2 _graphicsScroll;

        private void InstallCameraEffects()
        {
            if (!_gameCamera) return;
            if (_gameCamera.TryGetComponent(out _cameraEffects)) return;
            if (!_postProcessPrefab) return;
            _cameraEffects = _gameCamera.gameObject.AddComponent<PostProcessingController>();
            _cameraEffects.ApplyPrefabPreset(_postProcessPrefab);
        }

        private void DrawGraphicsSettings()
        {
            var effects = _cameraEffects;
            if (!effects)
            {
                GUILayout.Label(T("Graphics effects are unavailable.", "Os efeitos gráficos estão indisponíveis."));
                return;
            }
            if (!effects.EffectSupported)
                GUILayout.Label(T("The post-processing shader is unavailable.", "O shader de pós-processamento está indisponível."));
            _graphicsScroll = GUILayout.BeginScrollView(_graphicsScroll);
            effects.enablePostProcessing = GUILayout.Toggle(effects.enablePostProcessing,
                T("Post-processing", "Pós-processamento"));
            bool previouslyEnabled = GUI.enabled;
            GUI.enabled = previouslyEnabled && effects.RetroSupported;
            if (GUILayout.Button(T("Pixel Art Preset", "Predefinição Pixel Art"))) effects.ApplyPixelArtPreset();
            GUI.enabled = GUI.enabled && effects.enablePostProcessing;
            effects.lowResolution = GUILayout.Toggle(effects.lowResolution,
                T("Low resolution (point filter)", "Baixa resolução (pixels nítidos)"));
            if (effects.lowResolution)
                effects.pixelHeight = Mathf.RoundToInt(GraphicsSlider(T("Pixel height", "Altura em pixels"), effects.pixelHeight, 120, 480));
            effects.colorQuantization = GUILayout.Toggle(effects.colorQuantization,
                T("PS1 color quantization", "Quantização de cores PS1"));
            if (effects.colorQuantization)
            {
                effects.colorBits = Mathf.RoundToInt(GraphicsSlider(T("Bits per channel", "Bits por canal"), effects.colorBits, 3, 8));
                effects.ditherStrength = GraphicsSlider(T("Dithering", "Pontilhado"), effects.ditherStrength, 0, 1);
            }
            effects.crtScanlines = GUILayout.Toggle(effects.crtScanlines, T("CRT scanlines", "Linhas CRT"));
            if (effects.crtScanlines)
                effects.scanlineStrength = GraphicsSlider(T("Intensity", "Intensidade"), effects.scanlineStrength, 0, 0.6f);

            GUI.enabled = previouslyEnabled && effects.enablePostProcessing;
            effects.fxaa = GUILayout.Toggle(effects.fxaa, "FXAA");
            effects.smaa = GUILayout.Toggle(effects.smaa, T("SMAA (FXAA fallback)", "SMAA (usa FXAA)"));
            effects.bloom = GUILayout.Toggle(effects.bloom, T("Bloom", "Brilho"));
            if (effects.bloom)
            {
                effects.bloomIntensity = GraphicsSlider(T("Intensity", "Intensidade"), effects.bloomIntensity, 0, 10);
                effects.bloomThreshold = GraphicsSlider(T("Threshold", "Limiar"), effects.bloomThreshold, 0, 1);
            }
            effects.colorGrading = GUILayout.Toggle(effects.colorGrading, T("Color grading", "Correção de cor"));
            if (effects.colorGrading)
            {
                effects.temperature = GraphicsSlider(T("Temperature", "Temperatura"), effects.temperature, -100, 100);
                effects.tint = GraphicsSlider(T("Tint", "Matiz"), effects.tint, -100, 100);
                effects.saturation = GraphicsSlider(T("Saturation", "Saturação"), effects.saturation, 0, 2);
                effects.contrast = GraphicsSlider(T("Contrast", "Contraste"), effects.contrast, 0, 2);
            }
            effects.vignette = GraphicsEffect(T("Vignette", "Vinheta"), effects.vignette, ref effects.vignetteIntensity);
            effects.ambientOcclusion = GraphicsEffect(T("Ambient occlusion", "Oclusão de ambiente"),
                effects.ambientOcclusion, ref effects.aoIntensity);
            effects.depthOfField = GUILayout.Toggle(effects.depthOfField, T("Depth of field", "Profundidade de campo"));
            if (effects.depthOfField)
                effects.focalDistance = GraphicsSlider(T("Focal distance", "Distância focal"), effects.focalDistance, 0.1f, 100);
            effects.chromaticAberration = GraphicsEffect(T("Chromatic aberration", "Aberração cromática"),
                effects.chromaticAberration, ref effects.chromaticIntensity);
            effects.filmGrain = GraphicsEffect(T("Film grain", "Granulação"), effects.filmGrain, ref effects.grainIntensity);
            GUI.enabled = previouslyEnabled;
            effects.msaa = GUILayout.Toggle(effects.msaa, "MSAA");
            GUILayout.EndScrollView();
            if (GUILayout.Button(T("Reset to preset", "Restaurar padrão"))) effects.ApplyPrefabPreset(_postProcessPrefab);
        }

        private static bool GraphicsEffect(string label, bool enabled, ref float intensity)
        {
            enabled = GUILayout.Toggle(enabled, label);
            if (enabled) intensity = GraphicsSlider(T("Intensity", "Intensidade"), intensity, 0, 1);
            return enabled;
        }

        private static float GraphicsSlider(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(110));
            value = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.Label(value.ToString("0.00"), GUILayout.Width(46));
            GUILayout.EndHorizontal();
            return value;
        }
    }
}
