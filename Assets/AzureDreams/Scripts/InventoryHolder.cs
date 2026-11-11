using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Holds an Inventory on a GameObject (e.g., the player).
    /// </summary>
    public sealed class InventoryHolder : MonoBehaviour
    {
        public Inventory Inventory { get; } = new Inventory();
    }
}
