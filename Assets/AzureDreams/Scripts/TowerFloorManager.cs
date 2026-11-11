using System;
using System.Collections.Generic;
using UnityEngine;
using CoreDungeon = AzureDreams.Core.DungeonGenerator;
using CoreRng = AzureDreams.Core.RetailRng;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Tower floor manager — owns floor generation/loading, layout (cells, heights,
    /// stairs) and progression between floors. Wraps the pure core
    /// <see cref="CoreDungeon"/> generator and exposes query helpers used by the
    /// tower controller and the item/trap manager. State is deterministic for a
    /// given (floorNumber, seed) pair so runs can be replayed and persisted.
    ///
    /// INTEGRATION POINT (Coder): TowerGameController.BuildTowerFloor() should call
    /// Generate(floor, seed) and apply the resulting layout; CompleteTowerCell()
    /// stair handling should call AdvanceFloor() then Generate(floor, seed).
    /// Do not mutate TowerGameController*.cs without a contract/diff first.
    /// </summary>
    public sealed class TowerFloorManager
    {
        internal const int TerrainBlockHeight = 32;
        private readonly List<Vector2Int> _openCells = new List<Vector2Int>();

        /// <summary>Current floor number (1-based inside the tower).</summary>
        public int CurrentFloor { get; private set; } = 1;

        /// <summary>Seed used for the current floor's generation.</summary>
        public int Seed { get; private set; }

        /// <summary>The generated dungeon for the current floor (core, pure).</summary>
        public CoreDungeon Dungeon { get; private set; }

        /// <summary>Stairs-down cell (entry) for the current floor.</summary>
        public Vector2Int StairsDownCell { get; private set; }

        /// <summary>Stairs-up cell (exit to next floor) for the current floor.</summary>
        public Vector2Int StairsUpCell { get; private set; }

        /// <summary>Per-cell ground height (flat = 0). Populated by generation.</summary>
        public int[,] Heights { get; private set; }

        /// <summary>Whether the current floor has been generated.</summary>
        public bool IsGenerated => Dungeon != null;

        /// <summary>
        /// Generate (or regenerate) a floor. Deterministic for (floorNumber, seed).
        /// </summary>
        public void Generate(int floorNumber, int seed)
        {
            if (floorNumber < 1) floorNumber = 1;
            if (floorNumber == 2)
            {
                // NTSC-U DUNGEON.BIN sector 3995, fixed encounter layout.
                LoadFixedTerrain(Resources.Load<TextAsset>("Town/Retail/floor_02").bytes,
                    floorNumber, seed, new Vector2Int(31, 56), new Vector2Int(55, 9));
                return;
            }
            if (floorNumber == TowerFloorRules.BeldoFloor)
            {
                GenerateBeldoArena(seed);
                return;
            }
            CurrentFloor = floorNumber;
            Seed = seed;
            Dungeon = new CoreDungeon(seed, floorNumber);
            Dungeon.GenerateTowerLayout(TowerFloorRules.AllowsRandomMonsters(floorNumber));

            Heights = new int[CoreDungeon.GridSize, CoreDungeon.GridSize];
            LocateStairs();
            RebuildOpenCells();
            GenerateHeightPatches(seed);
        }

        private void GenerateBeldoArena(int seed)
        {
            // A single fixed throne room with a narrow approach and a raised dais.
            const int cellSize = 6;
            int size = CoreDungeon.GridSize;
            var terrain = new byte[size * size * cellSize];
            for (int y = 15; y <= 50; y++)
                for (int x = 19; x <= 44; x++)
                {
                    bool room = y <= 40;
                    if (!room && (x < 30 || x > 33)) continue;
                    int offset = (y * size + x) * cellSize;
                    terrain[offset] = 1;
                    if (y <= 21 && x >= 25 && x <= 38) terrain[offset + 2] = TerrainBlockHeight;
                }
            LoadFixedTerrain(terrain, TowerFloorRules.BeldoFloor, seed, new Vector2Int(31, 49), new Vector2Int(32, 17));
        }

        private void GenerateHeightPatches(int seed)
        {
            // Use a separate stream so elevation leaves room and item rolls intact.
            var random = new System.Random(seed ^ 0x52A71);
            for (int i = 0; i < Dungeon.RoomCount; i++)
            {
                var room = Dungeon.Rooms[i];
                int count = Mathf.Max(1, (room.Width * room.Height) / 24);
                for (int patch = 0; patch < count; patch++)
                {
                    int width = random.Next(1, Mathf.Min(5, room.Width) + 1);
                    int height = random.Next(1, Mathf.Min(5, room.Height) + 1);
                    int x = room.X + random.Next(room.Width - width + 1);
                    int y = room.Y + random.Next(room.Height - height + 1);
                    int elevation = random.Next(2) == 0 ? -TerrainBlockHeight : TerrainBlockHeight;
                    for (int row = y; row < y + height; row++)
                    for (int column = x; column < x + width; column++)
                        if (Dungeon.Grid[row, column] == CoreDungeon.TileType.Floor)
                            Heights[row, column] = elevation;
                }
            }
        }

        internal void GenerateTutorial(byte[] terrain, Vector2Int entrance, Vector2Int exit)
            => LoadFixedTerrain(terrain, 1, 173, entrance, exit);

        private void LoadFixedTerrain(byte[] terrain, int floor, int seed, Vector2Int entrance, Vector2Int exit)
        {
            const int cellSize = 6;
            int size = CoreDungeon.GridSize;
            if (terrain == null || terrain.Length != size * size * cellSize)
                throw new ArgumentException("Invalid retail fixed-floor map.", nameof(terrain));
            CurrentFloor = floor;
            Seed = seed;
            Dungeon = new CoreDungeon(Seed, CurrentFloor);
            Heights = new int[size, size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int offset = (y * size + x) * cellSize;
                    int kind = terrain[offset] | terrain[offset + 1] << 8;
                    Heights[y, x] = (short)(terrain[offset + 2] | terrain[offset + 3] << 8);
                    bool blocked = kind is 0 or 3 or 102 or 104;
                    if (floor == 2)
                    {
                        int flags = terrain[offset + 4] | terrain[offset + 5] << 8;
                        // Keep the central doorway traversable in the Unity event flow.
                        bool doorway = y == 35 && (x == 31 || x == 32);
                        blocked |= (!doorway && (flags & 0x8400) != 0) || Heights[y, x] >= 192;
                    }
                    Dungeon.Grid[y, x] = blocked ? CoreDungeon.TileType.Wall : CoreDungeon.TileType.Floor;
                }
            Dungeon.Grid[entrance.y, entrance.x] = CoreDungeon.TileType.StairsDown;
            Dungeon.Grid[exit.y, exit.x] = CoreDungeon.TileType.StairsUp;
            LocateStairs();
            RebuildOpenCells();
        }

        internal bool TryMoveEntranceToWall(out Vector2Int inward)
        {
            inward = Vector2Int.zero;
            var entry = StairsDownCell;
            var best = entry;
            int bestDistance = int.MaxValue;
            var directions = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            foreach (var cell in _openCells)
            {
                var kind = Dungeon.Grid[cell.y, cell.x];
                if (kind is not (CoreDungeon.TileType.Floor or CoreDungeon.TileType.StairsDown) ||
                    Dungeon.GetFlags(cell.x, cell.y) != 0) continue;
                int distance = Mathf.Abs(cell.x - entry.x) + Mathf.Abs(cell.y - entry.y);
                if (distance >= bestDistance) continue;
                foreach (var direction in directions)
                {
                    var outside = cell - direction;
                    var inside = cell + direction;
                    if (IsWalkable(outside.x, outside.y) || !IsWalkable(inside.x, inside.y)) continue;
                    best = cell;
                    inward = direction;
                    bestDistance = distance;
                    break;
                }
            }
            if (inward == Vector2Int.zero) return false;
            Dungeon.Grid[entry.y, entry.x] = CoreDungeon.TileType.Floor;
            Dungeon.Grid[best.y, best.x] = CoreDungeon.TileType.StairsDown;
            Heights[best.y, best.x] = 0;
            StairsDownCell = best;
            return true;
        }

        /// <summary>Advance to the next floor (does not regenerate; caller calls Generate).</summary>
        public void AdvanceFloor() { if (CurrentFloor < int.MaxValue) CurrentFloor++; }

        /// <summary>Live four-neighbour terrain connectivity; does not repair layouts or model actors.</summary>
        public bool HasReachableExit
        {
            get
            {
                if (Dungeon == null ||
                    Dungeon.Grid[StairsDownCell.y, StairsDownCell.x] != CoreDungeon.TileType.StairsDown ||
                    Dungeon.Grid[StairsUpCell.y, StairsUpCell.x] != CoreDungeon.TileType.StairsUp) return false;
                var seen = new bool[CoreDungeon.GridSize, CoreDungeon.GridSize];
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(StairsDownCell);
                seen[StairsDownCell.y, StairsDownCell.x] = true;
                var steps = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    if (cell == StairsUpCell) return true;
                    foreach (var step in steps)
                    {
                        var next = cell + step;
                        if (!IsWalkable(next.x, next.y) || seen[next.y, next.x]) continue;
                        seen[next.y, next.x] = true;
                        queue.Enqueue(next);
                    }
                }
                return false;
            }
        }

        /// <summary>Reset to floor 1.</summary>
        public void Reset() { CurrentFloor = 1; Seed = 0; StairsDownCell = Vector2Int.zero; StairsUpCell = Vector2Int.zero; Dungeon = null; Heights = null; _openCells.Clear(); }

        /// <summary>Is the cell walkable on the current floor?</summary>
        public bool IsWalkable(int x, int y)
        {
            if (Dungeon == null) return false;
            if (x < 0 || y < 0 || x >= CoreDungeon.GridSize || y >= CoreDungeon.GridSize) return false;
            var t = Dungeon.Grid[y, x];
            return t == CoreDungeon.TileType.Floor || t == CoreDungeon.TileType.Corridor ||
                   t == CoreDungeon.TileType.StairsDown || t == CoreDungeon.TileType.StairsUp ||
                   t == CoreDungeon.TileType.Trap;
        }

        /// <summary>Is the cell a wall/empty (blocked)?</summary>
        public bool IsBlocked(int x, int y) => !IsWalkable(x, y);

        /// <summary>Ground height at a cell (flat = 0).</summary>
        public int GroundHeight(int x, int y)
        {
            if (Heights == null) return 0;
            if (x < 0 || y < 0 || x >= CoreDungeon.GridSize || y >= CoreDungeon.GridSize) return 0;
            return Heights[y, x];
        }

        /// <summary>Set ground height for a cell (used by height integration).</summary>
        public void SetHeight(int x, int y, int height)
        {
            if (Heights == null) return;
            if (x < 0 || y < 0 || x >= CoreDungeon.GridSize || y >= CoreDungeon.GridSize) return;
            Heights[y, x] = height;
        }

        /// <summary>Read-only snapshot of open (walkable) cells.</summary>
        public IReadOnlyList<Vector2Int> OpenCells => _openCells;

        /// <summary>Find an open cell preferring the given one, avoiding blocked cells.</summary>
        public Vector2Int FindOpenCell(Vector2Int preferred, params Vector2Int[] blocked)
        {
            if (IsWalkable(preferred.x, preferred.y) && NotBlocked(preferred, blocked))
                return preferred;
            foreach (var c in _openCells)
            {
                if (IsWalkable(c.x, c.y) && NotBlocked(c, blocked)) return c;
            }
            // Fallback: stairs down cell if walkable+not-blocked; never return a blocked
            // open cell (callers re-check IsWalkable and skip). Last resort: zero.
            if (IsWalkable(StairsDownCell.x, StairsDownCell.y) && NotBlocked(StairsDownCell, blocked))
                return StairsDownCell;
            return new Vector2Int(0, 0);
        }

        /// <summary>Room count of the current floor.</summary>
        public int RoomCount => Dungeon != null ? Dungeon.RoomCount : 0;

        /// <summary>Monster count placed on the current floor.</summary>
        public int MonsterCount => Dungeon != null ? Dungeon.MonsterCount : 0;

        /// <summary>Item count placed on the current floor.</summary>
        public int ItemCount => Dungeon != null ? Dungeon.ItemCount : 0;

        /// <summary>Stairs-down position as a Vector2Int (x,y).</summary>
        public Vector2Int StairsDown => StairsDownCell;

        /// <summary>Stairs-up position as a Vector2Int (x,y).</summary>
        public Vector2Int StairsUp => StairsUpCell;

        private void LocateStairs()
        {
            StairsDownCell = Vector2Int.zero;
            StairsUpCell = Vector2Int.zero;
            if (Dungeon == null) return;
            for (int y = 0; y < CoreDungeon.GridSize; y++)
            {
                for (int x = 0; x < CoreDungeon.GridSize; x++)
                {
                    if (Dungeon.Grid[y, x] == CoreDungeon.TileType.StairsDown) StairsDownCell = new Vector2Int(x, y);
                    if (Dungeon.Grid[y, x] == CoreDungeon.TileType.StairsUp) StairsUpCell = new Vector2Int(x, y);
                }
            }
        }

        private void RebuildOpenCells()
        {
            _openCells.Clear();
            if (Dungeon == null) return;
            for (int y = 0; y < CoreDungeon.GridSize; y++)
                for (int x = 0; x < CoreDungeon.GridSize; x++)
                    if (IsWalkable(x, y)) _openCells.Add(new Vector2Int(x, y));
        }

        private static bool NotBlocked(Vector2Int cell, Vector2Int[] blocked)
        {
            if (blocked == null) return true;
            for (int i = 0; i < blocked.Length; i++)
                if (cell == blocked[i]) return false;
            return true;
        }
    }
}
