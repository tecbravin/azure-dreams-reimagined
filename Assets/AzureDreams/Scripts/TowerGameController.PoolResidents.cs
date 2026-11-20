using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Ambient residents of the restored Wookie Wookie Pool, from the pool scene overlay
    /// (TOWN.BIN 0x51E000). Speaker table 0x520D34 lists them; each script's text follows its
    /// script pointer (file = VA - 0x80010000 + 0x5298D0) and the bytecode marks speaker changes
    /// with 0x57 &lt;person slot&gt;. Their swimsuit bodies are the initializers at 0x51690+ rendered
    /// with the swimsuit package at TOWN.BIN 0x8B8000 (banks "pool_*").
    /// English is the retail text (Shift-JIS, 0x534282-0x534C87); Portuguese is a translation.
    /// </summary>
    public sealed partial class TowerGameController
    {
        private static readonly (string Id, string Prefab, string English, string Portuguese)[] PoolResidents =
        {
            ("pool_swimmer_a", "dar", "Swimmer", "Banhista"),    // slot 83
            ("pool_swimmer_b", "dar", "Swimmer", "Banhista"),    // slot 84
            ("pool_girl", "dar", "Girl", "Menina"),              // slot 60, F_act_kiku
            ("pool_kid", "dar", "Boy", "Garoto"),                // slot 59, F_act_miru
            ("pool_grandpa", "dar", "Grandpa", "Vovô"),          // slot 47, F_act_gigi
            ("pool_woman", "dar", "Woman", "Mulher"),            // slot 61, F_act_narder (no swimsuit body)
            ("pool_hunter", "dar", "Hunter", "Caçador"),         // slot 52, F_act_hunter1
            ("pool_aunt", "dar", "Aunt", "Tia"),                 // slot 45, F_act_obasan
            ("pool_man", "dar", "Man", "Homem"),                 // slot 62, F_act_oyaji
            ("pool_grandma", "dar", "Grandma", "Vovó"),          // slot 48, F_act_baba
            ("pool_barry", "dar", "Barry", "Barry"),             // slot 16
            ("pool_kim_big", "dar", "Big Bro Kim", "Kim, o irmão mais velho"),       // slot 41
            ("pool_kim_little", "dar", "Little Bro Kim", "Kim, o irmão mais novo"),  // slot 42
        };

        private void SpawnPoolResidents()
        {
            foreach (var resident in PoolResidents)
                SpawnStoryResident(resident.Prefab, resident.Id, T(resident.English, resident.Portuguese), default);
            // The two brothers (slots 57/58, F_act_aniki/otouto) only matter while Koh is at the
            // pool with Vivian: their whole script is about him being friendly with her.
            if (TownSaveVariables.GetInt(VivianPoolDateKey) is 1 or 2)
            {
                SpawnStoryResident("dar", "pool_heckler_big", T("Big Brother", "Irmão mais velho"), default);
                SpawnStoryResident("dar", "pool_heckler_little", T("Little Brother", "Irmão mais novo"), default);
            }
        }

        private const string VivianPoolHecklersSeenKey = StoryPrefix + "vivian.poolHecklers";

        // TOWN.BIN 0x536312-0x536687 (script 0x8001C9B4, shared by speakers 0x39 and 0x3A).
        // "He's been to level N" prints a floor number; the reached floor is used here.
        private bool VivianPoolHecklers(NPCController talkedTo)
        {
            var big = FindStoryActor("pool_heckler_big") ?? talkedTo;
            var little = FindStoryActor("pool_heckler_little") ?? talkedTo;
            if (TownSaveVariables.GetBool(VivianPoolHecklersSeenKey))
                return StoryTalk(talkedTo, "Bye!", "Tchau!");
            int floor = Mathf.Max(1, TownSaveVariables.GetInt(TownReputation.HighestFloorKey));
            return StoryTalk(big, "Hey, wait a minute.", "Ei, espera aí.", () =>
                StoryTalk(big, "Yo!  Who do you think you are,\ntrying to look friendly with\nVivian.",
                    "Ei! Quem você pensa que é,\npara ficar todo simpático com\na Vivian?", () =>
                StoryTalk(little, "I − I don’t think you should be\nspeaking to him in that tone.",
                    "A-acho que você não devia\nfalar com ele nesse tom.", () =>
                StoryTalk(little, "He’s that demon trainer we keep\nhearing about.",
                    "Ele é aquele domador de monstros\nde quem todo mundo fala.", () =>
                StoryTalk(little, $"He’s been to level {floor} of\nthe tower.",
                    $"Ele já chegou ao {floor}º andar\nda torre.", () =>
                StoryTalk(big, $"You mean {HeroName}?", $"Você quer dizer o {HeroName}?", () =>
                StoryTalk(big, "The guy that Vivian can’t get\nher mind off...  No wonder\nVivian looked so thrilled...",
                    "O cara que não sai da cabeça\nda Vivian... Não é à toa que\na Vivian parecia tão empolgada...", () =>
                StoryTalk(big, "......\nAhh, never mind what I just\nsaid...",
                    "......\nAh, esquece o que eu acabei\nde dizer...", () =>
                StoryTalk(big, "Bye!", "Tchau!", () =>
                StoryTalk(little, "Bye!", "Tchau!", () =>
                {
                    TownSaveVariables.SetBool(VivianPoolHecklersSeenKey, true);
                    PersistTownProgress();
                }))))))))));
        }

        private bool PoolResidentDialogue(NPCController npc)
        {
            switch (npc.npcId)
            {
                case "pool_swimmer_a": // 0x534282
                    PoolSequence(npc, null, null,
                        ("Wotta, the manager of the\nWookie Wookie Pool, is always\nso energetic and lively.",
                            "O Wotta, o gerente da\nPiscina Wookie Wookie, está sempre\ntão animado e cheio de energia."),
                        ("He always seems to be having\nfun.", "Ele parece estar sempre\nse divertindo."));
                    return true;
                case "pool_swimmer_b": // 0x534367
                    return StoryTalk(npc, "It’s a beautiful pool.", "É uma piscina linda.");
                case "pool_girl": // 0x534395
                    return StoryTalk(npc, "Hey, hey!  Look at my swimsuit,\naren’t I cute?",
                        "Ei, ei! Olha o meu maiô,\nnão estou uma gracinha?");
                case "pool_kid": // 0x5343F4
                    return StoryTalk(npc, "Listen!  Listen!\nThere’s a sea monster in this\npool!  I’m not kidding!",
                        "Escuta! Escuta!\nTem um monstro marinho nesta\npiscina! Não estou brincando!");
                case "pool_grandpa": // 0x534485
                    PoolSequence(npc, null, null,
                        ("Just over there, a sea monster\nsaw me swimming, and suddenly\nscreamed.",
                            "Bem ali, um monstro marinho\nme viu nadando e, de repente,\ngritou."),
                        ("What a rude sea monster.", "Que monstro marinho mal-educado."));
                    return true;
                case "pool_woman": // 0x534544
                    return StoryTalk(npc, "It’s like an oasis in the\ndesert.\nBeautiful.",
                        "É como um oásis no\ndeserto.\nLindo.");
                case "pool_hunter": // 0x53459E
                    return StoryTalk(npc, "I heard you got the Water\nMedal back from Picket.\nYou’re unbelievable.",
                        "Ouvi dizer que você recuperou o\nMedalhão da Água do Picket.\nVocê é incrível.");
                case "pool_aunt": // 0x53462F
                    PoolSequence(npc, null, null,
                        ("This city really needs a pool.", "Esta cidade precisava mesmo de uma piscina."),
                        ($"I understand it was you that\nmade it happen, {HeroName}.\nThank you.",
                            $"Fiquei sabendo que foi você\nquem tornou isso possível, {HeroName}.\nObrigada."));
                    return true;
                case "pool_man": // 0x5346E1
                    return StoryTalk(npc, "Pools are great.\nIt brings out the romance in a\nman.",
                        "Piscinas são ótimas.\nDespertam o lado romântico de\num homem.");
                case "pool_grandma": // 0x534749
                    return StoryTalk(npc, "How do you like my new bathing\nsuit.\nPretty sexy, don’t you think?",
                        "Gostou do meu maiô novo?\nBem sensual, não acha?");
                case "pool_heckler_big" or "pool_heckler_little":
                    return VivianPoolHecklers(npc);
                case "pool_barry" or "pool_kim_big" or "pool_kim_little":
                    return BarryKimPoolScene(npc);
                default:
                    return false;
            }
        }

        // TOWN.BIN 0x5347D5-0x534C87: one script shared by Barry (speaker 0x10) and the Kim
        // brothers (0x29, 0x2A), so talking to any of the three plays it.
        private bool BarryKimPoolScene(NPCController talkedTo)
        {
            var barry = FindStoryActor("pool_barry") ?? talkedTo;
            var big = FindStoryActor("pool_kim_big") ?? barry;
            var little = FindStoryActor("pool_kim_little") ?? barry;
            return StoryTalk(barry, $"Yo, it’s {HeroName}.\nI’ve come to the pool with the\nKim Brothers.",
                $"Ei, é o {HeroName}.\nVim à piscina com os\nIrmãos Kim.", () =>
                StoryTalk(big, "Big Kim!  Flex!", "Kim mais velho! Flexionar!", () =>
                StoryTalk(little, "Little Kim!  Flex!", "Kim mais novo! Flexionar!", () =>
                StoryTalk(barry, "And my turn.  Flex!", "E agora eu. Flexionar!", () =>
                StoryTalk(barry, "The three of us together make\nup the Kim Step Brothers.",
                    "Nós três juntos formamos\nos Irmãos Kim de Criação.", () =>
                StoryAsk(barry, $"Won’t you swim with us?\nWe are truly fond of you,\n{HeroName}.",
                    $"Não quer nadar com a gente?\nGostamos muito de você,\n{HeroName}.",
                    (T("Well I’m not fond of you.", "Pois eu não gosto de vocês."), () =>
                        StoryTalk(barry, "You’re so mean, sob...", "Que maldade, buá...")),
                    (T("I’ll pass on that thought.", "Vou deixar essa ideia de lado."), () =>
                        StoryTalk(big, "Eeeeeek!", "Aaaaaai!", () =>
                        StoryTalk(little, "Eeeeeek!", "Aaaaaai!", () =>
                        StoryTalk(barry, "Eeek!  ...is how I feel about\nthis.",
                            "Aaai! ...é assim que eu me sinto\ncom isso."))))))))));
        }
    }
}
