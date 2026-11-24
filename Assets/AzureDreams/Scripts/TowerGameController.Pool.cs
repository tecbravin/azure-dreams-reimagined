using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string PoolRequestSeen = "pool.request_seen";
        private const string PoolNicoPortrait = NicoPortraitPrefix + "6_frame0";
        private const string PoolFurPortrait = FurPortraitPrefix + "5_frame0";
        private const string PoolMiaPortrait = MiaPortraitPrefix + "6_frame0";
        private const string PoolChangingRoomObjectId = "pool_changing_room";
        private static readonly Vector3 PoolChangingRoomPosition = new(20f, 0f, 13.5f);
        // Swimming follows the captured water triangles, including the shaped
        // corners and slide inlet, instead of treating the whole bounding box as water.
        private bool _poolKohWearingSwimsuit;
        private bool _poolKohSwimming;
        private bool _poolExitContact;
        private NPCController _poolChangingRoomSpeaker;

        private bool InRestoredPool => _area == Area.Town && PoolRestored && StoryRoom == PoolOpenRoom && _player;

        private bool IsPoolWater(Vector3 position) => InRestoredPool &&
            _interior.TryGetPoolWaterHeight(position, out _);

        private bool BlockPoolExitInSwimwear()
        {
            if (!_interior || _interior.interiorId != PoolOpenRoom || !_poolKohWearingSwimsuit) return false;
            if (!_poolExitContact) _message = T("I need to change first.", "Preciso me trocar primeiro.");
            _poolExitContact = true;
            return true;
        }

        private bool PoolWaterRequiresSwimsuit(Vector3 position) =>
            IsPoolWater(position) && !_poolKohWearingSwimsuit;

        private void UpdateKohPoolSwimming(Vector3 position)
        {
            float waterHeight = 0f;
            bool swimming = _poolKohWearingSwimsuit && InRestoredPool &&
                _interior.TryGetPoolWaterHeight(position, out waterHeight) &&
                position.y - TownActorFootOffset <= waterHeight + 0.05f;
            if (_kohSprite && (swimming || _poolKohSwimming != swimming))
                _kohSprite.PresentSwimming(swimming, waterHeight);
            if (_poolKohSwimming == swimming) return;
            _poolKohSwimming = swimming;
            PoolAttireView.SetSwimming(_player, swimming);
        }

        private void ResetPoolPresentation()
        {
            ResetPoolSlide();
            if (_kohSprite) { _kohSprite.PresentSwimming(false); _kohSprite.PresentSwimsuit(false); }
            if (_player) PoolAttireView.Remove(_player);
            _poolKohWearingSwimsuit = false;
            _poolKohSwimming = false;
            _poolExitContact = false;
            _poolChangingRoomSpeaker = null;
            _poolSceneAnimating = false;
            _poolEntrySceneChecked = false;
            _poolHecklersStarted = false;
            var flags = TownRetailScriptFlags.CreatePersistent();
            flags.ClearBit(0x1200);
            flags.ClearBit(0x1201);
            flags.ClearBit(KohSwimsuitFlag);
        }

        private void SetupRestoredPoolInterior()
        {
            if (!InRestoredPool) return;

            ConfigureRestoredPoolEventState();
            SetupPoolSlide();

            // The blue-diamond doorway already belongs to the original captured room.
        }

        private static bool ApproachingPoolChangingRoomDoor(Vector3 before, Vector3 after) =>
            before.z > 14.5f && after.z <= 14.5f && after.z >= 13.5f &&
            Mathf.Abs(after.x - PoolChangingRoomPosition.x) <= 0.45f;

        private bool TryEnterPoolChangingRoom(Vector3 before, Vector3 after)
        {
            if (!InRestoredPool || _changingInterior || _storySpeaker || _poolSceneAnimating ||
                _inventoryOpen || _settingsOpen || _heldTownPot || _carriedItem != null ||
                _townJumpHeight > TownGroundTolerance || _townJumpVelocity != 0f ||
                !ApproachingPoolChangingRoomDoor(before, after)) return false;
            _townMomentum = Vector2.zero;
            OpenPoolChangingRoom();
            return true;
        }
        private NPCController PoolChangingRoomSpeaker()
        {
            if (_poolChangingRoomSpeaker) return _poolChangingRoomSpeaker;
            var speakerObject = new GameObject("Pool changing room dialogue");
            speakerObject.transform.SetParent(transform, false);
            var stats = speakerObject.AddComponent<CharacterStats>();
            stats.characterId = PoolChangingRoomObjectId;
            stats.displayName = T("Changing room", "Vestiário");
            _poolChangingRoomSpeaker = speakerObject.AddComponent<NPCController>();
            _poolChangingRoomSpeaker.npcId = PoolChangingRoomObjectId;
            _generated.Add(speakerObject);
            return _poolChangingRoomSpeaker;
        }

        private void OpenPoolChangingRoom()
        {
            if (!InRestoredPool) return;
            var speaker = PoolChangingRoomSpeaker();
            if (_poolKohWearingSwimsuit)
            {
                StoryAsk(speaker, "Koh is wearing his swimsuit.", "Koh está usando sua roupa de banho.",
                    (T("Change back", "Trocar de volta"), RemoveKohSwimsuit),
                    (T("Keep it on", "Continuar usando"), null));
                return;
            }

            StoryAsk(speaker, "Use the changing room?", "Usar o vestiário?",
                (T("Change into swimwear", "Trocar para roupa de banho"), WearKohSwimsuit),
                (T("Not now", "Agora não"), null));
        }

        private void ReleasePoolChangingRoomInput()
        {
            _townObjectDialogueOpen = false;
            _dialogueLine = null;
            _visibleDialogue = string.Empty;
            if (_activeTownNpc == _poolChangingRoomSpeaker)
            {
                if (_activeTownNpc) _activeTownNpc.CancelDialogue();
                _activeTownNpc = null;
            }
            if (_townFlow && _townFlow.State.Current == TownGameState.State.TownDialogue)
                _townFlow.State.TrySet(TownGameState.State.TownIdle);
        }

        private void WearKohSwimsuit()
        {
            if (!InRestoredPool) return;
            if (_kohSprite && !_kohSprite.PresentSwimsuit(true))
            {
                _message = T("Swimwear sprites could not be loaded.", "Não foi possível carregar os sprites da roupa de banho.");
                return;
            }
            _poolKohWearingSwimsuit = true;
            TownRetailScriptFlags.CreatePersistent().SetBit(KohSwimsuitFlag);
            PoolAttireView.Remove(_player);
            _message = T("Koh changed into his swimsuit.", "Koh trocou para a roupa de banho.");
        }

        private void RemoveKohSwimsuit()
        {
            _poolKohWearingSwimsuit = false;
            if (_kohSprite) _kohSprite.PresentSwimsuit(false);
            TownRetailScriptFlags.CreatePersistent().ClearBit(KohSwimsuitFlag);
            if (_poolKohSwimming && _player)
            {
                Vector3 exit = new(17.1f, 0f, 17.2f);
                _player.transform.position = RetailTownActorPosition(exit, TownActorFootOffset);
                _playerCell = new Vector2Int(Mathf.RoundToInt(exit.x), Mathf.RoundToInt(exit.z));
            }
            UpdateKohPoolSwimming(_player ? _player.transform.position : Vector3.zero);
            PoolAttireView.Remove(_player);
            _message = T("Koh changed back into his regular clothes.", "Koh voltou a usar suas roupas normais.");
        }

        // The swimming-pool portraits are separate original-game variants.  Do
        // not let the shared town-prefab portrait leak into this interior.
        private void ApplyPoolPresentation(NPCController npc)
        {
            if (!npc) return;
            if (InRestoredPool)
            {
                var swimmer = npc.GetComponent<PoolSwimmerView>();
                if (!swimmer) swimmer = npc.gameObject.AddComponent<PoolSwimmerView>();
                swimmer.Configure(_interior);
            }
            // Wotta's retail bank already contains his complete outfit. The
            // generic trunks overlay sits at his feet instead of his waist.
            if (npc.npcId == "wookie")
            {
                RetailResidentBanks.ApplyBank(npc.gameObject, "wata");
                PoolAttireView.Remove(npc.gameObject);
                return;
            }
            string portrait = npc.npcId switch
            {
                "pool_nico" => PoolNicoPortrait,
                "pool_ghosh" => GhoshSwimsuitPortrait,
                "pool_fur" => PoolFurPortrait,
                "pool_patty" => PattyPortrait(4),
                "pool_cherrl" => CherrlPortrait(6),
                "pool_selfi" => SelfiPortrait(5),
                "vivian" when StoryRoom == PoolOpenRoom => VivianPortrait(5),
                "mia" when StoryRoom == PoolOpenRoom => PoolMiaPortrait,
                _ => null
            };
            if (portrait != null && npc.TryGetComponent<CharacterStats>(out var stats))
                stats.portraitTexture = StreamPortrait(portrait);

            string bank = PoolSwimsuitBank(npc.npcId);
            if (bank != null && RetailResidentBanks.ApplyBank(npc.gameObject, bank))
            {
                PoolAttireView.Remove(npc.gameObject);
                return;
            }

            switch (npc.npcId)
            {
                case "pool_nico":
                    PoolAttireView.Apply(npc, PoolAttireView.Style.OnePiece,
                        new Color(0.22f, 0.68f, 0.94f), new Color(0.95f, 0.95f, 1f));
                    break;
                case "pool_fur":
                    PoolAttireView.Apply(npc, PoolAttireView.Style.OnePiece,
                        new Color(0.96f, 0.80f, 0.22f), new Color(1f, 0.96f, 0.72f));
                    break;
                case "pool_ghosh":
                    PoolAttireView.Apply(npc, PoolAttireView.Style.Trunks,
                        new Color(0.58f, 0.27f, 0.82f), new Color(0.89f, 0.58f, 0.83f));
                    break;
                case "mia" when StoryRoom == PoolOpenRoom:
                    PoolAttireView.Apply(npc, PoolAttireView.Style.OnePiece,
                        new Color(0.34f, 0.66f, 0.95f), new Color(0.92f, 0.94f, 1f));
                    break;
                case "pool_selfi":
                    PoolAttireView.Apply(npc, PoolAttireView.Style.OnePiece,
                        new Color(0.30f, 0.38f, 0.86f), new Color(0.83f, 0.86f, 1f));
                    break;
                case "pool_patty":
                    PoolAttireView.Apply(npc, PoolAttireView.Style.OnePiece,
                        new Color(0.86f, 0.35f, 0.57f), new Color(1f, 0.79f, 0.88f));
                    break;
                case "pool_cherrl":
                    PoolAttireView.Apply(npc, PoolAttireView.Style.OnePiece,
                        new Color(0.66f, 0.79f, 0.73f), new Color(0.96f, 0.81f, 0.57f));
                    break;
                case "vivian" when StoryRoom == PoolOpenRoom:
                    PoolAttireView.Apply(npc, PoolAttireView.Style.OnePiece,
                        new Color(0.62f, 0.42f, 0.77f), new Color(0.89f, 0.75f, 0.48f));
                    break;
                case "pool_visitor":
                    PoolAttireView.Apply(npc, PoolAttireView.Style.Trunks,
                        new Color(0.16f, 0.48f, 0.86f), new Color(0.9f, 0.96f, 1f));
                    break;
            }
        }

        private bool PoolTalk(NPCController npc, string portrait, string en, string pt, System.Action next = null) =>
            StoryTalk(npc, en, pt, portrait, next);

        private bool PoolAsk(NPCController npc, string portrait, string en, string pt,
            params (string Label, System.Action Apply)[] choices) =>
            StoryAsk(npc, en, pt, portrait, choices);

        private void PoolSequence(NPCController npc, string portrait, System.Action done,
            params (string English, string Portuguese)[] lines)
        {
            void ShowLine(int index)
            {
                if (index >= lines.Length)
                {
                    done?.Invoke();
                    return;
                }
                PoolTalk(npc, portrait, lines[index].English, lines[index].Portuguese,
                    () => ShowLine(index + 1));
            }
            ShowLine(0);
        }

        private bool PoolService(NPCController npc)
        {
            if (npc.npcId == "wookie")
            {
                if (PoolRestored)
                {
                    if (StoryRoom == PoolOpenRoom)
                    {
                        // TOWN.BIN 0x533614: pool manager's repeat welcome.
                        return StoryTalk(npc, "Have lots of fun!", "Divirta-se bastante!");
                    }
                    return StoryTalk(npc, $"Hey, {HeroName}! You are free to come and go as you wish!",
                        $"Oi, {HeroName}! Pode entrar e sair quando quiser!");
                }

                if (FindStoryItem(QuestItemKind, WaterMedalId) >= 0)
                    return StoryAsk(npc, "Th-this is the Water Medal!", "E-esse é o Medalhão da Água!",
                        (T("Return the Water Medal", "Devolver o Medalhão da Água"), () =>
                        {
                            int medal = FindStoryItem(QuestItemKind, WaterMedalId);
                            if (medal < 0 || !_townInventory.ConsumeDungeonItem(medal, QuestItemKind)) return;
                            TownRetailScriptFlags.CreatePersistent().SetBit(PoolQuestCompleteFlag);
                            TownRetailScriptFlags.CreatePersistent().SetBit(PoolOpenFlag);
                            if (StoryStage("hal") >= HalWaterMedalSearch && StoryStage("hal") < HalPoolRestored)
                                AdvanceTownStory("hal", HalPoolRestored);
                            PersistTownProgress();
                            PoolSequence(npc, null, () => StartCoroutine(LoadInterior(PoolOpenRoom)),
                                ("The Water Medal! The water is clean again!", "O Medalhão da Água! A água está limpa novamente!"),
                                ($"Thank you, my friend! {HeroName}, you are free to come and go as you wish!",
                                    $"Obrigado, meu amigo! {HeroName}, você ganhou um passe vitalício. Entre quando quiser!"),
                                ("The Woogie Woogie Pool is open again. Come in and enjoy yourself!",
                                    "A piscina Woogie Woogie está aberta de novo. Entre e divirta-se!"));
                        }), (T("Later", "Depois"), null));

                if (TownSaveVariables.GetBool(PoolRequestSeen))
                    return StoryTalk(npc, $"Hey, {HeroName}. Did you get the Water Medal back? I'm depending on you.",
                        $"Oi, {HeroName}. Recuperou o Medalhão da Água? Estou contando com você.");
                return StoryAsk(npc, "A thief stole the Water Medal! I went after him, but he ran into the tower. Please! Could you get it back?",
                    "Um ladrão roubou o Medalhão da Água! Corri atrás dele, mas ele fugiu para a torre. Por favor, pode recuperá-lo?",
                    (T("I'll get it back for you.", "Vou recuperá-lo para você."), () =>
                    {
                        TownSaveVariables.SetBool(PoolRequestSeen, true);
                        if (StoryStage("hal") == HalPoolRequest)
                            AdvanceTownStory("hal", HalWaterMedalSearch);
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
                case "pool_ghosh":
                    return GhoshPoolStory(npc);
                case "pool_selfi":
                    return SelfiPoolStory(npc);
                case "pool_patty":
                    return PattyPoolStory(npc);
                case "pool_cherrl":
                    return CherrlPoolConversation(npc);
                case "pool_nico":
                    // TOWN.BIN 0x535687–0x5357E5.
                    return NicoPages(npc, NicoSwimsuit, null,
                        ("Hello, {name}.  You came too.", "Olá, {name}. Você também veio."),
                        ("Thanks to the pool, we no longer have to swim in the lake.",
                            "Graças à piscina, não precisamos mais nadar no lago."),
                        ("We don’t have to be afraid of being attacked by monsters.",
                            "Agora podemos nadar sem medo de sermos atacados por monstros."),
                        ("We can relax while we’re swimming.", "Dá para relaxar enquanto nadamos."),
                        ("I guess you could say that the pool is also very cultural.",
                            "Acho que dá para dizer que a piscina também é muito culta."));
                case "pool_fur":
                    return FurPoolStory(npc);
                // The retail pool residents (TowerGameController.PoolResidents.cs) replace the
                // former generic "pool_visitor" and its reconstructed lines.
                default: return PoolResidentDialogue(npc);
            }
        }
    }
}
