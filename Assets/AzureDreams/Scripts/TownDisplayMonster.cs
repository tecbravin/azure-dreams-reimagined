using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>Local presentation for monsters kept in town; never a tower combat actor.</summary>
    [DisallowMultipleComponent]
    public sealed class TownDisplayMonster : MonoBehaviour
    {
        private ActorLocomotionView _locomotion;
        private Vector3[] _points;
        private int _target;
        private float _wait, _cycle;
        internal string PlacementId { get; private set; }

        internal void Configure(string placementId, Vector3 center, Vector2 halfExtents, float delay)
        {
            PlacementId = placementId;
            _locomotion = GetComponent<ActorLocomotionView>();
            _points = new[]
            {
                center + new Vector3(-halfExtents.x, 0, -halfExtents.y),
                center + new Vector3(halfExtents.x, 0, -halfExtents.y),
                center + new Vector3(halfExtents.x, 0, halfExtents.y),
                center + new Vector3(-halfExtents.x, 0, halfExtents.y)
            };
            transform.position = center;
            _target = delay > 0.5f ? 2 : 0;
            _wait = delay;
            _locomotion?.Present(Vector3.back, 0f, false);
        }

        private void Update()
        {
            if (_points == null || !_locomotion) return;
            if (_wait > 0f)
            {
                _wait -= Time.deltaTime;
                _locomotion.Present(Vector3.zero, _cycle, false);
                return;
            }
            Vector3 before = transform.position;
            transform.position = Vector3.MoveTowards(before, _points[_target], 0.35f * Time.deltaTime);
            Vector3 movement = transform.position - before;
            _cycle += movement.magnitude;
            _locomotion.Present(movement, _cycle, movement.sqrMagnitude > 0.00000001f);
            if ((transform.position - _points[_target]).sqrMagnitude < 0.000001f)
            {
                _target = (_target + 1) % _points.Length;
                _wait = 0.8f;
            }
        }

        private void OnDisable()
        {
            if (_locomotion) _locomotion.Present(Vector3.zero, _cycle, false);
        }
    }
}
