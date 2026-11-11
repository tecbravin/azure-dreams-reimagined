using AzureDreams.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        [SerializeField] private string _liftBinding = "<Keyboard>/l";
        private InputAction _liftAction;
        private GameObject _carriedView;
        private CreatureState _carriedMonster;
        private DungeonItemEntry _carriedItem;
        private bool _carriedSalvage;
        private bool _carriedFamiliar;
        private ActorRecord _landingMonster;
        private bool _landingFamiliar;
        private GameObject _releasedView;
        private Vector3 _releaseFrom, _releaseTo;
        private bool _releaseThrown;
        private GameObject _salvageMarker;

        // Species flags transcribed from JustDevinMonsterMechanics.json.
        private static bool IsLiftable(byte species) => (MonsterSpecies)species is
            MonsterSpecies.ARACHNE or MonsterSpecies.BARONG or MonsterSpecies.CLOWN or
            MonsterSpecies.DREAMIN or MonsterSpecies.GARUDA or MonsterSpecies.GRIFFON or
            MonsterSpecies.KEWNE or MonsterSpecies.KID or MonsterSpecies.PICKET or
            MonsterSpecies.PULUNPA or MonsterSpecies.STEALTH or MonsterSpecies.TROLL or
            MonsterSpecies.TYRANT or MonsterSpecies.U_BOAT or MonsterSpecies.UNICORN or
            MonsterSpecies.VIPER or MonsterSpecies.VOLCANO or MonsterSpecies.ZU;

        public bool TryTowerLift()
        {
            if (!CanUseLift()) return false;
            if (KohSkipsAction) return TryTowerAction(Vector2Int.zero);
            if (_carriedView) return ReleaseCarriedObject(_towerFacing, false);

            var front = _playerCell + _towerFacing;
            if (_npcCells.Contains(front)) return false;
            int index = CreatureAt(front);
            if (index < 0) index = CreatureAt(_playerCell);
            bool familiar = index < 0 && _familiar && (_familiarCell == front || _familiarCell == _playerCell);
            if (index >= 0 || familiar)
            {
                var actor = familiar ? _familiarActor : _creatures[index].Actor;
                if (IsSubmerged(actor)) return false;
                if (!IsLiftable(actor.Species))
                {
                    _message = T("That monster is too heavy to lift.", "Esse monstro é pesado demais para levantar.");
                    return false;
                }
                // Use the run RNG. Awake hostile monsters resist one attempt in four.
                if (!familiar && !MonsterExhausted(actor) && !Combat.HasStatus(actor, StatusId.SLEEP) && _towerRun.Rng.Next() % 4 == 0)
                {
                    _message = T("The monster resists your lift.", "O monstro resiste.");
                    return TryTowerAction(Vector2Int.zero);
                }
                if (familiar)
                {
                    _carriedFamiliar = true;
                    _carriedView = _familiar;
                    _carriedView.GetComponent<KewneSpriteView>()?.SetCarried(true);
                    _familiar = null;
                    // The carried view follows Koh, but only Koh occupies his tile.
                    SetActorCell(actor, new Vector2Int(-1, -1));
                    Animate(_familiarAnimation, false);
                }
                else
                {
                    _carriedMonster = _creatures[index];
                    _creatures.RemoveAt(index);
                    _carriedView = _carriedMonster.View;
                    Animate(_carriedMonster.Animation, false);
                }
            }
            else if (!TakeFloorObject(front) && !TakeFloorObject(_playerCell))
            {
                return false;
            }

            PresentCarry(0f);
            return TryTowerAction(Vector2Int.zero);
        }

        private bool CanUseLift() => _area == Area.Tower && !_turnBusy && !_elevatorAscending && !MonsterDenPlaying &&
            _player && _mapView != MapView.Full;

        private int CreatureAt(Vector2Int cell)
        {
            for (int i = 0; i < _creatures.Count; i++)
                if (_creatures[i].Cell == cell) return i;
            return -1;
        }

        private bool TakeFloorObject(Vector2Int cell)
        {
            if (_hasLoot && cell == _lootCell && _salvageMarker)
            {
                _hasLoot = false;
                _carriedSalvage = true;
                _carriedView = _salvageMarker;
                return true;
            }
            if (IsBlueCollar(_towerItemTraps?.GetItemAt(cell)))
            {
                PutGroundItemInBag(cell);
                return false;
            }
            if (!_itemMarkers.TryGetValue(cell, out var view) || !view) return false;
            _carriedItem = _towerItemTraps?.TakeItem(cell);
            if (_towerItemTraps != null && _carriedItem == null) return false;
            _itemMarkers.Remove(cell);
            _carriedView = view;
            return true;
        }

        private bool CanLandObject(Vector2Int cell) =>
            InsideMap(cell) && !_walls.Contains(cell) && !_npcCells.Contains(cell) &&
            cell != _stairCell && (!_familiar || cell != _familiarCell) &&
            (_carriedMonster.Actor == null || !IsSacredTile(cell)) &&
            CreatureAt(cell) < 0 && !_thrownEggs.Exists(ally => ally.View && ally.Turns > 0 && ally.Cell == cell) &&
            !_itemMarkers.ContainsKey(cell) && (!_hasLoot || cell != _lootCell);

        private bool HasThrowPassage(Vector2Int from, Vector2Int step)
        {
            if (!CanTowerStep(from, step)) return false;
            if (step.x == 0 || step.y == 0) return true;
            return CanTowerStep(from, new Vector2Int(step.x, 0)) &&
                CanTowerStep(from, new Vector2Int(0, step.y));
        }

        private Vector3 ThrowLandingPosition(Vector2Int cell, bool actor) =>
            actor ? CellPosition(cell) : new Vector3(cell.x, WorldItemHeight + TowerGroundY(cell), cell.y);

        private static Vector3 ReleaseArc(Vector3 from, Vector3 to, float progress, bool throwing) =>
            Vector3.Lerp(from, to, progress) + Vector3.up * (throwing ? Mathf.Sin(progress * Mathf.PI) * 0.9f : 0f);

        private bool HasClearThrowArc(Vector3 from, Vector3 to)
        {
            const float radius = 0.13f;
            int segments = Mathf.Max(8, Mathf.CeilToInt(Vector3.Distance(from, to) * 6f));
            var previous = from;
            for (int i = 1; i <= segments; i++)
            {
                var point = ReleaseArc(from, to, (float)i / segments, true);
                var cell = new Vector2Int(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.z));
                if (!InsideMap(cell) || _walls.Contains(cell) || _npcCells.Contains(cell)) return false;

                var delta = point - previous;
                foreach (var hit in Physics.SphereCastAll(previous, radius, delta.normalized, delta.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    if (BlocksThrow(hit.collider)) return false;
                previous = point;
            }
            return true;
        }

        private bool BlocksThrow(Collider collider)
        {
            var hit = collider.transform;
            // Units are resolved by grid occupancy so thrown-item effects use the correct actor.
            return !hit.IsChildOf(_player.transform) && !hit.IsChildOf(_carriedView.transform) &&
                (!_familiar || !hit.IsChildOf(_familiar.transform)) &&
                !_creatures.Exists(creature => creature.View && hit.IsChildOf(creature.View.transform)) &&
                !_thrownEggs.Exists(ally => ally.View && hit.IsChildOf(ally.View.transform));
        }

        private bool ReleaseCarriedObject(Vector2Int direction, bool throwing)
        {
            if (!CanUseLift() || !IsTowerAttackDirection(direction)) return false;
            if (KohSkipsAction) return TryTowerAction(Vector2Int.zero);
            if (throwing) Physics.SyncTransforms();
            Vector3 releaseOrigin = _carriedView.transform.position;
            bool isMonster = _carriedMonster.Actor != null || _carriedFamiliar;
            var landing = _playerCell;
            bool impact = false;
            bool hitFamiliar = false;
            ThrownEggAlly hitAlly = null;
            int target = -1;
            Vector3 flightEnd = ThrowLandingPosition(landing, isMonster);
            var previous = _playerCell;
            for (int distance = 1; distance <= (throwing ? 8 : 1); distance++)
            {
                var cell = previous + direction;
                if (!HasThrowPassage(previous, direction))
                {
                    impact = true;
                    break;
                }

                int enemy = CreatureAt(cell);
                if (enemy >= 0 && IsSubmerged(_creatures[enemy].Actor)) enemy = -1;
                bool familiar = _familiar && cell == _familiarCell && !IsSubmerged(_familiarActor);
                var ally = _thrownEggs.Find(entry => entry.View && entry.Turns > 0 && entry.Cell == cell && !IsSubmerged(entry.Actor));
                bool hitsUnit = enemy >= 0 || familiar || ally != null;
                Vector3 end = hitsUnit
                    ? CellPosition(cell) - new Vector3(direction.x, 0f, direction.y).normalized * 0.33f
                    : ThrowLandingPosition(cell, isMonster);
                if (throwing && !HasClearThrowArc(releaseOrigin, end))
                {
                    impact = true;
                    break;
                }
                if (hitsUnit)
                {
                    target = enemy;
                    hitFamiliar = familiar;
                    hitAlly = ally;
                    impact = true;
                    flightEnd = end;
                    break;
                }
                if (CanLandObject(cell))
                {
                    landing = cell;
                    flightEnd = end;
                }
                previous = cell;
            }

            // An adjacent impact can leave no tile in the throw direction.
            // Monsters need a free square, so look for a nearby landing.
            if (throwing && isMonster && landing == _playerCell)
            {
                for (int i = 0; i < 8; i++)
                {
                    var (x, y) = DirStep.Step(i);
                    var step = new Vector2Int(x, y);
                    var neighbor = _playerCell + step;
                    if (!HasThrowPassage(_playerCell, step) || !CanLandObject(neighbor) ||
                        !HasClearThrowArc(releaseOrigin, ThrowLandingPosition(neighbor, true))) continue;
                    landing = neighbor;
                    break;
                }
            }
            if (throwing && _carriedItem != null && (target >= 0 || hitFamiliar || hitAlly != null || _carriedItem.Kind == MonsterEgg.Kind))
            {
                var hit = target >= 0 ? _creatures[target].Actor : hitFamiliar ? _familiarActor : hitAlly?.Actor;
                Vector2Int impactCell = target >= 0 ? _creatures[target].Cell : hitFamiliar ? _familiarCell : hitAlly?.Cell ?? landing;
                if (!HasClearThrowArc(releaseOrigin, flightEnd)) return false;
                return ThrowInventoryItem(hit, impactCell, landing, flightEnd);
            }

            if (landing == _playerCell && (isMonster || !throwing || !CanLandObject(landing)))
            {
                _message = T("There is no free tile to put it down.", "Não há espaço livre para colocá-lo no chão.");
                return false;
            }

            if (throwing && !HasClearThrowArc(releaseOrigin, ThrowLandingPosition(landing, isMonster))) return false;

            if (_carriedItem != null && _towerItemTraps?.TryPlaceItem(landing, _carriedItem) != true)
                return false;

            TryTowerFace(direction);
            _releasedView = _carriedView;
            _releaseFrom = releaseOrigin;
            _releaseTo = ThrowLandingPosition(landing, isMonster);
            _releaseThrown = throwing;

            int releasedIndex = -1;
            int familiarImpactDamage = 0;
            if (_carriedFamiliar)
            {
                _familiar = _carriedView;
                _familiar.GetComponent<KewneSpriteView>()?.SetCarried(false);
                _familiarCell = landing;
                _landingFamiliar = true;
                if (throwing && impact)
                {
                    familiarImpactDamage = SpellOrImpactDamage(_familiarActor, 4, 1);
                    AdjustItemHealth(_familiarActor, -familiarImpactDamage);
                }
            }
            else if (isMonster)
            {
                _carriedMonster.Cell = landing;
                _landingMonster = _carriedMonster.Actor;
                releasedIndex = _creatures.Count;
                _creatures.Add(_carriedMonster);
            }
            else if (_carriedSalvage)
            {
                _lootCell = landing;
                _hasLoot = true;
            }
            else
            {
                _itemMarkers.Add(landing, _carriedView);
            }
            _carriedView = null;
            _carriedMonster = default;
            _carriedItem = null;
            _carriedFamiliar = _carriedSalvage = false;

            TryTowerAction(Vector2Int.zero);
            _landingMonster = null;
            _landingFamiliar = false;
            if (throwing && impact)
            {
                RecordTowerHit(_playerActor, _familiarActor, familiarImpactDamage);
                if (releasedIndex >= 0)
                    QueueCreatureDamage(_playerActor, releasedIndex, SpellOrImpactDamage(_creatures[releasedIndex].Actor, 4, 1));
                if (isMonster && target >= 0)
                    QueueCreatureDamage(_playerActor, target, SpellOrImpactDamage(_creatures[target].Actor, 8, 1));
                if (isMonster && hitFamiliar)
                {
                    int damage = SpellOrImpactDamage(_familiarActor, 8, 1);
                    _pendingFamiliarDamage += damage;
                    RecordTowerHit(_playerActor, _familiarActor, damage, needsActiveFamiliar: true);
                }
            }
            if (!throwing) _message = T("You set the object down.", "Você coloca o objeto no chão.");
            return true;
        }

        private bool ThrowInventoryItem(ActorRecord hit, Vector2Int impact, Vector2Int landing, Vector3 flightEnd)
        {
            var item = _carriedItem;
            var view = _carriedView;
            if (!TryTowerAction(Vector2Int.zero)) return false;
            _releasedView = view;
            _releaseFrom = view.transform.position;
            _releaseTo = flightEnd;
            _releaseThrown = true;
            _carriedItem = null;
            _carriedView = null;
            _pendingItemCommand = () =>
            {
                RemoveGenerated(view);
                ResolveThrownItem(item, hit, impact, landing);
            };
            return true;
        }

        private bool ThrowInFlight => _releasedView && _releaseThrown;

        private static float ThrowFlightDuration(Vector3 from, Vector3 to)
        {
            float distance = new Vector2(to.x - from.x, to.z - from.z).magnitude;
            return Mathf.Clamp(0.35f + 0.055f * distance, 0.35f, 0.8f);
        }

        private void PresentCarry(float progress)
        {
            if (_carriedView) PlaceHeldView(_carriedView, _carriedFamiliar || _carriedMonster.Actor != null);
            if (_releasedView)
            {
                _releasedView.transform.position = ReleaseArc(_releaseFrom, _releaseTo, progress, _releaseThrown);
                if (progress >= 1f) _releasedView = null;
            }
        }

        private void PlaceHeldView(GameObject view, bool actor = false)
        {
            if (!_player || !view) return;
            Vector2 facing = _area == Area.Town ? _townFacing : (Vector2)_towerFacing;
            Vector3 hand = _kohSprite ? _kohSprite.LiftHandPosition(_gameCamera, facing) : _player.transform.position + Vector3.up;
            if (actor) hand += Vector3.up * TownActorFootOffset;
            else if (view.TryGetComponent<SpriteRenderer>(out var renderer) && renderer.sprite)
            {
                Vector3 up = _gameCamera ? _gameCamera.transform.up : Vector3.up;
                hand -= up * (renderer.sprite.bounds.min.y * renderer.transform.lossyScale.y);
            }
            view.transform.position = hand;
        }

        private void ResetCarry()
        {
            _carriedView = _releasedView = _salvageMarker = null;
            _carriedMonster = default;
            _carriedItem = null;
            _carriedFamiliar = _carriedSalvage = false;
            _landingMonster = null;
            _landingFamiliar = false;
        }
    }
}
