using System.Collections.Generic;
using System.Globalization;
using AzureDreams.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const float DebugItemRowHeight = 26f;
        private readonly List<(byte kind, byte id, string label)> _debugItems = new();
        private bool _debugItemPickerOpen, _debugItemPlacementFailed;
        private int _debugItemSelection;
        private Vector2 _debugItemScroll;
        private static readonly TowerTrapEffect[] DebugTrapEffects =
            (TowerTrapEffect[])System.Enum.GetValues(typeof(TowerTrapEffect));
        private bool _debugTrapPickerOpen, _debugTrapPlacementFailed;
        private int _debugTrapSelection;
        private Vector2 _debugTrapScroll;

        private bool TryOpenDebugTrapPicker()
        {
            if (_area != Area.Tower || !_player || _turnBusy || _inventoryOpen ||
                _inventoryMenu != InventoryMenu.Closed ||
                _towerRun?.CurrentState != TowerRunManager.State.InTower ||
                Keyboard.current?.numpad2Key.wasPressedThisFrame != true) return false;
            _debugTrapSelection = 0;
            _debugTrapScroll = Vector2.zero;
            _debugTrapPlacementFailed = false;
            _debugTrapPickerOpen = true;
            return true;
        }

        private void HandleDebugTrapPickerInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard?.numpad2Key.wasPressedThisFrame == true || _cancel?.WasPressedThisFrame() == true)
            {
                _debugTrapPickerOpen = false;
                return;
            }
            int step = MenuDirectionPressed(1) ? -1 : MenuDirectionPressed(2) ? 1 : 0;
            if (step != 0)
            {
                _debugTrapSelection = Mathf.Clamp(_debugTrapSelection + step, 0, DebugTrapEffects.Length - 1);
                float top = _debugTrapSelection * DebugItemRowHeight;
                _debugTrapScroll.y = Mathf.Clamp(_debugTrapScroll.y, top - DebugItemViewportHeight + DebugItemRowHeight, top);
            }
            if (_interact?.WasPressedThisFrame() == true || keyboard?.enterKey.wasPressedThisFrame == true ||
                keyboard?.numpadEnterKey.wasPressedThisFrame == true)
                SpawnDebugTrap(_debugTrapSelection);
        }

        private void DrawDebugTrapPicker(float width, float height)
        {
            float panelWidth = Mathf.Min(424f, width - 20f);
            float listHeight = DebugItemViewportHeight;
            var panel = new Rect((width - panelWidth) * .5f, (height - listHeight - 72f) * .5f, panelWidth, listHeight + 72f);
            DrawHudPanel(panel);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 6f, panel.width - 106f, 24f),
                T("Spawn inactive trap [Num 2]", "Gerar armadilha desativada [Num 2]"));
            if (GUI.Button(new Rect(panel.xMax - 90f, panel.y + 6f, 78f, 24f), T("Close", "Fechar")))
                _debugTrapPickerOpen = false;
            var viewport = new Rect(panel.x + 12f, panel.y + 34f, panel.width - 24f, listHeight);
            float rowWidth = viewport.width - 17f;
            _debugTrapScroll = GUI.BeginScrollView(viewport, _debugTrapScroll,
                new Rect(0f, 0f, rowWidth, DebugTrapEffects.Length * DebugItemRowHeight));
            int clicked = -1;
            for (int i = 0; i < DebugTrapEffects.Length; i++)
                if (GUI.Button(new Rect(0f, i * DebugItemRowHeight, rowWidth, DebugItemRowHeight - 2f),
                    (i == _debugTrapSelection ? "> " : "") + DebugTrapEffects[i])) clicked = i;
            GUI.EndScrollView();
            GUI.Label(new Rect(panel.x + 12f, viewport.yMax + 4f, panel.width - 24f, 26f),
                _debugTrapPlacementFailed ? T("Cannot place a trap here.", "Não é possível colocar uma armadilha aqui.") :
                T("Click a trap or use the arrows and Enter.", "Clique em uma armadilha ou use as setas e Enter."));
            if (clicked >= 0) SpawnDebugTrap(clicked);
        }

        private void SpawnDebugTrap(int index)
        {
            if (_area != Area.Tower || !_player || (uint)index >= (uint)DebugTrapEffects.Length) return;
            Vector2Int cell = _playerCell;
            var effect = DebugTrapEffects[index];
            if (_towerItemTraps != null)
            {
                if (!_towerItemTraps.PlaceDebugDisarmedTrap(cell, effect)) { _debugTrapPlacementFailed = true; return; }
            }
            else
            {
                var trap = new TowerTrapState { Effect = effect };
                trap.Reveal();
                trap.Disarm(out _);
                _demoTraps[cell] = trap;
            }
            if (_trapMarkers.TryGetValue(cell, out var marker))
            {
                _trapMarkers.Remove(cell);
                if (marker) RemoveGenerated(marker);
            }
            _sprungGoUpTraps.Remove(cell);
            CreateTrapMarker(cell);
            _debugTrapPickerOpen = false;
        }

        private bool TryOpenDebugItemPicker()
        {
            if (Keyboard.current?.numpad0Key.wasPressedThisFrame != true || !_player) return false;
            if (_area == Area.Town && (_townDoorOpening || _townJumpHeight > 0f || _townJumpVelocity != 0f ||
                _activeTownNpc || _townObjectDialogueOpen ||
                (_townFlow && _townFlow.State.Current != TownGameState.State.TownIdle))) return false;

            _debugItems.Clear();
            foreach (var entry in InventoryText.Entries)
                _debugItems.Add(((byte)entry.kind, (byte)entry.id, T(entry.en, entry.ptBr)));
            var species = GetEggCatalog()?.species;
            if (species != null)
                foreach (var entry in species)
                    _debugItems.Add((MonsterEgg.Kind, (byte)entry.id, T($"{entry.name} Egg", $"Ovo de {entry.name}")));

            if (_debugItems.Count == 0)
            {
                Debug.LogError("Debug item catalogs could not be loaded from Resources/Town/Retail.");
                return false;
            }

            var compare = CultureInfo.GetCultureInfo(GameLanguage.Portuguese ? "pt-BR" : "en").CompareInfo;
            _debugItems.Sort((a, b) =>
            {
                int byName = compare.Compare(a.label, b.label, CompareOptions.IgnoreCase);
                return byName != 0 ? byName : (a.kind * 256 + a.id).CompareTo(b.kind * 256 + b.id);
            });
            _debugItemScroll = Vector2.zero;
            _debugItemSelection = 0;
            _debugItemPlacementFailed = false;
            _debugItemPickerOpen = true;
            return true;
        }

        private void HandleDebugItemPickerInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard?.numpad0Key.wasPressedThisFrame == true || _cancel?.WasPressedThisFrame() == true)
            {
                _debugItemPickerOpen = false;
                return;
            }
            if (keyboard == null || _debugItems.Count == 0) return;
            int step = MenuDirectionPressed(1) ? -1 : MenuDirectionPressed(2) ? 1 : 0;
            if (step != 0)
            {
                _debugItemSelection = Mathf.Clamp(_debugItemSelection + step, 0, _debugItems.Count - 1);
                float top = _debugItemSelection * DebugItemRowHeight;
                _debugItemScroll.y = Mathf.Clamp(_debugItemScroll.y, top - DebugItemViewportHeight + DebugItemRowHeight, top);
            }
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                SpawnDebugItem(_debugItemSelection);
        }

        private static float DebugItemViewportHeight
        {
            get
            {
                float scale = Mathf.Max(0.5f, Mathf.Min(Screen.width / 640f, Screen.height / 480f));
                return Mathf.Min(360f, Screen.height / scale - 92f);
            }
        }

        private void DrawDebugItemPicker(float width, float height)
        {
            float panelWidth = Mathf.Min(424f, width - 20f);
            float listHeight = DebugItemViewportHeight;
            float panelHeight = listHeight + 72f;
            var panel = new Rect((width - panelWidth) * 0.5f, (height - panelHeight) * 0.5f, panelWidth, panelHeight);
            DrawHudPanel(panel);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 6f, panel.width - 106f, 24f),
                T("Spawn item [Numpad 0]", "Gerar item [Num 0]"));
            if (GUI.Button(new Rect(panel.xMax - 90f, panel.y + 6f, 78f, 24f), T("Close", "Fechar")))
                _debugItemPickerOpen = false;
            var viewport = new Rect(panel.x + 12f, panel.y + 34f, panel.width - 24f, listHeight);
            float rowWidth = viewport.width - 17f;
            _debugItemScroll = GUI.BeginScrollView(viewport, _debugItemScroll,
                new Rect(0f, 0f, rowWidth, _debugItems.Count * DebugItemRowHeight));
            int clicked = -1;
            int first = Mathf.Max(0, Mathf.FloorToInt(_debugItemScroll.y / DebugItemRowHeight));
            int last = Mathf.Min(_debugItems.Count, first + Mathf.CeilToInt(listHeight / DebugItemRowHeight) + 1);
            for (int i = first; i < last; i++)
            {
                var item = _debugItems[i];
                string prefix = i == _debugItemSelection ? "> " : "";
                if (GUI.Button(new Rect(0f, i * DebugItemRowHeight, rowWidth, DebugItemRowHeight - 2f),
                    $"{prefix}{item.label}  [{item.kind:X2}:{item.id:X2}]")) clicked = i;
            }
            GUI.EndScrollView();

            GUI.Label(new Rect(panel.x + 12f, viewport.yMax + 4f, panel.width - 24f, 26f),
                _debugItemPlacementFailed
                    ? T("No reachable free tile is available for this item.", "Não há um piso livre acessível para este item.")
                    : T("Click an item or use the arrow keys and Enter.", "Clique em um item ou use as setas e Enter."));
            if (clicked >= 0) SpawnDebugItem(clicked);
        }

        private void SpawnDebugItem(int index)
        {
            var (kind, id, _) = _debugItems[index];
            var item = kind == MonsterEgg.Kind
                ? MonsterEgg.Create(id, identified: true)
                : ItemSystem.CreateFloorItem(id, kind, kind == ItemSystem.Kind.Ball ? (sbyte)5 : (sbyte)0);
            if (_area == Area.Tower && kind is ItemSystem.Kind.Sword or ItemSystem.Kind.Shield or ItemSystem.Kind.Wand)
                item = TowerItemTrapManager.CreateTowerEquipment(kind, id, Random.Range(0, 1024));
            else if (kind == ItemSystem.Kind.Sword && id == DarkSword)
                item.Flags |= Inventory.CursedEquipmentFlag;

            // Reuse normal discard placement: a debug item must still be
            // creatable when Koh is standing on another dropped item.
            if (!TryFindDiscardCell(_playerCell, out var destination) || !PlaceInventoryItem(destination, item))
            {
                _debugItemPlacementFailed = true;
                return;
            }

            if (_area == Area.Tower && destination == _playerCell) _discardedUnderKoh = destination;
            _debugItemPickerOpen = false;
        }
    }
}
