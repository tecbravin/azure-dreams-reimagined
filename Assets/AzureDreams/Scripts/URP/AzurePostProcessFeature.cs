using AzureDreamsReimagined.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace AzureDreamsReimagined.URP
{
    /// <summary>
    /// URP replacement for the two OnRenderImage effects, which URP never calls. Add it to the URP
    /// renderer. For each game camera it reads the camera's own components and applies them in the
    /// same order as the Built-in chain:
    ///   1. PostProcessingController: Hidden/AzureDreams/URP/PostProcess (FXAA, bloom, grading, AO, DOF...),
    ///      then Hidden/AzureDreams/URP/PSX Color Quantization on a point-filtered low-resolution buffer;
    ///   2. PsxLowResRender: point downsample to its target size and back.
    /// Parameters come from the same methods the Built-in path uses, so both pipelines match.
    /// </summary>
    public sealed class AzurePostProcessFeature : ScriptableRendererFeature
    {
        [SerializeField] Shader _effectShader;
        [SerializeField] Shader _retroShader;
        [SerializeField] RenderPassEvent _event = RenderPassEvent.BeforeRenderingPostProcessing;

        Material _effect;
        Material _retro;
        AzurePostProcessPass _pass;

        public override void Create()
        {
            _pass = new AzurePostProcessPass { renderPassEvent = _event };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;
            if (renderingData.cameraData.cameraType != CameraType.Game || !camera) return;
            camera.TryGetComponent(out PostProcessingController effects);
            camera.TryGetComponent(out PsxLowResRender lowRes);
            bool useEffects = effects && effects.isActiveAndEnabled && effects.enablePostProcessing && EnsureMaterials();
            bool useLowRes = lowRes && lowRes.isActiveAndEnabled;
            if (!useEffects && !useLowRes) return;

            _pass.Setup(useEffects ? effects : null, useLowRes ? lowRes : null, _effect, _retro);
            _pass.ConfigureInput(useEffects && effects.NeedsDepth ? ScriptableRenderPassInput.Depth : ScriptableRenderPassInput.None);
            renderer.EnqueuePass(_pass);
        }

        bool EnsureMaterials()
        {
            if (!_effect)
            {
                var shader = _effectShader ? _effectShader : Shader.Find("Hidden/AzureDreams/URP/PostProcess");
                if (shader) _effect = CoreUtils.CreateEngineMaterial(shader);
            }
            if (!_retro)
            {
                var shader = _retroShader ? _retroShader : Shader.Find("Hidden/AzureDreams/URP/PSX Color Quantization");
                if (shader) _retro = CoreUtils.CreateEngineMaterial(shader);
            }
            return _effect;
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_effect);
            CoreUtils.Destroy(_retro);
            _effect = _retro = null;
        }
    }

    sealed class AzurePostProcessPass : ScriptableRenderPass
    {
        static readonly int SourceTexelSize = Shader.PropertyToID("_AzureSourceTexelSize");

        PostProcessingController _effects;
        PsxLowResRender _lowRes;
        Material _effect;
        Material _retro;

        class PassData
        {
            public TextureHandle source;
            public Material material;
            public Vector4 texelSize;
            public bool bilinear;
        }

        public AzurePostProcessPass()
        {
            // The passes read the camera colour, so URP must not render straight to the back buffer.
            requiresIntermediateTexture = true;
            profilingSampler = new ProfilingSampler("AzureDreams Post Process");
        }

        public void Setup(PostProcessingController effects, PsxLowResRender lowRes, Material effect, Material retro)
        {
            _effects = effects;
            _lowRes = lowRes;
            _effect = effect;
            _retro = retro;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) return;
            TextureHandle color = resources.activeColorTexture;
            TextureDesc desc = renderGraph.GetTextureDesc(color);
            desc.clearBuffer = false;
            desc.depthBufferBits = DepthBits.None;
            desc.msaaSamples = MSAASamples.None;
            int width = desc.width, height = desc.height;
            TextureHandle depth = _effects && _effects.NeedsDepth ? resources.cameraDepthTexture : TextureHandle.nullHandle;

            if (_effects)
            {
                _effects.ApplyEffectParameters(_effect);
                if (_retro && _effects.RetroActive)
                {
                    // Main effect into a point-filtered buffer at the retro resolution, then quantize to full size.
                    _effects.RetroTargetSize(width, height, out int retroWidth, out int retroHeight);
                    var pixels = Target(renderGraph, desc, "AzureDreams Retro Pixels", retroWidth, retroHeight, FilterMode.Point);
                    Draw(renderGraph, "AzureDreams Effect", color, pixels, _effect, width, height, depth, true);
                    _effects.ApplyRetroParameters(_retro, retroHeight);
                    var retroOut = Target(renderGraph, desc, "AzureDreams Retro", width, height, desc.filterMode);
                    Draw(renderGraph, "AzureDreams Color Quantization", pixels, retroOut, _retro, retroWidth, retroHeight, TextureHandle.nullHandle, false);
                    color = retroOut;
                }
                else
                {
                    var effectOut = Target(renderGraph, desc, "AzureDreams Effect", width, height, desc.filterMode);
                    Draw(renderGraph, "AzureDreams Effect", color, effectOut, _effect, width, height, depth, true);
                    color = effectOut;
                }
            }

            if (_lowRes)
            {
                int lowWidth = Mathf.Clamp(_lowRes.targetWidth, 1, SystemInfo.maxTextureSize);
                int lowHeight = Mathf.Clamp(_lowRes.targetHeight, 1, SystemInfo.maxTextureSize);
                var low = Target(renderGraph, desc, "AzureDreams Low Res", lowWidth, lowHeight, _lowRes.filterMode);
                Draw(renderGraph, "AzureDreams Low Res Down", color, low, null, width, height, TextureHandle.nullHandle, true);
                var upscaled = Target(renderGraph, desc, "AzureDreams Low Res Up", width, height, desc.filterMode);
                Draw(renderGraph, "AzureDreams Low Res Up", low, upscaled, null, lowWidth, lowHeight, TextureHandle.nullHandle,
                    _lowRes.filterMode != FilterMode.Point);
                color = upscaled;
            }

            resources.cameraColor = color;
        }

        static TextureHandle Target(RenderGraph renderGraph, TextureDesc desc, string name, int width, int height, FilterMode filter)
        {
            desc.name = name;
            desc.width = width;
            desc.height = height;
            desc.filterMode = filter;
            desc.wrapMode = TextureWrapMode.Clamp;
            return renderGraph.CreateTexture(desc);
        }

        void Draw(RenderGraph renderGraph, string name, TextureHandle source, TextureHandle destination, Material material,
            int sourceWidth, int sourceHeight, TextureHandle depth, bool bilinear)
        {
            using var builder = renderGraph.AddRasterRenderPass<PassData>(name, out var data, profilingSampler);
            data.source = source;
            data.material = material;
            data.texelSize = new Vector4(1f / sourceWidth, 1f / sourceHeight, sourceWidth, sourceHeight);
            data.bilinear = bilinear;
            builder.UseTexture(source);
            if (depth.IsValid())
            {
                // SampleSceneDepth reads the global _CameraDepthTexture URP sets after its depth copy.
                builder.UseTexture(depth);
                builder.UseAllGlobalTextures(true);
            }
            builder.SetRenderAttachment(destination, 0);
            builder.SetRenderFunc(static (PassData d, RasterGraphContext context) =>
            {
                if (d.material)
                {
                    d.material.SetVector(SourceTexelSize, d.texelSize);
                    Blitter.BlitTexture(context.cmd, d.source, new Vector4(1, 1, 0, 0), d.material, 0);
                }
                else
                    Blitter.BlitTexture(context.cmd, d.source, new Vector4(1, 1, 0, 0), 0, d.bilinear);
            });
        }
    }
}
