using System;
using System.Collections.Generic;
using AzureDreams.Core;
using UnityEngine;

namespace AzureDreamsReimagined
{
    internal static class InventoryText
    {
        [Serializable] private sealed class Catalog { public Entry[] entries; }
        [Serializable] internal sealed class Entry
        {
            public int kind, id, gear;
            public string en, ptBr, descriptionEn, descriptionPtBr, icon;
        }
        private static Dictionary<int, Entry> _entries;
        internal static IEnumerable<Entry> Entries => GetEntries().Values;

        // Domain reload can be disabled in the editor.  Always begin a play
        // session with a fresh catalog, rather than retaining a cache made
        // before Resources finished importing the JSON asset.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => _entries = null;

        internal static Entry Find(DungeonItemEntry item) =>
            GetEntries().TryGetValue(item.Kind * 256 + item.Subtype, out var text) ? text : null;

        internal static string IconName(int kind, int subtype) =>
            GetEntries().TryGetValue(kind * 256 + subtype, out var text) ? text.icon : null;

        private static Dictionary<int, Entry> GetEntries()
        {
            // A failed early Resources lookup used to cache an empty list for
            // the entire session, leaving the debug item picker blank. Retry
            // an empty catalog so an import/initialization race can recover.
            if (_entries == null || _entries.Count == 0)
            {
                var entries = new Dictionary<int, Entry>();
                var asset = AssetMode.Load<TextAsset>("Town/Retail/inventory_text");
                var catalog = asset ? JsonUtility.FromJson<Catalog>(asset.text) : null;
                if (catalog?.entries != null)
                    foreach (var entry in catalog.entries) entries[entry.kind * 256 + entry.id] = entry;
                _entries = entries;
            }
            return _entries;
        }
    }
}
