using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed class TowerEventNpc : MonoBehaviour
    {
        private Vector3 _anchor;
        private Vector3 _approach;
        private Vector3 _depart;

        private MonsterTurnSpriteView _sprite;

        private void Awake() => _sprite = GetComponentInChildren<MonsterTurnSpriteView>();

        public bool HasSpoken { get; private set; }

        internal void MarkSpoken() => HasSpoken = true;

        public void SetStage(Vector3 playerPosition)
        {
            _anchor = transform.position;
            Vector3 direction = playerPosition - _anchor;
            direction.y = 0f;
            direction.Normalize();
            _approach = _anchor + direction * 0.25f;
            _depart = _anchor - direction * 0.40f;
            Face(playerPosition);
        }

        public void Approach(float progress) => Move(_anchor, _approach, progress);
        public void Depart(float progress) => Move(_approach, _depart, progress);

        public void Face(Vector3 point)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f) return;
            transform.rotation = Quaternion.LookRotation(direction);
            if (_sprite) _sprite.Face(new Vector2Int(Mathf.RoundToInt(direction.normalized.x), Mathf.RoundToInt(direction.normalized.z)));
        }

        private void Move(Vector3 from, Vector3 to, float progress)
        {
            float smooth = Mathf.SmoothStep(0f, 1f, progress);
            transform.position = Vector3.Lerp(from, to, smooth) + Vector3.up * (0.10f * Mathf.Sin(progress * Mathf.PI));
        }
    }
}
