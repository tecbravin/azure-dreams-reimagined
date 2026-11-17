using System;
using UnityEngine;
using UnityEngine.InputSystem;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        internal const string CherrlHealingQuestCompleted = "quest.cherrl.healing_herb_delivered";
        internal const string WreathFruitAvailable = "home.wreath_pita_available";
        [Serializable] private sealed class MonsterBookPage { public int number; public string name, description, portrait, namePtBr, descriptionPtBr; }
        [Serializable] private sealed class MonsterBook { public MonsterBookPage[] pages; }
        private const string WeedyPortrait = "Dialogue/Weedy_variant0_frame0";
        private const string WeedyBookOfferPortrait = "Dialogue/Weedy_variant1_frame0";
        private const string WeedyBookEyesClosed = "Dialogue/Weedy_variant1_frame1";
        private const string WeedyBookEyesClosedRetail = "Dialogue/Weedy_variant1_frame2";
        private const string WeedyBookEyesHalfClosed = "Dialogue/Weedy_variant1_frame3";
        private static readonly int[] WeedyBookBlinkDurations = { 96, 6, 16, 4 };
        private const int WreathFruitGivenFlag = 22; // sn_mam_pan_give
        private const int FirstHouseGoodsPurchaseFlag = 5321; // F_sb22_goods_first
        private const int PoolRequestAcceptedFlag = 1531; // F_sbs13_pad02
        private const int PoolInvitationFinishedFlag = 1533; // F_sbs13_pad04
        private const string WreathPortrait = "Dialogue/Wreath_variant0_frame0";
        private const string WreathPregnantPortrait = "Dialogue/Wreath_variant1_frame0";
        private const string WreathAmuletPortrait = "Dialogue/WreathBrokenAmulet";
        private const string WreathAmuletOriginalPortrait = "Dialogue/Wreath_variant2_frame0";
        private const string WreathBirthPortrait = "Dialogue/Wreath_variant3_frame0";
        private const string WreathHomecomingKey = StoryPrefix + "wreath.homecoming";
        private const int WreathBeldoVisionSeenFlag = 0x1389; // F_veldil_dungeon_deatta
        private enum WreathHomecoming { Unscheduled, Pending, Seen }
        private NPCController _wreathServiceSpeaker;
        private MonsterBook _weedyBook;
        private bool _weedyReading;
        private int _weedyPage;
        private Vector2 _weedyDescriptionScroll;
        private GUIStyle _weedyDescriptionStyle;

        // TOWN.BIN 0x3E677C–0x3E6852.
        private bool WeedyStory(NPCController npc)
        {
            return StoryAsk(npc, "Do you want to read my Monster Book?", "Quer ler meu Livro dos Monstros?",
                WeedyBookOfferPortrait,
                (T("Sure. Give me that cute book.", "Claro. Me empresta esse livrinho fofo."), OpenWeedyBook),
                (T("I’m too busy for that. Sorry.", "Estou ocupado demais. Desculpe."), null));
        }

        private void WeedyHomeReply(string english, string portuguese, Action next = null)
        {
            var weedy = FindStoryActor("weedy");
            if (weedy && weedy.gameObject.activeInHierarchy)
                StoryTalk(weedy, english, portuguese, WeedyPortrait, next);
        }

        private int WeedyBookBlinkFrame()
        {
            int elapsed = Mathf.FloorToInt((Time.unscaledTime - _storyPortraitStarted) * 60f);
            return StoryPortraitFrame(WeedyBookBlinkDurations, elapsed);
        }

        private Texture2D WeedyBookDialoguePortrait()
        {
            var open = StreamPortrait(WeedyBookOfferPortrait);
            var closed = StreamPortrait(WeedyBookEyesClosed);
            // The existing HD closed-eye artwork matches retail frame 2.
            return WeedyBookBlinkFrame() == 2
                ? closed ?? open ?? StreamPortrait(WeedyBookEyesClosedRetail)
                : open;
        }

        private void DrawWeedyBookBlink(Texture2D portrait, Rect box)
        {
            if (_storyPortraitPath != WeedyBookOfferPortrait || Event.current.type != EventType.Repaint) return;
            var halfClosed = StreamPortrait(WeedyBookEyesHalfClosed);
            if (!portrait || !halfClosed || WeedyBookBlinkFrame() is not (1 or 3)) return;

            // Frames 1 and 3 share the same half-blink. Draw only the eye area from
            // the supplemental frame to retain the existing portrait's silhouette and alpha.
            DrawDialoguePortraitPatch(portrait, halfClosed, box, Rect.MinMaxRect(520f / 1024f, 560f / 1536f,
                880f / 1024f, 699f / 1536f));
        }

        private bool WreathSequence(NPCController npc, Action done, params (string English, string Portuguese)[] pages)
        {
            bool ShowPage(int index)
            {
                var (english, portuguese) = pages[index];
                Action next = index + 1 < pages.Length ? () => { ShowPage(index + 1); } : done;
                return StoryTalk(npc, english, portuguese, WreathPortrait, next);
            }
            return ShowPage(0);
        }

        private bool WreathStory(NPCController npc)
        {
            var reputation = TownReputation.Read();
            int entry = TownSaveVariables.GetInt(TownReputation.WakeEntryKey);
            var context = new TownHomeDialogueSelector.Context
            {
                Gold = _gold,
                Progress = reputation.HighestFloor,
                HouseQuery = CurrentHomeRank,
                EventState = entry != 0 ? 2 : 0,
                SelectionMode = entry != 0 ? 2 : 0,
                DispatchState = entry != 0 ? 1 : 0,
                SelectedEntry = entry,
                EnabledRecords = TownRetailStoryEventSelector.CountHomeHelpers(reputation),
                FamiliarA = _familiarActor.Species,
                FamiliarB = _bagFamiliars.Count > 0 ? _bagFamiliars[0].Species : 0
            };
            bool HasFlag(int flag) => flag switch
            {
                WreathFruitGivenFlag => !TownSaveVariables.GetBool(WreathFruitAvailable),
                FirstHouseGoodsPurchaseFlag => reputation.IsFlagSet(flag) ||
                    Array.Exists(GeneralStock, item => GeneralStoreOwned(item.Id) > 0),
                PoolRequestAcceptedFlag => reputation.IsFlagSet(flag) || TownSaveVariables.GetBool(PoolRequestSeen),
                PoolInvitationFinishedFlag => reputation.IsFlagSet(flag) || PoolRestored,
                _ => reputation.IsFlagSet(flag)
            };
            uint address = TownHomeDialogueSelector.Select(context, HasFlag, reputation.ApplyOneTimeFlagOperation);
            TownSaveVariables.SetString("dialogue.wreath.retail_text_address", address.ToString("X8"));
            TownSaveVariables.Save();

            // TOWN.BIN 0x3E5000, loaded at 0x80016000. Each pair is a
            // message page, and the selected address owns its own ending.
            switch (address)
            {
                case 0x8001602A or 0x80016359 when PendingWreathHomecoming:
                    return WreathHomecomingConversation(npc);
                case 0x80016359: return WreathServices(npc, WreathServicePrompt.Depart);
                case 0x800164CC:
                    return StoryAsk(npc, "What? You're buying something for the house?",
                        "O quê? Você vai comprar algo para a casa?", WreathPortrait,
                        (T("Yes I am.", "Sim."), () => WreathSequence(npc, null,
                            ("You've made my day.", "Você alegrou meu dia."),
                            ("Well, let's see.", "Bem, vamos ver."),
                            ("Why don't you go out to the town and browse through the stores to see what you can get.",
                                "Que tal dar uma volta pela cidade e olhar nas lojas para ver o que você encontra?"),
                            ("It would be great if you could get something that everyone could use, like a TV or refrigerator, but within a price range you're comfortable with.",
                                "Seria ótimo comprar algo que todos possam usar, como uma TV ou uma geladeira, dentro do preço que você pode pagar."))),
                        (T("I'm buying it for my own use.", "Vou comprar algo para mim."), () => WreathSequence(npc, null,
                            ("Is that so.\nHmm...", "É mesmo.\nHmm..."),
                            ("You've become so cold hearted. Oh, Guy.", "Como você ficou insensível. Ah, Guy."),
                            ("What did I do wrong in bringing up this child? Whew!",
                                "Onde foi que eu errei ao criar esse menino? Ufa!"))));
                case 0x8001688A:
                    return WreathSequence(npc, null,
                        ("Good morning. {name}.", "Bom dia, {name}."),
                        ("Until now, every time you went on an adventure, you have had to sell all items you could not take with you, right?",
                            "Até agora, sempre que saía em uma aventura, você precisava vender os itens que não podia levar, certo?"),
                        ("Well if you had a carpenter expand your house so that it had a larger storage space, you could store your items there.",
                            "Se pedisse ao carpinteiro para ampliar a casa e fazer um depósito maior, você poderia guardar seus itens lá."),
                        ("Every storage room you add, the more items you would be able to store.",
                            "A cada depósito que você acrescentar, mais itens poderá guardar."),
                        ("Also, think of all that extra space you would have for everyone to relax!",
                            "Além disso, imagine todo o espaço a mais para todo mundo descansar!"),
                        ("Well, what do you think?", "Então, o que acha?"));
                case 0x80016BCF:
                case 0x80016C24:
                case 0x80016C77:
                case 0x80016CCE:
                case 0x80016D77:
                    var page = address switch
                    {
                        0x80016BCF => ("Nico is a tremendous help to the house.", "A Nico ajuda muito aqui em casa."),
                        0x80016C24 => ("Fur is a tremendous help to the house.", "A Fur ajuda muito aqui em casa."),
                        0x80016C77 => ("Selfi is a tremendous help to the house.", "A Selfi ajuda muito aqui em casa."),
                        0x80016CCE => ("Mia is a tremendous help to the house.", "A Mia ajuda muito aqui em casa."),
                        _ => ("Come on, {name}.\nAll these girlfriends, I've got to keep a closer eye on you.",
                            "Ora, {name}.\nCom tantas namoradas, vou ter que ficar de olho em você.")
                    };
                    return WreathSequence(npc, () =>
                    {
                        TownSaveVariables.SetInt(TownReputation.WakeEntryKey, 0);
                        TownSaveVariables.Save();
                        WreathDepartureChoice(npc, afterHelper: true);
                    }, page);
                case 0x80016E0C:
                    return WreathSequence(npc, null,
                        ("{name}.", "{name}."),
                        ("Thanks to you, {name}, the house has become a much more comfortable place to live in.",
                            "Graças a você, {name}, nossa casa ficou muito mais confortável."),
                        ("Once again, thank you very much.", "Mais uma vez, muito obrigada."),
                        ("It might be good to spend the money you've saved on the town.",
                            "Talvez seja bom gastar o dinheiro que você juntou com a cidade."),
                        ("Why not talk to the bishop about it?", "Por que não conversa com o bispo sobre isso?"),
                        ("As I recall, the bishop is a very kind person, always looking out for other people.",
                            "Pelo que lembro, o bispo é muito bondoso e sempre cuida dos outros."),
                        ("As a result, he hasn't done a thing about the temple...",
                            "Por isso, acabou sem fazer nada pelo templo..."),
                        ("It might be a good thing to make the temple look more impressive.",
                            "Seria bom deixar o templo com uma aparência mais imponente."));
                case 0x80017152:
                    return WreathSequence(npc, null,
                        ("By the way, Mr. Issac, who lives in front of the pond, just dropped by.",
                            "Aliás, o Sr. Isaac, que mora em frente ao lago, acabou de passar por aqui."),
                        ("He seemed to be somewhat troubled.", "Ele parecia um pouco preocupado."),
                        ("{name}, could you not talk to him and find out what's on his mind?",
                            "{name}, você poderia falar com ele e descobrir o que o preocupa?"));
                case 0x800172A3:
                    return WreathSequence(npc, null,
                        ("Could you not talk to Mr. Issac to see what's on his mind?",
                            "Você poderia falar com o Sr. Isaac para saber o que o preocupa?"),
                        ("Mr. Issac lives by the fields where there is a windmill, south of the city.",
                            "O Sr. Isaac mora perto dos campos onde fica o moinho, ao sul da cidade."),
                        ("Do you know where it is?", "Você sabe onde fica?"));
                case 0x800173E4:
                    return WreathSequence(npc, null,
                        ("Oh, I've got something to tell you.", "Ah, tenho algo para te contar."),
                        ("You know Wotta who manages the Woogie Woogie Pool?",
                            "Você conhece o Wotta, que cuida da piscina Woogie Woogie?"),
                        ("Well, he was telling me that he needed to talk to you.", "Ele me disse que precisava falar com você."));
                case 0x80017501:
                    return WreathSequence(npc, null,
                        ("Have you been to the Woogie Woogie Pool?", "Você já foi à piscina Woogie Woogie?"),
                        ("Wotta was looking for you.", "O Wotta estava procurando por você."));
                default: return WreathDepartureChoice(npc);
            }
        }

        private static bool PendingWreathHomecoming =>
            TownSaveVariables.GetInt(WreathHomecomingKey) == (int)WreathHomecoming.Pending;

        private static void ScheduleWreathHomecoming()
        {
            if (TownSaveVariables.GetInt(WreathHomecomingKey) == (int)WreathHomecoming.Unscheduled)
                TownSaveVariables.SetInt(WreathHomecomingKey, (int)WreathHomecoming.Pending);
        }

        // TOWN.BIN 0x42884D–0x428E08. Koh can ask about selling items or simply greet Wreath.
        private bool WreathHomecomingConversation(NPCController npc)
        {
            void Finish()
            {
                TownSaveVariables.SetInt(WreathHomecomingKey, (int)WreathHomecoming.Seen);
                PersistTownProgress();
            }

            void ExplainSafe()
            {
                int capacity = HomeStorageCapacity;
                if (capacity > 10)
                {
                    string thanks = capacity == 60
                        ? "Thanks to you, {name}, the house is a lot bigger,"
                        : "Thanks to you, {name}, the house has a lot more space,";
                    WreathSequence(npc, Finish,
                        ($"{thanks} and now you can leave up to {capacity} items in our home safe.",
                            $"Graças a você, {{name}}, a casa tem muito mais espaço. Agora pode guardar até {capacity} itens no nosso cofre."),
                        ("If there are items you want to keep aside, leave them with me, and I’ll keep them in the safe.",
                            "Se quiser guardar algum item, deixe comigo que eu ponho no cofre."));
                    return;
                }
                WreathSequence(npc, Finish,
                    ("If you’re not taking items to the tower, you can always leave them with me.",
                        "Se não for levar algum item à torre, pode deixar comigo."),
                    ("I’ll keep them in the safe. You can leave up to 10 items in our home safe.",
                        "Vou guardar tudo no cofre. Você pode deixar até 10 itens nele."));
            }

            return StoryAsk(npc, "Oh hi, {name}.", "Ah, olá, {name}.", WreathPortrait,
                (T("I’m home.", "Cheguei."), () => StoryTalk(npc, "You must be tired, get some sleep.",
                    "Você deve estar cansado. Vá dormir um pouco.", WreathPortrait, ExplainSafe)),
                (T("I’m selling some items.", "Vou vender alguns itens."), () => WreathSequence(npc, ExplainSafe,
                    ("Of course, because you can only take up to five items and familiars with you when entering the tower.",
                        "É claro. Ao entrar na torre, você só pode levar cinco itens e familiares no total."),
                    ("Go to the general store and sell everything you don’t need.",
                        "Vá à loja de variedades e venda tudo de que não precisa."),
                    ("You must be tired, get some sleep.", "Você deve estar cansado. Vá dormir um pouco."))));
        }

        private void WreathWakeUpStory(NPCController npc, bool survived)
        {
            if (!survived)
            {
                // TOWN.BIN 0x41C137–0x41C402.
                WreathSequence(npc, () => WeedyHomeReply("Get up! Yup! Up! Pp! p! !", "Levanta! Isso! Ta! Ta! A! !",
                        () => WeedyHomeReply("{name}! Yup!", "{name}! Isso!")),
                    ("Oh, you’re up. You don’t need to feel you should get up yet.",
                        "Ah, você acordou. Não precisa se levantar ainda."),
                    ("It seems that you’re pushing yourself awfully lately. But you never tell me anything unless you really wanted to, isn’t that right?",
                        "Parece que você tem se esforçado demais ultimamente. Mas você só me conta o que quer contar, não é?"),
                    ("That’s fine, you’re a grown man now.", "Tudo bem, você já é adulto."),
                    ("But at the same time, don’t do anything to bring tears to those who care about you. You need to take better care of yourself.",
                        "Só não faça nada que deixe triste quem se importa com você. Você precisa cuidar melhor de si mesmo."),
                    ("Tell me if you plan to go somewhere.", "Avise se estiver pensando em sair."));
                return;
            }
            if (TownRetailScriptFlags.CreatePersistent().IsFlagSet(WreathBeldoVisionSeenFlag) &&
                !TownSaveVariables.GetBool(BeldoClearedKey))
            {
                // TOWN.BIN 0x41B085–0x41B14D.
                WreathSequence(npc, null,
                    ("Well, you’re up!", "Ora, você acordou!"),
                    ("Aren’t you working yourself too hard recently? You can take it easier, you know.",
                        "Você não anda se esforçando demais? Pode ir com mais calma, sabia?"),
                    ("Tell me if you plan to go somewhere.", "Avise se estiver pensando em sair."));
                return;
            }
            if (StoryDay == 1)
            {
                // TOWN.BIN 0x415871–0x415A44.
                WreathSequence(npc, () => WeedyHomeReply("It’s always the same.", "É sempre a mesma coisa."),
                    ("You’re up! You don’t need to be up so early.", "Você acordou! Não precisa levantar tão cedo."),
                    ("What? Are you going to the tower?", "O quê? Já vai à torre?"),
                    ("You can’t get your mind away from the tower, can you?", "Você não consegue parar de pensar na torre, né?"),
                    ("You seem like you’ve grown up...", "Parece que você cresceu..."),
                    ("Yet on the other hand you’re still like a kid.", "Mas, por outro lado, ainda parece uma criança."),
                    ("Here you are.", "Aqui está."),
                    ("Go do you’re best!", "Vá lá e dê o seu melhor!"));
                return;
            }

            // TOWN.BIN 0x3E658F–0x3E6732.
            WreathSequence(npc, null,
                ("You’re up! You don’t need to be up so early.", "Você acordou! Não precisa levantar tão cedo."),
                ("What? Are you going to the tower?", "O quê? Já vai à torre?"),
                ("You can’t get your mind away from the tower, can you?", "Você não consegue parar de pensar na torre, né?"),
                ("You seem like you’ve grown up, yet on the other hand you’re still like a kid.",
                    "Parece que você cresceu, mas por outro lado ainda parece uma criança."),
                ("Here you are. Go do you’re best!", "Aqui está. Vá lá e dê o seu melhor!"));
        }

        private bool WreathDepartureChoice(NPCController npc, bool afterHelper = false) =>
            StoryAsk(npc, afterHelper ? "What is it?  Are you going to the Tower?" : "Are you heading out for the tower already?",
                afterHelper ? "O que foi? Você vai à Torre?" : "Já está indo para a torre?", WreathPortrait,
                (T("I'm off to the tower.", "Vou para a torre."), () => WreathOfferFruit(npc)),
                (T("I've got a favor to ask.", "Quero pedir um favor."), () => WreathServices(npc, WreathServicePrompt.Favor)),
                (T("I'm not leaving yet.", "Ainda não vou sair."), () =>
                    StoryTalk(npc, "Tell me when you're ready to leave.", "Avise quando estiver pronto para sair.", WreathPortrait)));

        private void WreathOfferFruit(NPCController npc)
        {
            if (!TownSaveVariables.GetBool(WreathFruitAvailable) ||
                _townInventory.TotalItems + FamiliarInventoryCount >= Inventory.SlotCapacity)
            {
                WreathFarewell(npc);
                return;
            }
            StoryTalk(npc, "Then take this with you.", "Então leve isto com você.", WreathPortrait, () =>
            {
                _townInventory.AddDungeonItem(ItemSystem.CreateFloorItem(ItemSystem.Id.PitaFruit, ItemSystem.Kind.Fruit, 40));
                TownSaveVariables.SetBool(WreathFruitAvailable, false);
                PersistTownProgress();
                StoryTalk(npc, "(A Pita Fruit is received!)", "(Você recebeu uma Fruta Pita!)", WreathPortrait);
            });
        }

        private void WreathFarewell(NPCController npc) => StoryTalk(npc, "Take care.", "Tome cuidado.", WreathPortrait);

        private enum WreathServicePrompt { Favor, Depart, Repeat }

        private bool WreathServices(NPCController npc, WreathServicePrompt prompt)
        {
            bool repeat = prompt == WreathServicePrompt.Repeat;
            string cancel = prompt switch
            {
                WreathServicePrompt.Repeat => T("No, there's nothing more.", "Não, era só isso."),
                WreathServicePrompt.Depart => T("I'm off.", "Já vou."),
                _ => T("No, it's nothing.", "Não, não é nada.")
            };
            return StoryAsk(npc, repeat ? "Is there anything else?" : "What is it?",
                repeat ? "Mais alguma coisa?" : "O que foi?", WreathPortrait,
                (T("Open the safe for me.", "Abra o cofre para mim."), () =>
                    WreathSequence(npc, () =>
                    {
                        _wreathServiceSpeaker = npc;
                        OpenHomeStorage(fromWreath: true);
                    },
                    ("Oh, you're going to transfer items in and out of it, right?", "Ah, você vai guardar ou retirar itens, certo?"))),
                (T("SAVE DATA", "Salvar jogo"), () =>
                {
                    _wreathServiceSpeaker = npc;
                    OpenSaveSlots(false);
                }),
                (cancel, () =>
                {
                    if (prompt == WreathServicePrompt.Depart) WreathFarewell(npc);
                    else StoryTalk(npc, "Is that so.", "Ah, entendi.", WreathPortrait);
                }));
        }

        private void ReturnToWreathServices()
        {
            var speaker = _wreathServiceSpeaker;
            _wreathServiceSpeaker = null;
            if (speaker && HouseIndex >= 0) WreathServices(speaker, WreathServicePrompt.Repeat);
        }

        private void ResetWeedyBook()
        {
            _weedyReading = false;
        }

        private bool HandleWeedyBookInput()
        {
            if (!_weedyReading) return false;
            if (_cancel?.WasPressedThisFrame() == true) ResetWeedyBook();
            else if (MenuDirectionPressed(3)) SelectWeedyPage(_weedyPage - 1);
            else if (MenuDirectionPressed(4)) SelectWeedyPage(_weedyPage + 1);
            return true;
        }

        private void OpenWeedyBook()
        {
            if (_weedyBook == null)
            {
                var asset = Resources.Load<TextAsset>("Town/WeedyBook/pages");
                if (asset) _weedyBook = JsonUtility.FromJson<MonsterBook>(asset.text);
            }
            if (_weedyBook?.pages == null || _weedyBook.pages.Length == 0)
            { _message = T("Weedy's book is unavailable.", "O livro da Weedy está indisponível."); return; }
            _weedyReading = true;
            SelectWeedyPage(0);
        }

        private void SelectWeedyPage(int index)
        {
            _weedyPage = Mathf.Clamp(index, 0, _weedyBook.pages.Length - 1);
            _weedyDescriptionScroll = Vector2.zero;
        }

        private void DrawWeedyBook()
        {
            if (!_weedyReading) return;
            var page = _weedyBook.pages[_weedyPage];
            var portrait = StreamPortrait("Book/" + page.portrait);
            DrawHudPanel(new Rect(66, 58, 508, 324));
            GUI.Label(new Rect(90, 75, 450, 26), T("Weedy's Monster Book", "Livro dos monstros da Weedy") + $"   {page.number} / {_weedyBook.pages.Length}");
            if (portrait) GUI.DrawTexture(new Rect(98, 121, 156, 156), portrait, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(275, 121, 260, 26), T(page.name, page.namePtBr));
            _weedyDescriptionStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true, alignment = TextAnchor.UpperLeft };
            var description = new GUIContent(T(page.description, page.descriptionPtBr));
            var viewport = new Rect(275, 156, 273, 148);
            float width = viewport.width - 17f;
            float height = _weedyDescriptionStyle.CalcHeight(description, width);
            _weedyDescriptionScroll = GUI.BeginScrollView(viewport, _weedyDescriptionScroll, new Rect(0f, 0f, width, height));
            GUI.Label(new Rect(0f, 0f, width, height), description, _weedyDescriptionStyle);
            GUI.EndScrollView();
            if (GUI.Button(new Rect(100, 319, 125, 32), T("Previous", "Anterior"))) SelectWeedyPage(_weedyPage - 1);
            string back = _cancel?.GetBindingDisplayString() ?? "Q";
            if (GUI.Button(new Rect(250, 319, 140, 32), T("Close", "Fechar") + $" [{back}]")) ResetWeedyBook();
            if (_weedyReading && GUI.Button(new Rect(415, 319, 125, 32), T("Next", "Próxima"))) SelectWeedyPage(_weedyPage + 1);
        }
    }
}
