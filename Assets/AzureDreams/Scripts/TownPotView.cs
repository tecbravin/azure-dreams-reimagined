using UnityEngine;

namespace AzureDreamsReimagined
{
    [DisallowMultipleComponent]
    public sealed class TownPotView : MonoBehaviour
    {
        [SerializeField] private Texture2D _sheet;
        [SerializeField, Range(0, 3)] private int _variant;
        private const float PixelsPerUnit = 40f / 1.4f;
        private const int Sides = 16;
        // Height and half-width in pixels, measured on the retail sprite.
        private static readonly Vector2[] BodyProfile =
        {
            new(0f, 5f), new(2f, 7f), new(6f, 8f), new(12f, 11f),
            new(18f, 13f), new(20f, 13f), new(24f, 10f), new(26f, 8f),
            new(28f, 9f), new(30f, 8f), new(32f, 4.5f)
        };
        private Mesh _mesh;
        private Material _material;
        private BoxCollider _obstacle;
        internal bool IsSolid => _obstacle && _obstacle.enabled;
        // Town pots stay upright. Read the live transform so lift, drop, and
        // throw do not depend on the timing of the next physics sync.
        internal Bounds ObstacleBounds => new(transform.TransformPoint(_obstacle.center),
            Vector3.Scale(_obstacle.size, transform.lossyScale));

        internal void SetSolid(bool solid)
        {
            if (_obstacle) _obstacle.enabled = solid;
        }

        public void SetVariant(int variant) => _variant = Mathf.Clamp(variant, 0, 3);

        private void Awake()
        {
            if (!_sheet) return;
            int[] left = { 9, 41, 73, 105 };
            var visual = new GameObject("Vase body");
            visual.transform.SetParent(transform, false);
            _mesh = BuildBody(left[_variant]);
            visual.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _material = new Material(Shader.Find("AzureDreams/TownCaptured")) { mainTexture = _sheet };
            visual.AddComponent<MeshRenderer>().sharedMaterial = _material;
            _obstacle = gameObject.AddComponent<BoxCollider>();
            float width = 26f / PixelsPerUnit;
            float height = 32f / PixelsPerUnit;
            _obstacle.center = Vector3.up * (height * 0.5f);
            _obstacle.size = new Vector3(width, height, width);
        }

        private Mesh BuildBody(int left)
        {
            var vertices = new Vector3[BodyProfile.Length * Sides + 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[BodyProfile.Length * Sides * 6];
            int cursor = 0;
            for (int ring = 0; ring < BodyProfile.Length; ring++)
            {
                var profile = BodyProfile[ring];
                for (int side = 0; side < Sides; side++)
                {
                    int index = ring * Sides + side;
                    float angle = side * (2f * Mathf.PI / Sides);
                    float x = Mathf.Cos(angle) * profile.y;
                    vertices[index] = new Vector3(x, profile.x, Mathf.Sin(angle) * profile.y) / PixelsPerUnit;
                    uv[index] = PixelUv(left + 15f + x * 0.78f, profile.x);
                    if (ring == 0) continue;
                    int next = ring * Sides + (side + 1) % Sides;
                    triangles[cursor++] = index - Sides;
                    triangles[cursor++] = next;
                    triangles[cursor++] = index;
                    triangles[cursor++] = index - Sides;
                    triangles[cursor++] = next - Sides;
                    triangles[cursor++] = next;
                }
            }

            int bottom = vertices.Length - 2;
            int top = vertices.Length - 1;
            vertices[top] = Vector3.up * (32f / PixelsPerUnit);
            uv[bottom] = PixelUv(left + 15f, 0f);
            uv[top] = PixelUv(left + 15f, 32f);
            int topRing = (BodyProfile.Length - 1) * Sides;
            for (int side = 0; side < Sides; side++)
            {
                int next = (side + 1) % Sides;
                triangles[cursor++] = bottom;
                triangles[cursor++] = side;
                triangles[cursor++] = next;
                triangles[cursor++] = top;
                triangles[cursor++] = topRing + next;
                triangles[cursor++] = topRing + side;
            }
            var mesh = new Mesh { name = "Vase body", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            return mesh;
        }

        // Normalize the original 150 x 125 sheet coordinates for upscaled imports.
        private static Vector2 PixelUv(float x, float height) =>
            new(x / 150f, 1f - (52f - height) / 125f);

        private void OnDestroy()
        {
            if (_mesh) Destroy(_mesh);
            if (_material) Destroy(_material);
        }
    }
}
