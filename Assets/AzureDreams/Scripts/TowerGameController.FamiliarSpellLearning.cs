using System.Collections.Generic;
using AzureDreams.Core;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Familiars learn the spell of a crystal ball handed to them when it holds 10 or more
    /// charges (the charges become the spell level, charges / 10, as BallSpellLevel). Spells live
    /// in the three native slots of the actor record (offsets 8/11/14: id, stored level, level),
    /// the same slots fusion, seeds and level-ups already maintain, so a familiar keeps at most
    /// three. The Acid Rain ball (subtype 17, spell 49) cannot be given to a familiar.
    /// </summary>
    public sealed partial class TowerGameController
    {
        private const int MaxFamiliarSpells = 3;
        private const int BallLearnCharges = 10;
        private const int AcidRainBallSubtype = 17;

        private static int SpellSlotOffset(int slot) => 8 + slot * 3;

        private static bool IsAcidRainBall(DungeonItemEntry item) =>
            item != null && item.Kind == ItemKind.Ball && item.Subtype == AcidRainBallSubtype;

        private static bool TeachesFamiliar(DungeonItemEntry item) =>
            item != null && item.Kind == ItemKind.Ball && !IsAcidRainBall(item) &&
            !SecondTowerItems.IsUnknown(item) && item.Param >= BallLearnCharges &&
            item.Subtype > 0 && item.Subtype < BallSpellIds.Length;

        /// <summary>Spells a familiar can cast, in slot order (native spell when no slot is filled).</summary>
        private List<(int Spell, int Level)> FamiliarSpellList(ActorRecord actor)
        {
            var spells = new List<(int Spell, int Level)>(MaxFamiliarSpells);
            if (actor == null) return spells;
            for (int slot = 0; slot < MaxFamiliarSpells; slot++)
            {
                int offset = SpellSlotOffset(slot);
                int id = actor.U8(offset);
                if (id > 0 && SpellFamily(id) >= 0) spells.Add((id, Mathf.Max(1, actor.U8(offset + 1))));
            }
            if (spells.Count == 0)
            {
                var profile = UnitProfile(actor.Species);
                int native = profile?.nativeSpellId ?? 0;
                if (native == 0) native = profile?.hiddenSpellId ?? 0;
                if (native > 0) spells.Add((native, Mathf.Max(1, (int)actor.Level)));
            }
            return spells;
        }

        private int FamiliarSpellLevel(ActorRecord actor, int spell)
        {
            foreach (var known in FamiliarSpellList(actor))
                if (known.Spell == spell) return known.Level;
            return Mathf.Max(1, (int)actor.Level);
        }

        /// <summary>Spell id adapted to the familiar's element, as fusion and seeds already do.</summary>
        private int SpellForElement(int spell, int element)
        {
            int variant = element == Element.Fire ? 1 : element == Element.Water ? 2 : element == Element.Wind ? 3 : 0;
            return spell is > 0 and <= 45 && variant != 0 ? SpellFamily(spell) * 3 + variant : spell;
        }

        private enum SpellLearnResult { Learned, Improved, AlreadyKnown, NoRoom, Invalid }

        private SpellLearnResult TeachFamiliarSpell(ActorRecord actor, DungeonItemEntry ball, out int learnedSpell)
        {
            learnedSpell = 0;
            if (actor == null || !TeachesFamiliar(ball)) return SpellLearnResult.Invalid;
            int spell = SpellForElement(BallSpellIds[ball.Subtype], UnitElement(actor));
            int level = Mathf.Clamp(BallSpellLevel(ball.Param), 1, 99);
            learnedSpell = spell;

            // A familiar whose only spell is its implicit native one gets it written to slot 0
            // first, so learning a ball never replaces the species' own spell.
            bool anyStored = false;
            for (int slot = 0; slot < MaxFamiliarSpells; slot++) anyStored |= actor.U8(SpellSlotOffset(slot)) != 0;
            if (!anyStored)
            {
                var native = FamiliarSpellList(actor);
                if (native.Count > 0) WriteSpellSlot(actor, 0, native[0].Spell, native[0].Level);
            }

            int free = -1;
            for (int slot = 0; slot < MaxFamiliarSpells; slot++)
            {
                int offset = SpellSlotOffset(slot);
                int id = actor.U8(offset);
                if (id == 0) { if (free < 0) free = slot; continue; }
                if (SpellFamily(id) != SpellFamily(spell)) continue;
                if (actor.U8(offset + 1) >= level) return SpellLearnResult.AlreadyKnown;
                WriteSpellSlot(actor, slot, id, level);
                return SpellLearnResult.Improved;
            }
            if (free < 0) return SpellLearnResult.NoRoom;
            WriteSpellSlot(actor, free, spell, level);
            return SpellLearnResult.Learned;
        }

        private static void WriteSpellSlot(ActorRecord actor, int slot, int spell, int level)
        {
            int offset = SpellSlotOffset(slot);
            actor.SetRaw(offset, (byte)spell);
            actor.SetRaw(offset + 1, (byte)Mathf.Clamp(level, 1, 99));
            actor.SetRaw(offset + 2, (byte)Mathf.Clamp(level, 1, 99));
        }

        /// <summary>Handles "Give to familiar" for a ball with 10+ charges. Returns true when handled.</summary>
        private bool TryGiveBallToLearn(Inventory inventory, int index, DungeonItemEntry item, System.Func<bool> remove, ActorRecord recipient=null, System.Action refresh=null)
        {
            if (!TeachesFamiliar(item)) return false;
            var actor = recipient ?? _familiarActor;
            bool present = GiveRecipientAvailable(actor);
            if (actor == null || !present || IsFamiliarLost(actor)) return false;
            string spellName = FamiliarSpellNames[SpellFamily(SpellForElement(BallSpellIds[item.Subtype], UnitElement(actor)))];

            // Check without changing anything first, so a refused ball is not consumed.
            var probe = new List<(int Spell, int Level)>();
            for (int slot = 0; slot < MaxFamiliarSpells; slot++)
            {
                int id = actor.U8(SpellSlotOffset(slot));
                if (id != 0) probe.Add((id, actor.U8(SpellSlotOffset(slot) + 1)));
            }
            if (probe.Count == 0) probe = FamiliarSpellList(actor);
            int family = SpellFamily(BallSpellIds[item.Subtype]);
            int newLevel = Mathf.Clamp(BallSpellLevel(item.Param), 1, 99);
            bool known = probe.Exists(spell => SpellFamily(spell.Spell) == family);
            if (known && probe.Exists(spell => SpellFamily(spell.Spell) == family && spell.Level >= newLevel))
            {
                _message = T($"{FamiliarDisplayName} already knows {spellName}.", $"{FamiliarDisplayName} já conhece {spellName}.");
                return true;
            }
            if (!known && probe.Count >= MaxFamiliarSpells)
            {
                _message = T($"{FamiliarDisplayName} cannot learn more than {MaxFamiliarSpells} spells.",
                    $"{FamiliarDisplayName} não pode aprender mais de {MaxFamiliarSpells} magias.");
                return true;
            }

            ScheduleInventoryAction(() =>
            {
                var current = index < inventory.DungeonItems.Count ? inventory.DungeonItems[index] : null;
                if (!GiveRecipientAvailable(actor) || current == null || current.Kind != ItemKind.Ball ||
                    current.Subtype != item.Subtype || current.Param != item.Param || !remove()) return;
                var result = TeachFamiliarSpell(actor, item, out _);
                PlayRetailSound(RetailSfx.Restore);
                if (_area == Area.Tower && _familiar) SpawnFx(_fxMagic, CellPosition(_familiarCell));
                _message = result == SpellLearnResult.Improved
                    ? T($"{FamiliarDisplayName}'s {spellName} rose to LV {newLevel}.",
                        $"{spellName} de {FamiliarDisplayName} subiu para o NÍV {newLevel}.")
                    : T($"{FamiliarDisplayName} learned {spellName}!", $"{FamiliarDisplayName} aprendeu {spellName}!");
                if(refresh!=null) refresh(); else RefreshInventoryCommands();
            });
            return true;
        }
    }
}
