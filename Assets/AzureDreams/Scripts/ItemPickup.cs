using UnityEngine;
using AzureDreamsReimagined;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// A pickup the player can collect: potions, gold, key items.
    /// Implements Interactable so the player can grab it with E.
    /// </summary>
    public sealed class ItemPickup : MonoBehaviour, Interactable
    {
        public readonly struct CollectedEvent
        {
            public readonly ItemDefinition Item;
            public readonly int Amount;
            public readonly GameObject Source;
            public CollectedEvent(ItemDefinition item, int amount, GameObject source)
            { Item = item; Amount = amount; Source = source; }
        }

        [Header("Item")]
        public ItemDefinition item;
        [Min(1)] public int amount = 1;
        public bool destroyOnPickup = true;
        [Tooltip("Optional TownSaveVariables key used to keep a one-shot pickup collected across Town rebuilds.")]
        public string persistenceKey;

        [Header("Visual")]
        public float bobSpeed = 2f;
        public float bobHeight = 0.15f;
        private Vector3 _basePos;
        private bool _collected;

        private void Start() => _basePos = transform.position;

        private void Update()
        {
            // Gentle bobbing animation
            float y = _basePos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(_basePos.x, y, _basePos.z);
        }

        public void Interact(PlayerController player)
        {
            if (!player || _collected || item == null) return;
            // Find or create inventory on the player
            var inv = player.GetComponent<InventoryHolder>();
            if (inv == null) inv = player.gameObject.AddComponent<InventoryHolder>();
            TryCollect(inv.Inventory);
        }

        public bool TryCollect(Inventory inventory)
        {
            if (_collected || !isActiveAndEnabled || inventory == null || item == null || amount <= 0 || !inventory.HasSpace(amount))
                return false;
            // Claim before scheduling destruction: another E/callback in this
            // frame must not add the same item a second time.
            _collected = true;
            if (!string.IsNullOrWhiteSpace(persistenceKey))
            {
                TownSaveVariables.SetBool("pickup_" + persistenceKey, true);
                TownSaveVariables.Save();
            }
            inventory.AddItem(item, amount);
            TownEventBus.Publish(TownEventBus.EventId.ItemCollected, new CollectedEvent(item, amount, gameObject));
            Debug.Log($"Picked up {amount}x {item.DisplayName}");
            if (destroyOnPickup) Destroy(gameObject);
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
