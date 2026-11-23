using System;
using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Retail floor tileset (resource types 0x29/0x2A/0x2B, extracted by Tools/extract_tower_retail.py).
    /// Same CellFace / MapVertex / MapNormal tables that render_map_cell_mesh.c and
    /// sample_terrain_surface_height (terrain_surface_height_g00351.c) read.
    /// </summary>
    public sealed class RetailTowerGeometry
    {
        public const int FaceSize = 24;
        private static readonly Dictionary<string, RetailTowerGeometry> Cache = new();

        public byte[] Payload { get; private set; }
        public int Count { get; private set; }
        public int FaceTableEnd { get; private set; }
        public int[] Starts { get; private set; }
        public short[] Vertices { get; private set; } // x, y, z per vertex
        public short[] Normals { get; private set; }  // x, y, z per normal
        public byte[] Aux { get; private set; }
        public int VertexCount => Vertices.Length / 3;
        public int NormalCount => Normals.Length / 3;
        private readonly Dictionary<int, int[]> _reachable = new();

        public static RetailTowerGeometry Load(string name)
        {
            if (Cache.TryGetValue(name, out var cached)) return cached;
            var asset = AssetMode.Load<TextAsset>("Tower/Retail/" + name);
            if (!asset) return null;
            var data = asset.bytes;
            if (data.Length < 28 || data[0] != 'A' || data[1] != 'D' || data[2] != 'G' || data[3] != '1') return null;
            var geometry = new RetailTowerGeometry();
            int count = BitConverter.ToUInt16(data, 4);
            int faces = BitConverter.ToInt32(data, 8), vertices = BitConverter.ToInt32(data, 12);
            int normals = BitConverter.ToInt32(data, 16), aux = BitConverter.ToInt32(data, 20);
            int length = BitConverter.ToInt32(data, 24);
            var payload = new byte[length];
            Buffer.BlockCopy(data, 28, payload, 0, length);
            geometry.Payload = payload;
            geometry.Count = count;
            geometry.FaceTableEnd = faces;
            geometry.Starts = new int[count];
            for (int i = 0; i < count; i++) geometry.Starts[i] = BitConverter.ToInt32(payload, faces + i * 4);
            int vertexCount = (normals - vertices) / 8, normalCount = (aux - normals) / 8;
            geometry.Vertices = new short[vertexCount * 3];
            for (int i = 0; i < vertexCount; i++)
                for (int c = 0; c < 3; c++) geometry.Vertices[i * 3 + c] = BitConverter.ToInt16(payload, vertices + i * 8 + c * 2);
            geometry.Normals = new short[normalCount * 3];
            for (int i = 0; i < normalCount; i++)
                for (int c = 0; c < 3; c++) geometry.Normals[i * 3 + c] = BitConverter.ToInt16(payload, normals + i * 8 + c * 2);
            geometry.Aux = new byte[count];
            Buffer.BlockCopy(payload, aux, geometry.Aux, 0, Math.Min(count, payload.Length - aux));
            Cache[name] = geometry;
            return geometry;
        }

        public ushort U16(int offset) => BitConverter.ToUInt16(Payload, offset);
        public uint U32(int offset) => BitConverter.ToUInt32(Payload, offset);

        /// <summary>
        /// Every face record render_map_cell_mesh can reach for a tile from some viewpoint: both the drawn branch
        /// (skip alternates / continue) and the culled branch (step or jump past the opposite group) are followed.
        /// </summary>
        public int[] ReachableFaces(int tile)
        {
            if (tile <= 0 || tile >= Count) return Array.Empty<int>();
            if (_reachable.TryGetValue(tile, out var known)) return known;
            var seen = new HashSet<int>();
            var stack = new Stack<int>();
            var faces = new List<int>();
            stack.Push(Starts[tile]);
            while (stack.Count > 0)
            {
                int pos = stack.Pop();
                if (pos < 0 || pos + FaceSize > FaceTableEnd || !seen.Add(pos)) continue;
                uint info = U32(pos + 20);
                int control = (int)(info >> 16) & 0xFF;
                int flag = (sbyte)(info >> 24);
                int normal = U16(pos + 16);
                faces.Add(pos);
                if ((control & 0xF) == 1)
                {
                    if (flag >= 0) stack.Push(pos + ((control >> 4) + 1) * FaceSize);
                }
                else stack.Push(pos + FaceSize);
                int normalZ = normal < NormalCount ? Normals[normal * 3 + 2] : 0;
                if (normalZ < 0)
                {
                    if (((info >> 16) & 0x80FF) != 0x8001) stack.Push(pos + FaceSize);
                }
                else if ((control & 0xF0) != 0 || flag >= 0)
                    stack.Push(pos + Math.Max(1, control & 0xF) * FaceSize);
            }
            faces.Sort();
            var result = faces.ToArray();
            _reachable[tile] = result;
            return result;
        }

        private static int Ratan2(int y, int x) =>
            x == 0 && y == 0 ? 0 : (int)Math.Round(Math.Atan2(y, x) * 2048.0 / Math.PI);

        // are_quad_corner_turns_within_half_revolution: corners relative to the query point.
        private static bool QuadContains(int x0, int y0, int x1, int y1, int x2, int y2, int x3, int y3)
        {
            if (x0 == 0 && y0 == 0) return true;
            if (x3 == 0 && y3 == 0) return true;
            int a0 = Ratan2((short)y0, (short)x0), a1 = Ratan2((short)y3, (short)x3);
            int d1 = a1 - a0;
            if (((0x1000 - d1) & 0xFFF) >= 0x801) return false;
            if (x2 == 0 && y2 == 0) return true;
            int d2 = Ratan2((short)y2, (short)x2) - a0;
            if (((d1 - d2) & 0xFFF) >= 0x801) return false;
            if (x1 == 0 && y1 == 0) return true;
            int d3 = (Ratan2((short)y1, (short)x1) - a0) & 0xFFF;
            if (d3 >= 0x801) return false;
            return ((d2 - d3) & 0xFFF) < 0x801;
        }

        /// <summary>sample_terrain_surface_height: lowest surface (z down) at or below <paramref name="minimum"/>; 0x400 if none.</summary>
        public int SampleSurface(ushort[] kinds, short[] values, int x, int y, int minimum)
        {
            int localX = x & 0x3F, localY = y & 0x3F;
            int cellX = Mathf.Clamp((x & 0xFFFF) >> 6, 0, 63), cellY = Mathf.Clamp((y & 0xFFFF) >> 6, 0, 63);
            int index = cellX + (cellY << 6);
            int best = 0x400;
            int kind = kinds[index];
            if (kind == 0 || kind >= Count) return best;
            int offset = (short)-values[index];
            int pos = Starts[kind];
            for (int guard = 0; guard < 512 && pos + FaceSize <= FaceTableEnd; guard++)
            {
                int normal = U16(pos + 16);
                if (normal >= NormalCount || Normals[normal * 3 + 2] >= -2047) break;
                int control = Payload[pos + 22];
                int flag = (sbyte)Payload[pos + 23];
                if ((flag & 1) == 0)
                {
                    int v0 = U16(pos), v1 = U16(pos + 2), v2 = U16(pos + 4), v3 = U16(pos + 6);
                    if (v0 < VertexCount && v1 < VertexCount && v2 < VertexCount && v3 < VertexCount &&
                        QuadContains(
                            (short)(Vertices[v0 * 3] - localX), (short)(Vertices[v0 * 3 + 1] - localY),
                            (short)(Vertices[v1 * 3] - localX), (short)(Vertices[v1 * 3 + 1] - localY),
                            (short)(Vertices[v3 * 3] - localX), (short)(Vertices[v3 * 3 + 1] - localY),
                            (short)(Vertices[v2 * 3] - localX), (short)(Vertices[v2 * 3 + 1] - localY)))
                    {
                        int nx = Normals[normal * 3], ny = Normals[normal * 3 + 1], nz = Normals[normal * 3 + 2];
                        int height = (nx * (Vertices[v0 * 3] - localX) + ny * (Vertices[v0 * 3 + 1] - localY) +
                            nz * Vertices[v0 * 3 + 2]) / nz;
                        int cellHeight = height + offset;
                        if (cellHeight >= minimum && cellHeight < best) best = cellHeight;
                    }
                }
                if ((control & 0x0F) == 1)
                {
                    if (flag < 0) break;
                    if ((control & 0xF0) != 0) { pos += (control >> 4) * FaceSize + FaceSize; continue; }
                }
                pos += FaceSize;
            }
            return (short)best;
        }
    }

    /// <summary>
    /// One tower floor in the retail cell format (64x64 cells of kind, height value, flags), either generated by
    /// the retail random generator or loaded from a fixed layout. Transliteration of src/dungeon/level/generate.c:
    /// generate_floor_layout and the room/corridor/terrain helpers it calls.
    /// </summary>
    public sealed class RetailTowerFloor
    {
        public const int Size = 64;
        private static readonly int[] Dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
        private static readonly int[] Dy = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] OffsetX = { 64, 64, 0, -64, -64, -64, 0, 64 };
        private static readonly int[] OffsetY = { 0, 64, 64, 64, 0, -64, -64, -64 };
        private static readonly int[] Turns = { 2, -2 };

        public readonly ushort[] Kind = new ushort[Size * Size];
        public readonly short[] Value = new short[Size * Size];
        public readonly ushort[] Flags = new ushort[Size * Size];
        public readonly List<RectInt> Rooms = new();
        public readonly List<(int Kind, Vector2Int Cell)> Markers = new();
        public readonly List<(Vector2Int Cell, int Size)> Dens = new();
        public readonly List<Vector2Int> RoomItems = new();
        public readonly List<(Vector2Int Cell, int Kind)> RoomTraps = new();
        public RetailTowerGeometry Geometry { get; private set; }
        public string TextureSet { get; private set; }
        public int BackdropPackage { get; private set; }
        public int BackdropSpeed { get; private set; } = 2;
        public bool IsFixedLayout { get; private set; }
        public int Layout { get; private set; }

        // ---------------------------------------------------------------- floor selection
        private static byte[] _floorVariants;

        public static int FloorVariant(int floor)
        {
            if (_floorVariants == null)
            {
                var asset = AssetMode.Load<TextAsset>("Tower/Retail/floor_variants");
                _floorVariants = asset ? asset.bytes : Array.Empty<byte>();
            }
            // Floors past the authored tables (retail mode 2) pick a random variant; keep it stable per floor.
            if (floor >= 1 && floor < Mathf.Min(40, _floorVariants.Length)) return _floorVariants[floor] % 24;
            return Math.Abs(floor * 7) % 24;
        }

        public static RetailTowerFloor CreateRandom(int floor, int seed, bool eventFloor)
        {
            var geometry = RetailTowerGeometry.Load("geometry_29");
            if (geometry == null) return null;
            int variant = FloorVariant(floor);
            var result = new RetailTowerFloor
            {
                Geometry = geometry,
                TextureSet = $"tiles_v{variant:00}",
                BackdropPackage = variant + 3,
                BackdropSpeed = 2
            };
            new Generator(result, geometry, seed, floor, eventFloor).Run();
            result.ApplyTileFlags();
            result.WriteGridBorder();
            return result;
        }

        /// <summary>Fixed layout (load_dungeon_layout) with the track tileset/texture set of its floor event.</summary>
        public static RetailTowerFloor CreateFixed(int layout, bool beldoArena)
        {
            var geometry = RetailTowerGeometry.Load(beldoArena ? "geometry_2a" : "geometry_2b");
            var asset = AssetMode.Load<TextAsset>($"Tower/Retail/layout_{layout:00}");
            if (geometry == null || !asset) return null;
            var data = asset.bytes;
            if (data.Length < 4 + Size * Size * 6 || data[0] != 'A' || data[3] != '1') return null;
            var result = new RetailTowerFloor
            {
                Geometry = geometry,
                TextureSet = beldoArena ? "tiles_t1" : "tiles_t2",
                BackdropPackage = beldoArena ? 1 : 2,
                BackdropSpeed = beldoArena ? 4 : 2,
                IsFixedLayout = true,
                Layout = layout
            };
            int pos = 8;
            int rooms = data[pos++];
            for (int i = 0; i < rooms; i++, pos += 8)
                result.Rooms.Add(new RectInt(BitConverter.ToUInt16(data, pos), BitConverter.ToUInt16(data, pos + 2),
                    BitConverter.ToUInt16(data, pos + 4), BitConverter.ToUInt16(data, pos + 6)));
            int markers = data[pos++];
            for (int i = 0; i < markers; i++, pos += 3)
                result.Markers.Add((data[pos], new Vector2Int(data[pos + 1], data[pos + 2])));
            int cellStart = data.Length - Size * Size * 6;
            for (int i = 0; i < Size * Size; i++)
            {
                result.Kind[i] = BitConverter.ToUInt16(data, cellStart + i * 6);
                result.Value[i] = BitConverter.ToInt16(data, cellStart + i * 6 + 2);
                result.Flags[i] = BitConverter.ToUInt16(data, cellStart + i * 6 + 4);
            }
            result.ApplyTileFlags();
            return result;
        }

        // ---------------------------------------------------------------- queries
        public int Index(int x, int y) => (x + (y << 6)) & 4095;

        private int WalkSurface(int x,int y)
        {
            int index=Index(x,y);
            // Doorway emits four orientations, five variants each (79..98).
            // All contain an overhead lintel; sample the passage floor beneath it.
            int minimum=Kind[index] is >=79 and <=98 ? -Value[index]-TowerFloorManager.TerrainBlockHeight : -0x400;
            return Geometry.SampleSurface(Kind,Value,(x<<6)+0x20,(y<<6)+0x20,minimum);
        }

        /// <summary>Surface height at the cell centre in the port's convention (positive up, 64 per tile).</summary>
        public int GroundHeight(int x, int y)
        {
            int surface = WalkSurface(x,y);
            return surface >= 0x200 ? Value[Index(x, y)] : -surface;
        }

        public bool IsWalkable(int x, int y)
        {
            if (x <= 0 || y <= 0 || x >= Size - 1 || y >= Size - 1) return false;
            int i = Index(x, y);
            if (Kind[i] == 0 || (Flags[i] & 0x8408) != 0) return false;
            return WalkSurface(x,y) < 0x200;
        }

        public bool InRoom(int x, int y)
        {
            foreach (var room in Rooms)
                if (x >= room.x && y >= room.y && x < room.xMax && y < room.yMax) return true;
            return false;
        }

        // apply_state_index_tile_masks / apply_tile_flags_to_rectangle with the tileset's per-tile flag table.
        private void ApplyTileFlags()
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int i = Index(x, y);
                int flags = Kind[i] < Geometry.Count ? Geometry.Aux[Kind[i]] : 0;
                if ((flags & 1) != 0) Flags[i] |= 1;
                if ((flags & 4) != 0) Flags[i] |= 4;
                if ((flags & 8) != 0) Flags[i] |= 8;
                if ((flags & 0x20) != 0) Flags[i] |= 0x400;
                if ((flags & 2) != 0)
                {
                    if (x > 0) Flags[i - 1] |= 1;
                    if (x < Size - 1) Flags[i + 1] |= 1;
                    if (y > 0) Flags[i - Size] |= 1;
                    if (y < Size - 1) Flags[i + Size] |= 1;
                }
                if ((flags & 0x80) != 0) Flags[i] |= 0x8000;
                if ((flags & 0x40) != 0) Flags[i] |= 0x40;
            }
        }

        // initialize_grid_border_cells
        private void WriteGridBorder()
        {
            for (int e = 0; e < Size; e++)
            {
                foreach (int i in new[] { Index(0, e), Index(Size - 1, e), Index(e, 0), Index(e, Size - 1) })
                {
                    Kind[i] = 3;
                    Value[i] = 0x400;
                    Flags[i] |= 0x8001;
                }
            }
        }

        // ---------------------------------------------------------------- generator
        private sealed class Room
        {
            public int X, Y, W, H, Tint, Active, Link;
        }

        private sealed class Generator
        {
            private readonly RetailTowerFloor _f;
            private readonly RetailTowerGeometry _g;
            private readonly int _floor;
            private readonly bool _eventFloor;
            private uint _seed, _savedSeed;
            private readonly Room[] _rooms = new Room[36];
            private int _cols, _rows, _ready;
            private bool _specialPlaced;
            private readonly List<(int A, int B)> _links = new();
            private readonly int[] _blocked = new int[4];
            private int _forwardLimit;
            private readonly int[,] _sideMarks = new int[2, 8];

            public Generator(RetailTowerFloor floor, RetailTowerGeometry geometry, int seed, int floorNumber, bool eventFloor)
            {
                _f = floor;
                _g = geometry;
                _seed = unchecked((uint)seed);
                _floor = floorNumber;
                _eventFloor = eventFloor;
                for (int i = 0; i < _rooms.Length; i++) _rooms[i] = new Room();
            }

            // generate_random_value / random_dungeon_range_16 / save_or_restore_random_seed
            private int Next()
            {
                _seed = unchecked(_seed * 0x41C64E6Du + 0x3039u);
                return (int)((_seed >> 16) & 0x7FFF);
            }

            private int Range16(int a, int b)
            {
                uint lower = unchecked((uint)a), upper = unchecked((uint)b);
                if (upper < lower) (lower, upper) = (upper, lower);
                uint r = (uint)(Next() & 0xFFFF);
                return (int)(((r % (upper - lower + 1)) + lower) & 0xFFFF);
            }

            private ushort[] K => _f.Kind;
            private short[] V => _f.Value;
            private ushort[] F => _f.Flags;
            private int I(int x, int y) => (x + (y << 6)) & 4095;

            public void Run()
            {
                for (int attempt = 0; attempt < 64; attempt++)
                {
                    Array.Clear(K, 0, K.Length);
                    Array.Clear(V, 0, V.Length);
                    Array.Clear(F, 0, F.Length);
                    foreach (var room in _rooms) { room.X = room.Y = room.W = room.H = room.Tint = room.Active = room.Link = 0; }
                    _f.Dens.Clear(); _f.RoomItems.Clear(); _f.RoomTraps.Clear(); _f.Rooms.Clear();
                    if (GenerateLayout() == 0 && _ready != 0) break;
                }
                foreach (var room in _rooms)
                    if (room.Active != 0) _f.Rooms.Add(new RectInt(room.X, room.Y, room.W, room.H));
            }

            // ---- terrain sampling helpers (tile.c / generate.c)
            private int Surface(int x, int y, int minimum) => _g.SampleSurface(K, V, x, y, minimum);

            private int OffsetKind(int x, int y, int d, out int flags)
            {
                int i = I(x + Dx[d], y + Dy[d]);
                flags = F[i];
                return (short)K[i];
            }

            private bool DiagonalOk(int d, int x, int y, int h)
            {
                if ((d & 1) == 0) return true;
                for (int side = -1; side < 2; side += 2)
                {
                    int dd = (d + side) & 7;
                    if (OffsetKind(x, y, dd, out _) != 0 &&
                        Surface((ushort)((x << 6) + 0x20 + OffsetX[dd]), (ushort)((y << 6) + 0x20 + OffsetY[dd]), h) > 0x200)
                        return false;
                }
                return true;
            }

            private int Classify(int x, int y, int current, int d)
            {
                int probe = (short)(current - 0x20);
                if (!DiagonalOk(d, x, y, probe)) return 0;
                if (OffsetKind(x, y, d, out int flags) == 0) return 0;
                int nx = x + Dx[d], ny = y + Dy[d];
                if ((flags & 0x8400) != 0) return 0;
                int target = Surface(((nx << 6) + 0x20) & 0xFFE0, ((ny << 6) + 0x20) & 0xFFE0, probe);
                if (target < 0x200)
                {
                    if ((short)current >= target) return 2;
                    if (target - (short)current < 0x21) return 1;
                }
                return 0;
            }

            private int CountUnblocked(Room room)
            {
                int count = 0;
                for (int y = room.Y; y < room.Y + room.H; y++)
                for (int x = room.X; x < room.X + room.W; x++)
                    if ((F[I(x, y)] & 0x8500) == 0) count++;
                return count;
            }

            private int Highest(Room room, out int ox, out int oy)
            {
                int best = -0x200;
                ox = oy = 0;
                for (int y = room.Y; y < room.Y + room.H; y++)
                for (int x = room.X; x < room.X + room.W; x++)
                {
                    if ((F[I(x, y)] & 0x8400) != 0) continue;
                    int value = Surface(((x << 6) + 0x20) & 0xFFE0, ((y << 6) + 0x20) & 0xFFFF, -0x400);
                    if (value < 0x200 && best < value) { best = value; ox = x; oy = y; }
                }
                return best == -0x200 ? 0x200 : best;
            }

            private int FirstQualifying(Room room, out int ox, out int oy)
            {
                ox = oy = 0;
                for (int y = room.Y; y < room.Y + room.H; y++)
                for (int x = room.X; x < room.X + room.W; x++)
                {
                    int flags = F[I(x, y)];
                    if ((flags & 0x8400) != 0 || (flags & 0x300) != 0x200) continue;
                    int value = Surface(((x << 6) + 0x20) & 0xFFE0, ((y << 6) + 0x20) & 0xFFFF, -0x400);
                    if (value < 0x200) { ox = x; oy = y; return value; }
                }
                return 0x200;
            }

            // mark_path_through_neighboring_tiles
            private bool MarkPath(Room room)
            {
                int tile = Highest(room, out int x, out int y);
                if (tile >= 0x200) return false;
                F[I(x, y)] |= 0x100;
                int steps = CountUnblocked(room);
                for (int guard = 0; guard < 8192; guard++)
                {
                    int next = -1, open = 0;
                    for (int d = 7; d >= 0; d--)
                    {
                        if (Classify(x, y, tile, d) == 0) continue;
                        int i = I(x + Dx[d], y + Dy[d]);
                        if ((F[i] & 0x100) == 0) { F[i] |= 0x200; next = d; }
                        open++;
                    }
                    if (open == 0) return false;
                    if (next >= 0)
                    {
                        x += Dx[next]; y += Dy[next];
                        F[I(x, y)] |= 0x100;
                        if (--steps <= 0) return true;
                        tile = Surface(((x << 6) + 0x20) & 0xFFE0, ((y << 6) + 0x20) & 0xFFE0, -0x400);
                    }
                    else
                    {
                        tile = FirstQualifying(room, out x, out y);
                        if (tile >= 0x200) return false;
                        F[I(x, y)] |= 0x100;
                        if (--steps <= 0) return true;
                    }
                }
                return false;
            }

            // ---- room terrain
            private void MaskRect(int x0, int y0, int w, int h, int mask)
            {
                for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++) F[I(x, y)] &= (ushort)mask;
            }

            private void Put(int x, int y, int kind, int value, int flags)
            {
                int i = I(x, y);
                K[i] = (ushort)kind; V[i] = (short)value; F[i] = (ushort)flags;
            }

            // write_rectangular_border
            private void WriteBorder(int left, int top, int right, int bottom, int baseValue)
            {
                int value = (short)(baseValue + 0x40);
                Put(left - 1, top - 1, 0xE, value, 9);
                Put(right, top - 1, 0xB, value, 9);
                Put(left - 1, bottom, 0xD, value, 9);
                Put(right, bottom, 0xC, value, 9);
                for (int x = left; x < right; x++) { Put(x, top - 1, 0x12, value, 9); Put(x, bottom, 0x10, value, 9); }
                for (int y = top; y < bottom; y++) { Put(left - 1, y, 0x11, value, 9); Put(right, y, 0xF, value, 9); }
            }

            private void StepUp(int i)
            {
                if (F[i] >= 9) return;
                K[i]++; V[i] = (short)(V[i] + 0x20); F[i]++;
            }

            private void StepDown(int i)
            {
                if ((short)F[i] <= 0) return;
                K[i]--; V[i] = (short)(V[i] - 0x20); F[i]--;
            }

            // fill_region_with_random_patches
            private void FillRegion(Room room)
            {
                int value = room.Tint, left = room.X, top = room.Y, width = room.W, height = room.H;
                int right = left + width, bottom = top + height;
                for (int y = top; y < bottom; y++)
                for (int x = left; x < right; x++) Put(x, y, 25, value - 0x80, 6);
                int swap = Next() & 1;
                int sum = width + height;
                int count = _floor < 5 ? sum >> 4 : sum >> 2;
                for (int patch = 0; patch < count; patch++)
                {
                    int px = left + (Next() & 0xFFFF) % width;
                    int py = top + (Next() & 0xFFFF) % height;
                    int pw = (width - (px - left)) / 2 + (Next() & 7) - 4;
                    int ph = (height - (py - top)) / 2 + (Next() & 7) - 4;
                    bool style = (Next() & 7) != 0;
                    for (int row = 0, sy = py; row < ph; row++, sy++)
                    {
                        if (sy >= bottom) continue;
                        for (int col = 0, sx = px; col < pw; col++, sx++)
                        {
                            if (sx >= right) continue;
                            int i = I(sx, sy);
                            if ((swap != 0) == style) StepUp(i); else StepDown(i);
                        }
                    }
                }
                WriteBorder(left, top, right, bottom, value);
            }

            // adjust_random_parity_tile_heights
            private void ParityPillars(Room room)
            {
                int parity = Next() & 1;
                for (int y = room.Y; y < room.Y + room.H; y++)
                for (int x = room.X; x < room.X + room.W; x++)
                {
                    if ((x & 1) != parity || (y & 1) != parity) continue;
                    int i = I(x, y);
                    K[i] = (ushort)((F[i] >> 1) + 0x63);
                    V[i] = (short)(V[i] - 0x60);
                    F[i] |= 0x8000;
                }
            }

            // initialize_random_room_slot: the monster den object.
            private void RoomSlot(Room room)
            {
                if (_eventFloor) return;
                int bonus = Next() & 7;
                int size = Math.Min(((room.W * room.H) >> 2) + bonus, 0x1C);
                int x = Range16(0, (room.W - 1) & 0xFFFF), y = Range16(0, (room.H - 1) & 0xFFFF);
                _f.Dens.Add((new Vector2Int(room.X + x, room.Y + y), size));
            }

            // place_room_trap_records
            private void TrapRoom(Room room)
            {
                int special = Next() & 0xFF;
                int limit = Math.Min(((room.H * room.W) >> 3) + (Next() & 7), 28);
                for (int slot = limit; slot >= 0; slot--)
                {
                    int x = Range16(0, (room.W - 1) & 0xFFFF), y = Range16(0, (room.H - 1) & 0xFFFF);
                    OffsetKind(room.X + x - 1, room.Y + y, 0, out int flags);
                    if ((flags & 0x8820) != 0) continue;
                    int kind = special == 0 ? 7 : (Next() & 0xFFFF) % 19 + 1;
                    _f.RoomTraps.Add((new Vector2Int(room.X + x, room.Y + y), kind));
                    F[I(room.X + x, room.Y + y)] |= 0x0002;
                }
            }

            // mark_random_eligible_region_tiles (item room). Item selection itself is left to the port's item tables.
            private void ItemRoom(Room room)
            {
                int bits = Next();
                int count = Math.Min(((room.H * room.W) >> 6) + (bits & 7) + 4, 0x3C);
                for (int slot = count; slot >= 0; slot--)
                {
                    int x = Range16(0, (room.W - 1) & 0xFFFF), y = Range16(0, (room.H - 1) & 0xFFFF);
                    int cx = room.X + x, cy = room.Y + y;
                    int kind = OffsetKind(cx - 1, cy, 0, out int flags);
                    if (kind != 0 && (flags & 0xFF20) != 0) continue;
                    F[I(cx, cy)] |= 0x0010;
                    _f.RoomItems.Add(new Vector2Int(cx, cy));
                    Next();
                }
            }

            // choose_random_region_feature
            private void Feature(Room room)
            {
                room.Active = 1;
                int roll = Next() & 0xFF;
                switch (roll)
                {
                    case 0:
                        room.Active = 2;
                        return;
                    case 1 when !_specialPlaced:
                        room.Active = 3; _specialPlaced = true; RoomSlot(room);
                        return;
                    case 2 when !_specialPlaced:
                        _specialPlaced = true; room.Active = 2; ItemRoom(room);
                        return;
                    case 4 when !_specialPlaced:
                        _specialPlaced = true; room.Active = 4; TrapRoom(room);
                        return;
                }
                if (roll >= 5 && roll <= 12) ParityPillars(room);
            }

            // update_region_until_complete
            private void Region(Room room)
            {
                for (int guard = 0; guard < 64; guard++)
                {
                    FillRegion(room);
                    Feature(room);
                    if (MarkPath(room)) break;
                }
                MaskRect(room.X, room.Y, room.W, room.H, 0xF);
            }

            private int FirstMark(int x0, int y0, int w, int h)
            {
                if (y0 <= 0 || x0 <= 0 || y0 + h >= 64 || x0 + w >= 64) return -1;
                for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                    if (F[I(x, y)] != 0) return (short)F[I(x, y)];
                return 0;
            }

            private void FillMarks(int x0, int y0, int w, int h, int value)
            {
                for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++) F[I(x, y)] = (ushort)value;
            }

            // ---- corridors
            private (int X, int Y) EdgePoint(int side, Room room)
            {
                if (room.Active == 0) return (0, 0);
                switch (side)
                {
                    case 0: return (room.W, (Next() & 0xFFFF) % (room.H - 2) + 1);
                    case 2: return ((Next() & 0xFFFF) % (room.W - 2) + 1, room.H);
                    case 4: return (-1, (Next() & 0xFFFF) % (room.H - 2) + 1);
                    default: return ((Next() & 0xFFFF) % (room.W - 2) + 1, -1);
                }
            }

            private bool HasLink(int a, int b)
            {
                for (int i = _links.Count - 1; i >= 0; i--)
                {
                    var (p, q) = _links[i];
                    if ((p == a || p == b) && (q == a || q == b)) return true;
                }
                return false;
            }

            // search_outward_for_eligible_tile
            private int Search(ref int facing, int ox, int oy, int[] tiles, int radius, int filter)
            {
                int failures = 0;
                int x = (short)(ox + Dx[facing] * radius), y = (short)(oy + Dy[facing] * radius);
                if (x >= 0 && x < _cols && y >= 0 && y < _rows && _blocked[facing >> 1] == 0)
                {
                    int index = x + y * _cols;
                    int tile = tiles[index];
                    if (tile > 0)
                    {
                        if (!HasLink(index, filter)) return index;
                        failures = 1;
                    }
                    else if (tile < 0) failures = 1;
                }
                else failures++;
                if (failures != 0)
                {
                    _blocked[facing >> 1] = 1;
                    if (radius < _forwardLimit) _forwardLimit = radius;
                }
                int limit = radius > _forwardLimit ? _forwardLimit : radius;
                for (int forward = 1; forward < radius; forward++)
                {
                    if (forward >= limit) { failures++; continue; }
                    int sideSteps = radius - forward;
                    int side = Next() & 1;
                    for (int sides = 2; sides > 0; sides--, side ^= 1)
                    {
                        int turn = (facing + Turns[side]) & 6;
                        int nx = (short)(ox + Dx[facing] * forward + Dx[turn] * sideSteps);
                        int ny = (short)(oy + Dy[facing] * forward + Dy[turn] * sideSteps);
                        if (nx >= 0 && nx < _cols && ny >= 0 && ny < _rows && forward < 8 && _sideMarks[side, forward] == 0)
                        {
                            int index = nx + ny * _cols;
                            int tile = tiles[index];
                            if (tile == 0) continue;
                            if (tile > 0 && !HasLink(index, filter)) { facing = turn; return index; }
                            _sideMarks[side, forward] = 1;
                        }
                        failures++;
                    }
                }
                if (failures < 3 && radius < 16) return Search(ref facing, ox, oy, tiles, radius + 1, filter);
                return -1;
            }

            private static int Heading(int sx, int sy, int ex, int ey)
            {
                int y = ey - sy, x = ex - sx;
                return x == 0 && y == 0 ? 0 : (int)Math.Round(Math.Atan2(y, x) * 2048.0 / Math.PI);
            }

            /// <summary>choose_next_step_toward_destination (no height) / carve_corridor_toward_destination (height).</summary>
            private int Route(int startX, int startY, int dir, int destX, int destY, bool random, int[] offsets,
                int[] height, int step)
            {
                int count = 0, cx = startX, cy = startY, slot = dir;
                int rangeX = Math.Abs(destX - cx) - 4;
                if (rangeX <= 0) rangeX = 1;
                int wx = Math.Min(rangeX, 4);
                rangeX -= wx;
                if (rangeX <= 0) rangeX = 1;
                int rangeY = Math.Abs(destY - cy) - 4;
                if (rangeY <= 0) rangeY = 1;
                int wy = Math.Min(rangeY, 4);
                rangeY -= wy;
                if (rangeY <= 0) rangeY = 1;
                if (random)
                {
                    offsets[0] = (Next() & 0xFFFF) % rangeX;
                    wx = cx + offsets[0] * Dx[dir] + Dx[dir] * wx;
                    offsets[1] = (Next() & 0xFFFF) % rangeY;
                    wy = cy + offsets[1] * Dy[dir] + Dy[dir] * wy;
                }
                else
                {
                    wx = cx + offsets[0] * Dx[dir] + Dx[dir] * wx;
                    wy = cy + offsets[1] * Dy[dir] + Dy[dir] * wy;
                }
                wx = (short)wx; wy = (short)wy;

                bool Walk(int tx, int ty, int d)
                {
                    for (int guard = 0; guard < 256; guard++)
                    {
                        int i = I(cx, cy);
                        if (height == null)
                        {
                            K[i] = 104; F[i] = 1;
                        }
                        else
                        {
                            int tile = (short)((height[0] >> 16) & 0xFFE0);
                            if (V[i] != 16384 && Math.Abs(V[i] - tile) >= 33) return false;
                            V[i] = (short)tile;
                        }
                        if (cx == tx && cy == ty) return true;
                        if (height != null) height[0] += step;
                        count++;
                        cx += Dx[d]; cy += Dy[d];
                    }
                    return false;
                }

                if (!Walk(wx, wy, dir)) return -1;
                if (wx != destX || wy != destY)
                {
                    if (Dx[slot] != 0) wy = destY; else wx = destX;
                    if (!Walk(wx, wy, (Heading(cx, cy, wx, wy) >> 9) & 6)) return -1;
                }
                if (wx != destX || wy != destY)
                    if (!Walk(destX, destY, (Heading(cx, cy, destX, destY) >> 9) & 6)) return -1;
                return count;
            }

            private void Doorway(int edge, int inner, int d, int travel)
            {
                int edgeKind = (short)K[edge], innerKind = (short)K[inner];
                if (edgeKind >= 15 && edgeKind <= 18)
                {
                    K[edge] = (ushort)((F[inner] >> 1) + (d / 2) * 5 + 79);
                    V[edge] = (short)(V[inner] - ((F[inner] & 1) << 5));
                    if (innerKind >= 99 && innerKind <= 103) V[edge] = (short)(V[edge] + 96);
                }
                if (innerKind >= 19 && innerKind <= 28)
                    K[inner] = (ushort)(F[inner] + (d / 2) * 10 + 39);
                else if (innerKind >= 99 && innerKind <= 103)
                {
                    K[inner] = (ushort)(F[inner] + (travel / 2) * 10 + 39);
                    V[inner] = (short)(V[inner] + 96);
                }
            }

            // connect_rooms_with_corridor
            private bool Connect(int srcDir, int destDir, int si, int di, int[] marks)
            {
                var src = _rooms[si];
                var dst = _rooms[di];
                int path = 0;
                var (sox, soy) = EdgePoint(srcDir, src);
                var (dox, doy) = EdgePoint((destDir + 4) & 6, dst);
                int saved = srcDir;
                _savedSeed = _seed; // save_or_restore_random_seed(1): the carve pass replays the same random bends
                var offsets = new int[2];

                bool Plan(bool carve, int[] mainHeight, int[] sideHeight, int heightStep)
                {
                    int travel = saved;
                    int sx = src.X + sox, sy = src.Y + soy;
                    if (src.Active != 0) { sx += Dx[travel]; sy += Dy[travel]; }
                    int startDir = travel;
                    int cx = si % _cols + Dx[travel], cy = si / _cols + Dy[travel];
                    int gx = di % _cols, gy = di / _cols;
                    for (int guard = 0; guard < 64; guard++)
                    {
                        if (cx == gx && cy == gy)
                        {
                            int ex = dst.Active != 0 ? dst.X + dox : dst.X;
                            int ey = dst.Active != 0 ? dst.Y + doy : dst.Y;
                            ex -= Dx[destDir]; ey -= Dy[destDir];
                            int r = Route(sx, sy, startDir, ex, ey, true, offsets, mainHeight, heightStep);
                            if (carve && r < 0) return false;
                            if (!carve) path += Math.Max(0, r);
                            sx += Dx[(travel + 2) & 7]; sy += Dy[(travel + 2) & 7];
                            ex += Dx[(destDir + 2) & 7]; ey += Dy[(destDir + 2) & 7];
                            var side = new[] { (int)(short)(offsets[0] - Dx[travel]), (int)(short)(offsets[1] - Dy[travel]) };
                            r = Route(sx, sy, startDir, ex, ey, false, side, sideHeight, heightStep);
                            return !(carve && r < 0);
                        }
                        int ri = cy * _cols + cx;
                        marks[ri] = 2;
                        int wx = _rooms[ri].X, wy = _rooms[ri].Y;
                        int r1 = Route(sx, sy, startDir, wx, wy, true, offsets, mainHeight, heightStep);
                        if (carve && r1 < 0) return false;
                        if (!carve) path += Math.Max(0, r1);
                        var sideOffsets = new[] { (int)(short)(offsets[0] - Dx[travel]), (int)(short)(offsets[1] - Dy[travel]) };
                        int r2 = Route(sx + Dx[(travel + 2) & 7], sy + Dy[(travel + 2) & 7], startDir, wx, wy, false,
                            sideOffsets, sideHeight, heightStep);
                        if (carve && r2 < 0) return false;
                        sx = wx; sy = wy;
                        if (Dx[travel] != 0) { if (cx == gx) startDir = (Heading(cx, cy, gx, gy) >> 9) & 6; }
                        else if (cy == gy) startDir = (Heading(cx, cy, gx, gy) >> 9) & 6;
                        cx += Dx[startDir]; cy += Dy[startDir];
                        travel = startDir;
                    }
                    return false;
                }

                Plan(false, null, null, 0);
                int t = saved, back = (t + 4) & 7;
                int ax = src.X + sox, ay = src.Y + soy;
                Doorway(I(ax, ay), I(ax + Dx[back], ay + Dy[back]), t, t);
                int bx = ax + Dx[(t + 2) & 7], by = ay + Dy[(t + 2) & 7];
                Doorway(I(bx, by), I(bx + Dx[back], by + Dy[back]), t, t);
                if (dst.Active != 0)
                {
                    int ex = dst.X + dox, ey = dst.Y + doy;
                    int entry = (destDir + 4) & 6;
                    Doorway(I(ex, ey), I(ex + Dx[destDir], ey + Dy[destDir]), entry, t);
                    int fx = ex + Dx[(entry - 2) & 7], fy = ey + Dy[(entry - 2) & 7];
                    Doorway(I(fx, fy), I(fx + Dx[destDir], fy + Dy[destDir]), entry, t);
                }
                _seed = _savedSeed;
                int srcHeight = src.Active != 0 ? V[I(src.X + sox, src.Y + soy)] : V[I(src.X, src.Y)];
                int dstHeight = dst.Active != 0 ? V[I(dst.X + dox, dst.Y + doy)] : V[I(dst.X, dst.Y)];
                if (path == 0) path = 1;
                int gap = Math.Abs(dstHeight - srcHeight);
                if ((gap + 31) / 32 > path) return true;
                int heightStepValue = (gap << 16) / path;
                if (dstHeight < srcHeight) heightStepValue = -heightStepValue;
                var main = new[] { srcHeight << 16 };
                var sideRoute = new[] { srcHeight << 16 };
                offsets[0] = offsets[1] = 0;
                return !Plan(true, main, sideRoute, heightStepValue);
            }

            // update_active_area_tiles: checkerboard floor variants.
            private void UpdateAreaTiles()
            {
                foreach (var room in _rooms)
                {
                    if (room.Active == 0) continue;
                    for (int y = room.Y; y < room.Y + room.H; y++)
                    for (int x = room.X; x < room.X + room.W; x++)
                    {
                        int i = I(x, y);
                        int kind = (short)K[i];
                        if (kind < 0x13 || kind > 0x1C) continue;
                        bool alternate = (y & 1) != 0 ? (x & 1) != 0 : (x & 1) == 0;
                        if (alternate) K[i] = (ushort)(kind + 10);
                    }
                }
            }

            private void ClearRegion(Room room)
            {
                room.Active = 0;
                for (int y = room.Y - 1; y < room.Y - 1 + room.H + 2; y++)
                for (int x = room.X; x < room.X + room.W + 2; x++)
                {
                    int i = I(x, y);
                    K[i] = 0; V[i] = 0; F[i] = 0;
                }
            }

            // generate_floor_layout
            private int GenerateLayout()
            {
                int r = Next();
                _cols = (r & 0x7F) == 0 ? 2 : (r & 0x1F) == 0 ? 3 : 4;
                r = Next();
                _rows = (r & 0x7F) == 0 ? 2 : (r & 0x1F) == 0 ? 3 : 4;
                _links.Clear();
                int cols = _cols, rows = _rows, n = rows * cols;
                var flags = new int[16];
                var connections = new int[16, 4];
                int built = 0;
                if ((short)Range16(0, 512) < 2) built = 1;
                else
                {
                    for (int k = 0; k < n; k++) flags[k] = (Next() & 3) != 0 ? 1 : 0;
                    int cellW = 64 / cols, cellH = 64 / rows;
                    for (int oy = 0; oy < rows; oy++)
                    for (int ox = 0; ox < cols; ox++)
                    {
                        int k = oy * cols + ox;
                        var room = _rooms[k];
                        if (flags[k] > 0)
                        {
                            int rx = Range16(0, cellW / 2 - 1) + ox * cellW;
                            int ry = Range16(0, cellH / 2 - 1) + oy * cellH;
                            int dw = Range16(0, cellW / 2 - 1);
                            int jr = Range16(0, cellH / 2 - 1) + 5;
                            int rh = (short)jr >= 5 ? jr : 5;
                            int x0 = (short)(rx - 2), y0 = (short)(ry - 2), w0 = (short)(dw + 9), h0 = (short)(rh + 4);
                            if (FirstMark(x0, y0, w0, h0) == 0)
                            {
                                room.Active = 1;
                                FillMarks(x0, y0, w0, h0, k + 1);
                                room.X = rx + 1; room.Y = ry + 1; room.W = dw + 3; room.H = rh - 2;
                                room.Tint = Next() & 0x60;
                                built++;
                                flags[k] = 2;
                                for (int d = 0; d < 4; d++) connections[k, d] = 1;
                                continue;
                            }
                            flags[k] = 0;
                        }
                        room.X = ox * cellW + (Next() & 7) + 6;
                        room.Y = oy * cellH + (Next() & 7) + 6;
                        V[I(room.X, room.Y)] = (short)(Next() & 0x60);
                    }
                }
                foreach (var room in _rooms) room.Link = 0;
                _ready = built;
                if (built < 2)
                {
                    _ready = 1;
                    var room = _rooms[0];
                    room.X = 2; room.Y = 2; room.Active = 1; room.W = 60; room.H = 60;
                    room.Tint = Next() & 0x60;
                    Region(room);
                }
                else
                {
                    for (int k = 0; k < n; k++)
                        if (_rooms[k].Active != 0) Region(_rooms[k]);
                    for (int y = 1; y < 63; y++)
                    for (int x = 1; x < 63; x++)
                        if (K[I(x, y)] == 0) V[I(x, y)] = 16384;
                    int current = (Next() & 0xFFFF) % n;
                    int dir = Next() & 6;
                    int dv = dir;
                    int tries = 1024;
                    for (int guard = 0; guard < 4096; guard++)
                    {
                        int i = 0;
                        while (i < n)
                        {
                            if (flags[current] > 0 && _rooms[current].Active != 0) break;
                            current = (current + 1) % n; i++;
                        }
                        if (i >= n) break;
                        int ox = current % cols, oy = current / cols, next = current;
                        for (i = 4; i > 0; i--, dir = (dir + 2) & 6)
                        {
                            int sourceDir = dir;
                            if (connections[current, dir >> 1] == 0) continue;
                            Array.Clear(_blocked, 0, 4);
                            _forwardLimit = cols;
                            Array.Clear(_sideMarks, 0, _sideMarks.Length);
                            dv = dir;
                            next = Search(ref dv, ox, oy, flags, 1, current);
                            if (next < 0) continue;
                            if (Connect(sourceDir, dv, current, next, flags)) return 1;
                            connections[current, dir >> 1]--;
                            connections[next, ((dv + 4) & 7) >> 1]--;
                            _rooms[current].Link = 1; _rooms[next].Link = 1;
                            _links.Add((current, next));
                            break;
                        }
                        if (i <= 0)
                        {
                            int j = (current + 1) % n;
                            for (int q = 0; q < n; q++)
                            {
                                if (_rooms[j].Link != 0 && flags[j] > 0) break;
                                j = (j + 1) % n;
                            }
                            next = j;
                        }
                        else
                        {
                            dir = (dv + 2) & 7;
                            if (--flags[current] <= 0) flags[current] = -1;
                        }
                        current = next;
                        if (--tries < 0)
                        {
                            for (int q = n - 1; q >= 0; q--)
                                if (_rooms[q].Link == 0 && _rooms[q].Active != 0) ClearRegion(_rooms[q]);
                            break;
                        }
                    }
                }
                MaskRect(0, 0, 64, 64, 0);
                UpdateAreaTiles();
                return 0;
            }
        }
    }
}
