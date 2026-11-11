using System;

namespace AzureDreamsReimagined
{
    public interface ITownRetailAudioBackend
    {
        void SendCommand(ushort command);
        void DrainCommands();
        void SelectStream(short streamIndex, short mode);
    }

    /// <summary>The four signed halfwords shared by Town audio selection records.</summary>
    public struct TownRetailSoundSelection
    {
        public short Bank;
        public short EffectCommand;
        public short Stream;
        public short SequenceCommand;

        public TownRetailSoundSelection(short bank, short effectCommand, short stream, short sequenceCommand)
        {
            Bank = bank;
            EffectCommand = effectCommand;
            Stream = stream;
            SequenceCommand = sequenceCommand;
        }

        public static TownRetailSoundSelection Unset => new TownRetailSoundSelection(-1, -1, -1, -1);
    }

    /// <summary>
    /// Ports town_seq_reserve and apply_sound_selections_and_update. Map audio
    /// records and stream-to-asset resolution are supplied by the caller.
    /// Reserving a pair does not play sound; Apply performs the driver calls.
    /// </summary>
    public sealed class TownRetailAudioState
    {
        private TownRetailSoundSelection _pending = TownRetailSoundSelection.Unset;
        private TownRetailSoundSelection _active = TownRetailSoundSelection.Unset;
        private TownRetailSoundSelection _saved = TownRetailSoundSelection.Unset;

        public TownRetailSoundSelection Pending => _pending;
        public TownRetailSoundSelection Active => _active;
        public TownRetailSoundSelection Saved => _saved;
        public bool HoldCurrentSequence { get; set; }

        public void SetMapSelection(TownRetailSoundSelection selection) => _active = selection;

        /// <summary>FNO_town_seq_reserve (native call 190 / service offset 0x2F8).</summary>
        public void ReserveSequence(short streamIndex, short sequenceCommand)
        {
            _pending.Stream = streamIndex;
            _pending.SequenceCommand = sequenceCommand;
        }

        public void Apply(ITownRetailAudioBackend backend)
        {
            if (backend == null) throw new ArgumentNullException(nameof(backend));
            ResolvePending(ref _pending.EffectCommand, ref _active.EffectCommand);
            ResolvePending(ref _pending.Stream, ref _active.Stream);
            ResolvePending(ref _pending.SequenceCommand, ref _active.SequenceCommand);

            if (_active.Stream != -1)
            {
                if (_saved.Stream != _active.Stream)
                {
                    StopAndDrain(backend);
                    backend.SelectStream(_active.Stream, 0);
                    if (_active.SequenceCommand != -1) SendSequence(backend);
                }
                else if (_active.SequenceCommand != -1)
                {
                    if (_saved.SequenceCommand != _active.SequenceCommand || !HoldCurrentSequence)
                    {
                        StopAndDrain(backend);
                        SendSequence(backend);
                    }
                }
                else StopAndDrain(backend);
            }
            else
            {
                StopAndDrain(backend);
                if (_active.SequenceCommand != -1) SendSequence(backend);
            }

            if (_active.EffectCommand != -1)
                backend.SendCommand(unchecked((ushort)_active.EffectCommand));
            _pending = TownRetailSoundSelection.Unset;
            HoldCurrentSequence = false;
            _saved = _active;
        }

        private static void ResolvePending(ref short pending, ref short active)
        {
            if (pending == -5)
            {
                pending = -1;
                active = -1;
            }
            if (pending != -1) active = pending;
        }

        private static void StopAndDrain(ITownRetailAudioBackend backend)
        {
            backend.SendCommand(0x71);
            backend.DrainCommands();
        }

        private void SendSequence(ITownRetailAudioBackend backend) =>
            backend.SendCommand(unchecked((ushort)_active.SequenceCommand));
    }
}
