using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// The Gym (Interior_08_gym): its residents and the Kim Brothers' barbell
    /// minigame.
    ///
    /// Retail sources (TOWN.BIN):
    ///  - Scene overlay at 0x470000 (loaded at 0x80010000).  Its talk table
    ///    at 0x8001159C lists seven residents: speaker 0x34 (the hunter from
    ///    the pub), 0x29/0x2A (big/little brother Kim, one shared script),
    ///    0x10 (Barry), 0x3E, 0x45 and 0x2D.
    ///  - Scene script/text at 0x473000-0x474600 (loaded at 0x80017874).  The
    ///    Kims' script tests F 0x525 (first meeting) and sets F 0x524 to start
    ///    the minigame.
    ///  - Minigame overlay ovl_79617c (TOWN.BIN 0x798C8C+):
    ///    initialize_gym_minigame, update_gym_minigame,
    ///    update_gym_lane_selection and update_gym_score_object.
    ///    Lanes (gym_lane_positions, 0x80026F24) are X = 800/896/992 units on
    ///    the line Y = 0x380; barbells leave the thrower at X = 0x2A0, one
    ///    square up, take 32..63 ticks to reach the lane and fall back to the
    ///    same height on arrival.  A barbell is caught while it is falling
    ///    between 1.5 and 0.25 squares above the floor over Koh's lane; the
    ///    first one to reach the floor ends the game.  Barbells are thrown
    ///    every max(1, 32 - record / 10) ticks.  The record (0x800135C2) starts
    ///    at 20 and is capped with the score at 9999.
    /// </summary>
    public sealed partial class TowerGameController
    {
        private const string GymRoom = "Interior_08_gym";
        private const string GymRecordKey = "gym.record";
        private const string GymKimMetKey = "gym.kim_met";       // F 0x525
        private const string GymKimNamesKey = "gym.kim_names";   // F 0x526
        private const float GymTick = 1f / 30f;
        private const float GymUnits = 64f;                      // retail units per square
        private const float GymLineZ = 0x380 / GymUnits;         // 14
        private const float GymThrowerX = 0x2A0 / GymUnits;      // 10.5
        private static readonly int[] GymLaneUnits = { 800, 896, 992 };
        private static readonly Vector3 GymRestingBarbell = new(0x380 / GymUnits, 0f, 0x3C0 / GymUnits);

        private enum GymPhase { Off, Approach, Ready, Play, Over, Finale, Outro }

        private sealed class GymBarbell
        {
            public GameObject View;
            public float X, Height;          // squares (Height above the floor)
            public float VelocityX, VelocityHeight, Gravity;
            public int Target, State, Timer;
            public bool Final;
        }

        private GymPhase _gymPhase;
        private float _gymAccumulator;
        private int _gymTicks, _gymScore, _gymRecord, _gymLane, _gymLaneDelay, _gymSpawnClock, _gymTimer;
        private float _gymKohX, _gymKohVelocity, _gymKohHop, _gymKohHopVelocity;
        private bool _gymMissed, _gymFinaleDone, _gymNewRecord;
        private Vector3 _gymReturnPosition;
        private Vector2 _gymReturnFacing;
        private readonly List<GymBarbell> _gymBarbells = new();
        private GameObject _gymRestingBarbell;
        private MaterialPropertyBlock _gymTint;
        private readonly HashSet<string> _gymTalked = new();

        private bool InGym => _interior && _interior.interiorId == GymRoom;
        private static float GymLaneX(int lane) => GymLaneUnits[lane] / GymUnits;
        private static float GymTargetX(int lane) => (GymLaneUnits[lane] + lane * 8) / GymUnits;
        private int GymRecord => Mathf.Max(20, TownSaveVariables.GetInt(GymRecordKey));

        // ---------------------------------------------------------------- set-up

        private void SetupGym()
        {
            if (!InGym) return;
            _gymTalked.Clear();
            // initialize_gym_minigame: mesh 0x34 spins over the back wall
            // (0x380, 0x260, one and a half squares up) and four 0x35 meshes
            // spin at the sides (X 0x270/0x490, Y 0x2E0/0x3E0, half a square up).
            CreateGymEmblem(new Vector3(0x380 / GymUnits, 1.5f, 0x260 / GymUnits));
            foreach (float x in new[] { 0x270 / GymUnits, 0x490 / GymUnits })
                foreach (float z in new[] { 0x2E0 / GymUnits, 0x3E0 / GymUnits })
                    CreateGymRoller(new Vector3(x, 0.5f, z));
            _gymRestingBarbell = CreateGymBarbell("Gym barbell (resting)");
            PlaceGymBarbell(_gymRestingBarbell, GymRestingBarbell.x, 0.08f, GymRestingBarbell.z);
        }

        private void SpawnGymResidents()
        {
            // Scene positions are not decoded yet; these keep the residents on
            // the mat and clear of the barbell line (Y = 14).
            SpawnStoryResident("monster_shop_hunter", "gym_kim_little", T("Little Bro Kim", "Kim, o irmão mais novo"),
                default, RetailTownActorPosition(new Vector3(GymThrowerX, 0f, GymLineZ), 0f));
            SpawnStoryResident("monster_shop_hunter", "gym_kim_big", T("Big Bro Kim", "Kim, o irmão mais velho"), default);
            SpawnStoryResident("monster_shop_hunter", "gym_hunter", T("Hunter", "Caçador"), default);
            SpawnRetailResident("gym_barry", "Barry", "Town/Residents/OriginalSprites/Town_Barry");
            SpawnRetailResident("gym_woman", T("Woman", "Mulher"), "Title/AuntRain");
            SpawnRetailResident("gym_legs", T("Old man", "Senhor"), "Title/GrandpaRain");
            SpawnStoryResident("dar", "gym_developer", T("Man", "Homem"), default);

            foreach (var id in new[] { "gym_kim_little", "gym_kim_big" })
            {
                var kim = FindStoryActor(id);
                if (!kim) continue;
                ApplyNativeMinigameResident(id, "Kim");
                var toLine = new Vector3(1f, 0f, 0f);
                kim.transform.rotation = Quaternion.LookRotation(toLine, Vector3.up);
            }
            // Only Barry's upper body was captured; he works out on a bench
            // that hides his legs.
            var barry = FindStoryActor("gym_barry");
            if (barry && barry.GetComponentInChildren<SpriteRenderer>(true) is { } torso)
            {
                torso.transform.localPosition += Vector3.up * 0.32f;
                var bench = GymPart(PrimitiveType.Cube, "Gym bench", barry.transform.position + new Vector3(0f, 0.22f, 0.34f),
                    new Vector3(0.95f, 0.44f, 0.42f), new Color(0.32f, 0.20f, 0.13f));
                bench.transform.SetParent(barry.transform, true);
            }
        }

        private void TintGymResident(GameObject resident, Color tint)
        {
            _gymTint ??= new MaterialPropertyBlock();
            foreach (var renderer in resident.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is SpriteRenderer sprite) { sprite.color = tint; continue; }
                renderer.GetPropertyBlock(_gymTint);
                _gymTint.SetColor("_Color", tint);
                _gymTint.SetColor("_BaseColor", tint);
                renderer.SetPropertyBlock(_gymTint);
            }
        }

        private GameObject GymPart(PrimitiveType type, string label, Vector3 point, Vector3 size, Color color)
        {
            var part = CreatePrimitive(type, label, point, size, null, false);
            _gymTint ??= new MaterialPropertyBlock();
            _gymTint.Clear();
            _gymTint.SetColor("_BaseColor", color);
            _gymTint.SetColor("_Color", color);
            part.GetComponent<Renderer>().SetPropertyBlock(_gymTint);
            return part;
        }

        private GameObject CreateGymBarbell(string label)
        {
            if (label.Contains("0x34")) return CreateNativeMinigameObject("GymEmblem", label);
            if (label.Contains("0x35")) return CreateNativeMinigameObject("GymDumbbell", label);
            var root = new GameObject(label);
            root.transform.SetParent(transform, false);
            var visual = new GameObject("Original barbell sprite — TOWN 0x800F9B40");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.up * 0.25f;
            var texture = AssetMode.Load<Texture2D>("Town/Minigames/GymBarbell");
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 48f);
            visual.AddComponent<SpriteRenderer>().sprite = sprite;
            visual.AddComponent<AzureDreamsReimagined.Rendering.CameraFacingSprite>();
            _constructionGeometry.Add(sprite);
            _generated.Add(root);
            return root;
        }

        private void CreateGymEmblem(Vector3 point)
        {
            var emblem = CreateGymBarbell("Gym emblem (mesh 0x34)");
            emblem.transform.position = new Vector3(point.x, TownTerrainHeight(point) + point.y, point.z);
            emblem.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            // rotate_gym_mesh_y: +0x20 per tick around Y.
            emblem.AddComponent<GymSpinner>().Configure(Vector3.up, 0x20 / 4096f * 360f * 30f);
        }

        private void CreateGymRoller(Vector3 point)
        {
            var roller = CreateGymBarbell("Gym dumbbell (mesh 0x35)");
            roller.transform.position = new Vector3(point.x, TownTerrainHeight(point) + point.y, point.z);
            // rotate_gym_mesh_x: -0x20 per tick around X.
            roller.AddComponent<GymSpinner>().Configure(Vector3.right, -0x20 / 4096f * 360f * 30f);
        }

        private void PlaceGymBarbell(GameObject view, float x, float height, float z)
        {
            if (!view) return;
            var floor = new Vector3(x, 0f, z);
            view.transform.position = new Vector3(x, TownTerrainHeight(floor) + height, z);
        }

        // ------------------------------------------------------------ residents

        private bool GymResidentDialogue(NPCController npc)
        {
            if (!InGym || !npc) return false;
            bool again = !_gymTalked.Add(npc.npcId);
            switch (npc.npcId)
            {
                // TOWN.BIN 0x473004-0x4732AB (speaker 0x10).
                case "gym_barry":
                    return StoryAsk(npc,
                        $"Hello, hello.  You’re {HeroName}.\nYou’re a very great person.\nYou made my wish come true.",
                        $"Olá, olá. Você é o {HeroName}.\nVocê é uma pessoa muito especial.\nVocê realizou o meu desejo.",
                        (T("No matter, Barry.", "Não foi nada, Barry."), () =>
                            StoryTalk(npc, "That’s just like you\nto say that.", "É bem típico de você\ndizer isso.")),
                        (T("You’d better be grateful!", "É bom mesmo que você seja grato!"), () =>
                            TownDialogueSequence(npc, null, null,
                                ($"I am very grateful.\nThat’s what I like about you,\n{HeroName}.\nBlush...",
                                    $"Sou muito grato.\nÉ isso que eu gosto em você,\n{HeroName}.\nQue vergonha..."),
                                ("And although small, you’ve got\nthat well developed body.",
                                    "E apesar de pequeno, você tem\num corpo bem definido."),
                                ("Blush... blush...", "Que vergonha... que vergonha..."),
                                ($"[{HeroName}: Shiver, shiver, brrrrrrrrr...]",
                                    $"[{HeroName}: Arrepios, arrepios, brrrrrrrrr...]"))));

                // TOWN.BIN 0x4732AE-0x473891 (speaker 0x2D).
                case "gym_woman":
                    if (again)
                        return StoryTalk(npc, string.Join("\n", System.Linq.Enumerable.Repeat("Gotta lose weight.", 5)),
                            string.Join("\n", System.Linq.Enumerable.Repeat("Preciso emagrecer.", 5)));
                    return TownDialogueSequence(npc, null, null,
                        ("My husband’s rude.\nOnly three months into our\nmarriage, just because",
                            "Meu marido é grosso.\nCom só três meses de\ncasamento, só porque"),
                        ("I gained 20 kilos above my\nprevious 40 kilos, he started\ngetting angry",
                            "engordei 20 quilos além dos\nmeus 40 quilos de antes, ele\ncomeçou a ficar bravo"),
                        ("and said he wanted a divorce.\nI’ve got to make an effort to\nlose weight.",
                            "e disse que quer o divórcio.\nPreciso me esforçar para\nemagrecer."),
                        (string.Join("\n", System.Linq.Enumerable.Repeat("Gotta lose weight.", 6)),
                            string.Join("\n", System.Linq.Enumerable.Repeat("Preciso emagrecer.", 6))),
                        ("Pant, pant.\nThat should be good for\n10 kilos.", "Uf, uf.\nIsso deve valer uns\n10 quilos."),
                        ("Eeeeeek!\nI’ve only lost 5 grams!", "Aaaaaai!\nSó perdi 5 gramas!"));

                // TOWN.BIN 0x473892-0x473A9B (speaker 0x45).
                case "gym_developer":
                    return TownDialogueSequence(npc, null, null,
                        ("I recently went to the\nhospital in Kichijoji",
                            "Fui há pouco tempo ao\nhospital de Kichijoji"),
                        ("because I was always feeling\ntired and stressed out.",
                            "porque vivia cansado\ne estressado."),
                        ("Following numerous x-rays and\nexaminations, I was told that\nI was [out of shape].",
                            "Depois de vários raios X e\nexames, me disseram que eu\nestava [fora de forma]."),
                        ("Do you also work in the game\ndevelopment industry?\nYou’ve got to really\nbe careful.",
                            "Você também trabalha com\ndesenvolvimento de jogos?\nPrecisa tomar muito\ncuidado."));

                // TOWN.BIN 0x473A9E-0x473BDB (speaker 0x3E).
                case "gym_legs":
                    return TownDialogueSequence(npc, null, null,
                        ("You could strengthen your legs\nby walking in the desert or on\nrocky areas,",
                            "Dá para fortalecer as pernas\nandando no deserto ou em\nlugares rochosos,"),
                        ("but it’s also cool to move your\nbody in a safe and clean indoor\nenvironment like here.",
                            "mas também é bom mexer o corpo\nnum lugar fechado, seguro e limpo\ncomo este."));

                // TOWN.BIN 0x473BE0-0x473E2D (speaker 0x34).
                case "gym_hunter":
                    if (again)
                        return TownDialogueSequence(npc, null, null,
                            ("Did you come here for\na workout as well?", "Você também veio\nmalhar?"),
                            ("If you don’t want to be wiped\nout at the tower,\nyou’d better put some muscle\non that skinny body of yours.",
                                "Se não quiser ser derrotado\nna torre, é melhor ganhar\nalgum músculo nesse corpo\nmagricela."));
                    return TownDialogueSequence(npc, null, null,
                        ($"Yo! {HeroName}.\nHave you forgotten?\nWe met at the pub.",
                            $"E aí, {HeroName}!\nEsqueceu de mim?\nA gente se conheceu no bar."),
                        ("Oh, forget it.\nI hear you built this gym.\nNice going.",
                            "Ah, deixa pra lá.\nOuvi dizer que você construiu esta academia.\nMandou bem."),
                        ("It’s just perfect for doing\na workout before work.",
                            "É perfeita para malhar\nantes do trabalho."));

                case "gym_kim_big":
                case "gym_kim_little":
                    return KimBrothersDialogue();
            }
            return false;
        }

        // TOWN.BIN 0x473E30-0x4745A0 (speakers 0x29/0x2A, shared script).
        private bool KimBrothersDialogue()
        {
            var big = FindStoryActor("gym_kim_big");
            var little = FindStoryActor("gym_kim_little");
            if (!big || !little) return false;

            void Offer()
            {
                TownDialogueSequence(big, null, () =>
                    StoryTalk(little, $"Mr. {HeroName}.  Flex.", $"Sr. {HeroName}. Flex.", () =>
                        StoryAsk(big, "Won’t you join us in pumping\nsome iron?", "Não quer puxar ferro\ncom a gente?",
                            (T("Let’s work out together.", "Vamos malhar juntos."), () => Instructions()),
                            (T("No, I don’t think so.", "Não, acho que não."), () =>
                                StoryTalk(little, "Well, let’s train together\nnext time.\nFlex!",
                                    "Bom, vamos treinar juntos\nna próxima vez.\nFlex!")))),
                    ("Flex,\n flex.", "Flex,\n flex."),
                    ("We work here as trainers.", "Trabalhamos aqui como treinadores."));
            }

            void Instructions() => TownDialogueSequence(little, null, BeginGymMinigame,
                ("I’ll throw the barbell.\nWhat you need to do is move to\nwhere the barbell will fall,",
                    "Eu vou jogar a barra.\nO que você tem que fazer é ir\naté onde a barra vai cair,"),
                ("catch it and throw it back to\nme.", "pegá-la e jogá-la de volta\npara mim."),
                ("You can move left or right by\npressing left or right on\nDirectional button.",
                    "Você se move para a esquerda ou\npara a direita com os direcionais."),
                ("(By moving to where the\n barbell falls,\n the barbell will automatically\n be caught and thrown back.)",
                    "(Basta ir até onde a barra\n cai para pegá-la e devolvê-la\n automaticamente.)"),
                ("If you let even one barbell\nfall to the floor,\nthe game will end.",
                    "Se deixar uma barra que seja\ncair no chão,\no jogo acaba."),
                ("Ready? Start.  Flex.", "Pronto? Começar. Flex."));

            void Names()
            {
                TownSaveVariables.SetBool(GymKimNamesKey, true);
                PersistTownProgress();
                StoryTalk(big, "I’m big brother Kim.", "Eu sou o Kim, o irmão mais velho.", () =>
                    StoryTalk(little, "And I’m little brother Kim.", "E eu sou o Kim, o irmão mais novo.", () =>
                        StoryTalk(big, "Together,\nwe’re the Kim Brothers! Flex.", "Juntos,\nsomos os Irmãos Kim! Flex.", Offer)));
            }

            if (!TownSaveVariables.GetBool(GymKimMetKey))
            {
                TownSaveVariables.SetBool(GymKimMetKey, true);
                PersistTownProgress();
                return TownDialogueSequence(big, null, () =>
                    StoryTalk(little, "Flex!  What’s the matter,\nbig bro.", "Flex! O que foi,\nmano?", () =>
                        StoryTalk(big, "I found a cute kid, flex.", "Achei um garoto fofo, flex.", () =>
                            TownDialogueSequence(little, null, Names,
                                ($"The kid you’re talking\nabout...\nHe’s {HeroName}.", $"O garoto de quem\nvocê está falando...\nÉ o {HeroName}."),
                                ("He built this gym.", "Foi ele que construiu esta academia."),
                                ("But you’re right.\nHe is kinda cute, flex.", "Mas você tem razão.\nEle é bem fofinho, flex.")))),
                    ("Flex!  Cute kid.  Flex, flex.", "Flex! Que garoto fofo. Flex, flex."),
                    ("He’s got a small body but well\ntoned muscles.\nFlex!", "Ele é pequeno, mas tem\nmúsculos bem definidos.\nFlex!"));
            }
            if (!TownSaveVariables.GetBool(GymKimNamesKey)) { Names(); return true; }
            Offer();
            return true;
        }

        // ------------------------------------------------------------- minigame

        private void ResetGym()
        {
            foreach (var barbell in _gymBarbells)
                if (barbell.View) RemoveGenerated(barbell.View);
            _gymBarbells.Clear();
            _gymPhase = GymPhase.Off;
            _gymRestingBarbell = null;
        }

        private void BeginGymMinigame()
        {
            if (!InGym || !_player || _gymPhase != GymPhase.Off) return;
            _gymReturnPosition = _player.transform.position;
            _gymReturnFacing = _townFacing;
            _gymRecord = GymRecord;
            _gymScore = 0;
            _gymLane = 1;
            _gymLaneDelay = 0;
            _gymSpawnClock = 0;
            _gymMissed = _gymFinaleDone = _gymNewRecord = false;
            _gymKohX = _player.transform.position.x;
            _gymKohVelocity = 0f;
            // update_gym_minigame states 1-3: Koh hops onto the middle lane.
            _gymKohHop = 0f;
            _gymKohHopVelocity = 12f / GymUnits;
            _gymTimer = 8;
            _gymTicks = 0;
            _gymAccumulator = 0f;
            _gymPhase = GymPhase.Approach;
            _message = string.Empty;
            if (_gymRestingBarbell) _gymRestingBarbell.SetActive(false);
        }

        private bool HandleGymInput()
        {
            if (_gymPhase == GymPhase.Off) return false;
            if (!InGym || !_player) { ResetGym(); return false; }

            if (_gymPhase == GymPhase.Play && _gymLaneDelay == 0)
            {
                float screenRight = _gameCamera && _gameCamera.transform.right.x < 0f ? -1f : 1f;
                int step = MenuDirectionPressed(3) ? -1 : MenuDirectionPressed(4) ? 1 : 0;
                step = (int)(step * screenRight);
                // update_gym_lane_selection: 0x30 units per tick, three ticks of easing.
                if (step < 0 && _gymLane > 0) { _gymLane--; _gymKohVelocity = -48f / GymUnits; _gymLaneDelay = 3; }
                else if (step > 0 && _gymLane < 2) { _gymLane++; _gymKohVelocity = 48f / GymUnits; _gymLaneDelay = 3; }
            }

            _gymAccumulator += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            while (_gymAccumulator >= GymTick && _gymPhase != GymPhase.Off)
            {
                _gymAccumulator -= GymTick;
                TickGym();
            }
            PresentGymKoh();
            return true;
        }

        private void TickGym()
        {
            _gymTicks++;
            _gymTimer--;
            switch (_gymPhase)
            {
                case GymPhase.Approach:
                    _gymKohX += (GymLaneX(1) - _gymKohX) / Mathf.Max(1, _gymTimer + 1);
                    if (_gymTimer <= 0) { _gymKohX = GymLaneX(1); _gymPhase = GymPhase.Ready; _gymTimer = 24; }
                    break;
                case GymPhase.Ready:
                    if (_gymTimer <= 0) { _gymPhase = GymPhase.Play; _gymSpawnClock = 0; }
                    break;
                case GymPhase.Play:
                {
                    int interval = Mathf.Max(1, 32 - _gymRecord / 10);
                    if (_gymSpawnClock++ % interval == 0) ThrowGymBarbell(Random.Range(0, 3), Random.Range(0x20, 0x40), false);
                    if (_gymMissed)
                    {
                        _gymPhase = GymPhase.Over;
                        _gymLane = 1;
                        _gymLaneDelay = 0;
                        _gymKohVelocity = 0f;
                        _gymTimer = 0x40;
                    }
                    break;
                }
                case GymPhase.Over:
                    _gymKohX += (GymLaneX(1) - _gymKohX) * 0.5f;
                    if (_gymTimer <= 0)
                    {
                        ThrowGymBarbell(1, 0x20, true);
                        _gymPhase = GymPhase.Finale;
                    }
                    break;
                case GymPhase.Finale:
                    if (_gymFinaleDone) { _gymPhase = GymPhase.Outro; _gymTimer = 6 + 60; }
                    break;
                case GymPhase.Outro:
                    if (_gymTimer <= 0) EndGymMinigame();
                    break;
            }

            if (_gymPhase is GymPhase.Play or GymPhase.Over or GymPhase.Finale)
            {
                if (_gymLaneDelay > 0)
                {
                    _gymLaneDelay--;
                    _gymKohX += _gymKohVelocity;
                    _gymKohVelocity -= _gymKohVelocity * 0.5f;
                }
                else if (_gymPhase == GymPhase.Play) _gymKohX = GymLaneX(_gymLane);
            }
            if (_gymKohHop > 0f || _gymKohHopVelocity > 0f)
            {
                _gymKohHop += _gymKohHopVelocity;
                _gymKohHopVelocity -= 1.75f / GymUnits;
                if (_gymKohHop <= 0f) { _gymKohHop = 0f; _gymKohHopVelocity = 0f; }
            }

            for (int i = _gymBarbells.Count - 1; i >= 0; i--)
                if (TickGymBarbell(_gymBarbells[i]))
                {
                    if (_gymBarbells[i].View) RemoveGenerated(_gymBarbells[i].View);
                    _gymBarbells.RemoveAt(i);
                }
        }

        private void ThrowGymBarbell(int lane, int flightTicks, bool final)
        {
            var barbell = new GymBarbell
            {
                View = CreateGymBarbell("Gym barbell (thrown)"),
                X = GymThrowerX,
                Height = 1f,
                Target = lane,
                VelocityX = (GymTargetX(lane) - GymThrowerX) / flightTicks,
                VelocityHeight = 12f / GymUnits,
                Gravity = 24f / flightTicks / GymUnits,
                State = final ? 0x40 : 0,
                Final = final
            };
            _gymBarbells.Add(barbell);
            PlaceGymBarbell(barbell.View, barbell.X, barbell.Height, GymLineZ);
        }

        /// <summary>update_gym_score_object.  Returns true when the barbell is gone.</summary>
        private bool TickGymBarbell(GymBarbell b)
        {
            b.X += b.VelocityX;
            b.Height += b.VelocityHeight;
            b.VelocityHeight -= b.Gravity;
            b.Timer--;
            switch (b.State)
            {
                case 0:
                case 0x40:
                    if (b.VelocityHeight > 0f) break;
                    if (b.Height < 0.25f)
                    {
                        if (!_gymMissed) PlayRetailSound(RetailSfx.GymMiss);
                        _gymMissed = true;
                        return true;
                    }
                    if (b.Height >= 1.5f) break;
                    if (b.State == 0 && _gymMissed) break;
                    if (_gymLaneDelay != 0 || b.Target != _gymLane) break;
                    PlayRetailSound(RetailSfx.GymCatch);
                    if (b.State == 0 && _gymScore < 9999)
                    {
                        _gymScore++;
                        if (_gymScore > _gymRecord) { _gymRecord = _gymScore; _gymNewRecord = true; }
                    }
                    b.Timer = b.State == 0 ? 4 : 8;
                    b.VelocityX = b.VelocityHeight = b.Gravity = 0f;
                    b.Height = 1.15f;
                    b.X = _gymKohX;
                    b.State++;
                    break;
                case 1:
                    if (b.Timer > 0) break;
                    b.Timer = 8;
                    b.VelocityX = (GymThrowerX - b.X) / b.Timer;
                    b.VelocityHeight = (1f - b.Height) / b.Timer;
                    b.State = 2;
                    break;
                case 2:
                    if (b.Timer >= 0) break;
                    PlayRetailSound(RetailSfx.GymThrowBack);
                    return true;
                case 0x41:
                    if (b.Timer > 0) break;
                    // Record cue: the retail game plays a fanfare from the CD
                    // when the score matches the record.
                    if (_gymNewRecord || _gymScore == _gymRecord) PlayRetailSound(RetailSfx.Restore);
                    b.Timer = 7;
                    b.State = 0x42;
                    break;
                case 0x42:
                    b.X += (GymRestingBarbell.x - b.X) * 0.5f;
                    b.Height += (0.08f - b.Height) * 0.5f;
                    if (b.Timer > 0) break;
                    _gymFinaleDone = true;
                    if (_gymRestingBarbell) _gymRestingBarbell.SetActive(true);
                    return true;
            }
            if (b.View)
            {
                float z = b.State == 0x42 ? Mathf.Lerp(GymLineZ, GymRestingBarbell.z, 0.5f) : GymLineZ;
                PlaceGymBarbell(b.View, b.X, b.Height, z);
                if (b.State is 0 or 0x40) b.View.transform.Rotate(Vector3.forward, -12f, Space.World);
            }
            return false;
        }

        private void PresentGymKoh()
        {
            if (!_player) return;
            var point = new Vector3(_gymKohX, 0f, GymLineZ);
            var position = RetailTownActorPosition(point, TownActorFootOffset);
            position.y += _gymKohHop;
            _player.transform.position = position;
            _playerCell = new Vector2Int(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.z));
            _townFacing = Vector2.left;
            if (_kohSprite) _kohSprite.SetFacing(_townFacing);
            if (_playerLocomotion) _playerLocomotion.Present(Vector3.zero, 0f, false);
        }

        private void EndGymMinigame()
        {
            if (_gymScore > 0 || _gymRecord > GymRecord)
            {
                TownSaveVariables.SetInt(GymRecordKey, Mathf.Clamp(_gymRecord, 20, 9999));
                PersistTownProgress();
            }
            foreach (var barbell in _gymBarbells)
                if (barbell.View) RemoveGenerated(barbell.View);
            _gymBarbells.Clear();
            if (_gymRestingBarbell) _gymRestingBarbell.SetActive(true);
            _gymPhase = GymPhase.Off;
            if (!_player) return;
            var back = new Vector3(GymLaneX(1), 0f, GymLineZ + 1.5f);
            _player.transform.position = RetailTownActorPosition(back, TownActorFootOffset);
            _playerCell = new Vector2Int(Mathf.RoundToInt(back.x), Mathf.RoundToInt(back.z));
            _townFacing = _gymReturnFacing.sqrMagnitude > 0f ? _gymReturnFacing : Vector2.up;
            if (_kohSprite) _kohSprite.SetFacing(_townFacing);
            UpdateCameraFollow(snap: true);
            _message = T($"Score {_gymScore}.  Record {_gymRecord}.", $"Pontos: {_gymScore}. Recorde: {_gymRecord}.");
        }

        private bool DrawGym(float width, float height)
        {
            if (_gymPhase == GymPhase.Off || _gymPhase == GymPhase.Approach) return false;
            // Retail panel: (200, 160) 96x28 on a 320x240 screen, RECORD above SCORE.
            float sx = width / 320f, sy = height / 240f;
            var panel = new Rect(200f * sx - 18f, 160f * sy - 10f, 96f * sx + 30f, 28f * sy + 20f);
            DrawDialogueFrame(panel);
            float line = (panel.height - 10f) * 0.5f;
            DrawHudText(new Rect(panel.x + 12f, panel.y + 5f, panel.width - 20f, line),
                T($"RECORD {_gymRecord:0000}", $"RECORDE {_gymRecord:0000}"), _bodyStyle);
            DrawHudText(new Rect(panel.x + 12f, panel.y + 5f + line, panel.width - 20f, line),
                T($"SCORE  {_gymScore:0000}", $"PONTOS {_gymScore:0000}"), _bodyStyle);
            if (_gymPhase is GymPhase.Finale or GymPhase.Outro)
            {
                // Retail box: (120, 114) 80x16.
                var box = new Rect(120f * sx - 16f, 114f * sy - 8f, 80f * sx + 32f, 16f * sy + 16f);
                DrawDialogueFrame(box);
                DrawHudText(new Rect(box.x + 10f, box.y + 4f, box.width - 20f, box.height - 8f),
                    T("GAME OVER", "FIM DE JOGO"), _bodyStyle);
            }
            return false; // the town view keeps drawing underneath
        }
    }
}
