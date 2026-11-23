using System;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>Retail gift selection: TOWN.BIN get_group_record_table_entry / item_query_checks_g01145.c.</summary>
    public static class TownHeroineGifts
    {
        [Serializable] public sealed class Hero { public string id; public int group, loveFlag; public uint[] matrix; }
        [Serializable] public sealed class Response { public uint address; public string[] pages, portuguese; public bool accepted, increment; }
        [Serializable] public sealed class Catalog { public Hero[] heroes; public Response[] responses; }
        private static Catalog _catalog;
        public static Catalog Data => _catalog ??= JsonUtility.FromJson<Catalog>(
            Resources.Load<TextAsset>("Town/Retail/heroine-gifts").text);

        public static Hero Find(string npcId)
        {
            if (npcId == null) return null;
            if (npcId.StartsWith("pool_", StringComparison.Ordinal)) npcId = npcId[5..];
            if (npcId.StartsWith("nico_", StringComparison.Ordinal)) npcId = "nico";
            return Array.Find(Data.heroes, h => h.id == npcId);
        }

        public static Response Select(Hero hero, int kind, int subtype, int sellPrice, int count, Func<int, bool> flag)
        {
            if (hero == null || (kind == 13 && subtype is not (3 or 4)) || (kind == 1 && subtype == 8)) return null;
            if (hero.group == 9 && kind == 13 && subtype == 3)
                return flag(4811) ? Array.Find(Data.responses, r => r.address == 0x8006BC7A) : null;
            int column = kind is 11 or 4 or 6 or 7 or 8 or 9 or 18 or 13
                ? sellPrice < 100 ? 0 : sellPrice < 5000 ? 1 : 2 : 3;
            if (hero.group == 9 && kind == 13) column = 3;
            int row = flag(hero.loveFlag) ? 3 :
                (hero.group == 11 && flag(4602)) || (hero.group == 9 && flag(4603)) ||
                (hero.group == 10 && !flag(1274)) ? 0 :
                count >= 3 || (hero.group == 10 && !flag(1275)) ? 2 : 1;
            uint address = hero.matrix[row * 4 + column];
            return Array.Find(Data.responses, r => r.address == address);
        }
    }
}
