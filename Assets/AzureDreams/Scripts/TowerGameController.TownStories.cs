using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string StoryPrefix = "town.story.";
        private const int StoryComplete = 100;
        private const string NicoHouse = "Interior_22_nico_house";
        private const string CherrlHouse = "Interior_18_cherrl_house";
        private const string HushClinic = "Interior_0f_hospital";
        private const string CherrlHospital = "Interior_10_hospital_2";
        private const string RhodesHouse = "Interior_21_rhodes_house";
        private const string DrHalHouse = "Interior_24_dr_hal_house";
        private const string LibraryRoom = "Interior_07_library";
        private const string RestaurantRoom = "Interior_0e_restaurant";
        private const string BarRoom = "Interior_20_bar";
        private const string FortuneHouseInterior = "Interior_17_fortune_house";
        private const string TheaterRoom = "Interior_0b_theater";
        private const string PoolClosedRoom = "Interior_0d_pool_quest_not_done";
        private const string PoolOpenRoom = "Interior_0d_pool_quest_done";
        private const string CasinoRoom = "Interior_2b_casino_1";
        private const string MiaGlassesObjectId = "mia_glasses";
        private const int MiaFirstBook = 0;
        private const int MiaBookInterest = 1;
        private const int MiaBookAmused = 2;
        private const int MiaGhoshCollision = 3;
        private const int MiaGlassesOnFloor = 4;
        private const int MiaGlassesReturned = 5;
        private const int MiaAfterGlasses = 6;
        private const int MiaOpenUp = 7;
        private const int MiaPoolInvitation = 8;
        private const int MiaPoolDate = 9;
        private const int MiaLibraryFollowUpOne = 10;
        private const int MiaLibraryFollowUpTwo = 11;
        private const int MiaFollowStarts = 12;
        private const int MiaFollowing = 13;
        private const int MiaFollowingReady = 14;
        // The progression and terminal flag are the retail Nico route: Ft_niko_love.
        private const int NicoLoveFlag = 5230;
        private const int NicoFirstDonation = 1;
        private const int NicoSecondDonation = 2;
        private const int NicoFountainTalk = 3;
        private const int NicoAfterSelfiEgg = 4;
        private const int NicoCultureTalkFirst = 5;
        private const int NicoCultureTalkLast = 9;
        private const int NicoCheroIntroduction = 10;
        private const int NicoGhoshConfrontation = 11;
        private const int NicoCheroOpinion = 12;
        private const int NicoConfessionWake = 13;
        private const int NicoHouseGuard = 14;
        private const int NicoHouseTalkFirst = 15;
        private const int NicoHouseTalkSecond = 16;
        private const int NicoFinalArrival = 17;
        private const int CherrlNurseFlag = 4605; // Fp_cheriru_kangofu
        private const int CherrlLoveFlag = 5240; // Ft_cheriru_love
        private const int CherrlFirstMeeting = 0;
        private const int CherrlFriendship = 1;
        private const int CherrlAlone = 2;
        private const int CherrlMotherRequest = 3;
        private const int CherrlWalkRequest = 4;
        private const int CherrlHerbAvailable = 5;
        private const int CherrlHerbDelivered = 6;
        private const int CherrlRecovery = 7;
        private const int CherrlHospitalFollowUp = 8;
        private const int CherrlNurseChoice = 9;
        private const int CherrlNurseDebut = 10;
        private const int CherrlHospitalAbsence = 11;
        private const int CherrlFinalTalk = 12;
        private const int CherrlHomeRecovery = 13;
        private const int CherrlHomeMascot = 14;
        private const int HushClinicIntroduction = 0;
        private const int HushClinicDonation = 1;
        private const int HushClinicPlanReady = 2;
        // Dr. Hal starts the Water Medal / library branch recorded in the
        // original town-event table (K3, K5, K7, K9 and K11).
        private const int HalTowerQuestion = 0;
        private const int HalPoolRequest = 1;
        private const int HalWaterMedalSearch = 2;
        private const int HalPoolRestored = 3;
        private const int HalResearchTalk = 4;
        private const int HalLibraryProposal = 5;
        private const int HalLibraryRequested = 6;
        // Ftt_fkoya_isaku_scene00_first / _ok in the retail town-event table.
        private const int IsaacQuestFloor = 5;
        private const int IsaacMetFlag = 0x145E;
        private const int IsaacAcceptedFlag = 0x145F;
        private const string CherrlRouteVersionKey = StoryPrefix + "cherrl.routeVersion";
        private const string CherrlPortraitPrefix = "Dialogue/CherrlRetail_variant";
        private const string CherrlFriendshipAskedKey = StoryPrefix + "cherrl.friendshipAsked";
        private const string CherrlWindowMeetingKey = StoryPrefix + "cherrl.windowMeeting";
        private const string CherrlInvitedInsideKey = StoryPrefix + "cherrl.invitedInside";
        private const string CherrlOutdoorWalkKey = StoryPrefix + "cherrl.outdoorWalk";
        private const string CherrlAnonymousHerbKey = StoryPrefix + "cherrl.anonymous";
        private const string CherrlHouseConversationKey = StoryPrefix + "cherrl.houseConversation";
        private const string CherrlMascotReceivedKey = StoryPrefix + "cherrl.mascotReceived";
        private const string HushHerbFollowUpKey = StoryPrefix + "hush.herbFollowUp";
        private const string VivianCasinoVisitKey = StoryPrefix + "vivian.casinoVisit";
        private const string VivianCasinoGreetingKey = StoryPrefix + "vivian.casinoGreeting";
        private const string VivianCasinoLoanKey = StoryPrefix + "vivian.casinoLoan";
        private const string VivianTheaterDreamKey = StoryPrefix + "vivian.theaterDream";
        private const string VivianRoseSceneKey = StoryPrefix + "vivian.roses";
        private const string VivianPoolDateKey = StoryPrefix + "vivian.poolDate";
        private const string VivianBarEpilogueKey = StoryPrefix + "vivian.barEpilogue";
        private static readonly int[][] CherrlPortraitFrameDurations =
        {
            new[] { 96, 4, 8, 2 }, new[] { 96, 4, 8, 2 }, new[] { 96, 4, 8, 2 },
            new[] { 2 }, new[] { 2 }, new[] { 64, 64 }, new[] { 96, 4, 8, 2 },
            new[] { 96, 4, 8, 2 }, new[] { 96, 2, 4, 8, 2, 4 }
        };
        // Miya's final portrait is the only one in her extracted set with
        // multiple cels.  These timings match the normal town portrait blink.
        private static readonly int[] MiaPortraitBlinkFrames = { 96, 6, 12, 2 };
        // Vivian's extracted expressions use the retail portrait blink cadence.
        // Variants 0 and 5 are still illustrations; the others contain blink cels.
        private static readonly int[][] VivianPortraitFrameDurations =
        {
            new[] { 2 }, new[] { 96, 4, 8, 2 }, new[] { 128, 16, 4, 4 },
            new[] { 96, 4, 8, 2 }, new[] { 96, 4, 64, 4 }, new[] { 2 }
        };
        private GameObject _miaGlassesObject;
        private bool _vivianFonScoutPlaying;
        private bool _vivianRoseScenePlaying;

        [SerializeField] private GameObject[] _storyResidents;
        private NPCController _storySpeaker;
        private (string Label, Action Apply)[] _storyChoices;
        private int _storyChoice;
        private Vector2 _storyScroll;
        private const int VisibleStoryChoices = 7;
        private bool _storyFocusChoice;
        private GUIStyle _storyChoiceStyle;
        private string _storyPortraitPath;
        private float _storyPortraitStarted;
        private int[] _storyPortraitFrameDurations;

        private static int StoryDay => TownSaveVariables.GetInt(StoryPrefix + "day");
        private static int StoryStage(string name) => TownSaveVariables.GetInt(StoryPrefix + name + ".stage");
        private static bool StoryNewDay(string name) => StoryDay > TownSaveVariables.GetInt(StoryPrefix + name + ".day", -1);
        private static bool StoryBuilding(string name) => TownSaveVariables.GetBool(ConstructionKey + name);
        private string StoryRoom => _interior ? _interior.interiorId : string.Empty;
        private static bool PoolRestored => TownRetailScriptFlags.CreatePersistent().IsFlagSet(PoolQuestCompleteFlag) ||
            TownRetailScriptFlags.CreatePersistent().IsFlagSet(PoolOpenFlag);

        private void AdvanceTownStory(string name, int stage)
        {
            TownSaveVariables.SetInt(StoryPrefix + name + ".stage", stage);
            TownSaveVariables.SetInt(StoryPrefix + name + ".day", StoryDay);
            PersistTownProgress();
        }

        private const string OkamiReimaginedPortrait = "Dialogue/Aunt_variant1_frame0";

        private bool StoryTalk(NPCController npc, string en, string pt, Action next = null)
        {
            return StoryTalk(npc, en, pt, null, next);
        }

        private bool TownDialogueSequence(NPCController npc, string portrait, Action done,
            params (string English, string Portuguese)[] lines)
        {
            bool ShowPage(int page) => StoryTalk(npc, lines[page].English, lines[page].Portuguese, portrait,
                page + 1 < lines.Length ? () => ShowPage(page + 1) : done);
            return ShowPage(0);
        }

        private void GhoshSequence(NPCController npc, Action done,
            params (string English, string Portuguese)[] lines)
        {
            void ShowLine(int index)
            {
                if (index >= lines.Length)
                {
                    done?.Invoke();
                    return;
                }
                GhoshTalk(npc, lines[index].English, lines[index].Portuguese,
                    () => ShowLine(index + 1));
            }
            ShowLine(0);
        }

        private bool StoryTalk(NPCController npc, string en, string pt, string portraitPath, Action next = null)
        {
            return StoryAsk(npc, en, pt, portraitPath, (T("Continue", "Continuar"), next));
        }

        private bool StoryAsk(NPCController npc, string en, string pt, params (string Label, Action Apply)[] choices)
        {
            return StoryAsk(npc, en, pt, null, choices);
        }

        private bool StoryAsk(NPCController npc, string en, string pt, string portraitPath,
            params (string Label, Action Apply)[] choices)
        {
            // Okami has no retail portrait; Reimagined gives her the redesigned aunt portrait, Classic none.
            if (portraitPath == null && npc && npc.npcId == "okami" && AssetMode.IsReimagined)
                portraitPath = OkamiReimaginedPortrait;
            _storySpeaker = npc;
            _storyChoices = choices;
            _storyChoice = 0;
            _storyScroll = Vector2.zero;
            _storyFocusChoice = false;
            _storyPortraitPath = portraitPath;
            _storyPortraitStarted = Time.unscaledTime;
            _storyPortraitFrameDurations = StoryPortraitFrameDurationsFor(portraitPath);
            var stats = npc.GetComponent<CharacterStats>();
            ShowTownDialogue(stats ? stats.displayName : npc.npcId, T(en, pt), stats ? stats.portraitTexture : null);
            _message = _dialogueLine;
            return true;
        }

        // Story portraits are polled every repaint so the first streaming request
        // may complete after a dialogue is already visible.
        private Texture2D StoryDialoguePortrait()
        {
            if (!_storySpeaker || string.IsNullOrEmpty(_storyPortraitPath)) return null;
            if (_storyPortraitPath == FonPortrait) return FonDialoguePortrait(_storyPortraitStarted);
            if (_storyPortraitPath == WeedyBookOfferPortrait) return WeedyBookDialoguePortrait();
            if (_storyPortraitPath == JordaPortrait) return JordaDialoguePortrait(_storyPortraitStarted);
            if (_storyPortraitPath is GhoshIrritatedPortrait or GhoshSwimsuitPortrait)
                return GhoshDialoguePortrait(_storyPortraitPath, _storyPortraitStarted);
            string path = _storyPortraitPath;
            if (_storyPortraitFrameDurations != null)
            {
                int elapsedFrames = Mathf.FloorToInt((Time.unscaledTime - _storyPortraitStarted) * 60f);
                int frame = StoryPortraitFrame(_storyPortraitFrameDurations, elapsedFrames);
                path = path.Replace("_frame0", "_frame" + frame);
            }
            return StreamPortrait(path) ?? (path != _storyPortraitPath ? StreamPortrait(_storyPortraitPath) : null);
        }

        private static int StoryPortraitFrame(int[] durations, int elapsedFrames)
        {
            if (durations == null || durations.Length == 0) return 0;
            int total = 0;
            for (int i = 0; i < durations.Length; i++) total += durations[i];
            if (total <= 0) return 0;
            int frameTime = Mathf.Max(0, elapsedFrames) % total;
            for (int i = 0; i < durations.Length; i++)
            {
                frameTime -= durations[i];
                if (frameTime < 0) return i;
            }
            return 0;
        }

        private static int[] StoryPortraitFrameDurationsFor(string portraitPath) =>
            PattyPortraitFrameDurationsFor(portraitPath) ?? CherrlPortraitFrameDurationsFor(portraitPath) ??
            MiaPortraitFrameDurationsFor(portraitPath) ?? VivianPortraitFrameDurationsFor(portraitPath) ??
            FurPortraitFrameDurationsFor(portraitPath) ?? SelfiPortraitFrameDurationsFor(portraitPath) ??
            NicoPortraitFrameDurationsFor(portraitPath);

        private const string MiaPortraitPrefix = "Dialogue/MiyaRetail_variant";

        private static string MiaPortrait(int expression) => $"{MiaPortraitPrefix}{expression}_frame0";

        private static int[] MiaPortraitFrameDurationsFor(string portraitPath) =>
            portraitPath == PoolMiaPortrait
                ? MiaPortraitBlinkFrames : null;

        private const string VivianPortraitPrefix = "Dialogue/VivianRetail_variant";

        private static int[] VivianPortraitFrameDurationsFor(string portraitPath)
        {
            const string prefix = VivianPortraitPrefix;
            if (string.IsNullOrEmpty(portraitPath) || !portraitPath.StartsWith(prefix, System.StringComparison.Ordinal) ||
                portraitPath.Length <= prefix.Length) return null;
            int variant = portraitPath[prefix.Length] - '0';
            return variant >= 0 && variant < VivianPortraitFrameDurations.Length
                ? VivianPortraitFrameDurations[variant] : null;
        }

        private bool MiaTalk(NPCController npc, int expression, string en, string pt, System.Action next = null) =>
            StoryTalk(npc, en, pt, MiaPortrait(expression), next);

        private bool MiaAsk(NPCController npc, int expression, string en, string pt,
            params (string Label, System.Action Apply)[] choices) =>
            StoryAsk(npc, en, pt, MiaPortrait(expression), choices);

        private static string VivianPortrait(int expression) => $"{VivianPortraitPrefix}{expression}_frame0";

        private bool VivianTalk(NPCController npc, int expression, string en, string pt, Action next = null) =>
            StoryTalk(npc, en, pt, VivianPortrait(expression), next);

        private bool VivianAsk(NPCController npc, int expression, string en, string pt,
            params (string Label, Action Apply)[] choices) =>
            StoryAsk(npc, en, pt, VivianPortrait(expression), choices);

        // Vivian's event data uses individual message pages between choices.
        // Do not combine these pages: the retail portraits and input cadence
        // change on each acknowledgement.
        private void VivianSequence(NPCController vivian, int expression, Action done,
            params (string English, string Portuguese)[] lines)
        {
            void ShowLine(int index)
            {
                if (index >= lines.Length)
                {
                    done?.Invoke();
                    return;
                }
                VivianTalk(vivian, expression, lines[index].English, lines[index].Portuguese,
                    () => ShowLine(index + 1));
            }
            ShowLine(0);
        }

        private bool NicoTalk(NPCController npc, int expression, string en, string pt, Action next = null) =>
            StoryTalk(npc, en, pt, NicoPortrait(expression), next);

        private bool NicoAsk(NPCController npc, int expression, string en, string pt,
            params (string Label, Action Apply)[] choices) =>
            StoryAsk(npc, en, pt, NicoPortrait(expression), choices);

        private void CloseStoryPrompt(bool finishWakeUpVisit = true)
        {
            _shielaChargeOpen = false;
            bool closingChangingRoom = _storySpeaker && _storySpeaker == _poolChangingRoomSpeaker;
            _storySpeaker = null;
            _storyChoices = null;
            _storyPortraitPath = null;
            _storyPortraitFrameDurations = null;
            _message = string.Empty;
            if (closingChangingRoom) ReleasePoolChangingRoomInput();
            if (finishWakeUpVisit)
            {
                FinishRestaurantMeal();
                _vivianFonScoutPlaying = false;
                _vivianRoseScenePlaying = false;
                _furGhoshScenePlaying = false;
                _furMusicBoxScenePlaying = false;
                ClearSelfiEggGuest();
                DismissNicoFountainGhosh();
                FinishWakeUpVisit();
            }
        }

        private void PickStoryChoice(int index)
        {
            if (_storyChoices == null || index < 0 || index >= _storyChoices.Length) return;
            var apply = _storyChoices[index].Apply;
            CloseStoryPrompt(finishWakeUpVisit: false);
            PlayRetailSound(RetailSfx.Confirm);
            apply?.Invoke();
            if (!_storySpeaker) FinishRestaurantMeal();
            if (!_storySpeaker) FinishWakeUpVisit();
        }

        private bool HandleTownStory()
        {
            if (!_storySpeaker || _storyChoices == null) return false;
            if (_cancel?.WasPressedThisFrame() == true) { CloseStoryPrompt(); return true; }
            if (Keyboard.current != null)
            {
                int step = (MenuDirectionPressed(2) ? 1 : 0) - (MenuDirectionPressed(1) ? 1 : 0);
                _storyChoice = (_storyChoice + step + _storyChoices.Length) % _storyChoices.Length;
                if (step != 0)
                {
                    _storyFocusChoice = true;
                    PlayRetailSound(RetailSfx.Cursor);
                }
            }
            if (_interact?.WasPressedThisFrame() == true) PickStoryChoice(_storyChoice);
            return true;
        }

        private float MeasureStoryChoices(float width)
        {
            float height = 0f;
            foreach (var choice in _storyChoices) height += StoryChoiceRowHeight(choice.Label, width);
            return height;
        }

        private float StoryChoiceRowHeight(string label, float width)
        {
            _storyChoiceStyle ??= new GUIStyle(_dialogueTextStyle) { alignment = TextAnchor.MiddleLeft };
            return Mathf.Max(DialogueChoiceRowHeight, _storyChoiceStyle.CalcHeight(new GUIContent(label), width - 10f) + 6f);
        }

        private void DrawStoryChoices(Rect viewport)
        {
            float contentHeight = MeasureStoryChoices(viewport.width);
            bool scroll = contentHeight > viewport.height;
            float rowWidth = scroll ? viewport.width - 18f : viewport.width;
            if (scroll) contentHeight = MeasureStoryChoices(rowWidth);
            if (_storyFocusChoice)
            {
                float top = 0f;
                for (int i = 0; i < _storyChoice; i++)
                    top += StoryChoiceRowHeight(_storyChoices[i].Label, rowWidth);
                float bottom = top + StoryChoiceRowHeight(_storyChoices[_storyChoice].Label, rowWidth);
                _storyScroll.y = Mathf.Clamp(_storyScroll.y, Mathf.Max(0f, bottom - viewport.height), top);
                _storyFocusChoice = false;
            }
            _storyScroll.y = Mathf.Clamp(_storyScroll.y, 0f, Mathf.Max(0f, contentHeight - viewport.height));
            var content = new Rect(0f, 0f, rowWidth, contentHeight);
            if (scroll) _storyScroll = GUI.BeginScrollView(viewport, _storyScroll, content);
            else GUI.BeginGroup(viewport);
            int picked = -1;
            float y = 0f;
            for (int i = 0; i < _storyChoices.Length; i++)
            {
                string label = _storyChoices[i].Label;
                float rowHeight = StoryChoiceRowHeight(label, rowWidth);
                var row = new Rect(0f, y, rowWidth, rowHeight - 2f);
                if (DrawInventoryButton(row, label, i == _storyChoice, _storyChoiceStyle)) picked = i;
                y += rowHeight;
            }
            if (scroll) GUI.EndScrollView();
            else GUI.EndGroup();
            if (picked >= 0) PickStoryChoice(picked);
        }

        private bool TryTownStory(NPCController npc)
        {
            if (TryConstructionStory(npc) || PoolService(npc)) return true;
            return npc.npcId switch
            {
                "center_nada" or "center_hear" or "center_jacof" or "center_pop" or
                    "center_uncle" or "center_aunt" or "center_aunt_2" => FountainResidentTalk(npc),
                "nico" or "nico_dad" or "chero" or "nico_fountain" or "nico_ghosh" or "nico_door_ghosh" or
                    "nico_library" or "nico_theater" => NicoStory(npc),
                "wreath" when HouseIndex >= 0 => WreathStory(npc),
                "weedy" when HouseIndex >= 0 => WeedyStory(npc),
                "ghosh" when StoryRoom == RhodesHouse => GhoshHouseTalk(npc),
                "isaac" => IsaacStory(npc),
                "cherrl" or "hush" => CherrlStory(npc),
                "nadia" when StoryRoom == CherrlHouse => NadiaStory(npc),
                "vivian" or "okami" => VivianStory(npc),
                "patty" => PattyStory(npc),
                "mia" => MiaStory(npc),
                "hal" => HalStory(npc),
                "selfi" => SelfiTownStory(npc),
                "fur" when InGeneralStore => FurStory(npc),
                "shiela" when StoryRoom == FortuneHouseInterior => ShielaConsultation(npc),
                _ => false
            };
        }

        private const string HalPortrait = "Dialogue/Grandpa_variant0_frame0";

        private bool HalTalk(NPCController npc, string en, string pt, System.Action next = null) =>
            StoryTalk(npc, en, pt, HalPortrait, next);

        private bool HalAsk(NPCController npc, string en, string pt,
            params (string Label, System.Action Apply)[] choices) =>
            StoryAsk(npc, en, pt, HalPortrait, choices);

        // This branch deliberately lives in Dr. Hal's house.  The temporary
        // outdoor construction giver used a generic resident and let players
        // bypass the original Water Medal sequence entirely.
        private bool HalStory(NPCController npc)
        {
            if (StoryRoom != DrHalHouse) return false;

            int stage = StoryStage("hal");
            bool libraryBuilt = StoryBuilding("library");
            bool libraryRequested = TownRetailScriptFlags.CreatePersistent().IsFlagSet(LibraryPlanFlag);

            if (libraryBuilt)
                return HalTalk(npc,
                    "A library at last. With these records, future tamers won't have to learn everything the hard way.",
                    "Finalmente, uma biblioteca. Com estes registros, os futuros domadores não precisarão aprender tudo da forma mais difícil.",
                    () => { if (stage != StoryComplete) AdvanceTownStory("hal", StoryComplete); });

            if (libraryRequested || stage >= HalLibraryRequested)
                return HalTalk(npc,
                    "The plans are with Tonka. Eight thousand gold will give Monsbaiya the library it deserves.",
                    "Os planos estão com Tonka. Oito mil de ouro darão a Monsbaiya a biblioteca que ela merece.");

            if (stage == HalTowerQuestion)
            {
                // Older saves could already have completed the pool before Dr.
                // Hal was placed in his house.  Continue from the equivalent
                // retail point instead of making that save state impossible.
                if (PoolRestored)
                    return HalTalk(npc,
                        "You restored the pool without my help. Excellent! I have been collecting what little is known about the Tower.",
                        "Você restaurou a piscina sem minha ajuda. Excelente! Estou reunindo tudo o que se sabe sobre a Torre.",
                        () => AdvanceTownStory("hal", HalPoolRestored));

                return HalAsk(npc,
                    "The Tower has swallowed too much history. Will you hear about the work I want to do?",
                    "A Torre engoliu história demais. Você quer ouvir sobre o trabalho que pretendo fazer?",
                    (T("Ask about the Tower", "Perguntar sobre a Torre"), () =>
                        HalTalk(npc,
                            "Start by helping Wookie at the pool. The Water Medal was stolen by a Picket said to hide on floor 25.",
                            "Comece ajudando Wookie na piscina. O Medalhão da Água foi roubado por um Picket que, dizem, se esconde no 25º andar.",
                            () => AdvanceTownStory("hal", HalPoolRequest))),
                    (T("Not now", "Agora não"), null));
            }

            if (stage == HalPoolRequest)
                return HalTalk(npc,
                    "Talk to Wookie at the pool. Once he asks for the Water Medal, a blue Picket will be waiting on floor 25.",
                    "Fale com Wookie na piscina. Quando ele pedir o Medalhão da Água, um Picket azul estará esperando no 25º andar.");

            if (stage == HalWaterMedalSearch)
                return HalTalk(npc,
                    "The Water Medal belongs to Wookie. Recover it from the Picket on floor 25, then return it to the pool.",
                    "O Medalhão da Água pertence a Wookie. Recupere-o com o Picket no 25º andar e devolva-o à piscina.");

            if (stage == HalPoolRestored)
                return HalTalk(npc,
                    "The pool is open again. That is proof that Monsbaiya can change when people work together.",
                    "A piscina está aberta de novo. Isso prova que Monsbaiya pode mudar quando todos trabalham juntos.",
                    () => AdvanceTownStory("hal", HalResearchTalk));

            if (stage == HalResearchTalk)
            {
                if (!StoryNewDay("hal"))
                    return HalTalk(npc, "Come back tomorrow. I need time to put my notes in order.",
                        "Volte amanhã. Preciso de tempo para organizar minhas anotações.");
                return HalTalk(npc,
                    "Every visit to the Tower reveals another fragment, but I have nowhere safe to keep the records.",
                    "Cada visita à Torre revela outro fragmento, mas não tenho um lugar seguro para guardar os registros.",
                    () => AdvanceTownStory("hal", HalLibraryProposal));
            }

            if (stage == HalLibraryProposal)
            {
                if (!StoryNewDay("hal"))
                    return HalTalk(npc, "A library could make this knowledge useful to everyone. Please think about it.",
                        "Uma biblioteca poderia tornar esse conhecimento útil para todos. Por favor, pense nisso.");
                return HalAsk(npc,
                    "Will you build a library for Monsbaiya? With it, we can preserve what the Tower teaches us.",
                    "Você construiria uma biblioteca para Monsbaiya? Com ela, poderemos preservar o que a Torre nos ensina.",
                    (T("I'll build it for you.", "Eu vou construí-la para você."), () =>
                    {
                        UnlockConstructionPlan(LibraryPlanFlag);
                        AdvanceTownStory("hal", HalLibraryRequested);
                    }),
                    (T("I need more time", "Preciso de mais tempo"), null));
            }

            return HalTalk(npc, "The Tower still has many secrets. A library will help us understand them.",
                "A Torre ainda guarda muitos segredos. Uma biblioteca nos ajudará a entendê-los.");
        }

        private int FindStoryItem(byte kind, byte id)
        {
            var items = _townInventory.DungeonItems;
            for (int i = 0; i < items.Count; i++)
                if (items[i].Kind == kind && items[i].Subtype == id) return i;
            return -1;
        }

        private bool IsaacStory(NPCController npc)
        {
            var flags = TownRetailScriptFlags.CreatePersistent();
            if (flags.IsFlagSet(WindmillQuestCompleteFlag))
                return StoryTalk(npc, "The windmills are working again. Thank you, {name}!",
                    "Os moinhos voltaram a funcionar. Obrigado, {name}!");
            if (!flags.IsFlagSet(IsaacAcceptedFlag) &&
                TownSaveVariables.GetInt(TownReputation.HighestFloorKey) < IsaacQuestFloor)
                return StoryTalk(npc, "I look after the windmills here.", "Eu cuido dos moinhos daqui.");

            if (!flags.IsFlagSet(IsaacAcceptedFlag))
            {
                flags.SetBit(IsaacMetFlag);
                PersistTownProgress();
                return StoryTalk(npc, "The magic pot produces Guru's oil on its own.",
                    "O pote mágico produz o óleo do Guru por conta própria.", () =>
                    StoryAsk(npc, "I heard there was another pot on the 15th floor of the Tower. Could you get it for me?",
                        "Ouvi dizer que há outro pote no 15º andar da Torre. Você pode buscá-lo para mim?",
                        (T("I'll go and get it for you.", "Vou buscar para você."), () =>
                        {
                            flags.SetBit(IsaacAcceptedFlag);
                            AdvanceTownStory("windmills", 1);
                            StoryTalk(npc, "Thanks. Look for it on the 15th floor.",
                                "Obrigado. Procure no 15º andar.");
                        }),
                        (T("Maybe later", "Talvez depois"), null)));
            }

            if (FindStoryItem(QuestItemKind, GurusOilPotId) < 0)
                return StoryTalk(npc, "I heard there was another pot on the 15th floor of the Tower. Could you get it for me?",
                    "Ouvi dizer que há outro pote no 15º andar da Torre. Você pode buscá-lo para mim?");

            return StoryAsk(npc, "Is that Guru's oil pot? May I have it?",
                "Esse é o Pote de Óleo do Guru? Pode me entregar?",
                (T("Give him the pot", "Entregar o pote"), () =>
                {
                    int pot = FindStoryItem(QuestItemKind, GurusOilPotId);
                    if (pot < 0 || !_townInventory.ConsumeDungeonItem(pot, QuestItemKind)) return;
                    flags.SetBit(WindmillQuestCompleteFlag);
                    AdvanceTownStory("windmills", StoryComplete);
                    StoryTalk(npc, "Thank you! Now I can get the windmills running again.",
                        "Obrigado! Agora posso pôr os moinhos para funcionar de novo.");
                }),
                (T("Keep it for now", "Guardar por enquanto"), null));
        }

        private NPCController FindStoryActor(string id) =>
            _townPeople.Find(candidate => candidate && candidate.npcId == id);

        private IEnumerator PlayStoryWalk(NPCController actor, Vector3 target, float duration)
        {
            if (!actor) yield break;
            bool enabled = actor.enabled;
            actor.enabled = false;
            var view = actor.GetComponent<ActorLocomotionView>();
            Vector3 start = actor.transform.position;
            Vector3 direction = target - start;
            direction.y = 0f;
            float elapsed = 0f;
            while (actor && elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                actor.transform.position = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                view?.Present(direction, elapsed * 4f, true);
                yield return null;
            }
            if (actor)
            {
                actor.transform.position = target;
                view?.Present(direction, 0f, false);
                actor.enabled = enabled;
            }
        }

        private void CompleteNicoStory()
        {
            TownRetailScriptFlags.CreatePersistent().SetBit(NicoLoveFlag);
            AdvanceTownStory("nico", StoryComplete);
        }

        private static int[] CherrlPortraitFrameDurationsFor(string portraitPath)
        {
            const string prefix = CherrlPortraitPrefix;
            if (string.IsNullOrEmpty(portraitPath) || !portraitPath.StartsWith(prefix, StringComparison.Ordinal) ||
                portraitPath.Length <= prefix.Length) return null;
            int variant = portraitPath[prefix.Length] - '0';
            return variant >= 0 && variant < CherrlPortraitFrameDurations.Length ? CherrlPortraitFrameDurations[variant] : null;
        }

        private static string CherrlPortrait(int expression) => $"{CherrlPortraitPrefix}{expression}_frame0";

        private bool CherrlTalk(NPCController npc, int expression, string en, string pt, Action next = null) =>
            StoryTalk(npc, en, pt, CherrlPortrait(expression), next);

        private bool CherrlAsk(NPCController npc, int expression, string en, string pt,
            params (string Label, Action Apply)[] choices) =>
            StoryAsk(npc, en, pt, CherrlPortrait(expression), choices);

        // The retail event script presents Cherrl's speeches as several short
        // pages. Keeping that cadence also retains each portrait change.
        private readonly struct CherrlStoryLine
        {
            internal readonly int Expression;
            internal readonly string English;
            internal readonly string Portuguese;

            internal CherrlStoryLine(int expression, string english, string portuguese)
            {
                Expression = expression;
                English = english;
                Portuguese = portuguese;
            }
        }

        private readonly struct TownStoryLine
        {
            internal readonly string English;
            internal readonly string Portuguese;

            internal TownStoryLine(string english, string portuguese)
            {
                English = english;
                Portuguese = portuguese;
            }
        }

        private bool CherrlTalkSequence(NPCController npc, Action next, params CherrlStoryLine[] lines)
        {
            if (lines == null || lines.Length == 0)
            {
                next?.Invoke();
                return true;
            }

            return CherrlTalkSequenceAt(npc, lines, 0, next);
        }

        private bool CherrlTalkSequenceAt(NPCController npc, CherrlStoryLine[] lines, int index, Action next)
        {
            CherrlStoryLine line = lines[index];
            Action continueSequence = index + 1 < lines.Length
                ? () => CherrlTalkSequenceAt(npc, lines, index + 1, next)
                : next;
            return CherrlTalk(npc, line.Expression, line.English, line.Portuguese, continueSequence);
        }

        private bool TownTalkSequence(NPCController npc, Action next, params TownStoryLine[] lines)
        {
            if (lines == null || lines.Length == 0)
            {
                next?.Invoke();
                return true;
            }

            return TownTalkSequenceAt(npc, lines, 0, next);
        }

        private bool TownTalkSequenceAt(NPCController npc, TownStoryLine[] lines, int index, Action next)
        {
            TownStoryLine line = lines[index];
            Action continueSequence = index + 1 < lines.Length
                ? () => TownTalkSequenceAt(npc, lines, index + 1, next)
                : next;
            return StoryTalk(npc, line.English, line.Portuguese, continueSequence);
        }

        // Hush opens the route from the original small clinic.  The plan flag is
        // consumed by the existing carpenter list, where the 4,000 G payment and
        // hospital construction are already authoritative.
        private bool HushClinicStory(NPCController hush)
        {
            if (StoryRoom != HushClinic || StoryBuilding("hospital")) return false;
            int stage = StoryStage("hush");
            if (stage == HushClinicIntroduction)
                return TownTalkSequence(hush, () => StoryAsk(hush,
                    "Look here. Don't you think this still remains a great looking clinic?",
                    "Olhe só. Você não acha que esta ainda é uma bela clínica?",
                    (T("What's so great about it?", "O que ela tem de tão boa?"), () =>
                        HushClinicFirstAnswer(hush, false)),
                    (T("Well... y-yes I guess...", "Bem... s-sim, eu acho..."), () =>
                        HushClinicFirstAnswer(hush, true))),
                    new TownStoryLine("{name}, how are you doing? I haven't seen you in a while.",
                        "{name}, como você está? Faz tempo que não vejo você."),
                    new TownStoryLine("In your case, germs and illnesses would want to run away from you, so you probably have little concern for a clinic.",
                        "No seu caso, até germes e doenças querem fugir. Então você provavelmente não se preocupa muito com uma clínica."));
            if (stage == HushClinicDonation && StoryNewDay("hush"))
                return TownTalkSequence(hush, () => StoryAsk(hush,
                    "{name}. Let me guess. Seeing that this clinic doesn't have sufficient equipment, you've come to make a donation towards building a hospital.",
                    "{name}. Deixe-me adivinhar. Vendo que esta clínica não tem equipamentos suficientes, você veio fazer uma doação para construir um hospital.",
                    (T("Yes, let me make a donation.", "Sim, quero fazer uma doação."), () =>
                        ConfirmHushClinicDonation(hush)),
                    (T("Not yet", "Ainda não"), null)),
                    new TownStoryLine("But there was a time when I could not save a patient because I didn't have the equipment.",
                        "Mas houve uma vez em que não consegui salvar um paciente porque não tinha o equipamento."),
                    new TownStoryLine("I should have been able to help...", "Eu deveria ter conseguido ajudar..."),
                    new TownStoryLine("By the way... Wreath tells me you're going to the tower.",
                        "Aliás... Wreath me contou que você está indo à torre."));
            if (stage == HushClinicPlanReady)
                return StoryTalk(hush, "I'm not that great a doctor, but a kind doctor who cares for the well being of the people of this city. He, he, he.",
                    "Não sou um médico tão extraordinário, mas sou um médico gentil que se preocupa com o bem-estar das pessoas desta cidade. He, he, he.");
            return false;
        }

        private void HushClinicFirstAnswer(NPCController hush, bool agrees)
        {
            StoryTalk(hush,
                agrees ? "He, he, he. Don't worry, you can tell me how you feel." : "He, he, he. I guess you're right.",
                agrees ? "He, he, he. Não se preocupe, pode dizer o que sente." : "He, he, he. Acho que você tem razão.",
                () => AdvanceTownStory("hush", HushClinicDonation));
        }

        private void ConfirmHushClinicDonation(NPCController hush)
        {
            TownTalkSequence(hush, () => StoryAsk(hush, "But Daddy Tonka the carpenter says it takes 4,000 G to build.",
                "Mas o papai Tonka, o carpinteiro, diz que são necessários 4.000 G para construir.",
                (T("Money is not a factor.", "O dinheiro não é um problema."), () => HushDonationLegalQuestion(hush)),
                (T("I haven't enough money.", "Não tenho dinheiro suficiente."), () => StoryTalk(hush,
                    "...that's what I thought. You surprised me for a moment there.",
                    "...era o que eu imaginava. Você me surpreendeu por um momento."))),
                new TownStoryLine("He, he, he. I'm just kidding. Well partly...", "He, he, he. Estou brincando. Bem, em parte..."),
                new TownStoryLine("So that means... What!? You're making a donation?", "Então isso quer dizer... O quê!? Você vai fazer uma doação?"));
        }

        private void HushDonationLegalQuestion(NPCController hush)
        {
            StoryAsk(hush, "Wow! Is the money legal!?", "Uau! Esse dinheiro é legal!?",
                (T("How could you say that!", "Como você pode dizer isso!?"), () => ConfirmHushDonationOrigin(hush)),
                (T("I'm a tamer, remember?", "Eu sou um domador, lembra?"), () => ConfirmHushDonationOrigin(hush)));
        }

        private void ConfirmHushDonationOrigin(NPCController hush)
        {
            TownTalkSequence(hush, () =>
            {
                UnlockConstructionPlan(HushClinicTalkFlag);
                AdvanceTownStory("hush", HushClinicPlanReady);
            },
                new TownStoryLine("I know, I know. You earned it at the tower, right?", "Eu sei, eu sei. Você ganhou isso na torre, certo?"),
                new TownStoryLine("You were a rowdy kid that loved to tease people, but I knew that someday you would become a great man.",
                    "Você era um garoto travesso que adorava provocar os outros, mas eu sabia que um dia se tornaria um grande homem."),
                new TownStoryLine("He, he, he.", "He, he, he."));
        }

        private bool CherrlStory(NPCController npc)
        {
            const string route = "cherrl";
            int stage = StoryStage(route);
            bool doctor = npc.npcId == "hush";
            bool cherrl = npc.npcId == "cherrl";
            if (doctor && !StoryBuilding("hospital")) return false;

            // Preserve a save that was already partway through the earlier,
            // abbreviated route: the old stage 4 was reached on herb delivery.
            if (!TownSaveVariables.GetBool(CherrlRouteVersionKey))
            {
                if (TownSaveVariables.GetBool(CherrlHealingQuestCompleted) && stage <= CherrlWalkRequest)
                {
                    AdvanceTownStory(route, CherrlHerbDelivered);
                    stage = CherrlHerbDelivered;
                }
                TownSaveVariables.SetBool(CherrlRouteVersionKey, true);
                PersistTownProgress();
            }

            if (doctor && TownSaveVariables.GetBool("town.hush_bought_herb") &&
                !TownSaveVariables.GetBool(HushHerbFollowUpKey))
                return StoryTalk(npc, "Keep a medicinal herb with you in the Tower. Use it before your injuries get too serious.",
                    "Leve uma erva medicinal para a Torre. Use-a antes que os ferimentos fiquem graves.", () =>
                    {
                        TownSaveVariables.SetBool(HushHerbFollowUpKey, true);
                        PersistTownProgress();
                    });

            if (stage == CherrlHerbAvailable && !TownSaveVariables.GetBool(CherrlWalkCompletedKey) &&
                !TownSaveVariables.GetBool(CherrlHealingQuestCompleted))
            { AdvanceTownStory(route,CherrlWalkRequest); stage=CherrlWalkRequest; }

            if (doctor && stage == CherrlWalkRequest && StoryRoom == CherrlHospital && TownSaveVariables.GetBool(CherrlWalkCompletedKey))
                return CherrlWalkAssessment(npc);

            if (doctor && StoryRoom == CherrlHospital &&
                stage == CherrlHospitalAbsence && StoryNewDay(route))
                return CherrlHospitalAbsenceVisit(npc);

            if (stage == CherrlNurseDebut && StoryRoom == CherrlHospital)
            {
                var nurse = cherrl ? npc : FindStoryActor("cherrl");
                var hush = doctor ? npc : FindStoryActor("hush");
                if (nurse && hush) return CherrlNurseDebutConversation(nurse, hush);
            }

            if (doctor && stage == CherrlHerbAvailable && StoryRoom == CherrlHospital)
            {
                int herb = FindCherrlHealingHerb();
                if (herb >= 0)
                    return TownTalkSequence(npc, () => StoryAsk(npc,
                        "I'll make sure to tell Cherrl to thank you.", "Vou fazer questão de dizer à Cherrl para agradecer você.",
                        (T("Don't tell her anything.", "Não conte nada a ela."), () => DeliverCherrlHerbWithReply(npc, herb, true)),
                        (T("Yes. Go ahead!", "Sim. Pode contar!"), () => DeliverCherrlHerbWithReply(npc, herb, false))),
                        new TownStoryLine("As long as we've got this, Cherrl will be cured.",
                            "Enquanto tivermos isto, Cherrl será curada."),
                        new TownStoryLine("I'll keep her in the hospital for a little while for full recovery.",
                            "Vou mantê-la no hospital por um tempo, para que se recupere totalmente."),
                        new TownStoryLine("{name}, you're actually a pretty tough guy, aren't you?", "{name}, você é mesmo um cara durão, não é?"),
                        new TownStoryLine("I'm sure Cherrl will be more than thankful. You put yourself in danger to get the Healing Herb and bring it here.",
                            "Tenho certeza de que a Cherrl ficará muito agradecida. Você se colocou em perigo para encontrar a Erva Curativa e trazê-la aqui."),
                        new TownStoryLine("Any girl would fall for you if you showed such courage to save her.",
                            "Qualquer garota se apaixonaria por você ao ver tanta coragem para salvá-la."));
                return TownTalkSequence(npc, null,
                    new TownStoryLine("Oh, that girl... Her immune system has been weak since birth.",
                        "Ah, aquela garota... O sistema imunológico dela é fraco desde que nasceu."),
                    new TownStoryLine("I've done everything I could, and as a result, her condition has stabilized.",
                        "Fiz tudo que pude e, como resultado, a condição dela se estabilizou."),
                    new TownStoryLine("However, she won't be able to leave her bed for the rest of her life.",
                        "Porém, ela não conseguirá sair da cama pelo resto da vida."),
                    new TownStoryLine("If we only could get our hands on the Healing Herb, we could completely heal Cherrl.",
                        "Se ao menos conseguíssemos a Erva Curativa, poderíamos curar a Cherrl completamente."));
            }

            if (!cherrl) return false;
            if (stage == CherrlFirstMeeting && StoryRoom.Length == 0)
                return CherrlFirstMeetingAtWindow(npc);
            if (stage == CherrlFirstMeeting && StoryRoom == CherrlHouse)
            {
                CherrlFirstMeetingInside(npc);
                return true;
            }
            if (stage == CherrlFriendship && StoryRoom == CherrlHouse && StoryNewDay(route))
                return CherrlFriendshipQuestion(npc);
            if (stage == CherrlAlone && StoryRoom == CherrlHouse && StoryNewDay(route))
                return CherrlAloneConversation(npc);
            if (stage == CherrlMotherRequest && StoryRoom == CherrlHouse && StoryNewDay(route))
                return CherrlMotherVisit(npc);
            if (stage == CherrlWalkRequest && StoryRoom == CherrlHouse && StoryBuilding("hospital"))
                return CherrlWalkInvitation(npc);
            if (stage == CherrlHerbAvailable && StoryRoom == CherrlHouse &&
                TownSaveVariables.GetBool(CherrlWalkCompletedKey) && !TownSaveVariables.GetBool(CherrlWalkApologyKey))
                return CherrlWalkApology(npc);
            if (stage == CherrlHerbAvailable && StoryRoom == CherrlHouse)
                return CherrlTalkSequence(npc, () => CherrlRestConversation(npc, null),
                    new CherrlStoryLine(0, "Thank you.", "Obrigada."),
                    new CherrlStoryLine(0, "You're always here to see me.\nOf the whole day, I'm happiest when I see you, {name}.",
                        "Você sempre vem me ver.\nÉ o momento mais feliz do meu dia, {name}."),
                    new CherrlStoryLine(0, "I also feel better when you're here.", "Também me sinto melhor quando você está aqui."));
            if (stage == CherrlHerbDelivered && StoryRoom == CherrlHospital && StoryNewDay(route))
                return CherrlTalkSequence(npc, () => CherrlAsk(npc, 0,
                    "If you have time, come again.", "Se tiver tempo, venha de novo.",
                    (T("I'll come again.", "Eu volto."), () => CherrlTalk(npc, 0, "Good.", "Que bom.", () => AdvanceTownStory(route, CherrlRecovery))),
                    (T("I can't promise that.", "Não posso prometer."), () => CherrlTalk(npc, 0, "Oh...", "Ah...", () => AdvanceTownStory(route, CherrlRecovery)))),
                    new CherrlStoryLine(0, "{name}! Did you come to see me?", "{name}! Você veio me ver?"),
                    new CherrlStoryLine(0, "Listen, the doctor says my illness can be cured.", "Escute, o médico disse que minha doença tem cura."),
                    new CherrlStoryLine(0, "If I stay here in the hospital for a while, and practice daily to move my body, I can soon run around again, just like you {name}.",
                        "Se eu ficar aqui um tempo e fizer exercícios todo dia, logo vou poder correr de novo, como você, {name}."),
                    new CherrlStoryLine(0, "I'll do my best.", "Vou me esforçar."));
            if (stage == CherrlRecovery && StoryRoom == CherrlHospital && StoryNewDay(route))
                return CherrlRecoveryConversation(npc);
            if (stage == CherrlHospitalFollowUp && StoryRoom == CherrlHospital && StoryNewDay(route))
                return CherrlTalkSequence(npc, () => AdvanceTownStory(route, CherrlNurseChoice),
                    new CherrlStoryLine(0, "{name}!\nDid you come to see me?\nI'm so glad!", "{name}!\nVocê veio me ver?\nQue bom!"),
                    new CherrlStoryLine(0, "I'll do my best.\nSo come again.", "Vou me esforçar.\nEntão volte para me ver."));
            if (stage == CherrlNurseChoice && StoryRoom == CherrlHouse && StoryNewDay(route))
                return CherrlNurseChoiceConversation(npc);
            if (stage == CherrlFinalTalk && StoryRoom == CherrlHospital)
                return CherrlFinalConversation(npc);
            if (stage == CherrlHomeRecovery && StoryRoom == CherrlHouse && StoryNewDay(route))
                return CherrlHomeDreamConversation(npc);
            if (stage == CherrlHomeMascot && StoryRoom == CherrlHouse && StoryNewDay(route))
                return CherrlMascotConversation(npc);
            if (stage == StoryComplete && StoryRoom == CherrlHouse)
                return CherrlCompletedHomeConversation(npc);
            if (stage == StoryComplete && StoryRoom == CherrlHospital)
                return CherrlNurseSmallTalk(npc);
            if (stage == CherrlWalkRequest && StoryRoom.Length == 0 && _cherrlWalkActive)
                return CherrlOutdoorWalk(npc);
            if (StoryRoom == CherrlHouse && stage < CherrlHerbDelivered)
                return CherrlRestConversation(npc, null);
            if (StoryRoom == CherrlHouse)
                return CherrlTalk(npc, 0, "I also feel better when you're here.", "Também me sinto melhor quando você está aqui.");
            if (StoryRoom == CherrlHospital && stage is >= CherrlHerbDelivered and <= CherrlHospitalFollowUp)
                return CherrlTalk(npc, 0, "I'll do my best.\nSo come again.", "Vou me esforçar.\nEntão volte para me ver.");
            return false;
        }

        private bool CherrlFirstMeetingAtWindow(NPCController cherrl)
        {
            if (BeginCherrlWindowMeeting(cherrl)) return true;
            // TOWN.BIN 0x2DB025–0x2DB45E.
            void InviteForTea() => CherrlAsk(cherrl, 0, "I would like you to have a tea in return for getting my Chappy back.",
                "Queria oferecer um chá por ter trazido o meu Chappy de volta.",
                (T("I guess I could come in.", "Acho que posso entrar."), () =>
                {
                    TownSaveVariables.SetBool(CherrlWindowMeetingKey, true);
                    TownSaveVariables.SetBool(CherrlInvitedInsideKey, true);
                    PersistTownProgress();
                    StartCoroutine(LoadInterior(CherrlHouse));
                }),
                (T("I'm in hurry... Later.", "Estou com pressa... Fica para depois."), () => CherrlTalk(cherrl, 0,
                    "Oh, is that right.\nCome again when it's a good time for you.", "Ah, entendi.\nVolte quando tiver tempo.", () =>
                    {
                        TownSaveVariables.SetBool(CherrlWindowMeetingKey, true);
                        PersistTownProgress();
                    })));
            if (TownSaveVariables.GetBool(CherrlWindowMeetingKey))
                return CherrlTalk(cherrl, 0, "Oh, it's you!", "Ah, é você!", InviteForTea);
            return CherrlTalkSequence(cherrl, InviteForTea,
                new CherrlStoryLine(0, "Oh, Chappy!\nYou picked him up for me, didn't you?", "Ah, Chappy!\nFoi você que pegou ele para mim, né?"),
                new CherrlStoryLine(0, "Someone was peeking through the window. I was so surprised that I... my Chappy... Oh, my stuffed toy.\nI threw him up.",
                    "Alguém estava espiando pela janela. Levei um susto tão grande que eu... O meu Chappy... Ah, o meu bichinho de pelúcia.\nAcabei jogando ele."),
                new CherrlStoryLine(0, "I've been too scared to look outside since then.\nBut, I'm so glad.... he's back.",
                    "Fiquei com medo demais de olhar lá para fora desde então.\nMas que bom... ele voltou."),
                new CherrlStoryLine(0, "Hey, I'll get my mom to open the door for you, would you come in?",
                    "Ei, vou pedir para minha mãe abrir a porta. Quer entrar?"));
        }

        private void CherrlFirstMeetingInside(NPCController cherrl)
        {
            // TOWN.BIN 0x65EA09–0x65EDB0, all pages select 0.
            CherrlAsk(cherrl, 0, "Wow!\n... Um...\nUm, what's your name?", "Nossa!\n... Hum...\nComo você se chama?",
                (T($"I'm {HeroName}.", $"Sou {HeroName}."), () => CherrlNameReply(cherrl)),
                (T("I don't want to tell you.", "Não quero dizer."), () => CherrlTalk(cherrl, 0,
                    "Why?\nNo, no. I want to know!\nPlease...", "Por quê?\nNão, eu quero saber!\nPor favor...",
                    () => CherrlNameReply(cherrl))));
        }

        private void CherrlNameReply(NPCController cherrl)
        {
            void NadiaThanks() => NadiaAsk(cherrl, "Welcome.\nI don't know how to thank you.\nI hear you found the doll.",
                "Bem-vindo.\nNem sei como agradecer.\nSoube que você encontrou o bichinho.", (T("Continue", "Continuar"), () =>
                    NadiaAsk(cherrl, "It's been years since she has been able to speak to someone around the same age as herself.\nShe has been weak ever since birth.",
                        "Faz anos que ela não consegue conversar com alguém da idade dela.\nEla tem a saúde frágil desde que nasceu.",
                        (T("Continue", "Continuar"), () => AdvanceTownStory("cherrl", CherrlFriendship)))));
            void ThankKoh() => CherrlTalkSequence(cherrl, NadiaThanks,
                new CherrlStoryLine(0, "Oh, I haven't thanked you yet!\nThank you very much.\nI mean it, really.",
                    "Ah, ainda nem agradeci!\nMuito obrigada.\nDe verdade."),
                new CherrlStoryLine(2, "Phew...\nI was so happy, I think I've exhausted myself.", "Ufa...\nFiquei tão feliz que acho que cansei."));

            CherrlTalkSequence(cherrl, () => CherrlAsk(cherrl, 0, "This doll is Chuppie, and I'm Cherrl.",
                    "O nome deste bichinho é Chuppie, e eu sou a Cherrl.",
                    (T("That's a cute name.", "Que nome fofo."), () => CherrlTalk(cherrl, 0,
                        "I like to be associated with the word cute.\nI like it more than beautiful, because cute has more warmth to it.",
                        "Gosto quando dizem que sou fofa.\nAté mais do que bonita. Parece mais carinhoso.", ThankKoh)),
                    (T("That's a beautiful name.", "Que nome bonito."), () => CherrlTalk(cherrl, 0, "Really?", "Sério?", ThankKoh))),
                new CherrlStoryLine(0, "{name}.... It's {name}, right?\nThis is {name}, Chuppie.",
                    "{name}... É {name}, né?\nEste é o {name}, Chuppie."));
        }

        private bool CherrlFriendshipQuestion(NPCController cherrl)
        {
            // TOWN.BIN 0x65F643–0x65FB71.
            if (TownSaveVariables.GetBool(CherrlFriendshipAskedKey))
                return CherrlFriendshipReply(cherrl, "{name}, you've come to see me.\nDo you want to be my friend?",
                    "{name}, você veio me ver.\nQuer ser meu amigo?");

            void AskAgain() => CherrlFriendshipReply(cherrl, "I'm sorry, I'll ask again.\nPlease be my friend.",
                "Desculpe, vou perguntar de novo.\nPor favor, seja meu amigo.");
            void FriendshipIsFree() => CherrlTalk(cherrl, 0, "Oh!  You're right.\nFriendship is not something you buy with things.",
                "Ah! Você tem razão.\nAmizade não se compra com presentes.", AskAgain);
            return CherrlTalkSequence(cherrl, () => CherrlAsk(cherrl, 0,
                    "I'm probably quite boring to you, but I'll give you anything you want in this room.",
                    "Provavelmente sou meio chata para você, mas posso dar qualquer coisa que quiser neste quarto.",
                    (T("I'm already your friend.", "Já sou seu amigo."), FriendshipIsFree),
                    (T("Did I give you anything?", "Eu dei alguma coisa para você?"), FriendshipIsFree),
                    (T("I want you.", "Eu quero você."), () => CherrlTalk(cherrl, 0, "What?", "O quê?", AskAgain))),
                new CherrlStoryLine(0, "Oh good, {name}, you've come.\nI have a favor to ask.\nI want you to be my friend.",
                    "Ah, que bom que veio, {name}.\nQuero pedir um favor.\nQuero que seja meu amigo."));
        }

        private bool CherrlFriendshipReply(NPCController cherrl, string english, string portuguese) =>
            CherrlAsk(cherrl, 0, english, portuguese,
                (T("I want to be your friend too.", "Também quero ser seu amigo."), () => CherrlTalk(cherrl, 0,
                    "Oh great!  Shake hands?\nGiggle.\nShake, shake.", "Que ótimo! Vamos apertar as mãos?\nHe, he.\nAperta, aperta.",
                    () => AdvanceTownStory("cherrl", CherrlAlone))),
                (T("I don't like you.", "Não gosto de você."), () => CherrlTalkSequence(cherrl, () =>
                    {
                        TownSaveVariables.SetBool(CherrlFriendshipAskedKey, true);
                        PersistTownProgress();
                    },
                    new CherrlStoryLine(0, "I guess you're right.\nWhat fun could it be to be friends with me, anyway?",
                        "Acho que você tem razão.\nQue graça teria ser meu amigo, afinal?"),
                    new CherrlStoryLine(0, "It's okay... forget what I said.", "Tudo bem... esqueça o que eu disse."))));

        private bool CherrlAloneConversation(NPCController cherrl)
        {
            void Rest() => CherrlRestConversation(cherrl, () => AdvanceTownStory("cherrl", CherrlMotherRequest));
            return CherrlTalk(cherrl, 0, "{name}, you're always on the go.\nI envy you.  I'm always getting tired.",
                "{name}, você não para nunca.\nQue inveja. Eu vivo cansada.", () =>
                    CherrlAsk(cherrl, 0, "I'll probably be like this for the rest of my life.",
                        "Provavelmente vou ficar assim a vida toda.",
                        (T("Don't think in that way.", "Não pense assim."), () => CherrlTalk(cherrl, 0,
                            "No, don't dislike me.\nI'll think about getting better.", "Não, não fique com raiva de mim.\nVou pensar em melhorar.", Rest)),
                        (T("You'll be fine Cherrl.", "Você vai ficar bem, Cherrl."), () => CherrlTalk(cherrl, 0,
                            "I hope so.", "Espero que sim.", Rest))));
        }

        private bool CherrlRestConversation(NPCController cherrl, Action next)
        {
            void SayGoodbye() => CherrlTalk(cherrl, 0, "Okay.\nSee you again, {name}.", "Está bem.\nAté a próxima, {name}.", next);
            return CherrlAsk(cherrl, 2, "Phew...\nI think I'm getting tired.", "Ufa...\nAcho que estou ficando cansada.",
                (T("Get some sleep.", "Descanse um pouco."), SayGoodbye),
                (T("All right, see you later.", "Tudo bem, até mais."), SayGoodbye));
        }

        private bool NadiaAsk(NPCController cherrl, string english, string portuguese,
            params (string Label, Action Apply)[] choices)
        {
            StoryAsk(FindStoryActor("nadia") ?? cherrl, english, portuguese, choices);
            _dialogueSpeaker = "Nadia";
            _dialoguePortrait = null;
            return true;
        }

        private bool NadiaStory(NPCController nadia)
        {
            var cherrl = FindStoryActor("cherrl");
            if (cherrl && StoryStage("cherrl") == CherrlMotherRequest && StoryNewDay("cherrl"))
                return CherrlMotherVisit(cherrl);
            // Native mother actor/speaker 0x1A (F_act_nerdia = 88).
            if (TownSaveVariables.GetBool(CherrlWalkCompletedKey) && StoryStage("cherrl") == CherrlHerbAvailable)
                return NadiaAsk(nadia, "It is not unusual for Cherrl to faint.\nDo not feel guilty about it.",
                    "A Cherrl desmaia às vezes.\nNão se sinta culpado.", (T("Continue", "Continuar"), null));
            return NadiaAsk(nadia, "If time permits,\nplease, make yourself at home.",
                "Se tiver tempo,\nfique à vontade.", (T("Continue", "Continuar"), null));
        }

        private bool CherrlMotherVisit(NPCController cherrl)
        {
            // TOWN.BIN 0x65FFFF–0x66048C. The mother is actor 0x1A, Nadia.
            void ContinueVisit() => NadiaAsk(cherrl, "Thank you very much.", "Muito obrigada.",
                (T("Continue", "Continuar"), () => CherrlMotherReflection(cherrl)));
            void AskKoh() => NadiaAsk(cherrl, "{name}, can I ask you to take care of her while I'm gone?",
                "{name}, pode cuidar dela enquanto eu estiver fora?",
                (T("Sure, I don't mind.", "Claro, sem problema."), ContinueVisit),
                (T("No way. She's too selfish.", "De jeito nenhum. Ela é egoísta demais."), () => CherrlTalk(cherrl, 0,
                    "I won't act selfish!", "Não vou ser egoísta!", () => NadiaAsk(cherrl,
                        "{name}, can I ask you to take care of her while I'm gone?", "{name}, pode cuidar dela enquanto eu estiver fora?",
                        (T("Continue", "Continuar"), ContinueVisit)))));
            void Explain() => NadiaAsk(cherrl,
                "Oh, {name}.  I'm sorry.\nWhenever I have to go out, she will sometimes act in this manner.\n{name} is here to see you.  I have to get going now.",
                "Ah, {name}, desculpe.\nÀs vezes ela fica assim quando preciso sair.\nO {name} veio ver você. Preciso ir agora.",
                (T("Continue", "Continuar"), AskKoh));
            void AskAboutClothes() => CherrlTalk(cherrl, 0, "Then,\nwhat if I want to get changed?", "E se eu quiser trocar de roupa?", () =>
                NadiaAsk(cherrl, "You can get changed yourself.", "Você consegue se trocar sozinha.", (T("Continue", "Continuar"), Explain)));
            return CherrlTalk(cherrl, 0, "Then,  who's going to give me water to drink?", "Então, quem vai me dar água?", () =>
                NadiaAsk(cherrl, "It's right here when you need it.", "Está bem aqui para quando precisar.",
                    (T("Continue", "Continuar"), AskAboutClothes)));
        }

        private void CherrlMotherReflection(NPCController cherrl)
        {
            void Rest() => CherrlRestConversation(cherrl, () => AdvanceTownStory("cherrl", CherrlWalkRequest));
            CherrlTalkSequence(cherrl, () => CherrlAsk(cherrl, 0,
                "I'm all alone.\nUnless I take a selfish attitude toward my mother, sometimes I'm not even certain whether or not I'm alive.",
                "Estou completamente sozinha.\nSe eu não agir de forma egoísta com a minha mãe, às vezes nem sei se estou viva.",
                (T("I came through this window.", "Eu entrei por esta janela."), () => CherrlTalkSequence(cherrl, Rest,
                    new CherrlStoryLine(0, "Hey, that's right.\nBy throwing Chuppie out the window, I was able to meet you, {name}.",
                        "Ei, é verdade.\nFoi porque joguei o Chuppie pela janela que conheci você, {name}."),
                    new CherrlStoryLine(0, "By taking action myself to do something about the outside world, I accomplished something!",
                        "Quando fiz alguma coisa por conta própria pelo mundo lá fora, consegui mudar alguma coisa!"),
                    new CherrlStoryLine(0, "It's not that I could only look at the outside world, but rather I would only look at the outside world, that's what you mean, right {name}?",
                        "Eu podia fazer mais do que só olhar para o mundo lá fora. Era eu que ficava só olhando. É isso, {name}?"))),
                (T("Still, no good to be selfish.", "Ainda assim, ser egoísta não ajuda."), () => CherrlTalk(cherrl, 0,
                    "I know that!\nI know that, but... I can't help it.", "Eu sei!\nEu sei, mas... não consigo evitar.", Rest))),
                new CherrlStoryLine(0, "I know myself.\nBut I'm here sleeping in bed all day.\nAll I have is this rectangular window.",
                    "Eu me conheço.\nMas fico aqui deitada o dia todo.\nSó tenho essa janela retangular."),
                new CherrlStoryLine(0, "Outside this window I can see the big blue sky and all those people walking around.",
                    "Por esta janela consigo ver o grande céu azul e todas aquelas pessoas caminhando."),
                new CherrlStoryLine(0, "All I can do is...\nwatch the scenery that appears on this window.",
                    "Tudo que consigo fazer é observar a paisagem que aparece nesta janela."));
        }

        private bool CherrlWalkInvitation(NPCController cherrl) =>
            CherrlTalk(cherrl, 0, "{name}, I have a favor to ask.\nI... I want to hold hands with you and walk outside.",
                "{name}, queria pedir um favor.\nEu... queria dar a mão para você e caminhar lá fora.", () =>
                    CherrlAsk(cherrl, 0, "Just for a little bit out in the front.\nI'll be sure to tell you as soon as I get tired.",
                        "Só um pouquinho aqui na frente.\nPrometo avisar assim que me cansar.",
                        (T("Okay. Deal.", "Está bem. Combinado."), () => CherrlWalkToHospital(cherrl)),
                        (T("No, I can't allow that.", "Não posso deixar."), () => CherrlTalk(cherrl, 0,
                            "Yeah, I guess you're right.\nJust a fragment of my imagination.\n... don't worry about it.",
                            "É, acho que tem razão.\nFoi só coisa da minha imaginação.\n... não se preocupe."))));

        private void DeliverCherrlHerbWithReply(NPCController hush, int herb, bool anonymous)
        {
            if (!DeliverCherrlHerb(herb, anonymous)) return;
            if (anonymous)
            {
                TownTalkSequence(hush, null,
                    new TownStoryLine("Why?... well, all right.", "Por quê?... bem, tudo bem."),
                    new TownStoryLine("It might concern her to know someone went through much risk to help her.",
                        "Pode preocupá-la saber que alguém correu tantos riscos para ajudá-la."),
                    new TownStoryLine("Furthermore, if she feels her illness was healed with the help of others rather than from within, she may continue to feel like a patient.",
                        "Além disso, se sentir que a doença foi curada pela ajuda de outros e não por sua própria força, ela pode continuar a se sentir uma paciente."),
                    new TownStoryLine("Okay, leave it to me. I'll admit her to the hospital right away.",
                        "Certo, deixe comigo. Vou interná-la no hospital agora mesmo."));
            }
            else
            {
                TownTalkSequence(hush, null,
                    new TownStoryLine("Well, you saved her life. {name} has truly matured.",
                        "Bem, você salvou a vida dela. {name} realmente amadureceu."),
                    new TownStoryLine("Okay, leave it to me. I'll admit her to the hospital right away.",
                        "Certo, deixe comigo. Vou interná-la no hospital agora mesmo."));
            }
        }

        private bool CherrlRecoveryConversation(NPCController cherrl)
        {
            return CherrlTalkSequence(cherrl, () => CherrlAsk(cherrl, 0,
                "Why,\nwhy did you go to get the Healing Herb, anyway?", "Por quê?\nPor que foi buscar a Erva Curativa?",
                (T("Because I wanted to go.", "Porque eu quis ir."), () => CherrlRecoveryAfterReason(cherrl, false)),
                (T("I did it all for you, Cherrl.", "Eu fiz tudo por você, Cherrl."), () => CherrlRecoveryAfterReason(cherrl, true))),
                new CherrlStoryLine(0, "{name}...  I'm so sorry.\nYou went through a lot of danger for me, didn't you?",
                    "{name}... Sinto muito.\nVocê correu muito perigo por minha causa, não correu?"),
                new CherrlStoryLine(0, "I've always been like that.\nAlways causing other people trouble.",
                    "Eu sempre fui assim. Sempre causando problemas para os outros."),
                new CherrlStoryLine(0, "I shouldn't be allowed to get better.\nImagine for a moment, {name}, if you had been killed at the tower.",
                    "Eu nem mereço melhorar.\nImagine por um momento, {name}, se você tivesse morrido na torre."));
        }

        private void CherrlRecoveryAfterReason(NPCController cherrl, bool forCherrl)
        {
            if (!forCherrl)
            {
                CherrlTalkSequence(cherrl, () => CherrlAskForAnotherVisit(cherrl),
                    new CherrlStoryLine(0, "I guess you're right.\nYou are a monster trainer after all.",
                        "Acho que tem razão.\nAfinal, você é um domador de monstros."),
                    new CherrlStoryLine(0, "Ha, ha.  And I'm here thinking you went through some kind of trouble for me.",
                        "Ha, ha. E eu aqui pensando que você tinha se metido em apuros por minha causa."),
                    new CherrlStoryLine(0, "I don't know what got into me.", "Não sei o que deu em mim."));
                return;
            }
            CherrlTalk(cherrl, 1, "Never do anything dangerous for me!\nDon't ever bother me again.",
                "Nunca faça nada perigoso por minha causa!\nNunca mais faça isso por mim.", () => CherrlAsk(cherrl, 0,
                    "I don't want you to suffer pain for me, {name}.", "Não quero que sofra por minha causa, {name}.",
                    (T("I wanted to see you well.", "Eu queria ver você bem."), () => FinishCherrlRecovery(cherrl)),
                    (T("Scolded by you is more pain.", "Ouvir sua bronca dói mais."), () => FinishCherrlRecovery(cherrl))));
        }

        private void FinishCherrlRecovery(NPCController cherrl)
        {
            CherrlTalk(cherrl, 0, "I'm sorry.\nI just couldn't forgive myself knowing that I put you through so much danger, {name}...",
                "Desculpe.\nEu não conseguia me perdoar por ter feito você correr tanto perigo, {name}...", () => CherrlAskForAnotherVisit(cherrl));
        }

        private void CherrlAskForAnotherVisit(NPCController cherrl)
        {
            void Finish() => AdvanceTownStory("cherrl", CherrlHospitalFollowUp);
            CherrlAsk(cherrl, 0, "I'll do my best.\nSo come again if you have time.", "Vou me esforçar.\nVolte se tiver tempo.",
                (T("I'll come again.", "Eu volto."), () => CherrlTalk(cherrl, 0, "Good.", "Que bom.", Finish)),
                (T("I can't promise that.", "Não posso prometer."), () => CherrlTalk(cherrl, 0, "Oh...", "Ah...", Finish)));
        }

        private bool CherrlFinalConversation(NPCController cherrl)
        {
            // TOWN.BIN 0x5A2F12–0x5A3317. Hush, actor 0x19, explains the secret.
            if (!TownSaveVariables.GetBool(CherrlAnonymousHerbKey)) return CherrlThanksForHerb(cherrl);
            void ExplainWhy()
            {
                var hush = FindStoryActor("hush");
                if (!hush)
                {
                    CherrlThanksForHerb(cherrl);
                    return;
                }
                TownTalkSequence(hush, () => CherrlThanksForHerb(cherrl),
                    new TownStoryLine("He insisted on keeping it a secret, so that you would make an effort to fully recover on your own.",
                        "Ele insistiu em manter segredo para que você se esforçasse para se recuperar por conta própria."),
                    new TownStoryLine("It wouldn't help you if your recovery was solely due to other people.",
                        "Não ajudaria se a sua recuperação dependesse só dos outros."));
            }
            return CherrlTalkSequence(cherrl, ExplainWhy,
                new CherrlStoryLine(7, "Yup...\njust as I thought.", "É...\ncomo eu imaginava."),
                new CherrlStoryLine(7, "I figured that the reason I was able to recover so quickly was because of Healing Herb.",
                    "Imaginei que tinha me recuperado tão rápido por causa da Erva Curativa."),
                new CherrlStoryLine(7, "And that you, {name}, were the one who brought them here...",
                    "E que você, {name}, tinha trazido a erva..."),
                new CherrlStoryLine(7, "Why didn't you tell me?", "Por que não me contou?"));
        }

        private bool CherrlThanksForHerb(NPCController cherrl)
        {
            void ThankKoh() => CherrlThankKoh(cherrl);
            return CherrlTalkSequence(cherrl, () => CherrlAsk(cherrl, 7,
                    "There's no way I'm going to put that effort to waste.", "De jeito nenhum vou deixar que esse esforço seja em vão.",
                    (T("Sorry.", "Desculpe."), ThankKoh),
                    (T("Sorry.", "Desculpe."), ThankKoh),
                    (T("Curry.", "Curry."), () => CherrlTalk(cherrl, 7, "?????", "?????", ThankKoh))),
                new CherrlStoryLine(7, "Of course I'm going to make an effort myself.\nWhy shouldn't I.",
                    "É claro que vou me esforçar.\nPor que eu não iria?"),
                new CherrlStoryLine(7, "{name}, a very important person to me, risked his life to get the Healing Herb for me.",
                    "{name}, uma pessoa muito importante para mim, arriscou a vida para conseguir a Erva Curativa."));
        }

        private void CherrlThankKoh(NPCController cherrl)
        {
            CherrlTalkSequence(cherrl, () => CherrlSkyDream(cherrl, 8,
                    () => CherrlNurseSmallTalk(cherrl, () => CompleteCherrlStory(true))),
                new CherrlStoryLine(7, "No, {name}.  There's no reason for you to apologize.", "Não, {name}. Não precisa se desculpar."),
                new CherrlStoryLine(7, "Give me a chance to express my appreciation.\nThank you.",
                    "Deixe que eu possa agradecer.\nObrigada."),
                new CherrlStoryLine(7, "Not only for bringing the Healing Herb for me, but for everything, ever since we first met.",
                    "Por trazer a Erva Curativa e por tudo mais desde o dia em que nos conhecemos."),
                new CherrlStoryLine(7, "I'm glad it was you, {name}, that brought Chuppy to me.",
                    "Fico feliz que tenha sido você, {name}, quem trouxe o Chuppy de volta."),
                new CherrlStoryLine(7, "giggle... so there you have it.", "He, he... era isso que eu queria dizer."),
                new CherrlStoryLine(7, "Huh... That's a load off my chest.  I just wanted to tell you how I felt.",
                    "Ufa... tirei um peso do peito. Só queria dizer como me sinto."),
                new CherrlStoryLine(7, "It's the first time ever that I've used up so much courage...\nthanks to you for that too, {name}.\nThank you.",
                    "Foi a primeira vez que precisei de tanta coragem...\nObrigada por isso também, {name}."));
        }

        private bool CherrlNurseSmallTalk(NPCController cherrl, Action next = null)
        {
            void Answer() => CherrlTalkSequence(cherrl, next,
                new CherrlStoryLine(7, "Nothing.\nI just wanted to call out your name.", "Nada.\nSó queria chamar seu nome."),
                new CherrlStoryLine(7, "... giggle...", "... he, he..."));
            return CherrlTalk(cherrl, 7, "Anytime I meet you, {name}, it lifts me up for the whole day.\nStrange, isn't it?",
                "Toda vez que vejo você, {name}, fico animada pelo resto do dia.\nEstranho, né?", () =>
                    CherrlAsk(cherrl, 7, "{name}!", "{name}!",
                        (T("What is it?", "O que foi?"), Answer), (T("Yes?", "Sim?"), Answer)));
        }

        private bool CherrlPoolConversation(NPCController cherrl) =>
            CherrlTalkSequence(cherrl, null, // TOWN.BIN 0x535DF3–0x53601D.
                new CherrlStoryLine(6, "Oh!  {name}.\nIt's like a dream come true to be able to swim in a pool.",
                    "Ah, {name}!\nPoder nadar numa piscina é como realizar um sonho."),
                new CherrlStoryLine(6, "I still can't quite believe it.\nLook, look at me!", "Ainda nem acredito.\nOlhe, olhe para mim!"),
                new CherrlStoryLine(6, "What do you think of this swimsuit?\nDoes it suit me?",
                    "O que acha deste maiô?\nFicou bem em mim?"),
                new CherrlStoryLine(6, "It's the first time in my life I've worn a swimsuit.", "É a primeira vez na vida que uso um maiô."),
                new CherrlStoryLine(6, "I've still a ways to go on my swimming though, right...?",
                    "Ainda tenho muito o que aprender a nadar, né...?"),
                new CherrlStoryLine(6, "Actually...\nDon't look at me... I'm getting embarrassed...",
                    "Na verdade...\nNão olhe para mim... estou ficando com vergonha..."));

        private bool CherrlOutdoorWalk(NPCController cherrl)
        {
            // TOWN.BIN 0x67B05A–0x67B496 explicitly selects 2, then 1.
            return CherrlAsk(cherrl, 2,
                "{name}.\nIf a stranger saw us, I wonder what they'd make of the two of us.",
                "{name}.\nSe um estranho visse a gente, o que será que pensaria de nós dois?",
                (T("Maybe... lovers?", "Talvez... namorados?"), () => CherrlTalkSequence(cherrl, () => CherrlOutdoorGhosh(cherrl),
                    new CherrlStoryLine(2, "......\nI'm blushing, how embarrassing\n......", "......\nEstou corando. Que vergonha...\n......"),
                    new CherrlStoryLine(2, "I'm so happy.", "Estou tão feliz."))),
                (T("Brother and sister?", "Irmão e irmã?"), () => CherrlTalk(cherrl, 2, "Yes, probably.", "É, provavelmente.", () => CherrlOutdoorGhosh(cherrl))));
        }

        private void CherrlWakeUpStory(NPCController cherrl)
        {
            // TOWN.BIN 0x417E98–0x418531. The first line selects the nurse portrait (7).
            void HospitalDuty() => CherrlTalkSequence(cherrl, null,
                new CherrlStoryLine(7, "I told Dr. Hush that I was coming here to see you, {name}.\nSo there's nothing to worry about.",
                    "Avisei o Dr. Hush que vinha ver você, {name}.\nEntão não precisa se preocupar."),
                new CherrlStoryLine(7, "When I said that, Dr. Hush smirked and said, 'take your time'.",
                    "Quando falei isso, o Dr. Hush deu um sorrisinho e disse para eu não ter pressa."),
                new CherrlStoryLine(7, "I'm uneasy about the way he looked at me, so I'm going right back to the hospital.",
                    "O jeito como ele me olhou me deixou sem graça, então vou voltar direto para o hospital."),
                new CherrlStoryLine(7, "See you later, {name}.", "Até mais, {name}."));
            CherrlTalkSequence(cherrl, () => CherrlAsk(cherrl, 7,
                    "The doctor tells me it's a pleasure having me as a nurse.\nI feel great pride in my work.",
                    "O doutor diz que é um prazer me ter como enfermeira.\nTenho muito orgulho do meu trabalho.",
                    (T("You're going to work today?", "Você vai trabalhar hoje?"), () => CherrlTalk(cherrl, 7,
                        "Yes, but...", "Sim, mas...", HospitalDuty)),
                    (T("Did you get a day off today?", "Hoje é seu dia de folga?"), HospitalDuty)),
                new CherrlStoryLine(7, "Hi there, {name}.\nMorning's here, time to get up.", "Oi, {name}.\nJá amanheceu. Hora de levantar."),
                new CherrlStoryLine(7, "There, open your eyes, and sit upright.\nThere, well done.", "Isso, abra os olhos e sente.\nMuito bem."),
                new CherrlStoryLine(7, "Good morning, {name}.\nWow, your hair's standing on end, well, as always I guess.",
                    "Bom dia, {name}.\nNossa, seu cabelo está todo arrepiado... como sempre, acho."),
                new CherrlStoryLine(7, "As you know, this city has many hunters.\nYou never know when someone's going to get injured and run to the hospital.",
                    "Como você sabe, há muitos caçadores nesta cidade.\nNunca se sabe quando alguém vai se machucar e correr para o hospital."),
                new CherrlStoryLine(7, "You've got to be alert 24 hours a day.\nSo there's really no time, day or night, that I can actually rest.",
                    "É preciso ficar de prontidão 24 horas por dia.\nDe dia ou de noite, nunca dá para descansar de verdade."));
        }

        private int FindCherrlHealingHerb()
        {
            for (int i = 0; i < _townInventory.DungeonItems.Count; i++)
                if (TowerItemTrapManager.IsCherrlHealingHerb(_townInventory.DungeonItems[i])) return i;
            return -1;
        }

        private bool DeliverCherrlHerb(int index, bool anonymous)
        {
            if (StoryStage("cherrl") != CherrlHerbAvailable || !TownSaveVariables.GetBool(CherrlWalkCompletedKey) ||
                (uint)index >= (uint)_townInventory.DungeonItems.Count ||
                !TowerItemTrapManager.IsCherrlHealingHerb(_townInventory.DungeonItems[index]) ||
                !_townInventory.ConsumeDungeonItem(index, ItemSystem.Kind.Herb)) return false;
            TownSaveVariables.SetBool(CherrlHealingQuestCompleted, true);
            TownSaveVariables.SetBool(CherrlAnonymousHerbKey, anonymous);
            AdvanceTownStory("cherrl", CherrlHerbDelivered);
            return true;
        }

        private void CompleteCherrlStory(bool isNurse)
        {
            var flags = TownRetailScriptFlags.CreatePersistent();
            if (isNurse) flags.SetBit(CherrlNurseFlag);
            else flags.ClearBit(CherrlNurseFlag);
            flags.SetBit(CherrlLoveFlag);
            AdvanceTownStory("cherrl", StoryComplete);
        }

        private bool CherrlNurseChoiceConversation(NPCController cherrl)
        {
            // TOWN.BIN 0x661940–0x661F0F.
            void Confirm() => CherrlTalk(cherrl, 0, "Then it's decided!  I'm going to become a nurse.",
                "Então está decidido! Vou me tornar enfermeira.", () =>
                {
                    TownRetailScriptFlags.CreatePersistent().SetBit(CherrlNurseFlag);
                    AdvanceTownStory("cherrl", CherrlNurseDebut);
                });
            void Decide() => CherrlAsk(cherrl, 0, "I want to study at the doctor's place to become a nurse.\nWhat do you think, {name}?",
                "Quero estudar no hospital para me tornar enfermeira.\nO que você acha, {name}?",
                (T("That's a great idea.", "Ótima ideia."), () => CherrlAsk(cherrl, 0,
                    "Really?\nDo you really think so?", "Sério?\nVocê acha mesmo?",
                    (T("Yes.", "Sim."), Confirm), (T("Ask yourself that question.", "Faça essa pergunta a você mesma."), Confirm))),
                (T("It doesn't suit you.", "Isso não combina com você."), () => CherrlTalk(cherrl, 0,
                    "Yeah, I guess you're right.\nJust a fragment of my imagination.",
                    "É, acho que tem razão.\nFoi só coisa da minha imaginação.", () => AdvanceTownStory("cherrl", CherrlHomeRecovery))));
            return CherrlTalkSequence(cherrl, Decide,
                new CherrlStoryLine(0, "Hi {name}!\nLook at me!  Look at me!", "Oi, {name}!\nOlhe para mim! Olhe!"),
                new CherrlStoryLine(0, "See, and I feel terrific.\nI can't believe it.\nIt's not a dream, is it?",
                    "Viu? Estou me sentindo ótima.\nNem acredito.\nNão é um sonho, né?"),
                new CherrlStoryLine(0, "But if that was all they did, couldn't it have been done long ago?",
                    "Mas, se era só isso, por que não dava para fazer antes?"),
                new CherrlStoryLine(0, "or... did you have anything to do with it, {name}?\nnaw... it couldn't be... no, I'm just talking to myself.",
                    "Ou... você teve alguma coisa a ver com isso, {name}?\nNão... não pode ser... estou só pensando em voz alta."),
                new CherrlStoryLine(0, "You know,\nI couldn't thank the doctor enough for healing me.", "Sabe,\neu nunca vou conseguir agradecer o suficiente ao médico por me curar."),
                new CherrlStoryLine(0, "And I was so moved.\nI mean, towards the profession of a doctor...\nIf only I could help sick people the way a doctor can.",
                    "E isso me tocou muito.\nQuero dizer, o trabalho de um médico...\nSe eu pudesse ajudar os doentes como ele."));
        }

        private bool CherrlHomeDreamConversation(NPCController cherrl)
        {
            // TOWN.BIN 0x66212B–0x662705, recovery without the nursing route.
            void WishKohLuck() => CherrlTalk(cherrl, 0, "I hope your dream comes true.\nNow it's my turn to cheer you on, {name}.",
                "Espero que seu sonho se realize.\nAgora é minha vez de torcer por você, {name}.",
                () => AdvanceTownStory("cherrl", CherrlHomeMascot));
            return CherrlTalk(cherrl, 0, "Hi {name}!\nLook at me!  Look at me!\nSee, and I feel terrific.",
                "Oi, {name}!\nOlhe para mim! Olhe!\nViu? Estou me sentindo ótima.", () =>
                    CherrlAsk(cherrl, 0, "{name}, what is your dream?", "{name}, qual é o seu sonho?",
                        (T("I wanna be stronger.", "Quero ficar mais forte."), () => CherrlTalkSequence(cherrl, WishKohLuck,
                            new CherrlStoryLine(0, "That's right.\nYou are very strong, {name}.\nEven though you don't look so strong.\nIt's amazing.",
                                "É verdade.\nVocê é muito forte, {name}.\nMesmo sem parecer tão forte.\nÉ incrível."),
                            new CherrlStoryLine(0, "But, even though you don't look strong,\nI can feel your strength.",
                                "Mesmo sem você parecer forte,\neu consigo sentir a sua força."),
                            new CherrlStoryLine(0, "Huh?  Am I making sense?\nWell, I'm saying what I feel...\nanyway, good luck.",
                                "Hã? Isso faz sentido?\nSó estou dizendo o que sinto...\nEnfim, boa sorte."))),
                        (T("Collect all monsters.", "Colecionar todos os monstros."), () => CherrlTalkSequence(cherrl, WishKohLuck,
                            new CherrlStoryLine(0, "Right.\nLet me see them when you get them all.", "Está bem.\nMostre todos quando conseguir."),
                            new CherrlStoryLine(0, "To tell you the truth, {name}, before I met you, I always imagined monster tamers to be very scary people.",
                                "Para dizer a verdade, {name}, antes de conhecer você, eu achava que domadores de monstros eram assustadores."),
                            new CherrlStoryLine(0, "But when I imagine you taming those monsters, {name}, it's kinda cute.",
                                "Mas, quando imagino você domando aqueles monstros, {name}, até que é fofo."))),
                        (T("Super model.", "Ser supermodelo."), () => CherrlTalk(cherrl, 0,
                            "Oh... really...\nI... don't really want you to try hard at that... but...",
                            "Ah... sério...\nEu... preferia que você não se esforçasse tanto para isso... mas...", WishKohLuck))));
        }

        private bool CherrlMascotConversation(NPCController cherrl)
        {
            // TOWN.BIN 0x66285A–0x662D69.
            void TakeMascot() => CherrlTalk(cherrl, 0, "Come again.\nI'm so happy whenever I can see you, {name}.",
                "Volte sempre.\nFico tão feliz quando vejo você, {name}.", () =>
                {
                    TownSaveVariables.SetBool(CherrlMascotReceivedKey, true);
                    TownSaveVariables.SetInt(CherrlHouseConversationKey, 0);
                    PersistTownProgress();
                    CherrlMascotMemories(cherrl, () => CompleteCherrlStory(false));
                });
            if (TownSaveVariables.GetBool(CherrlMascotReceivedKey))
            {
                CherrlMascotMemories(cherrl, () => CompleteCherrlStory(false));
                return true;
            }
            return CherrlTalkSequence(cherrl, () => CherrlAsk(cherrl, 0,
                    "I want you to have this one,\nthe {name} mascot No. 1!", "Quero que fique com este,\no mascote {name} número 1!",
                    (T("Thanks!", "Obrigado!"), TakeMascot),
                    (T("I don't want it.", "Não quero."), () => CherrlTalk(cherrl, 0,
                        "No, that's not acceptable.\nIn order for it to sell, the person himself has got to keep it on him.",
                        "Não, assim não dá.\nPara vender, a própria pessoa precisa carregar um consigo.", TakeMascot))),
                new CherrlStoryLine(0, "Hi!  Welcome!\nLook here.", "Oi! Bem-vindo!\nVeja só."),
                new CherrlStoryLine(0, "You're now a top notch hunter and popular amongst the people of this town, right {name}?",
                    "Agora você é um caçador de primeira e é popular na cidade, né, {name}?"),
                new CherrlStoryLine(0, "I'm sure a doll that looks exactly like you would sell.",
                    "Tenho certeza de que um boneco igualzinho a você faria sucesso."),
                new CherrlStoryLine(0, "This way, it would generate income for your mother, and besides, I enjoy making these.",
                    "Assim sua mãe ganharia algum dinheiro. Além disso, eu gosto de fazer esses bonecos."));
        }

        private void CherrlMascotMemories(NPCController cherrl, Action next) =>
            CherrlTalkSequence(cherrl, () => CherrlSkyDream(cherrl, 0, next),
                new CherrlStoryLine(0, "Hi!  Welcome!\nI was making your mascot doll again, {name}.",
                    "Oi! Bem-vindo!\nEu estava fazendo mais um mascote seu, {name}."),
                new CherrlStoryLine(0, "This window is my store.\nIt's selling pretty well.\n......\nIt's amazing.",
                    "Esta janela é a minha loja.\nEstá vendendo muito bem.\n......\nÉ incrível."));

        private bool CherrlSkyDream(NPCController cherrl, int portrait, Action next)
        {
            // The two routes share this theme (TOWN.BIN 0x5A384F and 0x663147).
            bool atHome = StoryRoom == CherrlHouse;
            return CherrlTalkSequence(cherrl, next,
                new CherrlStoryLine(portrait, atHome
                        ? "Until not too long ago, I'd spend everyday just gazing at the blue sky that reflected on the window."
                        : "Hey, {name}.\nSometimes I think of the irony.\nUntil not too long ago, I would spend every day looking out the window and gazing at the blue sky.",
                    atHome ? "Até pouco tempo atrás, eu passava os dias olhando para o céu azul refletido na janela."
                        : "Ei, {name}.\nÀs vezes penso na ironia disso.\nAté pouco tempo atrás, eu passava os dias olhando pela janela para o céu azul."),
                new CherrlStoryLine(portrait, atHome
                        ? "Right now, I'm living in the world outside this window, and although I'm still on this side, my feelings are out there."
                        : "Right now, I'm in that dream world outside of that window.",
                    atHome ? "Hoje eu vivo no mundo lá fora. Mesmo estando deste lado da janela, meus sentimentos estão lá fora."
                        : "Hoje estou naquele mundo de sonho, do lado de fora da janela."),
                new CherrlStoryLine(portrait, "Yes, life can be so unpredictable.", "É, a vida é tão imprevisível."),
                new CherrlStoryLine(portrait, atHome
                        ? "If I wasn't born so weak, I might have even become a nurse.\nbut then I might not have met you, {name}."
                        : "If I wasn't born so weak, I might have been able to have many other wonderful days in the past.\nBut on the other hand, I might have not met you, {name}.",
                    atHome ? "Se eu não tivesse nascido tão fraca, talvez tivesse virado enfermeira.\nMas talvez nunca tivesse conhecido você, {name}."
                        : "Se eu não tivesse nascido tão fraca, talvez tivesse vivido outros dias maravilhosos.\nMas talvez nunca tivesse conhecido você, {name}."),
                new CherrlStoryLine(portrait, "Even when I was ill, if I hadn't thrown that stuffed doll out the window, I might never have met you.",
                    "Mesmo doente, se eu não tivesse jogado aquele bichinho pela janela, talvez nunca tivesse conhecido você."),
                new CherrlStoryLine(portrait, "Every time you meet someone, every time you make a choice, life itself changes, right?",
                    "A cada pessoa que você conhece, a cada escolha que faz, a vida muda, né?"),
                new CherrlStoryLine(portrait, "I had hated my life so much in the past, but now I've realized that my life is the same color as this sky.",
                    "Antes, eu odiava tanto a minha vida. Hoje percebo que ela tem a mesma cor desse céu."),
                new CherrlStoryLine(portrait, "The color of the sky has always been the color I adored the most.",
                    "A cor do céu sempre foi a minha preferida."),
                new CherrlStoryLine(portrait, "It's the color that symbolizes the world outside the window, and also...",
                    "É a cor do mundo do lado de fora da janela, e também..."),
                new CherrlStoryLine(portrait, "My sky colored dream...\nis to be married to a certain somebody.",
                    "Meu sonho da cor do céu...\né me casar com uma certa pessoa."),
                new CherrlStoryLine(portrait, "But if I do marry that person, he's likely going to make me worry, which might make me sick again.",
                    "Mas, se eu me casar com ele, provavelmente vou me preocupar tanto que posso adoecer de novo."),
                new CherrlStoryLine(portrait, "Even so, I'd probably be the happiest woman around.",
                    "Mesmo assim, eu provavelmente seria a mulher mais feliz daqui."));
        }

        private bool CherrlCompletedHomeConversation(NPCController cherrl)
        {
            // TOWN.BIN 0x6638FD–0x663E47.
            int visit = TownSaveVariables.GetInt(CherrlHouseConversationKey);
            void Next()
            {
                TownSaveVariables.SetInt(CherrlHouseConversationKey, (visit + 1) % 4);
                PersistTownProgress();
            }
            return CherrlTalk(cherrl, 0, "Hi!  Welcome!", "Oi! Bem-vindo!", () =>
            {
                switch (visit)
                {
                    case 0:
                        CherrlAsk(cherrl, 0, "How's the hunting work coming along?", "Como vai o trabalho de caçador?",
                            (T("Swell.", "Muito bem."), () => CherrlTalk(cherrl, 0, "That's great.\nKeep up the good work.", "Que bom.\nContinue assim.", Next)),
                            (T("Not so good.", "Não muito bem."), () => CherrlTalk(cherrl, 0,
                                "Out of all people, {name}, you who encouraged me should not be feeling down yourself!\nGive it your best shot!",
                                "Logo você que me animou, {name}, não devia ficar desanimado!\nDê o seu melhor!", Next)));
                        break;
                    case 1:
                        void Answer() => CherrlTalk(cherrl, 0, "Because I met you, {name}!", "Porque encontrei você, {name}!", Next);
                        CherrlAsk(cherrl, 0, "Today looks to be a wonderful day.", "Parece que hoje vai ser um dia lindo.",
                            (T("You think so?", "Você acha?"), Answer), (T("Why?", "Por quê?"), Answer));
                        break;
                    case 2 when TownSaveVariables.GetBool(CherrlMascotReceivedKey):
                        void Laugh() => CherrlTalk(cherrl, 0, "giggle.", "He, he.", Next);
                        CherrlTalk(cherrl, 0, "{name}.  Your character doll is selling like hotcakes.\nMother's delighted too.",
                            "{name}, seu boneco está vendendo que nem pão quente.\nMinha mãe também está adorando.", () =>
                                CherrlAsk(cherrl, 0, "This also is due to you {name}.\nTo me, you're almost God.",
                                    "Isso também é graças a você, {name}.\nPara mim, você é quase um deus.",
                                    (T("You're exaggerating!", "Você está exagerando!"), Laugh),
                                    (T("Almost? No, I'm the God!", "Quase? Eu sou um deus!"), Laugh)));
                        break;
                    default:
                        CherrlTalkSequence(cherrl, Next,
                            new CherrlStoryLine(0, "I truly believe I am lucky because you come to see me so often!",
                                "Acho mesmo que tenho sorte por você vir me ver tantas vezes!"),
                            new CherrlStoryLine(0, "Whenever you have the time, come and see me.\nBecause, well... I worry.",
                                "Quando tiver tempo, venha me ver.\nÉ que... eu fico preocupada."));
                        break;
                }
            });
        }

        private IEnumerator PlayCherrlHospitalScene()
        {
            // Interior residents are ready one frame after the additive scene load.
            yield return null;
            if (StoryRoom != CherrlHospital) yield break;
            int stage = StoryStage("cherrl");
            var hush = FindStoryActor("hush");
            if (stage == CherrlNurseDebut)
            {
                var cherrl = FindStoryActor("cherrl");
                if (!cherrl || !hush) yield break;
                yield return PlayStoryWalk(cherrl, cherrl.transform.position + Vector3.left * 0.65f, 0.4f);
                if (!cherrl || StoryRoom != CherrlHospital) yield break;
                CherrlNurseDebutConversation(cherrl, hush);
                yield break;
            }
            if (stage == CherrlHospitalAbsence && hush && StoryNewDay("cherrl"))
                CherrlHospitalAbsenceVisit(hush);
        }

        private bool CherrlNurseDebutConversation(NPCController cherrl, NPCController hush)
        {
            // TOWN.BIN 0x5A1FA5–0x5A218E.
            void OpenHospitalMenu() => StoryTalk(hush, "He, he, he.", "He, he, he.", () =>
                StoryAsk(hush, "And what have you come for today?", "E o que trouxe você aqui hoje?",
                    (T("I'm buying some medicine.", "Vim comprar remédio."), () =>
                    {
                        _hospitalHerbShop = true;
                        SetBarryShopPage(BarryShopPage.Buy);
                    }),
                    (T("I'm injured.", "Estou ferido."), () => TownTalkSequence(hush, () => CherrlTreatsScratch(cherrl),
                        new TownStoryLine("It's only a scratch.", "É só um arranhão."),
                        new TownStoryLine("Cherrl, would you look after {name} for me?", "Cherrl, pode cuidar do {name} para mim?"))),
                    (T("I've come to see Cherrl.", "Vim ver a Cherrl."), () => CherrlTalkSequence(cherrl, () =>
                            AdvanceTownStory("cherrl", CherrlHospitalAbsence),
                        new CherrlStoryLine(7, "Ohh, {name}.", "Ah, {name}."),
                        new CherrlStoryLine(7, "Right now in my life, every single day is so much fun.",
                            "Agora todos os dias da minha vida são tão divertidos.")))));
            return CherrlTalk(cherrl, 7,
                "After repeatedly asking to help, the doctor has allowed me to assist him.",
                "De tanto pedir para ajudar, o médico finalmente me deixou trabalhar com ele.", () =>
                    StoryAsk(hush, "She was very insistent.\nI couldn't help but have her become a nurse.",
                        "Ela insistiu tanto\nque acabei deixando ela se tornar enfermeira.",
                        (T("So you're happy.", "Então o senhor está contente."), OpenHospitalMenu),
                        (T("Thank you doctor.", "Obrigado, doutor."), OpenHospitalMenu)));
        }

        private void CherrlTreatsScratch(NPCController cherrl)
        {
            // TOWN.BIN 0x5A259C–0x5A28F5.
            void Bandage() => CherrlTalkSequence(cherrl, null,
                new CherrlStoryLine(7, "... giggle...", "... he, he..."),
                new CherrlStoryLine(7, "There, I'm done!", "Pronto, terminei!"),
                new CherrlStoryLine(7, "Even a scratch can lead to major infections or diseases.\nBe careful, {name}.",
                    "Até um arranhão pode causar uma infecção ou doença grave.\nTenha cuidado, {name}."));
            void AskKoh() => CherrlAsk(cherrl, 7, "The male patients like it when I say that.\nDo you not like the way I said that?",
                "Os pacientes homens gostam quando falo assim.\nVocê não gostou?",
                (T("It doesn't really matter.", "Tanto faz."), Bandage), (T("I don't mind it", "Não me importo"), Bandage));
            CherrlAsk(cherrl, 7, "Right away.\nThis is going to hurt a little,\nstay still for a moment.",
                "É para já.\nVai doer um pouquinho.\nFique parado um instante.",
                (T("I don't feel good", "Não estou me sentindo bem"), AskKoh),
                (T("Don't treat me like a baby.", "Não me trate como bebê."), AskKoh));
        }

        private void CherrlMedicineHandoff()
        {
            int stage = StoryStage("cherrl");
            if (StoryRoom != CherrlHospital ||
                (stage is not (CherrlNurseDebut or CherrlHospitalAbsence or CherrlFinalTalk) &&
                    !(stage == StoryComplete && TownRetailScriptFlags.CreatePersistent().IsFlagSet(CherrlNurseFlag)))) return;
            var cherrl = FindStoryActor("cherrl");
            if (!cherrl) return;
            SetBarryShopPage(BarryShopPage.Closed);
            CherrlTalk(cherrl, 7, "Here's your medicine.\nDon't over work yourself.",
                "Aqui está seu remédio.\nNão se esforce demais.");
        }

        private bool CherrlHospitalAbsenceVisit(NPCController hush)
        {
            // TOWN.BIN 0x5A2C47–0x5A2E66.
            void Explain()
            {
                int floor = TownSaveVariables.GetInt(TownReputation.HighestFloorKey);
                TownTalkSequence(hush, () =>
                    {
                        AdvanceTownStory("cherrl", CherrlFinalTalk);
                        if (StoryRoom == CherrlHospital && !FindStoryActor("cherrl"))
                            SpawnStoryResident("cherrl", "cherrl", "Cherrl", default);
                    },
                    new TownStoryLine("Cherrl has gone to do some shopping for me.", "A Cherrl saiu para fazer umas compras para mim."),
                    new TownStoryLine($"By the way, I hear you're already gone to level {floor} of the tower.",
                        $"Aliás, ouvi dizer que você já chegou ao {floor}º andar da torre."),
                    new TownStoryLine("You're amazing.\nYour father was also very strong.\nLike father, like son.\nIt must run in the family.",
                        "Você é incrível.\nSeu pai também era muito forte.\nTal pai, tal filho.\nDeve ser de família."),
                    new TownStoryLine("The day you brought the Healing Herb confirmed that fact in my mind.",
                        "Tive certeza disso no dia em que você trouxe a Erva Curativa."));
            }
            return StoryAsk(hush, "Hi {name}, what have you come for today?", "Oi, {name}, o que trouxe você aqui hoje?",
                (T("Are you the only one here?", "Só o senhor está aqui?"), Explain),
                (T("Where is Cherrl?", "Onde está a Cherrl?"), Explain));
        }

        private bool VivianStory(NPCController npc)
        {
            const string route = "vivian";
            int stage = StoryStage(route);
            if (npc.npcId == "okami")
            {
                int cape = FindStoryItem(QuestItemKind, BlueCapeId);
                if (stage < 2 && cape >= 0)
                    return StoryAsk(npc, "Huh!? Wh... what's this!? It's his cape! This cape belongs to him... I know for sure...",
                        "Hã!? O qu... que é isto!? É a capa dele! Esta capa pertence a ele... eu sei com certeza...",
                        (T("Hand over the Blue Cape", "Entregar a Capa Azul"), () =>
                        {
                            if (!_townInventory.ConsumeDungeonItem(cape, QuestItemKind)) return;
                            TownRetailScriptFlags.CreatePersistent().SetBit(TavernQuestCompleteFlag);
                            TownSaveVariables.SetInt(OkamiBarPhaseKey,(int)OkamiBarPhase.Silent);
                            TownSaveVariables.SetBool(OkamiSadTalkKey,false);
                            TownSaveVariables.SetBool(OkamiReopenedThanksKey,false);
                            AdvanceTownStory(route, 2);
                            StoryTalk(npc, "...", "...");
                        }), (T("Later", "Depois"), null));
                if (stage < 2)
                {
                    // TOWN.BIN 0x6A5E06–0x6A6033.
                    return TownDialogueSequence(npc, null, stage == 0 ? () => AdvanceTownStory(route, 1) : null,
                        ("Well hello, {name}.", "Ora, olá, {name}."),
                        ("I’ve been waiting at this store for ages for this person to return.", "Espero há anos neste bar pela volta dessa pessoa."),
                        ("This [Hunter] has not returned since leaving for the tower.", "Esse [Caçador] não voltou desde que partiu para a torre."),
                        ("Boy did he look smart in his blue cape.", "Como ele ficava elegante com sua capa azul."),
                        ("They called him the blue warrior.", "Chamavam-no de guerreiro azul."),
                        ("You’d think he’d walk right through that door any minute now...,", "Até parece que ele vai entrar por aquela porta a qualquer momento...,"),
                        ("... and tell me he’s home.", "... e me dizer que chegou."));
                }
                if (stage == 2)
                    return OkamiBarClosed ? VivianClosedBarConversation(npc) : StoryTalk(npc,"...","...");
                if (TownSaveVariables.HasValue(OkamiBarPhaseKey) &&
                    CurrentOkamiBarPhase == OkamiBarPhase.Reopened && !TownSaveVariables.GetBool(OkamiReopenedThanksKey))
                    return OkamiReopeningThanks(npc);
                return false;
            }
            if (stage == 3 && StoryRoom == BarRoom && StoryNewDay(route))
                return VivianFirstMeeting(npc);
            if (stage == 4 && (StoryRoom is CasinoRoom or CasinoSlotsRoom) && StoryBuilding("casino"))
                return TownSaveVariables.GetBool(VivianCasinoVisitKey)
                    ? VivianTalk(npc, 1, "I might go to the bar tomorrow.", "Talvez eu vá ao bar amanhã.")
                    : VivianCasinoVisit(npc);
            if (stage == 4 && !TownSaveVariables.GetBool(VivianTheaterDreamKey) && StoryNewDay(route) &&
                StoryRoom == BarRoom && StoryBuilding("theater") &&
                (!StoryBuilding("casino") || TownSaveVariables.GetBool(VivianCasinoVisitKey)))
                return VivianTheaterDream(npc);
            if (stage == 4 && StoryRoom == BarRoom && StoryBuilding("theater") &&
                TownSaveVariables.GetBool(VivianTheaterDreamKey) && !TownSaveVariables.GetBool(StoryPrefix + "vivian.fonScout"))
            {
                StartCoroutine(PlayVivianFonScoutScene());
                return true;
            }
            if (stage == 4 && StoryRoom == BarRoom && !StoryBuilding("casino"))
                return VivianCasinoDream(npc);
            if (stage == 4 && StoryRoom == BarRoom)
                return VivianTalk(npc, 0, "It's a pity to have a dream but not the chance.\nMaybe I should start traveling again.",
                    "É uma pena ter um sonho sem ter a chance de realizá-lo.\nTalvez eu deva voltar a viajar.");
            if (stage == 8 && StoryRoom == BarRoom)
                return VivianReturnToBar(npc);
            if (stage >= StoryComplete && StoryRoom == BarRoom)
                return VivianBarEpilogue(npc);
            if (StoryRoom == PoolOpenRoom)
                return VivianPoolStory(npc);
            if (stage == StoryComplete && StoryRoom == TheaterRoom && PoolRestored &&
                TownSaveVariables.GetInt(VivianPoolDateKey) == 0)
                return VivianPoolInvitation(npc);
            if ((stage is 5 or 6 or 9 or 10 or 11) && StoryNewDay(route) && StoryRoom == TheaterRoom)
            {
                WatchVivian(npc);
                return true;
            }
            if (stage == 7 && StoryNewDay(route) && StoryRoom == TheaterRoom)
                return VivianStageFrightConversation(npc);
            if (stage == 7 && StoryRoom == TheaterRoom)
                return VivianTalk(npc, 2, "They're doing better without me...", "Eles estão se saindo melhor sem mim...");
            return false;
        }

        private bool VivianClosedBarConversation(NPCController okami)
        {
            if (TownSaveVariables.GetBool(OkamiSadTalkKey)) return OkamiClosedLastLine(okami);
            return StoryAsk(okami, "Zzzzz...... I'm closing the bar down, get out of here.",
                "Zzzzz...... Vou fechar o bar. Cai fora daqui.",
                (T("Give me some booze!", "Me dê uma bebida!"), () => VivianDrinkConversation(okami)),
                (T("Lift your head when talking.", "Levante a cabeça quando falar."), () => OkamiSadConversation(okami)));
        }

        private void VivianDrinkConversation(NPCController okami)
        {
            StoryTalk(okami, "Zzzz... What! Did you say you wanted some booze? Well that's a first! What can I get you.",
                "Zzzz... O quê! Você disse que queria uma bebida? Essa é nova! O que vai querer.", () =>
                StoryAsk(okami, "What will it be?", "O que vai ser?",
                    (T("Something strong.", "Algo forte."), () => VivianColaConversation(okami)),
                    (T("I haven't got a clue.", "Não faço ideia."), () => VivianColaConversation(okami))));
        }

        private void VivianColaConversation(NPCController okami)
        {
            StoryTalk(okami, "Okay! Give him some.", "Certo! Sirvam algo para ele.", () =>
                StoryAsk(okami, "Here you go.", "Aqui está.",
                    (T("Here I go.", "Aqui vou eu."), () => StoryTalk(okami,
                        "No, no, you mustn't drink it so quickly...!", "Não, não, você não deve beber tão rápido assim...!", () =>
                            StoryTalk(okami, "What did you serve him? I only served him a glass of cola.",
                                "O que você serviu para ele? Eu só servi um copo de refrigerante.", () =>
                                    StoryTalk(okami, "He, he, what a fellow. You mean he's drunk on cola?",
                                        "He, he, que sujeito. Quer dizer que ele ficou bêbado com refrigerante?",
                                        OpenVivianBar)))),
                    (T("Wait a minute.", "Espere um minuto."), () => StoryTalk(okami, "Is there a problem?", "Há algum problema?"))));
        }

        private void OpenVivianBar()
        {
            // Conversation completion does not reopen the building. The next
            // completed tower trip advances the cast and Vivian's introduction.
            TownSaveVariables.SetBool(OkamiSadTalkKey,true);
            PersistTownProgress();
            var okami=FindStoryActor("okami");
            if (okami) OkamiClosedLastLine(okami);
        }

        private bool VivianFirstMeeting(NPCController vivian)
        {
            // TOWN.BIN 0x6A1E0A–0x6A23A2.
            return VivianAsk(vivian, 0, "Well, what a cute looking hunter.\nYou must be {name}.",
                "Ora, que caçador bonitinho.\nVocê deve ser {name}.",
                (T("That's right.", "Isso mesmo."), () => VivianIntroduceHerself(vivian, "Nice to meet you, {name}.", "Prazer, {name}.")),
                (T($"No. I'm Mr. {HeroName}.", $"Não. Sou o Sr. {HeroName}."),
                    () => VivianIntroduceHerself(vivian, "Nice to meet you, Mr. {name}.", "Prazer, Sr. {name}.")));
        }

        private void VivianIntroduceHerself(NPCController vivian, string greeting, string greetingPt)
        {
            void AskAboutDance() => VivianAsk(vivian, 0, "How's my dancing?", "O que achou da minha dança?",
                (T("You were really lively.", "Você dançou com muita energia."), () => VivianDanceMetaphor(vivian)),
                (T("I don't understand dancing.", "Não entendo de dança."),
                    () => VivianTalk(vivian, 0, "Oh...", "Ah...", () => VivianDanceMetaphor(vivian))));

            void StartFirstDance() => BeginTheaterAct(TheaterAct.Dance, vivian, vivian, () =>
                VivianTalk(vivian, 0, "Phew!  Great timing.\nI was just about to take a break.",
                    "Ufa! Chegou na hora certa.\nEu ia fazer uma pausa.", AskAboutDance));

            VivianSequence(vivian, 0, StartFirstDance,
                ("My name is Vivian Merca.", "Meu nome é Vivian Merca."),
                ("I came to this city recently, and was hired as a dancer by Madame.",
                    "Vim para esta cidade recentemente e fui contratada como dançarina pela Madame."),
                (greeting, greetingPt));
        }

        private void VivianDanceMetaphor(NPCController vivian)
        {
            VivianSequence(vivian, 0, () => VivianAsk(vivian, 0,
                    "{name}, have you ever felt like a gorilla escaping from its cage?",
                    "{name}, você já se sentiu como um gorila escapando da jaula?",
                    (T("You calling me a gorilla?", "Você está me chamando de gorila?"), () => VivianTalk(vivian, 0,
                        "Was it that obvious? Sorry.", "Foi tão óbvio assim? Desculpe.", () => VivianDreamQuestion(vivian))),
                    (T("Yes I have.", "Sim, já senti."), () => VivianTalk(vivian, 0,
                        "Then you'd know how it feels like.", "Então você sabe como é essa sensação.", () => VivianDreamQuestion(vivian)))),
                ("You've got great sense when it comes to dancing.", "Você leva jeito para apreciar dança."),
                ("You know, when I get up on that stage, my body feels lighter.", "Sabe, quando subo no palco, meu corpo fica mais leve."),
                ("I'm like a bird escaping from its cage.", "Fico como um pássaro escapando da gaiola."));
        }

        private void VivianDreamQuestion(NPCController vivian)
        {
            VivianAsk(vivian, 0, "{name}, do you have a dream?", "{name}, você tem um sonho?",
                (T("Yes I do.", "Sim, tenho."), () => VivianTalk(vivian, 0,
                    "That's why your eyes reflect hope.\nYou know, I've got a dream too.",
                    "É por isso que seus olhos brilham de esperança.\nSabe, eu também tenho um sonho.", () => VivianDreamSpeech(vivian))),
                (T("... no I don't.", "... não tenho."), () => VivianSequence(vivian, 0, () => VivianDreamSpeech(vivian),
                    ("Really?\nBut you've got those eyes that reflect hope.", "Sério?\nMas seus olhos brilham de esperança."),
                    ("Well, I've got a dream.", "Bom, eu tenho um sonho."))));
        }

        private void VivianDreamSpeech(NPCController vivian)
        {
            VivianSequence(vivian, 0, () => AdvanceTownStory("vivian", 4),
                ("My dream is to dance on an enormous stage.", "Meu sonho é dançar num palco enorme."),
                ("But although I've been to several cities, not one of them has a decent theater.",
                    "Mas, embora eu tenha ido a várias cidades, nenhuma delas tinha um teatro decente."),
                ("Rumors had it that the city of Haruka had one, but it's gone.", "Diziam que a cidade de Haruka tinha um, mas ele desapareceu."),
                ("It's a pity to have a dream but not the chance.\nMaybe I should start traveling again.",
                    "É uma pena ter um sonho e não ter a oportunidade de realizá-lo.\nTalvez eu deva voltar a viajar."));
        }

        private bool VivianCasinoDream(NPCController vivian)
        {
            // TOWN.BIN 0x6A2B06–0x6A2DCA. Jorda handles the building request.
            VivianSequence(vivian, 0, () => VivianAsk(vivian, 0, "Have you heard of it?", "Já ouviu falar nisso?",
                    (T("Yes I have.", "Sim, já ouvi."), () => VivianSequence(vivian, 0, null,
                        ("They're real fun, aren't they.", "São muito divertidos, né?"),
                        ("There was one point where I forgot about work and stayed there day in and day out.",
                            "Teve uma época em que esqueci do trabalho e ficava lá dia após dia."))),
                    (T("No I haven't", "Não, nunca ouvi"), () => VivianSequence(vivian, 0, null,
                        ("They're a lot of fun.\nI even took out a loan...", "São muito divertidos.\nAté fiz um empréstimo..."),
                        ("Woops, I'm telling you too much,\nthat's pretty embarrassing, isn't it.",
                            "Opa, já falei demais.\nQue vergonha, né?")))),
                ("I use to live in the city of Haruka.", "Eu morava na cidade de Haruka."),
                ("That city had what they called a Casino.", "Aquela cidade tinha o que eles chamavam de Cassino."));
            return true;
        }

        private bool VivianTheaterDream(NPCController vivian)
        {
            void TalkAboutTheater() => VivianSequence(vivian, 1, FinishVivianTheaterDream,
                ("This city has a theater.", "Esta cidade tem um teatro."),
                ("My dream is...\nto dance on an enormous stage.", "Meu sonho é...\ndançar num palco enorme."),
                ("Until now, I've been to several cities, but not one of them had a decent theater.",
                    "Já passei por várias cidades, e nenhuma delas tinha um teatro decente."),
                ("Rumors had it that the city of Haruka had one, but it was gone...",
                    "Diziam que Haruka tinha um, mas ele desapareceu..."),
                ("I'm glad I came to this city.\nMy dream is going to come true.",
                    "Ainda bem que vim para esta cidade.\nMeu sonho vai se realizar."));
            if (TownSaveVariables.GetBool(VivianCasinoVisitKey))
                VivianSequence(vivian, 0, TalkAboutTheater,
                    ("I didn't realize this city had a theater.", "Eu nem sabia que esta cidade tinha um teatro."),
                    ("I was so caught up in the Casino that I never noticed it.\nI'm such a fool.",
                        "Eu estava tão distraída com o cassino que nem reparei.\nQue tola."));
            else TalkAboutTheater();
            return true;
        }

        private void FinishVivianTheaterDream()
        {
            TownSaveVariables.SetBool(VivianTheaterDreamKey, true);
            PersistTownProgress();
        }

        private bool VivianCasinoVisit(NPCController vivian)
        {
            // TOWN.BIN 0x4648CA–0x464F71 (roulette), 0x466AB8–0x466FC2 (slots).
            if (TownSaveVariables.GetBool(VivianCasinoLoanKey))
                return VivianCasinoLoanQuestion(vivian, "Lend me some.\nPlease, I just need a little.",
                    "Me empresta um pouco?\nPor favor, só preciso de um pouquinho.");
            if (TownSaveVariables.GetBool(VivianCasinoGreetingKey))
                return VivianTalk(vivian, 0, "Yes, yes.\nCome on, come on, aaaah,\nawww, almost.\nOh, {name}.",
                    "Isso, isso.\nVai, vai, aaaah...\nPuxa, quase.\nOi, {name}.", () => VivianCasinoQuestions(vivian));
            VivianSequence(vivian, 0, () =>
                {
                    TownSaveVariables.SetBool(VivianCasinoGreetingKey, true);
                    PersistTownProgress();
                    VivianCasinoQuestions(vivian);
                },
                ("Yes, yes.\nCome on, come on, aaaah,\nawww, almost.", "Isso, isso.\nVai, vai, aaaah...\nPuxa, quase."),
                ("Oh, {name}.\nIt seems this city also has a casino.", "Oi, {name}.\nParece que esta cidade também tem um cassino."),
                ("I couldn't quit at the casino in the city of Haruka...", "Eu não conseguia sair do cassino em Haruka..."),
                ("but it's even more fun here.\nIt's just great!", "mas aqui é ainda mais divertido.\nÉ ótimo!"));
            return true;
        }

        private void VivianCasinoQuestions(NPCController vivian) =>
            VivianAsk(vivian, 0,
                    "It's so fun here, working for a living just doesn't seem to make sense.",
                    "É tão divertido aqui que trabalhar para ganhar a vida nem parece fazer sentido.",
                    (T("Don't you have to work?", "Você não precisa trabalhar?"), () => VivianCasinoWorkQuestion(vivian)),
                    (T("Do you have money?", "Você tem dinheiro?"), () => VivianCasinoMoneyQuestion(vivian)));

        private void VivianCasinoWorkQuestion(NPCController vivian)
        {
            VivianAsk(vivian, 0, "It's okay, really!", "Está tudo bem, de verdade!",
                (T("I'd love to see you dance.", "Eu adoraria ver você dançar."), () => VivianCasinoVisitComplete(vivian, true)),
                (T("Enjoy yourself.", "Divirta-se."), () => VivianTalk(vivian, 1,
                    "Okay, once more!", "Certo, mais uma vez!")));
        }

        private void VivianCasinoMoneyQuestion(NPCController vivian)
        {
            VivianCasinoLoanQuestion(vivian, "Why, are you going to lend me some, {name}?", "Por quê? Vai me emprestar um pouco, {name}?");
        }

        private bool VivianCasinoLoanQuestion(NPCController vivian, string english, string portuguese)
        {
            var refusal = AtCasinoSlots
                ? T("Sorry. But not for gamble.", "Desculpe. Para apostar, não.")
                : T("I don't have cash right now.", "Estou sem dinheiro agora.");
            return VivianAsk(vivian, 0, english, portuguese,
                (T("No way.", "De jeito nenhum."), () => VivianTalk(vivian, 2, "You're so stingy", "Que mão de vaca!", () =>
                {
                    TownSaveVariables.SetBool(VivianCasinoLoanKey, true);
                    PersistTownProgress();
                })),
                (refusal, () => VivianCasinoVisitComplete(vivian, false)));
        }

        private void VivianCasinoVisitComplete(NPCController vivian, bool askedToDance)
        {
            VivianSequence(vivian, 1, () =>
                {
                    TownSaveVariables.SetBool(VivianCasinoVisitKey, true);
                    PersistTownProgress();
                },
                askedToDance ? ("Really?\n......", "Sério?\n......")
                    : ("Aa... yes,\nI guess you're right.\n......", "Ah... sim,\nacho que tem razão.\n......"),
                ("I might go to the bar tomorrow.", "Talvez eu vá ao bar amanhã."));
        }

        private bool VivianReturnToBar(NPCController vivian)
        {
            // TOWN.BIN 0x6A31D7–0x6A34F9, before Vivian finds her rhythm again.
            void TalkToGhosh()
            {
                if (!FindStoryActor("vivian_bar_ghosh")) SpawnStoryResident("ghosh", "vivian_bar_ghosh", "Ghosh", default);
                var ghosh = FindStoryActor("vivian_bar_ghosh");
                if (!ghosh)
                {
                    StartVivianBarDance(vivian);
                    return;
                }

                void Dance() => StartVivianBarDance(vivian);
                void Agree() => StoryAsk(ghosh, "Tsk.\nThat's the first time we've ever agreed on anything.",
                    "Tsc.\nÉ a primeira vez que concordamos em alguma coisa.", GhoshPortrait,
                    (T("That's true, and not bad.", "É verdade, e até que foi bom."), () => GhoshTalk(ghosh, "I guess you're right.", "Acho que você tem razão.", Dance)),
                    (T("It's a nightmare.", "É um pesadelo."), () => GhoshTalk(ghosh, "Same goes for me.", "Para mim também.", Dance)));

                StoryAsk(ghosh, "This is great...", "Isso é ótimo...", GhoshPortrait,
                    (T("I agree.", "Concordo."), Agree),
                    (T("You're kidding.", "Está brincando."), () => GhoshTalk(ghosh,
                        "Man, you've got no understanding for beauty.", "Cara, você não entende nada de beleza.", Dance)));
            }

            var madam = FindStoryActor("okami");
            if (madam)
                return StoryTalk(madam, "All of you together?  What's the occasion?",
                    "Vocês todos juntos? Qual é a ocasião?", () => StoryTalk(madam,
                        "Did you want to use the stage?\nOf course it's okay.\nIf it's enjoyable, it's fine by me.",
                        "Querem usar o palco?\nClaro que podem.\nSe for divertido, por mim tudo bem.", TalkToGhosh));
            TalkToGhosh();
            return true;
        }

        // TOWN.BIN 0x6A354E–0x6A3862 selects portrait 0 for these lines.
        private void StartVivianBarDance(NPCController vivian) =>
            BeginTheaterAct(TheaterAct.Dance, vivian, vivian, () =>
                VivianTalk(vivian, 0, "Yes!\nThis is it!\nThis is the feeling!", "Sim!\nÉ isso!\nEra essa sensação!", () =>
                    VivianSequence(vivian, 0, () => AdvanceTownStory("vivian", 9),
                        ("I'm so stupid.\nI love dancing so much, yet I can't get myself into it...",
                            "Que tola eu fui.\nAmo tanto dançar, mas não conseguia me entregar..."),
                        ("If I'm not into it, how are the customers expected to enjoy it?",
                            "Se eu mesma não me envolvo, como posso esperar que o público goste?"),
                        ("I...\nI froze, when my dream appeared before my eyes.",
                            "Eu...\nEu travei quando vi meu sonho se realizar bem diante dos meus olhos."),
                        ("But I'm all right now.\nEven on that enormous stage!", "Mas agora estou bem.\nAté naquele palco enorme!"),
                        ("Thank you Madam!", "Obrigada, Madame!"))));

        private bool VivianBarEpilogue(NPCController vivian)
        {
            // TOWN.BIN 0x6A7C2E–0x6A7D84. 0x6A7DCE is Madam's greeting.
            bool hasHeard = TownSaveVariables.GetBool(VivianBarEpilogueKey);
            bool CommentOnCouple() => VivianTalk(vivian, 0, "I think they'd make a great couple.",
                "Acho que eles formariam um belo casal.", hasHeard ? null : () =>
                {
                    TownSaveVariables.SetBool(VivianBarEpilogueKey, true);
                    PersistTownProgress();
                });
            if (hasHeard) return CommentOnCouple();
            return VivianTalk(vivian, 0, "That bartender's normally quite unfriendly, but he's really nice towards Madam.",
                "O barman costuma ser bem antipático, mas é muito gentil com a Madame.", () => CommentOnCouple());
        }

        private bool VivianStageFrightConversation(NPCController vivian)
        {
            // TOWN.BIN 0x4BB311 and 0x4BB607 select the lowered-head portrait.
            void AskVivian() => VivianAsk(vivian, 2, "{name}...", "{name}...",
                (T("Why don't you dance?", "Por que você não dança?"), () => VivianDancingWorry(vivian)),
                (T("Are you ill?", "Você está doente?"), () => VivianTalk(vivian, 2, "No.", "Não.", () => VivianDancingWorry(vivian))));

            return VivianTalk(vivian, 2, "Oh, {name}.\nYou were here.", "Ah, {name}.\nVocê estava aqui.", () =>
            {
                var fon = FindStoryActor("theater_fon");
                if (!fon)
                {
                    AskVivian();
                    return;
                }
                FonTalk(fon, "You came at the right time.\nLook at Vivian.\nShe's been like that for some time now.",
                    "Chegou na hora certa.\nOlhe para a Vivian.\nEla já está assim faz um tempo.", () =>
                        FonTalk(fon, "She doesn't go out, and she's hardly eating.\nI've never been good at coping with such a gloomy atmosphere.",
                            "Ela não sai e mal come.\nNunca soube lidar com um clima tão triste.", () =>
                                FonTalk(fon, "Hey, {name}, you'd be the perfect person to cheer her up by responding to her feelings, the way you responded to my lines.",
                                    "Ei, {name}, você é a pessoa certa para animá-la. Preste atenção ao que ela sente, como fez com as minhas falas.",
                                    AskVivian)));
            });
        }

        private void VivianDancingWorry(NPCController vivian)
        {
            void GoForWalk() => VivianTalk(vivian, 2, "Sure, I've got nothing else to do.\nLet's go.",
                "Claro, não tenho mais nada para fazer.\nVamos.", () => StartCoroutine(PlayVivianWalkScene(vivian)));

            VivianTalk(vivian, 2, "They're doing better without me...", "Eles estão se saindo melhor sem mim...", () =>
                VivianAsk(vivian, 2, "and I'm sure the audience would rather have it that way...\n......",
                    "e tenho certeza de que o público também prefere assim...\n......",
                    (T("Let's go for a walk!", "Vamos dar uma volta!"), GoForWalk),
                    (T("How about dating with me?", "Que tal sair comigo?"), () => VivianTalk(vivian, 2,
                        "What!? You want to go out on a date with ME, {name}?",
                        "O quê!? Você quer sair COMIGO, {name}?", GoForWalk))));
        }

        private bool VivianTheaterPerformance(NPCController vivian, int stage)
        {
            // TOWN.BIN 0x4BA62F, 0x4BAE6B, 0x4BBCA8, 0x4BC3F5 and 0x4BCE70.
            int portrait = stage >= 11 ? 1 : 0;
            var greeting = stage switch
            {
                9 => ("Oh, {name}, you came.", "Ah, {name}, você veio."),
                10 => ("Oh, {name}, I'm so happy that you came.", "Ah, {name}, que bom que você veio."),
                >= 11 => ("You made it!  I'm so glad.", "Você veio! Que bom."),
                _ => ("Oh, {name}.", "Ah, {name}.")
            };
            return VivianAsk(vivian, portrait, greeting.Item1 + "\nDid you see me on stage?",
                greeting.Item2 + "\nVocê me viu no palco?",
                (T("You did great.", "Você foi ótima."), () => VivianTheaterAfterAct(vivian, stage, portrait, true)),
                (T("I didn't see you.", "Eu não vi você."), () => VivianTheaterAfterAct(vivian, stage, portrait, false)));
        }

        private void VivianTheaterAfterAct(NPCController vivian, int stage, int portrait, bool sawPerformance)
        {
            if (!sawPerformance)
            {
                VivianSequence(vivian, portrait, () => CompleteVivianStory(stage),
                    ("Didn't you? Awww, that's too bad. Well, next time, okay?",
                        "Não viu? Ahhh, que pena. Bem, na próxima vez, certo?"));
                return;
            }

            if (stage is 5 or 6)
            {
                string nextShow = stage == 5
                    ? "But I'm sure I'll show you better dancing next time."
                    : "But I'll be sure to show you much better dancing the next time.";
                VivianTalk(vivian, portrait, "Thank you.", "Obrigada.", () =>
                    VivianAsk(vivian, portrait, "I'm not that good yet.\n" + nextShow,
                        "Ainda não sou tão boa.\nVou dançar ainda melhor da próxima vez.",
                        (T("Sure, I look forward to it.", "Claro, vou esperar pela próxima."), () => VivianTalk(vivian, portrait,
                            "Thanks.", "Obrigada.", () => CompleteVivianStory(stage))),
                        (T("You are a great dancer!", "Você é uma ótima dançarina!"), () => VivianFirstTheaterPraise(vivian, stage, portrait))));
                return;
            }

            if (stage >= 10)
            {
                VivianTalk(vivian, portrait, "If I knew you were coming ahead of time, {name}, I could have danced better.\nThat's too bad.",
                    "Se eu soubesse que você vinha, {name}, teria dançado melhor.\nQue pena.", () => CompleteVivianStory(stage));
                return;
            }

            VivianTalk(vivian, portrait, "Thank you.\nAnd for the other...\nThank you.",
                "Obrigada.\nE pela outra coisa...\nObrigada.", () =>
                VivianAsk(vivian, portrait, "I'll be sure to show you a much better show next time.",
                    "Vou fazer um espetáculo bem melhor da próxima vez.",
                    (T("Sure, I look forward to it.", "Claro, vou esperar pela próxima."), () => VivianTalk(vivian, portrait,
                        "Thanks.", "Obrigada.", () => CompleteVivianStory(stage))),
                    (T("What was the 2nd thanks for?", "Pelo que foi o segundo agradecimento?"), () => VivianSecondTheaterThanks(vivian, stage, portrait))));
        }

        private void VivianFirstTheaterPraise(NPCController vivian, int stage, int portrait)
        {
            VivianSequence(vivian, portrait, () => CompleteVivianStory(stage),
                ("What do you mean I'm a great dancer?", "Como assim eu sou uma ótima dançarina?"),
                ("{name}, you've got no idea what dancing is, do you?\nOr are you just making fun of me?",
                    "{name}, você não entende nada de dança, entende?\nOu está só tirando sarro de mim?"));
        }

        private void VivianSecondTheaterThanks(NPCController vivian, int stage, int portrait)
        {
            VivianTalk(vivian, portrait, "Not for your words of praise, but for you yourself.",
                "Foi por você mesmo, não pelos elogios.", () => CompleteVivianStory(stage));
        }

        private void CompleteVivianStory(int stage)
        {
            if (stage >= StoryComplete) return;
            if (stage is 5 or 6)
            {
                AdvanceTownStory("vivian", stage + 1);
                return;
            }

            var vivian = FindStoryActor("vivian");
            if (stage == 9 && vivian && StoryRoom == TheaterRoom && !TownSaveVariables.GetBool(VivianRoseSceneKey))
            {
                StartCoroutine(PlayVivianRoseScene(vivian));
                return;
            }

            if (stage is 9 or 10)
            {
                AdvanceTownStory("vivian", stage + 1);
                return;
            }

            FinishVivianStory();
        }

        private void FinishVivianStory()
        {
            // Ft_bibian_love in the retail town event table.
            TownRetailScriptFlags.CreatePersistent().SetBit(5238);
            TownSaveVariables.SetBool("quest.vivian.complete", true);
            AdvanceTownStory("vivian", StoryComplete);
        }

        private IEnumerator PlayVivianWalkScene(NPCController vivian)
        {
            if (!vivian || StoryRoom != TheaterRoom || StoryStage("vivian") != 6) yield break;
            Vector3 meetingPoint = vivian.transform.position + Vector3.left * 1.1f;
            yield return PlayStoryWalk(vivian, meetingPoint, 0.55f);
            if (!vivian || StoryRoom != TheaterRoom) yield break;

            if (!FindStoryActor("vivian_walk_ghosh")) SpawnStoryResident("ghosh", "vivian_walk_ghosh", "Ghosh", default);
            var ghosh = FindStoryActor("vivian_walk_ghosh");
            if (!ghosh)
            {
                AdvanceTownStory("vivian", 8);
                yield break;
            }
            Vector3 stop = meetingPoint + Vector3.right * 0.75f;
            ghosh.transform.position = stop + Vector3.right * 1.4f;
            yield return PlayStoryWalk(ghosh, stop, 0.55f);
            if (!vivian || !ghosh || StoryRoom != TheaterRoom) yield break;

            // TOWN.BIN 0x6B88D0–0x6B8C61.
            VivianSequence(vivian, 2, () => GhoshSequence(ghosh, () =>
                    VivianTalk(vivian, 0, "Are you talking about {name}?\nHe's an important friend of mine.",
                        "Está falando do {name}?\nEle é um amigo muito importante para mim.", () =>
                            GhoshSequence(ghosh, () => VivianBarOrTheaterChoice(vivian, ghosh),
                                ("It mustn't be.\nYou are being deceived.", "Não pode ser.\nEle está enganando você."),
                                ("Here, come to the bar with me.\nPlease let me see your wonderful dancing.",
                                    "Venha comigo ao bar.\nPor favor, quero ver você dançar de novo."))),
                    ("Hello, Miss Vivian.\nI bless this day, for being able to meet you.", "Olá, senhorita Vivian.\nQue dia abençoado, encontrar você assim."),
                    ("By the way...\nWhat is your relationship with that scum beside you?", "A propósito...\nQuem é esse canalha ao seu lado?")),
                ("I'd rather take a walk outside and take in all that fresh air.\nPhew...",
                    "Prefiro dar uma volta lá fora e respirar um pouco de ar fresco.\nUfa..."),
                ("Wow...\nI had forgotten how blue the sky is.", "Nossa...\nEu tinha esquecido como o céu é azul."));
        }

        private void VivianBarOrTheaterChoice(NPCController vivian, NPCController ghosh)
        {
            VivianSequence(vivian, 2, () => GhoshTalk(ghosh, "Is that so.\nNo wonder I never see you at the bar.",
                    "É mesmo?\nPor isso nunca mais vi você no bar.", () => StoryAsk(ghosh,
                        "I imagine you'd be very beautiful dancing on that large stage!",
                        "Imagino como você deve estar linda dançando naquele palco enorme!",
                        (T("Let's go to the Bar!", "Vamos ao bar!"), () => VivianReturnToBarWithGhosh(vivian, ghosh)),
                        (T("Dance again at the Theater!", "Dance de novo no teatro!"), () => VivianRemainAtTheater(vivian)))),
                ("The bar... It was a lot of fun there. To dance in that atmosphere.",
                    "O bar... Era muito divertido lá. Dançar naquela atmosfera."),
                ("I had no doubts when it came to dancing.", "Eu não tinha dúvidas quando se tratava de dançar."),
                ("You know, I no longer go to the bar. I go to the Theater...",
                    "Sabe, eu não vou mais ao bar. Eu vou ao Teatro..."));
        }

        private void VivianRemainAtTheater(NPCController vivian)
        {
            VivianSequence(vivian, 2, null,
                ("The Theater...\nMe...!?", "No teatro...\nEu...!?"),
                ("No I can't. I'll just get in the way of the others.", "Não, não consigo. Só vou atrapalhar os outros."),
                ("I... I must... return...", "Eu... eu preciso... voltar..."));
        }

        private void VivianReturnToBarWithGhosh(NPCController vivian, NPCController ghosh)
        {
            VivianSequence(vivian, 0, () => VivianAsk(vivian, 2,
                    "She was so kind to give me a job... then as soon as I was called on by the Theater, I immediately quit the Bar job.",
                    "Ela foi tão gentil em me dar trabalho... e, assim que o teatro me chamou, larguei o emprego no bar.",
                    (T("Of course she would.", "Claro que permitiria."), () => ReturnVivianToBar()),
                    (T("Hey Ghosh, join us.", "Ei, Ghosh, venha conosco."), () => GhoshTalk(ghosh,
                        "I'm not happy about going with you... but I want to see Vivian dance.",
                        "Não estou feliz em ir com você... mas quero ver a Vivian dançar.", () => GhoshTalk(ghosh,
                            "Hey, guys! Wait for me!", "Ei, pessoal! Esperem por mim!", ReturnVivianToBar)))),
                ("To the Bar!?\nDance at the Bar!?\nI'd love to dance.", "Ao bar!?\nDançar no bar!?\nEu adoraria."),
                ("But will Madam allow me to dance there?", "Mas a Madame vai deixar eu dançar lá?"));
        }

        private void ReturnVivianToBar()
        {
            AdvanceTownStory("vivian", 8);
            StartCoroutine(LoadInterior(BarRoom));
        }

        private IEnumerator PlayVivianRoseScene(NPCController vivian)
        {
            if (_vivianRoseScenePlaying || !vivian || StoryRoom != TheaterRoom ||
                StoryStage("vivian") != 9 || TownSaveVariables.GetBool(VivianRoseSceneKey)) yield break;

            _vivianRoseScenePlaying = true;
            if (!FindStoryActor("vivian_roses_ghosh")) SpawnStoryResident("ghosh", "vivian_roses_ghosh", "Ghosh", default);
            var ghosh = FindStoryActor("vivian_roses_ghosh");
            if (!ghosh)
            {
                FinishVivianRoseScene();
                yield break;
            }

            Vector3 stop = vivian.transform.position + Vector3.right * 0.8f;
            ghosh.transform.position = stop + Vector3.right * 1.25f;
            yield return PlayStoryWalk(ghosh, stop, 0.55f);
            if (!vivian || !ghosh || StoryRoom != TheaterRoom)
            {
                _vivianRoseScenePlaying = false;
                yield break;
            }

            GhoshTalk(ghosh, "Any beautiful thing in the world looks pale when compared to you.",
                "Qualquer coisa bonita no mundo parece pálida quando comparada a você.", () => GhoshTalk(ghosh,
                    "In search of something that would reflect your beauty, I brought this bouquet of flowers.",
                    "Em busca de algo que refletisse sua beleza, trouxe este buquê de flores.", () => VivianTalk(vivian, 0,
                        "Thank you Ghosh.", "Obrigada, Ghosh.", () => GhoshTalk(ghosh,
                            "Unfortunately, I must be going, as I have other plans today.",
                            "Infelizmente preciso ir, pois tenho outros planos hoje.", () => VivianTalk(vivian, 0,
                                "The flowers smell wonderful.", "As flores têm um cheiro maravilhoso.", FinishVivianRoseScene)))));
        }

        private void FinishVivianRoseScene()
        {
            TownSaveVariables.SetBool(VivianRoseSceneKey, true);
            _vivianRoseScenePlaying = false;
            AdvanceTownStory("vivian", 10);
        }

        private IEnumerator PlayVivianFonScoutScene()
        {
            if (_vivianFonScoutPlaying || StoryRoom != BarRoom || StoryStage("vivian") != 4 ||
                !StoryBuilding("theater") || TownSaveVariables.GetBool(StoryPrefix + "vivian.fonScout")) yield break;
            _vivianFonScoutPlaying = true;
            yield return null;
            var vivian = FindStoryActor("vivian");
            if (!vivian || StoryRoom != BarRoom)
            {
                _vivianFonScoutPlaying = false;
                yield break;
            }

            if (!FindStoryActor("vivian_fon")) SpawnStoryResident("dar", "vivian_fon", "Fon", default);
            var fon = FindStoryActor("vivian_fon");
            if (!fon)
            {
                _vivianFonScoutPlaying = false;
                yield break;
            }
            Vector3 stop = vivian.transform.position + Vector3.left * 0.9f;
            fon.transform.position = stop + Vector3.left * 1.3f;
            yield return PlayStoryWalk(fon, stop, 0.55f);
            if (!vivian || !fon || StoryRoom != BarRoom)
            {
                _vivianFonScoutPlaying = false;
                yield break;
            }

            // TOWN.BIN 0x6A3C34–0x6A3F29.
            void VivianAnnouncesOffer() => VivianTalk(vivian, 0,
                "{name}!  Listen!\nA person from the Theater asked me if I'd like to dance!",
                "{name}! Escute!\nUma pessoa do teatro me convidou para dançar!", () => VivianAsk(vivian, 1,
                    "I can now dance on that enormous stage!", "Agora posso dançar naquele palco enorme!",
                    (T("That's great.", "Que ótimo."), () => VivianTalk(vivian, 1, "Yes!", "Sim!", FinishVivianFonScout)),
                    (T("Oh.", "Ah."), () => VivianTalk(vivian, 3, "Aww.\nI thought you'd be more excited when you heard the good news.",
                        "Puxa.\nAchei que você fosse ficar mais animado com a notícia.", FinishVivianFonScout))));

            FonTalk(fon, "Good morning {name}!\nToday, I'm here as a scout, to see Vivian.\nWell, I've gotta get moving.",
                "Bom dia, {name}!\nVim ver a Vivian a trabalho, como olheiro.\nBom, já preciso ir.", () =>
                {
                    var madam = FindStoryActor("okami");
                    if (madam) StoryTalk(madam, "Yes, a scout was here from the Theater.", "Sim, veio um olheiro do teatro.", VivianAnnouncesOffer);
                    else VivianAnnouncesOffer();
                });
        }

        private void FinishVivianFonScout()
        {
            TownSaveVariables.SetBool(StoryPrefix + "vivian.fonScout", true);
            // The scouting scene is the hand-off from the bar route to the
            // first theater performance.  Without this transition, the
            // no-casino retail path can repeat the scout forever.
            if (StoryStage("vivian") == 4) AdvanceTownStory("vivian", 5);
            else PersistTownProgress();
            _vivianFonScoutPlaying = false;
        }

        private bool VivianPoolInvitation(NPCController vivian)
        {
            // TOWN.BIN 0x4B6807–0x4B69EE.
            return VivianAsk(vivian, 1, "Hey, Let's go to the pool.", "Ei, vamos à piscina.",
                (T("Good idea.", "Boa ideia."), () => VivianSequence(vivian, 1, () =>
                    {
                        TownSaveVariables.SetInt(VivianPoolDateKey, 1);
                        PersistTownProgress();
                        StartCoroutine(LoadInterior(PoolOpenRoom));
                    },
                    ("Great!\nI'm just in the mood to go to the pool.", "Ótimo!\nEu estava mesmo com vontade de ir à piscina."),
                    ("It would be great fun with you, {name}.", "Vai ser muito divertido com você, {name}."),
                    ("Well, what are we waiting for,\nlet's go!", "Bom, estamos esperando o quê?\nVamos!"))),
                (T("Sorry, I'm busy.", "Desculpe, estou ocupado."), () => VivianTalk(vivian, 0,
                    "Oh, okay.  Maybe some other time.", "Ah, tudo bem. Quem sabe outra hora.")));
        }

        private bool VivianPoolStory(NPCController vivian)
        {
            int visit = TownSaveVariables.GetInt(VivianPoolDateKey);
            if (visit == 1)
            {
                // TOWN.BIN 0x53363F–0x53392E.
                void WatchKohSwim() => VivianTalk(vivian, 5, "Wow, that's great!\nTo tell you the truth, I can't swim.",
                    "Uau, que ótimo!\nPara falar a verdade, eu não sei nadar.", () => VivianTalk(vivian, 5,
                        "{name}, You just keep swimming until you get bored.  I'll just be watching you from here.",
                        "{name}, nade até cansar. Eu fico daqui olhando.", () => SetVivianPoolVisit(2)));

                void AtPoolside() => VivianSequence(vivian, 5, () => VivianAsk(vivian, 5, "Can you swim, {name}?", "Você sabe nadar, {name}?",
                        (T("Nothing's impossible for me.", "Nada é impossível para mim."), () => VivianTalk(vivian, 5,
                            "Let me see!  Let me see you swim!", "Quero ver! Quero ver você nadar!", WatchKohSwim)),
                        (T("I like the wading pool.", "Gosto da piscina rasa."), () => VivianTalk(vivian, 5,
                            "What?  What's that?", "O quê? Como assim?", WatchKohSwim))),
                    ("Sorry to keep you waiting!", "Desculpe fazer você esperar!"),
                    ("A pool is really needed in the city of Monsbaiya.", "Monsbaiya precisava mesmo de uma piscina."),
                    ("Otherwise everyone would become mummies.", "Senão todo mundo ia virar múmia."));
                if (!WearingPoolSprite(vivian))
                    return VivianTalk(vivian, 0, "Wait for me at the pool side.", "Espere por mim na beira da piscina.", () =>
                    {
                        ChangePoolOutfit(vivian, AtPoolside);
                    });
                AtPoolside();
                return true;
            }
            if (visit == 2)
                return VivianAsk(vivian, 5, "Are you finished?\nShall we go now?", "Já terminou?\nVamos embora?",
                    (T("Let's go now.", "Vamos agora."), () => VivianFinishPoolDate(vivian)),
                    (T("I want to swim a little more.", "Quero nadar mais um pouco."), () => VivianTalk(vivian, 5,
                        "Okay.\nYou're just full of energy,\n{name}.", "Está bem.\nVocê tem energia de sobra,\n{name}.")));

            // TOWN.BIN 0x53609E–0x536277, the ordinary pool encounter.
            VivianSequence(vivian, 5, () => VivianAsk(vivian, 5,
                    "The sight of you soaking up the sun suits you, yes, more than seeing you inside.",
                    "Ver você tomando sol combina muito mais com você do que ver você lá dentro.",
                    (T("You're looking cool.", "Você está muito bem."), () => VivianTalk(vivian, 5, "Thank you.", "Obrigada.")),
                    (T("Thanks.", "Obrigado."), () => VivianTalk(vivian, 5, "giggle...", "Hi, hi..."))),
                ("Hi, {name}!", "Oi, {name}!"),
                ("You even look good in your swimsuit, {name}.", "Até de roupa de banho você fica bem, {name}."),
                ("You've got this glowing look about you {name}.", "Você está radiante, {name}."));
            return true;
        }

        private void VivianFinishPoolDate(NPCController vivian)
        {
            // TOWN.BIN 0x533AF5–0x533CCC.
            void SayGoodbye() => VivianSequence(vivian, 5, () => FinishVivianPoolTrip(vivian),
                ("I was satisfied just watching you swim, {name}.", "Só de ver você nadar já fiquei feliz, {name}."),
                ("See you, {name}.  Thanks for being with me today.", "Até mais, {name}. Obrigada por passar o dia comigo."));

            VivianTalk(vivian, 5, "Let's go.", "Vamos.", () =>
                VivianAsk(vivian, 5, "Today was so much fun.", "Hoje foi tão divertido.",
                    (T("Even you didn't swim?", "Mesmo sem nadar?"), SayGoodbye),
                    (T("You were sleeping.", "Você estava dormindo."), () => VivianSequence(vivian, 5, SayGoodbye,
                        ("I was awake!  {name}!", "Eu estava acordada, {name}!"),
                        ("I was awake and watching you the whole time!\nDidn't you notice?", "Eu fiquei acordada vendo você o tempo todo!\nVocê não percebeu?")))));
        }

        private void SetVivianPoolVisit(int visit)
        {
            TownSaveVariables.SetInt(VivianPoolDateKey, visit);
            if (visit >= 3)
            {
                var flags = TownRetailScriptFlags.CreatePersistent();
                flags.ClearBit(VivianPoolEventFlag);
                flags.SetBit(VivianPoolPresentFlag);
            }
            PersistTownProgress();
        }

        private void VivianWakeUpStory(NPCController vivian)
        {
            // TOWN.BIN 0x417B9B–0x417E53.
            VivianTalk(vivian, 3, "Ha, ha.\nHe looks adorable when sleeping.", "Ha, ha.\nEle fica tão fofo dormindo.", () =>
                VivianSequence(vivian, 0, () => VivianTalk(vivian, 1, "See you again, {name}!", "Até mais, {name}!"),
                    ("Good Morning.\nIt's a beautiful morning.", "Bom dia.\nQue manhã linda."),
                    ("Gooood Mornin'!\nHa, ha.", "Booom dia!\nHa, ha."),
                    ("We're getting big crowds everyday, thanks to you, {name}.", "Tem vindo muita gente todos os dias, graças a você, {name}."),
                    ("I'm headed for rehearsals again today.\nPlease come to the theater to see me.",
                        "Vou ensaiar de novo hoje.\nVenha me ver no teatro."),
                    ("If I knew you would be there to see me, {name}, I believe I could dance more graciously.",
                        "Se eu soubesse que você estaria lá para me ver, {name}, acho que dançaria com mais graça.")));
        }

        private void VivianComedyReaction(NPCController vivian, NPCController fon, bool finishedShow)
        {
            // TOWN.BIN 0x4B6A3F–0x4B6E8F.
            void Goodbye() => VivianTalk(vivian, 1, "You looked just fabulous on stage, {name}.\nSee you later.",
                "Você estava fabuloso no palco, {name}.\nAté mais.");
            void AskFon() => FonTalk(fon, "That's right.\nWould you consider becoming my stand-up partner?  Seriously.",
                "Isso mesmo.\nVocê toparia ser meu parceiro de comédia? Estou falando sério.", Goodbye);

            VivianAsk(vivian, 1, "Ha, ha, ha, ha, ha...\nThat was good.", "Ha, ha, ha, ha, ha...\nEssa foi boa.",
                (T("Great success!", "Foi um sucesso!"), () =>
                {
                    if (finishedShow)
                        VivianTalk(vivian, 1, "Seriously, you've got the knack for comedy.\nRight, Fon?",
                            "Sério, você leva jeito para comédia.\nNão é, Fon?", AskFon);
                    else
                        VivianTalk(vivian, 0, "Saying that it was a great success would be pushing it,\nbut as an amateur, you didn't give up.",
                            "Dizer que foi um sucesso seria exagero,\nmas, mesmo sendo amador, você foi até o fim.", Goodbye);
                }),
                (T("That sucked!", "Foi péssimo!"), () =>
                {
                    if (finishedShow)
                        VivianTalk(vivian, 0, "You're kidding.\nYou were just like a pro.\nRight, Fon?",
                            "Está brincando.\nVocê parecia um profissional.\nNão é, Fon?", AskFon);
                    else
                        VivianTalk(vivian, 0, "Yeah, you may be right.\nBut that's to be expected.\nYou were suddenly on stage with no preparation.",
                            "É, talvez você tenha razão.\nMas era de se esperar.\nVocê subiu ao palco de repente, sem ensaiar.", Goodbye);
                }));
        }
    }
}
