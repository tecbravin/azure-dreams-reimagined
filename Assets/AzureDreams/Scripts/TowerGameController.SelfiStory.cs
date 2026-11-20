using UnityEngine;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        // TOWN.BIN 0x2DD020–0x2DEBxx. The retail name control token is
        // normalized to {name} by the existing dialogue formatter.
        private const int SelfiTowerIntroductions = 1;
        private const int SelfiGhoshEncounter = 2;
        private const int SelfiTreasureHunt = 3;
        private const int SelfiOasis = 4;
        private const int SelfiPulunpaRescue = 5;
        private const int SelfiEggReward = 6;
        private const int SelfiNailRumor = 7;
        private const int SelfiPantsRumor = 8;
        private const int SelfiNoseRumor = 9;
        private const int SelfiMainInterest = 10;
        private const int SelfiUBoatRescue = 11;
        private const int SelfiFirstQuestion = 12;
        private const int SelfiSecondQuestion = 13;
        private const int SelfiMisunderstanding = 14;
        private const int SelfiVisitQuestion = 15;
        private const int SelfiFinalGreeting = 16;
        private const int SelfiLoveFlag = 5234;
        private const int SelfiTowerInvitationFlag = 5210; // F_selfy_dungeon_ok
        private const int SelfiGotEggFlag = 5211; // F_selfy_get_tamago
        private const string SelfiEggGrantedKey = StoryPrefix + "selfi.eggGranted";
        private const string SelfiRouteVersionKey = StoryPrefix + "selfi.routeVersion";
        private const int SelfiRouteVersion = 3;
        private GameObject _selfiEggGuest;

        private void EnsureSelfiRouteVersion()
        {
            int routeVersion = TownSaveVariables.GetInt(SelfiRouteVersionKey);
            if (routeVersion >= SelfiRouteVersion) return;
            int stage = StoryStage("selfi");
            // Version 0 contained only the key scenes. Version 2 represented
            // the new U-Boat scene but still folded all of the 2F visits into
            // one. Move existing saves to their nearest unrepeated retail beat.
            stage = routeVersion switch
            {
                <= 0 => stage switch
                {
                    2 => SelfiPulunpaRescue,
                    3 => SelfiEggReward,
                    4 => SelfiNailRumor,
                    5 => SelfiFirstQuestion,
                    6 => SelfiSecondQuestion,
                    _ => stage
                },
                2 => stage switch
                {
                    2 => SelfiPulunpaRescue,
                    3 => SelfiEggReward,
                    4 => SelfiNailRumor,
                    5 => SelfiUBoatRescue,
                    6 => SelfiFirstQuestion,
                    7 => SelfiSecondQuestion,
                    8 => SelfiMisunderstanding,
                    9 => SelfiVisitQuestion,
                    10 => SelfiFinalGreeting,
                    _ => stage
                },
                _ => stage
            };
            TownSaveVariables.SetInt(StoryPrefix + "selfi.stage", stage);
            TownSaveVariables.SetInt(SelfiRouteVersionKey, SelfiRouteVersion);
            PersistTownProgress();
        }

        // Frame durations from the original portrait descriptors, in PS1 frames.
        private const string SelfiPortraitPrefix = "Dialogue/SelfiRetail_variant";
        private const string SelfiSmilePortrait = SelfiPortraitPrefix + "4_frame0";
        private static readonly int[] SelfiBlinkFrames = { 96, 4, 8, 2 };
        private static readonly int[] SelfiSmileBlinkFrames = { 128, 4, 8, 4 };

        private static string SelfiPortrait(int expression) =>
            $"{SelfiPortraitPrefix}{Mathf.Clamp(expression, 0, 5)}_frame0";

        private static int[] SelfiPortraitFrameDurationsFor(string path) =>
            path != null && path.StartsWith(SelfiPortraitPrefix, System.StringComparison.Ordinal)
                ? path == SelfiSmilePortrait ? SelfiSmileBlinkFrames : SelfiBlinkFrames
                : null;

        private Texture2D VisitorDialoguePortrait()
        {
            if (!IsShowingVisitorDialogue || _visitorPortraitPath == null) return null;
            if (_visitorPortraitPath == GhoshIrritatedPortrait)
                return GhoshDialoguePortrait(_visitorPortraitPath, _visitorPortraitStarted);
            var durations = SelfiPortraitFrameDurationsFor(_visitorPortraitPath);
            if (durations == null) return StreamPortrait(_visitorPortraitPath);
            int elapsed = Mathf.FloorToInt((Time.unscaledTime - _visitorPortraitStarted) * 60f);
            int frame = StoryPortraitFrame(durations, elapsed);
            return StreamPortrait(_visitorPortraitPath.Replace("_frame0", "_frame" + frame))
                ?? StreamPortrait(_visitorPortraitPath);
        }

        private bool SelfiTalk(NPCController npc, int expression, string en, string pt,
            System.Action next = null) => StoryTalk(npc, en, pt, SelfiPortrait(expression), next);

        private bool SelfiAsk(NPCController npc, int expression, string en, string pt,
            params (string Label, System.Action Apply)[] choices) =>
            StoryAsk(npc, en, pt, SelfiPortrait(expression), choices);

        private readonly struct SelfiStoryLine
        {
            internal readonly int Expression;
            internal readonly string English;
            internal readonly string Portuguese;

            internal SelfiStoryLine(int expression, string english, string portuguese)
            {
                Expression = expression;
                English = english;
                Portuguese = portuguese;
            }
        }

        private bool SelfiTalkSequence(NPCController npc, System.Action next, params SelfiStoryLine[] lines)
        {
            if (lines == null || lines.Length == 0)
            {
                next?.Invoke();
                return true;
            }
            return SelfiTalkSequenceAt(npc, lines, 0, next);
        }

        private bool SelfiTalkSequenceAt(NPCController npc, SelfiStoryLine[] lines, int index, System.Action next)
        {
            var line = lines[index];
            System.Action continueSequence = index + 1 < lines.Length
                ? () => SelfiTalkSequenceAt(npc, lines, index + 1, next)
                : next;
            return SelfiTalk(npc, line.Expression, line.English, line.Portuguese, continueSequence);
        }

        private bool SelfiTownStory(NPCController npc)
        {
            const string route = "selfi";
            if (_interior || npc != _outdoorSelfi) return false;
            EnsureSelfiRouteVersion();

            int stage = StoryStage(route);
            if (stage == 0)
                return SelfiTalkSequence(npc, () =>
                    {
                        TownRetailScriptFlags.CreatePersistent().SetBit(SelfiTowerInvitationFlag);
                        AdvanceTownStory(route, SelfiTowerIntroductions);
                    },
                    new SelfiStoryLine(0, "Hi there, Puffy lips! I, too,\nstarted a tower adventure for\nmagic training.",
                        "Olá, Bocão! Eu também comecei\numa aventura na Torre para\ntreinar magia."),
                    new SelfiStoryLine(0, "I shall become a monster tamer\ntrue to my legendary family\nname, Rode.",
                        "Serei uma domadora de monstros\ndigna do lendário nome da\nminha família, Rode."));

            if (stage == SelfiEggReward)
                return GiveSelfiEgg(npc);

            if (stage == SelfiFirstQuestion && StoryNewDay(route))
                return AskSelfiFirstQuestion(npc);
            if (stage == SelfiSecondQuestion && StoryNewDay(route))
                return AskSelfiSecondQuestion(npc);
            if (stage == SelfiMisunderstanding && StoryNewDay(route))
                return SelfiTalk(npc, 0, "{name} doesn’t understand my\nfeelings. I wonder why. I feel\nsad. More mystery here.",
                    "{name} não entende meus\nsentimentos. Por quê? Isso me\ndeixa triste. Mais um mistério.",
                    () => AdvanceTownStory(route, SelfiVisitQuestion));
            if (stage == SelfiVisitQuestion && StoryNewDay(route))
                return AskSelfiVisitQuestion(npc);
            if (stage == SelfiFinalGreeting || stage >= StoryComplete)
                return SelfiTalkSequence(npc, stage == SelfiFinalGreeting ? CompleteSelfiStory : null,
                    new SelfiStoryLine(4, "Mr. {name}!", "Sr. {name}!"),
                    new SelfiStoryLine(4, "Mr. {name}. I’m so happy to\nsee you, Mr. {name}.",
                        "Sr. {name}. Estou tão feliz em\nver você, Sr. {name}."));
            return SelfiTownSmallTalk(npc, stage);
        }

        // TOWN.BIN 0x2DD19D–0x2DDFD1. These conversations are available
        // between the tower encounters and do not advance the rescue quest.
        private bool SelfiTownSmallTalk(NPCController npc, int stage) => stage switch
        {
            SelfiTowerIntroductions => SelfiAsk(npc, 0,
                "Hi there, Puffy lips! What a bad day, having a barbarian like you talking to me.",
                "Oi, Bocão! Que dia horrível, ter um bárbaro como você falando comigo.",
                (T("Who’s the barbarian?", "Quem é o bárbaro?"), () =>
                    SelfiTalk(npc, 0, "I heard the rumor from my brother Ghosh.", "Ouvi esse boato do meu irmão Ghosh.")),
                (T("You are so right.", "Você tem toda a razão."), () =>
                    SelfiTalk(npc, 0, "Ha ha ha.\nBe careful from now on.", "Ha ha ha.\nTenha cuidado de agora em diante."))),
            SelfiGhoshEncounter => SelfiTalkSequence(npc, () => SelfiAsk(npc, 0,
                    "You’re jealous of my brother Ghosh, and you’re harassing him again, aren’t you? Enough is enough.",
                    "Você tem inveja do meu irmão Ghosh e está importunando ele de novo, não é? Já chega.",
                    (T("Ghosh being jealous.", "É o Ghosh que tem inveja."), () =>
                        SelfiTalk(npc, 0, "Why would my brother be jealous of you? My brother has everything you don’t have, and you have nothing.",
                            "Por que meu irmão teria inveja de você? Ele tem tudo que você não tem, e você não tem nada.")),
                    (T("Is that right?", "É mesmo?"), () =>
                        SelfiTalk(npc, 0, "Can’t you take responsibility for yourself? I pity you.",
                            "Você não consegue assumir a responsabilidade pelos seus atos? Tenho pena de você."))),
                new SelfiStoryLine(0, "Ah! It’s Puffy lips! {name}.", "Ah! É o Bocão! {name}.")),
            SelfiTreasureHunt => SelfiTalkSequence(npc, null,
                new SelfiStoryLine(0, "Hi there, Puffy lips! I heard from my brother Ghosh. You seem to have nothing but",
                    "Oi, Bocão! Ouvi dizer pelo meu irmão Ghosh. Parece que você só coleciona"),
                new SelfiStoryLine(0, "failure in the tower as well. Human life is so fragile, you know. Take good care of it.",
                    "fracassos na torre também. A vida humana é tão frágil, sabia? Cuide bem dela."),
                new SelfiStoryLine(0, "Ha ha ha.", "Ha ha ha.")),
            SelfiOasis => SelfiTalkSequence(npc, () => SelfiAsk(npc, 0,
                    "But you and I are no match.", "Mas você e eu não combinamos.",
                    (T("Selfi sure looks heavy.", "A Selfi parece pesada."), () => SelfiRudeReply(npc)),
                    (T("I don’t want to be a match.", "Nem quero combinar com você."), () => SelfiRudeReply(npc))),
                new SelfiStoryLine(0, "Hi there, Puffy lips!\nI understand you wanted to talk to me.",
                    "Oi, Bocão!\nSoube que você queria falar comigo.")),
            SelfiPulunpaRescue when TownRetailScriptFlags.CreatePersistent().IsFlagSet(SelfiLeftInTowerFlag) =>
                SelfiTalkSequence(npc, null,
                    new SelfiStoryLine(0, "You are such a bastard!", "Você é um desgraçado!"),
                    new SelfiStoryLine(0, "You’re the worst!", "Você é o pior!")),
            SelfiPulunpaRescue => SelfiAsk(npc, 0,
                "Hi there, Puffy lips! What is the relationship between you and my brother?",
                "Oi, Bocão! Que tipo de relação você tem com o meu irmão?",
                (T("Friends.", "Somos amigos."), () =>
                    SelfiTalk(npc, 0, "You guys are close friends despite how it looks. All my brother talks about is {name}.",
                        "Vocês são grandes amigos, apesar das aparências. Meu irmão só fala do {name}.")),
                (T("Not good friends.", "Não somos bons amigos."), () =>
                    SelfiTalk(npc, 0, "Well... But all my brother talks about is {name}.",
                        "Bem... Mas meu irmão só fala do {name}."))),
            SelfiNailRumor when !StoryNewDay("selfi") =>
                SelfiTalk(npc, 0, "Hi there, Puffy lips.\n.......", "Oi, Bocão.\n......."),
            SelfiNailRumor => SelfiTalkSequence(npc, null,
                new SelfiStoryLine(0, "Hi there, Puffy lips! All my brother talks about at home is {name}.",
                    "Oi, Bocão! Lá em casa, meu irmão só fala do {name}."),
                new SelfiStoryLine(0, "I know so much about you.", "Sei tanta coisa sobre você.")),
            SelfiPantsRumor => AskSelfiTownRumor(npc, "I heard you wear frilly pink panties.",
                "Ouvi dizer que você usa calcinha rosa com babados."),
            SelfiNoseRumor => AskSelfiTownRumor(npc, "I heard your brain weighs as much as frog’s.",
                "Ouvi dizer que seu cérebro pesa tanto quanto o de um sapo."),
            SelfiMainInterest => AskSelfiTownRumor(npc, "I heard you’re really 30 years old.",
                "Ouvi dizer que na verdade você tem 30 anos."),
            SelfiUBoatRescue => AskSelfiTownRumor(npc, "I heard you have scales on your back.",
                "Ouvi dizer que você tem escamas nas costas."),
            SelfiFirstQuestion => SelfiTalk(npc, 0,
                "Hi there, Puffy lips!\nI thought there is still a lot that I don’t know yet.",
                "Oi, Bocão!\nPensei que ainda há muita coisa que eu não sei.", () => AskSelfiAboutMystery(npc)),
            _ => SelfiTalkSequence(npc, null,
                new SelfiStoryLine(0, "Hi there, Puffy lips!", "Oi, Bocão!"),
                new SelfiStoryLine(0, "I know so much about you.\nWell... I thought I did...",
                    "Sei tanta coisa sobre você.\nBem... eu achava que sabia..."))
        };

        private void SelfiRudeReply(NPCController npc) => SelfiTalk(npc, 0, "How rude.", "Que grosseria.");

        private void AskSelfiAboutMystery(NPCController npc)
        {
            void Reply(string en, string pt) => SelfiTalk(npc, 0, en, pt, () => SelfiTalk(npc, 0,
                "You are the most interesting person for me right now.", "Você é a pessoa mais interessante para mim agora."));
            SelfiAsk(npc, 0, "Listen! I will find out about your mystery myself.",
                "Escute! Vou desvendar seu mistério por conta própria.",
                (T("I don’t have any mystery.", "Não tenho mistério nenhum."), () =>
                    Reply("I’ve nothing to hide, you know.", "Eu não tenho nada a esconder, sabia?")),
                (T("That’s impossible.", "Isso é impossível."), () =>
                    Reply("I don’t care if I don’t find out.", "Não me importo se não descobrir.")));
        }

        private bool AskSelfiTownRumor(NPCController npc, string en, string pt)
        {
            void Reply() => SelfiTalk(npc, 0, "{name} is such a mystery.", "{name} é um verdadeiro mistério.");
            return SelfiAsk(npc, 0, en, pt,
                (T("No, that’s not true!", "Não, isso não é verdade!"), Reply),
                (T("Yes, that’s right.", "Sim, é verdade."), Reply));
        }

        private bool GiveSelfiEgg(NPCController npc)
        {
            if (TownSaveVariables.GetBool(SelfiEggGrantedKey))
                return FinishSelfiEggGift(npc);
            if (_townInventory.TotalItems + FamiliarInventoryCount >= Inventory.SlotCapacity)
                return SelfiTalk(npc, 0, "Please make room in your bag first.",
                    "Libere espaço na bolsa primeiro.");

            // TOWN.BIN 0x2D3868–0x2D3A94.
            void OfferEgg(string en, string pt) => SelfiTalk(npc, 0, en, pt, () =>
            {
                _townInventory.AddDungeonItem(MonsterEgg.Create((byte)MonsterSpecies.PULUNPA, true));
                TownSaveVariables.SetBool(SelfiEggGrantedKey, true);
                TownRetailScriptFlags.CreatePersistent().SetBit(SelfiGotEggFlag);
                PersistTownProgress();
                StoryTalk(npc, "(You got a monster egg.)", "(Você recebeu um ovo de monstro.)",
                    () => FinishSelfiEggGift(npc));
                _dialogueSpeaker = HeroName;
                _dialoguePortrait = null;
            });
            return SelfiAsk(npc, 0,
                "Hi there, Puffy lips!\nYou wouldn’t dare ask a favor of me because you rescued me.",
                "Oi, Bocão!\nVocê não ousaria me pedir um favor só porque me resgatou.",
                (T("Every life is valuable.", "Toda vida é valiosa."), () => OfferEgg("F... fine.", "T... tudo bem.")),
                (T("Be grateful.", "Agradeça."), () =>
                    SelfiTalk(npc, 0, "Huh, you are such a bastard after all.", "Hã, você é um desgraçado mesmo.",
                        () => OfferEgg("F... fine.", "T... tudo bem."))));
        }

        private bool FinishSelfiEggGift(NPCController npc) =>
            SelfiTalk(npc, 0, "This is for saving me in the tower.\nNow we are even.",
                "Isto é por ter me salvado na torre.\nAgora estamos quites.",
                () => SelfiEggGhoshVisit(npc));

        private void SelfiEggGhoshVisit(NPCController selfi)
        {
            var ghosh = FindStoryActor("ghosh");
            var prefab = FindStoryResidentPrefab("ghosh");
            if (!ghosh && prefab)
            {
                var position = selfi.transform.position;
                var near = new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z));
                foreach (var step in VisitorSteps)
                {
                    var cell = near + step;
                    if (!StoryResidentCellOpen(cell)) continue;
                    _selfiEggGuest = Spawn(prefab, cell, "Selfi egg event - Ghosh");
                    _selfiEggGuest.transform.position = RetailTownActorPosition(CellPosition(cell), 0f);
                    ghosh = _selfiEggGuest.GetComponent<NPCController>();
                    break;
                }
            }
            if (!ghosh)
            {
                FinishSelfiEggVisit();
                return;
            }

            // TOWN.BIN 0x2D3B6E: telling Ghosh directly skips Selfi's explanation.
            void GhoshReact() => GhoshTalk(ghosh, "The egg Selfi cherished so much?!", "O ovo que a Selfi tanto estimava?!",
                () => GhoshIrritatedTalk(ghosh,
                    "Grr... Come back in a million years if you want to seduce my sister.",
                    "Grr... Volte daqui a um milhão de anos se quiser conquistar a minha irmã.",
                    () => GhoshIrritatedTalk(ghosh, "Selfi, get away from that dirty thing!", "Selfi, afaste-se dessa coisa imunda!",
                        () => SelfiTalk(selfi, 0, "Yes, Brother.", "Sim, irmão.",
                            () => GhoshIrritatedTalk(ghosh, "We’re going home.", "Vamos para casa.", FinishSelfiEggVisit)))));
            StoryAsk(ghosh, "{name}! What are you doing?", "{name}! O que você está fazendo?", GhoshPortrait,
                (T("I received an egg.", "Recebi um ovo."), GhoshReact),
                (T("It’s a secret.", "É segredo."), () =>
                    SelfiTalk(selfi, 0, "I just gave him an egg.", "Acabei de dar um ovo a ele.", GhoshReact)));
        }

        private void FinishSelfiEggVisit()
        {
            ClearSelfiEggGuest();
            AdvanceTownStory("selfi", SelfiNailRumor);
        }

        private void ClearSelfiEggGuest()
        {
            if (!_selfiEggGuest) return;
            RemoveGenerated(_selfiEggGuest);
            _selfiEggGuest = null;
        }

        private bool SelfiQuestionAboutRescue(NPCController npc, params (string Label, System.Action Apply)[] choices) =>
            SelfiTalkSequence(npc, () => SelfiAsk(npc, 0, "Why is that, I wonder?", "Por que será?", choices),
                new SelfiStoryLine(0, "Puffy lips..... I don’t understand. Why did I jeopardize my own life to",
                    "Bocão..... Eu não entendo. Por que arrisquei minha própria vida para"),
                new SelfiStoryLine(0, "rescue someone like {name}?\nIsn’t this the biggest mystery of the century?",
                    "resgatar alguém como {name}?\nNão é o maior mistério do século?"));

        private bool AskSelfiFirstQuestion(NPCController npc)
        {
            return SelfiQuestionAboutRescue(npc,
                (T("You love me. Don't you?", "Você me ama. Não ama?"), () =>
                    SelfiTalkSequence(npc, () => AdvanceTownStory("selfi", SelfiSecondQuestion),
                        new SelfiStoryLine(0, "Ha ha ha ha. You must be\nkidding. Who me?\nHa ha ha ha.",
                            "Ha ha ha ha. Você só pode\nestar brincando. Eu?\nHa ha ha ha."),
                        new SelfiStoryLine(0, "I knew I shouldn’t have asked\nyou.",
                            "Eu sabia que não devia ter\nperguntado."))),
                (T("I have no idea.", "Não faço ideia."), () =>
                    SelfiTalkSequence(npc, () => AdvanceTownStory("selfi", SelfiSecondQuestion),
                        new SelfiStoryLine(0, "That’s right. If I don’t\nunderstand, how could anyone\nlike {name} understand?",
                            "É isso. Se eu não entendo,\ncomo alguém como {name}\npoderia entender?"),
                        new SelfiStoryLine(0, "I knew I shouldn’t have asked\nyou.",
                            "Eu sabia que não devia ter\nperguntado."))));
        }

        private bool AskSelfiSecondQuestion(NPCController npc)
        {
            return SelfiQuestionAboutRescue(npc,
                (T("You love me. Don't you?", "Você me ama. Não ama?"), () =>
                    SelfiTalkSequence(npc, () => AskSelfiConfessionAnswer(npc),
                        new SelfiStoryLine(0, "I fell in love with {name}?\nI love {name}?!\nI love {name}!",
                            "Eu me apaixonei por {name}?\nEu amo {name}?!\nEu amo {name}!"),
                        new SelfiStoryLine(0, "I must be in love with {name}!\nAhhhhhh! Mr. {name}!",
                            "Eu devo estar apaixonada por {name}!\nAhhhhhh! Sr. {name}!"),
                        new SelfiStoryLine(0, "It’s natural to be surprised.\nI’m a cute woman who is noble,\nsmart and beautiful.",
                            "É natural se surpreender. Sou\numa mulher fofa, nobre,\ninteligente e bonita."),
                        new SelfiStoryLine(0, "And this woman is attracted to\nyou. No, you don’t have to\nsay anything, Mr. {name}.",
                            "E essa mulher sente atração por\nvocê. Não, não precisa dizer\nnada, Sr. {name}."))),
                (T("I have no idea.", "Não faço ideia."), () =>
                    SelfiTalkSequence(npc, () => AdvanceTownStory("selfi", SelfiMisunderstanding),
                        new SelfiStoryLine(0, "That’s right. If I don’t\nunderstand, how could anyone\nlike {name} understand?",
                            "É isso. Se eu não entendo,\ncomo alguém como {name}\npoderia entender?"),
                        new SelfiStoryLine(0, "I knew I shouldn’t have asked\nyou.",
                            "Eu sabia que não devia ter\nperguntado."))));
        }

        private bool AskSelfiConfessionAnswer(NPCController npc) =>
            SelfiAsk(npc, 0, "Mr. {name} rescued me.\nDoes it mean Mr. {name} loves\nme too!?",
                "O Sr. {name} me salvou. Isso\nsignifica que o Sr. {name}\ntambém me ama!?",
                (T("That's not true.", "Não é verdade."), () => FinishSelfiConfession(npc)),
                (T("That's right.", "É verdade."), () => FinishSelfiConfession(npc)));

        private void FinishSelfiConfession(NPCController npc)
        {
            SelfiTalkSequence(npc, CompleteSelfiStory,
                new SelfiStoryLine(0, "No, no, you don’t have to say\nanything, I understand\neverything.",
                    "Não, não precisa dizer nada.\nEu entendo tudo."),
                new SelfiStoryLine(0, "And... which means... oh my,\nMr. {name}...\nI’m so embarrassed.",
                    "E... isso quer dizer... ai meu,\nSr. {name}...\nEstou tão envergonhada."));
        }

        private bool AskSelfiVisitQuestion(NPCController npc) =>
            SelfiAsk(npc, 4, "Mr. {name}. You came to see\nme, right?",
                "Sr. {name}. Você veio me ver,\nnão foi?",
                (T("That's not true.", "Não é verdade."), () =>
                    SelfiTalkSequence(npc, null,
                        new SelfiStoryLine(4, "It’s a lie. It’s a lie. It’s a lie. A lie. A lie.\nHoly cow!",
                            "É mentira. É mentira. É mentira. Mentira. Mentira.\nMeu Deus!"),
                        new SelfiStoryLine(4, "You came to see Selfi, right?", "Você veio ver a Selfi, não veio?"))),
                (T("That's right.", "É verdade."), () =>
                    SelfiTalk(npc, 4, "I’m so happy.", "Estou tão feliz.",
                        () => AdvanceTownStory("selfi", SelfiFinalGreeting))));

        private void SelfiWakeUpStory(NPCController npc, bool survived)
        {
            if (!survived) // TOWN.BIN 0x41BD0D.
            {
                SelfiTalkSequence(npc, null,
                    new SelfiStoryLine(0, "Oh yes, yes, yes!\nMr. {name}.\nYou have awaken!",
                        "Ah, sim, sim, sim!\nSr. {name}.\nVocê acordou!"),
                    new SelfiStoryLine(0, "Oh! This is so moving.\nThe sight of you slowly opening your eyes in the morning sunlight!",
                        "Ah! Que comovente. Ver você abrir os olhos devagar à luz da manhã!"),
                    new SelfiStoryLine(0, "You have returned from hell to once again spend some time with me.",
                        "Você voltou do inferno para passar mais um tempo comigo."),
                    new SelfiStoryLine(0, "Oh Mr. {name}, when the tamed monster brought you home all scarred and wounded,",
                        "Ah, Sr. {name}, quando o familiar trouxe você para casa todo machucado,"),
                    new SelfiStoryLine(0, "I fainted on the spot.\nBut I’m truly glad that I stayed by your side.",
                        "Eu desmaiei na hora.\nMas estou muito feliz por ter ficado ao seu lado."),
                    new SelfiStoryLine(0, "This is great!\nI couldn’t see how I could live on without you, Mr. {name}.",
                        "Que maravilha!\nEu não sabia como poderia viver sem você, Sr. {name}."),
                    new SelfiStoryLine(0, "I think I know a little about the dungeon, enough to ask you to please not overdo it.",
                        "Acho que conheço um pouco da masmorra, o suficiente para pedir que, por favor, não exagere."),
                    new SelfiStoryLine(0, "Please do not overdo it.", "Por favor, não exagere."));
                return;
            }

            // TOWN.BIN 0x41649B–0x41699C, the bedroom and farewell scenes.
            void AskAboutLeaving() => SelfiAsk(npc, 0, "Are you on your way out?", "Já está de saída?",
                (T("I’m going.", "Já vou."), () => SelfiTalk(npc, 0, "Take care, see you later.\nMr. {name}.",
                    "Cuide-se. Até mais,\nSr. {name}.")),
                (T("I’m not going yet.", "Ainda não vou sair."), () => SelfiTalkSequence(npc, null,
                    new SelfiStoryLine(0, "Oh my! What are you doing!? You’re embarrassing me!",
                        "Minha nossa! O que está fazendo!? Você está me deixando sem graça!"),
                    new SelfiStoryLine(0, "Well then, watch my beautiful body as you wish, get down on your knees,",
                        "Pois bem, contemple minha beleza à vontade, ajoelhe-se,"),
                    new SelfiStoryLine(0, "and when I put my foot forward.", "e quando eu puser o pé à frente."),
                    new SelfiStoryLine(0, "Oh! I was being my imaginary self, that was not the real me.",
                        "Ah! Eu estava fantasiando, aquela não era eu de verdade."),
                    new SelfiStoryLine(0, "Good Luck!", "Boa sorte!"))));

            SelfiAsk(npc, 0, "Good morning, Mr. {name}.\nDo you still feel sleepy?",
                "Bom dia, Sr. {name}.\nAinda está com sono?",
                (T("I’m sleepy.", "Estou com sono."), () => SelfiTalkSequence(npc, AskAboutLeaving,
                    new SelfiStoryLine(0, "You must still be quite exhausted. Let me do something about it.",
                        "Você ainda deve estar exausto. Deixe-me fazer algo a respeito."),
                    new SelfiStoryLine(0, "Sweet dreams...", "Bons sonhos..."),
                    new SelfiStoryLine(0, "I’ll be waiting in the living room.", "Vou esperar na sala."))),
                (T("No, I’m not sleepy", "Não, não estou com sono."), AskAboutLeaving));
        }

        private bool SelfiPoolStory(NPCController npc) => SelfiTalkSequence(npc, null, // TOWN.BIN 0x535BC9.
            new SelfiStoryLine(0, "Oh my, this is wonderful that I’m able to meet you like this, Mr. {name}.",
                "Minha nossa, que maravilha encontrar você assim, Sr. {name}."),
            new SelfiStoryLine(0, "Personally, I’m more relaxed in my own pool at home.",
                "Pessoalmente, fico mais à vontade na piscina lá de casa."),
            new SelfiStoryLine(0, "I’m glad I came. I thought there might be a possibility of seeing you if I came.",
                "Ainda bem que vim. Achei que, se viesse, talvez encontrasse você."),
            new SelfiStoryLine(0, "I’m sure this is a sign that we are to be together.\nYes, I’m certain.",
                "Tenho certeza de que é um sinal de que devemos ficar juntos.\nSim, tenho certeza."));

        private void CompleteSelfiStory()
        {
            TownRetailScriptFlags.CreatePersistent().SetBit(SelfiLoveFlag);
            TownSaveVariables.SetBool("quest.selfi.complete", true);
            AdvanceTownStory("selfi", StoryComplete);
        }
    }
}
