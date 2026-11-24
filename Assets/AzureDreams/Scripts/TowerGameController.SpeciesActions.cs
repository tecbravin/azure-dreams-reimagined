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
        private readonly Dictionary<ActorRecord, bool> _manoevaDisguised = new(); // true = disfarcado de item, false = de monstro
        // Ovo que cada Viper esta carregando (comeu do chao, ainda nao foi derrotada) -
        // ver TryViperGrabEgg. Cai de volta no chao quando a Viper e derrotada (Movement.cs).
        private readonly Dictionary<ActorRecord, AzureDreams.Core.DungeonItemEntry> _viperCarriedEgg = new();
        private string _speciesActionMessages;

        private static bool HasSpeciesCommand(ActorRecord actor) =>
            actor != null && (IsSpeciesObstacleCaster(actor) || actor.Species == (byte)MonsterSpecies.U_BOAT ||
                actor.Species == (byte)MonsterSpecies.MANOEVA);

        // Narcolepsia do Naplass e Abduct do Garuda sao involuntarias/hostis - nunca devem
        // aparecer como comando manual no menu da coleira, por isso ficam fora de
        // HasSpeciesCommand (que tambem controla essa UI) e tem seu proprio gate aqui.
        private static bool HasInvoluntarySpeciesAction(ActorRecord actor) =>
            actor != null && (actor.Species == (byte)MonsterSpecies.NAPLASS || actor.Species == (byte)MonsterSpecies.GARUDA);

        private static bool IsSpeciesObstacleCaster(ActorRecord actor) =>
            actor != null && (MonsterSpecies)actor.Species is MonsterSpecies.VOLCANO or MonsterSpecies.GLACIER;

        private string SpeciesCommandLabel(ActorRecord actor) => (MonsterSpecies)actor.Species switch
        {
            MonsterSpecies.U_BOAT => IsSubmerged(actor) ? T("Surface", "Emergir") : T("Dive", "Mergulhar"),
            MonsterSpecies.VOLCANO => T("Create Rock", "Criar pedra"),
            MonsterSpecies.GLACIER => T("Create Ice Wall", "Criar parede de gelo"),
            MonsterSpecies.MANOEVA => T("Revert Form", "Forma Original"),
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
                case MonsterSpecies.NAPLASS when RollSpeciesAiOnce(actor, 32):
                    NaplassNarcolepsy(actor, creature.Cell);
                    return true;
                case MonsterSpecies.GARUDA when RollSpeciesAiOnce(actor, 16) && TryGarudaAbduct(actor, creature.Cell):
                    return true;
                case MonsterSpecies.MANOEVA when ManoevaCanDisguise && !_manoevaDisguised.ContainsKey(actor) &&
                    RollSpeciesAiOnce(actor, 16) && TryManoevaDisguise(actor, creature.Cell):
                    return true;
                default:
                    return false;
            }
        }

        // Abduct do Garuda: pega um familiar adjacente (nunca Koh - so os dois "slots" de
        // familiar existem como alvo possivel, igual ao original, que varre apenas o anel
        // de aliados e nunca a lista de alvos do jogador) e o leva para um canto aleatorio
        // do andar, reaproveitando o mesmo teletransporte das armadilhas de warp.
        private bool TryGarudaAbduct(ActorRecord actor, Vector2Int cell)
        {
            ActorRecord target = null;
            Vector2Int targetCell = default;
            if (_familiar && !IsSubmerged(_familiarActor) && SpeciesDistance(cell, _familiarCell) == 1)
            {
                target = _familiarActor;
                targetCell = _familiarCell;
            }
            else if (_blueCollarAlly?.View && _blueCollarAlly.Actor.Hp > 0 && !IsSubmerged(_blueCollarAlly.Actor) &&
                     SpeciesDistance(cell, _blueCollarAlly.Cell) == 1)
            {
                target = _blueCollarAlly.Actor;
                targetCell = _blueCollarAlly.Cell;
            }
            if (target == null) return false;
            var name = TowerCombatName(actor);
            var familiarName = TowerCombatName(target);
            AddSpeciesMessage(T($"{name} carries {familiarName} away!", $"{name} levou {familiarName} pelos ares!"));
            WarpTrapActor(targetCell, target);
            return true;
        }

        // Manoeva so usa a habilidade de copiar a partir do andar 16 (ou na segunda torre) -
        // nos andares baixos (5-8) ela nunca aparece, conforme pedido. O limiar exato nao
        // esta nos dados extraidos, entao fica como escolha de design.
        private bool ManoevaCanDisguise => _floor >= 16 || InSecondTower;

        // Manoeva mira o alvo na frente dela (monstro ou item no chao, igual ao
        // check_forward_target do decomp) e se disfarca dele. Enquanto disfarcada ela fica
        // parada, mas ataca o primeiro alvo que chegar a distancia; atacar ou receber dano
        // desfaz o disfarce (ver RevertManoevaDisguise).
        private bool TryManoevaDisguise(ActorRecord actor, Vector2Int cell)
        {
            foreach (var step in BellSteps)
            {
                var target = cell + step;
                int enemy = CreatureAt(target);
                if (enemy >= 0 && _creatures[enemy].Actor != actor &&
                    _creatures[enemy].Actor.Species != (byte)MonsterSpecies.MANOEVA)
                {
                    _manoevaDisguised[actor] = false;
                    var copiedName = TowerCombatName(_creatures[enemy].Actor);
                    AddSpeciesMessage(T($"{TowerCombatName(actor)} copies {copiedName}!",
                        $"{TowerCombatName(actor)} copiou {copiedName}!"));
                    return true;
                }
            }
            if (_towerItemTraps?.GetItemAt(cell) == null) return false;
            _manoevaDisguised[actor] = true;
            AddSpeciesMessage(T($"{TowerCombatName(actor)} disguises as the item!",
                $"{TowerCombatName(actor)} se disfarcou do item!"));
            return true;
        }

        // Atacar ou receber dano devolve a forma original (pedido do usuario). Usado tanto
        // para monstros selvagens quanto para familiares disfarcados.
        private bool RevertManoevaDisguise(ActorRecord actor)
        {
            if (actor == null || !_manoevaDisguised.Remove(actor)) return false;
            var name = TowerCombatName(actor);
            AddSpeciesMessage(T($"{name} reverts to its true form!", $"{name} voltou a forma verdadeira!"));
            return true;
        }

        // User-requested behavior: walk onto the egg before picking it up.
        // Keep the exact item entry for the death drop, including species/heat/flags.
        private bool TryViperGrabEgg(ActorRecord actor, Vector2Int cell)
        {
            if (_viperCarriedEgg.ContainsKey(actor)) return false;
            var item = _towerItemTraps?.GetItemAt(cell);
            if (item == null || item.Kind != AzureDreams.Core.ItemKind.Egg) return false;
            item = _towerItemTraps.TakeItem(cell);
            if (item == null) return false;
            _viperCarriedEgg[actor] = item;
            if (_itemMarkers.Remove(cell, out var view) && view) RemoveGenerated(view);
            PlayRetailSound(RetailSfx.Pickup);
            AddSpeciesMessage(T($"{TowerCombatName(actor)} picked up the egg!",
                $"{TowerCombatName(actor)} pegou o ovo!"));
            return true;
        }

        // Manoeva se multiplica quando atingido e sobrevive - habilidade distinta e
        // paralela a de copiar forma (confirmado pelo usuario; nao achei o nome da
        // funcao correspondente no decomp nesta leva, entao a chance exata (50%) e a
        // divisao de HP (pela metade) sao uma reconstrucao razoavel, nao um valor
        // extraido byte a byte - ajustar se o usuario souber os numeros exatos).
        // So se aplica a Manoevas selvagens (lista _creatures); uma Manoeva domesticada
        // (familiar/aliado de colar azul) nao se multiplica.
        // Retorna o dano ajustado: quando ha divisao, o dano retornado e reduzido para
        // que o HP final do Manoeva original seja exatamente a metade que ele reteve.
        private int TryManoevaSplit(ActorRecord target, int damage)
        {
            if (damage <= 0 || target == null || target.Species != (byte)MonsterSpecies.MANOEVA ||
                _creatures.Count >= SpeciesSpawnLimit) return damage;
            int survivingHp = target.Hp - damage;
            if (survivingHp <= 1) return damage; // morreu ou ficaria com 1 HP - nao ha o que dividir
            int cellIndex = CreatureIndexOf(target);
            if (cellIndex < 0) return damage; // Manoeva domesticada (familiar/aliado) - nao se multiplica
            if (RollSpawnRange(2) != 0) return damage;
            Vector2Int cell = _creatures[cellIndex].Cell;
            Vector2Int? openCell = null;
            foreach (var step in BellSteps)
            {
                var candidate = cell + step;
                if (_walls.Contains(candidate) || CreatureAt(candidate) >= 0 || candidate == _playerCell ||
                    candidate == _stairCell || (_familiar && candidate == _familiarCell) ||
                    _towerItemTraps?.GetItemAt(candidate) != null) continue;
                openCell = candidate;
                break;
            }
            if (openCell == null) return damage; // sem espaco livre ao lado - nao se multiplica
            int half = Mathf.Max(1, survivingHp / 2);
            SpawnManagedCreature(openCell.Value, AzureDreams.Core.MonsterSpecies.MANOEVA,
                levelOverride: target.Level, allowSpawnSleep: false);
            int cloneIndex = _creatures.Count - 1;
            var clone = _creatures[cloneIndex];
            clone.Actor.Hp = StatByte(half);
            clone.Health = half;
            _creatures[cloneIndex] = clone;
            AddSpeciesMessage(T($"{TowerCombatName(target)} splits in two!",
                $"{TowerCombatName(target)} se multiplicou!"));
            return target.Hp - half; // dano ajustado para que o HP final seja exatamente 'half'
        }

        // Narcolepsia do Naplass: 1 chance em 32 por turno de cochilar sozinho,
        // sem exigir alvo ou linha de visao (igual ao jogo original - ver
        // start_naplass_status_action.c no decomp). Reaproveita o gate generico
        // de imunidade a status em Status(), entao qualquer talento de
        // Sleep-proof que o Naplass venha a ter no futuro continua funcionando.
        private void NaplassNarcolepsy(ActorRecord actor, Vector2Int cell)
        {
            SpawnFx(_fxMagic, CellPosition(cell));
            var name = TowerCombatName(actor);
            AddSpeciesMessage(T($"{name} nods off.", $"{name} cochilou."));
            Status(actor, StatusId.SLEEP, 4);
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
                    SpawnFx(AssetMode.Load<GameObject>("FX/FX_Water"), CellPosition(cell) + Vector3.up * 0.5f);
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
                return TowerItemTrapManager.CreateTowerEquipment(itemKind, subtype, RollSpawnRange(1024));
            sbyte param = itemKind switch
            {
                ItemKind.Ball => subtype == 17 ? (sbyte)1 : (sbyte)(4 + RollSpawnRange(4)),
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
            _manoevaDisguised.Clear();
            _viperCarriedEgg.Clear();
            _enemyStolenItems.Clear();
            _pendingEnemyThefts.Clear();
            _speciesActionMessages = null;
        }
    }
}
