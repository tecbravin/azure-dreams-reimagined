using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string GeneralStoreInterior = "Interior_16_general_store";
        private const string GeneralStorePurchases = "home.general_store_purchases";
        private const int GeneralStoreRows = 5;
        private enum GeneralStorePage { Closed, Services, Buy, Purchased }

        // Prices documented from the PS1 shop. IDs are stable across save slots.
        private static readonly (string Id, string English, string Portuguese, int Price, bool Unique)[] GeneralStock =
        {
            ("bed", "Bed", "Cama", 2500, false),
            ("canopy", "Canopy", "Dossel", 6000, false),
            ("table", "Table", "Mesa", 500, false),
            ("bonsai", "Bonsai", "Bonsai", 1000, false),
            ("plant", "Plant", "Planta", 2500, false),
            ("carpet_1", "Carpet 1", "Tapete 1", 1000, false),
            ("carpet_2", "Carpet 2", "Tapete 2", 2000, false),
            ("carpet_3", "Carpet 3", "Tapete 3", 5000, false),
            ("tiger_fur", "Tiger Fur", "Pele de tigre", 8000, false),
            ("stuffed_deer", "Stuffed Deer", "Cervo empalhado", 1000, false),
            ("painting", "Painting", "Quadro", 1000, false),
            ("scrolls", "Scrolls", "Pergaminhos decorativos", 5000, false),
            ("poster", "Poster", "Pôster", 10000, false),
            ("wallpaper_1", "Wallpaper 1", "Papel de parede 1", 1000, false),
            ("wallpaper_2", "Wallpaper 2", "Papel de parede 2", 5000, false),
            ("wallpaper_3", "Wallpaper 3", "Papel de parede 3", 3000, false),
            ("sculpture", "Sculpture", "Escultura", 12000, false),
            ("something", "Something", "Alguma coisa", 5000, false),
            ("tv", "TV", "Televisão", 2000, false),
            ("fridge", "Fridge", "Geladeira", 2000, false),
            ("washer", "Washing Machine", "Máquina de lavar", 1500, false),
            ("range", "Range", "Fogão", 1500, false),
            ("light", "Light", "Luminária", 1000, false),
            ("telephone", "Telephone", "Telefone", 5000, false),
            ("trophy", "Trophy", "Troféu", 5000, false),
            ("vest", "Good Vest", "Colete", 500, true),
            ("jacket", "Jacket", "Jaqueta", 1000, true),
            ("trousers", "Trousers", "Calça", 800, true),
            ("shoes", "Shoes", "Sapatos", 500, true),
            ("aura_bike", "Aura Bike", "Moto de aura", 20000, true),
            ("booster", "Booster", "Propulsor", 15000, true),
            ("music_box", "Music Box", "Caixa de música", 700, true)
        };

        private GeneralStorePage _generalStorePage;
        private readonly List<int> _generalStoreStockRows = new();
        private readonly List<int> _generalStorePurchasedRows = new();
        private int _generalStoreSelection;
        private bool _generalStoreConfirm;
        private string _generalStoreFeedback;
        private bool InGeneralStore => _interior && _interior.interiorId == GeneralStoreInterior;

        private void SetGeneralStorePage(GeneralStorePage page)
        {
            _generalStorePage = page;
            _generalStoreSelection = 0;
            _generalStoreConfirm = false;
            _generalStoreFeedback = null;
            _generalStoreStockRows.Clear();
            _generalStorePurchasedRows.Clear();
            if (page == GeneralStorePage.Buy)
            {
                int houseRank = CurrentHomeRank;
                for (int i = 0; i < GeneralStock.Length; i++)
                    if (houseRank > 0 || GeneralStock[i].Id is not ("telephone" or "trophy"))
                        _generalStoreStockRows.Add(i);
            }
            else if (page == GeneralStorePage.Purchased)
                for (int i = 0; i < GeneralStock.Length; i++)
                    if (GeneralStoreOwned(GeneralStock[i].Id) > 0) _generalStorePurchasedRows.Add(i);
        }

        private static int GeneralStoreOwned(string item) => TownSaveVariables.GetInt(GeneralStorePurchases + "." + item);

        private int GeneralStoreOptionCount => _generalStorePage switch
        {
            GeneralStorePage.Services => 3,
            GeneralStorePage.Buy => _generalStoreStockRows.Count,
            GeneralStorePage.Purchased => _generalStorePurchasedRows.Count,
            _ => 0
        };

        private bool HandleGeneralStoreInput()
        {
            if (_generalStorePage == GeneralStorePage.Closed) return false;
            if (!InGeneralStore)
            {
                SetGeneralStorePage(GeneralStorePage.Closed);
                return false;
            }
            if (_cancel?.WasPressedThisFrame() == true) BackFromGeneralStore();
            else if (_interact?.WasPressedThisFrame() == true)
            {
                if (_generalStoreConfirm) BuyGeneralStoreItem();
                else if (GeneralStoreOptionCount > 0) ChooseGeneralStoreRow(_generalStoreSelection);
            }
            else if (!_generalStoreConfirm && GeneralStoreOptionCount > 0 && Keyboard.current is { } keyboard)
            {
                int step = keyboard.upArrowKey.wasPressedThisFrame ? -1 : keyboard.downArrowKey.wasPressedThisFrame ? 1 : 0;
                int row = Mathf.Clamp(_generalStoreSelection + step, 0, GeneralStoreOptionCount - 1);
                if (row != _generalStoreSelection) PlayRetailSound(RetailSfx.Cursor);
                _generalStoreSelection = row;
            }
            return true;
        }

        private void BackFromGeneralStore()
        {
            PlayRetailSound(RetailSfx.Cancel);
            if (_generalStoreConfirm)
            {
                _generalStoreConfirm = false;
                _generalStoreFeedback = null;
            }
            else SetGeneralStorePage(_generalStorePage == GeneralStorePage.Services ? GeneralStorePage.Closed : GeneralStorePage.Services);
        }

        private void ChooseGeneralStoreRow(int index)
        {
            if (_generalStorePage == GeneralStorePage.Purchased) return;
            PlayRetailSound(RetailSfx.Confirm);
            if (_generalStorePage == GeneralStorePage.Services)
                SetGeneralStorePage(index == 0 ? GeneralStorePage.Buy :
                    index == 1 ? GeneralStorePage.Purchased : GeneralStorePage.Closed);
            else
            {
                _generalStoreSelection = index;
                _generalStoreConfirm = true;
                _generalStoreFeedback = null;
            }
        }

        private void BuyGeneralStoreItem()
        {
            if (!InGeneralStore || !_generalStoreConfirm || _generalStorePage != GeneralStorePage.Buy) return;
            var item = GeneralStock[_generalStoreStockRows[_generalStoreSelection]];
            int owned = GeneralStoreOwned(item.Id);
            if (item.Unique && owned > 0)
            {
                _generalStoreFeedback = T("You already bought this item.", "Você já comprou este item.");
                return;
            }
            if (item.Id == "booster" && GeneralStoreOwned("aura_bike") == 0)
            {
                _generalStoreFeedback = T("You need the Aura Bike first.", "Você precisa comprar a Moto de aura primeiro.");
                return;
            }
            if (_gold < item.Price)
            {
                _generalStoreFeedback = T("You don't have enough gold.", "Você não tem ouro suficiente.");
                return;
            }
            if (item.Id.StartsWith("wallpaper_", System.StringComparison.Ordinal))
                TownSaveVariables.SetString("home.wallpaper", item.Id);
            TownSaveVariables.SetInt(GeneralStorePurchases + "." + item.Id, owned == int.MaxValue ? owned : owned + 1);
            _gold -= item.Price;
            RecordFurPurchase();
            _generalStoreConfirm = false;
            _generalStoreFeedback = T($"Purchased {item.English}.", $"Compra concluída: {item.Portuguese}.");
            PlayRetailSound(RetailSfx.Confirm);
            TownEventBus.Publish(TownEventBus.EventId.ShopPurchased, item.Id);
        }

        private void DrawGeneralStore()
        {
            if (_generalStorePage == GeneralStorePage.Closed) return;
            DrawHudPanel(new Rect(85, 86, 470, 301));
            GUI.Label(new Rect(103, 101, 320, 24), T("General Store · Fur", "Loja Geral · Fur"));
            GUI.Label(new Rect(429, 101, 110, 24), _gold + " G");
            if (_generalStoreConfirm)
            {
                var item = GeneralStock[_generalStoreStockRows[_generalStoreSelection]];
                GUI.Label(new Rect(103, 144, 432, 50), _generalStoreFeedback ??
                    T($"Buy {item.English} for {item.Price} G?", $"Comprar {item.Portuguese} por {item.Price} G?"));
                if (GUI.Button(new Rect(103, 212, 215, 26), WithInteractHint("Confirm", "Confirmar"))) BuyGeneralStoreItem();
                if (GUI.Button(new Rect(320, 212, 215, 26), T("Back", "Voltar"))) BackFromGeneralStore();
                return;
            }
            string prompt = _generalStorePage switch
            {
                GeneralStorePage.Services => T("What can I do for you?", "Em que posso ajudar?"),
                GeneralStorePage.Buy => T("Choose an item to buy.", "Escolha um item para comprar."),
                _ => GeneralStoreOptionCount == 0 ? T("You haven't bought anything yet.", "Você ainda não comprou nenhum item.") :
                    T("Your purchases", "Suas compras")
            };
            GUI.Label(new Rect(103, 144, 432, 48), _generalStoreFeedback ?? prompt);
            int first = _generalStoreSelection / GeneralStoreRows * GeneralStoreRows;
            int count = GeneralStoreOptionCount;
            for (int i = first; i < Mathf.Min(first + GeneralStoreRows, count); i++)
            {
                var row = new Rect(103, 211 + (i - first) * 26, 432, 24);
                string label;
                if (_generalStorePage == GeneralStorePage.Services)
                    label = i == 0 ? T("Buy", "Comprar") : i == 1 ? T("Purchases", "Compras") : T("Leave", "Sair");
                else
                {
                    bool buying = _generalStorePage == GeneralStorePage.Buy;
                    var item = GeneralStock[buying ? _generalStoreStockRows[i] : _generalStorePurchasedRows[i]];
                    label = T(item.English, item.Portuguese) + (buying ? $"   {item.Price} G" : $"   ×{GeneralStoreOwned(item.Id)}");
                }
                if (GUI.Button(row, i == _generalStoreSelection ? "▸ " + label : label))
                {
                    _generalStoreSelection = i;
                    ChooseGeneralStoreRow(i);
                    break;
                }
            }
            if (first > 0 && GUI.Button(new Rect(103, 350, 110, 26), T("Previous", "Anterior")))
                _generalStoreSelection = first - GeneralStoreRows;
            if (first + GeneralStoreRows < count && GUI.Button(new Rect(218, 350, 110, 26), T("Next", "Próxima")))
                _generalStoreSelection = first + GeneralStoreRows;
            if (GUI.Button(new Rect(416, 350, 119, 26), T("Back [Esc]", "Voltar [Esc]"))) BackFromGeneralStore();
        }
    }
}
