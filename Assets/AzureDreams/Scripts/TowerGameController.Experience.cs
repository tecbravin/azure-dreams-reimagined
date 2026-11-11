using System;
using UnityEngine;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string FamiliarExperienceKey = "home.active_familiar_exp";
        private static readonly int[] ExperienceThresholds = Leveling.BuildExperienceTable();
        [SerializeField] private TextAsset _monsterMechanics;
        [Serializable] private sealed class ProgressionCatalog
        {
            public UnitGrowth[] units;
            public LevelStats[] koh;
            public EnemyFloor[] floors;
            public TalentText[] talents;
        }
        [Serializable] private sealed class TalentText { public int talentId; public string name, ptBr; }
        [Serializable] private sealed class UnitGrowth
        {
            public int unitId, baseHp, baseMp, baseAtk, baseDef, baseAgility, baseLuck, baseExp;
            public int growthHp, growthMp, growthAtk, growthDef, growthAgility, growthLuck, growthExp, talentIds;
            public int nativeElement, nativeSpellId, hiddenSpellId, likesPitas, likesBigPitas;
            public bool pushable;
            public string accuracy, evolvesInto;
        }
        [Serializable] private sealed class EnemyFloor { public int floor; public EnemyEntry[] entries; }
        [Serializable] private sealed class EnemyEntry { public int unitId, level; }
        [Serializable] private struct LevelStats
        {
            public int level, hp, mp, atk, def, agility, luck, expGiven, talents, hit;
            public int unboostedHp, unboostedMp, unboostedAttack, unboostedDefense;
        }
        private ProgressionCatalog _progression;
        private string _experienceNotice;
        private float _experienceNoticeUntil;
        private const float LevelUpCelebrationDuration = 2.4f;
        private bool _levelUpCelebrationPending;
        private float _levelUpCelebrationElapsed = -1f;
        private bool LevelUpCelebrationBlocksTurn => _levelUpCelebrationPending || _levelUpCelebrationElapsed >= 0f;

        private bool TickLevelUpCelebration(float deltaTime)
        {
            if (!LevelUpCelebrationBlocksTurn || _turnBusy) return false;
            if (_levelUpCelebrationPending)
            {
                _levelUpCelebrationPending = false;
                _levelUpCelebrationElapsed = 0f;
                ResetTowerPushHold();
                _towerInteractPending = false;
                _playerLocomotion?.Present(Vector3.zero, 0f, false);
                PlayLevelUpMusic();
            }
            else
                _levelUpCelebrationElapsed += deltaTime;

            if (_levelUpCelebrationElapsed >= LevelUpCelebrationDuration)
                ResetLevelUpCelebration();
            else if (_kohSprite)
                _kohSprite.PresentLevelUp(_levelUpCelebrationElapsed / LevelUpCelebrationDuration);
            return true;
        }

        private void ResetLevelUpCelebration()
        {
            _levelUpCelebrationPending = false;
            _levelUpCelebrationElapsed = -1f;
            if (_kohSprite) _kohSprite.PresentLevelUp(-1f);
        }

        private ProgressionCatalog GetProgression()
        {
            if (_progression == null)
            {
                var asset = Resources.Load<TextAsset>("Town/Retail/unit_progression");
                if (!asset) asset = _monsterMechanics;
                if (asset) _progression = JsonUtility.FromJson<ProgressionCatalog>(asset.text);
            }
            return _progression;
        }

        private static readonly UnitGrowth KohBase = new()
        {
            unitId = 0, baseHp = 16, baseMp = 100, baseAtk = 5, baseDef = 4, baseAgility = 4,
            baseLuck = 32, baseExp = 16, growthHp = 22, growthAtk = 13, growthDef = 14, growthAgility = 8, growthExp = 16,
            likesPitas = 3, likesBigPitas = 3
        };

        private const int QuickTalent = 1;
        private static readonly UnitGrowth SalamanderBase = new()
        {
            unitId = (int)MonsterSpecies.SALAMAN, baseHp = 255, baseMp = 255, baseAtk = 48,
            baseDef = 1, baseAgility = 32, baseLuck = 64, nativeElement = 1,
            accuracy = "80", talentIds = QuickTalent
        };

        private bool TryGetLevelStats(int species, int level, out LevelStats stats)
        {
            level = Mathf.Clamp(level, 1, Leveling.MaxLevel);
            var catalog = GetProgression();
            if (species == 0 && catalog?.koh != null && catalog.koh.Length >= level && catalog.koh[level - 1].level == level)
            {
                stats = catalog.koh[level - 1];
                return true;
            }

            var unit = UnitProfile(species);
            stats = default;
            if (unit == null) return false;
            stats.level = level;
            stats.hp = Leveling.BaseStatAt(5, unit.baseHp, unit.growthHp, level);
            if (species == 0 && level == 92) stats.hp = 227;
            stats.mp = Leveling.BaseStatAt(4, unit.baseMp, unit.growthMp, level);
            stats.atk = Leveling.BaseStatAt(0, unit.baseAtk, unit.growthAtk, level);
            stats.def = Leveling.BaseStatAt(1, unit.baseDef, unit.growthDef, level);
            stats.agility = Leveling.BaseStatAt(2, unit.baseAgility, unit.growthAgility, level);
            stats.luck = Leveling.BaseStatAt(3, unit.baseLuck, unit.growthLuck, level);
            stats.expGiven = Leveling.ExpReward(unit.baseExp, unit.growthExp, level - 1);
            stats.talents = unit.talentIds;
            stats.hit = int.TryParse(unit.accuracy, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out int hit) ? hit : 0x7C;
            stats.unboostedHp = stats.hp; stats.unboostedMp = stats.mp;
            stats.unboostedAttack = stats.atk; stats.unboostedDefense = stats.def;
            if ((unit.talentIds & 2) != 0) stats.hp *= 2;
            if ((unit.talentIds & 4) != 0) stats.mp *= 2;
            if ((unit.talentIds & 8) != 0) stats.atk *= 2;
            if ((unit.talentIds & 16) != 0) stats.def *= 2;
            return true;
        }

        private UnitGrowth UnitProfile(int species)
        {
            var units = GetProgression()?.units;
            var unit = units == null ? null : Array.Find(units, entry => entry.unitId == species);
            return unit ?? (species switch
            {
                0 => KohBase,
                (int)MonsterSpecies.SALAMAN => SalamanderBase,
                _ => null
            });
        }

        private static bool HasTalent(ActorRecord actor, int talent) => !IsFrog(actor) && (actor.StatusFlags & talent) != 0;

        private string DescribeTalents(ActorRecord actor)
        {
            var talents = GetProgression()?.talents;
            if (talents == null || actor.StatusFlags == 0) return T("None", "Nenhum");
            var names = new System.Collections.Generic.List<string>();
            foreach (var talent in talents)
                if (HasTalent(actor, talent.talentId)) names.Add(T(talent.name, talent.ptBr));
            return names.Count == 0 ? T("None", "Nenhum") : string.Join(", ", names);
        }

        private static byte StatByte(int value) => (byte)Mathf.Clamp(value, 0, byte.MaxValue);

        private static void ApplyFreshStats(ActorRecord actor, LevelStats stats)
        {
            actor.Level = StatByte(stats.level);
            actor.StatusFlags = stats.talents;
            actor.HitBaseline = StatByte(stats.hit == 0 ? 0x7C : stats.hit);
            actor.MaxHp = actor.Hp = StatByte(stats.hp);
            actor.BaseHp = StatByte(stats.unboostedHp > 0 ? stats.unboostedHp : stats.hp);
            actor.MaxMp = StatByte(stats.mp);
            actor.BaseMp = StatByte(stats.unboostedMp > 0 ? stats.unboostedMp : stats.mp);
            actor.CurrentMp = actor.MaxMp;
            actor.CurrentAttack = actor.EffectiveAttack = StatByte(stats.atk);
            actor.CurrentDefense = actor.EffectiveDefense = StatByte(stats.def);
            actor.BaseAttack = StatByte(stats.unboostedAttack > 0 ? stats.unboostedAttack : stats.atk);
            actor.BaseDefense = StatByte(stats.unboostedDefense > 0 ? stats.unboostedDefense : stats.def);
            actor.Agility = StatByte(stats.agility);
            actor.Luck = StatByte(stats.luck);
        }

        private void ResetKohProgression()
        {
            TryGetLevelStats(0, 1, out var stats);
            ApplyFreshStats(_playerActor, stats);
            _playerActor.Experience = 0;
            _health = _playerActor.MaxHp;
        }

        private int KohMaxHealth => _area == Area.Tower && _playerActor.MaxHp > 0 ? _playerActor.MaxHp :
            _playerDefinition ? _playerDefinition.MaxHealth : 20;
        private int FamiliarMaxHealth => _familiarActor.MaxHp > 0 ? _familiarActor.MaxHp :
            _familiarDefinition ? _familiarDefinition.MaxHealth : 10;

        private ActorRecord CreateTowerEnemy(MonsterSpecies species, int? levelOverride = null, bool allowSpawnSleep = true)
        {
            var actor = FamiliarSystem.CreateFamiliar(species);
            var floors = GetProgression()?.floors;
            var floor = floors == null ? null : Array.Find(floors, data => data.floor == _floor);
            var entry = floor?.entries == null ? null : Array.Find(floor.entries, data => data.unitId == (int)species);
            if (TryGetLevelStats((int)species, levelOverride ?? entry?.level ?? 1, out var stats)) ApplyFreshStats(actor, stats);
            else actor.MaxHp = actor.Hp = StatByte(_creatureDefinition ? _creatureDefinition.MaxHealth : 10);
            if (allowSpawnSleep && !InTowerTutorial && species != BeldoBossSpecies && !levelOverride.HasValue && _towerRun?.Rng is { } rng)
                RollMonsterSpawnSleep(actor, rng);
            if (species == MonsterSpecies.TROLL && _towerRun?.Rng is { } weaponRng)
            {
                byte subtype = TowerEncounterTable.RollTrollWeapon(_floor, weaponRng);
                if (subtype != 0) actor.Weapon = new DungeonItemEntry(subtype, ItemSystem.Kind.Sword, 0, 0);
            }
            return actor;
        }

        private static Texture2D EnemyTrollActionAtlas(ActorRecord actor)
        {
            if (actor.Species != (byte)MonsterSpecies.TROLL) return null;
            string atlas = actor.Weapon?.Subtype switch
            {
                TowerEncounterTable.TrollHammer => "TrollHammerActions",
                TowerEncounterTable.TrollBowGun => "TrollBowActions",
                TowerEncounterTable.TrollSword => "TrollSwordActions",
                _ => null
            };
            return atlas == null ? null : Resources.Load<Texture2D>("Tower/" + atlas);
        }

        private void RestoreLegacyFamiliarProgression(ActorRecord actor, ActorDefinition definition, int hatchedSpecies, int experience)
        {
            actor.Species = (byte)(hatchedSpecies > 0 ? hatchedSpecies :
                definition == _kewneDefinition ? (int)MonsterSpecies.KEWNE : 0);
            actor.Experience = Math.Max(0, experience);
            int level = LevelForExperience(1, actor.Experience);
            if (actor.Species == 0 || !TryGetLevelStats(actor.Species, level, out var stats))
            {
                var egg = actor.Species == 0 ? null : FindEggSpecies(actor.Species);
                stats = new LevelStats { level = level, hp = definition.MaxHealth, mp = egg?.mp ?? 100,
                    atk = definition.AttackDamage, def = egg?.defense ?? 0 };
            }
            ApplyFreshStats(actor, stats);
        }

        private static int LevelForExperience(int level, int experience)
        {
            while (Leveling.CanGainLevel(ExperienceThresholds, level, experience)) level++;
            return level;
        }

        private void AddExperience(ActorRecord actor, int award, ref int health)
        {
            actor.Experience = (int)Math.Min(int.MaxValue, (long)actor.Experience + award);
            SetActorLevel(actor, LevelForExperience(Mathf.Max(1, actor.Level), actor.Experience), ref health);
        }

        private void ChangeActorLevel(ActorRecord actor, int delta)
        {
            int oldLevel = Mathf.Max(1, actor.Level);
            int level = Mathf.Clamp(oldLevel + delta, 1, Leveling.MaxLevel);
            if (oldLevel == level) return;
            int minimumXp = ExperienceThresholds[level - 1];
            int maximumXp = level == Leveling.MaxLevel ? int.MaxValue : ExperienceThresholds[level] - 1;
            actor.Experience = Mathf.Clamp(actor.Experience, minimumXp, maximumXp);
            int creatureIndex = _creatures.FindIndex(creature => creature.Actor == actor);
            int health = actor == _playerActor ? _health : actor == _familiarActor ? _familiarHealth :
                creatureIndex >= 0 ? _creatures[creatureIndex].Health :
                actor == _carriedMonster.Actor ? _carriedMonster.Health : actor.Hp;
            SetActorLevel(actor, level, ref health);
            if (actor == _playerActor) _health = health;
            else if (actor == _familiarActor) _familiarHealth = health;
            else if (actor == _carriedMonster.Actor) _carriedMonster.Health = health;
            else if (creatureIndex >= 0)
            {
                var creature = _creatures[creatureIndex];
                creature.Health = health;
                _creatures[creatureIndex] = creature;
            }
        }

        private const int FamiliarEvolutionLevel = 20;

        private void SetActorLevel(ActorRecord actor, int level, ref int health)
        {
            int oldLevel = Mathf.Max(1, actor.Level);
            var origin = level >= FamiliarEvolutionLevel && level > oldLevel &&
                (actor.Flags14 & LevaFusionProtection) == 0 ? UnitProfile(actor.Species) : null;
            if (origin != null && byte.TryParse(origin.evolvesInto, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out byte species) && UnitProfile(species) is { } evolved)
            {
                bool primary = actor == _familiarActor && _familiarDefinition;
                var reserve = primary ? null : _bagFamiliars.Find(familiar => familiar.Actor == actor);
                var definition = primary || reserve != null ? EggFamiliarDefinition(species) : null;
                if (definition)
                {
                    // At 20 the familiar keeps the growth it earned in its original form.
                    ApplyActorLevelGrowth(actor, Mathf.Max(FamiliarEvolutionLevel, oldLevel), ref health);
                    EvolveFamiliar(actor, reserve, definition, species, evolved.baseLuck - origin.baseLuck);
                }
            }
            ApplyActorLevelGrowth(actor, level, ref health);
            if (actor == _playerActor && level > oldLevel && _area == Area.Tower)
                _levelUpCelebrationPending = true;
        }

        private void EvolveFamiliar(ActorRecord actor, BagFamiliar reserve, ActorDefinition definition, byte species, int luckDelta)
        {
            string previous = reserve?.Name ?? FamiliarDisplayName;
            actor.Species = species;
            actor.Luck = StatByte(actor.Luck + luckDelta);
            if (reserve == null)
            {
                _hatchedSpecies = species;
                _familiarDefinition = definition;
                if (_familiar)
                {
                    _familiar = ReplaceEvolvedFamiliarView(_familiar, _familiarCell, actor, FamiliarDisplayName);
                    _familiarLocomotion = ConfigureLocomotion(_familiar);
                    _familiarAnimation = _familiar.GetComponentInChildren<AzureDreamsReimagined.Rendering.PsxSpriteRenderer>();
                }
            }
            else
            {
                reserve.Species = species;
                reserve.Definition = definition;
                if (reserve == _blueCollarFamiliar && _blueCollarAlly?.View)
                {
                    _blueCollarAlly.View = ReplaceEvolvedFamiliarView(_blueCollarAlly.View, _blueCollarAlly.Cell, actor, reserve.Name);
                    ConfigureLocomotion(_blueCollarAlly.View);
                }
            }
            _message = JoinTowerMessages(_message, T($"{previous} evolved into {definition.DisplayName}!",
                $"{previous} evoluiu para {definition.DisplayName}!"));
        }

        private GameObject ReplaceEvolvedFamiliarView(GameObject previous, Vector2Int cell, ActorRecord actor, string name)
        {
            var view = SpawnActor(HatchedFamiliarPrefab(actor.Species), cell, "Familiar - " + name, species: actor.Species,
                palette: FamiliarPalette(actor.Species, actor));
            view.transform.SetPositionAndRotation(previous.transform.position, previous.transform.rotation);
            _frogViews.Remove(previous);
            previous.SetActive(false);
            RemoveGenerated(previous);
            PresentFrogForm(view, actor);
            SpawnFx(_fxMagic, view.transform.position);
            return view;
        }

        private void ApplyActorLevelGrowth(ActorRecord actor, int level, ref int health)
        {
            int oldLevel = Mathf.Max(1, actor.Level);
            actor.Level = StatByte(level);
            if (level == oldLevel || !TryGetLevelStats(actor.Species, oldLevel, out var oldStats) ||
                !TryGetLevelStats(actor.Species, level, out var newStats)) return;

            if (level > oldLevel)
                for (int offset = 8; offset < 17; offset += 3)
                {
                    if (actor.U8(offset) == 0) continue;
                    int stored = actor.U8(offset + 1);
                    for (int gainedLevel = oldLevel + 1; gainedLevel <= level; gainedLevel++)
                        stored = Leveling.SpellStoredLevelGrowth(stored, gainedLevel);
                    actor.SetRaw(offset + 1, (byte)stored);
                    actor.SetRaw(offset + 2, (byte)stored);
                }

            int oldMax = actor.MaxHp;
            int oldMp = actor.MaxMp;
            int InheritedStatDelta(int mask, int unboostedDelta) =>
                ((actor.StatusFlags & mask) != 0 ? unboostedDelta : 0) -
                ((newStats.talents & mask) != 0 ? unboostedDelta : 0);
            int deltaAttack = newStats.atk - oldStats.atk;
            int deltaDefense = newStats.def - oldStats.def;
            actor.BaseHp = StatByte(actor.BaseHp + (newStats.unboostedHp > 0 ? newStats.unboostedHp - oldStats.unboostedHp : newStats.hp - oldStats.hp));
            actor.MaxHp = StatByte(oldMax + newStats.hp - oldStats.hp +
                InheritedStatDelta(2, newStats.unboostedHp - oldStats.unboostedHp));
            actor.BaseMp = StatByte(actor.BaseMp + (newStats.unboostedMp > 0 ? newStats.unboostedMp - oldStats.unboostedMp : newStats.mp - oldStats.mp));
            actor.MaxMp = StatByte(oldMp + newStats.mp - oldStats.mp +
                InheritedStatDelta(4, newStats.unboostedMp - oldStats.unboostedMp));
            actor.BaseAttack = StatByte(actor.BaseAttack + (newStats.unboostedAttack > 0 ? newStats.unboostedAttack - oldStats.unboostedAttack : deltaAttack));
            deltaAttack += InheritedStatDelta(8, newStats.unboostedAttack - oldStats.unboostedAttack);
            actor.CurrentAttack = StatByte(actor.CurrentAttack + deltaAttack);
            actor.EffectiveAttack = StatByte(actor.EffectiveAttack + deltaAttack);
            actor.BaseDefense = StatByte(actor.BaseDefense + (newStats.unboostedDefense > 0 ? newStats.unboostedDefense - oldStats.unboostedDefense : deltaDefense));
            deltaDefense += InheritedStatDelta(16, newStats.unboostedDefense - oldStats.unboostedDefense);
            actor.CurrentDefense = StatByte(actor.CurrentDefense + deltaDefense);
            actor.EffectiveDefense = StatByte(actor.EffectiveDefense + deltaDefense);
            actor.Agility = StatByte(actor.Agility + newStats.agility - oldStats.agility);
            actor.Luck = StatByte(actor.Luck + newStats.luck - oldStats.luck);
            if (health > 0) health = Mathf.Clamp(health + actor.MaxHp - oldMax, 0, actor.MaxHp);
            actor.Hp = StatByte(health);
            actor.CurrentMp = Mathf.Clamp(actor.CurrentMp + actor.MaxMp - oldMp, 0, actor.MaxMp);
        }

        private const int GrowthPromotedTalent = 32;
        private const int AtrociousTalent = 2097152;

        private string DescribeDefeatAndGrantExperience(ActorRecord defeated, ActorRecord finisher)
        {
            string name = FindEggSpecies(defeated.Species)?.name ?? ((MonsterSpecies)defeated.Species).ToString();
            bool share = _familiar && _familiarHealth > 0;
            var blueActor = _blueCollarAlly?.Actor;
            bool shareBlue = blueActor != null && blueActor.Hp > 0;
            // Retail rounds the share up before applying each recipient's level bonus.
            int total = TryGetLevelStats(defeated.Species, defeated.Level, out var stats)
                ? stats.expGiven * (HasTalent(defeated, 64) ? 2 : 1) : 0;
            int participants = 1 + (share ? 1 : 0) + (shareBlue ? 1 : 0);
            int shareAmount = (total + participants - 1) / participants;
            int reward = ExperienceShare(_playerActor);

            int ExperienceShare(ActorRecord recipient)
            {
                int levelMultiplier = recipient.Level < defeated.Level ? 2 : 1;
                int growthMultiplier = recipient == finisher && HasTalent(recipient, GrowthPromotedTalent) ? 2 : 1;
                return Mathf.Min(ushort.MaxValue, shareAmount * levelMultiplier * growthMultiplier);
            }
            int before = _playerActor.Level;
            int previousKohXp = _playerActor.Experience;
            if (reward > 0) AddExperience(_playerActor, reward, ref _health);
            int kohXp = _playerActor.Experience - previousKohXp;
            string message = T($"Your party defeats {name}. Koh gains {kohXp} XP.",
                $"Seu grupo derrota {name}. Koh ganha {kohXp} XP.");
            if (share)
            {
                int familiarReward = ExperienceShare(_familiarActor);
                int previousFamiliarXp = _familiarActor.Experience;
                if (familiarReward > 0) AddExperience(_familiarActor, familiarReward, ref _familiarHealth);
                int familiarXp = _familiarActor.Experience - previousFamiliarXp;
                message += T($" Familiar gains {familiarXp} XP.", $" O familiar ganha {familiarXp} XP.");
            }
            if (shareBlue)
            {
                int hp = blueActor.Hp;
                int previousXp = blueActor.Experience;
                AddExperience(blueActor, ExperienceShare(blueActor), ref hp);
                int gained = blueActor.Experience - previousXp;
                string familiarName = _blueCollarFamiliar.Name;
                message += T($" {familiarName} gains {gained} XP.", $" {familiarName} ganha {gained} XP.");
            }
            _experienceNotice = $"+{kohXp} EXP" + (_playerActor.Level > before ? T($"  Level {_playerActor.Level}!", $"  Nível {_playerActor.Level}!") : "");
            _experienceNoticeUntil = Time.unscaledTime + 2f;
            if (_playerActor.Level > before)
            {
                SpawnFx(_fxMagic, CellPosition(_playerCell));
            }
            return message;
        }
    }
}
