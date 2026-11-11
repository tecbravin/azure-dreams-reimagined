using System;
using System.Collections.Generic;
using AzureDreams.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const byte RetailEggKind = 0x12;
        private const int ShopPageSize = 5;
        private const string MonsterShopScene = "Interior_14_monster_shop";
        private static readonly Vector2Int MonsterShopExit = new(16, 17);
        private static readonly Vector2Int MasterCounter = new(16, 12);
        private const string HunterId = "monster_shop_hunter";
        private enum MonsterShopPage { Closed, Greeting, Services, Buy, ConfirmBuy, Sell, Confirm }
        private MonsterShopPage _monsterShopPage;
        private int _monsterShopSelection;
        private MonsterShopData _monsterShopData;
        private bool InMonsterShop => _interior && _interior.interiorId == MonsterShopScene;

        [Serializable] private sealed class EggPrice { public int id, sell; public string name; }
        [Serializable] private sealed class HunterDialogue { public string[] lines, ptBr; }
        [Serializable] private sealed class MonsterShopData
        {
            public string buy, greeting, customer, sellPrompt, thanks;
            public string greetingPtBr, customerPtBr, sellPromptPtBr, thanksPtBr;
            public HunterDialogue[] hunters;
            public EggPrice[] eggs;
        }
        private readonly struct EggOffer
        {
            public readonly int Index;
            public readonly EggPrice Price;
            public EggOffer(int index, EggPrice price) { Index = index; Price = price; }
        }
        private readonly List<EggOffer> _eggOffers = new();
        private readonly List<EggPrice> _eggsForSale = new();
        private string _shopPurchaseMessage;

        private void SetupMonsterShop()
        {
            ResetMonsterShop();
            if (!InMonsterShop) return;
            if (_monsterShopData == null)
            {
                var text = Resources.Load<TextAsset>("Town/Retail/monster_shop");
                if (text) _monsterShopData = JsonUtility.FromJson<MonsterShopData>(text.text);
            }
            if (_monsterShopData?.hunters == null) return;
            int hunterNumber = 0;
            foreach (var npc in _townPeople)
            {
                if (!npc || npc.npcId != HunterId) continue;
                if (hunterNumber >= _monsterShopData.hunters.Length) break;
                npc.npcId = HunterId + "_" + ++hunterNumber;
                var sprite = npc.GetComponentInChildren<SpriteRenderer>();
                if (sprite)
                {
                    // Match 110% of Koh's standing height at the interior camera scale.
                    const float standingScale = 1.77375f;
                    sprite.transform.localScale *= standingScale * InteriorCameraDistance / _townCameraDistance;
                }
                LocalizeHunter(npc);
            }
            _message = T($"Monster Shop. Press {InteractKey} at the counter to speak with Master.",
                $"Loja de Monstros. Pressione {InteractKey} no balcão para falar com Master.");
        }

        private void LocalizeHunter(NPCController speaker)
        {
            if (!InMonsterShop || _monsterShopData?.hunters == null ||
                !speaker.npcId.StartsWith(HunterId + "_", StringComparison.Ordinal)) return;
            if (!int.TryParse(speaker.npcId.Substring(HunterId.Length + 1), out int number) ||
                number < 1 || number > _monsterShopData.hunters.Length) return;
            var dialogue = _monsterShopData.hunters[number - 1];
            speaker.dialogueLines = GameLanguage.Portuguese && dialogue.ptBr != null ? dialogue.ptBr : dialogue.lines;
            speaker.GetComponent<CharacterStats>().displayName = T("Hunter", "Caçador");
        }

        private bool TryMonsterShopCounter()
        {
            if (!InMonsterShop || _monsterShopData == null || !NearHomeCell(MasterCounter, 1.2f)) return false;
            SetMonsterShopPage(MonsterShopPage.Greeting);
            return true;
        }

        private void ResetMonsterShop()
        {
            SetMonsterShopPage(MonsterShopPage.Closed);
            _eggOffers.Clear();
            _eggsForSale.Clear();
            _shopPurchaseMessage = null;
        }

        private void SetMonsterShopPage(MonsterShopPage page)
        {
            _monsterShopPage = page;
            _monsterShopSelection = 0;
        }

        private void ReadEggOffers()
        {
            _eggOffers.Clear();
            var items = _townInventory.DungeonItems;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Kind != RetailEggKind) continue;
                var price = Array.Find(_monsterShopData.eggs, row => row.id == item.Subtype);
                if (price != null) _eggOffers.Add(new EggOffer(i, price));
            }
        }

        private static int PurchasePrice(EggPrice egg) => 2 * egg.sell;

        private void OpenEggStock()
        {
            _eggsForSale.Clear();
            foreach (var egg in _monsterShopData.eggs)
                if (FindEggSpecies(egg.id)?.forSale == true) _eggsForSale.Add(egg);
            _shopPurchaseMessage = null;
            SetMonsterShopPage(MonsterShopPage.Buy);
        }

        private void BuySelectedEgg()
        {
            if (_monsterShopSelection < 0 || _monsterShopSelection >= _eggsForSale.Count) return;
            var egg = _eggsForSale[_monsterShopSelection];
            int cost = PurchasePrice(egg);
            if (_gold < cost) { _shopPurchaseMessage = T("You don't have enough gold.", "Você não tem ouro suficiente."); return; }
            if (!_townInventory.HasSpace()) { _shopPurchaseMessage = T("Your inventory is full.", "Seu inventário está cheio."); return; }
            _townInventory.AddDungeonItem(MonsterEgg.Create((byte)egg.id, true));
            _gold -= cost;
            PersistTownProgress();
            _shopPurchaseMessage = $"Bought a {egg.name} Egg for {cost}G.";
            _message = _shopPurchaseMessage;
            _monsterShopPage = MonsterShopPage.Buy;
        }

        private void SellSelectedEgg()
        {
            if (_monsterShopSelection < 0 || _monsterShopSelection >= _eggOffers.Count) return;
            var offer = _eggOffers[_monsterShopSelection];
            var items = _townInventory.DungeonItems;
            if (offer.Index >= items.Count || items[offer.Index].Kind != RetailEggKind ||
                items[offer.Index].Subtype != offer.Price.id || _gold > int.MaxValue - offer.Price.sell)
                return;
            if (!_townInventory.ConsumeDungeonItem(offer.Index, RetailEggKind)) return;
            _gold += offer.Price.sell;
            PersistTownProgress();
            _message = T(_monsterShopData.thanks, _monsterShopData.thanksPtBr);
            ReadEggOffers();
            SetMonsterShopPage(MonsterShopPage.Sell);
        }

        private int ShopOptionCount => _monsterShopPage switch
        {
            MonsterShopPage.Greeting => 2,
            MonsterShopPage.Services => 3,
            MonsterShopPage.Sell => _eggOffers.Count,
            MonsterShopPage.Buy => _eggsForSale.Count,
            _ => 0
        };

        private bool HandleMonsterShopInput()
        {
            if (_monsterShopPage == MonsterShopPage.Closed) return false;
            var keys = Keyboard.current;
            if (keys == null) return true;
            if (keys.escapeKey.wasPressedThisFrame)
            {
                if (_monsterShopPage == MonsterShopPage.Confirm) SetMonsterShopPage(MonsterShopPage.Sell);
                else if (_monsterShopPage == MonsterShopPage.ConfirmBuy) { _monsterShopPage = MonsterShopPage.Buy; _shopPurchaseMessage = null; }
                else if (_monsterShopPage is MonsterShopPage.Buy or MonsterShopPage.Sell) SetMonsterShopPage(MonsterShopPage.Services);
                else ResetMonsterShop();
            }
            else if (_interact != null && _interact.WasPressedThisFrame())
            {
                if (_monsterShopPage == MonsterShopPage.Confirm) SellSelectedEgg();
                else if (_monsterShopPage == MonsterShopPage.ConfirmBuy) BuySelectedEgg();
                else if (ShopOptionCount > 0) SelectMonsterShopOption(_monsterShopSelection);
            }
            else if (ShopOptionCount > 0)
            {
                int step = keys.upArrowKey.wasPressedThisFrame ? -1 : keys.downArrowKey.wasPressedThisFrame ? 1 : 0;
                _monsterShopSelection = Mathf.Clamp(_monsterShopSelection + step, 0, ShopOptionCount - 1);
            }
            return true;
        }

        private void SelectMonsterShopOption(int index)
        {
            switch (_monsterShopPage)
            {
                case MonsterShopPage.Greeting:
                    SetMonsterShopPage(index == 0 ? MonsterShopPage.Services : MonsterShopPage.Closed);
                    break;
                case MonsterShopPage.Services:
                    if (index == 0) { ReadEggOffers(); SetMonsterShopPage(MonsterShopPage.Sell); }
                    else if (index == 1) OpenEggStock();
                    else SetMonsterShopPage(MonsterShopPage.Closed);
                    break;
                case MonsterShopPage.Buy:
                    _monsterShopSelection = index;
                    _shopPurchaseMessage = null;
                    _monsterShopPage = MonsterShopPage.ConfirmBuy;
                    break;
                case MonsterShopPage.Sell:
                    _monsterShopSelection = index;
                    _monsterShopPage = MonsterShopPage.Confirm;
                    break;
            }
        }

        private void DrawMonsterShop()
        {
            if (!InMonsterShop || _monsterShopData == null || _monsterShopPage == MonsterShopPage.Closed) return;
            DrawHudPanel(new Rect(85, 86, 470, 301));
            GUI.Label(new Rect(103, 101, 300, 24), T("Monster Shop - Master", "Loja de Monstros - Master"));
            GUI.Label(new Rect(429, 101, 110, 24), _gold + "G");
            string dialogue = _monsterShopPage switch
            {
                MonsterShopPage.Greeting => T(_monsterShopData.greeting, _monsterShopData.greetingPtBr),
                MonsterShopPage.Services => T(_monsterShopData.customer, _monsterShopData.customerPtBr),
                MonsterShopPage.Buy => _shopPurchaseMessage ?? T("Select an egg to buy.", "Escolha um ovo para comprar."),
                MonsterShopPage.ConfirmBuy => _shopPurchaseMessage ??
                    $"Buy {_eggsForSale[_monsterShopSelection].name} Egg for {PurchasePrice(_eggsForSale[_monsterShopSelection])}G?",
                MonsterShopPage.Sell => _eggOffers.Count > 0 ? T(_monsterShopData.sellPrompt, _monsterShopData.sellPromptPtBr) : T("No eggs to sell.", "Nenhum ovo para vender."),
                MonsterShopPage.Confirm => T("How about for ", "Que tal por ") + _eggOffers[_monsterShopSelection].Price.sell + "G?",
                _ => ""
            };
            _shopDialogueStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true };
            GUI.Label(new Rect(103, 135, 432, 102), dialogue, _shopDialogueStyle);
            switch (_monsterShopPage)
            {
                case MonsterShopPage.Greeting:
                    DrawShopOptions(T("I'm a customer.", "Sou um cliente."), T("Okay, okay, I'll go home.", "Está bem, vou para casa."));
                    break;
                case MonsterShopPage.Services:
                    DrawShopOptions(T("I've come to sell.", "Vim vender."), T("I've come to buy.", "Vim comprar."), T("Just looking around.", "Só estou olhando."));
                    break;
                case MonsterShopPage.Buy:
                    int first = _monsterShopSelection / ShopPageSize * ShopPageSize;
                    for (int i = first; i < Mathf.Min(first + ShopPageSize, _eggsForSale.Count); i++)
                    {
                        var egg = _eggsForSale[i];
                        if (DrawShopOption(i, i - first, egg.name + " Egg   " + PurchasePrice(egg) + "G"))
                        { SelectMonsterShopOption(i); break; }
                    }
                    if (GUI.Button(new Rect(104, 350, 110, 26), T("Previous", "Anterior"))) _monsterShopSelection = Mathf.Max(0, first - ShopPageSize);
                    if (GUI.Button(new Rect(218, 350, 110, 26), T("Next", "Próxima"))) _monsterShopSelection = Mathf.Min(
                        Mathf.Max(0, _eggsForSale.Count - 1), first + ShopPageSize);
                    if (GUI.Button(new Rect(416, 350, 119, 26), T("Back [Esc]", "Voltar [Esc]"))) SetMonsterShopPage(MonsterShopPage.Services);
                    break;
                case MonsterShopPage.ConfirmBuy:
                    if (GUI.Button(new Rect(103, 249, 215, 30), WithInteractHint("Buy", "Comprar"))) BuySelectedEgg();
                    if (GUI.Button(new Rect(320, 249, 215, 30), T("Cancel [Esc]", "Cancelar [Esc]")))
                    { _monsterShopPage = MonsterShopPage.Buy; _shopPurchaseMessage = null; }
                    break;
                case MonsterShopPage.Sell:
                    int start = _monsterShopSelection / ShopPageSize * ShopPageSize;
                    for (int i = start; i < Mathf.Min(start + ShopPageSize, _eggOffers.Count); i++)
                    {
                        var price = _eggOffers[i].Price;
                        if (DrawShopOption(i, i - start, price.name + "   " + price.sell + "G"))
                        { SelectMonsterShopOption(i); break; }
                    }
                    if (GUI.Button(new Rect(416, 350, 119, 26), T("Back [Esc]", "Voltar [Esc]"))) SetMonsterShopPage(MonsterShopPage.Services);
                    break;
                case MonsterShopPage.Confirm:
                    if (GUI.Button(new Rect(103, 249, 215, 30), WithInteractHint("I'll sell.", "Aceito."))) SellSelectedEgg();
                    if (GUI.Button(new Rect(320, 249, 215, 30), T("My mistake. [Esc]", "Cancelar [Esc]"))) SetMonsterShopPage(MonsterShopPage.Sell);
                    break;
            }
        }

        private GUIStyle _shopDialogueStyle;
        private void DrawShopOptions(params string[] options)
        {
            for (int i = 0; i < options.Length; i++)
                if (DrawShopOption(i, i, options[i])) { SelectMonsterShopOption(i); break; }
        }
        private bool DrawShopOption(int index, int row, string label) =>
            GUI.Button(new Rect(103, 212 + row * 26, 432, 24),
                index == _monsterShopSelection ? "> " + label : label);
    }
}
