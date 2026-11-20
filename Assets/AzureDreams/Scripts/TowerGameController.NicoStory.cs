using System;
using System.Collections;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string NicoDonationTotalKey = StoryPrefix + "nico.donation_total";
        private const string NicoLibrarySeenKey = StoryPrefix + "nico.library_seen";
        private const string NicoTowerReportSeenKey = StoryPrefix + "nico.tower_report_seen";
        private const int NicoFountainGoal = 3000;
        private const int NicoLegacyDonationTotal = 2001;
        private const int NicoCheroEndedFlag = 5213; // F_sero_end
        private const int NicoCheroMorningFlag = 5205; // F_niko_selro_asa
        private const int NicoAfterBeldoVisionFlag = 5001; // F_veldil_dungeon_deatta
        private const string NicoPortraitPrefix = "Dialogue/NicoRetail_variant";
        private const int NicoNeutral = 0;
        private const int NicoFlustered = 1;
        private const int NicoCollecting = 2;
        private const int NicoCounting = 3;
        private const int NicoWorried = 4;
        private const int NicoPlayful = 5;
        private const int NicoSwimsuit = 6;
        private const int NicoChild = 7;
        private static readonly int[] NicoBlinkFrames = { 96, 4, 8, 2 };
        private static readonly int[] NicoCountingFrames = { 2, 2, 2, 2 };
        private static readonly int[] NicoChildFrames = { 64, 4, 8, 4 };

        private static string NicoPortrait(int variant) => $"{NicoPortraitPrefix}{variant}_frame0";

        private static int[] NicoPortraitFrameDurationsFor(string path)
        {
            if (path == null || !path.StartsWith(NicoPortraitPrefix, StringComparison.Ordinal) ||
                path.Length <= NicoPortraitPrefix.Length) return null;
            return (path[NicoPortraitPrefix.Length] - '0') switch
            {
                NicoCounting => NicoCountingFrames,
                NicoChild => NicoChildFrames,
                >= NicoNeutral and <= NicoChild => NicoBlinkFrames,
                _ => null
            };
        }

        private Texture2D PrologueNicoPortrait(int variant)
        {
            string portrait = NicoPortrait(variant);
            int ticks = Mathf.FloorToInt((Time.unscaledTime - _prologueStarted) * 60f);
            int frame = StoryPortraitFrame(NicoPortraitFrameDurationsFor(portrait), ticks);
            return StreamPortrait(portrait.Replace("_frame0", "_frame" + frame)) ?? StreamPortrait(portrait);
        }

        private bool NicoPages(NPCController npc, int expression, Action done,
            params (string English, string Portuguese)[] lines)
        {
            bool ShowPage(int index) => NicoTalk(npc, expression, lines[index].English, lines[index].Portuguese,
                index + 1 < lines.Length ? () => ShowPage(index + 1) : done);
            return ShowPage(0);
        }

        private bool NicoStory(NPCController npc)
        {
            const string route = "nico";
            int stage = StoryStage(route);
            if (npc.npcId == "nico_dad" && stage == 0 && StoryRoom == NicoHouse)
                return StoryTalk(npc, "Well hello!  {name}!", "Ora, olá, {name}!", () =>
                {
                    var nico = FindStoryActor("nico");
                    if (nico) StartCoroutine(PlayNicoCultureIntroduction(nico));
                });

            if (npc.npcId == "nico_library" && StoryRoom == LibraryRoom) return NicoLibraryConversation(npc);
            if (npc.npcId == "nico_theater" && StoryRoom == TheaterRoom)
                return NicoPages(npc, NicoNeutral, null,
                    ("I hear that {name} made this theater.", "Ouvi dizer que foi o {name} quem construiu este teatro."),
                    ("Yup! It certainly has the smell of culture to it.", "Sim! Isto aqui tem mesmo cheiro de cultura."),
                    ("I truly hope this will bring more comfort to the people of this city.",
                        "Espero de verdade que isto traga mais conforto para o pessoal da cidade."),
                    ("Man, {name} really is a cultural kind of guy.", "O {name} é mesmo um sujeito culto."));

            if (StoryRoom.Length == 0)
            {
                if (npc.npcId == "nico" && (stage is NicoFirstDonation or NicoSecondDonation))
                    return StoryNewDay(route) ? NicoDonationOffer(npc, stage) : NicoDonationFollowUp(npc);
                if (npc.npcId == "nico" && stage == NicoFountainTalk)
                    return StoryNewDay(route) && StoryBuilding("fountain")
                        ? NicoFountainCelebration(npc)
                        : NicoTalk(npc, NicoNeutral, "You should be a little more cultured.",
                            "Você devia ser um pouco mais culto.");
                if (npc.npcId == "chero" && stage == NicoCheroIntroduction && StoryNewDay(route))
                    return StoryTalk(npc, "I’m a travelling cellist. Please listen to my song.",
                        "Sou um violoncelista viajante. Por favor, ouça minha música.", () =>
                        {
                            AdvanceTownStory(route, NicoGhoshConfrontation);
                            SpawnNicoFountainCast();
                            var ghosh = FindStoryActor("nico_ghosh");
                            if (ghosh) NicoGhoshFountainConversation(ghosh);
                        });
                if (stage == NicoGhoshConfrontation && (npc.npcId is "nico_ghosh" or "nico_fountain" or "chero"))
                {
                    var ghosh = FindStoryActor("nico_ghosh");
                    return ghosh && NicoGhoshFountainConversation(ghosh);
                }
                if (npc.npcId == "chero" && stage == NicoCheroOpinion)
                {
                    SpawnNicoFountainCast();
                    var nico = FindStoryActor("nico_fountain");
                    return nico && NicoAfterCheroPerformance(npc, nico);
                }
                if (npc.npcId == "chero" && stage == StoryComplete)
                    return CheroConcertOffer(npc);
                if (npc.npcId == "nico_door_ghosh" && stage == NicoHouseGuard)
                    return GhoshAtNicoDoor(npc);
            }

            if (npc.npcId != "nico") return false;
            if (StoryRoom != NicoHouse)
                return NicoTalk(npc, NicoNeutral, "Hey, {name}. Didn’t realize you were here.",
                    "Ei, {name}. Não tinha percebido que você estava aqui.");
            if (stage == NicoAfterSelfiEgg && StoryDay >= 7 && StoryNewDay(route) &&
                (TownSaveVariables.GetBool(SelfiEggGrantedKey) ||
                 TownRetailScriptFlags.CreatePersistent().IsFlagSet(SelfiGotEggFlag)))
                return NicoSelfiEggConversation(npc, () => AdvanceTownStory(route, NicoCultureTalkFirst));
            if (stage is >= NicoCultureTalkFirst and <= NicoCultureTalkLast && StoryNewDay(route))
                return NicoCultureTalk(npc, stage, () => AdvanceTownStory(route, stage + 1));
            if ((stage is NicoHouseTalkFirst or NicoHouseTalkSecond) && StoryNewDay(route))
                return NicoLateHouseTalk(npc, stage, () => AdvanceTownStory(route, stage + 1));
            if (stage == NicoFinalArrival && StoryNewDay(route)) return NicoFinalHouseConversation(npc);
            return NicoHouseSmallTalk(npc, stage);
        }

        // TOWN.BIN 0x66880C–0x6694D0. The route begins in Nico's house after Dar greets Koh.
        private IEnumerator PlayNicoCultureIntroduction(NPCController nico)
        {
            nico.gameObject.SetActive(true);
            Vector3 origin = nico.transform.position;
            nico.transform.position = origin - Vector3.forward * 0.85f;
            yield return PlayStoryWalk(nico, origin, 0.45f);
            if (!nico || StoryRoom != NicoHouse) yield break;
            NicoPages(nico, NicoNeutral, () => NicoAsk(nico, NicoNeutral,
                    "Those elegant people... they would sometimes have festivals... or concerts...",
                    "Aquelas pessoas elegantes... às vezes faziam festivais... ou concertos...",
                    (T("So you wanna go there?", "Então você quer ir para lá?"), () => NicoPages(nico, NicoNeutral,
                        () => NicoTownCultureChoice(nico),
                        ("Not necessarily.", "Não necessariamente."),
                        ("I mean, yes I want to go. But my home town is here, I want to make this town better.",
                            "Quer dizer, eu quero ir, sim. Mas a minha cidade é aqui. Quero melhorar este lugar."))),
                    (T("I’m not interested.", "Não me interessa."), () => NicoTalk(nico, NicoWorried,
                        "{name}! You can be so insensitive! You could learn a thing or two from Ghosh.",
                        "{name}! Como você é insensível! Podia aprender uma coisa ou duas com o Ghosh.",
                        () => NicoTownCultureChoice(nico)))),
                ("Hey, {name}.  Didn’t realize you were here.", "Ei, {name}. Não tinha percebido que você estava aqui."),
                ("This city is full of dust, it makes me sick.", "Esta cidade é cheia de poeira. Não aguento mais."),
                ("It hasn’t got one bit of culture attached to it.", "Aqui não tem um pingo de cultura."),
                ("I still remember your dad telling us all kinds of stories when we were wee kids.",
                    "Ainda lembro do seu pai contando todo tipo de história quando éramos pequenos."),
                ("When he used to tell us about his adventures to get to this city Monsbaiya, the city by the tower.",
                    "Ele contava as aventuras da viagem até Monsbaiya, a cidade ao pé da torre."),
                ("More than any of the stories of his courage, I enjoyed his story about a city he passed along the way.",
                    "Mais do que as histórias sobre a coragem dele, eu gostava da história de uma cidade por onde ele passou."),
                ("In the city would be a park where the people would come to rest.",
                    "Nessa cidade, havia um parque onde as pessoas iam descansar."),
                ("In the park would be a fountain, always spurting clear water into the air.",
                    "No parque, havia uma fonte que vivia jorrando água cristalina."));
        }

        private void NicoTownCultureChoice(NPCController nico)
        {
            void Explain()
            {
                NicoPages(nico, NicoNeutral, () => NicoAsk(nico, NicoNeutral,
                        "You should be more cultural too, {name}.", "Você também devia ser mais culto, {name}.",
                        (T("You should be more feminine.", "Você devia ser mais feminina."), () => NicoPages(nico, NicoFlustered,
                            () => AdvanceTownStory("nico", NicoFirstDonation),
                            ("Whatever. You know you’re the only one that doesn’t see me as a woman!",
                                "Tanto faz. Você é o único que não me vê como mulher!"),
                            ("All the other guys tell me I’m cute.", "Todos os outros rapazes dizem que sou bonita."))),
                        (T("I’m plenty cultural as it is.", "Já sou culto o bastante."), () => NicoTalk(nico, NicoPlayful,
                            "If you consider yourself plenty cultural, {name}, then I would be considered plenty feminine.",
                            "Se você se considera culto o bastante, {name}, então eu sou feminina o bastante.",
                            () => AdvanceTownStory("nico", NicoFirstDonation)))),
                    ("People here have trouble just getting by on their day to day lives, either emotionally or financially.",
                        "O pessoal daqui mal consegue dar conta do dia a dia, seja por problemas pessoais ou por falta de dinheiro."),
                    ("That’s why I sometimes volunteer in fund raising.", "Por isso às vezes ajudo a arrecadar doações."),
                    ("If each of us could contribute even just a little, we could make this city that much more cultural.",
                        "Se cada um ajudasse um pouquinho, poderíamos tornar esta cidade muito mais culta."));
            }

            NicoAsk(nico, NicoNeutral, "Wouldn’t you like to see this city become a better place, {name}?",
                "Você não gostaria que esta cidade se tornasse um lugar melhor, {name}?",
                (T("Well, of course.", "Claro que sim."), Explain),
                (T("Doesn’t matter.", "Tanto faz."), () => NicoTalk(nico, NicoNeutral,
                    "Right!? But at the same time, the people of this city don’t have the luxury to be as elegant.",
                    "Pois é! Só que o pessoal desta cidade não pode se dar ao luxo de ser tão elegante.", Explain)));
        }

        // TOWN.BIN 0x2CF804 selects the collection-box portrait before the donation menu.
        private bool NicoDonationOffer(NPCController nico, int stage) =>
            NicoPages(nico, NicoCollecting, () => NicoAsk(nico, NicoCollecting, "Are you going to donate?",
                    "Você vai fazer uma doação?",
                    (T("Sure, I will.", "Claro que sim."), () => NicoChooseDonation(nico, stage)),
                    (T("No way.", "De jeito nenhum."), () => NicoTalk(nico, NicoCollecting,
                        "Well... Nobody’s donating.", "Bem... Ninguém está doando."))),
                ("A fountain for the central square. Please help us with your donation!",
                    "Uma fonte para a praça central! Por favor, ajude com uma doação!"),
                ("Hey, {name}!", "Ei, {name}!"));

        private void NicoChooseDonation(NPCController nico, int stage)
        {
            string request = stage == NicoFirstDonation ? "Yeah, we’ve done it. {name} is the first donor." : "Are you going to donate?";
            string localized = stage == NicoFirstDonation ? "Conseguimos! {name} é o primeiro doador." : "Você vai fazer uma doação?";
            NicoAsk(nico, NicoCollecting, request, localized,
                (T("0 G...", "0 G..."), () => DonateToNicoFountain(nico, stage, 0)),
                (T("1 G...", "1 G..."), () => DonateToNicoFountain(nico, stage, 1)),
                ("100 G", () => DonateToNicoFountain(nico, stage, 100)),
                (T("1,000 G", "1.000 G"), () => DonateToNicoFountain(nico, stage, 1000)));
        }

        private void DonateToNicoFountain(NPCController nico, int stage, int amount)
        {
            if (amount == 0)
            {
                NicoTalk(nico, NicoCollecting, "....... It was stupid of me that I expected you to do better.",
                    "....... Fui boba de esperar mais de você.");
                return;
            }
            if (_gold < amount)
            {
                _message = T("Not enough gold.", "Ouro insuficiente.");
                return;
            }

            _gold -= amount;
            if (RecordNicoFountainDonation(stage, amount))
            {
                // The fountain goes up while Koh is away, like Tonka's orders (ApplyPendingConstruction).
                AdvanceTownStory("nico", NicoFountainTalk);
            }
            else AdvanceTownStory("nico", NicoSecondDonation);

            NicoTalk(nico, NicoCollecting, "Thank you, {name}!", "Obrigada, {name}!", () =>
                GhoshMatchesFountainDonation(nico, amount, () => NicoAsk(nico, NicoCollecting,
                    "Ghosh seems to be so cultured.", "O Ghosh parece tão culto.",
                    (T("That confused guy?", "Aquele sujeito confuso?"), () => NicoTalk(nico, NicoCollecting,
                        "A barbarian like {name} wouldn’t understand it.", "Um bárbaro como você não entenderia, {name}.")),
                    (T("I think he’s just stingy.", "Acho que ele só é pão-duro."), () => NicoPages(nico, NicoCollecting, null,
                        ("That Guy was just like him.", "O Guy era igual a ele."),
                        ("You should be a little more cultured.", "Você devia ser um pouco mais culto."))))));
        }

        private static bool RecordNicoFountainDonation(int stage, int amount)
        {
            if (amount <= 0) return false;
            int total = TownSaveVariables.GetInt(NicoDonationTotalKey,
                stage == NicoSecondDonation ? NicoLegacyDonationTotal : 0) + 2 * amount + 1;
            TownSaveVariables.SetInt(NicoDonationTotalKey, total);
            bool funded = total >= NicoFountainGoal;
            if (funded) TownSaveVariables.SetBool(PendingFountainKey, true);
            return funded;
        }

        private bool NicoDonationFollowUp(NPCController nico)
        {
            int remaining = NicoFountainGoal - TownSaveVariables.GetInt(NicoDonationTotalKey, NicoLegacyDonationTotal);
            return NicoPages(nico, NicoCounting, () => NicoAsk(nico, NicoCounting,
                    $"{remaining} G to go for... He, he, he, he.",
                    $"Ainda faltam {remaining} G... He, he, he, he.",
                    (T("You’re freaking me out.", "Você está me assustando."), () => NicoTalk(nico, NicoCollecting,
                        "Woops, I was absorbed in my own world.", "Opa, eu estava no meu próprio mundo.")),
                    (T("Good luck.", "Boa sorte."), () => NicoTalk(nico, NicoCollecting,
                        "Hey thanks! Feel free to make contributions anytime.",
                        "Ei, obrigada! Pode contribuir quando quiser."))),
                ("Hey, {name}. Didn’t realize you were here.", "Ei, {name}. Não tinha percebido que você estava aqui."),
                ("Let’s see, the total to date is... one, two, three... He, he, he, he.",
                    "Vejamos, o total até agora é... um, dois, três... He, he, he, he."));
        }

        // TOWN.BIN 0x2D2009–0x2D2498.
        private bool NicoFountainCelebration(NPCController nico) =>
            NicoPages(nico, NicoNeutral, () => NicoAsk(nico, NicoNeutral,
                    "We owe it to Ghosh and {name} mostly.", "Devemos isso principalmente ao Ghosh e ao {name}.",
                    (T("That’s good.", "Que bom."), () => NicoTalk(nico, NicoNeutral,
                        "Yes.", "Sim.", () => GhoshFountainCelebration(nico))),
                    (T("Yes, you owe it to me.", "Sim, deve isso a mim."), () => NicoTalk(nico, NicoPlayful,
                        "Yes, yes.", "Sim, sim.", () => GhoshFountainCelebration(nico)))),
                ("{name}! {name}! Look! Look! Look!", "{name}! {name}! Olha! Olha! Olha!"),
                ("This gave the town a much nicer feeling to, it didn’t it? I’m touched.",
                    "Isso deixou a cidade muito mais agradável, não deixou? Estou emocionada."));

        // TOWN.BIN 0x2D257D–0x2D2A15.
        private bool NicoSelfiEggConversation(NPCController nico, Action done)
        {
            void Reply() => NicoPages(nico, NicoWorried, done,
                ("I always thought she’s a girl who wouldn’t do anything if she couldn’t get something in return.",
                    "Eu sempre achei que ela não faria nada sem esperar algo em troca."),
                ("She does make a cute face in front of all the boys. But she always looks down on {name}.",
                    "Ela faz cara de boazinha na frente de todos os rapazes. Mas sempre olha você de cima, {name}."),
                (".......", "......."));
            return NicoAsk(nico, NicoWorried, "..... I’ve never seen Selfi give something so precious to anyone.",
                "..... Nunca vi a Selfi dar algo tão precioso para ninguém.",
                (T("Is that right?", "É mesmo?"), Reply),
                (T("Maybe it’s because of me.", "Talvez seja por minha causa."), () => NicoAsk(nico, NicoWorried,
                    "Hmmm, is that right?", "Hmmm, é mesmo?",
                    (T("Just a joke.", "Foi só brincadeira."), () => NicoTalk(nico, NicoNeutral, "Oh, I see.....", "Ah, entendi.....", Reply)),
                    (T("Heh heh.", "He, he."), () => NicoTalk(nico, NicoWorried,
                        "(Disgusting laugh.)", "(Risada nojenta.)", Reply)))));
        }

        private bool NicoCultureTalk(NPCController nico, int stage, Action done)
        {
            // TOWN.BIN 0x418DE3–0x419159.
            return stage switch
            {
                NicoCultureTalkFirst => NicoPages(nico, NicoNeutral, done,
                    ("You’ve got to keep shooting for the step above, today above yesterday, and tomorrow above today!",
                        "Você precisa sempre mirar um passo acima. Hoje melhor que ontem, amanhã melhor que hoje!"),
                    ("Well, from a cultural point of view.", "Bem, de um ponto de vista cultural.")),
                6 => NicoTalk(nico, NicoNeutral, "There’s no sense being alone feeling sorry for yourself.",
                    "Não adianta ficar sozinho sentindo pena de si mesmo.", done),
                7 => NicoTalk(nico, NicoWorried, "{name}, since when were you and Selfi so close to each other?",
                    "{name}, desde quando você e a Selfi ficaram tão próximos?", done),
                8 => NicoTalk(nico, NicoWorried, "No, forget that I asked that. It doesn’t change my own feelings...",
                    "Não, esqueça a pergunta. Isso não muda o que eu sinto...", () => NicoAsk(nico, NicoFlustered,
                        "Thank you! {name}.", "Obrigada, {name}!",
                        (T("Thank you for what?", "Obrigada pelo quê?"), () => NicoTalk(nico, NicoFlustered,
                            "I don’t know... whatever.", "Não sei... tanto faz.", done)),
                        (T("You’re welcome.", "De nada."), () => NicoTalk(nico, NicoPlayful, "Ha, ha.", "Ha, ha.", done)))),
                _ => NicoTalk(nico, NicoPlayful, "{name} is very cultured.", "O {name} é muito culto.", done)
            };
        }

        private void SpawnNicoFountainCast()
        {
            if (!FindStoryActor("nico_fountain"))
                SpawnStoryResident("nico", "nico_fountain", "Nico", TownResidentPlacementCatalog.GetTownCells("nico")[0]);
            if (StoryStage("nico") == NicoGhoshConfrontation) SpawnNicoFountainGhosh();
        }

        // TOWN.BIN 0x2D2B40–0x2D3819.
        private bool NicoGhoshFountainConversation(NPCController ghosh)
        {
            var nico = FindStoryActor("nico_fountain");
            var chero = FindStoryActor("chero");
            if (!nico || !chero) return false;
            return GhoshIrritatedTalk(ghosh, "Move, move, move!", "Saiam, saiam, saiam!", () =>
                GhoshIrritatedTalk(ghosh, "You grubby bum, polluting Nico’s park.", "Seu vagabundo imundo, sujando o parque da Nico.", () =>
                    NicoTalk(nico, NicoNeutral, "Stop it, Ghosh! This park isn’t just mine. It’s for everyone.",
                        "Pare, Ghosh! Este parque não é só meu. É de todos.", () =>
                            GhoshIrritatedTalk(ghosh, "No, I donated my money for you to build this park.",
                                "Não. Eu dei meu dinheiro para você construir este parque.", () =>
                                    GhoshIrritatedTalk(ghosh, "Get out, you bum.", "Saia daqui, vagabundo.", () =>
                                        StoryAsk(chero, "W... what?", "O... o quê?",
                                            (T("I’ll help you.", "Vou ajudar você."), () => NicoGhoshDefiesKoh(ghosh, nico, chero)),
                                            (T("Go on.", "Continue."), () => NicoGhoshDefiesKoh(ghosh, nico, chero))))))));
        }

        private void NicoGhoshDefiesKoh(NPCController ghosh, NPCController nico, NPCController chero) =>
            GhoshIrritatedTalk(ghosh, "What? It’s you again, {name}! Is this a deal between bums or what?!",
                "O quê? É você de novo, {name}! Isso é algum acordo entre vagabundos?", () =>
                    StoryAsk(ghosh, "What’s wrong with chasing out bums from a beautiful park?",
                        "O que há de errado em expulsar vagabundos de um parque bonito?", GhoshIrritatedPortrait,
                        (T("I lost my temper now.", "Agora perdi a paciência."), () => GhoshIrritatedTalk(ghosh,
                            "Great! It’s a battle!", "Ótimo! Vamos lutar!", () =>
                                NicoTalk(nico, NicoWorried, "Stop it, both of you. Pulling a sword out in the middle of the town!?",
                                    "Parem, vocês dois. Sacar uma espada no meio da cidade?!",
                                    () => NicoEndCheroInterruption(chero, nico)))),
                        (T("The music was so beautiful.", "A música era tão bonita."),
                            () =>
                            {
                                AdvanceTownStory("nico", NicoCheroOpinion);
                                NicoPraiseCheroMusic(chero, nico);
                            })));

        private void NicoEndCheroInterruption(NPCController chero, NPCController nico)
        {
            AdvanceTownStory("nico", NicoCheroOpinion);
            StoryAsk(chero, "Please wait. I’m not going to play tonight.", "Por favor, espere. Não vou tocar hoje à noite.",
                (T("You don’t have to leave.", "Você não precisa ir embora."), () => NicoAfterCheroPerformance(chero, nico)));
        }

        private bool NicoAfterCheroPerformance(NPCController chero, NPCController nico) =>
            NicoAsk(nico, NicoWorried, "{name}!?", "{name}!?",
                (T("The music was beautiful, wasn’t it?", "A música foi bonita, não foi?"), () => NicoPraiseCheroMusic(chero, nico)));

        private void NicoPraiseCheroMusic(NPCController chero, NPCController nico)
        {
            void ThankKoh() => StoryTalk(chero, "Thank you so much.", "Muito obrigado.", () => NicoCheroApology(nico));
            NicoPages(nico, NicoNeutral, () =>
                {
                    var ghosh = FindStoryActor("nico_ghosh");
                    if (ghosh) GhoshIrritatedTalk(ghosh, "Grr... Fools!", "Grr... Idiotas!", ThankKoh);
                    else ThankKoh();
                },
                ("The music was beautiful... Yes, it was beautiful. I want to hear more.",
                    "A música foi bonita... Sim, foi linda. Quero ouvir mais."),
                ("Even a cat wants to listen too.", "Até um gato quer ouvir."),
                ("Why can’t Ghosh understand something that even a cat appreciates?",
                    "Por que o Ghosh não entende algo que até um gato aprecia?"),
                ("Ghosh!", "Ghosh!"));
        }

        private void NicoCheroApology(NPCController nico)
        {
            void Reconcile() => NicoPages(nico, NicoNeutral, () =>
                {
                    TownRetailScriptFlags.CreatePersistent().SetBit(NicoCheroEndedFlag);
                    AdvanceTownStory("nico", NicoConfessionWake);
                },
                ("OK! From now on, {name} and I are back to where we were before, alright?",
                    "Pronto! A partir de agora, eu e você voltamos a ser como antes, certo, {name}?"),
                ("Bye, {name}!", "Até mais, {name}!"));
            NicoPages(nico, NicoWorried, () => NicoAsk(nico, NicoWorried,
                    "Are you upset? You must be. Give me a punch, if you like.",
                    "Você está chateado? Deve estar. Pode me dar um soco, se quiser.",
                    (T("Dad told me.", "Meu pai me disse."), () => NicoTalk(nico, NicoFlustered,
                        "Aha, the only thing {name} was taught from his father. Woman! Who me? Did you just say I’m a woman?",
                        "Ah, a única coisa que seu pai ensinou a você, {name}. Mulher? Eu? Você acabou de dizer que sou uma mulher?",
                        Reconcile)),
                    (T("I’ll punch you once.", "Vou dar um soco."), () => NicoTalk(nico, NicoFlustered, "Smack!", "Paf!", Reconcile))),
                ("{name}. Sorry.", "{name}. Desculpe."),
                ("I might have been cruel to you lately. I just didn’t want to see your face.",
                    "Talvez eu tenha sido cruel com você ultimamente. Eu só não queria ver sua cara."));
        }

        // TOWN.BIN 0x669F8D–0x66A5D7 and 0x66A634–0x66ABDF.
        private bool NicoLateHouseTalk(NPCController nico, int stage, Action done)
        {
            if (stage == NicoHouseTalkSecond)
                return NicoAsk(nico, NicoFlustered, "Hey, {name}. Glad to see ya. Recently, I get this great feeling when I see your face.",
                    "Ei, {name}. Que bom te ver. Ultimamente, fico tão feliz quando vejo você.",
                    (T("Stop it, that’s gross.", "Pare com isso, que nojo."), () => NicoTalk(nico, NicoPlayful,
                        "I get it... you’re shy...", "Já entendi... você está com vergonha...", done)),
                    (T("Stop it, that’s embarrassing.", "Pare com isso, que vergonha."), () => NicoTalk(nico, NicoPlayful,
                        "Well, well, well. Even you can get shy sometimes.", "Ora, ora. Até você fica com vergonha de vez em quando.", done)));

            void Continue() => NicoPages(nico, NicoNeutral, done,
                ("You know, I was brought up by my father alone.", "Sabe, meu pai me criou sozinho."),
                ("So before you know it, I was speaking exactly like him.", "Quando percebi, já falava igualzinho a ele."),
                ("Besides, as a kid, the other kids in the neighborhood were all rowdy rascals like yourself, {name}...",
                    "E quando eu era criança, os meninos do bairro eram todos uns bagunceiros como você, {name}..."),
                ("I guess you can’t change old habits overnight.", "Acho que não dá para mudar velhos hábitos da noite para o dia."));

            return NicoPages(nico, NicoFlustered, () => NicoAsk(nico, NicoFlustered,
                    "You’d rather me speak in a delicate womanly manner, wouldn’t you?",
                    "Você preferia que eu falasse de um jeito delicado e feminino, não é?",
                    (T("Doesn’t sound natural...", "Não soa natural..."), () => NicoTalk(nico, NicoNeutral,
                        "Yeah, I guess, I’m not speaking naturally, am I?", "Pois é, não estou falando de um jeito natural, né?", Continue)),
                    (T("I like it that way.", "Gosto assim."), () => NicoTalk(nico, NicoPlayful,
                        "I knew that would be the case. I’m goin’ to, -ah, no, not ‘goin’... Ha, ha.",
                        "Eu sabia. Eu vô... ah, não é ‘vô’... Ha, ha.", Continue))),
                ("Oh, hello {name}. Welcome.", "Ah, olá, {name}. Seja bem-vindo."),
                ("Glad to see ya, or, ah... I’m delighted to see you...", "Que bom te ver. Quer dizer, ah... é um prazer vê-lo..."));
        }

        private bool NicoHouseSmallTalk(NPCController nico, int stage)
        {
            if (stage == StoryComplete)
                return NicoAsk(nico, NicoPlayful, "{name}, which do you prefer, girls or adventures?",
                    "{name}, o que você prefere, garotas ou aventuras?",
                    (T("I like ’em both.", "Gosto dos dois."), () => NicoFeelingsConversation(nico)),
                    (T("I love ’em both.", "Adoro os dois."), () => NicoFeelingsConversation(nico)));
            if (stage >= NicoConfessionWake)
                return NicoTalk(nico, NicoFlustered, "{name}, when I see you smile, I just feel terrific.",
                    "{name}, quando vejo você sorrir, eu me sinto tão bem.");
            if (stage >= NicoCultureTalkFirst)
                return NicoAsk(nico, NicoWorried, "Hey..., {name}. ... how’s Selfi?",
                    "Ei..., {name}. ... como está a Selfi?",
                    (T("Why are you asking me?", "Por que está me perguntando?"), () => NicoTalk(nico, NicoFlustered,
                        "Ah... n-no specific reason.", "Ah... p-por nada em especial.")),
                    (T("I guess she’s fine.", "Acho que ela está bem."), () => NicoTalk(nico, NicoWorried,
                        "Is that so... you know, I was with Ghosh until just moments ago.",
                        "É mesmo... sabe, eu estava com o Ghosh até agora há pouco.")));
            return NicoTalk(nico, NicoNeutral, "My personality doesn’t allow me to stay still for long.",
                "Eu não consigo ficar parada por muito tempo.");
        }

        private void NicoFeelingsConversation(NPCController nico)
        {
            void Reply() => NicoPages(nico, NicoNeutral, null,
                ("Ha, ha, ha. Boy, I guess it won’t be for a while.", "Ha, ha, ha. Pelo visto ainda vai demorar."),
                ("I know {name}, all you can think of right now is that tower. I’m rooting for you.",
                    "Eu sei, {name}. Agora você só pensa naquela torre. Estou torcendo por você."),
                ("Good luck.", "Boa sorte."));
            NicoTalk(nico, NicoPlayful, "Just as I thought.", "Foi o que imaginei.", () => NicoAsk(nico, NicoPlayful,
                "I wonder how old you’ll be when you finally understand a woman’s feelings.",
                "Com que idade será que você vai entender os sentimentos de uma mulher?",
                (T("I do already.", "Já entendo."), Reply),
                (T("What kind of feeling?", "Que sentimentos?"), Reply)));
        }

        private bool NicoFinalHouseConversation(NPCController nico) =>
            NicoPages(nico, NicoPlayful, () => NicoAsk(nico, NicoPlayful,
                    "How come I didn’t notice until now?", "Como é que eu só percebi agora?",
                    (T("What are you talking about?", "Do que você está falando?"), () => NicoPages(nico, NicoPlayful, CompleteNicoStory,
                        ("It’s a secret!", "É segredo!"),
                        ("It just means that the bird of happiness is hovering not too far from here.",
                            "Só quer dizer que o pássaro da felicidade está voando por perto."))),
                    (T("About how handsome I am?", "Sobre como eu sou bonito?"), () => NicoPages(nico, NicoPlayful, CompleteNicoStory,
                        ("You’re outta your mind!", "Você está maluco!"),
                        ("Who ever gave you the idea that you’re handsome!? Yeah, but you are pretty neat.",
                            "Quem colocou na sua cabeça que você é bonito?! Mas você é bem legal, sim.")))),
                ("Hey, {name}. Looking good again today. You’re super!",
                    "Ei, {name}. Você está bem hoje de novo. Você é demais!"));

        // TOWN.BIN 0x4402DE–0x440529.
        private bool NicoLibraryConversation(NPCController nico)
        {
            void Finish()
            {
                TownSaveVariables.SetBool(NicoLibrarySeenKey, true);
                PersistTownProgress();
                nico.gameObject.SetActive(false);
            }
            return NicoPages(nico, NicoNeutral, () => NicoAsk(nico, NicoNeutral,
                    "L-I-B-R-A-R-Y, a library? So they have books and things here?",
                    "B-I-B-L-I-O-T-E-C-A, uma biblioteca? Quer dizer que aqui tem livros e essas coisas?",
                    (T("Very cultural, isn’t it?", "Muito culto, não é?"), () => NicoTalk(nico, NicoWorried,
                        "Why? Your father never talked about it.", "Por quê? Seu pai nunca falou disso.", Finish)),
                    (T("You can borrow books here.", "Dá para pegar livros emprestados aqui."), () =>
                        NicoPages(nico, NicoNeutral, Finish,
                            ("Really?", "É mesmo?"),
                            ("But your father never talked about it.", "Mas seu pai nunca falou disso."),
                            ("You should have built something more cultural.", "Você devia ter construído algo mais culto.")))),
                ("Howdy, {name}. I heard that you made this building, but what is it?",
                    "Olá, {name}. Ouvi dizer que você construiu este prédio, mas o que é isto?"));
        }

        private void NicoWakeUpStory(NPCController nico, bool survived, bool thanks, bool confession)
        {
            if (thanks)
            {
                NicoDonationWakeUp(nico);
                return;
            }
            if (confession)
            {
                NicoConfessionVisit(nico);
                return;
            }

            int floor = TownSaveVariables.GetInt(TownReputation.HighestFloorKey);
            if (!survived && floor >= 39)
            {
                // TOWN.BIN 0x41B1A1–0x41B5C3.
                NicoPages(nico, NicoWorried, null,
                    ("{name}!  {name}! Hey! Can you hear me!", "{name}! {name}! Ei! Consegue me ouvir?!"),
                    ("Oh thank goodness. You seem to be all right...", "Ah, graças a Deus. Parece que você está bem..."),
                    ("When the tamed monster brought you home, I saw you in a state worse than ever before. {name}, I honestly thought my heart was going to explode!",
                        "Quando seu familiar trouxe você para casa, eu nunca tinha visto você tão mal. {name}, achei que meu coração ia explodir!"),
                    ("But I’m glad you’re okay. I’m not going to stop you from pursuing your dreams. But will you make me one promise?",
                        "Mas fico feliz que esteja bem. Não vou impedir você de seguir seus sonhos. Só me prometa uma coisa?"),
                    ("Don’t do something foolish that would make me cry! Please promise me that much!",
                        "Não faça nenhuma loucura que me faça chorar! Por favor, só me prometa isso!"),
                    ("{name}, as long as you’re healthy, I’m satisfied. I don’t care about anything else. So please.",
                        "{name}, para mim basta que você esteja bem. Não me importo com mais nada. Então, por favor."),
                    ("{name}, relax.", "{name}, descanse."));
                return;
            }
            if (survived && floor >= 39 && !TownSaveVariables.GetBool(BeldoClearedKey))
            {
                // TOWN.BIN 0x41A789–0x41A9C7.
                void Farewell() => NicoPages(nico, NicoWorried, null,
                    ("{name}!", "{name}!"), ("Are you leaving?", "Já vai?"), ("Be careful!", "Tome cuidado!"));
                NicoPages(nico, NicoWorried, () => NicoAsk(nico, NicoWorried, "Whimper, whimper...", "Snif, snif...",
                        (T("Don’t cry. Dad will be upset.", "Não chore. Meu pai vai ficar triste."), Farewell),
                        (T("You’re like the cry baby.", "Você parece um bebê chorão."), Farewell)),
                    ("The time is approaching for your battle.", "Está chegando a hora da sua luta."),
                    ("{name}, I’m worried!", "{name}, estou preocupada!"),
                    ("Well, if Beldo... and I emphasize, IF... If Beldo were to defeat you, I..., I can’t...!",
                        "Bom, se o Beldo... E eu disse SE... Se o Beldo derrotar você, eu... eu não consigo...!"));
                return;
            }
            if (survived && !TownSaveVariables.GetBool(BeldoClearedKey) &&
                TownRetailScriptFlags.CreatePersistent().IsFlagSet(NicoAfterBeldoVisionFlag) &&
                !TownSaveVariables.GetBool(NicoTowerReportSeenKey))
            {
                NicoAfterBeldoVision(nico);
                return;
            }
            if (!survived)
            {
                // TOWN.BIN 0x415CB1–0x415F30.
                NicoPages(nico, NicoWorried, () => NicoAsk(nico, NicoWorried, "Don’t worry me so much.",
                        "Não me deixe tão preocupada.",
                        (T("Who asked you to bother me.", "Quem pediu para você vir me incomodar?"), () => NicoPages(nico, NicoPlayful, null,
                            ("Don’t be shy, I know you’re glad to see me.", "Não fique com vergonha. Sei que está feliz em me ver."),
                            ("Can’t you be a bit more positive?", "Não dá para ser um pouco mais otimista?"))),
                        (T("You look cool as always.", "Você está ótima, como sempre."), () => NicoTalk(nico, NicoFlustered,
                            "I’m here to wake you up,{name}.", "Vim acordar você, {name}."))),
                    ("Good Morning, {name}.", "Bom dia, {name}."),
                    ("You failed again, did you? If your tamed monster didn’t bring you home, I don’t know how many times you would be killed.",
                        "Você falhou de novo, não foi? Se seu familiar não trouxesse você para casa, nem sei quantas vezes você teria morrido."));
                return;
            }

            // TOWN.BIN 0x415BE9–0x416057, 0x4160EB–0x41635E.
            NicoPages(nico, NicoNeutral, () => NicoAsk(nico, NicoNeutral, "Are you going to the tower today too?",
                    "Você vai à torre hoje também?",
                    (T("Yes I’m going", "Sim, eu vou."), () => NicoPages(nico, NicoNeutral, null,
                        ("Is that so, well, be careful.", "É mesmo? Bom, tome cuidado."),
                        ("Don’t exhaust yourself.", "Não se esgote."),
                        ("Don’t give up, but at the same time be careful right to the end.",
                            "Não desista, mas tome cuidado até o fim."),
                        ("I do worry about you.", "Eu me preocupo mesmo com você."))),
                    (T("I’m not going", "Não vou."), () => NicoTalk(nico, NicoPlayful,
                        "That’s great! You’ve got to get some rest once in a while.",
                        "Que bom! Você precisa descansar de vez em quando.", () => WeedyHomeReply("Yup!", "Isso!")))),
                ("Hey, {name}!", "Ei, {name}!"),
                ("Get up. What’s keeping you in bed for so long.", "Levante. Por que está demorando tanto para sair da cama?"),
                ("That’s no good. You sleeeeepy head!", "Assim não dá, seu dorminhoco!"));
        }

        // TOWN.BIN 0x419959–0x419CE2.
        private void NicoAfterBeldoVision(NPCController nico)
        {
            NicoPages(nico, NicoWorried, () =>
                {
                    TownSaveVariables.SetBool(NicoTowerReportSeenKey, true);
                    PersistTownProgress();
                    NicoTalk(nico, NicoNeutral, "Go get ’em, {name}!", "Mostre o que sabe fazer, {name}!");
                },
                ("Good morning, {name}. What’s the matter? Is something wrong?",
                    "Bom dia, {name}. O que foi? Aconteceu alguma coisa?"),
                ("You don’t say, all that happened at the tower?", "É mesmo? Tudo isso aconteceu na torre?"),
                ("Well... I’m not going to tell your mother. I don’t want to get her all worried.",
                    "Bem... Não vou contar à sua mãe. Não quero deixar ela preocupada."),
                ("She’s regularly very strong-witted, but not when it comes to your father.",
                    "Ela costuma ser muito forte, mas fica abalada quando o assunto é seu pai."),
                ("Hey, if there’s anything that I can do, just let me know.", "Ei, se eu puder fazer alguma coisa, é só falar."),
                ("I want to help. It can be anything...", "Quero ajudar. Pode ser qualquer coisa..."),
                ("I may not show it, but I really worry about you. Even more than your mother does.",
                    "Posso até não demonstrar, mas me preocupo muito com você. Até mais que a sua mãe."));
        }

        // TOWN.BIN 0x41918B–0x41960A.
        private void NicoDonationWakeUp(NPCController nico)
        {
            NicoPages(nico, NicoNeutral, () => NicoAsk(nico, NicoNeutral, "Then why did you make this donation?",
                    "Então por que você fez essa doação?",
                    (T("Cause you were enthusiastic.", "Porque você estava animada."), () => NicoPages(nico, NicoNeutral, null,
                        ("There are two types of people, those who make an effort to reach their dreams, and those who sit back and dream about their dreams.",
                            "Há dois tipos de gente. Quem se esforça para realizar seus sonhos e quem fica sentado só sonhando."),
                        ("My personality doesn’t allow me to stay still for long.", "Eu não consigo ficar parada por muito tempo."))),
                    (T("I felt sorry for you.", "Fiquei com pena de você."), () => NicoPages(nico, NicoPlayful, null,
                        ("You mean when I’m out collecting donations?", "Você quer dizer quando saio arrecadando doações?"),
                        ("I’m just doing that because I want to.", "Só faço isso porque quero.")))),
                ("Are you awake now?", "Já acordou?"),
                ("Thanks for the donation the other day!", "Obrigada pela doação do outro dia!"),
                ("Hey, I understand that you’ve sold all the tamed monsters your father left you to get by on living expenses,",
                    "Eu sei que você vendeu todos os familiares que seu pai deixou para pagar as despesas da casa,"),
                ("but I also understand you’re starting to run low on your funds.",
                    "e também sei que o dinheiro está começando a acabar."));
        }

        // TOWN.BIN 0x4188E5–0x418D6C.
        private void NicoConfessionVisit(NPCController nico)
        {
            void Continue() => NicoPages(nico, NicoNeutral, () => NicoAsk(nico, NicoNeutral,
                    "Things and sources originating from such hearts are what culture is all about.",
                    "É das coisas que nascem desses corações que se faz a cultura.",
                    (T("?????", "?????"), () => NicoFinishConfession(nico)),
                    (T("What you’re talking about?", "Do que você está falando?"), () => NicoFinishConfession(nico))),
                ("That was pretty music... Beautiful.", "Aquela música era bonita... Linda."),
                ("I knew from then.", "Eu entendi naquele momento."),
                ("That to be cultured is not to obtain things or skills, but it comes from the heart...",
                    "Que ser culto não é ter coisas ou habilidades. Isso vem do coração..."),
                ("The heart to try and make it better.", "Do coração que tenta melhorar as coisas."),
                ("The heart to understand the beauty in something that is beautiful.",
                    "Do coração capaz de entender a beleza daquilo que é belo."));
            NicoTalk(nico, NicoPlayful, "Yo, {name}! Are you awake by now?", "Ei, {name}! Você já acordou?", () =>
                NicoAsk(nico, NicoPlayful, "Ha, ha. You were pretty cool.", "Ha, ha. Você foi bem legal.",
                    (T("Oh, right", "Ah, é."), Continue),
                    (T("What are you talking about", "Do que você está falando?"), () => NicoTalk(nico, NicoNeutral,
                        "You helped me play the cello at the park.", "Você me ajudou a tocar violoncelo no parque.", Continue))));
        }

        private void NicoFinishConfession(NPCController nico) =>
            NicoPages(nico, NicoFlustered, null,
                ("Well, that doesn’t matter, since I do understand it.", "Bom, isso não importa, porque eu entendo."),
                ("That’s why I wanted to be honest to my heart and feelings.",
                    "É por isso que eu quis ser sincera com meu coração e com o que sinto."));
    }
}
