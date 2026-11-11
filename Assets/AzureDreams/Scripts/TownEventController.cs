using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Orchestrates town events into state-machine transitions and
    /// persisted variable updates. Single entry point for high-level
    /// town flow (enter town, enter tower, return, save).
    /// </summary>
    public sealed partial class TownEventController : MonoBehaviour
    {
        [SerializeField] private TowerGameController _gameController;

        private TownGameState _state = new TownGameState();

        public TownGameState State => _state;

        private void OnEnable()
        {
            TownEventBus.Subscribe(TownEventBus.EventId.PlayerEnteredTown, OnPlayerEnteredTown);
            TownEventBus.Subscribe(TownEventBus.EventId.PlayerEnteredTower, OnPlayerEnteredTower);
            TownEventBus.Subscribe(TownEventBus.EventId.PlayerReturnedFromTower, OnPlayerReturned);
            TownEventBus.Subscribe(TownEventBus.EventId.SaveRequested, OnSaveRequested);
            TownEventBus.Subscribe(TownEventBus.EventId.NpcTalked, OnNpcTalked);
            TownEventBus.Subscribe(TownEventBus.EventId.ObjectInteracted, OnObjectInteracted);
            TownEventBus.Subscribe(TownEventBus.EventId.FountainUsed, OnFountainUsed);
            TownEventBus.Subscribe(TownEventBus.EventId.ShopPurchased, OnShopPurchased);
            TownEventBus.Subscribe(TownEventBus.EventId.ItemCollected, OnItemCollected);
            TownEventBus.Subscribe(TownEventBus.EventId.GoldChanged, OnGoldChanged);
            TownEventBus.Subscribe(TownEventBus.EventId.TownLevelChanged, OnTownLevelChanged);
        }

        private void OnDisable()
        {
            TownEventBus.Unsubscribe(TownEventBus.EventId.PlayerEnteredTown, OnPlayerEnteredTown);
            TownEventBus.Unsubscribe(TownEventBus.EventId.PlayerEnteredTower, OnPlayerEnteredTower);
            TownEventBus.Unsubscribe(TownEventBus.EventId.PlayerReturnedFromTower, OnPlayerReturned);
            TownEventBus.Unsubscribe(TownEventBus.EventId.SaveRequested, OnSaveRequested);
            TownEventBus.Unsubscribe(TownEventBus.EventId.NpcTalked, OnNpcTalked);
            TownEventBus.Unsubscribe(TownEventBus.EventId.ObjectInteracted, OnObjectInteracted);
            TownEventBus.Unsubscribe(TownEventBus.EventId.FountainUsed, OnFountainUsed);
            TownEventBus.Unsubscribe(TownEventBus.EventId.ShopPurchased, OnShopPurchased);
            TownEventBus.Unsubscribe(TownEventBus.EventId.ItemCollected, OnItemCollected);
            TownEventBus.Unsubscribe(TownEventBus.EventId.GoldChanged, OnGoldChanged);
            TownEventBus.Unsubscribe(TownEventBus.EventId.TownLevelChanged, OnTownLevelChanged);
        }

        private void OnPlayerEnteredTown(object payload)
        {
            _state.TrySet(TownGameState.State.TownIdle);
            TownSaveVariables.Save();
        }

        private void OnPlayerEnteredTower(object payload)
        {
            _state.TrySet(TownGameState.State.TransitionToTower);
            _state.TrySet(TownGameState.State.TowerRunning);
        }

        private void OnPlayerReturned(object payload)
        {
            _state.TrySet(TownGameState.State.ReturnToTown);
            _state.TrySet(TownGameState.State.TownIdle);
            TownSaveVariables.Save();
        }

        private void OnNpcTalked(object payload)
        {
            // Only reopen dialogue for spoken lines (Completed == false).
            // Final events (Completed == true) close the conversation and
            // must not reopen TownDialogue.
            if (!(payload is NPCDialogue.TalkEvent e) || e.Completed || !e.Source ||
                !e.Source.transform.IsChildOf(transform)) return;
            _state.TrySet(TownGameState.State.TownDialogue);
        }

        private void OnObjectInteracted(object payload)
        {
            if (!(payload is TownObject.InteractionEvent e) || !e.Source ||
                !e.Source.transform.IsChildOf(transform)) return;
            _state.TrySet(TownGameState.State.TownDialogue);
        }

        private void OnFountainUsed(object payload)
        {
            TownSaveVariables.Save();
        }

        private void OnShopPurchased(object payload)
        {
            if (payload is ItemDefinition item && item)
            {
                string key = "shop_purchase_" + item.SaveId;
                TownSaveVariables.SetInt(key, TownSaveVariables.GetInt(key) + 1);
            }
            TownSaveVariables.Save();
        }

        private void OnItemCollected(object payload)
        {
            if (!(payload is ItemPickup.CollectedEvent collected) || !collected.Source ||
                !collected.Source.transform.IsChildOf(transform) || !collected.Item) return;
            string key = "item_" + collected.Item.SaveId;
            TownSaveVariables.SetInt(key, TownSaveVariables.GetInt(key) + collected.Amount);
            TownSaveVariables.Save();
        }

        private void OnGoldChanged(object payload)
        {
            if (payload is int gold) TownSaveVariables.Gold = gold;
            TownSaveVariables.Save();
        }

        private void OnTownLevelChanged(object payload)
        {
            if (payload is int level) TownSaveVariables.TownLevel = level;
            TownSaveVariables.Save();
        }

        private void OnSaveRequested(object payload)
        {
            TownSaveVariables.Save();
        }
    }
}
