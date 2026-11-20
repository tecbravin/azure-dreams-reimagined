using AzureDreams.Core;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private static bool BarryCanTemper(DungeonItemEntry item) => item!=null &&
            (CanTemperEquipment(item,false)||CanTemperEquipment(item,true)) && item.Subtype>0 &&
            !(item.Kind==ItemSystem.Kind.Sword && item.Subtype is 14 or 15) && ItemSystem.GetEquipmentModifier(item)<MaximumTemper;
        private static int BarryTemperPrice(DungeonItemEntry item) => Mathf.Max(1,ItemSystem.GetEquipmentModifier(item)+1)*100;

        private void ReadBarryTemperOffers()
        {
            _barryOffers.Clear();
            var items=_townInventory.DungeonItems;
            for(int i=0;i<items.Count;i++)
                if(BarryCanTemper(items[i]))_barryOffers.Add((i,items[i],BarryTemperPrice(items[i])));
        }

        private bool TryBarryTemper(int index,DungeonItemEntry expected)
        {
            if(_area!=Area.Town || ! _interior || _interior.interiorId!=BlacksmithInterior || expected==null)return false;
            var items=_townInventory.DungeonItems;
            if(index<0||index>=items.Count)return false;
            var item=items[index];
            if(!BarryCanTemper(item)||item.Kind!=expected.Kind||item.Subtype!=expected.Subtype||item.Param!=expected.Param||item.Flags!=expected.Flags)return false;
            int price=BarryTemperPrice(item);
            if(_gold<price)return false;
            item.Param++;
            if(!_townInventory.ReplaceDungeonItem(index,item.Kind,item))return false;
            _gold-=price;
            PersistTownProgress();
            return true;
        }

        private void TradeBarryTemper()
        {
            var offer=_barryOffers[_barrySelection];
            if(!TryBarryTemper(offer.Index,offer.Item))
            {
                _barryFeedback=_gold<offer.Price ? T("You don't have enough money.","Você não tem dinheiro suficiente.") :
                    T("That equipment can no longer be tempered.","Esse equipamento não pode mais ser temperado.");
                return;
            }
            PlayRetailSound(RetailSfx.Equip);
            int selection=_barrySelection;
            SetBarryShopPage(BarryShopPage.Temper);
            _barrySelection=Mathf.Clamp(selection,0,Mathf.Max(0,_barryOffers.Count-1));
            _barryFeedback=T("Improved by +1.","Melhorado em +1.");
        }

        private void DrawBarryTemper()
        {
            GUI.Label(new Rect(103,132,432,52),_barryFeedback ?? (_barryOffers.Count==0 ?
                T("No swords, shields or Trained Wands below +99.","Nenhuma espada, escudo ou Varinha Treinada abaixo de +99.") :
                T("Temper: +1, up to +99. Cost: resulting level × 100 G.","Temperar: +1, até +99. Preço: nível resultante × 100 G.")));
            if(_barryConfirm)
            {
                var offer=_barryOffers[_barrySelection];
                GUI.Label(new Rect(103,185,432,50),T($"Temper {InventoryItemName(offer.Item)} to {offer.Item.Param+1:+0;-0;+0} for {offer.Price} G?",
                    $"Temperar {InventoryItemName(offer.Item)} para {offer.Item.Param+1:+0;-0;+0} por {offer.Price} G?"));
                if(GUI.Button(new Rect(103,244,215,26),WithInteractHint("Temper","Temperar")))TradeWithBarry();
                if(GUI.Button(new Rect(320,244,215,26),T("Back","Voltar")))BackFromBarryShop();
                return;
            }
            int first=(_barrySelection/BarryRowsPerPage)*BarryRowsPerPage;
            for(int i=first;i<Mathf.Min(first+BarryRowsPerPage,_barryOffers.Count);i++)
            {
                var offer=_barryOffers[i];
                string label=$"{InventoryItemName(offer.Item)} → +{offer.Item.Param+1}   {offer.Price} G";
                if(GUI.Button(new Rect(103,195+(i-first)*26,432,24),(_barrySelection==i ? "> " : "")+label)){ChooseBarryOption(i);break;}
            }
            if(first>0 && GUI.Button(new Rect(103,350,110,26),T("Previous","Anterior")))_barrySelection=first-1;
            if(first+BarryRowsPerPage<_barryOffers.Count && GUI.Button(new Rect(218,350,110,26),T("Next","Próxima")))_barrySelection=first+BarryRowsPerPage;
            if(GUI.Button(new Rect(416,350,119,26),T("Back","Voltar")))BackFromBarryShop();
        }
    }
}
