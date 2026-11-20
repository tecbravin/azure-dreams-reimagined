using System;
using System.Collections.Generic;
using System.Text;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Retail second-tower item rules (dungeon mode 2, 0x80012090 == 2).
    ///
    /// * Entering the tower calls mark_category_items_flag_400(2): every item type of the categories whose
    ///   itemCategoryTable flag byte has bit 2 set (SLUS 0x80073414: herbs, fruits, seeds, balls, scrolls,
    ///   bells, glasses, loupes, sand) loses its identification (flag 0x400), and
    ///   shuffle_enabled_item_category_pointers reshuffles the records' appearance names (record +0x0C), so
    ///   select_item_data shows "red herb", "heavy ball"... until the type is identified.  Sand bags have no
    ///   appearance names (+0x0C is a number), so they keep their names here.
    /// * build_cumulative_item_weights only admits items whose definition flag 0x40 is set while the mode is
    ///   2, and weighs every item by its rarity bits (flags &amp; 0x3000: 0x80 / 0x55 / 0x20 / 1).
    /// </summary>
    internal static class SecondTowerItems
    {
        private const string SeedKey = "tower2.item_seed";
        private const string KnownKey = "tower2.known_items";

        private static bool _active;
        private static readonly Dictionary<int, int> Appearance = new();
        private static readonly HashSet<int> Known = new();

        // Record +0x0C strings of the retail records (Japanese in SLUS), indexed by item id - 1.
        private static readonly Dictionary<byte, (string en, string pt)[]> Names = new()
        {
            [ItemSystem.Kind.Herb] = new[]
            {
                ("Red herb", "Erva vermelha"), ("Blue herb", "Erva azul"), ("Yellow herb", "Erva amarela"),
                ("Black herb", "Erva preta"), ("Green herb", "Erva verde"), ("Pink herb", "Erva rosa"),
                ("White herb", "Erva branca"), ("Gray herb", "Erva cinza"), ("Purple herb", "Erva roxa"),
                ("Light blue herb", "Erva azul-clara"), ("Gold herb", "Erva dourada"), ("Silver herb", "Erva prateada"),
                ("Orange herb", "Erva laranja"), ("Vermilion herb", "Erva escarlate"), ("Navy herb", "Erva azul-marinho"),
            },
            [ItemSystem.Kind.Fruit] = new[]
            {
                ("Red fruit", "Fruta vermelha"), ("Blue fruit", "Fruta azul"), ("Black fruit", "Fruta preta"),
                ("Green fruit", "Fruta verde"), ("Yellow fruit", "Fruta amarela"), ("Pink fruit", "Fruta rosa"),
                ("White fruit", "Fruta branca"), ("Brown fruit", "Fruta marrom"), (null, null),
                ("Light blue fruit", "Fruta azul-clara"),
            },
            [ItemSystem.Kind.Seed] = new[]
            {
                ("Red seed", "Semente vermelha"), ("Blue seed", "Semente azul"), ("Black seed", "Semente preta"),
                ("White seed", "Semente branca"), ("Yellow seed", "Semente amarela"), ("Purple seed", "Semente roxa"),
                ("Green seed", "Semente verde"), ("Light blue seed", "Semente azul-clara"),
                ("Gold seed", "Semente dourada"), ("Silver seed", "Semente prateada"),
            },
            [ItemSystem.Kind.Ball] = new[]
            {
                ("Warped ball", "Bola deformada"), ("Bright ball", "Bola brilhante"), ("Heavy ball", "Bola pesada"),
                ("Light ball", "Bola leve"), ("Pretty ball", "Bola bonita"), ("Small ball", "Bola pequena"),
                ("Large ball", "Bola grande"), ("Dirty ball", "Bola suja"), ("Slimy ball", "Bola viscosa"),
                (null, null), ("Shining ball", "Bola reluzente"), ("Rough ball", "Bola áspera"),
                ("Clear ball", "Bola transparente"), ("Bumpy ball", "Bola irregular"), (null, null), (null, null),
                ("Smooth ball", "Bola lisa"),
            },
            [ItemSystem.Kind.Scroll] = new[]
            {
                ("Luxurious scroll", "Pergaminho luxuoso"), ("Tattered scroll", "Pergaminho esfarrapado"),
                ("Old scroll", "Pergaminho velho"), ("Miraculous scroll", "Pergaminho milagroso"),
                ("New scroll", "Pergaminho novo"), ("Thin scroll", "Pergaminho fino"), ("Thick scroll", "Pergaminho grosso"),
            },
            [ItemSystem.Kind.Bell] = new[]
            {
                ("Large bell", "Sino grande"), ("Small bell", "Sino pequeno"), ("Old bell", "Sino velho"),
            },
            [ItemSystem.Kind.Glasses] = new[]
            {
                ("Round glasses", "Óculos redondos"), ("Square glasses", "Óculos quadrados"),
            },
            [ItemSystem.Kind.Loupe] = new[]
            {
                ("Light blue loupe", "Lupa azul-clara"), ("Purple loupe", "Lupa roxa"), ("Brown loupe", "Lupa marrom"),
                ("White loupe", "Lupa branca"), ("Black loupe", "Lupa preta"),
            },
        };

        internal static bool Active => _active;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => End();

        private static int Key(byte kind, byte subtype) => kind * 256 + subtype;

        private static bool HasAppearance(byte kind, byte subtype) =>
            Names.TryGetValue(kind, out var names) && subtype >= 1 && subtype <= names.Length && names[subtype - 1].en != null;

        /// <summary>Enter the second tower: a fresh entry reshuffles, a resumed save restores the stored run.</summary>
        internal static void Begin(bool freshEntry, int newSeed)
        {
            int seed = newSeed;
            Known.Clear();
            if (!freshEntry && TownSaveVariables.HasValue(SeedKey))
            {
                seed = TownSaveVariables.GetInt(SeedKey);
                foreach (var token in TownSaveVariables.GetString(KnownKey).Split(',', StringSplitOptions.RemoveEmptyEntries))
                    if (int.TryParse(token, out int key)) Known.Add(key);
            }
            TownSaveVariables.SetInt(SeedKey, seed);
            SaveKnown();
            Shuffle(seed);
            _active = true;
        }

        internal static void End()
        {
            _active = false;
            Known.Clear();
            Appearance.Clear();
        }

        private static void Shuffle(int seed)
        {
            Appearance.Clear();
            foreach (var pair in Names)
            {
                var ids = new List<byte>();
                for (int i = 0; i < pair.Value.Length; i++)
                    if (pair.Value[i].en != null) ids.Add((byte)(i + 1));
                var faces = new List<byte>(ids);
                var random = new Random(seed ^ (pair.Key * 0x9E37));
                for (int i = faces.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (faces[i], faces[j]) = (faces[j], faces[i]);
                }
                for (int i = 0; i < ids.Count; i++) Appearance[Key(pair.Key, ids[i])] = faces[i];
            }
        }

        internal static bool IsUnknown(DungeonItemEntry item) =>
            _active && item != null && HasAppearance(item.Kind, item.Subtype) && !Known.Contains(Key(item.Kind, item.Subtype));

        internal static string AppearanceName(DungeonItemEntry item)
        {
            var names = Names[item.Kind];
            int face = Appearance.TryGetValue(Key(item.Kind, item.Subtype), out int f) ? f : item.Subtype;
            var name = names[face - 1];
            return GameLanguage.Portuguese ? name.pt : name.en;
        }

        /// <summary>Identifies the item's whole type (use, Truth Glasses).</summary>
        internal static bool Learn(DungeonItemEntry item)
        {
            if (!IsUnknown(item)) return false;
            Known.Add(Key(item.Kind, item.Subtype));
            SaveKnown();
            return true;
        }

        private static void SaveKnown()
        {
            var text = new StringBuilder();
            foreach (int key in Known) text.Append(key).Append(',');
            TownSaveVariables.SetString(KnownKey, text.ToString());
        }

        // (kind, id, rarity weight, mode-2 only) from the SLUS item records, categories 1-10 and 15-17.
        // Records flagged 0x10 (never generated) are left out.  Eggs come from the port's egg roll.
        private static readonly (byte kind, byte id, int weight, bool tower2)[] Loot =
        {
            (1,2,128,false),(1,3,128,false),(1,4,128,false),(1,5,32,false),(1,6,128,false),(1,7,128,true),
            (1,9,128,true),(1,10,128,true),(1,11,128,true),(1,12,128,true),(1,13,128,true),(1,14,128,false),
            (1,15,128,false),(2,1,128,true),(2,2,1,true),(2,3,85,false),(2,4,85,false),(2,5,32,false),
            (2,6,85,false),(2,7,1,false),(2,8,85,false),(2,10,85,true),(3,1,32,false),(3,2,32,false),
            (3,3,32,false),(3,4,128,false),(3,5,85,false),(3,6,85,false),(3,7,85,false),(3,8,128,true),
            (3,9,128,true),(3,10,32,true),(4,1,32,false),(4,2,32,false),(4,3,32,false),(4,4,32,false),
            (4,5,32,false),(4,6,32,false),(4,7,32,false),(4,8,32,false),(4,9,32,false),(4,11,32,false),
            (4,12,32,false),(4,13,32,false),(4,14,32,false),(4,17,32,false),(5,1,85,false),(5,2,85,false),
            (5,3,32,false),(5,4,32,false),(5,5,128,false),(5,6,128,true),(5,7,32,true),(6,1,32,false),
            (6,2,32,false),(6,3,32,false),(7,1,85,false),(7,2,32,false),(7,3,32,false),(8,1,128,false),
            (8,2,128,false),(9,1,128,false),(9,2,128,false),(9,4,128,false),(9,5,128,false),(10,1,85,false),
            (10,2,85,false),(10,3,85,false),(15,1,85,false),(15,2,128,false),(15,3,128,false),(15,4,85,false),
            (15,5,32,false),(15,6,32,false),(15,7,32,false),(15,8,32,false),(15,9,1,false),(15,10,1,false),
            (15,13,32,false),(15,14,32,false),(15,15,1,false),(16,1,128,false),(16,2,1,true),(16,3,32,false),
            (16,4,85,false),(16,5,32,false),(16,6,85,false),(16,7,85,false),(16,8,85,false),(16,9,32,false),
            (17,1,128,false),(17,2,128,false),(17,3,32,false),(17,4,85,false),(17,5,85,false),(17,6,85,false),
            (17,7,32,false),(17,8,32,false),(17,9,32,false),(17,10,32,false),(17,11,32,false),
        };

        private static int _lootTotal = -1;

        /// <summary>select_weighted_floor_item for mode 2 (every record, flag-0x40 ones included).</summary>
        internal static DungeonItemEntry RollLoot(Func<int, int> roll)
        {
            if (_lootTotal < 0)
            {
                _lootTotal = 0;
                foreach (var entry in Loot) _lootTotal += entry.weight;
            }
            int pick = roll(_lootTotal);
            foreach (var entry in Loot)
            {
                pick -= entry.weight;
                if (pick >= 0) continue;
                if (entry.kind is ItemSystem.Kind.Sword or ItemSystem.Kind.Wand or ItemSystem.Kind.Shield)
                    return TowerItemTrapManager.CreateTowerEquipment(entry.kind, entry.id, roll(1024));
                sbyte param = entry.kind == ItemSystem.Kind.Ball ? (sbyte)(entry.id == 17 ? 1 : 4 + roll(4)) : (sbyte)0;
                return new DungeonItemEntry(entry.id, entry.kind, param, 0);
            }
            return new DungeonItemEntry(15, ItemSystem.Kind.Herb, 0, 0);
        }
    }
}
