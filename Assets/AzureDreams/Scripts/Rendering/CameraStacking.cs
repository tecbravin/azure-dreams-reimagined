using System;
using UnityEngine;

namespace AzureDreamsReimagined.Rendering
{
    /// <summary>
    /// Extra cameras drawn over the gameplay camera (the fusion glow) register here. In the Built-in
    /// pipeline the camera depth order already layers them, so nothing happens. When URP is active,
    /// AzureDreams.URP installs handlers that make the camera an Overlay in the base camera's stack.
    /// Kept as hooks so the runtime assembly does not reference the URP package.
    /// </summary>
    public static class CameraStacking
    {
        public static Action<Camera, Camera> OverlayAdded;
        public static Action<Camera, Camera> OverlayRemoved;

        public static void AddOverlay(Camera baseCamera, Camera overlay)
        {
            if (baseCamera && overlay) OverlayAdded?.Invoke(baseCamera, overlay);
        }

        public static void RemoveOverlay(Camera baseCamera, Camera overlay)
        {
            if (baseCamera && overlay) OverlayRemoved?.Invoke(baseCamera, overlay);
        }
    }
}
