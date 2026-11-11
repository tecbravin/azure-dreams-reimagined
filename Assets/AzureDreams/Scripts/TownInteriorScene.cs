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

        internal float FurnitureTop(Vector3 position, float radius)
        {
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

        private void OnEnable()
        {
            if (_mesh || !geometry) return;
            var data = JsonUtility.FromJson<Geometry>(geometry.text);
            _mesh = new Mesh { name = displayName, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            _mesh.vertices = data.vertices; _mesh.uv = data.uv;
            _mesh.subMeshCount = data.surfaces.Length;
            for (int i = 0; i < data.surfaces.Length; i++) _mesh.SetTriangles(data.surfaces[i].triangles, i);
            _mesh.RecalculateBounds(); _mesh.RecalculateNormals();
            if (!TryGetComponent<MeshFilter>(out var filter)) filter = gameObject.AddComponent<MeshFilter>();
            if (!TryGetComponent<MeshRenderer>(out var renderer)) renderer = gameObject.AddComponent<MeshRenderer>();
            filter.sharedMesh = _mesh; renderer.sharedMaterials = materials;
        }
        private void Start()
        {
            // Opening an interior directly in the Editor also creates the playable controller.
            if (Application.isPlaying && !FindAnyObjectByType<TowerGameController>())
                SceneManager.LoadSceneAsync("ReimaginedDemo", LoadSceneMode.Additive);
        }
        private void OnDisable()
        {
            if (!_mesh) return;
            if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
            _mesh = null;
        }
    }
}
