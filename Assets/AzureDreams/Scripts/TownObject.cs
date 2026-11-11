using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>Unity presentation adapter; IDs/content are authored, not decoded PS1 event opcodes.</summary>
    [DisallowMultipleComponent]
    public class TownObject : MonoBehaviour, Interactable
    {
        public string objectId;
        [TextArea] public string message;
        public bool repeatable = true;
        public bool persistInteraction = true;
        [Min(0f)] public float interactionRadius = 1.5f;
        private bool _interacting;
        private bool _used;

        public readonly struct InteractionEvent
        {
            public readonly string ObjectId;
            public readonly string Message;
            public readonly GameObject Actor;
            public readonly TownObject Source;
            public InteractionEvent(TownObject source, GameObject actor)
            { Source = source; ObjectId = source.objectId; Message = source.message; Actor = actor; }
        }

        public void Interact(PlayerController player) { if (player) OnInteract(player.gameObject); }

        public virtual bool OnInteract(GameObject actor)
        {
            if (!actor || !isActiveAndEnabled || _interacting) return false;
            Vector3 delta = actor.transform.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > interactionRadius * interactionRadius) return false;
            var flow = GetComponentInParent<TownEventController>();
            if (flow && flow.State.Current != TownGameState.State.TownIdle) return false;
            if (!repeatable && (_used || (persistInteraction && !string.IsNullOrEmpty(objectId) &&
                TownSaveVariables.GetBool("object_" + objectId)))) return false;
            _interacting = true;
            try
            {
                if (!Apply(actor)) return false;
                _used = true;
                if (persistInteraction && !string.IsNullOrEmpty(objectId))
                {
                    TownSaveVariables.SetBool("object_" + objectId, true);
                    TownSaveVariables.Save();
                }
                var payload = new InteractionEvent(this, actor);
                TownEventBus.Publish(TownEventBus.EventId.ObjectInteracted, payload);
                AfterInteract(payload);
                return true;
            }
            finally { _interacting = false; }
        }

        protected virtual bool Apply(GameObject actor) => true;
        protected virtual void AfterInteract(InteractionEvent payload) { }
    }
}
