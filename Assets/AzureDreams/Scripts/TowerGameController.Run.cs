namespace AzureDreamsReimagined
{
    /// <summary>
    /// Controller bridge for Architect-owned run lifecycle. Generation/spawn
    /// managers remain separate; no mid-run persistence is implied here.
    /// </summary>
    public sealed partial class TowerGameController
    {
        private TowerRunManager _towerRun;

        private bool FinishManagedTowerRun(bool survived)
        {
            if (_area != Area.Tower || _towerRun == null ||
                (_towerRun.CurrentState != TowerRunManager.State.InTower &&
                 _towerRun.CurrentState != TowerRunManager.State.Defeat &&
                 _towerRun.CurrentState != TowerRunManager.State.Victory)) return false;
            if (_carriedItem != null)
            {
                if (!CreditCoinPickup(_carriedItem))
                {
                    if (_runInventory != null && _runInventory.HasSpace()) _runInventory.AddDungeonItem(_carriedItem);
                    else if (_townStorage.TotalItems < HomeStorageCapacity) _townStorage.AddDungeonItem(_carriedItem);
                    else _townInventory.AddDungeonItem(_carriedItem);
                }
                _carriedItem = null;
            }
            _runInventory?.ClearEquipped(BlueFamiliarEquipped);
            RestoreConditions(_playerActor);
            RestoreConditions(_familiarActor);
            foreach (var reserve in _bagFamiliars) RestoreConditions(reserve.Actor);
            PrepareTownFamiliarRoster();
            _towerRun.SetHealth(_health);
            _towerRun.FamiliarName = _familiarDefinition ? _familiarDefinition.DisplayName : string.Empty;
            // Count remaining stacks immediately before the manager transfers
            // ownership and saves; do not persist the initial BeginRun count.
            _towerRun.Potions = _potionItem
                ? _townInventory.GetCount(_potionItem) + (_runInventory != null ? _runInventory.GetCount(_potionItem) : 0)
                : 0;
            if (!survived) _towerRun.Defeat();
            int carried = _towerRun.RunLoot;
            int reward = _towerRun.ReturnToTown(survived);
            _gold = _towerRun.Gold;
            _townLevel = _towerRun.TownLevel;
            TownEventBus.Publish(TownEventBus.EventId.GoldChanged, _gold);
            TownEventBus.Publish(TownEventBus.EventId.TownLevelChanged, _townLevel);
            _runInventory = null;
            _townInventory.IdentifyEggs();
            _message = survived
                ? T($"You returned with {carried} salvage. You now have {_gold} G.", $"Você voltou com {carried} em tesouros. Agora tem {_gold} G.")
                : T($"You were rescued from the Tower and kept {reward} G of the {carried} you found.", $"Você foi resgatado da torre e ficou com {reward} G dos {carried} G que encontrou.");
            _towerRun.FinishReturn();
            _familiarOrders.Clear();
            if (_starterJoinedThisRun) TownSaveVariables.SetBool("starter_kewne_received", true);
            _starterJoinedThisRun = false;
            TownSaveVariables.SetInt(StoryPrefix + "day", StoryDay + 1);
            TownSaveVariables.SetInt(TownReputation.WakeEntryKey, 0);
            TownRetailScriptFlags.CreatePersistent().ClearBit(TownReputation.WreathSpokenFlag);
            PersistTownProgress();
            return true;
        }

        private void BeginManagedTowerRun()
        {
            ResetKohProgression();
            for (int i = 0; i < 4; i++) _playerActor.SetStatus(i, 0, 0);
            _completedTowerTurns = 0;
            _familiarOrders.Clear();
            _towerRun = new TowerRunManager();
            int potions = _potionItem ? _townInventory.GetCount(_potionItem) : 0;
            int seed = InTowerTutorial ? 173 : UnityEngine.Random.Range(1, int.MaxValue);
            _towerRun.BeginRun(seed, _floor, _townInventory, _health, _gold, _townLevel,
                _familiarDefinition ? _familiarDefinition.DisplayName : string.Empty, potions);
            _runInventory = _towerRun.RunInventory;
            if (Inventory.IsCursedEquipment(CombatGear(_playerActor, false)) ||
                Inventory.IsCursedEquipment(CombatGear(_playerActor, true)))
                Status(_playerActor, AzureDreams.Core.StatusId.CURSE, 1);
        }
    }
}
