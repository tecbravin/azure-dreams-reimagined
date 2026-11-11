using System;
using System.Collections.Generic;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Runtime callbacks needed by the retail Town event scheduler. The Unity
    /// port supplies these only after its script flag/event adapters are ready.
    /// </summary>
    public interface ITownRetailEventBackend
    {
        bool IsFlagSet(int flagId);
        void ResetEventState(int stateId);
        void DispatchEvent(int eventId);
    }

    /// <summary>
    /// Ports the seven-row event cycle at TOWN.BIN D_80019088 (file offset
    /// 0x521088), dispatched by event_state_dispatch_g00731.c.
    /// The caller owns/persists CurrentIndex and decides when Advance is called.
    /// </summary>
    public sealed class TownRetailEventScheduler
    {
        public readonly struct Entry
        {
            public readonly int EventId;
            public readonly int ConditionFlag;
            public readonly bool Enabled;

            public Entry(int eventId, int conditionFlag, bool enabled)
            {
                EventId = eventId;
                ConditionFlag = conditionFlag;
                Enabled = enabled;
            }
        }

        private static readonly Entry[] RetailEntries =
        {
            new Entry(0x0607, 0x11FA, false), // Fp_niko_kenkatyu
            new Entry(0x0608, 0x11FB, false), // Fp_far_kenkatyu
            new Entry(0x0609, 0x1472, true),  // Ft_selfy_love
            new Entry(0x060A, 0x11FD, true),  // Fp_cheriru_kangofu
            new Entry(0x060B, 0x11FE, true),  // Fp_bibian_pool
            new Entry(0x060C, 0x11FF, true),  // Fp_mia_pool
            new Entry(0x060D, 0x147A, true)   // Ft_paty_love
        };
        private static readonly IReadOnlyList<Entry> RetailEntriesView = Array.AsReadOnly(RetailEntries);

        private const int BibianPoolEventFlag = 0x1200;
        private const int MiaPoolEventFlag = 0x1201;
        private const int FixedStateA = 1550; // F_sbs13_pad21
        private const int FixedStateB = 1542; // F_sbs13_pad13
        private const int FixedStateC = 1553; // F_sbs13_pad24

        public int CurrentIndex { get; private set; }
        public static IReadOnlyList<Entry> Entries => RetailEntriesView;

        /// <param name="currentIndex">Retail cursor value before the next cycle.</param>
        public TownRetailEventScheduler(int currentIndex)
        {
            CurrentIndex = ModuloSeven(currentIndex);
        }

        /// <summary>
        /// Advances one retail cycle and reports its dispatch sequence. Returns
        /// false when the pool-event guard blocks scanning; resets still occur.
        /// </summary>
        public bool Advance(ITownRetailEventBackend backend, IList<int> dispatchedEvents = null)
        {
            if (backend == null) throw new ArgumentNullException(nameof(backend));

            CurrentIndex = ModuloSeven(CurrentIndex + 1);
            for (int i = 0; i < RetailEntries.Length; i++)
                backend.ResetEventState(RetailEntries[i].EventId);
            backend.ResetEventState(FixedStateA);
            backend.ResetEventState(FixedStateB);
            backend.ResetEventState(FixedStateC);

            if (backend.IsFlagSet(BibianPoolEventFlag) || backend.IsFlagSet(MiaPoolEventFlag))
                return false;

            for (int checkedCount = 0; checkedCount < RetailEntries.Length; checkedCount++)
            {
                Entry entry = RetailEntries[CurrentIndex];
                bool conditionIsClear = !backend.IsFlagSet(entry.ConditionFlag);
                if (conditionIsClear != entry.Enabled)
                {
                    Dispatch(backend, dispatchedEvents, entry.EventId);
                    break;
                }

                CurrentIndex = ModuloSeven(CurrentIndex + 1);
            }

            if (CurrentIndex != 5)
                Dispatch(backend, dispatchedEvents, FixedStateA);
            Dispatch(backend, dispatchedEvents, FixedStateB);
            return true;
        }

        private static void Dispatch(ITownRetailEventBackend backend, IList<int> output, int eventId)
        {
            backend.DispatchEvent(eventId);
            output?.Add(eventId);
        }

        private static int ModuloSeven(int value) => (value % RetailEntries.Length + RetailEntries.Length) % RetailEntries.Length;
    }
}
