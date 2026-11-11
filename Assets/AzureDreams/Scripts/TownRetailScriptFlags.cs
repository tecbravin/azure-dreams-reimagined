using System;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Retail bitmap operations used by Town story selection. The module at
    /// TOWN.BIN 0x3E5000 implements set/clear/query at 0x3ED578/5F0/670.
    /// Storage is injected; the bitmap's retail allocation extent is unknown.
    /// </summary>
    public sealed class TownRetailScriptFlags : ITownRetailStoryEventBackend
    {
        private readonly Func<int, uint> _readWord;
        private readonly Action<int, uint> _writeWord;

        public TownRetailScriptFlags(Func<int, uint> readWord, Action<int, uint> writeWord)
        {
            _readWord = readWord ?? throw new ArgumentNullException(nameof(readWord));
            _writeWord = writeWord ?? throw new ArgumentNullException(nameof(writeWord));
        }

        /// <summary>
        /// Uses the existing Town save lifecycle. TownSaveVariables.Save flushes
        /// writes, and TownSaveVariables.Clear removes the tracked bitmap words.
        /// </summary>
        public static TownRetailScriptFlags CreatePersistent()
        {
            return new TownRetailScriptFlags(
                index => unchecked((uint)TownSaveVariables.GetInt("retail_flag_word_" + index)),
                (index, word) => TownSaveVariables.SetInt("retail_flag_word_" + index, unchecked((int)word)));
        }

        public bool IsFlagSet(int flagId) => GetMask(flagId) != 0;

        /// <summary>Returns the mask (not a normalized boolean), as the retail helper does.</summary>
        public uint GetMask(int flagId)
        {
            Validate(flagId);
            if (flagId == 0) return 0;
            if (flagId == 1) return 1;
            return _readWord(flagId / 32) & (1u << (flagId % 32));
        }

        public void ApplyOneTimeFlagOperation(int flagId) => SetBit(flagId);
        public void ApplyEventFlagOperation(int flagId) => ClearBit(flagId);

        public void SetBit(int flagId)
        {
            Validate(flagId);
            if (flagId == 0) return;
            int index = flagId / 32;
            _writeWord(index, _readWord(index) | (1u << (flagId % 32)));
        }

        public void ClearBit(int flagId)
        {
            Validate(flagId);
            if (flagId == 1) return;
            int index = flagId / 32;
            _writeWord(index, _readWord(index) & ~(1u << (flagId % 32)));
        }

        private static void Validate(int flagId)
        {
            if (flagId < 0) throw new ArgumentOutOfRangeException(nameof(flagId));
        }
    }
}
