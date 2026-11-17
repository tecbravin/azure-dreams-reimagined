using System.Collections.Generic;
using AzureDreams.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using ItemKind = AzureDreams.Core.ItemSystem.Kind;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const byte RustySwordSubtype = 16;
        private const byte FireStaffSubtype = 10;
        private const int BarryRowsPerPage = 5;
        private const byte ShopMedicinalHerbSubtype = 15;
        private const int ShopMedicinalHerbPrice = 20;
        private static readonly (byte Kind, byte Subtype, int Price) ShopMedicinalHerb =
            (ItemKind.Herb, ShopMedicinalHerbSubtype, ShopMedicinalHerbPrice);
        private enum BarryShopPage { Closed, Services, Buy, Sell, Farewell }

        private static readonly (byte Kind, byte Subtype, int Price)[] BarryStock =
        {
            (ItemKind.Sword, RustySwordSubtype, 100),
            (ItemKind.Sword, 3, 200),
            (ItemKind.Wand, FireStaffSubtype, 300),
            ShopMedicinalHerb
        };

        // Base resale values indexed by retail subtype. Zero denotes a weapon Barry will not buy.
        private static readonly int[] BarrySwordValues = { 0, 1000, 50, 100, 150, 400, 400, 400, 800, 10000, 10000, 0, 0, 0, 0, 0, 50 };
        private static readonly int[] BarryWandValues = { 0, 10, 5000, 1000, 100, 1000, 150, 150, 150, 500, 150 };
        private static readonly int[] BarryShieldValues = { 0, 50, 30, 1000, 100, 150, 300, 1500, 600, 600, 600, 600 };

        private readonly List<(int Index, DungeonItemEntry Item, int Price)> _barryOffers = new();
        private BarryShopPage _barryPage;
        private bool _hospitalHerbShop;
        private int _barrySelection;
        private bool _barryConfirm;
        private string _barryFeedback;

        private void SetBarryShopPage(BarryShopPage page)
        {
            _barryPage = page;
            if (page == BarryShopPage.Closed) _hospitalHerbShop = false;
            _barrySelection = 0;
            _barryConfirm = false;
            _barryFeedback = null;
            _barryOffers.Clear();
            if (page != BarryShopPage.Sell) return;

            var items = _townInventory.DungeonItems;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                int price = BarryResalePrice(item);
                if (price > 0 && !Equipped(item)) _barryOffers.Add((i, item, price));
            }
        }

        private static int BarryResalePrice(DungeonItemEntry item)
        {
            if (IsSword(item, DarkSword) && Inventory.IsCursedEquipment(item)) return 50000;
            int[] values = item.Kind switch
            {
                ItemKind.Sword => BarrySwordValues,
                ItemKind.Wand => BarryWandValues,
                ItemKind.Shield => BarryShieldValues,
                _ => null
            };
            if (values == null || item.Subtype >= values.Length) return 0;
            int basePrice = values[item.Subtype];
            if (basePrice == 0) return 0;
            return basePrice * Mathf.Max(0, 10 + ItemSystem.GetEquipmentModifier(item)) / 10 +
                (Inventory.IsCursedEquipment(item) ? 1000 : 0);
        }

        private int BarryOptionCount => _barryPage switch
        {
            BarryShopPage.Services => 3,
            BarryShopPage.Farewell => 1,
            BarryShopPage.Buy => _hospitalHerbShop ? 1 : BarryStock.Length,
            BarryShopPage.Sell => _barryOffers.Count,
            _ => 0
        };

        private void BackFromBarryShop()
        {
            PlayRetailSound(RetailSfx.Cancel);
            if (_barryConfirm)
            {
                _barryConfirm = false;
                _barryFeedback = null;
            }
            else SetBarryShopPage(_hospitalHerbShop || _barryPage is BarryShopPage.Services or BarryShopPage.Farewell
                ? BarryShopPage.Closed : BarryShopPage.Services);
        }

        private bool HandleBarryShopInput()
        {
            if (_barryPage == BarryShopPage.Closed) return false;
            if (_cancel != null && _cancel.WasPressedThisFrame()) BackFromBarryShop();
            else if (_interact != null && _interact.WasPressedThisFrame())
            {
                if (_barryConfirm) TradeWithBarry();
                else if (BarryOptionCount > 0) ChooseBarryOption(_barrySelection);
            }
            else if (!_barryConfirm && BarryOptionCount > 0 && Keyboard.current is { } keyboard)
            {
                int step = MenuDirectionPressed(1) ? -1 : MenuDirectionPressed(2) ? 1 : 0;
                int previous = _barrySelection;
                _barrySelection = Mathf.Clamp(_barrySelection + step, 0, BarryOptionCount - 1);
                if (_barrySelection != previous) PlayRetailSound(RetailSfx.Cursor);
            }
            return true;
        }

        private void ChooseBarryOption(int index)
        {
            PlayRetailSound(RetailSfx.Confirm);
            if (_barryPage == BarryShopPage.Services)
                SetBarryShopPage(index == 0 ? BarryShopPage.Buy : index == 1 ? BarryShopPage.Sell : BarryShopPage.Farewell);
            else if (_barryPage == BarryShopPage.Farewell)
                SetBarryShopPage(BarryShopPage.Closed);
            else
            {
                _barrySelection = index;
                _barryConfirm = true;
                _barryFeedback = null;
            }
        }

        private (byte Kind, byte Subtype, int Price) StockOffer(int index) =>
            _hospitalHerbShop ? ShopMedicinalHerb : BarryStock[index];

        private DungeonItemEntry BarryStockItem(int index)
        {
            var stock = StockOffer(index);
            if (stock.Kind == ItemKind.Herb)
                return new DungeonItemEntry(stock.Subtype, stock.Kind, 20, 0);
            return ItemSystem.CreateFloorItem(stock.Subtype, stock.Kind, 0);
        }

        private void TradeWithBarry()
        {
            if (!_barryConfirm || _barrySelection >= BarryOptionCount) return;
            PlayRetailSound(RetailSfx.Confirm);
            bool buying = _barryPage == BarryShopPage.Buy;
            if (buying)
            {
                int price = StockOffer(_barrySelection).Price;
                if (_gold < price)
                {
                    _barryFeedback = _hospitalHerbShop
                        ? T("You don't have enough gold.", "Você não tem ouro suficiente.")
                        : T("You don’t have enough money.", "Você não tem dinheiro suficiente.");
                    return;
                }
                if (!_townInventory.HasSpace()) { _barryFeedback = T("Your inventory is full.", "Seu inventário está cheio."); return; }
                _townInventory.AddDungeonItem(BarryStockItem(_barrySelection));
                _gold -= price;
                if (_hospitalHerbShop)
                    TownSaveVariables.SetBool("town.hush_bought_herb", true);
            }
            else
            {
                var offer = _barryOffers[_barrySelection];
                var items = _townInventory.DungeonItems;
                if (offer.Index >= items.Count || Equipped(items[offer.Index]) ||
                    items[offer.Index].Kind != offer.Item.Kind || items[offer.Index].Subtype != offer.Item.Subtype ||
                    BarryResalePrice(items[offer.Index]) != offer.Price || _gold > int.MaxValue - offer.Price ||
                    !_townInventory.ConsumeDungeonItem(offer.Index, offer.Item.Kind)) return;
                _gold += offer.Price;
            }

            PersistTownProgress();
            int selection = _barrySelection;
            SetBarryShopPage(_barryPage);
            _barrySelection = Mathf.Clamp(selection, 0, Mathf.Max(0, BarryOptionCount - 1));
            _barryFeedback = _hospitalHerbShop ? T("Thanks.", "Obrigado.") : T("Thanks very much.", "Muito obrigado.");
            if (buying && _hospitalHerbShop) CherrlMedicineHandoff();
        }

        private void DrawBarryShop()
        {
            if (_barryPage == BarryShopPage.Closed) return;
            DrawHudPanel(new Rect(85, 86, 470, 301));
            GUI.Label(new Rect(103, 101, 310, 24), _hospitalHerbShop ? T("Hospital - Hush", "Hospital - Hush") : T("Blacksmith - Barry", "Ferraria - Barry"));
            GUI.Label(new Rect(429, 101, 110, 24), _gold + "G");

            if (_barryPage == BarryShopPage.Farewell)
            {
                DrawHudText(new Rect(103, 139, 432, 50), T("Then good-bye.", "Então, até logo."), _messageStyle, dialogue: true);
                if (GUI.Button(new Rect(103, 212, 432, 26), WithInteractHint("Continue", "Continuar"))) ChooseBarryOption(0);
                return;
            }

            bool buying = _barryPage == BarryShopPage.Buy;
            if (_barryConfirm)
            {
                var item = buying ? BarryStockItem(_barrySelection) : _barryOffers[_barrySelection].Item;
                int price = buying ? StockOffer(_barrySelection).Price : _barryOffers[_barrySelection].Price;
                string action = buying ? T("Buy", "Comprar") : T("Sell", "Vender");
                var confirmation = new Rect(103, 139, 432, 52);
                if (_barryFeedback == null) confirmation = DrawEquipmentCurseIcon(confirmation, item);
                string offer = _hospitalHerbShop ? $"{action} {InventoryItemName(item)} ({price}G)?" :
                    buying ? T($"That’ll be {price}G.", $"São {price}G.") : T($"How about {price}G?", $"Que tal {price}G?");
                GUI.Label(confirmation, _barryFeedback ?? offer);
                string accept = _hospitalHerbShop ? WithInteractHint("Confirm", "Confirmar") :
                    buying ? T("I’ll buy.", "Vou comprar.") : T("I’ll sell.", "Vou vender.");
                string decline = _hospitalHerbShop ? T("Back", "Voltar") : T("My mistake.", "Foi engano meu.");
                if (GUI.Button(new Rect(103, 212, 215, 26), accept)) TradeWithBarry();
                if (GUI.Button(new Rect(320, 212, 215, 26), decline)) BackFromBarryShop();
                return;
            }

            string[] services = _barryPage == BarryShopPage.Services
                ? new[] { T("I’ve come to buy.", "Vim comprar."), T("I’ve come to sell.", "Vim vender."),
                    T("Just looking around.", "Só estou olhando.") }
                : null;
            // TOWN.BIN 0x61B2BC–0x61B7D4.
            string prompt = services != null ? string.Empty : buying
                ? _hospitalHerbShop ? T("Choose an item.", "Escolha um item.") :
                    T("Due to the ancient ruins, we’ve seen an increase in hunters, so as a result we’re always out of items.\nSo there’s not much to sell.\nWhat are you looking for.",
                        "Por causa das ruínas antigas, tem vindo mais caçadores e, por isso, as mercadorias estão sempre acabando.\nEntão não há muito para vender.\nO que você procura.")
                : _barryOffers.Count > 0 ? T("Thanks to the tower, I’ve got many buyers.  I’m very thankful that you’ll sell.\nWhat are you willing to sell?",
                    "Graças à torre, tenho muitos compradores. Fico muito grato por você querer vender.\nO que você está disposto a vender?")
                : T("You have no unequipped gear I can buy.", "Você não tem equipamento desequipado que eu possa comprar.");
            DrawHudText(new Rect(103, 139, 432, 70), _barryFeedback ?? prompt, _messageStyle, dialogue: true);
            int first = _barrySelection / BarryRowsPerPage * BarryRowsPerPage;
            for (int i = first; i < Mathf.Min(first + BarryRowsPerPage, BarryOptionCount); i++)
            {
                string label = services != null ? services[i] : buying
                    ? $"{InventoryItemName(BarryStockItem(i))}   {StockOffer(i).Price}G"
                    : $"{InventoryItemName(_barryOffers[i].Item)}   {_barryOffers[i].Price}G";
                var row = new Rect(103, 212 + (i - first) * 26, 432, 24);
                if (services == null && !buying) row = DrawEquipmentCurseIcon(row, _barryOffers[i].Item);
                if (GUI.Button(row, i == _barrySelection ? "> " + label : label))
                { ChooseBarryOption(i); break; }
            }
            if (_barryConfirm || _barryPage == BarryShopPage.Closed) return;
            if (first > 0 && GUI.Button(new Rect(103, 350, 110, 26), T("Previous", "Anterior"))) _barrySelection = first - BarryRowsPerPage;
            if (first + BarryRowsPerPage < BarryOptionCount && GUI.Button(new Rect(218, 350, 110, 26), T("Next", "Próxima"))) _barrySelection = first + BarryRowsPerPage;
            if (GUI.Button(new Rect(416, 350, 119, 26), T("Back [Esc]", "Voltar [Esc]"))) BackFromBarryShop();
        }
    }
}
