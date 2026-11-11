using System;
using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// A route between two grid cells, expressed as an ordered list of
    /// waypoints. Used by NPCs, the player and camera tracking.
    /// </summary>
    [Serializable]
    public class TownRoute
    {
        public List<Vector2Int> Waypoints = new List<Vector2Int>();

        public int Length => Waypoints != null ? Waypoints.Count : 0;

        public bool IsValid => Waypoints != null && Waypoints.Count >= 2;

        public Vector2Int Start => Waypoints.Count > 0 ? Waypoints[0] : Vector2Int.zero;
        public Vector2Int End => Waypoints.Count > 0 ? Waypoints[Waypoints.Count - 1] : Vector2Int.zero;
    }

    /// <summary>
    /// Grid pathfinder (A*) for the town map. Avoids walls and blocked cells.
    /// </summary>
    public static class TownPathfinder
    {
        public static TownRoute FindPath(HashSet<Vector2Int> walls, Vector2Int from, Vector2Int to, int mapSize)
        {
            var route = new TownRoute();
            if (from == to) { route.Waypoints.Add(from); return route; }
            if (walls.Contains(to)) return route; // unreachable

            // A* on grid.
            var open = new List<Vector2Int> { from };
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var gScore = new Dictionary<Vector2Int, int> { [from] = 0 };
            var fScore = new Dictionary<Vector2Int, int> { [from] = Manhattan(from, to) };

            while (open.Count > 0)
            {
                // pick lowest fScore
                Vector2Int current = open[0];
                int bestF = fScore[current];
                for (int i = 1; i < open.Count; i++)
                {
                    if (fScore[open[i]] < bestF) { bestF = fScore[open[i]]; current = open[i]; }
                }
                if (current == to) break;
                open.Remove(current);

                foreach (var n in Neighbors(current, mapSize))
                {
                    if (walls.Contains(n)) continue;
                    int tentative = gScore[current] + 1;
                    if (!gScore.TryGetValue(n, out int g) || tentative < g)
                    {
                        cameFrom[n] = current;
                        gScore[n] = tentative;
                        fScore[n] = tentative + Manhattan(n, to);
                        if (!open.Contains(n)) open.Add(n);
                    }
                }
            }

            if (!cameFrom.ContainsKey(to) && from != to) return route; // no path

            // Reconstruct.
            var path = new List<Vector2Int>();
            Vector2Int cur = to;
            path.Add(cur);
            while (cur != from)
            {
                if (!cameFrom.TryGetValue(cur, out var prev)) break;
                cur = prev;
                path.Add(cur);
            }
            path.Reverse();
            route.Waypoints = path;
            return route;
        }

        private static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        private static IEnumerable<Vector2Int> Neighbors(Vector2Int c, int mapSize)
        {
            if (c.x > 0) yield return new Vector2Int(c.x - 1, c.y);
            if (c.x < mapSize - 1) yield return new Vector2Int(c.x + 1, c.y);
            if (c.y > 0) yield return new Vector2Int(c.x, c.y - 1);
            if (c.y < mapSize - 1) yield return new Vector2Int(c.x, c.y + 1);
        }
    }
}
