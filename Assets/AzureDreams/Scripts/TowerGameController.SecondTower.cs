using System.Collections.Generic;
using UnityEngine;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// The second tower (retail dungeon mode 2, 0x80012090 == 2), reached from the floor-2 elevator.
    /// The port keeps it on internal floors 41..140, shown as floors 1..100 of the second tower.
    ///
    /// Retail rules (decomp):
    /// * setup_dungeon_floor_encounters: no floor events, no fixed floor tables.  The music group is
    ///   rng &amp; 7 on every floor.  Four monster groups, each one random species MONSTER_DRAGON..MONSTER_MAXIMUM
    ///   at level floor + (rng &amp; 1) (capped at 99).  The first floor rolls all four; afterwards groups 2,3
    ///   slide into 0,1 (keeping their species and level) and 2,3 are rolled anew.
    /// * The special floors (Blue Collar 12, Guru's pot 15, Water Medal 25, Beldo 31/40, floor 2 visitors)
    ///   only run in mode 0, so none of them happen here.
    /// * Items: mark_category_items_flag_400(2) + shuffled appearances (see SecondTowerItems); flag-0x40
    ///   records (Poison/Paralyze/Sleep herbs, Pita, Geropita, Lar/Slow/Tovar seeds, Flat/Alchemic scrolls,
    ///   Focus loupe, Trained wand, Picket and U-Boat eggs) only appear here.
    /// * place_floor_gold: the gold budget is floor * 200 (± up to half), spent in coin piles of 160-240 G
    ///   (gold), 80-120 G (silver) or 16-24 G (copper).
    /// * draw_dungeon_value_and_label uses draw mode 1: the floor label is tinted (colour 0x202080).
    /// </summary>
    public sealed partial class TowerGameController
    {
        internal const int SecondTowerFloorCount = 100;
        internal const int SecondTowerTopFloor = SecondTowerFirstFloor + SecondTowerFloorCount - 1;

        private readonly MonsterSpecies[] _secondTowerGroups = new MonsterSpecies[4];
        private readonly int[] _secondTowerLevels = new int[4];
        private int _secondTowerGroupsFloor = -1;
        private int _secondTowerMusicGroup;
        private int _secondTowerRolledGroup = -1;

        private bool InSecondTower => _area == Area.Tower && _floor >= SecondTowerFirstFloor;
        private int SecondTowerFloorNumber => _floor - TowerFloorRules.BeldoFloor;
        /// <summary>Floor number used by floor-scaled rules (hatch level...): the second tower counts from 1.</summary>
        private int EffectiveTowerFloor => InSecondTower ? SecondTowerFloorNumber : _floor;
        private bool AtSecondTowerTop => _floor >= SecondTowerTopFloor;
        private bool CanAscendFromFloor => _floor != TowerFloorRules.BeldoFloor && !AtSecondTowerTop;
        private int NextElevatorFloor => _ascendToSecondTower && _floor == 2 ? SecondTowerFirstFloor : _floor + 1;

        private static readonly Color SecondTowerLabelTint = new(0.45f, 0.5f, 1f, 1f);

        private string TowerFloorLabel(int floor) => floor >= SecondTowerFirstFloor
            ? T($"Second tower, floor {floor - TowerFloorRules.BeldoFloor}",
                $"Segunda torre, {floor - TowerFloorRules.BeldoFloor}º andar")
            : T($"floor {floor}", $"{floor}º andar");

        private string HudFloorText => InSecondTower
            ? T($"{SecondTowerFloorNumber}F", $"{SecondTowerFloorNumber}º")
            : T($"{_floor}F", $"{_floor}º");

        private static string SaveFloorLocation(int floor)
        {
            if (floor == SecondTowerFirstFloor)
                return T("Tower 2F  →  Continue at second tower 1F", "Torre 2º andar  →  Retomar na segunda torre, 1º");
            if (floor > SecondTowerFirstFloor)
            {
                int n = floor - TowerFloorRules.BeldoFloor;
                return T($"Second tower {n - 1}F  →  Continue at {n}F",
                    $"Segunda torre {n - 1}º andar  →  Retomar no {n}º");
            }
            return T($"Tower {Mathf.Max(1, floor - 1)}F  →  Continue at {floor}F",
                $"Torre {Mathf.Max(1, floor - 1)}º andar  →  Retomar no {floor}º");
        }

        private void DrawFloorHudText(Rect rect)
        {
            var previous = GUI.color;
            if (InSecondTower) GUI.color = previous * SecondTowerLabelTint;
            DrawHudText(rect, HudFloorText, _bodyStyle);
            GUI.color = previous;
        }

        /// <summary>BuildTowerFloor hook, before the floor music starts.</summary>
        private void BeginSecondTowerFloor()
        {
            if (!InSecondTower)
            {
                if (SecondTowerItems.Active) SecondTowerItems.End();
                _secondTowerGroupsFloor = -1;
                return;
            }
            if (!SecondTowerItems.Active)
                SecondTowerItems.Begin(_floor == SecondTowerFirstFloor, _towerRun.Rng.Next() | (_towerRun.Rng.Next() << 15));
            _secondTowerMusicGroup = RollSpawnRange(8);
            RollSecondTowerMonsterGroups();
        }

        private void RollSecondTowerMonsterGroups()
        {
            if (_secondTowerGroupsFloor != _floor - 1)
            {
                RollSecondTowerGroup(0);
                RollSecondTowerGroup(1);
            }
            else
            {
                _secondTowerGroups[0] = _secondTowerGroups[2];
                _secondTowerLevels[0] = _secondTowerLevels[2];
                _secondTowerGroups[1] = _secondTowerGroups[3];
                _secondTowerLevels[1] = _secondTowerLevels[3];
            }
            RollSecondTowerGroup(2);
            RollSecondTowerGroup(3);
            _secondTowerGroupsFloor = _floor;
        }

        private void RollSecondTowerGroup(int group)
        {
            const int first = (int)MonsterSpecies.DRAGON, last = (int)MonsterSpecies.MAXIMUM;
            _secondTowerGroups[group] = (MonsterSpecies)(first + RollSpawnRange(last - first + 1));
            _secondTowerLevels[group] = Mathf.Min(99, SecondTowerFloorNumber + RollSpawnRange(2));
        }

        /// <summary>Random floor monster: the floor table in the first tower, the four groups in the second.</summary>
        private MonsterSpecies RollTowerSpecies()
        {
            if (!InSecondTower)
            {
                _secondTowerRolledGroup = -1;
                return TowerEncounterTable.Roll(_floor, _towerRun.Rng);
            }
            if (_secondTowerGroupsFloor != _floor) RollSecondTowerMonsterGroups();
            _secondTowerRolledGroup = RollSpawnRange(4);
            return _secondTowerGroups[_secondTowerRolledGroup];
        }

        private int? SecondTowerMonsterLevel(MonsterSpecies species)
        {
            if (!InSecondTower) return null;
            int rolled = _secondTowerRolledGroup;
            _secondTowerRolledGroup = -1;
            if (rolled >= 0 && _secondTowerGroups[rolled] == species) return _secondTowerLevels[rolled];
            for (int i = 0; i < _secondTowerGroups.Length; i++)
                if (_secondTowerGroups[i] == species) return _secondTowerLevels[i];
            return Mathf.Clamp(SecondTowerFloorNumber, 1, 99);
        }

        private string SecondTowerMusicCue => $"Tower{_secondTowerMusicGroup + 1}";

        /// <summary>Second-tower loot: retail weighted items (mode 2 table) and the floor gold budget.</summary>
        private void ApplySecondTowerLoot()
        {
            var dungeon = _towerFloorLayout?.Dungeon;
            if (dungeon == null) return;
            var items = dungeon.FloorItems;
            for (int i = 0; i < items.Length; i++)
                if (items[i] != null && items[i].Kind != MonsterEgg.Kind)
                    items[i] = SecondTowerItems.RollLoot(RollSpawnRange);

            int budget = SecondTowerFloorNumber * 200;
            int swing = RollSpawnRange(budget / 2 + 1);
            budget += RollSpawnRange(2) == 0 ? swing : -swing;
            var cells = new List<Vector2Int>();
            foreach (var cell in _towerFloorLayout.OpenCells)
            {
                if (cell == _towerFloorLayout.StairsDownCell || cell == _towerFloorLayout.StairsUpCell ||
                    dungeon.Grid[cell.y, cell.x] != DungeonGenerator.TileType.Floor ||
                    _towerItemTraps.GetItemAt(cell) != null || _towerItemTraps.TrapAt(cell) != null) continue;
                cells.Add(cell);
            }
            while (budget >= 0 && cells.Count > 0)
            {
                int spent;
                DungeonItemEntry coin;
                if (budget >= 301)
                {
                    spent = 160 + RollSpawnRange(81);
                    coin = new DungeonItemEntry(ItemSystem.Id.GoldCoin, ItemSystem.Kind.Coin, (sbyte)(spent / 10), 1);
                }
                else if (budget >= 151)
                {
                    spent = 80 + RollSpawnRange(41);
                    coin = new DungeonItemEntry(ItemSystem.Id.SilverCoin, ItemSystem.Kind.Coin, (sbyte)spent, 0);
                }
                else
                {
                    spent = 16 + RollSpawnRange(9);
                    coin = new DungeonItemEntry(ItemSystem.Id.CopperCoin, ItemSystem.Kind.Coin, (sbyte)spent, 0);
                }
                int index = RollSpawnRange(cells.Count);
                var target = cells[index];
                cells.RemoveAt(index);
                if (!_towerItemTraps.TryPlaceItem(target, coin)) break;
                budget -= spent;
            }
        }

        /// <summary>Egg pool of the second tower: every tower egg, plus the mode-2-only Picket and U-Boat.</summary>
        private EggWeight[] SecondTowerEggChoices(EggFloor[] floors)
        {
            var seen = new HashSet<int>();
            var choices = new List<EggWeight>();
            if (floors != null)
                foreach (var floor in floors)
                    if (floor?.choices != null)
                        foreach (var choice in floor.choices)
                            if (choice.weight > 0 && seen.Add(choice.species))
                                choices.Add(new EggWeight { species = choice.species, weight = 1 });
            foreach (int species in new[] { (int)MonsterSpecies.U_BOAT, (int)MonsterSpecies.PICKET })
                if (seen.Add(species)) choices.Add(new EggWeight { species = species, weight = 1 });
            return choices.ToArray();
        }

        private void ShowNoMoreFloors() =>
            _message = T("No more floors above you!", "Não há mais andares acima de você!");
    }
}
