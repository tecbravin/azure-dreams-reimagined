using System;
using System.Collections.Generic;
using UnityEngine;
using CoreDungeon = AzureDreams.Core.DungeonGenerator;
using CoreItem = AzureDreams.Core.DungeonItemEntry;
using CoreItems = AzureDreams.Core.ItemSystem;
using CoreRng = AzureDreams.Core.RetailRng;

namespace AzureDreamsReimagined
{
    internal enum TowerTrapEffect
    {
        Slam, Rust, Poison, Frog, Blind, Chaos, Prison, Bomb, Bump, Crack,
        GoUp, Reversal, Seal, Sleep, Slow, Upheaval, Warp
    }

    public sealed class TowerTrapState
    {
        public bool IsRevealed { get; private set; }
        public bool IsActive { get; private set; } = true;
        public bool FirstTime { get; private set; } = true;
        internal TowerTrapEffect Effect { get; set; }
        private bool _discoveryAwarded;

        internal void Reveal() => IsRevealed = true;

        internal bool Discover()
        {
            if (!FirstTime || _discoveryAwarded) return false;
            Reveal();
            _discoveryAwarded = true;
            return true;
        }

        internal bool Spring()
        {
            if (!IsActive) return false;
            Reveal();
            IsActive = false;
            FirstTime = false;
            return true;
        }

        internal bool Rearm()
        {
            if (!IsRevealed || IsActive) return false;
            IsActive = true;
            return true;
        }
    }

    /// <summary>Tracks items and traps for the current dungeon and uses the run RNG to place them.</summary>
    public sealed class TowerItemTrapManager
    {
        private readonly TowerFloorManager _floor;
        private CoreRng _rng;
        private System.Random _rareLootRng;
        private readonly List<int> _pickedIndices = new List<int>();
        private readonly HashSet<Vector2Int> _triggeredTraps = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, TowerTrapState> _traps = new();
        private readonly Dictionary<Vector2Int, int> _cellToSlot = new Dictionary<Vector2Int, int>();
        private readonly Dictionary<Vector2Int, CoreDungeon.TileType> _trapTerrain = new Dictionary<Vector2Int, CoreDungeon.TileType>();
        private static readonly byte[] ProceduralSwords = { 2, 3, 4, 16 };
        private static readonly byte[] ProceduralShields = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };

        private CoreDungeon _trackedDungeon;

        private void EnsureFloor()
        {
            if (ReferenceEquals(_trackedDungeon, _floor.Dungeon)) return;
            _trackedDungeon = _floor.Dungeon;
            _rareLootRng = new System.Random(_floor.Seed ^ 0x4B5A3);
            _pickedIndices.Clear();
            _triggeredTraps.Clear();
            _traps.Clear();
            _cellToSlot.Clear();
            _trapTerrain.Clear();
        }

        private List<Vector2Int> PlacementCandidates(bool allowCorridors)
        {
            var cells = new List<Vector2Int>();
            const CoreDungeon.TileFlag occupied = CoreDungeon.TileFlag.HasItem | CoreDungeon.TileFlag.HasTrap |
                CoreDungeon.TileFlag.HasActor | CoreDungeon.TileFlag.Blocked | CoreDungeon.TileFlag.Door;
            for (int y = 0; y < CoreDungeon.GridSize; y++)
            for (int x = 0; x < CoreDungeon.GridSize; x++)
            {
                var cell = new Vector2Int(x, y);
                var terrain = _floor.Dungeon.Grid[y, x];
                if (terrain != CoreDungeon.TileType.Floor && !(allowCorridors && terrain == CoreDungeon.TileType.Corridor)) continue;
                if (cell == _floor.StairsDownCell || cell == _floor.StairsUpCell) continue;
                if ((_floor.Dungeon.GetFlags(x, y) & (ushort)occupied) != 0) continue;
                if (_cellToSlot.ContainsKey(cell) || _traps.ContainsKey(cell) || _triggeredTraps.Contains(cell)) continue;
                cells.Add(cell);
            }
            return cells;
        }

        private Vector2Int TakeCandidate(List<Vector2Int> cells)
        {
            int index = _rng.Next() % cells.Count;
            var cell = cells[index];
            cells.RemoveAt(index);
            return cell;
        }

        public TowerItemTrapManager(TowerFloorManager floor, int seed)
        {
            if (floor == null) throw new ArgumentNullException(nameof(floor));
            _floor = floor;
            _rng = new CoreRng((uint)seed);
            RegisterExistingItems();
        }

        public TowerItemTrapManager(TowerFloorManager floor, CoreRng rng)
        {
            if (floor == null) throw new ArgumentNullException(nameof(floor));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            _floor = floor;
            _rng = rng;
            RegisterExistingItems();
        }

        /// <summary>Use the run's RNG stream for deterministic spawns.</summary>
        public void UseRunRng(CoreRng rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            _rng = rng;
        }

        /// <summary>Number of floor item slots.</summary>
        public int Capacity => CoreDungeon.MaxFloorItems;

        /// <summary>Current floor manager this manager operates on.</summary>
        public TowerFloorManager Floor => _floor;

        internal const int CherrlHealingHerbFloor = 28;

        internal static bool IsCherrlHealingHerb(CoreItem item) =>
            item != null && item.Kind == CoreItems.Kind.Herb && item.Subtype == CoreItems.Id.HealingHerb;

        internal void ConfigureCherrlHealingHerb(bool questCompleted)
        {
            RegisterExistingItems();
            var items = _floor.Dungeon.FloorItems;
            for (int i = 0; i < items.Length; i++)
                if (IsCherrlHealingHerb(items[i])) items[i] = CoreItems.CreateFloorItem(1, CoreItems.Kind.Herb, 20);

            if (_floor.CurrentFloor != CherrlHealingHerbFloor || questCompleted) return;
            var herb = CoreItems.CreateFloorItem(CoreItems.Id.HealingHerb, CoreItems.Kind.Herb, 0);
            TryPlaceGeneratedRoomItem(herb, seedSalt: 0x28C4);
        }

        internal void TryPlaceGeneratedRoomItem(CoreItem item, int seedSalt)
        {
            var candidates = PlacementCandidates(allowCorridors: false);
            if (candidates.Count == 0) return;
            var random = new System.Random(_floor.Seed ^ seedSalt);
            TryPlaceItem(candidates[random.Next(candidates.Count)], item);
        }

        internal void ClearGeneratedLoot()
        {
            var dungeon = _floor.Dungeon;
            Array.Clear(dungeon.FloorItems, 0, dungeon.FloorItems.Length);
            for (int y = 0; y < CoreDungeon.GridSize; y++)
            for (int x = 0; x < CoreDungeon.GridSize; x++)
                dungeon.Flags[y, x] &= unchecked((ushort)~(ushort)CoreDungeon.TileFlag.HasItem);
            _cellToSlot.Clear();
        }

        internal void AddEquipmentToGeneratedLoot()
        {
            // Preserve item positions, counts and the run RNG while supplementing the existing loot table.
            var random = new System.Random(_floor.Seed ^ 0x6A17D);
            var items = _floor.Dungeon.FloorItems;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;
                int roll = random.Next(100);
                var rare = RollRareFloorItem();
                if (roll < 5)
                    items[i] = CreateTowerEquipment(CoreItems.Kind.Sword,
                        ProceduralSwords[random.Next(ProceduralSwords.Length)], random.Next(100));
                else if (roll < 10)
                    items[i] = CreateTowerEquipment(CoreItems.Kind.Shield,
                        ProceduralShields[random.Next(ProceduralShields.Length)], random.Next(100));
                else if (roll < 15)
                    items[i] = CoreItems.CreateFloorItem(5, CoreItems.Kind.Scroll, 0);
                if (rare != null) items[i] = rare;
            }
        }

        internal static CoreItem CreateTowerEquipment(byte kind, byte subtype, int roll)
        {
            sbyte modifier = roll switch
            {
                < 60 => 0,
                < 75 => 1,
                < 85 => 2,
                < 90 => 3,
                _ => -1
            };
            byte flags = Inventory.UnrevealedEquipmentFlag;
            if (modifier == -1 || (kind == CoreItems.Kind.Sword && subtype == TowerGameController.DarkSword))
                flags |= Inventory.CursedEquipmentFlag;
            return new CoreItem(subtype, kind, modifier, flags);
        }

        /// <summary>
        /// Spawn a fresh set of items on the current floor using the run RNG.
        /// Returns the list of (cell, item) placements.
        /// </summary>
        public List<(Vector2Int cell, CoreItem item)> SpawnItems(int count)
        {
            var result = new List<(Vector2Int, CoreItem)>();
            if (_floor.Dungeon == null) return result;
            RegisterExistingItems();
            if (count < 0) count = 0;
            if (count > CoreDungeon.MaxFloorItems) count = CoreDungeon.MaxFloorItems;

            var candidates = PlacementCandidates(allowCorridors: false);
            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                var cell = TakeCandidate(candidates);
                var item = SelectItem();
                if (item == null) continue;
                int idx = FirstFreeSlot();
                if (idx < 0) break;
                _floor.Dungeon.FloorItems[idx] = item;
                _floor.Dungeon.SetFlag(cell.x, cell.y, CoreDungeon.TileFlag.HasItem);
                _cellToSlot[cell] = idx;
                result.Add((cell, item));
            }
            return result;
        }

        private static readonly TowerTrapEffect[] GeneratedTrapEffects =
        {
            TowerTrapEffect.Slam, TowerTrapEffect.Blind, TowerTrapEffect.Poison,
            TowerTrapEffect.Frog, TowerTrapEffect.Rust, TowerTrapEffect.Chaos,
            TowerTrapEffect.Prison, TowerTrapEffect.Bomb, TowerTrapEffect.Bump,
            TowerTrapEffect.Crack, TowerTrapEffect.Upheaval, TowerTrapEffect.Seal,
            TowerTrapEffect.Sleep, TowerTrapEffect.Slow, TowerTrapEffect.Warp,
            TowerTrapEffect.Reversal, TowerTrapEffect.GoUp
        };

        private static TowerTrapEffect GeneratedTrapEffect(int floor, int index) =>
            floor <= 2 ? TowerTrapEffect.Slam :
            GeneratedTrapEffects[((floor - 3) * 5 + index) % GeneratedTrapEffects.Length];

        /// <summary>
        /// Spawn traps on the current floor. Returns placed trap cells.
        /// </summary>
        public List<Vector2Int> SpawnTraps(int count)
        {
            var result = new List<Vector2Int>();
            if (_floor.Dungeon == null) return result;
            if (count < 0) count = 0;

            RegisterExistingItems();
            var candidates = PlacementCandidates(allowCorridors: true);
            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                var cell = TakeCandidate(candidates);
                _trapTerrain[cell] = _floor.Dungeon.Grid[cell.y, cell.x];
                _floor.Dungeon.Grid[cell.y, cell.x] = CoreDungeon.TileType.Trap;
                _floor.Dungeon.SetFlag(cell.x, cell.y, CoreDungeon.TileFlag.HasTrap);
                _traps.Add(cell, new TowerTrapState { Effect = GeneratedTrapEffect(_floor.CurrentFloor, i) });
                result.Add(cell);
            }
            return result;
        }

        /// <summary>
        /// Attempt to pick up the item on the given cell. Returns the item if
        /// picked (and clears the slot/flag), else null.
        /// </summary>
        public CoreItem TryPickup(Vector2Int cell) => TakeItem(cell, true);

        internal bool IsCorridor(Vector2Int cell) =>
            _trapTerrain.TryGetValue(cell, out var terrain) ? terrain == CoreDungeon.TileType.Corridor :
                _floor.Dungeon.Grid[cell.y, cell.x] == CoreDungeon.TileType.Corridor;

        internal CoreItem GetItemAt(Vector2Int cell)
        {
            EnsureFloor();
            return _cellToSlot.TryGetValue(cell, out int slot) ? _floor.Dungeon.FloorItems[slot] : null;
        }

        internal CoreItem TakeItem(Vector2Int cell, bool countPickup = false)
        {
            if (_floor.Dungeon == null) return null;
            RegisterExistingItems();
            if (cell.x < 0 || cell.y < 0 || cell.x >= CoreDungeon.GridSize || cell.y >= CoreDungeon.GridSize) return null;
            if ((_floor.Dungeon.GetFlags(cell.x, cell.y) & (ushort)CoreDungeon.TileFlag.HasItem) == 0) return null;
            if (!_cellToSlot.TryGetValue(cell, out int slot)) return null;

            var it = _floor.Dungeon.FloorItems[slot];
            if (it == null) return null;
            _floor.Dungeon.FloorItems[slot] = null;
            _floor.Dungeon.Flags[cell.y, cell.x] &= (ushort)~CoreDungeon.TileFlag.HasItem;
            _cellToSlot.Remove(cell);
            if (countPickup) _pickedIndices.Add(slot);
            return it;
        }

        internal bool TryPlaceItem(Vector2Int cell, CoreItem item)
        {
            RegisterExistingItems();
            if (!_floor.IsWalkable(cell.x, cell.y) || _cellToSlot.ContainsKey(cell)) return false;
            int slot = FirstFreeSlot();
            if (slot < 0) return false;
            _floor.Dungeon.FloorItems[slot] = item;
            _floor.Dungeon.SetFlag(cell.x, cell.y, CoreDungeon.TileFlag.HasItem);
            _cellToSlot.Add(cell, slot);
            return true;
        }

        /// <summary>
        /// Trigger the trap on the given cell if present and not already triggered.
        /// Returns true if a trap was triggered.
        /// </summary>
        public bool TryTriggerTrap(Vector2Int cell)
        {
            if (TrapAt(cell)?.Spring() != true) return false;
            _triggeredTraps.Add(cell);
            _floor.Dungeon.Grid[cell.y, cell.x] = _trapTerrain.TryGetValue(cell, out var original)
                ? original : CoreDungeon.TileType.Floor;
            _floor.Dungeon.Flags[cell.y, cell.x] &= (ushort)~CoreDungeon.TileFlag.HasTrap;
            return true;
        }

        public TowerTrapState TrapAt(Vector2Int cell)
        {
            EnsureFloor();
            if (_traps.TryGetValue(cell, out var trap)) return trap;
            if (_floor.Dungeon == null || cell.x < 0 || cell.y < 0 ||
                cell.x >= CoreDungeon.GridSize || cell.y >= CoreDungeon.GridSize ||
                _floor.Dungeon.Grid[cell.y, cell.x] != CoreDungeon.TileType.Trap) return null;
            trap = new TowerTrapState();
            _traps.Add(cell, trap);
            return trap;
        }

        public bool TryRearmTrap(Vector2Int cell)
        {
            if (TrapAt(cell)?.Rearm() != true) return false;
            _floor.Dungeon.Grid[cell.y, cell.x] = CoreDungeon.TileType.Trap;
            _floor.Dungeon.SetFlag(cell.x, cell.y, CoreDungeon.TileFlag.HasTrap);
            return true;
        }

        /// <summary>Whether a trap on this cell has already been triggered.</summary>
        public bool IsTrapTriggered(Vector2Int cell) { EnsureFloor(); return _triggeredTraps.Contains(cell); }

        /// <summary>Number of items picked up this floor.</summary>
        public int PickedCount { get { EnsureFloor(); return _pickedIndices.Count; } }

        /// <summary>Number of traps triggered this floor.</summary>
        public int TriggeredTrapCount { get { EnsureFloor(); return _triggeredTraps.Count; } }

        /// <summary>Reset counters; preserve ownership of live items/traps on the same dungeon.</summary>
        public void ResetFloor()
        {
            EnsureFloor();
            _pickedIndices.Clear();
            _triggeredTraps.Clear();
        }

        private void RegisterExistingItems()
        {
            EnsureFloor();
            if (_floor.Dungeon == null) return;
            var usedSlots = new HashSet<int>(_cellToSlot.Values);
            for (int y = 0; y < CoreDungeon.GridSize; y++)
            for (int x = 0; x < CoreDungeon.GridSize; x++)
            {
                var cell = new Vector2Int(x, y);
                if (_cellToSlot.ContainsKey(cell)) continue;
                if ((_floor.Dungeon.GetFlags(x, y) & (ushort)CoreDungeon.TileFlag.HasItem) == 0) continue;
                int slot = -1;
                for (int i = 0; i < CoreDungeon.MaxFloorItems; i++)
                {
                    if (usedSlots.Contains(i)) continue;
                    if (_floor.Dungeon.FloorItems[i] != null) { slot = i; break; }
                }
                if (slot < 0) break;
                _cellToSlot[cell] = slot;
                usedSlots.Add(slot);
            }
        }

        private int FirstFreeSlot()
        {
            if (_floor.Dungeon == null) return -1;
            for (int i = 0; i < CoreDungeon.MaxFloorItems; i++)
                if (_floor.Dungeon.FloorItems[i] == null) return i;
            return -1;
        }

        // NON-RETAIL FALLBACK: SelectItem uses a 4-way modulo roll. Retail uses
        // rarity-weighted selection (build_cumulative_item_weights / select_weighted_floor_item
        // in spawn.c) with definition flags and floor-specific overrides. Tables not yet
        // ported. Do not claim retail fidelity for item spawns.
        private CoreItem RollRareFloorItem()
        {
            // Five distinct outcomes out of 5,000, one per rare item.
            return _rareLootRng.Next(5000) switch
            {
                0 => CreateTowerEquipment(CoreItems.Kind.Sword, 15, _rareLootRng.Next(100)), // Bow Gun
                1 => CreateTowerEquipment(CoreItems.Kind.Sword, 9, _rareLootRng.Next(100)), // Dark Sword
                2 => CreateTowerEquipment(CoreItems.Kind.Sword, 10, _rareLootRng.Next(100)), // Holy Sword
                3 => CoreItems.CreateFloorItem(7, CoreItems.Kind.Fruit, 0), // Roche Fruit
                4 => CreateTowerEquipment(CoreItems.Kind.Wand, 2, _rareLootRng.Next(100)), // Trained Wand
                _ => null
            };
        }

        private CoreItem SelectItem()
        {
            int r = _rng.Next() % 4;
            return RollRareFloorItem() ?? (r switch
            {
                0 => CoreItems.CreateFloorItem(1, CoreItems.Kind.Herb, 20),
                1 => CoreItems.CreateFloorItem(CoreItems.Id.PitaFruit, CoreItems.Kind.Fruit, 40),
                2 => CoreItems.CreateFloorItem(CoreItems.Id.LightSeed, CoreItems.Kind.Seed, 0),
                _ => CoreItems.CreateFloorItem(CoreItems.Id.CopperCoin, CoreItems.Kind.Coin, 10),
            });
        }
    }
}
