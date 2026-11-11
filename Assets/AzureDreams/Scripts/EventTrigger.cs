using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    [RequireComponent(typeof(BoxCollider))]
    [DisallowMultipleComponent]
    public sealed class EventTrigger : MonoBehaviour
    {
        public string triggerId;
        public TownEventBus.EventId eventId = TownEventBus.EventId.ObjectInteracted;
        public string customEventId;
        public bool once = true;
        public bool persistActivation = true;
        [Tooltip("Requires an explicit interaction call instead of activating on collider entry.")]
        public bool requireInteraction;
        private bool _activated;
        private readonly Dictionary<Collider, GameObject> _inside = new Dictionary<Collider, GameObject>();

        public readonly struct TriggerEvent
        {
            public readonly string TriggerId;
            public readonly string CustomEventId;
            public readonly GameObject Actor;
            public readonly EventTrigger Source;
            public TriggerEvent(EventTrigger source, GameObject actor)
            { Source = source; TriggerId = source.triggerId; CustomEventId = source.customEventId; Actor = actor; }
        }

        private void Awake() => GetComponent<BoxCollider>().isTrigger = true;
        private void OnDisable() => _inside.Clear();

        public bool TryActivate(GameObject actor)
        {
            var player = ResolvePlayer(actor ? actor.transform : null);
            if (!player || !isActiveAndEnabled || (eventId == TownEventBus.EventId.None && string.IsNullOrEmpty(customEventId)))
                return false;
            if (once && (_activated || (persistActivation && !string.IsNullOrEmpty(triggerId) &&
                TownSaveVariables.GetBool("trigger_" + triggerId)))) return false;
            _activated = true;
            if (persistActivation && !string.IsNullOrEmpty(triggerId))
            {
                TownSaveVariables.SetBool("trigger_" + triggerId, true);
                TownSaveVariables.Save();
            }
            var id = eventId == TownEventBus.EventId.None ? TownEventBus.EventId.ObjectInteracted : eventId;
            TownEventBus.Publish(id, new TriggerEvent(this, player));
            return true;
        }

        private static GameObject ResolvePlayer(Transform node)
        {
            while (node)
            {
                if (node.CompareTag("Player") || node.GetComponent<PlayerController>()) return node.gameObject;
                node = node.parent;
            }
            return null;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (requireInteraction) return;
            if (!other || _inside.ContainsKey(other)) return;
            var player = ResolvePlayer(other.transform);
            if (!player) return;
            bool alreadyInside = _inside.ContainsValue(player);
            _inside.Add(other, player);
            if (!alreadyInside) TryActivate(player);
        }

        private void OnTriggerExit(Collider other) { if (other) _inside.Remove(other); }
    }
}
