using UnityEngine;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// First ported slice of Pulunpa spawn factory (create_actor_grid_object).
    /// Initializes a fresh ActorRecord with identity, ally/enemy flags, the original
    /// sleep roll and normal/variant state selection. Attribute formulas and animation
    /// tables are intentionally not invented here.
    /// </summary>
    public sealed class PulunpaSpawn : MonoBehaviour
    {
        public enum FrameSetChoice
        {
            FrameSet1,
            DirectionFrames,
            FrameSet2
        }

        public enum CallbackChoice
        {
            NormalResume,
            VariantAction
        }

        public sealed class SpawnResult
        {
            public ActorRecord Record { get; }
            public int Flags { get; }
            public int Kind { get; }
            public int Variant { get; }
            public int SpawnValue { get; }
            public byte VariantCode { get; }
            public bool ResistSleep { get; }
            public bool SleepRollPerformed { get; }
            public bool SecondRandomConsumed { get; }
            public bool SleepApplied { get; }
            public int SleepValue { get; }
            public FrameSetChoice FrameSet { get; }
            public CallbackChoice Callback { get; }
            public byte ByteA7 { get; }

            internal SpawnResult(
                ActorRecord record,
                int flags,
                int kind,
                int variant,
                int spawnValue,
                byte variantCode,
                bool resistSleep,
                bool sleepRollPerformed,
                bool secondRandomConsumed,
                bool sleepApplied,
                int sleepValue,
                FrameSetChoice frameSet,
                CallbackChoice callback,
                byte byteA7)
            {
                Record = record;
                Flags = flags;
                Kind = kind;
                Variant = variant;
                SpawnValue = spawnValue;
                VariantCode = variantCode;
                ResistSleep = resistSleep;
                SleepRollPerformed = sleepRollPerformed;
                SecondRandomConsumed = secondRandomConsumed;
                SleepApplied = sleepApplied;
                SleepValue = sleepValue;
                FrameSet = frameSet;
                Callback = callback;
                ByteA7 = byteA7;
            }
        }

        private ActorRecord _record;

        public ActorRecord Record => _record;
        public SpawnResult LastResult { get; private set; }

        /// <summary>
        /// Port of create_actor_grid_object for a freshly allocated ActorRecord.
        /// </summary>
        public SpawnResult Initialize(
            int flags,
            int kindId,
            int variant,
            int spawnValue,
            RetailRng rng,
            byte variantCode)
        {
            if (rng == null)
                throw new System.ArgumentNullException(nameof(rng));

            ActorRecord record = new ActorRecord();
            _record = record;

            // Identity: work->unk_13 = MONSTER_PULUNPA (offset 0x13).
            record.Species = (byte)MonsterSpecies.PULUNPA;

            int kind = flags & 3;

            bool resistSleep = (record.Flags14 & 0x4000) != 0;
            bool sleepRollPerformed = false;
            bool secondRandomConsumed = false;
            bool sleepApplied = false;
            int sleepValue = 0;

            FrameSetChoice frameSet = FrameSetChoice.FrameSet1;
            CallbackChoice callback = CallbackChoice.NormalResume;
            byte byteA7 = 0;

            if (kind == 1)
            {
                // flags14 |= ACTOR_CLASS_ENABLED | ACTOR_PARTY_MEMBER
                record.Flags14 |= (int)ActorPropFlags.PlayerSide | (int)ActorPropFlags.PartyMember;
                // flags1C |= ACTOR_PLAYER_SIDE | ACTOR_PARTY_MEMBER
                record.Flags1C |= (int)ActorPropFlags.PlayerSide | (int)ActorPropFlags.PartyMember;
                frameSet = FrameSetChoice.FrameSet1;
                callback = CallbackChoice.NormalResume;
            }
            else if (kind >= 2)
            {
                record.Flags14 |= (int)ActorPropFlags.PlayerSide; // ACTOR_CLASS_ENABLED
                record.Flags1C |= (int)ActorPropFlags.PlayerSide; // ACTOR_PLAYER_SIDE
                frameSet = FrameSetChoice.FrameSet1;
                callback = CallbackChoice.NormalResume;
            }
            else if (((flags & -4) << 16) == 0)
            {
                if (!resistSleep)
                {
                    sleepRollPerformed = true;
                    int random = rng.Next();
                    if ((random & 1) != 0)
                    {
                        secondRandomConsumed = true;
                        random = rng.Next();
                        sleepValue = (random & 0x3F) | 0x20;
                        // New record: empty slots; preserve slot 3 first.
                        record.SetStatus(3, (sbyte)StatusId.SLEEP, (sbyte)sleepValue);
                        sleepApplied = true;
                        frameSet = FrameSetChoice.DirectionFrames;
                    }
                }

                if (variantCode < 10)
                {
                    callback = CallbackChoice.NormalResume;
                    frameSet = FrameSetChoice.FrameSet1;
                }
                else
                {
                    byteA7 = 1;
                    callback = CallbackChoice.VariantAction;
                    frameSet = FrameSetChoice.FrameSet2;
                }
            }
            else
            {
                callback = CallbackChoice.NormalResume;
                frameSet = FrameSetChoice.FrameSet1;
            }

            // actor->unk_9A = 0xFF; unk_9C = -1; unk_A8 = 0.
            record.SetRaw(0x9A, 0xFF);
            record.SetRaw(0x9C, 0xFF);
            record.SetRaw(0xA8, 0x00);
            if (byteA7 != 0)
                record.SetRaw(0xA7, byteA7);

            LastResult = new SpawnResult(
                record,
                flags,
                kind,
                variant,
                spawnValue,
                variantCode,
                resistSleep,
                sleepRollPerformed,
                secondRandomConsumed,
                sleepApplied,
                sleepValue,
                frameSet,
                callback,
                byteA7);

            return LastResult;
        }
    }
}