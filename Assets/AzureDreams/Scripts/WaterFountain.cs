using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>Repeatable full recovery adapter. Exact PS1 fountain rules remain unverified.</summary>
    public sealed class WaterFountain : TownObject
    {
        protected override bool Apply(GameObject actor)
        {
            if (!actor.TryGetComponent<CharacterStats>(out var stats) || !stats.isActive) return false;
            stats.currentHealth = Mathf.Max(0, stats.maxHealth);
            stats.currentMana = Mathf.Max(0, stats.maxMana);
            return true;
        }

        protected override void AfterInteract(InteractionEvent payload)
        {
            TownEventBus.Publish(TownEventBus.EventId.FountainUsed, payload);
        }
    }
}
