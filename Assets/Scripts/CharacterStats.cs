using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Base stats and configuration for a game character.
    /// Attach to Player, Monster, or NPC root objects.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterStats : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("Unique identifier for this character instance.")]
        public string characterId;

        [Tooltip("Display name shown in UI/dialogue.")]
        public string displayName = "Unnamed";

        [Header("Movement")]
        [Tooltip("Movement speed in units per second.")]
        public float moveSpeed = 3.5f;

        [Tooltip("Rotation speed in degrees per second.")]
        public float rotationSpeed = 360f;

        [Header("Combat")]
        [Tooltip("Current health points.")]
        public int currentHealth = 100;

        [Tooltip("Maximum health points.")]
        public int maxHealth = 100;

        [Tooltip("Attack power.")]
        public int attackPower = 10;

        [Tooltip("Defense rating.")]
        public int defense = 5;

        [Header("Magic (town recovery adapter)")]
        public int currentMana;
        public int maxMana;

        [Header("Visuals")]
        [Tooltip("Primary sprite/texture for this character.")]
        public Texture2D portraitTexture;

        [Tooltip("Material used for rendering.")]
        public Material characterMaterial;

        [Header("Behavior")]
        [Tooltip("Is this character currently active/interactable?")]
        public bool isActive = true;

        [Tooltip("Layer mask for detecting other characters.")]
        public LayerMask interactionLayer = Physics.DefaultRaycastLayers;

        /// <summary>
        /// Apply damage to this character, clamping at zero.
        /// </summary>
        public virtual void TakeDamage(int amount)
        {
            if (!isActive) return;
            int damage = Mathf.Max(amount - defense, 1);
            currentHealth = Mathf.Max(currentHealth - damage, 0);
            if (currentHealth == 0)
            {
                OnDeath();
            }
        }

        /// <summary>
        /// Heal this character, clamping at maxHealth.
        /// </summary>
        public virtual void Heal(int amount)
        {
            currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        }

        /// <summary>
        /// Called when health reaches zero. Override for death behavior.
        /// </summary>
        protected virtual void OnDeath()
        {
            isActive = false;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Reset stats to initial values.
        /// </summary>
        public virtual void ResetStats()
        {
            currentHealth = maxHealth;
            isActive = true;
            gameObject.SetActive(true);
        }
    }
}
