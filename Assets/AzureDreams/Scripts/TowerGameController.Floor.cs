using System;
using System.Collections.Generic;
using UnityEngine;
using CoreDungeon = AzureDreams.Core.DungeonGenerator;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Playable bridge to the Architect's generated layout. Generation itself
    /// retains its documented non-retail fallbacks; this does not invent spawns.
    /// </summary>
    public sealed partial class TowerGameController
    {
        [SerializeField] private bool _useGeneratedTowerFloors;
        private TowerFloorManager _towerFloorLayout;
        private TowerItemTrapManager _towerItemTraps;
        private readonly Dictionary<Vector2Int, GameObject> _itemMarkers = new Dictionary<Vector2Int, GameObject>();
        private readonly Dictionary<Vector2Int, GameObject> _trapMarkers = new Dictionary<Vector2Int, GameObject>();

        private void ConsumeFloorMarker(Dictionary<Vector2Int, GameObject> markers, Vector2Int cell)
        {
            if (!markers.TryGetValue(cell, out var marker)) return;
            markers.Remove(cell);
            if (!marker) return;
            marker.SetActive(false);
            RemoveGenerated(marker);
        }
        private const int BlueCollarFloor = 12;
        private const int GurusOilPotFloor = 15;
        private const int BlueCapeFloor = 20;
        private const int WaterMedalFloor = 25;
        // The combat unit has a separate ID from Beldo's vision.
        private const AzureDreams.Core.MonsterSpecies BeldoBossSpecies = (AzureDreams.Core.MonsterSpecies)56;
        private const byte QuestItemKind = 0x0D;
        private const byte GurusOilPotId = 1;
        private const byte BlueCapeId = 6;
        private const byte WaterMedalId = 2;
        // F_okami_give_cape and F_wata_mizu_medaru in the retail script symbols.
        private const int WindmillQuestCompleteFlag = 0x1460; // Ftt_fkoya_isaku_scene01_ok
        private const int TavernQuestCompleteFlag = 4806;
        private const int PoolQuestCompleteFlag = 150;
        private bool UsesManagedTowerLayout => _useGeneratedTowerFloors || _floor is 2 or BlueCollarFloor or GurusOilPotFloor or BlueCapeFloor or WaterMedalFloor or TowerFloorRules.BeldoFloor;
        private int ActiveMapSize => _area == Area.Tower && UsesManagedTowerLayout ? CoreDungeon.GridSize : MapSize;
        private int TowerTileHeight(Vector2Int cell) => _area == Area.Tower && UsesManagedTowerLayout &&
            !InTowerTutorial && _towerFloorLayout != null ? _towerFloorLayout.GroundHeight(cell.x, cell.y) : 0;
        private float TowerGroundY(Vector2Int cell) => TowerTileHeight(cell) / 64f;

        public void ConfigureGeneratedTowerFloors(bool enabled)
        {
            if (_area != Area.Town || _turnBusy)
                throw new InvalidOperationException("Change floor layout mode in town, not during a run.");
            _useGeneratedTowerFloors = enabled;
        }

        private void BuildManagedTowerFloor()
        {
            ClearGenerated();
            if (_towerFloorLayout == null) _towerFloorLayout = new TowerFloorManager();
            if (InTowerTutorial)
                _towerFloorLayout.GenerateTutorial(Resources.Load<TextAsset>("Town/Retail/tutorial_floor").bytes,
                    TutorialEntrance, TutorialElevator);
            else _towerFloorLayout.Generate(_floor, _towerRun.Rng.Next());
            if (_floor == 1) PrepareManagedTowerEntrance();
            _towerItemTraps = new TowerItemTrapManager(_towerFloorLayout, _towerRun.Rng);
            if (InTowerTutorial) PlaceTutorialContents();
            else if (_floor == TowerFloorRules.BeldoFloor) _towerItemTraps.ClearGeneratedLoot();
            else
            {
                _towerItemTraps.AddEquipmentToGeneratedLoot();
                _towerItemTraps.ConfigureCherrlHealingHerb(TownSaveVariables.GetBool(CherrlHealingQuestCompleted));
                if (_floor == GurusOilPotFloor && !TownRetailScriptFlags.CreatePersistent().IsFlagSet(WindmillQuestCompleteFlag))
                    _towerItemTraps.TryPlaceGeneratedRoomItem(new AzureDreams.Core.DungeonItemEntry(GurusOilPotId, QuestItemKind, 0, 0),
                        seedSalt: 0x15C4);
                if (_floor == BlueCapeFloor && !TownRetailScriptFlags.CreatePersistent().IsFlagSet(TavernQuestCompleteFlag))
                    _towerItemTraps.TryPlaceGeneratedRoomItem(new AzureDreams.Core.DungeonItemEntry(BlueCapeId, QuestItemKind, 0, 0),
                        seedSalt: 0x20CA);
                if (_floor == BlueCollarFloor && !HasBlueCollar)
                    _towerItemTraps.TryPlaceGeneratedRoomItem(new AzureDreams.Core.DungeonItemEntry(
                        AzureDreams.Core.ItemSystem.Id.BlueCollar, CollarKind, 0, 0), seedSalt: 0x12C4);
                _towerItemTraps.SpawnTraps(4 + _floor);
            }
            _npcCells.Clear();
            for (int y = 0; y < CoreDungeon.GridSize; y++)
                for (int x = 0; x < CoreDungeon.GridSize; x++)
                    if (!_towerFloorLayout.IsWalkable(x, y)) _walls.Add(new Vector2Int(x, y));

            BuildManagedFloorMeshes();
            _playerCell = _towerFloorLayout.StairsDownCell;
            _stairCell = _towerFloorLayout.StairsUpCell;
            if (!_towerFloorLayout.IsWalkable(_playerCell.x, _playerCell.y) ||
                !_towerFloorLayout.IsWalkable(_stairCell.x, _stairCell.y) || _stairCell == _playerCell)
                throw new InvalidOperationException("Generated floor must have distinct walkable entry and exit stairs.");
            _player = SpawnKoh(_playerCell, "Player - generated floor");
            if (_familiarDefinition && !_familiarDefeated && !_familiarInBag)
            {
                _familiarCell = FindManagedActorCell(_playerCell + Vector2Int.right, _playerCell, _stairCell);
                _familiar = SpawnFamiliar(showSummoningEffect: _floor != 1 || !_starterJoinedThisRun);
                _familiarHealth = _floor == 1 ? FamiliarMaxHealth : Mathf.Min(_familiarHealth, FamiliarMaxHealth);
                _familiarActor.Hp = StatByte(_familiarHealth);
            }
            RestoreBlueCollarSummon();
            RestoreSalamanderSummons();
            _creatures.Clear();
            if (InTowerTutorial)
                SpawnManagedCreature(new Vector2Int(50, 44), AzureDreams.Core.MonsterSpecies.PULUNPA,
                    levelOverride: 1, allowSpawnSleep: false);
            else if (_floor == TowerFloorRules.BeldoFloor && !TownSaveVariables.GetBool(BeldoClearedKey))
            {
                SpawnManagedCreature(new Vector2Int(32, 22), BeldoBossSpecies);
            }
            else if (TowerFloorRules.AllowsRandomMonsters(_floor))
            {
                int average = Mathf.Max(1, Mathf.RoundToInt(_towerFloorLayout.RoomCount / 3f));
                int count = Mathf.Max(1, average - 1 + RollSpawnRange(3));
                // Preserve the previous population baseline before halving it.
                if (_floor == 1) count = Mathf.Max(count, TowerEncounterTable.FirstFloorCount);
                count = Mathf.Max(1, (count + 1) / 2);
                bool spawnMedalPicket = _floor == WaterMedalFloor &&
                    !TownRetailScriptFlags.CreatePersistent().IsFlagSet(PoolQuestCompleteFlag);
                for (int i = 0; i < count; i++)
                {
                    if (!TryFindManagedCreatureCell(false, out var cell)) break;
                    var species = TowerEncounterTable.Roll(_floor, _towerRun.Rng);
                    bool carriesWaterMedal = i == 0 && spawnMedalPicket;
                    SpawnManagedCreature(cell, carriesWaterMedal ? AzureDreams.Core.MonsterSpecies.PICKET : species,
                        carriesWaterMedal);
                }
                _turnsUntilReinforcement = 25 + RollSpawnRange(36);
            }
            if (_floor == 2) SpawnTowerEventNpc();
            if (!InTowerTutorial && _floor != TowerFloorRules.BeldoFloor) PlaceGeneratedEgg();
            _hasLoot = false;
            for (int y = 0; y < CoreDungeon.GridSize; y++)
                for (int x = 0; x < CoreDungeon.GridSize; x++)
                {
                    var cell = new Vector2Int(x, y);
                    var item = _towerItemTraps.GetItemAt(cell);
                    if (item != null) _itemMarkers[cell] = CreateGroundItemView(cell, item);
                    if (_towerItemTraps.TrapAt(cell) != null) CreateTrapMarker(cell);
                }
            PlanMonsterDen();
            if (_floor != TowerFloorRules.BeldoFloor) CreateTowerElevator();
            ConfigurePlayerView();
            InitializeTowerEvents();
            if (_floor == 1)
                CreateInnerTowerDoor(_towerEntranceEdge, _towerEntranceInward, _towerEntranceWidth, addThreshold: true);
            UpdateCameraFollow(snap: true);
        }

        private int _turnsUntilReinforcement;

        private int RollSpawnRange(int count) => RollTowerRange(_towerRun.Rng, count);

        private static int RollTowerRange(AzureDreams.Core.RetailRng rng, int count)
        {
            int limit = 32768 - 32768 % count;
            int roll;
            do { roll = rng.Next(); } while (roll >= limit);
            return roll % count;
        }

        private bool TryFindManagedCreatureCell(bool distantRoom, out Vector2Int selected)
        {
            var dungeon = _towerFloorLayout.Dungeon;
            var candidates = new List<Vector2Int>();
            int bestDistance = 0;
            for (int i = 0; i < dungeon.RoomCount; i++)
            {
                var room = dungeon.Rooms[i];
                int right = room.X + room.Width - 1;
                int top = room.Y + room.Height - 1;
                int distance = Mathf.Abs(_playerCell.x - Mathf.Clamp(_playerCell.x, room.X, right))
                    + Mathf.Abs(_playerCell.y - Mathf.Clamp(_playerCell.y, room.Y, top));
                if (distantRoom && (distance < 6 || distance < bestDistance)) continue;

                for (int y = room.Y; y <= top; y++)
                for (int x = room.X; x <= right; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (dungeon.Grid[y, x] != CoreDungeon.TileType.Floor ||
                        cell == _playerCell || cell == _stairCell || cell == _towerFloorLayout.StairsDownCell ||
                        (_familiar && cell == _familiarCell) || _thrownEggs.Exists(ally => ally.View && ally.Cell == cell) || CreatureAt(cell) >= 0 || IsSacredTile(cell) ||
                        (dungeon.GetFlags(x, y) & (ushort)CoreDungeon.TileFlag.HasItem) != 0)
                        continue;
                    if (distantRoom && distance > bestDistance)
                    {
                        candidates.Clear();
                        bestDistance = distance;
                    }
                    candidates.Add(cell);
                }
            }

            selected = candidates.Count == 0 ? default : candidates[RollSpawnRange(candidates.Count)];
            return candidates.Count > 0;
        }

        private GameObject SpawnManagedCreature(Vector2Int cell, AzureDreams.Core.MonsterSpecies species,
            bool carriesWaterMedal = false, int? levelOverride = null, bool allowSpawnSleep = true, GameObject visual = null)
        {
            string label = carriesWaterMedal ? "Blue Picket" : species == BeldoBossSpecies ? "Beldo" : species.ToString();
            string name = $"Tower creature: {label} - generated floor";
            var palette = carriesWaterMedal ? MonsterPaletteVariant.Water : MonsterPaletteVariant.Enemy;
            var actor = CreateTowerEnemy(species, levelOverride, allowSpawnSleep);
            var view = SpawnActor(visual ? visual : MonsterPrefab(species), cell, name, species: (int)species, palette: palette,
                actionAtlas: EnemyTrollActionAtlas(actor));
            SetActorCell(actor, cell);
            var creature = new CreatureState
            {
                Cell = cell,
                Actor = actor,
                CarriesWaterMedal = carriesWaterMedal,
                Health = actor.MaxHp,
                View = view,
                Locomotion = ConfigureLocomotion(view),
                Animation = view.GetComponentInChildren<AzureDreamsReimagined.Rendering.PsxSpriteRenderer>(),
            };
            Animate(creature.Animation, false);
            _creatures.Add(creature);
            return view;
        }

        private void DropWaterMedal(Vector2Int origin)
        {
            if (TownRetailScriptFlags.CreatePersistent().IsFlagSet(PoolQuestCompleteFlag)) return;
            DropTowerReward(origin, new AzureDreams.Core.DungeonItemEntry(WaterMedalId, QuestItemKind, 0, 0));
        }

        private bool DropTowerReward(Vector2Int origin, AzureDreams.Core.DungeonItemEntry item)
        {
            if (PlaceInventoryItem(origin, item)) return true;

            // If the enemy fell on an occupied item tile, use the closest free tile.
            Vector2Int? nearest = null;
            int bestDistance = int.MaxValue;
            foreach (var cell in _towerFloorLayout.OpenCells)
            {
                int distance = Distance(origin, cell);
                if (distance >= bestDistance || !CanPlaceInventoryItem(cell) || _walls.Contains(cell) ||
                    _towerItemTraps.GetItemAt(cell) != null) continue;
                bestDistance = distance;
                nearest = cell;
            }
            return nearest.HasValue && PlaceInventoryItem(nearest.Value, item);
        }

        private void TickTowerReinforcements()
        {
            var run = _towerRun;
            if (_area != Area.Tower || !_useGeneratedTowerFloors || InTowerTutorial ||
                !TowerFloorRules.AllowsRandomMonsters(_floor) ||
                run?.CurrentState != TowerRunManager.State.InTower || run.Rng == null ||
                _towerFloorLayout?.Dungeon == null) return;
            if (_turnsUntilReinforcement <= 0 || --_turnsUntilReinforcement > 0) return;
            _turnsUntilReinforcement = 25 + RollSpawnRange(36);
            if (TryFindManagedCreatureCell(true, out var cell))
                SpawnManagedCreature(cell, TowerEncounterTable.Roll(_floor, run.Rng));
        }

        private Vector2Int FindManagedActorCell(Vector2Int preferred, params Vector2Int[] blocked)
        {
            var cell = _towerFloorLayout.FindOpenCell(preferred, blocked);
            if (!_towerFloorLayout.IsWalkable(cell.x, cell.y))
                throw new InvalidOperationException("No walkable actor spawn cell.");
            foreach (var occupied in blocked)
                if (cell == occupied) throw new InvalidOperationException("No distinct actor spawn cell.");
            return cell;
        }
    }
}
