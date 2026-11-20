namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        // Retail dialogue recovered from TOWN.BIN 0x62C026-0x636C9E.
        private const string ShielaReadingKey = "town.shiela.reading";
        private const int QuestionsPerReading = 5;

        private static readonly string[] LoveQuestions =
            "Would you like to play outside if the weather is good?|Can you laugh and listen to selfish remarks by a person you like?|Would you like the woman to be a family oriented person?|Do you think money is everything?|Are you a good listener?|Do you think someone bugs you loves you in that person's heart?|Do you think slyness is necessary to live on?|When being kind to someone, do you think it should be done without being obvious about it?|Would you say a tomboy type of woman would be your type?|Do you think flowers are beautiful because they do not last forever?|Do you believe making something yourself will lead to greater joy?|Do you feel uneasy unless you are doing something?|Are you the type that can focus on one thing and one thing only?|Do you like people that have confidence in their actions?|Are you unable to leave someone alone when they are depressed?|Do you like cultural things?|Do you think you are good at making people laugh at dirty jokes?|Do you often see yourself judging people by their appearances?|Do you have more fun being with your buddies than with girls?|Would you rather not even think about going out with a girl?|Do you often think about falling in love with a good looking man?|Do you think not to fight those that are weak?|Funny to say so yourself, but do you consider yourself spoiled?|Do you believe dreams are achieved without the help of others?|Do you think that you are broad-minded?|Do you feel that being somewhat self centered helps you gain identity?|Do you find you do not know how someone feels from time to time?|Are you happy just thinking of the person you like?|Do you find childishness in an adult woman attractive?|To you, is being a good cook a prerequisite for a lover?|Do you feel that there are times you must have the courage to step away from the one you love?".Split('|');

        private static readonly string[] CombatQuestions =
            "Do you like animals?|Do you believe you take good care of others?|Are you interested in mystical things?|Have you felt an urge to use magic?|Have you been told that you were short tempered?|Do you feel that allies are wonderful to have?|Are you better at individual sports than team sports?|Do you feel that you presently do not have time to think of anyone other than yourself?|Do you feel you absolutely do not want to get involved in anything complicated?|Are you normally quick to throw old items away, even if you've been close to it for years?|Are you totally against evil?|Do you consider mathematics a nightmare?|Do you often find yourself acting as a mediator in a fight?|Generally, do you take good care of things?".Split('|');

        private static readonly string[] LifeQuestions =
            "Between work and love, what's important is love, right?|Do you believe you cannot live through life alone?|Are you the type that can easily get excited and then easily calm down?|Is there somebody of the opposite sex that you truly love?|Are you very shy around other people?|Do you hate to lose?|Do you dislike studying?|Do you think that you have more patience than the average person?|Do you feel that you'd like to conquer life itself?|Are you easily swayed by other people's opinions?|Do you feel that people can become stronger because there are other people that are there to support them?|Window shopping is wonderful, isn't it?|If your friend was hurt, would you not be able to sit still?|Do you feel that there's nothing better than to earn easy money?|Would you be able to give up your feelings for a girl to a love rival if that love rival was your friend?|It's a great feeling to see an increase in the number of items in your room, isn't it?|Are you the kind of person that will never forget the feeling of gratefulness under any situation?|Do you frequently feel that you need someone that will root for you?|Are you good at taking care of animals?|Do you feel you are finally at peace with yourself?|Do you feel that gambling is not a good thing?|You do enjoy games, don't you?".Split('|');

        private static readonly string[] LoveResults =
        {
            "Haw! I can see the perfect woman for you! That woman is a bit on the vicious side, but is very kind hearted... you could say she's a healthy and pretty woman. To have her notice you, you must support her dream. You might think of her as a hard-to-handle tomboy, but deep inside there is no doubt she is very much a lady. Sometimes you can't see the forest for the trees. Pay special attention to that.",
            "Haw! I can see the perfect woman for you right here in my crystal ball! Ironically, I can also see the [Flame of Destiny] with it. It may be that this person is a sister of your arch rival. To have her notice you, your manliness might be the key. A location of beauty, such as the fountain at the park, may be the place fit for her.",
            "Haw! I can see the perfect woman for you! That woman appears to have a realistic sense of spending, and may often be saying things that upset you. The person is very shy, and hides her true feelings. Your lucky item is octopus dumplings. Good luck!",
            "Haw! I can see the perfect woman for you! She is a very family oriented and clear minded person. She will be an excellent wife! To have her notice you, bring out the motherhood side of her. Eating everything on your plate is a main ingredient to your good health.",
            "Haw! I can see the perfect woman for you! In one word this person can be compared to a star. But the destiny of this star is headed on a different road from your own. Depending on your actions, you may meet this star. Your lucky color is [Blue]. Anyway, Blue, think Blue.",
            "Haw! I can see the perfect woman for you! First you will meet each other in a bar without knowing the existence of each other... and then, love in the air! A star needs the light of the sun to shine brightly. You must be her sun to fulfill her dream.",
            "Haw! I can see the perfect woman for you! She's a cute girl, younger than yourself. It appears here that you have not met this person yet. I see lots of books. That should be enough to give you an idea, right?",
            "Haw! I can see the perfect woman for you! She dislikes having men even talk to her. Continue to talk to her, and she will open up her heart. Be careful, though. She is one young lady you don't want as an enemy.",
            "Haw! I can see the perfect woman for you! Her aura is much weaker than what you would normally expect from a girl. Your lucky zone is the hospital. Always tell yourself that you truly want to meet this girl. Good luck!",
            "Haw! I can see the perfect woman for you! She's a cute person you'd want to take good care of. The lucky item is an herb that I have never seen before. Depending on your actions, you may completely change the life of a young lady. Be very careful.",
            "Haw! I can see the perfect woman... what? No, a man... what am I seeing? The perfect man for you is brought up by rich parents, stingy, a show-off, and arrogant. Seeing how much he loves women, perhaps there is no way to have him think seriously of you."
        };

        private static readonly string[] CombatResults =
        {
            "Hah! I see it! The one thing you lack the most when you fight is how to control your familiars! Other monsters have feelings too. Pay special attention to their physical and magical powers. The best place to return monsters is in the Monster Hut. I pray for your health.",
            "Hah! I see it! The one thing you lack the most when you fight is how to use your tools! Pay attention to the timing, direction, placement and height of the target. Many misses can be caused by simple misunderstandings. I pray for your health.",
            "Hah! I see it! The one thing you lack the most when you fight is magic. Fire, water and wind are the three categories. For fire monsters use water magic; for water monsters use wind magic; for wind monsters use fire magic. Your specific commands make the difference. I pray for your health.",
            "Hah! I see it! The one thing you lack the most when you fight is how to use your weapons. You cannot leave all the fighting to your monsters. Use one weapon well and keep getting better at it. Choose weapons depending on who you are fighting. I pray for your health.",
            "Hah! I see it! The one thing you lack the most when you fight is knowing your enemy first! Study their physical and magical levels, magic and special powers. Even harmless-looking monsters may have unusual defenses. I pray for your health.",
            "Hah! I see it! The one thing you lack the most when you fight is fusion! The post fusion monster will be of the stronger category, and its appearance will be that of the higher level monster. Fusing monsters without magic can still produce new magic or increase basic status."
        };

        private static readonly string[] LifeResults =
        {
            "Haw! I can see it. This is the exciting lifestyle I recommend to you! Start placing an emphasis on your love life. It is more than possible to have a Monsbaiya woman fall in love with you!",
            "Haw! I can see it. Collect all the monsters you can. Obtain every type of familiar that exists in Monsbaiya, accommodate them in your home, and collect every page of the illustrated monster book.",
            "Haw! I can see it. Throw your heart and soul into volunteer work! Provide donations to places like the temple and hospital. Being appreciated by the people is not a bad feeling.",
            "Haw! I can see it. Enjoy your life to the fullest! A gambler's life! How about building facilities such as a casino, or a horse track? That depends on whether the local carpenter knows how to build those.",
            "Haw! I can see it. Thank your mother! Take good care of your mother. Your mother brought you up alone through tough times. By making your house bigger using the money you've earned, she will be relieved.",
            "Haw! I can see it. A lifestyle full of entertainment suits you! You could build an entertainment center, a bowling center and a pool. Just imagine the girl of your dreams in a swimsuit!",
            "Haw! I can see it. The total conquest of the Monster Tower! You, along with your most loyal monsters, heading for the top level of the tower! What could be waiting at the final level?",
            "Haw! I can see it. Enjoy your life collecting items! This city sells an abundance of items. Consider the Aura Bike: it is pricey, but any collector would go crazy for it."
        };

        private bool ShielaConsultation(NPCController npc) => ShielaSequence(npc, () => StoryAsk(npc,
            "Would you like to know your fortune?", "Você quer saber sua sorte?",
            (T("Yes", "Sim"), () => ShielaTopics(npc)),
            (T("Charge — 200 G", "Carregar — 200 G"), () => ShielaChargeMenu(npc)),
            (T("I won't believe what you say", "Não vou acreditar no que você disser"), () => ShielaSequence(npc, null,
                "Haw! Don't kid around with me.", "I'm even giving away a prize of a special pendant that makes a person's love come true just by wearing it, to a lucky winner..."))),
            "Welcome to the Fortune Telling Palace.", "I'm Mademoiselle Shiela, known as the mother of Monsbaiya.");

        private bool ShielaTopics(NPCController npc) => StoryAsk(npc, "What would you like me to look into today?", "Sobre o que você quer que eu veja hoje?",
            (T("Charge — 200 G", "Carregar — 200 G"), () => ShielaChargeMenu(npc)),
            (T("On my love life", "Minha vida amorosa"), () => ShielaBegin(npc, LoveQuestions, LoveResults, "Haw! You're still young, naturally this is what you want to know. All right. Relax and answer my questions.")),
            (T("On how to fight", "Como lutar"), () => ShielaBegin(npc, CombatQuestions, CombatResults, "Haw! You're very clear-minded person. It has to do with your work, doesn't it? All right. Relax and answer my questions please.")),
            (T("On how to enjoy life", "Como aproveitar a vida"), () => ShielaBegin(npc, LifeQuestions, LifeResults, "Haw! There's no limit to the number of ways on how. All right. Relax and give me answers to my questions.")));

        private void ShielaBegin(NPCController npc, string[] questions, string[] results, string intro)
        {
            int reading = TownSaveVariables.GetInt(ShielaReadingKey);
            TownSaveVariables.SetInt(ShielaReadingKey, reading + 1);
            PersistTownProgress();
            ShielaSequence(npc, () => ShielaQuestion(npc, questions, results, reading, 0, 0), intro);
        }

        private void ShielaQuestion(NPCController npc, string[] questions, string[] results, int reading, int index, int yes)
        {
            if (index >= QuestionsPerReading)
            {
                ShielaSequence(npc, () => ShielaAfterReading(npc), results[(reading + yes) % results.Length]);
                return;
            }

            string question = questions[(reading * QuestionsPerReading + index) % questions.Length];
            StoryAsk(npc, question, question,
                (T("Yes", "Sim"), () => ShielaQuestion(npc, questions, results, reading, index + 1, yes + 1)),
                (T("No", "Não"), () => ShielaQuestion(npc, questions, results, reading, index + 1, yes)));
        }

        private void ShielaAfterReading(NPCController npc) => StoryAsk(npc,
            "Is there anything else you would like to know?", "Há mais alguma coisa que você gostaria de saber?",
            (T("Yes there is", "Sim"), () => ShielaTopics(npc)),
            (T("I'm leaving", "Vou embora"), () => ShielaSequence(npc, null,
                "Thank you very much.", "Come back again whenever you have something on your mind.", "I, Mademoiselle Shiela, mother of Monsbaiya, shall solve your problems.")));

        private bool ShielaSequence(NPCController npc, System.Action next, params string[] lines)
        {
            void Show(int index)
            {
                if (index >= lines.Length) { next?.Invoke(); return; }
                // The recovered retail script is English, so keep its wording verbatim.
                StoryTalk(npc, lines[index], lines[index], () => Show(index + 1));
            }
            Show(0);
            return true;
        }
    }
}
