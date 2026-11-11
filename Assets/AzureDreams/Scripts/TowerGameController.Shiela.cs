namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private bool ShielaConsultation(NPCController npc) => StoryAsk(npc,
            "I'm Mademoiselle Shiela, known as the mother of Monsbaiya. Would you like me to tell your fortune?",
            "Sou Mademoiselle Shiela, conhecida como a mãe de Monsbaiya. Quer que eu leia a sua sorte?",
            (T("Yes", "Sim"), () => ShielaTopics(npc)),
            (T("I won't believe what you say", "Não vou acreditar no que você disser"), () =>
                StoryTalk(npc, "Come back if you change your mind.", "Volte se mudar de ideia.")));

        private bool ShielaTopics(NPCController npc) => StoryAsk(npc,
            "What would you like my advice on?", "Sobre o que você quer um conselho?",
            (T("On my love life", "Minha vida amorosa"), () => ShielaLove(npc)),
            (T("On how to fight", "Como lutar"), () => ShielaCombat(npc)),
            (T("On how to enjoy life", "Como aproveitar a vida"), () => ShielaTown(npc)));

        private bool ShielaLove(NPCController npc) => StoryAsk(npc,
            "Do you like animals?", "Você gosta de animais?",
            (T("Yes", "Sim"), () => ShielaAdvice(npc,
                "Visit Cherrl. Her little rabbit is usually close by. Listen to what she has to say.",
                "Visite a Cherrl. O coelhinho dela costuma estar por perto. Escute o que ela tem a dizer.")),
            (T("No", "Não"), () => ShielaAdvice(npc,
                "Talk to the people you meet in town. Nico cares a great deal about what happens to Monsbaiya.",
                "Converse com as pessoas que encontrar na cidade. Nico se importa muito com o futuro de Monsbaiya.")));

        private bool ShielaCombat(NPCController npc) => StoryAsk(npc,
            "Have you felt an urge to use magic?", "Você sente vontade de usar magia?",
            (T("Yes", "Sim"), () => ShielaAdvice(npc,
                "Let a familiar support you with magic. Water is strong against fire, fire against wind, and wind against water.",
                "Peça a um familiar para ajudar com magia. Água vence fogo, fogo vence vento e vento vence água.")),
            (T("No", "Não"), () => ShielaAdvice(npc,
                "Choose your weapon according to who you are fighting. A sword that works well against one monster may be weaker against another.",
                "Escolha sua arma de acordo com o inimigo. Uma espada que funciona bem contra um monstro pode ser mais fraca contra outro.")));

        private bool ShielaTown(NPCController npc) => ShielaAdvice(npc,
            "Listen to what the townspeople need. When you have a plan for a new building, take it to Tonka.",
            "Escute o que os moradores precisam. Quando tiver um plano para uma nova construção, fale com Tonka.");

        private bool ShielaAdvice(NPCController npc, string english, string portuguese) => StoryAsk(npc,
            english, portuguese,
            (T("Ask about something else", "Perguntar sobre outro assunto"), () => ShielaTopics(npc)),
            (T("Thank you", "Obrigado pela ajuda"), null));
    }
}
