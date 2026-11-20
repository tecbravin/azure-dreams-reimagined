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
        private static readonly Vector3 PoolChangingRoomPosition = new(12.5f, 0f, 13.25f);
        // Surface 15 of the captured pool mesh is the original water plane:
        // X 18..29, Z 18..30. Keep the test independent from presentation so
        // entry/exit state stays deterministic.
        private static readonly Rect PoolWaterBounds = Rect.MinMaxRect(18f, 18f, 29f, 30f);
        private bool _poolKohWearingSwimsuit;
        private bool _poolKohSwimming;
        private NPCController _poolChangingRoomSpeaker;

        private bool InRestoredPool => _area == Area.Town && PoolRestored && StoryRoom == PoolOpenRoom && _player;

        private bool IsPoolWater(Vector3 position) => InRestoredPool &&
            PoolWaterBounds.Contains(new Vector2(position.x, position.z));

        private bool PoolWaterRequiresSwimsuit(Vector3 position) =>
            IsPoolWater(position) && !_poolKohWearingSwimsuit;

        private void UpdateKohPoolSwimming(Vector3 position)
        {
            bool swimming = _poolKohWearingSwimsuit && IsPoolWater(position);
            if (_poolKohSwimming == swimming) return;
            _poolKohSwimming = swimming;
            if (_kohSprite) _kohSprite.PresentSwimming(swimming);
            PoolAttireView.SetSwimming(_player, swimming);
        }

        private void ResetPoolPresentation()
        {
            if (_kohSprite) _kohSprite.PresentSwimming(false);
            if (_player) PoolAttireView.Remove(_player);
            _poolKohWearingSwimsuit = false;
            _poolKohSwimming = false;
            _poolChangingRoomSpeaker = null;
        }

        private void SetupRestoredPoolInterior()
        {
            if (!InRestoredPool) return;

            // The changing cubicle is a separate interactable rather than part
            // of the captured pool mesh, so it stays non-blocking and cannot
            // alter the source room's walkability or resident positions.
            var room = new GameObject("Pool changing room");
            room.transform.SetParent(transform, false);
            _generated.Add(room);
            float ground = RetailTownHeight(PoolChangingRoomPosition);
            Vector3 center = new(PoolChangingRoomPosition.x, ground, PoolChangingRoomPosition.z);

            CreatePoolChangingRoomPart(room.transform, "changing-room floor", center + Vector3.up * 0.03f,
                new Vector3(2.6f, 0.06f, 2.1f), _wallMaterial, Color.white);
            CreatePoolChangingRoomPart(room.transform, "changing-room back wall", center + new Vector3(0f, 0.85f, 0.92f),
                new Vector3(2.55f, 1.7f, 0.12f), _wallMaterial, Color.white);
            CreatePoolChangingRoomPart(room.transform, "changing-room left post", center + new Vector3(-1.15f, 0.9f, -0.75f),
                new Vector3(0.13f, 1.8f, 0.13f), _accentMaterial, new Color(0.7f, 0.88f, 1f));
            CreatePoolChangingRoomPart(room.transform, "changing-room right post", center + new Vector3(1.15f, 0.9f, -0.75f),
                new Vector3(0.13f, 1.8f, 0.13f), _accentMaterial, new Color(0.7f, 0.88f, 1f));
            CreatePoolChangingRoomPart(room.transform, "changing-room curtain", center + new Vector3(0f, 0.84f, -0.79f),
                new Vector3(2.15f, 1.55f, 0.08f), _accentMaterial, new Color(0.13f, 0.43f, 0.88f));
            CreatePoolChangingRoomPart(room.transform, "changing-room sign", center + new Vector3(0f, 1.72f, -0.81f),
                new Vector3(1.05f, 0.24f, 0.05f), _accentMaterial, new Color(1f, 0.95f, 0.53f));

            var changingRoom = room.AddComponent<TownObject>();
            changingRoom.objectId = PoolChangingRoomObjectId;
            changingRoom.message = T("Changing room", "Vestiário");
            changingRoom.interactionRadius = 1.65f;
            changingRoom.persistInteraction = false;
            _townObjects.Add(changingRoom);
        }

        private void CreatePoolChangingRoomPart(Transform parent, string label, Vector3 position, Vector3 scale,
            Material material, Color tint)
        {
            var part = CreatePrimitive(PrimitiveType.Cube, "Pool " + label, position, scale, material, false);
            part.transform.SetParent(parent, true);
            if (!part.TryGetComponent<Renderer>(out var renderer)) return;
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetColor("_Color", tint);
            properties.SetColor("_BaseColor", tint);
            renderer.SetPropertyBlock(properties);
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

        private void WearKohSwimsuit()
        {
            if (!InRestoredPool) return;
            _poolKohWearingSwimsuit = true;
            PoolAttireView.Apply(_player, PoolAttireView.Style.Trunks,
                new Color(0.19f, 0.50f, 0.96f), new Color(0.9f, 0.97f, 1f), 1.48f);
            _message = T("Koh changed into his swimsuit.", "Koh trocou para a roupa de banho.");
        }

        private void RemoveKohSwimsuit()
        {
            _poolKohWearingSwimsuit = false;
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
            string portrait = npc.npcId switch
            {
                "pool_nico" => PoolNicoPortrait,
                "pool_ghosh" => GhoshSwimsuitPortrait,
                "pool_fur" => PoolFurPortrait,
                "pool_patty" => PattyPortrait(4),
                "pool_cherrl" => CherrlPortrait(6),
                "vivian" when StoryRoom == PoolOpenRoom => VivianPortrait(5),
                "mia" when StoryRoom == PoolOpenRoom => PoolMiaPortrait,
                _ => null
            };
            if (portrait != null && npc.TryGetComponent<CharacterStats>(out var stats))
                stats.portraitTexture = StreamPortrait(portrait);

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
                case "wookie":
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
                        PoolSequence(npc, null, null,
                            ($"Hey, {HeroName}. Thanks for coming! Have a whale of a time.",
                                $"Oi, {HeroName}. Obrigado por vir! Divirta-se muito."),
                            ("The task of managing this pool is calling me! Keep the water clean and watch your step around the deep end.",
                                "O trabalho de cuidar desta piscina está me chamando! Mantenha a água limpa e cuidado na parte funda."));
                        return true;
                    }
                    return StoryAsk(npc, $"Hey, {HeroName}! You are free to come and go as you wish!",
                        $"Oi, {HeroName}! Pode entrar e sair quando quiser!",
                        (T("Enter the pool", "Entrar na piscina"), () => StartCoroutine(LoadInterior(PoolOpenRoom))),
                        (T("Later", "Depois"), null));
                }

                if (FindStoryItem(QuestItemKind, WaterMedalId) >= 0)
                    return StoryAsk(npc, "Th-this is the Water Medal!", "E-esse é o Medalhão da Água!",
                        (T("Return the Water Medal", "Devolver o Medalhão da Água"), () =>
                        {
                            int medal = FindStoryItem(QuestItemKind, WaterMedalId);
                            if (medal < 0 || !_townInventory.ConsumeDungeonItem(medal, QuestItemKind)) return;
                            TownRetailScriptFlags.CreatePersistent().SetBit(PoolQuestCompleteFlag);
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
