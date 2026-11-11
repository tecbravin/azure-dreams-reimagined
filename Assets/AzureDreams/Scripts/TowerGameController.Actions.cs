using AzureDreams.Core;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Individual tower commands. The shared turn clock remains in Movement;
    /// floor/spawn/run ownership belongs to the manager layer.
    /// Front-tile attacks (including empty swings) follow begin_front_tile_attack.c.
    /// Facing/front-tile selection supports eight directions. Movement is still
    /// cardinal; configured demo damage and scheduling are not retail behavior.
    /// Original terrain-height and party-target checks remain separate obligations.
    /// </summary>
    public sealed partial class TowerGameController
    {
        private Vector2Int _towerFacing = Vector2Int.up;
        public Vector2Int TowerFacing => _towerFacing;
        private bool KohSkipsAction => ItemImmobilized(_playerActor) || SlowBlocksAction(_playerActor);
        private bool TryPassStatusBlockedTurn()
        {
            if (_area != Area.Tower || !KohSkipsAction || _inventoryOpen || _inventoryMenu != InventoryMenu.Closed)
                return false;

            _towerInteractPending = false;
            ResetTowerPushHold();
            return TryTowerAction(Vector2Int.zero);
        }

        private bool KohIsAsleep => Combat.HasStatus(_playerActor, StatusId.SLEEP);
        private bool KohIsParalyzed => Combat.HasStatus(_playerActor, StatusId.PARALYSIS);
        private bool KohIsConfused => Combat.HasStatus(_playerActor, StatusId.CONFUSION) && !HasHolySwordProtection(_playerActor);

        // begin_front_tile_attack indexes eight dir_step entries. Movement
        // remains cardinal until the shared resolver and side probes are ported.
        private static bool IsTowerAttackDirection(Vector2Int direction) =>
            direction.x >= -1 && direction.x <= 1 && direction.y >= -1 && direction.y <= 1 &&
            direction != Vector2Int.zero;

        public bool TryTowerFace(Vector2Int direction)
        {
            if (_area != Area.Tower || _turnBusy || LevelUpCelebrationBlocksTurn || !_player || ItemImmobilized(_playerActor) ||
                !IsTowerAttackDirection(direction))
                return false;
            _towerFacing = direction;
            if (_kohSprite) _kohSprite.SetFacing(direction);
            // PSX billboard orientation is selected by its animation data;
            // rotating its root would incorrectly rotate the camera-facing quad.
            if (!_playerAnimation)
                _player.transform.rotation = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.y));
            PresentCarry(0f);
            return true;
        }

        private ItemDefinition _pendingUseItem;
        private bool _kohAttacking;

        public bool TryTowerUseItem(ItemDefinition item)
        {
            if (_area != Area.Tower || _turnBusy || !_player || !item ||
                item.Type != ItemDefinition.ItemType.Heal || _runInventory == null ||
                !_runInventory.HasItem(item, 1)) return false;
            if (KohSkipsAction) return TryTowerAction(Vector2Int.zero);
            if (!TryTowerAction(Vector2Int.zero)) return false;
            _pendingUseItem = item;
            SpawnFx(_fxMagic, CellPosition(_playerCell));
            return true;
        }

        public bool TryTowerAttack(Vector2Int direction)
        {
            if (_area != Area.Tower || _turnBusy || !_player ||
                !IsTowerAttackDirection(direction))
                return false;

            if (KohSkipsAction || IsFrog(_playerActor)) return TryTowerAction(Vector2Int.zero);
            if (KohIsConfused) direction = RollTowerDirection(allowDiagonals: true);
            if (_carriedView) return ReleaseCarriedObject(direction, true);

            // Select from the same pre-turn snapshot as the other actors. A
            // creature approaching during this turn is NOT an original target.
            // begin_front_tile_attack: target cell = tile + dir_step[direction].
            // Resolve through the retail DirStep table (decomp include/shared/dir_step.h)
            // instead of ad-hoc Vector2Int addition, so the Unity path matches the
            // original eight-way indexing (direction 0..7).
            int dirIndex = AzureDreams.Core.DirStep.IndexOf((short)direction.x, (short)direction.y);
            Vector2Int step = direction;
            if (dirIndex >= 0)
            {
                var (sx, sy) = AzureDreams.Core.DirStep.Step(dirIndex);
                step = new Vector2Int(sx, sy);
            }
            Vector2Int targetCell = _playerCell + step;
            int targetCreatureIndex = -1;
            for (int i = 0; i < _creatures.Count; i++)
                if (_creatures[i].Cell == targetCell) { targetCreatureIndex = i; break; }
            TryTowerFace(direction);
            if (!TryTowerAction(Vector2Int.zero)) return false;
            if (_kohMixingThisTurn) return true;
            bool armed = CarriedInventory?.EquippedItem(PlayerEquipped, false) != null;
            PlayRetailSound(armed ? RetailSfx.WeaponSwing : RetailSfx.Punch);
            DiscoverTrapWithAttack(targetCell);
            if (targetCreatureIndex >= 0)
            {
                // The familiar may already have committed damage to another enemy.
                // Keep this hit attached to the explicitly selected snapshot target.
                QueuePlayerWeaponHit(targetCreatureIndex, targetCell);
            }
            _kohAttacking = true;
            if (_kohSprite) _kohSprite.PresentAttack(direction, 0f);
            return true;
        }
    }
}
