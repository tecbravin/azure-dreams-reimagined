using System;
namespace AzureDreamsReimagined
{
    /// <summary>Port of town/scene/dialogue/select_next_dialogue_line.c.
    /// Returns original text addresses; unresolved service queries are explicit inputs.</summary>
    public static class TownHomeDialogueSelector
    {
        public struct Context
        {
            public int Progress, Gold, HouseQuery, FamiliarA, FamiliarB;
            public int EventState, SelectionMode, DispatchState, SelectedEntry, EnabledRecords;
        }
        public static uint Select(Context c, Func<int, bool> flag, Action<int> set)
        {
            int initial = 0;
            if (!flag(TownReputation.WreathSpokenFlag)) set(TownReputation.WreathSpokenFlag); else initial = 1;
            if (!flag(initial) && c.Progress != 0)
            {
                if (c.Progress >= 5 && !flag(5214))
                { if (!flag(158)) { set(158); return 0x80017152; } return 0x800172A3; }
                if (flag(5216) && !flag(1533) && !flag(1531))
                { if (!flag(159)) { set(159); return 0x800173E4; } return 0x80017501; }
                if (unchecked((uint)c.Gold) >= 10000 && !flag(5321)) return 0x800164CC;
                if (unchecked((uint)c.Gold) >= 20000 && c.HouseQuery == 0) return 0x8001688A;
                if (c.HouseQuery > 0 && c.FamiliarA != 0x12 && c.FamiliarB != 0x12 && !flag(174))
                { set(174); return 0x80016E0C; }
                if (c.EventState == 2 && c.SelectionMode == 2 && c.DispatchState == 1)
                {
                    uint response = HomeHelpersLine(c.HouseQuery, c.EnabledRecords, c.SelectedEntry);
                    if (response != 0) return response;
                }
            }
            return flag(22) ? 0x80016359u : 0x8001602Au;
        }

        internal static uint HomeHelpersLine(int homeRank, int count, int entry)
        {
            if (homeRank == 2 && count >= 3) return 0x80016D77;
            if (homeRank == 2 && count != 1) return 0;
            return entry switch
            {
                1 => 0x80016BCF, 2 => 0x80016C77, 3 => 0x80016C24, 6 => 0x80016CCE, _ => 0u
            };
        }
    }
}
