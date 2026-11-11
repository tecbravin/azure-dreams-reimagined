using System.Collections.Generic;
using AzureDreams.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        [SerializeField] private string _inventoryBinding = "<Keyboard>/tab";
        [SerializeField] private Texture2D _kohStatusFaces;
        [SerializeField] private Texture2D _kewneMenuPortrait;
        [SerializeField] private Texture2D _hatchedEggInventoryIcon;
        [SerializeField] private Texture2D _kewneStatusSheet;
        private InputAction _inventoryAction;
        private bool _inventoryOpen, _familiarStatus;
        private IReadOnlyList<DungeonItemEntry> _inventoryEntries;
        private const int InventoryRowsPerPage = 5;
        private const int InventoryPageCount = Inventory.SlotCapacity / InventoryRowsPerPage;
        private int _inventoryPage;
        private int _inventoryOffset;
        private Vector2 _inventoryFamiliarScroll;
        private int OccupiedInventoryPageCount => Mathf.Clamp(
            (VisibleItemCount - FamiliarInventoryCount + InventoryRowsPerPage - 1) / InventoryRowsPerPage, 0, InventoryPageCount);
        private int FirstItemRowOnPage => FamiliarInventoryCount + _inventoryPage * InventoryRowsPerPage;
        private GUIStyle _unrevealedGearStyle;
        private (Inventory Inventory, int SourceIndex, Vector2Int? GroundCell, ItemTargetMode Mode)? _itemTargetSource;
        private enum ItemTargetMode { Recharge, Identify, Restore }
        private readonly List<(int Index, DungeonItemEntry Item)> _itemTargets = new();
        private int _itemTargetSelection;
        private Vector2 _itemTargetScroll;
        private const float ItemTargetRowHeight = 28f;
        private float ItemTargetListHeight => Mathf.Min(6, _itemTargets.Count) * ItemTargetRowHeight;

        private void OpenItemTargetPicker(int sourceIndex, Vector2Int? groundCell, ItemTargetMode mode)
        {
            var inventory = CarriedInventory;
            var items = inventory?.DungeonItems;
            _itemTargets.Clear();
            if (mode == ItemTargetMode.Restore)
            {
                if (_familiarDefinition && _familiarHealth <= 0) _itemTargets.Add((FamiliarInventoryRow, null));
                for (int i = 0; i < _bagFamiliars.Count; i++)
                    if (_bagFamiliars[i].Actor.Hp == 0) _itemTargets.Add((FamiliarInventoryRow - i - 1, null));
            }
            else
            {
                for (int i = 0; i < (items?.Count ?? 0); i++)
                    if (mode == ItemTargetMode.Identify ? IsUnidentifiedItem(items[i]) : CanRechargeBall(items[i]))
                        _itemTargets.Add((i, items[i]));
            }
            if (_itemTargets.Count == 0)
            {
                _message = mode switch
                {
                    ItemTargetMode.Restore => T("No familiar has 0 HP.", "Nenhum familiar está com 0 PV."),
                    ItemTargetMode.Identify => T("There are no unidentified items in the bag.", "Não há itens não identificados na bolsa."),
                    _ => T("There are no magic balls with 8 or fewer charges in the bag.",
                        "Não há bolas de cristal com 8 cargas ou menos na bolsa.")
                };
                return;
            }
            _itemTargetSource = (inventory, sourceIndex, groundCell, mode);
            _itemTargetSelection = 0;
            _itemTargetScroll = Vector2.zero;
        }

        private bool HandleItemTargetPicker()
        {
            if (!_itemTargetSource.HasValue) return false;
            if (_cancel?.WasPressedThisFrame() == true || _inventoryAction?.WasPressedThisFrame() == true)
            {
                _itemTargetSource = null;
                return true;
            }
            var keyboard = Keyboard.current;
            int delta = keyboard?.upArrowKey.wasPressedThisFrame == true ? -1 :
                keyboard?.downArrowKey.wasPressedThisFrame == true ? 1 : 0;
            if (delta != 0)
            {
                _itemTargetSelection = Mathf.Clamp(_itemTargetSelection + delta, 0, _itemTargets.Count - 1);
                float top = _itemTargetSelection * ItemTargetRowHeight;
                _itemTargetScroll.y = Mathf.Clamp(_itemTargetScroll.y, top + ItemTargetRowHeight - ItemTargetListHeight, top);
            }
            if (_interact?.WasPressedThisFrame() == true) ConfirmItemTarget(_itemTargetSelection);
            return true;
        }

        private void ConfirmItemTarget(int selection)
        {
            if (!_itemTargetSource.HasValue) return;
            var (inventory, sourceIndex, groundCell, mode) = _itemTargetSource.Value;
            int targetIndex = _itemTargets[selection].Index;
            if (ScheduleInventoryAction(() =>
            {
                if (mode == ItemTargetMode.Restore) RestoreFamiliarFromScroll(inventory, targetIndex, sourceIndex, groundCell);
                else if (mode == ItemTargetMode.Identify) IdentifyInventoryItem(inventory, targetIndex, sourceIndex, groundCell);
                else RechargeBall(inventory, targetIndex, sourceIndex, groundCell);
            }))
                _itemTargetSource = null;
        }

        private void DrawItemTargetPicker(float width, float height)
        {
            float panelWidth = Mathf.Min(360f, width - 24f);
            float panelHeight = ItemTargetListHeight + 68f;
            var panel = new Rect((width - panelWidth) * 0.5f, (height - panelHeight) * 0.5f, panelWidth, panelHeight);
            DrawDialogueFrame(panel);
            var mode = _itemTargetSource.Value.Mode;
            string title = mode switch
            {
                ItemTargetMode.Restore => T("Choose a familiar to restore", "Escolha um familiar para restaurar"),
                ItemTargetMode.Identify => T("Choose an item to identify", "Escolha um item para identificar"),
                _ => T("Choose a magic ball", "Escolha uma bola de cristal")
            };
            DrawHudText(new Rect(panel.x + 10f, panel.y + 6f, panel.width - 20f, 24f), title, _bodyStyle);
            var viewport = new Rect(panel.x + 10f, panel.y + 31f, panel.width - 20f, ItemTargetListHeight);
            float rowWidth = viewport.width - 17f;
            _itemTargetScroll = GUI.BeginScrollView(viewport, _itemTargetScroll,
                new Rect(0f, 0f, rowWidth, _itemTargets.Count * ItemTargetRowHeight));
            int clicked = -1;
            for (int i = 0; i < _itemTargets.Count; i++)
            {
                var item = _itemTargets[i].Item;
                var row = new Rect(0f, i * ItemTargetRowHeight, rowWidth, ItemTargetRowHeight - 2f);
                bool focused = i == _itemTargetSelection;
                if (focused) DrawInventoryFocusBand(row);
                string label = mode == ItemTargetMode.Restore
                    ? ReserveForRow(_itemTargets[i].Index)?.Name ?? FamiliarDisplayName
                    : InventoryItemName(item);
                if (mode == ItemTargetMode.Recharge) label += "  " + BallChargeText(item);
                if (Event.current.type == EventType.Repaint)
                {
                    var icon = new Rect(row.x + 4f, row.center.y - 10f, 20f, 20f);
                    if (mode != ItemTargetMode.Restore) DrawInventoryItemIcon(icon, item);
                    else if (_hatchedEggInventoryIcon) GUI.DrawTexture(icon, _hatchedEggInventoryIcon, ScaleMode.ScaleToFit);
                }
                if (focused) DrawInventoryFocusMarker(row);
                var text = row;
                text.xMin += 28f;
                DrawHudText(text, label, InventoryNameStyle(item));
                if (GUI.Button(row, GUIContent.none, GUIStyle.none)) clicked = i;
            }
            GUI.EndScrollView();
            if (GUI.Button(new Rect(viewport.x, viewport.yMax + 5f, viewport.width, 25f), T("Cancel", "Cancelar")))
                _itemTargetSource = null;
            else if (clicked >= 0) ConfirmItemTarget(clicked);
        }

        private void ClampInventorySelection(int offset)
        {
            _inventoryPage = Mathf.Clamp(_inventoryPage, 0, Mathf.Max(0, OccupiedInventoryPageCount - 1));
            int first = _familiarStatus ? 0 : FirstItemRowOnPage;
            int end = _familiarStatus ? FamiliarInventoryCount :
                Mathf.Min(VisibleItemCount, first + InventoryRowsPerPage);
            _inventoryOffset = first < end ? Mathf.Clamp(offset, first, end - 1) : -1;
        }

        private void SelectInventoryPage(int page)
        {
            int previousPage = _inventoryPage;
            _familiarStatus = false;
            _inventoryPage = Mathf.Clamp(page, 0, Mathf.Max(0, OccupiedInventoryPageCount - 1));
            ClampInventorySelection(FirstItemRowOnPage);
            if (_inventoryPage != previousPage) PlayRetailSound(RetailSfx.Cursor);
        }

        private void ToggleInventoryFamiliars()
        {
            if (_familiarStatus) SelectInventoryPage(_inventoryPage);
            else if (FamiliarInventoryCount > 0)
            {
                _familiarStatus = true;
                ClampInventorySelection(0);
            }
        }

        private bool HandleTowerInventory()
        {
            if (_area == Area.Town && (_activeTownNpc || _townObjectDialogueOpen ||
                (_townFlow && _townFlow.State.Current != TownGameState.State.TownIdle))) return false;
            var keyboard = Keyboard.current;
            if (_inventoryOpen && HandleFamiliarInventoryPrompt()) return true;
            bool switchingWindows = keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
            if (HandleInventoryRoot(keyboard, switchingWindows)) return true;
            if (!switchingWindows && _inventoryAction != null && _inventoryAction.WasPressedThisFrame())
            {
                PlayRetailSound(RetailSfx.Cancel);
                _inventoryOpen = false;
                _itemCommandIndex = -1;
                return true;
            }
            if (!_inventoryOpen) return false;
            if (_cancel != null && _cancel.WasPressedThisFrame())
            {
                PlayRetailSound(RetailSfx.Cancel);
                if (_familiarOrderPicker != FamiliarOrderPicker.None)
                {
                    _fusionPreview = null;
                    _familiarOrderPicker = FamiliarOrderPicker.None;
                }
                else if (_itemCommandIndex >= 0) _itemCommandIndex = -1;
                else
                {
                    _inventoryOpen = false;
                    if (_area == Area.Tower) OpenInventoryRoot();
                }
                return true;
            }
            if (keyboard == null) return true;
            if (_inventoryActorSlot == 0 && _itemCommandIndex < 0 && !_familiarStatus && _liftAction?.WasPressedThisFrame() == true)
            { SortInventory(); return true; }
            bool confirm = _interact != null && _interact.WasPressedThisFrame();
            int delta = keyboard.upArrowKey.wasPressedThisFrame ? -1 : keyboard.downArrowKey.wasPressedThisFrame ? 1 : 0;
            if (_itemCommandIndex >= 0)
            {
                int previousCommand = _itemCommandIndex;
                _itemCommandIndex = Mathf.Clamp(_itemCommandIndex + delta, 0, _availableItemCommands.Count - 1);
                if (_itemCommandIndex != previousCommand) PlayRetailSound(RetailSfx.Cursor);
                if (confirm) ExecuteItemCommand(_availableItemCommands[_itemCommandIndex]);
                return true;
            }
            int horizontal = keyboard.leftArrowKey.wasPressedThisFrame ? -1 :
                keyboard.rightArrowKey.wasPressedThisFrame ? 1 : 0;
            if (_inventoryActorSlot > FamiliarInventoryCount)
            {
                _inventoryActorSlot = 0;
                _familiarOrderPicker = FamiliarOrderPicker.None;
            }
            var collarActor = InventoryCollarActor(InventoryActorCollar(_inventoryActorSlot));
            if (collarActor != null && _familiarOrderPicker != FamiliarOrderPicker.None)
            {
                HandleFamiliarOrders(collarActor, horizontal, delta, confirm);
                return true;
            }
            if (!confirm && keyboard.fKey.wasPressedThisFrame && FamiliarInventoryCount > 0)
            {
                _inventoryActorSlot = 0;
                ToggleInventoryFamiliars();
                return true;
            }
            int characterDelta = InventoryCharacterDelta(keyboard);
            if (characterDelta != 0)
            {
                CycleInventoryActor(characterDelta);
                return true;
            }
            if (collarActor != null)
            {
                HandleFamiliarOrders(collarActor, 0, delta, confirm);
                return true;
            }
            if (horizontal != 0 && !_familiarStatus)
            {
                int next = _inventoryPage + horizontal;
                if (next >= 0 && next < OccupiedInventoryPageCount) SelectInventoryPage(next);
                return true;
            }
            if (delta != 0)
            {
                int previousOffset = _inventoryOffset;
                ClampInventorySelection(_inventoryOffset + delta);
                if (_inventoryOffset != previousOffset) PlayRetailSound(RetailSfx.Cursor);
            }
            if (confirm) OpenItemCommands();
            return true;
        }

        private static int InventoryCharacterDelta(Keyboard keyboard) =>
            keyboard?.pageUpKey.wasPressedThisFrame == true ? -1 :
            keyboard?.pageDownKey.wasPressedThisFrame == true ? 1 : 0;

        private void DrawTowerInventory(float width, float height)
        {
            if (_itemTargetSource.HasValue)
            {
                DrawItemTargetPicker(width, height);
                return;
            }
            if (DrawInventoryRoot(width, height)) return;
            if (!_inventoryOpen) return;
            if (_familiarPrompt.HasValue)
            {
                DrawFamiliarInventoryPrompt(width, height);
                return;
            }
            float panelWidth = Mathf.Min(228f, (width - 44f) * 0.5f);
            var items = new Rect(width * 0.5f - panelWidth - 5f, height * 0.25f, panelWidth, 207f);
            var stats = new Rect(items.xMax + 10f, items.y, panelWidth, items.height);
            DrawDialogueFrame(items);
            DrawDialogueFrame(stats);
            var collarActor = InventoryCollarActor(InventoryActorCollar(_inventoryActorSlot));
            if (collarActor != null)
            {
                DrawInventoryCollarOrders(items, stats, collarActor);
                return;
            }
            int count = VisibleItemCount;
            int itemCount = count - FamiliarInventoryCount;
            DrawHudText(new Rect(items.x + 10f, items.y + 6f, panelWidth - 20f, 22f),
                _familiarStatus ? T("Familiars", "Familiares") :
                    T("Items", "Itens") + $" {itemCount}/{Inventory.SlotCapacity}", _bodyStyle);
            if (!_familiarStatus && _itemCommandIndex < 0 && itemCount > 1 && GUI.Button(new Rect(items.xMax - 88, items.y + 6, 78, 24),
                T($"Sort [{_liftAction?.GetBindingDisplayString()}]",
                    $"Ordenar [{_liftAction?.GetBindingDisplayString()}]"))) SortInventory();
            DrawInventoryTabs(items);
            int first = _familiarStatus ? Mathf.Max(0, _inventoryOffset) / InventoryRowsPerPage * InventoryRowsPerPage :
                FirstItemRowOnPage;
            int end = _familiarStatus ? FamiliarInventoryCount : count;
            for (int i = first; i < first + InventoryRowsPerPage; i++)
            {
                var row = new Rect(items.x + 10f, items.y + 61f + (i - first) * 24f, panelWidth - 20f, 23f);
                if (i >= end)
                {
                    if (!_familiarStatus)
                    {
                        DrawHudFill(row, new Color(0.08f, 0.12f, 0.19f, 0.30f));
                        row.xMin += 27f;
                        DrawHudText(row, "-", _messageStyle);
                    }
                    continue;
                }
                var item = ItemAtInventoryRow(i);
                bool familiarRow = _inventoryOrder[i] <= FamiliarInventoryRow;
                var reserve = ReserveForRow(_inventoryOrder[i]);
                string label = familiarRow
                    ? T("Familiar: ", "Familiar: ") + (reserve?.Name ?? FamiliarDisplayName)
                    : item == null ? T("Medicinal Herb", "Erva medicinal") : InventoryItemName(item);
                if (item != null && Equipped(item)) label += " [E]";
                if (item?.Kind == ItemKind.Ball) label += $" ({Mathf.Max(0, (int)item.Param)})";
                var hitArea = row;
                bool focused = i == _inventoryOffset;
                if (focused) DrawInventoryFocusBand(hitArea);
                bool familiarInBag = familiarRow && (_area == Area.Town ||
                    (reserve != null ? reserve != _blueCollarFamiliar : !_familiar && !_carriedFamiliar));
                if (!familiarRow || familiarInBag)
                {
                    if (Event.current.type == EventType.Repaint)
                    {
                        var iconRect = new Rect(row.x, row.y + 1f, 20f, 20f);
                        if (familiarInBag)
                        {
                            if (_hatchedEggInventoryIcon)
                                GUI.DrawTexture(iconRect, _hatchedEggInventoryIcon, ScaleMode.ScaleToFit);
                        }
                        else DrawInventoryItemIcon(iconRect, item);
                    }
                    row.xMin += 24f;
                }
                row = DrawEquipmentCurseIcon(row, item);
                var labelStyle = InventoryNameStyle(item);
                if (focused) DrawInventoryFocusMarker(hitArea);
                row.xMin = Mathf.Max(row.xMin, hitArea.x + 27f);
                if (familiarRow) DrawFamiliarInventoryName(row, label, labelStyle, reserve);
                else DrawHudText(row, label, labelStyle);
                if (_itemCommandIndex < 0 && GUI.Button(hitArea, GUIContent.none, GUIStyle.none))
                { _inventoryOffset = i; OpenItemCommands(); }
            }
            float descriptionY = items.yMax + 8f;
            if (!_familiarStatus && FamiliarInventoryCount > 0)
                descriptionY = DrawInventoryFamiliarStrip(items);
            if (_inventoryOffset >= 0)
            {
                var description = new Rect(items.x, descriptionY, panelWidth * 2f + 10f, 48f);
                DrawDialogueFrame(description);
                var selected = SelectedInventoryItem(out _);
                if (SelectedRawIndex <= FamiliarInventoryRow)
                    DrawHudText(new Rect(description.x + 10f, description.y + 5f, description.width - 20f, 38f),
                        T("E: Familiar actions", "E: Ações do familiar"), _messageStyle);
                else if (selected != null)
                    DrawHudText(new Rect(description.x + 10f, description.y + 5f, description.width - 20f, 38f),
                        ItemDescription(selected) + (selected.Kind == ItemKind.Ball ?
                            "  " + BallChargeText(selected) + T(" · Spell Lv. ", " · Nív. da magia ") +
                            BallSpellLevel(selected.Param) : ""), _messageStyle);
            }

            int actorRow = _familiarStatus ? SelectedRawIndex : InventoryActorRow(_inventoryActorSlot);
            bool viewingFamiliar = actorRow <= FamiliarInventoryRow;
            var actor = DrawInventoryActorStatus(stats, actorRow);
            if (FamiliarInventoryCount > 0 && _itemCommandIndex < 0 &&
                GUI.Button(new Rect(stats.x + 10f, viewingFamiliar ? (_familiarStatus ? descriptionY + 52f : stats.yMax + 8f) :
                    stats.yMax - 51f, stats.width - 20f, 21f),
                    _familiarStatus ? T("Items [F]", "Itens [F]") : T("Familiars [F]", "Familiares [F]")))
                ToggleInventoryFamiliars();
            if (!viewingFamiliar) DrawHudText(new Rect(stats.x + 10f, stats.yMax - 29f, panelWidth - 20f, 27f),
                T("Left/Right: tabs\nPgUp/PgDn: character", "Esq./Dir.: abas\nPgUp/PgDn: personagem"), _messageStyle);
            if (_familiarStatus && actor != null && _itemCommandIndex < 0)
            {
                var talents = new Rect(stats.x, stats.yMax + 88f, stats.width, 67f);
                DrawDialogueFrame(talents);
                DrawHudText(new Rect(talents.x + 8f, talents.y + 5f, talents.width - 16f, talents.height - 10f),
                    T("Talents: ", "Talentos: ") + DescribeTalents(actor), _messageStyle);
            }
            DrawItemCommands(width, height);
        }

        private ActorRecord DrawInventoryActorStatus(Rect panel, int actorRow)
        {
            bool familiar = actorRow <= FamiliarInventoryRow;
            var reserve = ReserveForRow(actorRow);
            var actor = familiar ? reserve?.Actor ?? _familiarActor : _playerActor;
            string name = familiar ? reserve?.Name ?? FamiliarDisplayName : "Koh";
            DrawActorStatus(panel, name, actor, familiar,
                reserve?.Actor.Hp ?? (familiar ? _familiarHealth : _health),
                reserve?.Actor.MaxHp ?? (familiar ? FamiliarMaxHealth : KohMaxHealth),
                reserve?.Actor.CurrentAttack ?? (familiar ? FamiliarWeaponDamage() : _playerActor.CurrentAttack),
                actor?.CurrentDefense ?? 0);
            return actor;
        }

        private float DrawInventoryFamiliarStrip(Rect items)
        {
            const float rowHeight = 22f;
            int count = FamiliarInventoryCount;
            var panel = new Rect(items.x, items.yMax + 6f, items.width, 28f + Mathf.Min(count, 3) * rowHeight);
            DrawDialogueFrame(panel);
            DrawHudText(new Rect(panel.x + 9f, panel.y + 3f, panel.width - 18f, 20f), T("Familiars", "Familiares"), _bodyStyle);
            var list = new Rect(panel.x + 9f, panel.y + 22f, panel.width - 18f, panel.height - 25f);
            float rowWidth = list.width - (count > 3 ? 16f : 0f);
            _inventoryFamiliarScroll = GUI.BeginScrollView(list, _inventoryFamiliarScroll,
                new Rect(0f, 0f, rowWidth, count * rowHeight));
            int clicked = -1;
            for (int i = 0; i < count; i++)
            {
                int raw = _inventoryOrder[i];
                var reserve = ReserveForRow(raw);
                var row = new Rect(0f, i * rowHeight, rowWidth, rowHeight);
                if (i == _inventoryOffset) DrawInventoryFocusBand(row);
                if (Event.current.type == EventType.Repaint && _hatchedEggInventoryIcon)
                    GUI.DrawTexture(new Rect(2f, row.y + 1f, 20f, 20f), _hatchedEggInventoryIcon, ScaleMode.ScaleToFit);
                var label = row;
                label.xMin += 26f;
                DrawFamiliarInventoryName(label, reserve?.Name ?? FamiliarDisplayName, _bodyStyle, reserve);
                if (_itemCommandIndex < 0 && GUI.Button(row, GUIContent.none, GUIStyle.none)) clicked = i;
            }
            GUI.EndScrollView();
            if (clicked >= 0)
            {
                _inventoryOffset = clicked;
                OpenItemCommands();
            }
            return panel.yMax + 6f;
        }

        private void DrawInventoryTabs(Rect panel)
        {
            const float gap = 4f;
            float tabWidth = (panel.width - 20f - gap * (InventoryPageCount - 1)) / InventoryPageCount;
            for (int page = 0; page < OccupiedInventoryPageCount; page++)
            {
                var tab = new Rect(panel.x + 10f + page * (tabWidth + gap), panel.y + 33f, tabWidth, 22f);
                if (GUI.Button(tab, (page + 1).ToString()) && _itemCommandIndex < 0)
                    SelectInventoryPage(page);
                if (!_familiarStatus && page == _inventoryPage)
                    DrawHudFill(new Rect(tab.x + 2f, tab.yMax - 3f, tab.width - 4f, 2f),
                        new Color(0.93f, 0.78f, 0.42f));
            }
        }

        private void DrawFamiliarInventoryName(Rect row, string label, GUIStyle style, BagFamiliar reserve)
        {
            int collarSlot = 0;
            if (_area == Area.Tower)
            {
                if (reserve == null && (_familiar || _carriedFamiliar)) collarSlot = RedCollarIconSlot;
                else if (reserve != null && reserve == _blueCollarFamiliar && _blueCollarAlly?.View)
                    collarSlot = BlueCollarIconSlot;
            }

            if (collarSlot == 0)
            {
                DrawHudText(row, label, style);
                return;
            }

            const float badgeSize = 20f;
            var badge = new Rect(row.xMax - badgeSize, row.center.y - badgeSize * 0.5f, badgeSize, badgeSize);
            if (Event.current.type == EventType.Repaint)
            {
                var icon = SuppliedItemSprite(GroundIconNames[collarSlot]);
                if (!icon) icon = RetailGroundItemSprite(collarSlot);
                if (icon) DrawItemSprite(badge, icon);
            }
            row.xMax = badge.x - 4f;
            GUI.BeginGroup(row);
            DrawHudText(new Rect(Vector2.zero, row.size), label, style);
            GUI.EndGroup();
        }

        private static void DrawInventoryFocusBand(Rect row) =>
            DrawHudFill(row, new Color(0.24f, 0.33f, 0.63f, 0.38f));

        private static void DrawInventoryFocusMarker(Rect row)
        {
            if (Event.current.type != EventType.Repaint) return;
            var marker = new Rect(row.x, row.y, 26f, row.height);
            if (HasRetailHudFrames)
            {
                DrawFramePart(marker, 0, 40, 80, 32, 32);
                return;
            }

            var gold = new Color(0.93f, 0.78f, 0.42f);
            DrawHudFill(new Rect(marker.x, marker.y, marker.width, 2f), gold);
            DrawHudFill(new Rect(marker.x, marker.y, 2f, marker.height), gold);
        }

        private GUIStyle InventoryNameStyle(DungeonItemEntry item)
        {
            if (!Inventory.IsUnrevealedEquipment(item)) return _bodyStyle;
            if (_unrevealedGearStyle == null)
            {
                var cyan = new Color(0.35f, 0.95f, 1f);
                _unrevealedGearStyle = new GUIStyle(_bodyStyle)
                {
                    normal = { textColor = cyan }, hover = { textColor = cyan },
                    active = { textColor = cyan }, focused = { textColor = cyan }
                };
            }
            return _unrevealedGearStyle;
        }

        private Rect DrawEquipmentCurseIcon(Rect row, DungeonItemEntry item)
        {
            if (!Inventory.IsCursedEquipment(item) || Inventory.IsUnrevealedEquipment(item)) return row;
            if (Event.current.type == EventType.Repaint)
            {
                var icon = SuppliedItemSprite("cursed");
                if (icon) DrawItemSprite(new Rect(row.x, row.center.y - 8f, 16f, 16f), icon);
            }
            row.xMin += 19f;
            return row;
        }

        private void DrawInventoryItemIcon(Rect rect, DungeonItemEntry item)
        {
            var icon = item == null
                ? ItemDisplaySprite(ItemSystem.Kind.Herb, 1)
                : ItemDisplaySprite(item);
            if (!icon && item != null) icon = ItemDisplaySprite(item.Kind, 1);
            if (!icon)
            {
                DrawHudText(rect, "?", _bodyStyle);
                return;
            }
            DrawItemSprite(rect, icon);
        }

        private static void DrawItemSprite(Rect rect, Sprite icon)
        {
            var texture = icon.texture;
            var pixels = icon.textureRect;
            float scale = Mathf.Min(rect.width / pixels.width, rect.height / pixels.height);
            var size = pixels.size * scale;
            var destination = new Rect(rect.center - size * 0.5f, size);
            GUI.DrawTextureWithTexCoords(destination, texture, new Rect(
                pixels.x / texture.width, pixels.y / texture.height,
                pixels.width / texture.width, pixels.height / texture.height));
        }

        private void DrawActorStatus(Rect stats, string name, ActorRecord actor, bool familiar,
            int health, int maxHp, int attack, int defense)
        {
            if (familiar && actor != null)
            {
                DrawFamiliarStatus(stats, name, actor, health, maxHp, attack, defense);
                return;
            }
            if (actor == null)
            {
                DrawHudText(new Rect(stats.x + 10f, stats.y + 6f, stats.width - 20f, 22f), name, _bodyStyle);
                return;
            }
            float left = stats.x + 10f, top = stats.y + 8f, width = stats.width - 20f;
            if (Event.current.type == EventType.Repaint)
            {
                var portrait = new Rect(left, top, 48f, 48f);
                if (IsFrog(actor))
                {
                    var frogPortrait = FamiliarStatusTexture("Tower/portrait_koh_frog");
                    if (frogPortrait) GUI.DrawTexture(portrait, frogPortrait, ScaleMode.ScaleToFit);
                }
                else if (_kohStatusFaces)
                {
                    int face = KohStatusFace();
                    int x = face % 5 * 32;
                    int y = face / 5 * 32;
                    GUI.DrawTextureWithTexCoords(portrait, _kohStatusFaces,
                        new Rect(x / 160f, 1f - (y + 32f) / 64f, 32f / 160f, 0.5f));
                }
            }
            DrawHudText(new Rect(left + 53f, top, width - 53f, 20f), HeroName, _bodyStyle);
            DrawHudText(new Rect(left + 53f, top + 23f, width - 105f, 20f), T("TAMER", "DOMADOR"), _messageStyle);
            DrawHudText(new Rect(left + width - 48f, top + 23f, 48f, 20f),
                T("LV ", "NÍV ") + Mathf.Max(1, actor.Level), _messageStyle);
            DrawHudText(new Rect(left, top + 50f, 50f, 33f), T("HLTH", "VIDA"), _bodyStyle);
            DrawFamiliarStatusIcon(new Rect(left + 50f, top + 51f, 14f, 14f), "hp");
            DrawHudText(new Rect(left + 68f, top + 49f, width - 68f, 17f),
                T("HP ", "PV ") + $"{Mathf.Max(0, health)} / {maxHp}", _messageStyle);

            var inventory = CarriedInventory;
            float shieldX = left + (width + 49f) * 0.5f;
            DrawGear(new Rect(left + 50f, top + 67f, 14f, 14f), false);
            DrawGear(new Rect(shieldX, top + 67f, 14f, 14f), true);
            DrawStatusAttributes(left, top, width, actor, attack, defense);
            DrawDialogueFrame(new Rect(left - 5f, top + 138f, width + 10f, stats.height - 146f));

            void DrawGear(Rect iconRect, bool shield)
            {
                var equipped = inventory?.EquippedItem(PlayerEquipped, shield);
                if (Event.current.type == EventType.Repaint)
                {
                    var icon = equipped == null ? ItemDisplaySprite(shield ? ItemKind.Shield : ItemKind.Sword, 1) :
                        ItemDisplaySprite(equipped);
                    if (icon) DrawItemSprite(iconRect, icon);
                }
                int strength = IsFrog(actor) ? 0 : shield ? PlayerShieldDefense() : EquipmentBonus(inventory, PlayerEquipped, false);
                DrawHudText(new Rect(iconRect.xMax + 4f, iconRect.y - 1f, 32f, 17f), strength.ToString(), _messageStyle);
            }
        }

        private readonly Dictionary<string, Texture2D> _familiarStatusTextures = new();

        private Texture2D FamiliarStatusTexture(string path)
        {
            if (!_familiarStatusTextures.TryGetValue(path, out var texture))
                _familiarStatusTextures[path] = texture = Resources.Load<Texture2D>(path);
            return texture;
        }

        private void DrawFamiliarStatus(Rect panel, string name, ActorRecord actor, int health, int maxHp, int attack, int defense)
        {
            float left = panel.x + 10f, width = panel.width - 20f, top = panel.y + 8f;
            string species = FindEggSpecies(actor.Species)?.name ?? ((MonsterSpecies)actor.Species).ToString();
            if (actor.Species == 0) species = name;
            int genus = UnitElement(actor);
            var portrait = new Rect(left, top, 48f, 48f);
            if (Event.current.type == EventType.Repaint)
            {
                var color = GUI.color;
                if (Combat.HasStatus(actor, StatusId.ANGER)) GUI.color = color * new Color(1f, 0.62f, 0.62f);
                DrawFamiliarStatusPortrait(portrait, actor, species);
                GUI.color = color;
            }
            DrawFamiliarStatusIcon(new Rect(left + 53f, top + 2f, 17f, 17f), FamiliarGenusIcon(genus));
            GUI.BeginGroup(new Rect(left + 74f, top, width - 74f, 20f));
            string displayName = (actor.Flags14 & LevaFusionProtection) != 0 ? name + " [Leva]" : name;
            DrawHudText(new Rect(0f, 0f, width - 74f, 20f), displayName, _bodyStyle);
            GUI.EndGroup();
            DrawHudText(new Rect(left + 53f, top + 23f, width - 105f, 20f), species.ToUpperInvariant(), _messageStyle);
            DrawHudText(new Rect(left + width - 48f, top + 23f, 48f, 20f),
                T("LV ", "NÍV ") + Mathf.Max(1, actor.Level), _messageStyle);

            DrawHudText(new Rect(left, top + 51f, 51f, 34f), T("HLTH", "VIDA"), _bodyStyle);
            DrawFamiliarStatusIcon(new Rect(left + 50f, top + 52f, 14f, 14f), "hp");
            DrawFamiliarStatusIcon(new Rect(left + 50f, top + 69f, 14f, 14f), "mp");
            DrawHudText(new Rect(left + 68f, top + 49f, width - 68f, 17f),
                T("HP ", "PV ") + $"{Mathf.Max(0, health)} / {maxHp}", _messageStyle);
            DrawHudText(new Rect(left + 68f, top + 66f, width - 68f, 17f),
                T("MP ", "PM ") + $"{actor.CurrentMp} / {actor.MaxMp}", _messageStyle);

            DrawStatusAttributes(left, top, width, actor, attack, defense);

            var spells = new Rect(left - 5f, top + 138f, width + 10f, 57f);
            DrawDialogueFrame(spells);
            DrawFamiliarSpells(spells, actor);
        }

        private void DrawStatusAttributes(float left, float top, float width, ActorRecord actor, int attack, int defense)
        {
            float half = width * 0.5f;
            DrawHudText(new Rect(left, top + 85f, half, 17f), T("ATK ", "ATQ ") + attack, _messageStyle);
            DrawHudText(new Rect(left + half, top + 85f, half, 17f), "DEF " + (IsFrog(actor) ? Mathf.Max(1, defense / 2) : defense), _messageStyle);
            DrawHudText(new Rect(left, top + 102f, half, 17f), T("MAX ", "MÁX ") + actor.EffectiveAttack, _messageStyle);
            DrawHudText(new Rect(left + half, top + 102f, half, 17f), T("MAX ", "MÁX ") + actor.EffectiveDefense, _messageStyle);
            DrawHudText(new Rect(left, top + 119f, 35f, 16f), "EXP", _messageStyle);
            DrawExperienceStars(new Rect(left + 38f, top + 120f, width - 38f, 13f), ExperienceHalfStars(actor.Level, actor.Experience));
        }

        private void DrawFamiliarStatusPortrait(Rect portrait, ActorRecord actor, string species)
        {
            if (actor.Species == (byte)MonsterSpecies.KEWNE && !IsFrog(actor) && _kewneStatusSheet)
            {
                float width = portrait.height * 93f / 140f;
                var region = new Rect(portrait.center.x - width * 0.5f, portrait.y, width, portrait.height);
                GUI.DrawTextureWithTexCoords(region, _kewneStatusSheet, new Rect(28f / 319f, 1f - 181f / 273f, 93f / 319f, 140f / 273f));
                return;
            }
            _weedyBook ??= Resources.Load<TextAsset>("Town/WeedyBook/pages") is { } book
                ? JsonUtility.FromJson<MonsterBook>(book.text) : null;
            var page = _weedyBook?.pages == null ? null : System.Array.Find(_weedyBook.pages,
                entry => string.Equals(entry.name, species, System.StringComparison.OrdinalIgnoreCase));
            var texture = IsFrog(actor) ? FamiliarStatusTexture("Tower/portrait_monster_frog") :
                actor.Species == (byte)MonsterSpecies.KEWNE && _kewneMenuPortrait ? _kewneMenuPortrait :
                page != null ? StreamPortrait("Book/" + page.portrait) : null;
            if (texture) GUI.DrawTexture(portrait, texture, ScaleMode.ScaleToFit);
        }

        private static int ExperienceHalfStars(int level, int experience)
        {
            level = Mathf.Clamp(level, 1, Leveling.MaxLevel);
            if (level == Leveling.MaxLevel) return 20;
            int start = ExperienceThresholds[level - 1], end = ExperienceThresholds[level];
            return (int)((long)Mathf.Clamp(experience - start, 0, end - start) * 20 / (end - start));
        }

        private void DrawExperienceStars(Rect rect, int halfStars)
        {
            if (Event.current.type != EventType.Repaint) return;
            var texture = FamiliarStatusTexture("Tower/Familiar_exp_star");
            if (!texture) return;
            float step = Mathf.Min(15f, rect.width / 10f);
            float size = Mathf.Min(rect.height, step);
            var previous = GUI.color;
            for (int star = 0; star < 10; star++)
            {
                var icon = new Rect(rect.x + star * step, rect.y, size, size);
                GUI.color = previous * new Color(0.40f, 0.40f, 0.40f, 0.7f);
                GUI.DrawTexture(icon, texture);
                GUI.color = previous;
                float fill = Mathf.Clamp(halfStars - star * 2, 0, 2) * 0.5f;
                if (fill > 0f)
                {
                    icon.width *= fill;
                    GUI.DrawTextureWithTexCoords(icon, texture, new Rect(0f, 0f, fill, 1f));
                }
            }
        }

        private static string FamiliarGenusIcon(int element) => element switch
        {
            Element.Fire => "genus_fire", Element.Water => "genus_water", Element.Wind => "genus_wind", _ => "genus_tri"
        };

        private void DrawFamiliarStatusIcon(Rect rect, string name)
        {
            if (Event.current.type != EventType.Repaint) return;
            var icon = FamiliarStatusTexture("Tower/Familiar_" + name);
            if (icon) GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit);
        }

        private static readonly string[] FamiliarSpellNames =
        {
            "Breath", "Sled", "Brid", "Rise", "Poison", "DeWall", "DeMirror", "DeRock", "DeHeal",
            "DeForth", "LoBlind", "LoBind", "LoSleep", "LoDown", "LoGrave", "Dark Wave", "Acid Rain"
        };

        private void DrawFamiliarSpells(Rect panel, ActorRecord actor)
        {
            int row = 0;
            var shown = new HashSet<int>();
            if (actor.Spells != null)
                foreach (var spell in actor.Spells)
                    if (spell != null) DrawSpell(spell.SpellId, spell.StoredLevel);
            for (int slot = 0; slot < 3; slot++)
            {
                int offset = 8 + slot * 3;
                DrawSpell(actor.U8(offset), actor.U8(offset + 1));
            }
            if (row == 0) DrawSpell(UnitProfile(actor.Species)?.nativeSpellId ?? 0, actor.Level);
            if (row == 0) DrawHudText(new Rect(panel.x + 6f, panel.y + 6f, panel.width - 12f, 17f),
                T("No spells", "Sem magias"), _messageStyle);

            void DrawSpell(int id, int level)
            {
                int family = SpellFamily(id);
                if (row >= 3 || family < 0 || !shown.Add(family)) return;
                float y = panel.y + 4f + row++ * 16f;
                int element = id == 49 ? Element.Water : (id - 1) % 3 == 0 ? Element.Fire :
                    (id - 1) % 3 == 1 ? Element.Water : Element.Wind;
                DrawFamiliarStatusIcon(new Rect(panel.x + 6f, y + 1f, 13f, 13f), FamiliarGenusIcon(element));
                DrawHudText(new Rect(panel.x + 23f, y, panel.width - 79f, 16f), FamiliarSpellNames[family], _messageStyle);
                DrawHudText(new Rect(panel.xMax - 49f, y, 45f, 16f), T("LV ", "NÍV ") + Mathf.Clamp(level, 1, 99), _messageStyle);
            }
        }

        private int KohStatusFace()
        {
            if (Combat.HasStatus(_playerActor, StatusId.FROG)) return 0;
            if (Combat.HasStatus(_playerActor, StatusId.SLEEP)) return 7;
            if (KohIsParalyzed) return 5;
            if (Combat.HasStatus(_playerActor, StatusId.POISON)) return 2;
            if (KohIsBlind) return 4;
            if (KohIsConfused) return 6;
            if (Combat.HasStatus(_playerActor, StatusId.ANGER)) return 8;
            int speed = MovementSpeedTurns(_playerActor);
            if (speed != 0) return speed > 0 ? 9 : 3;
            return 1;
        }

        private string ItemDescription(DungeonItemEntry item)
        {
            if (Inventory.IsUnrevealedEquipment(item))
                return T("The properties of this equipment are unknown.", "As propriedades deste equipamento são desconhecidas.");
            var text = InventoryText.Find(item);
            if (text == null) return InventoryItemName(item);
            string description = T(text.descriptionEn, text.descriptionPtBr);
            if (!Equipment(item)) return description;

            int effectiveStat = Inventory.EquipmentStat(item, text);
            if (effectiveStat == text.gear) return description;

            string statLabel = item.Kind == ItemKind.Shield ? "DEF" : T("ATK", "ATQ");
            string baseStat = $"{statLabel} {text.gear}.";
            return description.EndsWith(baseStat, System.StringComparison.Ordinal)
                ? description.Substring(0, description.Length - baseStat.Length) + $"{statLabel} {effectiveStat}."
                : description;
        }

        private static string BallChargeText(DungeonItemEntry ball) =>
            T("Charges: ", "Cargas: ") + Mathf.Max(0, (int)ball.Param);

        private void ShowItemPickup(DungeonItemEntry item)
        {
            string name = InventoryItemName(item);
            _message = T($"You pick up {name}.", $"Você recolhe: {name}.");
        }

        private string InventoryItemName(DungeonItemEntry item)
        {
            if (item.Kind == MonsterEgg.Kind) return DescribeEgg(item);
            if (item.Kind == ItemKind.Familiar) return FindEggSpecies(item.Subtype)?.name ?? T("Familiar", "Familiar");
            if (Inventory.IsUnrevealedEquipment(item))
                return item.Kind switch
                {
                    ItemKind.Shield => T("Unidentified shield", "Escudo não identificado"),
                    ItemKind.Wand => T("Unidentified wand", "Cajado não identificado"),
                    _ => T("Unidentified sword", "Espada não identificada")
                };
            var text = InventoryText.Find(item);
            string name = text != null ? T(text.en, text.ptBr)
                : T($"Unknown item ({item.Kind:X2}:{item.Subtype:X2})", $"Item desconhecido ({item.Kind:X2}:{item.Subtype:X2})");
            int modifier = Equipment(item) ? ItemSystem.GetEquipmentModifier(item) : 0;
            return modifier == 0 ? name : $"{name} {modifier:+0;-0}";
        }
    }
}
