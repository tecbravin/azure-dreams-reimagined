using System.Collections.Generic;
using AzureDreams.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        // Shelf fronts from the three captured house interiors, facing +Z.
        private static readonly (float Left, float Right)[] StorageShelfWidths =
        {
            (17f, 18f), (24f, 27f), (47f, 51f)
        };
        private const float StorageShelfFront = 9.5f;
        private const int StorageRowsPerPage = 7;
        private static readonly int[] HomeStorageSlots = { 10, 30, 60 };
        private int HomeStorageCapacity => HomeStorageSlots[HouseIndex >= 0 ? HouseIndex : CurrentHomeRank];
        private bool _homeStorageOpen, _storageFromWreath, _storageSelectDepot;
        private int _storageRow;
        private string _storageFeedback;
        private IReadOnlyList<DungeonItemEntry> _storageBagRows, _storageDepotRows;

        private bool TryHomeStorageShelf()
        {
            int house = HouseIndex;
            if (house < 0 || _townFacing.y > -0.35f || _townJumpHeight > TownGroundTolerance) return false;
            var position = _player.transform.position;
            var (left, right) = StorageShelfWidths[house];
            if (position.x < left - 0.2f || position.x > right + 0.2f ||
                position.z < StorageShelfFront || position.z > StorageShelfFront + 1.2f) return false;
            OpenHomeStorage(fromWreath: false);
            return true;
        }

        private void OpenHomeStorage(bool fromWreath)
        {
            _checkpointMenu = CheckpointMenu.Closed;
            _homeStorageOpen = true;
            _storageFromWreath = fromWreath;
            _storageSelectDepot = _townInventory.TotalItems == 0 && _townStorage.TotalItems > 0;
            _storageRow = 0;
            _storageFeedback = null;
            RefreshHomeStorageRows();
        }

        private void CloseHomeStorage()
        {
            _homeStorageOpen = false;
            _storageBagRows = _storageDepotRows = null;
            if (!_storageFromWreath) return;
            _storageFromWreath = false;
            _checkpointSelection = WreathStorageOption;
            _checkpointMenu = CheckpointMenu.Wreath;
        }

        private void RefreshHomeStorageRows()
        {
            _storageBagRows = _townInventory.DungeonItems;
            _storageDepotRows = _townStorage.DungeonItems;
        }

        private Inventory StorageSide(bool depot) => depot ? _townStorage : _townInventory;
        private IReadOnlyList<DungeonItemEntry> StorageRows(bool depot) => depot ? _storageDepotRows : _storageBagRows;
        private int StorageCount(bool depot) => StorageRows(depot).Count + StorageSide(depot).GetCount(_potionItem);
        private string StorageItemName(DungeonItemEntry item) =>
            item == null ? T("Medicinal Herb", "Erva Medicinal") : InventoryItemName(item);

        private bool HandleHomeStorageInput()
        {
            if (!_homeStorageOpen) return false;
            if (_cancel?.WasPressedThisFrame() == true) { CloseHomeStorage(); return true; }
            if (Keyboard.current is { } keyboard)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame) SelectStorageSide(false);
                else if (keyboard.rightArrowKey.wasPressedThisFrame) SelectStorageSide(true);
                int step = keyboard.upArrowKey.wasPressedThisFrame ? -1 : keyboard.downArrowKey.wasPressedThisFrame ? 1 : 0;
                _storageRow = Mathf.Clamp(_storageRow + step, 0, Mathf.Max(0, StorageCount(_storageSelectDepot) - 1));
            }
            if (_interact?.WasPressedThisFrame() == true) TransferHomeStorageItem();
            return true;
        }

        private void SelectStorageSide(bool depot)
        {
            if (_storageSelectDepot == depot) return;
            _storageSelectDepot = depot;
            _storageRow = 0;
            _storageFeedback = null;
        }

        private void TransferHomeStorageItem()
        {
            bool withdrawing = _storageSelectDepot;
            if (_storageRow >= StorageCount(withdrawing)) return;
            if (withdrawing && _townInventory.TotalItems + FamiliarInventoryCount >= Inventory.SlotCapacity)
            {
                _storageFeedback = T("Your bag is full.", "Sua sacola está cheia.");
                return;
            }

            if (!withdrawing && _townStorage.TotalItems >= HomeStorageCapacity)
            {
                _storageFeedback = T("The item storage is full.", "O depósito de itens está cheio.");
                return;
            }

            var source = StorageSide(withdrawing);
            var destination = StorageSide(!withdrawing);
            var rows = StorageRows(withdrawing);
            var item = _storageRow < rows.Count ? rows[_storageRow] : null;
            if (item == null)
            {
                if (!source.RemoveItem(_potionItem)) return;
                destination.AddItem(_potionItem);
            }
            else
            {
                if (item.Kind == ItemSystem.Kind.Familiar)
                {
                    _storageFeedback = T("Familiars stay in the monster hut.", "Os familiares ficam no alojamento de monstros.");
                    return;
                }
                if (!source.ConsumeDungeonItem(_storageRow, item.Kind)) return;
                destination.AddDungeonItem(OffBody(item));
            }

            string name = StorageItemName(item);
            _storageFeedback = withdrawing ? T($"Took {name}.", $"Retirou {name}.") : T($"Stored {name}.", $"Guardou {name}.");
            RefreshHomeStorageRows();
            _storageRow = Mathf.Clamp(_storageRow, 0, Mathf.Max(0, StorageCount(withdrawing) - 1));
            PersistTownProgress();
        }

        private void DrawHomeStorage(float width, float height)
        {
            var panel = new Rect(width * 0.5f - 280f, height * 0.5f - 161f, 560f, 322f);
            DrawDialogueFrame(panel);
            DrawHudText(new Rect(panel.x + 13f, panel.y + 8f, 370f, 24f),
                T("Koh's item storage", "Depósito de itens do Koh"), _bodyStyle);
            DrawStorageColumn(false, panel.x + 13f, panel.y + 42f);
            DrawStorageColumn(true, panel.x + 284f, panel.y + 42f);
            string action = _storageSelectDepot ? WithInteractHint("Take", "Retirar") : WithInteractHint("Store", "Guardar");
            DrawHudText(new Rect(panel.x + 13f, panel.y + 249f, 534f, 30f), _storageFeedback ??
                T("Select an item to move it between the bag and storage.", "Selecione um item para mover entre a sacola e o depósito."), _messageStyle);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && StorageCount(_storageSelectDepot) > 0;
            if (GUI.Button(new Rect(panel.x + 13f, panel.y + 282f, 262f, 26f), action)) TransferHomeStorageItem();
            GUI.enabled = previousEnabled;
            if (GUI.Button(new Rect(panel.x + 284f, panel.y + 282f, 262f, 26f), T("Back", "Voltar"))) CloseHomeStorage();
        }

        private void DrawStorageColumn(bool depot, float x, float y)
        {
            int count = StorageCount(depot);
            bool active = _storageSelectDepot == depot;
            int page = active ? _storageRow / StorageRowsPerPage : 0;
            int first = page * StorageRowsPerPage;
            var items = StorageRows(depot);
            string title = depot ? T("Storage", "Depósito") : T("Bag", "Sacola");
            string tally = depot ? $"{_townStorage.TotalItems}/{HomeStorageCapacity}" : $"{_townInventory.TotalItems + FamiliarInventoryCount}/{Inventory.SlotCapacity}";
            if (GUI.Button(new Rect(x, y, 262f, 23f), $"{(active ? "▸ " : "")}{title} · {tally}")) SelectStorageSide(depot);
            if (count == 0)
                DrawHudText(new Rect(x + 8f, y + 28f, 245f, 23f), T("Empty", "Vazio"), _messageStyle);
            for (int i = first; i < Mathf.Min(count, first + StorageRowsPerPage); i++)
            {
                var item = i < items.Count ? items[i] : null;
                var row = new Rect(x, y + 26f + (i - first) * 22f, 262f, 21f);
                if (GUI.Button(row, GUIContent.none))
                {
                    SelectStorageSide(depot);
                    _storageRow = i;
                    _storageFeedback = null;
                }
                if (active && _storageRow == i) DrawHudFill(row, new Color(0.2f, 0.34f, 0.68f, 0.35f));
                DrawInventoryItemIcon(new Rect(row.x + 5f, row.y + 2f, 17f, 17f), item);
                row.xMin += 26f;
                if (item != null) row = DrawEquipmentCurseIcon(row, item);
                string name = StorageItemName(item);
                if (item?.Kind == ItemSystem.Kind.Ball) name += $" ({Mathf.Max(0, (int)item.Param)})";
                DrawHudText(row, name, item == null ? _messageStyle : InventoryNameStyle(item));
            }
            if (first > 0 && GUI.Button(new Rect(x, y + 183f, 126f, 22f), T("Previous", "Anterior")))
                _storageRow = first - StorageRowsPerPage;
            if (first + StorageRowsPerPage < count &&
                GUI.Button(new Rect(x + 132f, y + 183f, 130f, 22f), T("Next", "Próxima")))
            {
                SelectStorageSide(depot);
                _storageRow = first + StorageRowsPerPage;
            }
        }
    }
}
