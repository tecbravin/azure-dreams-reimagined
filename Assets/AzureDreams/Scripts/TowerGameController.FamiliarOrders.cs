using System.Collections.Generic;
using AzureDreams.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private enum FamiliarAi { Wait = 1, Follow, Support, Direct, Aggressive }
        private enum FamiliarOrderPicker { None, Formation, Ai, Fusion }
        private const int FusionOrderIndex = 2;
        private int FamiliarOrderCount => HasBlueCollar ? 3 : 2;

        private sealed class FamiliarOrders
        {
            internal FamiliarAi Ai = FamiliarAi.Direct;
            internal Vector2Int Offset;
            internal ActorRecord Target;
        }

        private readonly Dictionary<ActorRecord, FamiliarOrders> _familiarOrders = new();
        private readonly Queue<(Vector2Int Cell, Vector2Int Step)> _familiarPath = new();
        private readonly HashSet<Vector2Int> _familiarVisited = new();
        private int _inventoryActorSlot, _familiarOrderChoice;
        private FamiliarOrderPicker _familiarOrderPicker;
        private FamiliarAi _familiarAiCursor;
        private Vector2Int _formationCursor;
        private static readonly Vector2Int[] FormationDirections =
        {
            Vector2Int.up, new(1, 1), Vector2Int.right, new(1, -1),
            Vector2Int.down, new(-1, -1), Vector2Int.left, new(-1, 1)
        };
        private const string RedFormationKey = "familiar.formation.red";
        private const string BlueFormationKey = "familiar.formation.blue";

        private int CollarFormationDirection(bool blue)
        {
            int red = TownSaveVariables.GetInt(RedFormationKey, 6) & 7;
            int second = TownSaveVariables.GetInt(BlueFormationKey, 2) & 7;
            return blue ? (second == red ? (red + 1) & 7 : second) : red;
        }

        private Vector2Int CollarFormationOffset(ActorRecord actor) =>
            FormationDirections[CollarFormationDirection(actor == _blueCollarAlly?.Actor)];

        private int KohFormationFacing => Mathf.Max(0, System.Array.IndexOf(FormationDirections, _towerFacing));

        private Vector2Int FamiliarFormationTargetOffset(ActorRecord actor) =>
            FormationDirections[(CollarFormationDirection(actor == _blueCollarAlly?.Actor) + KohFormationFacing) & 7];

        private Vector2Int FindArrivalFamiliarCell(bool blue, Vector2Int? occupied = null)
        {
            var preferred = _playerCell + FormationDirections[(CollarFormationDirection(blue) + KohFormationFacing) & 7];
            Vector2Int? reserveBlue = !blue && HasBlueCollar && _blueCollarFamiliar is { Defeated: false } reserve && reserve.Actor.Hp > 0
                ? _playerCell + FormationDirections[(CollarFormationDirection(true) + KohFormationFacing) & 7] : null;
            var queue = new Queue<Vector2Int>();
            var seen = new HashSet<Vector2Int> { _playerCell };
            queue.Enqueue(_playerCell);
            Vector2Int? best = null;
            int bestDistance = int.MaxValue;
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (cell != _playerCell && cell != _stairCell && cell != occupied && cell != reserveBlue &&
                    TrapAt(cell)?.IsActive != true)
                {
                    int distance = (cell - preferred).sqrMagnitude;
                    if (distance < bestDistance) { best = cell; bestDistance = distance; }
                    if (distance == 0) return cell;
                }
                foreach (var step in FormationDirections)
                {
                    var next = cell + step;
                    if (!seen.Contains(next) && CanTowerStep(cell, step))
                    { seen.Add(next); queue.Enqueue(next); }
                }
            }
            // Only reserve the other collar's ideal cell while another free cell exists.
            if (!best.HasValue && reserveBlue.HasValue)
                return FindArrivalFamiliarCell(true, occupied);
            return best ?? throw new System.InvalidOperationException("No reachable free familiar arrival cell.");
        }

        private bool FormationPositionAvailable(ActorRecord actor, Vector2Int screen)
        {
            int world = System.Array.IndexOf(FormationDirections, FormationWorldOffset(screen));
            if (world < 0) return false;
            bool blue = actor == _blueCollarAlly?.Actor;
            return ((world - KohFormationFacing) & 7) != CollarFormationDirection(!blue);
        }

        private void OpenFamiliarFormation(ActorRecord actor)
        {
            if (IsFamiliarLost(actor)) return;
            _familiarOrderPicker = FamiliarOrderPicker.Formation;
            _formationCursor = Vector2Int.left;
            foreach (var option in FormationDirections)
                if (FormationWorldOffset(option) == FamiliarFormationTargetOffset(actor)) _formationCursor = option;
            PlayRetailSound(RetailSfx.Confirm);
        }
        private Vector3 _blueFamiliarFrom;
        private BagFamiliar _fusionFirst, _fusionSecond;
        private bool _fusionPendingSecond;
        private string _fusionMessage;

        private ActorRecord InventoryCollarActor(int slot) => _area != Area.Tower ? null : slot switch
        {
            1 when _familiar || _carriedFamiliar => _familiarActor,
            2 when _blueCollarAlly?.View => _blueCollarAlly.Actor,
            _ => null
        };

        private FamiliarOrders OrdersFor(ActorRecord actor)
        {
            if (_familiarOrders.TryGetValue(actor, out var orders))
            {
                orders.Offset = CollarFormationOffset(actor);
                return orders;
            }
            orders = new FamiliarOrders { Offset = CollarFormationOffset(actor) };
            _familiarOrders.Add(actor, orders);
            return orders;
        }

        private SaveSystem.FamiliarOrder CaptureFamiliarOrder(ActorRecord actor)
        {
            if (!_familiarOrders.TryGetValue(actor, out var orders) && (actor != _familiarActor || !_familiarWaiting))
                return default;
            orders ??= OrdersFor(actor);
            return new SaveSystem.FamiliarOrder
            {
                ai = (int)(actor == _familiarActor && _familiarWaiting ? FamiliarAi.Wait : orders.Ai),
                offset = orders.Offset
            };
        }

        private void RestoreFamiliarOrder(ActorRecord actor, SaveSystem.FamiliarOrder saved)
        {
            if (saved.ai is < (int)FamiliarAi.Wait or > (int)FamiliarAi.Aggressive) return;
            var orders = OrdersFor(actor);
            orders.Ai = (FamiliarAi)saved.ai;
            if (saved.offset != Vector2Int.zero && saved.offset.x is >= -1 and <= 1 && saved.offset.y is >= -1 and <= 1)
                orders.Offset = saved.offset;
        }

        private BagFamiliar FindBagFamiliar(ActorRecord actor)
        {
            if (actor == null) return null;
            // Search in the familiar inventory (bag)
            if (_bagFamiliars != null)
            {
                foreach (var f in _bagFamiliars)
                {
                    if (f.Actor == actor) return f;
                }
            }
            // Check blue collar familiar (BagFamiliar)
            if (_blueCollarFamiliar != null && _blueCollarFamiliar.Actor == actor) return _blueCollarFamiliar;
            // Check blue collar ally (ThrownEggAlly)
            if (_blueCollarAlly != null && _blueCollarAlly.Actor == actor) return null; // ThrownEggAlly is not a BagFamiliar
            return null;
        }

        // Status-page cycling follows the party on the current floor. Familiars
        // stored in the bag remain available from the familiar list, but do not
        // appear when the player switches character pages with PgUp/PgDn or
        // left/right in the root menu.
        private bool PrimaryFamiliarActiveForInventory => _familiarDefinition &&
            (_area != Area.Tower || _familiar || _carriedFamiliar);

        private bool BlueFamiliarActiveForInventory => _area == Area.Tower && _blueCollarAlly?.View;

        private bool IsActiveInventoryActorSlot(int slot) => slot == 0 ||
            (slot == 1 && PrimaryFamiliarActiveForInventory) ||
            (slot == 2 && BlueFamiliarActiveForInventory);

        private int ActiveInventoryActorCount => 1 +
            (PrimaryFamiliarActiveForInventory ? 1 : 0) +
            (BlueFamiliarActiveForInventory ? 1 : 0);

        private int ActiveInventoryActorIndex(int slot)
        {
            if (slot == 0) return 0;
            int index = 1;
            if (PrimaryFamiliarActiveForInventory)
            {
                if (slot == 1) return index;
                index++;
            }
            return BlueFamiliarActiveForInventory && slot == 2 ? index : 0;
        }

        private int InventoryActorSlotAtActiveIndex(int index)
        {
            if (index <= 0) return 0;
            if (PrimaryFamiliarActiveForInventory)
            {
                if (index == 1) return 1;
                index--;
            }
            return BlueFamiliarActiveForInventory && index == 1 ? 2 : 0;
        }

        private void CycleInventoryActor(int direction)
        {
            _familiarStatus = false;
            _familiarOrderChoice = 0;
            _familiarOrderPicker = FamiliarOrderPicker.None;

            int count = ActiveInventoryActorCount;
            int current = IsActiveInventoryActorSlot(_inventoryActorSlot)
                ? ActiveInventoryActorIndex(_inventoryActorSlot) : 0;
            int next = (current + direction % count + count) % count;
            _inventoryActorSlot = InventoryActorSlotAtActiveIndex(next);
            ClampInventorySelection(_inventoryOffset);
        }

        // Slots are fixed to the red and blue collar familiars, while cycling
        // decides which of those active actors may be selected.
        private int InventoryActorRow(int slot)
        {
            if (slot == 1 && PrimaryFamiliarActiveForInventory) return FamiliarInventoryRow;
            if (slot == 2 && BlueFamiliarActiveForInventory)
            {
                int blue = _bagFamiliars.IndexOf(_blueCollarFamiliar);
                return blue >= 0 ? FamiliarInventoryRow - blue - 1 : -1;
            }
            return -1;
        }

        private int InventoryActorCollar(int slot) => slot switch
        {
            1 when PrimaryFamiliarActiveForInventory && InventoryCollarActor(1) != null => 1,
            2 when BlueFamiliarActiveForInventory && InventoryCollarActor(2) != null => 2,
            _ => 0
        };

        private static string FamiliarAiLabel(FamiliarAi ai) => ai switch
        {
            FamiliarAi.Wait => T("Stay still", "Ficar parado"),
            FamiliarAi.Follow => T("Follow", "Seguir"),
            FamiliarAi.Support => T("Support with magic", "Usar magia de apoio"),
            FamiliarAi.Direct => T("Attack directly", "Atacar diretamente"),
            _ => T("Attack aggressively", "Atacar agressivamente")
        };

        private bool IsFamiliarLost(ActorRecord actor)
        {
            if (_area != Area.Tower || actor == null) return false;
            if (actor == _familiarActor && _familiar) return IsFamiliarDistant();
            return _blueCollarAlly?.View && actor == _blueCollarAlly.Actor &&
                Distance(_playerCell, _blueCollarAlly.Cell) > FamiliarLostDistance;
        }

        private Vector2Int FormationWorldOffset(Vector2Int screen) => Vector2Int.RoundToInt(CameraRelativeMovement(screen));

        private void OpenFamiliarOrderPicker(FamiliarOrders orders)
        {
            if (_familiarOrderChoice == FusionOrderIndex)
            {
                if (_fusionPendingSecond)
                {
                    // Second familiar selected: build the fusion preview from both familiars
                    var redFamiliar = _fusionFirst;
                    var blueFamiliar = FindBagFamiliar(orders.Target);
                    if (redFamiliar != null && blueFamiliar != null)
                    {
                        BuildFusionPreview(redFamiliar, blueFamiliar);
                        _fusionPendingSecond = false;
                        _fusionFirst = null;
                        _fusionMessage = null;
                        _familiarOrderPicker = FamiliarOrderPicker.Fusion;
                        _fusionSecond = null;
                        return;
                    }
                    // Fallback: if BagFamiliar not found, reset and exit
                    _fusionFirst = null;
                    _fusionSecond = null;
                    _fusionPendingSecond = false;
                    _fusionMessage = null;
                    return;
                }
                else
                {
                    // First familiar selected: store the BagFamiliar object
                    var familiar = FindBagFamiliar(orders.Target);
                    if (familiar != null)
                        _fusionFirst = familiar;
                    _fusionSecond = null;
                    _fusionMessage = null;
                    return;
                }
            }
            if (_familiarOrderChoice == 1)
            {
                _familiarAiCursor = _inventoryActorSlot == 1 && _familiarWaiting ? FamiliarAi.Wait : orders.Ai;
                _familiarOrderPicker = FamiliarOrderPicker.Ai;
                return;
            }
            var actor = InventoryCollarActor(_inventoryActorSlot == 2 ? 2 : 1);
            if (actor != null) OpenFamiliarFormation(actor);
        }

        private void SetFamiliarAi(ActorRecord actor, FamiliarAi ai)
        {
            var orders = OrdersFor(actor);
            orders.Ai = ai;
            orders.Target = null;
            if (actor == _familiarActor) _familiarWaiting = false;
            _familiarOrderPicker = FamiliarOrderPicker.None;
        }

        private void SetFamiliarFormation(ActorRecord actor, Vector2Int offset)
        {
            if (IsFamiliarLost(actor) || !FormationPositionAvailable(actor, offset)) return;
            int world = System.Array.IndexOf(FormationDirections, FormationWorldOffset(offset));
            bool blue = actor == _blueCollarAlly?.Actor;
            TownSaveVariables.SetInt(blue ? BlueFormationKey : RedFormationKey, (world - KohFormationFacing) & 7);
            OrdersFor(actor).Offset = CollarFormationOffset(actor);
            PlayRetailSound(RetailSfx.Confirm);
            _familiarOrderPicker = FamiliarOrderPicker.None;
        }

        private void HandleFamiliarOrders(ActorRecord actor, int horizontal, int vertical, bool confirm)
        {
            if (IsFamiliarLost(actor)) return;
            var orders = OrdersFor(actor);
            if (_fusionPendingSecond)
            {
                if (vertical != 0)
                {
                    _familiarOrderChoice = Mathf.Clamp(_familiarOrderChoice + vertical, 0, FamiliarOrderCount - 1);
                    _fusionMessage = null;
                }
                if (confirm)
                {
                    OpenFamiliarOrderPicker(orders);
                }
                return;
            }
            switch (_familiarOrderPicker)
            {
                case FamiliarOrderPicker.None:
                    _familiarOrderChoice = Mathf.Clamp(_familiarOrderChoice + vertical, 0, FamiliarOrderCount - 1);
                    if (confirm) OpenFamiliarOrderPicker(orders);
                    break;
                case FamiliarOrderPicker.Formation:
                    var delta = new Vector2Int(horizontal, -vertical);
                    var next = _formationCursor + delta;
                    if (next == Vector2Int.zero) next += delta;
                    if (Mathf.Abs(next.x) <= 1 && Mathf.Abs(next.y) <= 1 && next != Vector2Int.zero &&
                        FormationPositionAvailable(actor, next) && next != _formationCursor)
                    {
                        _formationCursor = next;
                        PlayRetailSound(RetailSfx.Cursor);
                    }
                    if (confirm && _formationCursor != Vector2Int.zero) SetFamiliarFormation(actor, _formationCursor);
                    break;
                case FamiliarOrderPicker.Ai:
                    if (ReadFamiliarAiNumber(UnityEngine.InputSystem.Keyboard.current) is int number && number != 0)
                    {
                        SetFamiliarAi(actor, (FamiliarAi)number);
                        break;
                    }
                    if (vertical != 0 || horizontal != 0)
                        _familiarAiCursor = (FamiliarAi)Mathf.Clamp((int)_familiarAiCursor + (vertical != 0 ? vertical : horizontal), 1, 5);
                    if (confirm) SetFamiliarAi(actor, _familiarAiCursor);
                    break;
                case FamiliarOrderPicker.Fusion:
                    if (confirm) ConfirmFamiliarFusion();
                    break;
            }
        }

        private void DrawInventoryCollarOrders(Rect panel, Rect stats, ActorRecord actor)
        {
            bool blue = actor == _blueCollarAlly?.Actor;
            bool frogBlocksFusion = _familiarOrderPicker == FamiliarOrderPicker.Fusion && FusionBlockedByFrog;
            if (_familiarOrderPicker != FamiliarOrderPicker.Fusion || _fusionPreview == null || frogBlocksFusion)
                DrawActorStatus(stats, blue ? _blueCollarFamiliar.Name : FamiliarDisplayName, actor, true,
                    blue ? actor.Hp : _familiarHealth, actor.MaxHp, actor.CurrentAttack, actor.CurrentDefense);
            DrawInventoryText(new Rect(stats.x + 10f, stats.yMax + 6f, stats.width - 20f, 23f),
                _familiarOrderPicker == FamiliarOrderPicker.None
                    ? InventoryCharacterHint
                    : WithInteractHint("Confirm", "Confirmar") + "   " + WithCancelHint("Back", "Voltar"), _messageStyle);

            var orders = OrdersFor(actor);
            if (_familiarOrderPicker == FamiliarOrderPicker.Fusion)
            {
                DrawInventoryText(new Rect(panel.x + 10f, panel.y + 7f, panel.width - 20f, 23f),
                    T("Fusion", "Fusão"), _bodyStyle);
                bool bothActive = _familiar && _blueCollarAlly?.View;
                string details = frogBlocksFusion ? FrogFusionMessage() :
                    _fusionPreview != null
                        ? _fusionFirst.Definition.DisplayName + " + " + _fusionSecond.Definition.DisplayName + "\n" +
                          T("The other familiar will be absorbed.", "O outro familiar será absorvido.")
                        : _fusionPendingSecond
                            ? (_fusionFirst != null ? _fusionFirst.Definition.DisplayName : T("Select first familiar first.", "Selecione o primeiro familiar."))
                            : bothActive ? T("These familiars cannot fuse.", "Esses familiares não podem se fundir.") :
                                T("Summon both familiars first.", "Invoque os dois familiares primeiro.");
                DrawInventoryText(new Rect(panel.x + 10f, panel.y + 38f, panel.width - 20f, 52f), details, _messageStyle);
                if (_fusionMessage != null)
                    DrawInventoryText(new Rect(panel.x + 10f, panel.y + 93f, panel.width - 20f, 38f), _fusionMessage, _messageStyle);
                if (!frogBlocksFusion && _fusionPreview is { } preview)
                {
                    var result = preview.Result;
                    DrawFamiliarStatus(stats, preview.Name, result, result.Hp, result.MaxHp,
                        result.CurrentAttack, result.CurrentDefense);
                    DrawFusionResultStatsChart(new Rect(stats.x + 5f, stats.y + 142f, stats.width - 10f, 58f), result);
                    DrawInventoryText(new Rect(panel.x + 10f, panel.y + 93f, panel.width - 20f, 38f),
                        DescribeTalents(result), _messageStyle);
                    if (DrawInventoryButton(new Rect(panel.x + 10f, panel.yMax - 69f, panel.width - 20f, 26f),
                        T("Confirm fusion", "Confirmar fusão")))
                        ConfirmFamiliarFusion();
                }
                if (DrawInventoryButton(new Rect(panel.x + 10f, panel.yMax - 38f, panel.width - 20f, 26f), T("Back", "Voltar")))
                {
                    _fusionPreview = null;
                    _familiarOrderPicker = FamiliarOrderPicker.None;
                }
                return;
            }
            if (_familiarOrderPicker == FamiliarOrderPicker.Formation)
            {
                DrawInventoryText(new Rect(panel.x + 10f, panel.y + 7f, panel.width - 20f, 23f),
                    T("Position", "Posição"), _bodyStyle);
                for (int y = 1; y >= -1; y--)
                    for (int x = -1; x <= 1; x++)
                    {
                        var offset = new Vector2Int(x, y);
                        var tile = new Rect(panel.center.x + x * 49f - 22f, panel.y + 42f + (1 - y) * 44f, 44f, 39f);
                        if (offset == Vector2Int.zero)
                        {
                            DrawDialogueFrame(tile);
                            DrawInventoryText(new Rect(tile.x + 4f, tile.y, tile.width - 8f, tile.height), "Koh", _bodyStyle);
                        }
                        else
                        {
                            bool available = FormationPositionAvailable(actor, offset);
                            bool enabled = GUI.enabled;
                            GUI.enabled = enabled && available && !IsFamiliarLost(actor);
                            bool clicked = DrawInventoryButton(tile, "", offset == _formationCursor);
                            GUI.enabled = enabled;
                            if (Event.current.type == EventType.Repaint && (!available || offset == _formationCursor))
                            {
                                int slot = (available ? blue : !blue) ? BlueCollarIconSlot : RedCollarIconSlot;
                                var icon = SuppliedItemSprite(GroundIconNames[slot]);
                                if (!icon) icon = RetailGroundItemSprite(slot);
                                if (icon) DrawItemSprite(new Rect(tile.center.x - 12f, tile.center.y - 12f, 24f, 24f), icon);
                            }
                            if (clicked) SetFamiliarFormation(actor, offset);
                        }
                    }
                return;
            }
            if (_familiarOrderPicker == FamiliarOrderPicker.Ai)
            {
                DrawInventoryText(new Rect(panel.x + 10f, panel.y + 7f, panel.width - 20f, 23f),
                    T("Change AI", "Mudar IA"), _bodyStyle);
                for (int i = 1; i <= 5; i++)
                {
                    var ai = (FamiliarAi)i;
                    string label = $"{i}  {FamiliarAiLabel(ai)}";
                    if (DrawInventoryButton(new Rect(panel.x + 10f, panel.y + 34f + (i - 1) * 29f, panel.width - 20f, 26f), label,
                        ai == _familiarAiCursor))
                        SetFamiliarAi(actor, ai);
                }
                return;
            }

            DrawInventoryText(new Rect(panel.x + 10f, panel.y + 7f, panel.width - 46f, 23f),
                blue ? T("Familiar 2", "Familiar 2") : T("Familiar 1", "Familiar 1"), _bodyStyle);
            if (Event.current.type == EventType.Repaint)
            {
                int slot = blue ? BlueCollarIconSlot : RedCollarIconSlot;
                var icon = SuppliedItemSprite(GroundIconNames[slot]);
                if (!icon) icon = RetailGroundItemSprite(slot);
                if (icon) DrawItemSprite(new Rect(panel.xMax - 31f, panel.y + 8f, 20f, 20f), icon);
            }
            int orderCount = FamiliarOrderCount;
            for (int i = 0; i < orderCount; i++)
            {
                string label = i switch
                {
                    0 => T("Position", "Posição"),
                    1 => T("Change AI", "Mudar IA"),
                    _ => T("Fusion", "Fusão")
                };
                if (DrawInventoryButton(new Rect(panel.x + 10f, panel.y + 37f + i * 30f, panel.width - 20f, 26f), label,
                    i == _familiarOrderChoice))
                {
                    _familiarOrderChoice = i;
                    OpenFamiliarOrderPicker(orders);
                }
            }
            DrawInventoryText(new Rect(panel.x + 10f, panel.y + 44f + orderCount * 30f, panel.width - 20f, 38f),
                FamiliarAiLabel(actor == _familiarActor && _familiarWaiting ? FamiliarAi.Wait : orders.Ai), _messageStyle);
        }

        private (Vector2Int Step, int Attack) PlanFamiliarTurn(ActorRecord actor, GameObject view, Vector2Int from,
            Vector2Int kohDestination, Vector2Int? reserved, Vector2Int? otherDestination, bool executeFamiliarCommands)
        {
            // Plan against Koh's completed step, including distance, support targets and
            // spell origins. Restore the snapshot before resolving movement conflicts.
            var previousCell = _playerCell;
            int previousX = _playerActor.TileX, previousY = _playerActor.TileY;
            try
            {
                _playerCell = kohDestination;
                SetActorCell(_playerActor, kohDestination);
                return PlanFamiliarAfterKoh(actor, view, from, kohDestination, reserved, otherDestination, executeFamiliarCommands);
            }
            finally
            {
                _playerCell = previousCell;
                _playerActor.TileX = (sbyte)previousX;
                _playerActor.TileY = (sbyte)previousY;
            }
        }

        private (Vector2Int Step, int Attack) PlanFamiliarAfterKoh(ActorRecord actor, GameObject view, Vector2Int from,
            Vector2Int kohDestination, Vector2Int? reserved, Vector2Int? otherDestination, bool executeFamiliarCommands)
        {
            if (IsSubmerged(actor) && (MonsterExhausted(actor) || IsFrog(actor)))
            {
                Surface(actor, from);
                return (Vector2Int.zero, -1);
            }
            if (executeFamiliarCommands && !IsFamiliarLost(actor) && _pendingFamiliarCommands.TryGetValue(actor, out var manual))
                return PlanManualFamiliarOrder(actor, view, from, manual.Direction, manual.Order, kohDestination, reserved, otherDestination);
            var orders = OrdersFor(actor);
            bool staysStill = orders.Ai == FamiliarAi.Wait || (actor == _familiarActor && _familiarWaiting);
            bool isLost = IsFamiliarLost(actor);
            if (isLost) _pendingFamiliarCommands.Remove(actor);
            if (MonsterExhausted(actor) || ItemImmobilized(actor) || SlowBlocksAction(actor) ||
                (actor == _familiarActor && _landingFamiliar)) return (Vector2Int.zero, -1);
            if (isLost)
            {
                // A lost familiar cannot be given orders or cast support magic. It still
                // defends itself when a monster reaches an adjacent, connected tile.
                int enemy = _creatures.FindIndex(entry => entry.Health > 0 && !IsSubmerged(entry.Actor) &&
                    Distance(from, entry.Cell) == 1 && FamiliarHasSight(from, entry.Cell));
                if (enemy >= 0) return (Vector2Int.zero, enemy);
                if (staysStill) return (Vector2Int.zero, -1);

                // Every non-wait AI searches for Koh. The path routine settles on the
                // nearest legal cell next to Koh because Koh's own tile is occupied.
                var seekKoh = FindFamiliarOrderStep(actor, from, kohDestination, kohDestination,
                    reserved, otherDestination, false);
                return (seekKoh, -1);
            }
            if (staysStill) return (Vector2Int.zero, -1);
            if (orders.Ai == FamiliarAi.Support && !IsSubmerged(actor) && TryFamiliarSupportMagic(actor, view, from))
                return (Vector2Int.zero, -1);
            if (orders.Ai is FamiliarAi.Direct or FamiliarAi.Aggressive && !IsFrog(actor))
            {
                bool aggressive = orders.Ai == FamiliarAi.Aggressive;
                int enemy = aggressive && orders.Target != null
                    ? _creatures.FindIndex(entry => entry.Actor == orders.Target && entry.Health > 0 && !IsSubmerged(entry.Actor)) : -1;
                if (enemy < 0)
                {
                    orders.Target = null;
                    int nearest = aggressive ? int.MaxValue : 3;
                    for (int i = 0; i < _creatures.Count; i++)
                    {
                        var candidate = _creatures[i];
                        int distance = Distance(from, candidate.Cell);
                        if (candidate.Health <= 0 || IsSubmerged(candidate.Actor) || distance >= nearest ||
                            !FamiliarHasSight(from, candidate.Cell)) continue;
                        nearest = distance;
                        enemy = i;
                    }
                    if (aggressive && enemy >= 0) orders.Target = _creatures[enemy].Actor;
                }
                if (enemy >= 0)
                {
                    var target = _creatures[enemy].Cell;
                    if ((Distance(from, target) == 1 && HasTowerPassage(from, target - from)) ||
                        FindFamiliarBowGunTarget(actor, from) == enemy)
                    {
                        if (!IsSubmerged(actor)) return (Vector2Int.zero, enemy);
                        Surface(actor, from);
                        return (Vector2Int.zero, -1);
                    }
                    var approach = FindFamiliarOrderStep(actor, from, target, kohDestination, reserved, otherDestination, true);
                    if (approach != Vector2Int.zero) return (approach, -1);
                }
            }
            return (FindFamiliarOrderStep(actor, from, kohDestination + FamiliarFormationTargetOffset(actor),
                kohDestination, reserved, otherDestination, false), -1);
        }

        private bool FamiliarOrderCellOpen(ActorRecord actor, Vector2Int cell, Vector2Int kohDestination,
            Vector2Int? reserved, Vector2Int? otherDestination) =>
            cell != kohDestination && cell != reserved && cell != otherDestination && cell != _stairCell &&
            (!_familiar || actor == _familiarActor || cell != _familiarCell) && CreatureAt(cell) < 0 &&
            !_thrownEggs.Exists(ally => ally.Actor != actor && ally.View && ally.Cell == cell) &&
            !IsRevealedActiveTrap(cell);

        // A revealed active trap is known danger for a familiar. Hidden traps and
        // disabled traps stay traversable, preserving the normal exploration rules.
        private bool IsRevealedActiveTrap(Vector2Int cell) =>
            TrapAt(cell) is { IsRevealed: true, IsActive: true };

        private Vector2Int FindFamiliarOrderStep(ActorRecord actor, Vector2Int from, Vector2Int target,
            Vector2Int kohDestination, Vector2Int? reserved, Vector2Int? otherDestination, bool approachingEnemy)
        {
            _familiarPath.Clear();
            _familiarVisited.Clear();
            _familiarPath.Enqueue((from, Vector2Int.zero));
            _familiarVisited.Add(from);
            int bestDistance = int.MaxValue;
            Vector2Int fallback = Vector2Int.zero;
            while (_familiarPath.Count > 0)
            {
                var (cell, first) = _familiarPath.Dequeue();
                if (approachingEnemy)
                {
                    if (Distance(cell, target) == 1 && HasTowerPassage(cell, target - cell)) return first;
                }
                else
                {
                    if (cell == target) return first;
                    var relative = cell - kohDestination;
                    int distance = Distance(cell, target);
                    if (Mathf.Abs(relative.x) <= 1 && Mathf.Abs(relative.y) <= 1 && relative != Vector2Int.zero &&
                        distance < bestDistance)
                    {
                        bestDistance = distance;
                        fallback = first;
                    }
                }
                foreach (var step in FormationDirections)
                {
                    var next = cell + step;
                    if (_familiarVisited.Contains(next) || !CanTowerStep(cell, step) ||
                        !FamiliarOrderCellOpen(actor, next, kohDestination, reserved, otherDestination)) continue;
                    _familiarVisited.Add(next);
                    _familiarPath.Enqueue((next, cell == from ? step : first));
                }
            }
            return fallback;
        }

        private bool FamiliarHasSight(Vector2Int from, Vector2Int to, bool clearShot = false)
        {
            var delta = to - from;
            int dx = Mathf.Abs(delta.x), dy = Mathf.Abs(delta.y);
            int sx = System.Math.Sign(delta.x), sy = System.Math.Sign(delta.y);
            int error = dx - dy;
            while (from != to)
            {
                int twice = 2 * error;
                var step = Vector2Int.zero;
                if (twice > -dy) { error -= dy; step.x = sx; }
                if (twice < dx) { error += dx; step.y = sy; }
                if (!HasTowerPassage(from, step) || (step.x != 0 && step.y != 0 &&
                    (!HasTowerPassage(from, new Vector2Int(step.x, 0)) || !HasTowerPassage(from, new Vector2Int(0, step.y)))))
                    return false;
                from += step;
                if (clearShot && from != to && BellCellOccupied(from)) return false;
            }
            return true;
        }

        // Tries every known spell (up to three, including ones learned from balls) in slot order.
        private bool TryFamiliarSupportMagic(ActorRecord actor, GameObject view, Vector2Int from)
        {
            if (!CanCastMagic(actor)) return false;
            foreach (var known in FamiliarSpellList(actor))
                if (TryFamiliarSupportSpell(actor, view, from, known.Spell)) return true;
            return false;
        }

        private bool TryFamiliarSupportSpell(ActorRecord actor, GameObject view, Vector2Int from, int spell)
        {
            int cost = NativeSpellCost(actor, spell);
            if (cost == 0 || actor.MpRaw < cost && !_mpProtection.ContainsKey(actor)) return false;
            int family = SpellFamily(spell);
            if (family == 5 && !Combat.HasStatus(actor, WallProtectionStatus(spell)))
                return Cast(actor, from);
            if (family == 6 && _reflectionTurns == 0) return Cast(actor, from);
            if (family is 8 or 9)
            {
                if (_health > 0 && _health < KohMaxHealth && Distance(from, _playerCell) <= 4 &&
                    FamiliarHasSight(from, _playerCell, true)) return Cast(_playerActor, _playerCell);
                if (actor != _familiarActor && _familiar && _familiarHealth > 0 && _familiarHealth < FamiliarMaxHealth &&
                    Distance(from, _familiarCell) <= 4 && FamiliarHasSight(from, _familiarCell, true))
                    return Cast(_familiarActor, _familiarCell);
                var blue = _blueCollarAlly;
                if (blue?.View && actor != blue.Actor && blue.Actor.Hp > 0 && blue.Actor.Hp < blue.Actor.MaxHp &&
                    Distance(from, blue.Cell) <= 4 && FamiliarHasSight(from, blue.Cell, true))
                    return Cast(blue.Actor, blue.Cell);
                if (actor.Hp < actor.MaxHp) return Cast(actor, from);
                return false;
            }
            int power = DirectSpellPower(spell);
            sbyte status = DirectSpellStatus(spell);
            if (power == 0 && status == 0 && family != 13) return false;
            int nearest = 5, enemy = -1;
            for (int i = 0; i < _creatures.Count; i++)
            {
                var candidate = _creatures[i];
                int distance = Distance(from, candidate.Cell);
                if (candidate.Health <= 0 || distance >= nearest ||
                    (family == 13 && candidate.Actor.Level <= 1) ||
                    (status != 0 && Combat.HasStatus(candidate.Actor, status)) ||
                    !FamiliarHasSight(from, candidate.Cell, true)) continue;
                nearest = distance;
                enemy = i;
            }
            return enemy >= 0 && Cast(_creatures[enemy].Actor, _creatures[enemy].Cell);

            bool Cast(ActorRecord target, Vector2Int cell)
            {
                if (!SpendMonsterMp(actor, cost)) return false;
                var direction = cell == from ? _towerFacing : cell - from;
                QueueMonsterSpriteAction(view, direction, casting: true);
                int element = UnitElement(actor);
                QueueSpellCue(spell, element, from, cell);
                int level = FamiliarSpellLevel(actor, spell);
                _spellImpactAction += () => ResolveCrystalBall(spell, element, actor, target, from, cell, direction, from, level);
                return true;
            }
        }
    }
}
