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

        internal void FitFacade(Material material, Texture2D texture)
        {
            if (!_leaf || !_leaf.TryGetComponent<Renderer>(out var renderer)) return;
            transform.localScale = new Vector3(1.04f / 0.95f, 1.77f / 1.52f, 0.55f);
            renderer.sharedMaterial = material;
            var properties = new MaterialPropertyBlock();
            properties.SetTexture("_MainTex", texture);
            renderer.SetPropertyBlock(properties);
        }

        internal bool BlocksFacade(Vector3 point, float radius)
        {
            if (IsPassable) return false;
            var threshold = Threshold;
            return Mathf.Abs(point.x - threshold.x) < 0.52f + radius &&
                Mathf.Abs(point.z - threshold.z) < 0.022f + radius;
        }

        private void Awake()
        {
            if (!_leaf) return;
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
