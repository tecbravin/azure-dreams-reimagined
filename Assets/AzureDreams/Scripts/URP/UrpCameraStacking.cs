using AzureDreamsReimagined.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AzureDreamsReimagined.URP
{
    /// <summary>
    /// URP side of CameraStacking: an extra camera drawn over the gameplay camera (the fusion glow)
    /// becomes an Overlay in the base camera's stack. Overlays clear only depth (URP default), like the Built-in
    /// ClearFlags.Depth the fusion glow camera uses. Does nothing while the Built-in pipeline is active.
    /// </summary>
    static class UrpCameraStacking
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Install()
        {
            CameraStacking.OverlayAdded = Add;
            CameraStacking.OverlayRemoved = Remove;
        }

        static bool UrpActive => GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;

        static UniversalAdditionalCameraData Data(Camera camera)
        {
            if (!camera.TryGetComponent(out UniversalAdditionalCameraData data))
                data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            return data;
        }

        static void Add(Camera baseCamera, Camera overlay)
        {
            if (!UrpActive) return;
            var overlayData = Data(overlay);
            overlayData.renderType = CameraRenderType.Overlay;
            var stack = Data(baseCamera).cameraStack;
            if (stack != null && !stack.Contains(overlay)) stack.Add(overlay);
        }

        static void Remove(Camera baseCamera, Camera overlay)
        {
            if (!UrpActive || !baseCamera.TryGetComponent(out UniversalAdditionalCameraData data)) return;
            data.cameraStack?.Remove(overlay);
        }
    }
}
