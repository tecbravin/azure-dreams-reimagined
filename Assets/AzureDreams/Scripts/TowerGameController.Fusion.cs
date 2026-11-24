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
            public string RedName, BlueName;
            public string Name => BlueSurvives ? BlueName : RedName;
        }

        private FusionPreview _fusionPreview;
        private bool _fusionPlaying;

        private static string FrogFusionMessage() => T(
            "A familiar is a frog. Wait for the effect to wear off before fusing.",
            "Um familiar está em forma de sapo. Espere o efeito passar para fundir.");

        private void DrawFusionResultHeader(Rect rect, FusionPreview preview)
        {
            var result = preview.Result;
            const float portraitSize = 44f;
            if (Event.current.type == EventType.Repaint)
                DrawFamiliarStatusPortrait(new Rect(rect.x, rect.y, portraitSize, portraitSize), result,
                    FindEggSpecies(result.Species)?.name ?? preview.Name);
            float x = rect.x + portraitSize + 7f;
            DrawInventoryFittedText(new Rect(x, rect.y, rect.xMax - x, 23f),
                T($"Result: {preview.Name}", $"Resultado: {preview.Name}"), _bodyStyle);
            DrawFamiliarStatusIcon(new Rect(x, rect.y + 25f, 16f, 16f), FamiliarGenusIcon(UnitElement(result)));
            DrawInventoryFittedText(new Rect(x + 21f, rect.y + 23f, rect.xMax - x - 21f, 20f),
                T($"Lv. {result.Level}", $"Nív. {result.Level}"), _messageStyle);
        }

        // Fusion previews show the actual record that will be kept, so this
        // chart must read from the calculated result rather than either input.
        // Keeping the scale local to the four displayed values makes useful
        // differences visible for low-level familiars as well as high-level ones.
        private void DrawFusionResultStatsChart(Rect rect, ActorRecord result)
        {
            if (result == null) return;

            DrawDialogueFrame(rect);
            DrawInventoryFittedText(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, 18f),
                T("Resulting stats", "Atributos resultantes"), _messageStyle);

            int hp = Mathf.Max(1, result.MaxHp);
            int mp = Mathf.Max(1, result.MaxMp);
            int attack = Mathf.Max(1, result.CurrentAttack);
            int defense = Mathf.Max(1, result.CurrentDefense);
            int ceiling = Mathf.Max(20, hp, mp, attack, defense);
            ceiling = Mathf.CeilToInt(ceiling / 10f) * 10;

            float rowHeight = (rect.height - 24f) * 0.25f;
            float labelWidth = 28f;
            float valueWidth = 31f;
            DrawFusionStatChartBar(0, "HP", hp, new Color(0.37f, 0.79f, 0.94f));
            DrawFusionStatChartBar(1, "MP", mp, new Color(0.47f, 0.54f, 0.95f));
            DrawFusionStatChartBar(2, T("ATK", "ATQ"), attack, new Color(0.96f, 0.48f, 0.34f));
            DrawFusionStatChartBar(3, "DEF", defense, new Color(0.50f, 0.85f, 0.52f));

            void DrawFusionStatChartBar(int row, string label, int value, Color color)
            {
                float y = rect.y + 21f + row * rowHeight;
                var labelRect = new Rect(rect.x + 6f, y - 1f, labelWidth, rowHeight);
                var valueRect = new Rect(rect.xMax - valueWidth - 5f, y - 1f, valueWidth, rowHeight);
                var track = Rect.MinMaxRect(labelRect.xMax + 2f, y + 2f, valueRect.x - 3f, y + rowHeight - 2f);
                DrawInventoryFittedText(labelRect, label, _messageStyle);
                DrawHudFill(track, new Color(0.10f, 0.15f, 0.27f, 0.9f));
                DrawHudFill(new Rect(track.x, track.y, track.width * Mathf.Clamp01(value / (float)ceiling), track.height), color);
                DrawInventoryFittedText(valueRect, value.ToString(), _messageStyle);
            }
        }

        private bool CanFuseTowerPair(ActorRecord red, ActorRecord blue) =>
            _area == Area.Tower && HasBlueCollar && _familiar && _blueCollarAlly?.View &&
            red != null && blue != null && red == _familiarActor &&
            blue == _blueCollarAlly.Actor && blue == _blueCollarFamiliar?.Actor &&
            !IsFamiliarLost(red) && !IsFamiliarLost(blue) && red.Hp > 0 && blue.Hp > 0 &&
            !IsFrog(red) && !IsFrog(blue);

        private void OpenTowerFusion()
        {
            var blue = _blueCollarAlly?.Actor;
            _fusionPreview = CanFuseTowerPair(_familiarActor, blue)
                ? BuildFusionPreview(_familiarActor, FamiliarDisplayName, blue, _blueCollarFamiliar.Name)
                : null;
            _familiarOrderPicker = FamiliarOrderPicker.Fusion;
        }

        private FusionPreview BuildFusionPreview(ActorRecord red, string redName, ActorRecord blue, string blueName)
        {
            if (red == null || blue == null || red == blue || red.Hp <= 0 || blue.Hp <= 0 ||
                IsFrog(red) || IsFrog(blue)) return null;
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
                BlueSurvives = blueSurvives, RedName = redName, BlueName = blueName };
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
            if (preview == null || _fusionPlaying || _turnBusy || KohSkipsAction ||
                !CanFuseTowerPair(preview.Red, preview.Blue)) return;
            if (ScheduleInventoryAction(() => _finishFamiliarAction = () =>
            {
                if (_health > 0 && CanFuseTowerPair(preview.Red, preview.Blue))
                    StartCoroutine(PresentFamiliarFusion(preview));
            }))
            {
                _inventoryMenu = InventoryMenu.Closed;
                _familiarOrderPicker = FamiliarOrderPicker.None;
                _fusionPreview = null;
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
            if (_blueCollarAlly != null)
                _thrownEggs.Remove(_blueCollarAlly);
            if (_blueCollarFamiliar != null)
                _bagFamiliars.Remove(_blueCollarFamiliar);
            _runInventory?.ClearEquipped(BlueFamiliarEquipped);
            _blueCollarAlly = null;
            _blueCollarFamiliar = null;
            _pendingFamiliarCommands.Remove(preview.Red);
            _pendingFamiliarCommands.Remove(preview.Blue);
            _familiarOrders.Remove(donor);
            if (_familiarOrders.TryGetValue(winner, out var orders)) orders.Target = null;
            // Keep the living record so status timers and order references remain valid.
            winner.Flags14 = (winner.Flags14 & ~(LevaFusionProtection | 7)) | (preview.Result.Flags14 & 7);
            donor.Flags14 &= ~LevaFusionProtection;
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
