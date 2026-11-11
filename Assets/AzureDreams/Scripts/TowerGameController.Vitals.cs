using System.Collections.Generic;
using UnityEngine;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const int SleepProofTalent = 0x02000000;
        private int _completedTowerTurns;
        private readonly Dictionary<GameObject, MonsterExhaustedView> _exhaustedViews = new();

        private static bool MonsterExhausted(ActorRecord actor) => actor != null && actor.MaxMp > 0 && actor.MpRaw == 0;

        private static void RollMonsterSpawnSleep(ActorRecord actor, RetailRng rng)
        {
            if (!HasTalent(actor, SleepProofTalent) && RollTowerRange(rng, 10) == 0)
                ActorStatusEffects.SetOrClear(actor, StatusId.SLEEP, (sbyte)(8 + RollTowerRange(rng, 33)));
        }

        private void TryWakeMonsterFromAttack(ActorRecord actor, RetailRng rng)
        {
            if (actor.Hp > 0 && Combat.HasStatus(actor, StatusId.SLEEP) && RollTowerRange(rng, 10) < 3)
                Status(actor, StatusId.SLEEP, 0);
        }

        private bool SpendMonsterMp(ActorRecord actor, int rawCost)
        {
            if (actor == null || actor.MpRaw == 0) return false;
            if (_mpProtection.ContainsKey(actor)) return true;
            if (_doubleMpCost.ContainsKey(actor)) rawCost *= 2;
            if (!IsFrog(actor) && (MonsterSpecies)actor.Species is (MonsterSpecies.GOLEM or MonsterSpecies.MAXIMUM))
                rawCost *= 2;
            Leveling.SpendActorMpRaw(actor, rawCost);
            return true;
        }

        private void TickTowerVitals()
        {
            _completedTowerTurns++;
            bool recover = _completedTowerTurns % 10 == 0;
            bool drain = _completedTowerTurns % 30 == 0;
            if (!recover && !drain) return;

            if (_health > 0)
            {
                if (recover) _health = Mathf.Min(KohMaxHealth, _health + 1);
                if (drain) SpendMonsterMp(_playerActor, 256);
                _playerActor.Hp = StatByte(_health);
                _towerRun?.SetHealth(_health);
            }
            if ((_familiar || _carriedFamiliar) && _familiarHealth > 0)
            {
                if (recover) _familiarHealth = Mathf.Min(FamiliarMaxHealth, _familiarHealth + 1);
                if (drain) SpendMonsterMp(_familiarActor, 256);
                _familiarActor.Hp = StatByte(_familiarHealth);
            }
            for (int i = 0; i < _creatures.Count; i++)
            {
                var creature = _creatures[i];
                if (creature.Health <= 0) continue;
                TickMonsterVitals(ref creature, recover, drain);
                _creatures[i] = creature;
            }
            if (_carriedMonster.Actor != null && _carriedMonster.Health > 0)
                TickMonsterVitals(ref _carriedMonster, recover, drain);
            foreach (var ally in _thrownEggs)
            {
                if (ally.Actor.Hp == 0) continue;
                if (recover && ally.Actor.Hp < ally.Actor.MaxHp) ally.Actor.Hp++;
                if (drain) SpendMonsterMp(ally.Actor, 256);
            }
        }

        private void TickMonsterVitals(ref CreatureState creature, bool recover, bool drain)
        {
            if (recover && creature.Health < creature.Actor.MaxHp) creature.Health++;
            if (drain) SpendMonsterMp(creature.Actor, 256);
            creature.Actor.Hp = StatByte(creature.Health);
        }

        private readonly Dictionary<GameObject, (GameObject Frog, Renderer[] Hidden, MonsterTurnSpriteView Sprite)> _frogViews = new();

        private void PresentFrogForm(GameObject view, ActorRecord actor)
        {
            if (!view) return;
            if (_frogViews.TryGetValue(view, out var form))
            {
                if (IsFrog(actor)) return;
                foreach (var renderer in form.Hidden)
                    if (renderer) renderer.forceRenderingOff = false;
                if (form.Frog)
                {
                    form.Frog.SetActive(false);
                    Destroy(form.Frog);
                }
                _frogViews.Remove(view);
                return;
            }
            if (!IsFrog(actor)) return;
            var prefab = RegisteredMonsterPrefab((int)MonsterSpecies.FROG);
            if (!prefab) return;
            var hidden = System.Array.FindAll(view.GetComponentsInChildren<Renderer>(), renderer => !renderer.forceRenderingOff);
            var frog = Instantiate(prefab, view.transform, false);
            foreach (var renderer in hidden) renderer.forceRenderingOff = true;
            var sprite = frog.GetComponentInChildren<MonsterTurnSpriteView>();
            if (sprite)
            {
                sprite.transform.localScale *= 2f;
                sprite.PlaybackSpeed = TowerAnimationSpeed;
            }
            _frogViews.Add(view, (frog, hidden, sprite));
        }

        private void PresentMonsterExhaustion()
        {
            if (_area != Area.Tower) return;
            PresentFrogForm(_player, _playerActor);
            if (_player && _frogViews.TryGetValue(_player, out var form) && form.Sprite)
                form.Sprite.Face(_towerFacing);
            foreach (var creature in _creatures) PresentMonsterExhaustion(creature.View, creature.Actor);
            if (_familiar) PresentMonsterExhaustion(_familiar, _familiarActor);
            if (_carriedFamiliar) PresentMonsterExhaustion(_carriedView, _familiarActor, carried: true);
            else if (_carriedMonster.Actor != null) PresentMonsterExhaustion(_carriedView, _carriedMonster.Actor, carried: true);
            foreach (var ally in _thrownEggs) PresentMonsterExhaustion(ally.View, ally.Actor);
        }

        private void PresentMonsterExhaustion(GameObject view, ActorRecord actor, bool carried = false)
        {
            if (!view) return;
            PresentFrogForm(view, actor);
            bool exhausted = !carried && MonsterExhausted(actor);
            bool sleeping = exhausted || (!carried && actor != null && Combat.HasStatus(actor, StatusId.SLEEP));
            if (view.GetComponentInChildren<MonsterTurnSpriteView>()?.PresentSleep(sleeping, exhausted) == true ||
                view.GetComponent<KewneSpriteView>()?.PresentSleep(sleeping, exhausted) == true)
            {
                if (_exhaustedViews.TryGetValue(view, out var fallback)) fallback.Present(false, false);
                return;
            }
            if (!_exhaustedViews.TryGetValue(view, out var effect))
            {
                if (!sleeping) return;
                effect = view.AddComponent<MonsterExhaustedView>();
                _exhaustedViews.Add(view, effect);
            }
            effect.Present(sleeping, exhausted);
        }
    }
}
