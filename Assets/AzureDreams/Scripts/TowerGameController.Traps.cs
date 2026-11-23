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
        // Visual 'mola aberta' (go_up2) mostrado so no Reimagined depois que a armadilha de
        // Go Up e ativada - no Classic nao ha uma segunda imagem, entao ela fica null e o
        // marcador simplesmente mantem o sprite normal (igual ao comportamento de antes).
        private Sprite _goUpSprungSprite;
        private bool _goUpSprungChecked;
        private readonly Dictionary<Vector2Int, (int Original, int Until)> _trapTerrainChanges = new();
        private int _trapReversalUntil;
        private bool _trapGoUp;
        private bool _goUpTransition;
        private bool _trapGoDown;
        private bool _goDownTransition;

        private System.Collections.IEnumerator PlayGoUpTransition()
        {
            _goUpTransition = true;
            try
            {
                yield return new WaitForSecondsRealtime(.32f / (TowerAnimationSpeed * .7f));
                const float fadeSeconds = 0.3f;
                for (float elapsed = 0f; elapsed < fadeSeconds; elapsed += Time.unscaledDeltaTime * 0.7f)
                {
                    _fadeToBlack = elapsed / fadeSeconds;
                    yield return null;
                }
                _fadeToBlack = 1f;
                yield return new WaitForSecondsRealtime(2f);
                CompleteElevatorAscent();
            }
            finally
            {
                _fadeToBlack = 0f;
                _goUpTransition = false;
                _trapAnimationSpeed = TowerAnimationSpeed;
            }
        }

        // Armadilha Go Down: mesma apresentacao (fade para preto) do Go Up, mas descendo um
        // andar em vez de subir - nao ha elevador/mola envolvido, e so o alcapao abrindo.
        private System.Collections.IEnumerator PlayGoDownTransition()
        {
            _goDownTransition = true;
            try
            {
                const float fadeSeconds = 0.3f;
                for (float elapsed = 0f; elapsed < fadeSeconds; elapsed += Time.unscaledDeltaTime * 0.7f)
                {
                    _fadeToBlack = elapsed / fadeSeconds;
                    yield return null;
                }
                _fadeToBlack = 1f;
                yield return new WaitForSecondsRealtime(2f);
                CompleteTrapDescent();
            }
            finally
            {
                _fadeToBlack = 0f;
                _goDownTransition = false;
                _trapAnimationSpeed = TowerAnimationSpeed;
            }
        }

        private void CompleteTrapDescent()
        {
            _towerRun.JumpToFloor(Mathf.Max(1, _floor - 1));
            _floor = _towerRun.Floor;
            BuildTowerFloor();
            _towerRun.CompleteFloorTransition();
            _message = T($"You fall through to {TowerFloorLabel(_floor)}.", $"Você cai para o {TowerFloorLabel(_floor)}.");
        }
        private float _trapAnimationSpeed = TowerAnimationSpeed;
        private const int TerrainTrapTurns = 20;
        private const float TrapMarkerWidth = 1.10f;
        private static readonly Vector2Int[] TrapCross =
        {
            Vector2Int.zero, Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };

        private Sprite TrapSprite(TowerTrapEffect effect)
        {
            if (_trapSprites.TryGetValue(effect, out var sprite)) return sprite;
            string resourceName = effect switch
            {
                TowerTrapEffect.GoUp => "Go_up_trap",
                TowerTrapEffect.GoDown => "Go_down_trap",
                TowerTrapEffect.Upheaval => "Upheaval",
                _ => $"{effect}_trap"
            };
            var texture = AssetMode.Load<Texture2D>($"Tower/Traps/{resourceName}");
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

        private Sprite GoUpSprungSprite()
        {
            if (_goUpSprungChecked) return _goUpSprungSprite;
            _goUpSprungChecked = true;
            var texture = AssetMode.Load<Texture2D>("Tower/Traps/Go_up_trap_sprung");
            if (texture)
            {
                texture.filterMode = FilterMode.Point;
                _goUpSprungSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect);
            }
            return _goUpSprungSprite;
        }
        private readonly Dictionary<Vector2Int, TowerTrapState> _demoTraps = new();
        private Vector2Int? _trapRearmCell;
        private Vector2Int? _trapDisarmCell;

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
            // The artwork already contains perspective and raised mechanisms.
            // Tilting it slightly above the floor preserves that volume instead
            // of projecting it a second time as a flat floor decal.
            Quaternion.Euler(55f, _gameCamera ? _gameCamera.transform.eulerAngles.y : 0f, 0f);

        private void OrientVisibleTrapMarkers()
        {
            if (_trapMarkers.Count == 0) return;
            var rotation = TrapMarkerRotation();
            foreach (var marker in _trapMarkers.Values)
                if (marker && marker.activeSelf)
                {
                    marker.transform.rotation = rotation;
                    GroundTrapMarker(marker);
                }
        }

        private void GroundTrapMarker(GameObject marker)
        {
            var visual = marker.GetComponent<SpriteRenderer>();
            if (!visual || !visual.sprite) return;
            var position = marker.transform.position;
            var cell = new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z));
            float halfHeight = visual.sprite.bounds.extents.y * marker.transform.localScale.y;
            position.y = TowerGroundY(cell) + .025f + halfHeight * Mathf.Abs(marker.transform.up.y);
            marker.transform.position = position;
        }

        // Efeitos continuos dos marcadores de armadilha ja revelados: pentagrama pulsando
        // (Seal e Frog) e a mola do Go Up balancando depois de ativada (go_up2, so Reimagined).
        private void AnimateTrapMarkerEffects()
        {
            if (_trapMarkers.Count == 0) return;
            foreach (var pair in _trapMarkers)
            {
                var marker = pair.Value;
                if (!marker || !marker.activeSelf) continue;
                var trap = TrapAt(pair.Key);
                var markerSprite = marker.GetComponent<SpriteRenderer>();
                if (trap == null || !markerSprite) continue;
                float seed = pair.Key.x * 0.7f + pair.Key.y * 0.9f;
                switch (trap.Effect)
                {
                    case TowerTrapEffect.Seal or TowerTrapEffect.Frog:
                    {
                        float baseAlpha = trap.IsActive ? 1f : 0.5f;
                        float pulse = 0.78f + 0.22f * Mathf.Sin(Time.time * 2.2f + seed);
                        markerSprite.color = new Color(1f, 1f, 1f, baseAlpha * pulse);
                        break;
                    }
                    case TowerTrapEffect.GoUp when !trap.IsActive && _sprungGoUpTraps.Contains(pair.Key):
                    {
                        var sprung = GoUpSprungSprite();
                        if (!sprung) break;
                        if (markerSprite.sprite != sprung) markerSprite.sprite = sprung;
                        marker.transform.localScale = Vector3.one * (TrapMarkerWidth / sprung.bounds.size.x);
                        GroundTrapMarker(marker);
                        break;
                    }
                }
            }
        }

        private void RefreshTrapMarker(Vector2Int cell)
        {
            if (!_trapMarkers.TryGetValue(cell, out var marker) || !marker) return;
            var trap = TrapAt(cell);
            marker.SetActive(trap?.IsRevealed == true);
            if (trap?.IsRevealed == true)
            {
                var renderer = marker.GetComponent<SpriteRenderer>();
                if (trap.IsActive)
                {
                    _sprungGoUpTraps.Remove(cell);
                    renderer.sprite = TrapSprite(trap.Effect);
                    if (renderer.sprite) marker.transform.localScale = Vector3.one * (TrapMarkerWidth / renderer.sprite.bounds.size.x);
                }
                renderer.color = new Color(1f, 1f, 1f, trap.IsActive ? 0.5f : 1f);
                GroundTrapMarker(marker);
            }
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
            var trap = TrapAt(cell);
            if (trap == null || !trap.RevealByAttack(out bool awardExperience)) return;
            RefreshTrapMarker(cell);
            if (!awardExperience)
            {
                _message = T("Koh reveals a trap.", "Koh revela uma armadilha.");
                return;
            }
            AddExperience(_playerActor, 2, ref _health);
            _towerRun?.SetHealth(_health);
            _message = T("Koh reveals a trap and gains 2 XP.", "Koh revela uma armadilha e ganha 2 XP.");
        }

        private void TriggerTrapFor(Vector2Int cell, ActorRecord actor, bool enemy = false)
        {
            if (IsSubmerged(actor)) return;
            bool deferMinimapReveal = enemy && !IsInKohsTrapRoom(cell, BellRoomAt(_playerCell));
            bool sprung = _towerItemTraps != null
                ? _towerItemTraps.TryTriggerTrap(cell, deferMinimapReveal)
                : TrapAt(cell)?.Spring(deferMinimapReveal) == true;
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
                    _sprungGoUpTraps.Add(cell);
                    StartCoroutine(PresentGoUpSpring(cell));
                    if (actor == _playerActor && CanAscendFromFloor &&
                        _towerRun?.CurrentState == TowerRunManager.State.InTower)
                    {
                        _trapGoUp = true;
                        _trapAnimationSpeed = TowerAnimationSpeed * 0.7f; // GoUp traps work 30% slower
                    }
                    else if (actor == _playerActor && InSecondTower && AtSecondTowerTop) ShowNoMoreFloors();
                    break;
                case TowerTrapEffect.GoDown:
                    // Sem restricao extra de andar: a armadilha ja nao e gerada nos andares
                    // 1, 2, 3 e 40 (TowerItemTrapManager.GeneratedTrapEffect), entao o andar
                    // de destino (_floor - 1) nunca fica abaixo de 3. JumpToFloor nao tem
                    // limite inferior, igual ao usado pelo elevador para subir.
                    if (actor == _playerActor && _towerRun?.CurrentState == TowerRunManager.State.InTower)
                        _trapGoDown = true;
                    break;
                case TowerTrapEffect.Reversal:
                    if (actor == _playerActor) _trapReversalUntil = _completedTowerTurns + 21;
                    break;
                case TowerTrapEffect.Bump: WakeUnitsOnFloor(); break;
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
            TowerTrapEffect.GoDown => T("Go down", "Descida"),
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
            StartCoroutine(PresentBlastTrap(center));
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
            }
        }

        private void WakeUnitsOnFloor()
        {
            Wake(_playerActor, _player);
            if (_familiar || _carriedFamiliar) Wake(_familiarActor, _familiar ? _familiar : _carriedView);
            foreach (var creature in _creatures) Wake(creature.Actor, creature.View);
            foreach (var ally in _thrownEggs) Wake(ally.Actor, ally.View);
            if (_blueCollarAlly != null) Wake(_blueCollarAlly.Actor, _blueCollarAlly.View);
            if (_carriedMonster.Actor != null) Wake(_carriedMonster.Actor, _carriedView);

            void Wake(ActorRecord actor, GameObject view)
            {
                if (actor == null) return;
                Status(actor, StatusId.SLEEP, 0);
                if (!view) return;
                if (actor == _playerActor && _kohSprite) _kohSprite.PresentSleep(false);
                bool exhausted = MonsterExhausted(actor), paralyzed = Combat.HasStatus(actor, StatusId.PARALYSIS);
                view.GetComponentInChildren<MonsterTurnSpriteView>()?.PresentStatus(false, exhausted, paralyzed);
                view.GetComponent<KewneSpriteView>()?.PresentStatus(false, exhausted, paralyzed);
            }
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
            if (TrapAt(cell) is { IsRevealed: true, IsActive: true })
            {
                _trapDisarmCell = cell;
                return true;
            }
            if (TrapAt(cell) is not { IsRevealed: true, IsActive: false }) cell = _playerCell;
            if (TrapAt(cell) is not { IsRevealed: true, IsActive: false }) return false;
            _trapRearmCell = cell;
            return true;
        }

        private bool HandleTrapRearmInput()
        {
            if (!_trapRearmCell.HasValue && !_trapDisarmCell.HasValue) return false;
            if (_cancel?.WasPressedThisFrame() == true)
            {
                _trapRearmCell = null;
                _trapDisarmCell = null;
            }
            else if (_trapDisarmCell.HasValue && _interact?.WasPressedThisFrame() == true) ConfirmTrapDisarm();
            else if (_interact?.WasPressedThisFrame() == true) ConfirmTrapRearm();
            return true;
        }

        private void ConfirmTrapDisarm()
        {
            if (!_trapDisarmCell.HasValue) return;
            var cell = _trapDisarmCell.Value;
            _trapDisarmCell = null;
            if (TrapAt(cell) is not { IsRevealed: true, IsActive: true }) return;
            ScheduleInventoryAction(() =>
            {
                bool awardExperience;
                bool disarmed;
                if (_towerItemTraps != null)
                    disarmed = _towerItemTraps.TryDisarmTrap(cell, out awardExperience);
                else if (_demoTraps.TryGetValue(cell, out var trap))
                    disarmed = trap.Disarm(out awardExperience);
                else
                {
                    disarmed = false;
                    awardExperience = false;
                }
                if (!disarmed) return;
                RefreshTrapMarker(cell);
                if (!awardExperience)
                {
                    _message = T("Koh disarms the trap.", "Koh desarma a armadilha.");
                    return;
                }
                AddExperience(_playerActor, 2, ref _health);
                _towerRun?.SetHealth(_health);
                _message = T("Koh disarms the trap and gains 2 XP.", "Koh desarma a armadilha e ganha 2 XP.");
            });
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
            if (_trapDisarmCell.HasValue)
            {
                var disarmRect = new Rect(width * 0.5f - 145f, height * 0.5f - 90f, 290f, 180f);
                DrawDialogueFrame(disarmRect);
                GUILayout.BeginArea(WindowDesignSystem.Content(disarmRect, footer: true));
                GUILayout.Label(T("Disarm this trap?", "Desarmar esta armadilha?"));
                if (GUILayout.Button(WithInteractHint("Yes", "Sim"))) ConfirmTrapDisarm();
                if (GUILayout.Button(WithCancelHint("No", "Não"))) _trapDisarmCell = null;
                GUILayout.EndArea();
                return;
            }
            if (!_trapRearmCell.HasValue) return;
            var rect = new Rect(width * 0.5f - 145f, height * 0.5f - 90f, 290f, 180f);
            DrawDialogueFrame(rect);
            GUILayout.BeginArea(WindowDesignSystem.Content(rect, footer: true));
            GUILayout.Label(T("Rearm this trap?", "Reativar esta armadilha?"));
            if (GUILayout.Button(WithInteractHint("Yes", "Sim"))) ConfirmTrapRearm();
            if (GUILayout.Button(WithCancelHint("No", "Não"))) _trapRearmCell = null;
            GUILayout.EndArea();
        }
    }
}
