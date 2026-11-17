using UnityEngine;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string PattyMeals = StoryPrefix + "patty.meals";
        private const string PattyMealDue = StoryPrefix + "patty.mealDue";
        private const string PattyGhoshCheapMealSeen = StoryPrefix + "patty.ghoshCheapMealSeen";
        private const string PattyRouteVersion = StoryPrefix + "patty.routeVersion";
        private const int PattyLoveFlag = 5242;
        private const int PattyUnpaidMealFlag = 1003; // F_paty_kuinige
        // Keep the existing stage numbers for restaurant visits and saves.
        private const int PattyFirstMeal = 0;
        private const int PattySecondMeal = 1;
        private const int PattyChooseMeal = 2;
        private const int PattyGhoshScene = 3;
        private const int PattyFinalTalk = 4;
        private const int PattyOctopusMeal = 5;
        private const string PattyPortraitPrefix = "Dialogue/PattyRetail_variant";
        private static readonly int[][] PattyPortraitFrameDurations =
        {
            new[] { 96, 4, 8, 2 },
            new[] { 128, 4, 6, 32, 2, 2 },
            new[] { 128, 4, 48, 2 },
            new[] { 96, 4, 8, 2 },
            new[] { 2 }
        };
        private static readonly (string English, string Portuguese, int Gold, string DescriptionEnglish,
            string DescriptionPortuguese)[] RestaurantMeals =
        {
            ("Rice", "Arroz", 20, "One order of rice, coming up.", "Um pedido de arroz, já vai."),
            ("Natto soy beans", "Soja natto", 30, "One order Natto soy beans, coming up.", "Um pedido de soja natto, já vai."),
            ("Hiyayakko tofu", "Tofu hiyayakko", 40, "One order Hiyayakko tofu, coming up.", "Um pedido de tofu hiyayakko, já vai."),
            ("Sandsand", "Sandsand", 60, "One order Sandsand, coming up.", "Um pedido de Sandsand, já vai."),
            ("Zuroro cutlet", "Costeleta zuroro", 70, "One order Zuroro cutlet, coming up.", "Um pedido de costeleta zuroro, já vai."),
            ("Crystal curry", "Curry de cristal", 80, "One order Crystal curry, coming up.", "Um pedido de curry de cristal, já vai."),
            ("Spiral fried rice", "Arroz frito em espiral", 90, "One order Spiral fried rice, coming up.", "Um pedido de arroz frito em espiral, já vai."),
            ("Yakitori chicken", "Frango yakitori", 100, "One order Yakitori chicken, coming up.", "Um pedido de frango yakitori, já vai."),
            ("Fried Shining Prawn", "Camarão Brilhante frito", 150, "One order Fried Shining Prawn, coming up.", "Um pedido de Camarão Brilhante frito, já vai."),
            ("Stewed Beef on Rice", "Carne cozida com arroz", 290, "One order Stewed Beef on Rice coming up.", "Um pedido de carne cozida com arroz, já vai."),
            ("Special", "Especial", 300, "One order of the Special, coming up.", "Um pedido do Especial, já vai."),
            ("Red Sushi", "Sushi vermelho", 500, "One order Red Sushi, coming up.", "Um pedido de sushi vermelho, já vai."),
            ("Blue Sushi", "Sushi azul", 500, "One order Blue Sushi, coming up.", "Um pedido de sushi azul, já vai."),
            ("Yellow Sushi", "Sushi amarelo", 500, "One order Yellow Sushi, coming up.", "Um pedido de sushi amarelo, já vai."),
            ("Green Sushi", "Sushi verde", 500, "One order Green Sushi, coming up.", "Um pedido de sushi verde, já vai.")
        };
        private bool _pattyFinalScenePlaying;

        private static int[] PattyPortraitFrameDurationsFor(string portraitPath)
        {
            if (string.IsNullOrEmpty(portraitPath) ||
                !portraitPath.StartsWith(PattyPortraitPrefix, System.StringComparison.Ordinal) ||
                portraitPath.Length <= PattyPortraitPrefix.Length) return null;
            int variant = portraitPath[PattyPortraitPrefix.Length] - '0';
            return variant >= 0 && variant < PattyPortraitFrameDurations.Length ? PattyPortraitFrameDurations[variant] : null;
        }

        private static string PattyPortrait(int variant) =>
            $"{PattyPortraitPrefix}{Mathf.Clamp(variant, 0, PattyPortraitFrameDurations.Length - 1)}_frame0";

        private bool PattyTalk(NPCController npc, int portrait, string en, string pt, System.Action next = null) =>
            StoryTalk(npc, en, pt, PattyPortrait(portrait), next);

        private bool PattyAsk(NPCController npc, int portrait, string en, string pt,
            params (string Label, System.Action Apply)[] choices) =>
            StoryAsk(npc, en, pt, PattyPortrait(portrait), choices);

        private bool PattyStory(NPCController npc)
        {
            const string route = "patty";
            if (StoryRoom != RestaurantRoom) return false;
            int due = TownSaveVariables.GetInt(PattyMealDue);
            if (due > 0) return PattyOutstandingMeal(npc, due);
            int stage = PattyStage();
            if (stage == PattyFirstMeal) return PattyFirstVisit(npc);
            if (stage == PattySecondMeal)
                return StoryNewDay(route) ? PattySecondVisit(npc) : PattyRegularVisit(npc);
            if (stage == PattyChooseMeal)
                return StoryNewDay(route) ? PattyThirdVisit(npc) : PattyRegularVisit(npc);
            if (stage == PattyOctopusMeal)
                return StoryNewDay(route) ? PattyOctopusVisit(npc) : PattyRegularVisit(npc);
            if (stage == PattyGhoshScene)
            {
                if (!StoryNewDay(route)) return PattyRegularVisit(npc);
                if (!_pattyFinalScenePlaying) StartCoroutine(PlayPattyFinalScene());
                return true;
            }
            if (stage == PattyFinalTalk)
                return PattySpecialVisit(npc);
            return PattyRegularVisit(npc);
        }

        // TOWN.BIN 0x56C61A–0x56C754.
        private bool PattyRegularVisit(NPCController npc) =>
            PattyVisitChoice(npc, 0, "Hello.  Can I help you?", "Olá. Posso ajudar?",
                () =>
                {
                    if (StoryStage("patty") >= StoryComplete) PattyMealAdvice(npc, () => OpenPattyMealMenu(npc));
                    else PattyTalk(npc, 0, "Really?  You want something to eat?\nThen please sit there.",
                        "É mesmo? Quer comer alguma coisa?\nEntão, por favor, sente-se ali.",
                        () => OpenPattyMealMenu(npc));
                },
                () => PattyTalk(npc, 0, "That’s what I thought.", "Foi o que pensei."));

        private bool PattyMealAdvice(NPCController npc, System.Action next) =>
            PattyTalk(npc, 0, "You don’t have to show off by having anything expensive, just have what you really want, okay?",
                "Você não precisa pedir algo caro para impressionar. Peça o que realmente quer, está bem?", next);

        private bool PattyVisitChoice(NPCController npc, int portrait, string en, string pt,
            System.Action accept, System.Action decline) =>
            PattyAsk(npc, portrait, en, pt,
                (T("I’d like something to eat.", "Quero comer alguma coisa."), accept),
                (T("No.", "Não."), decline));

        private int PattyStage()
        {
            int stage = StoryStage("patty");
            if (TownSaveVariables.GetInt(PattyRouteVersion) >= 2) return stage;

            // Migrate saves made with the short-lived all-menu-items route.
            // Its final three states had different meanings from retail.
            int retailStage = stage switch
            {
                3 => PattyChooseMeal,
                4 => PattyGhoshScene,
                5 => PattyFinalTalk,
                _ => stage
            };
            TownSaveVariables.SetInt(StoryPrefix + "patty.stage", retailStage);
            TownSaveVariables.SetInt(PattyRouteVersion, 2);
            PersistTownProgress();
            return retailStage;
        }

        private bool PattyFirstVisit(NPCController npc)
        {
            PattySequence(npc, 0, () => PattyAsk(npc, 0, "Are you hungry? I'll make you something.",
                    "Está com fome? Vou fazer alguma coisa para você.",
                    (T("Will you? Please!", "Vai mesmo? Por favor!"), () => PattyFirstMealMenu(npc)),
                    (T("No, don’t bother.", "Não, não precisa."), () =>
                        PattyTalk(npc, 0, "If you say so...", "Se você diz..."))),
                ("Hi {name}.\nWe’re closed today.", "Oi, {name}.\nHoje estamos fechados."),
                ("My father has gone on a trip to the city of Haruka to stock up on food.",
                    "Meu pai foi até a cidade de Haruka comprar comida."),
                ("My father still hasn't returned from his trip.", "Meu pai ainda não voltou da viagem."));
            return true;
        }

        private void PattyFirstMealMenu(NPCController npc) =>
            PattyAsk(npc, 0, "Let's see, what should I make.", "Vamos ver, o que eu devo fazer.",
                (T("I’ll tell you what I’d like.", "Vou dizer o que eu quero."), () => PattySpecialtyMenu(npc)),
                (T("Anything would be fine.", "Qualquer coisa serve."), () =>
                    PattyTalk(npc, 1, "I can’t stand people who can’t make up their minds.",
                        "Não suporto gente que não consegue se decidir.", () => PattySpecialtyMenu(npc))));

        private void PattySpecialtyMenu(NPCController npc) =>
            PattyAsk(npc, 0, "What would you like?", "O que você gostaria?",
                (T("Deep Fried Prawns", "Camarões fritos"), () => PattySequence(npc, 0,
                    () => ServePattyFirstMeal(npc),
                    ("I’m glad you chose Deep Fried Prawns.\nI’m pretty good at making this.",
                        "Que bom que escolheu Camarões Fritos.\nEu sei fazer muito bem esse prato."),
                    ("Just wait a moment.", "Espere só um momento."))),
                (T("Crystal curry", "Curry de cristal"), () =>
                    PattyTalk(npc, 0, "Okay, curry it is.", "Certo, então vai ser curry.", () => ServePattyFirstMeal(npc))),
                (T("Spiral fried rice", "Arroz frito em espiral"), () =>
                    PattyTalk(npc, 0, "Okay, fried rice it is.", "Certo, então vai ser arroz frito.",
                        () => ServePattyFirstMeal(npc))));

        private void ServePattyFirstMeal(NPCController npc)
        {
            // The first meal is prepared privately while Koppe is away.
            AdvanceTownStory("patty", PattySecondMeal);
            StartCoroutine(PlayPattyServeAnimation(npc));
            PattyTalk(npc, 0, "I'm almost done, can you wait at the table?",
                "Já está quase pronto, pode esperar à mesa?", () => PattyFirstMealAfter(npc));
        }

        // TOWN.BIN 0x56E6CE–0x56EB54.
        private bool PattySecondVisit(NPCController npc) =>
            PattyVisitChoice(npc, 0, "Hi {name}.\nWelcome.", "Oi, {name}.\nBoas-vindas.",
                () => PattySequence(npc, 0, () => PattySecretInterruption(npc),
                    ("Please be seated here.", "Por favor, sente-se aqui."),
                    ("{name}, don’t tell anybody about the other day.", "{name}, não conte a ninguém sobre o outro dia.")),
                () => PattyTalk(npc, 0, "Oh, that’s too bad.", "Ah, que pena."));

        private void PattySecretInterruption(NPCController npc)
        {
            void DiscussSecret() => PattyTalk(npc, 0,
                "That’s right!  {name}, you and I have a secret that only we know about.",
                "Isso mesmo! {name}, você e eu temos um segredo que só nós sabemos.", () =>
                PattyAsk(npc, 0, "That means we’ve got a secret relationship, what shall we do?",
                    "Isso quer dizer que temos uma relação secreta. O que vamos fazer?",
                    (T("What do you mean?", "O que você quer dizer?"), () => PattySecretMealMenu(npc)),
                    (T("Let’s take good care of it!", "Vamos cuidar bem dela!"), () => PattySecretMealMenu(npc))));
            var ghosh = FindPattyGhosh();
            if (!ghosh) { DiscussSecret(); return; }
            GhoshSequence(ghosh, () => PattyTalk(npc, 0, "Huh...", "Hã...", DiscussSecret),
                ("What, what’s that?\nPatty, it would be bad luck to share a secret with this guy.",
                    "O quê? O que foi?\nPatty, vai dar azar ter um segredo com esse sujeito."),
                ("You just concentrate on how you can show your love to me.",
                    "Concentre-se em como pode mostrar seu amor por mim."));
        }

        private void PattySecretMealMenu(NPCController npc) =>
            PattyTalk(npc, 0, "This is exciting.", "Isso é empolgante.",
                () => OpenPattyMealMenu(npc, PattyChooseMeal,
                    prompt: T("Well, what would you like to eat today?", "Bem, o que você quer comer hoje?")));

        // TOWN.BIN 0x56EBBA–0x56F55D.
        private bool PattyThirdVisit(NPCController npc) =>
            PattyVisitChoice(npc, 0, "Oh, hi {name}.", "Ah, oi, {name}.",
                () => PattyTalk(npc, 0, "Please be seated here.", "Por favor, sente-se aqui.",
                    () => PattyPersonalOrderScene(npc)),
                () => PattyTalk(npc, 0, "Aww, that’s a pity.\nWell come again.", "Ah, que pena.\nVolte outra hora."));

        private void PattyPersonalOrderScene(NPCController npc)
        {
            void AskPattyToCook() => PattyAsk(npc, 0, "I know!  {name}, do you mind if I prepared your order?",
                "Já sei! {name}, você se importa se eu preparar seu pedido?",
                (T("I’d love that!", "Eu adoraria!"), () => PattyTalk(npc, 0,
                    "Oh great!  It may not be as tasty as what my father makes, but I’ll do my best.",
                    "Que ótimo! Talvez não fique tão gostoso quanto o do meu pai, mas vou dar o meu melhor.",
                    () => OpenPattyMealMenu(npc, PattyOctopusMeal, prompt: T("So what do you want?", "Então, o que você quer?")))),
                (T("Leave me with my meal.", "Só quero a minha refeição."), () => PattyTalk(npc, 0,
                    "I guess you’re right.  You are paying for this.  Well, please enjoy what my father made.",
                    "Acho que tem razão. Você está pagando por isso. Bem, aproveite o que meu pai preparou.",
                    () => OpenPattyMealMenu(npc, prompt: T("And what will it be.", "E qual vai ser o pedido?")))));
            var ghosh = FindPattyGhosh();
            if (!ghosh) { AskPattyToCook(); return; }
            GhoshTalk(ghosh, "Yes!  The food prepared by Patty is the best!", "Sim! A comida que a Patty prepara é a melhor!", () =>
                PattyTalk(npc, 0, "Actually, the food is prepared by my father...", "Na verdade, é meu pai que prepara a comida...", () =>
                    GhoshSequence(ghosh, () => PattyTalk(npc, 0, "R...Really?", "S...Sério?", AskPattyToCook),
                        ("Oh, well your love for me is absorbed by the food when you bring it to me.",
                            "Ah, bem, seu amor por mim passa para a comida quando você a traz."),
                        ("This great taste is the taste of your love, and therefore it is the same as if you had",
                            "Esse sabor maravilhoso é o sabor do seu amor, então é como se você mesma"),
                        ("prepared it yourself.", "tivesse preparado o prato."))));
        }

        // 0x56F5DC and 0x56F6D7 select IMG_BPATY_L for this meal.
        private bool PattyOctopusVisit(NPCController npc) =>
            PattyVisitChoice(npc, 3, "Hi {name}!", "Oi, {name}!",
                () => PattySequence(npc, 3, () => OpenPattyMealMenu(npc, PattyGhoshScene, 3),
                    ("Please be seated here.", "Por favor, sente-se aqui."),
                    ("{name}...", "{name}..."),
                    ("You seem much tougher and stronger compared to before you went to the tower.",
                        "Você parece bem mais resistente e forte do que antes de ir à torre."),
                    ("Although from the outside, you haven’t changed much...\nEat lots today now.",
                        "Embora por fora você não tenha mudado muito...\nComa bastante hoje.")),
                () => PattyTalk(npc, 3, "Aww.  Recently, I’ve wanted to make something for you so badly, {name}.",
                    "Ah... ultimamente eu queria tanto preparar alguma coisa para você, {name}."));

        private void PattySequence(NPCController npc, int portrait, System.Action done,
            params (string English, string Portuguese)[] lines)
        {
            void ShowLine(int index)
            {
                if (index >= lines.Length)
                {
                    done?.Invoke();
                    return;
                }
                PattyTalk(npc, portrait, lines[index].English, lines[index].Portuguese,
                    () => ShowLine(index + 1));
            }
            ShowLine(0);
        }

        private bool OpenPattyMealMenu(NPCController npc, int advance = 0, int portrait = 0, string prompt = null)
        {
            var choices = new (string Label, System.Action Apply)[RestaurantMeals.Length + 1];
            for (int i = 0; i < RestaurantMeals.Length; i++)
            {
                int mealIndex = i;
                var meal = RestaurantMeals[i];
                bool ordered = (TownSaveVariables.GetInt(PattyMeals) & (1 << i)) != 0;
                string status = ordered ? T(" (Tried)", " (Provado)") : string.Empty;
                choices[i] = ($"{T(meal.English, meal.Portuguese)}{status}  ·  {meal.Gold} G",
                    () => BuyPattyMeal(npc, mealIndex, advance, portrait));
            }
            choices[^1] = (T("Leave", "Sair"), () =>
                PattyTalk(npc, portrait, "Are you going home?", "Já vai para casa?"));
            string line = prompt ?? T("What would you like?", "O que você gostaria?");
            return PattyAsk(npc, portrait, line, line, choices);
        }

        private void BuyPattyMeal(NPCController npc, int index, int advance = 0, int portrait = 0)
        {
            var meal = RestaurantMeals[index];
            // Story meals still advance at the order. Ordinary meals go
            // through the original post-meal bill, even if dialogue is closed.
            if (advance > 0 && _gold < meal.Gold)
            {
                PattyTalk(npc, portrait, "You don’t have enough gold for that dish.",
                    "Você não tem ouro suficiente para esse prato.", () => OpenPattyMealMenu(npc, advance, portrait));
                return;
            }
            if (TownSaveVariables.GetInt(PattyMealDue) > 0) return;
            int mask = TownSaveVariables.GetInt(PattyMeals) | (1 << index);
            TownSaveVariables.SetInt(PattyMeals, mask);
            if (advance > 0)
            {
                _gold -= meal.Gold;
                AdvanceTownStory("patty", advance);
            }
            else
            {
                TownSaveVariables.SetInt(PattyMealDue, meal.Gold);
                PersistTownProgress();
            }
            StartCoroutine(PlayPattyServeAnimation(npc));
            System.Action afterServe = advance switch
            {
                PattySecondMeal => () => PattyFirstMealAfter(npc),
                PattyChooseMeal => () => PattyTalk(npc, 0, "Here you go, enjoy.", "Aqui está. Aproveite."),
                PattyOctopusMeal => () => PattyThirdMealAfter(npc),
                PattyGhoshScene => () => PattyOctopusMealAfter(npc),
                _ => () => PattyTalk(npc, 0, "Here you are.", "Aqui está.",
                    () => PattyRegularMealAtTable(npc, index, meal.Gold))
            };
            PattyTalk(npc, portrait, meal.DescriptionEnglish, meal.DescriptionPortuguese, afterServe);
        }

        // TOWN.BIN 0x56CEBD–0x56D092.
        private void PattyRegularMealAtTable(NPCController npc, int mealIndex, int due)
        {
            var ghosh = mealIndex <= 2 && StoryStage("patty") >= StoryComplete &&
                !TownSaveVariables.GetBool(PattyGhoshCheapMealSeen) ? FindPattyGhosh() : null;
            if (!ghosh) { PattyBill(npc, due); return; }

            TownSaveVariables.SetBool(PattyGhoshCheapMealSeen, true);
            PersistTownProgress();
            StoryAsk(ghosh, "Can humans eat that?", "Isso é comida para gente?", GhoshPortrait,
                (T("It’s good, you want some?", "É bom. Quer um pouco?"), () =>
                    GhoshTalk(ghosh, "How could I possibly put that in my mouth?",
                        "Como é que eu poderia pôr isso na boca?", () => PattyBill(npc, due))),
                (T("Patty, listen to this guy.", "Patty, escuta só esse cara."), () =>
                    GhoshTalk(ghosh, "Ah, no.\nI’m not implying anything against the restaurant food...",
                        "Ah, não.\nNão quis falar mal da comida do restaurante...", () =>
                            PattyTalk(npc, 1, "... Will you please leave!", "... Faça o favor de sair!", () =>
                                GhoshTalk(ghosh, "Darn!  {name}, I’ll get back at you for this!",
                                    "Droga! {name}, vou fazer você pagar por isso!", () =>
                                    {
                                        StartCoroutine(PlayGhoshDeparture(ghosh, Vector3.right));
                                        PattyBill(npc, due);
                                    })))));
        }

        // TOWN.BIN 0x56CA36–0x56CC74 and 0x57010D–0x5702C1.
        private void PattyBill(NPCController npc, int due) =>
            PattyAsk(npc, 0, $"The bill comes to {due}G.", $"A conta fica em {due} G.",
                (T("I’m paying.", "Vou pagar."), () =>
                {
                    if (PayPattyBill(due)) PattyTalk(npc, 0, "Thank you very much.", "Muito obrigada.");
                    else PattyWashDishes(npc);
                }),
                (T("I don’t have that much.", "Não tenho tudo isso."), () => PattyWashDishes(npc)),
                (T("I’m running away.", "Vou fugir."), () =>
                {
                    TownRetailScriptFlags.CreatePersistent().SetBit(PattyUnpaidMealFlag);
                    PersistTownProgress();
                    PattyTalk(npc, 1, "Hey! Wait!", "Ei! Espere!");
                }));

        private bool PattyOutstandingMeal(NPCController npc, int due) =>
            PattyAsk(npc, 1, "What are you doing here?", "O que está fazendo aqui?",
                (T("To pay for the last meal.", "Vim pagar a última refeição."), () =>
                    PattyAsk(npc, 0, $"It’ll be {due}G.", $"São {due} G.",
                        (T("I’ll pay it.", "Vou pagar."), () =>
                        {
                            if (PayPattyBill(due))
                                PattyTalk(npc, 0, "As long as you pay, you’re a customer to us. Please have a seat, right over there.",
                                    "Se você pagar, é nosso cliente. Por favor, sente-se bem ali.");
                            else PattyTalk(npc, 1, "Get out, please!", "Saia, por favor!");
                        }),
                        (T("I haven’t got enough.", "Não tenho o suficiente."), () =>
                            PattyTalk(npc, 1, "Get out, please!", "Saia, por favor!")))),
                (T("No particular reason.", "Nada em particular."), () =>
                    PattyTalk(npc, 0, "Okay, bye.", "Certo, tchau.")));

        private bool PayPattyBill(int due)
        {
            if (due <= 0 || TownSaveVariables.GetInt(PattyMealDue) != due || _gold < due) return false;
            _gold -= due;
            ClearPattyBill();
            return true;
        }

        private void ClearPattyBill()
        {
            TownSaveVariables.SetInt(PattyMealDue, 0);
            TownRetailScriptFlags.CreatePersistent().ClearBit(PattyUnpaidMealFlag);
            PersistTownProgress();
        }

        // TOWN.BIN 0x570C20–0x570D44.
        private void PattyWashDishes(NPCController npc) =>
            PattyTalk(npc, 2, "... you haven’t got the money?\nCome with me!",
                "... você não tem o dinheiro?\nVenha comigo!", () =>
                PattyTalk(npc, 1, "Father.  He tried to eat for free.", "Pai, ele tentou comer de graça.", () =>
                {
                    var koppe = FindStoryActor("coppe");
                    var speaker = koppe ? koppe : npc;
                    void FinishDishes()
                    {
                        StoryTalk(speaker, "Scrub-a-dub.\nScrub-a-dub.\nScrub-a-dub.",
                            "Esfrega, esfrega.\nEsfrega, esfrega.\nEsfrega, esfrega.", () =>
                                StoryTalk(speaker, "That should do it.\nNext time, bring enough money for what you’re having.",
                                    "Isso deve bastar.\nDa próxima vez, traga dinheiro suficiente para o que pedir.", ClearPattyBill));
                        _dialogueSpeaker = HeroName;
                        _dialoguePortrait = null;
                    }
                    if (koppe)
                        StoryTalk(koppe, "We’ll have him wash dishes, then.", "Então vamos pôr ele para lavar a louça.", FinishDishes);
                    else FinishDishes();
                }));

        private void PattyFirstMealAfter(NPCController npc)
        {
            PattyAsk(npc, 0, "Sorry to keep you waiting.\nHere you go.", "Desculpe a demora.\nAqui está.",
                (T("Yum, Yum, Yum.", "Nhac, nhac, nhac."), () => PattySequence(npc, 0,
                    () => PattyTasteCheck(npc),
                    ("{name}, you eat it like you really enjoy it.", "{name}, você come como se estivesse gostando de verdade."),
                    ("I love to see a man chug his food down like you do.",
                        "Eu adoro ver um homem devorar a comida como você faz."),
                    ("Oh, you got some on your cheek. Let me get it for you.",
                        "Ah, você sujou a bochecha. Deixe-me limpar para você."))),
                (T("Yum, Oh this is delicious!", "Hum, está delicioso!"), () => PattySequence(npc, 0,
                    () => PattyTasteCheck(npc),
                    ("{name}, you do eat like a gentleman.", "{name}, você come como um verdadeiro cavalheiro."))));
        }

        private void PattyTasteCheck(NPCController npc) =>
            PattyAsk(npc, 0, "How does it taste?", "Como está o gosto?",
                (T("It’s delicious.", "Está delicioso."), () => PattySequence(npc, 0,
                    () => PattyCookingConfession(npc), ("Is it? Great!", "É mesmo? Que ótimo!"))),
                (T("It’s not too good.", "Não está muito bom."), () =>
                    PattyAsk(npc, 2, "Really!?\nWhat’s wrong with it?", "Sério!?\nO que há de errado?",
                        (T("I don’t think it has enough salt.", "Acho que ficou com pouco sal."), () =>
                            PattySequence(npc, 0, () => PattyCookingConfession(npc),
                                ("I see.  I guess you’re used to stronger seasoning in your food.",
                                    "Entendi. Acho que você está acostumado a temperos mais fortes."),
                                ("I’ll remember that for next time.", "Vou lembrar disso na próxima vez."))))));

        private void PattyCookingConfession(NPCController npc)
        {
            PattySequence(npc, 0, () => PattyAsk(npc, 0,
                    "How do you feel about that, {name}?", "O que você acha disso, {name}?",
                    (T("Your father is a great person, Patty.", "Seu pai é uma ótima pessoa, Patty."), () => PattySequence(npc, 0, null,
                        ("Oh... my father...?", "Ah... meu pai...?"),
                        ("Yes, of course!", "Sim, claro!"),
                        ("He's appreciated and liked by the people of this city for his great tasting cooking.",
                            "Ele é apreciado e querido pelas pessoas desta cidade por sua comida saborosa."),
                        ("Oh!  I see!", "Ah! Entendi!"),
                        ("So you’re telling me that only knowing the art of cooking is still a very wonderful thing.",
                            "Então você está dizendo que conhecer a arte de cozinhar já é algo realmente maravilhoso."),
                        ("Thank you {name}.", "Obrigada, {name}."),
                        ("I wish I could be a chef liked by everyone, just like my father...",
                            "Eu queria poder ser uma cozinheira querida por todos, igual ao meu pai..."),
                        ("Come again when we’re open.", "Volte quando estivermos abertos.")))),
                ("Cooking is really the only thing I do well...", "Cozinhar é mesmo a única coisa que faço bem..."),
                ("It's not as if I've got any hobbies, and I'm helping my father everyday from morning to evening...",
                    "Não é como se eu tivesse algum hobby, e ajudo meu pai todos os dias, da manhã até a noite..."));
        }

        private void PattyThirdMealAfter(NPCController npc)
        {
            var ghosh = FindStoryActor("patty_ghosh");
            if (!ghosh) { PattyThirdMealTasting(npc); return; }
            GhoshTalk(ghosh, "Hmmmmm.\nI’m not feeling well!\nI’m leaving.",
                "Hmmmmm.\nNão estou me sentindo bem!\nEstou indo embora.", () =>
                {
                    StartCoroutine(PlayGhoshDeparture(ghosh, Vector3.right));
                    void OnAnswer() => PattyTalk(npc, 1, "Oh no!  He hasn’t paid yet!\nMan!\nAnd I was in such a good mood!",
                        "Ah, não! Ele ainda não pagou!\nPoxa!\nE eu estava de tão bom humor!", () => PattyThirdMealTasting(npc));
                    PattyAsk(npc, 0, "Huh?  Where’d Ghosh go?", "Hã? Aonde o Ghosh foi?",
                        (T("He left.", "Ele foi embora."), OnAnswer),
                        (T("He’s escaped from reality.", "Ele fugiu da realidade."), OnAnswer));
                });
        }

        private void PattyThirdMealTasting(NPCController npc)
        {
            void TasteReply(int portrait, string en, string pt) => PattyTalk(npc, portrait, en, pt, () =>
            {
                void FinishMeal() => PattyTalk(npc, 0, "There’s no rush, take your time eating.",
                    "Não precisa ter pressa, coma com calma.");
                PattyAsk(npc, 0, "{name}, you really eat your food with enjoyment.  It feels good just watching you eat.",
                    "{name}, você come com tanto prazer. Dá gosto de ver você comer.",
                    (T("Really?", "É mesmo?"), FinishMeal),
                    (T("Would you leave me?", "Pode me deixar a sós?"), FinishMeal));
            });
            PattyAsk(npc, 0, "Oh well.\nHere’s your food.", "Bem...\nAqui está a sua comida.",
                (T("Looks good", "Parece ótimo."), () => TasteReply(0, "Thanks.", "Obrigada.")),
                (T("I’ll give it a try.", "Vou experimentar."), () =>
                    TasteReply(1, "That’s a rude thing to say...", "Que coisa grosseira de se dizer...")));
        }

        private void PattyOctopusMealAfter(NPCController npc) =>
            PattyTalk(npc, 3, "Here you are.  I’ve added wieners to your order.",
                "Aqui está. Acrescentei salsichas ao seu pedido.", () =>
                PattyAsk(npc, 3, "Look, they’re cut in the shape of an octopus.",
                    "Olhe, elas foram cortadas em forma de polvo.",
                    (T("Thank you", "Obrigado."), () => PattySequence(npc, 3, null,
                        ("I just love to watch you eat, {name}.", "Eu adoro ver você comer, {name}."),
                        ("Take your time.", "Coma com calma."))),
                    (T("I don’t want them.", "Não quero."), () => PattySequence(npc, 3, null,
                        ("Oh...\nSorry, I thought I was doing you a favor...", "Ah...\nDesculpe, achei que estava fazendo um agrado..."),
                        ("Take your time.", "Coma com calma.")))));

        private System.Collections.IEnumerator PlayPattyServeAnimation(NPCController patty)
        {
            if (!patty) yield break;
            var view = patty.GetComponent<ActorLocomotionView>();
            Vector3 direction = patty.transform.forward.sqrMagnitude > 0.001f ? patty.transform.forward : Vector3.forward;
            for (float elapsed = 0f; patty && elapsed < 0.45f; elapsed += Time.unscaledDeltaTime)
            {
                view?.Present(direction, elapsed * 5f, true);
                yield return null;
            }
            if (patty) view?.Present(Vector3.zero, 0f, false);
        }

        private NPCController FindPattyGhosh()
        {
            var ghosh = FindStoryActor("patty_ghosh");
            if (ghosh)
            {
                ghosh.gameObject.SetActive(true);
                return ghosh;
            }
            SpawnStoryResident("ghosh", "patty_ghosh", "Ghosh", default);
            return FindStoryActor("patty_ghosh");
        }

        private System.Collections.IEnumerator PlayPattyFinalScene()
        {
            if (_pattyFinalScenePlaying) yield break;
            _pattyFinalScenePlaying = true;
            yield return null;
            if (StoryRoom != RestaurantRoom || StoryStage("patty") != PattyGhoshScene ||
                !StoryNewDay("patty") || TownSaveVariables.GetInt(PattyMealDue) > 0)
            {
                _pattyFinalScenePlaying = false;
                yield break;
            }
            var patty = FindStoryActor("patty");
            if (!patty)
            {
                _pattyFinalScenePlaying = false;
                yield break;
            }
            var ghosh = FindPattyGhosh();
            if (!ghosh)
            {
                AdvanceTownStory("patty", PattyFinalTalk);
                _pattyFinalScenePlaying = false;
                yield break;
            }
            Vector3 side = Vector3.right;
            Vector3 stop = patty.transform.position + side * 1.25f;
            ghosh.transform.position = patty.transform.position + side * 4f;
            yield return PlayStoryWalk(ghosh, stop, 0.65f);
            if (!patty || !ghosh || StoryRoom != RestaurantRoom)
            {
                _pattyFinalScenePlaying = false;
                yield break;
            }
            patty.transform.rotation = Quaternion.LookRotation(side, Vector3.up);
            ghosh.transform.rotation = Quaternion.LookRotation(-side, Vector3.up);
            void LeaveRestaurant()
            {
                AdvanceTownStory("patty", PattyFinalTalk);
                StartCoroutine(PlayGhoshDeparture(ghosh, side));
            }

            // TOWN.BIN 0x56FA99–0x56FCDF.
            void AskException() => GhoshTalk(ghosh, "Can’t you make an exception for me?",
                "Não pode abrir uma exceção para mim?", () =>
                    PattyTalk(patty, 0, "Why?", "Por quê?", () =>
                        GhoshTalk(ghosh, "Hmmmmmmm.\nI’m leaving.", "Hmmmmmmm.\nVou embora.", LeaveRestaurant)));
            void DiscussKoh() => GhoshTalk(ghosh, "But the other day you prepared food for {name}.",
                "Mas outro dia você preparou comida para o {name}.", () =>
                    PattyTalk(patty, 3, "{name} is different.\nI asked my father to make an exception in {name}’s case.",
                        "Com o {name} é diferente.\nPedi ao meu pai que abrisse uma exceção para ele.", AskException));
            GhoshTalk(ghosh, "I’ll pay twice, no, three times.", "Pago o dobro, não, o triplo.", () =>
                PattySequence(patty, 0, DiscussKoh,
                    ("It’s not the money.", "Não é pelo dinheiro."),
                    ("My cooking is still not at the level where I can serve it to customers.",
                        "Minha comida ainda não está no nível que eu preciso para servir aos clientes.")));
            _pattyFinalScenePlaying = false;
        }

        // TOWN.BIN 0x56FF32–0x570079.
        private bool PattySpecialVisit(NPCController patty)
        {
            var ghosh = FindPattyGhosh();
            if (!ghosh)
                return PattyMealAdvice(patty, CompletePattyStory);
            return GhoshTalk(ghosh, "Let’s see...", "Vamos ver...", () =>
                GhoshTalk(ghosh, "Patty, I’ve changed my mind.\nI’ll have what I always have.",
                    "Patty, mudei de ideia.\nVou querer o que peço sempre.", () =>
                {
                    PattyTalk(patty, 0, "Okay, the special, right?", "Certo, o especial, não é?", () =>
                        GhoshTalk(ghosh, "And Patty, add some of your love to that.",
                            "E, Patty, acrescente um pouco do seu amor ao prato.", () =>
                                PattyTalk(patty, 3, "R...Right...", "C...Certo...", () =>
                                {
                                    CompletePattyStory();
                                    StartCoroutine(PlayGhoshDeparture(ghosh, Vector3.right));
                                })));
                }));
        }

        private void PattyWakeUpStory(NPCController npc) =>
            PattySequence(npc, 0, null, // TOWN.BIN 0x4177D7–0x417B22.
                ("Hi {name}!", "Oi, {name}!"),
                ("Get up, get up!\nIt is the start of today, yet another important day!",
                    "Levante, levante!\nMais um dia importante está começando!"),
                ("Don’t let this great day pass with you sleeping in!", "Não deixe esse dia maravilhoso passar dormindo!"),
                ("Yes, as always, you look swell!", "Sim, você está ótimo, como sempre!"),
                ("I envy the other girls.", "Tenho inveja das outras garotas."),
                ("I wanted myself to be the first girl you see when waking up, {name}.",
                    "Queria ser a primeira garota que você vê ao acordar, {name}."),
                ("To tell you the truth, I want to be here to wake you up everyday,",
                    "Para falar a verdade, queria vir acordar você todos os dias,"),
                ("but my Dad’s store is so busy.", "mas o restaurante do meu pai dá tanto trabalho."),
                ("I’ll find the time to come again, okay?", "Vou arrumar tempo para vir de novo, tudo bem?"),
                ("So I want you to visit our store too, {name}.\nBye!",
                    "Quero que você vá ao nosso restaurante também, {name}.\nTchau!"));

        private bool PattyPoolStory(NPCController npc)
        {
            PattySequence(npc, 4, null, // TOWN.BIN 0x536863–0x5368EF.
                ("Yeeeoooow!\nThis is just great, to come swimming every once in a while.",
                    "Iuuu!\nÉ tão bom vir nadar de vez em quando."),
                ("Oh, it feels good just being here along the pool side.", "Ah, é tão bom ficar aqui na beira da piscina."));
            return true;
        }

        private System.Collections.IEnumerator PlayGhoshDeparture(NPCController ghosh, Vector3 direction)
        {
            if (!ghosh) yield break;
            yield return PlayStoryWalk(ghosh, ghosh.transform.position + direction * 3f, 0.5f);
            if (ghosh) ghosh.gameObject.SetActive(false);
        }

        private void CompletePattyStory()
        {
            TownRetailScriptFlags.CreatePersistent().SetBit(PattyLoveFlag);
            AdvanceTownStory("patty", StoryComplete);
        }

        private const string MiaFirstGreetingKey = StoryPrefix + "mia.firstGreeting";
        private const int MiaFollowStartedFlag = 5217;
        private const int MiaFollowEndedFlag = 5218;
        private const int MiaLoveFlag = 5236;
        private const int MiaFollowCompletionFlag = 3465;

        private void MiaSequence(NPCController mia, int portrait, System.Action done,
            params (string English, string Portuguese)[] lines)
        {
            void ShowLine(int index)
            {
                if (index >= lines.Length)
                {
                    done?.Invoke();
                    return;
                }
                MiaTalk(mia, portrait, lines[index].English, lines[index].Portuguese,
                    () => ShowLine(index + 1));
            }
            ShowLine(0);
        }

        private bool MiaStory(NPCController npc)
        {
            const string route = "mia";
            int stage = MiaStage();
            if (StoryRoom == LibraryRoom)
            {
                if (!StoryBuilding("library"))
                    return MiaTalk(npc, 0, "I'm sorry.\nI just feel uncomfortable whenever a man approaches me like that.",
                        "Desculpe.\nFico sem jeito quando um homem se aproxima de mim assim.");
                if (stage == MiaFirstBook)
                {
                    if (!TownSaveVariables.GetBool(MiaFirstGreetingKey))
                    {
                        return MiaTalk(npc, 0, "I'm sorry.\nI just feel uncomfortable whenever a man approaches me like that.",
                            "Desculpe.\nFico sem jeito quando um homem se aproxima de mim assim.", () =>
                        {
                            TownSaveVariables.SetBool(MiaFirstGreetingKey, true);
                            PersistTownProgress();
                        });
                    }
                    return MiaFirstBookConversation(npc);
                }
                if (stage == MiaBookInterest) return MiaSecondBookConversation(npc);
                if ((stage is MiaBookAmused or MiaGhoshCollision) && StoryNewDay(route))
                {
                    StartCoroutine(PlayMiaGlassesScene());
                    return true;
                }
                if (stage == MiaGlassesOnFloor)
                    return MiaTalk(npc, 3, "My glasses...\nwhere are my glasses...", "Meus óculos...\nonde estão meus óculos...");
                if (stage == MiaGlassesReturned) return MiaGlassesReturnConversation(npc);
                if (stage == MiaAfterGlasses && StoryNewDay(route)) return MiaNameConversation(npc);
                if (stage == MiaOpenUp && StoryNewDay(route)) return MiaOpenUpConversation(npc);
                if (stage == MiaPoolInvitation && StoryNewDay(route)) return MiaPoolInvitationConversation(npc);
                if (stage == MiaLibraryFollowUpOne && StoryNewDay(route))
                {
                    MiaSequence(npc, 2, () => AdvanceTownStory(route, MiaLibraryFollowUpTwo),
                        ("Oh, hi, how are you?", "Ah, oi, como você está?"),
                        ("Recently, even when I read my favorite books, I'm just not satisfied...",
                            "Ultimamente, nem os meus livros preferidos me satisfazem..."),
                        ("Do you know why, {name}?", "Sabe por quê, {name}?"));
                    return true;
                }
                if (stage == MiaLibraryFollowUpTwo && StoryNewDay(route))
                {
                    MiaSequence(npc, 2, () => AdvanceTownStory(route, MiaFollowStarts),
                        ("Oh, hi, how are you?", "Ah, oi, como você está?"),
                        ("I think I've found something I like better than books.", "Acho que descobri algo de que gosto mais do que livros."),
                        ("Do you know what that is, {name}?", "Sabe o que é, {name}?"));
                    return true;
                }
                if ((stage == MiaFollowStarts && StoryNewDay(route)) || stage >= MiaFollowing)
                {
                    MiaSequence(npc, 2, stage == MiaFollowStarts ? StartMiaFollowing : null,
                        ("Hi! How are ya?\n......", "Oi! Como vai?\n......"),
                        ("Recently, I never come to the library to read books...", "Ultimamente, não venho à biblioteca para ler livros..."),
                        ("Do you know why I'm coming here?", "Sabe por que eu venho aqui?"));
                    return true;
                }
                if (stage == MiaOpenUp)
                    return MiaTalk(npc, 2, "Mr. {name}... you appear to me like a prince on a white horse.",
                        "Sr. {name}... você me parece um príncipe num cavalo branco.");
                if (stage >= MiaPoolInvitation)
                    return MiaTalk(npc, 2, "You look like a prince on a white horse, {name}.",
                        "Você parece um príncipe num cavalo branco, {name}.");
                return MiaTalk(npc, 0, "We meet a lot, don't we.\nYou enjoy books too, don't you?",
                    "A gente se encontra bastante, né?\nVocê também gosta de livros?");
            }
            if (StoryRoom == PoolOpenRoom)
            {
                if (stage == MiaPoolDate) return MiaPoolConversation(npc);
                if (stage > MiaPoolDate)
                {
                    MiaSequence(npc, 6, null, // TOWN.BIN 0x5366BF
                        ("Ever since you brought me here, I'm now able to come here alone.",
                            "Desde que você me trouxe aqui, já consigo vir sozinha."),
                        ("Let's... come again... together...", "Vamos... voltar aqui... juntos..."),
                        ("okay? Ow, I said it, I'm so embarrassed.", "tá? Ai, falei, que vergonha."));
                    return true;
                }
            }
            if (StoryRoom.Length == 0 && (stage is MiaFollowing or MiaFollowingReady))
            {
                const string greeting = "Oh, Hello.\nSuch a coincidence to meet at a place like this...";
                const string greetingPt = "Ah, olá.\nQue coincidência encontrar você aqui...";
                if (stage == MiaFollowing)
                    return MiaTalk(npc, 2, greeting, greetingPt, () => MiaFollowingCoincidence(npc));
                return MiaAsk(npc, 2, greeting, greetingPt,
                    (T("Yes.", "Sim."), () => MiaFollowingCoincidence(npc)),
                    (T("Why are you following me?", "Por que está me seguindo?"), () => MiaFollowingConversation(npc)));
            }
            return false;
        }

        private bool MiaFirstBookConversation(NPCController mia)
        {
            // TOWN.BIN 0x43D14E–0x43D89B.
            MiaSequence(mia, 0, () => MiaAsk(mia, 0,
                    "Don't you find me kind of boring?", "Você não me acha meio entediante?",
                    (T("No, it's quite interesting.", "Não, é bem interessante."), () => MiaSequence(mia, 2,
                        () => AdvanceTownStory("mia", MiaBookInterest),
                        ("Wow,\nI've never met anyone like you before.", "Uau,\nnunca conheci ninguém como você antes."),
                        ("Well, I must be going.", "Bem, preciso ir."))),
                    (T("You're boring.", "Você é entediante."), () => MiaTalk(mia, 0,
                        "That's right.\nThat's what everyone says.", "É verdade.\nÉ o que todos dizem."))),
                ("We meet a lot, don't we. You enjoy books too, don't you?", "Nós nos encontramos bastante, não é? Você também gosta de livros, não gosta?"),
                ("My favorite books are the novels written by Yuki Katsu.", "Meus livros favoritos são os romances escritos por Yuki Katsu."),
                ("In his novels,\nI feel I'm actually there!", "Nos romances dele,\nsinto como se estivesse lá!"),
                ("I just can't put them down!\nThey're so good that...", "Não consigo largá-los!\nSão tão bons que..."),
                ("And then...\nmumble, mumble...", "E então...\nblá, blá, blá..."),
                ("Oh, I'm sorry.\nI just get so carried away...", "Ah, desculpe.\nÉ que eu me empolgo..."),
                ("I've been often told that much of what I say doesn't make any sense.",
                    "Sempre me dizem que boa parte do que falo não faz sentido."));
            return true;
        }

        private bool MiaSecondBookConversation(NPCController mia)
        {
            // TOWN.BIN 0x43D93A–0x43E0A8.
            MiaSequence(mia, 0, () => MiaAsk(mia, 0,
                    "I'm going on again with topics that don't make sense.",
                    "Lá estou eu de novo falando de coisas que não fazem sentido.",
                    (T("It's actually pretty amusing.", "Na verdade é bem divertido."), () => MiaSequence(mia, 2,
                        () => AdvanceTownStory("mia", MiaGhoshCollision),
                        ("Wow,\nit's the first time.", "Uau,\né a primeira vez."),
                        ("The first time anyone has actually listened to what I have to say.",
                            "É a primeira vez que alguém realmente escuta o que eu tenho a dizer."),
                        ("Well, I've got to go.", "Bom, preciso ir."))),
                    (T("You're boring.", "Você é entediante."), () => MiaTalk(mia, 0,
                        "That's right.\nThat's what everyone tells me.", "É verdade.\nÉ o que todos me dizem."))),
                ("We meet a lot, don't we.\nYou enjoy books too, don't you?", "A gente se encontra bastante, né?\nVocê também gosta de livros?"),
                ("As I told you before, I enjoy reading novels by Yuki Katsu.", "Como já contei, gosto de ler os romances de Yuki Katsu."),
                ("I feel something very human in his writing.", "Acho a escrita dele muito humana."),
                ("It reminds me of being hugged by my mother when I was a child.",
                    "Lembra como minha mãe me abraçava quando eu era criança."),
                ("And then...\nmumble, mumble...", "E então...\nblá, blá, blá..."),
                ("Oh, I'm sorry.\nI just get so carried away.", "Ah, desculpe.\nÉ que eu me empolgo."));
            return true;
        }

        private bool MiaGlassesReturnConversation(NPCController mia)
        {
            // TOWN.BIN 0x43E4C3–0x43EC16.
            return MiaTalk(mia, 1, "Oh, thank you very much.\nI'm okay.",
                "Ah, muito obrigada.\nEstou bem.", () => MiaSequence(mia, 2, () => MiaAsk(mia, 0,
                    "I just said some pretty scary things, didn't I?", "Eu acabei de dizer coisas bem assustadoras, não disse?",
                    (T("Yeah, it was kind of scary", "Sim, foi meio assustador"), () => MiaTalk(mia, 4,
                        "Oh no, what should I do.\nThat was a big mistake.", "Ah, não. O que eu faço?\nFalei demais.",
                        () => AdvanceTownStory("mia", MiaAfterGlasses))),
                    (T("I wasn't listening", "Eu não estava ouvindo"), () => MiaTalk(mia, 0,
                        "Oh, good.\nWell, I've got to get going.", "Ah, que bom.\nBom, preciso ir.",
                        () => AdvanceTownStory("mia", MiaAfterGlasses)))),
                ("I can't stand people like that.\nNext time we meet, I'm going to put tacks in his shoes.",
                    "Não suporto gente assim.\nDa próxima vez, vou pôr tachinhas nos sapatos dele."),
                ("Or maybe hide his shoes.\nOr even better, I'll call in 50 orders of deluxe sushi to be paid and delivered under his name...",
                    "Ou talvez esconda os sapatos dele.\nMelhor ainda, vou encomendar 50 porções de sushi especial em nome dele e deixar que ele pague na entrega..."),
                ("mumble, mumble...", "blá, blá, blá..."),
                ("Oh, I'm sorry.\nI just get so carried away.", "Ah, desculpe.\nÉ que eu me empolgo.")));
        }

        private bool MiaNameConversation(NPCController mia)
        {
            // Both answers at 0x43ED73 point to 0x43EDAB.
            return MiaTalk(mia, 0, "Um, ahh...\nOh, I didn't get your name.", "Hum, ah...\nAh, não perguntei seu nome.", () =>
                MiaAsk(mia, 0, "My name is Mia Myria.\nWould you mind telling me your name?",
                    "Meu nome é Mia Myria.\nVocê pode me dizer o seu?",
                    (T($"It's Mr. {HeroName}", $"É Sr. {HeroName}"), () => MiaNameBook(mia)),
                    (T($"It's {HeroName}", $"É {HeroName}"), () => MiaNameBook(mia))));
        }

        private void MiaNameBook(NPCController mia)
        {
            MiaSequence(mia, 0, () => MiaAsk(mia, 0,
                    "I'm going on with a boring subject again, right?", "Estou falando de um assunto chato de novo, né?",
                    (T("Actually quite interesting.", "Na verdade, é bem interessante."), () => MiaTalk(mia, 2,
                        "!!!\nMr. {name}... you appear to me like a prince on a white horse.",
                        "!!!\nSr. {name}... você me parece um príncipe num cavalo branco.",
                        () => AdvanceTownStory("mia", MiaOpenUp))),
                    (T("You're boring.", "Você é entediante."), () => MiaTalk(mia, 0,
                        "That's right.\nBut...\nIt won't get better, will it?", "É verdade.\nMas...\nNão vai melhorar, né?"))),
                ("So it's Mr. {name}.", "Então é Sr. {name}."),
                ("The lead character in the novel called Love for Loli is also named {name}.",
                    "O protagonista do romance Amor por Loli também se chama {name}."),
                ("According to the novel,\n{name} helps a young lady he met at the library develop into a gorgeous woman.",
                    "No romance,\n{name} ajuda uma jovem que conheceu na biblioteca a se tornar uma mulher deslumbrante."),
                ("mumble, mumble...", "blá, blá, blá..."),
                ("oh, I'm sorry.\nI just get so carried away...", "Ah, desculpe.\nÉ que eu me empolgo..."));
        }

        private bool MiaOpenUpConversation(NPCController mia)
        {
            // Both replies at 0x43F5AB lead to the same topic menu.
            return MiaTalk(mia, 0, "Hello.", "Olá.", () => MiaAsk(mia, 0,
                "Umm... Ahh...\nI can't think of anything to talk about.",
                "Hum... Ah...\nNão consigo pensar em nada para dizer.",
                (T("I'm not forcing you to speak.", "Você não precisa falar."), () => MiaOpenUpTopics(mia)),
                (T("You're usually talkative.", "Você costuma falar bastante."), () => MiaOpenUpTopics(mia))));
        }

        private void MiaOpenUpTopics(NPCController mia)
        {
            MiaSequence(mia, 0, () => MiaAsk(mia, 0,
                    "But when it comes to topics that girls are likely to talk about...",
                    "Mas quando se trata de assuntos sobre os quais as garotas costumam falar...",
                    (T("Animation? Eavesdropping?", "Animação? Escutas?"), () => MiaEavesdroppingConversation(mia)),
                    (T("Talk what you want to.", "Fale do que quiser."), () => MiaOpenUpConclusion(mia, false))),
                ("Ask me anything regarding novels, cartoons or animation.",
                    "Pergunte o que quiser sobre romances, quadrinhos ou animação."),
                ("Even topics on pictures and eavesdropping.", "Até sobre filmes e escutas."));
        }

        private void MiaEavesdroppingConversation(NPCController mia)
        {
            void KohReacts()
            {
                StoryTalk(mia, "!!!", "!!!",
                    () => MiaTalk(mia, 4, "Why did I say that, the words just slipped out of my mouth.\nI'm so embarrassed...",
                        "Por que eu disse isso? As palavras simplesmente escaparam.\nQue vergonha...",
                        () => MiaOpenUpConclusion(mia, true)));
                ShowTownDialogue(HeroName, "!!!", null);
            }

            MiaSequence(mia, 0, KohReacts,
                ("All right.", "Está bem."),
                ("Regularly, I rely on demons and fairies to eavesdrop on someone, but aside from that,\nat the magic store in town of Haruka, they have this awesome miniature tapping device.",
                    "Costumo recorrer a demônios e fadas para escutar conversas, mas lá na loja de magia da cidade de Haruka tem um aparelho de escuta minúsculo, incrível."),
                ("You see, there are these bugs, found in the village of Dokoka,\nthat can communicate with each other from a distance.",
                    "Há uns insetos lá na vila de Dokoka que conseguem se comunicar a distância."),
                ("The tapping device uses these bugs.", "O aparelho de escuta usa esses insetos."),
                ("Actually, I'm already using one on you, {name}.", "Na verdade, já estou usando um para escutar você, {name}."));
        }

        private void MiaOpenUpConclusion(NPCController mia, bool startled) =>
            MiaTalk(mia, 2, (startled ? "!!!!\n" : "") + "You look like a prince on a white horse, {name}.",
                (startled ? "!!!!\n" : "") + "Você parece um príncipe num cavalo branco, {name}.",
                () => AdvanceTownStory("mia", MiaPoolInvitation));

        private bool MiaPoolInvitationConversation(NPCController mia)
        {
            if (!PoolRestored)
                return MiaTalk(mia, 2, "I'm afraid to go anyplace where there are a lot of people.",
                    "Tenho medo de ir a lugares cheios de gente.");
            MiaSequence(mia, 2, () => MiaAsk(mia, 2,
                    "Could you come to the pool with me right now?", "Você poderia vir à piscina comigo agora mesmo?",
                    (T("I'd be delighted to.", "Eu adoraria."), () => MiaTalk(mia, 2, "I'm so happy.", "Estou tão feliz!", () =>
                    {
                        AdvanceTownStory("mia", MiaPoolDate);
                        StartCoroutine(LoadInterior(PoolOpenRoom));
                    })),
                    (T("No way.", "De jeito nenhum."), () => MiaTalk(mia, 4,
                        "Oh... I understand...\nIt's not any fun coming to the pool with me anyway...",
                        "Ah... eu entendo...\nTambém não tem graça ir à piscina comigo..."))),
                ("Oh right!  {name}!\nHave you ever been in a pool before?", "Ah, é mesmo! {name}!\nVocê já foi a uma piscina?"),
                ("I went to the pool, but...\nI've got to take my glasses off, and...",
                    "Eu fui à piscina, mas...\nTenho que tirar os óculos, e..."),
                ("I'm afraid to go anyplace where there are a lot of people.",
                    "Tenho medo de ir a lugares cheios de gente."));
            return true;
        }

        private bool MiaPoolConversation(NPCController mia)
        {
            // TOWN.BIN 0x536A0A–0x536E39. The adjacent compliments at
            // 0x5360B5 belong to the Vivian scene (actor 0x0C).
            if (mia.TryGetComponent<PoolAttireView>(out _)) return MiaSwimsuitQuestion(mia);
            return MiaTalk(mia, 2, "Um, I'm going to go and get changed.", "Hum, vou me trocar.", () =>
            {
                ApplyPoolPresentation(mia);
                MiaSwimsuitQuestion(mia);
            });
        }

        private bool MiaSwimsuitQuestion(NPCController mia) =>
            MiaAsk(mia, 6, "How do you like my swimsuit?", "O que acha do meu maiô?",
                    (T("It's cute.", "É bonito."), () => MiaPoolAfterCompliment(mia)),
                    (T("You've got a poor figure.", "Seu corpo é feio."), () => MiaSequence(mia, 6, null,
                        ("Sob, sob...\nBoo hooooo!", "Snif, snif...\nBuaaa!"),
                        ("I hate you.", "Eu te odeio."),
                        ("But...\nfeel free to talk to me again.", "Mas...\npode falar comigo de novo."))));

        private void MiaPoolAfterCompliment(NPCController mia)
        {
            PoolSequence(mia, PoolMiaPortrait, () => MiaPoolGhoshConversation(mia),
                ("I'm so happy.\nIt's the first time ever.", "Estou tão feliz.\nÉ a primeira vez."),
                ("Or, should I say, I've always disliked any man saying those kinds of things to me, but with you it's different, {name}.",
                    "Quer dizer, sempre detestei ouvir esse tipo de coisa de um homem. Mas com você é diferente, {name}."),
                ("I'm so excited.", "Estou tão animada."));
        }

        private void MiaPoolGhoshConversation(NPCController mia)
        {
            var ghosh = FindStoryActor("pool_ghosh");
            if (!ghosh)
            {
                SpawnStoryResident("ghosh", "pool_ghosh", "Ghosh", default);
                ghosh = FindStoryActor("pool_ghosh");
            }
            if (!ghosh)
            {
                PoolTalk(mia, PoolMiaPortrait, "This was supposed to be a wonderful moment with you, {name}. But now I'm feeling terrible.",
                    "Este deveria ser um momento maravilhoso com você, {name}. Mas agora estou me sentindo péssima.",
                    () => AdvanceTownStory("mia", MiaLibraryFollowUpOne));
                return;
            }

            // TOWN.BIN 0x536E9D. The whole confrontation is kept page by
            // page so Ghosh's interruption, Mia's reply and his realization
            // retain their original portraits and acknowledgement cadence.
            GhoshSequence(ghosh, () =>
                PoolSequence(mia, PoolMiaPortrait, () =>
                    GhoshSequence(ghosh, () =>
                        PoolTalk(mia, PoolMiaPortrait, "No, I met you at the library!", "Não, eu encontrei você na biblioteca!", () =>
                            GhoshSequence(ghosh, () =>
                                MiaSequence(mia, 6, () =>
                                    MiaTalk(mia, 6, "Good-bye.", "Até logo.", () => AdvanceTownStory("mia", MiaLibraryFollowUpOne)),
                                    ("This was supposed to be a wonderful moment with you, {name}. But now I'm feeling terrible.",
                                        "Este deveria ser um momento maravilhoso com você, {name}. Mas agora estou me sentindo péssima."),
                                    ("I'm going home. Could you come with me again please?", "Vou para casa. Você poderia vir comigo de novo, por favor?")),
                                ("At the library? The only woman I met at the library...",
                                    "Na biblioteca? A única mulher que encontrei na biblioteca..."),
                                ("... was that gloomy girl wearing glasses. !!!",
                                    "... era aquela garota sombria de óculos. !!!"),
                                ("Come to think of it, your hairstyle and facial expression look familiar...",
                                    "Pensando bem, seu penteado e sua expressão parecem familiares..."),
                                ("No! You've got to be kidding! You're that girl with glasses?",
                                    "Não! Só pode estar brincando! Você é aquela garota de óculos?"),
                                ("You never told me you were so cute without your glasses. Oh no, what have I done!",
                                    "Você nunca me disse que era tão bonita sem os óculos. Ah não, o que eu fiz!"))),
                        ("You dislike me? The one and only Mr. Ghosh Road, ally of all beautiful women? Ha, ha.",
                            "Você não gosta de mim? O único Sr. Ghosh Road, aliado de todas as mulheres bonitas? Ha, ha."),
                        ("I don't believe you! Why this is the first time we've ever met.",
                            "Não acredito! Afinal, é a primeira vez que nos encontramos.")),
                ("Even if {name} is the most disliked man of the city, he is the person I like the most in this world!",
                    "Mesmo que {name} seja o homem mais odiado da cidade, ele é a pessoa de quem mais gosto neste mundo!"),
                ("And the person I dislike the most in this city is you!",
                    "E a pessoa de quem menos gosto nesta cidade é você!")),
            ("Hey!  {name}!\nWho's that cute girl you're with.", "Ei, {name}!\nQuem é essa garota bonita que está com você?"),
            ("If I don't know her, does that mean she just moved to this city recently?",
                "Se eu não conheço, será que ela acabou de se mudar para cá?"),
            ("I'll tell you something, Miss, that man you're with is the most disliked man of this city.",
                "Vou lhe dizer uma coisa, senhorita: esse homem com quem você está é o mais odiado desta cidade."),
            ("Hanging out with guys like that is like walking the backstreets of life.",
                "Andar com homens assim é como caminhar pelos becos da vida."),
            ("Why not come with me to discuss love!", "Por que não vem comigo para conversar sobre amor!"));
        }

        private void MiaFollowingConversation(NPCController mia)
        {
            // TOWN.BIN 0x2D511B–0x2D51B4.
            MiaTalk(mia, 4, "....\nYou caught me.", "....\nVocê me pegou.", () =>
                MiaAsk(mia, 4, "Ummm...\nIs it OK ?", "Hum...\nTudo bem por você?",
                    (T("That's fine.", "Tudo bem."), () => MiaTalk(mia, 2, "Ahhh.", "Ahhh.", CompleteMiaStory)),
                    (T("No.", "Não."), () => MiaTalk(mia, 4,
                        "I... I understand...\nAaa...\nWhoaaaa.", "Eu... eu entendo...\nAaa...\nUaaaa.", CompleteMiaStory))));
        }

        private void MiaFollowingCoincidence(NPCController mia) =>
            MiaTalk(mia, 2, "We must be destined.\nAhhh.", "Só pode ser o destino.\nAhhh.");

        private void MiaWakeUpStory(NPCController mia, bool survived)
        {
            if (!survived)
            {
                // TOWN.BIN 0x41B967–0x41BC94.
                MiaTalk(mia, 2, "Yeah, {name}, you're awake!", "Ah, {name}, você acordou!", () =>
                    MiaSequence(mia, 4, null,
                        ("Wheeeeeeew!\nPant, pant...", "Ufa!\nOfegante..."),
                        ("I think I had forgotten to breath for some time.", "Acho que fiquei um tempo sem respirar."),
                        ("I've been so so so worried.", "Fiquei tão, tão, tão preocupada."),
                        ("When the tamed monster brought you home, your face was swollen and body battered so badly.\nI almost threw you out with the garbage.",
                            "Quando o seu familiar trouxe você para casa, seu rosto estava inchado e seu corpo todo machucado.\nQuase joguei você fora com o lixo."),
                        ("Sob, sob, boo hoo.\nNow that I'm relieved, I can't keep back the tears.",
                            "Snif, snif, buá.\nAgora que estou aliviada, não consigo segurar as lágrimas."),
                        ("Boo hoo.\nSob, sob.\nNo, please don't do anything as drastic again, please.",
                            "Buá.\nSnif, snif.\nNão, por favor, nunca mais faça nada tão arriscado.")));
                return;
            }

            // TOWN.BIN 0x4171AF–0x417531.
            MiaSequence(mia, 0, () => MiaAsk(mia, 2,
                    "Ooooooh, myyyyyy!\nOh, {name}, he's soooo cute when asleep!",
                    "Aaaaai, meu Deus!\nAh, {name}, ele é tão fofo dormindo!",
                    (T("What is it!?", "O que foi!?"), () => MiaTalk(mia, 4,
                        "I...I'm sorry, {name}.\nYou looked so cute while asleep, just like a teddy bear.",
                        "D-desculpe, {name}.\nVocê estava tão fofo dormindo, feito um ursinho de pelúcia.",
                        () => MiaWakeUpFlustered(mia))),
                    (T("Shut up.", "Cale a boca."), () => MiaTalk(mia, 4,
                        "Oh...oh my goodness.\nI'm sorry, so sorry.\nBut I'm glad you've finally gotten up.",
                        "Ah... meu Deus.\nDesculpe mesmo.\nMas estou feliz que você finalmente tenha acordado.",
                        () => MiaWakeUpFlustered(mia)))),
                ("......\nUmm... Ahh...\nMorning is here.  It's time to get up.",
                    "......\nHum... Ah...\nJá amanheceu. É hora de levantar."),
                ("Please get up.\n......", "Por favor, levante.\n......"));
        }

        private void MiaWakeUpFlustered(NPCController mia)
        {
            MiaSequence(mia, 4, null,
                ("mumble, mumble...\nOh, Mr. {name}!", "blá, blá, blá...\nAh, Sr. {name}!"),
                ("Well the blood rushed to my head, and my mind just went blank.",
                    "O sangue subiu à minha cabeça e eu fiquei sem saber o que dizer."),
                ("I'm very sorry.", "Sinto muito."));
        }

        private void StartMiaFollowing()
        {
            TownRetailScriptFlags.CreatePersistent().SetBit(MiaFollowStartedFlag);
            AdvanceTownStory("mia", MiaFollowing);
        }

        private int MiaStage()
        {
            const string route = "mia";
            int stage = StoryStage(route);
            // Preserve saves made while the former abbreviated Mia route was
            // active.  New playthroughs stamp this once at stage zero, before
            // any stage can overlap the old numbering.
            const string versionKey = StoryPrefix + "mia.routeVersion";
            if (!TownSaveVariables.GetBool(versionKey))
            {
                if (stage == 1) AdvanceTownStory(route, MiaBookAmused);
                else if (stage == 2) AdvanceTownStory(route, MiaPoolInvitation);
                else if (stage == 3) AdvanceTownStory(route, MiaPoolDate);
                else if (stage == 4) AdvanceTownStory(route, MiaFollowingReady);
                TownSaveVariables.SetBool(versionKey, true);
                PersistTownProgress();
                stage = StoryStage(route);
            }
            return stage;
        }

        private void CompleteMiaStory()
        {
            TownSaveVariables.SetBool("quest.mia.complete", true);
            var flags = TownRetailScriptFlags.CreatePersistent();
            flags.SetBit(MiaFollowEndedFlag);
            flags.SetBit(MiaLoveFlag);
            flags.SetBit(MiaFollowCompletionFlag);
            AdvanceTownStory("mia", StoryComplete);
        }

        private void RecordMiaGlassesPickup(TownObject source)
        {
            if (StoryStage("mia") != MiaGlassesOnFloor) return;
            if (source) Destroy(source.gameObject);
            _miaGlassesObject = null;
            AdvanceTownStory("mia", MiaGlassesReturned);
            _message = T("You found Mia's glasses. Return them to her.", "Você encontrou os óculos da Mia. Devolva-os a ela.");
        }

        private void RecordMiaLibraryFollowVisit()
        {
            if (StoryStage("mia") != MiaFollowing) return;
            const string visitsKey = StoryPrefix + "mia.followLibraryVisits";
            int visits = TownSaveVariables.GetInt(visitsKey) + 1;
            TownSaveVariables.SetInt(visitsKey, visits);
            if (visits >= 5) AdvanceTownStory("mia", MiaFollowingReady);
            else PersistTownProgress();
        }

        private System.Collections.IEnumerator PlayMiaGlassesScene()
        {
            // Residents are instantiated after the additive interior load.
            yield return null;
            if (StoryRoom != LibraryRoom || StoryStage("mia") is not (MiaBookAmused or MiaGhoshCollision)) yield break;
            var mia = FindStoryActor("mia");
            if (!mia) yield break;

            var ghosh = FindStoryActor("mia_ghosh");
            if (!ghosh)
            {
                SpawnStoryResident("ghosh", "mia_ghosh", "Ghosh", default);
                ghosh = FindStoryActor("mia_ghosh");
            }
            if (!ghosh)
            {
                SpawnMiaGlasses(mia.transform.position + Vector3.back * 0.75f);
                AdvanceTownStory("mia", MiaGlassesOnFloor);
                MiaTalk(mia, 3, "My glasses...\nwhere are my glasses...", "Meus óculos...\nonde estão meus óculos...");
                yield break;
            }

            Vector3 miaPosition = mia.transform.position;
            ghosh.transform.position = miaPosition + Vector3.left * 2.1f;
            GhoshTalk(ghosh, "So this is the library. But it's no use to me, with my knowledge and intelligence.",
                "Então esta é a biblioteca. Mas ela não serve para mim, com todo o meu conhecimento e inteligência.",
                () => StartCoroutine(PlayMiaGlassesCollision(mia, ghosh, miaPosition)));
        }

        private System.Collections.IEnumerator PlayMiaGlassesCollision(NPCController mia, NPCController ghosh, Vector3 miaPosition)
        {
            if (!mia || !ghosh || StoryRoom != LibraryRoom) yield break;
            yield return PlayStoryWalk(ghosh, miaPosition + Vector3.right * 0.35f, 0.75f);
            if (!mia || !ghosh || StoryRoom != LibraryRoom) yield break;
            yield return PlayStoryWalk(mia, miaPosition + Vector3.back * 0.16f, 0.12f);
            if (!mia || !ghosh || StoryRoom != LibraryRoom) yield break;

            SpawnMiaGlasses(miaPosition + Vector3.back * 0.75f);
            AdvanceTownStory("mia", MiaGlassesOnFloor);
            // TOWN.BIN 0x43E27D–0x43E46E.
            MiaTalk(mia, 5, "Ahh!", "Ah!", () => GhoshSequence(ghosh, () =>
                {
                    if (!mia || StoryRoom != LibraryRoom) return;
                    if (ghosh)
                    {
                        ghosh.gameObject.SetActive(false);
                        RemoveGenerated(ghosh.gameObject);
                    }
                    MiaTalk(mia, 3, "My glasses...\nwhere are my glasses...", "Meus óculos...\nonde estão meus óculos...");
                },
                ("Oh, I didn't see you. You've got to make yourself more visible.", "Ah, não vi você. Trate de aparecer mais."),
                ("I'm on the side of all women worldwide,", "Estou do lado de todas as mulheres do mundo,"),
                ("but only those women who are [beautiful] are able to receive my love.",
                    "mas só as mulheres [bonitas] podem receber o meu amor."),
                ("Haw, haw, haw... get outta my way.", "Ha, ha, ha... saia da minha frente.")));
        }

        private System.Collections.IEnumerator RestoreMiaDroppedGlasses()
        {
            yield return null;
            if (StoryRoom != LibraryRoom || StoryStage("mia") != MiaGlassesOnFloor || _miaGlassesObject) yield break;
            var mia = FindStoryActor("mia");
            if (mia) SpawnMiaGlasses(mia.transform.position + Vector3.back * 0.75f);
        }

        private void SpawnMiaGlasses(Vector3 position)
        {
            if (_miaGlassesObject) Destroy(_miaGlassesObject);
            var glasses = new GameObject("Mia's dropped glasses");
            glasses.transform.SetParent(transform, false);
            glasses.transform.position = RetailTownActorPosition(position, 0.12f);
            var visual = glasses.AddComponent<SpriteRenderer>();
            visual.sprite = ItemDisplaySprite(ItemSystem.Kind.Glasses, 1);
            visual.sortingOrder = 8;
            glasses.transform.localScale = Vector3.one * 0.42f;
            glasses.AddComponent<AzureDreamsReimagined.Rendering.CameraFacingSprite>();
            var pickup = glasses.AddComponent<TownObject>();
            pickup.objectId = MiaGlassesObjectId;
            pickup.message = T("Mia's glasses.", "Os óculos da Mia.");
            pickup.repeatable = false;
            pickup.persistInteraction = false;
            pickup.interactionRadius = 1.2f;
            _townObjects.Add(pickup);
            _generated.Add(glasses);
            _miaGlassesObject = glasses;
        }

        private const int FurFirstMeeting = 0;
        private const int FurFirstPurchase = 1;
        private const int FurReturnCustomer = 2;
        private const int FurSecondPurchase = 3;
        private const int FurFiveFloorTalk = 4;
        private const int FurTenFloorTalk = 5;
        private const int FurMusicBox = 6;
        private const int FurGhoshScene = 7;
        private const int FurApologyTalk = 8;
        private const int FurFinalScene = 9;
        private const int FurLoveFlag = 5232; // Ft_far_love
        private const int FurQuarrelFlag = 4603; // Fp_far_kenkatyu
        private const string FurGhoshPathKey = StoryPrefix + "fur.ghoshPath";
        private const string FurPortraitPrefix = "Dialogue/FurRetail_variant";
        private const string FurDumplingsSharedKey = StoryPrefix + "fur.dumplingsShared";
        private const string FurDumplingsThanksKey = StoryPrefix + "fur.dumplingsThanks";
        private const string FurWindmillThanksKey = StoryPrefix + "fur.windmillThanks";
        private const int FurPathNone = 0;
        private const int FurPathForgiven = 1;
        private const int FurPathUnforgiven = 2;
        private const int FurPathWalkedAway = 3;
        private static readonly int[][] FurPortraitFrameDurations =
        {
            new[] { 96, 4, 8, 2 }, new[] { 2 }, new[] { 2 },
            new[] { 6, 6, 6, 6 }, new[] { 96, 4, 8, 2 }, new[] { 96, 4, 8, 2 }
        };
        private bool _furGhoshScenePlaying;
        private bool _furMusicBoxScenePlaying;

        private static int[] FurPortraitFrameDurationsFor(string portraitPath)
        {
            const string prefix = FurPortraitPrefix;
            if (string.IsNullOrEmpty(portraitPath) || !portraitPath.StartsWith(prefix, System.StringComparison.Ordinal) ||
                portraitPath.Length <= prefix.Length) return null;
            int variant = portraitPath[prefix.Length] - '0';
            return variant >= 0 && variant < FurPortraitFrameDurations.Length
                ? FurPortraitFrameDurations[variant] : null;
        }

        private static string FurPortrait(int expression) => $"{FurPortraitPrefix}{expression}_frame0";

        private bool FurTalk(NPCController npc, int expression, string en, string pt, System.Action next = null) =>
            StoryTalk(npc, en, pt, FurPortrait(expression), next);

        private bool FurAsk(NPCController npc, int expression, string en, string pt,
            params (string Label, System.Action Apply)[] choices) =>
            StoryAsk(npc, en, pt, FurPortrait(expression), choices);

        private void FurSequence(NPCController npc, int expression, System.Action done,
            params (string English, string Portuguese)[] lines)
        {
            void ShowLine(int index)
            {
                if (index >= lines.Length)
                {
                    done?.Invoke();
                    return;
                }
                FurTalk(npc, expression, lines[index].English, lines[index].Portuguese,
                    () => ShowLine(index + 1));
            }
            ShowLine(0);
        }

        private void FurOpenStore() => SetGeneralStorePage(GeneralStorePage.Services);

        private void RecordFurPurchase()
        {
            const string key = StoryPrefix + "fur.purchaseDay";
            if (TownSaveVariables.GetInt(key, -1) == StoryDay)
            {
                PersistTownProgress();
                return;
            }
            TownSaveVariables.SetInt(key, StoryDay);
            TownSaveVariables.SetInt(StoryPrefix + "fur.purchaseVisits",
                TownSaveVariables.GetInt(StoryPrefix + "fur.purchaseVisits") + 1);
            int stage = StoryStage("fur");
            if (stage == FurFirstPurchase) AdvanceTownStory("fur", FurReturnCustomer);
            else if (stage == FurSecondPurchase) AdvanceTownStory("fur", FurFiveFloorTalk);
            else PersistTownProgress();
        }

        private bool FurStory(NPCController npc)
        {
            const string route = "fur";
            int stage = StoryStage(route);
            if (stage == StoryComplete) return FurCompletedDialogue(npc);
            if (stage == FurFirstMeeting)
                return FurTalk(npc, 0, "Welcome.", "Bem-vindo.", () =>
                    {
                        AdvanceTownStory(route, FurFirstPurchase);
                        FurOpenStore();
                    });
            if (stage == FurReturnCustomer)
                return FurFirstTowerConversation(npc);
            if (stage == FurFiveFloorTalk && TownSaveVariables.GetInt(TownReputation.HighestFloorKey) >= 5)
                return FurFiveFloorConversation(npc);
            if (stage == FurTenFloorTalk && TownSaveVariables.GetInt(TownReputation.HighestFloorKey) >= 10)
                return FurTenFloorConversation(npc);
            if (stage == FurMusicBox)
            {
                StartCoroutine(PlayFurMusicBoxScene());
                return true;
            }
            if (stage == FurGhoshScene)
            {
                if (!FindStoryActor("fur_ghosh") && !FindStoryResidentPrefab("ghosh"))
                    return FurTalk(npc, 0, "Welcome.\n... so what do you want today?", "Bem-vindo.\n... então, o que você quer hoje?", FurOpenStore);
                StartCoroutine(PlayFurGhoshScene());
                return true;
            }
            if (stage == FurApologyTalk)
                return FurAfterGhoshConversation(npc);
            if (stage == FurFinalScene)
                return FurFinalConversation(npc);
            return FurTalk(npc, 0, "Welcome.\n... so what do you want today?", "Bem-vindo.\n... então, o que você quer hoje?", FurOpenStore);
        }

        private bool FurCompletedDialogue(NPCController fur)
        {
            int dumplings = FindStoryItem(QuestItemKind, OctopusDumplingId);
            // TOWN.BIN 0x410E6E–0x411569.
            if (!TownSaveVariables.GetBool(FurDumplingsSharedKey) && dumplings >= 0)
                return FurTalk(fur, 0, "Welcome.\nWhat is it?  I smell something delicious.",
                    "Bem-vindo.\nO que é isso? Senti um cheiro delicioso.", () =>
                        FurAsk(fur, 1, "You've got octopus dumplings, don't you?", "Você trouxe bolinhos de polvo, não trouxe?",
                            (T("You can have them, Fur.", "São para você, Fur."), () => GiveFurDumplings(fur, dumplings)),
                            (T("I'm not giving you any.", "Não vou dar nenhum."), () => FurSequence(fur, 1, FurOpenStore,
                                ("{name}, you sting bum.\nYou mean, lazy, (beep!), (beep!), (beep!), (beep!),",
                                    "{name}, seu pão-duro.\nSeu malvado, preguiçoso, (bipe!), (bipe!), (bipe!), (bipe!),"),
                                ("pant, pant, that should do it for today.", "Ufa, ufa... chega por hoje.")))));
            if (TownSaveVariables.GetBool(FurDumplingsSharedKey) && !TownSaveVariables.GetBool(FurDumplingsThanksKey))
                return FurTalk(fur, 0, "Welcome.  Munch, munch.\nI'm having octopus dumplings.\n... so what is it today?",
                    "Bem-vindo. Nhac, nhac.\nEstou comendo bolinhos de polvo.\n... então, o que você quer hoje?", () =>
                    {
                        TownSaveVariables.SetBool(FurDumplingsThanksKey, true);
                        PersistTownProgress();
                        FurOpenStore();
                    });
            if (!TownSaveVariables.GetBool(FurWindmillThanksKey) &&
                TownRetailScriptFlags.CreatePersistent().IsFlagSet(WindmillQuestCompleteFlag))
            {
                FurSequence(fur, 4, () =>
                    {
                        TownSaveVariables.SetBool(FurWindmillThanksKey, true);
                        PersistTownProgress();
                        FurOpenStore();
                    },
                    ("I heard from the guy at the food stand, {name}, that you went through all kinds of trouble to get the windmill fixed.",
                        "O homem da barraca me contou, {name}, que você passou por um bocado de coisa para consertar o moinho."),
                    ("Rich people with a poor past have a tendency of being greedy with their money and time, but you're really warm hearted, {name}.",
                        "Quem enriquece depois de passar necessidade costuma economizar tempo e dinheiro. Você tem um coração generoso, {name}."),
                    ("... so why are you here today?", "... então, o que trouxe você aqui hoje?"));
                return true;
            }

            return FurAsk(fur, 4, "Welcome.\nAre you here to do business, or did you come to see me?",
                "Bem-vindo.\nVeio fazer negócio ou veio me ver?",
                (T("Business, of course.", "Negócios, claro."), FurOpenStore),
                (T("Yes, I've come to see you.", "Vim ver você, sim."), () => FurTalk(fur, 3,
                    "......\nWh- what are you saying?", "......\nO-o que você está dizendo?", () => FurHeartQuestion(fur))));
        }

        private void FurHeartQuestion(NPCController fur)
        {
            // TOWN.BIN 0x410BA6–0x410E28.
            FurTalk(fur, 3, "Oh, {name}!\n......\nNo, don't look at me.", "Ah, {name}!\n......\nNão, não olhe para mim.", () =>
                FurAsk(fur, 3, "When you look at me, I think my heart skips a beat.\n... what do you think?",
                    "Quando você olha para mim, acho que meu coração falha uma batida.\n... o que você acha?",
                    (T("Should I get you a doctor?", "Quer que eu chame um médico?"), () => FurTalk(fur, 0,
                        "Chuckle.\nI like that about you, {name}.\n... but if you're saying that seriously, you've got to have your head examined.",
                        "He, he.\nÉ disso que eu gosto em você, {name}.\n... mas, se estiver falando sério, precisa examinar essa cabeça.", FurOpenStore)),
                    (T("You're joking again.", "Está brincando de novo."), () => FurTalk(fur, 4,
                        "About forty to fifty percent are.\n... so, what will it be today?",
                        "Uns quarenta a cinquenta por cento é brincadeira.\n... então, o que vai ser hoje?", FurOpenStore))));
        }

        private void GiveFurDumplings(NPCController fur, int inventoryIndex)
        {
            if (inventoryIndex < 0 || !_townInventory.ConsumeDungeonItem(inventoryIndex, QuestItemKind)) return;
            TownSaveVariables.SetBool(FurDumplingsSharedKey, true);
            PersistTownProgress();
            FurSequence(fur, 0, FurOpenStore,
                ("Really? Thanks. Appreciate it.", "Sério? Obrigada. Agradeço."),
                ("Yay, octopus dumplings. Munch, munch.", "Oba, bolinhos de polvo. Nhac, nhac."),
                ("For me to receive something from {name}.\nLife never ceases to amaze me.",
                    "Ganhar alguma coisa do {name}.\nA vida nunca para de me surpreender."),
                ("Food stand by the windmill.\nHaven't been to one of those in a while.",
                    "Uma barraca de comida perto do moinho.\nFaz tempo que não vou a uma dessas."),
                ("Back out west, I had this every day.", "Lá no oeste, eu comia isso todo dia."),
                ("Munch, munch.  This is good.\nMunch, munch.\nMunch, munch.  Thanks.", "Nhac, nhac. Que delícia.\nNhac, nhac.\nNhac, nhac. Obrigada."));
        }

        // TOWN.BIN 0x40DCC2–0x40DFEC, after the first purchase.
        private bool FurFirstTowerConversation(NPCController fur) =>
            FurAsk(fur, 0, "I hear you've started going to the tower!\nWhat's the tower like?",
                "Soube que você começou a ir à torre!\nComo ela é?",
                (T("It's going okay.", "Está indo bem."), () => FurTowerWildJoke(fur)),
                (T("Not going too well.", "Não está indo muito bem."), () => FurTowerHeightJoke(fur)));

        private void FurTowerWildJoke(NPCController fur)
        {
            void Reply() => FurTalk(fur, 1, "Of course.", "Claro.", FurContinueAfterFirstTowerTalk);
            FurTalk(fur, 0, "That's good.", "Que bom.", () =>
                FurAsk(fur, 1, "You've got that wild look, so I bet the wild monsters don't realize you're human.",
                    "Você tem um ar tão selvagem que aposto que os monstros nem percebem que você é humano.",
                    (T("You kidding, with my face?", "Está brincando com a minha cara?"), Reply),
                    (T("I do look wild, don't I?", "Tenho mesmo um ar selvagem, né?"), Reply)));
        }

        private void FurTowerHeightJoke(NPCController fur)
        {
            void Reply() => FurTalk(fur, 1, "Huh???", "Hã???", FurContinueAfterFirstTowerTalk);
            FurAsk(fur, 1, "Why?\nOh, I know.  The tower has a minimum height to enter, right?",
                "Por quê?\nAh, já sei. A torre tem altura mínima para entrar, né?",
                (T("I get bigger when excited!", "Eu cresço quando me empolgo!"), Reply),
                (T("Keep watching me!", "É só continuar me observando!"), Reply));
        }

        private void FurContinueAfterFirstTowerTalk()
        {
            AdvanceTownStory("fur", FurSecondPurchase);
            FurOpenStore();
        }

        // TOWN.BIN 0x40DFFE–0x40E72B.
        private bool FurFiveFloorConversation(NPCController fur)
        {
            void Reply() => FurTalk(fur, 1, "It looks like rather than doing better, you're bragging better.",
                "Parece que você só melhorou mesmo em se gabar.", () => FurTowerChangeQuestion(fur));
            return FurAsk(fur, 0, "How are your adventures at the tower? I hear you're doing better lately.",
                "Como estão suas aventuras na torre? Ouvi dizer que você está indo melhor ultimamente.",
                (T("I'm a genius!", "Sou um gênio!"), Reply),
                (T("I'm invincible!", "Sou invencível!"), Reply));
        }

        private void FurTowerChangeQuestion(NPCController fur) =>
            FurAsk(fur, 0, "Since you've been going to the tower, do you think you've changed?",
                "Desde que começou a ir à torre, você acha que mudou?",
                (T("I'm still the same old me.", "Continuo o mesmo de sempre."), () => FurSequence(fur, 0, () => FurTownAndFurChanged(fur),
                    ("That's right.", "É verdade."),
                    ("You haven't changed, but every time I see you, you seem a little different.", "Você continua o mesmo, mas parece um pouco diferente cada vez que eu vejo você."),
                    ("It's strange.", "É estranho."))),
                (T("I'm rich.", "Estou rico."), () => FurTalk(fur, 1, "Well, you do seem to be living a higher standard now.",
                    "Bem, parece mesmo que agora você vive melhor.", () => FurTownAndFurChanged(fur))));

        private void FurTownAndFurChanged(NPCController fur)
        {
            void Reply() => FurTalk(fur, 1, "Hey, that's rude.", "Ei, que falta de educação!", () =>
                FurAsk(fur, 0, "{name}, how do you feel about me?", "{name}, o que você acha de mim?",
                    (T("You're beautiful.", "Você é linda."), () => FurFiveFloorFinished(fur)),
                    (T("You're cute", "Você é fofa"), () => FurFiveFloorFinished(fur))));
            FurAsk(fur, 0, "The city of Monsbaiya is looking better and better...\n... and I'm becoming a better and better woman.",
                "Monsbaiya está cada vez mais bonita...\n... e eu estou me tornando uma mulher cada vez melhor.",
                (T("You're a tomboy.", "Você é uma moleca."), Reply),
                (T("Yeah, yeah, sure.", "Sei, sei, claro."), Reply));
        }

        private void FurFiveFloorFinished(NPCController fur)
        {
            void Finish() => AdvanceTownStory("fur", FurTenFloorTalk);
            FurTalk(fur, 4, "!!!!!\nWow, that was a direct answer, in your case.", "!!!!!\nUau, você foi bem direto dessa vez.", () =>
                FurAsk(fur, 0, "What's the matter, {name}.\nYou're not the same as usual. With that serious look on your face.",
                    "O que foi, {name}?\nVocê está diferente do normal, com essa cara séria.",
                    (T("Really?", "É mesmo?"), () => FurTalk(fur, 0, "See, you look a lot more serious than always.",
                        "Está vendo? Você está bem mais sério que de costume.", Finish)),
                    (T("I'm always serious!", "Eu sou sempre sério!"), () => FurTalk(fur, 1,
                        "Oh,\nyou've returned to the bratty {name} that we all know.", "Ah,\nvoltou aquele {name} irritante que todo mundo conhece.", Finish))));
        }

        // TOWN.BIN 0x40E79A–0x40EF0D.
        private bool FurTenFloorConversation(NPCController fur)
        {
            void Reply() => FurAsk(fur, 0, "Keep up the good work.", "Continue assim.",
                (T("Thanks.", "Obrigado."), () => FurTomboyConversation(fur)),
                (T("... no harsh attacks today?", "... sem piadas pesadas hoje?"), () => FurTalk(fur, 1,
                    "Nope.\nWhy?", "Não.\nPor quê?", () => FurTomboyConversation(fur))));
            return FurAsk(fur, 0, "I hear through the grapevine that you're doing pretty well.",
                "Ouvi por aí que você está indo muito bem.",
                (T("You're making me blush.", "Você está me deixando sem graça."), Reply),
                (T("That's me!  I'm terrific.", "Sou eu! Sou incrível."), Reply));
        }

        private void FurTomboyConversation(NPCController fur)
        {
            void Reply() => FurAsk(fur, 0, "What?  But I'd always say things that harm you, without even thinking.",
                "O quê? Mas eu vivia dizendo coisas que machucavam você, sem nem pensar.",
                (T("Cause you were unreasonable.", "Porque você era injusta."), () => FurTalk(fur, 3,
                    "So, {name}, you were angry because I wouldn't apologize even though I knew I was wrong.",
                    "Então, {name}, você ficou bravo porque eu sabia que estava errada e mesmo assim não pedia desculpas.",
                    () => FurTomboyResolution(fur))),
                (T("I don't get hurt that easily.", "Eu não me magoo tão fácil."), () => FurTomboyResolution(fur)));
            FurTalk(fur, 0, "Mrs. Lin says that I have become a much better lady and that I have become much more modest.",
                "A Sra. Lin disse que agora sou uma moça mais educada e bem mais modesta.", () =>
                    FurAsk(fur, 0, "Do you think so too, {name}?", "Você também acha, {name}?",
                        (T("I liked the tomboy.", "Eu gostava da moleca."), Reply),
                        (T("I like the way you are now.", "Gosto de você como está agora."), Reply)));
        }

        private void FurTomboyResolution(NPCController fur) =>
            FurAsk(fur, 3, "Huh?", "Hã?",
                (T("You're a big-hearted lady, Fur.", "Você é uma mulher de grande coração, Fur."), () => FurMusicBoxReady(fur)),
                (T("I'm a man enough.", "Sou homem de verdade."), () => FurMusicBoxReady(fur)));

        private void FurMusicBoxReady(NPCController fur)
        {
            void Reply() => FurSequence(fur, 0, () => AdvanceTownStory("fur", FurMusicBox),
                ("Whew, chuckle.", "Ufa, he, he."),
                ("That's a relief.  There was so much stress accumulating, I thought I would die.",
                    "Que alívio. Eu estava tão tensa que achei que ia morrer."),
                ("'Cuz I enjoy teasing you more than anything in the world.", "É que eu gosto de provocar você mais do que qualquer coisa no mundo."),
                ("{name}!  Luv ya!", "{name}! Te adoro!"));
            FurTalk(fur, 3, "Hmm.  {name}...", "Hum. {name}...", () =>
                FurAsk(fur, 3, "Then does that mean I can attack you with harsh jokes like before?",
                    "Então posso voltar a pegar pesado nas piadas, como antes?",
                    (T("Of course!", "Claro!"), Reply),
                    (T("But take it easy Fur.", "Mas pega leve, Fur."), Reply)));
        }

        private System.Collections.IEnumerator PlayFurMusicBoxScene()
        {
            if (_furMusicBoxScenePlaying || !InGeneralStore || StoryStage("fur") != FurMusicBox) yield break;
            _furMusicBoxScenePlaying = true;
            SetGeneralStorePage(GeneralStorePage.Closed);
            yield return null;
            var fur = FindStoryActor("fur");
            if (!fur || !InGeneralStore) { _furMusicBoxScenePlaying = false; yield break; }
            Vector3 home = fur.transform.position;
            yield return PlayStoryWalk(fur, home + Vector3.right * 0.38f, 0.28f);
            if (!fur || !InGeneralStore) { _furMusicBoxScenePlaying = false; yield break; }
            fur.transform.position = home;
            // TOWN.BIN 0x4104F6–0x410692.
            PlayRetailSound(RetailSfx.Confirm);
            FurSequence(fur, 0, () =>
                {
                    AdvanceTownStory("fur", FurGhoshScene);
                    SetGeneralStorePage(GeneralStorePage.Buy);
                    _furMusicBoxScenePlaying = false;
                },
                ("Welcome.", "Bem-vindo."),
                ("We got something re-ally rare today.  It's a mechanism called the music box.",
                    "Hoje chegou uma coisa bem rara. É um mecanismo chamado caixa de música."),
                ("Look, when you open the top like this, you can hear beautiful music.",
                    "Olhe: quando abre a tampa assim, você ouve uma música bonita."),
                ("It would be the perfect present for a girl.", "Seria o presente perfeito para uma garota."),
                ("I'm certain that whoever gets this will fall in love with you in an instant.",
                    "Tenho certeza de que quem ganhar a caixa vai se apaixonar por você na hora."));
        }

        private System.Collections.IEnumerator PlayFurGhoshScene()
        {
            if (_furGhoshScenePlaying || !InGeneralStore || StoryStage("fur") != FurGhoshScene) yield break;
            _furGhoshScenePlaying = true;
            SetGeneralStorePage(GeneralStorePage.Closed);
            yield return null;
            var fur = FindStoryActor("fur");
            if (!fur || !InGeneralStore) { _furGhoshScenePlaying = false; yield break; }
            if (!FindStoryActor("fur_ghosh")) SpawnStoryResident("ghosh", "fur_ghosh", "Ghosh", default);
            var ghosh = FindStoryActor("fur_ghosh");
            if (!ghosh) { _furGhoshScenePlaying = false; yield break; }
            ghosh.gameObject.SetActive(true);
            Vector3 stop = fur.transform.position + Vector3.left * 0.8f;
            ghosh.transform.position = stop + Vector3.left * 1.2f;
            yield return PlayStoryWalk(ghosh, stop, 0.5f);
            if (!fur || !ghosh || !InGeneralStore) { _furGhoshScenePlaying = false; yield break; }

            // TOWN.BIN 0x40B17B–0x40B7AE. Fur sets the price and Ghosh repeats it.
            FurTalk(fur, 0, "Welcome...\nbut what are you doing all the way up there?",
                "Bem-vindo...\nmas o que está fazendo aí em cima?", () => FurGhoshSale(fur, ghosh));
        }

        private void FurGhoshSale(NPCController fur, NPCController ghosh)
        {
            void GhoshNoticesKoh() => StoryAsk(ghosh,
                "Oh, {name}, I didn't notice you. You should not be in Ms. Fur's store.\nIt'll bring down the class of the store.",
                "Ah, {name}, nem tinha visto você. Você não devia entrar na loja da srta. Fur.\nVai baixar o nível da loja.",
                GhoshPortrait,
                (T("What about you?", "E você?"), () => FurGhoshStoreReply(fur, ghosh, false)),
                (T("He's blocking business, Fur.", "Ele está atrapalhando os negócios, Fur."), () => FurGhoshStoreReply(fur, ghosh, true)));

            void FurClosesSale() => FurSequence(fur, 0, () => GhoshTalk(ghosh,
                    "......\nAh, no... I'll come again.", "......\nAh, não... volto outra hora.", GhoshNoticesKoh),
                ("My, what should I do.", "Ora, o que eu faço?"),
                ("Even if it's a lie, I'm thrilled to hear those words from someone as charming as you.",
                    "Mesmo que seja mentira, fico encantada de ouvir isso de alguém tão charmoso."),
                ("Chuckle.\nI'm in receipt of 1000 G.\nThank you very much.", "He, he.\nRecebi os 1000 G.\nMuito obrigada."),
                ("Anything else?", "Mais alguma coisa?"));

            void GhoshPays() => GhoshSequence(ghosh, FurClosesSale,
                ("Only...only 1000 G.", "Só... só 1000 G."),
                ("Ha, ha, ha.  That's play money to me.  I will pay billions of Gs to see your beautiful smile.",
                    "Ha, ha, ha. Isso é trocado para mim. Pagaria bilhões de G para ver seu belo sorriso."));

            GhoshTalk(ghosh, "Ms. Fur.  When you look at me, the iris of your eyes send a ray right through my heart.",
                "Srta. Fur, quando você olha para mim, seus olhos mandam um raio direto para o meu coração.", () =>
                    FurSequence(fur, 1, GhoshPays,
                        ("Chuckle...\nYou're such a kidder,\nMr. Ghosh.", "He, he...\nO senhor é tão engraçado,\nSr. Ghosh."),
                        ("Three Cream. that will be 1000 G.", "Três cremes. São 1000 G.")));
        }

        private void FurGhoshStoreReply(NPCController fur, NPCController ghosh, bool callsOutGhosh)
        {
            string english = callsOutGhosh
                ? "Ah... no... no I've got no intention of doing that. I'm leaving."
                : "Of... of course I'm okay. How could you say that! I'm leaving.";
            string portuguese = callsOutGhosh
                ? "Ah... não... não tenho intenção de fazer isso. Estou indo embora."
                : "C... claro que estou bem. Como você pode dizer isso! Estou indo embora.";
            GhoshTalk(ghosh, english, portuguese, () =>
            {
                if (ghosh) StartCoroutine(PlayGhoshDeparture(ghosh, Vector3.left));
                FurGhoshHonestyQuestion(fur);
            });
        }

        private void FurGhoshHonestyQuestion(NPCController fur) =>
            FurAsk(fur, 0, "Welcome.", "Bem-vindo.",
                (T("Were you being honest?", "Você estava sendo sincera?"), () => FurTalk(fur, 0,
                    "When I'm speaking with a customer I dislike, I don't necessarily say what I feel, you know.",
                    "Quando falo com um cliente de quem não gosto, nem sempre digo o que sinto, sabe?",
                    () => FurGhoshLikeQuestion(fur, "Discussions might tend to be positive but more than needed.",
                        "Às vezes até exagero na simpatia."))),
                (T("Did you lie?", "Você mentiu?"), () => FurGhoshLikeQuestion(fur,
                    "When I don't like a customer, I often charge more.", "Quando não gosto de um cliente, costumo cobrar mais.")));

        private void FurGhoshLikeQuestion(NPCController fur, string english, string portuguese)
        {
            void Reply() => FurTalk(fur, 3, "Ah... yeah, I guess so.", "Ah... é, acho que sim.", () => FurGhoshInsult(fur));
            FurAsk(fur, 0, english, portuguese,
                (T("That means you like me.", "Então você gosta de mim."), Reply),
                (T("You mean you like me?", "Quer dizer que você gosta de mim?"), Reply));
        }

        private void FurGhoshInsult(NPCController fur) =>
            FurTalk(fur, 1, "... before I can ever dislike you, {name}, you're miserable, so I feel sorry for you.",
                "... antes que eu consiga antipatizar com você, {name}, sua vida já é tão triste que eu fico com pena.", () =>
                FurAsk(fur, 1, "You're ugly, short, look poor, and your father was found lying dead somewhere, and... ohh...",
                    "Você é feio, baixinho, parece pobre, e seu pai foi encontrado morto em algum lugar, e... ah...",
                    (T("Hey that's too much!", "Ei, já é demais!"), () => FurGhoshResponse(fur, true)),
                    (T("You're absolutely right.", "Você tem toda razão."), () => FurGhoshResponse(fur, false))));

        private void FurGhoshResponse(NPCController fur, bool defendedKoh)
        {
            void DemandApology() => FurTalk(fur, 1, "Shut up.If you're going to go on complaining, get out of the store!  ......",
                "Cale a boca. Se é para continuar reclamando, saia da loja! ......", () =>
                    FurTalk(fur, 2, "Done it again.\nI'm such a fool.", "Fiz de novo.\nComo sou tola.", () =>
                        FinishFurGhoshScene(FurPathNone)));

            if (defendedKoh)
            {
                FurTalk(fur, 4, "... w-what.\nwhat are you getting all excited about, {name}.",
                    "... o-o quê?\nPor que ficou tão bravo, {name}?", () =>
                        FurTalk(fur, 1, "Chuckle... who do you think you are talking back to me like that, anyway.",
                            "He, he... quem você pensa que é para falar assim comigo?", () =>
                                FurAsk(fur, 1, "Oh well, forget it.", "Ah, esquece.",
                                    (T("Forget it.", "Esquece."), () => FinishFurGhoshScene(FurPathWalkedAway)),
                                    (T("Apologize.", "Peça desculpas."), DemandApology))));
                return;
            }
            FurAsk(fur, 1, "......\n{name}, haven't you got any pride in yourself?", "......\n{name}, você não tem orgulho nenhum?",
                (T("I don't.", "Não tenho."), () => FinishFurGhoshScene(FurPathWalkedAway)),
                (T("You can apologize to me.", "Você pode pedir desculpas."), DemandApology));
        }

        private void FinishFurGhoshScene(int path)
        {
            TownSaveVariables.SetInt(FurGhoshPathKey, path);
            TownRetailScriptFlags.CreatePersistent().SetBit(FurQuarrelFlag);
            AdvanceTownStory("fur", FurApologyTalk);
            _furGhoshScenePlaying = false;
        }

        private bool FurAfterGhoshConversation(NPCController fur)
        {
            return TownSaveVariables.GetInt(FurGhoshPathKey) switch
            {
                FurPathForgiven => FurForgivenPath(fur),
                FurPathUnforgiven => FurUnforgivenPath(fur),
                FurPathWalkedAway => FurWalkedAwayPath(fur),
                _ => FurApologyStoreMenu(fur)
            };
        }

        // TOWN.BIN 0x40C078. All three replies lead to the apology.
        private bool FurApologyStoreMenu(NPCController fur)
        {
            void Reply() => FurApologyConversation(fur);
            return FurAsk(fur, 2, "Oh... {name}.\nWelcome...", "Ah... {name}.\nBem-vindo...",
                (T("I've come to buy.", "Vim comprar."), Reply),
                (T("I've come to sell.", "Vim vender."), Reply),
                (T("I'm just looking.", "Só estou olhando."), Reply));
        }

        private void FurApologyConversation(NPCController fur) =>
            FurSequence(fur, 2, () => FurAsk(fur, 2, "I'm terribly sorry.", "Sinto muito mesmo.",
                    (T("I accept your apology.", "Aceito suas desculpas."), () => FurAcceptedApology(fur)),
                    (T("I'll never forgive you.", "Nunca vou perdoar você."), () => FurNeverForgive(fur))),
                ("Why are you the same as always? Aren't you still angry? You've gotta be angry.",
                    "Por que você continua igual? Você ainda não está bravo? Devia estar bravo."),
                ("Be angry. I'm the worst, aren't I?", "Fique bravo. Eu sou a pior, não sou?"),
                ("I got caught up in our conversation and spoke before thinking, and ended up saying terrible things to you.",
                    "Eu me deixei levar pela conversa, falei antes de pensar e acabei dizendo coisas terríveis para você."));

        private void FurAcceptedApology(NPCController fur) =>
            FurSequence(fur, 4, () => FurAsk(fur, 3, "Oops, did I say something I shouldn't again?",
                    "Ops, falei de novo algo que não devia?",
                    (T("I know you were just kidding.", "Sei que estava brincando."), () => FurApologyRelief(fur)),
                    (T("Watch your mouth.", "Cuidado com o que diz."), () => FurAsk(fur, 2,
                        "Sorry, I'll be more careful.\nI'll be sure not to say anything harsh.",
                        "Desculpe, vou tomar mais cuidado.\nVou evitar dizer coisas que magoem.",
                        (T("I was kidding myself.", "Eu também estava brincando."), () => FurApologyRelief(fur)),
                        (T("I like your joke.", "Gosto das suas piadas."), () => FurApologyRelief(fur))))),
                ("Oh!  Thank you {name}.\nThat's so great.", "Ah! Obrigada, {name}.\nQue alívio."),
                ("I didn't know what to do with myself.  You're something, {name}, a real man.",
                    "Eu não sabia o que fazer. Você é mesmo especial, {name}, um homem de verdade."),
                ("You're not like the other shorties running around.", "Você não é como os outros pirralhos por aí."));

        private void FurNeverForgive(NPCController fur)
        {
            FurTalk(fur, 3, "You're right.\nIt's not something that can easily be forgiven.",
                "Você tem razão.\nÉ difícil perdoar uma coisa dessas.", () => FurSetPendingPath(FurPathUnforgiven));
        }

        private void FurApologyRelief(NPCController fur) =>
            FurTalk(fur, 0, "Hmm.  {name}.\n......\nThat's a relief, 'cuz I enjoy teasing you more than anything in the world.",
                "Hum, {name}.\n......\nQue alívio. Eu adoro provocar você mais do que qualquer coisa no mundo.",
                () => FurSetPendingPath(FurPathForgiven));

        private void FurSetPendingPath(int path)
        {
            TownSaveVariables.SetInt(FurGhoshPathKey, path);
            PersistTownProgress();
        }

        private bool FurWalkedAwayPath(NPCController fur) =>
            FurAsk(fur, 1, "What?  Whaddaya want?", "O quê? O que você quer?",
                (T("Wanna talk with you.", "Quero conversar com você."), () => FurTalk(fur, 1,
                    "Well, I've got nothing to talk to you about.", "Bem, não tenho nada para conversar com você.")),
                (T("I don't like you now.", "Não gosto de você agora."), () => FurTalk(fur, 1,
                    "I don't like you either,\n{name}.", "Eu também não gosto de você,\n{name}.",
                    () => FurSetPendingPath(FurPathUnforgiven))));

        private bool FurForgivenPath(NPCController fur) =>
            FurAsk(fur, 4, "Welcome.\nHey, looking good as always. Are you here today to shop?\n... or just to see my face?",
                "Bem-vindo. Você está bonito como sempre. Veio fazer compras... ou só ver meu rosto?",
                (T("I've come to buy.", "Vim comprar."), () => SetGeneralStorePage(GeneralStorePage.Buy)),
                (T("I've come to sell.", "Vim vender."), FurOpenStore),
                (T("I've come to see you.", "Vim ver você."), () => FurSeeYouPath(fur)));

        private void FurSeeYouPath(NPCController fur)
        {
            void Reply() => FurSequence(fur, 4, () => FurGhoshReconciliation(fur),
                ("Because I'm usually not that modest, right!?  {name}, you're really watching me.",
                    "Porque normalmente não sou tão modesta, né!? Você presta mesmo atenção em mim, {name}."),
                ("How should I say it, you don't agree with what you don't believe in, you're always so serious.",
                    "Como posso dizer? Você só concorda com o que acredita e leva tudo a sério."),
                ("That's whet is so great about you,  ... the biggest and the only thing.",
                    "É isso que eu mais gosto em você... a melhor coisa. A única, aliás."));

            FurTalk(fur, 4, "Really!?\nI know exactly how you feel.", "Sério!?\nSei bem como você se sente.", () => FurAsk(fur, 4,
                    "After we had that fight, why did you come here as always?",
                    "Depois daquela briga, por que você continuou vindo aqui como sempre?",
                    (T("I miss your joke.", "Sinto falta das suas piadas."), Reply),
                    (T("Cause I like you.", "Porque gosto de você."), Reply)));
        }

        // TOWN.BIN 0x40CC3A–0x40D39F. The second sale reflects the reconciliation.
        private void FurGhoshReconciliation(NPCController fur)
        {
            if (!FindStoryActor("fur_ghosh_reconcile")) SpawnStoryResident("ghosh", "fur_ghosh_reconcile", "Ghosh", default);
            var ghosh = FindStoryActor("fur_ghosh_reconcile");
            if (!ghosh)
            {
                FurReconciliationAfterSale(fur);
                return;
            }
            ghosh.gameObject.SetActive(true);

            void GhoshLeaves() => GhoshTalk(ghosh, "{name}!!!\nThat's gotta be a lie!  What is this world coming to!",
                "{name}!!!\nSó pode ser mentira! Onde esse mundo vai parar!?", () =>
                {
                    if (ghosh) StartCoroutine(PlayGhoshDeparture(ghosh, Vector3.left));
                    FurReconciliationAfterSale(fur);
                });

            void FurAnswersGhosh() => FurSequence(fur, 0, () =>
                    GhoshTalk(ghosh, "...... and who is this \"man\" you're talking about.",
                        "...... e quem é esse \"homem\" de quem você está falando?", () =>
                            FurTalk(fur, 4, "The person I'm looking at right now.", "A pessoa para quem estou olhando agora.", GhoshLeaves)),
                ("No, you've done nothing at all.", "Não, você não fez nada."),
                ("It's just that... my view of what a man should be has been polished.",
                    "É só que... fiquei mais exigente com o que espero de um homem."));

            void FurTakesPayment() => FurTalk(fur, 0, "I'm in receipt of 2000 G.\nThank you very much.\nAnything else?",
                "Recebi 2000 G.\nMuito obrigada.\nMais alguma coisa?", () =>
                    GhoshTalk(ghosh, "Ah, no... Have I done something to make you upset?",
                        "Ah, não... Fiz alguma coisa para deixar você chateada?", FurAnswersGhosh));

            GhoshTalk(ghosh, "Ms. Fur.  When you look at me, the iris of your eyes sends a ray right through my heart.",
                "Srta. Fur, quando você olha para mim, seus olhos mandam um raio direto para o meu coração.", () =>
                    FurTalk(fur, 0, "Is that so?\nThree Cream.\nThat will be 2000 G.",
                        "É mesmo?\nTrês cremes.\nSão 2000 G.", () =>
                            GhoshSequence(ghosh, FurTakesPayment,
                                ("Only...only 2000 G.\nHa, ha, ha.  That's play money to me.", "Só... só 2000 G.\nHa, ha, ha. Isso é trocado para mim."),
                                ("I will pay billions of Gs to see your beautiful smile.", "Pagaria bilhões de G para ver seu belo sorriso."))));
        }

        private void FurReconciliationAfterSale(NPCController fur)
        {
            void Finish() => FurFinishBranch(FurPathForgiven);
            FurAsk(fur, 4, "Welcome.  {name}.", "Bem-vindo, {name}.",
                (T("Take it easy Fur.", "Pega leve, Fur."), () => FurSequence(fur, 4, Finish,
                    ("After meeting someone with a deep heart like yours, it seemed stupid to serve people like that.",
                        "Depois de conhecer alguém com um coração como o seu, me pareceu tolice tratar as pessoas daquele jeito."),
                    ("Who do you think that someone is?  It's the person that you know most about, {name}.",
                        "Quem você acha que é? É a pessoa que você mais conhece, {name}."),
                    ("Then again, it might be the person you know least about.", "Pensando bem, pode ser a pessoa que você menos conhece."))),
                (T("Yes, I'm a \"man\".", "Sim, sou um \"homem\"."), () => FurTalk(fur, 1,
                    "You'd gross me out if you were a woman.", "Você me daria nojo se fosse uma mulher.", Finish)));
        }

        private bool FurUnforgivenPath(NPCController fur) =>
            FurAsk(fur, 1, "What?  Whaddaya want?", "O quê? O que você quer?",
                (T("Wanna talk with you.", "Quero conversar com você."), () => FurTalk(fur, 1,
                    "Well, I've got nothing to talk to you about.", "Bem, não tenho nada para conversar com você.")),
                (T("I don't like you now.", "Não gosto de você agora."), () => FurSequence(fur, 2,
                    () => FurRevisitArgument(fur),
                    ("I told you, I don't like you either, {name}.", "Já falei que também não gosto de você, {name}."),
                    ("I can't stand a man with too much pride who keeps bragging about himself, but I also can't stand a man who's got no pride.",
                        "Não suporto homem que vive se gabando, mas também não aguento quem não tem orgulho nenhum."),
                    ("I'm talking about our fight the other day.", "Estou falando da nossa briga do outro dia."))));

        private void FurRevisitArgument(NPCController fur)
        {
            void ReopenShop() => FurTalk(fur, 4, "Hi-ya {name}.\nWelcome.\n... so what do you want today?",
                "Oi, {name}!\nBem-vindo.\n... então, o que você quer hoje?", () =>
                {
                    FurFinishBranch(FurPathUnforgiven);
                    FurOpenStore();
                });
            void FinalReply() => FurSequence(fur, 0, ReopenShop,
                ("Uhh, umm...\nI...\n......", "Ah, hum...\nEu...\n......"),
                ("I'm sorry.  I'm sorry.", "Desculpe. Desculpe."));
            void AskWhy() => FurAsk(fur, 3, "What?", "O quê?",
                (T("You made me sad Fur...", "Você me deixou triste, Fur..."), FinalReply),
                (T("Because it was you...", "Porque foi você quem disse..."), FinalReply));
            void Surprised() => FurAsk(fur, 3, "Huh!?", "Hã!?",
                (T("But...", "Mas..."), AskWhy),
                (T("Just the same...", "Mesmo assim..."), AskWhy));
            FurAsk(fur, 2, "After being told off like that, how could you remain as if nothing's been said?",
                "Depois de ouvir tudo aquilo, como conseguiu agir como se nada tivesse acontecido?",
                (T("It's what everyone tells me.", "É o que todo mundo me diz."), Surprised),
                (T("I'm used to it now.", "Já me acostumei."), Surprised));
        }

        private void FurFinishBranch(int path)
        {
            TownSaveVariables.SetInt(FurGhoshPathKey, path);
            TownRetailScriptFlags.CreatePersistent().ClearBit(FurQuarrelFlag);
            AdvanceTownStory("fur", FurFinalScene);
        }

        private bool FurFinalConversation(NPCController fur)
        {
            if (TownSaveVariables.GetInt(TownReputation.HighestFloorKey) < 26)
                return FurWaitingStore(fur);
            return FurAsk(fur, 4, "Welcome.\nHow do you feel being treated like a big man?",
                "Como você se sente sendo tratado como um homem importante?",
                (T("I've always been top notch.", "Sempre fui de primeira."), () => FurFinalPages(fur)),
                (T("It's not a bad feeling.", "Até que é bom."), () => FurFinalStoreChoices(fur)));
        }

        // TOWN.BIN 0x40FB06–0x40FD6E.
        private bool FurWaitingStore(NPCController fur) =>
            FurTalk(fur, 1, "I was waiting for you, {name}.", "Estava esperando você, {name}.", () =>
                FurTalk(fur, 1, "The highlight of my day is to watch you come in, think thoroughly whether or not a 10G purchase is worthwhile, and decide not to buy it.",
                    "A melhor parte do meu dia é ver você entrar, pensar muito se vale gastar 10 G e decidir que não vai comprar.", () =>
                        FurAsk(fur, 1, "Go ahead, show me again today.", "Vamos lá, faça isso de novo hoje.",
                            (T("I've come to buy.", "Vim comprar."), () => SetGeneralStorePage(GeneralStorePage.Buy)),
                            (T("I've come to sell.", "Vim vender."), FurOpenStore),
                            (T("!!!!  I'm going home!", "!!!! Vou para casa!"), () => FurTalk(fur, 4,
                                "Aaaaah!\nYou always take me too seriously.\nI'm joking.  Just joking.  Look around, see what we have!",
                                "Aaaaah!\nVocê sempre me leva a sério demais.\nÉ brincadeira! Dê uma olhada no que temos.", FurOpenStore)))));

        // TOWN.BIN 0x41006E–0x4104A6.
        private void FurFinalPages(NPCController fur) =>
            FurSequence(fur, 4, () => FurFinalStoreChoices(fur),
                ("Well, yes.\nNo matter how badly you're put down, you don't go around getting all upset.",
                    "É mesmo.\nPor pior que falem de você, você não sai por aí todo irritado."),
                ("That's certainly one of your qualities, {name}.  That's why I can tease you without worry.",
                    "Essa é uma das suas qualidades, {name}. Por isso posso provocar você sem preocupação."));

        private void FurFinalStoreChoices(NPCController fur) =>
            FurSequence(fur, 4, () => FurAsk(fur, 0, "... so, what do you want today?", "... então, o que você quer hoje?",
                    (T("I've come to buy.", "Vim comprar."), () => CompleteFurStory(GeneralStorePage.Buy)),
                    (T("I've come to sell.", "Vim vender."), () => CompleteFurStory(GeneralStorePage.Services)),
                    (T("I'm just looking.", "Só estou olhando."), () => FurTalk(fur, 0, "Thanks a lot, come again.",
                        "Obrigada. Volte sempre.", () => CompleteFurStory(GeneralStorePage.Closed)))),
                ("But even with all that money, your stingy nature has not changed, has it?",
                    "Mas, mesmo com todo esse dinheiro, você continua mão de vaca, né?"));

        private System.Collections.IEnumerator PlayFurFinalScene()
        {
            if (_furGhoshScenePlaying || !InGeneralStore || StoryStage("fur") != FurFinalScene ||
                TownSaveVariables.GetInt(TownReputation.HighestFloorKey) < 26) yield break;
            _furGhoshScenePlaying = true;
            SetGeneralStorePage(GeneralStorePage.Closed);
            yield return null;
            var fur = FindStoryActor("fur");
            if (!fur || !InGeneralStore) { _furGhoshScenePlaying = false; yield break; }
            _furGhoshScenePlaying = false;
            FurFinalConversation(fur);
        }

        private void CompleteFurStory(GeneralStorePage page)
        {
            var flags = TownRetailScriptFlags.CreatePersistent();
            flags.ClearBit(FurQuarrelFlag);
            flags.SetBit(FurLoveFlag);
            TownSaveVariables.SetBool("quest.fur.complete", true);
            AdvanceTownStory("fur", StoryComplete);
            SetGeneralStorePage(page);
        }

        private bool FurPoolStory(NPCController fur)
        {
            // TOWN.BIN 0x535863–0x535B65.
            const int price = 100;
            void AskForPayment() => FurAsk(fur, 5, "No need to hesitate.\nFor 100 G, stare at me as long as you wish.",
                "Nem precisa pensar.\nPor 100 G, pode olhar o quanto quiser.",
                (T("That's reasonable.", "É um preço justo."), () =>
                {
                    if (_gold < price)
                    {
                        FurTalk(fur, 5, "You don't have the money.", "Você não tem dinheiro suficiente.");
                        return;
                    }
                    _gold -= price;
                    PersistTownProgress();
                    FurTalk(fur, 5, "Get out of here,\nof course I'm serious.\n(Fur takes 100 G.)",
                        "Ora essa,\nclaro que estou falando sério.\n(Fur pega 100 G.)");
                }),
                (T("Why should I pay!", "Por que eu deveria pagar!?"), () => FurTalk(fur, 5,
                    "You're not only a pervert, you're also stingy!", "Além de tarado, você é mão de vaca!")));

            return FurAsk(fur, 5, "Oh, it's you, {name}.\nSo you came to see me in my swimsuit.",
                "Ah, é você, {name}.\nEntão veio me ver de maiô.",
                (T("That's right.", "Isso mesmo."), () => FurTalk(fur, 5,
                    "Well, that's natural.\nFor the privilege of viewing this beautiful body, you'd wanna pay me 100 G,\nwouldn't you?",
                    "Ora, claro.\nPelo privilégio de admirar este corpo lindo, você pagaria 100 G,\nnão pagaria?", AskForPayment)),
                (T("Nope.", "Não."), AskForPayment));
        }

        private void FurWakeUpStory(NPCController fur, bool survived)
        {
            if (!survived)
            {
                // TOWN.BIN 0x41B5E4–0x41B8FD.
                FurTalk(fur, 3, "Oh, {name}!\nYou're awake...\nThank goodness.", "Ah, {name}!\nVocê acordou...\nAinda bem.", () =>
                    FurSequence(fur, 0, () => FurTalk(fur, 3,
                            "{name}, you've got to take better care of yourself.", "{name}, você precisa se cuidar melhor."),
                        ("You'd better thank that tamed monster.\nHe dragged you all the way back here.",
                            "É bom agradecer ao seu familiar.\nEle arrastou você até aqui."),
                        ("If not for him, you'd be wrecked in that tower by now.",
                            "Sem ele, você estaria acabado lá na torre."),
                        ("That would mean good-bye with you without even the chance to look after your wounds!",
                            "Eu teria que me despedir de você sem nem poder cuidar dos seus ferimentos!"),
                        ("As stupid as you are, {name}, I would never forgive you for letting something stupid like that happen to you.\nI'm not kidding...",
                            "Por mais bobo que seja, {name}, eu nunca perdoaria você por deixar acontecer uma coisa dessas.\nEstou falando sério...")));
                return;
            }

            if (TownSaveVariables.GetInt(TownReputation.HighestFloorKey) >= 39 &&
                !TownSaveVariables.GetBool(BeldoClearedKey))
            {
                FurWakeUpBeforeBeldo(fur);
                return;
            }

            // TOWN.BIN 0x416BDB–0x416FAB.
            void GoToTheTower() => FurSequence(fur, 0, null,
                ("You're going to the tower today too, aren't you?\nYou're not the brightest,\nwhich worries me a bit.",
                    "Vai à torre hoje também, né?\nVocê não é dos mais espertos,\ne isso me preocupa um pouco."),
                ("Go out and make some cash!", "Vá lá e ganhe um bom dinheiro!"));
            FurTalk(fur, 0, "Good Morning, {name}!\nYou've got to get up!\n......", "Bom dia, {name}!\nÉ hora de levantar!\n......", () =>
                FurAsk(fur, 1, "Hey! {name}!\nGet up or your brain's going to start melting!\nYou're pretty dull as it is, do you want that to get worse?",
                    "Ei, {name}!\nLevante antes que seu cérebro derreta!\nVocê já é meio lerdo. Quer piorar?",
                    (T("Who are you calling dumb!?", "Quem você está chamando de burro!?"), () => FurTalk(fur, 4,
                        "Oh!  Were you listening?", "Ah! Estava ouvindo?", GoToTheTower)),
                    (T("Okay, okay, I'll get up.", "Tá bom, já vou levantar."), () => FurTalk(fur, 1,
                        "You realize you've got a cutie like me closing shop and coming here to wake you up,\nand still you continue to sleep!",
                        "Uma moça linda como eu fecha a loja e vem até aqui acordar você,\ne você ainda fica dormindo!", GoToTheTower))));
        }

        private void FurWakeUpBeforeBeldo(NPCController fur)
        {
            // TOWN.BIN 0x41AA65–0x41AC64.
            void Reply() => FurTalk(fur, 4, "{name}!", "{name}!");
            FurSequence(fur, 3, () => FurAsk(fur, 3,
                    "{name}, you mind if I stuck closer to you for a little while?",
                    "{name}, se importa se eu ficar mais perto de você por um tempo?",
                    (T("I mind, stay where you are.", "Me importo. Fique aí."), Reply),
                    (T("No, I don't mind, go ahead.", "Claro, pode chegar mais perto."), Reply)),
                ("Looks like it won't be long until you confront Beldo.\n...", "Parece que falta pouco para você enfrentar Beldo.\n..."),
                ("Come on, why should I have to worry about this guy...", "Puxa, por que é que eu tenho que me preocupar tanto com esse sujeito..."),
                ("You realize how difficult it's going to be for me to wait for you downstairs?", "Sabe como vai ser difícil ficar lá embaixo esperando por você?"));
        }

    }
}
