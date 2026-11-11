using UnityEngine;

namespace AzureDreamsReimagined.Rendering
{
    /// <summary>
    /// PS1-style low-resolution rendering.
    /// Pixelates the rendered scene through a fixed-size buffer, then point-upscales.
    /// This is a visual post-effect, NOT a claim of reduced scene shading cost.
    /// Attach to a Camera and set the target resolution.
    ///
    /// Optimizations (Architect):
    ///  - RenderTexture is allocated on enable and recovered before camera rendering.
    ///  - OnRenderImage falls back to a passthrough Blit if the RT is missing.
    ///  - WebGL capability check disables low-res rendering when the RT format
    ///    is unsupported, falling back to native resolution.
    ///  - Runtime teardown uses Destroy (not DestroyImmediate).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PsxLowResRender : MonoBehaviour
    {
        public int targetWidth = 320;
        public int targetHeight = 240;
        public FilterMode filterMode = FilterMode.Point;

        private Camera _camera;
        private RenderTexture _lowResRT;
        private bool _supported = true;

        void OnEnable()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null) { _supported = false; return; }

            // WebGL capability fallback: if the RT format is unsupported, disable
            // low-res rendering and let the camera render to the backbuffer natively.
            if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32))
            {
                Debug.LogWarning("PsxLowResRender: ARGB32 RenderTexture unsupported on this platform; disabling low-res render.");
                _supported = false;
                return;
            }

            _supported = true;
            CreateRenderTexture();
        }

        void OnDisable()
        {
            if (_lowResRT != null)
            {
                _lowResRT.Release();
                if (Application.isPlaying) Destroy(_lowResRT);
                else DestroyImmediate(_lowResRT);
                _lowResRT = null;
            }
        }

        void CreateRenderTexture()
        {
            if (_lowResRT != null) return;
            _lowResRT = new RenderTexture(Mathf.Clamp(targetWidth, 1, SystemInfo.maxTextureSize), Mathf.Clamp(targetHeight, 1, SystemInfo.maxTextureSize), 0, RenderTextureFormat.ARGB32);
            _lowResRT.filterMode = filterMode;
            _lowResRT.wrapMode = TextureWrapMode.Clamp;
            // Do not redirect the Camera to this RT: that loses the backbuffer
            // presentation destination and can produce a self-blit/black game view.
            _supported = _lowResRT.Create();
        }

        void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            // Passthrough if low-res is disabled or the RT is missing (never allocate here).
            if (!_supported || _lowResRT == null || !_lowResRT.IsCreated())
            {
                Graphics.Blit(src, dest);
                return;
            }

            // The source contains the completed scene; the destination belongs to Unity.
            // Downsample once, then present with nearest-neighbor. No per-frame allocation.
            Graphics.Blit(src, _lowResRT);
            Graphics.Blit(_lowResRT, dest);
        }

        void OnPreCull()
        {
            if (_supported && _lowResRT && !_lowResRT.IsCreated()) _lowResRT.Create();
        }
    }
}
