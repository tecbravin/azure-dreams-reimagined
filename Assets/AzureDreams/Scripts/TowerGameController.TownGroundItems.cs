using System.Collections.Generic;
using UnityEngine;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const float TownItemBlockHeight = 0.45f;
        private const float TownItemBlockWidth = 0.6f;
        private readonly Dictionary<Vector2Int, List<(DungeonItemEntry item, GameObject view)>> _townItemStacks = new();

        private GameObject MakeTownItemBlock(Vector2Int cell, GameObject visual)
        {
            var root = new GameObject("Town item block");
            root.transform.SetParent(transform, false);
            var ground = CellPosition(cell);
            ground.y = TownTerrainHeight(ground);
            int lowerCount = 0;
            if (_townDroppedItems.TryGetValue(cell, out var previous))
            {
                if (!_townItemStacks.TryGetValue(cell, out var stack))
                    _townItemStacks[cell] = stack = new List<(DungeonItemEntry, GameObject)>();
                stack.Add(previous);
                lowerCount = stack.Count;
            }
            root.transform.position = ground + Vector3.up * (lowerCount * TownItemBlockHeight);
            var box = root.AddComponent<BoxCollider>();
            box.center = Vector3.up * (TownItemBlockHeight * 0.5f);
            box.size = new Vector3(TownItemBlockWidth, TownItemBlockHeight, TownItemBlockWidth);
            visual.transform.SetParent(root.transform, true);
            visual.transform.position = root.transform.position + Vector3.up * WorldItemHeight;
            _generated.Add(root);
            return root;
        }

        private bool RemoveTownGroundItem(Vector2Int cell, out (DungeonItemEntry item, GameObject view) drop)
        {
            if (!_townDroppedItems.Remove(cell, out drop)) return false;
            if (_townItemStacks.TryGetValue(cell, out var stack) && stack.Count > 0)
            {
                _townDroppedItems[cell] = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                if (stack.Count == 0) _townItemStacks.Remove(cell);
            }
            return true;
        }

        private float TownItemTop(Vector3 point, float radius, Transform ignored)
        {
            float top = float.NegativeInfinity;
            if (_area != Area.Town) return top;
            foreach (var drop in _townDroppedItems.Values)
            {
                if (!drop.view || drop.view.transform == ignored) continue;
                var center = drop.view.transform.position;
                float dx = Mathf.Max(0f, Mathf.Abs(point.x - center.x) - TownItemBlockWidth * 0.5f);
                float dz = Mathf.Max(0f, Mathf.Abs(point.z - center.z) - TownItemBlockWidth * 0.5f);
                if (dx * dx + dz * dz <= radius * radius)
                    top = Mathf.Max(top, center.y + TownItemBlockHeight);
            }
            return top;
        }

        private bool TryTownGroundItemCell(out Vector2Int cell)
        {
            // Prefer the object Koh is standing on, then the square in front.
            cell = _playerCell;
            if (_townDroppedItems.ContainsKey(cell)) return true;
            var facing = Mathf.Abs(_townFacing.x) > Mathf.Abs(_townFacing.y)
                ? new Vector2Int(_townFacing.x >= 0f ? 1 : -1, 0)
                : new Vector2Int(0, _townFacing.y >= 0f ? 1 : -1);
            cell += facing;
            if (!_townDroppedItems.TryGetValue(cell, out var drop) || !drop.view) return false;
            var point = drop.view.transform.position;
            var feet = _player.transform.position.y - TownActorFootOffset;
            return Mathf.Abs(point.y - feet) <= TownItemBlockHeight + 0.15f;
        }

        private bool TryLiftTownInventoryItem()
        {
            if (_heldTownPot || _carriedItem != null || !TryTownGroundItemCell(out var cell)) return false;
            if (!RemoveTownGroundItem(cell, out var drop)) return false;
            _carriedItem = OffBody(drop.item);
            _carriedView = drop.view;
            var collider = _carriedView.GetComponent<BoxCollider>();
            if (collider) collider.enabled = false;
            PresentTownInventoryItem();
            return true;
        }
    }
}
