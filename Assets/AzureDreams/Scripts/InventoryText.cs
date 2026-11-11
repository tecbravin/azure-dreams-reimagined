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
            public string en, ptBr, descriptionEn, descriptionPtBr;
        }
        private static Dictionary<int, Entry> _entries;
        internal static IEnumerable<Entry> Entries => GetEntries().Values;

        internal static Entry Find(DungeonItemEntry item) =>
            GetEntries().TryGetValue(item.Kind * 256 + item.Subtype, out var text) ? text : null;

        private static Dictionary<int, Entry> GetEntries()
        {
            if (_entries == null)
            {
                _entries = new Dictionary<int, Entry>();
                var asset = Resources.Load<TextAsset>("Town/Retail/inventory_text");
                var catalog = asset ? JsonUtility.FromJson<Catalog>(asset.text) : null;
                if (catalog?.entries != null)
                    foreach (var entry in catalog.entries) _entries[entry.kind * 256 + entry.id] = entry;
            }
            return _entries;
        }
    }
}
