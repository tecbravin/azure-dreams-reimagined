using System;
using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Lightweight publish/subscribe event bus for town systems.
    /// Decouples triggers (NPCs, objects, fountain) from handlers
    /// (state machine, save variables, UI, audio).
    /// </summary>
    public static class TownEventBus
    {
        public enum EventId
        {
            None,
            PlayerEnteredTown,
            PlayerLeftTown,
            PlayerEnteredTower,
            PlayerReturnedFromTower,
            NpcTalked,
            ObjectInteracted,
            ItemCollected,
            GoldChanged,
            TownLevelChanged,
            FamiliarRecruited,
            FamiliarLost,
            FountainUsed,
            ShopPurchased,
            QuestCompleted,
            SaveRequested,
            LoadRequested
        }

        private static readonly Dictionary<EventId, List<Action<object>>> _handlers =
            new Dictionary<EventId, List<Action<object>>>();

        public static void Subscribe(EventId id, Action<object> handler)
        {
            if (id == EventId.None || handler == null) return;
            if (!_handlers.TryGetValue(id, out var list))
            {
                list = new List<Action<object>>();
                _handlers[id] = list;
            }
            if (!list.Contains(handler)) list.Add(handler);
        }

        public static void Unsubscribe(EventId id, Action<object> handler)
        {
            if (handler == null || !_handlers.TryGetValue(id, out var list)) return;
            list.Remove(handler);
            if (list.Count == 0) _handlers.Remove(id);
        }

        public static void Publish(EventId id, object payload = null)
        {
            if (id == EventId.None || !_handlers.TryGetValue(id, out var list)) return;
            // Snapshot to allow subscribe/unsubscribe during dispatch.
            var snapshot = list.ToArray();
            foreach (var h in snapshot)
            {
                try { h?.Invoke(payload); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        public static void Clear() => _handlers.Clear();
    }
}
