using System;
using System.IO;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>Reads the resident-bound Town geometry without rewriting its packet stream.</summary>
    public sealed class TownRetailGeometry
    {
        [Serializable] private sealed class Regions
        {
            public int face_list_pointers;
            public int vertices;
            public int normals;
        }
        [Serializable] private sealed class Metadata
        {
            public int decoded_bytes;
            public int list_count;
            public int vertex_records;
            public int normal_record_capacity;
            public Regions region_offsets;
        }

        private readonly byte[] _bytes;
        private readonly Metadata _metadata;
        public int ListCount => _metadata.list_count;
        public int VertexCount => _metadata.vertex_records;
        public int NormalCapacity => _metadata.normal_record_capacity;

        public TownRetailGeometry(byte[] bytes, string metadataJson)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (string.IsNullOrEmpty(metadataJson)) throw new ArgumentException("Missing geometry metadata.");
            _metadata = JsonUtility.FromJson<Metadata>(metadataJson);
            var r = _metadata?.region_offsets;
            if (r == null || _metadata.decoded_bytes != bytes.Length || ListCount <= 0 ||
                VertexCount < 0 || NormalCapacity < 0 || r.face_list_pointers < 24 ||
                (long)r.face_list_pointers + (long)ListCount * 4 != r.vertices ||
                (long)r.vertices + (long)VertexCount * 8 != r.normals ||
                (long)r.normals + (long)NormalCapacity * 8 > bytes.Length)
                throw new InvalidDataException("Invalid Town geometry region bindings.");
            _bytes = (byte[])bytes.Clone();
            for (int i = 0; i < ListCount; i++) GetListStart(i);
        }

        public static TownRetailGeometry LoadType0()
        {
            var bytes = Resources.Load<TextAsset>("Town/Retail/town_geometry_type_0");
            var metadata = Resources.Load<TextAsset>("Town/Retail/town_geometry_type_0_metadata");
            if (bytes == null || metadata == null)
                throw new InvalidDataException("Town geometry payload or metadata is missing.");
            return new TownRetailGeometry(bytes.bytes, metadata.text);
        }

        public int GetListStart(int id)
        {
            if ((uint)id >= (uint)ListCount) throw new ArgumentOutOfRangeException(nameof(id));
            uint offset = U32(_metadata.region_offsets.face_list_pointers + id * 4);
            if (offset > int.MaxValue) throw new InvalidDataException("Invalid face-list offset.");
            CheckPacket((int)offset);
            return (int)offset;
        }

        // Native SVECTOR order: map X, map Y, height. Keep signed coordinates
        // intact; Unity axis/scale conversion belongs to the mesh builder.
        public Vector3Int ReadVertex(int index) => ReadVector(index, VertexCount, _metadata.region_offsets.vertices);
        public Vector3Int ReadNormal(int index) => ReadVector(index, NormalCapacity, _metadata.region_offsets.normals);

        private Vector3Int ReadVector(int index, int count, int start)
        {
            if ((uint)index >= (uint)count) throw new ArgumentOutOfRangeException(nameof(index));
            int p = start + index * 8;
            return new Vector3Int((short)U16(p), (short)U16(p + 2), (short)U16(p + 4));
        }

        public Face ReadFace(int offset)
        {
            CheckPacket(offset);
            return new Face(U16(offset), U16(offset + 2), U16(offset + 4), U16(offset + 6),
                U32(offset + 8), U32(offset + 12), U32(offset + 16), U32(offset + 20));
        }

        /// <summary>
        /// Native render_shaded_map_tiles traversal after projection. Returns -1
        /// at a terminator. A rejected face can jump over alternate packets;
        /// callers must supply the actual projection/normal result.
        /// </summary>
        public int NextPacket(int offset, bool frontFacing, bool depthAccepted = true)
        {
            Face f = ReadFace(offset);
            int step;
            if (!f.HasPolygon)
            {
                if (f.Branch == 1 && f.SignedFlags < 0) return -1;
                step = 1;
            }
            else if (frontFacing)
            {
                if (!depthAccepted) return -1;
                if ((f.Branch & 15) == 1)
                {
                    if (f.SignedFlags < 0) return -1;
                    step = 1 + (f.Branch >> 4);
                }
                else step = 1;
            }
            else if (ReadNormal(f.NormalIndex).z < 0)
            {
                if (f.Branch == 1 && f.SignedFlags < 0) return -1;
                step = 1;
            }
            else
            {
                if ((f.Branch & 240) == 0 && f.SignedFlags < 0) return -1;
                step = f.Branch & 15;
                if (step == 0) throw new InvalidDataException("Town face branch does not advance.");
            }
            int next = checked(offset + step * 24);
            CheckPacket(next);
            return next;
        }

        private void CheckPacket(int offset)
        {
            if (offset < 0 || (offset & 3) != 0 || offset > _metadata.region_offsets.face_list_pointers - 24)
                throw new InvalidDataException("Town face packet is outside its region.");
        }
        private ushort U16(int p) => (ushort)(_bytes[p] | _bytes[p + 1] << 8);
        private uint U32(int p) => U16(p) | ((uint)U16(p + 2) << 16);

        public readonly struct Face
        {
            public readonly ushort A, B, C, D;
            public readonly uint Uv0Clut, Uv1Page, NormalUv2, ControlUv3;
            public Face(ushort a, ushort b, ushort c, ushort d, uint uv0, uint uv1, uint normal, uint control)
            { A = a; B = b; C = c; D = d; Uv0Clut = uv0; Uv1Page = uv1; NormalUv2 = normal; ControlUv3 = control; }
            public bool HasPolygon => Uv1Page != 0;
            public ushort NormalIndex => (ushort)NormalUv2;
            public ushort Clut => (ushort)(Uv0Clut >> 16);
            public ushort TexturePage => (ushort)(Uv1Page >> 16);
            public byte Branch => (byte)(ControlUv3 >> 16);
            public sbyte SignedFlags => unchecked((sbyte)(ControlUv3 >> 24));
            public Vector2Int Uv0 => new Vector2Int((byte)Uv0Clut, (byte)(Uv0Clut >> 8));
            public Vector2Int Uv1 => new Vector2Int((byte)Uv1Page, (byte)(Uv1Page >> 8));
            public Vector2Int Uv2 => new Vector2Int((byte)(NormalUv2 >> 16), (byte)(NormalUv2 >> 24));
            public Vector2Int Uv3 => new Vector2Int((byte)ControlUv3, (byte)(ControlUv3 >> 8));
        }
    }
}
