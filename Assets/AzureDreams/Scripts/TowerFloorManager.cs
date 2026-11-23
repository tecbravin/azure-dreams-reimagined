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

        /// <summary>Retail cell data of the current floor (rendering and terrain), when built from retail data.</summary>
        public RetailTowerFloor Retail { get; private set; }

        // setup_dungeon_floor_encounters: floor 1 rolls events 3-6, i.e. fixed layouts 3, 6, 7 and 8.
        private static readonly int[] FirstFloorLayouts = { 3, 6, 7, 8 };

        /// <summary>Whether the current floor has been generated.</summary>
        public bool IsGenerated => Dungeon != null;

        /// <summary>
        /// Generate (or regenerate) a floor. Deterministic for (floorNumber, seed).
        /// </summary>
        public void Generate(int floorNumber, int seed)
        {
            if (floorNumber < 1) floorNumber = 1;
            Retail = null;
            if (TryGenerateRetail(floorNumber, seed)) return;
            if (floorNumber == 2)
            {
                // NTSC-U DUNGEON.BIN sector 3995, fixed encounter layout.
                var asset = AssetMode.Load<TextAsset>("Town/Retail/floor_02");
                var terrain = asset ? asset.bytes : null;
                if (terrain == null || terrain.Length != CoreDungeon.GridSize * CoreDungeon.GridSize * 6)
                    terrain = ReadEmbeddedSecondFloor();
                LoadFixedTerrain(terrain,
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

        /// <summary>
        /// Retail floors: fixed layouts for floors 1, 2 and 40 (setup_run_and_load_track track records) and the
        /// retail random generator (generate_floor_layout) with the floor's texture variant everywhere else.
        /// </summary>
        private bool TryGenerateRetail(int floorNumber, int seed)
        {
            RetailTowerFloor retail;
            Vector2Int entrance, exit;
            if (floorNumber == 2)
            {
                // NTSC-U DUNGEON.BIN layout 2 (sector 3995): the fixed encounter floor.
                retail = RetailTowerFloor.CreateFixed(2, false);
                entrance = new Vector2Int(31, 56);
                exit = new Vector2Int(55, 9);
                if (retail != null)
                {
                    // The second-tower elevator takes the matching centre of the
                    // north-west ring: give it the lift pad tile (kind 1) of (55, 9).
                    int pad = retail.Index(TowerGameController.SecondTowerElevatorCell.x, TowerGameController.SecondTowerElevatorCell.y);
                    retail.Kind[pad] = retail.Kind[retail.Index(exit.x, exit.y)];
                    retail.Value[pad] = retail.Value[retail.Index(exit.x, exit.y)];
                    retail.Flags[pad] = retail.Flags[retail.Index(exit.x, exit.y)];
                }
            }
            else if (floorNumber == TowerFloorRules.BeldoFloor)
            {
                // Layout 1 with the 0x2A arena tileset; Beldo stands at (32, 24).
                retail = RetailTowerFloor.CreateFixed(1, true);
                entrance = new Vector2Int(32, 49);
                exit = new Vector2Int(32, 26);
            }
            else if (floorNumber == 1)
            {
                retail = RetailTowerFloor.CreateFixed(FirstFloorLayouts[new CoreRng(unchecked((uint)seed)).Next() & 3], false);
                entrance = new Vector2Int(31, 61);
                exit = new Vector2Int(31, 17);
                if (retail != null)
                    foreach (var marker in retail.Markers)
                        if (marker.Kind == 2) exit = marker.Cell;
            }
            else
            {
                // Floor 31 carries the Beldo vision record, which disables the den room (dungeon_floor_flags).
                retail = RetailTowerFloor.CreateRandom(floorNumber, seed, floorNumber == 31);
                entrance = exit = default;
                if (retail != null && !PickRetailStairs(retail, seed, out entrance, out exit)) retail = null;
            }
            if (retail == null) return false;
            BuildFromRetail(retail, floorNumber, seed, entrance, exit);
            return true;
        }

        // place_random_floor_elevators / select_dungeon_spawn_tile: the exit lift stands on a free room tile;
        // Koh arrives in a different room when the floor has more than one.
        private static bool PickRetailStairs(RetailTowerFloor retail, int seed, out Vector2Int entrance, out Vector2Int exit)
        {
            var random = new System.Random(seed ^ 0x5A17);
            entrance = exit = default;
            if (retail.Rooms.Count == 0) return false;
            bool Pick(int roomIndex, out Vector2Int cell)
            {
                var room = retail.Rooms[roomIndex];
                for (int attempt = 0; attempt < 200; attempt++)
                {
                    cell = new Vector2Int(room.x + random.Next(room.width), room.y + random.Next(room.height));
                    int i = retail.Index(cell.x, cell.y);
                    if (retail.IsWalkable(cell.x, cell.y) && retail.Kind[i] >= 19 && retail.Kind[i] <= 38) return true;
                }
                cell = default;
                return false;
            }
            int exitRoom = random.Next(retail.Rooms.Count);
            int entryRoom = retail.Rooms.Count > 1 ? (exitRoom + 1 + random.Next(retail.Rooms.Count - 1)) % retail.Rooms.Count : exitRoom;
            if (!Pick(exitRoom, out exit) || !Pick(entryRoom, out entrance)) return false;
            for (int attempt = 0; entrance == exit && attempt < 20; attempt++) Pick(entryRoom, out entrance);
            return entrance != exit;
        }

        private void BuildFromRetail(RetailTowerFloor retail, int floor, int seed, Vector2Int entrance, Vector2Int exit)
        {
            int size = CoreDungeon.GridSize;
            CurrentFloor = floor;
            Seed = seed;
            Retail = retail;
            Dungeon = new CoreDungeon(seed, floor);
            Heights = new int[size, size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool walkable = retail.IsWalkable(x, y);
                    // Keep the central doorway traversable in the Unity event flow.
                    if (floor == 2 && y == 35 && (x == 31 || x == 32)) walkable = true;
                    Dungeon.Grid[y, x] = !walkable ? CoreDungeon.TileType.Wall :
                        retail.InRoom(x, y) ? CoreDungeon.TileType.Floor : CoreDungeon.TileType.Corridor;
                    Heights[y, x] = retail.GroundHeight(x, y);
                }
            int rooms = Mathf.Min(retail.Rooms.Count, Dungeon.Rooms.Length);
            for (int i = 0; i < rooms; i++)
            {
                var room = retail.Rooms[i];
                Dungeon.Rooms[i] = new CoreDungeon.Room { X = room.x, Y = room.y, Width = room.width, Height = room.height };
            }
            SetRoomCount(Dungeon, rooms);
            Dungeon.Grid[entrance.y, entrance.x] = CoreDungeon.TileType.StairsDown;
            Dungeon.Grid[exit.y, exit.x] = CoreDungeon.TileType.StairsUp;
            LocateStairs();
            RebuildOpenCells();
        }

        // The pure core keeps RoomCount private to its own generator.
        private static void SetRoomCount(CoreDungeon dungeon, int count)
        {
            var property = typeof(CoreDungeon).GetProperty(nameof(CoreDungeon.RoomCount));
            var setter = property?.GetSetMethod(true);
            if (setter != null)
            {
                setter.Invoke(dungeon, new object[] { count });
                return;
            }
            typeof(CoreDungeon).GetField("<RoomCount>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(dungeon, count);
        }

        // Backup of floor_02.bytes (retail sector 3995), for players with an incomplete asset import.
        private static byte[] ReadEmbeddedSecondFloor()
        {
            const string encoded =
                "H4sIAAAAAAAC/+2ca47TMBDHg3YPsEfgK7A8LtAqqrQSb5b38ihCInW2SCwf9gI9GkfhCNxgobaAqf6Z6dhJHKd1PoysNI7HP0/G" +
                "Y2fSg6I4LFYHWQ4qi3xEPzL/lPiX9lyWcWQI/2otV+dWLqzMJEPJaPnTOy+tnNrf6r0fCyQz8SCznX8Fd3bksVzv2ShUYI0cH56M" +
                "xJ8jj0/Zvo0CRx7JbBsFib9hyLt2H8I9qSZmp/kbhjxHhjI0Cv70bjfsNTdJ3ae2/LX4eVUcPiPnb9vyHfB+uyeX0F9K5tqaTEHJ" +
                "UIabT0Ezfzq+t6w8hrqPYNzv2jP3xDlil8q0vzIZynDTPzTz57wK9WOu7O4/s2VZ3mfkLDHZRs+aeBsuYqHj4uv/ubJr/bT48Uc+" +
                "B/nJypf2mlfFr6u/8rU985mpFV+G6fnWypn62THq+ZebQajfmxD+c6vJiS2jfMDIk8Skr55zwp/jM22wfG38j6OAfn4GmlQwZy3E" +
                "aE0j0RIurBbfrKRlLlrWtyLruSBedw72j3yayPutf2tRQ9TEPWUuRnLSMJEYvUaWNCpwnC/XxdVlcUTk+swFGYWwVmQ9DcOfG7Vl" +
                "i/UvF/1ORPunOnOrlToo6pts8HfMSyKP/vGftmiFW1HK/Hk/3+X+m/G0f6NYoXM+kytr+Gvuo2/XKPj7r0C7tP8zkb8+ltbE3hr/" +
                "4xvDa+IW5H+WgP0/tvKdlU/gqdfsUIWVufm3j7awR9jroezfafKe0STMw6RfF3s9rP1zmuCT/r24vtUj0Wva7GxgXXdnua67Rm5X" +
                "trr49v+BaCLvxGKZ7lD5Eo5TF3uEvR42/jltEf/gDpWeZJy63Pz7MZn4Z94i/km/bEYe/6fvYeS66cf/Mv/0PYxc1+T176B1x27/" +
                "3a5/49fdbftPvzx2+6dPun4F1IeH0a/+8A37eO0/fQ8j191t/59+dLrb/n+869/07b+/XKz275R934sNZf+VuJ+vef/bhySW4PRZ" +
                "xWxXtn99Zrhv/jP6TNSkjpgH2Oadcpt2OftHPnJOsm/+M/pM1KRv6d4zcrlq7tc4mqD9c3MKNwpd5b/FzGR7IeaqvYmeNRea/9bM" +
                "X/6yoCLnF2L+Z992yOWq9f3ccb9WxGK5t074FMj5z3QNKGe5o9+7/D8z9rIzwHHobxfC9Qj9vEwG1+BG5E/r4hqQy/+nfq9qEU/q" +
                "aXD+X0+1jZ64O82RoQwxF1Hv/+Us9/bfv+BKjVufyv7f/aq5T1d6ymR8/b+c/yzn9lRbMh79VnZcLDEV/b9+nRvGvxa/fTsXv5tb" +
                "qvnLo7DtywK/HnF2JbeFs38lfjky6cj+NV+paMhr+Mv5z7os33D7l6M4A/zp+bBoMGwUcP7Sk/Hd/6k7Zc7dWbMC5fj73ifsfwO6" +
                "ItPEX3OUntdoelR4te5o08gHv+7x6ct6Bm86r+UfJsf7/xuONp15u/juuIjci7HzR/8zrl5k/pl/G/68/8/8x+j/M//sf8bOXxND" +
                "Zv7hEvZ7Syfx30Uy//7I23318guRxw2jkPl373PsHktp99VLOv/aPfbS7u2UJvPvQcJ+bEn9vyNf9bA3lfnLo9BEPvv/GKPA72fu" +
                "J/8UjtJzn3bII///czr8sxxK/ga6+5CdAGAAAA==";
            using var input = new System.IO.MemoryStream(System.Convert.FromBase64String(encoded));
            using var stream = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress);
            using var output = new System.IO.MemoryStream();
            stream.CopyTo(output);
            return output.ToArray();
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
        {
            // Use the same surfaces for the tutorial's actors, movement and mesh.
            // Raw cell values omit raised floor faces contained in the tileset.
            var retail = RetailTowerFloor.CreateFixed(9, false);
            if (retail != null) BuildFromRetail(retail, 1, 173, entrance, exit);
            else LoadFixedTerrain(terrain, 1, 173, entrance, exit);
        }

        private void LoadFixedTerrain(byte[] terrain, int floor, int seed, Vector2Int entrance, Vector2Int exit)
        {
            const int cellSize = 6;
            int size = CoreDungeon.GridSize;
            if (terrain == null || terrain.Length != size * size * cellSize)
                throw new ArgumentException("Invalid retail fixed-floor map.", nameof(terrain));
            CurrentFloor = floor;
            Seed = seed;
            Retail = null;
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
            if (Retail == null) Heights[best.y, best.x] = 0;
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
        public void Reset() { Retail = null; CurrentFloor = 1; Seed = 0; StairsDownCell = Vector2Int.zero; StairsUpCell = Vector2Int.zero; Dungeon = null; Heights = null; _openCells.Clear(); }

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
