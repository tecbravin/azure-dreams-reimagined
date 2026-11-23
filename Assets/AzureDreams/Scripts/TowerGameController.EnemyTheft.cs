using System.Collections.Generic;
using AzureDreams.Core;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private readonly Dictionary<ActorRecord, DungeonItemEntry> _enemyStolenItems = new();
        private readonly List<ActorRecord> _pendingEnemyThefts = new();

        private static bool CanEnemyStealItem(DungeonItemEntry item) => item != null &&
            item.Kind != 0 && item.Kind != ItemSystem.Kind.Familiar &&
            (!Inventory.IsGear(item) || (item.Flags & Inventory.WornMask) == 0);

        private bool TryQueuePicketTheft(CreatureState creature, Vector2Int kohCell, bool canAct)
        {
            // Retail select_creature_action_and_target: empty-handed, next to Koh,
            // roll_actor_flag_once(..., 2), then special action 0x8009.
            if (!canAct || creature.Actor.Species != (byte)MonsterSpecies.PICKET ||
                creature.CarriesWaterMedal || _enemyStolenItems.ContainsKey(creature.Actor) ||
                SpeciesDistance(creature.Cell, kohCell) != 1 || IsSacredTile(kohCell) ||
                !CanTowerStep(creature.Cell, kohCell - creature.Cell) ||
                !RollSpeciesAiOnce(creature.Actor, 2)) return false;
            _pendingEnemyThefts.Add(creature.Actor);
            QueueMonsterSpriteAction(creature.View, kohCell - creature.Cell);
            SpendMonsterMp(creature.Actor, 256);
            return true;
        }

        private bool TryStealPlayerItem(ActorRecord thief)
        {
            if (thief == null || _enemyStolenItems.ContainsKey(thief)) return false;
            var items = _runInventory?.DungeonItems;
            if (items == null || items.Count == 0) return false;
            // A protected randomly selected slot makes the attempt fail, as in move_eligible_item.
            int index = RollSpawnRange(items.Count);
            var item = items[index];
            if (!CanEnemyStealItem(item) || !_runInventory.ConsumeDungeonItem(index, item.Kind)) return false;
            _enemyStolenItems.Add(thief, item);
            PlayRetailSound(RetailSfx.Pickup);
            AddSpeciesMessage(T($"{TowerCombatName(thief)} stole {InventoryItemName(item)}!",
                $"{TowerCombatName(thief)} roubou {InventoryItemName(item)}!"));
            return true;
        }

        private void ResolveEnemyThefts()
        {
            foreach (var thief in _pendingEnemyThefts)
            {
                int index = _creatures.FindIndex(creature => creature.Actor == thief);
                if (index < 0) continue;
                var cell = _creatures[index].Cell;
                if (SpeciesDistance(cell, _playerCell) != 1 || !CanTowerStep(cell, _playerCell - cell)) continue;
                if (!TryStealPlayerItem(thief))
                    AddSpeciesMessage(T($"{TowerCombatName(thief)} could not steal an item.",
                        $"{TowerCombatName(thief)} não conseguiu roubar um item."));
            }
            _pendingEnemyThefts.Clear();
        }

        private Vector2Int StolenItemEscapeStep(Vector2Int from, Vector2Int kohCell, Vector2Int? reserved)
        {
            var best = Vector2Int.zero;
            int bestDistance = -1;
            // All eight normal movement directions; no teleport, wall or height bypass.
            foreach (var step in FormationDirections)
            {
                var next = from + step;
                if (!CanTowerStep(from, step) || next == kohCell || next == reserved ||
                    next == _stairCell || IsSacredTile(next) || CreatureAt(next) >= 0 ||
                    (_familiar && next == _familiarCell) ||
                    _thrownEggs.Exists(ally => ally.View && ally.Actor.Hp > 0 && ally.Cell == next) ||
                    _turnIntents.Exists(intent => intent.Actor != _playerActor &&
                        intent.Action == AzureDreams.Core.MonsterAI.ActionType.Move &&
                        new Vector2Int(intent.Actor.TileX + intent.DeltaX, intent.Actor.TileY + intent.DeltaY) == next)) continue;
                int distance = (next - kohCell).sqrMagnitude;
                if (distance <= bestDistance) continue;
                best = step;
                bestDistance = distance;
            }
            return best;
        }

        private void DropEnemyStolenItem(ActorRecord actor, Vector2Int cell)
        {
            if (_enemyStolenItems.TryGetValue(actor, out var item) && DropTowerReward(cell, item))
                _enemyStolenItems.Remove(actor);
        }
    }
}
