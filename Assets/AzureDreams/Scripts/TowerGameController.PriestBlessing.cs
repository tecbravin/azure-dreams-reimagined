using System.Collections.Generic;
using AzureDreams.Core;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Reimagined temple services (not in the retail game).
    /// - Free de-cursing: Jorda inspects the cursed gear Koh carries, bonks him and lifts every
    ///   curse. On the third visit he gives Koh a blessing that completes the quest: coins picked
    ///   up give 10% more gold (CreditCoinPickup) and the cursed (-1) roll of tower equipment is
    ///   51/1024 (~5%) less likely (TowerItemTrapManager.CreateTowerEquipment). The Dark Sword is
    ///   always cursed, so the blessing does not change it.
    /// - Temple level 2 sells the De-curse Scroll (kind 5, id 5) for 1000G.
    /// </summary>
    public sealed partial class TowerGameController
    {
        internal const string PriestDecurseVisitsKey = "temple.decurse_visits";
        internal const string PriestBlessingKey = "temple.priest_blessing";
        private const int PriestBlessingVisits = 3;
        internal const int BlessedCurseReduction = 51; // of 1024, about 5 percentage points
        private const int DecurseScrollId = 5;
        private const int DecurseScrollPrice = 1000;

        internal static bool PriestBlessed => TownSaveVariables.GetBool(PriestBlessingKey);

        private static int BlessedCoinValue(int amount) =>
            PriestBlessed && amount > 0 ? amount + Mathf.Max(1, (amount + 9) / 10) : amount;

        private bool InTempleRoom => StoryRoom is "Interior_11_temple" or "Interior_12_temple_2";

        private List<DungeonItemEntry> CarriedCursedItems()
        {
            var cursed = new List<DungeonItemEntry>();
            foreach (var item in _townInventory.DungeonItems)
                if (Inventory.IsCursedEquipment(item)) cursed.Add(item);
            return cursed;
        }

        private bool TryPriestDecurse(NPCController npc)
        {
            var cursed = CarriedCursedItems();
            if (cursed.Count == 0) return false;
            int visits = TownSaveVariables.GetInt(PriestDecurseVisitsKey) + 1;
            bool blessing = visits == PriestBlessingVisits && !PriestBlessed;

            void Purify()
            {
                PlayRetailSound(RetailSfx.Punch);
                foreach (var item in cursed)
                    item.Flags = (byte)(item.Flags & ~(Inventory.CursedEquipmentFlag | Inventory.UnrevealedEquipmentFlag));
                TownSaveVariables.SetInt(PriestDecurseVisitsKey, visits);
                if (blessing) TownSaveVariables.SetBool(PriestBlessingKey, true);
                PersistTownProgress();
            }

            string names = string.Join(", ", cursed.ConvertAll(InventoryItemName));
            if (blessing)
                return JordaTalk(npc, "Again? Boy, you need a blessing.", "De novo? Rapaz, você precisa de uma bênção.",
                    () => JordaTalk(npc, "Mumble, mumble, mumble, mumble...", "Murmura, murmura, murmura, murmura...",
                        () => { Purify(); JordaTalk(npc, "*BONK!*", "*TOC!*",
                            () => JordaTalk(npc, "Done. You should feel more fortunate now.",
                                "Pronto. Você deve se sentir mais afortunado agora.",
                                () => JordaTalk(npc, "(Koh was blessed! Coins are worth 10% more and cursed items appear less often.)",
                                    "(Koh foi abençoado! As moedas valem 10% a mais e itens amaldiçoados aparecem menos.)"))); }));

            return JordaTalk(npc, "Hm? Let me have a look at that...", "Hm? Deixe-me dar uma olhada nisso...",
                () => JordaTalk(npc, $"Just as I thought. A curse clings to it: {names}.",
                    $"Como eu pensava. Há uma maldição nisso: {names}.",
                    () => { Purify(); JordaTalk(npc, "*BONK!*", "*TOC!*",
                        () => JordaTalk(npc, "There. The curse is gone. No charge, my boy.",
                            "Pronto. A maldição se foi. Não cobro nada, meu rapaz.")); }));
        }

        private bool JordaTempleShop(NPCController npc, string idleEn, string idlePt) =>
            JordaAsk(npc, "Welcome to the temple. Do you need anything?", "Bem-vindo ao templo. Precisa de algo?",
                (T($"Buy a De-curse Scroll ({DecurseScrollPrice}G)", $"Comprar Pergaminho de purificação ({DecurseScrollPrice}G)"),
                    () => BuyDecurseScroll(npc)),
                (T("Read the sacred book", "Ler o livro sagrado"), OpenJordaManual),
                (T("Just visiting.", "Só estou visitando."), () => JordaTalk(npc, idleEn, idlePt)));

        private void BuyDecurseScroll(NPCController npc)
        {
            if (_gold < DecurseScrollPrice)
            {
                JordaTalk(npc, "You don't have enough gold, my boy.", "Você não tem ouro suficiente, meu rapaz.");
                return;
            }
            if (!_townInventory.HasSpace())
            {
                JordaTalk(npc, "Your hands are full. Make some room first.", "Suas mãos estão cheias. Abra espaço primeiro.");
                return;
            }
            _gold -= DecurseScrollPrice;
            _townInventory.AddDungeonItem(ItemSystem.CreateFloorItem(DecurseScrollId, ItemSystem.Kind.Scroll, 0));
            PersistTownProgress();
            PlayRetailSound(RetailSfx.CoinPickup);
            JordaTalk(npc, "May it keep you safe in the tower.", "Que ele te proteja na torre.",
                () => JordaTalk(npc, $"(Received a De-curse Scroll! -{DecurseScrollPrice}G)",
                    $"(Você recebeu um Pergaminho de purificação! -{DecurseScrollPrice}G)"));
        }
    }
}
