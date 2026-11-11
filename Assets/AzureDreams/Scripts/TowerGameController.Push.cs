using UnityEngine;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const float PushHoldSeconds = 0.18f;
        private const float TownPushSpeed = 1.5f;
        private const float HeldPushPose = 0.5f;
        private ActorRecord _towerPushTarget;
        private Vector2Int _towerPushDirection;
        private float _towerPushHold;
        private bool _towerPushing;
        private ThrownEggAlly _pushedEggAlly;
        private Vector3 _pushedEggFrom;
        private Transform _townPushTarget;
        private Vector2 _townPushDirection;
        private float _townPushHold;

        private void TickTowerPush(Vector2Int step, float deltaTime, bool stayInPlace)
        {
            Vector2Int targetCell = _playerCell + step;
            int creature = CreatureAt(targetCell);
            var target = creature >= 0 ? _creatures[creature].Actor :
                _familiar && targetCell == _familiarCell ? _familiarActor :
                _thrownEggs.Find(ally => ally.Cell == targetCell && ally.Turns > 0)?.Actor;
            if (_carriedView || ItemImmobilized(_playerActor) || KohIsConfused || target == null || IsSubmerged(target))
            {
                ResetTowerPushHold();
                return;
            }

            if (_towerPushTarget != target || _towerPushDirection != step)
            {
                _towerPushHold = 0f;
                _towerPushTarget = target;
                _towerPushDirection = step;
                return;
            }
            _towerPushHold += deltaTime;
            if (_towerPushHold < PushHoldSeconds) return;
            _towerPushHold = 0f;
            if (SlowBlocksAction(_playerActor))
            {
                TryTowerAction(Vector2Int.zero);
                return;
            }

            var destination = targetCell + step;
            bool canPush = UnitProfile(target.Species)?.pushable == true &&
                HasTowerPassage(_playerCell, step) && (stayInPlace || CanClimbTowerHeight(_playerCell, targetCell)) &&
                CanTowerStep(targetCell, step) &&
                destination != _stairCell && (creature < 0 || !IsSacredTile(destination)) &&
                CreatureAt(destination) < 0 && (!_familiar || destination != _familiarCell) &&
                !_thrownEggs.Exists(ally => ally.Cell == destination);
            var kohStep = canPush && !stayInPlace ? step : Vector2Int.zero;
            if (CommitTowerTurn(kohStep, canPush ? target : null, step))
            {
                _towerPushing = true;
                if (_kohSprite) _kohSprite.PresentPush(step, HeldPushPose);
                bool moved = canPush && target.TileX == destination.x && target.TileY == destination.y;
                _message = moved
                    ? T("You push the unit back.", "Você empurra a unidade para trás.")
                    : T("The unit won't budge.", "A unidade não sai do lugar.");
            }
        }

        private void ResetTowerPushHold()
        {
            _towerPushTarget = null;
            _towerPushHold = 0f;
        }

        private Vector3 ResolveTownPushOverlap(Vector3 origin, Vector2 direction, float feetHeight)
        {
            if (direction.sqrMagnitude <= 0.001f || TownPositionOpen(origin, feetHeight)) return origin;

            var backwards = -new Vector3(direction.x, 0f, direction.y).normalized;
            var sideways = new Vector3(-backwards.z, 0f, backwards.x);
            // Prefer moving Koh back out along the push axis. A nearby wall may
            // leave room only on the side, so also try the other short exits.
            for (int step = 1; step <= 20; step++)
            {
                float distance = step * 0.05f;
                Vector3 candidate = origin + backwards * distance;
                if (TownPositionOpen(candidate, feetHeight)) return candidate;
                candidate = origin + sideways * distance;
                if (TownPositionOpen(candidate, feetHeight)) return candidate;
                candidate = origin - sideways * distance;
                if (TownPositionOpen(candidate, feetHeight)) return candidate;
                candidate = origin - backwards * distance;
                if (TownPositionOpen(candidate, feetHeight)) return candidate;
            }
            return origin;
        }

        private bool PushInTown(Vector3 origin, Vector2 direction, float deltaTime)
        {
            if (_kohSprite) _kohSprite.PresentPush(Vector2.zero, 0f);
            Transform target = null;
            float targetRadius = 0f;
            float closest = float.PositiveInfinity;
            var heading = direction.normalized;

            void Consider(Transform candidate, float radius)
            {
                var offset = new Vector2(candidate.position.x - origin.x, candidate.position.z - origin.z);
                float distance = offset.magnitude;
                float reach = radius + TownActorRadius + 0.10f;
                if (distance >= closest || Mathf.Abs(offset.x) > reach || Mathf.Abs(offset.y) > reach ||
                    Vector2.Dot(offset.normalized, heading) < 0.55f) return;
                target = candidate;
                targetRadius = radius;
                closest = distance;
            }

            if (direction.sqrMagnitude > 0.001f && !_heldTownPot && !_carriedView)
            {
                foreach (var pot in _townPots)
                    if (pot && pot.IsSolid && origin.y - TownActorFootOffset < pot.ObstacleBounds.max.y - TownGroundTolerance)
                        Consider(pot.transform, pot.ObstacleBounds.extents.x);
            }

            if (!target || _townPushTarget != target || Vector2.Dot(_townPushDirection, heading) < 0.9f)
                _townPushHold = 0f;
            _townPushTarget = target;
            _townPushDirection = heading;
            if (!target) return false;
            _townPushHold = Mathf.Min(PushHoldSeconds, _townPushHold + deltaTime);
            if (_kohSprite) _kohSprite.PresentPush(heading, HeldPushPose * (_townPushHold / PushHoldSeconds));
            if (_townPushHold < PushHoldSeconds) return false;
            var destination = target.position + new Vector3(heading.x, 0f, heading.y) * (TownPushSpeed * deltaTime);
            if (!TownPositionOpen(destination, float.NegativeInfinity, target, targetRadius)) return false;
            destination.y += TownTerrainHeight(destination) - TownTerrainHeight(target.position);
            target.position = destination;
            return true;
        }

        private void ResetPush()
        {
            _towerInteractPending = false;
            ResetTowerPushHold();
            _towerPushing = false;
            _pushedEggAlly = null;
            _townPushTarget = null;
            _townPushHold = 0f;
            if (_kohSprite) _kohSprite.PresentPush(Vector2.zero, 0f);
        }
    }
}
