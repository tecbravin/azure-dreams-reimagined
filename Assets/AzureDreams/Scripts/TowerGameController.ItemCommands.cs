using System;
using System.Collections.Generic;
using AzureDreams.Core;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private enum ItemCommand { Use, Equip, Unequip, Drop, Hold, Give, Warm, Select, Deselect, Return, Rename }
        private const byte PlayerEquipped = 0x80;
        private const byte FamiliarEquipped = 0x40;
        private const byte BlueFamiliarEquipped = 0x20;

        private static bool IsTrollWeapon(DungeonItemEntry item) =>
            item?.Kind == ItemKind.Sword && item.Subtype is >= 13 and <= 15;

        private static bool IsTroll(ActorRecord actor) => actor?.Species == (byte)MonsterSpecies.TROLL;
        private int _itemCommandIndex = -1;
        private Action _pendingItemCommand;
        private Vector2Int? _discardedUnderKoh;
        private readonly Dictionary<Vector2Int, (DungeonItemEntry item, GameObject view)> _townDroppedItems = new();
        private readonly List<ItemCommand> _availableItemCommands = new();
        private Inventory CarriedInventory => _area == Area.Tower ? _runInventory : _townInventory;
        private readonly List<int> _inventoryOrder = new();
        private bool _inventoryAlphabetical;
        private const int FamiliarInventoryRow = -2;
        private int VisibleItemCount => _inventoryOrder.Count;
        private int FamiliarInventoryCount => _bagFamiliars.Count + (_familiarDefinition ? 1 : 0);
        private int SelectedRawIndex => _inventoryOffset >= 0 && _inventoryOffset < VisibleItemCount
            ? _inventoryOrder[_inventoryOffset] : -1;

        private DungeonItemEntry ItemAtInventoryRow(int row)
        {
            int rawIndex = _inventoryOrder[row];
            return rawIndex >= 0 && rawIndex < (_inventoryEntries?.Count ?? 0) ? _inventoryEntries[rawIndex] : null;
        }

        private void SortInventory()
        {
            _inventoryAlphabetical = true;
            _inventoryPage = 0;
            _inventoryOffset = 0;
            RefreshInventoryCommands();
        }

        private DungeonItemEntry SelectedInventoryItem(out bool authoredHerb)
        {
            int rawCount = _inventoryEntries?.Count ?? 0;
            int rawIndex = SelectedRawIndex;
            authoredHerb = rawIndex >= rawCount;
            if (authoredHerb) return ItemSystem.CreateFloorItem(1, ItemKind.Herb, (sbyte)Mathf.Clamp(_potionItem.Value, 1, 127));
            return rawIndex >= 0 ? _inventoryEntries[rawIndex] : null;
        }

        private static bool Equipment(DungeonItemEntry item) => Inventory.IsGear(item);
        private static bool Equipped(DungeonItemEntry item) => Equipment(item) && (item.Flags & Inventory.WornMask) != 0;
        private static DungeonItemEntry WithFlags(DungeonItemEntry item, byte flags) => new(item.Subtype, item.Kind, item.Param, flags);
        private static DungeonItemEntry OffBody(DungeonItemEntry item) => Equipment(item)
            ? WithFlags(item, (byte)(item.Flags & ~Inventory.WornMask)) : item;

        private int EquipmentBonus(Inventory inventory, byte wearer, bool shield)
        {
            return inventory == null ? 0 : inventory.EquippedBonus(wearer, shield);
        }

        private int PlayerWeaponDamage() => Mathf.Max(1,
            (_area == Area.Tower && _playerActor.Level > 0 ? _playerActor.CurrentAttack : _playerDefinition ? _playerDefinition.AttackDamage : 3) +
            EquipmentBonus(CarriedInventory, PlayerEquipped, false));
        private int PlayerShieldDefense() => Mathf.Max(0, EquipmentBonus(CarriedInventory, PlayerEquipped, true));
        private int FamiliarWeaponDamage() => Mathf.Max(1,
            _familiarActor.Species > 0 ? _familiarActor.CurrentAttack : _familiarDefinition ? _familiarDefinition.AttackDamage : 2);

        private void RefreshInventoryCommands()
        {
            var inventory = CarriedInventory;
            _inventoryEntries = inventory?.DungeonItems;
            int rawCount = _inventoryEntries?.Count ?? 0;
            int herbCount = _potionItem && inventory != null ? inventory.GetCount(_potionItem) : 0;
            _inventoryOrder.Clear();
            for (int i = 0; i < rawCount + herbCount; i++) _inventoryOrder.Add(i);
            if (_inventoryAlphabetical)
            {
                var compare = System.Globalization.CultureInfo.GetCultureInfo(GameLanguage.Portuguese ? "pt-BR" : "en").CompareInfo;
                string Name(int i) => i < rawCount ? InventoryItemName(_inventoryEntries[i]) : T("Medicinal Herb", "Erva medicinal");
                _inventoryOrder.Sort((left, right) =>
                {
                    int order = compare.Compare(Name(left), Name(right), System.Globalization.CompareOptions.IgnoreCase);
                    return order != 0 ? order : left.CompareTo(right);
                });
            }
            // Familiars are stored separately from carried items.
            for (int i = _bagFamiliars.Count - 1; i >= 0; i--) _inventoryOrder.Insert(0, FamiliarInventoryRow - i - 1);
            if (_familiarDefinition) _inventoryOrder.Insert(0, FamiliarInventoryRow);
            if (_familiarStatus && FamiliarInventoryCount == 0) _familiarStatus = false;
            ClampInventorySelection(_inventoryOffset);
            _itemCommandIndex = -1;
            _availableItemCommands.Clear();
        }

        private bool CanFeedCarriedFamiliar(DungeonItemEntry item) =>
            _carriedFamiliar && item.Kind == ItemKind.Fruit && item.Subtype is 3 or 7;

        private void OpenItemCommands()
        {
            if (SelectedRawIndex <= FamiliarInventoryRow)
            {
                _availableItemCommands.Clear();
                var reserve = ReserveForRow(SelectedRawIndex);
                if (_area == Area.Tower && !_carriedFamiliar)
                    _availableItemCommands.Add((reserve == null && _familiar) || (reserve != null && reserve == _blueCollarFamiliar) ? ItemCommand.Deselect : ItemCommand.Select);
                if (reserve != null ? !reserve.Temporary && reserve != _blueCollarFamiliar : _parkedFamiliarActor == null)
                    _availableItemCommands.Add(ItemCommand.Return);
                _availableItemCommands.Add(ItemCommand.Rename);
                _itemCommandIndex = 0;
                PlayRetailSound(RetailSfx.Confirm);
                return;
            }
            var item = SelectedInventoryItem(out _);
            if (item == null || item.Kind == ItemKind.Familiar) return;
            _availableItemCommands.Clear();
            if (Equipment(item)) _availableItemCommands.Add(Equipped(item) ? ItemCommand.Unequip : ItemCommand.Equip);
            else if (item.Kind == MonsterEgg.Kind && _area == Area.Tower) _availableItemCommands.Add(ItemCommand.Warm);
            else if (!IsPitaFruit(item)) _availableItemCommands.Add(ItemCommand.Use);
            _availableItemCommands.Add(ItemCommand.Drop);
            _availableItemCommands.Add(ItemCommand.Hold);
            if (_area == Area.Tower && (_familiar || CanFeedCarriedFamiliar(item) ||
                (IsTrollWeapon(item) && IsTroll(_blueCollarAlly?.Actor))))
                _availableItemCommands.Add(ItemCommand.Give);
            _itemCommandIndex = 0;
            PlayRetailSound(RetailSfx.Confirm);
        }

        private static string CommandText(ItemCommand command) => command switch
        {
            ItemCommand.Use => T("Use", "Usar"),
            ItemCommand.Equip => T("Equip", "Equipar"),
            ItemCommand.Unequip => T("Unequip", "Desequipar"),
            ItemCommand.Drop => T("Discard", "Descartar"),
            ItemCommand.Hold => T("Hold", "Segurar"),
            ItemCommand.Give => T("Give to familiar", "Dar para o familiar"),
            ItemCommand.Select => T("Select", "Selecionar"),
            ItemCommand.Deselect => T("Deselect", "Desselecionar"),
            ItemCommand.Return => T("Return", "Enviar ao alojamento"),
            ItemCommand.Rename => T("Rename", "Renomear"),
            _ => T("Use (warm)", "Usar (aquecer)")
        };

        private void DrawItemCommands(float width, float height)
        {
            if (!_inventoryOpen || _itemCommandIndex < 0) return;
            float x = width / 2f + 10f;
            float y = height * 0.24f;
            DrawHudPanel(new Rect(x, y, 224, 36 + _availableItemCommands.Count * 31));
            GUI.Label(new Rect(x + 10, y + 6, 210, 24), SelectedRawIndex <= FamiliarInventoryRow ?
                T("Familiar actions", "Ações do familiar") : T("Item actions", "Ações do item"));
            for (int i = 0; i < _availableItemCommands.Count; i++)
                if (GUI.Button(new Rect(x + 10, y + 31 + i * 31, 204, 28),
                    (i == _itemCommandIndex ? "> " : "") + CommandText(_availableItemCommands[i])))
                { ExecuteItemCommand(_availableItemCommands[i]); break; }
        }

        private bool ScheduleInventoryAction(Action action, bool familiarCasting = false, Vector2Int? summonCell = null,
            bool curesImmobilization = false)
        {
            if (_area == Area.Tower)
            {
                bool canAct = (curesImmobilization || !ItemImmobilized(_playerActor)) &&
                    !SlowBlocksAction(_playerActor) && (!familiarCasting || !SlowBlocksAction(_familiarActor));
                if (!CommitTowerTurn(Vector2Int.zero, null, Vector2Int.zero, canAct && familiarCasting,
                    canAct ? summonCell : null)) return false;
                if (canAct) _pendingItemCommand = action;
                _inventoryOpen = false;
            }
            else
            {
                action();
                PersistTownProgress();
            }
            _itemCommandIndex = -1;
            return true;
        }

        private void ResolveItemCommand()
        {
            var action = _pendingItemCommand;
            _pendingItemCommand = null;
            action?.Invoke();
        }

        private void ResetInventoryCommands()
        {
            _pendingItemCommand = null;
            _fusionPreview = null;
            _itemTargetSource = null;
            _discardedUnderKoh = null;
            _townDroppedItems.Clear();
            _itemCommandIndex = -1;
            _thrownEggs.RemoveAll(ally => !ally.Salamander || _area != Area.Tower || ally.Turns <= 0);
            _blueCollarAlly = null;
            _iceBlocks.Clear();
            _sacredTiles.Clear();
            _demoItemsConvertedToGold = false;
            _returnWithCrystal = false;
            _reflectionTurns = _holyBellTurns = 0;
            foreach (var timer in new[] { _mpProtection, _doubleMpCost })
            {
                if (_area != Area.Tower) timer.Clear();
                else
                    foreach (var actor in new List<ActorRecord>(timer.Keys))
                        if (actor != _playerActor && actor != _familiarActor &&
                            !_bagFamiliars.Exists(reserve => reserve.Actor == actor)) timer.Remove(actor);
            }
            _revertedActors.Clear();
            ResetSpeciesActions();
        }

        private void ExecuteItemCommand(ItemCommand command)
        {
            if (SelectedRawIndex <= FamiliarInventoryRow)
            {
                PlayRetailSound(RetailSfx.Confirm);
                ExecuteFamiliarCommand(command);
                return;
            }
            var item = SelectedInventoryItem(out bool authoredHerb);
            var inventory = CarriedInventory;
            int index = SelectedRawIndex;
            if (item == null || inventory == null || item.Kind == ItemKind.Familiar) return;
            PlayRetailSound(RetailSfx.Confirm);
            if (command == ItemCommand.Warm) { WarmSelectedEgg(); _itemCommandIndex = -1; return; }
            bool Remove() => authoredHerb ? inventory.RemoveItem(_potionItem) : inventory.ConsumeDungeonItem(index, item.Kind);
            bool inTower = _area == Area.Tower;
            if (inTower && Inventory.IsLockedEquipment(item))
            {
                ShowLockedEquipmentMessage();
                return;
            }

            if (command is ItemCommand.Equip or ItemCommand.Unequip)
            {
                if (command == ItemCommand.Equip && IsTrollWeapon(item))
                { _message = T("Only Troll types can equip this.", "Só monstros do tipo Troll podem equipar isso."); return; }
                byte wearer = command == ItemCommand.Equip ? PlayerEquipped : (byte)0;
                if (!inventory.CanSetEquipped(index, wearer, inTower))
                {
                    ShowLockedEquipmentMessage();
                    return;
                }
                ScheduleInventoryAction(() =>
                {
                    if (!inventory.SetEquipped(index, wearer, inTower)) return;
                    if (wearer == PlayerEquipped) PlayRetailSound(RetailSfx.Equip);
                    if (inTower && wearer == PlayerEquipped && Inventory.IsCursedEquipment(inventory.DungeonItems[index]))
                        Status(_playerActor, StatusId.CURSE, 1);
                    if (inTower && HasHolySwordProtection(_playerActor))
                        Status(_playerActor, StatusId.CONFUSION, 0);
                    _message = CommandText(command) + ": " + InventoryItemName(inventory.DungeonItems[index]);
                    RefreshInventoryCommands();
                });
                return;
            }
            bool giving = command == ItemCommand.Give;
            if (giving && IsTrollWeapon(item))
            {
                bool redTroll = _familiar && IsTroll(_familiarActor);
                bool blueTroll = IsTroll(_blueCollarAlly?.Actor);
                bool useBlue = blueTroll && (!redTroll ||
                    (inventory.HasEquipped(FamiliarEquipped, false) && !inventory.HasEquipped(BlueFamiliarEquipped, false)));
                if (redTroll || blueTroll)
                {
                    byte wearer = useBlue ? BlueFamiliarEquipped : FamiliarEquipped;
                    var recipient = useBlue ? _blueCollarAlly.Actor : _familiarActor;
                    if (!inventory.CanSetEquipped(index, wearer, lockCursed: true))
                    {
                        ShowLockedEquipmentMessage();
                        return;
                    }
                    ScheduleInventoryAction(() =>
                    {
                        bool active = useBlue ? _blueCollarAlly?.Actor == recipient && recipient.Hp > 0 :
                            _familiar && _familiarActor == recipient && _familiarHealth > 0;
                        if (!active || !inventory.SetEquipped(index, wearer, lockCursed: true)) return;
                        PlayRetailSound(RetailSfx.Equip);
                        _message = T($"{TowerCombatName(recipient)} equipped {InventoryItemName(item)}.",
                            $"{TowerCombatName(recipient)} equipou {InventoryItemName(item)}.");
                        RefreshInventoryCommands();
                    });
                    return;
                }
            }
            if (giving && !_familiar && !CanFeedCarriedFamiliar(item)) return;
            if (giving && Equipment(item)) command = ItemCommand.Drop;
            if (command == ItemCommand.Drop)
            {
                Vector2Int cell = giving ? _familiarCell : _playerCell;
                if (giving ? !CanPlaceInventoryItem(cell) : !TryFindDiscardCell(cell, out _))
                {
                    _message = T("There is no room to drop the item.", "Não há espaço para descartar o item.");
                    return;
                }
                ScheduleInventoryAction(() =>
                {
                    if (giving && !_familiar) return;
                    Vector2Int destination = giving ? _familiarCell : cell;
                    if ((!giving && !TryFindDiscardCell(cell, out destination)) ||
                        !PlaceInventoryItem(destination, OffBody(item)))
                    {
                        _message = T("There is no room to drop the item.", "Não há espaço para descartar o item.");
                        return;
                    }
                    if (!Remove())
                    {
                        if (_area == Area.Tower) ConsumeGroundItem(destination);
                        else if (_townDroppedItems.Remove(destination, out var drop)) RemoveGenerated(drop.view);
                        return;
                    }
                    if (!giving && _area == Area.Tower && destination == _playerCell) _discardedUnderKoh = destination;
                    _message = T("Item dropped.", "Item descartado.");
                    RefreshInventoryCommands();
                });
                return;
            }
            if (command == ItemCommand.Hold)
            {
                if (_carriedView || _heldTownPot) { _message = T("Your hands are full.", "Suas mÃ£os estÃ£o ocupadas."); return; }
                ScheduleInventoryAction(() =>
                {
                    if (!Remove()) return;
                    _carriedItem = OffBody(item);
                    _carriedView = CreateGroundItemView(_playerCell, _carriedItem);
                    if (_area == Area.Town) _inventoryOpen = false;
                    PresentTownInventoryItem();
                    RefreshInventoryCommands();
                });
                return;
            }
            if (_area == Area.Town && item.Kind == MonsterEgg.Kind)
            {
                _message = T("Place the egg in the monster hut to hatch it.",
                    "Coloque o ovo no alojamento para chocá-lo.");
                return;
            }
            var target = giving ? _familiarActor : _playerActor;
            if (giving && item.Kind == ItemKind.Ball && MonsterExhausted(target))
            { _message = T("The familiar is out of MP.", "O familiar está sem PM."); return; }
            if (!CanUseInventoryEffect(item, giving))
            {
                _message = T("That item has no supported effect here.", "Esse item nÃ£o tem um efeito disponÃ­vel aqui.");
                return;
            }
            if (IsWhiteSand(item) || IsTruthGlasses(item) || IsRestoreScroll(item))
            {
                OpenItemTargetPicker(index, null, IsRestoreScroll(item) ? ItemTargetMode.Restore :
                            IsTruthGlasses(item) ? ItemTargetMode.Identify : ItemTargetMode.Recharge);
                return;
            }
            bool usingSand = item.Kind == ItemKind.Sand;
            if (usingSand && SandUpgradeTarget(item) == null) return;
            bool curesImmobilization = !giving && item.Kind == ItemKind.Herb &&
                (item.Subtype == 5 || (item.Subtype == 4 && Combat.HasStatus(_playerActor, StatusId.SLEEP)));
            ScheduleInventoryAction(() =>
            {
                if (giving && (_familiarActor != target || (!_familiar && !CanFeedCarriedFamiliar(item)))) return;
                if (giving && item.Kind == ItemKind.Fruit && item.Subtype == 7 && !CanRevertToEgg(target)) return;
                if (item.Kind == ItemKind.Crystal && item.Subtype == 1 && !TryFindHatchCell(out _)) return;
                var upgradeTarget = usingSand ? SandUpgradeTarget(item) : null;
                if (usingSand && upgradeTarget == null) return;
                if (item.Kind == ItemKind.Ball && item.Param > 1)
                {
                    if (!inventory.ReplaceDungeonItem(index, ItemKind.Ball,
                        new DungeonItemEntry(item.Subtype, item.Kind, (sbyte)(item.Param - 1), item.Flags))) return;
                }
                else if (!Remove()) return;
                if (upgradeTarget != null) TemperEquipment(upgradeTarget);
                else ApplyInventoryEffect(item, target);
                RefreshInventoryCommands();
            }, familiarCasting: giving && item.Kind == ItemKind.Ball, curesImmobilization: curesImmobilization);
        }

        private void ShowLockedEquipmentMessage() => _message = T(
            "Cursed equipment is stuck. Use a De-curse Scroll.",
            "O equipamento amaldiçoado está preso. Use um pergaminho de purificação.");

        private void RestoreFamiliarFromScroll(Inventory inventory, int row, int sourceIndex, Vector2Int? groundCell)
        {
            var reserve = ReserveForRow(row);
            bool current = row == FamiliarInventoryRow;
            var actor = current && _familiarDefinition && _familiarHealth <= 0 ? _familiarActor : reserve?.Actor;
            if (actor == null || (!current && actor.Hp != 0)) return;
            if (groundCell.HasValue)
            {
                if (groundCell.Value != _playerCell || !IsRestoreScroll(_towerItemTraps?.GetItemAt(groundCell.Value))) return;
                ConsumeGroundItem(groundCell.Value);
            }
            else
            {
                var items = inventory.DungeonItems;
                if (sourceIndex < 0 || sourceIndex >= items.Count || !IsRestoreScroll(items[sourceIndex]) ||
                    !inventory.ConsumeDungeonItem(sourceIndex, ItemKind.Scroll)) return;
            }

            actor.Hp = StatByte(Mathf.Max(1, (current ? FamiliarMaxHealth : actor.MaxHp) * 3 / 10));
            string name = reserve?.Name ?? FamiliarDisplayName;
            if (current)
            {
                _familiarHealth = actor.Hp;
                _familiarDefeated = false;
                _familiarInBag = true;
            }
            else reserve.Defeated = false;
            _message = T($"{name} recovered {actor.Hp} HP.", $"{name} recuperou {actor.Hp} PV.");
            RefreshInventoryCommands();
        }

        private void IdentifyInventoryItem(Inventory inventory, int targetIndex, int sourceIndex, Vector2Int? groundCell)
        {
            var items = inventory.DungeonItems;
            if (targetIndex < 0 || targetIndex >= items.Count || !IsUnidentifiedItem(items[targetIndex])) return;
            if (groundCell.HasValue)
            {
                if (groundCell.Value != _playerCell || !IsTruthGlasses(_towerItemTraps?.GetItemAt(groundCell.Value))) return;
            }
            else if (sourceIndex < 0 || sourceIndex >= items.Count || !IsTruthGlasses(items[sourceIndex])) return;

            var item = items[targetIndex];
            var identified = item.Kind == MonsterEgg.Kind ? MonsterEgg.Identify(item) :
                new DungeonItemEntry(item.Subtype, item.Kind, item.Param, (byte)(item.Flags & ~Inventory.UnrevealedEquipmentFlag));
            if (!inventory.ReplaceDungeonItem(targetIndex, item.Kind, identified)) return;
            if (groundCell.HasValue) ConsumeGroundItem(groundCell.Value);
            else inventory.ConsumeDungeonItem(sourceIndex, ItemKind.Glasses);
            string name = InventoryItemName(identified);
            _message = T($"Identified: {name}.", $"Item identificado: {name}.");
            RefreshInventoryCommands();
        }

        private void RechargeBall(Inventory inventory, int ballIndex, int sandIndex, Vector2Int? groundCell)
        {
            var items = inventory.DungeonItems;
            if (ballIndex < 0 || ballIndex >= items.Count || !CanRechargeBall(items[ballIndex])) return;
            if (groundCell.HasValue)
            {
                if (groundCell.Value != _playerCell || !IsWhiteSand(_towerItemTraps?.GetItemAt(groundCell.Value))) return;
            }
            else if (sandIndex < 0 || sandIndex >= items.Count || !IsWhiteSand(items[sandIndex])) return;

            var ball = items[ballIndex];
            ball.Param++;
            if (!inventory.ReplaceDungeonItem(ballIndex, ItemKind.Ball, ball)) return;
            if (groundCell.HasValue) ConsumeGroundItem(groundCell.Value);
            else inventory.ConsumeDungeonItem(sandIndex, ItemKind.Sand);
            _message = $"{InventoryItemName(ball)} — {BallChargeText(ball)}";
            RefreshInventoryCommands();
        }

        private bool TryFindDiscardCell(Vector2Int origin, out Vector2Int destination)
        {
            if (DiscardCellHasRoom(origin))
            {
                destination = origin;
                return true;
            }

            Vector2 facing = _area == Area.Tower ? _towerFacing : _townFacing;
            // Try the four tiles sharing an edge with Koh's tile.
            var front = Mathf.Abs(facing.x) > Mathf.Abs(facing.y)
                ? new Vector2Int(facing.x > 0f ? 1 : -1, 0)
                : new Vector2Int(0, facing.y >= 0f ? 1 : -1);
            var left = new Vector2Int(-front.y, front.x);
            foreach (var step in new[] { front, -front, left, -left })
            {
                Vector2Int cell = origin + step;
                bool reachable = _area == Area.Tower
                    ? CanTowerStep(origin, step) && CanLandObject(cell) &&
                      !_thrownEggs.Exists(ally => ally.View && ally.Turns > 0 && ally.Cell == cell)
                    : TownPositionOpen(CellPosition(cell));
                if (!reachable || !DiscardCellHasRoom(cell)) continue;
                destination = cell;
                return true;
            }

            destination = default;
            return false;
        }

        private bool DiscardCellHasRoom(Vector2Int cell) =>
            CanPlaceInventoryItem(cell) && (_area == Area.Tower
                ? _towerItemTraps.GetItemAt(cell) == null && (!_hasLoot || cell != _lootCell)
                : !_townPickups.Exists(pickup => pickup &&
                    Mathf.RoundToInt(pickup.transform.position.x) == cell.x &&
                    Mathf.RoundToInt(pickup.transform.position.z) == cell.y));

        private bool CanPlaceInventoryItem(Vector2Int cell) => _area == Area.Tower
            ? _towerItemTraps != null && !_itemMarkers.ContainsKey(cell) && cell != _stairCell
            : !_townDroppedItems.ContainsKey(cell);

        private bool PlaceInventoryItem(Vector2Int cell, DungeonItemEntry item)
        {
            if (!CanPlaceInventoryItem(cell)) return false;
            if (_area == Area.Tower)
            {
                if (!_towerItemTraps.TryPlaceItem(cell, item)) return false;
                _itemMarkers[cell] = CreateGroundItemView(cell, item);
            }
            else
            {
                var view = CreateGroundItemView(cell, item);
                if (UsingRetailTown) view.transform.position = RetailTownActorPosition(view.transform.position, WorldItemHeight);
                _townDroppedItems.Add(cell, (item, view));
            }
            return true;
        }

        private bool TryCollectDroppedTownItem()
        {
            if (!_townDroppedItems.TryGetValue(_playerCell, out var drop) ||
                (drop.item.Kind != ItemKind.Coin && !_townInventory.HasSpace())) return false;
            PlayRetailSound(drop.item.Kind == ItemKind.Coin ? RetailSfx.CoinPickup : RetailSfx.Pickup);
            _townDroppedItems.Remove(_playerCell);
            var item = drop.item.Kind == MonsterEgg.Kind ? MonsterEgg.Identify(drop.item) : drop.item;
            if (!CreditCoinPickup(item))
            {
                _townInventory.AddDungeonItem(item);
                ShowItemPickup(item);
            }
            RemoveGenerated(drop.view);
            PersistTownProgress();
            return true;
        }

        private void PresentTownInventoryItem()
        {
            if (_area != Area.Town || _carriedItem == null || !_carriedView || !_player) return;
            PlaceHeldView(_carriedView);
        }

        private void ReleaseTownInventoryItem(bool throwing)
        {
            if (_carriedItem == null) return;
            Vector2Int cell = _playerCell;
            if (throwing)
            {
                var step = new Vector2Int(Mathf.RoundToInt(_townFacing.x), Mathf.RoundToInt(_townFacing.y));
                for (int distance = 1; distance <= 8; distance++)
                {
                    var next = _playerCell + step * distance;
                    if (!TownPositionOpen(CellPosition(next))) break;
                    if (CanPlaceInventoryItem(next)) cell = next;
                }
            }
            if (!PlaceInventoryItem(cell, _carriedItem)) return;
            if (throwing && _carriedView)
                StartCoroutine(AnimateThrownTownItem(_townDroppedItems[cell].view, _carriedView.transform.position));
            RemoveGenerated(_carriedView);
            _carriedView = null;
            _carriedItem = null;
            PersistTownProgress();
        }

        private System.Collections.IEnumerator AnimateThrownTownItem(GameObject view, Vector3 from)
        {
            Vector3 destination = view.transform.position;
            float duration = ThrowFlightDuration(from, destination);
            float elapsed = 0f;
            view.transform.position = from;
            while (view && elapsed < duration)
            {
                yield return null;
                if (!view) yield break;
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                view.transform.position = Vector3.Lerp(from, destination, progress)
                    + Vector3.up * (Mathf.Sin(progress * Mathf.PI) * 0.9f);
            }
            if (view) view.transform.position = destination;
        }
    }
}
