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

        private bool TryOpenDebugItemPicker()
        {
            if (Keyboard.current?.numpad9Key.wasPressedThisFrame != true || !_player) return false;
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
            if (keyboard?.numpad9Key.wasPressedThisFrame == true || _cancel?.WasPressedThisFrame() == true)
            {
                _debugItemPickerOpen = false;
                return;
            }
            if (keyboard == null || _debugItems.Count == 0) return;
            int step = keyboard.upArrowKey.wasPressedThisFrame ? -1 : keyboard.downArrowKey.wasPressedThisFrame ? 1 : 0;
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
                T("Spawn item [Numpad 9]", "Gerar item [Num 9]"));
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
                    ? T("Cannot place an item here. Close and move to a free tile.", "Não é possível colocar o item aqui. Feche e vá para um piso livre.")
                    : T("Click an item or use the arrow keys and Enter.", "Clique em um item ou use as setas e Enter."));
            if (clicked >= 0) SpawnDebugItem(clicked);
        }

        private void SpawnDebugItem(int index)
        {
            var (kind, id, _) = _debugItems[index];
            var item = kind == MonsterEgg.Kind
                ? MonsterEgg.Create(id, identified: true)
                : ItemSystem.CreateFloorItem(id, kind, kind == ItemSystem.Kind.Ball ? (sbyte)5 : (sbyte)0);
            if (_area == Area.Tower && kind is ItemSystem.Kind.Sword or ItemSystem.Kind.Shield)
                item = TowerItemTrapManager.CreateTowerEquipment(kind, id, Random.Range(0, 100));

            if (!PlaceInventoryItem(_playerCell, item))
            {
                _debugItemPlacementFailed = true;
                return;
            }

            if (_area == Area.Tower) _discardedUnderKoh = _playerCell;
            else _townDroppedItems[_playerCell].view.transform.position =
                _player.transform.position - Vector3.up * (TownActorFootOffset - WorldItemHeight);
            _debugItemPickerOpen = false;
        }
    }
}
