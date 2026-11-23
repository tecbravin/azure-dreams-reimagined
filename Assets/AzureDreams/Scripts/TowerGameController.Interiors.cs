using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        [SerializeField] private bool _startInTower;
        private const string BlacksmithInterior = "Interior_15_blacksmith";
        private TownInteriorScene _interior;
        private bool AtBlacksmithCounter => _interior && _interior.interiorId == BlacksmithInterior && _player &&
            Rect.MinMaxRect(13f, 12.25f, 18f, 13.5f).Contains(new Vector2(_player.transform.position.x, _player.transform.position.z));
        private bool _changingInterior, _interiorMenu;
        private Vector2 _interiorScroll;
        private Vector3 _outsidePosition;
        private Vector2 _outsideFacing;
        private bool _spawnAboveInteriorExitMarker;
        [Serializable] private sealed class InteriorEntry { public string id, label, labelPtBr; }
        [Serializable] private sealed class InteriorCatalog { public InteriorEntry[] entries; }
        private InteriorCatalog _interiorCatalog;
        private bool HandleInteriorInput()
        {
            if (_changingInterior) return true;
            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame && !_activeTownNpc && !_heldTownPot)
                _interiorMenu = !_interiorMenu;
            return _interiorMenu;
        }
        private InteriorCatalog GetInteriorCatalog()
        {
            if (_interiorCatalog == null)
            {
                var data = AssetMode.Load<TextAsset>("Town/Interiors/catalog");
                if (data) _interiorCatalog = JsonUtility.FromJson<InteriorCatalog>(data.text);
            }
            return _interiorCatalog;
        }

        private void DrawInteriorMenu()
        {
            if (!_interiorMenu) return;
            GetInteriorCatalog();
            BeginHudMenu(new Rect(100, 60, 440, 350));
            GUILayout.Label(T("Interior preview travel (F2) - exterior door locations pending", "Visitar interiores (F2)"));
            _interiorScroll = GUILayout.BeginScrollView(_interiorScroll);
            string selectedPath = null;
            for (int index = 0; index < SceneManager.sceneCountInBuildSettings; index++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(index);
                string id = System.IO.Path.GetFileNameWithoutExtension(path);
                var entries = _interiorCatalog?.entries;
                var entry = entries == null ? null : Array.Find(entries, candidate => candidate.id == id);
                string label = entry == null ? id : T(entry.label, entry.labelPtBr);
                if (GUILayout.Button(label)) { selectedPath = path; break; }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            if (selectedPath == null) return;
            _interiorMenu = false;
            if (_area == Area.Town && selectedPath.StartsWith("Assets/AzureDreams/Scenes/Interiors/", StringComparison.Ordinal))
                StartCoroutine(LoadInterior(System.IO.Path.GetFileNameWithoutExtension(selectedPath)));
            else
                SceneManager.LoadSceneAsync(selectedPath, LoadSceneMode.Single);
        }
        private IEnumerator LoadInterior(string id, Vector3? facadeExit = null,
            bool? returnedFromTower = null, string returnMessage = null,
            bool spawnAboveExitMarker = false)
        {
            if (_changingInterior || _heldTownPot || _carriedItem != null || (_interior && _interior.interiorId == id)) yield break;
            if (id == PoolClosedRoom && PoolRestored) id = PoolOpenRoom;
            string path = "Assets/AzureDreams/Scenes/Interiors/" + id + ".unity";
            if (!Application.CanStreamedLevelBeLoaded(path))
            { _message = T("Interior scene is not in the build: " + id, $"O interior {id} nao esta disponivel nesta versao."); yield break; }
            _changingInterior = true;
            // Every entrance from the town places Koh inside the doorway, beyond
            // the blue return marker. Interior-to-interior travel and the wake-up
            // sequence keep their authored arrival positions.
            bool enteringFromTownDoor = _area == Area.Town && !_interior &&
                _prologue == Prologue.Off && !returnedFromTower.HasValue;
            _spawnAboveInteriorExitMarker = spawnAboveExitMarker || enteringFromTownDoor;
            _fadeToBlack = 0f;
            if (!_interior)
            {
                _outsidePosition = facadeExit ?? _player.transform.position;
                _outsideFacing = facadeExit.HasValue ? Vector2.up : _townFacing;
            }
            var previous = _interior ? _interior.gameObject.scene : default;
            // Start loading immediately. The old fixed two-second fade delayed
            // every doorway transition before Unity even began loading the room.
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            var loaded = SceneManager.GetSceneByPath(path);
            TownInteriorScene room = null;
            foreach (var root in loaded.GetRootGameObjects())
                if (root.TryGetComponent(out TownInteriorScene candidate)) { room = candidate; break; }
            if (room) AdoptInterior(room);
            else { _message = T("Interior metadata missing.", "Dados do interior ausentes."); yield return SceneManager.UnloadSceneAsync(loaded); }
            if (room && previous.IsValid() && previous != loaded) yield return SceneManager.UnloadSceneAsync(previous);
            if (room && returnedFromTower.HasValue && HouseIndex >= 0)
                yield return PlayWakeUp(returnedFromTower.Value, returnMessage);
            _changingInterior = false;
        }
        private static byte[] ReadInteriorHeights(TownInteriorScene room)
        {
            if (room)
            {
                try
                {
                    var asset = room.heights;
                    if (asset) return asset.bytes;
                }
                catch (MissingReferenceException)
                {
                    // Fall through to the Resources copy below when the scene's
                    // serialized TextAsset reference has been unloaded/staled.
                }

                var resource = AssetMode.Load<TextAsset>("Town/Interiors/" + room.interiorId + "/heights");
                if (resource) return resource.bytes;

                Debug.LogError("Missing interior height map for " + room.interiorId + ". Using a flat fallback to keep the interior playable.");
            }

            // Interior layouts use the town's 128x128 little-endian height grid.
            return new byte[RetailTownSize * RetailTownSize * 2];
        }
        private void AdoptInterior(TownInteriorScene room)
        {
            // Copy the binary height map before ClearGenerated destroys transient
            // scene objects. Some imported interiors keep this TextAsset reference
            // on the additive scene component, and accessing it after cleanup can
            // leave Unity holding a stale native object reference.
            byte[] interiorHeights = ReadInteriorHeights(room);
            bool returningFromHut = InHomeHut;
            ClearGenerated();
            _interior = room;
            if (_prologue == Prologue.Off)
                PlayAreaMusic(room.interiorId switch
                {
                    NicoHouse => "NicoTheme.mp3",
                    GeneralStoreInterior => "FurTheme.mp3",
                    RhodesHouse => "GhoshTheme.mp3",
                    CherrlHouse => "CherrlTheme.mp3",
                    BarRoom => "VivianTheme.mp3",
                    RestaurantRoom => "PattyTheme.mp3",
                    LibraryRoom => "MiaTheme.mp3",
                    BlacksmithInterior => "ArmShop.mp3",
                    MonsterShopScene => "MonsterShop.mp3",
                    CasinoRoom or CasinoSlotsRoom => "Casino.mp3",
                    RaceLobby or RaceCourse => RaceCourseMusicCue,
                    ArcadeMoleRoom or ArcadeBattingRoom => "Playroom.mp3",
                    TheaterRoom => TheaterMusicCue,
                    PoolClosedRoom or PoolOpenRoom => "SwimmingPool.mp3",
                    BowlingInterior => "Playroom.mp3",
                    "Interior_11_temple" or "Interior_12_temple_2" => "Temple1.mp3",
                    _ when HouseIndex >= 0 || InHomeHut => "KohHouseTheme.mp3",
                    _ => "Town"
                });
            _townHeights = interiorHeights;
            _townBuildings.AddRange(room.CollisionFootprints());
            Vector3 entry = InMonsterShop ? CellPosition(MonsterShopExit) : HomeEntry(room);
            int house = HouseIndex;
            // The hut's exit marker is the door square itself. Arriving on that
            // square makes the first movement retrigger the exit, so every
            // direction of travel through this link uses its interior-side cell.
            bool enteringHomeHut = InHomeHut && !returningFromHut;
            Vector3 inside = entry + InteriorDoorInward(room) * 2f;
            Vector3 arrival = returningFromHut && house >= 0 ? HomeHutArrival(house) :
                enteringHomeHut ? entry + Vector3.forward * 2f :
                _spawnAboveInteriorExitMarker ? inside :
                room.useArrivalPosition ? room.arrivalPosition : inside;
            if (TakeCasinoArrival() is { } casinoArrival) arrival = casinoArrival;
            // Never start on (or inside the trigger radius of) the exit mat.
            Vector3 fromExit = arrival - entry; fromExit.y = 0f;
            if (!(returningFromHut && house >= 0) && fromExit.sqrMagnitude < 2.25f) arrival = inside;
            _playerCell = new Vector2Int(Mathf.RoundToInt(arrival.x), Mathf.RoundToInt(arrival.z));
            _player = SpawnKoh(_playerCell, "Koh");
            _player.transform.position = RetailTownActorPosition(arrival, TownActorFootOffset);
            _townFacing = Vector2.up;
            ConfigurePlayerView(); ConfigureTriggerPlayer();
            _townFlow.State.ForceSet(TownGameState.State.TownIdle);
            _message = string.Empty;
            SpawnInteriorResidents(room);
            SpawnInteriorStoryResidents(room.interiorId);
            SpawnAmbientInteriorResidents(room.interiorId);
            SetupRestoredPoolInterior();
            CreatePrimitive(PrimitiveType.Cylinder, "Interior return marker (provisional)",
                new Vector3(entry.x, RetailTownHeight(entry) + 0.03f, entry.z),
                new Vector3(0.8f, 0.03f, 0.8f), _accentMaterial, false);
            CacheHomeFloorPlan();
            SetupKohHomeInterior();
            SetupMonsterShop();
            SetupCasino();
            SetupRacetrack();
            SetupInteriorDisplayMonsters();
            SetupBowling();
            SetupArcade();
            SetupGym();
            SetupNativeInteriorProps();
            SetupFortuneHouse();
            if (room.interiorId == BlacksmithInterior)
            {
                PlaceBlacksmithVase("Pot_Brown", new Vector3(13.25f, 0f, 9.5f));
                PlaceBlacksmithVase("Pot_Blue", new Vector3(12.15f, 0f, 9.5f));
            }
        }

        private void PlaceBlacksmithVase(string resource, Vector3 position)
        {
            var vase = CreateTownAsset(resource, "Blacksmith vase", RetailTownActorPosition(position, 0f), Quaternion.identity);
            if (vase && vase.TryGetComponent<TownPotView>(out var view)) _townPots.Add(view);
        }
        private void SpawnInteriorResidents(TownInteriorScene room)
        {
            var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            if (room.residentPrefabs != null)
            for (int i = 0; i < room.residentPrefabs.Length; i++)
            {
                var prefab = room.residentPrefabs[i];
                if (!prefab || !prefab.TryGetComponent<NPCController>(out var resident) ||
                    !ShouldSpawnStoryResident(resident, room.interiorId)) continue;
                string npcId = resident.npcId ?? string.Empty;
                occurrences.TryGetValue(npcId, out int occurrence);
                occurrences[npcId] = occurrence + 1;
                string placedId = room.interiorId == BarRoom ? npcId switch
                {
                    "monster_shop_hunter" => occurrence == 0 ? "bar_hunter_1" : "bar_hunter_2",
                    "restaurant_hunter" => "bar_hunter_3",
                    _ => npcId
                } : npcId;
                var cell = room.residentCells != null && i < room.residentCells.Length
                    ? room.residentCells[i] : new Vector2Int(-1, -1);
                // The catalog makes captured scene positions resilient to a
                // missing serialized cell (notably Nico's house) and keeps
                // repeated NPC prefabs in their distinct authored locations.
                if (TownResidentPlacementCatalog.TryGetInteriorCell(room.interiorId, placedId, placedId == npcId ? occurrence : 0, out var catalogCell))
                    cell = catalogCell;
                if (cell.x >= 0) cell = room.MapLayoutCell(cell);
                bool seated = IsSeatedInteriorResident(room.interiorId, placedId);
                var position = RetailTownActorPosition(CellPosition(cell), 0f);
                if (room.residentOffsets != null && i < room.residentOffsets.Length)
                    position += room.residentOffsets[i];
                bool templeJorda = placedId == "jorda" && room.interiorId is "Interior_11_temple" or "Interior_12_temple_2";
                if (templeJorda)
                {
                    // The captured altars occupy Z 10-11 / 11-12. Stand in the
                    // half-tile aisle behind them, rather than in the nave.
                    position = CellPosition(cell) + Vector3.forward * .5f;
                    // Yoda's cels extend six pixels below the bank's origin.
                    position = RetailTownActorPosition(position,6f/RetailTownResidentSpriteView.TownPixelsPerUnit);
                }
                bool useAuthoredCell = cell.x >= 0 && cell != _playerCell &&
                    (seated || templeJorda || TownPositionOpen(position, feetHeight: position.y));
                if (!useAuthoredCell)
                {
                    cell = FindInteriorResidentCell();
                    if (cell.x < 0) continue;
                    position = RetailTownActorPosition(CellPosition(cell), 0f);
                }
                var instance = Spawn(prefab, cell, prefab.name);
                instance.transform.position = position;
                var npc = instance.GetComponent<NPCController>();
                npc.npcId = placedId;
                RetailResidentBanks.Apply(instance, placedId);
                if (seated)
                {
                    instance.transform.position=new Vector3(cell.x+.5f,0f,cell.y+.625f);
                    instance.transform.rotation=Quaternion.LookRotation(Vector3.forward);
                    if(instance.TryGetComponent<RetailTownResidentSpriteView>(out var seatedView))seatedView.SetSeated(true);
                    var depth=instance.GetComponent<AzureDreamsReimagined.Rendering.ActorSpriteDepth>();
                    if(!depth)depth=instance.AddComponent<AzureDreamsReimagined.Rendering.ActorSpriteDepth>();
                    depth.SetSeated(true,new Vector3(0f,0f,.22f));
                }
                if (seated && npc.TryGetComponent<TownHumanoidView>(out var humanoid))
                    humanoid.SetSeated(0.5f);
                if (placedId is "bar_hunter_1" or "bar_hunter_2" or "bar_hunter_3")
                    npc.GetComponent<CharacterStats>().displayName = T("Hunter", "Caçador");
                if (npc.npcId == "hush") MatchHushVisualToKoh(npc);
                npc.ConfigureConversation(_townFlow.State);
                if (!instance.TryGetComponent<TownDialogueProgress>(out var progress))
                    progress = instance.AddComponent<TownDialogueProgress>();
                if (npc.npcId != "wreath") progress.Prepare(npc);
                _townPeople.Add(npc);
                _npcCells.Add(cell);
            }

            // Wreath and Weedy are permanent residents of Koh's home. Keep them
            // present even if an interior scene's resident list is incomplete or
            // one of its authored cells becomes blocked by a house upgrade.
            if (HouseIndex >= 0)
            {
                EnsureKohHomeResident(room, "wreath");
                EnsureKohHomeResident(room, "weedy");
            }
        }

        private void MatchHushVisualToKoh(NPCController hushNpc)
        {
            if (!hushNpc || !_player) return;
            // The retail bank already has Hush's native size and palette.
            if (hushNpc.TryGetComponent<RetailTownResidentSpriteView>(out _)) return;

            // Do not select an arbitrary descendant renderer here.  Koh's hand
            // equipment is also made of SpriteRenderers, so that selection can
            // make Hush inherit a weapon's tiny bounds and effectively vanish.
            var kohRoot = _player.transform.Find("Koh Sprite");
            var hushRoot = hushNpc.transform.Find("Character Sprite");
            var koh = kohRoot ? kohRoot.GetComponent<SpriteRenderer>() : null;
            var hush = hushRoot ? hushRoot.GetComponentInChildren<SpriteRenderer>() : null;
            if (koh && hush && koh.bounds.size.y > 0f && hush.bounds.size.y > 0f)
            {
                float scale = koh.bounds.size.y / hush.bounds.size.y;
                if (scale is > 0.1f and < 8f)
                    hushNpc.transform.localScale *= scale;
            }

            // His body is generated by TownCharacterSpriteView at instantiation.
            // Make the generated renderers explicit after fitting so scene state
            // cannot leave the story NPC present but visually disabled.
            foreach (var renderer in hushNpc.GetComponentsInChildren<SpriteRenderer>(true))
                renderer.enabled = true;
        }

        private void EnsureKohHomeResident(TownInteriorScene room, string npcId)
        {
            if (_townPeople.Exists(person => person && person.npcId == npcId)) return;

            GameObject prefab = null;
            if (room.residentPrefabs != null)
                foreach (var candidate in room.residentPrefabs)
                    if (candidate && candidate.TryGetComponent<NPCController>(out var resident) && resident.npcId == npcId)
                    {
                        prefab = candidate;
                        break;
                    }
            if (!prefab) prefab = FindStoryResidentPrefab(npcId);
            if (!prefab) return;

            Vector2Int cell = new(-1, -1);
            if (room.residentPrefabs != null && room.residentCells != null)
                for (int i = 0; i < room.residentPrefabs.Length && i < room.residentCells.Length; i++)
                    if (room.residentPrefabs[i] == prefab && room.residentCells[i].x >= 0 &&
                        room.MapLayoutCell(room.residentCells[i]) != _playerCell &&
                        TownPositionOpen(CellPosition(room.MapLayoutCell(room.residentCells[i]))))
                    {
                        cell = room.MapLayoutCell(room.residentCells[i]);
                        break;
                    }
            if (cell.x < 0) cell = FindInteriorResidentCell();
            if (cell.x < 0) return;

            var instance = Spawn(prefab, cell, prefab.name);
            instance.transform.position = RetailTownActorPosition(CellPosition(cell), 0f);
            var npc = instance.GetComponent<NPCController>();
            npc.ConfigureConversation(_townFlow.State);
            if (!instance.TryGetComponent<TownDialogueProgress>(out var progress))
                progress = instance.AddComponent<TownDialogueProgress>();
            if (npcId != "wreath") progress.Prepare(npc);
            _townPeople.Add(npc);
            _npcCells.Add(cell);
        }

        private Vector2Int FindInteriorResidentCell()
        {
            for (int radius = 2; radius < 12; radius++)
                for (int z = -radius; z <= radius; z++)
                    for (int x = -radius; x <= radius; x++)
                    {
                        var c = _playerCell + new Vector2Int(x, z);
                        if (Mathf.Abs(x) + Mathf.Abs(z) < 2 || _npcCells.Contains(c)) continue;
                        var pos = CellPosition(c);
                        if (TownPositionOpen(pos) && TownPositionOpen(pos + Vector3.right * 0.6f) &&
                            TownPositionOpen(pos + Vector3.forward * 0.6f)) return c;
                    }
            return new Vector2Int(-1, -1);
        }
        private bool TryExitInterior()
        {
            if (!_interior || !_player || _heldTownPot) return false;

            // Home-hut passages activate by touch. Check this before the house
            // exit because Level 1 keeps both markers close to the same wall.
            int house = HouseIndex;
            if (house >= 0 && NearHomePoint(HomeHutMarker(house), 0.58f))
            {
                StartCoroutine(LoadInterior(HutScenes[CurrentHutRank]));
                return true;
            }

            if (TryArcadePassage() || TryCasinoPassage()) return true;
            if (ArcadeBatting) return false;
            if (_interior.interiorId == CasinoRouletteRoom) return false;
            Vector3 delta = _player.transform.position - (InMonsterShop ? CellPosition(MonsterShopExit) : HomeEntry(_interior)); delta.y = 0f;
            float exitRadius = house == 0 ? 0.58f : 1.2f;
            if (delta.sqrMagnitude > exitRadius * exitRadius) { _poolExitContact = false; return false; }
            if (BlockPoolExitInSwimwear()) return true;

            // The Monster Hut is an extension of Koh's home, not a town building.
            // Walking through its return marker must therefore load the currently
            // built house level instead of using the generic town-exit path.
            if (InHomeHut)
            {
                StartCoroutine(LoadInterior(CurrentHomeScene()));
                return true;
            }
            if (InRaceCourse)
            {
                StartCoroutine(LoadInterior(RaceLobby));
                return true;
            }
            StartCoroutine(LeaveInterior()); return true;
        }
        private IEnumerator LeaveInterior()
        {
            if (_changingInterior || !_interior || _carriedItem != null) yield break;
            if (BlockPoolExitInSwimwear()) yield break;
            _changingInterior = true;
            var scene = _interior.gameObject.scene;
            _interior = null;
            _homePanel = 0;
            EnterTown();
            if (_outsidePosition != Vector3.zero)
            {
                _player.transform.position = _outsidePosition;
                _playerCell = new Vector2Int(Mathf.RoundToInt(_outsidePosition.x), Mathf.RoundToInt(_outsidePosition.z));
                _townFacing = _outsideFacing;
                if (_kohSprite) _kohSprite.SetFacing(_townFacing);
                UpdateCameraFollow(snap: true);
            }
            yield return SceneManager.UnloadSceneAsync(scene);
            _changingInterior = false;
        }
    }
}
