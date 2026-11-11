using System.Collections;
using System.Collections.Generic;
using AzureDreams.Core;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private sealed class FusionPreview
        {
            public ActorRecord Red, Blue, Result;
            public bool BlueSurvives;
            public string Name;
        }

        private FusionPreview _fusionPreview;
        private bool _fusionPlaying;

        private bool FusionBlockedByFrog => IsFrog(_familiarActor) || IsFrog(_blueCollarAlly?.Actor);

        private static string FrogFusionMessage() => T(
            "A familiar is a frog. Wait for the effect to wear off before fusing.",
            "Um familiar está em forma de sapo. Espere o efeito passar para fundir.");

        private FusionPreview BuildFusionPreview()
        {
            if (_area != Area.Tower || !HasBlueCollar || !_familiar || !_blueCollarAlly?.View ||
                _familiarHealth <= 0 || _blueCollarAlly.Actor.Hp == 0 ||
                FusionBlockedByFrog) return null;
            var red = _familiarActor;
            var blue = _blueCollarAlly.Actor;
            int Priority(ActorRecord actor) => actor.Species switch
            {
                (byte)MonsterSpecies.KEWNE => 2,
                (byte)MonsterSpecies.HIKEWNE => -1,
                _ => (actor.Flags14 & LevaFusionProtection) != 0 ? 1 : 0
            };
            int redPriority = Priority(red), bluePriority = Priority(blue);
            if (redPriority == 1 && bluePriority == 1) return null;
            bool blueSurvives = redPriority != bluePriority ? bluePriority > redPriority :
                red.Level != blue.Level ? blue.Level > red.Level :
                red.Experience != blue.Experience ? blue.Experience > red.Experience : (_completedTowerTurns & 1) == 0;
            var survivor = blueSurvives ? blue : red;
            var donor = blueSurvives ? red : blue;
            return new FusionPreview { Red = red, Blue = blue, Result = ComputeFusionResult(survivor, donor),
                BlueSurvives = blueSurvives, Name = blueSurvives ? _blueCollarFamiliar.Name : FamiliarDisplayName };
        }

        private ActorRecord ComputeFusionResult(ActorRecord survivor, ActorRecord donor)
        {
            var result = survivor.Clone();
            result.Flags14 &= ~LevaFusionProtection;
            int element = UnitElement(survivor), other = UnitElement(donor);
            if (element == 0 || (element, other) is (Element.Fire, Element.Water) or
                (Element.Water, Element.Wind) or (Element.Wind, Element.Fire))
                element = other;
            result.Flags14 = (result.Flags14 & ~7) | element;
            result.Flags1C = (result.Flags1C & ~7) | element;
            int talents = (survivor.StatusFlags & (UnitProfile(survivor.Species)?.talentIds ?? 0)) |
                (donor.StatusFlags & (UnitProfile(donor.Species)?.talentIds ?? 0));
            ApplyFusionTalents(result, talents);
            int mp = unchecked((ushort)(survivor.MpRaw + donor.MpRaw)) >> 9;
            result.MpRaw = (ushort)((Mathf.Min(result.MaxMp, mp) << 8) | 0xff);
            InheritFusionSpells(result, survivor, donor, element);
            return result;
        }

        private static void ApplyFusionTalents(ActorRecord actor, int talents)
        {
            int old = actor.StatusFlags;
            int Delta(int mask, int basis) =>
                StatByte((talents & mask) != 0 ? basis * 2 : basis) -
                StatByte((old & mask) != 0 ? basis * 2 : basis);
            int hpDelta = Delta(2, actor.BaseHp);
            actor.MaxHp = StatByte(Mathf.Max(1, actor.MaxHp + hpDelta));
            actor.Hp = (byte)Mathf.Clamp(actor.Hp + hpDelta, 1, actor.MaxHp);
            actor.MaxMp = StatByte(actor.MaxMp + Delta(4, actor.BaseMp));
            int attackDelta = Delta(8, actor.BaseAttack), defenseDelta = Delta(16, actor.BaseDefense);
            actor.CurrentAttack = StatByte(actor.CurrentAttack + attackDelta);
            actor.EffectiveAttack = StatByte(actor.EffectiveAttack + attackDelta);
            actor.CurrentDefense = StatByte(actor.CurrentDefense + defenseDelta);
            actor.EffectiveDefense = StatByte(actor.EffectiveDefense + defenseDelta);
            actor.StatusFlags = talents;
        }

        private List<(int Id, int Level)> KnownFusionSpells(ActorRecord actor)
        {
            var spells = new List<(int Id, int Level)>(3);
            void Add(int id, int level)
            {
                if (id <= 0 || id == 0x2e) return;
                int index = spells.FindIndex(spell => SpellFamily(spell.Id) == SpellFamily(id));
                if (index < 0) spells.Add((id, Mathf.Clamp(level, 1, 99)));
                else if (level > spells[index].Level) spells[index] = (id, Mathf.Min(level, 99));
            }
            for (int offset = 8; offset < 17; offset += 3) Add(actor.U8(offset), actor.U8(offset + 1));
            if (actor.Spells != null)
                foreach (var spell in actor.Spells)
                    if (spell != null) Add(spell.SpellId, spell.StoredLevel);
            if (spells.Count == 0) Add(UnitProfile(actor.Species)?.nativeSpellId ?? 0, actor.Level);
            return spells;
        }

        private void InheritFusionSpells(ActorRecord result, ActorRecord survivor, ActorRecord donor, int element)
        {
            var own = KnownFusionSpells(survivor);
            var inherited = KnownFusionSpells(donor);
            int limit = Mathf.Max(own.Count, inherited.Count) >= 3 ? 3 : 2;
            var selected = new List<(int Id, int Level)>(limit);
            void Add((int Id, int Level) spell)
            {
                int index = selected.FindIndex(entry => SpellFamily(entry.Id) == SpellFamily(spell.Id));
                if (index >= 0)
                {
                    if (spell.Level > selected[index].Level) selected[index] = spell;
                }
                else if (selected.Count < limit) selected.Add(spell);
            }
            void AddNative(ActorRecord actor, List<(int Id, int Level)> spells)
            {
                int native = UnitProfile(actor.Species)?.nativeSpellId ?? 0;
                if (native <= 0) return;
                foreach (var spell in spells)
                    if (SpellFamily(spell.Id) == SpellFamily(native)) { Add(spell); return; }
            }
            AddNative(survivor, own);
            AddNative(donor, inherited);
            for (int i = 0; i < Mathf.Max(own.Count, inherited.Count); i++)
            {
                if (i < own.Count) Add(own[i]);
                if (i < inherited.Count) Add(inherited[i]);
            }
            if (selected.Count == 0)
            {
                int hidden = UnitProfile(result.Species)?.hiddenSpellId ?? 0;
                if (hidden > 0 && hidden != 0x2e) Add((hidden, 1));
            }
            int variant = element == Element.Fire ? 1 : element == Element.Water ? 2 : element == Element.Wind ? 3 : 0;
            for (int i = 0; i < 3; i++)
            {
                int id = i < selected.Count ? selected[i].Id : 0;
                int level = i < selected.Count ? selected[i].Level : 0;
                if (id > 0 && id <= 45 && variant != 0) id = SpellFamily(id) * 3 + variant;
                int offset = 8 + i * 3;
                result.SetRaw(offset, (byte)id);
                result.SetRaw(offset + 1, (byte)level);
                result.SetRaw(offset + 2, (byte)level);
            }
        }

        private void ConfirmFamiliarFusion()
        {
            var preview = _fusionPreview;
            if (preview == null || _fusionPlaying || _turnBusy || !_familiar || !_blueCollarAlly?.View ||
                _familiarActor != preview.Red || _blueCollarAlly.Actor != preview.Blue ||
                KohSkipsAction || ItemImmobilized(_playerActor) || SlowBlocksAction(_playerActor)) return;
            if (FusionBlockedByFrog)
            {
                _fusionPreview = null;
                return;
            }
            if (ScheduleInventoryAction(() => _finishFamiliarAction = () =>
            {
                if (!_familiar || !_blueCollarAlly?.View || _familiarActor != preview.Red ||
                    _blueCollarAlly.Actor != preview.Blue || _familiarHealth <= 0 || preview.Blue.Hp <= 0 ||
                    _health <= 0) return;
                if (FusionBlockedByFrog)
                {
                    _message = FrogFusionMessage();
                    return;
                }
                StartCoroutine(PresentFamiliarFusion(preview));
            }))
            {
                _familiarOrderPicker = FamiliarOrderPicker.None;
                _fusionPreview = null;
            }
        }

        private IEnumerator PresentFamiliarFusion(FusionPreview preview)
        {
            var redView = _familiar;
            var blueView = _blueCollarAlly.View;
            Vector3 redStart = redView.transform.position, blueStart = blueView.transform.position;
            Vector3 center = (redStart + blueStart) * 0.5f;
            var redSprites = redView.GetComponentsInChildren<SpriteRenderer>();
            var blueSprites = blueView.GetComponentsInChildren<SpriteRenderer>();
            var redColors = System.Array.ConvertAll(redSprites, sprite => sprite.color);
            var blueColors = System.Array.ConvertAll(blueSprites, sprite => sprite.color);
            string musicCue = _playingMusicCue;
            _fusionPlaying = true;
            try
            {
                // MERGE.STR, file 1/channel 0, sectors 0 through 0x52f.
                var cue = Resources.Load<AudioClip>("Sfx/Retail/Fusion");
                SetAreaMusicPaused(true);
                if (cue) PlayRetailClip(cue, 0.75f);
                else PlayRetailSound(RetailSfx.Restore);
                bool flashed = false;
                float duration = cue ? cue.length : 7f;
                for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
                {
                    if (!redView || !blueView || _familiar != redView || _blueCollarAlly?.View != blueView) yield break;
                    float gather = Mathf.SmoothStep(0f, 1f, elapsed / 4.5f);
                    float darkness = Mathf.Lerp(Mathf.SmoothStep(1f, 0.28f, elapsed / 2f), 1f,
                        Mathf.InverseLerp(6.6f, duration, elapsed));
                    float rise = Mathf.Sin(gather * Mathf.PI) * 0.65f;
                    Vector3 lift = Vector3.up * rise;
                    redView.transform.position = Vector3.Lerp(redStart, center, gather) + lift;
                    blueView.transform.position = Vector3.Lerp(blueStart, center, gather) + lift;
                    float fade = Mathf.InverseLerp(6.6f, 4.5f, elapsed);
                    Tint(redSprites, redColors, darkness, preview.BlueSurvives ? fade : 1f);
                    Tint(blueSprites, blueColors, darkness, preview.BlueSurvives ? 1f : fade);
                    if (!flashed && elapsed >= 4.5f)
                    {
                        flashed = true;
                        SpawnFx(_fxMagic, center);
                    }
                    yield return null;
                }
                if (_familiar != redView || _blueCollarAlly?.View != blueView || !redView || !blueView) yield break;
                ApplyFamiliarFusion(preview, redView, blueView);
            }
            finally
            {
                Tint(redSprites, redColors, 1f, 1f);
                Tint(blueSprites, blueColors, 1f, 1f);
                if (redView) redView.transform.position = redStart;
                if (blueView) blueView.transform.position = blueStart;
                if (musicCue == _playingMusicCue && !_levelUpMusicActive) SetAreaMusicPaused(false);
                _fusionPlaying = false;
            }
            TryStartFloorCollapse();

            static void Tint(SpriteRenderer[] sprites, Color[] colors, float brightness, float alpha)
            {
                for (int i = 0; i < sprites.Length; i++)
                    if (sprites[i]) sprites[i].color = colors[i] * new Color(brightness, brightness, brightness, alpha);
            }
        }

        private void ApplyFamiliarFusion(FusionPreview preview, GameObject redView, GameObject blueView)
        {
            var winner = preview.BlueSurvives ? preview.Blue : preview.Red;
            var donor = preview.BlueSurvives ? preview.Red : preview.Blue;
            var blue = _blueCollarFamiliar;
            if (preview.BlueSurvives)
            {
                if (_parkedFamiliarDefinition) ExposeParkedFamiliar();
                _parkedFamiliarActor = blue.Temporary ? new ActorRecord() : null;
                _parkedFamiliarDefinition = null;
                _parkedFamiliarSpecies = 0;
                _hatchedSpecies = blue.Species;
                _familiarDefinition = blue.Definition;
                if (blue.Temporary) _towerHatchedNickname = blue.Nickname;
                else TownSaveVariables.SetString(FamiliarNicknameKey, blue.Nickname ?? "");
                _familiar = blueView;
                _familiarCell = _blueCollarAlly.Cell;
                _familiarLocomotion = ConfigureLocomotion(_familiar);
                _familiarAnimation = _familiar.GetComponentInChildren<AzureDreamsReimagined.Rendering.PsxSpriteRenderer>();
                _familiarWaiting = false;
                _runInventory?.ClearEquipped(FamiliarEquipped);
                if (_runInventory != null)
                    foreach (var item in _runInventory.DungeonItems)
                        if (Equipment(item) && (item.Flags & BlueFamiliarEquipped) != 0)
                            item.Flags = (byte)((item.Flags & ~BlueFamiliarEquipped) | FamiliarEquipped);
            }
            RemoveGenerated(preview.BlueSurvives ? redView : blueView);
            _thrownEggs.Remove(_blueCollarAlly);
            _bagFamiliars.Remove(blue);
            _runInventory?.ClearEquipped(BlueFamiliarEquipped);
            _blueCollarAlly = null;
            _blueCollarFamiliar = null;
            _pendingFamiliarCommands.Remove(preview.Red);
            _pendingFamiliarCommands.Remove(preview.Blue);
            _familiarOrders.Remove(donor);
            if (_familiarOrders.TryGetValue(winner, out var orders)) orders.Target = null;
            // Keep the living record so status timers and order references remain valid.
            winner.Flags14 = (winner.Flags14 & ~(LevaFusionProtection | 7)) | (preview.Result.Flags14 & 7);
            winner.Flags1C = (winner.Flags1C & ~7) | (preview.Result.Flags1C & 7);
            ApplyFusionTalents(winner, preview.Result.StatusFlags);
            winner.MpRaw = preview.Result.MpRaw;
            for (int offset = 8; offset < 17; offset++)
                winner.SetRaw(offset, preview.Result.U8(offset));
            winner.Spells = null;
            _familiarActor = winner;
            _familiarHealth = winner.Hp;
            _familiarDefeated = _familiarInBag = false;
            MonsterPalettes.Apply(_familiar, winner.Species, FamiliarPalette(winner.Species, winner));
            RefreshInventoryCommands();
            _message = T($"Fusion complete: {preview.Name}.", $"Fusão concluída: {preview.Name}.");
        }

    }
}
