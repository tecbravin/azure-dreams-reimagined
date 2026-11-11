using UnityEngine;

namespace AzureDreamsReimagined
{
    [DisallowMultipleComponent]
    public sealed class TownGateDoor : MonoBehaviour
    {
        [SerializeField] private Transform _leafPivot;
        private Transform _left, _right;
        private Mesh _leftLeafMesh, _rightLeafMesh;

        public void Configure(Transform leafPivot)
        {
            _leafPivot = leafPivot;
        }

        internal void ConfigureDoubleDoor(float width, float height, Material material)
        {
            if (_left) return;
            if (_leafPivot) _leafPivot.gameObject.SetActive(false);
            _left = CreateLeaf(-1, width * 0.5f, height, material);
            _right = CreateLeaf(1, width * 0.5f, height, material);
        }

        private Transform CreateLeaf(int side, float width, float height, Material material)
        {
            var hinge = new GameObject(side < 0 ? "Left door hinge" : "Right door hinge").transform;
            hinge.SetParent(transform, false);
            hinge.localPosition = Vector3.right * (side * width);
            var panel = AddPanel(hinge, "Iron door leaf", new Vector3(-side * width * 0.5f, height * 0.5f, 0f),
                new Vector3(width, height, 0.07f), material);
            var filter = panel.GetComponent<MeshFilter>();
            var mesh = Instantiate(filter.sharedMesh);
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var uv = mesh.uv;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 vertex = vertices[i];
                // The source is a single leaf with its hinge strip at U=0.
                // Map from the physical hinge on both faces, independent of cube UV winding.
                float u = 0.5f - side * vertex.x;
                if (Mathf.Abs(normals[i].z) < 0.5f)
                    u = Mathf.Lerp(u, Mathf.Clamp01(u + 0.06f), vertex.z + 0.5f);
                uv[i] = new Vector2(u, vertex.y + 0.5f);
            }
            mesh.uv = uv;
            filter.sharedMesh = mesh;
            if (side < 0) _leftLeafMesh = mesh;
            else _rightLeafMesh = mesh;
            return hinge;
        }

        private void OnDestroy()
        {
            if (_leftLeafMesh) Destroy(_leftLeafMesh);
            if (_rightLeafMesh) Destroy(_rightLeafMesh);
        }

        internal static GameObject AddPanel(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = name;
            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = position;
            panel.transform.localScale = size;
            if (material) panel.GetComponent<Renderer>().sharedMaterial = material;
            var collider = panel.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            return panel;
        }

        internal void SetOpen(float amount)
        {
            if (!_left || !_right) return;
            float angle = Mathf.Clamp01(amount) * 90f;
            _left.localRotation = Quaternion.Euler(0f, -angle, 0f);
            _right.localRotation = Quaternion.Euler(0f, angle, 0f);
        }
    }
}
