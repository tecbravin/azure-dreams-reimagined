using System;
using System.Collections.Generic;
using AzureDreams.Core;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const byte RockSpecies = 0x36;
        private const int RockMpCost = 4 * 256;
        private const int GlacierWallMpCost = 8 * 256;
        private const int IceWallSpecies = 52;
        private const int SpeciesSpawnLimit = 32;
        private readonly Dictionary<ActorRecord, bool> _speciesAiRolls = new();
        private readonly Dictionary<ActorRecord, (GameObject View, Renderer[] Renderers)> _submergedActors = new();
        private readonly List<(ActorRecord Actor, Vector2Int Cell)> _pendingRocks = new();
        private readonly HashSet<ActorRecord> _manoevaCopies = new();
        private string _speciesActionMessages;

        private static bool HasSpeciesCommand(ActorRecord actor) =>
            actor != null && (IsSpeciesObstacleCaster(actor) || actor.Species == (byte)MonsterSpecies.U_BOAT);

        private static bool IsSpeciesObstacleCaster(ActorRecord actor) =>
            actor != null && (MonsterSpecies)actor.Species is MonsterSpecies.VOLCANO or MonsterSpecies.GLACIER;

        private string SpeciesCommandLabel(ActorRecord actor) => (MonsterSpecies)actor.Species switch
        {
            MonsterSpecies.U_BOAT => IsSubmerged(actor) ? T("Surface", "Emergir") : T("Dive", "Mergulhar"),
            MonsterSpecies.VOLCANO => T("Create Rock", "Criar pedra"),
            MonsterSpecies.GLACIER => T("Create Ice Wall", "Criar parede de gelo"),
            _ => string.Empty
        };

        private bool IsSubmerged(ActorRecord actor) =>
            actor != null && _submergedActors.TryGetValue(actor, out var dive) && dive.View;

        private static int SpeciesDistance(Vector2Int from, Vector2Int to) =>
            Mathf.Max(Mathf.Abs(from.x - to.x), Mathf.Abs(from.y - to.y));

        private bool RollSpeciesAiOnce(ActorRecord actor, int divisor)
        {
            if (_speciesAiRolls.TryGetValue(actor, out bool enabled)) return enabled;
            enabled = RollSpawnRange(divisor) == 0;
            _speciesAiRolls.Add(actor, enabled);
            return enabled;
        }

        private bool TryEnemySpeciesAction(CreatureState creature, bool canAct, bool aware)
        {
            var actor = creature.Actor;
            int distance = SpeciesDistance(creature.Cell, _playerCell);
            if (IsSubmerged(actor))
            {
                if (IsFrog(actor) || MonsterExhausted(actor) || (canAct && distance <= 1))
                {
                    Surface(actor, creature.Cell);
                    return true;
                }
                return false;
            }
            if (!canAct || !aware) return false;
            switch ((MonsterSpecies)actor.Species)
            {
                case MonsterSpecies.U_BOAT when distance >= 2 && RollSpeciesAiOnce(actor, 8):
                    Dive(actor, creature.View, creature.Cell);
                    return true;
                case MonsterSpecies.VOLCANO when distance == 2 && RollSpeciesAiOnce(actor, 32):
                case MonsterSpecies.GLACIER when distance == 2 && RollSpeciesAiOnce(actor, 32):
                    var delta = _playerCell - creature.Cell;
                    var direction = new Vector2Int(Math.Sign(delta.x), Math.Sign(delta.y));
                    return TryQueueSpeciesObstacle(actor, creature.View, creature.Cell, direction);
                default:
                    return false;
            }
        }

        private void Dive(ActorRecord actor, GameObject view, Vector2Int cell)
        {
            var renderers = Array.FindAll(view.GetComponentsInChildren<Renderer>(), renderer => renderer.enabled);
            foreach (var renderer in renderers) renderer.enabled = false;
            _submergedActors[actor] = (view, renderers);
            SpawnFx(_fxMagic, CellPosition(cell));
            var name = TowerCombatName(actor);
            AddSpeciesMessage(T($"{name} dives underground.", $"{name} mergulhou no chão."));
        }

        private void Surface(ActorRecord actor, Vector2Int cell)
        {
            if (!_submergedActors.Remove(actor, out var dive)) return;
            foreach (var renderer in dive.Renderers)
                if (renderer) renderer.enabled = true;
            SpawnFx(_fxMagic, CellPosition(cell));
            var name = TowerCombatName(actor);
            AddSpeciesMessage(T($"{name} surfaces.", $"{name} emergiu."));
        }

        private bool IsOpenSpeciesTile(Vector2Int cell) =>
            InsideMap(cell) && !_walls.Contains(cell) && !_npcCells.Contains(cell) && cell != _stairCell &&
            !BellCellOccupied(cell) && !IsSacredTile(cell) && !_itemMarkers.ContainsKey(cell) &&
            (!_hasLoot || cell != _lootCell);

        private bool CanCreateRockAt(Vector2Int from, Vector2Int direction) =>
            direction != Vector2Int.zero && HasThrowPassage(from, direction) && IsOpenSpeciesTile(from + direction);

        private static int SpeciesObstacleCost(ActorRecord actor) =>
            actor.Species == (byte)MonsterSpecies.GLACIER ? GlacierWallMpCost : RockMpCost;

        private bool TryQueueSpeciesObstacle(ActorRecord actor, GameObject view, Vector2Int from, Vector2Int direction)
        {
            var cell = from + direction;
            int cost = SpeciesObstacleCost(actor) * (_doubleMpCost.ContainsKey(actor) ? 2 : 1);
            if (!CanCreateRockAt(from, direction) ||
                (!_mpProtection.ContainsKey(actor) && actor.MpRaw < cost) ||
                _pendingRocks.Exists(pending => pending.Cell == cell)) return false;
            _pendingRocks.Add((actor, cell));
            QueueMonsterSpriteAction(view, direction, casting: true);
            return true;
        }

        private void ResolveSpeciesObstacles()
        {
            foreach (var (caster, cell) in _pendingRocks)
            {
                if (!IsOpenSpeciesTile(cell)) continue;
                bool ice = caster.Species == (byte)MonsterSpecies.GLACIER;
                if ((!ice && _creatures.Count + _thrownEggs.Count >= SpeciesSpawnLimit) ||
                    !SpendMonsterMp(caster, SpeciesObstacleCost(caster))) continue;
                var name = TowerCombatName(caster);
                if (ice)
                {
                    CreateIceWall(cell);
                    SpawnFx(Resources.Load<GameObject>("FX/FX_Water"), CellPosition(cell) + Vector3.up * 0.5f);
                    AddSpeciesMessage(T($"{name} created a wall of ice.", $"{name} criou uma parede de gelo."));
                    continue;
                }
                var actor = CreateTowerEnemy((MonsterSpecies)RockSpecies, 1, allowSpawnSleep: false);
                actor.MaxHp = actor.Hp = StatByte(actor.MaxHp + caster.Level * 2 + RollSpawnRange(4));
                SetActorCell(actor, cell);
                var view = CreateMarker(cell, "Volcano Rock", _wallMaterial, PrimitiveType.Sphere, 0.78f, 0.39f);
                _creatures.Add(new CreatureState { Actor = actor, Cell = cell, Health = actor.Hp, View = view });
                SpawnFx(_fxMagic, CellPosition(cell));
                AddSpeciesMessage(T($"{name} created a rock.", $"{name} criou uma pedra."));
            }
            _pendingRocks.Clear();
        }

        private void CreateIceWall(Vector2Int cell)
        {
            var prefab = RegisteredMonsterPrefab(IceWallSpecies);
            GameObject view;
            if (prefab) view = Spawn(prefab, cell, "Wall of Ice");
            else
            {
                // The retail sprite is not decoded yet. Keep the ice distinct from the rock and floor.
                var position = CellPosition(cell) + Vector3.up * 0.45f;
                view = CreatePrimitive(PrimitiveType.Cube, "Wall of Ice", position,
                    new Vector3(0.82f, 0.90f, 0.76f), null, false);
                var tint = new MaterialPropertyBlock();
                var iceColor = new Color(0.43f, 0.76f, 0.96f);
                tint.SetColor("_BaseColor", iceColor);
                tint.SetColor("_Color", iceColor);
                view.GetComponent<Renderer>().SetPropertyBlock(tint);
            }
            _walls.Add(cell);
            _iceBlocks.Add((cell, view, 8));
        }

        private void QueueManoevaSplit(ActorRecord actor, int damage)
        {
            if (damage > 0 && actor.Species == (byte)MonsterSpecies.MANOEVA &&
                !IsFrog(actor) && !_manoevaCopies.Contains(actor)) _manoevaCloneCandidates.Add(actor);
        }

        private void ResolveManoevaClones()
        {
            foreach (var source in _manoevaCloneCandidates)
            {
                if (source.Hp == 0 || IsFrog(source) || _creatures.Count + _thrownEggs.Count >= SpeciesSpawnLimit) continue;
                int enemy = _creatures.FindIndex(creature => creature.Actor == source && creature.Health > 0);
                var ally = _thrownEggs.Find(entry => entry.Actor == source && entry.View && entry.Turns > 0);
                bool red = source == _familiarActor && _familiar && _familiarHealth > 0;
                if (enemy < 0 && ally == null && !red) continue;
                if (RollSpawnRange(4) != 0) continue;
                var origin = enemy >= 0 ? _creatures[enemy].Cell : red ? _familiarCell : ally.Cell;
                foreach (var step in BellSteps)
                {
                    var cell = origin + step;
                    if (!CanTowerStep(origin, step) || !IsOpenSpeciesTile(cell)) continue;
                    var species = MonsterSpecies.MANOEVA;
                    ActorRecord copy;
                    if (enemy >= 0)
                    {
                        SpawnManagedCreature(cell, species, levelOverride: 1, allowSpawnSleep: false);
                        copy = _creatures[^1].Actor;
                    }
                    else
                    {
                        copy = CreateTowerEnemy(species, 1, allowSpawnSleep: false);
                        var view = SpawnActor(HatchedFamiliarPrefab((byte)species), cell, "Manoeva copy",
                            species: (int)species, palette: FamiliarPalette((byte)species, source));
                        _thrownEggs.Add(new ThrownEggAlly { Actor = copy, Cell = cell, View = view, Turns = int.MaxValue });
                    }
                    copy.BaseHp = StatByte((source.BaseHp + 1) / 2);
                    copy.MaxHp = StatByte((source.MaxHp + 1) / 2);
                    copy.Hp = StatByte(Mathf.Clamp((source.Hp + 1) / 2, 1, copy.MaxHp));
                    if (enemy >= 0)
                    {
                        var clone = _creatures[^1];
                        clone.Health = copy.Hp;
                        _creatures[^1] = clone;
                    }
                    _manoevaCopies.Add(copy);
                    SpawnFx(_fxMagic, CellPosition(cell));
                    var name = TowerCombatName(source);
                    AddSpeciesMessage(T($"{name} split into two.", $"{name} se dividiu em dois."));
                    break;
                }
            }
            _manoevaCloneCandidates.Clear();
        }

        private void RevealStarGlassesTargets()
        {
            _seeMonsters = _seeElevator = true;
            ActivateSeeAllLoupe();
            RevealTowerTraps();
        }

        private bool TryScoutTowerFloor(ActorRecord actor, GameObject view, Vector2Int direction)
        {
            const int cost = 16 * 256;
            int effectiveCost = _doubleMpCost.ContainsKey(actor) ? cost * 2 : cost;
            if ((!_mpProtection.ContainsKey(actor) && actor.MpRaw < effectiveCost) ||
                !SpendMonsterMp(actor, cost)) return false;
            QueueMonsterSpriteAction(view, direction, casting: true);
            RevealStarGlassesTargets();
            string name = TowerCombatName(actor);
            AddSpeciesMessage(T($"{name} scouted the floor.", $"{name} explorou o andar."));
            return true;
        }

        private bool TryRecycleThrownItem(ActorRecord target, Vector2Int impact)
        {
            if (target == null || target.Species != (byte)MonsterSpecies.BARONG || target.Hp == 0 ||
                target.MpRaw == 0 || IsFrog(target)) return false;

            // Recycle is triggered by the impact even with less than 5 MP remaining.
            Leveling.SpendActorMpRaw(target, 5 * 256);
            var item = RollBarongItem();
            if (DropTowerReward(impact, item))
            {
                SpawnFx(_fxMagic, CellPosition(impact));
                string name = TowerCombatName(target);
                string itemName = InventoryItemName(item);
                AddSpeciesMessage(T($"{name} spat out {itemName}.", $"{name} cuspiu {itemName}."));
            }
            else AddSpeciesMessage(T("There is no room for the recycled item.", "Não há espaço para o item reciclado."));
            return true;
        }

        private DungeonItemEntry RollBarongItem()
        {
            var choices = new List<(byte Kind, byte Id)>();
            foreach (var entry in InventoryText.Entries)
            {
                byte kind = (byte)entry.kind;
                byte id = (byte)entry.id;
                // Ordinary tower categories. Quest items, collars and town merchandise are outside these ranges.
                bool eligible = kind switch
                {
                    ItemKind.Herb => id is >= 1 and <= 14 and not 8,
                    ItemKind.Sword => id is >= 1 and <= 15 and not SeraphimKeepsake and not SeraphimSword,
                    ItemKind.Wand => id is >= 1 and <= 9,
                    ItemKind.Shield => id is >= 1 and <= 11,
                    ItemKind.Fruit or ItemKind.Seed or ItemKind.Ball or ItemKind.Scroll or ItemKind.Crystal or
                    ItemKind.Bell or ItemKind.Glasses or ItemKind.Loupe or ItemKind.Sand or ItemKind.Coin => true,
                    _ => false
                };
                if (eligible) choices.Add((kind, id));
            }
            if (GetEggCatalog()?.floors is { } floors)
            {
                var species = new SortedSet<int>();
                foreach (var floor in floors)
                    if (floor.choices != null)
                        foreach (var choice in floor.choices)
                            if (choice.weight > 0 && IsRandomTowerEggSpecies(choice.species))
                                species.Add(choice.species);
                foreach (int id in species) choices.Add((MonsterEgg.Kind, (byte)id));
            }

            if (choices.Count == 0) return ItemSystem.CreateFloorItem(1, ItemKind.Herb, 20);
            var (itemKind, subtype) = choices[RollSpawnRange(choices.Count)];
            if (itemKind == MonsterEgg.Kind) return MonsterEgg.Create(subtype, identified: false);
            if (itemKind is ItemKind.Sword or ItemKind.Wand or ItemKind.Shield)
                return TowerItemTrapManager.CreateTowerEquipment(itemKind, subtype, RollSpawnRange(100));
            sbyte param = itemKind switch
            {
                ItemKind.Ball => (sbyte)(1 + RollSpawnRange(5)),
                ItemKind.Coin => 10,
                _ => 0
            };
            return ItemSystem.CreateFloorItem(subtype, itemKind, param);
        }

        private void AddSpeciesMessage(string message) =>
            _speciesActionMessages = JoinTowerMessages(_speciesActionMessages, message);

        private void ResetSpeciesActions()
        {
            _submergedActors.Clear();
            _speciesAiRolls.Clear();
            _pendingRocks.Clear();
            _manoevaCopies.Clear();
            _manoevaCloneCandidates.Clear();
            _speciesActionMessages = null;
        }
    }
}
