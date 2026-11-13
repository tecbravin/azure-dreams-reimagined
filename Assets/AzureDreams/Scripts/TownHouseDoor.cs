using UnityEngine;

namespace AzureDreamsReimagined
{
    [DisallowMultipleComponent]
    public sealed class TownHouseDoor : MonoBehaviour
    {
        [SerializeField] private Transform _leaf;
        [SerializeField, Min(1f)] private float _degreesPerSecond = 337.5f;
        private Transform _hinge;
        private float _angle;
        public bool IsOpeningOrOpen { get; private set; }
        public bool IsPassable => IsOpeningOrOpen && _angle >= 75f;
        public Vector3 Threshold => _hinge ? transform.TransformPoint(_closedCenter) : transform.position;
        private Vector3 _closedCenter;

        internal static TownHouseDoor CreateFacadeLeaf(Transform parent, Vector3 position, string label,
            float width, float height, Material material, Texture2D texture)
        {
            var root = new GameObject(label);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.name = "Door leaf";
            leaf.transform.SetParent(root.transform, false);
            leaf.transform.localPosition = Vector3.up * (height * 0.5f);
            leaf.transform.localScale = new Vector3(width, height, 0.04f);
            leaf.GetComponent<Collider>().enabled = false;
            var door = root.AddComponent<TownHouseDoor>();
            door._leaf = leaf.transform;
            door._degreesPerSecond = 90f * 30f / 16f;
            door.FitFacade(material, texture);
            door.InitializeHinge();
            return door;
        }

        internal void FitFacade(Material material, Texture2D texture)
        {
            if (!_leaf || !_leaf.TryGetComponent<Renderer>(out var renderer)) return;
            renderer.sharedMaterial = material;
            var properties = new MaterialPropertyBlock();
            properties.SetTexture("_MainTex", texture);
            renderer.SetPropertyBlock(properties);
        }

        internal bool BlocksFacade(Vector3 point, float radius)
        {
            if (IsPassable) return false;
            var threshold = Threshold;
            return Mathf.Abs(point.x - threshold.x) < (_leaf ? _leaf.localScale.x * 0.5f : 0.52f) + radius &&
                Mathf.Abs(point.z - threshold.z) < 0.022f + radius;
        }

        private void Awake() => InitializeHinge();

        private void InitializeHinge()
        {
            if (!_leaf || _hinge) return;
            _closedCenter = transform.InverseTransformPoint(_leaf.position);
            var parent = _leaf.parent;
            _hinge = new GameObject("Door hinge").transform;
            _hinge.SetParent(parent, false);
            _hinge.localPosition = _leaf.localPosition - Vector3.right * (_leaf.localScale.x * 0.5f);
            _leaf.SetParent(_hinge, true);
        }

        public bool Toggle()
        {
            if (!_hinge) return false;
            IsOpeningOrOpen = !IsOpeningOrOpen;
            return true;
        }

        private void Update()
        {
            if (!_hinge) return;
            float angle = Mathf.MoveTowards(_angle, IsOpeningOrOpen ? 90f : 0f, _degreesPerSecond * Time.deltaTime);
            if (angle == _angle) return;
            _angle = angle;
            _hinge.localRotation = Quaternion.Euler(0f, -_angle, 0f);
        }
    }
}
