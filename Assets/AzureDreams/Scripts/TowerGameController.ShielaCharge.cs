using System;
using System.Collections.Generic;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const int ShielaBallChargePrice = 200;

        private void ShielaChargeMenu(NPCController npc)
        {
            var choices = new List<(string Label, Action Apply)>();
            var items = _townInventory.DungeonItems;
            for (int i=0;i<items.Count;i++)
            {
                var ball=items[i];
                if (!CanRechargeBall(ball)) continue;
                int index=i;
                choices.Add(($"{InventoryItemName(ball)} — {BallChargeText(ball)}", () => ShielaConfirmCharge(npc,index,ball)));
            }
            if (choices.Count==0)
            {
                StoryTalk(npc,"You have no balls that can receive another charge.","Você não tem esferas que possam receber mais uma carga.",()=>ShielaTopics(npc));
                return;
            }
            choices.Add((T("Back","Voltar"),()=>ShielaTopics(npc)));
            StoryAsk(npc,"Choose a ball. Each charge costs 200 G.","Escolha uma esfera. Cada carga custa 200 G.",choices.ToArray());
        }

        private void ShielaConfirmCharge(NPCController npc,int index,DungeonItemEntry ball)
        {
            StoryAsk(npc,$"Add one charge to {InventoryItemName(ball)} for 200 G?",$"Adicionar uma carga a {InventoryItemName(ball)} por 200 G?",
                (T("Charge — 200 G","Carregar — 200 G"),()=>
                {
                    if (!TryShielaChargeBall(index,ball))
                    {
                        StoryTalk(npc,_gold<ShielaBallChargePrice ? "You don't have enough gold." : "That ball can no longer be charged.",
                            _gold<ShielaBallChargePrice ? "Você não tem ouro suficiente." : "Essa esfera não pode mais ser carregada.",()=>ShielaChargeMenu(npc));
                        return;
                    }
                    PlayRetailSound(RetailSfx.Restore);
                    StoryTalk(npc,"One charge added.","Uma carga adicionada.",()=>ShielaChargeMenu(npc));
                }),
                (T("Back","Voltar"),()=>ShielaChargeMenu(npc)));
        }

        private bool TryShielaChargeBall(int index,DungeonItemEntry expected)
        {
            if (_area!=Area.Town || !_interior || _interior.interiorId!=FortuneHouseInterior || _gold<ShielaBallChargePrice || expected==null) return false;
            var items=_townInventory.DungeonItems;
            if (index<0 || index>=items.Count) return false;
            var ball=items[index];
            if (!CanRechargeBall(ball) || ball.Kind!=expected.Kind || ball.Subtype!=expected.Subtype || ball.Param!=expected.Param || ball.Flags!=expected.Flags) return false;
            ball.Param++;
            if (!_townInventory.ReplaceDungeonItem(index,ItemSystem.Kind.Ball,ball)) return false;
            _gold-=ShielaBallChargePrice;
            PersistTownProgress();
            return true;
        }
    }
}
