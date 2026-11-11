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
        private Vector3 _blueFamiliarFrom;

        private ActorRecord InventoryCollarActor(int slot) => _area != Area.Tower ? null : slot switch
        {
            1 when _familiar || _carriedFamiliar => _familiarActor,
            2 when _blueCollarAlly?.View => _blueCollarAlly.Actor,
            _ => null
        };

        private FamiliarOrders OrdersFor(ActorRecord actor)
        {
            if (_familiarOrders.TryGetValue(actor, out var orders)) return orders;
            orders = new FamiliarOrders { Offset = actor == _blueCollarAlly?.Actor ? Vector2Int.right : Vector2Int.left };
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

        private void CycleInventoryActor(int direction)
        {
            _familiarStatus = false;
            _familiarOrderChoice = 0;
            _familiarOrderPicker = FamiliarOrderPicker.None;
            int count = FamiliarInventoryCount + 1;
            _inventoryActorSlot = (Mathf.Clamp(_inventoryActorSlot, 0, count - 1) + direction + count) % count;
            ClampInventorySelection(_inventoryOffset);
        }

        // Status pages include the entire bag. Put the two collared familiars first.
        private int InventoryActorRow(int slot)
        {
            if (slot <= 0 || slot > FamiliarInventoryCount) return -1;
            if (_familiarDefinition && slot-- == 1) return FamiliarInventoryRow;
            int blue = _blueCollarAlly?.View ? _bagFamiliars.IndexOf(_blueCollarFamiliar) : -1;
            if (blue >= 0)
            {
                if (slot-- == 1) return FamiliarInventoryRow - blue - 1;
                if (slot - 1 >= blue) slot++;
            }
            return FamiliarInventoryRow - slot;
        }

        private int InventoryActorCollar(int slot)
        {
            int row = InventoryActorRow(slot);
            if (row == FamiliarInventoryRow && InventoryCollarActor(1) != null) return 1;
            if (row < FamiliarInventoryRow && InventoryCollarActor(2) != null &&
                ReserveForRow(row) == _blueCollarFamiliar) return 2;
            return 0;
        }

        private static string FamiliarAiLabel(FamiliarAi ai) => ai switch
        {
            FamiliarAi.Wait => T("Stay still", "Ficar parado"),
            FamiliarAi.Follow => T("Follow", "Seguir"),
            FamiliarAi.Support => T("Support with magic", "Usar magia de apoio"),
            FamiliarAi.Direct => T("Attack directly", "Atacar diretamente"),
            _ => T("Attack aggressively", "Atacar agressivamente")
        };

        private Vector2Int FormationWorldOffset(Vector2Int screen) => Vector2Int.RoundToInt(CameraRelativeMovement(screen));

        private void OpenFamiliarOrderPicker(FamiliarOrders orders)
        {
            if (_familiarOrderChoice == FusionOrderIndex)
            {
                if (HasBlueCollar)
                {
                    _fusionPreview = BuildFusionPreview();
                    _familiarOrderPicker = FamiliarOrderPicker.Fusion;
                }
                return;
            }
            if (_familiarOrderChoice == 1)
            {
                _familiarAiCursor = _inventoryActorSlot == 1 && _familiarWaiting ? FamiliarAi.Wait : orders.Ai;
                _familiarOrderPicker = FamiliarOrderPicker.Ai;
                return;
            }
            _familiarOrderPicker = FamiliarOrderPicker.Formation;
            _formationCursor = Vector2Int.left;
            for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    var option = new Vector2Int(x, y);
                    if (FormationWorldOffset(option) == orders.Offset) _formationCursor = option;
                }
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
            OrdersFor(actor).Offset = FormationWorldOffset(offset);
            _familiarOrderPicker = FamiliarOrderPicker.None;
        }

        private void HandleFamiliarOrders(ActorRecord actor, int horizontal, int vertical, bool confirm)
        {
            var orders = OrdersFor(actor);
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
                    if (Mathf.Abs(next.x) <= 1 && Mathf.Abs(next.y) <= 1) _formationCursor = next;
                    if (confirm && _formationCursor != Vector2Int.zero) SetFamiliarFormation(actor, _formationCursor);
                    break;
                case FamiliarOrderPicker.Ai:
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
            DrawHudText(new Rect(stats.x + 10f, stats.yMax + 6f, stats.width - 20f, 23f),
                _familiarOrderPicker == FamiliarOrderPicker.None
                    ? T("PgUp/PgDn: character", "PgUp/PgDn: personagem")
                    : T("E: confirm   Esc: back", "E: confirmar   Esc: voltar"), _messageStyle);

            var orders = OrdersFor(actor);
            if (_familiarOrderPicker == FamiliarOrderPicker.Fusion)
            {
                DrawHudText(new Rect(panel.x + 10f, panel.y + 7f, panel.width - 20f, 23f),
                    T("Fusion", "Fusão"), _bodyStyle);
                bool bothActive = _familiar && _blueCollarAlly?.View;
                string details = frogBlocksFusion ? FrogFusionMessage() : _fusionPreview != null
                    ? FamiliarDisplayName + " + " + _blueCollarFamiliar.Name + "\n" +
                        T("The other familiar will be absorbed.", "O outro familiar será absorvido.")
                    : bothActive ? T("These familiars cannot fuse.", "Esses familiares não podem se fundir.") :
                        T("Summon both familiars first.", "Invoque os dois familiares primeiro.");
                DrawHudText(new Rect(panel.x + 10f, panel.y + 38f, panel.width - 20f, 52f), details, _messageStyle);
                if (!frogBlocksFusion && _fusionPreview is { } preview)
                {
                    var result = preview.Result;
                    DrawFamiliarStatus(stats, preview.Name, result, result.Hp, result.MaxHp,
                        result.CurrentAttack, result.CurrentDefense);
                    DrawHudText(new Rect(panel.x + 10f, panel.y + 93f, panel.width - 20f, 38f),
                        DescribeTalents(result), _messageStyle);
                    if (GUI.Button(new Rect(panel.x + 10f, panel.yMax - 69f, panel.width - 20f, 26f),
                        T("Confirm fusion", "Confirmar fusão")))
                        ConfirmFamiliarFusion();
                }
                if (GUI.Button(new Rect(panel.x + 10f, panel.yMax - 38f, panel.width - 20f, 26f), T("Back", "Voltar")))
                {
                    _fusionPreview = null;
                    _familiarOrderPicker = FamiliarOrderPicker.None;
                }
                return;
            }
            if (_familiarOrderPicker == FamiliarOrderPicker.Formation)
            {
                DrawHudText(new Rect(panel.x + 10f, panel.y + 7f, panel.width - 20f, 23f),
                    T("Change position", "Mudar posicionamento"), _bodyStyle);
                for (int y = 1; y >= -1; y--)
                    for (int x = -1; x <= 1; x++)
                    {
                        var offset = new Vector2Int(x, y);
                        var tile = new Rect(panel.center.x + x * 49f - 22f, panel.y + 42f + (1 - y) * 44f, 44f, 39f);
                        if (offset == Vector2Int.zero) GUI.Box(tile, "Koh");
                        else if (GUI.Button(tile, offset == _formationCursor ? "●" : "·")) SetFamiliarFormation(actor, offset);
                    }
                return;
            }
            if (_familiarOrderPicker == FamiliarOrderPicker.Ai)
            {
                DrawHudText(new Rect(panel.x + 10f, panel.y + 7f, panel.width - 20f, 23f),
                    T("Change AI", "Mudar IA"), _bodyStyle);
                for (int i = 1; i <= 5; i++)
                {
                    var ai = (FamiliarAi)i;
                    string label = $"{i}  {FamiliarAiLabel(ai)}";
                    if (ai == _familiarAiCursor) label = "▸ " + label;
                    if (GUI.Button(new Rect(panel.x + 10f, panel.y + 34f + (i - 1) * 29f, panel.width - 20f, 26f), label))
                        SetFamiliarAi(actor, ai);
                }
                return;
            }

            DrawHudText(new Rect(panel.x + 10f, panel.y + 7f, panel.width - 46f, 23f),
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
                    0 => T("Change position", "Mudar posicionamento"),
                    1 => T("Change AI", "Mudar IA"),
                    _ => T("Fusion", "Fusão")
                };
                if (i == _familiarOrderChoice) label = "▸ " + label;
                if (GUI.Button(new Rect(panel.x + 10f, panel.y + 37f + i * 30f, panel.width - 20f, 26f), label))
                {
                    _familiarOrderChoice = i;
                    OpenFamiliarOrderPicker(orders);
                }
            }
            DrawHudText(new Rect(panel.x + 10f, panel.y + 44f + orderCount * 30f, panel.width - 20f, 38f),
                FamiliarAiLabel(actor == _familiarActor && _familiarWaiting ? FamiliarAi.Wait : orders.Ai), _messageStyle);
        }

        private (Vector2Int Step, int Attack) PlanFamiliarTurn(ActorRecord actor, GameObject view, Vector2Int from,
            Vector2Int kohDestination, Vector2Int? reserved, Vector2Int? otherDestination, bool executeFamiliarCommands)
        {
            if (IsSubmerged(actor) && (MonsterExhausted(actor) || IsFrog(actor)))
            {
                Surface(actor, from);
                return (Vector2Int.zero, -1);
            }
            if (executeFamiliarCommands && _pendingFamiliarCommands.TryGetValue(actor, out var manual))
                return PlanManualFamiliarOrder(actor, view, from, manual.Direction, manual.Order, kohDestination, reserved, otherDestination);
            if (MonsterExhausted(actor) || ItemImmobilized(actor) || SlowBlocksAction(actor) ||
                (actor == _familiarActor && (_familiarWaiting || _landingFamiliar))) return (Vector2Int.zero, -1);
            var orders = OrdersFor(actor);
            if (orders.Ai == FamiliarAi.Wait) return (Vector2Int.zero, -1);
            if (Combat.HasStatus(actor, StatusId.BLIND) || Combat.HasStatus(actor, StatusId.CONFUSION))
            {
                var random = RandomTowerStep(from);
                return (FamiliarOrderCellOpen(actor, from + random, kohDestination, reserved, otherDestination) ?
                    random : Vector2Int.zero, -1);
            }
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
            return (FindFamiliarOrderStep(actor, from, kohDestination + orders.Offset,
                kohDestination, reserved, otherDestination, false), -1);
        }

        private bool FamiliarOrderCellOpen(ActorRecord actor, Vector2Int cell, Vector2Int kohDestination,
            Vector2Int? reserved, Vector2Int? otherDestination) =>
            cell != kohDestination && cell != reserved && cell != otherDestination && cell != _stairCell &&
            (!_familiar || actor == _familiarActor || cell != _familiarCell) && CreatureAt(cell) < 0 &&
            !_thrownEggs.Exists(ally => ally.Actor != actor && ally.View && ally.Cell == cell);

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
                foreach (var step in BellSteps)
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

        private bool TryFamiliarSupportMagic(ActorRecord actor, GameObject view, Vector2Int from)
        {
            if (!CanCastMagic(actor)) return false;
            var profile = UnitProfile(actor.Species);
            int spell = actor.U8(8);
            if (spell == 0) spell = profile?.nativeSpellId ?? 0;
            if (spell == 0) spell = profile?.hiddenSpellId ?? 0;
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
                int level = actor.U8(8) == spell ? Mathf.Max(1, actor.U8(9)) : actor.Level;
                _spellImpactAction += () => ResolveCrystalBall(spell, element, actor, target, from, cell, direction, from, level);
                return true;
            }
        }
    }
}
