using System;
using AzureDreams.Core;
using Species = AzureDreams.Core.MonsterSpecies;

namespace AzureDreamsReimagined
{
    public static class TowerEncounterTable
    {
        internal const byte TrollSword = 13;
        internal const byte TrollHammer = 14;
        internal const byte TrollBowGun = 15;

        // Baldwin's Enemy Monster Compendium. Equipment variants share one species.
        private static readonly Species[][] Floors =
        {
            new[] { Species.NOISE, Species.PULUNPA, Species.TROLL }, // 1F
            Array.Empty<Species>(), // 2F
            new[] { Species.CYCLONE, Species.FLAME, Species.PULUNPA, Species.TROLL }, // 3F
            new[] { Species.BALOON, Species.CYCLONE, Species.FLAME, Species.TROLL }, // 4F
            new[] { Species.BALOON, Species.CYCLONE, Species.FLAME, Species.MANOEVA }, // 5F
            new[] { Species.BALOON, Species.BLUME, Species.CYCLONE, Species.MANOEVA }, // 6F
            new[] { Species.BALOON, Species.BLUME, Species.MANOEVA, Species.U_BOAT }, // 7F
            new[] { Species.BLUME, Species.CLOWN, Species.MANOEVA, Species.U_BOAT }, // 8F
            new[] { Species.BLUME, Species.CLOWN, Species.DREAMIN, Species.U_BOAT }, // 9F
            new[] { Species.CLOWN, Species.DREAMIN, Species.TROLL, Species.VOLCANO }, // 10F
            new[] { Species.DREAMIN, Species.GRIFFON, Species.TROLL, Species.VOLCANO }, // 11F
            new[] { Species.DREAMIN, Species.GRIFFON, Species.KRAKEN, Species.VOLCANO }, // 12F
            new[] { Species.GRIFFON, Species.KRAKEN, Species.NYUEL, Species.VOLCANO }, // 13F
            new[] { Species.GARUDA, Species.KRAKEN, Species.NYUEL, Species.TROLL }, // 14F
            new[] { Species.GARUDA, Species.KRAKEN, Species.NYUEL, Species.TROLL }, // 15F
            new[] { Species.BARONG, Species.GARUDA, Species.MANOEVA, Species.TROLL }, // 16F
            new[] { Species.GARUDA, Species.MANOEVA, Species.PICKET, Species.TROLL }, // 17F
            new[] { Species.ARACHNE, Species.MANOEVA, Species.PICKET }, // 18F
            new[] { Species.ARACHNE, Species.MANOEVA, Species.PICKET, Species.WEADOG }, // 19F
            new[] { Species.ARACHNE, Species.UNICORN, Species.VIPER, Species.WEADOG }, // 20F
            new[] { Species.PULUNPA, Species.UNICORN, Species.VIPER, Species.WEADOG }, // 21F
            new[] { Species.BLOCK, Species.PULUNPA, Species.UNICORN, Species.VIPER }, // 22F
            new[] { Species.BLOCK, Species.STEALTH, Species.UNICORN, Species.VIPER }, // 23F
            new[] { Species.BLOCK, Species.STEALTH, Species.VIPER, Species.ZU }, // 24F
            new[] { Species.BLOCK, Species.PICKET, Species.STEALTH, Species.ZU }, // 25F
            new[] { Species.BARONG, Species.MANDARA, Species.SNOWMAN, Species.ZU }, // 26F
            new[] { Species.MANDARA, Species.MANOEVA, Species.SNOWMAN, Species.ZU }, // 27F
            new[] { Species.MANDARA, Species.MANOEVA, Species.NAPLASS, Species.SNOWMAN }, // 28F
            new[] { Species.KILLER, Species.MANOEVA, Species.NAPLASS, Species.SNOWMAN }, // 29F
            new[] { Species.KILLER, Species.MANOEVA, Species.NAPLASS, Species.TYRANT }, // 30F
            new[] { Species.DRAGON, Species.KILLER, Species.NAPLASS, Species.TYRANT }, // 31F
            new[] { Species.DRAGON, Species.GLACIER, Species.KILLER, Species.TYRANT }, // 32F
            new[] { Species.DRAGON, Species.GLACIER, Species.GOLEM, Species.TYRANT }, // 33F
            new[] { Species.DRAGON, Species.GLACIER, Species.GOLEM, Species.TYRANT }, // 34F
            new[] { Species.DRAGON, Species.GLACIER, Species.GOLEM, Species.MAXIMUM }, // 35F
            new[] { Species.BARONG, Species.GLACIER, Species.GOLEM, Species.MAXIMUM }, // 36F
            new[] { Species.DRAGON, Species.GLACIER, Species.GOLEM, Species.MAXIMUM }, // 37F
            new[] { Species.DRAGON, Species.GLACIER, Species.GOLEM, Species.MAXIMUM }, // 38F
            new[] { Species.DRAGON, Species.GLACIER, Species.GOLEM, Species.MAXIMUM }, // 39F
            Array.Empty<Species>(), // 40F
        };

        internal static int FirstFloorCount => Floors[0].Length;

        internal static byte RollTrollWeapon(int floor, RetailRng rng)
        {
            return floor switch
            {
                1 or 3 or 4 => TrollHammer,
                10 or 11 => TrollBowGun,
                14 or 15 or 16 => TrollSword,
                // Skrybe's floor 17 weights are 2 hammer, 3 bow, 2 sword out of 16 spawns.
                17 => RollIndex(7, rng) switch { < 2 => TrollHammer, < 5 => TrollBowGun, _ => TrollSword },
                _ => 0
            };
        }

        public static Species Roll(int floor, RetailRng rng)
        {
            // The current run can continue beyond the documented 40 floors.
            if (floor > Floors.Length) floor = 39;
            if (floor < 1 || Floors[floor - 1].Length == 0)
                throw new ArgumentOutOfRangeException(nameof(floor));

            var pool = Floors[floor - 1];
            return pool[RollIndex(pool.Length, rng)];
        }

        private static int RollIndex(int count, RetailRng rng)
        {
            int limit = 32768 - 32768 % count;
            int roll;
            do { roll = rng.Next(); } while (roll >= limit);
            return roll % count;
        }
    }
}
