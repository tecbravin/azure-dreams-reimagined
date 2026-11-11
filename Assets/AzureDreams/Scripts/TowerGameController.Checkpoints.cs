using System;
using System.Collections;
using AzureDreams.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private enum CheckpointMenu { Closed, Wreath, Ascend, SaveBeforeAscend, SaveSlots, LoadSlots, Overwrite }
        private CheckpointMenu _checkpointMenu;
        private bool _elevatorDeclined;
        private bool _saveAtElevator;
        private int _checkpointSelection;
        private const int WreathStorageOption = 2;
        private const int WreathOptionCount = 3;

        private bool HandleCheckpointInput()
        {
            if (_checkpointMenu == CheckpointMenu.Closed) return false;
            var keyboard = Keyboard.current;
            if (keyboard == null) return true;
            if (keyboard.escapeKey.wasPressedThisFrame) CancelCheckpointMenu();
            else if (_checkpointMenu == CheckpointMenu.SaveBeforeAscend && keyboard.nKey.wasPressedThisFrame) AscendFromElevator();
            else if (_checkpointMenu == CheckpointMenu.Wreath)
            {
                int delta = keyboard.upArrowKey.wasPressedThisFrame ? -1 : keyboard.downArrowKey.wasPressedThisFrame ? 1 : 0;
                _checkpointSelection = Mathf.Clamp(_checkpointSelection + delta, 0, WreathOptionCount - 1);
                if (_interact?.WasPressedThisFrame() == true) SelectWreathOption(_checkpointSelection);
            }
            else if (_checkpointMenu is CheckpointMenu.SaveSlots or CheckpointMenu.LoadSlots)
            {
                int delta = keyboard.upArrowKey.wasPressedThisFrame ? -1 : keyboard.downArrowKey.wasPressedThisFrame ? 1 : 0;
                _checkpointSelection = Mathf.Clamp(_checkpointSelection + delta, 0, SaveSystem.SlotCount - 1);
                if (_interact != null && _interact.WasPressedThisFrame()) SelectCheckpoint(_checkpointSelection);
            }
            else if (_interact != null && _interact.WasPressedThisFrame())
            {
                switch (_checkpointMenu)
                {
                    case CheckpointMenu.Ascend: _checkpointMenu = CheckpointMenu.SaveBeforeAscend; break;
                    case CheckpointMenu.SaveBeforeAscend: OpenSaveSlots(true); break;
                    case CheckpointMenu.Overwrite: WriteCheckpoint(_checkpointSelection); break;
                }
            }
            return true;
        }

        private void SelectWreathOption(int option)
        {
            switch (option)
            {
                case 0: OpenSaveSlots(false); break;
                case 1:
                    _checkpointSelection = 0;
                    RefreshCheckpointSlots();
                    _checkpointMenu = CheckpointMenu.LoadSlots;
                    break;
                case WreathStorageOption: OpenHomeStorage(fromWreath: true); break;
            }
        }

        private void OpenSaveSlots(bool elevator)
        {
            _saveAtElevator = elevator;
            _checkpointSelection = 0;
            RefreshCheckpointSlots();
            _checkpointMenu = CheckpointMenu.SaveSlots;
        }

        private void CancelCheckpointMenu()
        {
            switch (_checkpointMenu)
            {
                case CheckpointMenu.Overwrite: _checkpointMenu = CheckpointMenu.SaveSlots; break;
                case CheckpointMenu.LoadSlots: _checkpointSelection = 1; _checkpointMenu = CheckpointMenu.Wreath; break;
                case CheckpointMenu.SaveSlots: _checkpointSelection = 0; _checkpointMenu = _saveAtElevator ? CheckpointMenu.SaveBeforeAscend : CheckpointMenu.Wreath; break;
                case CheckpointMenu.SaveBeforeAscend: _checkpointMenu = CheckpointMenu.Ascend; break;
                case CheckpointMenu.Ascend:
                    _elevatorDeclined = true;
                    _checkpointMenu = CheckpointMenu.Closed;
                    break;
                default: _checkpointMenu = CheckpointMenu.Closed; break;
            }
        }

        private void SelectCheckpoint(int slot)
        {
            _checkpointSelection = slot;
            var saved = SaveSystem.ReadSlot(slot);
            if (_checkpointMenu == CheckpointMenu.LoadSlots)
            {
                if (saved != null) StartCoroutine(LoadCheckpoint(saved));
                return;
            }
            if (saved == null) WriteCheckpoint(slot);
            else _checkpointMenu = CheckpointMenu.Overwrite;
        }

        private static byte[] CaptureActor(ActorRecord actor)
        {
            var data = new byte[ActorRecord.Size];
            for (int i = 0; i < data.Length; i++) data[i] = actor.U8(i);
            return data;
        }

        private static void RestoreActor(ActorRecord actor, byte[] data)
        {
            if (data?.Length != ActorRecord.Size) return;
            for (int i = 0; i < data.Length; i++) actor.SetRaw(i, data[i]);
        }

        private void WriteCheckpoint(int slot)
        {
            var carried = _saveAtElevator ? _runInventory : _townInventory;
            if (carried == null) return;
            if (_carriedItem != null)
            {
                _checkpointMenu = CheckpointMenu.Closed;
                _message = T("Put down what you are holding before saving.", "Coloque no chão o que está segurando antes de salvar.");
                return;
            }
            var parked = _parkedFamiliarActor != null;
            RecordPlayTime();
            int saveCount = TownSaveVariables.GetInt(ManualSaveCountKey);
            TownSaveVariables.SetInt(ManualSaveCountKey, saveCount < int.MaxValue ? saveCount + 1 : saveCount);
            SaveBagRoster(includeTemporary: _saveAtElevator);
            SaveSystem.WriteSlot(slot, new SaveSystem.Checkpoint
            {
                gold = _gold, townLevel = _townLevel,
                familiar = _familiarDefinition ? _familiarDefinition.DisplayName : string.Empty,
                familiarSpecies = _hatchedSpecies,
                familiarDefeated = _saveAtElevator && _familiarDefeated,
                familiarInBag = _saveAtElevator && _familiarInBag,
                blueCollarRosterSlot = _saveAtElevator && _blueCollarFamiliar != null
                    ? _bagFamiliars.IndexOf(_blueCollarFamiliar) + 1 : 0,
                primaryFamiliarOrder = _saveAtElevator ? CaptureFamiliarOrder(_familiarActor) : default,
                reserveFamiliarOrders = _saveAtElevator
                    ? _bagFamiliars.ConvertAll(familiar => CaptureFamiliarOrder(familiar.Actor)).ToArray() : null,
                towerHatchedNickname = parked ? _towerHatchedNickname : null,
                actor = _saveAtElevator ? CaptureActor(_playerActor) : null,
                familiarActor = CaptureActor(_familiarActor),
                parkedActor = parked ? CaptureActor(_parkedFamiliarActor) : null,
                parkedFamiliar = parked && _parkedFamiliarDefinition ? _parkedFamiliarDefinition.DisplayName : string.Empty,
                parkedSpecies = _parkedFamiliarSpecies,
                carried = carried.SaveDungeonItems(), stored = _townStorage.SaveDungeonItems(),
                potions = _potionItem ? carried.GetCount(_potionItem) : 0,
                storedPotions = _potionItem ? _townStorage.GetCount(_potionItem) : 0,
                town = TownSaveVariables.Capture(),
                floor = _saveAtElevator ? _floor + 1 : 0,
                rng = _saveAtElevator ? unchecked((int)_towerRun.Rng.Seed) : 0,
                loot = _saveAtElevator ? _towerRun.RunLoot : 0,
                turns = _saveAtElevator ? _completedTowerTurns : 0,
                generated = _useGeneratedTowerFloors, starterJoined = _saveAtElevator && _starterJoinedThisRun
            });
            if (_saveAtElevator) AscendFromElevator();
            else _checkpointMenu = CheckpointMenu.Closed;
            _message = T($"Saved in slot {slot + 1}.", $"Jogo salvo no espaço {slot + 1}.");
        }

        private ActorDefinition RestoreCheckpointFamiliar(string name, int species, byte[] data)
        {
            if (species > 0 && EquipHatchedFamiliar(species, 1))
            {
                RestoreActor(_familiarActor, data);
                return _familiarDefinition;
            }
            RestoreActor(_familiarActor, data);
            if (_kewneDefinition && name == _kewneDefinition.DisplayName) return _kewneDefinition;
            return _creatureDefinition && name == _creatureDefinition.DisplayName ? _creatureDefinition : null;
        }

        private IEnumerator LoadCheckpoint(SaveSystem.Checkpoint data)
        {
            _checkpointMenu = CheckpointMenu.Closed;
            _changingInterior = true;
            var interior = _interior ? _interior.gameObject.scene : default;
            _interior = null;
            if (interior.IsValid() && interior.isLoaded && interior != gameObject.scene)
                yield return SceneManager.UnloadSceneAsync(interior);
            _thrownEggs.Clear();
            _mpProtection.Clear();
            _doubleMpCost.Clear();
            _unsavedPlaySeconds = 0f;
            TownSaveVariables.Restore(data.town);
            LoadBagRoster();
            _blueCollarFamiliar = data.blueCollarRosterSlot > 0 && data.blueCollarRosterSlot <= _bagFamiliars.Count
                ? _bagFamiliars[data.blueCollarRosterSlot - 1] : null;
            _gold = data.gold; _townLevel = data.townLevel;
            var discarded = new Inventory();
            _townInventory.TransferTo(discarded);
            _townStorage.TransferTo(discarded);
            _townInventory.RestoreDungeonItems(data.carried);
            _townStorage.RestoreDungeonItems(data.stored);
            if (!_potionItem) _potionItem = CreatePotionDefinition();
            _townInventory.AddItem(_potionItem, data.potions);
            _townStorage.AddItem(_potionItem, data.storedPotions);
            _tutorialRun = false;
            _starterJoinedThisRun = data.starterJoined;
            _tutorialDialogue = 0;
            _parkedFamiliarActor = null;
            _towerHatchedNickname = data.towerHatchedNickname ?? "";
            _familiarInBag = data.familiarInBag;
            if (data.parkedActor?.Length == ActorRecord.Size)
            {
                _familiarActor = new ActorRecord();
                _parkedFamiliarDefinition = RestoreCheckpointFamiliar(data.parkedFamiliar, data.parkedSpecies, data.parkedActor);
                _parkedFamiliarActor = _familiarActor;
                _parkedFamiliarSpecies = data.parkedSpecies;
                ExposeParkedFamiliar();
            }
            _familiarActor = new ActorRecord();
            _hatchedSpecies = data.familiarSpecies;
            _familiarDefinition = RestoreCheckpointFamiliar(data.familiar, data.familiarSpecies, data.familiarActor);
            _familiarHealth = _familiarActor.Hp;
            if (data.floor == 0)
            {
                EnterTown();
                PersistTownProgress();
            }
            else
            {
                _area = Area.Tower;
                TurnSystem.SetMode(TurnSystem.Mode.TurnBased);
                _useGeneratedTowerFloors = data.generated;
                _floor = data.floor;
                BeginManagedTowerRun();
                RestoreFamiliarOrder(_familiarActor, data.primaryFamiliarOrder);
                if (data.reserveFamiliarOrders != null)
                    for (int i = 0; i < Mathf.Min(_bagFamiliars.Count, data.reserveFamiliarOrders.Length); i++)
                        RestoreFamiliarOrder(_bagFamiliars[i].Actor, data.reserveFamiliarOrders[i]);
                _towerRun.Rng.Seed = unchecked((uint)data.rng);
                _towerRun.AddLoot(data.loot);
                _runLoot = data.loot;
                _completedTowerTurns = data.turns;
                RestoreActor(_playerActor, data.actor);
                _health = _playerActor.Hp;
                _towerRun.SetHealth(_health);
                _familiarDefeated = data.familiarDefeated;
                BuildTowerFloor();
                TownEventBus.Publish(TownEventBus.EventId.PlayerEnteredTower);
            }
            _changingInterior = false;
            _message = string.Empty;
        }

        private void AscendFromElevator()
        {
            _checkpointMenu = CheckpointMenu.Closed;
            if (_elevatorAscending || !_player || _turnBusy || ItemImmobilized(_playerActor) ||
                _towerRun?.CurrentState != TowerRunManager.State.InTower || _playerCell != _stairCell || _floor >= 40) return;
            BeginElevatorAscent();
        }

        private void CompleteElevatorAscent()
        {
            _towerRun.AdvanceFloor();
            _floor = _towerRun.Floor;
            BuildTowerFloor();
            _towerRun.CompleteFloorTransition();
            _message = T($"You reach floor {_floor}.", $"Você chega ao {_floor}º andar.");
        }

        private void DrawCheckpointMenu(float width, float height)
        {
            if (_checkpointMenu == CheckpointMenu.Closed) return;
            if (_checkpointMenu is CheckpointMenu.LoadSlots or CheckpointMenu.SaveSlots)
            {
                bool loading = _checkpointMenu == CheckpointMenu.LoadSlots;
                DrawSaveSlotList(width, height, loading ? T("Load game", "Carregar jogo") : T("Save game", "Salvar jogo"),
                    _checkpointSlots, _checkpointSelection, loading, SelectCheckpoint, CancelCheckpointMenu);
                return;
            }
            var rect = new Rect(width * 0.5f - 180, height * 0.5f - 100, 360, 200);
            DrawDialogueFrame(rect);
            GUILayout.BeginArea(new Rect(rect.x + 12, rect.y + 10, rect.width - 24, rect.height - 20));
            switch (_checkpointMenu)
            {
                case CheckpointMenu.Wreath:
                    GUILayout.Label(T("Wreath: What is it?", "Wreath: O que foi?"));
                    for (int i = 0; i < WreathOptionCount; i++)
                    {
                        string label = i switch
                        {
                            0 => T("Save game", "Salvar jogo"),
                            1 => T("Load game", "Carregar jogo"),
                            _ => T("Open item storage", "Abrir depósito de itens")
                        };
                        if (GUILayout.Button((_checkpointSelection == i ? "▸ " : "") + label))
                        { SelectWreathOption(i); break; }
                    }
                    break;
                case CheckpointMenu.Ascend:
                    GUILayout.Label(T("Do you want to go up?", "Quer subir?"));
                    if (GUILayout.Button(WithInteractHint("Yes", "Sim"))) _checkpointMenu = CheckpointMenu.SaveBeforeAscend;
                    break;
                case CheckpointMenu.SaveBeforeAscend:
                    GUILayout.Label(T("Save before going up?", "Quer salvar antes de subir?"));
                    if (GUILayout.Button(WithInteractHint("Save", "Salvar"))) OpenSaveSlots(true);
                    if (GUILayout.Button(T("Go up without saving [N]", "Subir sem salvar [N]"))) AscendFromElevator();
                    break;
                case CheckpointMenu.Overwrite:
                    GUILayout.Label(T("Overwrite this saved game?", "Quer substituir este jogo salvo?"));
                    if (GUILayout.Button(WithInteractHint("Overwrite", "Substituir"))) WriteCheckpoint(_checkpointSelection);
                    break;
            }
            if (_checkpointMenu != CheckpointMenu.Closed)
            {
                string cancel = _checkpointMenu == CheckpointMenu.Ascend ? T("No [Esc]", "Não [Esc]") : T("Back [Esc]", "Voltar [Esc]");
                if (GUILayout.Button(cancel)) CancelCheckpointMenu();
            }
            GUILayout.EndArea();
        }
    }
}
