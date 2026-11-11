using System.Collections.Generic;
using AzureDreams.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private enum InventoryMenu { Closed, Main, Ground, Familiar, Commands, Direction }
        private enum ManualOrder { Attack, Magic, Assist, Move, Wait, Species, Scout }
        private InventoryMenu _inventoryMenu;
        private int _inventoryMenuRow, _commandCollar;
        private Vector2Int _commandDirection;
        private ManualOrder _selectedManualOrder;
        private readonly Dictionary<ActorRecord, (ManualOrder Order, Vector2Int Direction)> _pendingFamiliarCommands = new();

        private void OpenInventoryRoot()
        {
            _inventoryMenu = InventoryMenu.Main;
            _inventoryMenuRow = 0;
            _inventoryActorSlot = 0;
        }

        private void OpenInventoryItems()
        {
            _inventoryMenu = InventoryMenu.Closed;
            _inventoryOpen = true;
            _inventoryActorSlot = _inventoryPage = _inventoryOffset = 0;
            _inventoryFamiliarScroll = Vector2.zero;
            _familiarOrderPicker = FamiliarOrderPicker.None;
            _familiarStatus = false;
            RefreshInventoryCommands();
        }

        private bool HandleInventoryRoot(Keyboard keyboard, bool switchingWindows)
        {
            if (!switchingWindows && _inventoryAction?.WasPressedThisFrame() == true && !_inventoryOpen)
            {
                PlayRetailSound(_inventoryMenu == InventoryMenu.Closed ? RetailSfx.Confirm : RetailSfx.Cancel);
                if (_area == Area.Town) OpenInventoryItems();
                else if (_inventoryMenu == InventoryMenu.Closed) OpenInventoryRoot();
                else _inventoryMenu = InventoryMenu.Closed;
                return true;
            }
            if (_inventoryMenu == InventoryMenu.Closed) return false;
            if (_cancel?.WasPressedThisFrame() == true)
            {
                BackInventoryMenu();
                return true;
            }
            int characterDelta = InventoryCharacterDelta(keyboard);
            if (_inventoryMenu != InventoryMenu.Direction && characterDelta != 0)
            {
                CycleInventoryActor(characterDelta);
                return true;
            }
            int vertical = keyboard?.upArrowKey.wasPressedThisFrame == true ? -1 :
                keyboard?.downArrowKey.wasPressedThisFrame == true ? 1 : 0;
            if (_inventoryMenu == InventoryMenu.Direction)
            {
                int horizontal = keyboard?.leftArrowKey.wasPressedThisFrame == true ? -1 :
                    keyboard?.rightArrowKey.wasPressedThisFrame == true ? 1 : 0;
                var direction = new Vector2Int(horizontal, -vertical);
                if (direction != Vector2Int.zero)
                {
                    var world = FormationWorldOffset(direction);
                    if (_selectedManualOrder == ManualOrder.Move && Mathf.Abs(world.x) + Mathf.Abs(world.y) == 2)
                        world = horizontal != 0 ? new Vector2Int(world.x, 0) : new Vector2Int(0, world.y);
                    _commandDirection = world;
                }
                if (_interact?.WasPressedThisFrame() == true) ConfirmManualOrder();
                return true;
            }
            int count = InventoryMenuRowCount();
            if (count > 0)
            {
                int previousRow = _inventoryMenuRow;
                _inventoryMenuRow = Mathf.Clamp(_inventoryMenuRow + vertical, 0, count - 1);
                if (_inventoryMenuRow != previousRow) PlayRetailSound(RetailSfx.Cursor);
                if (_interact?.WasPressedThisFrame() == true) SelectInventoryMenuRow(_inventoryMenuRow);
            }
            return true;
        }

        private void BackInventoryMenu()
        {
            PlayRetailSound(RetailSfx.Cancel);
            _inventoryMenu = _inventoryMenu switch
            {
                InventoryMenu.Direction => InventoryMenu.Commands,
                InventoryMenu.Commands when InventoryCollarActor(1) != null && InventoryCollarActor(2) != null => InventoryMenu.Familiar,
                InventoryMenu.Main => InventoryMenu.Closed,
                _ => InventoryMenu.Main
            };
            _inventoryMenuRow = 0;
        }

        private int InventoryMenuRowCount() => _inventoryMenu switch
        {
            InventoryMenu.Main => 3,
            InventoryMenu.Commands => InventoryCollarActor(_commandCollar)?.Species == (byte)MonsterSpecies.U_BOAT
                ? 7 : HasSpeciesCommand(InventoryCollarActor(_commandCollar)) ? 6 : 5,
            InventoryMenu.Familiar => 2,
            InventoryMenu.Ground => CanRearmUnderKoh ? 1 : 0,
            _ => 0
        };

        private void SelectInventoryMenuRow(int row)
        {
            switch (_inventoryMenu)
            {
                case InventoryMenu.Main:
                    if (row == 0)
                        OpenInventoryItems();
                    else if (row == 1)
                    {
                        bool red = _familiar;
                        bool blue = _blueCollarAlly?.View;
                        if (_area != Area.Tower || (!red && !blue)) return;
                        _commandCollar = red ? 1 : 2;
                        _inventoryMenu = red && blue ? InventoryMenu.Familiar : InventoryMenu.Commands;
                    }
                    else
                    {
                        if ((_area == Area.Tower && TryInspectGroundItem()) ||
                            (_area == Area.Town && ItemUnderKoh(_playerCell) != null))
                        {
                            _inventoryMenu = InventoryMenu.Closed;
                            if (_area == Area.Town) _groundItemPrompt = _playerCell;
                            _groundItemActionsOpen = _groundItemPrompt.HasValue;
                            _groundItemSelection = GroundItemAction.Use;
                        }
                        else _inventoryMenu = InventoryMenu.Ground;
                    }
                    _inventoryMenuRow = 0;
                    PlayRetailSound(RetailSfx.Confirm);
                    break;
                case InventoryMenu.Familiar:
                    PlayRetailSound(RetailSfx.Confirm);
                    _commandCollar = row + 1;
                    _inventoryMenu = InventoryMenu.Commands;
                    _inventoryMenuRow = 0;
                    break;
                case InventoryMenu.Commands:
                    _selectedManualOrder = (ManualOrder)row;
                    _commandDirection = _towerFacing == Vector2Int.zero ? Vector2Int.up : _towerFacing;
                    if (ManualOrderNeedsDirection(InventoryCollarActor(_commandCollar), _selectedManualOrder))
                    {
                        if (_selectedManualOrder == ManualOrder.Move && Mathf.Abs(_commandDirection.x) + Mathf.Abs(_commandDirection.y) != 1)
                            _commandDirection = Vector2Int.up;
                        _inventoryMenu = InventoryMenu.Direction;
                        PlayRetailSound(RetailSfx.Confirm);
                    }
                    else ConfirmManualOrder();
                    break;
                case InventoryMenu.Ground:
                    if (CanRearmUnderKoh)
                    {
                        PlayRetailSound(RetailSfx.Confirm);
                        _inventoryMenu = InventoryMenu.Closed;
                        _trapRearmCell = _playerCell;
                    }
                    break;
            }
        }

        private void ConfirmManualOrder()
        {
            var actor = InventoryCollarActor(_commandCollar);
            if (_turnBusy || actor == null || (_commandCollar == 1 && !_familiar)) return;
            var from = _commandCollar == 1 ? _familiarCell : _blueCollarAlly.Cell;
            if (ManualOrderNeedsDirection(actor, _selectedManualOrder) && !InsideMap(from + _commandDirection)) return;
            if (_selectedManualOrder == ManualOrder.Species &&
                (!HasSpeciesCommand(actor) || (IsSpeciesObstacleCaster(actor) &&
                    !CanCreateRockAt(from, _commandDirection)))) return;
            if (_selectedManualOrder == ManualOrder.Move &&
                (!CanTowerStep(from, _commandDirection) ||
                 !FamiliarOrderCellOpen(actor, from + _commandDirection, _playerCell, null, null))) return;
            _pendingFamiliarCommands[actor] = (_selectedManualOrder, _commandDirection);
            PlayRetailSound(RetailSfx.Confirm);
            _inventoryMenu = InventoryMenu.Closed;
        }

        private static bool ManualOrderNeedsDirection(ActorRecord actor, ManualOrder order) =>
            order is ManualOrder.Attack or ManualOrder.Magic or ManualOrder.Move ||
            (order == ManualOrder.Species && IsSpeciesObstacleCaster(actor));

        private string ManualOrderLabel(ManualOrder order) => order switch
        {
            ManualOrder.Attack => T("Attack", "Atacar"),
            ManualOrder.Magic => T("Use magic", "Usar magia"),
            ManualOrder.Assist => T("Assist", "Auxiliar"),
            ManualOrder.Move => T("Move", "Andar"),
            ManualOrder.Species when InventoryCollarActor(_commandCollar) is { } actor => SpeciesCommandLabel(actor),
            ManualOrder.Scout => T("Scout (16 MP)", "Explorar (16 MP)"),
            _ => T("Wait", "Esperar")
        };

        private bool DrawInventoryRoot(float width, float height)
        {
            if (_inventoryMenu == InventoryMenu.Closed) return false;
            if (_inventoryMenu == InventoryMenu.Direction)
            {
                DrawManualDirection(width, height);
                return true;
            }
            int count = InventoryMenuRowCount();
            var panel = new Rect(18f, height * 0.25f, 185f, count == 0 ? 62f : 16f + count * 30f);
            DrawDialogueFrame(panel);
            float statusWidth = Mathf.Min(228f, width - panel.xMax - 36f);
            if (statusWidth >= 140f)
            {
                var status = new Rect(width - statusWidth - 18f, panel.y, statusWidth, 207f);
                DrawDialogueFrame(status);
                DrawInventoryActorStatus(status, InventoryActorRow(_inventoryActorSlot));
                DrawHudText(new Rect(status.x + 10f, status.yMax + 3f, status.width - 20f, 22f),
                    T("PgUp/PgDn: character", "PgUp/PgDn: personagem"), _messageStyle);
            }
            if (count == 0)
                DrawHudText(new Rect(panel.x + 8f, panel.y + 8f, panel.width - 16f, 46f),
                    T("Nothing to use here.", "Não há nada para usar aqui."), _messageStyle);
            for (int row = 0; row < count; row++)
            {
                string label = _inventoryMenu switch
                {
                    InventoryMenu.Main => row == 0 ? T("Items", "Itens") : row == 1 ? T("Command", "Comandar") : T("Ground", "Chão"),
                    InventoryMenu.Familiar => row == 0 ? FamiliarDisplayName : _blueCollarFamiliar?.Name,
                    InventoryMenu.Commands => ManualOrderLabel((ManualOrder)row),
                    _ => T("Rearm", "Reativar")
                };
                var button = new Rect(panel.x + 8f, panel.y + 8f + row * 30f, panel.width - 16f, 27f);
                bool enabled = GUI.enabled;
                if (_inventoryMenu == InventoryMenu.Main && row == 1)
                    GUI.enabled = enabled && _area == Area.Tower && (_familiar || _blueCollarAlly?.View);
                bool clicked = GUI.Button(button, (row == _inventoryMenuRow ? "▸ " : "") + label);
                GUI.enabled = enabled;
                if (clicked) { SelectInventoryMenuRow(row); break; }
            }
            return true;
        }

        private void DrawManualDirection(float width, float height)
        {
            var actor = InventoryCollarActor(_commandCollar);
            if (actor == null || !_gameCamera) return;
            var from = _commandCollar == 1 ? _familiarCell : _blueCollarAlly.Cell;
            for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    var step = new Vector2Int(x, y);
                    int distance = Mathf.Abs(x) + Mathf.Abs(y);
                    if (distance == 0 || (_selectedManualOrder == ManualOrder.Move && (distance != 1 ||
                        !CanTowerStep(from, step) || !FamiliarOrderCellOpen(actor, from + step, _playerCell, null, null)))) continue;
                    if (!InsideMap(from + step) ||
                        (_selectedManualOrder == ManualOrder.Species && !CanCreateRockAt(from, step))) continue;
                    Vector3 point = _gameCamera.WorldToScreenPoint(CellPosition(from + step) - Vector3.up * 0.5f);
                    if (point.z <= 0f) continue;
                    var tile = new Rect(point.x * width / Screen.width - 13f,
                        (1f - point.y / Screen.height) * height - 10f, 26f, 20f);
                    DrawHudFill(tile, step == _commandDirection ? new Color(1f, 0.82f, 0.23f, 0.9f) : new Color(0.4f, 0.8f, 1f, 0.45f));
                    if (GUI.Button(tile, GUIContent.none, GUIStyle.none))
                    {
                        _commandDirection = step;
                        ConfirmManualOrder();
                        return;
                    }
                }
            var hint = new Rect(18f, 76f, 210f, 55f);
            DrawDialogueFrame(hint);
            DrawHudText(new Rect(hint.x + 8f, hint.y + 5f, hint.width - 16f, 43f),
                ManualOrderLabel(_selectedManualOrder) + "\n" + T("Arrows · Confirm · Esc", "Setas · Confirmar · Esc"), _messageStyle);
        }

        private (Vector2Int Step, int Attack) PlanManualFamiliarOrder(ActorRecord actor, GameObject view, Vector2Int from,
            Vector2Int direction, ManualOrder order, Vector2Int kohDestination, Vector2Int? reserved, Vector2Int? otherDestination)
        {
            var wait = (Vector2Int.zero, -1);
            if (MonsterExhausted(actor) || ItemImmobilized(actor) || SlowBlocksAction(actor) ||
                Combat.HasStatus(actor, StatusId.CONFUSION) || (actor == _familiarActor && _landingFamiliar))
                return wait;
            if (order == ManualOrder.Wait) return wait;
            if (order == ManualOrder.Move)
                return CanTowerStep(from, direction) &&
                    FamiliarOrderCellOpen(actor, from + direction, kohDestination, reserved, otherDestination)
                    ? (direction, -1) : wait;
            if (IsFrog(actor)) return wait;
            if (order == ManualOrder.Scout)
            {
                if (actor.Species != (byte)MonsterSpecies.U_BOAT || IsSubmerged(actor)) return wait;
                if (!TryScoutTowerFloor(actor, view, direction))
                    AddSpeciesMessage(T("Not enough MP to scout.", "MP insuficiente para explorar."));
                return wait;
            }
            if (order == ManualOrder.Species)
            {
                if (actor.Species == (byte)MonsterSpecies.U_BOAT)
                {
                    if (IsSubmerged(actor)) Surface(actor, from);
                    else Dive(actor, view, from);
                }
                else if (IsSpeciesObstacleCaster(actor) && !TryQueueSpeciesObstacle(actor, view, from, direction))
                    AddSpeciesMessage(T("The familiar could not place the obstacle.", "O familiar não conseguiu criar o obstáculo."));
                return wait;
            }
            if (IsSubmerged(actor))
            {
                Surface(actor, from);
                return wait;
            }
            if (order == ManualOrder.Attack)
            {
                int enemy = FamiliarHasSight(from, from + direction) ? CreatureAt(from + direction) : -1;
                if (enemy >= 0) return (Vector2Int.zero, enemy);
                if (SpendMonsterMp(actor, 256)) QueueMonsterSpriteAction(view, direction, targetHeight: 0);
                return wait;
            }
            if (!CanCastMagic(actor)) return wait;
            var profile = UnitProfile(actor.Species);
            int spell = actor.U8(8);
            if (spell == 0) spell = profile?.nativeSpellId ?? 0;
            if (spell == 0) spell = profile?.hiddenSpellId ?? 0;
            if (spell == 0) return wait;
            if (order == ManualOrder.Assist)
            {
                TryManualMixMagic(actor, view, from, kohDestination, spell);
                return wait;
            }

            int cost = NativeSpellCost(actor, spell);
            if (actor.MpRaw < cost && !_mpProtection.ContainsKey(actor) || !SpendMonsterMp(actor, cost)) return wait;
            int family = SpellFamily(spell);
            var end = from;
            ActorRecord target = null;
            if (family is 5 or 6) target = actor;
            else
            {
                for (int i = 1; i <= 8; i++)
                {
                    var next = from + direction * i;
                    if (!FamiliarHasSight(end, next)) break;
                    end = next;
                    int enemy = CreatureAt(end);
                    if (enemy >= 0) { target = _creatures[enemy].Actor; break; }
                    if (end == _playerCell) { target = _playerActor; break; }
                    if (_familiar && end == _familiarCell) { target = _familiarActor; break; }
                    if (_blueCollarAlly?.Cell == end) { target = _blueCollarAlly.Actor; break; }
                    if (BellCellOccupied(end)) break;
                }
                if (target == null && family is 8 or 9) { target = actor; end = from; }
            }
            int element = UnitElement(actor);
            QueueMonsterSpriteAction(view, direction, casting: true);
            bool reflected = target != null && target != actor && (DirectSpellPower(spell) > 0 || DirectSpellStatus(spell) != 0) &&
                ReflectMonsterSpell(target);
            var destination = reflected ? from : end;
            QueueSpellCue(spell, element, from, destination, reflected ? end : null);
            int level = actor.U8(8) == spell ? Mathf.Max(1, actor.U8(9)) : actor.Level;
            _spellImpactAction += () => ResolveCrystalBall(spell, element, actor, reflected ? actor : target, from, destination, direction,
                family == 7 && end != from && target == null ? end : from, level);
            return wait;
        }
    }
}
