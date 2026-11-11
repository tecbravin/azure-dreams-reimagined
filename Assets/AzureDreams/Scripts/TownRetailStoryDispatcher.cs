using System;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Scene-entry audio reservations selected by story state in module 0x3E5000.
    /// Service +0x2F8 is native call 190, town_seq_reserve. These signed arguments
    /// reserve a stream and sound-driver command; they do not launch story scripts.
    /// </summary>
    public sealed class TownRetailStoryDispatcher
    {
        private readonly ITownRetailStoryEventBackend _flags;
        private readonly TownRetailStorySelectionState _state;
        private readonly Action<short, short> _dispatch;
        private readonly TownRetailSelectionTable _table;
        private readonly TownRetailStoryEventSelector _selector = new TownRetailStoryEventSelector();
        // RAM 0x800178F8, file 0x3E68F8. Entry 8 is the fallback root
        // 0x80017918. Modes 9/10 use entry 1 at 0x800178FC.
        private static readonly short[] Commands = { -1, 9, 11, 10, 13, 15, 12, 16, 39 };

        public TownRetailStoryDispatcher(ITownRetailStoryEventBackend flags,
            TownRetailStorySelectionState state, TownRetailAudioState audio)
            : this(flags, state, (audio ?? throw new ArgumentNullException(nameof(audio))).ReserveSequence)
        {
        }

        public TownRetailStoryDispatcher(ITownRetailStoryEventBackend flags,
            TownRetailStorySelectionState state, Action<short, short> dispatch)
        {
            _flags = flags ?? throw new ArgumentNullException(nameof(flags));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
            _table = TownRetailSelectionTable.CreateForScene3E5000();
        }

        /// <summary>Retail start_dispatch_event: selects and starts only at phase zero.</summary>
        public bool TryStart()
        {
            if (_state.DispatchState != 0) return false;
            _selector.Select(_flags, _state, _table);
            _state.DispatchState++;
            DispatchSelected();
            return true;
        }

        /// <summary>Retail dispatch_enabled_message_coordinates: phase one only.</summary>
        public bool DispatchEnabledMessage()
        {
            if (_state.DispatchState != 1) return false;
            DispatchSelected();
            _state.DispatchState++;
            _flags.ApplyOneTimeFlagOperation(1032); // F_sbs01_pad19
            return true;
        }

        /// <summary>
        /// Retail dispatch_active_event_27 returns true for any phase below two,
        /// even when the selected arguments do not match 0x27/0x200.
        /// </summary>
        public bool DispatchActiveEvent27()
        {
            if (_state.DispatchState >= 2) return false;
            GetSelectedArguments(out short command, out short value);
            if (command == 0x27 && value == 0x200) _dispatch(0x27, 0x200);
            return true;
        }

        private void DispatchSelected()
        {
            GetSelectedArguments(out short command, out short value);
            _dispatch(command, value);
        }

        private void GetSelectedArguments(out short command, out short value)
        {
            int mode = _state.SelectionMode;
            int index = mode == 9 || mode == 10 ? 1 :
                mode >= 3 && mode <= 8 ? 8 : mode == 2 ? _state.SelectedEntry : 0;
            if (index >= Commands.Length || (mode == 2 && index > 7))
                throw new InvalidOperationException("Selected retail Town entry is outside the module table.");
            command = Commands[index];
            value = index == 0 ? (short)-1 : (short)0x200;
        }
    }
}
