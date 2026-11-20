using System.Collections.Generic;
using AzureDreams.Core;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Monster Shop "Magic Infusion": the Master raises one spell of a familiar by one level for
    /// 500G, as long as the spell level stays below the familiar's own level. Works on the active
    /// familiar and on every familiar kept in the hut (_bagFamiliars), editing the same native
    /// spell slots (offsets 8/11/14) that ball learning, fusion and level-ups use.
    /// </summary>
    public sealed partial class TowerGameController
    {
        private const int MagicInfusionCost = 500;
        private readonly List<(string Name, ActorRecord Actor)> _infusionFamiliars = new();
        private ActorRecord _infusionTarget;
        private string _infusionTargetName;

        private void OpenMagicInfusion()
        {
            _infusionFamiliars.Clear();
            if (_familiarDefinition && _familiarActor != null) _infusionFamiliars.Add((FamiliarDisplayName, _familiarActor));
            foreach (var familiar in _bagFamiliars)
                if (!familiar.Temporary && familiar.Actor != null && familiar.Definition)
                    _infusionFamiliars.Add((familiar.Name, familiar.Actor));
            _infusionTarget = null;
            _shopPurchaseMessage = null;
            SetMonsterShopPage(MonsterShopPage.InfusionFamiliar);
        }

        private void SelectInfusionFamiliar(int index)
        {
            if (index < 0 || index >= _infusionFamiliars.Count) return;
            (_infusionTargetName, _infusionTarget) = _infusionFamiliars[index];
            _shopPurchaseMessage = null;
            SetMonsterShopPage(MonsterShopPage.InfusionSpell);
        }

        private List<(int Spell, int Level)> InfusionSpells =>
            _infusionTarget != null ? FamiliarSpellList(_infusionTarget) : new List<(int Spell, int Level)>();

        private static string InfusionSpellName(int spell)
        {
            int family = SpellFamily(spell);
            return family >= 0 && family < FamiliarSpellNames.Length ? FamiliarSpellNames[family] : "?";
        }

        private string InfusionSpellRow((int Spell, int Level) spell)
        {
            int cap = Mathf.Max(1, (int)_infusionTarget.Level);
            string name = InfusionSpellName(spell.Spell);
            return spell.Level >= cap
                ? $"{name}   LV {spell.Level}   " + T("(max)", "(máx.)")
                : $"{name}   LV {spell.Level} → {spell.Level + 1}   {MagicInfusionCost}G";
        }

        private void InfuseSelectedSpell(int index)
        {
            var spells = InfusionSpells;
            if (_infusionTarget == null || index < 0 || index >= spells.Count) return;
            var (spell, level) = spells[index];
            string name = InfusionSpellName(spell);
            int cap = Mathf.Max(1, (int)_infusionTarget.Level);
            if (level >= cap)
            {
                _shopPurchaseMessage = T($"{name} can't go above {_infusionTargetName}'s level (LV {cap}).",
                    $"{name} não pode passar do nível de {_infusionTargetName} (NÍV {cap}).");
                return;
            }
            if (_gold < MagicInfusionCost)
            {
                _shopPurchaseMessage = T("You don't have enough gold.", "Você não tem ouro suficiente.");
                return;
            }

            // An implicit native spell is written to slot 0 first so it can hold its own level.
            bool anyStored = false;
            for (int slot = 0; slot < MaxFamiliarSpells; slot++) anyStored |= _infusionTarget.U8(SpellSlotOffset(slot)) != 0;
            if (!anyStored) WriteSpellSlot(_infusionTarget, 0, spell, level);
            bool written = false;
            for (int slot = 0; slot < MaxFamiliarSpells && !written; slot++)
                if (_infusionTarget.U8(SpellSlotOffset(slot)) == spell)
                {
                    WriteSpellSlot(_infusionTarget, slot, spell, level + 1);
                    written = true;
                }
            if (!written) return;

            _gold -= MagicInfusionCost;
            PersistTownProgress();
            PlayRetailSound(RetailSfx.Restore);
            _shopPurchaseMessage = T($"{_infusionTargetName}'s {name} rose to LV {level + 1}. (Paid {MagicInfusionCost}G.)",
                $"{name} de {_infusionTargetName} subiu para o NÍV {level + 1}. (Pagou {MagicInfusionCost}G.)");
            _message = _shopPurchaseMessage;
            _monsterShopSelection = index;
        }

        private string MagicInfusionDialogue() => _monsterShopPage == MonsterShopPage.InfusionFamiliar
            ? _shopPurchaseMessage ?? (_infusionFamiliars.Count == 0
                ? T("You don't have any familiars.", "Você não tem nenhum familiar.")
                : T($"Magic Infusion: I'll strengthen one spell by a level for {MagicInfusionCost}G.\nA spell can't exceed the familiar's level. Which familiar?",
                    $"Infusão Mágica: fortaleço uma magia em um nível por {MagicInfusionCost}G.\nA magia não pode passar do nível do familiar. Qual familiar?"))
            : _shopPurchaseMessage ?? (InfusionSpells.Count == 0
                ? T($"{_infusionTargetName} doesn't know any spells.", $"{_infusionTargetName} não conhece nenhuma magia.")
                : T($"{_infusionTargetName} (LV {(int)_infusionTarget.Level}). Which spell?",
                    $"{_infusionTargetName} (NÍV {(int)_infusionTarget.Level}). Qual magia?"));

        private void DrawMagicInfusionOptions()
        {
            bool familiars = _monsterShopPage == MonsterShopPage.InfusionFamiliar;
            int count = ShopOptionCount;
            int first = _monsterShopSelection / ShopPageSize * ShopPageSize;
            var spells = familiars ? null : InfusionSpells;
            for (int i = first; i < Mathf.Min(first + ShopPageSize, count); i++)
            {
                string label = familiars
                    ? $"{_infusionFamiliars[i].Name}   LV {(int)_infusionFamiliars[i].Actor.Level}"
                    : InfusionSpellRow(spells[i]);
                if (DrawShopOption(i, i - first, label)) { SelectMonsterShopOption(i); break; }
            }
            if (familiars && count > ShopPageSize)
            {
                if (GUI.Button(new Rect(104, 350, 110, 26), T("Previous", "Anterior"))) _monsterShopSelection = Mathf.Max(0, first - ShopPageSize);
                if (GUI.Button(new Rect(218, 350, 110, 26), T("Next", "Próxima"))) _monsterShopSelection = Mathf.Min(
                    Mathf.Max(0, count - 1), first + ShopPageSize);
            }
            if (GUI.Button(new Rect(416, 350, 119, 26), WithCancelHint("Back", "Voltar"))) MagicInfusionBack();
        }

        private void MagicInfusionBack()
        {
            _shopPurchaseMessage = null;
            if (_monsterShopPage == MonsterShopPage.InfusionSpell) SetMonsterShopPage(MonsterShopPage.InfusionFamiliar);
            else SetMonsterShopPage(MonsterShopPage.Services);
        }
    }
}
