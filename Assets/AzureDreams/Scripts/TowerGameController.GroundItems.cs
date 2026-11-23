using AzureDreams.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private enum GroundItemAction { Use, PutIntoBag, Lift, Give, Rearm }

        private Vector2Int? _groundItemPrompt;
        private bool _groundItemActionsOpen, _inspectItemOnArrival;
        private GroundItemAction _groundItemSelection;
        private GameObject _pickupView;
        private Vector3 _pickupOrigin;
        private float _pickupElapsed;
        private const float PickupDuration = 0.38f;
        private bool CanRearmUnderKoh => _area == Area.Tower && TrapAt(_playerCell) is { IsRevealed: true, IsActive: false };
        private DungeonItemEntry ItemUnderKoh(Vector2Int cell) => _area == Area.Tower
            ? _towerItemTraps?.GetItemAt(cell)
            : _townDroppedItems.TryGetValue(cell, out var drop) ? drop.item : null;

        private void CheckGroundItemOnArrival()
        {
            if (_discardedUnderKoh != _playerCell) _discardedUnderKoh = null;
            if (!EnteredTowerCell(_playerFrom, _playerCell) || _discardedUnderKoh == _playerCell) return;
            var item = _towerItemTraps?.GetItemAt(_playerCell);
            if (item == null) return;
            if (IsBlueCollar(item)) PutGroundItemInBag(_playerCell);
            else if (_inspectItemOnArrival || _interact?.IsPressed() == true) TryInspectGroundItem();
            else PutGroundItemInBag(_playerCell);
        }

        private bool TryInspectGroundItem()
        {
            if (_area != Area.Tower || ItemImmobilized(_playerActor) || _towerItemTraps?.GetItemAt(_playerCell) == null) return false;
            if (IsBlueCollar(_towerItemTraps.GetItemAt(_playerCell)))
            {
                PutGroundItemInBag(_playerCell);
                return true;
            }
            _groundItemPrompt = _playerCell;
            _groundItemActionsOpen = false;
            _groundItemSelection = GroundItemAction.PutIntoBag;
            return true;
        }

        private bool HandleGroundItemPrompt()
        {
            if (!_groundItemPrompt.HasValue) return false;
            if (_groundItemPrompt.Value != _playerCell || ItemUnderKoh(_playerCell) == null)
            {
                _groundItemPrompt = null;
                return false;
            }
            if (_cancel?.WasPressedThisFrame() == true)
            {
                _groundItemPrompt = null;
                return true;
            }
            var keyboard = Keyboard.current;
            if (_groundItemActionsOpen && keyboard != null)
            {
                int delta = MenuDirectionPressed(1) ? -1 : MenuDirectionPressed(2) ? 1 : 0;
                _groundItemSelection = (GroundItemAction)Mathf.Clamp((int)_groundItemSelection + delta, 0, CanRearmUnderKoh ? 4 : 3);
            }
            if (_interact?.WasPressedThisFrame() == true)
            {
                if (_groundItemActionsOpen) ExecuteGroundItemAction(_groundItemSelection);
                else _groundItemActionsOpen = true;
            }
            return true;
        }

        private void ExecuteGroundItemAction(GroundItemAction action)
        {
            if (!_groundItemPrompt.HasValue || _groundItemPrompt.Value != _playerCell || _turnBusy) return;
            var cell = _groundItemPrompt.Value;
            _groundItemPrompt = null;
            if (action == GroundItemAction.Give)
            {
                GiveGroundItem(cell);
                return;
            }
            if (_area == Area.Town)
            {
                ExecuteTownGroundAction(cell, action);
                return;
            }
            var item = _towerItemTraps?.GetItemAt(cell);
            if (item == null) return;
            switch (action)
            {
                case GroundItemAction.Rearm:
                    if (CanRearmUnderKoh) _trapRearmCell = cell;
                    break;
                case GroundItemAction.PutIntoBag:
                    if (CheckGroundItemBagSpace(item)) ScheduleInventoryAction(() => PutGroundItemInBag(cell));
                    break;
                case GroundItemAction.Use:
                    if (item.Kind == MonsterEgg.Kind && _carriedView)
                        _message = T("Put down what you are holding before warming an egg.", "Coloque no chão o que está segurando antes de aquecer um ovo.");
                    else if (item.Kind == MonsterEgg.Kind)
                        BeginEggWarming(-1, cell);
                    else if (IsPitaFruit(item))
                        _message = T("Koh cannot eat Pita Fruit.", "Koh não pode comer fruta Pita.");
                    else if (item.Kind != MonsterEgg.Kind && !CanUseInventoryEffect(item, false))
                        _message = T("That item has no supported effect here.", "Esse item não tem um efeito disponível aqui.");
                    else if (IsWhiteSand(item) || IsTruthGlasses(item) || IsRestoreScroll(item))
                        OpenItemTargetPicker(-1, cell, IsRestoreScroll(item) ? ItemTargetMode.Restore :
                            IsTruthGlasses(item) ? ItemTargetMode.Identify : ItemTargetMode.Recharge);
                    else if (item.Kind != ItemKind.Sand || SandUpgradeTarget(item) != null)
                        ScheduleInventoryAction(() => UseGroundItem(cell));
                    break;
                case GroundItemAction.Lift:
                    if (_carriedView) _message = T("Your hands are full.", "Suas mãos estão ocupadas.");
                    else if (TakeFloorObject(cell)) PresentCarry(0f);
                    break;
            }
        }

        private void GiveGroundItem(Vector2Int cell)
        {
            var item = ItemUnderKoh(cell);
            if (!IsGiveableToFamiliar(item))
            {
                _message = T("That item cannot be given to a familiar.", "Esse item não pode ser dado a um familiar.");
                return;
            }
            var area = _area;
            var source = new Inventory();
            source.AddDungeonItem(new DungeonItemEntry(item.Subtype, item.Kind, item.Param, item.Flags));
            bool Available() => _area == area && _playerCell == cell && ReferenceEquals(ItemUnderKoh(cell), item);
            void Commit()
            {
                if (!Available()) return;
                if (area == Area.Tower) ConsumeGroundItem(cell);
                else if (RemoveTownGroundItem(cell, out var drop)) RemoveGenerated(drop.view);
                // A charged ball stays on the ground; a Troll's previous weapon replaces the gift.
                foreach (var remaining in source.DungeonItems) PlaceInventoryItem(cell, remaining);
                if (area == Area.Town) PersistTownProgress();
            }
            OpenGiveRecipientPicker(item, actor =>
            {
                if (!Available()) return;
                GiveItemToRecipient(source, 0, source.DungeonItems[0], actor,
                    () => Available() && source.ConsumeDungeonItem(0, item.Kind), Commit, Available);
            });
        }
        private void ExecuteTownGroundAction(Vector2Int cell, GroundItemAction action)
        {
            if (!_townDroppedItems.TryGetValue(cell, out var drop)) return;
            if (action == GroundItemAction.PutIntoBag)
            {
                if (!TryCollectDroppedTownItem()) _message = T("Too many belongings in the bag", "Há itens demais na bolsa");
                return;
            }
            if (action == GroundItemAction.Lift)
            {
                if (_carriedView || _heldTownPot)
                {
                    _message = T("Your hands are full.", "Suas mãos estão ocupadas.");
                    return;
                }
                RemoveTownGroundItem(cell, out _);
                _carriedItem = OffBody(drop.item);
                _carriedView = drop.view;
                if (_carriedView.TryGetComponent<BoxCollider>(out var body)) body.enabled = false;
                PresentTownInventoryItem();
            }
            else if (action == GroundItemAction.Use)
            {
                if (drop.item.Kind == MonsterEgg.Kind)
                    _message = T("Place the egg in the monster hut to hatch it.", "Coloque o ovo no alojamento para chocá-lo.");
                else if (CanUseInventoryEffect(drop.item, false) && drop.item.Kind != ItemKind.Sand)
                {
                    RemoveTownGroundItem(cell, out _);
                    RemoveGenerated(drop.view);
                    ApplyInventoryEffect(drop.item, _playerActor);
                    PersistTownProgress();
                }
                else _message = T("That item has no supported effect here.", "Esse item não tem um efeito disponível aqui.");
            }
        }

        private bool CheckGroundItemBagSpace(DungeonItemEntry item)
        {
            if (IsBlueCollar(item) || item.Kind == ItemKind.Coin || _runInventory == null || _runInventory.HasSpace()) return true;
            _message = T($"{InventoryItemName(item)}, but there is no room in the inventory", $"{InventoryItemName(item)}, mas não há espaço no inventário");
            return false;
        }

        private void PutGroundItemInBag(Vector2Int cell)
        {
            var item = _towerItemTraps?.GetItemAt(cell);
            if (item == null || !CheckGroundItemBagSpace(item)) return;
            item = _towerItemTraps.TryPickup(cell);
            if (item == null) return;
            PlayRetailSound(item.Kind == ItemKind.Coin ? RetailSfx.CoinPickup : RetailSfx.Pickup);
            if (IsBlueCollar(item))
            {
                TownSaveVariables.SetBool(BlueCollarKey, true);
                TownSaveVariables.Save();
                _message = T("Blue Collar obtained. You can now summon two familiars.",
                    "Colar Azul obtido. Agora você pode invocar dois familiares.");
            }
            else if (!CreditCoinPickup(item))
            {
                _runInventory ??= new Inventory();
                _runInventory.AddDungeonItem(item);
                var name = InventoryItemName(item);
                _message = T($"{name} obtained", $"{name} — item obtido");
            }
            if (!_itemMarkers.Remove(cell, out _pickupView) || !_pickupView)
                _pickupView = CreateGroundItemView(cell, item);
            _pickupOrigin = _pickupView.transform.position;
            _pickupElapsed = 0f;
        }

        private static int CoinPickupValue(DungeonItemEntry item)
        {
            int multiplier = item.Flags & 0x1F;
            // Retail stores the value in Param, optionally scaled by the low five flag bits.
            if (item.Param > 0) return item.Param * (multiplier == 0 ? 1 : multiplier * 10);
            // The debug picker and older converted coins may have no stored amount.
            return item.Subtype switch
            {
                ItemSystem.Id.CopperCoin => 20,
                ItemSystem.Id.SilverCoin => 100,
                ItemSystem.Id.GoldCoin => 200,
                _ => 0
            };
        }

        private bool CreditCoinPickup(DungeonItemEntry item)
        {
            if (item.Kind != ItemKind.Coin) return false;
            int amount = BlessedCoinValue(CoinPickupValue(item));
            if (_area == Area.Tower)
            {
                _towerRun.AddLoot(amount);
                _runLoot = _towerRun.RunLoot;
            }
            else
            {
                amount = System.Math.Min(amount, int.MaxValue - _gold);
                _gold += amount;
            }
            _message = T($"{amount} G obtained", $"{amount} G obtidos");
            return true;
        }

        private void UseGroundItem(Vector2Int cell)
        {
            var item = _towerItemTraps?.GetItemAt(cell);
            if (item == null) return;
            if (item.Kind == MonsterEgg.Kind)
            {
                var warmed = MonsterEgg.Warm(item, 1 + RollSpawnRange(4));
                item.Param = warmed.Param;
                item.Flags = warmed.Flags;
                if (_itemMarkers.TryGetValue(cell, out var view) && view && view.TryGetComponent<MonsterEgg>(out var egg))
                    egg.Bind(item);
                HatchWarmedEgg(item, () => ConsumeGroundItem(cell));
                return;
            }
            if (!CanUseInventoryEffect(item, false) || IsWhiteSand(item) || IsTruthGlasses(item) || IsRestoreScroll(item)) return;
            if (item.Kind == ItemKind.Sand)
            {
                var equipment = SandUpgradeTarget(item);
                if (equipment == null) return;
                ConsumeGroundItem(cell);
                TemperEquipment(equipment);
                return;
            }
            if (item.Kind == ItemKind.Ball && item.Param > 1)
            {
                var cast = new DungeonItemEntry(item.Subtype, item.Kind, item.Param, item.Flags);
                item.Param--;
                ApplyInventoryEffect(cast, _playerActor);
            }
            else
            {
                ConsumeGroundItem(cell);
                ApplyInventoryEffect(item, _playerActor);
            }
        }

        private void ConsumeGroundItem(Vector2Int cell)
        {
            _towerItemTraps.TakeItem(cell);
            ConsumeFloorMarker(_itemMarkers, cell);
        }

        private void PresentGroundPickup()
        {
            if (!_pickupView) return;
            _pickupElapsed += TowerAnimationDelta;
            if (_pickupElapsed >= PickupDuration)
            {
                RemoveGenerated(_pickupView);
                _pickupView = null;
                return;
            }
            PlaceHeldView(_pickupView);
            float progress = Mathf.Clamp01(_pickupElapsed / 0.17f);
            _pickupView.transform.position = Vector3.Lerp(_pickupOrigin, _pickupView.transform.position,
                Mathf.SmoothStep(0f, 1f, progress));
        }

        private void DrawGroundItemPrompt(float width, float height)
        {
            if (!_groundItemPrompt.HasValue) return;
            var item = ItemUnderKoh(_groundItemPrompt.Value);
            if (item == null) return;
            var panel = new Rect(width * 0.5f - 145f, height * 0.5f - 60f, 290f,
                _groundItemActionsOpen ? (CanRearmUnderKoh ? 204f : 172f) : 62f);
            DrawDialogueFrame(panel);
            var title = DrawEquipmentCurseIcon(new Rect(panel.x + 10f, panel.y + 6f, panel.width - 20f, 23f), item);
            DrawHudText(title, InventoryItemName(item), InventoryNameStyle(item));
            if (!_groundItemActionsOpen)
            {
                if (GUI.Button(new Rect(panel.x + 10f, panel.y + 31f, panel.width - 20f, 24f), WithInteractHint("Actions", "Ações")))
                    _groundItemActionsOpen = true;
                return;
            }
            DrawGroundItemAction(panel, GroundItemAction.Use, _area == Area.Tower && item.Kind == MonsterEgg.Kind ? T("Warm", "Aquecer") : T("Use", "Usar"));
            DrawGroundItemAction(panel, GroundItemAction.PutIntoBag, item.Kind == ItemKind.Coin
                ? T("Collect gold", "Recolher ouro") : T("Pick up", "Pegar"));
            DrawGroundItemAction(panel, GroundItemAction.Lift, T("Lift", "Levantar"));
            DrawGroundItemAction(panel, GroundItemAction.Give, T("Give to familiar", "Dar para o familiar"));
            if (CanRearmUnderKoh) DrawGroundItemAction(panel, GroundItemAction.Rearm, T("Rearm", "Reativar"));
        }

        private void DrawGroundItemAction(Rect panel, GroundItemAction action, string label)
        {
            if (!_groundItemPrompt.HasValue) return;
            if (GUI.Button(new Rect(panel.x + 10f, panel.y + 32f + (int)action * 32f, panel.width - 20f, 28f),
                (_groundItemSelection == action ? "> " : "") + label))
                ExecuteGroundItemAction(action);
        }
    }
}
