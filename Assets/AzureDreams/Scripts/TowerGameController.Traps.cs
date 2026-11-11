using System.Collections.Generic;
using AzureDreams.Core;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        [SerializeField] private Texture2D _towerTrapTexture;
        private const sbyte PrisonTrapTurns = 16;
        private Sprite _towerTrapSprite;
        private readonly Dictionary<TowerTrapEffect, Sprite> _trapSprites = new();
        private readonly Dictionary<Vector2Int, (int Original, int Until)> _trapTerrainChanges = new();
        private int _trapReversalUntil;
        private bool _trapGoUp;
        private const int TerrainTrapTurns = 20;
        private const float TrapMarkerWidth = 1.10f;
        private static readonly Vector2Int[] TrapCross =
        {
            Vector2Int.zero, Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };

        private Sprite TrapSprite(TowerTrapEffect effect)
        {
            if (_trapSprites.TryGetValue(effect, out var sprite)) return sprite;
            string name = effect == TowerTrapEffect.GoUp ? "Go_up" : effect.ToString();
            var texture = Resources.Load<Texture2D>($"Tower/Traps/{name}_trap");
            if (texture)
            {
                texture.filterMode = FilterMode.Point;
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect);
                _trapSprites.Add(effect, sprite);
                return sprite;
            }
            if (!_towerTrapSprite && _towerTrapTexture)
                _towerTrapSprite = Sprite.Create(_towerTrapTexture,
                    new Rect(0, 0, _towerTrapTexture.width, _towerTrapTexture.height), Vector2.one * 0.5f);
            return _towerTrapSprite;
        }
        private readonly Dictionary<Vector2Int, TowerTrapState> _demoTraps = new();
        private Vector2Int? _trapRearmCell;

        private TowerTrapState TrapAt(Vector2Int cell) => _towerItemTraps != null
            ? _towerItemTraps.TrapAt(cell) : _demoTraps.TryGetValue(cell, out var trap) ? trap : null;

        private static bool EnteredTowerCell(Vector3 from, Vector2Int cell) =>
            Mathf.RoundToInt(from.x) != cell.x || Mathf.RoundToInt(from.z) != cell.y;

        private void CreateTrapMarker(Vector2Int cell)
        {
            var marker = new GameObject("Tower Trap");
            marker.transform.SetParent(transform, true);
            marker.transform.SetPositionAndRotation(new Vector3(cell.x, 0.025f + TowerGroundY(cell), cell.y),
                TrapMarkerRotation());
            var sprite = marker.AddComponent<SpriteRenderer>();
            sprite.sprite = TrapSprite(TrapAt(cell)?.Effect ?? TowerTrapEffect.Slam);
            if (sprite.sprite) marker.transform.localScale = Vector3.one * (TrapMarkerWidth / sprite.sprite.bounds.size.x);
            _generated.Add(marker);
            _trapMarkers.Add(cell, marker);
            RefreshTrapMarker(cell);
        }

        private Quaternion TrapMarkerRotation() =>
            Quaternion.Euler(90f, _gameCamera ? _gameCamera.transform.eulerAngles.y : 0f, 0f);

        private void OrientVisibleTrapMarkers()
        {
            if (_trapMarkers.Count == 0) return;
            var rotation = TrapMarkerRotation();
            foreach (var marker in _trapMarkers.Values)
                if (marker && marker.activeSelf) marker.transform.rotation = rotation;
        }

        private void RefreshTrapMarker(Vector2Int cell)
        {
            if (!_trapMarkers.TryGetValue(cell, out var marker) || !marker) return;
            var trap = TrapAt(cell);
            marker.SetActive(trap?.IsRevealed == true);
            if (trap?.IsRevealed == true)
                marker.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, trap.IsActive ? 0.5f : 1f);
        }

        private void RevealTowerTraps(bool rearm = false)
        {
            foreach (var cell in _trapMarkers.Keys)
            {
                var trap = TrapAt(cell);
                if (trap == null) continue;
                trap.Reveal();
                if (rearm)
                {
                    if (_towerItemTraps != null) _towerItemTraps.TryRearmTrap(cell);
                    else trap.Rearm();
                }
                RefreshTrapMarker(cell);
            }
        }

        private void DiscoverTrapWithAttack(Vector2Int cell)
        {
            if (TrapAt(cell)?.Discover() != true) return;
            RefreshTrapMarker(cell);
            int level = _playerActor.Level;
            AddExperience(_playerActor, 2, ref _health);
            _towerRun?.SetHealth(_health);
            if (_playerActor.Level > level) SpawnFx(_fxMagic, CellPosition(_playerCell));
            _message = T("Koh reveals a trap and gains 2 XP.", "Koh revela uma armadilha e ganha 2 XP.");
        }

        private void TriggerTrapFor(Vector2Int cell, ActorRecord actor)
        {
            if (IsSubmerged(actor)) return;
            bool sprung = _towerItemTraps != null
                ? _towerItemTraps.TryTriggerTrap(cell) : TrapAt(cell)?.Spring() == true;
            if (!sprung) return;
            RefreshTrapMarker(cell);
            var effect = TrapAt(cell).Effect;
            PlayRetailSound(effect switch
            {
                TowerTrapEffect.Frog => RetailSfx.Frog,
                TowerTrapEffect.Crack or TowerTrapEffect.Upheaval => RetailSfx.TerrainShift,
                _ => RetailSfx.Trap
            });
            switch (effect)
            {
                case TowerTrapEffect.Rust:
                    RustEquippedGear(actor);
                    break;
                case TowerTrapEffect.Prison:
                    if (_towerRun?.Rng == null || RollSpawnRange(Mathf.Max(1, AttackLuck(actor))) < 0x40)
                        Status(actor, StatusId.PARALYSIS, PrisonTrapTurns);
                    break;
                case TowerTrapEffect.Chaos: Status(actor, StatusId.CONFUSION, ChaosTurns); break;
                case TowerTrapEffect.Blind: Status(actor, StatusId.BLIND, BlindnessTurns); break;
                case TowerTrapEffect.Poison: Status(actor, StatusId.POISON, 8); break;
                case TowerTrapEffect.Frog: Status(actor, StatusId.FROG, 20); break;
                case TowerTrapEffect.Sleep: Status(actor, StatusId.SLEEP, 8); break;
                case TowerTrapEffect.Slow: Status(actor, StatusId.SPEED, -16); break;
                case TowerTrapEffect.Seal:
                    if (actor != _playerActor) Status(actor, StatusId.MAGIC_BIND, 20);
                    break;
                case TowerTrapEffect.GoUp:
                    if (actor == _playerActor && _floor < TowerFloorRules.BeldoFloor &&
                        _towerRun?.CurrentState == TowerRunManager.State.InTower) _trapGoUp = true;
                    break;
                case TowerTrapEffect.Reversal:
                    if (actor == _playerActor) _trapReversalUntil = _completedTowerTurns + 21;
                    break;
                case TowerTrapEffect.Bump: WakeMonstersInTrapRoom(cell); break;
                case TowerTrapEffect.Crack: ChangeTrapTerrain(cell, -TowerFloorManager.TerrainBlockHeight); break;
                case TowerTrapEffect.Upheaval: ChangeTrapTerrain(cell, TowerFloorManager.TerrainBlockHeight); break;
                case TowerTrapEffect.Warp: WarpTrapActor(cell, actor); break;
                case TowerTrapEffect.Bomb: ExplodeTrap(cell); break;
                case TowerTrapEffect.Slam:
                    StartCoroutine(PresentSlamTrap(cell));
                    DamageFromTrap(actor, 10, "a Slam trap", "uma armadilha de impacto");
                    break;
            }
            if (actor == _playerActor && effect != TowerTrapEffect.Rust)
            {
                var name = TrapDisplayName(effect);
                _message = T($"Koh steps on a trap ({name}).", $"Koh pisa em uma armadilha ({name}).");
            }
        }

        private string TrapDisplayName(TowerTrapEffect effect) => effect switch
        {
            TowerTrapEffect.Blind => T("Blinder", "Cegueira"),
            TowerTrapEffect.Bomb => T("Bomb", "Bomba"),
            TowerTrapEffect.Bump => T("Bump", "Despertar"),
            TowerTrapEffect.Chaos => T("Chaos", "Caos"),
            TowerTrapEffect.Crack => T("Crack", "Fenda"),
            TowerTrapEffect.Frog => T("Frog", "Sapo"),
            TowerTrapEffect.GoUp => T("Go up", "Subida"),
            TowerTrapEffect.Poison => T("Poison", "Veneno"),
            TowerTrapEffect.Prison => T("Prison", "Prisão"),
            TowerTrapEffect.Reversal => T("Reversal", "Inversão"),
            TowerTrapEffect.Seal => T("Seal", "Selo"),
            TowerTrapEffect.Slam => T("Slam", "Impacto"),
            TowerTrapEffect.Sleep => T("Sleep", "Sono"),
            TowerTrapEffect.Slow => T("Slow", "Lentidão"),
            TowerTrapEffect.Upheaval => T("Upheaval", "Elevação"),
            TowerTrapEffect.Warp => T("Warp", "Teleporte"),
            _ => T("Rust", "Ferrugem")
        };

        private System.Collections.IEnumerator PresentSlamTrap(Vector2Int cell)
        {
            var end = CellPosition(cell);
            var rock = CreatePrimitive(PrimitiveType.Sphere, "Slam trap weight",
                end + Vector3.up * 2f, Vector3.one * 0.4f, _wallMaterial, false);
            float elapsed = 0f;
            while (rock && elapsed < 0.24f)
            {
                yield return null;
                elapsed += Time.deltaTime * TrapAnimationSpeed;
                if (rock) rock.transform.position = end + Vector3.up * (2f * (1f - Mathf.Clamp01(elapsed / 0.24f)));
            }
            if (!rock) yield break;
            PlayRetailSound(RetailSfx.FallingWeight);
            SpawnFx(_fxDust, end, TrapAnimationSpeed);
            RemoveGenerated(rock);
        }

        private void DamageFromTrap(ActorRecord actor, int power, string source, string sourcePt)
        {
            if (actor.Hp == 0 || (actor == _playerActor && _testInvulnerable)) return;
            int damage = Mathf.Min(actor.Hp, SpellOrImpactDamage(actor, power, actor.Level));
            if (damage <= 0) return;
            int remaining = actor.Hp - damage;
            AdjustItemHealth(actor, -damage);
            actor.Hp = StatByte(remaining);
            if (actor == _playerActor) _towerRun?.SetHealth(_health);
            RecordTowerHazard(actor, damage, source, sourcePt);
        }

        private void ExplodeTrap(Vector2Int center)
        {
            foreach (var offset in TrapCross)
            {
                var cell = center + offset;
                if (!InsideMap(cell) || _walls.Contains(cell)) continue;
                _towerItemTraps?.TakeItem(cell);
                ConsumeFloorMarker(_itemMarkers, cell);
                if (_hasLoot && _lootCell == cell)
                {
                    _hasLoot = false;
                    if (_salvageMarker) RemoveGenerated(_salvageMarker);
                    _salvageMarker = null;
                }
                if (cell == _playerCell) DamageFromTrap(_playerActor, 10, "a Bomb trap", "uma armadilha de bomba");
                if (_familiar && cell == _familiarCell) DamageFromTrap(_familiarActor, 10, "a Bomb trap", "uma armadilha de bomba");
                int enemy = CreatureAt(cell);
                if (enemy >= 0) DamageFromTrap(_creatures[enemy].Actor, 10, "a Bomb trap", "uma armadilha de bomba");
                foreach (var ally in _thrownEggs)
                    if (ally.View && ally.Cell == cell) DamageFromTrap(ally.Actor, 10, "a Bomb trap", "uma armadilha de bomba");
                SpawnFx(_fxAttack, CellPosition(cell), TrapAnimationSpeed);
            }
        }

        private void WakeMonstersInTrapRoom(Vector2Int cell)
        {
            int room = BellRoomAt(cell);
            foreach (var creature in _creatures)
                if (room >= 0 ? BellRoomAt(creature.Cell) == room : creature.Cell == cell)
                    Status(creature.Actor, StatusId.SLEEP, 0);
        }

        private void ChangeTrapTerrain(Vector2Int center, int heightDelta)
        {
            if (_towerFloorLayout?.Heights == null || !UsesManagedTowerLayout || InTowerTutorial) return;
            var changes = new Dictionary<Vector2Int, int>();
            foreach (var offset in TrapCross)
            {
                var cell = center + offset;
                if (!_towerFloorLayout.IsWalkable(cell.x, cell.y) || cell == _stairCell) continue;
                int current = TowerTileHeight(cell);
                int original = _trapTerrainChanges.TryGetValue(cell, out var active) ? active.Original : current;
                _trapTerrainChanges[cell] = (original, _completedTowerTurns + TerrainTrapTurns + 1);
                changes[cell] = original + heightDelta;
            }
            if (changes.Count > 0)
                ApplyTowerHeightChanges(cell => changes.TryGetValue(cell, out int height) ? height : TowerTileHeight(cell));
        }

        private void RestoreExpiredTrapTerrain()
        {
            if (_trapTerrainChanges.Count == 0) return;
            Dictionary<Vector2Int, int> expired = null;
            foreach (var pair in _trapTerrainChanges)
            {
                if (pair.Value.Until > _completedTowerTurns) continue;
                expired ??= new Dictionary<Vector2Int, int>();
                expired.Add(pair.Key, pair.Value.Original);
            }
            if (expired == null) return;
            foreach (var cell in expired.Keys) _trapTerrainChanges.Remove(cell);
            ApplyTowerHeightChanges(cell => expired.TryGetValue(cell, out int height) ? height : TowerTileHeight(cell));
        }

        private void WarpTrapActor(Vector2Int from, ActorRecord actor)
        {
            var cells = new List<Vector2Int>();
            for (int y = 1; y < ActiveMapSize - 1; y++)
            for (int x = 1; x < ActiveMapSize - 1; x++)
            {
                var cell = new Vector2Int(x, y);
                if (cell == from || cell == _stairCell || _walls.Contains(cell) || _npcCells.Contains(cell) ||
                    BellCellOccupied(cell) || TrapAt(cell) is { IsActive: true } || IsSacredTile(cell)) continue;
                cells.Add(cell);
            }
            if (cells.Count == 0) return;
            var destination = cells[RollSpawnRange(cells.Count)];
            GameObject view;
            if (actor == _playerActor)
            {
                _playerCell = destination;
                view = _player;
                _elevatorDeclined = false;
            }
            else if (actor == _familiarActor)
            {
                _familiarCell = destination;
                view = _familiar;
            }
            else
            {
                int index = _creatures.FindIndex(creature => creature.Actor == actor);
                if (index >= 0)
                {
                    var creature = _creatures[index];
                    creature.Cell = destination;
                    _creatures[index] = creature;
                    view = creature.View;
                }
                else
                {
                    var ally = _thrownEggs.Find(candidate => candidate.Actor == actor);
                    if (ally == null) return;
                    ally.Cell = destination;
                    view = ally.View;
                }
            }
            SetActorCell(actor, destination);
            SpawnFx(_fxMagic, CellPosition(from), TrapAnimationSpeed);
            if (view) view.transform.position = CellPosition(destination);
            SpawnFx(_fxMagic, CellPosition(destination), TrapAnimationSpeed);
            if (actor == _playerActor) UpdateCameraFollow(snap: true);
        }

        private bool TryOpenTrapRearm()
        {
            if (_area != Area.Tower) return false;
            var cell = _playerCell + _towerFacing;
            if (TrapAt(cell) is not { IsRevealed: true, IsActive: false }) cell = _playerCell;
            if (TrapAt(cell) is not { IsRevealed: true, IsActive: false }) return false;
            _trapRearmCell = cell;
            return true;
        }

        private bool HandleTrapRearmInput()
        {
            if (!_trapRearmCell.HasValue) return false;
            if (_cancel?.WasPressedThisFrame() == true) _trapRearmCell = null;
            else if (_interact?.WasPressedThisFrame() == true) ConfirmTrapRearm();
            return true;
        }

        private void ConfirmTrapRearm()
        {
            if (!_trapRearmCell.HasValue) return;
            var cell = _trapRearmCell.Value;
            _trapRearmCell = null;
            if (TrapAt(cell) is not { IsRevealed: true, IsActive: false }) return;
            ScheduleInventoryAction(() =>
            {
                if (_towerItemTraps != null) _towerItemTraps.TryRearmTrap(cell);
                else _demoTraps[cell].Rearm();
                RefreshTrapMarker(cell);
            });
        }

        private void DrawTrapRearm(float width, float height)
        {
            if (!_trapRearmCell.HasValue) return;
            var rect = new Rect(width * 0.5f - 145f, height * 0.5f - 55f, 290f, 110f);
            DrawDialogueFrame(rect);
            GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 10f, rect.width - 24f, rect.height - 20f));
            GUILayout.Label(T("Rearm this trap?", "Reativar esta armadilha?"));
            if (GUILayout.Button(WithInteractHint("Yes", "Sim"))) ConfirmTrapRearm();
            if (GUILayout.Button(T("No [Esc]", "Não [Esc]"))) _trapRearmCell = null;
            GUILayout.EndArea();
        }
    }
}
