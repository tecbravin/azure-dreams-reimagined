using UnityEngine;

namespace AzureDreamsReimagined.Rendering
{
    public sealed class CameraFacingSprite : MonoBehaviour
    {
        private Vector3 _monsterDirection;
        private SpriteRenderer _sprite;
        private MeshRenderer _card;

        internal void FaceMonster(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f) return;
            _monsterDirection = direction;
            if (!_sprite) _sprite = GetComponent<SpriteRenderer>();
            if (!_card) _card = GetComponent<MeshRenderer>();
        }

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (!camera) return;
            transform.rotation = camera.transform.rotation;
            if (_monsterDirection == Vector3.zero) return;
            float side = Vector3.Dot(camera.transform.right, _monsterDirection.normalized);
            if (Mathf.Abs(side) < 0.05f) return;
            bool flip = side > 0f;
            if (_card && _card.enabled)
            {
                var scale = transform.localScale;
                scale.x = Mathf.Abs(scale.x) * (flip ? -1f : 1f);
                transform.localScale = scale;
            }
            else if (_sprite && _sprite.sprite) _sprite.flipX = flip;
        }
    }
}
