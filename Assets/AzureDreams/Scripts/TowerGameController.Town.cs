using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    // Scene wiring only. Demo text/coordinates are authored placeholders, not
    // reconstructed retail town event bytecode or proven NPC schedules.
    public sealed partial class TowerGameController
    {
        private TownEventController _townFlow;
        private readonly List<NPCController> _townPeople = new List<NPCController>();
        private NPCController _activeTownNpc;
        private readonly List<TownObject> _townObjects = new List<TownObject>();
        private bool _enteringViaTrigger;
        private bool _returnTownAtGate;
        private bool _townObjectDialogueOpen;
        private const int TowerEntryCapacity = 5;
        private readonly Inventory _townStorage = new Inventory();

        private int TowerItemCapacity => TowerEntryCapacity - FamiliarInventoryCount -
            (FamiliarInventoryCount == 0 && _kewneDefinition && !TownSaveVariables.GetBool("starter_kewne_received") ? 1 : 0);
        private bool CanEnterWithLoadout()
        {
            if (_carriedItem != null)
            { _message = T("Put down the item you are holding.", "Coloque no chão o item que está segurando."); return false; }
            if (!UsingRetailTown || _townInventory.TotalItems <= TowerItemCapacity) return true;
            _message = T("You can take five items and familiars in total. Leave extras in the storage at home.", "Você pode levar até cinco itens e familiares no total. Deixe o excedente no depósito de casa.");
            return false;
        }

        private void PersistTownProgress()
        {
            RecordPlayTime();
            int potions = _potionItem ? _townInventory.GetCount(_potionItem) : 0;
            TownSaveVariables.Gold = _gold;
            TownSaveVariables.TownLevel = _townLevel;
            if (_potionItem) TownSaveVariables.SetItemCount(_potionItem.SaveId, potions);
            var familiar = PersistentFamiliar;
            SaveBagRoster();
            TownSaveVariables.SetString("traveling_familiar_record", familiar
                ? EncodeFamiliar(familiar, PersistentEggSpecies, _parkedFamiliarActor ?? _familiarActor,
                    TownSaveVariables.GetString(FamiliarNicknameKey)) : "");
            TownSaveVariables.SetInt(ActiveEggSpeciesKey, PersistentEggSpecies);
            TownSaveVariables.SetInt(FamiliarExperienceKey,
                familiar ? (_parkedFamiliarActor ?? _familiarActor).Experience : 0);
            TownSaveVariables.SetString("familiar", familiar ? familiar.DisplayName : string.Empty);
            TownSaveVariables.SetString("town_carried_dungeon_items", _townInventory.SaveDungeonItems());
            TownSaveVariables.SetString("town_stored_dungeon_items", _townStorage.SaveDungeonItems());
            if (_potionItem) TownSaveVariables.SetItemCount("town_storage_potions", _townStorage.GetCount(_potionItem));
            TownSaveVariables.Save();
            SaveSystem.Save(_gold, _townLevel,
                familiar ? familiar.DisplayName : string.Empty, potions);
        }

        private void SubscribeTownEvents()
        {
            TownEventBus.Subscribe(TownEventBus.EventId.ObjectInteracted, OnTownObjectInteracted);
            TownEventBus.Subscribe(TownEventBus.EventId.FountainUsed, OnTownFountainUsed);
            TownEventBus.Subscribe(TownEventBus.EventId.PlayerEnteredTower, OnTownGateTriggered);
            TownEventBus.Subscribe(TownEventBus.EventId.GoldChanged, OnTownGoldChanged);
            TownEventBus.Subscribe(TownEventBus.EventId.TownLevelChanged, OnTownLevelChanged);
            TownEventBus.Subscribe(TownEventBus.EventId.ShopPurchased, OnTownShopPurchased);
        }

        private void UnsubscribeTownEvents()
        {
            TownEventBus.Unsubscribe(TownEventBus.EventId.ObjectInteracted, OnTownObjectInteracted);
            TownEventBus.Unsubscribe(TownEventBus.EventId.FountainUsed, OnTownFountainUsed);
            TownEventBus.Unsubscribe(TownEventBus.EventId.PlayerEnteredTower, OnTownGateTriggered);
            TownEventBus.Unsubscribe(TownEventBus.EventId.GoldChanged, OnTownGoldChanged);
            TownEventBus.Unsubscribe(TownEventBus.EventId.TownLevelChanged, OnTownLevelChanged);
            TownEventBus.Unsubscribe(TownEventBus.EventId.ShopPurchased, OnTownShopPurchased);
        }

        private void OnTownGoldChanged(object value)
        {
            if (_area != Area.Town || !(value is int gold)) return;
            _gold = Mathf.Max(0, gold);
            PersistTownProgress();
        }

        private void OnTownLevelChanged(object value)
        {
            if (_area != Area.Town || !(value is int level)) return;
            _townLevel = Mathf.Max(1, level);
            PersistTownProgress();
        }

        private void OnTownShopPurchased(object value)
        {
            if (_area == Area.Town) PersistTownProgress();
        }

        private GameObject CreateTownAsset(string resourceName, string objectName, Vector3 position, Quaternion rotation)
        {
            var prefab = Resources.Load<GameObject>("Town/" + resourceName);
            if (!prefab) return null;
            var instance = Instantiate(prefab, position, rotation, transform);
            instance.name = objectName;
            _generated.Add(instance);
            return instance;
        }

        private GameObject CreateTownAssetOrFallback(string resourceName, string objectName, Vector3 position,
            Quaternion rotation, PrimitiveType fallbackShape, Vector3 fallbackScale, Material fallbackMaterial, bool collider)
        {
            var instance = CreateTownAsset(resourceName, objectName, position, rotation);
            if (instance) return instance;
            instance = CreatePrimitive(fallbackShape, objectName, position, fallbackScale, fallbackMaterial, collider);
            instance.transform.rotation = rotation;
            return instance;
        }

        private void BuildTownPaths()
        {
            var pathCells = new HashSet<Vector2Int>();
            for (int x = 1; x <= 10; x++) pathCells.Add(new Vector2Int(x, 5));
            for (int z = 3; z <= 4; z++)
            {
                pathCells.Add(new Vector2Int(2, z));
                pathCells.Add(new Vector2Int(8, z));
            }
            for (int z = 5; z <= 8; z++) pathCells.Add(new Vector2Int(7, z));
            for (int x = 6; x <= 8; x++) pathCells.Add(new Vector2Int(x, 8));
            for (int z = 6; z <= 8; z++) pathCells.Add(new Vector2Int(4, z));
            for (int x = 2; x <= 4; x++) pathCells.Add(new Vector2Int(x, 7));
            pathCells.Add(new Vector2Int(3, 6));
            pathCells.Add(new Vector2Int(6, 9));
            pathCells.Add(new Vector2Int(7, 9));

            foreach (var cell in pathCells)
                CreateTownAsset("TownPathTile", "Town Path Tile", new Vector3(cell.x, 0.015f, cell.y), Quaternion.identity);
        }

        private void OnTownGateTriggered(object value)
        {
            if (!(value is EventTrigger.TriggerEvent e) || !e.Source || !e.Source.transform.IsChildOf(transform) ||
                e.Actor != _player || _area != Area.Town || _activeTownNpc || _enteringViaTrigger) return;
            bool confirmedMeeting = e.Source == _townTowerGate && _kewneTutorialChoice.HasValue;
            if (_townFlow && _townFlow.State.Current != TownGameState.State.TownIdle &&
                !(confirmedMeeting && _townFlow.State.Current == TownGameState.State.TowerRunning)) return;
            if (!confirmedMeeting && !CanEnterWithLoadout()) return;
            if (NeedsKewneMeeting && !_kewneTutorialChoice.HasValue)
            {
                TryStartKewneMeeting();
                return;
            }
            _enteringViaTrigger = true;
            try { EnterTower(); }
            finally { _enteringViaTrigger = false; }
        }

        private void SetTownReturnSpawn()
        {
            // Retail uses the destination door's position record during the map
            // change (town/map/door.c). Its exact retail coordinates have not
            // been resolved from TOWN.BIN, so align the return pose to our gate.
            if (UsingRetailTown)
            {
                _playerCell = _gateCell + Vector2Int.up;
                _player.transform.position = RetailTownActorPosition(CellPosition(_playerCell), 0.55f);
                _townFacing = Vector2.up;
                return;
            }
            _playerCell = new Vector2Int(9, 5);
            _player.transform.position = new Vector3(8.5f, 0.55f, 5f);
            _townFacing = Vector2.right;
        }

        private void ConfigureTriggerPlayer()
        {
            _player.tag = "Player";
            if (!_player.TryGetComponent<Rigidbody>(out var body)) body = _player.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        private void InitializeTowerEvents()
        {
            ConfigureTriggerPlayer();
            foreach (var instance in _generated)
            {
                if (!instance || instance.name != "Tower Stairs") continue;
                var trigger = instance.AddComponent<EventTrigger>();
                trigger.triggerId = "tower_stairs_" + _floor;
                trigger.customEventId = "tower_stairs";
                trigger.persistActivation = false;
                trigger.once = false;
            }
        }

        private void OnTownObjectInteracted(object value)
        {
            if (value is TownObject.InteractionEvent e && e.Source && e.Source.transform.IsChildOf(transform) && e.Actor == _player)
            {
                _message = e.Message;
                _townObjectDialogueOpen = true;
                _rowanOfferPending = false;
                if (_townFlow) _townFlow.State.TrySet(TownGameState.State.TownDialogue);
            }
        }

        private void OnTownFountainUsed(object value)
        {
            if (value is TownObject.InteractionEvent e && e.Source && e.Source.transform.IsChildOf(transform) &&
                e.Actor == _player && _player.TryGetComponent<CharacterStats>(out var stats))
            {
                _health = stats.currentHealth;
                _townObjectDialogueOpen = false;
                if (_townFlow) _townFlow.State.TrySet(TownGameState.State.TownIdle);
            }
        }

        private void InitializeTownInteractions()
        {
            if (!_townFlow && !TryGetComponent(out _townFlow))
                _townFlow = gameObject.AddComponent<TownEventController>();
            // Respect the public transition graph: Boot cannot jump to TownIdle.
            if (_townFlow.State.Current == TownGameState.State.Boot)
                _townFlow.State.TrySet(TownGameState.State.Title);
            if (_townFlow.State.Current == TownGameState.State.TowerRunning ||
                _townFlow.State.Current == TownGameState.State.TowerVictory ||
                _townFlow.State.Current == TownGameState.State.TowerDefeat)
                _townFlow.State.TrySet(TownGameState.State.ReturnToTown);
            TownEventBus.Publish(TownEventBus.EventId.PlayerEnteredTown);
            ConfigureTriggerPlayer();
            foreach (var instance in _generated)
            {
                if (!instance || instance.name != "Tower Gate") continue;
                var gate = instance.AddComponent<EventTrigger>();
                gate.triggerId = "tower_gate";
                gate.eventId = TownEventBus.EventId.PlayerEnteredTower;
                gate.once = false;
                gate.persistActivation = false;
                gate.requireInteraction = true;
                _townTowerGate = gate;
                var gateCollider = gate.GetComponent<BoxCollider>();
                gateCollider.center = new Vector3(0f, 0.9f, 0f);
                gateCollider.size = new Vector3(1.9f, 1.8f, 0.9f);
                ConfigureTownEntranceDoor(instance);
            }
            if (!_player.TryGetComponent<CharacterStats>(out var playerStats)) playerStats = _player.AddComponent<CharacterStats>();
            playerStats.maxHealth = _playerDefinition ? _playerDefinition.MaxHealth : 20;
            playerStats.currentHealth = _health;
            if (!UsingRetailTown)
            {
                var fountainObject = CreateTownAssetOrFallback("Water_Fountain", "Town Water Fountain", new Vector3(7, 0f, 6),
                    Quaternion.identity, PrimitiveType.Cylinder, new Vector3(0.6f, 0.15f, 0.6f), _accentMaterial, false);
                var fountain = fountainObject.GetComponent<WaterFountain>();
                if (!fountain) fountain = fountainObject.AddComponent<WaterFountain>();
                fountain.objectId = "town_fountain";
                fountain.message = "Your vitality and magic are restored.";
                _townObjects.Add(fountain);
                _townBuildings.Add(new Rect(fountainObject.transform.position.x - 0.45f, fountainObject.transform.position.z - 0.45f, 0.9f, 0.9f));
            }
            var signObject = CreateTownAssetOrFallback("Guild_Sign", "Town Guild Sign", UsingRetailTown ? new Vector3(66, 0f, 29) : new Vector3(6, 0f, 8),
                Quaternion.Euler(0f, 180f, 0f), PrimitiveType.Cube, new Vector3(0.55f, 0.9f, 0.15f), _accentMaterial, false);
            var sign = signObject.GetComponent<TownObject>();
            if (!sign) sign = signObject.AddComponent<TownObject>();
            sign.objectId = "guild_sign";
            sign.message = UsingRetailTown ? "The Monster Tower lies beyond this gate." : "Tower gate: east. Return safely to bank your salvage.";
            _townObjects.Add(sign);
            _townBuildings.Add(new Rect(signObject.transform.position.x - 0.36f, signObject.transform.position.z - 0.055f, 0.72f, 0.11f));
            if (!UsingRetailTown)
            {
                var chestObject = CreateTownAssetOrFallback("Town_Empty_Chest", "Town Empty Chest", new Vector3(3, 0f, 6),
                    Quaternion.identity, PrimitiveType.Cube, new Vector3(0.65f, 0.5f, 0.45f), _wallMaterial, false);
                var chest = chestObject.GetComponent<TownObject>();
                if (!chest) chest = chestObject.AddComponent<TownObject>();
                chest.objectId = "town_empty_chest";
                chest.message = "The storage chest is empty.";
                _townObjects.Add(chest);
                _townBuildings.Add(new Rect(chestObject.transform.position.x - 0.35f, chestObject.transform.position.z - 0.26f, 0.70f, 0.52f));
            }
            if (UsingRetailTown)
            {
                string[] variants = { "Gold", "Green" };
                Vector2Int[] cells = { new(62, 42), new(68, 42) };
                for (int i = 0; i < cells.Length; i++)
                {
                    var cell = cells[i];
                    var pot = CreateTownAsset("Pot_" + variants[i], "Town Pot - " + variants[i],
                        new Vector3(cell.x, 0f, cell.y), Quaternion.identity);
                    if (!pot) continue;
                    pot.transform.position = RetailTownActorPosition(pot.transform.position, 0f);
                    if (pot.TryGetComponent<TownPotView>(out var potView)) _townPots.Add(potView);
                }
            }
            foreach (var instance in _generated)
            {
                if (!instance || !instance.name.StartsWith("Townsperson - ")) continue;
                if (!instance.TryGetComponent<CharacterStats>(out var stats)) stats = instance.AddComponent<CharacterStats>();
                stats.characterId = "rowan";
                stats.displayName = "Rowan";
                if (!instance.TryGetComponent<NPCController>(out var npc)) npc = instance.AddComponent<NPCController>();
                if (!instance.GetComponent<ActorLocomotionView>()) instance.AddComponent<ActorLocomotionView>();
                npc.npcId = stats.characterId;
                if (!npc.dialogue && (npc.dialogueLines == null || npc.dialogueLines.Length == 0))
                    npc.dialogueLines = new[] { T("Welcome.", "Boas-vindas."), T("What are you looking for.", "O que está procurando?") };
                npc.loopDialogue = false;
                npc.ConfigureConversation(_townFlow.State);
                var route = new TownRoute();
                var cell = new Vector2Int(Mathf.RoundToInt(instance.transform.position.x), Mathf.RoundToInt(instance.transform.position.z));
                route.Waypoints.Add(cell);
                npc.ConfigurePatrol(route, _walls, UsingRetailTown ? RetailTownSize : MapSize, _townBuildings);
                _townPeople.Add(npc);
            }
        }

        private void ClearTownInteractionReferences()
        {
            if (_activeTownNpc) _activeTownNpc.CancelDialogue();
            _activeTownNpc = null;
            _townPeople.Clear();
            ResetWeedyBook();
            ResetMonsterShop();
            SetBarryShopPage(BarryShopPage.Closed);
            SetGeneralStorePage(GeneralStorePage.Closed);
            _townObjects.Clear();
            _homeStorageOpen = _storageFromWreath = false;
            _storageBagRows = _storageDepotRows = null;
            _townObjectDialogueOpen = false;
            _rowanOfferPending = false;
            if (_townFlow && _townFlow.State.Current == TownGameState.State.TownDialogue)
                _townFlow.State.TrySet(TownGameState.State.TownIdle);
        }

        private bool TryTownObjectInteraction()
        {
            if (_townObjectDialogueOpen)
            {
                _townObjectDialogueOpen = false;
                if (_townFlow) _townFlow.State.TrySet(TownGameState.State.TownIdle);
                return true;
            }
            TownObject nearest = null;
            float best = float.MaxValue;
            foreach (var target in _townObjects)
            {
                if (!target || !target.isActiveAndEnabled) continue;
                Vector3 delta = target.transform.position - _player.transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > target.interactionRadius * target.interactionRadius || delta.sqrMagnitude >= best) continue;
                best = delta.sqrMagnitude;
                nearest = target;
            }
            return nearest && nearest.OnInteract(_player);
        }

        private bool TryTownGateInteraction()
        {
            if (_townDoorOpening) return true;
            if (!_player || _area != Area.Town || (_townFlow &&
                _townFlow.State.Current != TownGameState.State.TownIdle)) return false;
            var gate = _townTowerGate;
            if (_interior || !gate) return false;
            if (TryStartKewneMeeting()) return true;

            Vector3 toGate = gate.transform.position - _player.transform.position;
            toGate.y = 0f;
            if (toGate.sqrMagnitude > 1.5f * 1.5f) return false;
            // The retail door checks overlap, the interaction input and actor
            // orientation before changing the map (town/map/door.c).
            Vector3 facing = new Vector3(_townFacing.x, 0f, _townFacing.y);
            // door.c compares 12-bit angles with
            // ((doorAngle - actorAngle) + 0x3FF) < 0x7FF. Quantize both
            // bearings to the PS1's 0x1000-unit circle before applying it.
            int actorAngle = ToRetailAngle12(facing);
            int gateBearingAngle = ToRetailAngle12(toGate);
            if (!RetailDoorAngleAccepts(gateBearingAngle, actorAngle))
            {
                _message = T($"Face the tower gate and press {InteractKey} to enter.",
                    $"Vire-se para o portão da torre e pressione {InteractKey} para entrar.");
                return true;
            }
            if (!CanEnterWithLoadout()) return true;
            var door = gate.GetComponent<TownGateDoor>();
            if (!door) return gate.TryActivate(_player);
            StartCoroutine(PlayTownTowerEntrance(gate, door));
            return true;
        }

        private static int ToRetailAngle12(Vector3 direction)
        {
            float radians = Mathf.Atan2(direction.x, direction.z);
            return Mathf.RoundToInt(radians * (4096f / (2f * Mathf.PI))) & 0xFFF;
        }

        private static bool RetailDoorAngleAccepts(int doorAngle, int actorAngle) =>
            ((doorAngle - actorAngle + 0x3FF) & 0xFFF) < 0x7FF;

        private bool TryTownConversation()
        {
            NPCController speaker = _activeTownNpc;
            if (!speaker)
            {
                float distance = float.PositiveInfinity;
                foreach (var npc in _townPeople)
                {
                    if (!npc || !npc.isActiveAndEnabled) continue;
                    Vector3 delta = npc.transform.position - _player.transform.position;
                    delta.y = 0f;
                    bool acrossCounter = (npc.npcId == "barry" && AtBlacksmithCounter) ||
                        (npc.npcId == "fur" && InGeneralStore) ||
                        (npc.npcId == "patty" && StoryRoom == RestaurantRoom) ||
                        (npc.npcId == "tonka" && _interior && _interior.interiorId == "Interior_13_carpenter");
                    float reach = acrossCounter ? 3f : 1.4f;
                    if (delta.sqrMagnitude > distance || delta.sqrMagnitude > reach * reach) continue;
                    distance = delta.sqrMagnitude;
                    speaker = npc;
                }
                if (!speaker) return false;
                LocalizeHunter(speaker);
                if (speaker.npcId == "rowan")
                    speaker.dialogueLines = new[] { T("Welcome.", "Boas-vindas."), T("What are you looking for.", "O que está procurando?") };
                MoveInTown(Vector2.zero, Time.deltaTime);
                Vector3 facing = _player.transform.position - speaker.transform.position;
                facing.y = 0f;
                if (facing.sqrMagnitude > 0.0001f) speaker.transform.rotation = Quaternion.LookRotation(facing);
                if (speaker.npcId == "barry" && _interior && _interior.interiorId == BlacksmithInterior)
                {
                    if (!TryConstructionStory(speaker)) SetBarryShopPage(BarryShopPage.Services);
                    return true;
                }
                if (TryTownStory(speaker) || TheaterService(speaker)) return true;
                if (speaker.npcId == "hush" && _interior &&
                    _interior.interiorId is "Interior_0f_hospital" or "Interior_10_hospital_2")
                {
                    _hospitalHerbShop = true;
                    SetBarryShopPage(BarryShopPage.Buy);
                    return true;
                }
            }
            if (_activeTownNpc && _dialogueLine != null &&
                (Time.unscaledTime - _dialogueStarted) * 42f < _dialogueLine.Length)
            {
                _dialogueStarted = Time.unscaledTime - (_dialogueLine.Length + 1) / 42f;
                return true;
            }
            string line = speaker.Talk();
            _activeTownNpc = speaker.IsTalking ? speaker : null;
            if (_activeTownNpc)
            {
                var stats = speaker.GetComponent<CharacterStats>();
                var portrait = speaker.npcId == "weedy" && line == TownDialogueProgress.WeedyBookOffer
                    ? StreamPortrait(WeedyBookOfferPortrait) ?? stats.portraitTexture
                    : stats.portraitTexture;
                ShowTownDialogue(stats.displayName, line, portrait);
            }
            _message = speaker.IsTalking ? $"{speaker.GetComponent<CharacterStats>().displayName}: {_dialogueLine}  [{InteractKey}]" : string.Empty;
            if (!speaker.IsTalking && string.IsNullOrEmpty(line)) CompleteHomeResidentTalk(speaker);
            if (!speaker.IsTalking && string.IsNullOrEmpty(line) && speaker.npcId == "fur" && InGeneralStore)
                SetGeneralStorePage(GeneralStorePage.Services);
            if (!speaker.IsTalking && string.IsNullOrEmpty(line) && speaker.npcId == "tonka" && InCarpenterShop)
                OpenCarpenter();
            if (!speaker.IsTalking && speaker.npcId == "rowan" && string.IsNullOrEmpty(line))
            {
                _rowanOfferPending = true;
                if (_townFlow) _townFlow.State.TrySet(TownGameState.State.TownShop);
                int price = _potionItem ? _potionItem.Price : 20;
                _message = _gold >= price
                    ? WithInteractHint($"Rowan: That’ll be {price} G.", $"Rowan: São {price} G.")
                    : WithInteractHint("Rowan: You don’t have enough money.", "Rowan: Você não tem dinheiro suficiente.");
            }
            return true;
        }

        private void ConfirmRowanPurchase()
        {
            if (!_rowanOfferPending)
            {
                if (_townFlow) _townFlow.State.TrySet(TownGameState.State.TownIdle);
                return;
            }
            _rowanOfferPending = false;
            if (!_potionItem) _potionItem = CreatePotionDefinition();
            int price = _potionItem.Price;
            if (!_townInventory.HasSpace())
            {
                _message = T("Your inventory is full.", "Seu inventário está cheio.");
                if (_townFlow) _townFlow.State.TrySet(TownGameState.State.TownIdle);
                return;
            }
            if (_gold < price)
            {
                _message = T("Rowan: You don’t have enough money.", "Rowan: Você não tem dinheiro suficiente.");
                if (_townFlow) _townFlow.State.TrySet(TownGameState.State.TownIdle);
                return;
            }
            _gold -= price;
            _townInventory.AddItem(_potionItem, 1);
            TownEventBus.Publish(TownEventBus.EventId.GoldChanged, _gold);
            TownEventBus.Publish(TownEventBus.EventId.ShopPurchased, _potionItem);
            _message = T("Rowan: Thanks very much. You’ve made the right decision.", "Rowan: Muito obrigado. Você fez a escolha certa.");
            PersistTownProgress();
            if (_townFlow) _townFlow.State.TrySet(TownGameState.State.TownIdle);
        }

        private void CancelRowanPurchase()
        {
            _rowanOfferPending = false;
            _message = T("Purchase cancelled.", "Compra cancelada.");
            if (_townFlow) _townFlow.State.TrySet(TownGameState.State.TownIdle);
        }

        private void RefreshTownNpcCells()
        {
            if (_townPeople.Count == 0) return;
            _npcCells.Clear();
            foreach (var npc in _townPeople)
                if (npc && npc.isActiveAndEnabled)
                    _npcCells.Add(new Vector2Int(Mathf.RoundToInt(npc.transform.position.x), Mathf.RoundToInt(npc.transform.position.z)));
        }
    }
}
