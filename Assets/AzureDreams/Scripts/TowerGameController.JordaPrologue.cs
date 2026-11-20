using System;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private enum BlessingCue
        {
            None, Impact, Offer, Basics, BasicsCheck, RepeatBasics, NextTopic,
            Secrets, SecretsCheck, Scold, RepeatSecrets, NoMoreRepeats, Scary,
            ScaryChoice, Decline, DepartureCheck, Stubborn, TowerChoice, PitaReceipt
        }

        private readonly struct BlessingPage
        {
            internal readonly (string Speaker, string En, string Pt) Dialogue;
            internal readonly BlessingCue Cue;

            internal BlessingPage(string speaker, string english, string portuguese, BlessingCue cue = BlessingCue.None)
            {
                Dialogue = (speaker, english, portuguese);
                Cue = cue;
            }
        }

        private bool _blessingRepeatingBasics, _blessingRepeatingSecrets;
        private bool _blessingPitaReceiptShown;

        // TOWN.BIN 0x412F40–0x4153C9. Both lessons use the original
        // acknowledgement pages again when Koh asks Jorda to repeat them.
        private static readonly BlessingPage[] BlessingPages =
        {
            new("Wreath", "Thanks again for waking him up every morning...", "Obrigada mais uma vez por acordá-lo todas as manhãs..."),
            new("Nico", "Don’t mention it. He will never get up until I get a good kick into him.", "De nada. Ele nunca levanta até eu dar um bom chute nele."),
            new("Jorda", "Ho, ho, ho, ho. You’ve grown up considerably since I last saw you.", "Ho, ho, ho, ho. Você cresceu bastante desde a última vez que eu o vi."),
            new("Jorda", "Cause you never come to the temple at all, do you?", "É que você nunca aparece no templo, não é?"),
            new("Jorda", "I hear from Wreath that you want to go to the monster tower.", "Wreath me disse que você quer ir à Torre dos Monstros."),
            new("Jorda", "Ho, ho, ho, ho.", "Ho, ho, ho, ho."),
            new("Jorda", "You’re fifteen today. You’re old enough now to go to the tower.", "Você faz quinze anos hoje. Já tem idade para ir à torre."),
            new("Jorda", "Here, just a moment. I’ve got a prayer for you.", "Espere um instante. Tenho uma oração para você."),
            new("Jorda", "Mumble, mumble... Mumble, mumble...", "Resmungo, resmungo... Resmungo, resmungo..."),
            new("Jorda", "There.", "Pronto."),
            new("", "Bonk!", "Toc!", BlessingCue.Impact),
            new("Jorda", "There you go. We’re done.", "Pronto. Terminamos."),
            new("Jorda", "What’s that you say... You look as if you want to go to the tower right this moment.",
                "O que foi... Parece que você quer ir à torre agora mesmo."),
            new("Jorda", "But... the tower is dangerous.", "Mas... a torre é perigosa."),
            new("Jorda", "Do you want me to give you information on the specifics of the tower?",
                "Quer que eu explique como funciona a torre?", BlessingCue.Offer),
            new("Jorda", "I’m going to teach it to you thoroughly, so listen up.", "Vou explicar tudo direitinho. Preste atenção."),
            new("Jorda", "When entering the dungeon of the tower, you’d be able to obtain rare items and monster eggs.",
                "Ao entrar na masmorra da torre, você pode encontrar itens raros e ovos de monstros.", BlessingCue.Basics),
            new("Jorda", "If you bring that back with you, you’d be able to exchange these for money.",
                "Se trouxer essas coisas de volta, poderá vendê-las por dinheiro."),
            new("Jorda", "But the tower is a monster nest.", "Mas a torre é um ninho de monstros."),
            new("Jorda", "Danger will always follow you. You may be an adult now, but in the eyes of the veteran hunters, you’re still a kid.",
                "O perigo vai acompanhá-lo sempre. Você pode ser adulto agora, mas, aos olhos dos caçadores veteranos, ainda é uma criança."),
            new("Jorda", "Whether or not you can survive the dungeon all depends on your ability to control the monsters.",
                "Sobreviver na masmorra depende da sua habilidade de controlar os monstros."),
            new("Jorda", "That’s a quality your ancestors had, so you were born with those skills.",
                "Seus antepassados tinham esse dom. Você nasceu com ele também."),
            new("Jorda", "You should be able to have monster eggs hatch, and train the young monsters to fight for you.",
                "Você pode chocar ovos de monstros e treinar os filhotes para lutar ao seu lado."),
            new("Jorda", "In other words, the key to your success depends on how well you can train monsters to fight for you.",
                "Em outras palavras, seu sucesso depende de quão bem você treina seus monstros para lutar ao seu lado."),
            new("Jorda", "As you know, you can also sell monsters, but they’re not as valuable as the unhatched monster eggs.",
                "Como sabe, você também pode vender monstros. Eles valem menos que os ovos ainda por chocar."),
            new("Jorda", "Also, the value of the eggs themselves can differ.", "O valor dos próprios ovos também varia."),
            new("Jorda", "Give some thought to which eggs you want to use as your monsters and which you want to sell.",
                "Pense bem em quais ovos você quer chocar para ter familiares e quais quer vender."),
            new("Jorda", "Okay, do you understand to this point?", "Certo, entendeu até aqui?", BlessingCue.BasicsCheck),
            new("Jorda", "I’ll tell you one more time, so make sure you listen well.",
                "Vou explicar mais uma vez. Trate de prestar atenção.", BlessingCue.RepeatBasics),
            new("Jorda", "Moving along to the next point...", "Vamos ao próximo ponto...", BlessingCue.NextTopic),
            new("Jorda", "There are three secrets to the tower.", "A torre guarda três segredos.", BlessingCue.Secrets),
            new("Jorda", "First, it is said that you will always get lost when first entering the tower.",
                "Primeiro, dizem que você sempre se perde quando entra na torre."),
            new("Jorda", "Why do you think this is?", "Por que você acha que isso acontece?"),
            new("Jorda", "There is a spell cast on the inner structure so that the interior will change.",
                "Um feitiço lançado sobre a estrutura muda o interior da torre."),
            new("Jorda", "This is the first secret.", "Esse é o primeiro segredo."),
            new("Jorda", "Next, even the warriors that repeatedly challenge return after only reaching the lower levels.",
                "Segundo, até os guerreiros que desafiam a torre repetidamente voltam depois de alcançar só os primeiros andares."),
            new("Jorda", "Why do you think this is?", "Por que você acha que isso acontece?"),
            new("Jorda", "Because there is a spell cast at the entrance to reduce the power of human beings.",
                "Porque um feitiço na entrada reduz o poder dos seres humanos."),
            new("Jorda", "That’s why everyone starts at the first level each and every time.",
                "Por isso todos começam no primeiro andar, sempre."),
            new("Jorda", "This is the second secret.", "Esse é o segundo segredo."),
            new("Jorda", "The last secret has to do with what you have.", "O último segredo tem a ver com o que você carrega."),
            new("Jorda", "If you try to enter with too many items, the entrance of the tower will close, not allowing you to enter.",
                "Se tentar entrar com itens demais, a entrada da torre se fecha e impede sua passagem."),
            new("Jorda", "Probably because it’s the monster tower, a place where [Monsters], with empty hearts, rest.",
                "Talvez por ser a Torre dos Monstros, onde descansam monstros de coração vazio."),
            new("Jorda", "Okay, do you understand to this point?", "Certo, entendeu até aqui?", BlessingCue.SecretsCheck),
            new("Jorda", "You’ve got to be kidding! ...... Oh well.", "Só pode ser brincadeira! ...... Pois bem.", BlessingCue.Scold),
            new("Jorda", "I’ll tell you one more time, so make sure you listen well.",
                "Vou explicar mais uma vez. Trate de prestar atenção.", BlessingCue.RepeatSecrets),
            new("Jorda", "You’ve got to be kidding! ...... Oh well.", "Só pode ser brincadeira! ...... Pois bem.", BlessingCue.NoMoreRepeats),
            new("Jorda", "I told you, I’m not going to repeat myself.", "Já avisei que não vou repetir."),
            new("Jorda", "You really should be listening when someone is talking.", "Devia prestar atenção quando alguém fala com você."),
            new("Jorda", "That’s all I have to tell you.", "É tudo o que tenho a dizer."),
            new("Jorda", "So what do you think. Scary isn’t it?", "Então, o que achou? Assustador, não é?", BlessingCue.Scary),
            new("Jorda", "Do you still want to go after all that?", "Ainda quer ir depois de tudo isso?", BlessingCue.ScaryChoice),
            new("Jorda", "Well, okay.", "Bem, está certo.", BlessingCue.Decline),
            new("Jorda", "There is no training that could top actual experience.", "Nenhum treino supera a experiência de verdade."),
            new("Jorda", "But are you all right with your lack of knowledge?", "Mas tem certeza de que quer ir sabendo tão pouco?", BlessingCue.DepartureCheck),
            new("Jorda", "Hmm... Even your stubbornness is just like Guy.", "Hum... Até na teimosia você é igual ao Guy.", BlessingCue.Stubborn),
            new("Jorda", "Listen, {name}. Everything I told you, I heard from Guy.", "Escute, {name}. Tudo o que contei a você eu ouvi do Guy."),
            new("Jorda", "Think of it as your father telling you, and don’t forget it.", "Pense que foi seu pai quem lhe contou e guarde essas palavras."),
            new("Wreath", "This is a magic collar, that used to belong to your father.", "Este é um colar mágico que pertenceu ao seu pai."),
            new("Wreath", "Any tamed monster with this around its neck will come to help you wherever you are in the tower.",
                "Qualquer familiar que usar este colar vai ajudar você, onde quer que esteja na torre."),
            new("Wreath", "When you find an egg, and it hatches, put this on the monster.", "Quando encontrar um ovo e ele chocar, coloque o colar no monstro."),
            new("Jorda", "Ho, ho, ho, ho. With a memento of his father, the great monster tamer,",
                "Ho, ho, ho, ho. Com uma lembrança de seu pai, o grande domador de monstros,"),
            new("Jorda", "in hand, the son rises to the challenge to be the next great monster tamer.",
                "o filho parte para o desafio de se tornar o próximo grande domador de monstros."),
            new("Jorda", "I’m sure up in heaven, Guy would be proud to see his son develop into a great monster tamer.",
                "Tenho certeza de que Guy, lá no céu, ficaria orgulhoso de ver o filho se tornar um grande domador de monstros."),
            new("Jorda", "Ho, ho, ho, ho.", "Ho, ho, ho, ho."),
            new("Nico", "That’s great news. That means that now you’re able to enter that tower you always wanted to go to.",
                "Que notícia boa! Agora você pode entrar naquela torre que sempre quis visitar."),
            new("Wreath", "Are you heading out for the tower already?", "Já vai sair para a torre?", BlessingCue.TowerChoice),
            new("Wreath", "Then take this with you.", "Então leve isto com você."),
            new("", "(A Pita Fruit is received)", "(Você recebeu uma Fruta Pita)", BlessingCue.PitaReceipt),
            new("Wreath", "Go do your best!", "Dê o seu melhor!"),
            new("Weedy", "Yup!", "Isso!"),
            new("Nico", "Don’t stumble!", "Cuidado para não tropeçar!"),
            new("Weedy", "Yup!", "Isso!")
        };

        private BlessingCue CurrentBlessingCue => BlessingPages[_prologueLine].Cue;

        private (string Speaker, string En, string Pt) CurrentBlessingDialogue() =>
            CurrentBlessingCue == BlessingCue.BasicsCheck && _blessingRepeatingBasics ||
            CurrentBlessingCue == BlessingCue.SecretsCheck && _blessingRepeatingSecrets
                ? ("Jorda", "All right, NOW do you understand?", "Muito bem. AGORA entendeu?")
                : BlessingPages[_prologueLine].Dialogue;

        private (string First, string Second) BlessingChoices() => CurrentBlessingCue switch
        {
            BlessingCue.Impact => (T("Hey, that hurts!", "Ei, isso dói!"), T("I’ve gotta be patient.", "Preciso ter paciência.")),
            BlessingCue.Offer => (T("Yes.", "Sim."), T("I don’t need you to tell me.", "Não precisa me explicar.")),
            BlessingCue.BasicsCheck or BlessingCue.SecretsCheck => (T("Yes.", "Sim."), T("Not at all.", "Não entendi nada.")),
            BlessingCue.NextTopic => (T("go on, go on.", "Pode continuar."), T("I’ve heard enough.", "Já ouvi o bastante.")),
            BlessingCue.ScaryChoice or BlessingCue.DepartureCheck => (T("Yes.", "Sim."), T("I’m going no matter what.", "Vou de qualquer jeito.")),
            BlessingCue.TowerChoice => (T("I’m going", "Já vou."), T("Of course", "É claro.")),
            _ => default
        };

        private bool TryBranchBlessing()
        {
            bool decline = _prologueChoice == 1;
            BlessingCue destination;
            switch (CurrentBlessingCue)
            {
                case BlessingCue.Offer when decline:
                case BlessingCue.NextTopic when decline:
                    destination = BlessingCue.Decline;
                    break;
                case BlessingCue.BasicsCheck:
                    destination = decline && !_blessingRepeatingBasics ? BlessingCue.RepeatBasics : BlessingCue.NextTopic;
                    break;
                case BlessingCue.RepeatBasics:
                    _blessingRepeatingBasics = true;
                    destination = BlessingCue.Basics;
                    break;
                case BlessingCue.SecretsCheck:
                    destination = !decline ? BlessingCue.Scary :
                        _blessingRepeatingSecrets ? BlessingCue.NoMoreRepeats : BlessingCue.Scold;
                    break;
                case BlessingCue.RepeatSecrets:
                    _blessingRepeatingSecrets = true;
                    destination = BlessingCue.Secrets;
                    break;
                case BlessingCue.ScaryChoice:
                    destination = BlessingCue.Stubborn;
                    break;
                default: return false;
            }
            _prologueLine = Array.FindIndex(BlessingPages, page => page.Cue == destination);
            _prologueChoice = 0;
            return true;
        }
    }
}
