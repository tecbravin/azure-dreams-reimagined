using UnityEngine;
namespace AzureDreamsReimagined
{
    /// <summary>Unity conversation progress, deliberately separate from unresolved retail quest flags.</summary>
    public sealed class TownDialogueProgress : MonoBehaviour
    {
        private NPCController _npc;
        private string _pending;
        private void OnEnable() { TownEventBus.Subscribe(TownEventBus.EventId.NpcTalked, OnTalk); }
        private void OnDisable() { TownEventBus.Unsubscribe(TownEventBus.EventId.NpcTalked, OnTalk); _pending = null; }
        public void Prepare(NPCController npc)
        {
            _npc = npc;
            bool met = TownSaveVariables.GetBool("dialogue." + npc.npcId + ".introduced");
            _pending = "introduced";
            // Native excerpts use NTSC disc offsets. Other lines are adapted for the current quest flow.
            switch (npc.npcId)
            {
                case "fur":
                    npc.dialogueLines = new[]
                    {
                        GameLanguage.Text("Welcome.", "Bem-vindo."),
                        GameLanguage.Text("What would you like?", "O que você gostaria?")
                    };
                    break;
                // Original restaurant conversations from the local PS1 recording.
                case "patty":
                    npc.dialogueLines = new[] { GameLanguage.Text("Hello. Can I help you?", "Olá. Posso ajudar?") };
                    break;
                case "coppe":
                    npc.dialogueLines = new[] { GameLanguage.Text("Hi-ya {name}! Making good food comes from the heart!",
                        "Oi, {name}! É preciso cozinhar com o coração para fazer comida boa!") };
                    break;
                case "dar":
                    npc.dialogueLines = new[] { GameLanguage.Text("It's good, it's good, it's great.", "É bom, é bom, é ótimo.") };
                    break;
                case "bet":
                    npc.dialogueLines = new[] { GameLanguage.Text("The food here is the best in Monsbaiya.", "A comida daqui é a melhor de Monsbaiya.") };
                    break;
                case "restaurant_hunter":
                    npc.dialogueLines = new[] { GameLanguage.Text("The food's great and Patty's got a great personality.",
                        "A comida é ótima e a Patty é muito simpática.") };
                    break;
                case "shiela": // 0x62C028
                    npc.dialogueLines = new[] { GameLanguage.Text("Welcome to the Fortune Telling Palace.", "Boas-vindas ao Palácio da Adivinhação."),
                        GameLanguage.Text("I’m Mademoiselle Shiela, known as the mother of Monsbaiya.", "Sou Mademoiselle Shiela, conhecida como a mãe de Monsbaiya.") };
                    break;
                case "hush": // 0x59C8AC, 0x59C916
                    if (TownSaveVariables.GetBool("town.hush_bought_herb"))
                    {
                        npc.dialogueLines = new[] { GameLanguage.Text(
                            "Keep a medicinal herb with you in the tower. Use it before your injuries get too serious.",
                            "Leve uma erva medicinal para a torre. Use antes que os ferimentos fiquem graves.") };
                        break;
                    }
                    npc.dialogueLines = new[] { GameLanguage.Text("How are you doing? I haven’t seen you in a while.", "Como você está? Faz tempo que não vejo você."),
                        GameLanguage.Text("In your case, germs and illnesses would want to run away from you, so you probably have little concern for a clinic.",
                            "No seu caso, os germes e as doenças devem fugir de você, então é provável que você nem pense muito na clínica.") };
                    break;
                case "jorda": // 0x5B56C0, 0x5B6474
                    npc.dialogueLines = met
                        ? new[] { GameLanguage.Text("Everything I told you, I heard from Guy. Think of it as your father telling you, and don’t forget.",
                            "Tudo o que contei a você eu ouvi do Guy. Pense que foi seu pai quem contou e guarde bem essas palavras.") }
                        : new[] { GameLanguage.Text("When entering the dungeon of the tower, you can find rare items and monster eggs.",
                            "Ao entrar na masmorra da torre, você pode encontrar itens raros e ovos de monstro."),
                            GameLanguage.Text("If you bring these back with you, you can exchange them for money.", "Se trouxer essas coisas de volta, pode trocá-las por dinheiro."),
                            GameLanguage.Text("But the tower is a monster nest. Danger will always follow you.", "Mas a torre é um ninho de monstros. O perigo vai acompanhar você.") };
                    break;
                case "selfi": // 0x2DDD0E
                    npc.dialogueLines = new[] { GameLanguage.Text("Hi there, Puffy lips! I know so much about you.", "Oi, Bocão! Sei tanta coisa sobre você."),
                        GameLanguage.Text("Well... I thought I did...", "Bem... eu achava que sabia...") };
                    break;
                case "ghosh": // 0x2D2395
                    npc.dialogueLines = new[] { GameLanguage.Text("A barbarian wouldn’t understand elegance.", "Um bárbaro jamais entenderia a elegância.") };
                    break;
                case "guy": // 0x6FB874, 0x6FB993
                    npc.dialogueLines = new[] { GameLanguage.Text("Yeah, what’s the matter Nico?", "Sim, o que foi, Nico?"),
                        GameLanguage.Text("Listen, {name}. A man must take good care of women.",
                            "Escute, {name}. Um homem deve tratar bem as mulheres.") };
                    break;
                case "youngnico": // 0x6FB8D8, with Koh substituted for the player name token.
                    npc.dialogueLines = new[] { GameLanguage.Text("Sob! {name} is teasing me.", "Snif! O {name} está me provocando.") };
                    break;
                case "introangel": // 0x6F681F
                    npc.dialogueLines = new[] { GameLanguage.Text("Oh, you’re awake. You are about to be reborn.", "Ah, você acordou. Você está prestes a renascer."),
                        GameLanguage.Text("All you can decide on is your name.", "A única coisa que você pode escolher é o seu nome.") };
                    break;
                case "cat": npc.dialogueLines = new[] { GameLanguage.Text("Meow.", "Miau.") }; break;
                case "bird": npc.dialogueLines = new[] { GameLanguage.Text("Chirp!", "Piu!") }; break;
                default:
                    _pending = null;
                    break;
            }
            if (_pending != null)
            {
                npc.dialogue = null;
                npc.loopDialogue = false;
            }
        }
        private void OnTalk(object value)
        {
            if (!(value is NPCDialogue.TalkEvent e) || e.Source != gameObject || !e.Completed || _npc == null || _pending == null) return;
            string prefix = "dialogue." + _npc.npcId + ".";
            TownSaveVariables.SetBool(prefix + _pending, true);
            TownSaveVariables.SetInt(prefix + "completed_conversations", TownSaveVariables.GetInt(prefix + "completed_conversations") + 1);
            TownSaveVariables.Save(); _pending = null;
        }
    }
}
