using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// The casino: two rooms joined by a passage.
    ///
    ///  - Interior_2b_casino_1 (entered from the town): the slot machine.
    ///    TOWN.BIN overlay ovl_79ded0 (file = VA - 0x80000000 + 0x77D800):
    ///    create_minigame_display_objects, process_slot_machine_state,
    ///    update_minigame_scroll and draw_scrolling_column.  The reels are a
    ///    texture region (VRAM 0x180,0x80 = captured page 134, rows 128-223)
    ///    redrawn from the symbol tiles above it; strips at 0x800244B8,
    ///    payouts at 0x800244E8.
    ///  - Interior_06_casino_2: the roulette with the diving board.
    ///    initialize_casino_minigame, update_betting_effect,
    ///    update_effect_position_spin_phases, update_minigame_transition.
    ///    Three switches (X 0x460/0x4E0/0x560, Y 0x360) bet on a wheel sector
    ///    paying 6x, 3x or 2x; the wheel (centre 0x360,0x360) has six sectors
    ///    per half turn: 2,1,2,1,2,0.
    ///  - Winnings are paid as bouncing coins (update_bouncing_pickup) worth
    ///    100, 1,000 or 10,000 G that Koh must walk over before they vanish.
    ///  - Residents and dialogue: scene scripts at TOWN.BIN 0x464000 (roulette
    ///    room, speakers 0x31, 0x2C, 0x23, 0x2B) and 0x466000 (slot room,
    ///    speakers 0x33, 0x32, 0x21).
    /// </summary>
    public sealed partial class TowerGameController
    {
        private const string CasinoRouletteRoom = "Interior_06_casino_2";
        private const string CasinoSlotsRoom = CasinoRouletteRoom; // legacy name used by older code paths
        private const float CasinoTick = 1f / 30f;
        private const float CasinoUnits = 64f;
        private const string CasinoGreetedKey = "casino.slot_attendant_greeted";   // F 0xB0D
        private const string CasinoGamblerKey = "casino.gambler_talked";

        private static readonly Vector3 SlotSwitch = new(0x460 / CasinoUnits, 0f, 0x360 / CasinoUnits);
        private static readonly Vector3 SlotLever = new(0x440 / CasinoUnits, 0f, 0x2C0 / CasinoUnits);
        private static readonly Vector3 SlotPassage = new(9.5f, 0f, 15.5f);
        private static readonly Vector3 SlotPassageArrival = new(11.5f, 0f, 15.5f);
        private static readonly Vector3 RoulettePassage = new(23.5f, 0f, 16f);
        private static readonly Vector3 WheelCenter = new(0x360 / CasinoUnits, 0f, 0x360 / CasinoUnits);
        private static readonly Vector3 DivingBoard = new(0x360 / CasinoUnits, 0f, 0x2A0 / CasinoUnits);
        private const float WheelRadius = 192f / CasinoUnits;
        private static readonly int[] SlotStrips =
        {
            8, 0, 1, 5, 6, 8, 4, 5, 2, 8, 4, 6,
            6, 0, 8, 2, 4, 1, 8, 6, 5, 4, 5, 8,
            5, 0, 2, 8, 4, 8, 6, 1, 8, 5, 6, 4
        };
        private static readonly int[] SlotPayouts = { 200, 50, 20, 0, 8, 4, 3, 0, 1 };
        private static readonly int[] CoinValues = { 100, 1000, 10000 };
        private static readonly Color[] CoinColors =
            { new(0.78f, 0.47f, 0.22f), new(0.82f, 0.84f, 0.9f), new(1f, 0.82f, 0.22f) };
        // Sector types around the mesh (local angle 0 = +Z), laid out so the sector
        // under Koh's landing point matches the retail rule on angle & 0x7FF.
        private static readonly int[] WheelSectorTypes = { 0, 2, 1, 2, 1, 2, 0, 2, 1, 2, 1, 2 };
        private static readonly Color[] WheelSectorColors =
            { new(0.92f, 0.22f, 0.18f), new(0.22f, 0.42f, 0.9f), new(0.2f, 0.7f, 0.35f) };

        private enum SlotMode { Idle, Bet, Starting, Spinning, Paying }
        private enum RouletteMode { Idle, Bet, ToBoard, Ready, Jumping, Riding, Paying }

        private sealed class CasinoCoin
        {
            public GameObject View;
            public Vector3 Position, Velocity;
            public int Denomination, State, Timer;
        }

        private sealed class SlotReel
        {
            public int Offset, Scroll, Step, State; // State: 0 stopped, 2 accelerating, 3 spinning, 4 stopping
        }

        private float _casinoAccumulator;
        private int _casinoHoldTimer;
        private int _casinoPrizeTotal;
        private readonly List<CasinoCoin> _casinoCoins = new();
        private MaterialPropertyBlock _casinoTint;
        private Vector3? _casinoArrival;

        // Slot machine
        private SlotMode _slotMode;
        private int _slotCoins, _slotTimer, _slotReel, _slotLock, _slotWinnings, _slotPayTimer;
        private readonly int[] _slotPayDigits = new int[3];
        private readonly SlotReel[] _slotReels = { new(), new(), new() };
        private Texture2D _slotTexture;
        private Color32[] _slotSource, _slotPixels;
        private GameObject _slotLever;
        private bool _slotJackpot;

        // Roulette
        private RouletteMode _rouletteMode;
        private int _roulettePad = -1, _rouletteBet, _rouletteTimer, _rouletteCount, _rouletteSector, _roulettePayTimer;
        private int _wheelAngle, _wheelSpeed, _wheelPhase, _wheelStopTicks;
        private Vector3 _rouletteKoh, _rouletteKohVelocity;
        private float _rouletteRideAngle;
        private GameObject _wheel;
        private readonly GameObject[] _roulettePads = new GameObject[3];
        private readonly List<GameObject>[] _betStacks = { new(), new(), new() };

        private bool AtCasinoSlots => _interior && _interior.interiorId == CasinoRoom;
        private bool AtCasinoRoulette => _interior && _interior.interiorId == CasinoRouletteRoom;
        private Vector2 CasinoPlayerPoint => new(_player.transform.position.x, _player.transform.position.z);
        private static Vector3 RoulettePadPosition(int pad) => new((0x460 + pad * 0x80) / CasinoUnits, 0f, 0x360 / CasinoUnits);

        // ================================================================= set-up

        private void SetupCasino()
        {
            _slotMode = SlotMode.Idle;
            _rouletteMode = RouletteMode.Idle;
            _roulettePad = -1;
            _casinoCoins.Clear();
            _slotTexture = null;
            _wheel = null;
            foreach (var stack in _betStacks) stack.Clear();
            if (!AtCasinoRoulette && !AtCasinoSlots) return;
            _casinoPrizeTotal = 0;
            if (AtCasinoSlots) SetupSlotRoom();
            else SetupRouletteRoom();
            SetupCasinoResidents();
        }

        private GameObject CasinoPart(PrimitiveType type, string label, Vector3 point, Vector3 size, Color color)
        {
            var part = CreatePrimitive(type, label, point, size, null, false);
            _casinoTint ??= new MaterialPropertyBlock();
            _casinoTint.Clear();
            _casinoTint.SetColor("_BaseColor", color);
            _casinoTint.SetColor("_Color", color);
            part.GetComponent<Renderer>().SetPropertyBlock(_casinoTint);
            return part;
        }

        private Vector3 CasinoFloor(Vector3 point, float lift)
        {
            point.y = TownTerrainHeight(point) + lift;
            return point;
        }

        private void SetupSlotRoom()
        {
            // The machine and its reel window are part of the captured room;
            // take over the reel texture so it can be redrawn like VRAM.
            if (_interior.TryGetComponent<MeshRenderer>(out var renderer))
                foreach (var material in renderer.sharedMaterials)
                {
                    if (!material || material.mainTexture is not Texture2D source ||
                        !source.name.StartsWith("page_134", System.StringComparison.Ordinal)) continue;
                    try
                    {
                        _slotSource = System.Array.ConvertAll(source.GetPixels(), c => (Color32)c);
                        _slotPixels = (Color32[])_slotSource.Clone();
                        _slotTexture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
                            { filterMode = source.filterMode, wrapMode = TextureWrapMode.Clamp, name = "Slot reels (runtime)" };
                        _slotTexture.SetPixels32(_slotPixels);
                        _slotTexture.Apply(false);
                        material.mainTexture = _slotTexture;
                    }
                    catch (UnityException) { _slotTexture = null; }
                }
            for (int i = 0; i < _slotReels.Length; i++)
            {
                _slotReels[i].Offset = 0; _slotReels[i].Scroll = 0; _slotReels[i].Step = 0; _slotReels[i].State = 0;
                DrawSlotReel(i);
            }
            ApplySlotTexture();

            // Lever (mesh 0x2C at 0x440, 0x2C0) and the switch in front of it.
            _slotLever = CreateNativeMinigameObject("SlotLever", "Slot lever — retail mesh 0x2C");
            _slotLever.transform.position = CasinoFloor(SlotLever, 0f);
            CasinoPart(PrimitiveType.Cylinder, "Slot switch", CasinoFloor(SlotSwitch, 0.03f),
                new Vector3(0.62f, 0.03f, 0.62f), new Color(1f, 0.8f, 0.2f));
            CreatePrimitive(PrimitiveType.Cylinder, "Casino passage", CasinoFloor(SlotPassage, 0.03f),
                new Vector3(0.8f, 0.03f, 0.8f), _accentMaterial, false);
        }

        private void SetupRouletteRoom()
        {
            BuildCasinoRampCollider();
            BuildRouletteWheel();
            var labels = new[] { "x6", "x3", "x2" };
            for (int pad = 0; pad < 3; pad++)
            {
                var point = RoulettePadPosition(pad);
                _roulettePads[pad] = CreateNativeMinigameObject("Roulette" + new[] { "6", "3", "2" }[pad], "Roulette switch " + labels[pad]);
                _roulettePads[pad].transform.position = CasinoFloor(point, 0f);
            }
            // The original diving-board geometry is part of the captured room;
            // do not cover it with an additional grey cuboid.
            UpdateBetStacks();
        }

        private MeshCollider[] _rouletteColliders = System.Array.Empty<MeshCollider>();

        private void BuildRouletteWheel()
        {
            _wheel = CreateNativeMinigameObject("RouletteWheel", "Roulette wheel — retail mesh 0x2D");
            _wheel.transform.position = CasinoFloor(WheelCenter, 0f);
            foreach(var filter in _wheel.GetComponentsInChildren<MeshFilter>())
            {
                if (!filter.sharedMesh) continue;
                // Retail rendering uses inward-facing triangles. Physics rays
                // from Koh's feet need the top faces to point upward instead.
                var collisionMesh = Instantiate(filter.sharedMesh);
                collisionMesh.name = "Roulette collision";
                var triangles = collisionMesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                    (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
                collisionMesh.triangles = triangles;
                collisionMesh.RecalculateNormals();
                _constructionGeometry.Add(collisionMesh);
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = collisionMesh;
            }
            _rouletteColliders = _wheel.GetComponentsInChildren<MeshCollider>();
            _wheelAngle = 0; _wheelSpeed = 0; _wheelPhase = 0;
        }

        private float RouletteObstacleTop(Vector3 position, float radius)
        {
            if (!AtCasinoRoulette || !_wheel || !_wheel.activeInHierarchy) return float.NegativeInfinity;
            var delta = position - _wheel.transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > (WheelRadius + radius) * (WheelRadius + radius))
                return float.NegativeInfinity;
            float top = float.NegativeInfinity;
            foreach (var collider in _rouletteColliders)
            {
                if (!collider || !collider.enabled) continue;
                var bounds = collider.bounds;
                // Sample the feet and their perimeter, allowing landing on the native
                // wheel mesh while preserving collision against its raised sides.
                for (int sample = 0; sample < 5; sample++)
                {
                    var point = position;
                    if (sample == 1) point.x += radius;
                    if (sample == 2) point.x -= radius;
                    if (sample == 3) point.z += radius;
                    if (sample == 4) point.z -= radius;
                    point.y = bounds.max.y + .5f;
                    if (collider.Raycast(new Ray(point, Vector3.down), out var hit, bounds.size.y + 1f))
                        top = Mathf.Max(top, hit.point.y);
                }
            }
            return top;
        }

        // ============================================================= residents

        private void SetupCasinoResidents()
        {
            foreach (var person in _townPeople)
            {
                if (!person || person.npcId == "vivian") continue;
                (string id, string en, string pt) role = (AtCasinoSlots, person.npcId) switch
                {
                    (true, "fur") => ("slot_attendant", "Attendant", "Atendente"),
                    (false, "fur") => ("roulette_attendant", "Attendant", "Atendente"),
                    (false, "monster_shop_hunter") => ("casino_gambler", "Gambler", "Apostador"),
                    (false, "dar") => ("casino_loser", "Man", "Homem"),
                    _ => (null, null, null)
                };
                if (role.id == null)
                {
                    // The slot room's captured extra is replaced by its retail residents below.
                    if (AtCasinoSlots && person.npcId == "monster_shop_hunter") person.gameObject.SetActive(false);
                    continue;
                }
                person.npcId = role.id;
                if(role.id is "slot_attendant" or "roulette_attendant")
                {
                    var position=person.transform.position+Vector3.forward*.75f;
                    person.transform.position=RetailTownActorPosition(position,0f);
                }
                person.dialogueLines = System.Array.Empty<string>();
                if (person.TryGetComponent<CharacterStats>(out var stats))
                {
                    stats.displayName = T(role.en, role.pt);
                    stats.portraitTexture = null;
                }
            }
            if (AtCasinoSlots)
            {
                SpawnRetailResident("slot_lucky_woman", T("Woman", "Mulher"), "Title/AuntRain");
                SpawnRetailResident("slot_old_man", T("Old man", "Senhor"), "Title/GrandpaRain");
            }
            else SpawnRetailResident("casino_broke", T("Man", "Homem"), "Title/GrandpaRain");
            if (AtCasinoSlots)
            {
                ApplyNativeMinigameResident("slot_attendant", "SlotAttendant");
                ApplyNativeMinigameResident("slot_lucky_woman", "CasinoWoman");
                ApplyNativeMinigameResident("slot_old_man", "CasinoOldMan");
            }
            else
            {
                // The two extracted atlases carry swapped names: the speaker table
                // (TOWN.BIN 0x451C8C) gives the attendant speaker 0x2B (person slot 43,
                // F_act_setumeiin, a young man) and the loser 0x2C (slot 44, F_act_der =
                // Dar, the old man), and the pixels of "CasinoLoser" match slot 43 while
                // "RouletteAttendant" matches slot 44.
                ApplyNativeMinigameResident("roulette_attendant", "CasinoLoser");
                ApplyNativeMinigameResident("casino_gambler", "CasinoGambler");
                ApplyNativeMinigameResident("casino_loser", "RouletteAttendant");
                ApplyNativeMinigameResident("casino_broke", "CasinoBroke");
            }
        }

        private bool CasinoResidentDialogue(NPCController npc)
        {
            if (!npc || (!AtCasinoSlots && !AtCasinoRoulette)) return false;
            switch (npc.npcId)
            {
                // TOWN.BIN 0x466004 (speaker 0x33).
                case "slot_attendant":
                {
                    void Explain(string first, string firstPt) => TownDialogueSequence(npc, null, null,
                        (first, firstPt),
                        ("Hit the switch in front of the\nlever by pressing × button.", "Aperte o botão na frente da\nalavanca com o botão ×."),
                        ("And adjust the amount you wish\nto bet by pressing up or down\non Directional button.",
                            "E ajuste quanto quer apostar\napertando para cima ou para baixo\nnos direcionais."),
                        ("When you’ve decided how much\nyou wish to wager,\npress × button.", "Quando decidir quanto quer\napostar, aperte o botão ×."),
                        ("While rotating,\nthe slot machine can be stopped\nby pressing × button.", "Enquanto gira, o caça-níqueis\npode ser parado com o botão ×."),
                        ("If you match three of the same\nimage, money will be paid out.", "Se alinhar três figuras iguais,\no dinheiro sai da máquina."),
                        ("Make sure you remember to\ncollect this money.", "Não se esqueça de\npegar esse dinheiro."));
                    void Offer() => StoryAsk(npc, "Won’t you play a little now\nyou’re here?", "Que tal jogar um pouco,\njá que está aqui?",
                        (T("I’ll play.", "Vou jogar."), () => Explain("Thank you.", "Obrigada.")),
                        (T("Do I have to pay?", "Preciso pagar?"), () =>
                            Explain("Of course.  It wouldn’t be any\nfun otherwise, would it?", "Claro. Senão não teria graça,\nnão é mesmo?")),
                        (T("Not today.", "Hoje não."), () => StoryTalk(npc, "Please come to play next time.", "Volte para jogar da próxima vez.")));
                    if (TownSaveVariables.GetBool(CasinoGreetedKey))
                        return StoryTalk(npc, "Welcome.\nOh, Mr. Owner.", "Bem-vindo.\nAh, senhor proprietário.", Offer);
                    TownSaveVariables.SetBool(CasinoGreetedKey, true);
                    PersistTownProgress();
                    return TownDialogueSequence(npc, null, Offer,
                        ("Welcome.\nOh, Mr. Owner.", "Bem-vindo.\nAh, senhor proprietário."),
                        ("Yes, this Casino is extremely\npopular amongst our customers.", "Sim, este cassino faz muito\nsucesso com os clientes."));
                }
                // TOWN.BIN 0x466480 (speaker 0x32).
                case "slot_lucky_woman":
                    return TownDialogueSequence(npc, null, null,
                        ("Yes, yes!  Ex-cellent!\nListen, listen!  Look, look!\nI got it!", "Isso, isso! Ex-celente!\nEscute, escute! Olhe, olhe!\nConsegui!"),
                        ("That’s one million Gs!", "É um milhão de G!"),
                        ("The goddess of happiness has\nbeen looking over me.\nOr am I the goddess!?", "A deusa da felicidade está\nolhando por mim.\nOu será que a deusa sou eu!?"));
                // TOWN.BIN 0x466560 (speaker 0x21).
                case "slot_old_man":
                    if (npc.dialogueLines is { Length: 1 } && npc.dialogueLines[0] == "seen")
                        return StoryTalk(npc, "I’m so grateful.", "Sou muito grato.");
                    npc.dialogueLines = new[] { "seen" };
                    return TownDialogueSequence(npc, null, null,
                        ("I’m so grateful for this place.\nAt this age,", "Sou muito grato por este lugar.\nNesta idade,"),
                        ("I’m finally able to meet girls\ndressed as bunnies.", "finalmente posso conhecer moças\nvestidas de coelhinha."),
                        ("They’re about as cute as Ette,\na girl I dated long ago.", "Elas são quase tão fofas quanto a Ette,\numa moça que namorei há muito tempo."));
                // TOWN.BIN 0x464A9C (speaker 0x2B).
                case "roulette_attendant":
                    return StoryTalk(npc, "Welcome to the casino.", "Bem-vindo ao cassino.", () =>
                        StoryAsk(npc, "Would you like an explanation\non the game?", "Quer uma explicação\nsobre o jogo?",
                            (T("Yes, I’m listening.", "Sim, estou ouvindo."), () => TownDialogueSequence(npc, null, null,
                                ("Press × button for the\nswitch you want to bet on.", "Aperte o botão × no\nbotão em que quer apostar."),
                                ("Adjust the amount you want to\nbet by pressing up or down on\nDirectional button.",
                                    "Ajuste quanto quer apostar\napertando para cima ou para baixo\nnos direcionais."),
                                ("Press × button\nwhen the betting amount\nhas been determined.", "Aperte o botão ×\nquando decidir o valor\nda aposta."),
                                ("Press × button when moving\non to the diving board.", "Aperte o botão × para pular\ndo trampolim."),
                                ("If you win a bet, the roulette\nwill spit out money. Make sure\nto collect this money.",
                                    "Se ganhar a aposta, a roleta\nvai cuspir dinheiro. Não se\nesqueça de pegar esse dinheiro."),
                                ("Good luck.", "Boa sorte."))),
                            (T("I don’t need an explanation.", "Não preciso de explicação."), () =>
                                StoryTalk(npc, "Good luck.", "Boa sorte."))));
                // TOWN.BIN 0x46403C (speaker 0x31).
                case "casino_gambler":
                    if (TownSaveVariables.GetBool(CasinoGamblerKey))
                        return StoryTalk(npc, "Hey, brother, how’s it going?", "E aí, mano, como vão as coisas?");
                    TownSaveVariables.SetBool(CasinoGamblerKey, true);
                    PersistTownProgress();
                    return StoryTalk(npc, "I’m a gambler.", "Eu sou um apostador.", () =>
                        StoryAsk(npc, "Yo, brother.\nYou wanna bet all your assets\nwith me?", "Ei, mano.\nQuer apostar tudo o que tem\ncomigo?",
                            (T("Sure.", "Claro."), () => TownDialogueSequence(npc, null, null,
                                ($"What!?  You’ve got\n{_gold} G", $"O quê!? Você tem\n{_gold} G"),
                                ("Hmmm.", "Hmmm."),
                                ("I was going to place a bet\nwith you, but I haven’t got\nthat kinda money today.",
                                    "Eu ia apostar com você,\nmas hoje não tenho\ntodo esse dinheiro."),
                                ("I’m gonna do you a favor and\npostpone my bet until the next\ntime we meet.",
                                    "Vou te fazer um favor e deixar\na aposta para a próxima vez\nque a gente se encontrar."),
                                ("Consider yourself lucky,\nbrother.", "Pode se considerar sortudo,\nmano."))),
                            (T("No way.", "De jeito nenhum."), () => StoryTalk(npc, "What a coward.", "Que covarde."))));
                // TOWN.BIN 0x4643B4 (speaker 0x2C).
                case "casino_loser":
                    return TownDialogueSequence(npc, null, null,
                        ("Ouch...\nI’ve lost 200 Gs today.", "Ai...\nPerdi 200 G hoje."),
                        ("But I’m still okay since I won\n200 Gs the other day.", "Mas tudo bem, porque outro dia\nganhei 200 G."),
                        ("Wait a minute,\nI vaguely remember losing\n500 Gs before that.", "Peraí,\nacho que antes disso\nperdi 500 G."),
                        ("Ouch... ouch...\nOh well, easy come, easy go.", "Ai... ai...\nBom, o que vem fácil vai fácil."));
                // TOWN.BIN 0x464A30 (speaker 0x23).
                case "casino_broke":
                    return TownDialogueSequence(npc, null, null,
                        ("Oh no, I’ve used up everything\nI’ve got.", "Ah, não, gastei tudo\no que eu tinha."),
                        ("Gosh... What am I going\nto tell my wife!", "Puxa... O que vou dizer\npara a minha mulher!"));
            }
            return false;
        }

        // ============================================================ passages

        private bool TryCasinoPassage()
        {
            if (!_player || _slotMode != SlotMode.Idle || _rouletteMode != RouletteMode.Idle) return false;
            Vector3 p = _player.transform.position;
            if (AtCasinoSlots && Flat(p - SlotPassage) < 0.6f)
            {
                StartCoroutine(LoadInterior(CasinoRouletteRoom));
                return true;
            }
            if (AtCasinoRoulette && Flat(p - RoulettePassage) < 0.9f)
            {
                _casinoArrival = SlotPassageArrival;
                StartCoroutine(LoadInterior(CasinoRoom));
                return true;
            }
            return false;
        }

        private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        private Vector3? TakeCasinoArrival()
        {
            var arrival = _casinoArrival;
            _casinoArrival = null;
            return arrival;
        }

        // ============================================================ interaction

        private bool TryCasinoInteraction()
        {
            if (!_player || (!AtCasinoRoulette && !AtCasinoSlots)) return false;
            if (AtCasinoSlots && _slotMode == SlotMode.Idle && Flat(_player.transform.position - SlotSwitch) < 0.9f)
            {
                // process_slot_machine_state mode 1: hitting the switch costs the first 100 G.
                if (_gold < 100) { _message = T("You need 100 G to play.", "Você precisa de 100 G para jogar."); return true; }
                _gold -= 100;
                _slotCoins = 1;
                _slotMode = SlotMode.Bet;
                _casinoHoldTimer = 5;
                PlayRetailSound(RetailSfx.CasinoOpen);
                _message = string.Empty;
                return true;
            }
            if (AtCasinoRoulette && _rouletteMode == RouletteMode.Idle)
                for (int pad = 0; pad < 3; pad++)
                    if (Flat(_player.transform.position - RoulettePadPosition(pad)) < 0.75f)
                    {
                        if (_gold < 100) { _message = T("You need 100 G to play.", "Você precisa de 100 G para jogar."); return true; }
                        _roulettePad = pad;
                        _rouletteBet = 0;
                        _rouletteMode = RouletteMode.Bet;
                        _casinoHoldTimer = 5;
                        PlayRetailSound(RetailSfx.CasinoOpen);
                        PresentRoulettePads();
                        _message = string.Empty;
                        return true;
                    }
            return false;
        }

        /// <summary>Up/Down with the retail auto-repeat (first step, then every tick after 5 held ticks).</summary>
        private int CasinoBetStep()
        {
            float y = _move?.ReadValue<Vector2>().y ?? 0f;
            bool upPressed = MenuDirectionPressed(1), downPressed = MenuDirectionPressed(2);
            bool held = Mathf.Abs(y) > 0.5f;
            if (!held) _casinoHoldTimer = 5;
            else if (_casinoHoldTimer > 0) _casinoHoldTimer--;
            if (upPressed || (y > 0.5f && _casinoHoldTimer <= 0)) return 1;
            if (downPressed || (y < -0.5f && _casinoHoldTimer <= 0)) return -1;
            return 0;
        }

        private bool HandleCasinoInput()
        {
            if (!AtCasinoSlots && !AtCasinoRoulette)
            {
                _slotMode = SlotMode.Idle;
                _rouletteMode = RouletteMode.Idle;
                return false;
            }

            bool lockKoh = (_slotMode is not SlotMode.Idle and not SlotMode.Paying) ||
                           (_rouletteMode is not RouletteMode.Idle and not RouletteMode.Paying);

            if (_slotMode == SlotMode.Bet)
            {
                int step = CasinoBetStep();
                if (step > 0 && _slotCoins < 3 && _gold >= 100) { _gold -= 100; _slotCoins++; PlayRetailSound(RetailSfx.Cursor); }
                else if (step < 0 && _slotCoins >= 2) { _gold += 100; _slotCoins--; PlayRetailSound(RetailSfx.Cursor); }
                if (_cancel?.WasPressedThisFrame() == true)
                {
                    _gold += _slotCoins * 100;
                    _slotCoins = 0;
                    _slotMode = SlotMode.Idle;
                    PlayRetailSound(RetailSfx.Cancel);
                    PersistTownProgress();
                }
                else if (_interact?.WasPressedThisFrame() == true)
                {
                    PlayRetailSound(RetailSfx.SlotStart);
                    _slotTimer = 20;
                    _slotMode = SlotMode.Starting;
                    PersistTownProgress();
                }
            }
            else if (_slotMode == SlotMode.Spinning && _slotLock == 0 && _slotReel < 3 &&
                     _slotReels[_slotReel].State == 3 && _interact?.WasPressedThisFrame() == true)
            {
                PlayRetailSound(RetailSfx.SlotReelStop);
                _slotReels[_slotReel].State = 4;
                _slotLock = 10;
                _slotReel++;
            }

            if (_rouletteMode == RouletteMode.Bet)
            {
                int step = CasinoBetStep();
                if (step > 0 && _rouletteBet < 10000 && _gold >= 100) { _gold -= 100; _rouletteBet += 100; PlayRetailSound(RetailSfx.Cursor); UpdateBetStacks(); }
                else if (step < 0 && _rouletteBet >= 100) { _gold += 100; _rouletteBet -= 100; PlayRetailSound(RetailSfx.Cursor); UpdateBetStacks(); }
                if (_cancel?.WasPressedThisFrame() == true) EndRoulette(refund: true);
                else if (_interact?.WasPressedThisFrame() == true && _rouletteBet > 0) BeginRouletteRun();
            }
            else if (_rouletteMode == RouletteMode.Ready && _wheelPhase == 2 && _interact?.WasPressedThisFrame() == true)
                RouletteJump();

            _casinoAccumulator += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            while (_casinoAccumulator >= CasinoTick)
            {
                _casinoAccumulator -= CasinoTick;
                if (AtCasinoSlots) TickSlotMachine();
                else TickRoulette();
                TickCasinoCoins();
            }
            if (AtCasinoSlots) ApplySlotTexture();
            if (_wheel) _wheel.transform.rotation = Quaternion.Euler(0f, _wheelAngle * 360f / 4096f, 0f);
            if (_rouletteMode is RouletteMode.ToBoard or RouletteMode.Ready or RouletteMode.Jumping or RouletteMode.Riding)
                PresentRouletteKoh();
            return lockKoh;
        }

        // ========================================================= slot machine

        private void TickSlotMachine()
        {
            for (int i = 0; i < _slotReels.Length; i++) TickSlotReel(i);
            if (_slotLock > 0) _slotLock--;
            switch (_slotMode)
            {
                case SlotMode.Starting:
                    if (_slotLever) _slotLever.transform.rotation = Quaternion.Euler(Mathf.Lerp(0f, 40f, Mathf.Sin((20 - _slotTimer) / 20f * Mathf.PI)), 0f, 0f);
                    if (--_slotTimer > 0) break;
                    foreach (var reel in _slotReels) reel.State = 2;
                    _slotReel = 0;
                    _slotLock = 0;
                    _slotMode = SlotMode.Spinning;
                    break;
                case SlotMode.Spinning:
                    if (_slotReel < 3 || _slotReels[2].State != 0)
                    {
                        if (_slotReel < 3 && _casinoTickCount % 6 == 0) PlayRetailSound(RetailSfx.SlotSpin, 0.35f);
                        break;
                    }
                    ResolveSlotMachine();
                    break;
                case SlotMode.Paying:
                    if (++_slotPayTimer % 4 != 0) break;
                    if (_slotPayDigits[0] + _slotPayDigits[1] + _slotPayDigits[2] == 0) { _slotMode = SlotMode.Idle; break; }
                    int denomination = _slotPayDigits[2] > 0 ? 2 : _slotPayDigits[1] > 0 ? 1 : 0;
                    _slotPayDigits[denomination]--;
                    // Coins leave the machine at the switch offset (-0x140, -0x40, one square up).
                    var origin = SlotSwitch + new Vector3(-0x140 / CasinoUnits, 0f, -0x40 / CasinoUnits);
                    SpawnCasinoCoin(denomination, CasinoFloor(origin, 1f),
                        new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(0.4f, 1.2f)) * (3f / CasinoUnits) + Vector3.up * (4f / CasinoUnits),
                        Random.Range(18, 30));
                    break;
            }
        }

        private void TickSlotReel(int index)
        {
            var reel = _slotReels[index];
            switch (reel.State)
            {
                case 2:
                case 3:
                    if (reel.State == 2 && (_casinoTickCount & 1) != 0)
                    {
                        if (reel.Step < 12) reel.Step++;
                        else reel.State = 3;
                    }
                    reel.Scroll -= reel.Step;
                    while (reel.Scroll < 0) { reel.Scroll += 32; reel.Offset = (reel.Offset + 1) % 12; }
                    DrawSlotReel(index);
                    break;
                case 4:
                    reel.Scroll -= reel.Scroll >> 2;
                    if (reel.Scroll < 4) { reel.Scroll = 0; reel.State = 0; reel.Step = 0; }
                    DrawSlotReel(index);
                    break;
            }
            if (index == 2) _casinoTickCount++;
        }

        private int _casinoTickCount;

        private int SlotSymbol(int reel, int row) => SlotStrips[reel * 12 + (row + _slotReels[reel].Offset) % 12];

        private void ResolveSlotMachine()
        {
            int win = 0;
            _slotJackpot = false;
            int Line(int a, int b, int c, int ra, int rb, int rc)
            {
                int s = SlotSymbol(a, ra);
                if (SlotSymbol(b, rb) != s || SlotSymbol(c, rc) != s) return 0;
                if (s == 0) _slotJackpot = true;
                return SlotPayouts[s] * 100 * _slotCoins;
            }
            for (int line = 0; line < _slotCoins; line++)
                win += line switch
                {
                    0 => Line(0, 1, 2, 1, 1, 1),
                    1 => Line(0, 1, 2, 0, 0, 0) + Line(0, 1, 2, 2, 2, 2),
                    _ => Line(0, 1, 2, 0, 1, 2) + Line(0, 1, 2, 2, 1, 0)
                };
            _slotCoins = 0;
            if (win == 0) { _slotMode = SlotMode.Idle; return; }
            _slotWinnings = win;
            SplitCoins(win / 100, _slotPayDigits);
            _slotPayTimer = 0;
            _slotMode = SlotMode.Paying;
            if (_slotJackpot) PlayRetailSound(RetailSfx.Restore);
        }

        /// <summary>Units/tens/hundreds of the 100 G count, borrowing so at least ten coins drop.</summary>
        private static void SplitCoins(int units, int[] digits)
        {
            digits[0] = units % 10;
            digits[1] = units / 10 % 10;
            digits[2] = units / 100 % 10;
            if (digits[0] + digits[1] + digits[2] >= 10) return;
            if (digits[2] > 0) { digits[2]--; digits[1] += 10; }
            else if (digits[1] > 0) { digits[1]--; digits[0] += 10; }
        }

        private void DrawSlotReel(int column)
        {
            if (_slotPixels == null || _slotTexture == null) return;
            var reel = _slotReels[column];
            int width = _slotTexture.width, height = _slotTexture.height;
            for (int row = -1; row < 3; row++)
            {
                if (row < 0 && reel.Scroll == 0) continue;
                int tile = SlotStrips[column * 12 + (reel.Offset + 12 + row) % 12];
                int rowY = ((2 - row) << 5) - reel.Scroll;
                int srcX = (tile & 3) * 32, srcY = (tile >> 2) * 32, h = 32, destY = rowY + 128;
                if (rowY < 0) { srcY -= rowY; h = rowY + 32; destY = 128; }
                if (destY + h > 224) h = 224 - destY;
                for (int y = 0; y < h; y++)
                {
                    int sy = height - 1 - (srcY + y), dy = height - 1 - (destY + y);
                    if (sy < 0 || dy < 0) continue;
                    System.Array.Copy(_slotSource, sy * width + srcX, _slotPixels, dy * width + column * 32, 32);
                }
            }
            _slotDirty = true;
        }

        private bool _slotDirty;

        private void ApplySlotTexture()
        {
            if (!_slotDirty || !_slotTexture) return;
            _slotDirty = false;
            _slotTexture.SetPixels32(_slotPixels);
            _slotTexture.Apply(false);
        }

        // ============================================================== roulette

        private void PresentRoulettePads()
        {
            for (int pad = 0; pad < 3; pad++)
            {
                if (!_roulettePads[pad]) continue;
                bool chosen = _roulettePad == pad;
                bool sunk = _roulettePad >= 0 && !chosen;
                var p = CasinoFloor(RoulettePadPosition(pad), sunk ? -0.06f : 0.08f);
                _roulettePads[pad].transform.position = p;
            }
        }

        private void UpdateBetStacks()
        {
            if (!AtCasinoRoulette) return;
            int units = _rouletteBet / 100;
            for (int column = 0; column < 3; column++)
            {
                int denominationUnits = column == 0 ? 1 : column == 1 ? 10 : 100;
                int visible = units / denominationUnits % 10;
                var stack = _betStacks[column];
                while (stack.Count < 9)
                {
                    int row = stack.Count;
                    // initialize_source_and_initial_data kind 0: X 0x520 - column*0x40, Y 0x3E0, Z -row*8.
                    var point = new Vector3((0x520 - column * 0x40) / CasinoUnits, 0f, 0x3E0 / CasinoUnits);
                    stack.Add(CreateNativeCasinoCoin(column, CasinoFloor(point, 0.05f + row * 0.125f)));
                }
                for (int row = 0; row < stack.Count; row++)
                    if (stack[row]) stack[row].SetActive(row < visible);
            }
        }

        private void BeginRouletteRun()
        {
            // update_betting_effect state 3/11/12: Koh moves to the diving
            // board while the wheel spins up.
            _rouletteKoh = _player.transform.position;
            _rouletteKoh.y = 0f;
            _rouletteTimer = 10;
            _rouletteKohVelocity = (DivingBoard - _rouletteKoh) / _rouletteTimer;
            _rouletteKohVelocity.y = 0f;
            _rouletteMode = RouletteMode.ToBoard;
            PlayTheaterSound("Casino700", 0.6f);
            _wheelPhase = 1;
            _wheelSpeed = 0;
            PersistTownProgress();
        }

        private int WheelSpeedLimit()
        {
            int kind = Mathf.Clamp(_roulettePad, 0, 2);
            return _rouletteBet / ((kind << 8) + 0x300) + 0x40 + ((3 - kind) << 5);
        }

        private void RouletteJump()
        {
            PlayRetailSound(RetailSfx.CasinoJump);
            _rouletteKohVelocity = new Vector3(0f, 12f / CasinoUnits, 0x48000 / 65536f / CasinoUnits);
            _rouletteCount = 0;
            _rouletteMode = RouletteMode.Jumping;
        }

        private void TickRoulette()
        {
            TickWheel();
            switch (_rouletteMode)
            {
                case RouletteMode.ToBoard:
                    _rouletteKoh += _rouletteKohVelocity;
                    if (--_rouletteTimer > 0) break;
                    _rouletteKoh = new Vector3(DivingBoard.x, 0.5f, DivingBoard.z);
                    _rouletteMode = RouletteMode.Ready;
                    break;
                case RouletteMode.Jumping:
                {
                    // Four bounces towards the wheel (SFX 0x508 at ticks 20, 36, 48 and 61).
                    _rouletteKoh.z += _rouletteKohVelocity.z;
                    _rouletteKoh.y += _rouletteKohVelocity.y;
                    _rouletteKohVelocity.y -= 2f / CasinoUnits;
                    if (_rouletteKoh.y < 0.06f && _rouletteKohVelocity.y < 0f)
                    {
                        _rouletteKoh.y = 0.06f;
                        PlayRetailSound(RetailSfx.CasinoBounce);
                        _rouletteCount++;
                        _rouletteKohVelocity.y = (_rouletteCount switch { 1 => 8f, 2 => 4f, _ => 3.5f }) / CasinoUnits;
                    }
                    float distance = Flat(_rouletteKoh - WheelCenter);
                    if (_rouletteKoh.z > WheelCenter.z && distance >= 161f / CasinoUnits && _rouletteCount >= 2)
                    {
                        // update_effect_position_spin_phases phase 3: the sector under Koh decides the bet.
                        int a = _wheelAngle & 0x7FF;
                        _rouletteSector = a >= 0x6AB ? 0 : (a >= 0x554 ? 2 : a >= 0x3FF ? 1 : a >= 0x2AA ? 2 : a >= 0x155 ? 1 : 2);
                        _wheelPhase = 3;
                        _rouletteRideAngle = Mathf.Atan2(_rouletteKoh.x - WheelCenter.x, _rouletteKoh.z - WheelCenter.z);
                        _rouletteKoh.y = 0.06f;
                        _rouletteMode = RouletteMode.Riding;
                    }
                    break;
                }
                case RouletteMode.Riding:
                    _rouletteRideAngle += _wheelSpeed / 4096f * Mathf.PI * 2f;
                    _rouletteKoh = WheelCenter + new Vector3(Mathf.Sin(_rouletteRideAngle), 0f, Mathf.Cos(_rouletteRideAngle)) * (160f / CasinoUnits);
                    _rouletteKoh.y = 0.06f;
                    if (_wheelPhase != 0) break;
                    if (_rouletteSector == _roulettePad)
                    {
                        // update_betting_effect state 8: amount * (6/(pad+1)) / 100 coins burst from the centre.
                        int units = _rouletteBet * (6 / (_roulettePad + 1)) / 100;
                        var digits = new int[3];
                        SplitCoins(units, digits);
                        int total = digits[0] + digits[1] + digits[2], index = 0;
                        PlayRetailSound(RetailSfx.Restore);
                        for (int d = 2; d >= 0; d--)
                            for (int n = 0; n < digits[d]; n++, index++)
                            {
                                float angle = index * Mathf.PI * 2f / Mathf.Max(1, total);
                                // rsin*5<<6 = 20 units per tick outwards, 2 squares up, -8 units vertical.
                                SpawnCasinoCoin(d, CasinoFloor(WheelCenter, 2f),
                                    new Vector3(Mathf.Sin(angle) * 20f, 8f, Mathf.Cos(angle) * 20f) / CasinoUnits / 2.5f, 8);
                            }
                        _rouletteMode = RouletteMode.Paying;
                        _roulettePayTimer = 0xF0;
                    }
                    else EndRoulette(refund: false);
                    _rouletteBet = 0;
                    UpdateBetStacks();
                    break;
                case RouletteMode.Paying:
                    if (--_roulettePayTimer <= 0) EndRoulette(refund: false);
                    break;
            }
        }

        private void TickWheel()
        {
            int previous = _wheelAngle;
            int limit = WheelSpeedLimit();
            switch (_wheelPhase)
            {
                case 1:
                    _wheelSpeed += (_wheelSpeed + 16) >> 4;
                    if (_wheelSpeed > limit) { _wheelSpeed = limit; _wheelPhase = 2; }
                    break;
                case 2:
                    _wheelSpeed = limit;
                    break;
                case 3:
                case 4:
                {
                    _wheelPhase = 4;
                    int shift = _roulettePad == 2 ? 6 : 5;
                    _wheelSpeed -= _wheelSpeed >> shift;
                    if (_wheelSpeed < (1 << shift)) { _wheelStopTicks = 0; _wheelPhase = 5; }
                    break;
                }
                case 5:
                    if ((++_wheelStopTicks & 3) == 0) _wheelSpeed -= 2;
                    if (_wheelSpeed < 4) { _wheelSpeed = 0; _wheelPhase = 0; }
                    break;
                default:
                    _wheelSpeed = 0;
                    break;
            }
            _wheelAngle = (_wheelAngle + _wheelSpeed) & 0xFFF;
            // SD 0x701 from the casino scene bank (TOWN.BIN VAB 0x45A000): the wheel's click.
            if ((previous >> 8) != (_wheelAngle >> 8)) PlayTheaterSound("Casino701", 0.4f);
        }

        private void PresentRouletteKoh()
        {
            var p = _rouletteKoh;
            var floor = CasinoFloor(new Vector3(p.x, 0f, p.z), 0f);
            if (_rouletteMode is RouletteMode.Jumping or RouletteMode.Riding)
            {
                float radius = Flat(p - WheelCenter);
                if (radius <= WheelRadius)
                {
                    // Native mesh 0x2D has a one-unit rim and a raised centre.
                    // Keep Koh above that surface rather than inside the model.
                    float surface = radius <= 2f ? 2f - radius * 0.5f : 1f;
                    p.y = Mathf.Max(p.y, surface + 0.06f);
                }
            }
            _player.transform.position = new Vector3(p.x, floor.y + TownActorFootOffset + p.y, p.z);
            _playerCell = new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z));
            _townFacing = Vector2.up;
            if (_kohSprite) _kohSprite.SetFacing(_townFacing);
        }

        private void EndRoulette(bool refund)
        {
            if (refund && _rouletteBet > 0) { _gold += _rouletteBet; PlayRetailSound(RetailSfx.Cancel); }
            _rouletteBet = 0;
            _rouletteMode = RouletteMode.Idle;
            _roulettePad = -1;
            _wheelPhase = 0;
            UpdateBetStacks();
            PresentRoulettePads();
            PersistTownProgress();
        }

        // ================================================================ coins

        private void SpawnCasinoCoin(int denomination, Vector3 position, Vector3 velocity, int flightTicks)
        {
            var view = CreateNativeCasinoCoin(denomination, position);
            _casinoCoins.Add(new CasinoCoin
            {
                View = view, Position = position, Velocity = velocity, Denomination = denomination,
                State = 2, Timer = flightTicks
            });
        }

        /// <summary>update_bouncing_pickup states 2-6.</summary>
        private void TickCasinoCoins()
        {
            for (int i = _casinoCoins.Count - 1; i >= 0; i--)
            {
                var coin = _casinoCoins[i];
                bool gone = false;
                float floor = CasinoFloor(coin.Position, 0.15f).y;
                if (coin.State == 2)
                {
                    coin.Position += coin.Velocity;
                    coin.Velocity.y -= 3f / CasinoUnits;
                    if (coin.Position.y < floor) { coin.Position.y = floor; coin.Velocity.y = Mathf.Abs(coin.Velocity.y) * 0.4f; }
                    if (--coin.Timer <= 0)
                    {
                        PlayRetailSound(RetailSfx.CoinBounce, 0.4f);
                        coin.Velocity = new Vector3(0f, Random.Range(0f, 0.6f) * 8f / CasinoUnits, 0f);
                        coin.Timer = Random.Range(30, 46);
                        coin.State = 3;
                    }
                }
                else if (coin.State == 3)
                {
                    coin.Position.y += coin.Velocity.y;
                    coin.Velocity.y -= 3f / CasinoUnits;
                    if (coin.Position.y < floor)
                    {
                        coin.Position.y = floor;
                        coin.Velocity.y = -coin.Velocity.y * 0.5f;
                    }
                    if (--coin.Timer <= 0) { coin.Timer = 180; coin.State = 4; coin.Position.y = floor; }
                }
                else if (coin.State == 4)
                {
                    if (--coin.Timer <= 0) { coin.Timer = 31; coin.State = 5; }
                }
                else if (coin.State == 5)
                {
                    if (coin.View) coin.View.SetActive(((coin.Timer >> 2) & 1) == 0);
                    if (--coin.Timer <= 0) gone = true;
                }
                if (!gone && coin.State >= 3 && _player && Flat(_player.transform.position - coin.Position) < 0.55f)
                {
                    int value = CoinValues[coin.Denomination];
                    _gold = (int)Mathf.Min((long)_gold + value, int.MaxValue);
                    _casinoPrizeTotal = Mathf.Min(_casinoPrizeTotal + value, 999999);
                    PlayRetailSound(RetailSfx.CoinPickup);
                    PersistTownProgress();
                    gone = true;
                }
                if (coin.View)
                {
                    coin.View.transform.position = coin.Position;
                    coin.View.transform.Rotate(Vector3.forward, 9f, Space.Self);
                }
                if (!gone) continue;
                if (coin.View) RemoveGenerated(coin.View);
                _casinoCoins.RemoveAt(i);
            }
        }

        // ================================================================== HUD

        private bool DrawCasino(float width, float height)
        {
            if (!AtCasinoSlots && !AtCasinoRoulette) return false;
            // initialize_minigame_display: "Prize" box (184, 20) 124x24 on 320x240.
            float sx = width / 320f, sy = height / 240f;
            if (_casinoPrizeTotal > 0 || _slotMode != SlotMode.Idle || _rouletteMode != RouletteMode.Idle)
            {
                var prize = new Rect(184f * sx, 20f * sy, 124f * sx, 24f * sy + 8f);
                DrawDialogueFrame(prize);
                DrawHudText(new Rect(prize.x + 10f, prize.y + 4f, prize.width - 20f, prize.height - 8f),
                    T($"Prize {_casinoPrizeTotal:000000}G", $"Prêmio {_casinoPrizeTotal:000000}G"), _bodyStyle);
            }

            string panelText = null, hint = null;
            if (_slotMode == SlotMode.Bet)
            {
                panelText = T($"Bet: {_slotCoins} coin{(_slotCoins > 1 ? "s" : "")} ({_slotCoins * 100} G)\nLines: " +
                              (_slotCoins == 1 ? "middle" : _slotCoins == 2 ? "3 rows" : "3 rows + diagonals"),
                    $"Aposta: {_slotCoins} moeda{(_slotCoins > 1 ? "s" : "")} ({_slotCoins * 100} G)\nLinhas: " +
                    (_slotCoins == 1 ? "do meio" : _slotCoins == 2 ? "3 fileiras" : "3 fileiras + diagonais"));
                hint = T($"↑↓ bet · [{InteractKey}] start · cancel: refund", $"↑↓ aposta · [{InteractKey}] girar · cancelar: devolve");
            }
            else if (_slotMode == SlotMode.Spinning && _slotReel < 3)
                hint = T($"[{InteractKey}] stop reel {_slotReel + 1}", $"[{InteractKey}] parar o rolo {_slotReel + 1}");
            else if (_slotMode == SlotMode.Paying)
                panelText = T($"You won {_slotWinnings} G! Collect the coins.", $"Você ganhou {_slotWinnings} G! Pegue as moedas.");
            else if (_rouletteMode == RouletteMode.Bet)
            {
                panelText = T($"Switch x{6 / (_roulettePad + 1)} · Bet {_rouletteBet} G", $"Botão x{6 / (_roulettePad + 1)} · Aposta {_rouletteBet} G");
                hint = T($"↑↓ bet · [{InteractKey}] decide · cancel: refund", $"↑↓ aposta · [{InteractKey}] confirmar · cancelar: devolve");
            }
            else if (_rouletteMode == RouletteMode.Ready)
                hint = _wheelPhase == 2 ? T($"[{InteractKey}] jump from the diving board", $"[{InteractKey}] pular do trampolim")
                    : T("The roulette is speeding up…", "A roleta está acelerando…");
            else if (_rouletteMode == RouletteMode.Paying)
                panelText = T("You won! Collect the money.", "Você ganhou! Pegue o dinheiro.");
            else if (_player && !_activeTownNpc)
            {
                if (AtCasinoSlots && Flat(_player.transform.position - SlotSwitch) < 0.9f && _slotMode == SlotMode.Idle)
                    hint = WithInteractHint("Hit the switch (100 G)", "Apertar o botão (100 G)");
                if (AtCasinoRoulette && _rouletteMode == RouletteMode.Idle)
                    for (int pad = 0; pad < 3; pad++)
                        if (Flat(_player.transform.position - RoulettePadPosition(pad)) < 0.75f)
                            hint = WithInteractHint($"Bet on x{6 / (pad + 1)}", $"Apostar no x{6 / (pad + 1)}");
            }

            if (panelText != null)
            {
                var box = new Rect(width * 0.5f - 210f, height - 150f, 420f, 62f);
                DrawDialogueFrame(box);
                DrawHudText(new Rect(box.x + 12f, box.y + 6f, box.width - 24f, box.height - 12f), panelText, _messageStyle);
            }
            if (hint != null)
            {
                var box = new Rect((width - 360f) * 0.5f, height * 0.66f, 360f, 32f);
                DrawDialogueFrame(box);
                DrawHudText(new Rect(box.x + 9f, box.y + 4f, box.width - 18f, 24f), hint, _messageStyle);
            }
            return false;
        }
    }
}
