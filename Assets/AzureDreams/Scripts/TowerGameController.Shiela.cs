namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        // Retail dialogue recovered from TOWN.BIN 0x62C026-0x636C9E.
        private const string ShielaReadingKey = "town.shiela.reading";
        private const int QuestionsPerReading = 5;

        private static readonly TownStoryLine[] LoveQuestions =
        {
            new("Would you like to play outside if the weather is good?",
                "Você gosta de brincar ao ar livre quando faz sol?"),
            new("Can you laugh and listen to selfish remarks by a person you like?",
                "Você consegue rir e ouvir os comentários egoístas de alguém de quem gosta?"),
            new("Would you like the woman to be a family oriented person?",
                "Você gostaria que sua parceira fosse dedicada à família?"),
            new("Do you think money is everything?",
                "Você acha que dinheiro é tudo?"),
            new("Are you a good listener?",
                "Você sabe ouvir?"),
            new("Do you think someone bugs you loves you in that person's heart?",
                "Você acha que quem vive implicando com você gosta de você no fundo?"),
            new("Do you think slyness is necessary to live on?",
                "Você acha que é preciso ser esperto para viver?"),
            new("When being kind to someone, do you think it should be done without being obvious about it?",
                "Você acha que devemos ser gentis sem chamar atenção para isso?"),
            new("Would you say a tomboy type of woman would be your type?",
                "Você diria que gosta de garotas mais despojadas?"),
            new("Do you think flowers are beautiful because they do not last forever?",
                "Você acha que as flores são bonitas porque não duram para sempre?"),
            new("Do you believe making something yourself will lead to greater joy?",
                "Você acredita que fazer algo com as próprias mãos traz mais alegria?"),
            new("Do you feel uneasy unless you are doing something?",
                "Você fica inquieto quando não está fazendo nada?"),
            new("Are you the type that can focus on one thing and one thing only?",
                "Você consegue se concentrar em uma coisa só?"),
            new("Do you like people that have confidence in their actions?",
                "Você gosta de pessoas seguras do que fazem?"),
            new("Are you unable to leave someone alone when they are depressed?",
                "Você não consegue deixar alguém sozinho quando está triste?"),
            new("Do you like cultural things?",
                "Você gosta de atividades culturais?"),
            new("Do you think you are good at making people laugh at dirty jokes?",
                "Você sabe fazer os outros rirem com piadas picantes?"),
            new("Do you often see yourself judging people by their appearances?",
                "Você costuma julgar alguém pela aparência?"),
            new("Do you have more fun being with your buddies than with girls?",
                "Você se diverte mais com seus amigos do que com garotas?"),
            new("Would you rather not even think about going out with a girl?",
                "Você prefere nem pensar em sair com uma garota?"),
            new("Do you often think about falling in love with a good looking man?",
                "Você costuma pensar em se apaixonar por um homem bonito?"),
            new("Do you think not to fight those that are weak?",
                "Você acha que não se deve brigar com quem é mais fraco?"),
            new("Funny to say so yourself, but do you consider yourself spoiled?",
                "Mesmo sendo estranho dizer isso de si mesmo, você se considera mimado?"),
            new("Do you believe dreams are achieved without the help of others?",
                "Você acredita que é possível realizar seus sonhos sem ajuda?"),
            new("Do you think that you are broad-minded?",
                "Você se considera uma pessoa de mente aberta?"),
            new("Do you feel that being somewhat self centered helps you gain identity?",
                "Você acha que pensar um pouco mais em si mesmo ajuda a descobrir quem você é?"),
            new("Do you find you do not know how someone feels from time to time?",
                "Às vezes você tem dificuldade de entender o que os outros sentem?"),
            new("Are you happy just thinking of the person you like?",
                "Você fica feliz só de pensar em quem gosta?"),
            new("Do you find childishness in an adult woman attractive?",
                "Você acha atraente quando uma mulher adulta tem um jeito infantil?"),
            new("To you, is being a good cook a prerequisite for a lover?",
                "Para você, saber cozinhar é essencial em um relacionamento?"),
            new("Do you feel that there are times you must have the courage to step away from the one you love?",
                "Você acha que às vezes é preciso ter coragem para se afastar de quem ama?"),
        };

        private static readonly TownStoryLine[] CombatQuestions =
        {
            new("Do you like animals?",
                "Você gosta de animais?"),
            new("Do you believe you take good care of others?",
                "Você acha que cuida bem dos outros?"),
            new("Are you interested in mystical things?",
                "Você se interessa por assuntos místicos?"),
            new("Have you felt an urge to use magic?",
                "Você já sentiu vontade de usar magia?"),
            new("Have you been told that you were short tempered?",
                "Já disseram que você perde a paciência com facilidade?"),
            new("Do you feel that allies are wonderful to have?",
                "Você acha bom ter aliados?"),
            new("Are you better at individual sports than team sports?",
                "Você se sai melhor em esportes individuais do que em equipe?"),
            new("Do you feel that you presently do not have time to think of anyone other than yourself?",
                "Você sente que agora não tem tempo para pensar em ninguém além de si mesmo?"),
            new("Do you feel you absolutely do not want to get involved in anything complicated?",
                "Você prefere ficar longe de qualquer complicação?"),
            new("Are you normally quick to throw old items away, even if you've been close to it for years?",
                "Você costuma jogar fora coisas velhas, mesmo quando as guarda há anos?"),
            new("Are you totally against evil?",
                "Você é totalmente contra o mal?"),
            new("Do you consider mathematics a nightmare?",
                "Você acha que matemática é um pesadelo?"),
            new("Do you often find yourself acting as a mediator in a fight?",
                "Você costuma apartar brigas?"),
            new("Generally, do you take good care of things?",
                "Em geral, você cuida bem das suas coisas?"),
        };

        private static readonly TownStoryLine[] LifeQuestions =
        {
            new("Between work and love, what's important is love, right?",
                "Entre trabalho e amor, o amor é mais importante, certo?"),
            new("Do you believe you cannot live through life alone?",
                "Você acha que ninguém consegue viver sozinho?"),
            new("Are you the type that can easily get excited and then easily calm down?",
                "Você se empolga e se acalma com facilidade?"),
            new("Is there somebody of the opposite sex that you truly love?",
                "Há alguém do sexo oposto que você realmente ama?"),
            new("Are you very shy around other people?",
                "Você fica muito tímido perto de outras pessoas?"),
            new("Do you hate to lose?",
                "Você odeia perder?"),
            new("Do you dislike studying?",
                "Você não gosta de estudar?"),
            new("Do you think that you have more patience than the average person?",
                "Você acha que tem mais paciência do que a maioria das pessoas?"),
            new("Do you feel that you'd like to conquer life itself?",
                "Você sente vontade de conquistar a própria vida?"),
            new("Are you easily swayed by other people's opinions?",
                "Você se deixa levar facilmente pela opinião dos outros?"),
            new("Do you feel that people can become stronger because there are other people that are there to support them?",
                "Você acha que as pessoas ficam mais fortes com o apoio de quem está por perto?"),
            new("Window shopping is wonderful, isn't it?",
                "Olhar vitrines é tão bom, não é?"),
            new("If your friend was hurt, would you not be able to sit still?",
                "Se um amigo se machucasse, você ficaria inquieto?"),
            new("Do you feel that there's nothing better than to earn easy money?",
                "Você acha que não há nada melhor do que ganhar dinheiro fácil?"),
            new("Would you be able to give up your feelings for a girl to a love rival if that love rival was your friend?",
                "Você abriria mão da garota de quem gosta se seu rival fosse um amigo?"),
            new("It's a great feeling to see an increase in the number of items in your room, isn't it?",
                "É bom ver seu quarto ganhar mais coisas, não é?"),
            new("Are you the kind of person that will never forget the feeling of gratefulness under any situation?",
                "Você se lembra de ser grato em qualquer situação?"),
            new("Do you frequently feel that you need someone that will root for you?",
                "Você costuma sentir falta de alguém que torça por você?"),
            new("Are you good at taking care of animals?",
                "Você sabe cuidar de animais?"),
            new("Do you feel you are finally at peace with yourself?",
                "Você sente que enfim está em paz consigo mesmo?"),
            new("Do you feel that gambling is not a good thing?",
                "Você acha que apostar não é uma boa ideia?"),
            new("You do enjoy games, don't you?",
                "Você gosta de jogos, não gosta?"),
        };

        private static readonly TownStoryLine[] LoveResults =
        {
            new("Haw! I can see the perfect woman for you! That woman is a bit on the vicious side, but is very kind hearted... you could say she's a healthy and pretty woman. To have her notice you, you must support her dream. You might think of her as a hard-to-handle tomboy, but deep inside there is no doubt she is very much a lady. Sometimes you can't see the forest for the trees. Pay special attention to that.",
                "Ah! Vejo a mulher ideal para você! Ela tem um jeito meio feroz, mas um bom coração. É bonita e cheia de vida. Apoie o sonho dela para que ela repare em você. Por trás daquele jeito difícil, ela é uma dama. Às vezes os detalhes escondem o que realmente importa. Preste atenção nisso."),
            new("Haw! I can see the perfect woman for you right here in my crystal ball! Ironically, I can also see the [Flame of Destiny] with it. It may be that this person is a sister of your arch rival. To have her notice you, your manliness might be the key. A location of beauty, such as the fountain at the park, may be the place fit for her.",
                "Ah! Vejo a mulher ideal para você na minha bola de cristal! Também vejo a [Chama do Destino]. Talvez ela seja irmã do seu maior rival. Para que ela repare em você, talvez seja preciso mostrar coragem. Um lugar bonito, como a fonte do parque, deve agradá-la."),
            new("Haw! I can see the perfect woman for you! That woman appears to have a realistic sense of spending, and may often be saying things that upset you. The person is very shy, and hides her true feelings. Your lucky item is octopus dumplings. Good luck!",
                "Ah! Vejo a mulher ideal para você! Ela é sensata com dinheiro e às vezes pode dizer coisas que incomodam você. Ela é tímida e esconde o que sente. Seu item da sorte é o bolinho de polvo. Boa sorte!"),
            new("Haw! I can see the perfect woman for you! She is a very family oriented and clear minded person. She will be an excellent wife! To have her notice you, bring out the motherhood side of her. Eating everything on your plate is a main ingredient to your good health.",
                "Ah! Vejo a mulher ideal para você! Ela tem juízo e valoriza a família. Será uma excelente esposa! Para que ela repare em você, desperte o lado maternal dela. Comer tudo o que está no prato é essencial para a saúde."),
            new("Haw! I can see the perfect woman for you! In one word this person can be compared to a star. But the destiny of this star is headed on a different road from your own. Depending on your actions, you may meet this star. Your lucky color is [Blue]. Anyway, Blue, think Blue.",
                "Ah! Vejo a mulher ideal para você! Ela é como uma estrela, mas segue um caminho diferente do seu. Dependendo do que você fizer, talvez se encontrem. Sua cor da sorte é [Azul]. Lembre-se, azul!"),
            new("Haw! I can see the perfect woman for you! First you will meet each other in a bar without knowing the existence of each other... and then, love in the air! A star needs the light of the sun to shine brightly. You must be her sun to fulfill her dream.",
                "Ah! Vejo a mulher ideal para você! Vocês vão se encontrar num bar sem sequer saber da existência um do outro. Depois, o amor estará no ar! Uma estrela precisa da luz do sol para brilhar. Seja o sol dela e ajude-a a realizar seu sonho."),
            new("Haw! I can see the perfect woman for you! She's a cute girl, younger than yourself. It appears here that you have not met this person yet. I see lots of books. That should be enough to give you an idea, right?",
                "Ah! Vejo a mulher ideal para você! É uma garota adorável, mais nova que você. Parece que vocês ainda não se conheceram. Vejo muitos livros. Já dá para ter uma ideia, não dá?"),
            new("Haw! I can see the perfect woman for you! She dislikes having men even talk to her. Continue to talk to her, and she will open up her heart. Be careful, though. She is one young lady you don't want as an enemy.",
                "Ah! Vejo a mulher ideal para você! Ela nem gosta que homens falem com ela. Continue conversando e ela abrirá o coração. Mas cuidado, você não vai querer tê-la como inimiga."),
            new("Haw! I can see the perfect woman for you! Her aura is much weaker than what you would normally expect from a girl. Your lucky zone is the hospital. Always tell yourself that you truly want to meet this girl. Good luck!",
                "Ah! Vejo a mulher ideal para você! A aura dela é mais fraca do que eu esperaria. Seu lugar de sorte é o hospital. Lembre-se sempre de que quer mesmo conhecê-la. Boa sorte!"),
            new("Haw! I can see the perfect woman for you! She's a cute person you'd want to take good care of. The lucky item is an herb that I have never seen before. Depending on your actions, you may completely change the life of a young lady. Be very careful.",
                "Ah! Vejo a mulher ideal para você! É uma garota adorável de quem você vai querer cuidar. Seu item da sorte é uma erva que nunca vi antes. Suas ações podem mudar a vida dela por completo. Tome muito cuidado."),
            new("Haw! I can see the perfect woman... what? No, a man... what am I seeing? The perfect man for you is brought up by rich parents, stingy, a show-off, and arrogant. Seeing how much he loves women, perhaps there is no way to have him think seriously of you.",
                "Ah! Vejo a mulher ideal... O quê? Um homem? O que estou vendo? O homem ideal para você veio de família rica, é avarento, exibido e arrogante. Pelo jeito que ele gosta de mulheres, talvez seja impossível fazer com que leve você a sério."),
        };

        private static readonly TownStoryLine[] CombatResults =
        {
            new("Hah! I see it! The one thing you lack the most when you fight is how to control your familiars! Other monsters have feelings too. Pay special attention to their physical and magical powers. The best place to return monsters is in the Monster Hut. I pray for your health.",
                "Ah! Já vejo! Em combate, o que mais lhe falta é aprender a comandar seus familiares. Os monstros também têm sentimentos. Preste atenção às habilidades físicas e mágicas deles. O melhor lugar para guardar seus monstros é o alojamento. Que você tenha saúde."),
            new("Hah! I see it! The one thing you lack the most when you fight is how to use your tools! Pay attention to the timing, direction, placement and height of the target. Many misses can be caused by simple misunderstandings. I pray for your health.",
                "Ah! Já vejo! Em combate, o que mais lhe falta é saber usar suas ferramentas. Preste atenção ao momento, à direção, à posição e à altura do alvo. Um simples engano pode fazer você errar. Que você tenha saúde."),
            new("Hah! I see it! The one thing you lack the most when you fight is magic. Fire, water and wind are the three categories. For fire monsters use water magic; for water monsters use wind magic; for wind monsters use fire magic. Your specific commands make the difference. I pray for your health.",
                "Ah! Já vejo! Em combate, o que mais lhe falta é magia. Há três elementos: fogo, água e vento. Use água contra fogo, vento contra água e fogo contra vento. Suas ordens fazem a diferença. Que você tenha saúde."),
            new("Hah! I see it! The one thing you lack the most when you fight is how to use your weapons. You cannot leave all the fighting to your monsters. Use one weapon well and keep getting better at it. Choose weapons depending on who you are fighting. I pray for your health.",
                "Ah! Já vejo! Em combate, o que mais lhe falta é saber usar armas. Você não pode deixar toda a luta para seus monstros. Aprenda a manejar bem uma arma e continue praticando. Escolha a arma de acordo com o adversário. Que você tenha saúde."),
            new("Hah! I see it! The one thing you lack the most when you fight is knowing your enemy first! Study their physical and magical levels, magic and special powers. Even harmless-looking monsters may have unusual defenses. I pray for your health.",
                "Ah! Já vejo! Em combate, o que mais lhe falta é conhecer o inimigo. Estude a força física e mágica dele, suas magias e poderes especiais. Até os monstros que parecem inofensivos podem ter defesas incomuns. Que você tenha saúde."),
            new("Hah! I see it! The one thing you lack the most when you fight is fusion! The post fusion monster will be of the stronger category, and its appearance will be that of the higher level monster. Fusing monsters without magic can still produce new magic or increase basic status.",
                "Ah! Já vejo! Em combate, o que mais lhe falta é a fusão. O monstro resultante terá o elemento mais forte e a aparência do monstro de nível mais alto. Fundir monstros sem magia ainda pode criar uma magia nova ou melhorar os atributos básicos."),
        };

        private static readonly TownStoryLine[] LifeResults =
        {
            new("Haw! I can see it. This is the exciting lifestyle I recommend to you! Start placing an emphasis on your love life. It is more than possible to have a Monsbaiya woman fall in love with you!",
                "Ah! Já vejo! Recomendo que dê mais atenção à sua vida amorosa. Uma mulher de Monsbaiya pode muito bem se apaixonar por você!"),
            new("Haw! I can see it. Collect all the monsters you can. Obtain every type of familiar that exists in Monsbaiya, accommodate them in your home, and collect every page of the illustrated monster book.",
                "Ah! Já vejo! Colecione todos os monstros que puder. Consiga cada tipo de familiar de Monsbaiya, acomode-os em casa e reúna todas as páginas do livro ilustrado de monstros."),
            new("Haw! I can see it. Throw your heart and soul into volunteer work! Provide donations to places like the temple and hospital. Being appreciated by the people is not a bad feeling.",
                "Ah! Já vejo! Dedique-se de corpo e alma ao trabalho voluntário. Faça doações ao templo e ao hospital. É bom ter o carinho das pessoas."),
            new("Haw! I can see it. Enjoy your life to the fullest! A gambler's life! How about building facilities such as a casino, or a horse track? That depends on whether the local carpenter knows how to build those.",
                "Ah! Já vejo! Aproveite a vida ao máximo. Viva como um apostador! Que tal construir um cassino ou uma pista de corrida? Isso depende de o carpinteiro da cidade saber construí-los."),
            new("Haw! I can see it. Thank your mother! Take good care of your mother. Your mother brought you up alone through tough times. By making your house bigger using the money you've earned, she will be relieved.",
                "Ah! Já vejo! Agradeça à sua mãe e cuide bem dela. Ela criou você sozinha em tempos difíceis. Use o dinheiro que ganhou para ampliar a casa e dar mais conforto a ela."),
            new("Haw! I can see it. A lifestyle full of entertainment suits you! You could build an entertainment center, a bowling center and a pool. Just imagine the girl of your dreams in a swimsuit!",
                "Ah! Já vejo! Uma vida cheia de diversão combina com você. Você pode construir um centro de jogos, um boliche e uma piscina. Imagine só a garota dos seus sonhos em traje de banho!"),
            new("Haw! I can see it. The total conquest of the Monster Tower! You, along with your most loyal monsters, heading for the top level of the tower! What could be waiting at the final level?",
                "Ah! Já vejo! Conquiste a Torre dos Monstros! Suba até o último andar ao lado dos seus monstros mais leais. O que será que espera por você no topo?"),
            new("Haw! I can see it. Enjoy your life collecting items! This city sells an abundance of items. Consider the Aura Bike: it is pricey, but any collector would go crazy for it.",
                "Ah! Já vejo! Divirta-se colecionando itens. Há muita coisa à venda nesta cidade. A Aura Bike custa caro, mas qualquer colecionador ficaria louco por ela."),
        };

        private bool ShielaConsultation(NPCController npc) => ShielaSequence(npc, () => StoryAsk(npc,
            "Would you like to know your fortune?", "Você quer saber sua sorte?",
            (T("Yes", "Sim"), () => ShielaTopics(npc)),
            (T("Charge — 200 G", "Carregar — 200 G"), () => ShielaChargeMenu(npc)),
            (T("I won't believe what you say", "Não vou acreditar no que você disser"), () => ShielaSequence(npc, null,
                new TownStoryLine("Haw! Don't kid around with me.", "Ah! Não brinque comigo."), new TownStoryLine("I'm even giving away a prize of a special pendant that makes a person's love come true just by wearing it, to a lucky winner...", "Estou até sorteando um pingente especial. Quem ganhar só precisa usá-lo para realizar seu desejo de amor...")))),
            new TownStoryLine("Welcome to the Fortune Telling Palace.", "Bem-vindo ao Palácio da Adivinhação."), new TownStoryLine("I'm Mademoiselle Shiela, known as the mother of Monsbaiya.", "Sou a senhorita Shiela, conhecida como a mãe de Monsbaiya."));

        private bool ShielaTopics(NPCController npc) => StoryAsk(npc, "What would you like me to look into today?", "Sobre o que você quer que eu veja hoje?",
            (T("Charge — 200 G", "Carregar — 200 G"), () => ShielaChargeMenu(npc)),
            (T("On my love life", "Minha vida amorosa"), () => ShielaBegin(npc, LoveQuestions, LoveResults, new TownStoryLine("Haw! You're still young, naturally this is what you want to know. All right. Relax and answer my questions.", "Ah! Você ainda é jovem. É natural que queira saber disso. Muito bem. Relaxe e responda às minhas perguntas."))),
            (T("On how to fight", "Como lutar"), () => ShielaBegin(npc, CombatQuestions, CombatResults, new TownStoryLine("Haw! You're very clear-minded person. It has to do with your work, doesn't it? All right. Relax and answer my questions please.", "Ah! Você sabe bem o que quer. Tem a ver com seu trabalho, não é? Muito bem. Relaxe e responda às minhas perguntas, por favor."))),
            (T("On how to enjoy life", "Como aproveitar a vida"), () => ShielaBegin(npc, LifeQuestions, LifeResults, new TownStoryLine("Haw! There's no limit to the number of ways on how. All right. Relax and give me answers to my questions.", "Ah! Há incontáveis maneiras de aproveitar a vida. Muito bem. Relaxe e responda às minhas perguntas."))));

        private void ShielaBegin(NPCController npc, TownStoryLine[] questions, TownStoryLine[] results, TownStoryLine intro)
        {
            int reading = TownSaveVariables.GetInt(ShielaReadingKey);
            TownSaveVariables.SetInt(ShielaReadingKey, reading + 1);
            PersistTownProgress();
            ShielaSequence(npc, () => ShielaQuestion(npc, questions, results, reading, 0, 0), intro);
        }

        private void ShielaQuestion(NPCController npc, TownStoryLine[] questions, TownStoryLine[] results, int reading, int index, int yes)
        {
            if (index >= QuestionsPerReading)
            {
                ShielaSequence(npc, () => ShielaAfterReading(npc), results[(reading + yes) % results.Length]);
                return;
            }

            TownStoryLine question = questions[(reading * QuestionsPerReading + index) % questions.Length];
            StoryAsk(npc, question.English, question.Portuguese,
                (T("Yes", "Sim"), () => ShielaQuestion(npc, questions, results, reading, index + 1, yes + 1)),
                (T("No", "Não"), () => ShielaQuestion(npc, questions, results, reading, index + 1, yes)));
        }

        private void ShielaAfterReading(NPCController npc) => StoryAsk(npc,
            "Is there anything else you would like to know?", "Há mais alguma coisa que você gostaria de saber?",
            (T("Yes there is", "Sim"), () => ShielaTopics(npc)),
            (T("I'm leaving", "Vou embora"), () => ShielaSequence(npc, null,
                new TownStoryLine("Thank you very much.", "Muito obrigada."), new TownStoryLine("Come back again whenever you have something on your mind.", "Volte sempre que tiver algo em mente."), new TownStoryLine("I, Mademoiselle Shiela, mother of Monsbaiya, shall solve your problems.", "Eu, a senhorita Shiela, mãe de Monsbaiya, vou resolver seus problemas."))));

        private bool ShielaSequence(NPCController npc, System.Action next, params TownStoryLine[] lines)
        {
            void Show(int index)
            {
                if (index >= lines.Length) { next?.Invoke(); return; }
                StoryTalk(npc, lines[index].English, lines[index].Portuguese, () => Show(index + 1));
            }
            Show(0);
            return true;
        }
    }
}
