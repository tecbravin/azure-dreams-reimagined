using System.Collections.Generic;
using AzureDreams.Core;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const int LevaFusionProtection = FamiliarSystem.LevaFusionProtection;
        private readonly Dictionary<ActorRecord, int> _mpProtection = new();
        private readonly Dictionary<ActorRecord, int> _doubleMpCost = new();
        private readonly List<ActorRecord> _pendingPoison = new();
        private readonly HashSet<ActorRecord> _revertedActors = new();
        private readonly HashSet<ActorRecord> _manoevaCloneCandidates = new();
        private int _reflectionTurns;
        private int _holyBellTurns;
        private static readonly Vector2Int[] BellSteps =
            { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        private const byte TovarSeed = 10;
        private const byte RedSand = 1;
        private const byte BlueSand = 2;
        private const byte WhiteSand = 3;
        private const int MaxRechargeableBallCharges = 8;
        private const int MaximumTemper = 99;
        private readonly List<(Vector2Int cell, GameObject view, int turns)> _iceBlocks = new();

        private void TickIceBlocks()
        {
            for (int i = _iceBlocks.Count - 1; i >= 0; i--)
            {
                var block = _iceBlocks[i];
                if (block.turns > 1) { _iceBlocks[i] = (block.cell, block.view, block.turns - 1); continue; }
                _walls.Remove(block.cell);
                RemoveGenerated(block.view);
                _iceBlocks.RemoveAt(i);
            }
        }
        private sealed class ThrownEggAlly
        {
            public GameObject View;
            public Vector2Int Cell;
            public ActorRecord Actor;
            public int Turns = 3;
            public bool Salamander;
        }
        private readonly List<ThrownEggAlly> _thrownEggs = new();

        private bool CanUseInventoryEffect(DungeonItemEntry item, bool giving)
        {
            return item.Kind switch
            {
                ItemKind.Herb => item.Subtype is >= 1 and <= 15 and not 8,
                ItemKind.Fruit => (IsPitaFruit(item) && giving) || (_area == Area.Tower && (item.Subtype switch
                {
                    3 or 5 or 6 or 8 or 10 => true,
                    4 => giving && (!IsTroll(_familiarActor) ||
                        (CombatGear(_familiarActor, false) == null && CombatGear(_familiarActor, true) == null)),
                    7 => giving && CanRevertToEgg(_familiarActor),
                    9 => _familiar || _blueCollarAlly != null,
                    _ => false
                })),
                ItemKind.Seed => _area == Area.Tower && item.Subtype is >= 1 and <= TovarSeed &&
                    (item.Subtype is not (5 or 6 or 7) || giving),
                ItemKind.Scroll => item.Subtype is >= 1 and <= 7 &&
                    (item.Subtype == 5 || _area == Area.Tower) &&
                    (item.Subtype != 4 || !giving),
                ItemKind.Glasses => !giving && (IsTruthGlasses(item) || (_area == Area.Tower && item.Subtype == 2)),
                ItemKind.Bell => _area == Area.Tower && !giving && item.Subtype is >= 1 and <= 3,
                ItemKind.Loupe => _area == Area.Tower && item.Subtype is >= 1 and <= 5,
                ItemKind.Ball => _area == Area.Tower && CanCastMagic(giving ? _familiarActor : _playerActor) && item.Param > 0 &&
                    item.Subtype > 0 && item.Subtype < BallSpellIds.Length,
                ItemKind.Crystal => _area == Area.Tower && !giving && item.Subtype is >= 1 and <= 3 &&
                    (item.Subtype != 1 || TryFindHatchCell(out _)),
                ItemKind.Sand => !giving && item.Subtype is RedSand or BlueSand or WhiteSand,
                _ => false
            };
        }

        private static bool IsPitaFruit(DungeonItemEntry item) =>
            item.Kind == ItemKind.Fruit && item.Subtype is ItemSystem.Id.PitaFruit or ItemSystem.Id.BigPitaFruit;

        private static bool IsRestoreScroll(DungeonItemEntry item) =>
            item != null && item.Kind == ItemKind.Scroll && item.Subtype == 4;

        private static bool IsTruthGlasses(DungeonItemEntry item) =>
            item != null && item.Kind == ItemKind.Glasses && item.Subtype == 1;

        private static bool IsUnidentifiedItem(DungeonItemEntry item) =>
            Inventory.IsUnrevealedEquipment(item) ||
            (item.Kind == MonsterEgg.Kind && !MonsterEgg.IdentifiedInTower(item));

        private static bool IsWhiteSand(DungeonItemEntry item) =>
            item != null && item.Kind == ItemKind.Sand && item.Subtype == WhiteSand;

        private static bool CanRechargeBall(DungeonItemEntry item) =>
            item.Kind == ItemKind.Ball && item.Param is >= 0 and <= MaxRechargeableBallCharges;

        private DungeonItemEntry SandUpgradeTarget(DungeonItemEntry sand)
        {
            bool shield = sand.Subtype == BlueSand;
            var equipment = CarriedInventory?.EquippedItem(PlayerEquipped, shield);
            if (equipment == null)
            {
                _message = shield
                    ? T("Equip a shield first.", "Equipe um escudo primeiro.")
                    : T("Equip a weapon first.", "Equipe uma arma primeiro.");
                return null;
            }
            if (!CanTemperEquipment(equipment, shield))
            {
                _message = T("Red Sand can temper swords and the Trained Wand.",
                    "A areia vermelha pode aprimorar espadas e a Varinha de treino.");
                return null;
            }
            if (ItemSystem.GetEquipmentModifier(equipment) >= MaximumTemper)
            {
                _message = T("This equipment is already at +99.", "Este equipamento já está em +99.");
                return null;
            }
            return equipment;
        }

        private static bool CanTemperEquipment(DungeonItemEntry equipment, bool shield) =>
            shield ? equipment.Kind == ItemKind.Shield :
                equipment.Kind == ItemKind.Sword ||
                (equipment.Kind == ItemKind.Wand && equipment.Subtype == ItemSystem.Id.TrainedWand);

        private void TemperEquipment(DungeonItemEntry equipment)
        {
            equipment.Param++;
            if (IsSword(equipment, HolySword) && HasHolySwordProtection(_playerActor))
                Status(_playerActor, StatusId.CONFUSION, 0);
            string name = InventoryItemName(equipment);
            _message = equipment.Kind == ItemKind.Shield
                ? T($"{name} gained 1 defense.", $"{name} ganhou 1 ponto de defesa.")
                : T($"{name} gained 1 attack.", $"{name} ganhou 1 ponto de ataque.");
        }

        private void ApplyInventoryEffect(DungeonItemEntry item, ActorRecord target, bool thrown = false)
        {
            byte originalSpecies = target.Species;
            if (item.Kind is ItemKind.Herb or ItemKind.Fruit)
            {
                if (!thrown) PlayRetailSound(target == _playerActor ? RetailSfx.Eat : RetailSfx.FeedFamiliar);
                if (item.Kind == ItemKind.Herb) ApplyHerb(item, target);
                else ApplyFruit(item, target);
            }
            else if (item.Kind == ItemKind.Seed) ApplySeed(item.Subtype, target);
            else if (item.Kind == ItemKind.Scroll) ApplyScroll(item, target);
            else if (item.Kind == ItemKind.Bell) ApplyBell(item.Subtype);
            else if (item.Kind == ItemKind.Glasses && item.Subtype == 2)
            {
                RevealStarGlassesTargets();
            }
            else if (item.Kind == ItemKind.Loupe)
            {
                switch (item.Subtype)
                {
                    case 1: _seeElevator = true; break;
                    case 2: RevealTowerTraps(); break;
                    case 3: RevealTowerTraps(); _seeMonsters = true; ActivateSeeAllLoupe(); break;
                    case 4: _seeMonsters = true; break;
                    case 5: ActivateSeeAllLoupe(); break;
                }
            }
            else if (item.Kind == ItemKind.Ball) CastCrystalBall(item, target);
            else if (item.Kind == ItemKind.Crystal) ApplyCrystal(item.Subtype);
            string used = T("Used ", "Usou ") + InventoryItemName(item) + ".";
            _message = originalSpecies == target.Species ? used : JoinTowerMessages(used, _message);
        }

        private void ApplySeed(int subtype, ActorRecord target)
        {
            switch (subtype)
            {
                case 1:
                    target.BaseAttack = StatByte(target.BaseAttack + 1);
                    target.CurrentAttack = StatByte(target.CurrentAttack + 1);
                    target.EffectiveAttack = StatByte(target.EffectiveAttack + 1);
                    break;
                case 2:
                    target.BaseDefense = StatByte(target.BaseDefense + 1);
                    target.CurrentDefense = StatByte(target.CurrentDefense + 1);
                    target.EffectiveDefense = StatByte(target.EffectiveDefense + 1);
                    break;
                case 3: ChangeActorLevel(target, 1); break;
                case 4: Status(target, StatusId.SPEED, 20); break;
                case 5: case 6: case 7:
                    if (target != _playerActor) ChangeSeedGenus(target, 1 << (subtype - 5));
                    break;
                case 8: ChangeActorLevel(target, -1); break;
                case 9: Status(target, StatusId.SPEED, -20); break;
                case TovarSeed: target.Luck = StatByte(target.Luck + 1); break;
            }
        }

        private void ChangeSeedGenus(ActorRecord actor, int element)
        {
            actor.Flags14 = (actor.Flags14 & ~7) | element;
            actor.Flags1C = (actor.Flags1C & ~7) | element;
            int variant = element == Element.Fire ? 1 : element == Element.Water ? 2 : 3;
            int ConvertSpell(int id) => (id - 1) / 3 * 3 + variant;
            for (int slot = 0; slot < 3; slot++)
            {
                int offset = 8 + slot * 3;
                int id = actor.U8(offset);
                if (id != 0) actor.SetRaw(offset, StatByte(ConvertSpell(id)));
            }
            if (actor.Spells != null)
                foreach (var spell in actor.Spells)
                    if (spell != null && spell.SpellId > 0) spell.SpellId = ConvertSpell(spell.SpellId);

            var view = actor == _familiarActor ? _familiar :
                _creatures.Find(creature => creature.Actor == actor).View ??
                _thrownEggs.Find(ally => ally.Actor == actor)?.View;
            if (view) MonsterPalettes.Apply(view, actor.Species, MonsterPalettes.ForElement(element));
        }

        private const int WaterCrystalRange = 8;

        private void ApplyCrystal(int subtype)
        {
            switch (subtype)
            {
                case 1:
                    if (!TryFindHatchCell(out var cell)) return;
                    var actor = new ActorRecord { Species = (byte)MonsterSpecies.SALAMAN };
                    TryGetLevelStats(actor.Species, _floor, out var stats);
                    ApplyFreshStats(actor, stats);
                    var summon = new ThrownEggAlly { Actor = actor, Cell = cell, Turns = 30, Salamander = true };
                    SpawnSalamanderView(summon);
                    _thrownEggs.Add(summon);
                    SpawnFx(_fxMagic, CellPosition(cell));
                    PlayRetailSound(RetailSfx.Summon);
                    break;
                case 2:
                    RestoreNearbyFamiliarVitals();
                    PlayRetailSound(RetailSfx.Restore);
                    break;
                case 3:
                    _returnWithCrystal = true;
                    PlayRetailSound(RetailSfx.ReturnMagic);
                    break;
            }
        }

        private void SpawnSalamanderView(ThrownEggAlly summon)
        {
            var prefab = RegisteredMonsterPrefab((int)MonsterSpecies.SALAMAN);
            var visualSpecies = prefab ? MonsterSpecies.SALAMAN : MonsterSpecies.FLAME;
            if (!prefab) prefab = MonsterPrefab(visualSpecies);
            summon.View = SpawnActor(prefab, summon.Cell, "Salamander - Fire Crystal",
                species: (int)visualSpecies, palette: MonsterPaletteVariant.Fire);
            ConfigureLocomotion(summon.View);
        }

        private void RestoreSalamanderSummons()
        {
            var occupied = new List<Vector2Int> { _playerCell, _stairCell };
            if (_familiar) occupied.Add(_familiarCell);
            if (_blueCollarAlly != null) occupied.Add(_blueCollarAlly.Cell);
            foreach (var summon in _thrownEggs)
            {
                if (!summon.Salamander) continue;
                var preferred = _playerCell + Vector2Int.right;
                summon.Cell = UsesManagedTowerLayout
                    ? FindManagedActorCell(preferred, occupied.ToArray())
                    : FindOpenCell(preferred, occupied.ToArray());
                occupied.Add(summon.Cell);
                SpawnSalamanderView(summon);
            }
        }

        private void RestoreNearbyFamiliarVitals()
        {
            RestoreConditions(_playerActor);
            var nearby = new HashSet<Vector2Int> { _playerCell };
            var pending = new Queue<(Vector2Int Cell, int Distance)>();
            pending.Enqueue((_playerCell, 0));
            while (pending.Count > 0)
            {
                var (cell, distance) = pending.Dequeue();
                if (distance == WaterCrystalRange) continue;
                foreach (var step in BellSteps)
                {
                    var next = cell + step;
                    if (CanTowerStep(cell, step) && nearby.Add(next)) pending.Enqueue((next, distance + 1));
                }
            }
            if (_familiar && nearby.Contains(_familiarCell) || _carriedFamiliar)
            {
                RestoreVitals(_familiarActor);
                _familiarHealth = _familiarActor.Hp;
            }
            foreach (var ally in _thrownEggs)
                if (!ally.Salamander && ally.View && ally.Actor.Hp > 0 && nearby.Contains(ally.Cell))
                    RestoreVitals(ally.Actor);

            void RestoreVitals(ActorRecord actor)
            {
                RestoreConditions(actor);
                actor.Hp = actor.MaxHp;
                actor.CurrentMp = actor.MaxMp;
            }
        }

        private bool _returnWithCrystal;

        private void AdjustItemHealth(ActorRecord actor, int delta)
        {
            if (actor == _playerActor)
                _health = Mathf.Clamp(_health + delta, 0, KohMaxHealth);
            else if (actor == _familiarActor)
                _familiarHealth = Mathf.Clamp(_familiarHealth + delta, 0, FamiliarMaxHealth);
            else if (_thrownEggs.Exists(ally => ally.Actor == actor))
                actor.Hp = StatByte(Mathf.Clamp(actor.Hp + delta, 0, actor.MaxHp));
            else
            {
                for (int i = 0; i < _creatures.Count; i++)
                {
                    var monster = _creatures[i];
                    if (monster.Actor != actor) continue;
                    monster.Health = Mathf.Clamp(monster.Health + delta, 0,
                        Mathf.Max(monster.Health, (int)actor.MaxHp));
                    _creatures[i] = monster;
                    break;
                }
            }
        }

        private const sbyte BlindnessTurns = 20;

        private void Status(ActorRecord actor, sbyte status, sbyte turns)
        {
            if (turns != 0)
            {
                if (status == StatusId.CONFUSION && HasHolySwordProtection(actor)) return;
                int immunity = status switch
                {
                    StatusId.SLEEP => SleepProofTalent,
                    StatusId.CONFUSION => 0x04000000,
                    StatusId.BLIND => 0x08000000,
                    StatusId.PARALYSIS => 0x10000000,
                    StatusId.POISON => 0x20000000,
                    StatusId.MAGIC_BIND => 0x40000000,
                    StatusId.BRAINWASH => 0x00100000,
                    _ => 0
                };
                if (!HasTalent(actor, immunity))
                    ActorStatusEffects.SetOrClear(actor, status, status == StatusId.BLIND ? BlindnessTurns : turns);
                return;
            }
            if (status == StatusId.POISON) _pendingPoison.Remove(actor);
            for (int i = 0; i < 4; i++)
                if (actor.StatusId(i) == status) actor.SetStatus(i, 0, 0);
        }
        private void RestoreConditions(ActorRecord actor)
        {
            _pendingPoison.Remove(actor);
            for (int i = 0; i < 4; i++)
            {
                sbyte id = actor.StatusId(i);
                bool negative = id is StatusId.SLEEP or StatusId.CONFUSION or StatusId.BLIND or StatusId.PARALYSIS or
                    StatusId.POISON or StatusId.MAGIC_BIND or StatusId.CURSE or StatusId.FROG or
                    StatusId.BRAINWASH or StatusId.MP_DRAIN or StatusId.ANOREXIA ||
                    (id == StatusId.SPEED && actor.StatusValue(i) < 0);
                if (negative) actor.SetStatus(i, 0, 0);
            }
            RestoreAttack(actor);
            RestoreDefense(actor);
            _doubleMpCost.Remove(actor);
        }

        private static void RestoreAttack(ActorRecord actor) =>
            actor.CurrentAttack = (byte)Mathf.Max(actor.CurrentAttack, actor.EffectiveAttack);

        private static void RestoreDefense(ActorRecord actor) =>
            actor.CurrentDefense = (byte)Mathf.Max(actor.CurrentDefense, actor.EffectiveDefense);

        private void ApplyMedicinalHerb(ActorRecord actor)
        {
            int health = actor == _playerActor ? _health : actor == _familiarActor ? _familiarHealth : actor.Hp;
            if (health > 0 && health == actor.MaxHp && actor.MaxHp < byte.MaxValue)
            {
                actor.BaseHp = StatByte(actor.BaseHp + 1);
                actor.MaxHp++;
                actor.Hp = actor.MaxHp;
            }
            AdjustItemHealth(actor, 20);
        }

        private void ApplyHerb(DungeonItemEntry item, ActorRecord actor)
        {
            switch (item.Subtype)
            {
                case 1: ApplyMedicinalHerb(actor); break;
                case ShopMedicinalHerbSubtype: AdjustItemHealth(actor, item.Param > 0 ? item.Param : 20); break;
                case 2: Status(actor, StatusId.POISON, 0); break;
                case 3: Status(actor, StatusId.CONFUSION, 0); break;
                case 4: Status(actor, StatusId.SLEEP, 0); break;
                case 5: RestoreConditions(actor); break;
                case 6: RestoreAttack(actor); break;
                case 7: RestoreDefense(actor); break;
                case 9: Status(actor, StatusId.POISON, 8); break;
                case 10: Status(actor, StatusId.PARALYSIS, 8); break;
                case 11: if (actor.CurrentAttack > 1) actor.CurrentAttack--; break;
                case 12: if (actor.CurrentDefense > 1) actor.CurrentDefense--; break;
                case 13: Status(actor, StatusId.SLEEP, 8); break;
                case 14: Status(actor, StatusId.BLIND, 0); break;
            }
        }

        private void ApplyFruit(DungeonItemEntry item, ActorRecord actor)
        {
            switch (item.Subtype)
            {
                case 1: case 2:
                    if (actor == _playerActor) break;
                    if (actor.MpRaw == actor.MaxMp * 256)
                    {
                        actor.BaseMp = StatByte(actor.BaseMp + 1);
                        actor.MaxMp = StatByte(actor.MaxMp + 1);
                        actor.CurrentMp = actor.MaxMp;
                    }
                    else
                    {
                        var diet = UnitProfile(actor.Species);
                        int recovery = item.Subtype == 1 ? diet?.likesPitas ?? 50 : diet?.likesBigPitas ?? 100;
                        actor.CurrentMp = Mathf.Min(actor.MaxMp, actor.CurrentMp + recovery);
                    }
                    break;
                case 3: Status(actor, StatusId.FROG, 20); break;
                case 4:
                    if (actor == _familiarActor || actor == _blueCollarAlly?.Actor)
                        actor.Flags14 |= LevaFusionProtection;
                    break;
                case 5: _mpProtection[actor] = 30; break;
                case 6: _doubleMpCost[actor] = 30; break;
                case 7: RevertToEgg(actor); break;
                case 8: Status(actor, StatusId.ANGER, 15); break;
                case 9:
                    var target = actor == _playerActor ? (_familiar ? _familiarActor : _blueCollarAlly?.Actor) : actor;
                    if (target != null && RemoveFruitTarget(target, out bool familiar) && familiar)
                        _returnWithCrystal = true;
                    break;
                case 10: actor.CurrentMp = 0; break;
            }
        }

        private Vector2Int? FruitTargetCell(ActorRecord actor)
        {
            if (actor == null || actor == _playerActor) return null;
            if (actor == _familiarActor)
                return _carriedFamiliar ? _playerCell : _familiar ? _familiarCell : (Vector2Int?)null;
            foreach (var ally in _thrownEggs)
                if (ally.Actor == actor && ally.View) return ally.Cell;
            foreach (var enemy in _creatures)
                if (enemy.Actor == actor) return enemy.Cell;
            return null;
        }

        private bool TryFindEggDrop(ActorRecord actor, out Vector2Int drop)
        {
            drop = default;
            if (actor == null || _manoevaCopies.Contains(actor) ||
                FindEggSpecies(actor.Species) == null || FruitTargetCell(actor) is not { } cell) return false;
            return TryFindDiscardCell(cell, out drop);
        }

        private bool CanRevertToEgg(ActorRecord actor) => TryFindEggDrop(actor, out _);

        private void RevertToEgg(ActorRecord actor)
        {
            if (!TryFindEggDrop(actor, out var drop) ||
                !PlaceInventoryItem(drop, MonsterEgg.Create(actor.Species, true))) return;
            if (_carriedFamiliar && actor == _familiarActor && drop == _playerCell)
                _discardedUnderKoh = drop;
            RemoveFruitTarget(actor, out _);
        }

        private bool RemoveFruitTarget(ActorRecord actor, out bool familiar)
        {
            familiar = false;
            if (actor == _familiarActor && (_familiar || _carriedFamiliar))
            {
                PutFamiliarInBag();
                _runInventory?.ClearEquipped(FamiliarEquipped);
                _familiarDefinition = null;
                _familiarActor = new ActorRecord();
                _hatchedSpecies = 0;
                _familiarDefeated = false;
                familiar = true;
                return true;
            }
            if (_blueCollarAlly?.Actor == actor)
            {
                _bagFamiliars.Remove(_blueCollarFamiliar);
                PutBlueCollarFamiliarInBag();
                familiar = true;
                return true;
            }
            var ally = _thrownEggs.Find(entry => entry.Actor == actor);
            if (ally != null)
            {
                RemoveGenerated(ally.View);
                _thrownEggs.Remove(ally);
                return true;
            }
            int enemy = _creatures.FindIndex(entry => entry.Actor == actor);
            if (enemy < 0) return false;
            _revertedActors.Add(actor);
            AdjustItemHealth(actor, -_creatures[enemy].Health);
            return true;
        }

        private static int GearElement(DungeonItemEntry item)
        {
            if (item == null) return 0;
            int elementIndex = item.Kind switch
            {
                ItemKind.Sword => item.Subtype - 5,
                ItemKind.Wand => item.Subtype == FireStaffSubtype ? 0 : item.Subtype - 6,
                ItemKind.Shield => item.Subtype - 8,
                _ => -1
            };
            return elementIndex is >= 0 and < 3 ? 1 << elementIndex : 0;
        }

        private int UnitElement(ActorRecord actor)
        {
            int genus = actor.Flags14 & 7;
            if (genus is Element.Fire or Element.Water or Element.Wind) return genus;
            if (actor.Species == (byte)MonsterSpecies.PICKET)
            {
                if (_carriedMonster.Actor == actor && _carriedMonster.CarriesWaterMedal) return Element.Water;
                foreach (var creature in _creatures)
                    if (creature.Actor == actor && creature.CarriesWaterMedal) return Element.Water;
            }
            return UnitProfile(actor.Species)?.nativeElement ?? 0;
        }

        private DungeonItemEntry CombatGear(ActorRecord actor, bool shield)
        {
            if (actor == _playerActor) return _runInventory?.EquippedItem(PlayerEquipped, shield);
            if (actor == _familiarActor) return _runInventory?.EquippedItem(FamiliarEquipped, shield);
            if (actor == _blueCollarAlly?.Actor) return _runInventory?.EquippedItem(BlueFamiliarEquipped, shield);
            return shield ? actor.Shield : actor.Weapon;
        }

        private DungeonItemEntry ActiveCombatShield(ActorRecord actor) =>
            IsFrog(actor) ? null : CombatGear(actor, true);

        private const int MirrorShield = 3;
        private const int LiveShield = 11;

        internal const byte DarkSword = 9;
        private const byte GoldSword = 1;
        private const byte VitalSword = 8;
        private const byte HolySword = 10;
        private const byte SeraphimKeepsake = 11;
        private const byte SeraphimSword = 12;

        private static bool IsSword(DungeonItemEntry item, byte subtype) =>
            item?.Kind == ItemKind.Sword && item.Subtype == subtype;

        private bool HasHolySwordProtection(ActorRecord actor)
        {
            if (actor != _playerActor) return false;
            var weapon = CombatGear(actor, false);
            return IsSword(weapon, HolySword) && ItemSystem.GetEquipmentModifier(weapon) >= 20;
        }

        private void RustEquippedGear(ActorRecord actor)
        {
            var weapon = CombatGear(actor, false);
            var shield = CombatGear(actor, true);
            bool rustWeapon = weapon?.Kind == ItemKind.Sword && weapon.Subtype != GoldSword &&
                weapon.Param > sbyte.MinValue && Inventory.EquipmentStat(weapon, InventoryText.Find(weapon)) > 1;
            bool rustShield = shield?.Kind == ItemKind.Shield && shield.Subtype is not (1 or 2 or MirrorShield or 7) &&
                shield.Param > sbyte.MinValue && Inventory.EquipmentStat(shield, InventoryText.Find(shield)) > 1;
            if (rustWeapon) weapon.Param--;
            if (rustShield) shield.Param--;
            if (actor != _playerActor) return;
            if (rustWeapon && rustShield)
                _message = T("Your sword and shield rusted. You lost 1 ATK and 1 DEF.",
                    "Sua espada e seu escudo enferrujaram. Você perdeu 1 ATQ e 1 DEF.");
            else if (rustWeapon)
                _message = T("Your sword rusted and lost 1 ATK.", "Sua espada enferrujou e perdeu 1 ATQ.");
            else if (rustShield)
                _message = T("Your shield rusted and lost 1 DEF.", "Seu escudo enferrujou e perdeu 1 DEF.");
        }

        private const byte LifeWand = 3;
        private const byte ParalyzeWand = 4;
        private const byte MoneyWand = 5;
        private const byte SealWand = 9;
        private const sbyte WandStatusTurns = 8;

        private void ResolveWandHit(byte wand, ActorRecord target)
        {
            switch (wand)
            {
                case LifeWand:
                    AdjustItemHealth(_playerActor, 1 + RollSpawnRange(4));
                    break;
                case ParalyzeWand when target.Hp > 0 && RollCombatItemEffect(20):
                    Status(target, StatusId.PARALYSIS, WandStatusTurns);
                    break;
                case SealWand when target.Hp > 0 && RollCombatItemEffect(20):
                    Status(target, StatusId.MAGIC_BIND, WandStatusTurns);
                    break;
            }
        }

        private static byte MoneyWandCoin(int floor) => floor <= 15 ? ItemSystem.Id.CopperCoin :
            floor <= 30 ? ItemSystem.Id.SilverCoin : ItemSystem.Id.GoldCoin;

        private string DropMoneyWandCoins(CreatureState creature)
        {
            if (!RollCombatItemEffect(30)) return null;
            var coin = ItemSystem.CreateFloorItem(MoneyWandCoin(_floor), ItemKind.Coin, 0);
            if (_towerItemTraps == null || !DropTowerReward(creature.Cell, coin))
            {
                CreditCoinPickup(coin);
                return null;
            }
            string monster = TowerCombatName(creature.Actor);
            return T($"{monster} turned into coins.", $"{monster} se transformou em moedas.");
        }

        private bool RollCombatItemEffect(int percent) => _towerRun?.Rng != null && RollSpawnRange(100) < percent;

        private static int ApplyElementalShield(int damage, int attackElements, DungeonItemEntry shield)
        {
            if (damage <= 0 || shield?.Kind != ItemKind.Shield || (GearElement(shield) & attackElements) == 0)
                return damage;
            return damage / 2 + damage % 2;
        }

        private bool ReflectMonsterSpell(ActorRecord target) =>
            (target == _playerActor && _reflectionTurns > 0) ||
            (ActiveCombatShield(target)?.Subtype == MirrorShield && RollCombatItemEffect(30));

        private void ApplyLiveShield(ActorRecord defender, ActorRecord attacker, int damage)
        {
            int returned = damage / 5;
            if (returned == 0 || BlocksDamage(attacker) || !RollCombatItemEffect(20)) return;
            int enemy = _creatures.FindIndex(entry => entry.Actor == attacker);
            if (enemy >= 0) QueueCreatureDamage(defender, enemy, returned);
            else
            {
                if (attacker == _playerActor) _pendingPlayerDamage += returned;
                else if (attacker == _familiarActor) _pendingFamiliarDamage += returned;
                else if (attacker == _blueCollarAlly?.Actor) _pendingBlueCollarDamage += returned;
                else
                {
                    var ally = _thrownEggs.Find(entry => entry.Actor == attacker);
                    if (ally == null) return;
                    ally.Actor.Hp = StatByte(ally.Actor.Hp - returned);
                }
                RecordTowerHit(defender, attacker, returned);
            }
        }

        private static void ApplyCombatDefense(ActorRecord target, DungeonItemEntry shield, bool bypassShield = false)
        {
            if (bypassShield || IsFrog(target))
                target.DefenseEquipBase = 0;
            else if (shield != null)
                target.DefenseEquipBase = (byte)Inventory.EquipmentStat(shield, InventoryText.Find(shield));
            else
                target.DefenseEquipBase = (byte)target.DefenseBonus();
            target.Shield = null;
        }

        private bool BlocksDamage(ActorRecord target) =>
            IsSubmerged(target) || (target == _playerActor && _testInvulnerable) ||
            Combat.HasStatus(target, StatusId.DAMAGE_GUARD);

        private static int IntrinsicWeaponDamage(ActorRecord actor, DungeonItemEntry weapon) =>
            (MonsterSpecies)actor.Species switch
            {
                MonsterSpecies.DEATH or MonsterSpecies.GOLEM => 4,
                MonsterSpecies.GHOSH => 6,
                MonsterSpecies.KILLER => 8,
                (MonsterSpecies)56 => 32, // Beldo in combat, distinct from the vision
                MonsterSpecies.TROLL when weapon?.Kind == ItemKind.Sword => weapon.Subtype switch
                {
                    13 => 8,
                    14 => 2,
                    15 => 1,
                    _ => 0
                },
                _ => 0
            };

        private int WithItemCombatEffects(ActorRecord actor, int attack, ActorRecord target,
            Vector2Int attackerCell, Vector2Int targetCell, int mixElement = 0, int mixLevel = 0)
        {
            if (BlocksDamage(target)) return 0;
            var weapon = CombatGear(actor, false);
            bool trollSword = IsTroll(actor) && IsTrollWeapon(weapon) && weapon.Subtype == 13;
            var shield = trollSword ? null : ActiveCombatShield(target);
            byte oldDefense = target.CurrentDefense;
            byte oldDefenseBonus = target.DefenseEquipBase;
            var oldShield = target.Shield;
            int oldTargetFlags = target.Flags1C;
            int oldActorFlags = actor.Flags1C;
            byte oldHitBaseline = actor.HitBaseline;
            byte oldLuck = actor.Luck;
            try
            {
                if (MonsterExhausted(target) && target != _playerActor) target.Flags1C |= ActorFlag.MP_EMPTY;
                if (Combat.HasStatus(target, StatusId.SLEEP)) target.Flags1C |= ActorFlag.ASLEEP;
                if (Combat.HasStatus(target, StatusId.PARALYSIS)) target.Flags1C |= ActorFlag.PARALYZED;
                if (Combat.HasStatus(target, StatusId.BLIND)) target.Flags1C |= ActorFlag.BLIND;
                if (HasTalent(actor, AtrociousTalent)) actor.Flags1C |= ActorFlag.ANGRY;
                if (Combat.HasStatus(actor, StatusId.BLIND))
                    actor.HitBaseline = StatByte(oldHitBaseline - 32);
                actor.Luck = AttackLuck(actor);
                target.Shield = trollSword ? null : shield;

                int weaponModifier = IsTroll(actor) && IsTrollWeapon(weapon) ? ItemSystem.GetEquipmentModifier(weapon) : 0;
                int power = 2 * (attack + IntrinsicWeaponDamage(actor, weapon) + weaponModifier);
                bool critical = false;
                var rng = _towerRun?.Rng;
                if (rng != null)
                {
                    var prepared = Combat.PrepareDirectionalAttack(actor, target, rng);
                    if (prepared == null || (IsSword(weapon, DarkSword) && DarkSwordMisses(actor, target))) return 0;
                    power += prepared.AttackPower - 2 * (actor.CurrentAttack + actor.AttackBonus());

                    int baseCritOutcomes = 1 + ((actor.Flags1C & ActorFlag.ANGRY) != 0 ? actor.Luck >> 1 : actor.Luck >> 6);
                    int critAdjustment = IsSword(weapon, DarkSword) ? 30 :
                        IsSword(weapon, SeraphimSword) || IsSword(weapon, SeraphimKeepsake) ? 20 :
                        HasHolySwordProtection(actor) ? 10 : 0;
                    if (HasHolySwordProtection(target)) critAdjustment -= 20;

                    // The fruit adds half the base critical rate. Gear adds percentage points.
                    int adjustment = critAdjustment * 256 + (Combat.HasStatus(actor, StatusId.ANGER) ? baseCritOutcomes * 100 : 0);
                    critical = AdjustRolledChance(prepared.Critical, baseCritOutcomes * 2, 256, adjustment);
                }

                ApplyCombatDefense(target, shield, bypassShield: trollSword);
                // Retail sword mixtures increase prepared attack power by power * level / (level + 1).
                if (mixLevel > 0) power += power * mixLevel / (mixLevel + 1);
                int attackElements = UnitElement(actor) | GearElement(weapon) | mixElement;
                int damage = -Combat.ResolvePhysicalDamage(actor, target, power, critical,
                    attackElements, UnitElement(target) | GearElement(shield));
                damage = ScaleElevationDamage(damage, TowerTileHeight(attackerCell), TowerTileHeight(targetCell));
                damage = ApplyElementalShield(damage, attackElements, shield);
                if (shield?.Subtype == LiveShield) ApplyLiveShield(target, actor, damage);
                if (!IsSword(weapon, VitalSword)) QueueManoevaSplit(target, damage);
                return damage;
            }
            finally
            {
                target.CurrentDefense = oldDefense;
                target.DefenseEquipBase = oldDefenseBonus;
                target.Shield = oldShield;
                target.Flags1C = oldTargetFlags;
                actor.Flags1C = oldActorFlags;
                actor.HitBaseline = oldHitBaseline;
                actor.Luck = oldLuck;
            }
        }

        private static byte AttackLuck(ActorRecord actor) => Combat.HasStatus(actor, StatusId.CURSE)
            ? (byte)(actor.Luck * 3 / 4) : actor.Luck;

        private bool DarkSwordMisses(ActorRecord attacker, ActorRecord target)
        {
            if (Combat.IsIncapacitatedForPhysicalAttack(target))
                return RollSpawnRange(100) < 30;

            int outcomes = Mathf.Max(1, (int)attacker.Luck);
            bool evadeShield = target.Species == 0 && target.Shield?.Subtype == 2;
            int threshold = evadeShield ? 170 : 128;
            int firstHit = Mathf.Max(0, (threshold - attacker.HitBaseline + 3) / 4);
            int hits = Mathf.Max(0, outcomes - firstHit);
            var (dodges, dodgeOutcomes) = Combat.PhysicalDodgeRoll(target);
            if (dodges == 0) return !AdjustRolledChance(true, hits, outcomes, -30 * outcomes);

            // The penalty is 30 percentage points off the joint hit probability.
            hits *= dodgeOutcomes - dodges;
            outcomes *= dodgeOutcomes;
            if (hits == 0) return true;
            int extraMiss = Mathf.Min(10000, (3000 * outcomes + hits / 2) / hits);
            return RollSpawnRange(10000) < extraMiss;
        }

        // Change an existing binary roll while preserving its outcome for an unmodified chance.
        private bool AdjustRolledChance(bool success, int favorable, int total, int adjustment)
        {
            if (!success && adjustment > 0)
                return RollSpawnRange(100 * (total - favorable)) < adjustment;
            if (success && adjustment < 0)
                return favorable > 0 && RollSpawnRange(100 * favorable) >= -adjustment;
            return success;
        }

        private static int ScaleElevationDamage(int damage, int attackerHeight, int targetHeight)
        {
            if (damage <= 0 || attackerHeight == targetHeight) return damage;
            int adjustment = (damage + 3) / 4;
            return attackerHeight > targetHeight ? damage + adjustment : Mathf.Max(1, damage - adjustment);
        }

        private static int WandSpellPower(DungeonItemEntry wand, int power, int spellLevel, int element)
        {
            if (wand?.Kind != ItemKind.Wand || wand.Subtype is < ItemSystem.Id.ScarletWand or > ItemSystem.Id.GulfWand ||
                (GearElement(wand) & element) == 0) return power;

            // Use the retail matching-wand multiplier at effect variant 1.
            // ResolveSpellDamage adds spellLevel to power before calculating damage.
            return power * 2 + spellLevel;
        }

        private int SpellOrImpactDamage(ActorRecord target, int power, int spellLevel, int element = 0, ActorRecord caster = null)
        {
            if (BlocksDamage(target)) return 0;
            var shield = ActiveCombatShield(target);
            byte oldDefense = target.CurrentDefense;
            byte oldDefenseBonus = target.DefenseEquipBase;
            var oldShield = target.Shield;
            try
            {
                ApplyCombatDefense(target, shield);
                if (caster != null) power = WandSpellPower(CombatGear(caster, false), power, spellLevel, element);
                int damage = -Combat.ResolveSpellDamage(caster, target, power, spellLevel,
                    element, UnitElement(target) | GearElement(shield), 0);
                damage = ApplyElementalShield(damage, element, shield);
                if (caster != null) QueueManoevaSplit(target, damage);
                return damage;
            }
            finally
            {
                target.CurrentDefense = oldDefense;
                target.DefenseEquipBase = oldDefenseBonus;
                target.Shield = oldShield;
            }
        }

        private static void TickItemTimer(Dictionary<ActorRecord, int> timer, ActorRecord actor)
        {
            if (!timer.TryGetValue(actor, out int turns)) return;
            if (turns <= 1) timer.Remove(actor); else timer[actor] = turns - 1;
        }

        private static bool IsFrog(ActorRecord actor) => actor != null && Combat.HasStatus(actor, StatusId.FROG);

        private static bool ItemImmobilized(ActorRecord actor) =>
            Combat.HasStatus(actor, StatusId.SLEEP) || Combat.HasStatus(actor, StatusId.PARALYSIS);

        private void ApplyBell(int subtype)
        {
            switch (subtype)
            {
                case 1: _holyBellTurns = 15; break;
                case 2:
                    for (int i = 0; i < _creatures.Count; i++)
                        if (_creatures[i].Health > 0) ChangeActorLevel(_creatures[i].Actor, 1);
                    if (_carriedMonster.Actor != null && _carriedMonster.Health > 0)
                        ChangeActorLevel(_carriedMonster.Actor, 1);
                    break;
                case 3: GatherFloorFamiliars(); break;
            }
        }

        private int BellRoomAt(Vector2Int cell)
        {
            var dungeon = _towerFloorLayout?.Dungeon;
            if (dungeon == null) return -1;
            for (int i = 0; i < dungeon.RoomCount; i++)
            {
                var room = dungeon.Rooms[i];
                if (cell.x >= room.X && cell.x < room.X + room.Width &&
                    cell.y >= room.Y && cell.y < room.Y + room.Height) return i;
            }
            return -1;
        }

        private Vector2Int HolyBellStep(Vector2Int from)
        {
            int room = BellRoomAt(from);
            if (room >= 0)
            {
                var visited = new HashSet<Vector2Int> { from };
                var pending = new Queue<(Vector2Int Cell, Vector2Int First)>();
                pending.Enqueue((from, Vector2Int.zero));
                while (pending.Count > 0)
                {
                    var (cell, first) = pending.Dequeue();
                    foreach (var step in BellSteps)
                    {
                        var next = cell + step;
                        if (!CanTowerStep(cell, step) || next == _stairCell || IsSacredTile(next) ||
                            (first == Vector2Int.zero && BellCellOccupied(next)) || !visited.Add(next)) continue;
                        if (BellRoomAt(next) != room) return first == Vector2Int.zero ? step : first;
                        pending.Enqueue((next, first == Vector2Int.zero ? step : first));
                    }
                }
                return Vector2Int.zero;
            }

            // In corridors, keep away from Koh and avoid entering another room.
            Vector2Int best = Vector2Int.zero;
            int bestDistance = Distance(from, _playerCell);
            foreach (var step in BellSteps)
            {
                var next = from + step;
                int distance = Distance(next, _playerCell);
                if (distance <= bestDistance || BellRoomAt(next) >= 0 || !CanTowerStep(from, step) ||
                    next == _stairCell || IsSacredTile(next) || BellCellOccupied(next)) continue;
                best = step;
                bestDistance = distance;
            }
            return best;
        }

        private bool BellCellOccupied(Vector2Int cell) =>
            cell == _playerCell || CreatureAt(cell) >= 0 || (_familiar && cell == _familiarCell) ||
            _thrownEggs.Exists(ally => ally.View && ally.Turns > 0 && ally.Cell == cell);

        private void GatherFloorFamiliars()
        {
            var allies = _thrownEggs.FindAll(ally => ally.View && ally.Turns > 0 && ally.Actor.Hp > 0);
            bool redActive = _familiar && _familiarHealth > 0;
            int count = allies.Count + (redActive ? 1 : 0);
            if (count == 0) return;

            int room = BellRoomAt(_playerCell);
            var destinations = new Queue<Vector2Int>();
            var visited = new HashSet<Vector2Int> { _playerCell };
            var pending = new Queue<Vector2Int>();
            pending.Enqueue(_playerCell);
            while (pending.Count > 0 && destinations.Count < count)
            {
                var cell = pending.Dequeue();
                foreach (var step in BellSteps)
                {
                    var next = cell + step;
                    if (!CanTowerStep(cell, step) || (room >= 0 && BellRoomAt(next) != room) ||
                        !visited.Add(next)) continue;
                    pending.Enqueue(next);
                    if (next == _stairCell || CreatureAt(next) >= 0 || TrapAt(next) is { IsActive: true }) continue;
                    destinations.Enqueue(next);
                    if (destinations.Count == count) break;
                }
            }

            // If space is scarce, keep tiles occupied by familiars already in the room.
            bool limitedSpace = destinations.Count < count;
            if (limitedSpace)
            {
                var free = destinations.Count;
                for (int i = 0; i < free; i++)
                {
                    var cell = destinations.Dequeue();
                    if (!BellCellOccupied(cell)) destinations.Enqueue(cell);
                }
            }
            if (redActive && destinations.Count > 0 && (!limitedSpace || room < 0 || BellRoomAt(_familiarCell) != room))
                TeleportFamiliar(_familiar, _familiarActor, ref _familiarCell);
            foreach (var ally in allies)
            {
                if (destinations.Count == 0) break;
                if (limitedSpace && room >= 0 && BellRoomAt(ally.Cell) == room) continue;
                TeleportFamiliar(ally.View, ally.Actor, ref ally.Cell);
            }

            void TeleportFamiliar(GameObject view, ActorRecord actor, ref Vector2Int cell)
            {
                cell = destinations.Dequeue();
                SetActorCell(actor, cell);
                view.transform.position = CellPosition(cell);
                SpawnFx(_fxMagic, view.transform.position);
            }
        }

        private void ApplyScroll(DungeonItemEntry item, ActorRecord actor)
        {
            switch (item.Subtype)
            {
                case 1: CreateSacredTile(actor == _familiarActor ? _familiarCell : _playerCell); break;
                case 2: Status(actor, StatusId.CONFUSION, 10); break;
                case 3: RevealTowerTraps(rearm: true); break;
                case 5: CarriedInventory?.RemoveEquipmentCurse(); break;
                case 6: LevelTowerFloor(); break;
                case 7: TurnGroundItemsIntoGold(); break;
            }
        }

        private readonly Dictionary<Vector2Int, (GameObject View, int Turns)> _sacredTiles = new();
        private Sprite _sacredTileSprite;

        private bool IsSacredTile(Vector2Int cell) => _sacredTiles.ContainsKey(cell);

        private void CreateSacredTile(Vector2Int cell)
        {
            if (_sacredTiles.TryGetValue(cell, out var existing))
            {
                _sacredTiles[cell] = (existing.View, 20);
                return;
            }
            var view = new GameObject("Sacred tile");
            view.transform.SetParent(transform, true);
            view.transform.SetPositionAndRotation(new Vector3(cell.x, TowerGroundY(cell) + 0.018f, cell.y),
                Quaternion.Euler(90f, 0f, 0f));
            if (!_sacredTileSprite)
            {
                var texture = Texture2D.whiteTexture;
                _sacredTileSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    Vector2.one * 0.5f, texture.width);
            }
            var renderer = view.AddComponent<SpriteRenderer>();
            renderer.sprite = _sacredTileSprite;
            renderer.color = new Color(1f, 1f, 1f, 0.72f);
            renderer.sortingOrder = -1;
            _generated.Add(view);
            _sacredTiles.Add(cell, (view, 20));
        }

        private void TickSacredTiles()
        {
            if (_sacredTiles.Count == 0) return;
            foreach (var cell in new List<Vector2Int>(_sacredTiles.Keys))
            {
                var tile = _sacredTiles[cell];
                if (tile.Turns > 1) _sacredTiles[cell] = (tile.View, tile.Turns - 1);
                else
                {
                    RemoveGenerated(tile.View);
                    _sacredTiles.Remove(cell);
                }
            }
        }

        private bool _demoItemsConvertedToGold;

        private void TurnGroundItemsIntoGold()
        {
            if (_towerItemTraps == null)
            {
                if (_demoItemsConvertedToGold) return;
                _demoItemsConvertedToGold = true;
                if (_hasLoot)
                {
                    _itemMarkers[_lootCell] = _salvageMarker;
                    _hasLoot = false;
                    _salvageMarker = null;
                }
                foreach (var cell in new List<Vector2Int>(_itemMarkers.Keys))
                {
                    ConsumeFloorMarker(_itemMarkers, cell);
                    _itemMarkers[cell] = CreateGroundItemView(cell, ItemSystem.ConvertToGold(null));
                }
                return;
            }
            foreach (var cell in _towerFloorLayout.OpenCells)
            {
                var item = _towerItemTraps.GetItemAt(cell);
                if (item == null || item.Kind == ItemKind.Coin) continue;
                var gold = ItemSystem.ConvertToGold(item);
                item.Kind = gold.Kind;
                item.Subtype = gold.Subtype;
                item.Param = gold.Param;
                item.Flags = gold.Flags;
                ConsumeFloorMarker(_itemMarkers, cell);
                _itemMarkers[cell] = CreateGroundItemView(cell, item);
            }
        }

        private void TickMovementSpeed(ActorRecord actor)
        {
            if (actor == null) return;
            int turns = MovementSpeedTurns(actor);
            if (turns != 0) Status(actor, StatusId.SPEED, (sbyte)(turns - System.Math.Sign(turns)));
        }

        private void TickItemStatuses(ActorRecord actor, bool carried = false)
        {
            if (!carried)
            {
                TickItemTimer(_mpProtection, actor);
                TickItemTimer(_doubleMpCost, actor);
            }
            for (int i = 0; i < 4; i++)
            {
                sbyte id = actor.StatusId(i);
                sbyte value = actor.StatusValue(i);
                if (id is 0 or StatusId.CURSE || (carried && id is not (StatusId.SLEEP or StatusId.FROG))) continue;
                if (id == StatusId.POISON && value > 0) _pendingPoison.Add(actor);
                if (id == StatusId.MP_DRAIN && value > 0 && !_mpProtection.ContainsKey(actor)) actor.CurrentMp = Mathf.Max(0, actor.CurrentMp - 1);
                value = (sbyte)(value - System.Math.Sign(value));
                actor.SetStatus(i, value == 0 ? (sbyte)0 : id, value);
            }
        }

        private void ResolvePoisonDamage()
        {
            // Retail caps poison using HP after the other damage committed this turn.
            foreach (var actor in _pendingPoison)
            {
                if (actor.Hp <= 1 || (actor == _playerActor && _testInvulnerable)) continue;
                int damage = Mathf.Min(actor.Hp - 1, (actor.MaxHp + 15) / 16);
                if (damage == 0) continue;
                int remaining = actor.Hp - damage;
                AdjustItemHealth(actor, -damage);
                actor.Hp = StatByte(remaining);
                if (actor == _playerActor) _towerRun?.SetHealth(_health);
                RecordTowerHazard(actor, damage, "poison", "envenenamento");
            }
            _pendingPoison.Clear();
        }

        private static bool CanCastMagic(ActorRecord actor) =>
            !IsFrog(actor) && !ItemImmobilized(actor) && !Combat.HasStatus(actor, StatusId.MAGIC_BIND);

        private void CastCrystalBall(DungeonItemEntry item, ActorRecord caster)
        {
            if (!CanCastMagic(caster) || IsSubmerged(caster)) return;
            if (caster == _familiarActor && !SpendMonsterMp(caster, HasTalent(caster, 256) ? 128 : 256)) return;
            Vector2Int origin = caster == _familiarActor ? _familiarCell : _playerCell;
            bool confused = caster == _playerActor ? KohIsConfused : Combat.HasStatus(caster, StatusId.CONFUSION);
            Vector2Int direction = confused && item.Subtype is not (6 or 7)
                ? RollTowerDirection(allowDiagonals: true) : _towerFacing;
            ActorRecord target = null;
            Vector2Int end = origin;
            Vector2Int lastFree = origin;
            for (int distance = 1; distance <= 8; distance++)
            {
                var cell = origin + direction * distance;
                if (!InsideMap(cell) || _walls.Contains(cell)) break;
                end = cell;
                int enemy = CreatureAt(cell);
                if (enemy >= 0 && !IsSubmerged(_creatures[enemy].Actor)) { target = _creatures[enemy].Actor; break; }
                if (_familiar && caster != _familiarActor && cell == _familiarCell && !IsSubmerged(_familiarActor))
                { target = _familiarActor; break; }
                if (_blueCollarAlly?.Cell == cell && !IsSubmerged(_blueCollarAlly.Actor))
                { target = _blueCollarAlly.Actor; break; }
                if (caster != _playerActor && cell == _playerCell) { target = _playerActor; break; }
                if (cell != _stairCell && !_npcCells.Contains(cell) && !_itemMarkers.ContainsKey(cell)) lastFree = cell;
            }
            int spell = BallSpellIds[item.Subtype];
            int family = SpellFamily(spell);
            int element = family <= 4 ? Element.Fire : family <= 9 || spell is 44 or 49 ? Element.Water : Element.Wind;
            bool self = family is 5 or 6;
            bool reflected = !self && target != null && target != caster && caster != _playerActor && ReflectMonsterSpell(target);
            Vector2Int destination = self || reflected ? origin : family == 7 && lastFree != origin ? origin + direction : end;
            QueueSpellCue(spell, element, origin, destination, reflected ? end : null);
            int level = BallSpellLevel(item.Param);
            _spellImpactAction += () => ResolveCrystalBall(spell, element, caster, reflected ? caster : target,
                origin, destination, direction, lastFree, level);
        }

        private void ResolveCrystalBall(int spell, int element, ActorRecord caster, ActorRecord target,
            Vector2Int origin, Vector2Int end, Vector2Int direction, Vector2Int lastFree, int level)
        {
            int family = SpellFamily(spell);
            switch (family)
            {
                case 5: Status(caster, WallProtectionStatus(spell), 8); return;
                case 6: _reflectionTurns = 8; return;
                case 7:
                    Vector2Int ice = origin + direction;
                    if (lastFree != origin && InsideMap(ice) && !_walls.Contains(ice) &&
                        ice != _playerCell && ice != _stairCell && !_npcCells.Contains(ice) && !_itemMarkers.ContainsKey(ice) &&
                        (!_familiar || ice != _familiarCell) && CreatureAt(ice) < 0 && !BellCellOccupied(ice))
                    {
                        _walls.Add(ice);
                        var view = CreateMarker(ice, "Ice Rock", _earlyTowerTiles ? _earlyTowerTiles : _accentMaterial, PrimitiveType.Cube, 0.75f);
                        _iceBlocks.Add((ice, view, 8));
                    }
                    return;
            }
            sbyte status = DirectSpellStatus(spell);
            if (status != 0)
            {
                ApplyDirectSpellStatus(caster, target, end, status, spread: target != caster);
                return;
            }
            if (target == null) return;
            switch (family)
            {
                case 8: AdjustItemHealth(target, 20); return;
                case 9: AdjustItemHealth(target, target.MaxHp); return;
                case 13: ChangeActorLevel(target, -1); return;
            }

            int power = DirectSpellPower(spell);
            if (power <= 0) return;
            int damage = SpellOrImpactDamage(target, power, EffectiveSpellLevel(caster, level), element, caster);
            damage = ScaleElevationDamage(damage, TowerTileHeight(origin), TowerTileHeight(end));
            int creature = _creatures.FindIndex(entry => entry.Actor == target);
            if (creature >= 0 && _creatures[creature].Health > 0 && damage >= _creatures[creature].Health)
                _finishingBlows[target] = (caster, 0);
            AdjustItemHealth(target, -damage);
            RecordTowerHit(caster, target, damage, element: element);
            if (target != caster && ActiveCombatShield(target)?.Subtype == LiveShield)
                ApplyLiveShield(target, caster, damage);
        }

        private void ApplyDirectSpellStatus(ActorRecord caster, ActorRecord target, Vector2Int cell,
            sbyte status, bool spread)
        {
            sbyte turns = status == StatusId.BLIND ? BlindnessTurns : status == StatusId.CONFUSION ? ChaosTurns : (sbyte)8;
            if (target != null && target.Hp > 0 && !IsSubmerged(target)) Status(target, status, turns);
            if (!spread || status != StatusId.POISON) return;
            int room = BellRoomAt(cell);
            if (room < 0) return;

            void PoisonInRoom(ActorRecord actor, Vector2Int position)
            {
                if (actor != target && actor.Hp > 0 && !IsSubmerged(actor) && BellRoomAt(position) == room) Status(actor, status, turns);
            }

            if (_creatures.Exists(creature => creature.Actor == caster))
            {
                PoisonInRoom(_playerActor, _playerCell);
                if (_familiar) PoisonInRoom(_familiarActor, _familiarCell);
                foreach (var ally in _thrownEggs)
                    if (ally.View && ally.Turns > 0) PoisonInRoom(ally.Actor, ally.Cell);
            }
            else
                foreach (var creature in _creatures) PoisonInRoom(creature.Actor, creature.Cell);
        }

        private void ResolveThrownItem(DungeonItemEntry item, ActorRecord target, Vector2Int impact, Vector2Int landing)
        {
            if (TryRecycleThrownItem(target, impact)) return;
            if (item.Kind == MonsterEgg.Kind)
            {
                var data = FindEggSpecies(item.Subtype);
                if (data == null) return;
                bool occupied = landing == _playerCell || CreatureAt(landing) >= 0 ||
                    (_familiar && landing == _familiarCell) || _thrownEggs.Exists(ally => ally.Cell == landing);
                if (occupied && !TryFindHatchCell(out landing))
                { _message = T("There is no room for the egg to hatch.", "Não há espaço para o ovo chocar."); return; }
                var view = SpawnActor(HatchedFamiliarPrefab(item.Subtype), landing, data.name + " (thrown egg)",
                    species: item.Subtype, palette: FamiliarPalette(item.Subtype));
                var actor = new ActorRecord { Species = item.Subtype };
                if (!TryGetLevelStats(item.Subtype, _floor, out var stats))
                    stats = new LevelStats { level = _floor, hp = data.hp, mp = data.mp, atk = data.attack, def = data.defense };
                ApplyFreshStats(actor, stats);
                _thrownEggs.Add(new ThrownEggAlly { View = view, Cell = landing, Actor = actor });
                SpawnFx(_fxMagic, CellPosition(landing));
                _message = $"{data.name} " + T("hatched at level ", "nasceu no nÃ­vel ") + _floor + ".";
            }
            else if (target != null && item.Kind == ItemKind.Crystal && item.Subtype == 3 &&
                _creatures.Exists(creature => creature.Actor == target))
                RemoveFruitTarget(target, out _);
            else if (target != null && item.Kind == ItemKind.Seed && item.Subtype is >= 1 and <= TovarSeed)
                ApplySeed(item.Subtype, target);
            else if (target != null && item.Kind == ItemKind.Scroll)
            {
                if (item.Subtype == 1) CreateSacredTile(impact);
                else if (item.Subtype == 2) Status(target, StatusId.CONFUSION, 10);
            }
            else if (target != null && item.Kind is ItemKind.Herb or ItemKind.Fruit)
            {
                if (item.Kind == ItemKind.Fruit && item.Subtype is 7 or 9)
                {
                    if (item.Subtype == 7) RevertToEgg(target);
                    else RemoveFruitTarget(target, out _);
                }
                else if (IsPitaFruit(item) ? target != _playerActor :
                    CanUseInventoryEffect(item, target == _familiarActor || target == _blueCollarAlly?.Actor))
                    ApplyInventoryEffect(item, target, thrown: true);
            }
        }

        private void PrepareThrownEggTurn(Vector2Int? reservedCell, Vector2Int kohDestination, ThrownEggAlly pushedAlly)
        {
            foreach (var ally in _thrownEggs)
            {
                if (ally == _blueCollarAlly || ally.Turns <= 0 || !ally.View) continue;
                ally.Turns--;
                if (ally == pushedAlly || MonsterExhausted(ally.Actor) || ItemImmobilized(ally.Actor) ||
                    SlowBlocksAction(ally.Actor) || (!ally.Salamander && _creatures.Count == 0)) continue;
                int target = -1;
                int start = ally.Salamander ? 0 : RollSpawnRange(_creatures.Count);
                if (!ally.Salamander) target = start;
                for (int i = 0; i < _creatures.Count; i++)
                {
                    int candidate = (start + i) % _creatures.Count;
                    int distance = Distance(ally.Cell, _creatures[candidate].Cell);
                    if (ally.Salamander && (target < 0 || distance < Distance(ally.Cell, _creatures[target].Cell)))
                        target = candidate;
                    if (distance != 1) continue;
                    target = candidate;
                    break;
                }
                bool blind = Combat.HasStatus(ally.Actor, StatusId.BLIND);
                bool confused = Combat.HasStatus(ally.Actor, StatusId.CONFUSION);
                var cell = target >= 0 ? _creatures[target].Cell : _playerCell;
                if (target >= 0 && !IsFrog(ally.Actor) && Distance(ally.Cell, cell) == 1)
                {
                    var direction = confused ? RollTowerDirection(allowDiagonals: true) : cell - ally.Cell;
                    cell = ally.Cell + direction;
                    if (confused) target = HasTowerPassage(ally.Cell, direction) ? CreatureAt(cell) : -1;
                    QueueMonsterSpriteAction(ally.View, direction);
                    SpendMonsterMp(ally.Actor, 256);
                    if (target >= 0)
                        QueueCreatureDamage(ally.Actor, target, WithItemCombatEffects(ally.Actor, ally.Actor.CurrentAttack,
                            _creatures[target].Actor, ally.Cell, cell));
                }
                else
                {
                    bool Available(Vector2Int tile) => tile != reservedCell && tile != _playerCell && tile != kohDestination &&
                        (!_familiar || tile != _familiarCell) && CreatureAt(tile) < 0 &&
                        !_thrownEggs.Exists(other => other != ally && other.View && other.Cell == tile);
                    var step = confused || blind ? RandomTowerStep(ally.Cell) : ChaseStep(ally.Cell, cell);
                    var next = ally.Cell + step;
                    if (!Available(next)) continue;
                    var extraStep = ally.Salamander && !confused && !blind ? ChaseStep(next, cell) : step;
                    var extra = next + extraStep;
                    if (step != Vector2Int.zero && extraStep != Vector2Int.zero &&
                        (MovementSpeedTurns(ally.Actor) > 0 || (ally.Salamander && HasTalent(ally.Actor, QuickTalent))) && extra != _stairCell &&
                        TrapAt(next) is not { IsActive: true } && CanTowerStep(next, extraStep) &&
                        CanClimbTowerHeight(ally.Cell, extra) && Available(extra))
                        next = extra;
                    ally.View.GetComponent<ActorLocomotionView>()?.FaceMonster(new Vector3(step.x, 0f, step.y));
                    ally.View.GetComponent<KewneSpriteView>()?.Face(step);
                    ally.Cell = next;
                    ally.View.transform.position = CellPosition(next);
                    if (step != Vector2Int.zero)
                    {
                        if (_doubleMpCost.ContainsKey(ally.Actor)) SpendMonsterMp(ally.Actor, 256);
                        TriggerTrapFor(next, ally.Actor);
                        if (ally.Actor.Hp == 0) ally.Turns = 0;
                    }
                }
            }
        }

        private void CompleteThrownEggTurn()
        {
            if (_blueCollarAlly != null && _blueCollarAlly.Actor.Hp == 0)
            {
                _blueCollarFamiliar.Defeated = true;
                PutBlueCollarFamiliarInBag();
            }
            for (int i = _thrownEggs.Count - 1; i >= 0; i--)
            {
                if (_thrownEggs[i].Turns > 0) continue;
                if (_thrownEggs[i].View) RetireTowerHitView(_thrownEggs[i].View);
                _thrownEggs.RemoveAt(i);
            }
        }
    }
}
