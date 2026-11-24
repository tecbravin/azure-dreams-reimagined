using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using AzureDreams.Core;
using CoreTurns = AzureDreams.Core.TurnSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController : MonoBehaviour
    {
        private enum Area { Town, Tower }

        [Header("Editable character prefabs and data")]
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private GameObject _creaturePrefab;
        [SerializeField] private GameObject _npcPrefab;
        [SerializeField] private ActorDefinition _playerDefinition;
        [SerializeField] private ActorDefinition _creatureDefinition;
        [SerializeField] private ActorDefinition _npcDefinition;
        [Header("Editable environment materials")]
        [SerializeField] private Material _groundMaterial;
        [SerializeField] private Material _wallMaterial;
        [SerializeField] private Material _accentMaterial;
        [SerializeField] private Camera _gameCamera;

        // --- Optional FX prefabs (loaded from Resources/FX if not assigned) ---
        [SerializeField] private GameObject _fxAttack;
        [SerializeField] private GameObject _fxMagic;
        [SerializeField] private GameObject _fxDust;
        private readonly List<GameObject> _generated = new List<GameObject>();
        private readonly HashSet<Vector2Int> _walls = new HashSet<Vector2Int>();
        private readonly List<Vector2Int> _npcCells = new List<Vector2Int>();
        private readonly List<ItemPickup> _townPickups = new List<ItemPickup>();
        [SerializeField] private string _runBinding = "<Keyboard>/leftShift";
        private InputAction _runAction;
        private InputAction _move;
        private InputAction _interact;
        private bool _towerInteractPending;
        private InputAction _wait;
        private InputAction _cancel;
        private InputAction _skipAction;
        private GameObject _player;
        /// <summary>Per-creature runtime state â€” supports multiple monsters per floor.</summary>
        private struct CreatureState
        {
            public GameObject View;
            public Vector2Int Cell;
            public int Health;
            public bool CarriesWaterMedal;
            public AzureDreams.Core.ActorRecord Actor;
            public AzureDreamsReimagined.Rendering.PsxSpriteRenderer Animation;
            public ActorLocomotionView Locomotion;
        }
        private readonly List<CreatureState> _creatures = new List<CreatureState>();
        private Vector2Int _playerCell;
        private Vector2Int _stairCell;
        private Vector2Int _gateCell;
        private Vector2Int _lootCell;
        private bool _hasLoot;
        private Area _area;
        private int _floor;
        private int _health;
        private int _gold;
        private int _runLoot;
        private int _townLevel = 1;
        private float _moveTimer;
        private const float MessageIdleSeconds = 5f;
        private const int MaxVisibleMessages = 4;
        private readonly List<string> _messageStack = new List<string>();
        private int _messageScroll;
        private int _messageRevision;
        private int _messageLineScroll = int.MaxValue;
        private float _messageStackActivity;
        private int _combatMessageLines;
        private string _message
        {
            get
            {
                if (_messageStack.Count == 0) return string.Empty;
                return _messageStack[Mathf.Clamp(_messageScroll, 0, _messageStack.Count - 1)];
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _messageRevision++;
                    _messageStack.Clear();
                    _messageScroll = 0;
                    return;
                }
                _messageRevision++;
                _messageStack.Add(value);
                _messageStackActivity = Time.unscaledTime;
                _messageScroll = _messageStack.Count - 1;
                _messageLineScroll = int.MaxValue;
            }
        }

        private bool HasVisibleMessage =>
            (IsShowingVisitorDialogue && !string.IsNullOrEmpty(_dialogueLine)) ||
            (_messageStack.Count > 0 &&
             (_activeTownNpc || _storySpeaker || KewneMeetingSpeaking || _townObjectDialogueOpen || _rowanOfferPending ||
              Time.unscaledTime - _messageStackActivity < MessageIdleSeconds));
        private bool _showControls;
        private bool _testInvulnerable;
        public bool IsTestInvulnerable => _testInvulnerable;
        private string _dialogueSpeaker;
        private string _dialogueLine;
        private string _visibleDialogue;
        private float _dialogueStarted;
        private Texture2D _dialoguePortrait;

        private void ShowTownDialogue(string speaker, string line, Texture2D portraitOverride)
        {
            _dialogueSpeaker = speaker;
            _dialogueLine = ResolveHeroName(line);
            _visibleDialogue = string.Empty;
            _dialogueStarted = Time.unscaledTime;
            _dialoguePortrait = portraitOverride;
            _dialogueTextScroll = Vector2.zero;
            if (!portraitOverride) FindDialoguePortrait(speaker);
        }

        private Texture2D FindDialoguePortrait(string speaker)
        {
            if (speaker == "Fon") return FonDialoguePortrait(_storyPortraitStarted);
            if (speaker == "Selfi") return StreamPortrait(SelfiPortrait(0));
            if (speaker == "Patty") return StreamPortrait(PattyPortrait(0));
            if (speaker is "Mia" or "Miya") return StreamPortrait(MiaPortrait(0));
            if (speaker == "Vivian") return StreamPortrait(VivianPortrait(0));
            if (speaker == "Fur") return StreamPortrait(FurPortrait(0));
            if (speaker == "Cherrl") return StreamPortrait(CherrlPortrait(0));
            if (speaker == "Nico") return StreamPortrait(NicoPortrait(NicoNeutral));
            if (speaker == "Wreath") return StreamPortrait(WreathPortrait);
            if (speaker == "Weedy") return StreamPortrait(WeedyPortrait);
            if (speaker == "Guy") return GuyDialoguePortrait(GuyPortrait);
            string name = speaker switch
            {
                // Silver uses the original elderly/Grandpa portrait rather
                // than falling back to the empty generic-resident portrait.
                "Silver" => "Grandpa",
                "Aunt" or "Ghosh" or "Grandpa" or "Jorda" or "Kewne" => speaker,
                _ => null
            };
            return name == null ? null : StreamPortrait($"Dialogue/{name}_variant0_frame0");
        }

        private static void DrawDialoguePortrait(Texture2D portrait, Rect box, bool rightAligned = false, float scale = 1f,
            float offset = 0f)
        {
            if (!portrait || Event.current.type != EventType.Repaint) return;
            var rect = DialoguePortraitRect(portrait, box, rightAligned, scale);
            rect.x += offset * rect.width;
            GUI.DrawTexture(rect, portrait, ScaleMode.ScaleToFit);
        }

        private static Rect DialoguePortraitRect(Texture2D portrait, Rect box, bool rightAligned = false, float scale = 1f)
        {
            float height = Mathf.Max(0f, Mathf.Min(134f * 3f, box.y - 8f)) * scale;
            float width = height * portrait.width / portrait.height;
            float x = rightAligned ? box.xMax - width - 8f : box.x + 8f;
            return new Rect(x, box.y - height, width, height);
        }

        private static void DrawDialoguePortraitPatch(Texture2D portrait, Texture2D patch, Rect box, Rect normalized)
        {
            Rect target = DialoguePortraitRect(portrait, box);
            var area = new Rect(target.x + normalized.x * target.width, target.y + normalized.y * target.height,
                normalized.width * target.width, normalized.height * target.height);
            var uv = new Rect(normalized.x, 1f - normalized.yMax, normalized.width, normalized.height);
            GUI.DrawTextureWithTexCoords(area, patch, uv, alphaBlend: true);
        }
        private GUIStyle _bodyStyle;
        private GUIStyle _messageStyle;
        private System.Random _random;

        // --- Inventory & familiar (port of Azure Dreams signature mechanics) ---
        private readonly Inventory _townInventory = new Inventory();
        private Inventory _runInventory;
        private ActorDefinition _familiarDefinition;
        private int _familiarHealth;
        private GameObject _familiar;
        [SerializeField] private GameObject _kewnePrefab;
        [SerializeField] private ActorDefinition _kewneDefinition;

        [SerializeField] private GameObject _pulunpaPrefab;
        [SerializeField] private GameObject _noisePrefab;
        [SerializeField] private GameObject _trollPrefab;

        [SerializeField] private GameObject _baloonPrefab;
        [SerializeField] private GameObject _flamePrefab;
        [SerializeField] private GameObject _cyclonePrefab;

        private GameObject MonsterPrefab(AzureDreams.Core.MonsterSpecies species)
        {
            var prefab = species switch
            {
                AzureDreams.Core.MonsterSpecies.PULUNPA => _pulunpaPrefab,
                AzureDreams.Core.MonsterSpecies.NOISE => _noisePrefab,
                AzureDreams.Core.MonsterSpecies.TROLL => _trollPrefab,
                AzureDreams.Core.MonsterSpecies.BALOON => _baloonPrefab,
                AzureDreams.Core.MonsterSpecies.FLAME => _flamePrefab,
                AzureDreams.Core.MonsterSpecies.CYCLONE => _cyclonePrefab,
                _ => null
            };
            if (prefab) return prefab;
            return RegisteredMonsterPrefab((int)species) ?? _creaturePrefab;
        }

        private bool _familiarWaiting;
        // Retail familiar control is lost once the familiar is more than six tiles away.
        // Keep this shared threshold in one place because both collars use the same rule.
        private const int FamiliarLostDistance = 6;
        private InputAction _familiarCommand;
        private GameObject SpawnFamiliar(bool pullFromBag = false, bool showSummoningEffect = true)
        {
            bool kewne = _kewneDefinition && _familiarDefinition == _kewneDefinition;
            if (kewne) _familiarDefinition = _kewneDefinition;
            _familiarActor.Species = _hatchedSpecies > 0 ? (byte)_hatchedSpecies :
                kewne ? (byte)AzureDreams.Core.MonsterSpecies.KEWNE : (byte)0;
            if (_familiarActor.Level == 0 && _familiarActor.Species != 0 &&
                TryGetLevelStats(_familiarActor.Species, 1, out var stats))
                ApplyFreshStats(_familiarActor, stats);
            if (_familiarActor.Species == 0 && _familiarActor.MaxMp == 0)
            {
                _familiarActor.MaxMp = _familiarActor.BaseMp = 100;
                _familiarActor.CurrentMp = 100;
            }
            var prefab = (kewne || _hatchedSpecies == (int)AzureDreams.Core.MonsterSpecies.KEWNE) && _kewnePrefab
                ? _kewnePrefab : _hatchedSpecies > 0 ? HatchedFamiliarPrefab(_hatchedSpecies) : _creaturePrefab;
            var familiar = SpawnActor(prefab,
                _familiarCell, "Familiar - " + FamiliarDisplayName, species: _familiarActor.Species,
                palette: FamiliarPalette(_familiarActor.Species, _familiarActor));
            if (showSummoningEffect && (_summoningSheet || pullFromBag))
                StartCoroutine(PresentFamiliarSummoning(familiar, pullFromBag));
            return familiar;
        }

        private bool IsFamiliarDistant()
        {
            if (!_familiar) return false;
            return Distance(_playerCell, _familiarCell) > FamiliarLostDistance;
        }
        private Vector2Int _familiarCell;
        private ItemDefinition _potionItem;
        private bool _rowanOfferPending;

        private const int MapSize = 12;

        public void Configure(GameObject playerPrefab, GameObject creaturePrefab, GameObject npcPrefab,
            ActorDefinition playerDefinition, ActorDefinition creatureDefinition, ActorDefinition npcDefinition,
            Material groundMaterial, Material wallMaterial, Material accentMaterial, Camera gameCamera)
        {
            _playerPrefab = playerPrefab;
            _creaturePrefab = creaturePrefab;
            _npcPrefab = npcPrefab;
            _playerDefinition = playerDefinition;
            _creatureDefinition = creatureDefinition;
            _npcDefinition = npcDefinition;
            _groundMaterial = groundMaterial;
            _wallMaterial = wallMaterial;
            _accentMaterial = accentMaterial;
            _gameCamera = gameCamera;
        }

        private void OnEnable()
        {
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            _wait = new InputAction("Wait", InputActionType.Button, "<Keyboard>/space");
            _cancel = new InputAction("Cancel", InputActionType.Button, "<Keyboard>/q");
            _move.Enable();
            _interact.Enable();
            _wait.Enable();
            _cancel.Enable();
            _mapAction = new InputAction("Map", InputActionType.Button, _mapBinding);
            _mapAction.Enable();
            _liftAction = new InputAction("Lift", InputActionType.Button, _liftBinding);
            _liftAction.Enable();
            _runAction = new InputAction("Run", InputActionType.Button, _runBinding);
            _runAction.Enable();
            _inventoryAction = new InputAction("Inventory", InputActionType.Button, _inventoryBinding);
            _inventoryAction.Enable();
            _familiarCommand = new InputAction("Familiar follow or wait", InputActionType.Button, "<Keyboard>/j");
            _familiarCommand.Enable();
            _skipAction = new InputAction("Skip turn", InputActionType.Button, "<Keyboard>/t");
            _skipAction.Enable();
            InitializeSettings();
            SubscribeTownEvents();
            SubscribeAssetMode();
        }

        private void OnDisable()
        {
            Shader.SetGlobalVector("_KohWallFadeTarget", Vector4.zero);
            ResetArcade(restorePlayer: true);
            ResetRacetrack();
            ResetRestaurantMeal();
            EndHomeBath();
            ReleaseAngelPresentation();
            ResetLevelUpCelebration();
            StopLevelUpMusic(resumeBackground: true);
            if (_titlePage == TitlePage.Opening) EndOpening();
            RestoreBlindnessView();
            if (_kohSprite)
            {
                _kohSprite.PresentStatusTint(false, false);
                _kohSprite.PresentParalysis(false);
                _kohSprite.PresentSleep(false);
            }
            ReleaseSettings();
            UnsubscribeTownEvents();
            UnsubscribeAssetMode();
            ReleaseReimaginedItemSprites();
            StopTowerEntrance();
            ResetElevatorAscent();
            if (_entranceAudio) _entranceAudio.Stop();
            if (_turnBusy) TickTurnPresentation(float.MaxValue);
            _move?.Dispose();
            _interact?.Dispose();
            _wait?.Dispose();
            _cancel?.Dispose();
            _skipAction?.Dispose();
            _skipAction = null;
            _familiarCommand?.Dispose();
            _familiarCommand = null;
            _inventoryAction?.Dispose();
            _inventoryAction = null;
            _inventoryOpen = false;
            _runAction?.Dispose();
            _runAction = null;
            _liftAction?.Dispose();
            _liftAction = null;
            _mapAction?.Dispose();
            _mapAction = null;
            ResetMinimap();
        }

        private void Start()
        {
            PrepareLevelUpMusic();
            PreloadCommonSounds();
            _message = T("Welcome to Monsbaiya. Find the gate to enter the Tower.", "Boas-vindas a Monsbaiya. Encontre o portão para entrar na torre.");
            if (!_gameCamera)
                _gameCamera = Camera.main;
            // Load optional FX prefabs from Resources/FX if not assigned.
            if (!_fxAttack) _fxAttack = AssetMode.Load<GameObject>("FX/FX_Lightning");
            if (!_fxMagic) _fxMagic = AssetMode.Load<GameObject>("FX/FX_Magic");
            if (!_fxDust) _fxDust = AssetMode.Load<GameObject>("FX/FX_Dust");
            // PS1-style fog
            AzureDreamsReimagined.Rendering.PsxFogSetup.ApplyLinearFog(
                new Color(0.05f, 0.05f, 0.12f), 8f, 30f);
            // Low-res render
            InstallCameraEffects();
            if (_gameCamera != null && _gameCamera.GetComponent<AzureDreamsReimagined.Rendering.PsxLowResRender>() == null)
                _gameCamera.gameObject.AddComponent<AzureDreamsReimagined.Rendering.PsxLowResRender>();
            if (_showTitleOnStart && gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene()) ShowTitle();
            else StartPlayableGame();
        }

        private void StartPlayableGame()
        {
            // Restore persisted progress. SaveSystem is the canonical save;
            // TownSaveVariables also receives live town events and acts as a
            // recovery source if the main save has not yet been written.
            if (HasAutomaticProgress)
            {
                int savedGold;
                int savedTownLevel;
                string savedFamiliar;
                int savedPotions;
                if (SaveSystem.HasSave)
                {
                    SaveSystem.Load(out savedGold, out savedTownLevel, out savedFamiliar, out savedPotions);
                }
                else
                {
                    savedGold = TownSaveVariables.Gold;
                    savedTownLevel = TownSaveVariables.TownLevel;
                    savedFamiliar = TownSaveVariables.GetString("familiar");
                    savedPotions = TownSaveVariables.GetItemCount("Healing Draught");
                }
                _gold = savedGold;
                _townLevel = savedTownLevel;
                if (!string.IsNullOrEmpty(savedFamiliar) && _creatureDefinition != null &&
                    _creatureDefinition.DisplayName == savedFamiliar)
                {
                    _familiarDefinition = _creatureDefinition;
                }
                if (_kewneDefinition && savedFamiliar == _kewneDefinition.DisplayName) _familiarDefinition = _kewneDefinition;
                if (savedPotions >= 0)
                {
                    if (_potionItem == null) _potionItem = CreatePotionDefinition();
                    _townInventory.AddItem(_potionItem, savedPotions);
                }
            }
            if (!HasAutomaticProgress && !TownSaveVariables.HasValue(TownReputation.HighestFloorKey))
                TownSaveVariables.SetInt(TownReputation.HighestFloorKey, 0);
            _townInventory.RestoreDungeonItems(TownSaveVariables.GetString("town_carried_dungeon_items"));
            _townStorage.RestoreDungeonItems(TownSaveVariables.GetString("town_stored_dungeon_items"));
            int storedPotions = TownSaveVariables.GetItemCount("town_storage_potions");
            if (storedPotions > 0)
            {
                if (!_potionItem) _potionItem = CreatePotionDefinition();
                _townStorage.AddItem(_potionItem, storedPotions);
            }
            int savedEggSpecies = TownSaveVariables.GetInt(ActiveEggSpeciesKey);
            if (savedEggSpecies > 0) EquipHatchedFamiliar(savedEggSpecies, 1);
            if (_familiarDefinition)
            {
                if (!RestoreStoredFamiliarActor(TownSaveVariables.GetString("traveling_familiar_record"), _familiarActor))
                    RestoreLegacyFamiliarProgression(_familiarActor, _familiarDefinition, _hatchedSpecies,
                        TownSaveVariables.GetInt(FamiliarExperienceKey));
                _familiarHealth = _familiarActor.Hp;
            }
            LoadBagRoster();
            EnterTown();
            var openedInterior = FindAnyObjectByType<TownInteriorScene>();
            if (openedInterior) AdoptInterior(openedInterior);
            else if (_startInTower) EnterTower();
        }

        private bool _applicationUnfocused;
        private int _focusChangedFrame = -1;

        private void OnApplicationFocus(bool focused)
        {
            _applicationUnfocused = !focused;
            _focusChangedFrame = Time.frameCount;
            if (!isActiveAndEnabled) return;

            _move?.Reset();
            _interact?.Reset();
            _towerInteractPending = false;
            _cancel?.Reset();
            _wait?.Reset();
            _runAction?.Reset();
            _liftAction?.Reset();
            _mapAction?.Reset();
            _kewneSkipHeld = 0f;
            _inventoryAction?.Reset();
            _familiarCommand?.Reset();
            ResetSettingsInput();
            if (focused)
            {
                UpdateCameraFollow(snap: true);
                return;
            }
            if (_mapView == MapView.Preview)
            {
                _mapView = MapView.Sidebar;
                _mapHeldSeconds = 0f;
            }
            if (_area == Area.Town)
            {
                ResetPush();
                if (_playerLocomotion) _playerLocomotion.Present(Vector3.zero, 0f, false);
            }
            else
            {
                ResetTowerPushHold();
            }
        }

        internal const float TowerAnimationSpeed = 0.85f;
        private const float TrapAnimationSpeed = 0.70f;
        private static float TowerAnimationDelta => Time.deltaTime * TowerAnimationSpeed;

        private float _fadeToBlack; // screen fade-to-black timer
        private float _fadeDuration; // screen fade-to-black duration

        private void Update()
        {
            TickLevelUpMusic();
            UpdateOutdoorSelfiMusic();
            if (_applicationUnfocused && Application.isFocused) OnApplicationFocus(true);
            if (_applicationUnfocused || _focusChangedFrame == Time.frameCount) return;
            if (_area == Area.Town)
            {
                if (_familiarDefinition) RestoreTownFamiliarVitals(_familiarActor);
                foreach (var familiar in _bagFamiliars) RestoreTownFamiliarVitals(familiar.Actor);
            }
            if (HandleTitleInput() || HandlePrologueInput() || _changingInterior || _wakeUpVisitorArriving || _wakeUpVisitorLeaving || _kewneSkipTransition || _goUpTransition || _goDownTransition || _kohCollapsePlaying) return;
            if (_player) _unsavedPlaySeconds += Time.unscaledDeltaTime;
            if (TickHomeBath(Time.unscaledDeltaTime) || _restaurantMealAnimating || _poolSceneAnimating || _cherrlWalkAnimating || _cherrlWindowAnimating || _nicoFountainAnimating) return;
            if (HandleBeldoVision() || HandleBeldoEncounter()) return;
            if (_elevatorAscending)
            {
                TickElevatorAscent(TowerAnimationDelta);
                return;
            }
            if (MonsterDenPlaying)
            {
                TickMonsterDen(TowerAnimationDelta);
                UpdateMinimap();
                return;
            }
            if (_townDoorOpening || _towerArrivalPlaying || _pullingFamiliarFromBag || _pickupView || _fusionPlaying || _familiarEvolutionsPlaying > 0) return;
            if (TickLevelUpCelebration(Time.unscaledDeltaTime)) return;
            if (_debugTrapPickerOpen) { HandleDebugTrapPickerInput(); return; }
            if (_debugItemPickerOpen) { HandleDebugItemPickerInput(); return; }
            if (_debugQuestPickerOpen) { HandleDebugQuestPickerInput(); return; }
            if (HandleTheaterAct() || HandleArcadeInput() || HandleGymInput() || HandleBowlingInput() || HandleRacetrackInput() || HandleCasinoInput() || HandleSettingsInput()) return;
            if (Keyboard.current != null && Keyboard.current.numpad7Key.wasPressedThisFrame)
            {
                _testInvulnerable = !_testInvulnerable;
                _message = _testInvulnerable ? T("Test invulnerability ON", "Invulnerabilidade de teste ativada") : T("Test invulnerability OFF", "Invulnerabilidade de teste desativada");
            }
            if (Keyboard.current != null && Keyboard.current.numpad8Key.wasPressedThisFrame)
            {
                int amount = Mathf.Min(10000, int.MaxValue - _gold);
                _gold += amount;
                PersistTownProgress();
                _message = T($"+{amount}G (test). Money: {_gold}G.", $"+{amount}G (teste). Dinheiro: {_gold}G.");
            }
            if (HandleGiveRecipientPicker() || HandleItemTargetPicker()) return;
            if (HandleHomeStorageInput() || HandleCheckpointInput() || HandleGroundItemPrompt() || HandleTrapRearmInput() || HandleTutorialInput() || HandleVisitorEvent()) return;
            if (HandleRestoredPoolEvents()) return;
            if (HandleNicoCheroArrival()) return;
            if (HandleOctopusDumplingStallInput() || HandleTownStory() || HandleCarpenterInput() || HandleGeneralStoreInput() || HandleBarryShopInput() || HandleMonsterShopInput() || HandleWeedyBookInput() || HandleKohHomeInput() || HandleInteriorInput()) return;
            if (HandleKewneMeeting() || TryStartKewneMeeting()) return;
            if (_area == Area.Tower && Keyboard.current?.numpad1Key.wasPressedThisFrame == true &&
                !_inventoryOpen && _inventoryMenu == InventoryMenu.Closed &&
                _towerRun?.CurrentState == TowerRunManager.State.InTower && _floor < int.MaxValue)
            {
                // Debug ascent skips the elevator animation, but keeps normal floor setup.
                // In particular, floor 2 must go to 3 rather than the sealed second tower.
                _ascendToSecondTower = false;
                StopEggWarming();
                CompleteElevatorAscent();
                return;
            }
            bool runHeld = _runAction != null && _runAction.IsPressed();
            bool passTurns = _area == Area.Tower && runHeld && _liftAction != null && _liftAction.IsPressed();
            bool wasBusy = _turnBusy;
            bool wasQuickStep = _quickTowerStep;
            bool wasRunningStep = _towerRunningStep;
            TickTurnPresentation(TowerAnimationDelta * (passTurns && _mapView != MapView.Full && !ThrowInFlight && !_spellPresentationStarted && _spellCues.Count == 0 ? 4f : 1f));
            UpdateMinimap();
            if (LevelUpCelebrationBlocksTurn) return;
            if (!wasBusy && HandleEggWarming()) return;
            if (_mapView == MapView.Full && !wasBusy)
            {
                if (!TryPassStatusBlockedTurn()) MoveInTower(ReadMovementDirection());
                return;
            }
            if (wasBusy)
            {
                // Chain held movement only. Attacks and menu commands still wait for a fresh frame.
                if ((wasQuickStep || wasRunningStep) && !_turnBusy && !MonsterDenPlaying && !passTurns && _area == Area.Tower && !_pickupView &&
                    !KohSkipsAction && !_groundItemPrompt.HasValue && _checkpointMenu == CheckpointMenu.Closed &&
                    !_inventoryOpen && _inventoryMenu == InventoryMenu.Closed && _faceAction?.IsPressed() != true && _interact?.IsPressed() != true &&
                    _interact?.WasReleasedThisFrame() != true &&
                    ((runHeld && TowerRoomsClearForRunning(_playerCell, _playerCell)) ||
                     !AnyCreatureWithin(_playerCell, CombatMovementRadius)))
                    MoveInTower(ReadMovementDirection());
                return;
            }
            if (TryPassStatusBlockedTurn()) return;
            if (HandleTowerInventory()) return;
            if (passTurns || (_area == Area.Tower && _skipAction?.IsPressed() == true))
            {
                _towerInteractPending = false;
                ResetTowerPushHold();
                TryTowerAction(Vector2Int.zero);
                return;
            }
            if (TryOpenDebugTrapPicker() || TryOpenDebugQuestPicker() || TryOpenDebugItemPicker()) return;
            if (_area==Area.Town && Keyboard.current?.tKey.wasPressedThisFrame==true && SummonAuraBike()) return;
            ReadCameraControls();
            Vector2 direction = ReadMovementDirection();
            if (_area == Area.Tower && HandleTowerInteractInput(direction, runHeld)) return;
            if (_area == Area.Town && _ridingAuraBike)
            {
                if (_interact?.WasPressedThisFrame() == true) { TryAuraBikeInteraction(); return; }
                if (MoveAuraBike(direction, Time.deltaTime)) return;
            }
            if (_area == Area.Town && Keyboard.current?.fKey.wasPressedThisFrame == true &&
                !_activeTownNpc && !_heldTownPot && _carriedItem == null && TryTownGroundItemCell(out _))
            {
                if (!TryCollectDroppedTownItem())
                    _message = T("There is no room in your inventory.", "Não há espaço no inventário.");
                return;
            }
            if (_area == Area.Town && _liftAction != null && _liftAction.WasPressedThisFrame())
            { if (_carriedItem != null) ReleaseTownInventoryItem(false); else if (!TryLiftTownInventoryItem()) TryTownPotLift(); return; }
            if (_area == Area.Town && _carriedItem != null && _attackAction?.WasPressedThisFrame() == true)
            { ReleaseTownInventoryItem(true); return; }
            if (_area == Area.Town && _heldTownPot && _attackAction?.WasPressedThisFrame() == true)
            { ThrowTownPot(); return; }
            if (_area == Area.Tower && !KohIsAsleep && _familiar && !IsFamiliarLost(_familiarActor) &&
                _familiarCommand != null && _familiarCommand.WasPressedThisFrame())
            {
                if (OrdersFor(_familiarActor).Ai == FamiliarAi.Wait) SetFamiliarAi(_familiarActor, FamiliarAi.Follow);
                else _familiarWaiting = !_familiarWaiting;
                _message = _familiarWaiting ? T("Your familiar will wait here.", "Seu familiar vai esperar aqui.") :
                    T("Your familiar will resume its orders.", "Seu familiar vai retomar suas ordens.");
                return;
            }
            if (_area == Area.Tower && !KohIsAsleep && _returnAction?.WasPressedThisFrame() == true)
            { ReturnToTown(true); return; }
            if (_area == Area.Tower && _liftAction != null && _liftAction.WasPressedThisFrame())
            { TryTowerLift(); return; }
            if (_area == Area.Tower && _attackAction?.WasPressedThisFrame() == true)
            { TryTowerAttack(_towerFacing); return; }
            if (_area == Area.Town && _townFlow &&
                _townFlow.State.Current == TownGameState.State.TownShop &&
                _cancel != null && _cancel.WasPressedThisFrame())
            { CancelRowanPurchase(); return; }
            if (_area == Area.Town && _interact?.WasPressedThisFrame() == true) { Interact(); return; }
            if (_area == Area.Tower && _wait != null && _wait.WasPressedThisFrame())
            { TryTowerAction(Vector2Int.zero); return; }
            if (_area == Area.Town)
            {
                MoveInTown(direction, Time.deltaTime, _autorun ? !runHeld : runHeld);
                if (direction != Vector2.zero && _townJumpHeight <= TownGroundTolerance) TryExitInterior();
                return;
            }
            MoveInTower(direction);
        }

        private Vector2 ReadMovementDirection() =>
            CameraRelativeMovement(_move != null ? _move.ReadValue<Vector2>() : Vector2.zero);

        private bool HandleTowerInteractInput(Vector2 direction, bool pushHeld)
        {
            // Defer a tap until release so a held Interact never triggers the tile
            // interaction before the player has a chance to pick a facing direction.
            if (_interact.WasPressedThisFrame()) _towerInteractPending = true;
            bool interactHeld = _interact.IsPressed();
            if (interactHeld)
            {
                if (direction.sqrMagnitude >= 0.5f)
                {
                    _towerInteractPending = false;
                    FaceTowerDirection(direction);
                    if (Mathf.Abs(_towerFacing.x) + Mathf.Abs(_towerFacing.y) == 1)
                        TickTowerPush(_towerFacing, Time.deltaTime, stayInPlace: true);
                    else ResetTowerPushHold();
                    return true;
                }
                if (pushHeld)
                {
                    _towerInteractPending = false;
                    if (Mathf.Abs(_towerFacing.x) + Mathf.Abs(_towerFacing.y) == 1)
                        TickTowerPush(_towerFacing, Time.deltaTime, stayInPlace: true);
                    return true;
                }
            }
            if (interactHeld)
            {
                ResetTowerPushHold();
                return false;
            }
            bool interact = _towerInteractPending && _interact.WasReleasedThisFrame();
            _towerInteractPending = false;
            if (interact)
            {
                ResetTowerPushHold();
                Interact();
            }
            return interact;
        }

        private void FaceTowerDirection(Vector2 direction)
        {
            // Facing accepts all eight front-tile directions.
            Vector2 normalized = direction.normalized;
            TryTowerFace(new Vector2Int(Mathf.RoundToInt(normalized.x), Mathf.RoundToInt(normalized.y)));
        }

        private void MoveInTower(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.5f)
            {
                ResetTowerPushHold();
                return;
            }
            if (_faceAction?.IsPressed() == true)
            {
                ResetTowerPushHold();
                FaceTowerDirection(direction);
                return;
            }
            Vector2 normalized = direction.normalized;
            Vector2Int step = new(Mathf.RoundToInt(normalized.x), Mathf.RoundToInt(normalized.y));
            TryTowerFace(step);
            if (CreatureAt(_playerCell + step) >= 0)
            {
                TickTowerPush(step, Time.deltaTime, stayInPlace: false);
                return;
            }
            ResetTowerPushHold();
            TryTowerAction(step);
        }

        private void Interact()
        {
            if (_area == Area.Town && _townJumpHeight > 0f) return;
            if (_turnBusy || (_area == Area.Tower && KohIsAsleep)) return;
            if (_area == Area.Tower && CanAscendFromFloor && _playerCell == _stairCell && !_carriedView)
            {
                _checkpointMenu = CheckpointMenu.Ascend;
                return;
            }
            if (_area == Area.Tower && TrySecondTowerElevator(fromInteract: true)) return;
            if (TryTowerEventInteraction() || TryInspectGroundItem() || TryOpenTrapRearm()) return;

            if (_area == Area.Town)
            {
                if (_townFlow && _townFlow.State.Current == TownGameState.State.TownShop)
                {
                    if (_cancel != null && _cancel.WasPressedThisFrame())
                    {
                        CancelRowanPurchase();
                        return;
                    }
                    ConfirmRowanPurchase();
                    return;
                }
                if (_activeTownNpc && TryTownConversation()) return;
                if (TryAuraBikeInteraction()) return;
                if (_townObjectDialogueOpen && TryTownObjectInteraction()) return;
                if (TryTownGateInteraction()) return;
                if (TryCollectDroppedTownItem() || TryCollectNearbyTownItem()) return;
                if (TryTownObjectInteraction()) return;
                if (TryBowlingInteraction() || TryCasinoInteraction() || TryMonsterShopCounter()) return;
                if (TryConstructionEntrance()) return;
                if (TryKohHomeInteraction()) return;
                if (TryTownConversation()) return;
            }
            else
            {
                // A successful potion use consumes exactly one shared tower turn.
                if (_runInventory != null && _potionItem != null && _runInventory.HasItem(_potionItem, 1))
                {
                    if (TryTowerUseItem(_potionItem))
                        _message = T("You drink a medicinal herb as the tower takes its turn.", "Você usa uma erva medicinal enquanto os monstros agem.");
                }
            }
        }

        private ItemDefinition CreatePotionDefinition()
        {
            var potion = ScriptableObject.CreateInstance<ItemDefinition>();
            potion.Configure("Medicinal Herb", ItemDefinition.ItemType.Heal, 20, 20, "Restores 20 HP.");
            potion.SetSaveId("Healing Draught");
            return potion;
        }

        private void EnterTown()
        {
            if (_area == Area.Tower && _towerRun != null &&
                (_towerRun.CurrentState == TowerRunManager.State.InTower ||
                 _towerRun.CurrentState == TowerRunManager.State.Defeat ||
                 _towerRun.CurrentState == TowerRunManager.State.Victory))
            {
                ReturnToTown(_health > 0 && _towerRun.CurrentState != TowerRunManager.State.Defeat);
                return;
            }
            _area = Area.Town;
            _blueCollarFamiliar = null;
            _cameraYawOffset = 0f; // reset camera orientation when returning to town
            if (TryGetLevelStats(0, Mathf.Max(1, _playerActor.Level), out var townStats))
            {
                _playerActor.BaseHp = _playerActor.MaxHp = _playerActor.Hp = StatByte(townStats.hp);
                _playerActor.BaseAttack = _playerActor.CurrentAttack = _playerActor.EffectiveAttack = StatByte(townStats.atk);
                _playerActor.BaseDefense = _playerActor.CurrentDefense = _playerActor.EffectiveDefense = StatByte(townStats.def);
            }
            PrepareTownFamiliarRoster();
            if (_prologue == Prologue.Off) PlayAreaMusic("Town");
            _townInventory.IdentifyAllDungeonItems();
            _townStorage.IdentifyAllDungeonItems();
            if (_townInventory.StoreOverflow(_townStorage, Inventory.SlotCapacity, HomeStorageCapacity) > 0) PersistTownProgress();
            _familiarDefeated = false;
            AzureDreams.Core.TurnSystem.SetMode(AzureDreams.Core.TurnSystem.Mode.Free);
            _floor = 0;
            _runLoot = 0;
            _health = _playerDefinition ? _playerDefinition.MaxHealth : 20;
            _runInventory = null;
            ClearGenerated();
            BuildTownMap();
            _playerCell = UsingRetailTown ? new Vector2Int(64, 43) : new Vector2Int(5, 5);
            _townFacing = Vector2.up;
            _player = SpawnKoh(_playerCell, "Player - editable prefab");
            if (UsingRetailTown) _player.transform.position = RetailTownActorPosition(_player.transform.position, 0.55f);
            if (_returnTownAtGate)
            {
                _returnTownAtGate = false;
                SetTownReturnSpawn();
            }
            _npcCells.Clear();
            if (!UsingRetailTown)
            {
                var rowanCell = new Vector2Int(9, 4);
                _npcCells.Add(rowanCell);
                var rowanPrefab = AssetMode.Load<GameObject>("Town/NPC_Rowan");
                var rowan = Spawn(rowanPrefab ? rowanPrefab : _npcPrefab, rowanCell, "Townsperson - Rowan");
                rowan.transform.position = new Vector3(rowan.transform.position.x, 0f, rowan.transform.position.z);
            }
            if (UsingRetailTown)
            {
                var gate = new GameObject("Tower Gate");
                gate.transform.SetParent(transform, false);
                gate.transform.position = new Vector3(_gateCell.x, 0f, _gateCell.y);
                _generated.Add(gate);
            }
            else
            {
                CreateTownAssetOrFallback("Tower_Gate", "Tower Gate", new Vector3(_gateCell.x, 0f, _gateCell.y),
                    Quaternion.identity, PrimitiveType.Cylinder, Vector3.one * 0.62f, _accentMaterial, false);
            }
            ConfigurePlayerView();
            UpdateCameraFollow(snap: true);
            InitializeTownInteractions();
            PlaceKohHomeEntrance();
            SpawnOutdoorStoryResidents();
        }

        private void EnterTower()
        {
            TownSaveVariables.SetBool(WreathFruitAvailable, true);
            TownSaveVariables.SetBool(WeedyFruitGivenKey, false);
            TownSaveVariables.Save();
            TownEventBus.Publish(TownEventBus.EventId.PlayerLeftTown);
            bool enteringWithKewne = _kewneTutorialChoice.HasValue;
            if (enteringWithKewne) _useGeneratedTowerFloors = true;
            _area = Area.Tower;
            AzureDreams.Core.TurnSystem.SetMode(AzureDreams.Core.TurnSystem.Mode.TurnBased);
            _floor = 1;
            _runLoot = 0;
            if (!_familiarDefinition && _bagFamiliars.Count > 0) ActivateBagFamiliar(_bagFamiliars[0]);
            _familiarInBag = _familiarDefinition;
            if (_familiarInBag)
            {
                _familiarHealth = FamiliarMaxHealth;
                _familiarActor.Hp = StatByte(_familiarHealth);
            }
            bool metAtGate = _kewneTutorialChoice.HasValue;
            _tutorialRun = _kewneTutorialChoice ??
                (_useGeneratedTowerFloors && !_familiarDefinition && NeedsKewneMeeting);
            _kewneTutorialChoice = null;
            _tutorialHintsSeen = 0;
            _starterJoinedThisRun = metAtGate;
            if (metAtGate)
            {
                var kewne = new BagFamiliar { Definition = _kewneDefinition, Actor = new AzureDreams.Core.ActorRecord(), Nickname = _kewneNameDraft };
                _bagFamiliars.Add(kewne);
                ActivateBagFamiliar(kewne);
                _familiarInBag = _kewneSkipTransition;
                if (_familiarInBag && TryGetLevelStats((int)AzureDreams.Core.MonsterSpecies.KEWNE, 1, out var stats))
                {
                    _familiarActor.Species = (byte)AzureDreams.Core.MonsterSpecies.KEWNE;
                    ApplyFreshStats(_familiarActor, stats);
                    _familiarHealth = _familiarActor.Hp;
                }
            }
            if (!_tutorialRun && !_familiarDefinition && _kewneDefinition && !TownSaveVariables.GetBool("starter_kewne_received"))
            {
                _familiarDefinition = _kewneDefinition;
                _starterJoinedThisRun = true;
            }
            BeginManagedTowerRun();
            BuildTowerFloor();
            if (!_enteringViaTrigger) TownEventBus.Publish(TownEventBus.EventId.PlayerEnteredTower);
            if (!_kewneSkipTransition) StartCoroutine(PlayTowerArrival());
            else
            {
                // Holding cancel through the Kewne intro (SkipKewneMeeting) calls EnterTower
                // with _kewneSkipTransition already true, which also skips PlayTowerArrival -
                // the walk-in cutscene that normally opens the floor 1 inner tower door and
                // then clears _innerTowerDoor so CrossesClosedTowerDoor stops blocking it.
                // BuildTowerFloor above still creates that door closed either way, so without
                // this it stayed shut and impassable (both to Koh and monsters) for the rest
                // of the run whenever the intro was skipped. Finish it instantly instead.
                if (_innerTowerDoor)
                {
                    _innerTowerDoor.SetOpen(1f);
                    _innerTowerDoor = null;
                }
            }
        }

        private void ReturnToTown(bool survived)
        {
            if (!FinishManagedTowerRun(survived)) return;
            TownEventBus.Publish(TownEventBus.EventId.PlayerReturnedFromTower, survived);
            _returnTownAtGate = true;
            string returnMessage = _message;
            EnterTown();
            StartCoroutine(LoadInterior(CurrentHomeScene(), HomeReturnPosition(), survived, returnMessage));
        }


        private void BuildTowerFloor()
        {
            // Every floor transition starts a fresh log, including elevator and trap travel.
            _message = string.Empty;
            _messageLineScroll = int.MaxValue;
            _combatMessageLines = 0;
            _messageStackActivity = 0f;
            // The second tower (floors 41+) does not count towards the main tower's record.
            if (_floor <= TowerFloorRules.BeldoFloor && _floor > TownSaveVariables.GetInt(TownReputation.HighestFloorKey))
                TownSaveVariables.SetInt(TownReputation.HighestFloorKey, _floor);
            if (_floor <= TowerFloorRules.BeldoFloor) UnlockBarryStockForFloor(_floor);
            _floorStartTurn = _completedTowerTurns;
            BeginSecondTowerFloor();
            PlayAreaMusic(TowerMusicCue(_floor));
            if (UsesManagedTowerLayout)
            {
                BuildManagedTowerFloor();
                return;
            }
            ClearGenerated();
            BuildBase();
            _npcCells.Clear();
            _playerCell = new Vector2Int(1, 1);
            _player = SpawnKoh(_playerCell, "Player - editable prefab");

            _random = new System.Random(173 + _floor * 997);
            for (int x = 2; x <= 9; x++)
            {
                for (int z = 2; z <= 9; z++)
                {
                    if ((x == 1 || z == 10) || _random.NextDouble() < 0.14)
                    {
                        Vector2Int cell = new Vector2Int(x, z);
                        if (cell != new Vector2Int(10, 10))
                            CreateWall(cell);
                    }
                }
            }
            _playerCell = new Vector2Int(1, 1);
            _player.transform.position = CellPosition(_playerCell);
            _stairCell = new Vector2Int(10, 10);
            // Bring the traveling familiar into the tower as a companion.
            if (_familiarDefinition != null && !_familiarDefeated && !_familiarInBag)
            {
                _familiarCell = FindArrivalFamiliarCell(false);
                _familiar = SpawnFamiliar();
                _familiarHealth = _floor == 1 ? FamiliarMaxHealth : Mathf.Min(_familiarHealth, FamiliarMaxHealth);
                _familiarActor.Hp = StatByte(_familiarHealth);
            }

            CreateTowerElevator();

            _creatures.Clear();
            RestoreBlueCollarSummon();
            RestoreSalamanderSummons();
            int creatureCount = TowerFloorRules.AllowsRandomMonsters(_floor) ? 1 + _floor / 2 : 0;
            creatureCount = _floor == 1 ? TowerEncounterTable.FirstFloorCount : System.Math.Min(creatureCount, 5);
            creatureCount = (creatureCount + 1) / 2;
            for (int c = 0; c < creatureCount; c++)
            {
                var candidates = new List<Vector2Int>();
                for (int x = 1; x < MapSize - 1; x++)
                    for (int y = 1; y < MapSize - 1; y++)
                    {
                        var cell = new Vector2Int(x, y);
                        if (_walls.Contains(cell) || cell == _playerCell || cell == _stairCell || (_familiar && cell == _familiarCell) || _thrownEggs.Exists(ally => ally.View && ally.Cell == cell)) continue;
                        bool occupied = false;
                        foreach (var other in _creatures) if (cell == other.Cell) { occupied = true; break; }
                        if (!occupied) candidates.Add(cell);
                    }
                if (candidates.Count == 0) break;
                Vector2Int cc = candidates[_random.Next(candidates.Count)];
                var species = TowerEncounterTable.Roll(_floor, _towerRun.Rng);
                var actor = CreateTowerEnemy(species);
                var cs = new CreatureState
                {
                    Cell = cc,
                    Actor = actor,
                    Health = actor.MaxHp,
                    View = SpawnActor(MonsterPrefab(species), cc, $"Tower creature {c + 1}: {species} - editable prefab",
                        species: (int)species, actionAtlas: EnemyTrollActionAtlas(actor)),
                };
                _creatures.Add(cs);
            }
            if (_floor == 2) SpawnTowerEventNpc();
            _lootCell = FindOpenCell(new Vector2Int(4, 7), _stairCell, _playerCell);
            foreach (var cs in _creatures)
                if (_lootCell == cs.Cell) _lootCell = FindOpenCell(new Vector2Int(4, 6), _stairCell, _playerCell);
            _hasLoot = true;
            _salvageMarker = CreateItemMarker(_lootCell, "Salvage", PrimitiveType.Cube);
            // Add item and trap markers on the non-generated floor.
            for (int i = 0; i < 3; i++)
            {
                Vector2Int itemCell = FindOpenCell(new Vector2Int(3 + i * 2, 5), _stairCell, _playerCell, _lootCell);
                _itemMarkers[itemCell] = CreateItemMarker(itemCell, "Tower Item", PrimitiveType.Sphere);
            }
            for (int i = 0; i < 1; i++)
            {
                Vector2Int trapCell = FindOpenCell(new Vector2Int(5 + i * 2, 8), _stairCell, _playerCell, _lootCell);
                if (_demoTraps.TryAdd(trapCell, new TowerTrapState())) CreateTrapMarker(trapCell);
            }
            ConfigurePlayerView();
            InitializeTowerEvents();
            if (_floor == 1)
                CreateInnerTowerDoor(_playerCell, Vector2Int.up, 1, addThreshold: false);
            UpdateCameraFollow(snap: true);
        }

        private void BuildBase()
        {
            _walls.Clear();
            _npcCells.Clear();
            _hasLoot = false;
            GameObject townGround = _area == Area.Town
                ? CreateTownAsset("TownGround", "Ground", new Vector3(5.5f, -0.18f, 5.5f), Quaternion.identity)
                : null;
            if (townGround)
                townGround.transform.localScale = new Vector3(MapSize, 0.36f, MapSize);
            else
            {
                var ground = CreatePrimitive(PrimitiveType.Cube, "Ground", new Vector3(5.5f, -0.18f, 5.5f),
                    new Vector3(MapSize, 0.36f, MapSize), _groundMaterial, true);
                if (_area == Area.Tower) _demoTowerGround = ground;
            }

            if (_area == Area.Town)
            {
                BuildTownPaths();
                for (int i = 0; i < MapSize; i++)
                {
                    CreateWall(new Vector2Int(0, i));
                    if (i != _gateCell.y) CreateWall(new Vector2Int(MapSize - 1, i));
                    CreateWall(new Vector2Int(i, 0));
                    CreateWall(new Vector2Int(i, MapSize - 1));
                }
            }
            else
            {
                for (int i = 0; i < MapSize; i++)
                {
                    CreateWall(new Vector2Int(0, i));
                    CreateWall(new Vector2Int(MapSize - 1, i));
                    if (_floor == 1 && i == 1) _walls.Add(new Vector2Int(i, 0));
                    else CreateWall(new Vector2Int(i, 0));
                    CreateWall(new Vector2Int(i, MapSize - 1));
                }
            }
        }

        private void AddTownLandmarks()
        {
            // Each footprint is derived from the actual prefab dimensions and
            // placement; movement and NPC patrols use these same rectangles.
            _townBuildings.Add(new Rect(0.625f, 1.025f, 2.75f, 2.05f));
            _townBuildings.Add(new Rect(6.625f, 1.025f, 2.75f, 2.05f));
            CreateTownAssetOrFallback("House_Inn", "Inn", new Vector3(2f, 0f, 2f), Quaternion.identity,
                PrimitiveType.Cube, new Vector3(2.5f, 0.9f, 1.8f), _wallMaterial, true);
            CreateTownAssetOrFallback("House_Market", "Market", new Vector3(8f, 0f, 2f), Quaternion.identity,
                PrimitiveType.Cube, new Vector3(2.5f, 0.9f, 1.8f), _wallMaterial, true);
            // The two front walks are the only authored routes through the
            // house frontage (x=2 and x=8). Keep their center cells open while
            // using the existing fence prefab to give the lots a readable edge.
            AddTownFrontFencePair("Inn", 2f, 4.15f);
            AddTownFrontFencePair("Market", 8f, 4.15f);
            // The original town is a settlement around the tower, not an empty
            // plaza. Use the third house prefab to make the outskirts legible.
            _townBuildings.Add(new Rect(0.625f, 7.025f, 2.75f, 2.05f));
            CreateTownAssetOrFallback("House_Residence", "Town Residence", new Vector3(2f, 0f, 8f), Quaternion.Euler(0f, 180f, 0f),
                PrimitiveType.Cube, new Vector3(2.5f, 0.9f, 1.8f), _wallMaterial, true);
        }

        private void AddTownFrontFencePair(string lotName, float entranceX, float z)
        {
            // This is a presentation pass over the current placeholder layout.
            // The decompilation has not yet yielded retail building coordinates.
            const float halfLotWidth = 1.05f;
            foreach (int side in new[] { -1, 1 })
            {
                float x = entranceX + side * halfLotWidth;
                var fence = CreateTownAsset("Fence_Section", lotName + " Front Fence " + side,
                    new Vector3(x, 0f, z), Quaternion.identity);
                if (fence) fence.isStatic = true;
            }
        }

        private void CreateWall(Vector2Int cell)
        {
            if (!_walls.Add(cell))
                return;
            if (_area == Area.Town)
            {
                Quaternion rotation = cell.x == 0 || cell.x == MapSize - 1
                    ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
                if (CreateTownAsset("Fence_Section", "Town Fence", new Vector3(cell.x, 0f, cell.y), rotation))
                    return;
            }
            CreatePrimitive(PrimitiveType.Cube, "Stone Wall", new Vector3(cell.x, 0.55f, cell.y), new Vector3(1f, 1.1f, 1f), WallVisibilityMaterial(_wallMaterial), true);
        }

        private GameObject CreateMarker(Vector2Int cell, string objectName, Material material, PrimitiveType shape, float scale,
            float height = 0.18f)
        {
            GameObject marker = CreatePrimitive(shape, objectName, new Vector3(cell.x, height + TowerGroundY(cell), cell.y), Vector3.one * scale, material, false);
            marker.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
            return marker;
        }

        private GameObject SpawnKoh(Vector2Int cell, string objectName) =>
            SpawnActor(_playerPrefab, cell, objectName, koh: true);

        private MonsterPaletteVariant FamiliarPalette(int species, AzureDreams.Core.ActorRecord record = null)
        {
            int element = record == null ? 0 : record.Flags14 & 7;
            return MonsterPalettes.ForElement(element is 1 or 2 or 4 ? element : UnitProfile(species)?.nativeElement ?? 0);
        }

        private GameObject SpawnActor(GameObject prefab, Vector2Int cell, string objectName, bool koh = false,
            int species = 0, MonsterPaletteVariant palette = MonsterPaletteVariant.Enemy, bool groundedIdle = false,
            Texture2D actionAtlas = null)
        {
            var actor = Spawn(prefab, cell, objectName);
            // Koh and familiar prefabs may acquire locomotion only after Spawn.
            // Attach depth correction to the actor path itself, not just NPCs
            // or prefabs which happen to serialize ActorLocomotionView.
            // Tower units use the same upright depth plane as town residents:
            // the head must not intersect scenery which is behind the feet.
            if (!actor.TryGetComponent<AzureDreamsReimagined.Rendering.ActorSpriteDepth>(out var depth))
                depth=actor.AddComponent<AzureDreamsReimagined.Rendering.ActorSpriteDepth>();
            depth.SetInterior(_area == Area.Town ? _interior : null);
            // Primitive fallbacks need the locomotion view to move their mesh off the root.
            if (!prefab) ConfigureLocomotion(actor);
            var feet = Vector3.down * TownActorFootOffset;
            float visualScale = koh ? RetailTownResidentSpriteView.KohVisualScale : 2f;
            foreach (Transform visual in actor.transform)
            {
                visual.localPosition = feet + (visual.localPosition - feet) * visualScale;
                visual.localScale *= visualScale;
            }
            if (!koh && !actor.TryGetComponent<KewneSpriteView>(out _))
                ConfigureLocomotion(actor).EnableMonsterFacing();
            if (!koh && species is >= 1 and <= 45 && !actor.TryGetComponent<KewneSpriteView>(out _))
            {
                var communityAtlas = AssetMode.Load<Texture2D>($"Monsters/Community/monster_{species:x2}");
                if (communityAtlas)
                {
                    var monsterView = actor.GetComponentInChildren<MonsterTurnSpriteView>();
                    if (!monsterView)
                    {
                        foreach (var oldRenderer in actor.GetComponentsInChildren<Renderer>()) oldRenderer.enabled = false;
                        var visual = new GameObject("Community Monster Sprite");
                        visual.transform.SetParent(actor.transform, false);
                        visual.transform.localScale = Vector3.one * visualScale;
                        visual.AddComponent<SpriteRenderer>();
                        monsterView = visual.AddComponent<MonsterTurnSpriteView>();
                        ConfigureLocomotion(actor).EnableMonsterFacing();
                    }
                    monsterView.UseCommunityAtlas(species, communityAtlas);
                }
            }
            if (_area == Area.Tower)
            {
                if (actor.TryGetComponent<KewneSpriteView>(out var kewne)) kewne.PlaybackSpeed = TowerAnimationSpeed;
                var monster = actor.GetComponentInChildren<MonsterTurnSpriteView>();
                if (monster) monster.PlaybackSpeed = TowerAnimationSpeed;
                foreach (var renderer in actor.GetComponentsInChildren<Rendering.PsxSpriteRenderer>())
                    renderer.PlaybackSpeed *= TowerAnimationSpeed;
                foreach (var animator in actor.GetComponentsInChildren<Animator>())
                    animator.speed *= TowerAnimationSpeed;
            }
            if ((groundedIdle || actionAtlas) && actor.GetComponentInChildren<MonsterTurnSpriteView>() is { } sprite)
            {
                sprite.GroundedIdle = groundedIdle;
                if (actionAtlas) sprite.SetActionAtlas(actionAtlas);
            }
            if (prefab && species > 0) MonsterPalettes.Apply(actor, species, palette);
            return actor;
        }

        private GameObject Spawn(GameObject prefab, Vector2Int cell, string objectName)
        {
            GameObject instance;
            if (prefab)
                instance = Instantiate(prefab, CellPosition(cell), Quaternion.identity);
            else
                instance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            instance.transform.position = CellPosition(cell);
            instance.transform.SetParent(transform, true);
            instance.name = objectName;
            if (instance.GetComponent<NPCController>() || instance.GetComponent<ActorLocomotionView>())
            {
                if (!instance.TryGetComponent<AzureDreamsReimagined.Rendering.ActorSpriteDepth>(out var depth))
                    depth = instance.AddComponent<AzureDreamsReimagined.Rendering.ActorSpriteDepth>();
                depth.SetInterior(_area == Area.Town ? _interior : null);
            }
            if (instance.TryGetComponent<NPCController>(out var resident) &&
                !RetailResidentBanks.Apply(instance, resident.npcId))
                OriginalTownSpriteView.Apply(instance, resident.npcId);
            // Only this controller may drive actor transforms.
            foreach (var driver in instance.GetComponentsInChildren<PlayerController>()) driver.enabled = false;
            foreach (var driver in instance.GetComponentsInChildren<MonsterAI>()) driver.enabled = false;
            foreach (var body in instance.GetComponentsInChildren<Rigidbody>()) body.isKinematic = true;
            foreach (var animator in instance.GetComponentsInChildren<Animator>()) animator.applyRootMotion = false;
            _generated.Add(instance);
            return instance;
        }

        private GameObject CreatePrimitive(PrimitiveType shape, string objectName, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            GameObject instance = GameObject.CreatePrimitive(shape);
            instance.transform.SetParent(transform, true);
            instance.name = objectName;
            instance.transform.position = position;
            instance.transform.localScale = scale;
            Renderer renderer = instance.GetComponent<Renderer>();
            if (renderer && material)
                renderer.sharedMaterial = material;
            if (!collider && instance.TryGetComponent<Collider>(out Collider hitbox))
                Destroy(hitbox);
            _generated.Add(instance);
            return instance;
        }

        private void CreateItemPickup(Vector2Int cell, string name, string persistenceId)
        {
            if (TownSaveVariables.GetBool("pickup_" + persistenceId)) return;
            var go = CreatePrimitive(PrimitiveType.Sphere, name, new Vector3(cell.x, 0.5f, cell.y),
                Vector3.one * (0.3f * WorldItemScale), _accentMaterial, false);
            var pickup = go.AddComponent<ItemPickup>();
            if (_potionItem == null) _potionItem = CreatePotionDefinition();
            pickup.item = _potionItem;
            pickup.amount = 1;
            pickup.persistenceKey = persistenceId;
            _townPickups.Add(pickup);
        }

        private bool TryCollectNearbyTownItem()
        {
            if (!_player) return false;
            ItemPickup nearest = null;
            float bestDistance = 1.25f * 1.25f;
            foreach (var pickup in _townPickups)
            {
                if (!pickup) continue;
                Vector3 delta = pickup.transform.position - _player.transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > bestDistance) continue;
                nearest = pickup;
                bestDistance = delta.sqrMagnitude;
            }
            if (!nearest || !nearest.TryCollect(_townInventory)) return false;
            _townPickups.Remove(nearest);
            string itemName = nearest.item == _potionItem ? T("Medicinal Herb", "Erva medicinal") : nearest.item.DisplayName;
            _message = T($"Collected {nearest.amount}× {itemName}.", $"Você pegou {nearest.amount}× {itemName}.");
            PersistTownProgress();
            return true;
        }

        private Vector2Int FindOpenCell(Vector2Int preferred, params Vector2Int[] blocked)
        {
            if (IsOpen(preferred, blocked))
                return preferred;
            for (int x = 1; x < MapSize - 1; x++)
                for (int z = 1; z < MapSize - 1; z++)
                {
                    Vector2Int cell = new Vector2Int(x, z);
                    if (IsOpen(cell, blocked))
                        return cell;
                }
            return new Vector2Int(8, 8);
        }

        private bool IsOpen(Vector2Int cell, Vector2Int[] blocked)
        {
            if (_walls.Contains(cell)) return false;
            for (int i = 0; i < blocked.Length; i++)
                if (cell == blocked[i]) return false;
            return true;
        }

        private bool InsideMap(Vector2Int cell) => cell.x > 0 && cell.x < ActiveMapSize - 1 && cell.y > 0 && cell.y < ActiveMapSize - 1;
        private Vector3 CellPosition(Vector2Int cell) => new Vector3(cell.x, 0.55f + TowerGroundY(cell), cell.y);

        /// <summary>Spawns a one-shot FX prefab at a world position (no-op if prefab missing).</summary>
        private void SpawnFx(GameObject prefab, Vector3 position, float? animationSpeed = null)
        {
            if (!prefab) return;
            var instance = Instantiate(prefab, position, Quaternion.identity);
            instance.transform.SetParent(transform, true);
            var ps = instance.GetComponentInChildren<ParticleSystem>();
            float lifetime = 2f;
            if (ps != null)
            {
                var main = ps.main;
                if (main.loop) main.loop = false;
                main.simulationSpeed *= animationSpeed ?? (_area == Area.Tower ? TowerAnimationSpeed : 1f);
                lifetime = (main.duration + main.startLifetime.constantMax) / Mathf.Max(0.01f, main.simulationSpeed) + 0.5f;
            }
            Destroy(instance, lifetime);
        }

        private void ClearGenerated()
        {
            ReleaseFractalBackdrop();
            ResetCherrlWalk();
            ResetHomeSlide();
            _familiarEvolutionVersion++;
            _familiarEvolutionsPlaying = 0;
            _damageNumbers.Clear();
            ResetArcade();
            ResetRestaurantMeal();
            EndHomeBath();
            ResetPoolPresentation();
            ResetLevelUpCelebration();
            ResetBowling();
            ResetGym();
            ResetRacetrack(preserveTransition: _raceScreen == RaceScreen.Loading);
            EndTheaterAct();
            if (_storySpeaker) CloseStoryPrompt();
            ResetBeldoEncounter();
            ResetConstructionView();
            RestoreBlindnessView();
            StopTowerEntrance();
            ResetElevatorAscent();
            ResetVisitorEvent();
            _ghoshDuelActive = false;
            ResetMonsterDen();
            _tutorialDialogue = 0;
            _tutorialKewne = null;
            _townKewne = null;
            ResetMinimap();
            ReleaseTowerTiles(_area == Area.Tower ? TowerTileLabel(_floor) : null);
            ResetCarry();
            ResetPush();
            _checkpointMenu = CheckpointMenu.Closed;
            _elevatorDeclined = false;
            ResetInventoryCommands();
            _debugItemPickerOpen = false;
            _debugTrapPickerOpen = false;
            _debugQuestPickerOpen = false;
            _heldTownPot = null;
            _townPots.Clear();
            _townJumpHeight = _townJumpVelocity = _townAirSpeedLimit = 0f;
            _townMomentum = Vector2.zero;
            _inventoryOpen = false;
            _inventoryEntries = null;
            _inventoryMenu = InventoryMenu.Closed;
            _pendingFamiliarCommands.Clear();
            _familiarPrompt = null;
            _finishFamiliarAction = null;
            _pullingFamiliarFromBag = false;
            _familiarWaiting = false;
            _turnsUntilReinforcement = 0;
            _townHeights = null;
            ClearTownInteractionReferences();
            _nicoFountainAnimating = false;
            _nicoFountainSceneVersion++;
            _nicoGhoshLeaving = false;
            _nicoGhoshMusic = false;
            _nicoFountainGhoshGuest = null;
            _cherrlDroppedToy = null;
            _cherrlWindowAnimating = false;
            if (_cherrlToySprite) Destroy(_cherrlToySprite);
            _cherrlToySprite = null;
            CancelTurn();
            _townBuildings.Clear();
            _townFacadeDoors.Clear();
            _facadeInteriors.Clear();
            _townPickups.Clear();
            if (_player) _player.SetActive(false);
            for (int i = 0; i < _generated.Count; i++)
                if (_generated[i]) Destroy(_generated[i]);
            _generated.Clear();
            _exhaustedViews.Clear();
            _frogViews.Clear();
            foreach (var mesh in _ownedManagedFloorMeshes)
                if (mesh) Destroy(mesh);
            _ownedManagedFloorMeshes.Clear();
            foreach (var material in _wallVisibilityMaterials.Values)
                if (material) Destroy(material);
            _wallVisibilityMaterials.Clear();
            _towerCutawayMesh = null;
            _towerWallVertices = null;
            _towerCutawayFaces.Clear();
            _player = null;
            _cameraFollowPlayer = null;
            _creatures.Clear();
            _familiar = null;
            StopEggWarming();
            _walls.Clear();
            _npcCells.Clear();
            _towerItemTraps = null;
            _itemMarkers.Clear();
            _trapMarkers.Clear();
            _sprungGoUpTraps.Clear();
            _demoTraps.Clear();
            _trapTerrainChanges.Clear();
            _trapReversalUntil = 0;
            _trapGoUp = false;
            _trapGoDown = false;
            _trapRearmCell = null;
            _trapDisarmCell = null;
            _groundItemPrompt = null;
            _pickupView = null;
        }

        private void RemoveGenerated(GameObject instance)
        {
            _generated.Remove(instance);
            if (instance) _exhaustedViews.Remove(instance);
            Destroy(instance);
        }

        private void OnGUI()
        {
            var previousSkin = GUI.skin;
            var previousMatrix = GUI.matrix;
            if (!_windowSkin) InitialiseWindowSkin(previousSkin);
            GUI.skin = _windowSkin;
            try
            {
                DrawDamageNumbers();
                DrawGameGui(previousMatrix);
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.skin = previousSkin;
            }
        }

        private void DrawGameGui(Matrix4x4 previousMatrix)
        {
            // Screen fade-to-black when transitioning to a new interior
            if (_fadeToBlack > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, _fadeToBlack);
                // This overload uses its explicit tint instead of GUI.color.  Keep the
                // transition genuinely black (including Koh's collapse) rather than
                // drawing a translucent white screen.
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0f, 0f, 0f, _fadeToBlack), 0f, 0f);
                GUI.color = Color.white;
                if (_fadeToBlack <= 0f) _fadeToBlack = 0f;
            }
            if (_bodyStyle == null)
            {
                _bodyStyle = new GUIStyle(GUI.skin.label)
                {
                    // Reduced from 17: OpenDyslexic's taller ascent/descent metrics
                    // were overflowing fixed-height row Rects sized for the old font
                    // (e.g. the Inventory list), causing visible top-of-glyph clipping.
                    fontSize = 15,
                    padding = new RectOffset(0, 0, 0, 0),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    richText = true,
                    normal = { textColor = Color.white }
                };
                _messageStyle = new GUIStyle(_bodyStyle) { fontSize = 13, wordWrap = true, fontStyle = FontStyle.Normal };
                _dialogueTextStyle = new GUIStyle(_messageStyle) { alignment = TextAnchor.UpperLeft, clipping = TextClipping.Clip };
                _logLineStyle = new GUIStyle(_messageStyle) { wordWrap = false, clipping = TextClipping.Clip };
                // Dialogue/log text stays the default white (set above). A couple of
                // message categories get their own color baked in via <color> rich-text
                // tags at the point the message is built (damage: red, see RecordTowerHit/
                // RecordTowerHazard) or their own tinted style (XP notice: blue, below) -
                // everything else is left plain white.
                _experienceNoticeStyle = new GUIStyle(_bodyStyle) { normal = { textColor = TowerStatusColors.Paralysis } };
            }

            float scale = Mathf.Max(0.5f, Mathf.Min(Screen.width / 640f, Screen.height / 480f));
            GUI.matrix = previousMatrix * Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            if (_titlePage != TitlePage.Closed)
            {
                DrawTitle(width, height);
                GUI.matrix = previousMatrix;
                return;
            }
            if (_prologue != Prologue.Off || _prologueBlack > 0f)
            {
                DrawPrologue(width, height);
                GUI.matrix = previousMatrix;
                return;
            }
            if (_kewneSkipTransition)
            {
                if (_area == Area.Town) DrawKewneSkipPrompt(width);
                DrawHudFill(new Rect(0f, 0f, width, height), new Color(0f, 0f, 0f, _kewneSkipFade));
                GUI.matrix = previousMatrix;
                return;
            }
            DrawGym(width, height);
            if (DrawTheaterAct(width, height) || DrawArcade(width, height) || DrawBowling(width, height) || DrawRacetrack(width, height) || DrawCasino(width, height))
            {
                GUI.matrix = previousMatrix;
                return;
            }
            DrawKewneSkipPrompt(width);
            if (_elevatorAscending)
            {
                float fade = Mathf.InverseLerp(0.70f, ElevatorAscentDuration, ElevatorTravelTime);
                DrawHudFill(new Rect(0f, 0f, width, height), new Color(0f, 0f, 0f, fade));
                if (_quakeAscending)
                {
                    var box = new Rect(30f, height - 94f, width - 60f, 66f);
                    DrawDialogueFrame(box);
                    DrawHudText(new Rect(box.x + 13f, box.y + 6f, box.width - 26f, box.height - 12f), _message, _messageStyle);
                }
                GUI.matrix = previousMatrix;
                return;
            }
            if (_townDoorOpening)
            {
                DrawTowerEntranceOverlay(width, height);
                GUI.matrix = previousMatrix;
                return;
            }
            if (_debugTrapPickerOpen)
            {
                DrawDebugTrapPicker(width, height);
                GUI.matrix = previousMatrix;
                return;
            }
            if (_debugItemPickerOpen)
            {
                DrawDebugItemPicker(width, height);
                GUI.matrix = previousMatrix;
                return;
            }
            if (_debugQuestPickerOpen)
            {
                DrawDebugQuestPicker(width, height);
                GUI.matrix = previousMatrix;
                return;
            }
            if (_settingsOpen)
            {
                DrawSettingsPanel(width, height);
                GUI.matrix = previousMatrix;
                return;
            }
            if (_area == Area.Tower)
                DrawRetailTowerHud(width);
            else
            {
                DrawTownMoneyHud(width);
            }

            if (_towerArrivalPlaying)
            {
                DrawTowerEntranceOverlay(width, height);
                GUI.matrix = previousMatrix;
                return;
            }
            DrawFusionStatusWindows(width);
            if (HasVisibleMessage) DrawMessageWindow(width, height);
            if (_showControls)
            {
                var box = new Rect(14f, 42f, Mathf.Min(450f, width - 28f), 134f);
                DrawHudPanel(box);
                DrawHudText(new Rect(box.x + 10f, box.y, box.width - 20f, box.height), ControlsHint(), _messageStyle);
            }
            if (_area == Area.Tower && Time.unscaledTime < _experienceNoticeUntil)
                DrawHudText(new Rect(width * 0.5f - 100f, height * 0.38f, 240f, 24f), _experienceNotice, _experienceNoticeStyle);
            if (_homeStorageOpen)
            {
                DrawHomeStorage(width, height);
                GUI.matrix = previousMatrix;
                return;
            }
            DrawTowerInventory(width, height);
            DrawInteriorMenu();
            DrawKohHomePanel(width, height);
            DrawWeedyBook();
            DrawMonsterShop();
            DrawBarryShop();
            DrawGeneralStore();
            DrawOctopusDumplingStall();
            DrawCarpenter();
            DrawTutorial(width, height);
            DrawCheckpointMenu(width, height);
            DrawBeldoDialogue(width, height);
            DrawTrapRearm(width, height);
            DrawGroundItemPrompt(width, height);

            DrawGiveRecipientPicker(width,height);
            GUI.matrix = previousMatrix;
            if (!_inventoryOpen) DrawMinimap();
        }

        private const float DialogueChoiceRowHeight = 28f;
        private Vector2 _dialogueTextScroll;

        private void DrawMessageWindow(float width, float height)
        {
            // Was a flat 21 (tuned for LiberationSans at this style's 15pt size). OpenDyslexic
            // is a visibly taller face - bigger x-height, chunkier ascenders/descenders - so a
            // fixed 21 clipped the bottom of its descenders (g/y/p/j) against _logLineStyle's
            // TextClipping.Clip, and let wrapped lines sit closer together than the glyphs
            // actually need. Measure the real line height from whatever font is active instead
            // of a magic number, so this keeps working if the font changes again later. "Ag"
            // probes both an ascender and a descender in one go; floor it at the old 21 so
            // nothing shrinks below what already fit.
            float logLineHeight = Mathf.Max(21f, _logLineStyle.CalcHeight(new GUIContent("Ag"), 1000f));
            const float inset = 13f;
            const float padding = 9f;
            const float sliderWidth = 14f;
            const float choiceGap = 6f;
            float boxWidth = width * 0.87f;
            float textWidth = boxWidth - inset * 2f;
            bool speaking = ((_area == Area.Town && (_activeTownNpc || _storySpeaker || KewneMeetingSpeaking)) ||
                IsShowingVisitorDialogue) && _dialogueLine != null;
            bool storyChoices = _storySpeaker && _storyChoices is { Length: > 0 };
            bool visitorChoices = _visitorStage is VisitorStage.Choice or VisitorStage.Challenge;
            int choiceRows = visitorChoices ? 2 : KewneMeetingSpeaking ? (KewneMeetingHasChoices ? 2 : 1) : 0;
            float maxHeight = height * 0.65f;
            // A story prompt with a single "Continue"/"Continuar" option has nothing to
            // actually choose - it is just acknowledging a line - so it gets the small
            // lifted-page-corner icon in DrawContinuePrompt instead of a full-width choice
            // button bar, matching how the original handled a plain "press to advance" cue.
            bool kewneContinue = KewneMeetingSpeaking && !KewneMeetingHasChoices;
            bool singleContinueChoice = kewneContinue || (storyChoices && _storyChoices.Length == 1 &&
                _storyChoices[0].Label == T("Continue", "Continuar"));
            float choicesHeight = singleContinueChoice
                ? 0f
                : storyChoices
                    ? Mathf.Min(MeasureStoryChoices(textWidth), Mathf.Min(VisibleStoryChoices * DialogueChoiceRowHeight, maxHeight * 0.72f))
                    : choiceRows * DialogueChoiceRowHeight;
            float reservedHeight = padding * 2f + WindowDesignSystem.Footer + (choicesHeight > 0f ? choiceGap + choicesHeight : 0f);

            if (speaking)
            {
                // Suppress the inline "›" advance cue for the single-Continue case too -
                // DrawContinuePrompt below draws its own icon, so showing both would be redundant.
                string fullLine = _dialogueLine;
                float fullHeight = Mathf.Max(logLineHeight, _dialogueTextStyle.CalcHeight(new GUIContent(fullLine), textWidth) + 2f);
                float textHeight = Mathf.Min(fullHeight, Mathf.Max(1f, maxHeight - reservedHeight));
                float panelHeight = Mathf.Max(height * 0.23f, textHeight + reservedHeight);
                var panel = new Rect(width * 0.065f, height * 0.96f - panelHeight, boxWidth, panelHeight);
                DrawDialogueFrame(panel, dialoguePole: true);
                DrawMessagePortrait(panel);

                int count = Mathf.Clamp(Mathf.FloorToInt((Time.unscaledTime - _dialogueStarted) * 42f), 0, _dialogueLine.Length);
                if (_visibleDialogue.Length != count) _visibleDialogue = _dialogueLine.Substring(0, count);
                string line = count == _dialogueLine.Length ? fullLine : _visibleDialogue;
                var textRect = new Rect(panel.x + inset, panel.y + padding, textWidth, textHeight);
                DrawDialogueText(textRect, line, fullLine);

                if (singleContinueChoice)
                {
                    DrawContinuePrompt(panel, kewneContinue ? () => AdvanceKewneMeeting(_kewneMeetingSelection) : null,
                        WindowDesignSystem.ShowContinuation(kewneContinue || _storyChoices[0].Apply != null, count == _dialogueLine.Length));
                    return;
                }
                var choices = new Rect(textRect.x, panel.yMax - padding - WindowDesignSystem.Footer - choicesHeight, textWidth, choicesHeight);
                // GUI choices execute immediately and can close/replace the dialogue
                // (the pool changing room clears _dialogueLine). Finish reading this
                // page's state before invoking any of those callbacks.
                bool hasNext = (_activeTownNpc && _activeTownNpc.HasDialogueContinuation) ||
                    (IsShowingVisitorDialogue && _visitorLine + 1 < CurrentVisitorScript.Length);
                bool showContinuation = WindowDesignSystem.ShowContinuation(hasNext, count == fullLine.Length, storyChoices || visitorChoices);
                if (storyChoices) DrawStoryChoices(choices);
                else if (visitorChoices) DrawVisitorChoices(choices);
                else if (KewneMeetingSpeaking) DrawKewneMeetingChoices(choices);
                if (showContinuation)
                    DrawWindowPageCorner(panel);
                return;
            }

            var lines = WrapTowerLogLines(_messageStack, textWidth, _logLineStyle);
            if (lines.Count > MaxVisibleMessages)
            {
                textWidth -= sliderWidth + choiceGap;
                lines = WrapTowerLogLines(_messageStack, textWidth, _logLineStyle);
            }
            int visibleCount = Mathf.Min(lines.Count, MaxVisibleMessages);
            float boxHeight = Mathf.Max(height * 0.23f, visibleCount * logLineHeight + padding * 2f);
            var box = new Rect(width * 0.065f, height * 0.96f - boxHeight, boxWidth, boxHeight);
            DrawDialogueFrame(box, dialoguePole: true);
            int maxScroll = Mathf.Max(0, lines.Count - visibleCount);
            _messageLineScroll = Mathf.Clamp(_messageLineScroll, 0, maxScroll);
            if (maxScroll > 0)
            {
                var slider = new Rect(box.xMax - sliderWidth - 8f, box.y + padding, sliderWidth, visibleCount * logLineHeight);
                _messageLineScroll = Mathf.RoundToInt(GUI.VerticalSlider(slider, _messageLineScroll, 0f, maxScroll));
            }
            for (int i = 0; i < visibleCount; i++)
                DrawHudText(new Rect(box.x + inset, box.y + padding + i * logLineHeight, textWidth, logLineHeight),
                    lines[_messageLineScroll + i], _logLineStyle, dialogue: true);
        }

        private void DrawMessagePortrait(Rect box)
        {
            var portrait = VisitorDialoguePortrait();
            var storyPortrait = StoryDialoguePortrait();
            var scopedPortrait = portrait ? portrait : storyPortrait;
            var displayedPortrait = scopedPortrait ? scopedPortrait : _dialoguePortrait ? _dialoguePortrait :
                FindDialoguePortrait(_dialogueSpeaker);
            if (_storyPortraitPath == FonPortrait || _dialogueSpeaker == "Fon")
                displayedPortrait = FonDialoguePortrait(_storyPortraitStarted);
            if (_storySpeaker && _storySpeaker.npcId == "chero" || _dialogueSpeaker == "Chero")
                displayedPortrait = CheroDialoguePortrait();
            DrawDialoguePortrait(displayedPortrait, box, offset: GhoshDialoguePortraitOffset(scopedPortrait));
            if (!portrait) DrawWeedyBookBlink(storyPortrait, box);
            if (!portrait && _storyPortraitPath == JordaPortrait)
                DrawJordaBlink(storyPortrait, box, _storyPortraitStarted);
            if (string.IsNullOrEmpty(_dialogueSpeaker)) return;
            DrawSpeakerWindow(box, _dialogueSpeaker);
        }

        private void DrawDialogueText(Rect viewport, string line, string fullLine)
        {
            if (_dialogueTextStyle.CalcHeight(new GUIContent(fullLine), viewport.width) <= viewport.height)
            {
                DrawHudText(viewport, line, _dialogueTextStyle, dialogue: true);
                return;
            }

            float textWidth = viewport.width - 18f;
            float contentHeight = _dialogueTextStyle.CalcHeight(new GUIContent(fullLine), textWidth) + 2f;
            var content = new Rect(0f, 0f, textWidth, contentHeight);
            _dialogueTextScroll = GUI.BeginScrollView(viewport, _dialogueTextScroll, content);
            DrawHudText(content, line, _dialogueTextStyle, dialogue: true);
            GUI.EndScrollView();
        }

        private int DrawDialogueChoices(Rect area, int selected, string first, string second = null)
        {
            var row = new Rect(area.x, area.y, area.width, DialogueChoiceRowHeight - 2f);
            if (DrawInventoryButton(row, first, selected == 0)) return 0;
            row.y += DialogueChoiceRowHeight;
            return second != null && DrawInventoryButton(row, second, selected == 1) ? 1 : -1;
        }

        private static Texture2D _pageCornerIcon;

        // Original PSX dialogue boxes cue "press to continue" with a small icon instead
        // of a full choice button when there is nothing to actually pick. Reimagined's
        // single-option "Continue"/"Continuar" story prompts had no equivalent - they drew
        // a full-width button bar - so this recreates that cue as a small animated icon
        // resembling a book page corner being lifted, tucked into the panel's bottom-right.
        private void DrawContinuePrompt(Rect panel, System.Action advance = null, bool showMarker = true)
        {
            if (showMarker) DrawWindowPageCorner(panel);
            // Clicking anywhere on the box also advances, same as the interact button
            // (handled separately in HandleTownStory for keyboard/controller) - the icon
            // is just the visual cue, not the only way to continue.
            if (GUI.Button(panel, GUIContent.none, GUIStyle.none))
            {
                if (advance != null) advance();
                else PickStoryChoice(0);
            }
        }

        private static Texture2D BuildPageCornerIcon()
        {
            const int size = 28;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var page = new Color32(232, 221, 186, 255);  // parchment face of the lifted corner
            var edge = new Color32(255, 250, 230, 255);  // bright rim where the raised paper catches light
            var shade = new Color32(150, 120, 74, 255);  // shadow pinched into the fold's tip
            var clear = new Color32(0, 0, 0, 0);
            var pixels = new Color32[size * size];
            const int leg = size - 2;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dx = (size - 1) - x;
                    int dy = (size - 1) - y;
                    int d = dx + dy; // taxicab distance from the bottom-right corner
                    Color32 color = clear;
                    if (d <= leg)
                    {
                        if (d <= 5) color = shade;
                        else if (d >= leg - 3) color = edge;
                        else color = page;
                    }
                    pixels[y * size + x] = color;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private static string T(string en, string ptBr) => GameLanguage.Text(en, ptBr);

        private static void DrawHudPanel(Rect rect) => DrawDialogueFrame(rect);

        private static void BeginHudMenu(Rect rect)
        {
            DrawDialogueFrame(rect);
            GUILayout.BeginArea(WindowDesignSystem.Content(rect, footer: true));
        }

        private static void DrawFlatHudPanel(Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;
            var gold = new Color(0.91f, 0.77f, 0.43f);
            DrawHudFill(new Rect(rect.x, rect.y, rect.width, 2f), gold);
            DrawHudFill(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), gold);
            DrawHudFill(new Rect(rect.x, rect.y + 2f, 2f, rect.height - 4f), gold);
            DrawHudFill(new Rect(rect.xMax - 2f, rect.y + 2f, 2f, rect.height - 4f), gold);
            DrawHudFill(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f),
                WindowDesignSystem.Background);
        }

        private static void DrawHudFill(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint || rect.width <= 0f) return;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, 0f);
        }

        [SerializeField] private Font _windowFont;
        private GUISkin _windowSkin;
        private GUIStyle _dialogueTextStyle;
        private GUIStyle _logLineStyle;
        private GUIStyle _experienceNoticeStyle;

        private void InitialiseWindowSkin(GUISkin original)
        {
            _windowFont = Resources.Load<Font>(WindowDesignSystem.FontResource) ?? _windowFont;
            _windowSkin = Instantiate(original);
            _windowSkin.hideFlags = HideFlags.HideAndDontSave;
            if (_windowFont) _windowSkin.font = _windowFont;
            foreach (var style in new[] { _windowSkin.label, _windowSkin.button, _windowSkin.box,
                _windowSkin.toggle, _windowSkin.textField, _windowSkin.textArea, _windowSkin.window })
            {
                WindowDesignSystem.ApplyText(style, _windowFont);
                style.padding = new RectOffset(8, 8, 5, 5);
                style.margin = new RectOffset(0, 0, 4, 4);
            }
        }

        private sealed class TowerLogWrapCache
        {
            internal readonly List<string> Lines = new();
            internal GUIStyle Style;
            internal Font Font;
            internal FontStyle FontStyle;
            internal bool RichText;
            internal int FontSize, Revision = -1, Processed, LastUse;
            internal float Width;
        }
        // Two widths are used: the full log panel and its scrollbar inset.
        private readonly TowerLogWrapCache[] _towerLogWrapCaches = { new(), new() };
        private int _towerLogWrapCacheUse;

        private List<string> WrapTowerLogLines(List<string> messages, float maxWidth, GUIStyle style)
        {
            if (!ReferenceEquals(messages, _messageStack)) return BuildTowerLogLines(messages, maxWidth, style);
            TowerLogWrapCache cache = null;
            foreach (var entry in _towerLogWrapCaches)
                if (ReferenceEquals(entry.Style, style) && entry.Font == style.font && entry.FontSize == style.fontSize &&
                    entry.FontStyle == style.fontStyle && entry.RichText == style.richText && entry.Width == maxWidth)
                { cache = entry; break; }
            if (cache == null)
            {
                cache = _towerLogWrapCaches[0].LastUse <= _towerLogWrapCaches[1].LastUse
                    ? _towerLogWrapCaches[0] : _towerLogWrapCaches[1];
                cache.Lines.Clear(); cache.Processed = 0; cache.Revision = _messageRevision - messages.Count;
                cache.Style = style; cache.Font = style.font; cache.FontSize = style.fontSize;
                cache.FontStyle = style.fontStyle; cache.RichText = style.richText; cache.Width = maxWidth;
            }
            cache.LastUse = ++_towerLogWrapCacheUse;
            if (cache.Revision == _messageRevision) return cache.Lines;
            // Append only new messages. Clearing/replacing the log invalidates its
            // revision independently of the count, so old floor text cannot survive.
            if (_messageRevision - cache.Revision != messages.Count - cache.Processed)
            { cache.Lines.Clear(); cache.Processed = 0; }
            BuildTowerLogLines(messages, maxWidth, style, cache.Lines, cache.Processed);
            cache.Processed = messages.Count; cache.Revision = _messageRevision;
            return cache.Lines;
        }

        private List<string> BuildTowerLogLines(List<string> messages, float maxWidth, GUIStyle style, List<string> lines = null, int firstMessage = 0)
        {
            lines ??= new List<string>();
            if (messages == null || messages.Count == 0) return lines;

            for (int messageIndex = firstMessage; messageIndex < messages.Count; messageIndex++)
            {
                string message = messages[messageIndex];
                string[] paragraphs = (message ?? string.Empty).Split('\n');
                foreach (string paragraph in paragraphs)
                {
                    string current = string.Empty;
                    string[] words = paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string word in words)
                    {
                        string candidate = current.Length == 0 ? word : current + " " + word;
                        bool fits = style.CalcSize(new GUIContent(candidate)).x <= maxWidth;
                        if (fits)
                        {
                            current = candidate;
                            continue;
                        }

                        if (current.Length > 0)
                        {
                            lines.Add(current);
                            current = string.Empty;
                        }

                        string remainder = word;
                        while (remainder.Length > 0)
                        {
                            int take = remainder.Length;
                            while (take > 1)
                            {
                                string chunk = remainder.Substring(0, take);
                                bool chunkFits = style.CalcSize(new GUIContent(chunk)).x <= maxWidth;
                                if (chunkFits) break;
                                take--;
                            }

                            string part = remainder.Substring(0, take);
                            remainder = remainder.Substring(take);
                            if (remainder.Length > 0) lines.Add(part);
                            else current = part;
                        }
                    }

                    if (current.Length > 0 || words.Length == 0)
                        lines.Add(current);
                }
            }

            return lines;
        }

        private void DrawHudText(Rect rect, string value, GUIStyle style, bool dialogue = false)
        {
            if (!style.wordWrap) style = InventoryFittedStyle(rect, value, style);
            Color previousColor = GUI.contentColor;
            GUI.contentColor = Color.black;
            GUI.Label(new Rect(rect.x + 1f, rect.y + (dialogue ? 1f : 2f), rect.width, rect.height), value, style);
            GUI.contentColor = previousColor;
            GUI.Label(rect, value, style);
        }

        private IEnumerator FadeToBlackCoroutine()
        {
            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                _fadeToBlack = Mathf.Lerp(1f, 0f, elapsed / _fadeDuration);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            _fadeToBlack = 0f;
            _fadeDuration = 0f;
        }
    }
}
