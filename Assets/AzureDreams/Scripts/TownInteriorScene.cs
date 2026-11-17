using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AzureDreamsReimagined
{
    // Captured geometry, not a guessed room layout. Navigation is a sampled floor mask.
    [ExecuteAlways]
    public sealed class TownInteriorScene : MonoBehaviour
    {
        private const string KohHouseLevelOneId = "Interior_01_koh_house_1";
        // The first captured house uses the PS1 coordinate frame, whose X axis
        // is opposite to the town/interior frame used by the player controller.
        // Keep the correction here so mesh, residents and collision footprints
        // share one authoritative conversion.
        private const float KohHouseLevelOneMirrorAxisX = 16f;
        [Serializable] public sealed class Surface { public int[] triangles; }
        [Serializable] public sealed class Geometry
        { public Vector3[] vertices; public Vector2[] uv; public Surface[] surfaces; }
        [Serializable] public struct FurnitureSurface
        {
            public Rect footprint;
            public float top;
        }
        public string interiorId;
        public string displayName;
        public TextAsset geometry;
        public TextAsset heights;
        public Material[] materials;
        [SerializeField] private Texture2D[] surfaceTextures;
        [SerializeField] private Shader surfaceShader;
        public Vector3 entryPosition;
        public bool useArrivalPosition;
        public Vector3 arrivalPosition;
        public GameObject[] residentPrefabs;
        [Tooltip("Optional floor cell for each resident prefab, in the same order.")]
        public Vector2Int[] residentCells;
        [Tooltip("Optional world-space offsets from the floor of each authored resident cell.")]
        public Vector3[] residentOffsets;
        [Tooltip("Solid furniture footprints in interior X/Z coordinates.")]
        public Rect[] furnitureFootprints;
        [Tooltip("Jumpable furniture footprints and their top Y coordinates.")]
        public FurnitureSurface[] furnitureSurfaces;
        private Mesh _mesh;
        private Material[] _runtimeMaterials;

        internal bool UsesMirroredCoordinateFrame => interiorId == KohHouseLevelOneId;

        internal Vector3 MapLayoutPosition(Vector3 position)
        {
            if (UsesMirroredCoordinateFrame) position.x = 2f * KohHouseLevelOneMirrorAxisX - position.x;
            return position;
        }

        internal Vector2Int MapLayoutCell(Vector2Int cell)
        {
            if (UsesMirroredCoordinateFrame) cell.x = Mathf.RoundToInt(2f * KohHouseLevelOneMirrorAxisX) - cell.x;
            return cell;
        }

        internal Rect MapLayoutFootprint(Rect footprint)
        {
            if (!UsesMirroredCoordinateFrame) return footprint;
            return Rect.MinMaxRect(
                2f * KohHouseLevelOneMirrorAxisX - footprint.xMax, footprint.yMin,
                2f * KohHouseLevelOneMirrorAxisX - footprint.xMin, footprint.yMax);
        }

        internal IEnumerable<Rect> CollisionFootprints()
        {
            if (furnitureFootprints == null) yield break;
            foreach (var footprint in furnitureFootprints) yield return MapLayoutFootprint(footprint);
        }

        private static Shader FindCapturedFallbackShader(Shader preferred)
        {
            // Captured interior surfaces use opaque, per-surface texture crops.
            // An alpha-test shader can discard the whole surface when an import
            // or platform alpha channel is absent, leaving the room white/empty.
            if (preferred && preferred.isSupported) return preferred;

            var shader = Shader.Find("AzureDreams/TownInteriorUnlit");
            if (shader && shader.isSupported) return shader;

            shader = Shader.Find("Unlit/Texture");
            if (shader && shader.isSupported) return shader;

            shader = Shader.Find("AzureDreams/TownCaptured");
            return shader && shader.isSupported ? shader : null;
        }

        internal float FurnitureTop(Vector3 position, float radius)
        {
            position = MapLayoutPosition(position);
            float top = float.NegativeInfinity;
            if (furnitureSurfaces == null) return top;
            foreach (var surface in furnitureSurfaces)
            {
                var bounds = surface.footprint;
                if (position.x > bounds.xMin - radius && position.x < bounds.xMax + radius &&
                    position.z > bounds.yMin - radius && position.z < bounds.yMax + radius)
                    top = Mathf.Max(top, surface.top);
            }
            return top;
        }

        /// <summary>
        /// Applies the currently selected General Store wallpaper without replacing
        /// the captured PS1 texture pages.  The captured room materials are runtime
        /// instances, so the tint is local to this loaded interior and is rebuilt
        /// cleanly on the next visit.
        /// </summary>
        internal void ApplyHomeWallpaper(string wallpaperId)
        {
            if (_runtimeMaterials == null) return;
            Color tint = wallpaperId switch
            {
                "wallpaper_1" => new Color(1f, 0.93f, 0.74f, 1f),
                "wallpaper_2" => new Color(0.79f, 0.91f, 1f, 1f),
                "wallpaper_3" => new Color(0.98f, 0.79f, 0.88f, 1f),
                _ => Color.white
            };
            foreach (var material in _runtimeMaterials)
            {
                if (!material) continue;
                if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            }
        }

        private void OnEnable()
        {
            if (_mesh || !geometry) return;
            var data = JsonUtility.FromJson<Geometry>(geometry.text);
            if (UsesMirroredCoordinateFrame && data.vertices != null)
                for (int i = 0; i < data.vertices.Length; i++)
                    data.vertices[i] = MapLayoutPosition(data.vertices[i]);
            _mesh = new Mesh { name = displayName, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            _mesh.vertices = data.vertices; _mesh.uv = data.uv;
            _mesh.subMeshCount = data.surfaces.Length;
            for (int i = 0; i < data.surfaces.Length; i++) _mesh.SetTriangles(data.surfaces[i].triangles, i);
            _mesh.RecalculateBounds(); _mesh.RecalculateNormals();
            if (!TryGetComponent<MeshFilter>(out var filter)) filter = gameObject.AddComponent<MeshFilter>();
            if (!TryGetComponent<MeshRenderer>(out var renderer)) renderer = gameObject.AddComponent<MeshRenderer>();
            filter.sharedMesh = _mesh;
            var assigned = new Material[_mesh.subMeshCount];
            _runtimeMaterials = new Material[assigned.Length];
            for (int i = 0; i < assigned.Length; i++)
            {
                var source = materials != null && i < materials.Length ? materials[i] : null;
                // Use an opaque unlit shader for the captured mesh. These
                // surface textures are already individual crops; alpha testing
                // can make them disappear on platforms that import no alpha.
                var shader = FindCapturedFallbackShader(surfaceShader);
                if (!shader && source && source.shader && source.shader.isSupported) shader = source.shader;
                if (!shader && surfaceShader && surfaceShader.isSupported) shader = surfaceShader;
                if (!shader) continue;

                var useSourceMaterial = source && source.shader == shader && source.shader.isSupported;
                var material = useSourceMaterial ? new Material(source) : new Material(shader);
                material.name = $"{displayName} surface {i}";
                var texture = surfaceTextures != null && i < surfaceTextures.Length
                    ? surfaceTextures[i]
                    : null;
                if (!texture && source) texture = source.mainTexture as Texture2D;
                if (texture && material.HasProperty("_MainTex"))
                    material.SetTexture("_MainTex", texture);
                else if (texture && material.HasProperty("_BaseMap"))
                    material.SetTexture("_BaseMap", texture);

                assigned[i] = material;
                _runtimeMaterials[i] = material;
            }
            renderer.sharedMaterials = assigned;
            renderer.enabled = true;
        }
        private void Start()
        {
            // Opening an interior directly in the Editor also creates the playable controller.
            if (Application.isPlaying && !FindAnyObjectByType<TowerGameController>())
                SceneManager.LoadSceneAsync("ReimaginedDemo", LoadSceneMode.Additive);
        }
        private void OnDisable()
        {
            if (_runtimeMaterials != null)
                foreach (var material in _runtimeMaterials)
                    if (material)
                    {
                        if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
                    }
            _runtimeMaterials = null;
            if (!_mesh) return;
            if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
            _mesh = null;
        }
    }
}
