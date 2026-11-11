using System;

namespace AzureDreamsReimagined
{
    /// <summary>Retail flag calls used while selecting a Town story event.</summary>
    public interface ITownRetailStoryEventBackend
    {
        bool IsFlagSet(int flagId);
        void ApplyOneTimeFlagOperation(int flagId); // retail func_8001E578
        void ApplyEventFlagOperation(int flagId);   // retail func_8001E5F0
    }

    /// <summary>One 12-byte retail town-selection record's flag fields.</summary>
    public readonly struct TownRetailSelectionRecord
    {
        public readonly int EnabledFlag;
        public readonly int PairedFlag;

        public TownRetailSelectionRecord(int enabledFlag, int pairedFlag)
        {
            EnabledFlag = enabledFlag;
            PairedFlag = pairedFlag;
        }
    }

    /// <summary>
    /// Decoded views of the retail table. Records are indexed by entry ID
    /// (0..7); both order arrays are zero-terminated and contain IDs 1..7.
    /// Tables must be selected for their owning scene module.
    /// </summary>
    public sealed class TownRetailSelectionTable
    {
        private readonly TownRetailSelectionRecord[] _records;
        private readonly byte[] _firstEntryOrder;
        private readonly byte[] _cyclingEntryOrder;

        /// <summary>
        /// TOWN.BIN module 0x3E5000, loaded at 0x80016000. MIPS consumers
        /// reference records at 0x8001791C, cycling order at 0x80017BB0,
        /// and first-entry priority at 0x80017BC0. This is scene-specific data.
        /// </summary>
        public static TownRetailSelectionTable CreateForScene3E5000()
        {
            return new TownRetailSelectionTable(new[]
            {
                new TownRetailSelectionRecord(0, 0),
                new TownRetailSelectionRecord(5230, 1015),
                new TownRetailSelectionRecord(5234, 1016),
                new TownRetailSelectionRecord(5232, 1017),
                new TownRetailSelectionRecord(5242, 1018),
                new TownRetailSelectionRecord(5238, 1019),
                new TownRetailSelectionRecord(5236, 1020),
                new TownRetailSelectionRecord(4605, 1021)
            }, new byte[] { 1, 2, 3, 6, 0 },
                new byte[] { 1, 2, 3, 6, 5, 1, 2, 3, 6, 4, 1, 2, 3, 6, 7, 0 });
        }

        public TownRetailSelectionTable(TownRetailSelectionRecord[] records, byte[] firstEntryOrder,
            byte[] cyclingEntryOrder)
        {
            if (records == null || records.Length < 8)
                throw new ArgumentException("Retail selection data must contain records indexed 0 through 7.", nameof(records));
            _records = (TownRetailSelectionRecord[])records.Clone();
            _firstEntryOrder = CloneTerminatedOrder(firstEntryOrder, nameof(firstEntryOrder));
            _cyclingEntryOrder = CloneTerminatedOrder(cyclingEntryOrder, nameof(cyclingEntryOrder));
        }

        internal TownRetailSelectionRecord Record(int entryId)
        {
            if (entryId < 1 || entryId > 7)
                throw new ArgumentOutOfRangeException(nameof(entryId), "Retail Town entry IDs are 1 through 7.");
            return _records[entryId];
        }

        internal int FirstEntryOrderLength => _firstEntryOrder.Length;
        internal int CyclingEntryOrderLength => _cyclingEntryOrder.Length;
        internal byte FirstEntryAt(int index) => _firstEntryOrder[index];
        internal byte CyclingEntryAt(int index) => _cyclingEntryOrder[index];

        private static byte[] CloneTerminatedOrder(byte[] order, string parameterName)
        {
            if (order == null || order.Length == 0 || order[order.Length - 1] != 0)
                throw new ArgumentException("Retail entry order must end with a zero terminator.", parameterName);
            for (int i = 0; i < order.Length - 1; i++)
                if (order[i] < 1 || order[i] > 7)
                    throw new ArgumentException("Retail entry order may contain only IDs 1 through 7 before its terminator.", parameterName);
            return (byte[])order.Clone();
        }
    }

    /// <summary>Mutable retail TownEventState bytes observed at RAM +2..+5.</summary>
    public sealed class TownRetailStorySelectionState
    {
        public byte DispatchState;
        public byte EntryIndex = byte.MaxValue;
        public byte SavedEntry;
        public byte SelectedEntry;
        public byte SelectionMode;
    }

    /// <summary>
    /// Ports select_next_event_state and its four entry-table helpers.
    /// The selected state feeds scene logic and the soundtrack reservation
    /// dispatcher; it does not execute narrative scripts itself.
    /// </summary>
    public sealed class TownRetailStoryEventSelector
    {
        private const int FEndEnding = 162;
        private const int SnReturnSuccessful = 1000;
        private const int FSbs01Pad09 = 1022;
        private const int FSbs01Pad16 = 1029;
        private const int FSbs01Pad18 = 1031;
        private const int FpNikoKenkatyu = 4602;
        private const int FVeldilDungeonMet = 5001;
        private const int FS04NikoStep = 5202;
        private const int FNikoSelroMorning = 5205;
        private const int FSelfyGotEgg = 5211;
        private const int FSeroEnd = 5213;
        private const int FtNikoLove = 5230;

        public byte Select(ITownRetailStoryEventBackend backend, TownRetailStorySelectionState state,
            TownRetailSelectionTable table)
        {
            if (backend == null) throw new ArgumentNullException(nameof(backend));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (table == null) throw new ArgumentNullException(nameof(table));

            if (backend.IsFlagSet(FSeroEnd) && !backend.IsFlagSet(FNikoSelroMorning))
            {
                backend.ApplyOneTimeFlagOperation(FNikoSelroMorning);
                backend.ApplyEventFlagOperation(FpNikoKenkatyu);
                state.SelectionMode = 9;
                return state.SelectionMode;
            }

            if (backend.IsFlagSet(FS04NikoStep) && !backend.IsFlagSet(FSbs01Pad18))
            {
                backend.ApplyOneTimeFlagOperation(FSbs01Pad18);
                state.SelectionMode = 10;
                return state.SelectionMode;
            }

            if (!backend.IsFlagSet(FEndEnding))
            {
                bool metVeldil = backend.IsFlagSet(FVeldilDungeonMet);
                if (metVeldil && !backend.IsFlagSet(FSbs01Pad16))
                {
                    backend.ApplyOneTimeFlagOperation(FSbs01Pad16);
                    state.SavedEntry = ToByte(FindFirstEnabledEntryId(backend, table));
                }

                if (backend.IsFlagSet(FSbs01Pad09) && !backend.IsFlagSet(SnReturnSuccessful))
                {
                    state.SelectionMode = SelectSavedOrFallback(state, 7, 8);
                    return state.SelectionMode;
                }

                if (backend.IsFlagSet(FSbs01Pad09))
                {
                    state.SelectionMode = SelectSavedOrFallback(state, 5, 6);
                    return state.SelectionMode;
                }

                if (metVeldil)
                {
                    backend.ApplyOneTimeFlagOperation(FSbs01Pad09);
                    state.SelectionMode = SelectSavedOrFallback(state, 3, 4);
                    return state.SelectionMode;
                }
            }

            byte completedEntry = SelectCompletedEntry(backend, state, table);
            if (completedEntry != 0)
            {
                state.SelectedEntry = completedEntry;
                state.SelectionMode = 2;
                return state.SelectionMode;
            }

            state.SelectionMode = (!backend.IsFlagSet(FSelfyGotEgg) || backend.IsFlagSet(FtNikoLove))
                ? (byte)0
                : (byte)1;
            return state.SelectionMode;
        }

        internal static byte SelectCompletedEntry(ITownRetailStoryEventBackend backend,
            TownRetailStorySelectionState state, TownRetailSelectionTable table)
        {
            if (CountEnabledTownEntries(backend, table) == 0) return 0;
            int selected = FindFirstEnabledPairedEntry(backend, table);
            return ToByte(selected != 0 ? selected : AdvanceToNextEnabledEntry(backend, state, table));
        }

        // The home module's counted list at 0x80017BD0 is 1, 2, 3, 6, 0.
        internal static int CountHomeHelpers(ITownRetailStoryEventBackend backend)
        {
            int count = 0;
            if (backend.IsFlagSet(5230)) count++; // Nico
            if (backend.IsFlagSet(5234)) count++; // Selfi
            if (backend.IsFlagSet(5232)) count++; // Fur
            if (backend.IsFlagSet(5236)) count++; // Mia
            return count;
        }

        /// <summary>Retail count_enabled_town_entries: records 1 through 7.</summary>
        private static int CountEnabledTownEntries(ITownRetailStoryEventBackend backend,
            TownRetailSelectionTable table)
        {
            int count = 0;
            for (int entryId = 1; entryId < 8; entryId++)
                if (backend.IsFlagSet(table.Record(entryId).EnabledFlag)) count++;
            return count;
        }

        /// <summary>Retail find_first_enabled_entry_id and D_80017BC0 ordering.</summary>
        private static int FindFirstEnabledEntryId(ITownRetailStoryEventBackend backend,
            TownRetailSelectionTable table)
        {
            for (int i = 0; i < table.FirstEntryOrderLength; i++)
            {
                byte entryId = table.FirstEntryAt(i);
                if (entryId == 0) return 0;
                if (backend.IsFlagSet(table.Record(entryId).EnabledFlag)) return entryId;
            }
            return 0;
        }

        /// <summary>Retail find_first_enabled_paired_entry; consumes the paired flag.</summary>
        private static int FindFirstEnabledPairedEntry(ITownRetailStoryEventBackend backend,
            TownRetailSelectionTable table)
        {
            for (int entryId = 1; entryId < 8; entryId++)
            {
                TownRetailSelectionRecord record = table.Record(entryId);
                if (backend.IsFlagSet(record.EnabledFlag) && !backend.IsFlagSet(record.PairedFlag))
                {
                    backend.ApplyOneTimeFlagOperation(record.PairedFlag);
                    return entryId;
                }
            }
            return 0;
        }

        /// <summary>
        /// Retail advance_to_next_enabled_entry. EntryIndex stores a position
        /// in town_enabled_entry_order, not the selected entry ID.
        /// </summary>
        private static int AdvanceToNextEnabledEntry(ITownRetailStoryEventBackend backend,
            TownRetailStorySelectionState state, TownRetailSelectionTable table)
        {
            int startIndex = unchecked((sbyte)state.EntryIndex);
            int nextIndex = startIndex + 1;
            int orderCount = table.CyclingEntryOrderLength - 1;
            if (orderCount <= 0) throw new InvalidOperationException("Retail Town cycling order is empty.");

            // The retail routine searches until it returns to its cursor. Bound
            // the managed loop to one cycle so malformed decoded data cannot hang Unity.
            for (int scanned = 0; scanned < orderCount; scanned++)
            {
                if (nextIndex < 0 || nextIndex >= orderCount) nextIndex = 0;
                byte entryId = table.CyclingEntryAt(nextIndex);
                if (backend.IsFlagSet(table.Record(entryId).EnabledFlag))
                {
                    state.EntryIndex = ToByte(nextIndex);
                    return entryId;
                }
                nextIndex++;
            }

            throw new InvalidOperationException("No enabled retail Town entry exists in the cycling order.");
        }

        private static byte SelectSavedOrFallback(TownRetailStorySelectionState state, byte savedMode,
            byte fallbackMode)
        {
            if (state.SavedEntry != 0)
            {
                state.SelectedEntry = state.SavedEntry;
                return savedMode;
            }
            return fallbackMode;
        }

        private static byte ToByte(int value) => unchecked((byte)value);
    }
}
