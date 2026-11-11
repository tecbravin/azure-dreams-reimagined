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
        private static readonly Vector2Int InitialHouseDoor = new(20, 14);
        private static readonly Vector2Int[] BathPoints = { new(20, 11), new(34, 13), new(54, 14) };
        private static readonly Vector2Int[] BathDoors = { new(12, 10), new(17, 10), new(20, 10) };
        private static readonly Vector3[] HutLinks = { new(21f, 0f, 13f), new(32.5f, 0f, 8.75f), new(68f, 0f, 10f) };
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
        private string _homeBeforeHut;
        private int _homePanel;
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

        private bool ToggleHouseDoor(TownHouseDoor door) => door && door.Toggle();

        internal static int CurrentHomeRank =>
            Mathf.Clamp(TownSaveVariables.GetInt(HomeRankKey), 0, HomeScenes.Length - 1);

        private static string CurrentHomeScene() => HomeScenes[CurrentHomeRank];

        private void CheckTownEntranceCrossing(Vector3 before, Vector3 after)
        {
            if (_interior || _changingInterior || _heldTownPot || _carriedItem != null ||
                _townJumpHeight > TownGroundTolerance || _townJumpVelocity != 0f) return;

            if (_homeEntranceDoor && _homeEntranceDoor.IsPassable && NearHomeCell(HomeDoorCell, 0.52f))
            {
                StartCoroutine(LoadInterior(CurrentHomeScene()));
                return;
            }

            // The captured façades face +Z. Enter only when walking into the recess.
            if (after.z >= before.z) return;
            var mansion = BuiltKohHome();
            if (mansion != null)
            {
                // Trigger just outside the solid façade, within the doorway opening.
                float thresholdZ = mansion.Entrance.y + TownActorRadius + 0.10f;
                if (before.z > thresholdZ && after.z <= thresholdZ)
                {
                    float crossing = (thresholdZ - before.z) / (after.z - before.z);
                    if (Mathf.Abs(Mathf.Lerp(before.x, after.x, crossing) - mansion.Entrance.x) <= 0.30f)
                    {
                        EnterConstruction(mansion);
                        return;
                    }
                }
            }
            foreach (var (door, interior) in _facadeInteriors)
            {
                if (!door || !door.IsPassable) continue;
                var threshold = door.Threshold;
                float entranceZ = threshold.z + 0.10f;
                if (before.z <= entranceZ || after.z > entranceZ) continue;
                float crossing = (entranceZ - before.z) / (after.z - before.z);
                if (Mathf.Abs(Mathf.Lerp(before.x, after.x, crossing) - threshold.x) > 0.30f) continue;
                var outside = new Vector3(threshold.x, 0f, threshold.z + 1f);
                outside.y = TownTerrainHeight(outside) + TownActorFootOffset;
                string destination = interior == "KohHome" ? CurrentHomeScene() : interior;
                StartCoroutine(LoadInterior(destination, outside));
                return;
            }
        }

        private bool BedroomPassageOpen(Vector3 position)
        {
            int house = HouseIndex;
            if (house < 0) return true;
            var cell = BedroomDoors[house];
            if (Mathf.Abs(position.x - cell.x) >= 0.28f) return true;
            return Mathf.Abs(position.z - cell.y) < 0.65f;
        }

        private void PlaceHomeMarker(string label, Vector3 position, Material material)
        {
            position = UsingRetailTown ? RetailTownActorPosition(position, 0.035f) : new Vector3(position.x, 0.035f, position.z);
            CreatePrimitive(PrimitiveType.Cylinder, label, position, new Vector3(0.68f, 0.035f, 0.68f), material, false);
        }

        private Vector3 HomeEntry(TownInteriorScene room)
        {
            int index = Array.IndexOf(HomeScenes, room.interiorId);
            if (index == 0) return CellPosition(InitialHouseDoor);
            int hut = Array.IndexOf(HutScenes, room.interiorId);
            return hut > 0 ? CellPosition(HutDoor) : room.entryPosition;
        }

        private void SetupKohHomeInterior()
        {
            _homePanel = 0;
            _homeEntranceDoor = null;
            int index = HouseIndex;
            if (index >= 0)
            {
                PlaceHomeMarker("Bath passage", CellPosition(BathDoors[index]), _accentMaterial);
                PlaceHomeMarker("Monster hut passage", HutLinks[index], _accentMaterial);
                _message = T("Koh's house. M: floor plan. E: use marked locations.", "Casa do Koh. M: planta. E: interagir nos pontos marcados.");
            }
            else if (InHomeHut)
            {
                RefreshHutMonsters();
                _message = T("Monster hut. E at a storage pad: manage familiars. E at the entrance: return.", "Alojamento. E junto a uma vaga: gerenciar familiares. E na entrada: voltar.");
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
            if (house >= 0 && NearHomeCell(BathDoors[house], 0.9f))
            {
                BeginHomeBath(house);
                return true;
            }
            if (house >= 0 && NearHomeCell(BathPoints[house], 0.75f))
            {
                MoveThroughBathPassage(BathDoors[house]);
                return true;
            }
            if (house >= 0 && NearHomeCell(Beds[house], 1.4f))
            {
                _health = _playerDefinition ? _playerDefinition.MaxHealth : 20;
                if (_player.TryGetComponent<CharacterStats>(out var stats)) stats.currentHealth = _health;
                _message = T("Koh rests in his bed.", "Koh descansa na cama.");
                return true;
            }
            if (house >= 0 && NearHomePoint(HutLinks[house], 0.95f))
            {
                _homeBeforeHut = _interior.interiorId;
                StartCoroutine(LoadInterior(HutScenes[CurrentHutRank]));
                return true;
            }
            if (InHomeHut)
            {
                var exit = HomeEntry(_interior) - _player.transform.position;
                exit.y = 0f;
                if (exit.sqrMagnitude <= 0.85f * 0.85f)
                {
                    string destination = string.IsNullOrEmpty(_homeBeforeHut) ?
                        CurrentHomeScene() : _homeBeforeHut;
                    StartCoroutine(LoadInterior(destination));
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
                if (nearest >= 0) { _hutSlot = nearest; _homePanel = 2; return true; }
            }
            return false;
        }

        private bool HandleKohHomeInput()
        {
            if (!_interior || (HouseIndex < 0 && !InHomeHut)) { _homePanel = 0; return false; }
            var keys = Keyboard.current;
            if (_homePanel != 0 && keys != null && keys.escapeKey.wasPressedThisFrame)
            { _homePanel = 0; return true; }
            if (keys != null && keys.mKey.wasPressedThisFrame && !_activeTownNpc && !_interiorMenu)
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
                    if (view) _hutViews.Add(view);
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
                _hutViews.Add(familiar);
            }
        }

        private void DrawKohHomePanel()
        {
            if (_homePanel == 1) { DrawHomeFloorPlan(); return; }
            if (_homePanel != 2 || !InHomeHut) return;
            var roster = ReadHutRoster();
            string slot = roster[_hutSlot];
            var storedFamiliar = HutDefinition(slot);
            if (storedFamiliar)
            {
                DrawHutFamiliarStatus(roster, slot, storedFamiliar);
                return;
            }
            GUILayout.BeginArea(new Rect(128, 72, 384, 336), GUI.skin.box);
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
            if (GUILayout.Button(T("Close [Esc]", "Fechar [Esc]"))) _homePanel = 0;
            GUILayout.EndArea();
            if (changed) SaveHutRoster(roster);
        }

        private void DrawHutFamiliarStatus(List<string> roster, string slot, ActorDefinition definition)
        {
            int hatchedSpecies = ReadHatchedSlot(slot);
            if (_hutStatusActor == null || _hutStatusEntry != slot)
            {
                var actor = ReadStoredFamiliarActor(slot, definition);
                actor.Hp = actor.MaxHp;
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
            if (GUI.Button(new Rect(panel.xMax - 110f, panel.yMax - 32f, 100f, 24f), T("Close [Esc]", "Fechar [Esc]")))
                _homePanel = 0;
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
            GUI.Label(new Rect(125, 91, 390, 25), InHomeHut ? T("Monster hut - floor plan", "Planta do alojamento") : T("Koh's house - floor plan", "Planta da casa do Koh"));
            var mapBounds = _homeMapBounds;
            float scale = Mathf.Min(386f / mapBounds.width, 200f / mapBounds.height);
            Vector2 origin = new(320f - mapBounds.width * scale * 0.5f, 226f - mapBounds.height * scale * 0.5f);
            Rect Tile(Vector2Int p, float inset) => new(origin.x + (mapBounds.xMax - 1 - p.x) * scale + inset,
                origin.y + (p.y - mapBounds.yMin) * scale + inset, scale - 2f * inset, scale - 2f * inset);
            foreach (var tile in _homeMapCells) DrawHudFill(Tile(tile, 0.15f), new Color(0.38f, 0.31f, 0.23f));
            int index = HouseIndex;
            if (index >= 0)
            {
                DrawHudFill(Tile(BathDoors[index], 0f), Color.cyan);
                DrawHudFill(Tile(Beds[index], 0f), new Color(0.4f, 0.65f, 1f));
                DrawHudFill(Tile(HomeMapCell(HutLinks[index]), 0f), Color.magenta);
                DrawHudFill(Tile(HomeExitMapCell(), 0f), Color.yellow);
            }
            else
            {
                foreach (var pad in HutPadLayouts[HutIndex]) DrawHudFill(Tile(pad, 0f), Color.magenta);
                DrawHudFill(Tile(HomeExitMapCell(), 0f), Color.yellow);
            }
            DrawHudFill(Tile(_playerCell, scale * 0.19f), Color.red);
            GUI.Label(new Rect(125, 352, 400, 24), T("Red: Koh  Yellow: exit  Cyan: bath  Blue: bed  Purple: hut", "Vermelho: Koh  Amarelo: saída  Ciano: banho  Azul: cama  Roxo: alojamento"));
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
            if (index >= 0) return HouseFootprints[index].Contains(tilePosition);
            int hut = HutIndex;
            if (hut <= 0) return hut < 0 || HutFootprint.Contains(tilePosition);
            var last = HutPadLayouts[hut][^1];
            int right = hut <= 2 ? 26 : hut == 3 ? 29 : 32;
            return Rect.MinMaxRect(last.x - 2f, 8f, right + 3f, last.y + 3f).Contains(tilePosition);
        }

        private void CacheHomeFloorPlan()
        {
            _homeMapCells.Clear();
            if (HouseIndex < 0 && !InHomeHut) return;
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
