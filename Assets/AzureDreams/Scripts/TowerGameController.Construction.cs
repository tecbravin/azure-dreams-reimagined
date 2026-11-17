using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string CarpenterInterior = "Interior_13_carpenter";
        private const string SilverHouse = "Interior_2c_silver_house";
        private const string ConstructionResources = "Town/Retail/Construction/";
        private const string ConstructionKey = "town.construction.";
        private const string ConstructionLotSuffix = ".lot";
        // Availability halfwords from town_building_records at TOWN.BIN 0x54EE4.
        private const int CasinoPlanFlag = 4807; // F_casino_build
        private const int LibraryPlanFlag = 4800; // F_hal_build
        private const int GymPlanFlag = 4808; // F_gym_build
        private const int ArcadePlanFlag = 4809; // F_playland_build
        private const int CircuitPlanFlag = 2865; // F_sbs44_pad03
        private const int TheaterPlanFlag = 4802; // F_engei_build
        private const int BowlingPlanFlag = 4810; // F_bowling_build
        private const int TempleRenovationCost = 4000;
        private const int HushClinicTalkFlag = 4003; // F_shospital_first_hush
        private const string TempleRequestKey = ConstructionKey + "temple_requested";
        private const string TempleThankedKey = ConstructionKey + "temple_thanked";
        private const string GymRequestKey = ConstructionKey + "gym_requested";
        private const string ArcadeRequestStageKey = ConstructionKey + "arcade.request_stage";
        private const string CircuitRequestStageKey = ConstructionKey + "circuit.request_stage";
        private const string GymRequestStageKey = ConstructionKey + "gym.request_stage";
        // No separate Fon portrait was recovered from the retail disc.  The
        // captured town elder portrait is the art used by his temporary actor.
        private const string FonPortrait = "Dialogue/Grandpa_variant0_frame0";
        private const string JordaPortrait = "Dialogue/Jorda_variant0_frame0";
        private const string JordaEyesClosed = "Dialogue/Jorda_variant0_frame1";
        private const string JordaEyesClosedRetail = "Dialogue/Jorda_variant0_frame2";
        private const string JordaEyesHalfClosed = "Dialogue/Jorda_variant0_frame3";
        private static readonly int[] JordaBlinkDurations = { 32, 6, 64, 4 };

        private sealed class BuildingProject
        {
            public readonly string Id, English, Portuguese, Interior, Lot;
            public readonly int Price;
            public readonly int HutRank;
            // Bounds includes visual roof/edge overhangs. ReplaceBounds is the
            // original retail-town lot whose base mesh may be removed before
            // drawing the constructed exterior.
            public readonly Rect Bounds, ReplaceBounds, Footprint;
            public readonly Vector2 Entrance;
            public readonly Vector2 LotOrigin, WorldOffset;

            public BuildingProject(string id, string english, string portuguese, int price,
                string interior = null, Rect bounds = default, Rect footprint = default, Vector2 entrance = default,
                string lot = null, int hutRank = 0, Vector2 lotOrigin = default, Rect replaceBounds = default)
            {
                Id = id; English = english; Portuguese = portuguese; Price = price;
                Interior = interior; Bounds = bounds;
                ReplaceBounds = replaceBounds.width > 0f ? replaceBounds : bounds;
                Footprint = footprint; Entrance = entrance; Lot = lot ?? id;
                HutRank = hutRank;
                LotOrigin = lotOrigin == default ? bounds.position : lotOrigin;
                WorldOffset = Vector2.zero;
            }

            private BuildingProject(BuildingProject source, Vector2 offset)
            {
                Id = source.Id; English = source.English; Portuguese = source.Portuguese; Price = source.Price;
                Interior = source.Interior; Lot = source.Lot; HutRank = source.HutRank;
                Bounds = OffsetRect(source.Bounds, offset);
                ReplaceBounds = OffsetRect(source.ReplaceBounds, offset);
                Footprint = OffsetRect(source.Footprint, offset);
                Entrance = source.Entrance + offset;
                LotOrigin = source.LotOrigin;
                WorldOffset = source.WorldOffset + offset;
            }

            public BuildingProject AtLot(Vector2 origin) => new(this, origin - LotOrigin);

            private static Rect OffsetRect(Rect rect, Vector2 offset) =>
                new(rect.x + offset.x, rect.y + offset.y, rect.width, rect.height);

            public string Name => T(English, Portuguese);
        }

        private sealed class ConstructionLot
        {
            public readonly int Slot;
            public readonly Vector2 Origin;
            public readonly string English, Portuguese;

            public ConstructionLot(int slot, float x, float z, string english, string portuguese)
            {
                Slot = slot;
                Origin = new Vector2(x, z);
                English = english;
                Portuguese = portuguese;
            }

            public string Name => T(English, Portuguese);
        }

        private static readonly BuildingProject[] BuildingProjects =
        {
            new("home_1", "Expand Koh's house I", "Ampliar a casa do Koh I", 6000, "KohHome",
                Rect.MinMaxRect(48, 44, 63, 58), Rect.MinMaxRect(50, 47, 61, 53), new(56, 56),
                replaceBounds: Rect.MinMaxRect(50, 44, 62, 58)),
            new("home_2", "Expand Koh's house II", "Ampliar a casa do Koh II", 60000, "KohHome",
                Rect.MinMaxRect(48, 44, 63, 58), Rect.MinMaxRect(50, 47, 61, 53), new(56, 56), "home",
                replaceBounds: Rect.MinMaxRect(50, 44, 62, 58)),
            new("hut_1", "Expand the monster hut to 3×3", "Ampliar o alojamento para 3×3", 4000, hutRank: 1),
            new("hut_2", "Expand the monster hut to 4×4", "Ampliar o alojamento para 4×4", 6000, hutRank: 2),
            new("hut_3", "Expand the monster hut to 6×6", "Ampliar o alojamento para 6×6", 20000, hutRank: 3),
            new("hut_4", "Expand the monster hut to 8×8", "Ampliar o alojamento para 8×8", 60000, hutRank: 4),
            new("hospital", "Renovate the hospital", "Reformar o hospital", 4000, "Interior_10_hospital_2",
                Rect.MinMaxRect(8, 62, 23, 72), Rect.MinMaxRect(11, 65, 21, 69), new(16, 69.25f)),
            new("temple", "Renovate the temple", "Reformar o templo", TempleRenovationCost, "Interior_12_temple_2",
                Rect.MinMaxRect(64, 27, 84, 44), Rect.MinMaxRect(66, 31, 82, 41), new(74, 41)),
            new("library", "Build the library", "Construir a biblioteca", 8000, "Interior_07_library",
                Rect.MinMaxRect(90, 30, 102, 41), Rect.MinMaxRect(92, 35, 100, 38), new(96, 38), lotOrigin: new(88, 28)),
            new("fountain", "Build the fountain", "Construir a fonte", 2000, null,
                Rect.MinMaxRect(55, 64, 73, 80), Rect.MinMaxRect(60, 69, 66, 74)),
            new("alley", "Build the bowling alley", "Construir o boliche", 40000, "Interior_0c_alley",
                Rect.MinMaxRect(42, 29, 54, 41), Rect.MinMaxRect(43, 33, 53, 37), new(48, 37), lotOrigin: new(40, 28)),
            new("gym", "Build the gym", "Construir a academia", 30000, "Interior_08_gym",
                Rect.MinMaxRect(25, 88, 38, 98), Rect.MinMaxRect(27, 91, 37, 95), new(32, 95), lotOrigin: new(24, 86)),
            new("circuit", "Build the racetrack", "Construir a pista de corrida", 30000, "Interior_0a_racetrack_entrance",
                Rect.MinMaxRect(7, 23, 25, 44), Rect.MinMaxRect(9, 25, 24, 40), new(16, 40)),
            new("theater", "Build the theater", "Construir o teatro", 8000, "Interior_0b_theater",
                Rect.MinMaxRect(26, 28, 38, 40), Rect.MinMaxRect(27, 32, 37, 36), new(32, 36), lotOrigin: new(24, 28)),
            new("casino", "Build the casino", "Construir o cassino", 100000, "Interior_2b_casino_1",
                Rect.MinMaxRect(91, 88, 102, 98), Rect.MinMaxRect(92, 91.5f, 100, 95), new(96, 95), lotOrigin: new(88, 86)),
            new("arcade", "Build the amusement center", "Construir o centro de diversão", 3000, "Interior_09_arcade_1",
                Rect.MinMaxRect(104, 62, 119, 73), Rect.MinMaxRect(107, 65, 117, 69), new(112, 69))
        };

        // These are the five interchangeable 16x14 construction slots from
        // the retail town layout. Other projects have one compatible lot and
        // remain fixed, exactly as the original land-selection rules require.
        private static readonly ConstructionLot[] FlexibleConstructionLots =
        {
            new(4, 24, 28, "West lot", "Terreno oeste"),
            new(5, 40, 28, "West-central lot", "Terreno centro-oeste"),
            new(7, 88, 28, "Northeast lot", "Terreno nordeste"),
            new(28, 24, 86, "Southwest lot", "Terreno sudoeste"),
            new(31, 88, 86, "Southeast lot", "Terreno sudeste")
        };

        private const int ConstructionRows = 5;
        private readonly List<BuildingProject> _builtTownLots = new();
        private readonly List<BuildingProject> _availableConstructionProjects = new();
        private readonly List<ConstructionLot> _availableConstructionLots = new();
        private readonly Dictionary<int, float> _constructionGround = new();
        private readonly List<UnityEngine.Object> _constructionGeometry = new();
        private Transform _retailTownObjectRoot;
        private bool _constructionOpen, _constructionConfirm, _constructionChoosingLot;
        private int _constructionRow, _constructionLotRow;
        private ConstructionLot _selectedConstructionLot;
        private string _constructionMessage;
        private bool InCarpenterShop => _interior && _interior.interiorId == CarpenterInterior;

        private int ConstructionUpgradeScore
        {
            get
            {
                int score = CurrentHomeRank + CurrentHutRank;
                foreach (var project in BuildingProjects)
                {
                    if (project.Id is "home_1" or "home_2" || project.HutRank > 0) continue;
                    if (ProjectBuilt(project)) score++;
                }
                return score;
            }
        }

        private static bool ProjectBuilt(BuildingProject project) => ProjectBuilt(project, CurrentHomeRank, CurrentHutRank,
            TownSaveVariables.GetBool(ConstructionKey + project.Id));

        private static bool ProjectBuilt(BuildingProject project, int homeRank, int hutRank, bool completed) => project.Id switch
        {
            "home_1" => homeRank >= 1,
            "home_2" => homeRank >= 2,
            _ => project.HutRank > 0 ? hutRank >= project.HutRank : completed
        };

        private static bool UsesFlexibleConstructionLot(BuildingProject project) => project.Id is
            "library" or "alley" or "gym" or "theater" or "casino";

        private static string ConstructionLotKey(BuildingProject project) =>
            ConstructionKey + project.Id + ConstructionLotSuffix;

        private static ConstructionLot DefaultConstructionLot(BuildingProject project)
        {
            int slot = project.Id switch
            {
                "theater" => 4,
                "alley" => 5,
                "library" => 7,
                "gym" => 28,
                "casino" => 31,
                _ => -1
            };
            return Array.Find(FlexibleConstructionLots, candidate => candidate.Slot == slot);
        }

        private static ConstructionLot SavedConstructionLot(BuildingProject project)
        {
            if (!UsesFlexibleConstructionLot(project)) return null;
            string key = ConstructionLotKey(project);
            int slot = TownSaveVariables.HasValue(key) ? TownSaveVariables.GetInt(key) : -1;
            return Array.Find(FlexibleConstructionLots, candidate => candidate.Slot == slot) ??
                DefaultConstructionLot(project);
        }

        private static BuildingProject PlaceProjectOnSavedLot(BuildingProject project)
        {
            var lot = SavedConstructionLot(project);
            return lot == null ? project : project.AtLot(lot.Origin);
        }

        private void RefreshConstructionLots(BuildingProject project)
        {
            _availableConstructionLots.Clear();
            _selectedConstructionLot = null;
            _constructionLotRow = 0;
            if (!UsesFlexibleConstructionLot(project)) return;

            foreach (var lot in FlexibleConstructionLots)
            {
                bool occupied = false;
                foreach (var candidate in BuildingProjects)
                {
                    if (candidate.Id == project.Id || !UsesFlexibleConstructionLot(candidate) || !ProjectBuilt(candidate))
                        continue;
                    if (SavedConstructionLot(candidate)?.Slot == lot.Slot)
                    {
                        occupied = true;
                        break;
                    }
                }
                if (!occupied) _availableConstructionLots.Add(lot);
            }
        }

        private void SelectConstructionLot(int row)
        {
            if (row < 0 || row >= _availableConstructionLots.Count) return;
            _constructionLotRow = row;
            _selectedConstructionLot = _availableConstructionLots[row];
            _constructionChoosingLot = false;
            _constructionConfirm = true;
            _constructionMessage = null;
            PlayRetailSound(RetailSfx.Confirm);
        }

        private static int ConstructionPlanFlag(string id) => id switch
        {
            "hospital" => HushClinicTalkFlag,
            "library" => LibraryPlanFlag,
            "alley" => BowlingPlanFlag,
            "gym" => GymPlanFlag,
            "circuit" => CircuitPlanFlag,
            "theater" => TheaterPlanFlag,
            "casino" => CasinoPlanFlag,
            "arcade" => ArcadePlanFlag,
            _ => 0
        };

        private static string ProjectRequirement(BuildingProject project)
        {
            int flag = ConstructionPlanFlag(project.Id);
            if (flag != 0 && !TownRetailScriptFlags.CreatePersistent().IsFlagSet(flag))
                return T("Talk to the resident who requested this project.", "Fale com o morador que pediu esta obra.");

            return project.Id switch
            {
                // Nico's donations complete the fountain directly.
                "fountain" => T("Help Nico with her fundraiser.", "Ajude a Nico na arrecadação."),
                "temple" when !TownSaveVariables.GetBool(TempleRequestKey) =>
                    T("Talk to Jorda about the temple.", "Fale com Jorda sobre o templo."),
                "home_2" or "temple" when CurrentHomeRank == 0 =>
                    T("Expand Koh's house first.", "Amplie a casa do Koh primeiro."),
                _ when project.HutRank > CurrentHutRank + 1 =>
                    T("Order the previous hut expansion first.", "Encomende a ampliação anterior do alojamento primeiro."),
                _ => null
            };
        }

        private void UnlockConstructionPlan(int flag)
        {
            TownRetailScriptFlags.CreatePersistent().SetBit(flag);
            PersistTownProgress();
        }

        private bool OfferConstructionPlan(NPCController npc, int flag, string english, string portuguese) =>
            StoryAsk(npc, english, portuguese,
                (T("Ask Tonka to build it", "Pedir ao Tonka para construir"), () => UnlockConstructionPlan(flag)),
                (T("Later", "Depois"), null));

        private static int JordaBlinkFrame(float started)
        {
            int elapsed = Mathf.FloorToInt((Time.unscaledTime - started) * 60f);
            return StoryPortraitFrame(JordaBlinkDurations, elapsed);
        }

        private Texture2D JordaDialoguePortrait(float started)
        {
            var open = StreamPortrait(JordaPortrait);
            var closed = StreamPortrait(JordaEyesClosed);
            // The existing HD closed-eye artwork matches retail frame 2.
            return JordaBlinkFrame(started) == 2 ? closed ?? open ?? StreamPortrait(JordaEyesClosedRetail) : open;
        }

        private void DrawJordaBlink(Texture2D portrait, Rect box, float started)
        {
            if (Event.current.type != EventType.Repaint) return;
            var halfClosed = StreamPortrait(JordaEyesHalfClosed);
            if (!portrait || !halfClosed || JordaBlinkFrame(started) is not (1 or 3)) return;
            // The original uses the same transitional pose in frames 1 and 3.
            DrawDialoguePortraitPatch(portrait, halfClosed, box, Rect.MinMaxRect(635f / 1024f, 465f / 1536f,
                878f / 1024f, 678f / 1536f));
        }

        // The Temple conversation is recorded in TOWN.BIN around 0x5B56C0.
        // Keep its small pages separate: the original message flow advanced one
        // retail window at a time and included choices between each lesson.
        private bool JordaTalk(NPCController npc, string english, string portuguese, System.Action next = null) =>
            StoryTalk(npc, english, portuguese, JordaPortrait, next);

        private bool JordaAsk(NPCController npc, string english, string portuguese,
            params (string Label, System.Action Apply)[] choices) =>
            StoryAsk(npc, english, portuguese, JordaPortrait, choices);

        private readonly struct JordaStoryLine
        {
            internal readonly string English;
            internal readonly string Portuguese;

            internal JordaStoryLine(string english, string portuguese)
            {
                English = english;
                Portuguese = portuguese;
            }
        }

        private bool JordaTalkSequence(NPCController npc, System.Action next, params JordaStoryLine[] lines)
        {
            if (lines == null || lines.Length == 0)
            {
                next?.Invoke();
                return true;
            }
            return JordaTalkSequenceAt(npc, lines, 0, next);
        }

        private bool JordaTalkSequenceAt(NPCController npc, JordaStoryLine[] lines, int index, System.Action next)
        {
            JordaStoryLine line = lines[index];
            System.Action continueSequence = index + 1 < lines.Length
                ? () => JordaTalkSequenceAt(npc, lines, index + 1, next)
                : next;
            return JordaTalk(npc, line.English, line.Portuguese, continueSequence);
        }

        private static readonly JordaStoryLine[] JordaTowerBasics =
        {
            new("When entering the dungeon of the tower, you can find rare items and monster eggs.",
                "Ao entrar na masmorra da torre, você pode encontrar itens raros e ovos de monstros."),
            new("If you bring these back with you, you can exchange them for money.",
                "Se trouxer essas coisas de volta, poderá trocá-las por dinheiro."),
            new("But the tower is a monster nest. Danger will always follow you.",
                "Mas a torre é um ninho de monstros. O perigo sempre acompanhará você."),
            new("You may be an adult now, but in the eyes of the veteran tamers, you're still a kid.",
                "Você pode ser adulto agora, mas, aos olhos dos domadores veteranos, ainda é uma criança."),
            new("Whether you can survive the dungeon or not all depends on your ability to tame the monsters.",
                "Sobreviver ou não à masmorra depende inteiramente da sua habilidade de domar monstros."),
            new("It's a quality your ancestors had, so you should have inherited the same skills.",
                "É uma qualidade que seus antepassados possuíam, então você deve ter herdado essas habilidades."),
            new("You should get monster eggs to hatch, and train them to fight for you.",
                "Você deve chocar ovos de monstros e treiná-los para lutar por você."),
            new("The key to your success depends on this!", "A chave do seu sucesso depende disso!"),
            new("As you know, you can also sell familiars, but they're not as valuable as the unhatched monster eggs.",
                "Como você sabe, também pode vender familiares, mas eles não valem tanto quanto os ovos não chocados."),
            new("Also, the value of the eggs themselves can differ.",
                "Além disso, o valor dos próprios ovos pode variar."),
            new("Give some thought to which eggs you want to use as your familiars and which you want to sell.",
                "Pense bem em quais ovos quer usar como familiares e quais quer vender.")
        };

        private static readonly JordaStoryLine[] JordaTowerSecrets =
        {
            new("There are three secrets to the tower.", "Há três segredos na torre."),
            new("First, it is said that you will always get lost when first entering the tower. Why do you think this is?",
                "Primeiro, dizem que você sempre se perde ao entrar na torre. Por que será?"),
            new("There is a spell cast on the inner structure so that the interior will change.",
                "Há um feitiço lançado na estrutura interna que faz o interior mudar."),
            new("This is the first secret.", "Esse é o primeiro segredo."),
            new("Next, even the warriors that repeatedly challenge return after only reaching the lower levels. Why do you think this is?",
                "Depois, até guerreiros que desafiam a torre repetidamente voltam após chegar apenas aos andares baixos. Por quê?"),
            new("Because there is a spell cast at the entrance to reduce the power of human beings.",
                "Porque há um feitiço na entrada que reduz o poder dos seres humanos."),
            new("That's why everyone starts at the first level each and every time.",
                "É por isso que todos começam no primeiro andar todas as vezes."),
            new("This is the second secret.", "Esse é o segundo segredo."),
            new("The last secret has to do with what you have.", "O último segredo tem a ver com o que você carrega."),
            new("If you try to enter with too many items, the entrance of the tower will close, not allowing you to enter.",
                "Se tentar entrar com itens demais, a entrada da torre se fechará e não deixará você entrar."),
            new("Probably because it's the monster tower, a place where demons with empty hearts rest.",
                "Provavelmente porque é a torre dos monstros, um lugar onde demônios de coração vazio repousam.")
        };

        private bool JordaTowerLesson(NPCController npc)
        {
            return JordaAsk(npc, "Do you want me to give you information on the specifics of the tower?",
                "Quer que eu explique os detalhes da torre?",
                (T("Yes.", "Sim."), () => JordaTalk(npc, "I'm going to instruct you thoroughly, so listen up.",
                    "Vou ensinar tudo em detalhe, então preste atenção.", () => JordaTeachBasics(npc))),
                (T("I don't need you to tell me.", "Não preciso que me diga."), () => JordaTalk(npc,
                    "Ha, ha, ha...", "Ha, ha, ha...")));
        }

        private void JordaTeachBasics(NPCController npc)
        {
            JordaTalkSequence(npc, () => JordaAsk(npc, "Okay, do you understand to this point?",
                    "Certo, você entendeu até aqui?",
                    (T("Yes.", "Sim."), () => JordaOfferSecrets(npc)),
                    (T("Not at all.", "De jeito nenhum."), () => JordaRepeatBasics(npc))),
                JordaTowerBasics);
        }

        private void JordaRepeatBasics(NPCController npc)
        {
            JordaTalk(npc, "I'll tell you one more time, so make sure you listen well.",
                "Vou explicar mais uma vez, então trate de ouvir direito.", () =>
                JordaTalkSequence(npc, () => JordaAsk(npc, "All right, NOW do you understand?",
                        "Muito bem, AGORA você entendeu?",
                        (T("Yes.", "Sim."), () => JordaOfferSecrets(npc)),
                        (T("Not at all.", "De jeito nenhum."), () => JordaOfferSecrets(npc))),
                    JordaTowerBasics));
        }

        private bool JordaOfferSecrets(NPCController npc) =>
            JordaAsk(npc, "Moving along to the next point...", "Vamos ao próximo ponto...",
                (T("Go on, go on.", "Pode continuar."), () => JordaTeachSecrets(npc)),
                (T("I’ve heard enough.", "Já ouvi o bastante."), () => JordaFinishTowerLesson(npc)));

        private void JordaTeachSecrets(NPCController npc)
        {
            JordaTalkSequence(npc, () => JordaAsk(npc, "Okay, do you understand to this point?",
                    "Certo, você entendeu até aqui?",
                    (T("Yes.", "Sim."), () => JordaFinishTowerLesson(npc)),
                    (T("Not at all.", "De jeito nenhum."), () => JordaRepeatSecrets(npc))),
                JordaTowerSecrets);
        }

        private void JordaRepeatSecrets(NPCController npc)
        {
            JordaTalk(npc, "You’ve got to be kidding! ...... Oh well.", "Só pode ser brincadeira! ...... Pois bem.", () =>
                JordaTalk(npc, "I’ll tell you one more time, so make sure you listen well.",
                    "Vou explicar mais uma vez. Trate de prestar atenção.", () =>
                        JordaTalkSequence(npc, () => JordaAsk(npc, "All right, NOW do you understand?",
                                "Muito bem. AGORA entendeu?",
                                (T("Yes.", "Sim."), () => JordaFinishTowerLesson(npc)),
                                (T("Not at all", "De jeito nenhum."), () =>
                                    JordaTalkSequence(npc, () => JordaFinishTowerLesson(npc),
                                        new JordaStoryLine("You’ve got to be kidding! ...... Oh well.",
                                            "Só pode ser brincadeira! ...... Pois bem."),
                                        new JordaStoryLine("I told you, I’m not going to repeat myself.",
                                            "Já avisei que não vou repetir."),
                                        new JordaStoryLine("You really should be listening when someone is talking.",
                                            "Devia prestar atenção quando alguém fala com você.")))),
                            JordaTowerSecrets)));
        }

        private void JordaFinishTowerLesson(NPCController npc)
        {
            JordaTalkSequence(npc, null,
                new JordaStoryLine("Listen, {name}. Everything I told you, I heard from Guy.",
                    "Escute, {name}. Tudo o que contei a você eu ouvi do Guy."),
                new JordaStoryLine("Think of it as your father telling you, and don't forget it.",
                    "Pense nisso como se seu pai estivesse contando a você, e não se esqueça."));
        }

        private bool JordaTempleRebuildStory(NPCController npc)
        {
            return JordaTalk(npc, "Oh, hi {name}. You have really come a long way.",
                "Ah, olá, {name}. Você já chegou longe.", () => JordaAsk(npc,
                    "So, why are you here today?", "Então, por que veio hoje?",
                    (T("To build a bigger temple.", "Para construir um templo maior."), () =>
                        JordaTalkSequence(npc, () => JordaAsk(npc,
                                "But to rebuild this would cost 4,000 G. Do you have that kind of money?",
                                "Mas a reforma custaria 4.000 G. Você tem esse dinheiro?",
                                (T("I’ll give you 4,000 G.", "Vou dar 4.000 G."), () => JordaAcceptTempleOffer(npc)),
                                (T("I haven’t got that much.", "Não tenho tudo isso."), () => JordaTempleCannotPay(npc))),
                            new JordaStoryLine("What did you say!? You’re going to rebuild this temple!?",
                                "O que você disse? Vai reformar este templo?!"),
                            new JordaStoryLine("That’s wonderful! Bless your heart!",
                                "Que maravilha! Deus abençoe seu coração!"),
                            new JordaStoryLine("It’s a fact that this temple is becoming too small for its purposes.",
                                "A verdade é que este templo está ficando pequeno para o que precisamos."))),
                    (T("Nothing in particular.", "Nada em especial."), () => JordaTalk(npc,
                        "Ha, ha, Doesn’t matter. Even if you’re here for no reason, just the fact that you’ve come is a big step forward.",
                        "Ha, ha. Mesmo que tenha vindo sem motivo, só de estar aqui já é um grande passo."))));
        }

        private void JordaTempleCannotPay(NPCController npc) =>
            JordaTalk(npc, "Don’t feel bad, it’s the feeling that counts.", "Não fique triste. O que conta é a intenção.");

        private void JordaAcceptTempleOffer(NPCController npc)
        {
            if (_gold < TempleRenovationCost)
            {
                JordaTempleCannotPay(npc);
                return;
            }

            JordaTalk(npc, "Is that right? Well in that case, I shall gratefully accept.",
                "É mesmo? Nesse caso, aceito com gratidão.", () =>
                {
                    TownSaveVariables.SetBool(TempleRequestKey, true);
                    PersistTownProgress();
                    if (!StoryBuilding("alley") && !TownRetailScriptFlags.CreatePersistent().IsFlagSet(BowlingPlanFlag))
                        JordaBowlingProposal(npc);
                });
        }

        // TOWN.BIN 0x5CB806–0x5CBABA.
        private bool JordaTempleThanks(NPCController npc)
        {
            return JordaTalk(npc, "{name}. What do you think? Don’t you think the temple turned out great?",
                "{name}, o que achou? O templo ficou ótimo, não acha?", () => JordaAsk(npc,
                    "I owe this all to you. Your father up in heaven would be mighty proud of you.",
                    "Devo tudo isso a você. Seu pai, lá no céu, ficaria muito orgulhoso.",
                    (T("I’m doing this for the city.", "Estou fazendo isso pela cidade."), () =>
                        JordaTalkSequence(npc, MarkJordaTempleThanked,
                            new JordaStoryLine("That’s great! That’s wonderful!! What happened to you!?",
                                "Que ótimo! Que maravilha! O que aconteceu com você?!"),
                            new JordaStoryLine("As a kid you were always up to no good.",
                                "Quando era criança, você vivia aprontando."))),
                    (T("I’m regretting it, though.", "Mas estou me arrependendo."), () =>
                        JordaTalkSequence(npc, MarkJordaTempleThanked,
                            new JordaStoryLine("Ha, ha... Well, at least you’re honest.",
                                "Ha, ha... Pelo menos você é sincero."),
                            new JordaStoryLine("But those feelings of regret should pass once you see the people’s delighted faces.",
                                "O arrependimento vai passar quando você vir a alegria no rosto das pessoas.")))));
        }

        private void MarkJordaTempleThanked()
        {
            TownSaveVariables.SetBool(TempleThankedKey, true);
            PersistTownProgress();
        }

        private bool JordaBowlingProposal(NPCController npc)
        {
            return JordaTalkSequence(npc, () => UnlockConstructionPlan(BowlingPlanFlag),
                new JordaStoryLine("{name}. If you want to help this town, I'm sure people would be delighted if you could build places where the people can go to enjoy themselves.",
                    "{name}, se quiser ajudar esta cidade, tenho certeza de que as pessoas adorariam ter lugares onde possam se divertir."),
                new JordaStoryLine("Let’s see, for example how about a [Bowling Center]?",
                    "Vejamos... que tal um salão de boliche?"),
                new JordaStoryLine("People can get together and have fun, that would be great to have.",
                    "As pessoas poderiam se reunir e se divertir. Seria ótimo ter um."));
        }

        private bool JordaCasinoProposal(NPCController npc)
        {
            return JordaTalkSequence(npc, () => UnlockConstructionPlan(CasinoPlanFlag),
                new JordaStoryLine("{name}, oh {name}.", "{name}, ah, {name}."),
                new JordaStoryLine("I’ve been thinking since the last time, on what the people of this town would enjoy,",
                    "Desde a última vez venho pensando no que o pessoal da cidade gostaria de fazer para se divertir,"),
                new JordaStoryLine("and came up with the idea of a [Casino]. What do you think? Gambling can be a lot of fun.",
                    "e tive a ideia de um cassino. O que você acha? Apostar pode ser muito divertido."),
                new JordaStoryLine("But don't tell anybody this was my idea.",
                    "Mas não conte a ninguém que essa foi minha ideia."));
        }

        private bool FonTalk(NPCController npc, string english, string portuguese, System.Action next = null) =>
            StoryTalk(npc, english, portuguese, FonPortrait, next);

        private bool FonAsk(NPCController npc, string english, string portuguese,
            params (string Label, System.Action Apply)[] choices) =>
            StoryAsk(npc, english, portuguese, FonPortrait, choices);

        private readonly struct FonStoryLine
        {
            internal readonly string English;
            internal readonly string Portuguese;

            internal FonStoryLine(string english, string portuguese)
            {
                English = english;
                Portuguese = portuguese;
            }
        }

        private bool FonTalkSequence(NPCController npc, System.Action next, params FonStoryLine[] lines)
        {
            if (lines == null || lines.Length == 0)
            {
                next?.Invoke();
                return true;
            }
            return FonTalkSequenceAt(npc, lines, 0, next);
        }

        private bool FonTalkSequenceAt(NPCController npc, FonStoryLine[] lines, int index, System.Action next)
        {
            FonStoryLine line = lines[index];
            System.Action continueSequence = index + 1 < lines.Length
                ? () => FonTalkSequenceAt(npc, lines, index + 1, next)
                : next;
            return FonTalk(npc, line.English, line.Portuguese, continueSequence);
        }

        // TOWN.BIN 0x2DA806–0x2DAF20.
        private bool FonConstructionStory(NPCController npc, bool theaterPlanned)
        {
            if (theaterPlanned)
                return FonTheaterPlanReminder(npc);

            return FonAsk(npc, "{name}!\nHow’s it going?", "{name}!\nComo vão as coisas?",
                (T("Not too bad.", "Nada mal."), () =>
                    FonTalk(npc, "That’s good!\nTell me if you have something juicy.",
                        "Que bom! Me conte se tiver alguma novidade interessante.", () => FonEntertainmentQuestion(npc))),
                (T("Not good at all.", "Nada bem."), () =>
                    FonTalk(npc, "Wish you well.", "Desejo-lhe sorte.", () => FonEntertainmentQuestion(npc))));
        }

        private void FonEntertainmentQuestion(NPCController npc)
        {
            FonAsk(npc, "By the way, are you interested in entertainment, {name}?",
                "A propósito, você se interessa por entretenimento, {name}?",
                (T("I say no to fenderbender", "Eu digo não à confusão."), () =>
                    FonTalk(npc, "Whhaat?\nShoo, shoo. I’m busy looking for sponsors. Go away.",
                        "O quêêê? Xô, xô. Estou ocupado procurando patrocinadores. Vá embora.")),
                (T("I am the entertainment!", "Eu sou o entretenimento!"), () =>
                    FonTheaterProposal(npc)));
        }

        private void FonTheaterProposal(NPCController npc)
        {
            FonTalkSequence(npc, () => UnlockConstructionPlan(TheaterPlanFlag),
                new FonStoryLine("{name}, baby. Let’s get along.", "{name}, meu chapa. Vamos nos dar bem."),
                new FonStoryLine("To commemorate our friendship, I’ll tell you something very juicy. Theater! {name}, listen.",
                    "Para comemorar nossa amizade, vou lhe contar uma coisa muito interessante. Teatro! {name}, escute."),
                new FonStoryLine("The future is an age of culture! This town Monsbaiya is going to be famous for its theater.",
                    "O futuro é a era da cultura! Esta cidade, Monsbaiya, ficará famosa por seu teatro."),
                new FonStoryLine("You said you were going to make money in the tower.",
                    "Você disse que iria ganhar dinheiro na Torre."),
                new FonStoryLine("How about building a theater and hiring me, Fon, the entertainer?!",
                    "Que tal construir um teatro e contratar a mim, Fon, o artista?!"),
                new FonStoryLine("I promise you sold out shows every night.\nTheater it is, don’t forget it!",
                    "Eu prometo espetáculos lotados todas as noites.\nEntão é teatro, não se esqueça!"));
        }

        private bool FonTheaterPlanReminder(NPCController npc)
        {
            return FonTalkSequence(npc, () =>
                FonAsk(npc, "We’re such good friends! Oh please! Build a theater, c’mon!",
                    "Somos tão bons amigos! Ah, por favor! Construa um teatro, vamos!",
                    (T("I’ll think about it.", "Vou pensar no assunto."), () =>
                        FonTalk(npc, "I asked you, OK?", "Eu pedi, está bem?")),
                    (T("No.", "Não."), () =>
                        FonTalk(npc, "Oh no! What a terrible things to say to a friend, {name}, you meanie!",
                            "Ah, não! Que coisa terrível de dizer a um amigo, {name}, seu malvado!"))),
                new FonStoryLine("{name}! {name}!", "{name}! {name}!"),
                new FonStoryLine("How come you aren’t building a theater? You meanie! Oh please! Please build a theater!",
                    "Por que você ainda não construiu um teatro? Seu malvado! Ah, por favor! Construa um teatro!"));
        }

        // TOWN.BIN 0x2D6809–0x2D697F.
        private bool FonTheaterDoorStory(NPCController npc)
        {
            if (TownSaveVariables.GetBool(StoryPrefix + "fon.theaterDoorThanked"))
                return FonTalk(npc, "We will finish the preparation soon so look forward to it, won’t you?",
                    "Terminaremos os preparativos em breve. Está ansioso, não está?");
            return FonTalkSequence(npc, () =>
                {
                    TownSaveVariables.SetBool(StoryPrefix + "fon.theaterDoorThanked", true);
                    PersistTownProgress();
                },
                new FonStoryLine("Hey, {name}! Hey, {name}!\nThank you ! I just love it!",
                    "Ei, {name}! Ei, {name}!\nObrigado! Adorei!"),
                new FonStoryLine("You built such a magnificant theater for me!", "Você construiu um teatro tão magnífico para mim!"),
                new FonStoryLine("What ?\nNot for me ?", "O quê?\nNão foi para mim?"),
                new FonStoryLine("Don’t be so uptight.\nI will make Monsbaiya the best place for entertainment.",
                    "Não leve tudo tão a sério.\nFarei de Monsbaiya o melhor lugar para entretenimento."));
        }

        private bool PenConstructionStory(NPCController npc, bool arcadePlanned)
        {
            int score = ConstructionUpgradeScore;
            int stage = TownSaveVariables.GetInt(ArcadeRequestStageKey);
            if (arcadePlanned)
                return StoryTalk(npc, "The amusement-center plans are with Tonka. Once you have 3,000 G, he can begin.",
                    "Os planos do centro de diversão estão com o Tonka. Quando você tiver 3.000 G, ele poderá começar.");
            if (score < 1)
                return StoryTalk(npc, "A town needs to grow before it can play. Finish your first building improvement, then come back.",
                    "Uma cidade precisa crescer antes de poder brincar. Faça sua primeira melhoria e depois volte.");
            if (score < 2)
                return StoryTalk(npc, "That is one improvement! Keep building—Monsbaiya is beginning to feel alive.",
                    "Essa é uma melhoria! Continue construindo — Monsbaiya está começando a ganhar vida.");
            if (score < 3)
                return StoryTalk(npc, "Two improvements already. One more and I will tell you my biggest dream.",
                    "Duas melhorias já. Mais uma e vou contar meu maior sonho.");
            if (stage < 1)
                return StoryTalk(npc, "My dream is an amusement center: games, lights, and a place where people can forget the Tower for a while.",
                    "Meu sonho é um centro de diversão: jogos, luzes e um lugar onde as pessoas esqueçam a Torre por um tempo.", () =>
                    {
                        TownSaveVariables.SetInt(ArcadeRequestStageKey, 1);
                        PersistTownProgress();
                    });
            if (stage < 2)
                return StoryAsk(npc, "Do you want me to ask Tonka for a 3,000 G amusement-center project?",
                    "Quer que eu peça ao Tonka um projeto de centro de diversão de 3.000 G?",
                    (T("Do you want me to pay for it?", "Quer que eu pague por isso?"), () =>
                    {
                        TownSaveVariables.SetInt(ArcadeRequestStageKey, 2);
                        PersistTownProgress();
                    }),
                    (T("Not yet", "Ainda não"), null));
            return StoryAsk(npc, "Will you hand Tonka the money and make the amusement center real?",
                "Você vai entregar o dinheiro ao Tonka e tornar o centro de diversão realidade?",
                (T("I'll hand you that money", "Vou entregar esse dinheiro"), () => UnlockConstructionPlan(ArcadePlanFlag)),
                (T("Later", "Depois"), null));
        }

        // TOWN.BIN 0x66D823–0x66E546.
        private bool SilverConstructionStory(NPCController npc, bool circuitPlanned)
        {
            if (circuitPlanned)
                return TownDialogueSequence(npc, SilverPortrait, null,
                    ("Har, har, har.\nMonster racing is a lot of fun.", "Ha, ha, ha.\nCorridas de monstros são muito divertidas."),
                    ("I believe you about the Monster Race Course.\nAsk the carpenter about it!",
                        "Eu acredito em você sobre a Pista de Corrida de Monstros.\nFale com o carpinteiro!"));
            if (TownSaveVariables.GetInt(CircuitRequestStageKey) != 0)
                return SilverRaceTrackOffer(npc);
            return TownDialogueSequence(npc, SilverPortrait, () =>
                {
                    TownSaveVariables.SetInt(CircuitRequestStageKey, 1);
                    PersistTownProgress();
                    SilverRaceTrackOffer(npc);
                },
                ("What is it kid?\nYou interested in horses?", "O que foi, garoto?\nGosta de cavalos?"),
                ("I guess it’s true that monster tamers understand the heart of a monster.",
                    "Parece que é verdade que os domadores entendem o coração dos monstros."),
                ("In the old days I would take a horse that I brought up here to the Monster Race Course in the city of Haruka, and win lots of money with my son.",
                    "Antigamente, eu levava um cavalo que criei aqui para a Pista de Corrida de Monstros na cidade de Haruka e ganhava muito dinheiro com meu filho."),
                ("Now that’s not possible.\nWith my leg in this state, I cannot endure the long journey.",
                    "Agora isso não é possível.\nCom minha perna assim, não aguento a longa viagem."),
                ("I live on the money I make from selling my horses to the Treasure Hunters.",
                    "Vivo do dinheiro que ganho vendendo meus cavalos aos caçadores de tesouros."),
                ("If only I had the money, I would rebuild this into a Monster Race Course, and then I’d be in business again.",
                    "Se eu tivesse dinheiro, reconstruiria isto como uma Pista de Corrida de Monstros e poderia voltar aos negócios."),
                ("When that happens, I’ll be happy to use you as a jockey from time to time.",
                    "Quando isso acontecer, ficarei feliz em chamar você para ser jóquei de vez em quando."),
                ("Is there something else you wanted?", "Você queria mais alguma coisa?"),
                ("Har, har, har.\nMonster racing is a lot of fun.", "Ha, ha, ha.\nCorridas de monstros são muito divertidas."));
        }

        private bool SilverRaceTrackOffer(NPCController npc) =>
            SilverAsk(npc, "What is it?", "O que foi?",
                (T("Nothing.", "Nada."), null),
                (T("I’m building a race track!", "Vou construir uma pista de corrida!"), () =>
                    TownDialogueSequence(npc, SilverPortrait, () =>
                        SilverAsk(npc, "It’ll cost 30000G.", "Vai custar 30000G.",
                            (T("I don’t have enough money.", "Não tenho dinheiro suficiente."), () =>
                                SilverTalk(npc, "Didn’t think so.", "Bem que eu imaginei.")),
                            (T("Money is not a concern.", "Dinheiro não é problema."), () => SilverAcceptsRaceTrack(npc))),
                        ("Har, har, har, har.\nYeah, that’s the spirit.", "Ha, ha, ha, ha.\nIsso, é assim que se fala."),
                        ("......\n... you serious?", "......\n... está falando sério?"))));

        private void SilverAcceptsRaceTrack(NPCController npc)
        {
            const string nameKnown = StoryPrefix + "silver.knowsName";
            int highestFloor = TownReputation.Read().HighestFloor;
            if (highestFloor >= 20 && !TownSaveVariables.GetBool(nameKnown))
            {
                TownDialogueSequence(npc, SilverPortrait, () =>
                    {
                        TownSaveVariables.SetBool(nameKnown, true);
                        SilverRaceTrackAgreement(npc);
                    },
                    ("... I don’t believe it.\nWho are you, anyway?", "... Não acredito.\nQuem é você, afinal?"),
                    ("... what, you’re {name}!?\nOf course, there isn’t a soul in the city that doesn’t know your name.",
                        "... Como, você é {name}!?\nClaro, não há uma alma na cidade que não conheça seu nome."),
                    ($"In that tower where the veteran adults return after only making it to level one or two, I hear you’ve made it all the way to level {highestFloor}.",
                        $"Naquela torre de onde os adultos mais experientes voltam depois de chegar só ao primeiro ou segundo andar, ouvi dizer que você chegou até o andar {highestFloor}."),
                    ("That’s amazing.\nJust incredible.", "Impressionante.\nSimplesmente incrível."),
                    ("Well if it’s you thats offering, {name}, I guess it’s true.",
                        "Bem, se é você que está oferecendo, {name}, então deve ser verdade."),
                    ("Good.  Will you let me manage the place?", "Ótimo. Vai me deixar administrar a pista?"));
                return;
            }

            SilverTalk(npc, "... I don’t believe it.\nYou are serious.\nThen will you let me manage the place?",
                "... Não acredito.\nVocê está falando sério.\nEntão vai me deixar administrar a pista?", () => SilverRaceTrackAgreement(npc));
        }

        private void SilverRaceTrackAgreement(NPCController npc)
        {
            TownDialogueSequence(npc, SilverPortrait, () => UnlockConstructionPlan(CircuitPlanFlag),
                ("I’ll make sure you’re treated royally when you come.",
                    "Quando você vier, farei questão de que seja tratado como um rei."),
                ("If you could call upon the carpenter now, it should be built by the time you come back from one adventure.",
                    "Se você chamar o carpinteiro agora, a pista deve ficar pronta quando você voltar de uma aventura."),
                ("Har, har, har, har, har.\nLife is wonderful.", "Ha, ha, ha, ha, ha.\nA vida é maravilhosa."));
        }

        private bool SilverTalk(NPCController npc, string english, string portuguese, System.Action next = null) =>
            StoryTalk(npc, english, portuguese, SilverPortrait, next);

        private bool SilverAsk(NPCController npc, string english, string portuguese,
            params (string Label, System.Action Apply)[] choices) =>
            StoryAsk(npc, english, portuguese, SilverPortrait, choices);

        // TOWN.BIN 0x61B7F6–0x61C30B.
        private bool BarryShopConversation(NPCController npc)
        {
            int highestFloor = TownSaveVariables.GetInt(TownReputation.HighestFloorKey);
            int stage = TownSaveVariables.GetInt(GymRequestStageKey);
            bool gymBuilt = StoryBuilding("gym");
            var flags = TownRetailScriptFlags.CreatePersistent();
            if (gymBuilt)
                return TownDialogueSequence(npc, null, OpenBarryServices,
                    ("What?. Well.\nBut you’re so much more powerful now, {name}.",
                        "O quê? Bem.\nMas você está muito mais forte agora, {name}."),
                    ("I like to keep fit and lift weights myself.\nI go to the Gym often.",
                        "Gosto de me manter em forma e de levantar pesos.\nVou à academia com frequência."),
                    ("But my dad gets angry at me because there’s no one at this store.",
                        "Mas meu pai fica bravo comigo porque não fica ninguém na loja."),
                    ("Regardless, I am full of praise for you, {name}.",
                        "De qualquer forma, só tenho elogios para você, {name}."));
            if (!flags.IsFlagSet(GymPlanFlag) && (highestFloor >= 30 || stage >= 4 || TownSaveVariables.GetBool(GymRequestKey)))
                return TownDialogueSequence(npc, null, () =>
                    {
                        UnlockConstructionPlan(GymPlanFlag);
                        OpenBarryServices();
                    },
                    ("Welcome.\nI’m naturally expecting some business.", "Bem-vindo.\nEspero fazer negócios, é claro."),
                    ("What?... Well.\nBut you’re so much more powerful now, {name}.",
                        "O quê?... Bem.\nMas você está muito mais forte agora, {name}."),
                    ("I like to keep fit and lift weights myself.  The other day I heard about a Gym.",
                        "Gosto de me manter em forma e de levantar pesos. Outro dia, ouvi falar de uma academia."),
                    ("It’s in some other city.\nIt would be nice to have one here too.",
                        "Fica em outra cidade.\nSeria bom ter uma aqui também."));
            if (stage == 0)
                return TownDialogueSequence(npc, null, () =>
                    StoryAsk(npc, "You’re only here to bug me anyway.", "De qualquer jeito, você só veio me amolar.",
                        (T("No I’m not!", "Não vim, não!"), () => BarryFirstConversation(npc)),
                        (T("You’re absolutely right.", "Você tem toda razão."), OpenBarryServices)),
                    ("Welco...  Oh, it’s you {name}.\nDon’t get in the way of my business, {name}.",
                        "Bem-vi... Ah, é você, {name}.\nNão atrapalhe meus negócios, {name}."));
            if (highestFloor >= 20 && stage < 3)
                return StoryAsk(npc, "Welcome {name}.\nWhat?... Well.  By the way, how is it at the tower?",
                    "Bem-vindo, {name}.\nO quê?... Bem. A propósito, como estão as coisas na torre?",
                    (T("Nothing to it.", "É moleza."), () => BarryTowerTraining(npc)),
                    (T("It’s a bit scary.", "Dá um pouco de medo."), () => BarryTowerTraining(npc)),
                    (T("Great place to earn money.", "É um ótimo lugar para ganhar dinheiro."), () => BarryTowerTraining(npc)));
            if (highestFloor >= 10 && stage == 1)
                return TownDialogueSequence(npc, null, () => SaveBarryConversation(2),
                    ("Hello {name}.", "Olá, {name}."),
                    ("What?... Well.  You seem different from before, {name}.\nWhat could it be?",
                        "O quê?... Bem. Você parece diferente, {name}.\nO que será?"));
            return StoryTalk(npc, "Oh, it’s you {name}.\nI really can’t expect business from you.",
                "Ah, é você, {name}.\nNão posso esperar fazer negócios com você.", OpenBarryServices);
        }

        private void BarryFirstConversation(NPCController npc)
        {
            TownDialogueSequence(npc, null, () =>
                StoryAsk(npc, "You can’t be a brat forever, you know.", "Não dá para ser um moleque para sempre, sabe?",
                    (T("I’m a hunter.", "Sou caçador."), () =>
                        StoryTalk(npc, "The ruins of the tower.", "As ruínas da torre.", () => SaveBarryConversation(1))),
                    (T("I’m a kid forever.", "Vou ser criança para sempre."), () =>
                        StoryTalk(npc, "You might be a kid at heart, but your appearance will give away your age.",
                            "Você pode ser criança de coração, mas sua aparência vai entregar sua idade.", () => SaveBarryConversation(1)))),
                ("As I recall...", "Se bem me lembro..."),
                ("You’ve had your fifteenth birthday, to officially become an adult, right?",
                    "Você já fez quinze anos e virou adulto oficialmente, certo?"),
                ("I was fifteen when I started looking after this store.", "Eu tinha quinze anos quando comecei a cuidar da loja."));
        }

        private void BarryTowerTraining(NPCController npc)
        {
            TownDialogueSequence(npc, null, () => SaveBarryConversation(3),
                ("But you’re looking so much more powerful, {name}.", "Mas você parece muito mais forte, {name}."),
                ("It’s a good way to train your body.  I’d like to go to the tower myself.",
                    "É uma boa maneira de treinar o corpo. Eu mesmo gostaria de ir à torre."),
                ("But I’ve got to look after the store.", "Mas tenho que cuidar da loja."));
        }

        private void SaveBarryConversation(int stage)
        {
            TownSaveVariables.SetInt(GymRequestStageKey, stage);
            PersistTownProgress();
            OpenBarryServices();
        }

        private void OpenBarryServices() => SetBarryShopPage(BarryShopPage.Services);

        private bool TryConstructionStory(NPCController npc)
        {
            var flags = TownRetailScriptFlags.CreatePersistent();
            int highestFloor = TownSaveVariables.GetInt(TownReputation.HighestFloorKey);
            switch (npc.npcId)
            {
                case "tonka" when InCarpenterShop:
                    return TonkaConstructionStory(npc);
                case "hush" when StoryRoom == HushClinic && highestFloor > 0 && !StoryBuilding("hospital"):
                    return HushClinicStory(npc);
                case "jorda" when StoryRoom == "Interior_11_temple":
                    if (CurrentHomeRank > 0 && !StoryBuilding("temple") &&
                        !TownSaveVariables.GetBool(TempleRequestKey))
                        return JordaTempleRebuildStory(npc);
                    if (TownSaveVariables.GetBool(TempleRequestKey) && !StoryBuilding("alley") && !flags.IsFlagSet(BowlingPlanFlag))
                        return JordaBowlingProposal(npc);
                    return JordaTowerLesson(npc);
                case "jorda" when StoryRoom == "Interior_12_temple_2":
                    if (!TownSaveVariables.GetBool(TempleThankedKey))
                        return JordaTempleThanks(npc);
                    if (!StoryBuilding("casino") && !flags.IsFlagSet(CasinoPlanFlag))
                        return JordaCasinoProposal(npc);
                    if (!StoryBuilding("alley") && !flags.IsFlagSet(BowlingPlanFlag))
                        return JordaBowlingProposal(npc);
                    if (StoryBuilding("casino"))
                        return JordaTalk(npc, "Ha, ha, ha...", "Ha, ha, ha...");
                    return JordaTalk(npc, "Look at how beautiful this place is!", "Veja como este lugar está bonito!");
                case "construction_fon" or "carpenter_fon" when !StoryBuilding("theater"):
                    return FonConstructionStory(npc, flags.IsFlagSet(TheaterPlanFlag));
                case "theater_door_fon" when StoryBuilding("theater"):
                    return FonTheaterDoorStory(npc);
                case "construction_silver" or "silver" when !StoryBuilding("circuit"):
                    return SilverConstructionStory(npc, flags.IsFlagSet(CircuitPlanFlag));
                case "construction_pen" when !StoryBuilding("arcade"):
                    return PenConstructionStory(npc, flags.IsFlagSet(ArcadePlanFlag));
            }
            return false;
        }

        private void SpawnConstructionResidents()
        {
            if (StoryBuilding("theater"))
            {
                // Use the placed lot, rather than the theater's default coordinates:
                // a player may choose a different construction lot for the building.
                var theater = _builtTownLots.Find(project => project.Id == "theater") ??
                    PlaceProjectOnSavedLot(Array.Find(BuildingProjects, project => project.Id == "theater"));
                Vector2 entrance = theater.Entrance;
                SpawnStoryResident("dar", "theater_door_fon", "Fon",
                    new Vector2Int(Mathf.RoundToInt(entrance.x), Mathf.RoundToInt(entrance.y + 1f)));
            }
            else if (StoryStage("vivian") >= 4)
                SpawnStoryResident("dar", "construction_fon", "Fon", new Vector2Int(87, 70));
            if (!StoryBuilding("circuit"))
                SpawnStoryResident("dar", "construction_silver", "Silver", new Vector2Int(21, 45));
        }

        // TOWN.BIN 0x5EA806–0x5EAFC3.
        private bool TonkaConstructionStory(NPCController npc)
        {
            const string introduced = "dialogue.tonka.introduced";
            if (!TownSaveVariables.GetBool(introduced))
                return TownDialogueSequence(npc, null, () =>
                    {
                        TownSaveVariables.SetBool(introduced, true);
                        PersistTownProgress();
                        TonkaWorkChoice(npc, "He was a great warrior and tamer.\nWhat is it today?",
                            "Ele foi um grande guerreiro e domador.\nO que vai ser hoje?");
                    },
                    ("Oh, it’s you {name}.", "Ah, é você, {name}."),
                    ("You’re looking much more like your father than the last time I saw you some time ago.",
                        "Você está muito mais parecido com seu pai do que da última vez que o vi, já faz algum tempo."));

            return CurrentHomeRank switch
            {
                2 => TownDialogueSequence(npc, null, () =>
                        TonkaWorkChoice(npc, "For some reason, when I see you, I can’t wait to get right to work.",
                            "Por algum motivo, quando vejo você, mal posso esperar para começar a trabalhar."),
                    ("Oh, it’s you {name}.", "Ah, é você, {name}.")),
                1 => TonkaWorkChoice(npc, "Oh, it’s you {name}.\nDid you bring something for me to do?",
                    "Ah, é você, {name}.\nTrouxe algum trabalho para mim?"),
                _ => TonkaWorkChoice(npc, "Oh, it’s you {name}.\nWhat is it today?",
                    "Ah, é você, {name}.\nO que vai ser hoje?")
            };
        }

        private bool TonkaWorkChoice(NPCController npc, string english, string portuguese) =>
            StoryAsk(npc, english, portuguese,
                (T("Do you want some work?", "Quer um trabalho?"), () =>
                    StoryTalk(npc, "What!?  You want me to do work for you!?!?\nOh, sorry.",
                        "O quê!?  Quer que eu faça um trabalho para você!?!?\nAh, foi mal.", OpenCarpenter)),
                (T("I have nothing else to say.", "Não tenho mais nada a dizer."), null));

        private void OpenCarpenter()
        {
            _constructionOpen = true;
            _constructionConfirm = false;
            _constructionChoosingLot = false;
            _constructionRow = 0;
            _constructionLotRow = 0;
            _selectedConstructionLot = null;
            _constructionMessage = null;
            RefreshConstructionProjects();
        }

        private void RefreshConstructionProjects()
        {
            _availableConstructionProjects.Clear();
            foreach (var project in BuildingProjects)
                if (!ProjectBuilt(project) && ProjectRequirement(project) == null)
                    _availableConstructionProjects.Add(project);

            _constructionRow = Mathf.Clamp(_constructionRow, 0, Mathf.Max(0, _availableConstructionProjects.Count - 1));
        }

        private void BackFromCarpenter()
        {
            PlayRetailSound(RetailSfx.Cancel);
            _constructionMessage = null;
            if (_constructionConfirm)
            {
                _constructionConfirm = false;
                _constructionMessage = T("Well, think about it.", "Bem, pense no assunto.");
                if (_selectedConstructionLot != null) _constructionChoosingLot = true;
            }
            else if (_constructionChoosingLot)
            {
                _constructionChoosingLot = false;
                _selectedConstructionLot = null;
            }
            else _constructionOpen = false;
        }

        private bool HandleCarpenterInput()
        {
            if (!_constructionOpen) return false;
            if (!InCarpenterShop) { _constructionOpen = false; return false; }
            if (_cancel?.WasPressedThisFrame() == true) BackFromCarpenter();
            else if (_interact?.WasPressedThisFrame() == true)
            {
                if (_constructionConfirm) ConfirmConstruction();
                else if (_constructionChoosingLot) SelectConstructionLot(_constructionLotRow);
                else ChooseConstruction(_constructionRow);
            }
            else if (_constructionChoosingLot && _availableConstructionLots.Count > 0 && Keyboard.current != null)
            {
                int step = MenuDirectionPressed(1) ? -1 : MenuDirectionPressed(2) ? 1 : 0;
                int next = Mathf.Clamp(_constructionLotRow + step, 0, _availableConstructionLots.Count - 1);
                if (next != _constructionLotRow) PlayRetailSound(RetailSfx.Cursor);
                _constructionLotRow = next;
            }
            else if (!_constructionConfirm && _availableConstructionProjects.Count > 0 && Keyboard.current is { } keys)
            {
                int step = MenuDirectionPressed(1) ? -1 : MenuDirectionPressed(2) ? 1 : 0;
                int next = Mathf.Clamp(_constructionRow + step, 0, _availableConstructionProjects.Count - 1);
                if (next != _constructionRow) PlayRetailSound(RetailSfx.Cursor);
                _constructionRow = next;
            }
            return true;
        }

        private void ChooseConstruction(int row)
        {
            if (row < 0 || row >= _availableConstructionProjects.Count) return;
            _constructionRow = row;
            var project = _availableConstructionProjects[row];
            _constructionMessage = ProjectBuilt(project) ? T("Already built.", "Obra já concluída.") : ProjectRequirement(project);
            if (_constructionMessage != null) return;

            RefreshConstructionLots(project);
            if (UsesFlexibleConstructionLot(project))
            {
                if (_availableConstructionLots.Count == 0)
                {
                    _constructionMessage = T("There are no free lots large enough for this project.",
                        "Não há terrenos livres grandes o bastante para esta obra.");
                    return;
                }
                _constructionChoosingLot = true;
                PlayRetailSound(RetailSfx.Confirm);
                return;
            }

            _constructionConfirm = true;
            PlayRetailSound(RetailSfx.Confirm);
        }

        private void ConfirmConstruction()
        {
            if (!_constructionConfirm || !InCarpenterShop) return;
            var project = _availableConstructionProjects[_constructionRow];
            if (ProjectBuilt(project) || ProjectRequirement(project) != null) return;
            if (UsesFlexibleConstructionLot(project) && _selectedConstructionLot == null) return;
            if (_gold < project.Price)
            {
                _constructionMessage = T("You don't have enough gold.", "Você não tem ouro suficiente.");
                return;
            }
            string scene = project.HutRank > 0 ? HutScenes[project.HutRank] :
                project.Id == "home_1" ? HomeScenes[1] : project.Interior == "KohHome" ? HomeScenes[2] : project.Interior;
            if ((project.Bounds.width > 0 && !Resources.Load<TextAsset>(ConstructionResources + project.Lot)) ||
                (scene != null && !Application.CanStreamedLevelBeLoaded("Assets/AzureDreams/Scenes/Interiors/" + scene + ".unity")))
            {
                _constructionMessage = T("This project is unavailable in this build.", "Esta obra não está disponível nesta versão.");
                return;
            }
            if (project.Id == "home_1" || project.Id == "home_2")
                TownSaveVariables.SetInt(HomeRankKey, project.Id == "home_1" ? 1 : 2);
            else if (project.HutRank > 0)
                TownSaveVariables.SetInt(HutRankKey, project.HutRank);
            else
            {
                TownSaveVariables.SetBool(ConstructionKey + project.Id, true);
                if (_selectedConstructionLot != null)
                    TownSaveVariables.SetInt(ConstructionLotKey(project), _selectedConstructionLot.Slot);
            }
            _gold -= project.Price;
            PersistTownProgress();
            _constructionConfirm = false;
            _constructionChoosingLot = false;
            _selectedConstructionLot = null;
            RefreshConstructionProjects();
            if (project.Id == "theater")
            {
                TownSaveVariables.SetBool(StoryPrefix + "fon.theaterBuilt", true);
                PersistTownProgress();
            }
            var tonka = _townPeople.Find(npc => npc && npc.npcId == "tonka");
            if (tonka)
            {
                _constructionOpen = false;
                TownDialogueSequence(tonka, null, OpenCarpenter,
                    ($"Yes, I have {project.Price}G here.\nGive me a little time to build.",
                        $"Sim, recebi {project.Price}G.\nDê-me um tempinho para construir."),
                    ("I should have it built before you return from your adventures.",
                        "Deve ficar pronto antes de você voltar de suas aventuras."),
                    ("I assure you, you’d be pleased with my work.", "Garanto que você vai gostar do meu trabalho."));
            }
            PlayRetailSound(RetailSfx.Confirm);
        }

        private void DrawCarpenter()
        {
            if (!_constructionOpen || !InCarpenterShop) return;
            DrawHudPanel(new Rect(85, 86, 470, 301));
            GUI.Label(new Rect(103, 101, 310, 24), T("Carpenter · Tonka", "Carpintaria · Tonka"));
            GUI.Label(new Rect(429, 101, 110, 24), _gold + " G");
            if (_constructionConfirm)
            {
                var project = _availableConstructionProjects[_constructionRow];
                GUI.Label(new Rect(103, 140, 432, 50), _constructionMessage ??
                    T($"For that, you’d be looking at about {project.Price}G.", $"Para isso, ficaria em cerca de {project.Price}G."));
                if (GUI.Button(new Rect(103, 212, 215, 26), T("I’m buying.", "Vou pagar."))) ConfirmConstruction();
                if (GUI.Button(new Rect(320, 212, 215, 26), T("Naw, forget it.", "Não, esqueça."))) BackFromCarpenter();
                return;
            }
            if (_constructionChoosingLot)
            {
                GUI.Label(new Rect(103, 139, 432, 38), _constructionMessage ??
                    T("Where in the city do you want it built?", "Onde na cidade você quer que eu construa?"));
                for (int i = 0; i < _availableConstructionLots.Count; i++)
                {
                    var lot = _availableConstructionLots[i];
                    if (GUI.Button(new Rect(103, 195 + i * 28, 432, 25),
                        (i == _constructionLotRow ? "› " : "") + lot.Name))
                    { SelectConstructionLot(i); break; }
                }
                if (GUI.Button(new Rect(416, 350, 119, 26), T("Back [Esc]", "Voltar [Esc]"))) BackFromCarpenter();
                return;
            }
            string prompt = _availableConstructionProjects.Count > 0 ? T("So what do you want me to build?", "Então, o que você quer que eu construa?") :
                T("No projects are available.", "Nenhuma obra disponível.");
            GUI.Label(new Rect(103, 139, 432, 38), _constructionMessage ?? prompt);
            int first = _constructionRow / ConstructionRows * ConstructionRows;
            for (int i = first; i < Mathf.Min(first + ConstructionRows, _availableConstructionProjects.Count); i++)
            {
                var project = _availableConstructionProjects[i];
                if (GUI.Button(new Rect(103, 195 + (i - first) * 28, 432, 25),
                    (i == _constructionRow ? "› " : "") + project.Name + $"   {project.Price} G"))
                { ChooseConstruction(i); break; }
            }
            if (first > 0 && GUI.Button(new Rect(103, 350, 110, 26), T("Previous", "Anterior"))) _constructionRow = first - ConstructionRows;
            if (first + ConstructionRows < _availableConstructionProjects.Count &&
                GUI.Button(new Rect(218, 350, 110, 26), T("Next", "Próxima"))) _constructionRow = first + ConstructionRows;
            if (GUI.Button(new Rect(416, 350, 119, 26), T("Back [Esc]", "Voltar [Esc]"))) BackFromCarpenter();
        }

        private void ResetConstructionView()
        {
            _constructionOpen = false;
            _builtTownLots.Clear();
            _constructionGround.Clear();
            _retailTownObjectRoot = null;
            foreach (var asset in _constructionGeometry)
                if (asset) Destroy(asset);
            _constructionGeometry.Clear();
        }

        private void ApplyTownConstruction(GameObject town)
        {
            var shader = Shader.Find("AzureDreams/TownCaptured");
            if (!shader) return;
            foreach (var project in BuildingProjects)
            {
                if (project.Bounds.width <= 0 || !ProjectBuilt(project)) continue;
                // The second upgrade replaces the intermediate exterior.
                if (project.Id == "home_1" && CurrentHomeRank >= 2) continue;
                var data = Resources.Load<TextAsset>(ConstructionResources + project.Lot);
                if (!data) continue;
                var placedProject = PlaceProjectOnSavedLot(project);
                _builtTownLots.Add(placedProject);
                if (placedProject.Footprint.width > 0) _townBuildings.Add(placedProject.Footprint);
                // The final house upgrade has a raised east wing. Its collision must
                // cover the complete captured exterior, not just the small centre
                // section, otherwise Koh can walk through the visible extension.
                if (placedProject.Id == "home_2") _townBuildings.Add(Rect.MinMaxRect(58f, 50f, 62f, 57f));
                CreateConstructionMesh(RetailTownObjectRoot(town.transform.parent), shader, data, placedProject);
            }
            if (_builtTownLots.Count == 0) return;

            foreach (var filter in town.GetComponentsInChildren<MeshFilter>())
            {
                var source = filter.sharedMesh;
                if (!source || !source.isReadable) continue;
                var positions = source.vertices;
                var mesh = Instantiate(source);
                for (int sub = 0; sub < source.subMeshCount; sub++)
                {
                    int[] indices = source.GetTriangles(sub);
                    var kept = new List<int>(indices.Length);
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        var center = filter.transform.TransformPoint(
                            (positions[indices[i]] + positions[indices[i + 1]] + positions[indices[i + 2]]) / 3f);
                        if (IsBuiltLot(new Vector2(center.x, center.z))) continue;
                        kept.Add(indices[i]); kept.Add(indices[i + 1]); kept.Add(indices[i + 2]);
                    }
                    mesh.SetTriangles(kept, sub, false);
                }
                filter.sharedMesh = mesh;
                _constructionGeometry.Add(mesh);
            }
        }

        private bool IsBuiltLot(Vector2 position)
        {
            foreach (var project in _builtTownLots)
                if (project.ReplaceBounds.Contains(position)) return true;
            return false;
        }

        private Transform RetailTownObjectRoot(Transform parent)
        {
            if (_retailTownObjectRoot) return _retailTownObjectRoot;
            var root = new GameObject("Monsbaiya objects — original retail lots");
            root.transform.SetParent(parent, false);
            _generated.Add(root);
            _retailTownObjectRoot = root.transform;
            return _retailTownObjectRoot;
        }

        private void CreateConstructionMesh(Transform parent, Shader shader, TextAsset asset, BuildingProject project)
        {
            using var reader = new BinaryReader(new MemoryStream(asset.bytes));
            int sections = reader.ReadInt32();
            var materials = new Material[sections];
            var triangles = new int[sections][];
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            for (int sub = 0; sub < sections; sub++)
            {
                string textureName = reader.ReadString();
                int count = reader.ReadInt32();
                var texture = Resources.Load<Texture2D>(ConstructionResources + "Textures/" + textureName);
                var material = new Material(shader) { mainTexture = texture };
                materials[sub] = material;
                _constructionGeometry.Add(material);
                triangles[sub] = new int[count];
                for (int i = 0; i < count; i++)
                {
                    triangles[sub][i] = vertices.Count;
                    vertices.Add(new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
                    uv.Add(new Vector2(reader.ReadSingle(), reader.ReadSingle()));
                }
            }
            int sampleCount = reader.ReadInt32();
            for (int i = 0; i < sampleCount; i++)
            {
                int x = reader.ReadByte(), z = reader.ReadByte();
                int placedX = x + Mathf.RoundToInt(project.WorldOffset.x);
                int placedZ = z + Mathf.RoundToInt(project.WorldOffset.y);
                _constructionGround[placedZ * RetailTownSize + placedX] = reader.ReadInt16() / 256f;
            }
            var mesh = new Mesh { name = project.English, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = sections;
            for (int i = 0; i < sections; i++) mesh.SetTriangles(triangles[i], i, false);
            mesh.RecalculateBounds();
            Bounds bounds = mesh.bounds;
            TownRetailOriginalObjectLayout.ValidateVisualBounds(project.Id,
                Rect.MinMaxRect(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z));
            _constructionGeometry.Add(mesh);
            var root = new GameObject(TownRetailOriginalObjectLayout.ObjectName(project.English, project.Id));
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(project.WorldOffset.x, 0f, project.WorldOffset.y);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterials = materials;
            _generated.Add(root);
        }

        private BuildingProject BuiltKohHome() => _builtTownLots.Find(project => project.Interior == "KohHome");

        private Vector3 ConstructionReturnPosition(BuildingProject project)
        {
            var outside = new Vector3(project.Entrance.x, 0f, project.Entrance.y + 1f);
            outside.y = TownTerrainHeight(outside) + TownActorFootOffset;
            return outside;
        }

        private void EnterConstruction(BuildingProject project)
        {
            string interior = project.Interior == "KohHome" ? CurrentHomeScene() : project.Interior;
            StartCoroutine(LoadInterior(interior, ConstructionReturnPosition(project), spawnAboveExitMarker: true));
        }

        private bool TryConstructionEntrance()
        {
            if (_interior || !_player || _heldTownPot || _carriedItem != null) return false;
            var position = _player.transform.position;
            foreach (var project in _builtTownLots)
            {
                if (project.Interior == null || Mathf.Abs(position.x - project.Entrance.x) > 1.05f ||
                    position.z < project.Entrance.y - 0.2f || position.z > project.Entrance.y + 1.7f) continue;
                EnterConstruction(project);
                return true;
            }
            return false;
        }
    }
}
