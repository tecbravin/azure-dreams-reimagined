using System;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Lightweight save/load for browser (WebGL) builds using PlayerPrefs.
    /// Persists town progress: gold, town level, recruited familiar, and carried potions.
    /// </summary>
    public static class SaveSystem
    {
        private const string KeyGold = "ad_gold";
        private const string KeyTownLevel = "ad_townLevel";
        private const string KeyFamiliar = "ad_familiar";
        private const string KeyPotions = "ad_potions";

        public const int SlotCount = 4;

        [Serializable]
        internal struct FamiliarOrder
        {
            public int ai;
            public Vector2Int offset;
        }

        [Serializable]
        internal sealed class Checkpoint
        {
            public int gold, townLevel, potions, storedPotions, floor, rng, loot, turns;
            public int familiarSpecies, parkedSpecies;
            public string familiar, parkedFamiliar, carried, stored;
            public bool familiarDefeated, generated, starterJoined;
            public bool familiarInBag;
            public int blueCollarRosterSlot;
            public FamiliarOrder primaryFamiliarOrder;
            public FamiliarOrder[] reserveFamiliarOrders;
            public string towerHatchedNickname;
            public byte[] actor, familiarActor, parkedActor;
            public TownSaveVariables.SavedValue[] town;

            internal string ProgressText(string key)
            {
                if (town == null) return null;
                foreach (var value in town)
                    if (value?.key == key && value.kind == 3) return value.text;
                return null;
            }

            internal double? ProgressValue(string key)
            {
                if (town == null) return null;
                foreach (var value in town)
                {
                    if (value?.key != key) continue;
                    if (value.kind == 1) return value.integer;
                    if (value.kind == 2) return value.number;
                }
                return null;
            }
        }

        internal static Checkpoint ReadSlot(int slot)
        {
            if (slot < 0 || slot >= SlotCount) return null;
            string json = PlayerPrefs.GetString("ad_checkpoint_" + slot, string.Empty);
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var data = JsonUtility.FromJson<Checkpoint>(json);
                return data != null && data.townLevel >= 1 && data.floor is >= 0 and <= 40 ? data : null;
            }
            catch (ArgumentException) { return null; }
        }

        internal static void WriteSlot(int slot, Checkpoint data)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            PlayerPrefs.SetString("ad_checkpoint_" + slot, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static bool HasSave => PlayerPrefs.HasKey(KeyGold);

        public static void Save(int gold, int townLevel, string familiarName, int potions)
        {
            PlayerPrefs.SetInt(KeyGold, gold);
            PlayerPrefs.SetInt(KeyTownLevel, townLevel);
            PlayerPrefs.SetString(KeyFamiliar, familiarName ?? string.Empty);
            PlayerPrefs.SetInt(KeyPotions, potions);
            PlayerPrefs.Save();
        }

        public static void Load(out int gold, out int townLevel, out string familiarName, out int potions)
        {
            gold = PlayerPrefs.GetInt(KeyGold, 0);
            townLevel = PlayerPrefs.GetInt(KeyTownLevel, 1);
            familiarName = PlayerPrefs.GetString(KeyFamiliar, string.Empty);
            potions = PlayerPrefs.GetInt(KeyPotions, 0);
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(KeyGold);
            PlayerPrefs.DeleteKey(KeyTownLevel);
            PlayerPrefs.DeleteKey(KeyFamiliar);
            PlayerPrefs.DeleteKey(KeyPotions);
            PlayerPrefs.Save();
        }
    }
}
