using UnityEngine;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string PattyMeals = StoryPrefix + "patty.meals";
        private static readonly (string English, string Portuguese, int Gold)[] RestaurantMeals =
        {
            ("Rice", "Arroz", 20),
            ("Soy Beans", "Soja fermentada", 30),
            ("Tofu", "Tofu", 40),
            ("Sandsand", "Sanduíche de peixe da areia", 60),
            ("Cutlet", "Costeleta empanada", 70),
            ("Crystal Curry", "Curry de cristal", 80),
            ("Spiral Rice", "Arroz em espiral", 90),
            ("Chicken", "Frango yakitori", 100),
            ("Shining Prawn", "Camarão Brilhante", 150),
            ("Beef Rice", "Arroz com carne", 290),
            ("Special", "Especial", 300),
            ("Red Sushi", "Sushi vermelho", 500),
            ("Blue Sushi", "Sushi azul", 500),
            ("Yellow Sushi", "Sushi amarelo", 500),
            ("Green Sushi", "Sushi verde", 500)
        };
        private const int ShiningPrawn = 8;

        private bool PattyStory(NPCController npc)
        {
            const string route = "patty";
            int stage = StoryStage(route);
            if (StoryRoom != RestaurantRoom) return false;
            if (stage == 1 && StoryNewDay(route))
                return StoryAsk(npc, "We're closed today, but I could cook you something. The Shining Prawn is my specialty.",
                    "Estamos fechados hoje, mas posso preparar algo para você. O Camarão Brilhante é minha especialidade.",
                    (T("Order Shining Prawn", "Pedir Camarão Brilhante"), () => BuyPattyMeal(npc, ShiningPrawn, 2)),
                    (T("Come back later", "Voltar depois"), null));
            if (stage == 2 && StoryNewDay(route))
                return StoryAsk(npc, "Ghosh heard us talking. Could we keep yesterday's meal between us?",
                    "Ghosh ouviu nossa conversa. Podemos guardar segredo sobre o almoço de ontem?",
                    (T("It's our secret", "É nosso segredo"), () => BuyPattyMeal(npc, ShiningPrawn, 3)),
                    (T("Come back later", "Voltar depois"), null));
            if (stage is 3 or 4 && StoryNewDay(route))
                return StoryTalk(npc, stage == 3 ? "Ghosh walked out without paying. I won't cook a special meal for him after that." :
                    "{name}, you always made me feel that my cooking mattered. I look forward to seeing you every day.",
                    stage == 3 ? "Ghosh saiu sem pagar. Depois disso, não vou preparar nada especial para ele." :
                    "{name}, você sempre valorizou minha comida. Fico esperando sua visita todos os dias.",
                    () => AdvanceTownStory(route, stage == 3 ? 4 : StoryComplete));
            var choices = new (string Label, System.Action Apply)[RestaurantMeals.Length + 1];
            for (int i = 0; i < RestaurantMeals.Length; i++)
            {
                int mealIndex = i;
                var meal = RestaurantMeals[i];
                choices[i] = ($"{T(meal.English, meal.Portuguese)}  ·  {meal.Gold} G", () => BuyPattyMeal(npc, mealIndex));
            }
            choices[^1] = (T("Leave", "Sair"), null);
            return StoryAsk(npc, "What would you like to order?", "O que você gostaria de pedir?", choices);
        }

        private void BuyPattyMeal(NPCController npc, int index, int advance = 0)
        {
            var meal = RestaurantMeals[index];
            if (_gold < meal.Gold)
            {
                StoryTalk(npc, "You don't have enough gold for that dish.", "Você não tem ouro suficiente para esse prato.");
                return;
            }
            _gold -= meal.Gold;
            int mask = TownSaveVariables.GetInt(PattyMeals) | (1 << index);
            TownSaveVariables.SetInt(PattyMeals, mask);
            if (advance > 0) AdvanceTownStory("patty", advance);
            else if (StoryStage("patty") == 0 && mask == (1 << RestaurantMeals.Length) - 1) AdvanceTownStory("patty", 1);
            else PersistTownProgress();
            StoryTalk(npc, $"Here is your {meal.English}. Enjoy!", $"Aqui está seu pedido: {meal.Portuguese}. Bom apetite!");
        }

        private bool MiaStory(NPCController npc)
        {
            const string route = "mia";
            int stage = StoryStage(route);
            if (!StoryBuilding("library") || stage == StoryComplete) return false;
            if (StoryRoom == LibraryRoom && StoryNewDay(route))
            {
                if (stage == 0)
                    return StoryAsk(npc, "Most visitors never ask what I'm reading.", "Quase ninguém pergunta o que estou lendo.",
                        (T("Tell me about the book", "Conte-me sobre o livro"), () => AdvanceTownStory(route, 1)),
                        (T("Leave her to read", "Deixá-la ler"), null));
                if (stage == 1)
                    return StoryAsk(npc, "Ghosh knocked my glasses off. I can't see where they went.", "Ghosh derrubou meus óculos. Não consigo enxergar onde caíram.",
                        (T("Pick up and return her glasses", "Pegar e devolver os óculos"), () => AdvanceTownStory(route, 2)),
                        (T("Leave", "Sair"), null));
                if (stage == 2 && PoolRestored)
                    return StoryAsk(npc, "Would you go to the pool with me sometime?", "Você iria à piscina comigo algum dia?",
                        (T("I'd be delighted", "Eu adoraria"), () =>
                        {
                            AdvanceTownStory(route, 3);
                            StartCoroutine(LoadInterior(PoolOpenRoom));
                        }), (T("Maybe later", "Talvez depois"), null));
                if (stage == 2)
                    return StoryTalk(npc, "I'm curious what you do when you're not in the library.",
                        "Tenho curiosidade de saber o que você faz quando não está na biblioteca.", () => AdvanceTownStory(route, 4));
            }
            if (stage == 3 && StoryRoom == PoolOpenRoom)
                return StoryTalk(npc, "I was nervous about coming here, but I'm glad I did.", "Eu estava nervosa, mas fico feliz de ter vindo.",
                    () => AdvanceTownStory(route, 4));
            if (stage == 4 && StoryRoom.Length == 0 && StoryNewDay(route))
                return StoryAsk(npc, "You noticed me outside the library? I wanted to see you again.",
                    "Você me viu do lado de fora da biblioteca? Eu queria ver você de novo.",
                    (T("Why are you following me?", "Por que está me seguindo?"), () =>
                        StoryTalk(npc, "I wasn't sure how to tell you. I like you, {name}.", "Eu não sabia como dizer. Eu gosto de você, {name}.",
                            () => AdvanceTownStory(route, StoryComplete))),
                    (T("Talk later", "Conversar depois"), null));
            return false;
        }

        private void RecordFurPurchase()
        {
            const string key = StoryPrefix + "fur.purchaseDay";
            if (TownSaveVariables.GetInt(key, -1) == StoryDay) return;
            TownSaveVariables.SetInt(key, StoryDay);
            TownSaveVariables.SetInt(StoryPrefix + "fur.purchaseVisits",
                TownSaveVariables.GetInt(StoryPrefix + "fur.purchaseVisits") + 1);
        }

        private bool FurStory(NPCController npc)
        {
            const string route = "fur";
            int stage = StoryStage(route);
            if (!StoryNewDay(route) || stage == StoryComplete) return false;
            if (stage == 0 && TownSaveVariables.GetInt(StoryPrefix + "fur.purchaseVisits") >= 3)
                return StoryAsk(npc, "Ghosh was here making fun of you. I laughed along. It was unkind, wasn't it?",
                    "Ghosh passou aqui e fez piada de você. Eu ri junto. Foi maldade, não foi?",
                    (T("You hurt my feelings", "Você me magoou"), () => AdvanceTownStory(route, 1)),
                    (T("Open the shop", "Abrir a loja"), () => SetGeneralStorePage(GeneralStorePage.Services)));
            if (stage == 1)
                return StoryAsk(npc, "I owe you an apology. Did you come here just to shop?",
                    "Estou te devendo um pedido de desculpas. Você veio só para comprar?",
                    (T("I came to see you", "Vim ver você"), () => AdvanceTownStory(route, 2)),
                    (T("I came to shop", "Vim comprar"), () => SetGeneralStorePage(GeneralStorePage.Services)));
            if (stage == 2)
                return StoryTalk(npc, "I told Ghosh to stop coming around just to insult people. I'm always happy when you visit, though.",
                    "Disse ao Ghosh para parar de aparecer só para insultar os outros. Mas eu sempre fico feliz quando você vem.",
                    () => AdvanceTownStory(route, StoryComplete));
            return false;
        }

        private bool SelfiTownStory(NPCController npc)
        {
            const string route = "selfi";
            int stage = StoryStage(route);
            if (StoryRoom != RhodesHouse) return false;
            if (stage == 0)
                return StoryTalk(npc, "Ghosh says the Tower is too dangerous for me. You'll see me up there anyway.",
                    "Ghosh diz que a Torre é perigosa demais para mim. Mesmo assim, você vai me ver lá em cima.",
                    () => AdvanceTownStory(route, 1));
            if (stage == 3)
                return StoryAsk(npc, "Here. Take this egg for helping me with the Pulunpa.",
                    "Aqui. Aceite este ovo por ter me ajudado com o Pulunpa.",
                    (T("Take the egg", "Aceitar o ovo"), () =>
                    {
                        if (!_townInventory.HasSpace())
                        {
                            StoryTalk(npc, "Make room in your bag first.", "Libere um espaço na bolsa primeiro.");
                            return;
                        }
                        _townInventory.AddDungeonItem(MonsterEgg.Create((byte)MonsterSpecies.PULUNPA, true));
                        AdvanceTownStory(route, 4);
                    }), (T("Later", "Depois"), null));
            if (stage is 5 or 6 && StoryNewDay(route))
                return StoryAsk(npc, "I didn't even stop to think when I saw the U-Boat coming for you. Why did I do that?",
                    "Nem parei para pensar quando vi o U-Boat indo atrás de você. Por que fiz isso?",
                    (T("Because you like me", "Porque você gosta de mim"), () =>
                    {
                        AdvanceTownStory(route, stage == 5 ? 6 : StoryComplete);
                        StoryTalk(npc, stage == 5 ? "Ha! You certainly think highly of yourself." : "All right. Maybe you were right, {name}.",
                            stage == 5 ? "Ha! Você se acha mesmo, hein?" : "Está bem. Talvez você estivesse certo, {name}.");
                    }), (T("Talk later", "Conversar depois"), null));
            return false;
        }
    }
}
