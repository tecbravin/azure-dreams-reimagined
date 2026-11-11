using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Continuous, normalized town movement with footprint collision.
    /// Used by the player and NPCs. Decoupled from game state so it can be
    /// unit-tested in isolation.
    /// </summary>
    public sealed class TownMovementController : MonoBehaviour
    {
        [SerializeField] private float _speed = 3.5f;
        [SerializeField] private float _collisionRadius = 0.45f;

        private readonly HashSet<Vector2Int> _walls = new HashSet<Vector2Int>();
        private readonly List<Rect> _buildings = new List<Rect>();

        public float Speed { get => _speed; set => _speed = Mathf.Max(0.1f, value); }

        public void SetWalls(IEnumerable<Vector2Int> walls)
        {
            _walls.Clear();
            if (walls != null) foreach (var w in walls) _walls.Add(w);
        }

        public void SetBuildings(IEnumerable<Rect> buildings)
        {
            _buildings.Clear();
            if (buildings != null) foreach (var b in buildings) _buildings.Add(b);
        }

        private bool IsNearWall(Vector3 pos)
        {
            float r = _collisionRadius + 0.5f;
            float r2 = r * r;
            foreach (var w in _walls)
            {
                float dx = pos.x - w.x;
                float dz = pos.z - w.y;
                if (dx * dx + dz * dz < r2) return true;
            }
            return false;
        }

        /// <summary>
        /// Move by a normalized direction over dt seconds, applying collision.
        /// Returns the new world position.
        /// </summary>
        public Vector3 Move(Vector3 position, Vector2 direction, float dt, int mapSize)
        {
            if (direction.sqrMagnitude < 0.0001f) return position;
            Vector2 n = direction.normalized;
            Vector3 delta = new Vector3(n.x, 0f, n.y) * _speed * dt;
            Vector3 candidate = position + delta;

            // Footprint collision against walls (radius-aware).
            if (IsNearWall(candidate))
            {
                // Try slide along X.
                Vector3 slideX = position + new Vector3(delta.x, 0f, 0f);
                if (!IsNearWall(slideX)) candidate = slideX;
                else
                {
                    // Try slide along Z.
                    Vector3 slideZ = position + new Vector3(0f, 0f, delta.z);
                    if (!IsNearWall(slideZ)) candidate = slideZ;
                    else candidate = position;
                }
            }

            // Building footprints.
            foreach (var b in _buildings)
            {
                if (b.Contains(new Vector2(candidate.x, candidate.z)))
                {
                    candidate = position; // block
                    break;
                }
            }

            // Map bounds.
            candidate.x = Mathf.Clamp(candidate.x, 0.5f, mapSize - 1.5f);
            candidate.z = Mathf.Clamp(candidate.z, 0.5f, mapSize - 1.5f);
            return candidate;
        }
    }
}
