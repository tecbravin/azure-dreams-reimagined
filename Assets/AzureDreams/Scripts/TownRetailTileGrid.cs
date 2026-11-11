using System;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Town's 128x128 halfword tile grid. This is distinct from the dungeon's
    /// six-byte cells. Ports copy_variant_building_tiles and Town cell queries;
    /// terrain geometry, textures, and collision meanings are separate data.
    /// </summary>
    public sealed class TownRetailTileGrid
    {
        public const int Size = 128;
        public const int WorldUnitsPerTile = 64;
        private readonly ushort[] _cells = new ushort[Size * Size];

        public void Clear() => Array.Clear(_cells, 0, _cells.Length);
        public ushort[] CopyCells() => (ushort[])_cells.Clone();

        public ushort ReadRaw(int tileX, int tileY) => _cells[(tileX & 127) + ((tileY & 127) << 7)];
        public int ReadTileId(int tileX, int tileY) => ReadRaw(tileX, tileY) & 0x3FFF;

        // The original accepts signed halfword world coordinates and divides
        // by 64 with truncation toward zero, including for negative positions.
        public int ReadWorldTileId(short worldX, short worldY) =>
            ReadTileId(worldX / WorldUnitsPerTile, worldY / WorldUnitsPerTile);

        public void ApplyLittleEndianPatch(int x, int y, byte[] bank, int byteOffset)
            => CopyLittleEndianPatch(x, y, bank, byteOffset, true);

        public void BlitLittleEndianPatch(int x, int y, byte[] bank, int byteOffset)
            => CopyLittleEndianPatch(x, y, bank, byteOffset, false);

        private void CopyLittleEndianPatch(int x, int y, byte[] bank, int byteOffset, bool coordinateVariants)
        {
            if (bank == null) throw new ArgumentNullException(nameof(bank));
            if (byteOffset < 0 || byteOffset > bank.Length - 4 || (byteOffset & 1) != 0)
                throw new ArgumentOutOfRangeException(nameof(byteOffset));
            int width = bank[byteOffset] | (bank[byteOffset + 1] << 8);
            int height = bank[byteOffset + 2] | (bank[byteOffset + 3] << 8);
            if (width > Size || height > Size) throw new ArgumentException("Invalid Town tile dimensions.");
            int words = 2 + width * height;
            if (bank.Length - byteOffset < words * 2) throw new ArgumentException("Truncated Town tile bank.");
            var patch = new ushort[words];
            for (int i = 0; i < words; i++)
                patch[i] = (ushort)(bank[byteOffset + i * 2] | (bank[byteOffset + i * 2 + 1] << 8));
            CopyPatch(x, y, patch, 0, coordinateVariants);
        }

        /// <summary>
        /// Applies one encoded block: width, height, then row-major halfwords.
        /// Transparent zero entries preserve cells already written by other blocks.
        /// Validates the complete region before writing to protect against bad decoding.
        /// </summary>
        public void ApplyPatch(int x, int y, ushort[] source, int offset = 0)
            => CopyPatch(x, y, source, offset, true);

        /// <summary>Retail blit_nonzero_u16_block_to_grid: preserves tile IDs without parity substitution.</summary>
        public void BlitNonzeroPatch(int x, int y, ushort[] source, int offset = 0)
            => CopyPatch(x, y, source, offset, false);

        private void CopyPatch(int x, int y, ushort[] source, int offset, bool coordinateVariants)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (offset < 0 || offset > source.Length - 2) throw new ArgumentOutOfRangeException(nameof(offset));
            int width = source[offset];
            int height = source[offset + 1];
            if (width > Size || height > Size || x < 0 || y < 0 || x > Size - width || y > Size - height)
                throw new ArgumentException("Town tile block lies outside the 128x128 grid.");
            if (source.Length - offset - 2 < width * height)
                throw new ArgumentException("Town tile block is truncated.", nameof(source));
            int cursor = offset + 2;
            for (int row = 0; row < height; row++)
                for (int col = 0; col < width; col++)
                {
                    ushort tile = source[cursor++];
                    if (tile != 0)
                        _cells[(y + row) * Size + x + col] = coordinateVariants ? Variant(tile, x + col, y + row) : tile;
                }
        }

        private static ushort Variant(ushort tile, int x, int y)
        {
            bool oddX = (x & 1) != 0;
            bool oddY = (y & 1) != 0;
            switch (tile)
            {
                case 0x135: case 0x137: case 0x154: case 0x15E:
                    return oddY ? (ushort)(oddX ? 0x15E : 0x137) : (ushort)(oddX ? 0x154 : 0x135);
                case 0x136: case 0x138: case 0x153: case 0x15D:
                    return oddY ? (ushort)(oddX ? 0x138 : 0x15D) : (ushort)(oddX ? 0x136 : 0x153);
                case 0x141: case 0x142:
                    return (ushort)(oddX ? 0x142 : 0x141);
                case 0x143: case 0x144: case 0x15B: case 0x15C:
                    return oddY ? (ushort)(oddX ? 0x144 : 0x143) : (ushort)(oddX ? 0x15C : 0x15B);
                case 0x13D: case 0x13E: case 0x13F: case 0x140:
                    return oddY ? (ushort)(oddX ? 0x140 : 0x13F) : (ushort)(oddX ? 0x13E : 0x13D);
                case 0x149: case 0x14A: case 0x14B: case 0x14C:
                    return oddY ? (ushort)(oddX ? 0x14C : 0x14B) : (ushort)(oddX ? 0x14A : 0x149);
                default: return tile;
            }
        }
    }
}
