using System;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Applies tower floor theme visuals (materials, fog, lighting) to an existing floor instance.
    /// Attach to the floor root GameObject or call from TowerGameController.
    /// </summary>
    public class TowerFloorThemeApplier : MonoBehaviour
    {
        [Header("Theme References")]
        public TowerFloorThemeData[] themeData;

        [Header("Floor Objects")]
        public Renderer floorRenderer;
        public Renderer wallRenderer;
        public Renderer backgroundRenderer;
        public ParticleSystem[] floorFx;

        private TowerFloorTheme _currentTheme;

        /// <summary>Apply theme for a given floor number.</summary>
        public void ApplyTheme(int floorNumber)
        {
            TowerFloorTheme theme = TowerFloorThemeResolver.GetThemeForFloor(floorNumber);
            ApplyTheme(theme);
        }

        /// <summary>Apply theme by enum.</summary>
        public void ApplyTheme(TowerFloorTheme theme)
        {
            _currentTheme = theme;
            var data = GetThemeData(theme);
            if (data == null)
            {
                Debug.LogWarning($"[TowerFloorThemeApplier] No data for theme {theme}");
                return;
            }

            if (floorRenderer && data.floorMaterial)
                floorRenderer.sharedMaterial = data.floorMaterial;
            if (wallRenderer && data.wallMaterial)
                wallRenderer.sharedMaterial = data.wallMaterial;
            if (backgroundRenderer && data.backgroundMaterial)
                backgroundRenderer.sharedMaterial = data.backgroundMaterial;

            RenderSettings.ambientSkyColor = data.ambientColor;
            RenderSettings.fogColor = data.fogColor;
            RenderSettings.fogDensity = data.fogDensity;

            // Clear existing FX
            if (floorFx != null)
            {
                foreach (var fx in floorFx)
                {
                    if (fx) Destroy(fx.gameObject);
                }
            }

            // Spawn new FX
            if (data.fxPrefabs != null)
            {
                foreach (var prefab in data.fxPrefabs)
                {
                    if (prefab)
                    {
                        var instance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
                        var ps = instance.GetComponent<ParticleSystem>();
                        if (ps) Array.Resize(ref floorFx, floorFx != null ? floorFx.Length + 1 : 1);
                        if (floorFx != null) floorFx[floorFx.Length - 1] = ps;
                    }
                }
            }

            Debug.Log($"[TowerFloorThemeApplier] Applied theme {theme} to floor");
        }

        private TowerFloorThemeData GetThemeData(TowerFloorTheme theme)
        {
            if (themeData == null) return null;
            foreach (var data in themeData)
            {
                if (data.theme == theme) return data;
            }
            return null;
        }

        public TowerFloorTheme GetCurrentTheme()
        {
            return _currentTheme;
        }
    }
}
