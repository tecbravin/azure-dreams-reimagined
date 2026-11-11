using System;
using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Extended town save variables (beyond SaveSystem's core progress).
    /// Persisted via PlayerPrefs with a stable prefix so browser (WebGL)
    /// builds retain town state across sessions.
    /// </summary>
    public static class TownSaveVariables
    {
        private const string Prefix = "ad_town_";
        private const string KnownKeys = Prefix + "__known_keys";

        public static int Gold
        {
            get => GetInt("gold");
            set => SetInt("gold", Mathf.Max(0, value));
        }

        public static int TownLevel
        {
            get => Mathf.Max(1, GetInt("level", 1));
            set => SetInt("level", Mathf.Max(1, value));
        }

        public static int GetItemCount(string itemId) => string.IsNullOrWhiteSpace(itemId)
            ? 0 : Mathf.Max(0, GetInt("item_" + itemId));

        public static void SetItemCount(string itemId, int count)
        {
            if (!string.IsNullOrWhiteSpace(itemId)) SetInt("item_" + itemId, Mathf.Max(0, count));
        }

        public static void SetInt(string key, int value) { Track(key); PlayerPrefs.SetInt(Prefix + key, value); }
        public static int GetInt(string key, int defaultValue = 0) => PlayerPrefs.GetInt(Prefix + key, defaultValue);

        public static void SetFloat(string key, float value) { Track(key); PlayerPrefs.SetFloat(Prefix + key, value); }
        public static float GetFloat(string key, float defaultValue = 0f) => PlayerPrefs.GetFloat(Prefix + key, defaultValue);

        public static void SetString(string key, string value) { Track(key); PlayerPrefs.SetString(Prefix + key, value ?? string.Empty); }
        public static string GetString(string key, string defaultValue = "") => PlayerPrefs.GetString(Prefix + key, defaultValue);

        public static void SetBool(string key, bool value) => SetInt(key, value ? 1 : 0);
        public static bool GetBool(string key, bool defaultValue = false) => PlayerPrefs.GetInt(Prefix + key, defaultValue ? 1 : 0) == 1;

        public static bool HasValue(string key) =>
            !string.IsNullOrWhiteSpace(key) && PlayerPrefs.HasKey(Prefix + key);

        public static void Save() => PlayerPrefs.Save();
        public static void Clear()
        {
            string[] tracked = PlayerPrefs.GetString(KnownKeys, string.Empty).Split('|');
            foreach (string key in tracked)
                if (!string.IsNullOrEmpty(key)) PlayerPrefs.DeleteKey(Prefix + key);
            foreach (var key in new[] { "gold", "level", "item_Healing Draught" }) PlayerPrefs.DeleteKey(Prefix + key);
            PlayerPrefs.DeleteKey(KnownKeys);
            PlayerPrefs.Save();
        }

        [Serializable]
        internal sealed class SavedValue
        {
            public string key, text;
            public int kind, integer;
            public float number;
        }

        internal static SavedValue[] Capture()
        {
            var values = new List<SavedValue>();
            foreach (string key in PlayerPrefs.GetString(KnownKeys, string.Empty).Split('|'))
            {
                if (!HasValue(key)) continue;
                string pref = Prefix + key;
                int integer = PlayerPrefs.GetInt(pref, int.MinValue);
                float number = PlayerPrefs.GetFloat(pref, float.NaN);
                if (integer != int.MinValue) values.Add(new SavedValue { key = key, kind = 1, integer = integer });
                else if (!float.IsNaN(number)) values.Add(new SavedValue { key = key, kind = 2, number = number });
                else values.Add(new SavedValue { key = key, text = PlayerPrefs.GetString(pref) });
            }
            return values.ToArray();
        }

        internal static void Restore(SavedValue[] values)
        {
            Clear();
            if (values == null) return;
            foreach (var value in values)
            {
                if (value == null || string.IsNullOrEmpty(value.key)) continue;
                if (value.kind == 1) SetInt(value.key, value.integer);
                else if (value.kind == 2) SetFloat(value.key, value.number);
                else SetString(value.key, value.text);
            }
        }

        private static void Track(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Contains("|")) return;
            string current = PlayerPrefs.GetString(KnownKeys, string.Empty);
            foreach (string existing in current.Split('|'))
                if (existing == key) return;
            PlayerPrefs.SetString(KnownKeys, string.IsNullOrEmpty(current) ? key : current + "|" + key);
        }
    }
}
