using UnityEngine;

namespace AzureDreamsReimagined
{
    internal static class TowerStatusColors
    {
        internal static readonly Color Paralysis = new Color(0.26f, 0.62f, 1f, 1f);
    }

    [DefaultExecutionOrder(110)]
    internal sealed class MonsterExhaustedView : MonoBehaviour
    {
        private SpriteRenderer _standing;
        private SpriteRenderer _sleeping;
        private bool _sourceHidden;
        private float _collapsedAt;
        private bool _sourceWasHidden;
        private bool _exhausted;
        private bool _asleep, _paralyzed;
        private Color? _standingColor;
        private MeshRenderer _mesh;
        private MaterialPropertyBlock _meshColorBeforeParalysis;

        public void Present(bool sleeping, bool exhausted, bool paralyzed)
        {
            _exhausted = exhausted;
            _paralyzed = paralyzed;
            if (sleeping && !_asleep) _collapsedAt = Time.unscaledTime;
            _asleep = sleeping;
            enabled = sleeping || paralyzed;
        }

        private void LateUpdate()
        {
            if (!_standing)
            {
                _standing = GetComponentInChildren<SpriteRenderer>();
                if (!_standing)
                {
                    PresentMeshParalysis();
                    return;
                }
            }
            if (!_standing.sprite) return;
            if (!_asleep)
            {
                RestoreStandingPose();
                _standingColor ??= _standing.color;
                _standing.color = _standingColor.Value * TowerStatusColors.Paralysis;
                return;
            }
            RestoreStandingColor();
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
            _sleeping.color = _paralyzed ? original * TowerStatusColors.Paralysis : _exhausted
                ? Color.Lerp(original, new Color(0.28f, 0.55f, 1f, original.a),
                    0.48f + 0.45f * Mathf.Sin(elapsed * 12f))
                : original;
        }

        private void PresentMeshParalysis()
        {
            if (!_paralyzed)
            {
                RestoreMeshColor();
                return;
            }
            if (_meshColorBeforeParalysis != null) return;
            if (!_mesh) _mesh = GetComponentInChildren<MeshRenderer>();
            if (!_mesh || !_mesh.sharedMaterial) return;
            var material = _mesh.sharedMaterial;
            string colorProperty = material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            if (!material.HasProperty(colorProperty)) return;
            _meshColorBeforeParalysis = new MaterialPropertyBlock();
            _mesh.GetPropertyBlock(_meshColorBeforeParalysis);
            var tinted = new MaterialPropertyBlock();
            _mesh.GetPropertyBlock(tinted);
            var color = tinted.HasColor(colorProperty) ? tinted.GetColor(colorProperty) : material.GetColor(colorProperty);
            var blue = TowerStatusColors.Paralysis;
            blue.a = color.a;
            tinted.SetColor(colorProperty, blue);
            _mesh.SetPropertyBlock(tinted);
        }

        private void RestoreMeshColor()
        {
            if (_mesh && _meshColorBeforeParalysis != null) _mesh.SetPropertyBlock(_meshColorBeforeParalysis);
            _meshColorBeforeParalysis = null;
        }

        private void RestoreStandingColor()
        {
            if (_standing && _standingColor is { } color) _standing.color = color;
            _standingColor = null;
        }

        private void RestoreStandingPose()
        {
            if (_standing && _sourceHidden) _standing.forceRenderingOff = _sourceWasHidden;
            _sourceHidden = false;
            if (_sleeping) _sleeping.enabled = false;
        }

        private void OnDisable()
        {
            RestoreStandingPose();
            RestoreStandingColor();
            RestoreMeshColor();
        }
    }
}
