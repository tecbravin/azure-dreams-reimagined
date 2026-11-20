using UnityEngine;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private bool RacetrackResidentDialogue(NPCController npc)
        {
            switch (npc.npcId)
            {
                case "race_clerk":
                    if (BoughtRaceTickets > 0)
                    {
                        _raceScreen = RaceScreen.Tickets;
                        return true;
                    }
                    // TOWN.BIN 0x4E2284–0x4E2740.
                    return StoryAsk(npc, "Would you like to buy some betting tickets?",
                        "Gostaria de comprar bilhetes de aposta?",
                        (T("Yes I’m buying some.", "Sim, vou comprar."), () =>
                        {
                            if (_gold < RaceTicketPrice)
                                StoryTalk(npc, "To buy horse race betting tickets, you would need a minimum of 100 G.",
                                    "Para comprar bilhetes de aposta nas corridas, você precisa de pelo menos 100 G.");
                            else _raceScreen = RaceScreen.Tickets;
                        }),
                        (T("I’m only joking", "Só estou brincando."), () =>
                            StoryTalk(npc, "Please buy some next time, okay?", "Compre alguns na próxima vez, está bem?")));
                case "race_silver":
                    if (TownSaveVariables.GetBool(RaceRecord))
                    {
                        TownSaveVariables.SetBool(RaceRecord, false);
                        PersistTownProgress();
                        return TownDialogueSequence(npc, SilverPortrait, () => SilverRacetrackMenu(npc),
                            ("A new record?\nThat’s pretty awesome.", "Um novo recorde?\nIsso é impressionante."),
                            ("Your racing abilities remind me of myself, during my younger days.",
                                "Sua habilidade nas corridas me lembra de mim mesmo quando era jovem."));
                    }
                    return SilverRacetrackMenu(npc);
                case "race_bet": return RaceBetDialogue(npc);
                case "race_husband": return RaceHusbandDialogue(npc);
            }
            if (!npc.npcId.StartsWith("race_nyuel_", System.StringComparison.Ordinal) ||
                !int.TryParse(npc.npcId.Substring("race_nyuel_".Length), out int horse) ||
                (uint)horse >= (uint)RaceNames.Length) return false;
            var silver = FindStoryActor("race_silver");
            return SilverTalk(silver ? silver : npc, $"She’s No. {horse + 1}, {RaceNames[horse]}.",
                $"Ela é a número {horse + 1}, {RaceNames[horse]}.");
        }

        // TOWN.BIN 0x4E276A–0x4E28EE.
        private bool SilverRacetrackMenu(NPCController npc) =>
            SilverAsk(npc, "Hi, {name}.\nLook, isn’t it a beautiful Monster racetrack?\nThank you so much.\nWhat are you going to do today?\nDo you want to get on a horse?",
                "Oi, {name}.\nVeja, que bela pista de corrida de monstros, não acha?\nMuito obrigado.\nO que vai fazer hoje?\nQuer montar um cavalo?",
                (T("I’m going to race.", "Vou correr."), () =>
                    SilverTalk(npc, "Good luck!", "Boa sorte!", () => BeginNyuelRace(false))),
                (T("I’ll just watch.", "Só vou assistir."), () => BeginNyuelRace(true)),
                (T("Show me how to ride a horse.", "Ensine-me a montar um cavalo."), () => ShowNyuelInstructions(npc)),
                (T("Not today.", "Hoje não."), () => SilverTalk(npc,
                    "I see. Well, if you need me for anything, let me know.",
                    "Entendo. Bem, se precisar de alguma coisa, me avise.")));

        private void ShowNyuelInstructions(NPCController npc)
        {
            string run = _runAction?.GetBindingDisplayString() ?? "Shift";
            string jump = _wait?.GetBindingDisplayString() ?? "Space";
            TownDialogueSequence(npc, SilverPortrait, () => SilverRacetrackMenu(npc),
                ($"Press {run} to move forward, and down to move backwards.",
                    $"Aperte {run} para avançar e para baixo para recuar."),
                ($"If you keep pressing the button, the horse will trot slowly, and if you quickly push the button several times, the horse will trot faster.",
                    "Se mantiver o botão apertado, o cavalo trota devagar. Se apertar várias vezes rapidamente, ele trota mais depressa."),
                ($"Press {jump} for the horse to jump. To move left or right, press left or right on the Directional button.",
                    $"Aperte {jump} para o cavalo pular. Para ir à esquerda ou à direita, aperte a direção correspondente."));
        }

        // TOWN.BIN 0x4E1814–0x4E1F15. Bet reacts to Koh's latest race.
        private bool RaceBetDialogue(NPCController npc)
        {
            int place = TownSaveVariables.GetInt(RaceLastPlace);
            if (place == 1)
                return TownDialogueSequence(npc, null, null,
                    ("Yesss! You’re the best!\nYou’re going to be huge some day.", "Isso! Você é o melhor!\nAinda vai ser alguém importante."),
                    ("At the same time, this shows my gambler instincts are not that rusty after all.",
                        "Ao mesmo tempo, isso mostra que meus instintos de apostador não estão tão enferrujados."),
                    ("Thanks, bro.\nNow I can return home with my head high.", "Obrigado, irmão.\nAgora posso voltar para casa de cabeça erguida."));
            if (place > 1)
                return TownDialogueSequence(npc, null, null,
                    ("It’s okay, bro.\nA person cannot grow until he knows the humiliation of losing.\nDon’t worry about it!",
                        "Tudo bem, irmão.\nUma pessoa só cresce quando conhece a humilhação de perder.\nNão se preocupe!"),
                    ("All the cash I had didn’t amount to much anyway.\n......sniff......",
                        "Todo o dinheiro que eu tinha nem era tanto assim.\n......snif......"),
                    ("...sniff...weep... boo hooo, awwwwww!", "...snif...snif... buááá, aaah!"));
            return TownDialogueSequence(npc, null, null,
                ("Whew.\nMe,of all people, to lose again.", "Ufa.\nLogo eu, perder de novo."),
                ("The problem with that last race was not with the monsters! It was the jockey that was shameful!",
                    "O problema da última corrida não foram os monstros! O jóquei é que foi uma vergonha!"),
                ("’Cause there’s no way I could be wrong with my instincts as a gambler.",
                    "Porque não há como meus instintos de apostador estarem errados."),
                ("Are you going to be in today’s race?\nIf you are, I’m gonna bet on you.",
                    "Vai participar da corrida de hoje?\nSe for, vou apostar em você."),
                ("I’m betting everything!\nI’m what you’d call a genuine gambler.\nI’m counting on ya, bro!",
                    "Vou apostar tudo!\nSou o que chamam de um verdadeiro apostador.\nConto com você, irmão!"));
        }

        // TOWN.BIN 0x4E1F3D–0x4E2244.
        private bool RaceHusbandDialogue(NPCController npc) =>
            TownDialogueSequence(npc, null, () => StoryAsk(npc,
                "Oh, it’s only you, {name}, don’t scare me like that!\nI thought you were my wife.",
                "Ah, é só você, {name}. Não me assuste assim!\nAchei que fosse minha esposa.",
                (T("...keep trying", "...continue tentando."), () => StoryTalk(npc,
                    "Of course!", "Claro!")),
                (T("Why don’t quit if scared?", "Por que não desiste, se está com medo?"), () =>
                    TownDialogueSequence(npc, null, null,
                        ("What did you say!?\nI’m a man, and a man’s got to do what a man’s got to do!",
                            "O que você disse!?\nSou homem, e um homem tem que fazer o que tem que fazer!"),
                        ("You keep trying.", "Continue tentando.")))),
                ("Aaaaaaah!\nForgive me, Naco!", "Aaaaaaah!\nMe perdoe, Naco!"),
                ("????", "????"));
    }
}
