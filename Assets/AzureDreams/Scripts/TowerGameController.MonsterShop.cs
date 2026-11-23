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
        private enum MonsterShopPage { Closed, Greeting, CustomerIntro, Services, Buy, ConfirmBuy, Sell, Confirm, Farewell, InfusionFamiliar, InfusionSpell }
        private MonsterShopPage _monsterShopPage;
        private int _monsterShopSelection;
        private MonsterShopData _monsterShopData;
        private bool InMonsterShop => _interior && _interior.interiorId == MonsterShopScene;

        [Serializable] private sealed class EggPrice { public int id, sell; public string name; }
        [Serializable] private sealed class HunterDialogue
        {
            public int actor, minimumFloor;
            public bool kewneChoice;
            public string[] lines, ptBr;
        }
        [Serializable] private sealed class MonsterShopData
        {
            public string buy, greeting, customer, sellPrompt, thanks;
            public string buyPtBr, greetingPtBr, customerPtBr, sellPromptPtBr, thanksPtBr;
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

        private static int MasterReputationTier(TownReputation reputation)
        {
            // Tower progress is the principal proof of Koh's reputation.  Completed
            // town routes are an alternate public marker so that the dialogue still
            // acknowledges a player who has been helping the people of Monsbaiya.
            if (reputation.HighestFloor >= 40) return 5;
            if (reputation.HighestFloor >= 30) return 4;
            if (reputation.HighestFloor >= 20 || reputation.CompletedRouteCount >= 4) return 3;
            if (reputation.HighestFloor >= 10 || reputation.CompletedRouteCount >= 2) return 2;
            if (reputation.HighestFloor >= 5 || reputation.CompletedRouteCount >= 1) return 1;
            return 0;
        }

        private static string[] MasterDialogueLines(int tier)
        {
            return tier switch
            {
                5 => new[] { MasterLateWelcome() },
                4 => new[]
                {
                    GameLanguage.Text("As of late, there’s not one person in this city who does not know of you.",
                        "Ultimamente, não há uma só pessoa nesta cidade que não conheça você."),
                    GameLanguage.Text("At this rate, you could surpass even Guy himself as a monster tamer.",
                        "Nesse ritmo, você pode superar até o próprio Guy como domador de monstros."),
                    MasterLateWelcome()
                },
                3 => new[]
                {
                    GameLanguage.Text("Welcome.\nHow may I assist you today.",
                        "Boas-vindas. Como posso ajudar hoje?")
                },
                2 => new[]
                {
                    GameLanguage.Text("You know, you’re becoming a pretty good tamer.",
                        "Sabe, você está se tornando um domador muito bom."),
                    GameLanguage.Text("Welcome.\nHow may I assist you today.", "Boas-vindas.\nComo posso ajudar hoje?")
                },
                1 => new[]
                {
                    GameLanguage.Text("Welcome.\nHow can I help you today.",
                        "Boas-vindas. Como posso ajudar hoje?")
                },
                _ => new[]
                {
                    GameLanguage.Text("Your father, Guy, was super.", "Seu pai, Guy, era incrível."),
                    GameLanguage.Text("He would frequently come to sell top quality monsters.", "Ele vinha sempre vender monstros de primeira.")
                }
            };
        }

        private static string MasterLateWelcome() =>
            GameLanguage.Text("Welcome.  I’ve been waiting for you, {name}.\nHow may I assist you today.",
                "Boas-vindas. Estive esperando por você, {name}. Como posso ajudar hoje?");

        private string MasterShopGreeting()
        {
            return T(_monsterShopData.greeting, _monsterShopData.greetingPtBr).Replace("{name}", HeroName);
        }

        private string MasterCustomerIntro() => T(
            _monsterShopData.customer + "\nI doubt you’re here to buy anything... and you’ve run out of the familiars your father left you, right?",
            _monsterShopData.customerPtBr + "\nDuvido que tenha vindo comprar algo... e você já ficou sem os familiares que seu pai deixou, não é?");

        private string MasterServicesPrompt()
        {
            return T("My stock has been low for some time on familiars.\nI’m especially interested in the eggs, I’ll offer a lot more for them.",
                "Meu estoque de familiares está baixo há algum tempo.\nTenho interesse especial em ovos e vou oferecer muito mais por eles.");
        }

        private void SetupMonsterShop()
        {
            ResetMonsterShop();
            if (!InMonsterShop) return;
            if (_monsterShopData == null)
            {
                var text = AssetMode.Load<TextAsset>("Town/Retail/monster_shop");
                if (text) _monsterShopData = JsonUtility.FromJson<MonsterShopData>(text.text);
            }
            int hunterNumber = 0;
            foreach (var npc in _townPeople)
            {
                if (!npc || npc.npcId != HunterId) continue;
                if (hunterNumber >= 3) break;
                npc.npcId = HunterId + "_" + ++hunterNumber;
                // Scene order matches the three original hunter speaker slots.
                // Replace the legacy captured body only after assigning its identity.
                RetailResidentBanks.Apply(npc.gameObject, npc.npcId);
                npc.GetComponent<CharacterStats>().displayName = T("Hunter", "Caçador");
            }
        }

        private bool TryMonsterShopHunterDialogue(NPCController speaker)
        {
            if (!InMonsterShop || _monsterShopData?.hunters == null ||
                !speaker.npcId.StartsWith(HunterId + "_", StringComparison.Ordinal)) return false;
            if (!int.TryParse(speaker.npcId.Substring(HunterId.Length + 1), out int number) ||
                number < 1 || number > 3) return false;
            int actor = 0x33 + number;
            int highestFloor = TownReputation.Read().HighestFloor;
            var available = Array.FindAll(_monsterShopData.hunters,
                dialogue => dialogue.actor == actor && highestFloor >= dialogue.minimumFloor);
            if (available.Length == 0) return false;
            var dialogue = available[Mathf.Max(0, StoryDay) % available.Length];
            if (dialogue.kewneChoice)
                return StoryAsk(speaker, dialogue.lines[0], dialogue.ptBr[0],
                    (T("I just heard about it now", "Acabei de ouvir falar"), () =>
                        StoryTalk(speaker, "What should we do if this monster is a scary creature?",
                            "O que vamos fazer se esse monstro for assustador?")),
                    (T("I haven’t heard about it", "Não ouvi falar disso"), () =>
                        StoryTalk(speaker, "Oh, right.  You’d better be careful yourself.",
                            "Ah, certo. É melhor você tomar cuidado também.")));
            speaker.dialogue = null;
            speaker.dialogueLines = GameLanguage.Portuguese && dialogue.ptBr != null ? dialogue.ptBr : dialogue.lines;
            return false;
        }

        private bool TryMonsterShopCounter()
        {
            if (!InMonsterShop || _monsterShopData == null || !NearHomeCell(MasterCounter, 1.2f)) return false;
            var master = _townPeople.Find(npc => npc && npc.npcId == "master");
            if (master) return TalkToMaster(master);
            SetMonsterShopPage(MonsterShopPage.Greeting);
            return true;
        }

        private bool TalkToMaster(NPCController master)
        {
            if (!InMonsterShop || _monsterShopData == null) return false;
            const string introduced = "dialogue.master.introduced";
            int tier = MasterReputationTier(TownReputation.Read());
            bool firstVisit = !TownSaveVariables.GetBool(introduced);
            if (tier == 0 && !firstVisit)
            {
                SetMonsterShopPage(MonsterShopPage.Greeting);
                return true;
            }

            var lines = MasterDialogueLines(tier);
            void Continue(int index)
            {
                if (index < lines.Length)
                {
                    string line = lines[index];
                    StoryTalk(master, line, line, () => Continue(index + 1));
                    return;
                }
                if (firstVisit)
                {
                    TownSaveVariables.SetBool(introduced, true);
                    PersistTownProgress();
                }
                SetMonsterShopPage(tier > 0 ? MonsterShopPage.Services : MonsterShopPage.Greeting);
            }
            Continue(0);
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
            _shopPurchaseMessage = null;
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
            PlayRetailSound(RetailSfx.Confirm);
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
            PlayRetailSound(RetailSfx.Confirm);
            var offer = _eggOffers[_monsterShopSelection];
            var items = _townInventory.DungeonItems;
            if (offer.Index >= items.Count || items[offer.Index].Kind != RetailEggKind ||
                items[offer.Index].Subtype != offer.Price.id || _gold > int.MaxValue - offer.Price.sell)
                return;
            if (!_townInventory.ConsumeDungeonItem(offer.Index, RetailEggKind)) return;
            _gold += offer.Price.sell;
            PersistTownProgress();
            ReadEggOffers();
            _shopPurchaseMessage = T($"(Receives {offer.Price.sell}G.)", $"(Recebe {offer.Price.sell}G.)");
            SetMonsterShopPage(MonsterShopPage.Sell);
        }

        private void DeclineEggSale()
        {
            _shopPurchaseMessage = T("Don’t waste my time.", "Não perca meu tempo.");
            SetMonsterShopPage(MonsterShopPage.Sell);
        }

        private int ShopOptionCount => _monsterShopPage switch
        {
            MonsterShopPage.Greeting => 2,
            MonsterShopPage.CustomerIntro => 1,
            MonsterShopPage.Farewell => 1,
            MonsterShopPage.Services => 4,
            MonsterShopPage.InfusionFamiliar => _infusionFamiliars.Count,
            MonsterShopPage.InfusionSpell => InfusionSpells.Count,
            MonsterShopPage.Sell => _eggOffers.Count,
            MonsterShopPage.Buy => _eggsForSale.Count,
            _ => 0
        };

        private bool HandleMonsterShopInput()
        {
            if (_monsterShopPage == MonsterShopPage.Closed) return false;
            var keys = Keyboard.current;
            if (keys == null) return true;
            if (_cancel?.WasPressedThisFrame() == true)
            {
                PlayRetailSound(RetailSfx.Cancel);
                if (_monsterShopPage == MonsterShopPage.Confirm) DeclineEggSale();
                else if (_monsterShopPage == MonsterShopPage.ConfirmBuy) { _monsterShopPage = MonsterShopPage.Buy; _shopPurchaseMessage = null; }
                else if (_monsterShopPage is MonsterShopPage.Buy or MonsterShopPage.Sell) SetMonsterShopPage(MonsterShopPage.Services);
                else if (_monsterShopPage is MonsterShopPage.InfusionFamiliar or MonsterShopPage.InfusionSpell) MagicInfusionBack();
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
                int step = MenuDirectionPressed(1) ? -1 : MenuDirectionPressed(2) ? 1 : 0;
                int previous = _monsterShopSelection;
                _monsterShopSelection = Mathf.Clamp(_monsterShopSelection + step, 0, ShopOptionCount - 1);
                if (_monsterShopSelection != previous) PlayRetailSound(RetailSfx.Cursor);
            }
            return true;
        }

        private void SelectMonsterShopOption(int index)
        {
            PlayRetailSound(RetailSfx.Confirm);
            switch (_monsterShopPage)
            {
                case MonsterShopPage.Greeting:
                    SetMonsterShopPage(index == 0 ? MonsterShopPage.CustomerIntro : MonsterShopPage.Closed);
                    break;
                case MonsterShopPage.CustomerIntro:
                    SetMonsterShopPage(MonsterShopPage.Services);
                    break;
                case MonsterShopPage.Services:
                    if (index == 0) { ReadEggOffers(); SetMonsterShopPage(MonsterShopPage.Sell); }
                    else if (index == 1) OpenEggStock();
                    else if (index == 2) OpenMagicInfusion();
                    else SetMonsterShopPage(MonsterShopPage.Farewell);
                    break;
                case MonsterShopPage.Farewell:
                    ResetMonsterShop();
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
                case MonsterShopPage.InfusionFamiliar:
                    SelectInfusionFamiliar(index);
                    break;
                case MonsterShopPage.InfusionSpell:
                    InfuseSelectedSpell(index);
                    break;
            }
        }

        private void DrawMonsterShop()
        {
            if (!InMonsterShop || _monsterShopData == null || _monsterShopPage == MonsterShopPage.Closed) return;
            DrawHudPanel(new Rect(85, 86, 470, 301));
            GUI.Label(new Rect(103, 101, 300, 24), T("Monster Shop - Master", "Loja de Monstros - Master"));
            string dialogue = _monsterShopPage switch
            {
                MonsterShopPage.Greeting => MasterShopGreeting(),
                MonsterShopPage.CustomerIntro => MasterCustomerIntro(),
                MonsterShopPage.Services => MasterServicesPrompt(),
                MonsterShopPage.Farewell => T(_monsterShopData.thanks, _monsterShopData.thanksPtBr),
                MonsterShopPage.Buy => _shopPurchaseMessage ?? (_eggsForSale.Count == 0
                    ? T(_monsterShopData.buy, _monsterShopData.buyPtBr)
                    : T("Select an egg to buy.", "Escolha um ovo para comprar.")),
                MonsterShopPage.ConfirmBuy => _shopPurchaseMessage ??
                    $"Buy {_eggsForSale[_monsterShopSelection].name} Egg for {PurchasePrice(_eggsForSale[_monsterShopSelection])}G?",
                MonsterShopPage.Sell => _shopPurchaseMessage ?? (_eggOffers.Count > 0
                    ? T(_monsterShopData.sellPrompt, _monsterShopData.sellPromptPtBr)
                    : T("No eggs to sell.", "Nenhum ovo para vender.")),
                MonsterShopPage.InfusionFamiliar or MonsterShopPage.InfusionSpell => MagicInfusionDialogue(),
                MonsterShopPage.Confirm => T("How about for ", "Que tal por ") + _eggOffers[_monsterShopSelection].Price.sell + "G.",
                _ => ""
            };
            _shopDialogueStyle ??= new GUIStyle(_messageStyle) { wordWrap = true, fontSize = 13 };
            DrawHudText(new Rect(103, 135, 432, 102), dialogue, _shopDialogueStyle, dialogue: true);
            switch (_monsterShopPage)
            {
                case MonsterShopPage.Greeting:
                    DrawShopOptions(T("I’m a customer.", "Sou um cliente."), T("Okay, okay, I’ll go home.", "Está bem, está bem, vou para casa."));
                    break;
                case MonsterShopPage.CustomerIntro:
                case MonsterShopPage.Farewell:
                    DrawShopOptions(WithInteractHint("Continue", "Continuar"));
                    break;
                case MonsterShopPage.Services:
                    DrawShopOptions(T("I’ve come to sell.", "Vim vender."), T("I’ve come to buy.", "Vim comprar."),
                        T("Magic Infusion", "Infusão Mágica"), T("Just looking around.", "Só estou olhando."));
                    break;
                case MonsterShopPage.InfusionFamiliar:
                case MonsterShopPage.InfusionSpell:
                    DrawMagicInfusionOptions();
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
                    if (GUI.Button(new Rect(416, 350, 119, 26), WithCancelHint("Back", "Voltar"))) SetMonsterShopPage(MonsterShopPage.Services);
                    break;
                case MonsterShopPage.ConfirmBuy:
                    if (GUI.Button(new Rect(103, 249, 215, 30), WithInteractHint("Buy", "Comprar"))) BuySelectedEgg();
                    if (GUI.Button(new Rect(320, 249, 215, 30), WithCancelHint("Cancel", "Cancelar")))
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
                    if (GUI.Button(new Rect(416, 350, 119, 26), WithCancelHint("Back", "Voltar"))) SetMonsterShopPage(MonsterShopPage.Services);
                    break;
                case MonsterShopPage.Confirm:
                    if (GUI.Button(new Rect(103, 249, 215, 30), WithInteractHint("I'll sell.", "Aceito."))) SellSelectedEgg();
                    if (GUI.Button(new Rect(320, 249, 215, 30), WithCancelHint("My mistake.", "Cancelar"))) DeclineEggSale();
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
