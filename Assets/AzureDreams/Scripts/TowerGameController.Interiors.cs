using System;
using System.Collections;
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
        [Serializable] private sealed class InteriorEntry { public string id, label, labelPtBr; }
        [Serializable] private sealed class InteriorCatalog { public InteriorEntry[] entries; }
        private InteriorCatalog _interiorCatalog;
        private bool HandleInteriorInput()
        {
            if (_changingInterior) return true;
            if (_area != Area.Town) return false;
            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame && !_activeTownNpc && !_heldTownPot)
                _interiorMenu = !_interiorMenu;
            return _interiorMenu;
        }
        private InteriorCatalog GetInteriorCatalog()
        {
            if (_interiorCatalog == null)
            {
                var data = Resources.Load<TextAsset>("Town/Interiors/catalog");
                if (data) _interiorCatalog = JsonUtility.FromJson<InteriorCatalog>(data.text);
            }
            return _interiorCatalog;
        }

        private void DrawInteriorMenu()
        {
            if (!_interiorMenu) return;
            GetInteriorCatalog();
            GUILayout.BeginArea(new Rect(100, 60, 440, 350), GUI.skin.box);
            GUILayout.Label(T("Interior preview travel (F2) - exterior door locations pending", "Visitar interiores (F2)"));
            if (_interior && GUILayout.Button(T("Return to Monsbaiya", "Voltar a Monsbaiya"))) { _interiorMenu = false; StartCoroutine(LeaveInterior()); }
            _interiorScroll = GUILayout.BeginScrollView(_interiorScroll);
            if (_interiorCatalog?.entries != null)
                foreach (var entry in _interiorCatalog.entries)
                    if (GUILayout.Button(T(entry.label, entry.labelPtBr)))
                    { _interiorMenu = false; StartCoroutine(LoadInterior(entry.id)); break; }
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private IEnumerator LoadInterior(string id, Vector3? facadeExit = null,
            bool? returnedFromTower = null, string returnMessage = null)
        {
            if (_changingInterior || _heldTownPot || _carriedItem != null || (_interior && _interior.interiorId == id)) yield break;
            if (id == PoolClosedRoom && PoolRestored) id = PoolOpenRoom;
            string path = "Assets/AzureDreams/Scenes/Interiors/" + id + ".unity";
            if (!Application.CanStreamedLevelBeLoaded(path))
            { _message = T("Interior scene is not in the build: " + id, $"O interior {id} não está disponível nesta versão."); yield break; }
            _changingInterior = true;
            if (!_interior)
            {
                _outsidePosition = facadeExit ?? _player.transform.position;
                _outsideFacing = facadeExit.HasValue ? Vector2.up : _townFacing;
            }
            var previous = _interior ? _interior.gameObject.scene : default;
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
        private void AdoptInterior(TownInteriorScene room)
        {
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
                    TheaterRoom => TheaterMusicCue,
                    PoolClosedRoom or PoolOpenRoom => "SwimmingPool.mp3",
                    BowlingInterior => "Playroom.mp3",
                    "Interior_11_temple" or "Interior_12_temple_2" => "Temple1.mp3",
                    _ when HouseIndex >= 0 || InHomeHut => "KohHouseTheme.mp3",
                    _ => "Town"
                });
            _townHeights = room.heights.bytes;
            if (room.furnitureFootprints != null) _townBuildings.AddRange(room.furnitureFootprints);
            Vector3 entry = InMonsterShop ? CellPosition(MonsterShopExit) : HomeEntry(room);
            int house = HouseIndex;
            Vector3 arrival = returningFromHut && house >= 0 ? HutLinks[house] :
                room.useArrivalPosition ? room.arrivalPosition : entry;
            _playerCell = new Vector2Int(Mathf.RoundToInt(arrival.x), Mathf.RoundToInt(arrival.z));
            _player = SpawnKoh(_playerCell, "Koh");
            _player.transform.position = RetailTownActorPosition(arrival, TownActorFootOffset);
            _townFacing = Vector2.up;
            ConfigurePlayerView(); ConfigureTriggerPlayer();
            _townFlow.State.ForceSet(TownGameState.State.TownIdle);
            SpawnInteriorResidents(room);
            SpawnInteriorStoryResidents(room.interiorId);
            CreatePrimitive(PrimitiveType.Cylinder, "Interior return marker (provisional)",
                new Vector3(entry.x, RetailTownHeight(entry) + 0.03f, entry.z),
                new Vector3(0.8f, 0.03f, 0.8f), _accentMaterial, false);
            var label = GetInteriorCatalog()?.entries;
            var entryLabel = label == null ? null : Array.Find(label, entry => entry.id == room.interiorId);
            string roomName = entryLabel == null ? room.displayName : T(entryLabel.label, entryLabel.labelPtBr);
            _message = roomName + T(". E at the entrance: return. F2: interiors.", ". E na entrada: voltar. F2: interiores.");
            CacheHomeFloorPlan();
            SetupKohHomeInterior();
            SetupMonsterShop();
            SetupCasino();
            SetupRacetrack();
            SetupBowling();
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
            if (room.residentPrefabs == null) return;
            for (int i = 0; i < room.residentPrefabs.Length; i++)
            {
                var prefab = room.residentPrefabs[i];
                if (!prefab || !prefab.TryGetComponent<NPCController>(out var resident) ||
                    !ShouldSpawnStoryResident(resident, room.interiorId)) continue;
                var cell = room.residentCells != null && i < room.residentCells.Length
                    ? room.residentCells[i] : new Vector2Int(-1, -1);
                bool useAuthoredCell = cell.x >= 0 && cell != _playerCell && TownPositionOpen(CellPosition(cell));
                if (!useAuthoredCell) cell = FindInteriorResidentCell();
                if (cell.x < 0) continue;

                var position = RetailTownActorPosition(CellPosition(cell), 0f);
                if (useAuthoredCell && room.residentOffsets != null && i < room.residentOffsets.Length)
                    position += room.residentOffsets[i];
                var instance = Spawn(prefab, cell, prefab.name);
                instance.transform.position = position;
                var npc = instance.GetComponent<NPCController>();
                npc.ConfigureConversation(_townFlow.State);
                if (!instance.TryGetComponent<TownDialogueProgress>(out var progress))
                    progress = instance.AddComponent<TownDialogueProgress>();
                if (npc.npcId != "wreath") progress.Prepare(npc);
                _townPeople.Add(npc);
                _npcCells.Add(cell);
            }
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
            Vector3 delta = _player.transform.position - (InMonsterShop ? CellPosition(MonsterShopExit) : HomeEntry(_interior)); delta.y = 0f;
            if (delta.sqrMagnitude > 1.44f) return false;
            StartCoroutine(LeaveInterior()); return true;
        }
        private IEnumerator LeaveInterior()
        {
            if (_changingInterior || !_interior || _carriedItem != null) yield break;
            _changingInterior = true;
            var scene = _interior.gameObject.scene;
            _interior = null;
            _homePanel = 0;
            _homeBeforeHut = null;
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
