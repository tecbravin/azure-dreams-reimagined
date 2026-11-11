using UnityEngine;
namespace AzureDreamsReimagined
{
    /// <summary>Unity conversation progress, deliberately separate from unresolved retail quest flags.</summary>
    public sealed class TownDialogueProgress : MonoBehaviour
    {
        internal static string WeedyBookOffer => GameLanguage.Text("Do you want to read my Monster Book?", "Quer ler meu Livro dos Monstros?");
        private NPCController _npc;
        private string _pending;
        private bool _wreathHelperPending;
        private void OnEnable() { TownEventBus.Subscribe(TownEventBus.EventId.NpcTalked, OnTalk); }
        private void OnDisable() { TownEventBus.Unsubscribe(TownEventBus.EventId.NpcTalked, OnTalk); _pending = null; }
        public void Prepare(NPCController npc)
        {
            _npc = npc;
            bool met = TownSaveVariables.GetBool("dialogue." + npc.npcId + ".introduced");
            _pending = "introduced";
            // Native excerpts use NTSC disc offsets. Other lines are adapted for the current quest flow.
            switch (npc.npcId)
            {
                case "wreath":
                    var reputation = TownReputation.Read();
                    int entry = TownSaveVariables.GetInt(TownReputation.WakeEntryKey);
                    bool completedQuestVisit = entry != 0;
                    var context = new TownHomeDialogueSelector.Context
                    {
                        Gold = TownSaveVariables.Gold,
                        Progress = reputation.HighestFloor,
                        HouseQuery = TowerGameController.CurrentHomeRank,
                        EventState = completedQuestVisit ? 2 : 0,
                        SelectionMode = completedQuestVisit ? 2 : 0,
                        DispatchState = completedQuestVisit ? 1 : 0,
                        SelectedEntry = entry,
                        EnabledRecords = TownRetailStoryEventSelector.CountHomeHelpers(reputation),
                        FamiliarA = TownSaveVariables.GetInt("retail.home.familiar_a"),
                        FamiliarB = TownSaveVariables.GetInt("retail.home.familiar_b")
                    };
                    uint address = TownHomeDialogueSelector.Select(context, reputation.IsFlagSet, reputation.ApplyOneTimeFlagOperation);
                    TownSaveVariables.SetString("dialogue.wreath.retail_text_address", address.ToString("X8"));
                    uint helperLine = completedQuestVisit && reputation.HighestFloor > 0
                        ? TownHomeDialogueSelector.HomeHelpersLine(context.HouseQuery, context.EnabledRecords, entry) : 0;
                    _wreathHelperPending = helperLine != 0;
                    npc.dialogueLines = (helperLine != 0 && address != helperLine,
                        TownSaveVariables.GetBool(TowerGameController.WreathFruitAvailable)) switch
                    {
                        (true, true) => new[] { WreathLine(address), WreathLine(helperLine), WreathFruitLine() },
                        (true, false) => new[] { WreathLine(address), WreathLine(helperLine) },
                        (false, true) => new[] { WreathLine(address), WreathFruitLine() },
                        _ => new[] { WreathLine(address) }
                    };
                    break;
                case "weedy": npc.dialogueLines = new[] { met ? GameLanguage.Text("Have you found any new monsters for the book?", "Encontrou algum monstro novo para o livro?") : GameLanguage.Text("Tell me about the monsters you meet in the tower!", "Me conte sobre os monstros que você encontrar na torre!"), WeedyBookOffer }; break;
                case "nico": // TOWN.BIN 0x41953B, 0x419B6C, 0x418DE3
                    npc.dialogueLines = met
                        ? new[] { GameLanguage.Text("If there’s anything that I can do, just let me know.", "Se tiver alguma coisa que eu possa fazer, é só me avisar."),
                            GameLanguage.Text("You’ve got to keep shooting for the step above, today above yesterday, and tomorrow above today!", "Você tem que continuar mirando mais alto, superar ontem hoje e superar hoje amanhã!") }
                        : new[] { GameLanguage.Text("My personality doesn’t allow me to stay still for long.", "Eu não consigo ficar parada por muito tempo."),
                            GameLanguage.Text("There are two types of people, those who make an effort to reach their dreams, and those who sit back and dream about their dreams.",
                                "Há dois tipos de pessoas. As que se esforçam para realizar seus sonhos e as que ficam sentadas só sonhando.") };
                    break;
                // English lines recovered from the NTSC TOWN.BIN. Line breaks follow the Unity dialogue box.
                case "cherrl": // 0x65EB66
                    npc.dialogueLines = new[] { GameLanguage.Text("This doll is Chuppie, and I’m Cherrl.",
                        "Este bichinho de pelúcia é o Chuppie, e eu sou a Cherrl.") };
                    break;
                case "mia": // 0x43D0B1, 0x43D0C7, 0x43D14D, 0x43D1BF
                    npc.dialogueLines = met
                        ? new[] { GameLanguage.Text("We meet a lot, don’t we. You enjoy books too, don’t you?", "A gente se encontra bastante, né? Você também gosta de livros?"),
                            GameLanguage.Text("My favorite books are the novels written by Yuki Katsu.", "Meus livros favoritos são os romances do Yuki Katsu.") }
                        : new[] { GameLanguage.Text("I’m sorry. I just feel uncomfortable whenever a man approaches me like that.",
                            "Desculpa. É que fico sem jeito quando um homem se aproxima de mim assim.") };
                    break;
                case "vivian": // 0x6A1EBA, 0x6A1EEB
                    npc.dialogueLines = new[]
                    {
                        GameLanguage.Text("My name is Vivian Merca.", "Meu nome é Vivian Merca."),
                        GameLanguage.Text("I came to this city recently, and was hired as a dancer by Madame.",
                            "Cheguei à cidade há pouco tempo, e a Madame me contratou como dançarina.")
                    };
                    break;
                case "fur":
                    npc.dialogueLines = new[]
                    {
                        met ? GameLanguage.Text("Welcome back, {name}. What brings you here today?", "Bem-vindo de volta, {name}. O que o traz aqui hoje?")
                            : GameLanguage.Text("Oh, it's you, {name}. Pleasant surprise. What brings you here?",
                                "Ah, é você, {name}. Que surpresa agradável. O que o traz aqui?"),
                        GameLanguage.Text("Are you looking for something for your house?", "Está procurando algo para a sua casa?")
                    };
                    break;
                case "barry": // TOWN.BIN 0x61B2BC, 0x61B388
                    npc.dialogueLines = new[]
                    {
                        GameLanguage.Text("Due to the ancient ruins, we’ve seen an increase in hunters, so as a result we’re always out of items.",
                            "Por causa das ruínas antigas, temos recebido mais caçadores. Por isso, os itens vivem acabando."),
                        GameLanguage.Text("So there’s not much to sell.", "Então não tenho muita coisa para vender.")
                    };
                    break;
                case "master": // 0x604F06
                    npc.dialogueLines = new[] { GameLanguage.Text("Your father, Guy, was super.", "Seu pai, Guy, era incrível."),
                        GameLanguage.Text("He would frequently come to sell top quality monsters.", "Ele vinha sempre vender monstros de primeira.") };
                    break;
                case "tonka": // Carpenter shop, local PS1 recording at 33:32.
                    npc.dialogueLines = new[] { GameLanguage.Text("What is it today?", "O que vai ser hoje?") };
                    break;
                // Original restaurant conversations from the local PS1 recording.
                case "patty":
                    npc.dialogueLines = new[] { GameLanguage.Text("Hello. Can I help you?", "Olá. Posso ajudar?") };
                    break;
                case "coppe":
                    npc.dialogueLines = new[] { GameLanguage.Text("Hi-ya {name}! Making good food comes from the heart!",
                        "Oi, {name}! É preciso cozinhar com o coração para fazer comida boa!") };
                    break;
                case "dar":
                    npc.dialogueLines = new[] { GameLanguage.Text("It's good, it's good, it's great.", "É bom, é bom, é ótimo.") };
                    break;
                case "bet":
                    npc.dialogueLines = new[] { GameLanguage.Text("The food here is the best in Monsbaiya.", "A comida daqui é a melhor de Monsbaiya.") };
                    break;
                case "restaurant_hunter":
                    npc.dialogueLines = new[] { GameLanguage.Text("The food's great and Patty's got a great personality.",
                        "A comida é ótima e a Patty é muito simpática.") };
                    break;
                case "shiela": // 0x62C028
                    npc.dialogueLines = new[] { GameLanguage.Text("Welcome to the Fortune Telling Palace.", "Boas-vindas ao Palácio da Adivinhação."),
                        GameLanguage.Text("I’m Mademoiselle Shiela, known as the mother of Monsbaiya.", "Sou Mademoiselle Shiela, conhecida como a mãe de Monsbaiya.") };
                    break;
                case "hush": // 0x59C8AC, 0x59C916
                    npc.dialogueLines = new[] { GameLanguage.Text("How are you doing? I haven’t seen you in a while.", "Como você está? Faz tempo que não vejo você."),
                        GameLanguage.Text("In your case, germs and illnesses would want to run away from you, so you probably have little concern for a clinic.",
                            "No seu caso, os germes e as doenças devem fugir de você, então é provável que você nem pense muito na clínica.") };
                    break;
                case "jorda": // 0x5B56C0, 0x5B6474
                    npc.dialogueLines = met
                        ? new[] { GameLanguage.Text("Everything I told you, I heard from Guy. Think of it as your father telling you, and don’t forget.",
                            "Tudo o que contei a você eu ouvi do Guy. Pense que foi seu pai quem contou e guarde bem essas palavras.") }
                        : new[] { GameLanguage.Text("When entering the dungeon of the tower, you can find rare items and monster eggs.",
                            "Ao entrar na masmorra da torre, você pode encontrar itens raros e ovos de monstro."),
                            GameLanguage.Text("If you bring these back with you, you can exchange them for money.", "Se trouxer essas coisas de volta, pode trocá-las por dinheiro."),
                            GameLanguage.Text("But the tower is a monster nest. Danger will always follow you.", "Mas a torre é um ninho de monstros. O perigo vai acompanhar você.") };
                    break;
                case "selfi": // 0x2DDD0E
                    npc.dialogueLines = new[] { GameLanguage.Text("Hi there, Puffy lips! I know so much about you.", "Oi, Bocão! Sei tanta coisa sobre você."),
                        GameLanguage.Text("Well... I thought I did...", "Bem... eu achava que sabia...") };
                    break;
                case "ghosh": // 0x2D2395
                    npc.dialogueLines = new[] { GameLanguage.Text("A barbarian wouldn’t understand elegance.", "Um bárbaro jamais entenderia a elegância.") };
                    break;
                case "guy": // 0x6FB874, 0x6FB993
                    npc.dialogueLines = new[] { GameLanguage.Text("Yeah, what’s the matter Nico?", "Sim, o que foi, Nico?"),
                        GameLanguage.Text("A man must take good care of women.", "Um homem deve tratar bem as mulheres.") };
                    break;
                case "youngnico": // 0x6FB8D8, with Koh substituted for the player name token.
                    npc.dialogueLines = new[] { GameLanguage.Text("Sob! {name} is teasing me.", "Snif! O {name} está me provocando.") };
                    break;
                case "introangel": // 0x6F681F
                    npc.dialogueLines = new[] { GameLanguage.Text("Oh, you’re awake. You are about to be reborn.", "Ah, você acordou. Você está prestes a renascer."),
                        GameLanguage.Text("All you can decide on is your name.", "A única coisa que você pode escolher é o seu nome.") };
                    break;
                case "cat": npc.dialogueLines = new[] { GameLanguage.Text("Meow.", "Miau.") }; break;
                case "bird": npc.dialogueLines = new[] { GameLanguage.Text("Chirp!", "Piu!") }; break;
                default:
                    _pending = null;
                    break;
            }
            if (_pending != null)
            {
                npc.dialogue = null;
                npc.loopDialogue = false;
            }
        }
        private static string WreathFruitLine() => GameLanguage.Text("Then take this with you.", "Então leve isto com você.");

        private static string WreathLine(uint address)
        {
            switch (address)
            {
                case 0x80017152: case 0x800172A3: return GameLanguage.Text("Isaac is by the fields. You should go and speak with him.", "Isaac está nos campos. Vá falar com ele.");
                case 0x800173E4: case 0x80017501: return GameLanguage.Text("Wotta from the Woogie Woogie Pool was looking for you.", "O Wotta, da piscina Woogie Woogie, estava procurando por você.");
                case 0x80016359: return GameLanguage.Text("What is it?", "O que foi?");
                case 0x800164CC: return GameLanguage.Text("What? You're buying something for the house?", "O quê? Você vai comprar algo para a casa?");
                case 0x8001688A: return GameLanguage.Text("Good morning. You've been selling things after every adventure. Have you thought about expanding the house?",
                    "Bom dia. Você tem vendido coisas depois de cada aventura. Já pensou em ampliar a casa?");
                case 0x80016BCF: return GameLanguage.Text("Nico is a tremendous help to the house.", "A Nico ajuda muito aqui em casa.");
                case 0x80016C24: return GameLanguage.Text("Fur is a tremendous help to the house.", "A Fur ajuda muito aqui em casa.");
                case 0x80016C77: return GameLanguage.Text("Selfi is a tremendous help to the house.", "A Selfi ajuda muito aqui em casa.");
                case 0x80016CCE: return GameLanguage.Text("Mia is a tremendous help to the house.", "A Mia ajuda muito aqui em casa.");
                case 0x80016D77: return GameLanguage.Text("All these girlfriends! I've got to keep a closer eye on you, {name}.",
                    "Com tantas namoradas, vou ter que ficar de olho em você, {name}.");
                case 0x80016E0C: return GameLanguage.Text("Thanks to you, the house has become a much more comfortable place to live in.",
                    "Graças a você, a casa ficou muito mais confortável.");
                default: return GameLanguage.Text("Are you heading out for the tower already?", "Já está indo para a torre?"); // TOWN.BIN 0x3E502D
            }
        }
        private void OnTalk(object value)
        {
            if (!(value is NPCDialogue.TalkEvent e) || e.Source != gameObject || !e.Completed || _npc == null || _pending == null) return;
            string prefix = "dialogue." + _npc.npcId + ".";
            TownSaveVariables.SetBool(prefix + _pending, true);
            TownSaveVariables.SetInt(prefix + "completed_conversations", TownSaveVariables.GetInt(prefix + "completed_conversations") + 1);
            if (_wreathHelperPending)
            {
                TownSaveVariables.SetInt(TownReputation.WakeEntryKey, 0);
                _wreathHelperPending = false;
            }
            TownSaveVariables.Save(); _pending = null;
        }
    }
}
