using System;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string GhoshPortrait = "Dialogue/Ghosh_variant0_frame0";
        private const string GhoshIrritatedPortrait = "Dialogue/Ghosh_variant1_frame0";
        private const string GhoshSwimsuitPortrait = "Dialogue/GhoshSwimsuitHD";
        private const string GhoshSwimsuitRetail = "Dialogue/Ghosh_variant2_frame0";
        private const string GhoshPoolVisitKey = StoryPrefix + "ghosh.pool_visited";
        private static readonly int[] GhoshIrritatedFrames = { 2, 2, 2, 2 };
        private NPCController _nicoFountainGhoshGuest;

        private static string GhoshPortraitPath(int expression) => expression switch
        {
            1 => GhoshIrritatedPortrait,
            2 => GhoshSwimsuitPortrait,
            _ => GhoshPortrait
        };

        private int GhoshIrritatedFrame(float started)
        {
            int ticks = Mathf.FloorToInt((Time.unscaledTime - started) * 60f);
            return StoryPortraitFrame(GhoshIrritatedFrames, ticks);
        }

        private Texture2D GhoshDialoguePortrait(string path, float started)
        {
            var image = StreamPortrait(path);
            if (path == GhoshSwimsuitPortrait) return image ?? StreamPortrait(GhoshSwimsuitRetail);
            if (path != GhoshIrritatedPortrait || image) return image;
            int frame = GhoshIrritatedFrame(started);
            return StreamPortrait(path.Replace("_frame0", "_frame" + Mathf.Max(1, frame)));
        }

        private float GhoshDialoguePortraitOffset(Texture2D portrait)
        {
            string path = IsShowingVisitorDialogue ? _visitorPortraitPath : _storyPortraitPath;
            if (path != GhoshIrritatedPortrait || !portrait || portrait.width <= 105) return 0f;
            float started = IsShowingVisitorDialogue ? _visitorPortraitStarted : _storyPortraitStarted;
            // The extracted frames shift left by one pixel after the first pose.
            // Apply that motion to the existing HD portrait at the disc's cadence.
            return GhoshIrritatedFrame(started) == 0 ? 0f : -1f / 105f;
        }

        private bool GhoshTalk(NPCController npc, string en, string pt, Action next = null) =>
            StoryTalk(npc, en, pt, StoryRoom == PoolOpenRoom ? GhoshSwimsuitPortrait : GhoshPortrait, next);

        private bool GhoshIrritatedTalk(NPCController npc, string en, string pt, Action next = null) =>
            StoryTalk(npc, en, pt, GhoshIrritatedPortrait, next);

        // Use the original remark about the Rhodes house in his ordinary home interaction.
        private bool GhoshHouseTalk(NPCController npc) =>
            GhoshTalk(npc, "My house has nine rooms. How many rooms does yours have?",
                "Minha casa tem nove cômodos. Quantos a sua tem?");

        // TOWN.BIN 0x2DF009–0x2DF0EA.
        private bool GhoshAtNicoDoor(NPCController ghosh) =>
            GhoshTalk(ghosh, "Nico, it’s me. Ghosh, the prince of Monsbaiya. Why can’t you let me in?",
                "Nico, sou eu. Ghosh, o príncipe de Monsbaiya. Por que você não me deixa entrar?", () =>
                    GhoshIrritatedTalk(ghosh, "Go away!", "Vá embora!", () =>
                        GhoshIrritatedTalk(ghosh, "Whaaaaa... Oh no. Why would you let a stupid guy like Mr. {name} in and not ME?",
                            "O quêêê... Ah, não. Por que deixa um idiota como o Sr. {name} entrar e não EU?",
                            () => AdvanceTownStory("nico", NicoHouseTalkFirst))));

        // TOWN.BIN 0x2CFB8A–0x2CFD0B.
        private void GhoshMatchesFountainDonation(NPCController nico, int amount, Action done)
        {
            SpawnNicoFountainGhosh();
            var ghosh = FindStoryActor("nico_ghosh");
            if (!ghosh)
            {
                done();
                return;
            }
            _nicoFountainGhoshGuest = ghosh;

            GhoshSequence(ghosh, () => NicoTalk(nico, NicoCollecting, $"He donated {amount} G.", $"Ele doou {amount} G.", () =>
                    GhoshTalk(ghosh, $"Well then, I shall donate {amount + 1} G.", $"Então vou doar {amount + 1} G.", () =>
                        NicoTalk(nico, NicoCollecting, "Wow, Thank you.", "Nossa, obrigada.", () =>
                            GhoshTalk(ghosh, "Well, will you excuse me, Lady?", "Bem, com licença, milady.", () =>
                            {
                                DismissNicoFountainGhosh();
                                done();
                            })))),
                ("Nico Southey. Here I am, I, Ghosh Rode have come to you.",
                    "Nico Southey. Aqui estou eu, Ghosh Rode, para vê-la."),
                ("By the way, did this dummy make a donation for the fountain?",
                    "Aliás, esse bobo fez alguma doação para a fonte?"));
        }

        // TOWN.BIN 0x2D220D–0x2D24EF.
        private void GhoshFountainCelebration(NPCController nico)
        {
            SpawnNicoFountainGhosh();
            var ghosh = FindStoryActor("nico_ghosh");
            if (!ghosh)
            {
                AdvanceTownStory("nico", NicoAfterSelfiEgg);
                return;
            }
            _nicoFountainGhoshGuest = ghosh;

            void Finish()
            {
                DismissNicoFountainGhosh();
                AdvanceTownStory("nico", NicoAfterSelfiEgg);
            }

            void Invite() => GhoshTalk(ghosh, "Nico,let’s talk about culture and love.",
                "Nico, vamos falar de cultura e de amor.", () =>
                    NicoTalk(nico, NicoFlustered, "Alright.", "Está bem.", () =>
                        GhoshTalk(ghosh, "Aha ha ha ha ha. Ahem.", "Ah, ha, ha, ha, ha. Aham.", Finish)));

            GhoshTalk(ghosh, "Magnificent, Nico. A beautiful place like this really suits you.",
                "Magnífico, Nico. Um lugar bonito assim combina mesmo com você.", () =>
                    NicoTalk(nico, NicoFlustered, "Heh heh heh.", "He, he, he.", () =>
                        StoryAsk(ghosh, "It’s {name} who’s not a good match for this beautiful place or for you.",
                            "Quem não combina com este lugar bonito nem com você é o {name}.", GhoshPortrait,
                            (T("Neither do you.", "Você também não combina."), () =>
                                GhoshIrritatedTalk(ghosh, "A barbarian wouldn’t understand elegance.",
                                    "Um bárbaro jamais entenderia a elegância.", Invite)),
                            (T("We make a good couple.", "Nós formamos um belo casal."), () =>
                                NicoTalk(nico, NicoWorried, "Who’s the couple you’re talking about, {name}?",
                                    "De que casal você está falando, {name}?", Invite)))));
        }

        private void DismissNicoFountainGhosh()
        {
            var ghosh = _nicoFountainGhoshGuest;
            _nicoFountainGhoshGuest = null;
            if (!ghosh) return;
            _townPeople.Remove(ghosh);
            RemoveGenerated(ghosh.gameObject);
            RefreshTownNpcCells();
        }

        // TOWN.BIN 0x534D13–0x53555F, first and later visits to the reopened pool.
        private bool GhoshPoolStory(NPCController ghosh)
        {
            if (TownSaveVariables.GetBool(GhoshPoolVisitKey))
                return PoolTalk(ghosh, GhoshSwimsuitPortrait, "Hi!  {name}!", "Olá, {name}!", () =>
                    PoolAsk(ghosh, GhoshSwimsuitPortrait,
                        "I wouldn’t want to see any accidents at this pool due to the lack of staff looking over it. So I’ve decided to come here from time to time.",
                        "Não quero que aconteça nenhum acidente por falta de gente para tomar conta da piscina. Por isso decidi vir aqui de vez em quando.",
                        (T("You’ve got a tiresome life.", "Que vida cansativa a sua."), () =>
                            GhoshTalk(ghosh, "What do you mean by that!? How rude can you get! Good-bye!",
                                "O que quer dizer com isso?! Que falta de educação! Até logo!")),
                        (T("I’ve come to see the girls.", "Vim ver as garotas."), () =>
                            GhoshSequence(ghosh, null,
                                ("That’s right. I mean, what a beautiful sight this is.",
                                    "Pois é. Quer dizer, que vista bonita."),
                                ("It’s just natural for any man to want to come here to see the girls...",
                                    "É natural que qualquer homem queira vir aqui para ver as garotas..."),
                                ("I honestly came to look over the pool, to be a gentleman... ... I’ll come again.",
                                    "De verdade, vim só para vigiar a piscina e agir como um cavalheiro... Eu volto outra hora.")))));

            void Finish()
            {
                TownSaveVariables.SetBool(GhoshPoolVisitKey, true);
                TownSaveVariables.Save();
            }

            return PoolTalk(ghosh, GhoshSwimsuitPortrait, "Hi!  {name}! I thought I’d do you a favor and come here.",
                "Olá, {name}! Achei que faria um favor a você vindo aqui.", () =>
                    PoolTalk(ghosh, GhoshSwimsuitPortrait,
                        "Otherwise the city probably would not acknowledge this as a building for the city.",
                        "Senão, talvez a cidade nem reconhecesse isto como uma obra da cidade.", () =>
                            PoolAsk(ghosh, GhoshSwimsuitPortrait, "I would think the least you could do is thank me for coming here.",
                                "Acho que o mínimo que você podia fazer era agradecer pela minha presença.",
                                (T("Which girl do you like?", "De qual garota você gosta?"), () => GhoshSequence(ghosh, Finish,
                                    ("Let’s see, I’d say the one that girl’s wearing over... wait a minute!",
                                        "Deixe-me ver. Eu diria o daquela garota ali... Espere um minuto!"),
                                    ("How shameless. Are you implying I came here just to gaze at the girls’ swimsuits?",
                                        "Que descaramento. Está insinuando que vim só para olhar os maiôs das garotas?"),
                                    ("How rude. I never came here for... Umm... Good bye!",
                                        "Que grosseria. Eu jamais viria aqui para... Hum... Até logo!"))),
                                (T("I am honored to have you come", "É uma honra receber você."), () => GhoshSequence(ghosh, Finish,
                                    ("You’re obedience is appreciated. Don’t forget that feeling.",
                                        "Aprecio sua obediência. Guarde esse sentimento."),
                                    ("In that case, I support the presence of this pool. Ha, ha, ha...",
                                        "Nesse caso, apoio a existência desta piscina. Ha, ha, ha..."))))));
        }
    }
}
