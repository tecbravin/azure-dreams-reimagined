using System.Collections.Generic;
using AzureDreams.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        [SerializeField] private string _inventoryBinding = "<Keyboard>/i";
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
            int delta = MenuDirectionPressed(1) ? -1 :
                MenuDirectionPressed(2) ? 1 : 0;
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
            DrawInventoryText(new Rect(panel.x + 10f, panel.y + 6f, panel.width - 20f, 24f), title, _bodyStyle);
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
                DrawInventoryText(text, label, InventoryNameStyle(item));
                if (GUI.Button(row, GUIContent.none, GUIStyle.none)) clicked = i;
            }
            GUI.EndScrollView();
            if (DrawInventoryButton(new Rect(viewport.x, viewport.yMax + 5f, viewport.width, 25f), T("Cancel", "Cancelar")))
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
            int delta = (MenuDirectionPressed(1) ? -1 :
                MenuDirectionPressed(2) ? 1 : 0);
            if (_itemCommandIndex >= 0)
            {
                int previousCommand = _itemCommandIndex;
                _itemCommandIndex = Mathf.Clamp(_itemCommandIndex + delta, 0, _availableItemCommands.Count - 1);
                if (_itemCommandIndex != previousCommand) PlayRetailSound(RetailSfx.Cursor);
                if (confirm) ExecuteItemCommand(_availableItemCommands[_itemCommandIndex]);
                return true;
            }
            int horizontal = (MenuDirectionPressed(3) ? -1 :
                MenuDirectionPressed(4) ? 1 : 0);
            if (!IsActiveInventoryActorSlot(_inventoryActorSlot))
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
                if (horizontal > 0 && next >= OccupiedInventoryPageCount && TryInspectGroundItem())
                {
                    _inventoryOpen = false;
                    _groundItemActionsOpen = _groundItemPrompt.HasValue;
                    _groundItemSelection = GroundItemAction.Use;
                    PlayRetailSound(RetailSfx.Confirm);
                    return true;
                }
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

        private int InventoryCharacterDelta(Keyboard keyboard) =>
            _rotateLeftAction?.WasPressedThisFrame() == true ? -1 :
            _rotateRightAction?.WasPressedThisFrame() == true ? 1 : 0;

        private string InventoryCharacterKeys => $"{_rotateLeftAction?.GetBindingDisplayString() ?? "K"}/{_rotateRightAction?.GetBindingDisplayString() ?? "L"}";
        private string InventoryCharacterHint => T($"{InventoryCharacterKeys}: character", $"{InventoryCharacterKeys}: personagem");

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
            var layout = InventoryPanels(width,height,FamiliarInventoryCount,_familiarStatus);
            var items = layout.Items;
            var stats = layout.Stats;
            float panelWidth = items.width;
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
            // Familiars share the same 20 slots as items (see HomeStorage's combined
            // "_townInventory.TotalItems + FamiliarInventoryCount" tally), so the header
            // counts both toward the total here too - showing only itemCount/SlotCapacity
            // understated how full the bag actually was whenever familiars were carried.
            bool showSort = !_familiarStatus && _itemCommandIndex < 0 && itemCount > 1;
            DrawInventoryFittedText(new Rect(items.x + 10f, items.y + 6f, panelWidth - 54f - (showSort ? 104f : 0f), 30f),
                _familiarStatus ? T("Familiars", "Familiares") + $" {FamiliarInventoryCount}/{Inventory.SlotCapacity}" :
                    T("Items", "Itens") + $" {count}/{Inventory.SlotCapacity}", InventoryEmphasisStyle(_bodyStyle));
            if (!_familiarStatus && _itemCommandIndex < 0 && itemCount > 1 && DrawInventoryFittedButton(
                new Rect(items.xMax - 136, items.y + 6, 92, 24), T("Sort", "Ordenar"))) SortInventory();
            DrawCornerSettingsButton(new Rect(items.xMax-36f,items.y+6f,26f,26f));
            DrawInventoryTabs(items);
            int first = _familiarStatus ? Mathf.Max(0, _inventoryOffset) / InventoryRowsPerPage * InventoryRowsPerPage :
                FirstItemRowOnPage;
            int end = _familiarStatus ? FamiliarInventoryCount : count;
            for (int i = first; i < first + InventoryRowsPerPage; i++)
            {
                float rowHeight=(items.height-80f-(!_familiarStatus && OccupiedInventoryPageCount>1 ? 24f : 0f))/InventoryRowsPerPage;
                var row = new Rect(items.x + 10f, items.y + 73f + (i - first) * rowHeight, panelWidth - 20f, rowHeight-2f);
                if (i >= end)
                {
                    if (!_familiarStatus)
                    {
                        DrawHudFill(row, new Color(0.08f, 0.12f, 0.19f, 0.30f));
                        row.xMin += 27f;
                        DrawInventoryText(row, "-", _messageStyle);
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
                if (item?.Kind == ItemKind.Ball && !SecondTowerItems.IsUnknown(item)) label += $" ({Mathf.Max(0, (int)item.Param)})";
                var hitArea = row;
                bool focused = i == _inventoryOffset;
                if (focused) DrawInventoryFocusBand(hitArea);
                bool familiarInBag = familiarRow && (_area == Area.Town ||
                    (reserve != null ? reserve != _blueCollarFamiliar : !_familiar && !_carriedFamiliar));
                if (!familiarRow || familiarInBag)
                {
                    if (Event.current.type == EventType.Repaint)
                    {
                        var iconRect = new Rect(row.x, row.center.y - 10f, 20f, 20f);
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
                else DrawInventoryFittedText(row, label, labelStyle);
                if (_itemCommandIndex < 0 && GUI.Button(hitArea, GUIContent.none, GUIStyle.none))
                { _inventoryOffset = i; OpenItemCommands(); }
            }
            if (!_familiarStatus && OccupiedInventoryPageCount>1) DrawInventoryNextPage(items);
            float descriptionY = layout.Description.y;
            var shortcuts = new Rect(items.x,items.yMax+10f,items.width,56f);
            DrawDialogueFrame(shortcuts);
            DrawInventoryFamiliarStrip(stats,layout.Description.y-10f);
            if (_inventoryOffset >= 0)
            {
                var description = new Rect(items.x, descriptionY, layout.Description.width, layout.Description.height);
                DrawDialogueFrame(description);
                var selected = SelectedInventoryItem(out _);
                if (SelectedRawIndex <= FamiliarInventoryRow)
                    DrawInventoryParagraph(new Rect(description.x + 10f, description.y + 6f, description.width - 20f, description.height - 12f),
                        T("E: Familiar actions", "E: Ações do familiar"));
                else if (selected != null)
                    DrawInventoryParagraph(new Rect(description.x + 10f, description.y + 6f, description.width - 20f, description.height - 12f),
                        ItemDescription(selected) + (selected.Kind == ItemKind.Ball && !SecondTowerItems.IsUnknown(selected) ?
                            "  " + BallChargeText(selected) + T(" · Spell Lv. ", " · Nív. da magia ") +
                            BallSpellLevel(selected.Param) : ""));
            }

            int actorRow = _familiarStatus ? SelectedRawIndex : InventoryActorRow(_inventoryActorSlot);
            bool viewingFamiliar = actorRow <= FamiliarInventoryRow;
            var actor = DrawInventoryActorStatus(stats, actorRow);
            if (FamiliarInventoryCount > 0 && _itemCommandIndex < 0 &&
                DrawInventoryFittedButton(new Rect(shortcuts.x+8f,shortcuts.y+4f,shortcuts.width-16f,24f),
                    _familiarStatus ? T("Items [F]", "Itens [F]") : T("Familiars [F]", "Familiares [F]")))
                ToggleInventoryFamiliars();
            DrawInventoryFittedText(new Rect(shortcuts.x+8f,shortcuts.y+30f,shortcuts.width-16f,22f),
                T($"Left/Right: tabs · {InventoryCharacterKeys}: character", $"Esq./Dir.: abas · {InventoryCharacterKeys}: personagem"),_messageStyle);
            if (_familiarStatus && actor != null && _itemCommandIndex < 0)
            {
                var talents = new Rect(layout.Description.x+10f,descriptionY+33f,layout.Description.width-20f,layout.Description.height-40f);
                DrawInventoryParagraph(talents,T("Talents: ", "Talentos: ")+DescribeTalents(actor),3);
            }
            DrawItemCommands(width, height);
        }

        private bool _drawingInventoryStats;

        private ActorRecord DrawInventoryActorStatus(Rect panel, int actorRow)
        {
            bool familiar = actorRow <= FamiliarInventoryRow;
            var reserve = ReserveForRow(actorRow);
            var actor = familiar ? reserve?.Actor ?? _familiarActor : _playerActor;
            string name = familiar ? reserve?.Name ?? FamiliarDisplayName : "Koh";
            var body = _bodyStyle;
            var message = _messageStyle;
            _bodyStyle = InventoryFitStyle(body,body.fontSize+6,false);
            _messageStyle = InventoryFitStyle(message,message.fontSize+6,false);
            _drawingInventoryStats=true;
            try { DrawActorStatus(panel, name, actor, familiar,
                reserve?.Actor.Hp ?? (familiar ? _familiarHealth : _health),
                reserve?.Actor.MaxHp ?? (familiar ? FamiliarMaxHealth : KohMaxHealth),
                reserve?.Actor.CurrentAttack ?? (familiar ? FamiliarWeaponDamage() : _playerActor.CurrentAttack),
                actor?.CurrentDefense ?? 0); }
            finally { _bodyStyle=body; _messageStyle=message; _drawingInventoryStats=false; }
            return actor;
        }

        private float DrawInventoryFamiliarStrip(Rect items,float bottom)
        {
            const float rowHeight = 28f;
            int count = FamiliarInventoryCount;
            var panel = new Rect(items.x, items.yMax + 10f, items.width, Mathf.Max(56f,bottom-items.yMax-10f));
            DrawDialogueFrame(panel);
            DrawInventoryText(new Rect(panel.x + 9f, panel.y + 3f, panel.width - 18f, 20f), T("Familiars", "Familiares"), _bodyStyle);
            var list = new Rect(panel.x + 9f, panel.y + 22f, panel.width - 18f, panel.height - 25f);
            float rowWidth = list.width - (count > 1 ? 16f : 0f);
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
                var tab = new Rect(panel.x + 10f + page * (tabWidth + gap), panel.y + 42f, tabWidth, 24f);
                if (DrawInventoryButton(tab, (page + 1).ToString(), !_familiarStatus && page == _inventoryPage) && _itemCommandIndex < 0)
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
                DrawInventoryFittedText(row, label, InventoryEmphasisStyle(style));
                return;
            }

            const float badgeSize = 20f;
            var badge = new Rect(row.xMax - badgeSize, row.center.y - badgeSize * 0.5f, badgeSize, badgeSize);
            if (Event.current.type == EventType.Repaint)
            {
                bool lost = IsFamiliarLost(reserve?.Actor ?? _familiarActor);
                var icon = SuppliedItemSprite(lost
                    ? (collarSlot == RedCollarIconSlot ? "lost_red_collar" : "lost_blue_collar")
                    : GroundIconNames[collarSlot]);
                if (!icon) icon = RetailGroundItemSprite(collarSlot);
                if (icon) DrawItemSprite(badge, icon);
            }
            row.xMax = badge.x - 4f;
            GUI.BeginGroup(row);
            DrawInventoryFittedText(new Rect(Vector2.zero, row.size), label, InventoryEmphasisStyle(style));
            GUI.EndGroup();
        }

        private void DrawInventoryText(Rect rect, string value, GUIStyle style) =>
            DrawHudText(rect, value, style, dialogue: true);

        private bool DrawInventoryButton(Rect rect, string value, bool selected = false, GUIStyle textStyle = null)
        {
            if (Event.current.type == EventType.Repaint)
            {
                DrawHudFill(rect, new Color(0.02f, 0.035f, 0.13f, 0.64f));
                if (selected) DrawInventoryFocusBand(rect);
                var gold = new Color(0.76f, 0.61f, 0.32f, selected ? 0.9f : 0.45f);
                DrawHudFill(new Rect(rect.x, rect.y, rect.width, 1f), gold);
                DrawHudFill(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), gold);
            }
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            var text = new Rect(rect.x + 5f, rect.y, rect.width - 10f, rect.height);
            DrawInventoryText(text, value, textStyle ?? _bodyStyle);
            return clicked;
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
                _unrevealedGearStyle = new GUIStyle(_bodyStyle) { fontStyle = FontStyle.BoldAndItalic };
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
                DrawInventoryText(rect, "?", _bodyStyle);
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
                DrawInventoryFittedText(new Rect(stats.x + 10f, stats.y + 6f, stats.width - 20f, 22f), name, _bodyStyle);
                return;
            }
            bool large = _drawingInventoryStats || stats.height >= 260f;
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
            DrawInventoryFittedText(new Rect(left + 53f, top, width - 53f, large ? 30f : 20f), HeroName, large ? InventoryEmphasisStyle(_bodyStyle) : _bodyStyle);
            DrawInventoryFittedText(new Rect(left + 53f, top + (large ? 28f : 23f), width - 105f, large ? 24f : 20f), T("TAMER", "DOMADOR"), _messageStyle);
            DrawInventoryFittedText(new Rect(left + width - 48f, top + (large ? 28f : 23f), 48f, large ? 24f : 20f),
                T("LV ", "NÍV ") + Mathf.Max(1, actor.Level), _messageStyle);
            DrawInventoryFittedText(new Rect(left, top + 50f, 50f, 33f), T("HLTH", "VIDA"), _bodyStyle);
            DrawFamiliarStatusIcon(new Rect(left + 50f, top + 51f, 14f, 14f), "hp");
            DrawInventoryFittedText(new Rect(left + 68f, top + (large ? 54f : 49f), width - 68f, large ? 28f : 17f),
                T("HP ", "PV ") + $"{Mathf.Max(0, health)} / {maxHp}", large ? InventoryEmphasisStyle(_messageStyle) : _messageStyle);

            var inventory = CarriedInventory;
            float shieldX = left + (width + 49f) * 0.5f;
            DrawGear(new Rect(left + 50f, top + (large ? 84f : 67f), 14f, 14f), false);
            DrawGear(new Rect(shieldX, top + (large ? 84f : 67f), 14f, 14f), true);
            DrawStatusAttributes(left, top, width, actor, attack, defense, large);


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
                DrawInventoryFittedText(new Rect(iconRect.xMax + 4f, iconRect.y - 1f, 40f, large ? 22f : 17f), strength.ToString(), large ? InventoryEmphasisStyle(_messageStyle) : _messageStyle);
            }
        }

        private readonly Dictionary<string, Texture2D> _familiarStatusTextures = new();

        private Texture2D FamiliarStatusTexture(string path)
        {
            if (!_familiarStatusTextures.TryGetValue(path, out var texture))
                _familiarStatusTextures[path] = texture = AssetMode.Load<Texture2D>(path);
            return texture;
        }

        private void DrawFamiliarStatus(Rect panel, string name, ActorRecord actor, int health, int maxHp, int attack, int defense)
        {
            bool large = _drawingInventoryStats || panel.height >= 260f;
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
            GUI.BeginGroup(new Rect(left + 74f, top, width - 74f, large ? 30f : 20f));
            string displayName = (actor.Flags14 & LevaFusionProtection) != 0 ? name + " [Leva]" : name;
            DrawInventoryFittedText(new Rect(0f, 0f, width - 74f, large ? 30f : 20f), displayName, large ? InventoryEmphasisStyle(_bodyStyle) : _bodyStyle);
            GUI.EndGroup();
            DrawInventoryFittedText(new Rect(left + 53f, top + (large ? 28f : 23f), width - 105f, large ? 24f : 20f), species.ToUpperInvariant(), _messageStyle);
            DrawInventoryFittedText(new Rect(left + width - 48f, top + (large ? 28f : 23f), 48f, large ? 24f : 20f),
                T("LV ", "NÍV ") + Mathf.Max(1, actor.Level), _messageStyle);

            if (!large) DrawInventoryFittedText(new Rect(left, top + 51f, 51f, 34f), T("HLTH", "VIDA"), _bodyStyle);
            DrawFamiliarStatusIcon(new Rect(left + (large ? 2f : 50f), top + (large ? 60f : 52f), 14f, 14f), "hp");
            DrawFamiliarStatusIcon(new Rect(left + (large ? 2f : 50f), top + (large ? 88f : 69f), 14f, 14f), "mp");
            DrawInventoryFittedText(new Rect(left + (large ? 20f : 68f), top + (large ? 54f : 49f), large ? width*.52f-30f : width-68f, large ? 28f : 17f),
                T("HP ", "PV ") + $"{Mathf.Max(0, health)} / {maxHp}", large ? InventoryEmphasisStyle(_messageStyle) : _messageStyle);
            DrawInventoryFittedText(new Rect(left + (large ? 20f : 68f), top + (large ? 82f : 66f), large ? width*.52f-30f : width-68f, large ? 28f : 17f),
                T("MP ", "PM ") + $"{actor.CurrentMp} / {actor.MaxMp}", large ? InventoryEmphasisStyle(_messageStyle) : _messageStyle);

            DrawStatusAttributes(left, top+(large ? 22f : 0f), width, actor, attack, defense, large);

            var heldWeapon = actor.Weapon;
            bool heldFruit = HasLevaFruit(actor);
            bool hasHeldItem = heldWeapon != null || heldFruit;
            if (hasHeldItem)
            {
                var held = heldWeapon ?? new DungeonItemEntry(4, ItemKind.Fruit, 0, 0);
                DrawInventoryItemIcon(new Rect(left+(large ? width*.52f : 0f), top + (large ? 54f : 136f), 14f, 14f), held);
                DrawInventoryFittedText(new Rect(left+(large ? width*.52f : 0f)+18f, top+(large ? 52f : 135f), large ? width*.48f-18f : width-18f,large ? 24f : 16f),
                    heldWeapon != null ? InventoryItemName(heldWeapon) : T("Leva Fruit", "Fruta Leva"), _messageStyle);
            }
            var spells = large
                ? new Rect(left+width*.52f,top+(hasHeldItem ? 78f : 54f),width*.48f,54f)
                : new Rect(left-5f,top+(hasHeldItem ? 154f : 138f),width+10f,hasHeldItem ? 41f : 57f);
            if (!large) DrawDialogueFrame(spells);
            DrawFamiliarSpells(spells, actor);
        }

        private void DrawStatusAttributes(float left, float top, float width, ActorRecord actor, int attack, int defense, bool large = false)
        {
            var style = large ? InventoryEmphasisStyle(_messageStyle) : _messageStyle;
            float columnWidth=width*(large ? .34f : .5f);
            float defenseX=left+width*(large ? .39f : .5f);
            float rowHeight=large ? 28f : 17f;
            DrawValue(left,top+(large ? 110f : 85f),T("ATK","ATQ"),attack);
            DrawValue(defenseX,top+(large ? 110f : 85f),"DEF",IsFrog(actor) ? Mathf.Max(1,defense/2) : defense);
            DrawValue(left,top+(large ? 138f : 102f),T("MAX","MÁX"),actor.EffectiveAttack);
            DrawValue(defenseX,top+(large ? 138f : 102f),T("MAX","MÁX"),actor.EffectiveDefense);
            void DrawValue(float x,float y,string label,int value)
            {
                float numberWidth=columnWidth*.4f;
                DrawInventoryFittedText(new Rect(x,y,columnWidth-numberWidth,rowHeight),label,style);
                DrawInventoryFittedText(new Rect(x+columnWidth-numberWidth,y,numberWidth,rowHeight),value.ToString(),InventoryNumberStyle(style));
            }
            DrawInventoryFittedText(new Rect(left, top + (large ? 166f : 119f), 45f, large ? 24f : 16f), "EXP", _messageStyle);
            DrawExperienceStars(new Rect(left + 48f, top + (large ? 172f : 120f), width - 48f, 13f), ExperienceHalfStars(actor.Level, actor.Experience));
        }

        private Texture2D _monsterPortraitSheet;

        private void DrawFamiliarStatusPortrait(Rect portrait, ActorRecord actor, string species)
        {
            if (actor.Species == (byte)MonsterSpecies.KEWNE && !IsFrog(actor) && _kewneStatusSheet)
            {
                float width = portrait.height * 93f / 140f;
                var region = new Rect(portrait.center.x - width * 0.5f, portrait.y, width, portrait.height);
                GUI.DrawTextureWithTexCoords(region, _kewneStatusSheet, new Rect(28f / 319f, 1f - 181f / 273f, 93f / 319f, 140f / 273f));
                return;
            }
            // Status portraits are the drawn genus portraits (Spriters "Monster Portraits": three 30x32 columns
            // fire/water/wind under a 16 px genus header, one row per species from Dragon to Maximum).
            if (!IsFrog(actor) && actor.Species >= (byte)MonsterSpecies.DRAGON && actor.Species <= (byte)MonsterSpecies.MAXIMUM)
            {
                _monsterPortraitSheet ??= AssetMode.Load<Texture2D>("Tower/MonsterPortraits");
                if (_monsterPortraitSheet)
                {
                    if (Event.current.type != EventType.Repaint) return;
                    int column = UnitElement(actor) switch { Element.Water => 1, Element.Wind => 2, _ => 0 };
                    int row = actor.Species - (byte)MonsterSpecies.DRAGON;
                    float sheetWidth = _monsterPortraitSheet.width, sheetHeight = _monsterPortraitSheet.height;
                    var cell = new Rect(column * 30f / sheetWidth, 1f - (16f + (row + 1) * 32f) / sheetHeight,
                        30f / sheetWidth, 32f / sheetHeight);
                    float width = portrait.height * 30f / 32f;
                    GUI.DrawTextureWithTexCoords(new Rect(portrait.center.x - width * 0.5f, portrait.y, width, portrait.height),
                        _monsterPortraitSheet, cell);
                    return;
                }
            }
            _weedyBook ??= AssetMode.Load<TextAsset>("Town/WeedyBook/pages") is { } book
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
            if (row == 0) DrawInventoryFittedText(new Rect(panel.x + 6f, panel.y + 6f, panel.width - 12f, _drawingInventoryStats ? 21f : 17f),
                T("No spells", "Sem magias"), _messageStyle);

            void DrawSpell(int id, int level)
            {
                int family = SpellFamily(id);
                if (row >= 3 || family < 0 || !shown.Add(family)) return;
                float y = panel.y + 2f + row++ * (_drawingInventoryStats ? 17f : panel.height < 50f ? 12f : 16f);
                int element = id == 49 ? Element.Water : (id - 1) % 3 == 0 ? Element.Fire :
                    (id - 1) % 3 == 1 ? Element.Water : Element.Wind;
                DrawFamiliarStatusIcon(new Rect(panel.x + 6f, y + 1f, 13f, 13f), FamiliarGenusIcon(element));
                DrawInventoryFittedText(new Rect(panel.x + 23f, y, panel.width - 73f, _drawingInventoryStats ? 19f : 16f), FamiliarSpellNames[family], _messageStyle);
                DrawInventoryFittedText(new Rect(panel.xMax - 47f, y, 43f, _drawingInventoryStats ? 19f : 16f), T("LV ", "NÍV ") + Mathf.Clamp(level, 1, 99), _messageStyle);
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
            if (SecondTowerItems.IsUnknown(item))
                return T("An unidentified item. Use it or look at it through Truth Glasses to learn what it is.",
                    "Um item não identificado. Use-o ou examine-o com os Óculos da Verdade para descobrir o que é.");
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
            if (SecondTowerItems.IsUnknown(item)) return SecondTowerItems.AppearanceName(item);
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
