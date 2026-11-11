using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using AzureDreams.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string BlueCollarKey = "tower.blue_collar_acquired";
        private const byte CollarKind = 0x0C;
        private static bool HasBlueCollar => TownSaveVariables.GetBool(BlueCollarKey);
        private static bool IsBlueCollar(DungeonItemEntry item) =>
            item?.Kind == CollarKind && item.Subtype == ItemSystem.Id.BlueCollar;
        private BagFamiliar _blueCollarFamiliar;
        private ThrownEggAlly _blueCollarAlly;

        private void SpawnBlueCollarFamiliar(BagFamiliar reserve, Vector2Int cell, bool animateSummon)
        {
            var prefab = reserve.Species > 0 ? HatchedFamiliarPrefab(reserve.Species) :
                reserve.Definition == _kewneDefinition ? _kewnePrefab : _creaturePrefab;
            var view = SpawnActor(prefab, cell, reserve.Name + " (Blue Collar)",
                species: reserve.Actor.Species, palette: FamiliarPalette(reserve.Actor.Species, reserve.Actor));
            ConfigureLocomotion(view);
            _blueCollarFamiliar = reserve;
            _blueCollarAlly = new ThrownEggAlly { Actor = reserve.Actor, View = view, Cell = cell, Turns = int.MaxValue };
            _thrownEggs.Add(_blueCollarAlly);
            if (animateSummon) StartCoroutine(PresentFamiliarSummoning(view, true));
        }

        private IEnumerator SummonTowerEntryFamiliars(GameObject arrivingKoh)
        {
            // The inventory lists the primary familiar first, then the reserves in roster order.
            if (_familiarDefinition && !_familiarDefeated && _familiarInBag &&
                TryEntrySummonDirection(Vector2Int.left, out var redDirection))
            {
                SummonFromBag(redDirection);
                while (_towerArrivalPlaying && _player == arrivingKoh && _pullingFamiliarFromBag)
                    yield return null;
            }

            if (!_towerArrivalPlaying || _player != arrivingKoh || !HasBlueCollar || _bagFamiliars.Count == 0) yield break;
            var second = _bagFamiliars[0];
            if (second.Defeated || second.Actor.Hp == 0 ||
                !TryEntrySummonDirection(Vector2Int.right, out var blueDirection)) yield break;

            SpawnBlueCollarFamiliar(second, _playerCell + blueDirection, animateSummon: true);
            while (_towerArrivalPlaying && _player == arrivingKoh && _pullingFamiliarFromBag)
                yield return null;
        }

        private bool TryEntrySummonDirection(Vector2Int side, out Vector2Int direction)
        {
            var left = new Vector2Int(-_towerFacing.y, _towerFacing.x);
            var preferred = side == Vector2Int.left ? left : -left;
            direction = CanSummonFamiliarAt(preferred) ? preferred :
                CanSummonFamiliarAt(_towerFacing) ? _towerFacing :
                CanSummonFamiliarAt(-_towerFacing) ? -_towerFacing : -preferred;
            return CanSummonFamiliarAt(direction);
        }

        private void RestoreBlueCollarSummon()
        {
            if (!HasBlueCollar || _blueCollarFamiliar == null || _blueCollarFamiliar.Actor.Hp == 0) return;
            var preferred = _playerCell + Vector2Int.left;
            var cell = UsesManagedTowerLayout
                ? FindManagedActorCell(preferred, _playerCell, _stairCell, _familiar ? _familiarCell : _playerCell)
                : FindOpenCell(preferred, _playerCell, _stairCell, _familiar ? _familiarCell : _playerCell);
            SpawnBlueCollarFamiliar(_blueCollarFamiliar, cell, animateSummon: false);
        }

        private void PutBlueCollarFamiliarInBag()
        {
            if (_blueCollarAlly?.View)
            {
                _submergedActors.Remove(_blueCollarAlly.Actor);
                if (_blueCollarAlly.Actor.Hp == 0) RetireTowerHitView(_blueCollarAlly.View);
                else RemoveGenerated(_blueCollarAlly.View);
            }
            _thrownEggs.Remove(_blueCollarAlly);
            _runInventory?.ClearEquipped(BlueFamiliarEquipped);
            _blueCollarAlly = null;
            _blueCollarFamiliar = null;
        }

        private const string FamiliarNicknameKey = "traveling_familiar_nickname";
        private bool _familiarInBag;
        private string _towerHatchedNickname = "";
        private ItemCommand? _familiarPrompt;
        private string _familiarNameDraft;
        private Action _finishFamiliarAction;
        private const string BagRosterKey = "traveling_familiar_reserves";
        private readonly List<BagFamiliar> _bagFamiliars = new();
        private sealed class BagFamiliar
        {
            internal ActorDefinition Definition;
            internal ActorRecord Actor;
            internal int Species;
            internal string Nickname;
            internal bool Defeated, Temporary;
            internal string Name => string.IsNullOrEmpty(Nickname) ? Definition.DisplayName : Nickname;
        }

        private BagFamiliar ReserveForRow(int row)
        {
            int index = FamiliarInventoryRow - row - 1;
            return row < FamiliarInventoryRow && index < _bagFamiliars.Count ? _bagFamiliars[index] : null;
        }

        private void SaveBagRoster(bool includeTemporary = false)
        {
            var entries = new List<string>();
            foreach (var familiar in _bagFamiliars)
                if (!familiar.Temporary || includeTemporary)
                    entries.Add(EncodeFamiliar(familiar.Definition, familiar.Species, familiar.Actor, familiar.Nickname) +
                        "@" + (familiar.Defeated ? "1" : "0") + "@" + (familiar.Temporary ? "1" : "0"));
            TownSaveVariables.SetString(BagRosterKey, string.Join("|", entries));
        }

        private void LoadBagRoster()
        {
            _bagFamiliars.Clear();
            foreach (var slot in TownSaveVariables.GetString(BagRosterKey).Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                var definition = HutDefinition(slot);
                if (!definition) continue;
                var fields = slot.Split('@');
                _bagFamiliars.Add(new BagFamiliar { Definition = definition, Actor = ReadStoredFamiliarActor(slot, definition),
                    Species = ReadHatchedSlot(slot), Nickname = HutFamiliarNickname(slot),
                    Defeated = fields.Length > 4 && fields[4] == "1", Temporary = fields.Length > 5 && fields[5] == "1" });
            }
        }

        private void PrepareTownFamiliarRoster()
        {
            RestorePersistentFamiliar();
            _bagFamiliars.RemoveAll(familiar => familiar.Temporary);
            foreach (var familiar in _bagFamiliars)
            {
                familiar.Defeated = false;
                familiar.Actor.Hp = familiar.Actor.MaxHp;
            }
            if (!_familiarDefinition && _bagFamiliars.Count > 0) ActivateBagFamiliar(_bagFamiliars[0]);
        }

        private void ExposeParkedFamiliar()
        {
            if (_parkedFamiliarActor == null || !_parkedFamiliarDefinition) return;
            _bagFamiliars.Add(new BagFamiliar { Definition = _parkedFamiliarDefinition, Actor = _parkedFamiliarActor,
                Species = _parkedFamiliarSpecies, Nickname = TownSaveVariables.GetString(FamiliarNicknameKey),
                Defeated = _parkedFamiliarActor.Hp == 0 });
            _parkedFamiliarActor = new ActorRecord();
            _parkedFamiliarDefinition = null;
            _parkedFamiliarSpecies = 0;
            TownSaveVariables.SetString(FamiliarNicknameKey, "");
        }

        private void ActivateBagFamiliar(BagFamiliar familiar)
        {
            if (!_bagFamiliars.Remove(familiar)) return;
            _runInventory?.ClearEquipped(FamiliarEquipped);
            if (_familiarDefinition)
            {
                bool temporary = _parkedFamiliarActor != null;
                _bagFamiliars.Add(new BagFamiliar { Definition = _familiarDefinition, Actor = _familiarActor,
                    Species = _hatchedSpecies, Nickname = temporary ? _towerHatchedNickname : TownSaveVariables.GetString(FamiliarNicknameKey),
                    Defeated = _familiarDefeated, Temporary = temporary });
                if (temporary) RestorePersistentFamiliar();
                else _familiarDefinition = null;
            }
            if (familiar.Temporary)
            {
                _parkedFamiliarActor = _familiarActor;
                _parkedFamiliarDefinition = _familiarDefinition;
                _parkedFamiliarSpecies = _familiarDefinition ? _hatchedSpecies : 0;
                _towerHatchedNickname = familiar.Nickname;
            }
            else
            {
                if (_familiarDefinition)
                    _bagFamiliars.Add(new BagFamiliar { Definition = _familiarDefinition, Actor = _familiarActor,
                        Species = _hatchedSpecies, Nickname = TownSaveVariables.GetString(FamiliarNicknameKey),
                        Defeated = _familiarActor.Hp == 0 });
                TownSaveVariables.SetString(FamiliarNicknameKey, familiar.Nickname);
            }
            _familiarDefinition = familiar.Definition;
            _familiarActor = familiar.Actor;
            _hatchedSpecies = familiar.Species;
            _familiarHealth = familiar.Actor.Hp;
            _familiarDefeated = familiar.Defeated;
            _familiarInBag = true;
        }

        private string FamiliarDisplayName
        {
            get
            {
                string nickname = _parkedFamiliarActor != null ? _towerHatchedNickname :
                    TownSaveVariables.GetString(FamiliarNicknameKey);
                return string.IsNullOrEmpty(nickname) ? _familiarDefinition.DisplayName : nickname;
            }
        }

        private void ExecuteFamiliarCommand(ItemCommand command)
        {
            var reserve = ReserveForRow(SelectedRawIndex);
            if ((!_familiarDefinition && reserve == null) || _turnBusy ||
                (_area == Area.Tower && KohIsAsleep)) return;
            switch (command)
            {
                case ItemCommand.Select:
                    if (_area != Area.Tower || _carriedFamiliar) return;
                    bool useBlueCollar = _familiar && HasBlueCollar && reserve != null && _blueCollarFamiliar == null;
                    if (_familiar && !useBlueCollar)
                    {
                        _message = T("Put your summoned familiar back in the bag first.", "Guarde o familiar invocado na bolsa primeiro.");
                        _inventoryOpen = false;
                        return;
                    }
                    if (reserve != null && !useBlueCollar && !CanRemoveFamiliarEquipment()) return;
                    if (reserve?.Defeated ?? _familiarDefeated)
                    {
                        _message = T("Your familiar cannot fight.", "Seu familiar não pode lutar.");
                        _inventoryOpen = false;
                        return;
                    }
                    var left = new Vector2Int(-_towerFacing.y, _towerFacing.x);
                    var direction = CanSummonFamiliarAt(left) ? left :
                        CanSummonFamiliarAt(_towerFacing) ? _towerFacing : -_towerFacing;
                    if (CanSummonFamiliarAt(direction))
                        ScheduleInventoryAction(() => _finishFamiliarAction = () =>
                        {
                            if (useBlueCollar)
                            {
                                if (CanSummonFamiliarAt(direction))
                                    SpawnBlueCollarFamiliar(reserve, _playerCell + direction, animateSummon: true);
                                return;
                            }
                            if (reserve != null) ActivateBagFamiliar(reserve);
                            SummonFromBag(direction);
                        },
                            summonCell: _playerCell + direction);
                    else
                    {
                        _message = T("There is no room to summon your familiar.", "Não há espaço para invocar seu familiar.");
                        _inventoryOpen = false;
                    }
                    break;
                case ItemCommand.Deselect:
                    if (_area != Area.Tower) return;
                    if (reserve != null && reserve == _blueCollarFamiliar && _blueCollarAlly?.View)
                    {
                        if (CanDeselectFamiliar(_blueCollarAlly.Cell, reserve.Name) &&
                            CanRemoveFamiliarEquipment(BlueFamiliarEquipped))
                            ScheduleInventoryAction(() => _finishFamiliarAction = PutBlueCollarFamiliarInBag);
                    }
                    else if (reserve == null && _familiar && CanDeselectFamiliar(_familiarCell, FamiliarDisplayName))
                        ScheduleInventoryAction(() => _finishFamiliarAction = PutFamiliarInBag);
                    break;
                case ItemCommand.Return:
                    if (reserve != null ? !reserve.Temporary : _parkedFamiliarActor == null) _familiarPrompt = command;
                    break;
                case ItemCommand.Rename:
                    _familiarNameDraft = reserve?.Name ?? FamiliarDisplayName;
                    _familiarPrompt = command;
                    break;
            }
        }

        private bool CanDeselectFamiliar(Vector2Int cell, string name)
        {
            if (Distance(_playerCell, cell) <= 6) return true;

            _inventoryOpen = false;
            _itemCommandIndex = -1;
            _message = T($"{name} is too far away", $"{name} está muito distante");
            return false;
        }

        private bool CanSummonFamiliarAt(Vector2Int direction)
        {
            var cell = _playerCell + direction;
            return InsideMap(cell) && !_walls.Contains(cell) && !_npcCells.Contains(cell) && cell != _stairCell &&
                (!_familiar || _familiarCell != cell) && CreatureAt(cell) < 0 && !_thrownEggs.Exists(ally => ally.View && ally.Cell == cell) &&
                !CrossesClosedTowerDoor(_playerCell, direction);
        }

        private void SummonFromBag(Vector2Int direction)
        {
            if (!CanSummonFamiliarAt(direction))
            {
                _message = T("There is no room to summon your familiar.", "Não há espaço para invocar seu familiar.");
                return;
            }
            _familiarInBag = false;
            _familiarWaiting = false;
            _familiarCell = _playerCell + direction;
            _familiar = SpawnFamiliar(pullFromBag: true);
            _familiarHealth = Mathf.Clamp(_familiarHealth, 1, FamiliarMaxHealth);
            _familiarActor.Hp = StatByte(_familiarHealth);
            _familiarLocomotion = ConfigureLocomotion(_familiar);
            _familiarAnimation = _familiar.GetComponentInChildren<Rendering.PsxSpriteRenderer>();
            Animate(_familiarAnimation, false);
        }

        private void PutFamiliarInBag()
        {
            var view = _carriedFamiliar ? _carriedView : _familiar;
            if (!view) return;
            _submergedActors.Remove(_familiarActor);
            SetActorCell(_familiarActor, new Vector2Int(-1, -1));
            _frogViews.Remove(view);
            view.SetActive(false);
            RemoveGenerated(view);
            if (_carriedFamiliar)
            {
                _carriedFamiliar = false;
                _carriedView = null;
            }
            _familiar = null;
            _familiarLocomotion = null;
            _familiarAnimation = null;
            _familiarInBag = true;
        }

        private void ReturnFamiliarToHut(BagFamiliar reserve)
        {
            if ((reserve != null && reserve == _blueCollarFamiliar) || reserve?.Temporary == true || (reserve == null && (!_familiarDefinition || _parkedFamiliarActor != null))) return;
            if (reserve == null && !CanRemoveFamiliarEquipment()) return;
            var roster = ReadHutRoster();
            int slot = roster.FindIndex(entry => entry == EmptyHutSlot);
            if (slot < 0 || slot >= HutPads.Length)
            {
                _message = T("The monster hut is full.", "O alojamento de monstros está cheio.");
                _inventoryOpen = false;
                return;
            }
            roster[slot] = reserve == null ? EncodeTravelingFamiliar() :
                EncodeFamiliar(reserve.Definition, reserve.Species, reserve.Actor, reserve.Nickname);
            if (reserve != null) _bagFamiliars.Remove(reserve);
            else
            {
                PutFamiliarInBag();
                _runInventory?.ClearEquipped(FamiliarEquipped);
                _familiarDefinition = null;
                _hatchedSpecies = 0;
                _familiarActor = new ActorRecord();
                _familiarHealth = 0;
                _familiarInBag = _familiarDefeated = _familiarWaiting = false;
                TownSaveVariables.SetString(FamiliarNicknameKey, "");
            }
            if (_area == Area.Town && !_familiarDefinition && _bagFamiliars.Count > 0)
                ActivateBagFamiliar(_bagFamiliars[0]);
            if (InHomeHut) SaveHutRoster(roster);
            else
            {
                TownSaveVariables.SetString(HutRosterKey, string.Join("|", roster));
                PersistTownProgress();
            }
            RefreshInventoryCommands();
            _inventoryOpen = false;
            _message = T("The familiar returned to the monster hut.", "O familiar voltou ao alojamento.");
        }

        private bool CanRemoveFamiliarEquipment(byte wearer = FamiliarEquipped)
        {
            if (_area != Area.Tower ||
                (!Inventory.IsCursedEquipment(_runInventory?.EquippedItem(wearer, false)) &&
                 !Inventory.IsCursedEquipment(_runInventory?.EquippedItem(wearer, true)))) return true;
            ShowLockedEquipmentMessage();
            return false;
        }

        private string EncodeTravelingFamiliar() => EncodeFamiliar(_familiarDefinition, _hatchedSpecies, _familiarActor,
            TownSaveVariables.GetString(FamiliarNicknameKey));

        private string EncodeFamiliar(ActorDefinition definition, int species, ActorRecord actor, string nickname)
        {
            string id = species > 0 ? HatchedPrefix + species : definition == _kewneDefinition ? "kewne" : "recruited";
            return id + "@" + actor.Experience + "@" + Convert.ToBase64String(Encoding.UTF8.GetBytes(nickname ?? "")) +
                "@" + Convert.ToBase64String(CaptureActor(actor));
        }

        private static bool RestoreStoredFamiliarActor(string slot, ActorRecord actor)
        {
            var fields = slot.Split('@');
            if (fields.Length < 4) return false;
            try
            {
                var bytes = Convert.FromBase64String(fields[3]);
                if (bytes.Length != ActorRecord.Size) return false;
                RestoreActor(actor, bytes);
                return true;
            }
            catch (FormatException) { return false; }
        }

        private ActorRecord ReadStoredFamiliarActor(string slot, ActorDefinition definition)
        {
            var actor = new ActorRecord();
            if (!RestoreStoredFamiliarActor(slot, actor))
                RestoreLegacyFamiliarProgression(actor, definition, ReadHatchedSlot(slot), FamiliarSlotExperience(slot));
            return actor;
        }

        private static string HutFamiliarNickname(string slot)
        {
            var fields = slot.Split('@');
            if (fields.Length < 3) return "";
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(fields[2])); }
            catch (FormatException) { return ""; }
        }

        private bool HandleFamiliarInventoryPrompt()
        {
            if (!_familiarPrompt.HasValue) return false;
            if (_cancel?.WasPressedThisFrame() == true) _familiarPrompt = null;
            else if (_familiarPrompt == ItemCommand.Return && _interact?.WasPressedThisFrame() == true)
                ConfirmFamiliarInventoryPrompt();
            else if (_familiarPrompt == ItemCommand.Rename && Keyboard.current?.enterKey.wasPressedThisFrame == true)
                ConfirmFamiliarInventoryPrompt();
            return true;
        }

        private void ConfirmFamiliarInventoryPrompt()
        {
            var prompt = _familiarPrompt;
            _familiarPrompt = null;
            var reserve = ReserveForRow(SelectedRawIndex);
            if (prompt == ItemCommand.Return) ReturnFamiliarToHut(reserve);
            else if (prompt == ItemCommand.Rename)
            {
                string name = _familiarNameDraft.Trim();
                if (name.Length == 0) return;
                if (reserve != null)
                {
                    reserve.Nickname = name;
                    if (!reserve.Temporary) PersistTownProgress();
                }
                else if (_parkedFamiliarActor != null) _towerHatchedNickname = name;
                else
                {
                    TownSaveVariables.SetString(FamiliarNicknameKey, name);
                    PersistTownProgress();
                }
            }
        }

        private void DrawFamiliarInventoryPrompt(float width, float height)
        {
            bool rename = _familiarPrompt == ItemCommand.Rename;
            var panel = new Rect(width * 0.5f - 140f, height * 0.5f - 56f, 280f, 112f);
            DrawDialogueFrame(panel);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 10f, 256f, 25f), rename
                ? T("Name your familiar", "Nome do familiar")
                : T("Return the familiar to the hut?", "Enviar o familiar ao alojamento?"));
            if (rename)
                _familiarNameDraft = GUI.TextField(new Rect(panel.x + 12f, panel.y + 38f, 256f, 24f), _familiarNameDraft, 24);
            if (GUI.Button(new Rect(panel.x + 12f, panel.yMax - 36f, 122f, 24f), T("Confirm", "Confirmar")))
                ConfirmFamiliarInventoryPrompt();
            if (GUI.Button(new Rect(panel.xMax - 134f, panel.yMax - 36f, 122f, 24f), T("Cancel", "Cancelar")))
                _familiarPrompt = null;
        }
    }
}
