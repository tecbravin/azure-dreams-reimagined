using System;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private static string GiftCountKey(int group) => "retail.gift_count." + group;

        private string GiftPortrait(TownHeroineGifts.Hero hero)
        {
            bool pool = StoryRoom == PoolOpenRoom;
            return hero.id switch
            {
                "nico" => pool ? PoolNicoPortrait : NicoPortrait(0),
                "selfi" => SelfiPortrait(pool ? 5 : 0),
                "fur" => pool ? PoolFurPortrait : FurPortrait(0),
                "mia" => pool ? PoolMiaPortrait : MiaPortrait(0),
                "patty" => PattyPortrait(pool ? 4 : 0),
                "vivian" => VivianPortrait(pool ? 5 : 0),
                "cherrl" => CherrlPortrait(pool ? 6 :
                    TownRetailScriptFlags.CreatePersistent().IsFlagSet(CherrlNurseFlag) ? 7 : 0),
                _ => null
            };
        }

        private int GiftSellPrice(AzureDreams.Core.DungeonItemEntry item)
        {
            if (Inventory.IsGear(item)) return BarryResalePrice(item);
            _nativeResaleValues ??= Resources.Load<TextAsset>("Town/Retail/item_resale")?.bytes;
            int offset = (item.Kind * 256 + item.Subtype) * 2;
            return _nativeResaleValues != null && offset + 1 < _nativeResaleValues.Length
                ? _nativeResaleValues[offset] | _nativeResaleValues[offset + 1] << 8 : 0;
        }

        private bool TryHeldHeroineGift(NPCController npc)
        {
            if (_area != Area.Town || _carriedItem == null || !npc) return false;
            var hero = TownHeroineGifts.Find(npc.npcId);
            if (hero == null) return false;
            var item = _carriedItem;
            var flags = TownRetailScriptFlags.CreatePersistent();
            string portrait = GiftPortrait(hero);
            var response = TownHeroineGifts.Select(hero, item.Kind, item.Subtype, GiftSellPrice(item),
                TownSaveVariables.GetInt(GiftCountKey(hero.group)), flags.IsFlagSet);
            // Fur's first dumpling scene is a full story event, before the generic repeat response.
            bool firstDumplings = hero.id == "fur" && IsOctopusDumplings(item) && !flags.IsFlagSet(4811);
            if (response == null && !firstDumplings) return false; // Preserve quest-specific handovers.
            void Give()
            {
                if (!npc || !ReferenceEquals(_carriedItem, item)) return;
                if (firstDumplings)
                {
                    TownDialogueSequence(npc, portrait, () => CompleteHeldGift(item, hero, true, false, true),
                        ("Really?  Thanks. Appreciate it.", "Sério? Obrigada. Agradeço."),
                        ("Yay, octopus dumplings.", "Oba, bolinhos de polvo."),
                        ("Munch, munch.", "Nhac, nhac."),
                        ("For me to receive something from {name}! Life never ceases to amaze me.",
                            "Eu recebendo algo de {name}! A vida não para de me surpreender."),
                        ("Food stand by the windmill. Haven't been to one of those in a while.",
                            "A barraca perto do moinho. Faz tempo que não vou a uma dessas."),
                        ("Back out west, I had this every day.", "Lá no oeste, eu comia isto todos os dias."),
                        ("Munch, munch.  This is good.", "Nhac, nhac. Está bom."),
                        ("Munch, munch.  Thanks.", "Nhac, nhac. Obrigada."));
                    return;
                }
                void Page(int index)
                {
                    if (!npc || !ReferenceEquals(_carriedItem, item)) return;
                    if (index >= response.pages.Length)
                    {
                        CompleteHeldGift(item, hero, response.accepted, response.increment, false);
                        return;
                    }
                    string line = response.pages[index];
                    StoryTalk(npc, line, response.portuguese[index], portrait, () => Page(index + 1));
                }
                Page(0);
            }
            // Original shared offer: SLUS_006.14 0x8006C894.
            return StoryAsk(npc, firstDumplings
                    ? "Welcome. What is it? I smell something delicious. You've got octopus dumplings, don't you?" : "",
                firstDumplings ? "Boas-vindas. O que é? Estou sentindo um cheiro delicioso. Você está com bolinhos de polvo, não está?" : "",
                portrait,
                (T("[This is for you.]", "[Isto é para você.]"), Give),
                (T("[Well... Nothing.]", "[Bem... Nada.]"), null));
        }

        private void CompleteHeldGift(AzureDreams.Core.DungeonItemEntry item, TownHeroineGifts.Hero hero,
            bool accepted, bool increment, bool dumplings)
        {
            if (!accepted || !ReferenceEquals(_carriedItem, item)) return;
            if (_carriedView) RemoveGenerated(_carriedView);
            _carriedView = null;
            _carriedItem = null;
            if (increment) TownSaveVariables.SetInt(GiftCountKey(hero.group),
                Mathf.Min(3, TownSaveVariables.GetInt(GiftCountKey(hero.group)) + 1));
            if (dumplings) TownRetailScriptFlags.CreatePersistent().SetBit(4811);
            PersistTownProgress();
        }
    }
}
