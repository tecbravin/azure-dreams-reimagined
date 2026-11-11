using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Simple stackable inventory for consumables and key items.
    /// The tower run carries its own copy; town keeps a persistent bank.
    /// </summary>
    public sealed class Inventory
    {
        private readonly Dictionary<ItemDefinition, int> _items = new Dictionary<ItemDefinition, int>();

        private readonly List<AzureDreams.Core.DungeonItemEntry> _dungeonItems = new List<AzureDreams.Core.DungeonItemEntry>();

        // Raw four-byte records are carried, not priced or made into guessed definitions.
        // Return defensive copies: callers cannot mutate inventory ownership indirectly.
        public IReadOnlyList<AzureDreams.Core.DungeonItemEntry> DungeonItems
        {
            get { return _dungeonItems.ConvertAll(CopyDungeonItem).AsReadOnly(); }
        }
        private static AzureDreams.Core.DungeonItemEntry CopyDungeonItem(AzureDreams.Core.DungeonItemEntry item)
            => new AzureDreams.Core.DungeonItemEntry(item.Subtype, item.Kind, item.Param, item.Flags);

        public void AddDungeonItem(AzureDreams.Core.DungeonItemEntry item)
        {
            if (item != null) _dungeonItems.Add(CopyDungeonItem(item));
        }

        internal string SaveDungeonItems()
        {
            var bytes = new byte[_dungeonItems.Count * 4];
            for (int i = 0; i < _dungeonItems.Count; i++)
            {
                var item = _dungeonItems[i];
                bytes[i * 4] = item.Subtype;
                bytes[i * 4 + 1] = item.Kind;
                bytes[i * 4 + 2] = unchecked((byte)item.Param);
                bytes[i * 4 + 3] = item.Flags;
            }
            return System.Convert.ToBase64String(bytes);
        }

        internal void RestoreDungeonItems(string saved)
        {
            if (string.IsNullOrEmpty(saved)) return;
            byte[] bytes;
            try { bytes = System.Convert.FromBase64String(saved); }
            catch (System.FormatException) { return; }
            if (bytes.Length % 4 != 0) return;
            _dungeonItems.Clear();
            for (int i = 0; i < bytes.Length; i += 4)
                _dungeonItems.Add(new AzureDreams.Core.DungeonItemEntry(
                    bytes[i], bytes[i + 1], unchecked((sbyte)bytes[i + 2]), bytes[i + 3]));
        }

        internal bool ReplaceDungeonItem(int index, byte kind, AzureDreams.Core.DungeonItemEntry replacement)
        {
            if (index < 0 || index >= _dungeonItems.Count || _dungeonItems[index].Kind != kind) return false;
            _dungeonItems[index] = CopyDungeonItem(replacement);
            return true;
        }

        internal void IdentifyEggs()
        {
            for (int i = 0; i < _dungeonItems.Count; i++)
                if (_dungeonItems[i].Kind == MonsterEgg.Kind && !MonsterEgg.IdentifiedInTower(_dungeonItems[i]))
                    _dungeonItems[i] = MonsterEgg.Identify(_dungeonItems[i]);
        }

        internal bool ConsumeDungeonItem(int index, byte kind)
        {
            if (index < 0 || index >= _dungeonItems.Count || _dungeonItems[index].Kind != kind) return false;
            _dungeonItems.RemoveAt(index);
            return true;
        }

        internal const byte WornMask = 0xE0;
        internal const byte CursedEquipmentFlag = 0x04;
        internal const byte UnrevealedEquipmentFlag = 0x08;
        internal static bool IsGear(AzureDreams.Core.DungeonItemEntry item) =>
            item.Kind is AzureDreams.Core.ItemSystem.Kind.Sword or AzureDreams.Core.ItemSystem.Kind.Wand or AzureDreams.Core.ItemSystem.Kind.Shield;

        internal static bool IsCursedEquipment(AzureDreams.Core.DungeonItemEntry item) =>
            item != null && IsGear(item) && (item.Flags & CursedEquipmentFlag) != 0;

        internal static bool IsUnrevealedEquipment(AzureDreams.Core.DungeonItemEntry item) =>
            item != null && IsGear(item) && (item.Flags & UnrevealedEquipmentFlag) != 0;

        internal static bool IsLockedEquipment(AzureDreams.Core.DungeonItemEntry item) =>
            IsCursedEquipment(item) && (item.Flags & WornMask) != 0;

        internal bool CanSetEquipped(int index, byte wearer, bool lockCursed)
        {
            if (index < 0 || index >= _dungeonItems.Count || !IsGear(_dungeonItems[index])) return false;
            if (!lockCursed) return true;
            var item = _dungeonItems[index];
            if (IsLockedEquipment(item) && (item.Flags & WornMask) != wearer) return false;
            bool shield = item.Kind == AzureDreams.Core.ItemSystem.Kind.Shield;
            var replaced = wearer == 0 ? null : EquippedItem(wearer, shield);
            return replaced == item || !IsCursedEquipment(replaced);
        }

        internal bool SetEquipped(int index, byte wearer, bool lockCursed)
        {
            if (!CanSetEquipped(index, wearer, lockCursed)) return false;
            bool shield = _dungeonItems[index].Kind == AzureDreams.Core.ItemSystem.Kind.Shield;
            for (int i = 0; i < _dungeonItems.Count; i++)
            {
                var item = _dungeonItems[i];
                if (!IsGear(item)) continue;
                if (i == index)
                {
                    item.Flags = (byte)((item.Flags & ~WornMask) | wearer);
                    if (wearer != 0) item.Flags = (byte)(item.Flags & ~UnrevealedEquipmentFlag);
                }
                else if ((item.Kind == AzureDreams.Core.ItemSystem.Kind.Shield) == shield)
                    item.Flags = (byte)(item.Flags & ~wearer);
            }
            return true;
        }

        internal void ClearEquipped(byte wearer)
        {
            foreach (var item in _dungeonItems)
                if (IsGear(item)) item.Flags = (byte)(item.Flags & ~wearer);
        }

        internal bool HasEquipped(byte wearer, bool shield)
        {
            foreach (var item in _dungeonItems)
                if (IsGear(item) && (item.Kind == AzureDreams.Core.ItemSystem.Kind.Shield) == shield && (item.Flags & wearer) != 0)
                    return true;
            return false;
        }

        internal AzureDreams.Core.DungeonItemEntry EquippedItem(byte wearer, bool shield)
        {
            foreach (var item in _dungeonItems)
                if (IsGear(item) && (item.Kind == AzureDreams.Core.ItemSystem.Kind.Shield) == shield && (item.Flags & wearer) != 0)
                    return item;
            return null;
        }

        internal int EquippedBonus(byte wearer, bool shield)
        {
            var item = EquippedItem(wearer, shield);
            return item == null ? 0 : EquipmentStat(item, InventoryText.Find(item));
        }

        internal static int EquipmentStat(AzureDreams.Core.DungeonItemEntry item, InventoryText.Entry text) =>
            Mathf.Clamp((text?.gear ?? 1) + AzureDreams.Core.ItemSystem.GetEquipmentModifier(item), 0, 99);

        internal void RemoveEquipmentCurse()
        {
            foreach (var item in _dungeonItems)
                if (IsCursedEquipment(item)) item.Flags = (byte)(item.Flags & ~CursedEquipmentFlag);
        }

        public const int SlotCapacity = 20;
        public bool HasSpace(int count = 1) => count > 0 && TotalItems <= SlotCapacity - count;

        public int Count => _items.Count + _dungeonItems.Count;
        public int TotalItems
        {
            get
            {
                int total = _dungeonItems.Count;
                foreach (var kvp in _items) total += kvp.Value;
                return total;
            }
        }

        public void AddItem(ItemDefinition item, int amount = 1)
        {
            if (item == null || amount <= 0) return;
            if (_items.TryGetValue(item, out int current))
                _items[item] = current + amount;
            else
                _items[item] = amount;
        }

        public bool RemoveItem(ItemDefinition item, int amount = 1)
        {
            if (item == null || amount <= 0) return false;
            if (!_items.TryGetValue(item, out int current) || current < amount)
                return false;
            current -= amount;
            if (current <= 0) _items.Remove(item);
            else _items[item] = current;
            return true;
        }

        public int GetCount(ItemDefinition item)
        {
            return item != null && _items.TryGetValue(item, out int c) ? c : 0;
        }

        public bool HasItem(ItemDefinition item, int amount = 1)
        {
            return GetCount(item) >= amount;
        }

        /// <summary>
        /// Deep copy used to hand a carried set into a tower run.
        /// </summary>
        public Inventory Clone()
        {
            var copy = new Inventory();
            foreach (var kvp in _items) copy._items[kvp.Key] = kvp.Value;
            foreach (var item in _dungeonItems) copy.AddDungeonItem(item);
            return copy;
        }

        /// <summary>Move ownership of all stacks; self-transfer is a no-op.</summary>
        public void TransferTo(Inventory destination)
        {
            if (destination == null || ReferenceEquals(this, destination)) return;
            destination.Merge(this);
            _items.Clear();
            _dungeonItems.Clear();
        }

        // Move excess entries without copying or reordering the retained records.
        internal int StoreOverflow(Inventory storage, int capacity, int storageCapacity)
        {
            int excess = Mathf.Min(Mathf.Max(0, TotalItems - capacity),
                Mathf.Max(0, storageCapacity - storage.TotalItems));
            if (excess == 0) return 0;
            int remaining = excess;
            int rawCount = Mathf.Min(remaining, _dungeonItems.Count);
            int first = _dungeonItems.Count - rawCount;
            storage._dungeonItems.AddRange(_dungeonItems.GetRange(first, rawCount));
            _dungeonItems.RemoveRange(first, rawCount);
            remaining -= rawCount;
            if (remaining > 0)
            {
                var definitions = new List<ItemDefinition>(_items.Keys);
                foreach (var definition in definitions)
                {
                    int count = Mathf.Min(remaining, _items[definition]);
                    RemoveItem(definition, count);
                    storage.AddItem(definition, count);
                    remaining -= count;
                    if (remaining == 0) break;
                }
            }
            return excess;
        }

        internal bool RetrieveOne(Inventory destination)
        {
            if (_dungeonItems.Count > 0)
            {
                destination._dungeonItems.Add(_dungeonItems[0]);
                _dungeonItems.RemoveAt(0);
                return true;
            }
            ItemDefinition definition = null;
            foreach (var item in _items.Keys) { definition = item; break; }
            if (!definition || !RemoveItem(definition)) return false;
            destination.AddItem(definition);
            return true;
        }

        /// <summary>
        /// Merge a returned run's leftover items back into the town bank.
        /// </summary>
        public void Merge(Inventory source)
        {
            if (source == null || ReferenceEquals(source, this)) return;
            foreach (var kvp in source._items)
                AddItem(kvp.Key, kvp.Value);
            foreach (var item in source._dungeonItems) AddDungeonItem(item);
        }
    }
}
