using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Base interface for anything the player can interact with (NPCs, items, etc.).
    /// </summary>
    public interface Interactable
    {
        void Interact(PlayerController player);
    }
}
