using System;
using System.IO;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>PS1 texture sampling with explicit coverage for recovered VRAM writes.</summary>
    public sealed class TownRetailVram
    {
        public const int Width = 1024;
        public const int Height = 512;
        private readonly byte[] _words;
        private readonly byte[] _written;

        public TownRetailVram(byte[] littleEndianWords, byte[] writtenWords)
        {
            if (littleEndianWords == null || writtenWords == null)
                throw new ArgumentNullException("VRAM payload and coverage are required.");
            if (littleEndianWords.Length != Width * Height * 2 || writtenWords.Length != Width * Height)
                throw new InvalidDataException("Invalid PS1 VRAM snapshot dimensions.");
            _words = (byte[])littleEndianWords.Clone();
            _written = (byte[])writtenWords.Clone();
        }

        public static TownRetailVram LoadVariant0()
        {
            var image = Resources.Load<TextAsset>("Town/Retail/town_vram_variant_0");
            var coverage = Resources.Load<TextAsset>("Town/Retail/town_vram_variant_0_written");
            if (image == null || coverage == null) throw new InvalidDataException("Town VRAM snapshot is missing.");
            return new TownRetailVram(image.bytes, coverage.bytes);
        }

        // Returns false for unknown uploads or reserved depth. A zero texel in
        // known memory is transparent; unknown memory must never become black art.
        public bool TrySample(ushort page, ushort clut, byte u, byte v, out Color32 color, out bool semiTransparent)
        {
            color = default;
            semiTransparent = false;
            int depth = (page >> 7) & 3;
            if (depth == 3) return false;
            int x = (page & 15) * 64 + (u >> (2 - depth));
            int y = ((page >> 4) & 1) * 256 + v;
            if (!TryWord(x, y, out ushort word)) return false;
            if (depth < 2)
            {
                int index = depth == 0 ? (word >> ((u & 3) * 4)) & 15 : (word >> ((u & 1) * 8)) & 255;
                if (!TryWord((clut & 63) * 16 + index, clut >> 6, out word)) return false;
            }
            semiTransparent = (word & 0x8000) != 0;
            color = new Color32(Expand(word & 31), Expand((word >> 5) & 31), Expand((word >> 10) & 31),
                (byte)(word == 0 ? 0 : 255));
            return true;
        }

        private bool TryWord(int x, int y, out ushort value)
        {
            int p = (x & 1023) + ((y & 511) * Width);
            value = 0;
            if (_written[p] == 0) return false;
            value = (ushort)(_words[p * 2] | _words[p * 2 + 1] << 8);
            return true;
        }
        private static byte Expand(int fiveBits) => (byte)((fiveBits << 3) | (fiveBits >> 2));
    }
}
