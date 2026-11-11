using UnityEngine;

namespace AzureDreamsReimagined
{
    [DefaultExecutionOrder(110)]
    internal sealed class MonsterExhaustedView : MonoBehaviour
    {
        private SpriteRenderer _standing;
        private SpriteRenderer _sleeping;
        private bool _sourceHidden;
        private float _collapsedAt;
        private bool _sourceWasHidden;
        private bool _exhausted;

        public void Present(bool sleeping, bool exhausted)
        {
            _exhausted = exhausted;
            if (enabled == sleeping && (!sleeping || _sleeping)) return;
            enabled = sleeping;
            if (!sleeping) return;
            _collapsedAt = Time.unscaledTime;
        }

        private void LateUpdate()
        {
            if (!_standing)
            {
                _standing = GetComponentInChildren<SpriteRenderer>();
                if (!_standing || !_standing.sprite) return;
            }
            if (!_sleeping)
            {
                var child = new GameObject("Sleeping pose");
                child.transform.SetParent(transform, false);
                _sleeping = child.AddComponent<SpriteRenderer>();
                _sleeping.sharedMaterial = _standing.sharedMaterial;
                _sleeping.sortingLayerID = _standing.sortingLayerID;
                _sleeping.sortingOrder = _standing.sortingOrder;
            }
            if (!_sourceHidden)
            {
                _sourceWasHidden = _standing.forceRenderingOff;
                _standing.forceRenderingOff = true;
                _sourceHidden = true;
            }
            _sleeping.enabled = !_sourceWasHidden && _standing.enabled;
            _sleeping.sprite = _standing.sprite;
            _sleeping.flipX = _standing.flipX;
            float elapsed = Time.unscaledTime - _collapsedAt;
            float fall = Mathf.SmoothStep(0f, 1f, elapsed / 0.16f);
            var camera = Camera.main;
            var orientation = camera ? camera.transform.rotation : _standing.transform.rotation;
            _sleeping.transform.SetPositionAndRotation(
                Vector3.Lerp(_standing.transform.position, transform.position + Vector3.down * 0.37f, fall),
                orientation * Quaternion.Euler(0f, 0f, 78f * fall));
            var parentScale = transform.lossyScale;
            var sourceScale = _standing.transform.lossyScale;
            float breath = 1f + 0.035f * Mathf.Sin(elapsed * 4f);
            _sleeping.transform.localScale = new Vector3(
                sourceScale.x / Mathf.Max(0.001f, parentScale.x),
                sourceScale.y / Mathf.Max(0.001f, parentScale.y) * breath,
                sourceScale.z / Mathf.Max(0.001f, parentScale.z));
            var original = _standing.color;
            _sleeping.color = _exhausted
                ? Color.Lerp(original, new Color(0.28f, 0.55f, 1f, original.a),
                    0.48f + 0.45f * Mathf.Sin(elapsed * 12f))
                : original;
        }

        private void OnDisable()
        {
            if (_standing && _sourceHidden) _standing.forceRenderingOff = _sourceWasHidden;
            _sourceHidden = false;
            if (_sleeping) _sleeping.enabled = false;
        }
    }
}
