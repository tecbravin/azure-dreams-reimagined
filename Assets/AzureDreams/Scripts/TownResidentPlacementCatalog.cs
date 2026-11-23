using System;
using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Stable resident cells for the captured town interiors and the exterior
    /// story actors.  The entries mirror the authored scene positions rather
    /// than relying on the nearest free tile at load time.
    /// </summary>
    internal static class TownResidentPlacementCatalog
    {
        internal readonly struct InteriorPlacement
        {
            internal readonly string RoomId;
            internal readonly string NpcId;
            internal readonly int Occurrence;
            internal readonly Vector2Int Cell;

            internal InteriorPlacement(string roomId, string npcId, int occurrence, int x, int z)
            {
                RoomId = roomId;
                NpcId = npcId;
                Occurrence = occurrence;
                Cell = new Vector2Int(x, z);
            }
        }

        // Source: residentPrefabs/residentCells in the captured interior scenes.
        // Nico's house did not serialize a residentCells entry; its position is
        // deliberately fixed here rather than depending on free-cell search.
        private static readonly InteriorPlacement[] InteriorPlacements =
        {
            new("Interior_01_koh_house_1", "wreath", 0, 19, 11),
            new("Interior_01_koh_house_1", "weedy", 0, 17, 11),
            new("Interior_02_koh_house_2", "wreath", 0, 33, 12),
            new("Interior_02_koh_house_2", "weedy", 0, 26, 12),
            new("Interior_03_koh_house_3", "wreath", 0, 53, 12),
            new("Interior_03_koh_house_3", "weedy", 0, 46, 15),
            new("Interior_06_casino_2", "fur", 0, 20, 9),
            // The retail wheel fills X/Z 10.5-16.5 around (13.5, 13.5); keep its residents off it.
            new("Interior_06_casino_2", "monster_shop_hunter", 0, 11, 17),
            new("Interior_06_casino_2", "dar", 0, 22, 11),
            new("Interior_06_casino_2", "casino_broke", 0, 17, 19),
            new("Interior_07_library", "mia", 0, 21, 17),
            new("Interior_07_library", "library_keeper", 0, 19, 10),
            new("Interior_07_library", "library_reader_woman", 0, 13, 15),
            new("Interior_07_library", "library_reader_man", 0, 26, 16),
            // Gym: the thrower stands at the retail barbell origin (X 0x2A0, Y 0x380).
            new("Interior_08_gym", "gym_kim_little", 0, 10, 14),
            new("Interior_08_gym", "gym_kim_big", 0, 10, 12),
            new("Interior_08_gym", "gym_hunter", 0, 16, 17),
            new("Interior_08_gym", "gym_barry", 0, 17, 10),
            new("Interior_08_gym", "gym_woman", 0, 11, 16),
            new("Interior_08_gym", "gym_legs", 0, 12, 10),
            new("Interior_08_gym", "gym_developer", 0, 17, 15),
            new("Interior_0a_racetrack_entrance", "race_clerk", 0, 22, 10),
            new("Interior_0a_racetrack_entrance", "race_silver", 0, 19, 14),
            new("Interior_0a_racetrack_entrance", "race_bet", 0, 25, 13),
            new("Interior_0a_racetrack_entrance", "race_husband", 0, 24, 15),
            new("Interior_0b_theater", "vivian", 0, 17, 15),
            new("Interior_18_cherrl_house", "nadia", 0, 20, 16),
            new("Interior_0c_alley", "fur", 0, 28, 17),
            new("Interior_0c_alley", "monster_shop_hunter", 0, 12, 17),
            new("Interior_0c_alley", "bet", 0, 27, 14),
            new("Interior_0c_alley", "hush", 0, 29, 13),
            // Restored Woogie Woogie Pool.  These are kept separate from the
            // generic closest-free-cell search so the swimmers remain spread
            // around the pool and the Mia scene has a clear conversation spot.
            new("Interior_0d_pool_quest_not_done", "wookie", 0, 16, 14),
            new("Interior_0d_pool_quest_done", "wookie", 0, 16, 14),
            new("Interior_0d_pool_quest_done", "pool_selfi", 0, 36, 24),
            new("Interior_0d_pool_quest_done", "pool_patty", 0, 26, 33),
            new("Interior_0d_pool_quest_done", "pool_nico", 0, 19, 22),
            new("Interior_0d_pool_quest_done", "pool_fur", 0, 30, 22),
            new("Interior_0d_pool_quest_done", "pool_visitor", 0, 35, 15),
            new("Interior_0d_pool_quest_done", "mia", 0, 24, 15),
            new("Interior_0d_pool_quest_done", "pool_ghosh", 0, 28, 15),
            new("Interior_0d_pool_quest_done", "vivian", 0, 34, 19),
            new("Interior_0d_pool_quest_done", "pool_cherrl", 0, 17, 25),
            // Retail pool residents (overlay 0x51E000). Native positions (OSETDT) are not
            // decoded yet; these are free platform/water cells of the captured pool.
            new("Interior_0d_pool_quest_done", "pool_barry", 0, 13, 16),
            new("Interior_0d_pool_quest_done", "pool_kim_big", 0, 14, 17),
            new("Interior_0d_pool_quest_done", "pool_kim_little", 0, 12, 17),
            new("Interior_0d_pool_quest_done", "pool_swimmer_a", 0, 20, 20),
            new("Interior_0d_pool_quest_done", "pool_swimmer_b", 0, 25, 24),
            new("Interior_0d_pool_quest_done", "pool_girl", 0, 13, 28),
            new("Interior_0d_pool_quest_done", "pool_kid", 0, 15, 29),
            new("Interior_0d_pool_quest_done", "pool_grandpa", 0, 22, 28),
            new("Interior_0d_pool_quest_done", "pool_woman", 0, 32, 30),
            new("Interior_0d_pool_quest_done", "pool_hunter", 0, 37, 16),
            new("Interior_0d_pool_quest_done", "pool_aunt", 0, 32, 25),
            new("Interior_0d_pool_quest_done", "pool_man", 0, 37, 28),
            new("Interior_0d_pool_quest_done", "pool_grandma", 0, 38, 31),
            new("Interior_0d_pool_quest_done", "pool_heckler_big", 0, 36, 20),
            new("Interior_0d_pool_quest_done", "pool_heckler_little", 0, 37, 21),
            new("Interior_0e_restaurant", "patty", 0, 17, 12),
            new("Interior_0e_restaurant", "coppe", 0, 31, 12),
            new("Interior_0e_restaurant", "dar", 0, 11, 12),
            new("Interior_0e_restaurant", "bet", 0, 15, 12),
            new("Interior_0e_restaurant", "restaurant_hunter", 0, 21, 12),
            new("Interior_0f_hospital", "hush", 0, 15, 13),
            new("Interior_10_hospital_2", "hush", 0, 20, 13),
            new("Interior_11_temple", "jorda", 0, 17, 9),
            new("Interior_12_temple_2", "jorda", 0, 23, 10),
            new("Interior_13_carpenter", "tonka", 0, 15, 10),
            // Fon waits beside Tonka's counter while the theater commission is pending.
            new("Interior_13_carpenter", "carpenter_fon", 0, 21, 10),
            new("Interior_13_carpenter", "construction_pen", 0, 20, 13),
            new("Interior_14_monster_shop", "master", 0, 16, 10),
            new("Interior_14_monster_shop", "monster_shop_hunter", 0, 19, 13),
            new("Interior_14_monster_shop", "monster_shop_hunter", 1, 11, 14),
            new("Interior_14_monster_shop", "monster_shop_hunter", 2, 20, 16),
            new("Interior_15_blacksmith", "barry", 0, 15, 10),
            new("Interior_16_general_store", "fur", 0, 13, 10),
            new("Interior_17_fortune_house", "shiela", 0, 16, 12),
            new("Interior_18_cherrl_house", "cherrl", 0, 14, 14),
            // Original bar seating, aligned to the captured counter and tables.
            new("Interior_20_bar", "vivian", 0, 17, 14),
            new("Interior_20_bar", "bar_barten", 0, 18, 10),
            new("Interior_20_bar", "okami", 0, 14, 10),
            new("Interior_20_bar", "bar_hunter_1", 0, 20, 12),
            new("Interior_20_bar", "bar_hunter_2", 0, 13, 13),
            new("Interior_20_bar", "bar_hunter_3", 0, 23, 13),
            new("Interior_21_rhodes_house", "rhodes_butler", 0, 18, 18),
            new("Interior_21_rhodes_house", "ghosh", 0, 19, 17),
            new("Interior_21_rhodes_house", "selfi", 0, 16, 17),
            new("Interior_22_nico_house", "nico", 0, 15, 12),
            new("Interior_22_nico_house", "nico_dad", 0, 17, 12),
            new("Interior_23_generic_house", "aunt", 0, 17, 12),
            new("Interior_23_generic_house", "grandpa", 0, 15, 12),
            new("Interior_24_dr_hal_house", "hal", 0, 16, 11),
            new("Interior_2b_casino_1", "fur", 0, 19, 10),
            new("Interior_2b_casino_1", "monster_shop_hunter", 0, 11, 15),
            new("Interior_2b_casino_1", "slot_lucky_woman", 0, 11, 13),
            new("Interior_2b_casino_1", "slot_old_man", 0, 19, 15),
            new("Interior_2c_silver_house", "silver", 0, 16, 12),
        };

        // These are the established exterior story positions.  Selfi keeps a
        // small ordered set because plants and the nearby tree can block a cell.
        private static readonly Dictionary<string, Vector2Int[]> TownPlacements = new(StringComparer.Ordinal)
        {
            // Isaac's original field/windmill position.
            ["isaac"] = new[] { new Vector2Int(75, 93) },
            ["selfi"] = new[] { new Vector2Int(86, 53), new Vector2Int(87, 54), new Vector2Int(86, 55), new Vector2Int(85, 55) },
            ["nico"] = new[] { new Vector2Int(58, 72) },
            ["chero"] = new[] { new Vector2Int(58, 72) },
            ["nico_ghosh"] = new[] { new Vector2Int(60, 72) },
            ["nico_house_guard"] = new[] { new Vector2Int(72, 58) },
            ["wookie"] = new[] { new Vector2Int(70, 79) },
            ["mia"] = new[] { new Vector2Int(96, 40) },
        };

        internal static bool TryGetInteriorCell(string roomId, string npcId, int occurrence, out Vector2Int cell)
        {
            foreach (var placement in InteriorPlacements)
                if (placement.RoomId == roomId && placement.NpcId == npcId && placement.Occurrence == occurrence)
                {
                    cell = placement.Cell;
                    return true;
                }
            cell = default;
            return false;
        }

        internal static IReadOnlyList<Vector2Int> GetTownCells(string npcId) =>
            TownPlacements.TryGetValue(npcId, out var cells) ? cells : Array.Empty<Vector2Int>();
    }
}
