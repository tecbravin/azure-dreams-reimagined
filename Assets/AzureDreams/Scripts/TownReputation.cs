using System;

namespace AzureDreamsReimagined
{
    // Retail uses the maximum floor and independent quest flags as dialogue inputs.
    internal sealed class TownReputation : ITownRetailStoryEventBackend
    {
        internal const string HighestFloorKey = "save.highest_floor";
        internal const string WakeEntryKey = "town.story.wake.entry";
        internal const string WakeCursorKey = "town.story.wake.cursor";
        internal const int WreathSpokenFlag = 160; // Ft_home01_rees_once
        internal const int NicoWakeUpFlag = 147; // F_sb01s001_niko_wake_up_end
        private const int CherrlNurseFlag = 4605; // Fp_cheriru_kangofu

        // Indexed in save-icon order. The wake-up module has a different entry order.
        private static readonly (string Id, int CompletionFlag)[] Routes =
        {
            ("nico", 5230), ("selfi", 5234), ("fur", 5232), ("vivian", 5238),
            ("cherrl", 5240), ("patty", 5242), ("mia", 5236)
        };

        private readonly TownRetailScriptFlags _flags;
        private readonly int _completedRoutes;

        internal TownReputation(int highestFloor, int completedRoutes, TownRetailScriptFlags flags)
        {
            HighestFloor = highestFloor;
            _completedRoutes = completedRoutes;
            _flags = flags;
        }

        internal int HighestFloor { get; }

        internal static TownReputation Read()
        {
            var flags = TownRetailScriptFlags.CreatePersistent();
            int floor = TownSaveVariables.GetInt(HighestFloorKey);
            if (TownSaveVariables.GetBool("tower.beldo_defeated")) floor = TowerFloorRules.BeldoFloor;
            return new TownReputation(floor, CompletedRoutes(key => TownSaveVariables.GetInt(key), flags.IsFlagSet), flags);
        }

        internal static int CompletedRoutes(Func<string, int> stage, Func<int, bool> hasFlag)
        {
            int mask = 0;
            for (int i = 0; i < Routes.Length; i++)
            {
                var route = Routes[i];
                if (stage("town.story." + route.Id + ".stage") >= 100 || hasFlag(route.CompletionFlag))
                    mask |= 1 << i;
            }
            return mask;
        }

        public bool IsFlagSet(int id)
        {
            if (_flags.IsFlagSet(id)) return true;
            // A completed Unity Cherrl route includes her decision to become a nurse.
            if (id == CherrlNurseFlag) return (_completedRoutes & (1 << 4)) != 0;
            for (int i = 0; i < Routes.Length; i++)
                if (Routes[i].CompletionFlag == id) return (_completedRoutes & (1 << i)) != 0;
            return false;
        }

        public void ApplyOneTimeFlagOperation(int id) => _flags.SetBit(id);
        public void ApplyEventFlagOperation(int id) => _flags.ClearBit(id);
    }
}
