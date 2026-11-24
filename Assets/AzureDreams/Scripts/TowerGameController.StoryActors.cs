using UnityEngine;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        // Reimagined usa o retrato novo Grandpa_variant1_frame0 (so existe no overlay Reimagined);
        // Classic mantem o retrato original Grandpa_variant0_frame0.
        private static string SilverPortrait => AssetMode.IsReimagined
            ? "Dialogue/Grandpa_variant1_frame0"
            : "Dialogue/Grandpa_variant0_frame0";

        // Use the ordinary NPC interaction, portrait and collision path for story actors.
        private void SpawnStoryResident(string prefabId, string id, string name, Vector2Int cell, Vector3? placedPosition = null)
        {
            var prefab = FindStoryResidentPrefab(prefabId);
            if (!prefab) return;
            if (_interior)
            {
                if (TownResidentPlacementCatalog.TryGetInteriorCell(_interior.interiorId, id, 0, out var authoredCell))
                    cell = _interior.MapLayoutCell(authoredCell);
                else
                    cell = FindInteriorResidentCell();
                if (cell.x < 0) return;
            }
            if (!_interior && !placedPosition.HasValue && !StoryResidentCellOpen(cell))
            {
                var center = cell;
                cell = new Vector2Int(-1, -1);
                for (int radius = 1; radius <= 4 && cell.x < 0; radius++)
                    for (int z = -radius; z <= radius && cell.x < 0; z++)
                        for (int x = -radius; x <= radius; x++)
                        {
                            var candidate = center + new Vector2Int(x, z);
                            if (!StoryResidentCellOpen(candidate)) continue;
                            cell = candidate;
                            break;
                        }
            }
            if (cell.x < 0) return;
            var instance = Spawn(prefab, cell, "Story resident - " + name);
            instance.transform.position = placedPosition ?? RetailTownActorPosition(CellPosition(cell), 0f);
            var npc = instance.GetComponent<NPCController>();
            var stats = instance.GetComponent<CharacterStats>();
            npc.npcId = id;
            bool retailSprite = RetailResidentBanks.Apply(instance, id);
            SetupCherrlFirstMeetingVisual(npc);
            if (!retailSprite && IsSilverResident(id)) ConfigureSilverPresentation(npc);
            if (!retailSprite && id == "wookie" && instance.GetComponentInChildren<SpriteRenderer>() is { } sprite)
            {
                // Match Koh's standing height while keeping the sprite anchored at the feet.
                // No interior framingScale here anymore (see ConfigurePlayerView): Koh himself
                // no longer shrinks indoors, so wookie shouldn't either - otherwise the two
                // would go back out of sync with each other inside buildings.
                const float standingScale = 1.6125f;
                sprite.transform.localScale *= standingScale;
            }
            npc.ConfigureConversation(_townFlow.State);
            if (stats)
            {
                stats.displayName = name;
                if (IsSilverResident(id)) stats.portraitTexture = StreamPortrait(SilverPortrait);
                else if (prefabId != id) stats.portraitTexture = null;
            }
            // The restored-pool cast has unique swimwear and portrait variants.
            // Apply these only in its interior: the same source prefabs are used
            // unchanged throughout Monsbaiya.
            if (StoryRoom == PoolOpenRoom && (id is "wookie" or "pool_nico" or "pool_fur" or "pool_selfi" or "pool_patty" or
                "pool_cherrl" or "pool_visitor" or "pool_ghosh" ||
                (id == "vivian" && TownSaveVariables.GetInt(VivianPoolDateKey) != 1) ||
                (id == "mia" && (MiaStage() > MiaPoolDate ||
                    TownRetailScriptFlags.CreatePersistent().IsFlagSet(MiaPoolPresentFlag)))))
                ApplyPoolPresentation(npc);
            if (prefabId == id)
            {
                if (!instance.TryGetComponent<TownDialogueProgress>(out var progress))
                    progress = instance.AddComponent<TownDialogueProgress>();
                progress.Prepare(npc);
            }
            else npc.dialogueLines = new[] { T("Come back another day.", "Volte outro dia.") };
            _townPeople.Add(npc);
            _npcCells.Add(cell);
        }

        private static bool IsSilverResident(string id) =>
            id == "silver";

        private static void ConfigureSilverPresentation(NPCController npc)
        {
            if (!npc) return;
            if (RetailResidentBanks.Apply(npc.gameObject, npc.npcId)) return;
            // Silver is the retail "Grandpa" town actor: use its original
            // front/back/side sprites (the same set as at the racetrack)
            // instead of the generic helper sprite of the shared prefab.
            if (npc.TryGetComponent<RacetrackResidentSpriteView>(out _)) return;
            if (npc.TryGetComponent<SilverSpriteView>(out var legacy)) Destroy(legacy);
            npc.gameObject.AddComponent<RacetrackResidentSpriteView>();
        }

        private GameObject FindStoryResidentPrefab(string id) =>
            System.Array.Find(_storyResidents ?? System.Array.Empty<GameObject>(), candidate =>
                candidate && candidate.TryGetComponent<NPCController>(out var controller) && controller.npcId == id);

        private bool StoryResidentCellOpen(Vector2Int cell) => cell != _playerCell &&
            !_npcCells.Contains(cell) && TownPositionOpen(CellPosition(cell));

        private NPCController _outdoorSelfi;

        private void UpdateOutdoorSelfiMusic()
        {
            if (_nicoGhoshMusic) return;
            UpdateCheroConcertMusic();
            if (_area != Area.Town || _interior || !_outdoorSelfi || !_player || _prologue != Prologue.Off) return;
            float distance = Vector3.Distance(_player.transform.position, _outdoorSelfi.transform.position);
            if (distance <= 4f && _playingMusicCue == "Town") PlayAreaMusic(SelfiMusicCue);
            else if (distance > 5f && _playingMusicCue == SelfiMusicCue) PlayAreaMusic("Town");
        }

        private void SpawnOutdoorStoryResidents()
        {
            if (!UsingRetailTown) return;
            SpawnConstructionResidents();
            SpawnFountainTownspeople();
            SpawnStoryResident("isaac", "isaac", "Isaac",
                TownResidentPlacementCatalog.GetTownCells("isaac")[0]);
            _outdoorSelfi = null;
            foreach (var cell in TownResidentPlacementCatalog.GetTownCells("selfi"))
            {
                if (!StoryResidentCellOpen(cell) ||
                    !TownPositionOpen(CellPosition(cell), radius: 0.55f)) continue;
                int count = _townPeople.Count;
                SpawnStoryResident("selfi", "selfi", "Selfi", cell);
                if (_townPeople.Count > count) _outdoorSelfi = _townPeople[count];
                break;
            }
            int nicoStage = StoryStage("nico");
            if (nicoStage is NicoFirstDonation or NicoSecondDonation or NicoFountainTalk)
                SpawnStoryResident("nico", "nico", "Nico", TownResidentPlacementCatalog.GetTownCells("nico")[0]);
            if ((nicoStage == NicoCheroIntroduction && StoryNewDay("nico")) ||
                nicoStage is NicoGhoshConfrontation or NicoCheroOpinion or StoryComplete)
                SpawnStoryResident("monster_shop_hunter", "chero", "Chero", TownResidentPlacementCatalog.GetTownCells("chero")[0]);
            if (nicoStage is NicoGhoshConfrontation or NicoCheroOpinion)
                SpawnNicoFountainCast();
            if (nicoStage == NicoHouseGuard)
                SpawnStoryResident("ghosh", "nico_door_ghosh", "Ghosh", TownResidentPlacementCatalog.GetTownCells("nico_house_guard")[0]);
            // Keep Wookie above the captured park furniture without changing his scale.
            Vector3? fountainPerch = _builtTownLots.Exists(project => project.Id == "fountain")
                ? new Vector3(70.5f, Mathf.Max(1f, FountainSurface(new Vector3(70.5f, 0f, 78.5f))), 78.5f) : null;
            SpawnStoryResident("monster_shop_hunter", "wookie", "Wookie",
                fountainPerch.HasValue ? new Vector2Int(70, 78) : TownResidentPlacementCatalog.GetTownCells("wookie")[0], fountainPerch);
            if (MiaStage() is MiaFollowing or MiaFollowingReady)
                SpawnStoryResident("mia", "mia", "Mia", TownResidentPlacementCatalog.GetTownCells("mia")[0]);
            int cherrlStage = StoryStage("cherrl");
            if (cherrlStage == CherrlFirstMeeting && StoryBuilding("hospital"))
            {
                foreach (var (door, interior) in _facadeInteriors)
                {
                    if (!door || interior != CherrlHouse) continue;
                    Vector3 atWindow = door.Threshold + new Vector3(1.5f, 0f, 1.25f);
                    SpawnStoryResident("cherrl", "cherrl", "Cherrl",
                        new Vector2Int(Mathf.RoundToInt(atWindow.x), Mathf.RoundToInt(atWindow.z)));
                    break;
                }
            }
        }

        private bool ShouldSpawnStoryResident(NPCController npc, string room)
        {
            if (room == BarRoom && OkamiBarClosed) return npc.npcId == "okami";
            if (npc.npcId == "selfi" && room == RhodesHouse) return false;
            if (room == RestaurantRoom && npc.npcId != "patty" && PattyAloneInRestaurant) return false;
            if (npc.npcId == "cherrl" && room == CherrlHouse)
                return StoryStage("cherrl") is < CherrlHerbDelivered or CherrlNurseChoice or >= CherrlHomeRecovery;
            if (npc.npcId != "vivian") return true;
            int stage = StoryStage("vivian");
            return room switch
            {
                // Once Vivian's casino scene ends, she returns to the bar so
                // the theater-scout branch can play.  Until then the retail
                // route intentionally keeps her at the casino.
                BarRoom => (stage is 3 or 8 or >= StoryComplete) || (stage == 4 &&
                    (!StoryBuilding("casino") || TownSaveVariables.GetBool(VivianCasinoVisitKey))),
                TheaterRoom => stage >= 5 && stage != 8,
                _ => true
            };
        }

        private void SpawnInteriorStoryResidents(string room)
        {
            switch (room)
            {
                case CherrlHouse:
                    if (!FindStoryActor("nadia"))
                        SpawnStoryResident("dar", "nadia", "Nadia", new Vector2Int(20,16));
                    break;
                case NicoHouse:
                {
                    int nicoStage = StoryStage("nico");
                    SpawnRetailResident("nico_dad", T("NcsDad", "Pai da Nico"), "Town/Residents/NicoDad");
                    if (nicoStage == 0)
                    {
                        var nico = _townPeople.Find(candidate => candidate && candidate.npcId == "nico");
                        if (nico) nico.gameObject.SetActive(false);
                    }
                    else if (nicoStage == NicoFinalArrival && StoryNewDay("nico"))
                        StartCoroutine(PlayNicoFinalArrival());
                    break;
                }
                case RestaurantRoom when PattyStage() == PattyGhoshScene && StoryNewDay("patty"):
                    StartCoroutine(PlayPattyFinalScene());
                    break;
                case BarRoom when StoryStage("vivian") == 4 && StoryBuilding("theater") &&
                    (!StoryBuilding("casino") || TownSaveVariables.GetBool(VivianCasinoVisitKey)) &&
                    TownSaveVariables.GetBool(VivianTheaterDreamKey) &&
                    !TownSaveVariables.GetBool(StoryPrefix + "vivian.fonScout"):
                    StartCoroutine(PlayVivianFonScoutScene());
                    break;
                case GeneralStoreInterior when StoryStage("fur") == FurMusicBox:
                    StartCoroutine(PlayFurMusicBoxScene());
                    break;
                case GeneralStoreInterior when StoryStage("fur") == FurGhoshScene:
                    StartCoroutine(PlayFurGhoshScene());
                    break;
                case GeneralStoreInterior when StoryStage("fur") == FurFinalScene:
                    StartCoroutine(PlayFurFinalScene());
                    break;
                case LibraryRoom:
                    if ((MiaStage() is MiaBookAmused or MiaGhoshCollision) && StoryNewDay("mia"))
                        StartCoroutine(PlayMiaGlassesScene());
                    else if (MiaStage() == MiaGlassesOnFloor)
                        StartCoroutine(RestoreMiaDroppedGlasses());
                    else
                    {
                        RecordMiaLibraryFollowVisit();
                        if (StoryBuilding("library") && !TownSaveVariables.GetBool(NicoLibrarySeenKey))
                            SpawnStoryResident("nico", "nico_library", "Nico", default);
                    }
                    break;
                case CarpenterInterior:
                    if (!StoryBuilding("arcade") && !FindStoryActor("construction_pen"))
                    {
                        string penPrefab = FindStoryResidentPrefab("pen") ? "pen" : "dar";
                        SpawnStoryResident(penPrefab, "construction_pen", "Pen", default);
                    }
                    if (StoryStage("vivian") >= 4 && !StoryBuilding("theater"))
                        SpawnStoryResident("dar", "carpenter_fon", "Fon", default);
                    break;
                case SilverHouse:
                    SpawnStoryResident("dar", "silver", "Silver", default);
                    break;
                case CasinoRoom or CasinoSlotsRoom when StoryStage("vivian") == 4 && !TownSaveVariables.GetBool(VivianCasinoVisitKey):
                    SpawnStoryResident("vivian", "vivian", "Vivian", default);
                    break;
                case TheaterRoom:
                    SpawnStoryResident("dar", "theater_fon", "Fon", default);
                    SpawnStoryResident("monster_shop_hunter", "theater_audience", T("Spectator", "Espectador"), default);
                    SpawnStoryResident("nico", "nico_theater", "Nico", default);
                    SetRoomDialogue("theater_audience", T("The dancing is great, but I can't wait to hear the stand-up comedy.",
                        "A dança é ótima, mas mal posso esperar pelo número de comédia."));
                    break;
                case PoolClosedRoom:
                    SpawnStoryResident("monster_shop_hunter", "wookie", "Wotta", default);
                    break;
                case PoolOpenRoom:
                    SpawnStoryResident("monster_shop_hunter", "wookie", "Wotta", default);
                    SpawnStoryResident("ghosh", "pool_ghosh", "Ghosh", default);
                    SpawnStoryResident("nico", "pool_nico", "Nico", default);
                    SpawnStoryResident("fur", "pool_fur", "Fur", default);
                    if (StoryStage("selfi") >= StoryComplete ||
                        TownRetailScriptFlags.CreatePersistent().IsFlagSet(SelfiLoveFlag))
                        SpawnStoryResident("selfi", "pool_selfi", "Selfi", default);
                    if (StoryStage("patty") >= StoryComplete ||
                        TownRetailScriptFlags.CreatePersistent().IsFlagSet(PattyLoveFlag))
                        SpawnStoryResident("patty", "pool_patty", "Patty", default);
                    SpawnPoolResidents();
                    if (MiaStage() >= MiaPoolDate || TownRetailScriptFlags.CreatePersistent().IsFlagSet(MiaPoolPresentFlag))
                        SpawnStoryResident("mia", "mia", "Mia", default);
                    if (StoryStage("vivian") >= StoryComplete || TownSaveVariables.GetInt(VivianPoolDateKey) is 1 or 2 ||
                        TownRetailScriptFlags.CreatePersistent().IsFlagSet(VivianPoolPresentFlag))
                        SpawnStoryResident("vivian", "vivian", "Vivian", default);
                    if (StoryStage("cherrl") >= StoryComplete)
                        SpawnStoryResident("cherrl", "pool_cherrl", "Cherrl", default);
                    break;
                case CherrlHospital:
                {
                    int cherrlStage = StoryStage("cherrl");
                    // Cherrl is absent for the penultimate hospital visit, as in
                    // the original sequence; she returns on the following visit.
                    if ((cherrlStage is >= CherrlHerbDelivered and <= CherrlNurseDebut or CherrlFinalTalk) ||
                        (cherrlStage == StoryComplete && TownRetailScriptFlags.CreatePersistent().IsFlagSet(CherrlNurseFlag)))
                        SpawnStoryResident("cherrl", "cherrl", "Cherrl", default);
                    if (cherrlStage is CherrlNurseDebut or CherrlHospitalAbsence)
                        StartCoroutine(PlayCherrlHospitalScene());
                    break;
                }
                case DrHalHouse:
                    // Town_Dar is the captured retail old-man sprite.  Dr. Hal
                    // receives his own runtime id and Grandpa portrait here so
                    // he no longer appears as the generic construction NPC.
                    SpawnStoryResident("dar", "hal", "Dr. Hal", default);
                    break;
            }
        }

        private void SpawnNicoFountainGhosh()
        {
            if (_townPeople.Exists(candidate => candidate && candidate.npcId == "nico_ghosh")) return;
            int priorCount = _townPeople.Count;
            SpawnStoryResident("ghosh", "nico_ghosh", "Ghosh", TownResidentPlacementCatalog.GetTownCells("nico_ghosh")[0]);
            if (_townPeople.Count > priorCount)
            {
                var ghosh = _townPeople[_townPeople.Count - 1];
                ghosh.GetComponent<ActorLocomotionView>()?.Present(Vector3.left, 0f, false);
            }
        }

        private System.Collections.IEnumerator PlayNicoFinalArrival()
        {
            // Wait until the additive interior load has completed before
            // opening the final dialogue from entering Nico's house on day 17.
            yield return null;
            if (StoryRoom != NicoHouse || StoryStage("nico") != NicoFinalArrival) yield break;
            var nico = _townPeople.Find(candidate => candidate && candidate.npcId == "nico");
            if (!nico) yield break;
            NicoFinalHouseConversation(nico);
        }

        private void SetRoomDialogue(string id, string line)
        {
            var person = _townPeople.Find(npc => npc && npc.npcId == id);
            if (person) person.dialogueLines = new[] { line };
        }

        private static readonly VisitorLine[][] SelfiStoryScripts =
        {
            null,
            new VisitorLine[]
            {
                // DUNGEON.BIN 0x7D2E7A. Portrait bank 0 is selected by default.
                new(true, "Hi there, Puffy lips!", "Oi, Bocão!"),
                new(true, "You look so handsome inside the tower. Must be the darkness.\nHa ha ha ha.",
                    "Você parece tão bonito dentro da Torre. Deve ser a escuridão.\nHa ha ha ha.")
            },
            new VisitorLine[]
            {
                // 0x7D207B and 0x7D20F7 explicitly select speakers 7 and 6.
                new(true, "What?\nI put an end to it, so this monster’s money is mine!",
                    "O quê? Eu dei o golpe final, então o dinheiro do monstro é meu!"),
                new(false, "What are you talking about? I’m the one who damaged him the most. So the money is mine!",
                    "Do que você está falando? Eu causei mais dano nele. Então o dinheiro é meu!", 1),
                new(true, "I treat you as my big brother in\ntown, but it’s different here in\nthe tower.",
                    "Na cidade eu trato você como meu\nirmão mais velho, mas aqui na\nTorre é diferente."),
                new(false, "It’s my money!", "O dinheiro é meu!", 1),
                new(true, "It’s mine!", "É meu!"),
                new(false, "Huh, it’s {name}.", "Ah, é o {name}."),
                new(true, "Hey! Puffy lips.", "Ei! Bocão."),
                new(false, "Selfi, How dare you talk like that?", "Selfi, como você ousa falar assim?", 1),
                new(false, "As a member of the legendary Rode family, and as the sister of the best swordsman in the world, I'm so ashamed of you.",
                    "Como membro da lendária família Rode e irmã do melhor espadachim do mundo, tenho muita vergonha de você.", 1),
                new(true, "I'm so sorry, Brother.", "Sinto muito, irmão."),
                new(false, "{name}! It would be a million years before such a peasant like you could talk to Selfi.",
                    "{name}! Levaria um milhão de anos para um camponês como você poder falar com a Selfi.", 1),
                new(false, "Don't you ever go near her again!", "Nunca mais chegue perto dela!", 1)
            },
            new VisitorLine[]
            {
                new(true, "Hi there, Puffy lips!\nHow are things with you?",
                    "Oi, Bocão!\nComo vão as coisas?"),
                new(true, "You seem so busy treasure-hunting, unlike me,",
                    "Você parece tão ocupado caçando tesouros, ao contrário de mim,"),
                new(true, "a daughter of the noble Rode family.\nHa ha ha ha.",
                    "filha da nobre família Rode.\nHa ha ha ha.")
            },
            new VisitorLine[]
            {
                new(true, "Hi there, Puffy lips!", "Oi, Bocão!"),
                new(true, "How do you feel coming across an oasis like me in the desert of the dungeon?",
                    "Como se sente ao encontrar um oásis como eu no deserto da masmorra?")
            },
            new VisitorLine[]
            {
                new(true, "{name}!", "{name}!"),
                new(true, "Help!\nAhhh.", "Socorro!\nAhhh.")
            },
            null,
            new VisitorLine[]
            {
                new(true, "Hi there, Puffy lips!", "Oi, Bocão!"),
                new(true, "My brother talks about {name} all the time at home. I know so much about you.",
                    "Meu irmão fala do {name} o tempo todo em casa. Sei tanta coisa sobre você."),
                new(true, "I heard you put fire on nails for the light in the tower.",
                    "Ouvi dizer que você põe fogo em pregos para iluminar a Torre.")
            },
            new VisitorLine[]
            {
                new(true, "I heard you wet your pants in the tower.", "Ouvi dizer que você fez xixi nas calças na Torre.")
            },
            new VisitorLine[]
            {
                new(true, "I heard you can tell where the monsters are with your nose.",
                    "Ouvi dizer que você consegue farejar onde os monstros estão.")
            },
            new VisitorLine[]
            {
                new(true, "Hi there, Puffy lips!", "Oi, Bocão!"),
                new(true, "Well, I thought I did...", "Bem, achei que sabia..."),
                new(true, "{name} is such a mystery.", "{name} é um verdadeiro mistério."),
                new(true, "Hi there, Puffy lips!", "Oi, Bocão!"),
                new(true, "I thought I knew so much about you.", "Achei que sabia tanto sobre você."),
                new(true, "But there is still a lot I don’t yet know.", "Mas ainda há muita coisa que não sei."),
                new(true, "Listen! I will uncover the mystery surrounding you.", "Escute! Vou desvendar o mistério que cerca você.")
            },
            new VisitorLine[]
            {
                new(VisitorSpeaker.Koh, "Woahhhhh...!", "Uauuuuu...!"),
                new(VisitorSpeaker.Koh, "Oh!", "Oh!"),
                new(VisitorSpeaker.Koh, "Selfi!?", "Selfi!?"),
                new(VisitorSpeaker.Koh, "S...Selfi.", "S...Selfi."),
                new(true, "Puffy lips...\nAre you OK?", "Bocão...\nVocê está bem?")
            }
        };

        private static readonly VisitorLine[] SelfiPulunpaDeclinedScript =
        {
            new(true, "You are so cruel, {name}.\nI hope you rot in hell.",
                "Você é tão cruel, {name}.\nEspero que apodreça no inferno.")
        };

        private static readonly VisitorLine[] SelfiPulunpaSecondPleaScript =
        {
            new(true, "Please I beg you. {name}!\nPlease help...",
                "Por favor, eu imploro. {name}!\nPor favor, ajude...")
        };

        private const int SelfiSavedInTowerFlag = 5000; // F_selfy_dungeon_tasuketa
        private const int SelfiLeftInTowerFlag = 5002; // F_selfy_dungeon_tasukenai

        // DUNGEON.BIN 0x7D1C90: Ghosh reacts to the failed rescue.
        private static readonly VisitorLine[] SelfiRefusedRescueArgument =
        {
            new(false, "My money!", "Meu dinheiro!"),
            new(true, "No, it’s my money.", "Não, é meu dinheiro."),
            new(false, "Huh, it’s {name}.....", "Ah, é o {name}....."),
            new(false, "I heard you left Selfi to die.", "Ouvi dizer que você deixou a Selfi para morrer."),
            new(true, "You are such a bastard.", "Você é um desgraçado."),
            new(false, "Selfi, that’s not true!", "Selfi, não é verdade!"),
            new(false, "{name} is the worst.", "{name} é o pior."),
            new(true, "You are so right, Brother.", "Você tem toda a razão, irmão."),
            new(false, "Men who cannot help a lady in a crisis are not true men no matter how nice they usually are.",
                "Homens que não ajudam uma dama em perigo não são homens de verdade, por mais gentis que sejam."),
            new(false, "{name}! I, as a man myself, wouldn’t regard you as a man.",
                "{name}! Como homem, eu não considero você um homem de verdade."),
            new(true, "You are the worst.", "Você é o pior.")
        };

        private VisitorLine[] SelfiArgumentScript()
        {
            var flags = TownRetailScriptFlags.CreatePersistent();
            return flags.IsFlagSet(SelfiLeftInTowerFlag) && !flags.IsFlagSet(SelfiSavedInTowerFlag)
                ? SelfiRefusedRescueArgument : SelfiStoryScripts[SelfiGhoshEncounter];
        }

        private (string Left, string Right) SelfiVisitorChoices() => _selfiVisitorStory switch
        {
            SelfiPulunpaRescue when !_selfiPulunpaDeclined => _selfiPulunpaSecondPlea
                ? (T("Alright!", "Tudo bem!"), T("Absolutely no way.", "De jeito nenhum mesmo."))
                : (T("I’m coming!", "Estou indo!"), T("No way.", "De jeito nenhum.")),
            SelfiOasis => (T("Not too bad.", "Nada mal."), T("You? An oasis!?", "Você? Um oásis!?")),
            SelfiNailRumor or SelfiPantsRumor or SelfiNoseRumor => (T("That’s not true.", "Não é verdade."),
                T("That’s right.", "É verdade.")),
            SelfiMainInterest => (T("I don’t have any mystery.", "Não tenho mistério nenhum."),
                T("That’s impossible.", "Isso é impossível.")),
            SelfiUBoatRescue => (T("How about you? Selfi", "E você, Selfi?"), T("I’m alright.", "Estou bem.")),
            _ => default
        };

        private VisitorLine[] SelfiVisitorReply(bool right) => _selfiVisitorStory switch
        {
            SelfiOasis => right
                ? new VisitorLine[] { new(true, "I am an oasis.", "Eu sou um oásis.") }
                : new VisitorLine[] { new(true, "Fine!", "Ótimo!") },
            SelfiNailRumor or SelfiPantsRumor or SelfiNoseRumor =>
                new VisitorLine[] { new(true, "{name} is such a mystery.", "{name} é um verdadeiro mistério.") },
            SelfiMainInterest => new VisitorLine[]
            {
                right ? new(true, "I don’t care if I don’t find out.", "Não me importo se não descobrir.")
                    : new(true, "No need to hide!", "Não precisa esconder!"),
                new(true, "You are my main interest right now.", "Você é meu principal interesse agora.")
            },
            SelfiUBoatRescue => new VisitorLine[]
            {
                right ? new(true, "That’s good. Really.", "Que bom. De verdade.")
                    : new(true, "I’m OK.\nJust surprised a bit.", "Estou bem.\nSó fiquei um pouco surpresa."),
                new(true, "Phew.", "Ufa."),
                new(true, "But come to think of it, why do I jeopardize my life for {name}?",
                    "Mas, pensando bem, por que arrisco minha vida pelo {name}?"),
                new(true, "That’s really stupid. When I realized {name} was in trouble, I couldn’t help myself...",
                    "Que burrice. Quando percebi que {name} estava em apuros, não consegui me conter..."),
                new(true, "What’s wrong with me?", "O que há de errado comigo?")
            },
            _ => null
        };

        private static readonly VisitorLine[] SelfiRescueThanks =
        {
            new(true, "Phew, Thank you for helping me. But you killed him like it was a piece of cake.",
                "Ufa, obrigada por me ajudar. Você acabou com ele como se fosse a coisa mais fácil do mundo."),
            new(true, "I guess I underestimated you.", "Acho que subestimei você."),
            new(true, "Umm... Well, it must be an honor for you to rescue me though.",
                "Hum... Bem, deve ser uma honra para você me resgatar."),
            new(true, "Ha ha ha ha.", "Ha ha ha ha.")
        };

        private int _selfiVisitorStory;
        private ActorRecord _selfiEncounterMonster;

        private int PendingSelfiEncounter()
        {
            EnsureSelfiRouteVersion();
            int stage = StoryStage("selfi");
            return StoryNewDay("selfi") && (stage is SelfiTowerIntroductions or SelfiGhoshEncounter or
                SelfiTreasureHunt or SelfiOasis or SelfiPulunpaRescue or SelfiNailRumor or
                SelfiPantsRumor or SelfiNoseRumor or SelfiMainInterest or SelfiUBoatRescue) ? stage : 0;
        }

        private bool SpawnSelfiEncounterMonster(MonsterSpecies species)
        {
            foreach (var step in VisitorSteps)
            {
                Vector2Int cell = FloorTwoVisitorCell + step;
                if (!CanTowerStep(FloorTwoVisitorCell, step) || BellCellOccupied(cell)) continue;
                SpawnManagedCreature(cell, species, levelOverride: 2, allowSpawnSleep: false);
                int index = CreatureAt(cell);
                if (index < 0) continue;
                _selfiEncounterMonster = _creatures[index].Actor;
                return true;
            }
            return false;
        }

        private void CompleteSelfiVisitor()
        {
            if (_selfiVisitorStory == SelfiPulunpaRescue)
            {
                if (_selfiPulunpaAccepted && SpawnSelfiEncounterMonster(MonsterSpecies.PULUNPA))
                    _message = T("Protect Selfi! Defeat the Pulunpa.", "Proteja a Selfi! Derrote o Pulunpa.");
                else if (!_selfiPulunpaAccepted)
                {
                    TownRetailScriptFlags.CreatePersistent().SetBit(SelfiLeftInTowerFlag);
                    SetSelfiTowerStage(SelfiPulunpaRescue);
                }
            }
            else if (_selfiVisitorStory == SelfiTowerIntroductions)
                SetSelfiTowerStage(SelfiGhoshEncounter);
            else if (_selfiVisitorStory == SelfiGhoshEncounter)
                SetSelfiTowerStage(SelfiTreasureHunt);
            else if (_selfiVisitorStory == SelfiTreasureHunt)
                SetSelfiTowerStage(SelfiOasis);
            else if (_selfiVisitorStory == SelfiOasis)
                SetSelfiTowerStage(SelfiPulunpaRescue);
            else if (_selfiVisitorStory == SelfiNailRumor)
                SetSelfiTowerStage(SelfiPantsRumor);
            else if (_selfiVisitorStory == SelfiPantsRumor)
                SetSelfiTowerStage(SelfiNoseRumor);
            else if (_selfiVisitorStory == SelfiNoseRumor)
                SetSelfiTowerStage(SelfiMainInterest);
            else if (_selfiVisitorStory == SelfiMainInterest)
                SetSelfiTowerStage(SelfiUBoatRescue);
            else if (_selfiVisitorStory == SelfiUBoatRescue)
            {
                if (_selfiEncounterMonster != null)
                {
                    int index = _creatures.FindIndex(creature => creature.Actor == _selfiEncounterMonster);
                    if (index >= 0)
                    {
                        var creature = _creatures[index];
                        SpawnFx(_fxMagic, CellPosition(creature.Cell));
                        RemoveGenerated(creature.View);
                        _creatures.RemoveAt(index);
                    }
                    _selfiEncounterMonster = null;
                }
                SetSelfiTowerStage(SelfiFirstQuestion);
            }
        }

        private void ResolveSelfiRescue()
        {
            if (!IsTowerEventFloor || StoryStage("selfi") != SelfiPulunpaRescue || _selfiEncounterMonster == null ||
                _carriedMonster.Actor == _selfiEncounterMonster ||
                _creatures.Exists(creature => creature.Actor == _selfiEncounterMonster)) return;
            _selfiEncounterMonster = null;
            TownRetailScriptFlags.CreatePersistent().SetBit(SelfiSavedInTowerFlag);
            SetSelfiTowerStage(SelfiEggReward);
            foreach (var step in VisitorSteps)
            {
                Vector2Int cell = _playerCell + step;
                if (!CanTowerStep(_playerCell, step) || BellCellOccupied(cell)) continue;
                _towerEventSelfi = SpawnVisitor(_floorTwoSelfi, cell, "Selfi");
                _towerEventSelfi.SetStage(_player.transform.position);
                _towerEventSelfi.Approach(1f);
                _visitorEvent = VisitorEvent.Selfi;
                _selfiVisitorStory = 0;
                _visitorScriptOverride = SelfiRescueThanks;
                _visitorStage = VisitorStage.Dialogue;
                _visitorLine = 0;
                _visitorFocus = Vector3.Lerp(_player.transform.position, _towerEventSelfi.transform.position, 0.5f);
                _visitorFocus.y = 0f;
                PlayAreaMusic(SelfiMusicCue);
                ShowVisitorLine();
                return;
            }
            _message = T("Selfi made it out safely.", "Selfi conseguiu escapar em segurança.");
        }

        private static void SetSelfiTowerStage(int stage)
        {
            TownSaveVariables.SetInt(StoryPrefix + "selfi.stage", stage);
            TownSaveVariables.SetInt(StoryPrefix + "selfi.day", StoryDay);
            TownSaveVariables.Save();
        }
    }
}
