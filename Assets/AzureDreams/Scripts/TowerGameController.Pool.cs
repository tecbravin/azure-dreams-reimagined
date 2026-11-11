using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string PoolRequestSeen = "pool.request_seen";

        private bool PoolService(NPCController npc)
        {
            if (npc.npcId == "wookie")
            {
                if (PoolRestored)
                {
                    if (StoryRoom == PoolOpenRoom)
                        return StoryTalk(npc, $"Hey, {HeroName}. Thanks for coming! Have a whale of a time. The task of managing this pool is calling me!",
                            $"Oi, {HeroName}. Obrigado por vir! Divirta-se muito. Agora preciso cuidar da piscina!");
                    return StoryAsk(npc, $"Hey, {HeroName}! You are free to come and go as you wish!",
                        $"Oi, {HeroName}! Pode entrar e sair quando quiser!",
                        (T("Enter the pool", "Entrar na piscina"), () => StartCoroutine(LoadInterior(PoolOpenRoom))),
                        (T("Later", "Depois"), null));
                }

                if (FindStoryItem(QuestItemKind, WaterMedalId) >= 0)
                    return StoryAsk(npc, "Th-this is the Water Medal!", "E-esse é o Medalhão da Água!",
                        (T("Return the Water Medal", "Devolver o Medalhão da Água"), () =>
                        {
                            int medal = FindStoryItem(QuestItemKind, WaterMedalId);
                            if (medal < 0 || !_townInventory.ConsumeDungeonItem(medal, QuestItemKind)) return;
                            TownRetailScriptFlags.CreatePersistent().SetBit(PoolQuestCompleteFlag);
                            PersistTownProgress();
                            StoryTalk(npc, $"Thank you, my friend! {HeroName}, you are free to come and go as you wish!",
                                $"Obrigado, meu amigo! {HeroName}, você ganhou um passe vitalício. Entre quando quiser!",
                                () => StartCoroutine(LoadInterior(PoolOpenRoom)));
                        }), (T("Later", "Depois"), null));

                if (TownSaveVariables.GetBool(PoolRequestSeen))
                    return StoryTalk(npc, $"Hey, {HeroName}. Did you get the Water Medal back? I'm depending on you.",
                        $"Oi, {HeroName}. Recuperou o Medalhão da Água? Estou contando com você.");
                return StoryAsk(npc, "A thief stole the Water Medal! I went after him, but he ran into the tower. Please! Could you get it back?",
                    "Um ladrão roubou o Medalhão da Água! Corri atrás dele, mas ele fugiu para a torre. Por favor, pode recuperá-lo?",
                    (T("I'll get it back for you.", "Vou recuperá-lo para você."), () =>
                    {
                        TownSaveVariables.SetBool(PoolRequestSeen, true);
                        PersistTownProgress();
                        StoryTalk(npc, "I'm certain it was Picket. He lives on level 25. Bring it back and I'll give you a lifetime pass!",
                            "Tenho certeza de que foi um Picket. Ele vive no 25º andar. Traga o medalhão e você ganha um passe vitalício!");
                    }),
                    (T("What are you talking about?", "Do que você está falando?"), () =>
                        StoryTalk(npc, "The medal keeps the water clean. Without it, there's no way we can operate the pool.",
                            "O medalhão mantém a água limpa. Sem ele, a piscina precisa ficar fechada.")));
            }

            if (!PoolRestored || StoryRoom != PoolOpenRoom) return false;
            switch (npc.npcId)
            {
                case "pool_nico":
                    return StoryAsk(npc, $"Can you swim, {HeroName}?", $"Você sabe nadar, {HeroName}?",
                        (T("I can swim.", "Sei nadar."), () =>
                            StoryTalk(npc, "Wow, that's great! To tell you the truth, I can't swim. I'll just be watching you from here.",
                                "Uau, que legal! Para falar a verdade, eu não sei nadar. Vou ficar olhando daqui.")),
                        (T("I like the wading pool.", "Prefiro a piscina rasa."), () =>
                            StoryTalk(npc, "Let me see! Let me see you swim!", "Deixa eu ver! Quero ver você nadar!")));
                case "pool_fur":
                    return StoryAsk(npc, "How do you like my swimsuit? Does it suit me?", "O que achou do meu maiô? Ficou bom em mim?",
                        (T("It looks good on you.", "Ficou muito bom."), () =>
                            StoryTalk(npc, $"You even look good in your swimsuit, {HeroName}.", $"Até de roupa de banho você fica bem, {HeroName}.")),
                        (T("Let's go for a swim.", "Vamos nadar."), () =>
                            StoryTalk(npc, "It's the first time in my life I've worn a swimsuit.", "É a primeira vez na vida que uso um maiô.")));
                case "pool_visitor":
                    return StoryTalk(npc, "Listen! There's a sea monster in this pool! I'm not kidding!",
                        "Escute! Tem um monstro marinho nesta piscina! É sério!");
                default: return false;
            }
        }
    }
}
