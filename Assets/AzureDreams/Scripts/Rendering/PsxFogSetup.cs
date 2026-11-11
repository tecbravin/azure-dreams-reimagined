using UnityEngine;
using UnityEngine.Rendering;

namespace AzureDreamsReimagined.Rendering
{
    /// <summary>
    /// PS1-style fog configuration. PS1 games used fog extensively for depth and atmosphere.
    /// Linear fog: fog = (far - distance) / (far - near), clamped to [0,1].
    /// </summary>
    public static class PsxFogSetup
    {
        public static void ApplyLinearFog(Color fogColor, float fogStart, float fogEnd)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
        }

        public static void ApplyExpFog(Color fogColor, float density)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = density;
        }

        public static void DisableFog()
        {
            RenderSettings.fog = false;
        }
    }
}
