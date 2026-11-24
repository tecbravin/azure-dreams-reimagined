using System;
using System.Collections.Generic;
using AzureDreams.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string HomeRankKey = "home.house_rank";
        private const string HutRosterKey = "home.monster_hut";
        private const string HutRankKey = "home.hut_rank";
        private static readonly string[] HutScenes =
        {
            "Interior_25_monster_hut_2x2", "Interior_26_monster_hut_3x3", "Interior_27_monster_hut_4x4",
            "Interior_28_monster_hut_6x6", "Interior_29_monster_hut_8x8"
        };
        private static readonly Vector2Int[][] HutPadLayouts = BuildHutPadLayouts();
        private static readonly string[] HomeScenes =
        {
            "Interior_01_koh_house_1", "Interior_02_koh_house_2", "Interior_03_koh_house_3"
        };
        // Walkable cells next to features visible in the supplied capture views.
        // Centre of the actual blue exit mat: page 149 UV (64..96, 64..96).
        // Keep half-cell precision and mirror it with House 1's geometry.
        private static readonly Vector3 InitialHouseDoor = new(19.5f, 0f, 14.5f);
        // Level 1's Monster Hut passage is the brown mat on the left; the
        // exterior return uses the black doorway tile captured at (12, 13).
        // The old entry duplicated that exact door coordinate, so the hut
        // passage and the house exit sat on the same point and fought over
        // every step near the door.  The captured geometry marks a separate
        // floor tile at (12.5, 8.5), a few steps back from the doorway on
        // the same wall, which is the mat this passage should use instead.
        private static readonly Vector3[] HutLinks = { new(12.5f, 0f, 8.5f), new(32.5f, 0f, 8.75f), new(68f, 0f, 10f) };
        private static int CurrentHutRank => Mathf.Clamp(TownSaveVariables.GetInt(HutRankKey), 0, HutScenes.Length - 1);
        private static Vector2Int[] HutPads => HutPadLayouts[CurrentHutRank];
        private static readonly Vector2Int[] BedroomDoors = { new(16, 12), new(22, 15), new(22, 16) };
        private static readonly Vector2Int[] Beds = { new(11, 12), new(10, 12), new(12, 14) };
        private TownHouseDoor _homeEntranceDoor;
        private static readonly Vector2Int HutDoor = new(26, 10);
        // The capture includes a paved apron outside each room. Keep it off the interior map.
        private static readonly Rect[] HouseFootprints =
        {
            Rect.MinMaxRect(8f, 8f, 23f, 15f),
            Rect.MinMaxRect(8f, 8f, 37f, 20f),
            Rect.MinMaxRect(8f, 8f, 71f, 20f)
        };
        private static readonly Rect HutFootprint = Rect.MinMaxRect(20f, 8f, 29f, 17f);
        private readonly List<GameObject> _hutViews = new();
        private int _homePanel;
        private const float HutFusionRowHeight = 30f;
        private const float HutFusionListHeight = 118f;
        private readonly List<(int Slot, BagFamiliar Familiar)> _hutFusionCandidates = new();
        private BagFamiliar _hutFusionSource;
        private FusionPreview _hutFusionPreview;
        private int _hutFusionTarget = -1;
        private Vector2 _hutFusionScroll;
        private int HutIndex => _interior ? Array.IndexOf(HutScenes, _interior.interiorId) : -1;
        private bool InHomeHut => HutIndex >= 0;
        private int HouseIndex => _interior ? Array.IndexOf(HomeScenes, _interior.interiorId) : -1;

        private static Vector2Int[][] BuildHutPadLayouts()
        {
            var sizes = new[] { 2, 3, 4, 6, 8 };
            var layouts = new Vector2Int[sizes.Length][];
            var pads = new List<Vector2Int>();
            for (int rank = 0; rank < sizes.Length; rank++)
            {
                int size = sizes[rank];
                int right = size <= 4 ? 26 : size == 6 ? 29 : 32;
                for (int row = 0; row < size; row++)
                    for (int column = 0; column < size; column++)
                    {
                        var pad = new Vector2Int(right - 3 * column, 11 + 3 * row);
                        if (!pads.Contains(pad)) pads.Add(pad);
                    }
                layouts[rank] = pads.ToArray();
            }
            return layouts;
        }

        private static readonly Vector2Int HomeDoorCell = new(5, 3);

        private static Vector2Int HomeLayoutCell(int rank, Vector2Int sourceCell) =>
            rank == 0 ? new Vector2Int(32 - sourceCell.x, sourceCell.y) : sourceCell;

        private static Vector3 HomeLayoutPoint(int rank, Vector3 sourcePoint) =>
            rank == 0 ? new Vector3(32f - sourcePoint.x, sourcePoint.y, sourcePoint.z) : sourcePoint;

        // Keep the marker and the arrival cell together.  House 1's captured
        // geometry is mirrored, so using the raw HutLinks coordinate here
        // would leave the visual marker and the touch trigger on opposite
        // sides of the room.
        private static Vector3 HomeHutMarker(int rank) => HomeLayoutPoint(rank, HutLinks[rank]);

        // A return from the hut must not place Koh directly on its touch
        // marker, otherwise the next movement immediately sends him back.
        // +Z is the interior-side (below-the-door) cell for every house rank.
        private static Vector3 HomeHutArrival(int rank) => HomeHutMarker(rank) + Vector3.forward * 2f;

        private static bool IsInHomeBedroom(int rank, Vector2Int cell)
        {
            int doorway = HomeLayoutCell(rank, BedroomDoors[rank]).x;
            return rank == 0 ? cell.x > doorway : cell.x < doorway;
        }

        private void PlaceKohHomeEntrance()
        {
            var cell = HomeDoorCell;
            _homeEntranceDoor = UsingRetailTown ? null : PlaceHouseDoor("Koh's house entrance", cell, 90f);
        }

        private TownHouseDoor PlaceHouseDoor(string label, Vector2Int cell, float yaw)
        {
            Vector3 position = CellPosition(cell);
            position.y = UsingRetailTown ? RetailTownHeight(position) : 0f;
            var instance = CreateTownAsset("House_Door", label, position, Quaternion.Euler(0f, yaw, 0f));
            return instance ? instance.GetComponent<TownHouseDoor>() : null;
        }

        private bool TryFacadeDoor()
        {
            TownHouseDoor closest = null;
            float best = 1.45f * 1.45f;
            foreach (var instance in _generated)
            {
                if (!instance || !instance.TryGetComponent<TownHouseDoor>(out var door) ||
                    door == _homeEntranceDoor) continue;
                var delta = door.Threshold - _player.transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude >= best) continue;
                closest = door;
                best = delta.sqrMagnitude;
            }
            return closest && ToggleHouseDoor(closest);
        }

        private bool ToggleHouseDoor(TownHouseDoor door)
        {
            if (!door) return false;
            if (_facadeInteriors.TryGetValue(door, out var interior) && interior == CherrlHouse)
            {
                if (CherrlHouseUnlocked) door.OpenImmediately();
                else _message = T("The door is locked.", "A porta está trancada.");
                return true;
            }
            return door.Toggle();
        }

        internal static int CurrentHomeRank =>
            Mathf.Clamp(TownSaveVariables.GetInt(HomeRankKey), 0, HomeScenes.Length - 1);

        private static string CurrentHomeScene() => HomeScenes[CurrentHomeRank];

        private void CheckTownEntranceCrossing(Vector3 before, Vector3 after)
        {
            if (TryEnterPoolChangingRoom(before, after)) return;
            if (_interior || _changingInterior || _heldTownPot || _carriedItem != null ||
                _townJumpHeight > TownGroundTolerance || _townJumpVelocity != 0f) return;

            // The pool is entered at its building doorway, not through Wookie's dialogue.
            // Trigger in front of the door leaf so its closed collision cannot prevent entry.
            foreach (var (door, interior) in _facadeInteriors)
            {
                if (!door || interior is not (PoolClosedRoom or PoolOpenRoom) ||
                    !ApproachingPoolDoor(door.Threshold, before, after)) continue;
                var outside = door.Threshold + Vector3.forward * 1.5f;
                outside.y = TownTerrainHeight(outside) + TownActorFootOffset;
                StartCoroutine(LoadInterior(PoolRestored ? PoolOpenRoom : PoolClosedRoom,
                    outside, spawnAboveExitMarker: true));
                return;
            }

            if (_homeEntranceDoor && _homeEntranceDoor.IsPassable && NearHomeCell(HomeDoorCell, 0.52f))
            {
                StartCoroutine(LoadInterior(CurrentHomeScene(), spawnAboveExitMarker: true));
                return;
            }

            // The captured façades face +Z. Enter only when walking into the recess.
            if (after.z >= before.z) return;
            foreach (var project in _builtTownLots)
            {
                if (project.Interior == null) continue;
                // Trigger just outside the solid façade, within the doorway opening.
                float thresholdZ = project.Entrance.y + TownActorRadius + 0.10f;
                if (before.z > thresholdZ && after.z <= thresholdZ)
                {
                    float crossing = (thresholdZ - before.z) / (after.z - before.z);
                    if (Mathf.Abs(Mathf.Lerp(before.x, after.x, crossing) - project.Entrance.x) <= 0.30f)
                    {
                        EnterConstruction(project);
                        return;
                    }
                }
            }
            foreach (var (door, interior) in _facadeInteriors)
            {
                if (!door || !door.IsPassable) continue;
                if (interior == CherrlHouse && !CherrlHouseUnlocked) continue;
                var threshold = door.Threshold;
                float entranceZ = threshold.z + 0.10f;
                if (before.z <= entranceZ || after.z > entranceZ) continue;
                float crossing = (entranceZ - before.z) / (after.z - before.z);
                if (Mathf.Abs(Mathf.Lerp(before.x, after.x, crossing) - threshold.x) > 0.30f) continue;
                var outside = new Vector3(threshold.x, 0f, threshold.z + 1f);
                outside.y = TownTerrainHeight(outside) + TownActorFootOffset;
                string destination = interior == "KohHome" ? CurrentHomeScene() : interior;
                StartCoroutine(LoadInterior(destination, outside, spawnAboveExitMarker: true));
                return;
            }
        }

        private static bool ApproachingPoolDoor(Vector3 threshold, Vector3 before, Vector3 after) =>
            after.z < before.z && Mathf.Abs(after.x - threshold.x) <= 0.55f &&
            after.z >= threshold.z - 0.15f && after.z <= threshold.z + 0.85f;

        private void PlaceHomeMarker(string label, Vector3 position, Material material)
        {
            position = UsingRetailTown ? RetailTownActorPosition(position, 0.035f) : new Vector3(position.x, 0.035f, position.z);
            CreatePrimitive(PrimitiveType.Cylinder, label, position, new Vector3(0.68f, 0.035f, 0.68f), material, false);
        }

        private Vector3 HomeEntry(TownInteriorScene room)
        {
            int index = Array.IndexOf(HomeScenes, room.interiorId);
            if (index == 0) return HomeLayoutPoint(index, InitialHouseDoor);
            int hut = Array.IndexOf(HutScenes, room.interiorId);
            return hut > 0 ? CellPosition(HutDoor) : RetailInteriorDoor(room);
        }

        // Exit squares of the captured interiors.  Every room marks its way
        // out with the blue arrow mat (texture page 149) set into the doorway
        // of the front wall; these are the centres of those mats in the
        // captured geometry.  The pool is the exception: its mat sits on the
        // top edge of the tiled platform, so Koh walks in downwards.
        private static readonly Dictionary<string, (Vector3 door, Vector3 inward)> RetailInteriorDoors =
            new(StringComparer.Ordinal)
        {
            ["Interior_07_library"] = (new Vector3(19.5f, 0f, 19.5f), Vector3.back),
            ["Interior_08_gym"] = (new Vector3(14.5f, 0f, 18.5f), Vector3.back),
            ["Interior_09_arcade_1"] = (new Vector3(12.5f, 0f, 18.5f), Vector3.back),
            ["Interior_0a_racetrack_entrance"] = (new Vector3(22.5f, 0f, 16.5f), Vector3.back),
            ["Interior_0b_theater"] = (new Vector3(13.5f, 0f, 27.5f), Vector3.back),
            ["Interior_0c_alley"] = (new Vector3(20.5f, 0f, 21.5f), Vector3.back),
            ["Interior_0d_pool_quest_done"] = (new Vector3(13.75f, 0f, 11.5f), Vector3.forward),
            ["Interior_0d_pool_quest_not_done"] = (new Vector3(13.75f, 0f, 11.5f), Vector3.forward),
            ["Interior_0e_restaurant"] = (new Vector3(17.5f, 0f, 18.5f), Vector3.back),
            ["Interior_0f_hospital"] = (new Vector3(13.5f, 0f, 16.5f), Vector3.back),
            ["Interior_10_hospital_2"] = (new Vector3(19.5f, 0f, 19.5f), Vector3.back),
            ["Interior_11_temple"] = (new Vector3(16.75f, 0f, 19.5f), Vector3.back),
            ["Interior_12_temple_2"] = (new Vector3(22.75f, 0f, 29.5f), Vector3.back),
            ["Interior_13_carpenter"] = (new Vector3(14.5f, 0f, 16.5f), Vector3.back),
            ["Interior_15_blacksmith"] = (new Vector3(14.5f, 0f, 17.5f), Vector3.back),
            ["Interior_16_general_store"] = (new Vector3(13.5f, 0f, 16.5f), Vector3.back),
            ["Interior_17_fortune_house"] = (new Vector3(16f, 0f, 16.5f), Vector3.back),
            ["Interior_18_cherrl_house"] = (new Vector3(15.5f, 0f, 17.5f), Vector3.back),
            ["Interior_19_generic_building"] = (new Vector3(15.5f, 0f, 17.5f), Vector3.back),
            ["Interior_1a_generic_building"] = (new Vector3(15.5f, 0f, 17.5f), Vector3.back),
            ["Interior_1b_generic_building"] = (new Vector3(15.5f, 0f, 17.5f), Vector3.back),
            ["Interior_1c_generic_building"] = (new Vector3(15.5f, 0f, 17.5f), Vector3.back),
            ["Interior_1d_generic_building"] = (new Vector3(15.5f, 0f, 17.5f), Vector3.back),
            ["Interior_20_bar"] = (new Vector3(17.5f, 0f, 19.5f), Vector3.back),
            ["Interior_21_rhodes_house"] = (new Vector3(17.5f, 0f, 19.5f), Vector3.back),
            ["Interior_22_nico_house"] = (new Vector3(15.5f, 0f, 17.5f), Vector3.back),
            ["Interior_23_generic_house"] = (new Vector3(15.5f, 0f, 17.5f), Vector3.back),
            ["Interior_24_dr_hal_house"] = (new Vector3(12.5f, 0f, 15.5f), Vector3.back),
            ["Interior_2b_casino_1"] = (new Vector3(15.5f, 0f, 18.5f), Vector3.back),
            // The roulette room has no street door: its mat is the passage to the slot room.
            ["Interior_06_casino_2"] = (new Vector3(23.5f, 0f, 16f), Vector3.left),
            ["Interior_2c_silver_house"] = (new Vector3(15.5f, 0f, 17.5f), Vector3.back),
        };

        private static Vector3 RetailInteriorDoor(TownInteriorScene room) =>
            room && RetailInteriorDoors.TryGetValue(room.interiorId, out var link) ? link.door : room.entryPosition;

        // Direction from the exit mat into the room.  Arrivals stand two
        // squares along it so the first step never retriggers the exit.
        private Vector3 InteriorDoorInward(TownInteriorScene room)
        {
            if (room && RetailInteriorDoors.TryGetValue(room.interiorId, out var link)) return link.inward;
            if (InHomeHut) return Vector3.forward;
            return Vector3.back;
        }

        private void SetupKohHomeInterior()
        {
            _homePanel = 0;
            _homeEntranceDoor = null;
            int index = HouseIndex;
            if (index >= 0)
            {
                PlaceHomeMarker("Bath passage", BathDoorPosition(index), _accentMaterial);
                PlaceHomeMarker("Monster hut passage", HomeHutMarker(index), _accentMaterial);
                ApplyPurchasedHomeItems();
                SetupHomeBathScene(index);
                _message = string.Empty;
            }
            else if (InHomeHut)
            {
                RefreshHutMonsters();
                _message = string.Empty;
            }
        }

        private bool NearHomeCell(Vector2Int cell, float reach = 1.2f) => NearHomePoint(CellPosition(cell), reach);

        private bool NearHomePoint(Vector3 point, float reach)
        {
            var delta = point - _player.transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= reach * reach;
        }

        private void MoveThroughBathPassage(Vector2Int destination)
        {
            _playerCell = destination;
            _player.transform.position = RetailTownActorPosition(CellPosition(destination), TownActorFootOffset);
            _townJumpHeight = _townJumpVelocity = _townAirSpeedLimit = 0f;
            _townMomentum = Vector2.zero;
            if (_kohSprite) _kohSprite.SetAirborne(false);
            UpdateCameraFollow(snap: true);
        }

        private bool TryKohHomeInteraction()
        {
            if (!_player || _heldTownPot) return false;
            if (!_interior)
            {
                if (NearHomeCell(HomeDoorCell, 1.35f) && ToggleHouseDoor(_homeEntranceDoor))
                {
                    if (_homeEntranceDoor.IsOpeningOrOpen) _message = T("Walk through the open door to enter Koh's house.", "Passe pela porta aberta para entrar na casa do Koh.");
                    return true;
                }
                return TryFacadeDoor();
            }

            if (TryHomeStorageShelf()) return true;
            int house = HouseIndex;
            if (house >= 0 && NearHomePoint(BathDoorPosition(house), 1.2f))
            {
                BeginHomeBath(house);
                return true;
            }
            if (house >= 0 && NearHomeCell(HomeLayoutCell(house, Beds[house]), 1.4f))
            {
                _health = _playerDefinition ? _playerDefinition.MaxHealth : 20;
                if (_player.TryGetComponent<CharacterStats>(out var stats)) stats.currentHealth = _health;
                _message = T("Koh rests in his bed.", "Koh descansa na cama.");
                return true;
            }
            if (house >= 0 && NearHomePoint(HomeHutMarker(house), 0.95f))
            {
                StartCoroutine(LoadInterior(HutScenes[CurrentHutRank]));
                return true;
            }
            if (InHomeHut)
            {
                var exit = HomeEntry(_interior) - _player.transform.position;
                exit.y = 0f;
                if (exit.sqrMagnitude <= 0.85f * 0.85f)
                {
                    // Always resolve the house from the current saved upgrade
                    // level. This also keeps the return correct after a home
                    // upgrade while Koh is visiting the Monster Hut.
                    StartCoroutine(LoadInterior(CurrentHomeScene()));
                    return true;
                }
                int nearest = -1;
                float closest = 1.5f * 1.5f;
                var pads = HutPadLayouts[HutIndex];
                for (int i = 0; i < pads.Length && i < HutPads.Length; i++)
                {
                    var delta = CellPosition(pads[i]) - _player.transform.position;
                    delta.y = 0f;
                    if (delta.sqrMagnitude >= closest) continue;
                    nearest = i;
                    closest = delta.sqrMagnitude;
                }
                if (nearest >= 0) { _hutSlot = nearest; _hutFusionTarget = -1; _homePanel = 2; return true; }
            }
            return false;
        }

        private bool HandleKohHomeInput()
        {
            if (!_interior || (HouseIndex < 0 && !InHomeHut)) { _homePanel = 0; return false; }
            var keys = Keyboard.current;
            if (_homePanel != 0 && keys != null && (_cancel?.WasPressedThisFrame() == true))
            { _homePanel = _homePanel == 3 ? 2 : 0; return true; }
            if (_homePanel == 3)
            {
                int delta = MenuDirectionPressed(1) ? -1 : MenuDirectionPressed(2) ? 1 : 0;
                if (delta != 0 && _hutFusionCandidates.Count > 0)
                    SelectHutFusionTarget(Mathf.Clamp(_hutFusionTarget + delta, 0, _hutFusionCandidates.Count - 1));
                else if (_interact?.WasPressedThisFrame() == true && _hutFusionCandidates.Count > 0)
                {
                    if (_hutFusionTarget < 0) SelectHutFusionTarget(0);
                    else ConfirmHutFusion();
                }
                return true;
            }
            if (InHomeHut && keys != null && keys.mKey.wasPressedThisFrame && !_activeTownNpc && !_interiorMenu)
            { _homePanel = _homePanel == 1 ? 0 : 1; return true; }
            return _homePanel != 0;
        }

        private int _hutSlot;
        private Vector2 _hutEggScroll;
        private string _hutStatusEntry;
        private ActorRecord _hutStatusActor;

        private static List<string> ReadHutRoster()
        {
            var slots = new List<string>(TownSaveVariables.GetString(HutRosterKey)
                .Split('|', StringSplitOptions.RemoveEmptyEntries));
            while (slots.Count < HutPads.Length) slots.Add(EmptyHutSlot);
            return slots;
        }

        private ActorDefinition HutDefinition(string id)
        {
            id = FamiliarSlotId(id);
            if (id == "kewne") return _kewneDefinition;
            if (id == "recruited") return _creatureDefinition;
            return EggFamiliarDefinition(ReadHatchedSlot(id));
        }

        private void RefreshHutMonsters()
        {
            foreach (var view in _hutViews)
                if (view) RemoveGenerated(view);
            _hutViews.Clear();
            var roster = ReadHutRoster();
            if (!InHomeHut) return;
            var pads = HutPadLayouts[HutIndex];
            for (int i = 0; i < pads.Length && i < HutPads.Length; i++)
            {
                var cell = pads[i];
                if (TryReadHutEgg(roster[i], out var egg))
                {
                    var view = CreateEggView(cell, egg, RetailTownHeight(CellPosition(cell)) + WorldItemHeight);
                    if (view)
                    {
                        AddStoredFamiliarLight(view);
                        _hutViews.Add(view);
                    }
                    continue;
                }
                var definition = HutDefinition(roster[i]);
                if (!definition) continue;
                const int kewneSpecies = (int)AzureDreams.Core.MonsterSpecies.KEWNE;
                int species = FamiliarSlotId(roster[i]) == "kewne" ? kewneSpecies : ReadHatchedSlot(roster[i]);
                var prefab = species == kewneSpecies && _kewnePrefab
                    ? _kewnePrefab : species > 0 ? HatchedFamiliarPrefab(species) : _creaturePrefab;
                if (!prefab) continue;
                var record = ReadStoredFamiliarActor(roster[i], definition);
                int paletteSpecies = record.Species > 0 ? record.Species : species;
                var familiar = SpawnActor(prefab, cell, "Stored familiar - " + definition.DisplayName,
                    species: paletteSpecies, palette: FamiliarPalette(paletteSpecies, record), groundedIdle: true);
                familiar.transform.position = RetailTownActorPosition(familiar.transform.position, TownActorFootOffset);
                AddStoredFamiliarLight(familiar);
                PresentTrollEquipment(familiar, record);
                _hutViews.Add(familiar);
            }
        }

        private void DrawKohHomePanel(float width, float height)
        {
            if (_homePanel == 1) { DrawHomeFloorPlan(); return; }
            if (!InHomeHut) return;
            if (_homePanel == 3) { DrawHutFusion(width, height); return; }
            if (_homePanel != 2) return;
            var roster = ReadHutRoster();
            string slot = roster[_hutSlot];
            var storedFamiliar = HutDefinition(slot);
            if (storedFamiliar)
            {
                DrawHutFamiliarStatus(roster, slot, storedFamiliar);
                return;
            }
            BeginHudMenu(new Rect(128, 72, 384, 336));
            GUILayout.Label(T("Monster hut", "Alojamento de monstros") + T(" - slot ", " - vaga ") + $"{_hutSlot + 1}/{HutPads.Length}");
            GUILayout.Label(_familiarDefinition ? T("Traveling with Koh: ", "Viajando com Koh: ") + _familiarDefinition.DisplayName : T("No traveling familiar", "Sem acompanhante"));
            bool changed = false;
            if (TryReadHutEgg(slot, out var egg))
            {
                GUILayout.Label(DescribeEgg(egg));
                if (FindEggSpecies(egg.Subtype) != null && GUILayout.Button(T("Hatch", "Chocar")))
                {
                    roster[_hutSlot] = HatchedPrefix + egg.Subtype;
                    _message = FindEggSpecies(egg.Subtype).name + T(" hatched at level 1!", " nasceu no nível 1!");
                    changed = true;
                }
                else if (_townInventory.HasSpace() && GUILayout.Button(T("Take egg back", "Retirar ovo")))
                {
                    _townInventory.AddDungeonItem(MonsterEgg.Identify(egg));
                    roster[_hutSlot] = EmptyHutSlot;
                    changed = true;
                }
            }
            else if (slot == EmptyHutSlot)
            {
                string active = !_familiarDefinition ? null : _hatchedSpecies > 0 ? HatchedPrefix + _hatchedSpecies :
                    _familiarDefinition == _kewneDefinition ? "kewne" : _familiarDefinition == _creatureDefinition ? "recruited" : null;
                if (active != null && GUILayout.Button(T("Store traveling familiar", "Guardar acompanhante")))
                {
                    roster[_hutSlot] = EncodeTravelingFamiliar();
                    TownSaveVariables.SetString(FamiliarNicknameKey, "");
                    _familiarDefinition = null;
                    _hatchedSpecies = 0;
                    _familiarActor = new AzureDreams.Core.ActorRecord();
                    changed = true;
                }
                for (int i = 0; !changed && i < _bagFamiliars.Count; i++)
                {
                    var reserve = _bagFamiliars[i];
                    if (!GUILayout.Button(T("Store ", "Guardar ") + reserve.Name)) continue;
                    roster[_hutSlot] = EncodeFamiliar(reserve.Definition, reserve.Species, reserve.Actor, reserve.Nickname);
                    _bagFamiliars.RemoveAt(i);
                    changed = true;
                }
                _hutEggScroll = GUILayout.BeginScrollView(_hutEggScroll, GUILayout.MaxHeight(204));
                var carried = _townInventory.DungeonItems;
                for (int i = 0; !changed && i < carried.Count; i++)
                {
                    var item = carried[i];
                    if (item.Kind != MonsterEgg.Kind || !GUILayout.Button(T("Place ", "Colocar ") + DescribeEgg(item))) continue;
                    if (_townInventory.ConsumeDungeonItem(i, MonsterEgg.Kind))
                    {
                        roster[_hutSlot] = EncodeHutEgg(MonsterEgg.Identify(item));
                        changed = true;
                    }
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
            DrawHutCloseButton(new Rect(128, 72, 384, 336));
            if (changed) SaveHutRoster(roster);
        }

        private void OpenHutFusion(List<string> roster)
        {
            _hutFusionSource = HutFusionFamiliar(roster[_hutSlot]);
            if (_hutFusionSource == null) return;
            _hutFusionCandidates.Clear();
            for (int i = 0; i < HutPads.Length; i++)
            {
                if (i == _hutSlot) continue;
                var candidate = HutFusionFamiliar(roster[i]);
                if (candidate != null) _hutFusionCandidates.Add((i, candidate));
            }
            _hutFusionTarget = -1;
            _hutFusionPreview = null;
            _hutFusionScroll = Vector2.zero;
            _homePanel = 3;
            PlayRetailSound(RetailSfx.Confirm);
        }

        private void SelectHutFusionTarget(int index)
        {
            if (index == _hutFusionTarget) return;
            _hutFusionTarget = index;
            float top = index * HutFusionRowHeight;
            _hutFusionScroll.y = Mathf.Clamp(_hutFusionScroll.y, top + HutFusionRowHeight - HutFusionListHeight, top);
            var target = _hutFusionCandidates[index].Familiar;
            _hutFusionPreview = BuildFusionPreview(_hutFusionSource.Actor, _hutFusionSource.Name, target.Actor, target.Name);
            PlayRetailSound(RetailSfx.Cursor);
        }

        private void ConfirmHutFusion()
        {
            var preview = _hutFusionPreview;
            if (preview == null) return;
            var (slot, second) = _hutFusionCandidates[_hutFusionTarget];
            var first = _hutFusionSource;
            var survivor = preview.BlueSurvives ? second : first;
            var roster = ReadHutRoster();
            RestoreTownFamiliarVitals(preview.Result);
            roster[_hutSlot] = EncodeFamiliar(survivor.Definition, survivor.Species, preview.Result, survivor.Nickname);
            roster[slot] = EmptyHutSlot;
            _hutStatusActor = null;
            _hutFusionPreview = null;
            _homePanel = 0;
            SaveHutRoster(roster);
            PlayHutFusionCue();
            _message = T($"{first.Name} was fused with {second.Name}.\nResult: {preview.Name}.",
                $"{first.Name} foi fundido com {second.Name}.\nResultado: {preview.Name}.");
        }

        private void DrawHutFusion(float width, float height)
        {
            float panelWidth = Mathf.Min(420f, width - 24f);
            const float panelHeight = 422f;
            var panel = new Rect((width - panelWidth) * 0.5f, (height - panelHeight) * 0.5f, panelWidth, panelHeight);
            DrawDialogueFrame(panel);
            float x = panel.x + 12f, innerWidth = panel.width - 24f;
            DrawInventoryFittedText(new Rect(x, panel.y + 9f, innerWidth - 24f, 24f),
                T("Choose a stored familiar", "Escolha um familiar guardado"), _bodyStyle);
            DrawHutCloseButton(panel);
            DrawInventoryFittedText(new Rect(x, panel.y + 37f, innerWidth, 22f),
                T($"First familiar: {_hutFusionSource.Name}", $"Primeiro familiar: {_hutFusionSource.Name}"), _messageStyle);

            var list = new Rect(x, panel.y + 65f, innerWidth, HutFusionListHeight);
            if (_hutFusionCandidates.Count == 0)
                DrawInventoryParagraph(list, T("There are no other familiars in the hut.", "Não há outro familiar no alojamento."));
            else
            {
                float rowWidth = list.width - 18f;
                _hutFusionScroll = GUI.BeginScrollView(list, _hutFusionScroll,
                    new Rect(0f, 0f, rowWidth, _hutFusionCandidates.Count * HutFusionRowHeight));
                for (int i = 0; i < _hutFusionCandidates.Count; i++)
                {
                    var (slot, familiar) = _hutFusionCandidates[i];
                    if (DrawInventoryFittedButton(new Rect(0f, i * HutFusionRowHeight, rowWidth, HutFusionRowHeight - 2f),
                        $"{slot + 1}. {familiar.Name}", i == _hutFusionTarget))
                        SelectHutFusionTarget(i);
                }
                GUI.EndScrollView();
            }

            var preview = _hutFusionPreview;
            if (preview != null)
            {
                DrawFusionResultHeader(new Rect(x, panel.y + 192f, innerWidth, 44f), preview);
                DrawFusionResultStatsChart(new Rect(x, panel.y + 242f, innerWidth, 98f), preview.Result);
                DrawInventoryParagraph(new Rect(x, panel.y + 345f, innerWidth, 36f),
                    DescribeTalents(preview.Result), 2);
                if (DrawInventoryFittedButton(new Rect(x, panel.yMax - 36f, (innerWidth - 8f) * 0.5f, 26f),
                    WithInteractHint("Confirm fusion", "Confirmar fusão")))
                    ConfirmHutFusion();
            }
            else if (_hutFusionTarget >= 0)
            {
                DrawInventoryParagraph(new Rect(x, panel.y + 192f, innerWidth, 60f),
                    T("These familiars cannot fuse.", "Esses familiares não podem se fundir."));
            }
            if (DrawInventoryFittedButton(new Rect(panel.center.x + 4f, panel.yMax - 36f, (innerWidth - 8f) * 0.5f, 26f),
                WithCancelHint("Back", "Voltar")))
                _homePanel = 2;
        }

        private BagFamiliar HutFusionFamiliar(string slot)
        {
            var definition = HutDefinition(slot);
            if (!definition) return null;
            var actor = ReadStoredFamiliarActor(slot, definition);
            RestoreTownFamiliarVitals(actor);
            return new BagFamiliar
            {
                Definition = definition,
                Species = ReadHatchedSlot(slot),
                Actor = actor,
                Nickname = HutFamiliarNickname(slot)
            };
        }

        private void DrawHutFamiliarStatus(List<string> roster, string slot, ActorDefinition definition)
        {
            int hatchedSpecies = ReadHatchedSlot(slot);
            if (_hutStatusActor == null || _hutStatusEntry != slot)
            {
                var actor = ReadStoredFamiliarActor(slot, definition);
                RestoreTownFamiliarVitals(actor);
                _hutStatusActor = actor;
                _hutStatusEntry = slot;
            }

            var stored = _hutStatusActor;
            var panel = new Rect(176f, 80f, 288f, 290f);
            DrawDialogueFrame(panel);
            string nickname = HutFamiliarNickname(slot);
            DrawActorStatus(panel, string.IsNullOrEmpty(nickname) ? definition.DisplayName : nickname, stored, true,
                stored.Hp, stored.MaxHp, stored.CurrentAttack, stored.CurrentDefense);
            DrawHudText(new Rect(panel.x + 10f, panel.y + 208f, panel.width - 20f, 46f),
                T("Talents: ", "Talentos: ") + DescribeTalents(stored), _messageStyle);

            if (DrawInventoryFittedButton(new Rect(panel.x + 115f, panel.yMax - 32f, 80f, 24f), T("Fuse", "Fundir")))
                OpenHutFusion(roster);

            if (GUI.Button(new Rect(panel.x + 10f, panel.yMax - 32f, 100f, 24f), T("Take", "Retirar")))
            {
                var familiar = new BagFamiliar { Definition = definition, Species = hatchedSpecies,
                    Actor = stored, Nickname = nickname };
                _bagFamiliars.Add(familiar);
                if (!_familiarDefinition) ActivateBagFamiliar(familiar);
                _hutStatusActor = null;
                roster[_hutSlot] = EmptyHutSlot;
                _homePanel = 0;
                RefreshInventoryCommands();
                SaveHutRoster(roster);
            }
            DrawHutCloseButton(panel);
        }

        private void DrawHutCloseButton(Rect panel)
        {
            if (GUI.Button(new Rect(panel.xMax - 27f, panel.y + 5f, 22f, 22f),
                new GUIContent("X", WithCancelHint("Close", "Fechar"))))
            {
                PlayRetailSound(RetailSfx.Cancel);
                _homePanel = 0;
            }
        }

        private void SaveHutRoster(List<string> roster)
        {
            TownSaveVariables.SetString(HutRosterKey, string.Join("|", roster));
            PersistTownProgress();
            RefreshHutMonsters();
        }

        private void DrawHomeFloorPlan()
        {
            if (Event.current.type != EventType.Repaint || _townHeights == null) return;
            var bounds = new Rect(108, 80, 424, 310);
            DrawHudPanel(bounds);
            GUI.Label(new Rect(125, 91, 390, 25), T("Monster hut - floor plan", "Planta do alojamento"));
            var mapBounds = _homeMapBounds;
            float scale = Mathf.Min(386f / mapBounds.width, 200f / mapBounds.height);
            Vector2 origin = new(320f - mapBounds.width * scale * 0.5f, 226f - mapBounds.height * scale * 0.5f);
            Rect Tile(Vector2Int p, float inset) => new(origin.x + (mapBounds.xMax - 1 - p.x) * scale + inset,
                origin.y + (p.y - mapBounds.yMin) * scale + inset, scale - 2f * inset, scale - 2f * inset);
            foreach (var tile in _homeMapCells) DrawHudFill(Tile(tile, 0.15f), new Color(0.38f, 0.31f, 0.23f));
            foreach (var pad in HutPadLayouts[HutIndex]) DrawHudFill(Tile(pad, 0f), Color.magenta);
            DrawHudFill(Tile(HomeExitMapCell(), 0f), Color.yellow);
            DrawHudFill(Tile(_playerCell, scale * 0.19f), Color.red);
            GUI.Label(new Rect(125, 352, 400, 24), T("Red: Koh  Yellow: exit  Purple: storage slot", "Vermelho: Koh  Amarelo: saída  Roxo: vaga"));
        }

        private Vector2Int HomeExitMapCell() => HomeMapCell(HomeEntry(_interior));

        private static Vector2Int HomeMapCell(Vector3 position) =>
            new(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.z));

        private readonly List<Vector2Int> _homeMapCells = new();
        private RectInt _homeMapBounds;
        private bool WithinKohHome(Vector3 position)
        {
            int index = HouseIndex;
            var tilePosition = new Vector2(position.x, position.z);
            if (index >= 0)
            {
                var footprint = HouseFootprints[index];
                if (index == 0)
                    footprint = Rect.MinMaxRect(32f - footprint.xMax, footprint.yMin,
                        32f - footprint.xMin, footprint.yMax);
                return footprint.Contains(tilePosition);
            }
            int hut = HutIndex;
            if (hut <= 0) return hut < 0 || HutFootprint.Contains(tilePosition);
            var last = HutPadLayouts[hut][^1];
            int right = hut <= 2 ? 26 : hut == 3 ? 29 : 32;
            return Rect.MinMaxRect(last.x - 2f, 8f, right + 3f, last.y + 3f).Contains(tilePosition);
        }

        private void CacheHomeFloorPlan()
        {
            _homeMapCells.Clear();
            if (!InHomeHut) return;
            var min = Vector2Int.one * RetailTownSize;
            var max = Vector2Int.zero;
            for (int z = 0; z < RetailTownSize; z++)
                for (int x = 0; x < RetailTownSize; x++)
                {
                    var tile = new Vector2Int(x, z);
                    var position = CellPosition(tile);
                    if (!WithinKohHome(position) || RetailTownHeight(position) < -0.75f) continue;
                    _homeMapCells.Add(tile);
                    min = Vector2Int.Min(min, tile);
                    max = Vector2Int.Max(max, tile);
                }
            _homeMapBounds = new RectInt(min, Vector2Int.Max(Vector2Int.one, max - min + Vector2Int.one));
        }
    }
}
