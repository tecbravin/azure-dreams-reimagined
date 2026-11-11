using System;
using System.Collections.Generic;
using UnityEngine;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string ActiveEggSpeciesKey = "home.active_hatched_species";
        private const string StoredEggPrefix = "egg:";
        private const string HatchedPrefix = "hatched:";
        private const string EmptyHutSlot = "empty";
        [Serializable] private sealed class EggSpecies
        {
            public int id, hp, attack, defense, mp;
            public string name;
            public bool forSale;
        }
        [Serializable] private sealed class EggWeight { public int species, weight; }
        [Serializable] private sealed class EggFloor { public int floor; public EggWeight[] choices; }
        [Serializable] private sealed class EggCatalog { public EggSpecies[] species; public EggFloor[] floors; }
        private EggCatalog _eggCatalog;
        private EggFamiliarPrefabs _eggFamiliarPrefabs;

        private GameObject HatchedFamiliarPrefab(int species)
        {
            var prefab = RegisteredMonsterPrefab(species);
            return prefab ? prefab : MonsterPrefab((MonsterSpecies)species);
        }

        private GameObject RegisteredMonsterPrefab(int species)
        {
            if (!_eggFamiliarPrefabs) _eggFamiliarPrefabs = Resources.Load<EggFamiliarPrefabs>("Town/Retail/EggFamiliarPrefabs");
            return _eggFamiliarPrefabs ? _eggFamiliarPrefabs.Find(species) : null;
        }
        private readonly Dictionary<int, ActorDefinition> _hatchedDefinitions = new();
        private int _hatchedSpecies;
        private ActorRecord _parkedFamiliarActor;
        private ActorDefinition _parkedFamiliarDefinition;
        private int _parkedFamiliarSpecies;
        private int _warmingEggIndex = -1;
        private ActorDefinition PersistentFamiliar => _parkedFamiliarActor != null ? _parkedFamiliarDefinition : _familiarDefinition;
        private int PersistentEggSpecies => _parkedFamiliarActor != null ? _parkedFamiliarSpecies : _hatchedSpecies;

        private EggCatalog GetEggCatalog()
        {
            if (_eggCatalog == null)
            {
                var asset = Resources.Load<TextAsset>("Town/Retail/egg_catalog");
                if (asset) _eggCatalog = JsonUtility.FromJson<EggCatalog>(asset.text);
            }
            return _eggCatalog;
        }

        private EggSpecies FindEggSpecies(int id)
        {
            var entries = GetEggCatalog()?.species;
            return entries == null ? null : Array.Find(entries, e => e.id == id);
        }

        private string DescribeEgg(DungeonItemEntry egg)
        {
            bool identified = _area == Area.Town || MonsterEgg.IdentifiedInTower(egg);
            string color = MonsterEgg.ColorOf(egg);
            string translatedColor = color switch
            {
                "White" => "branco", "Red" => "vermelho", "Blue" => "azul", "Green" => "verde",
                "Yellow" => "amarelo", "Purple" => "roxo", "Orange" => "laranja", _ => "rosa"
            };
            string name = identified ? FindEggSpecies(egg.Subtype)?.name ?? T("Unknown species", "Espécie desconhecida") : color;
            return T($"{name} Egg", identified ? $"Ovo de {name}" : $"Ovo {translatedColor}") + $" ({MonsterEgg.HeatOf(egg)}%)";
        }

        private GameObject CreateEggView(Vector2Int cell, DungeonItemEntry egg, float height)
        {
            var instance = CreateTownAsset("MonsterEgg", DescribeEgg(egg),
                new Vector3(cell.x, height, cell.y), Quaternion.identity);
            if (!instance) return null;
            instance.GetComponent<MonsterEgg>().Bind(_area == Area.Town ? MonsterEgg.Identify(egg) : egg);
            var sprite = instance.GetComponent<SpriteRenderer>().sprite;
            if (sprite) instance.transform.localScale = Vector3.one *
                (0.35f * WorldItemScale / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
            return instance;
        }

        private void PlaceGeneratedEgg()
        {
            if (!TowerFloorRules.AllowsRandomMonsters(_floor)) return;
            var floors = GetEggCatalog()?.floors;
            var choices = floors == null ? null : Array.Find(floors, e => e.floor == _floor)?.choices;
            if (choices == null || choices.Length == 0) return;
            int total = 0;
            foreach (var choice in choices)
                if (choice.species != (int)MonsterSpecies.FROG) total += Mathf.Max(0, choice.weight);
            if (total <= 0) return;
            var candidates = new List<Vector2Int>();
            foreach (var cell in _towerFloorLayout.OpenCells)
            {
                if (cell == _playerCell || cell == _stairCell || (_familiar && cell == _familiarCell) ||
                    _npcCells.Contains(cell) || CreatureAt(cell) >= 0 || _towerItemTraps.GetItemAt(cell) != null ||
                    _towerFloorLayout.Dungeon.Grid[cell.y, cell.x] != DungeonGenerator.TileType.Floor) continue;
                candidates.Add(cell);
            }
            if (candidates.Count == 0) return;
            int roll = RollSpawnRange(total);
            foreach (var choice in choices)
            {
                if (choice.species == (int)MonsterSpecies.FROG) continue;
                roll -= Mathf.Max(0, choice.weight);
                if (roll >= 0) continue;
                _towerItemTraps.TryPlaceItem(candidates[RollSpawnRange(candidates.Count)],
                    MonsterEgg.Create((byte)choice.species, identified: false));
                return;
            }
        }

        private ActorDefinition EggFamiliarDefinition(int species)
        {
            if (_hatchedDefinitions.TryGetValue(species, out var definition)) return definition;
            var data = FindEggSpecies(species);
            if (data == null) return null;
            definition = ScriptableObject.CreateInstance<ActorDefinition>();
            definition.Configure(data.name, ActorKind.Familiar, data.hp, data.attack, 0.16f, Color.white, null);
            _hatchedDefinitions.Add(species, definition);
            return definition;
        }

        private bool EquipHatchedFamiliar(int species, int level)
        {
            var definition = EggFamiliarDefinition(species);
            if (!definition) return false;
            var data = FindEggSpecies(species);
            _hatchedSpecies = species;
            _familiarDefinition = definition;
            _familiarActor = new ActorRecord { Species = (byte)species };
            if (!TryGetLevelStats(species, level, out var stats))
                stats = new LevelStats { level = level, hp = data.hp, mp = data.mp, atk = data.attack, def = data.defense };
            ApplyFreshStats(_familiarActor, stats);
            _familiarHealth = FamiliarMaxHealth;
            _familiarWaiting = _familiarDefeated = false;
            return true;
        }

        private void RestorePersistentFamiliar()
        {
            if (_parkedFamiliarActor == null) return;
            _familiarActor = _parkedFamiliarActor;
            _familiarDefinition = _parkedFamiliarDefinition;
            _hatchedSpecies = _parkedFamiliarSpecies;
            _parkedFamiliarActor = null;
            _parkedFamiliarDefinition = null;
            _parkedFamiliarSpecies = 0;
        }

        private void WarmSelectedEgg()
        {
            if (_area != Area.Tower) return;
            if (_carriedView)
            {
                _message = T("Put down what you are holding before warming an egg.", "Coloque no chão o que está segurando antes de aquecer um ovo.");
                return;
            }
            int index = SelectedRawIndex;
            ScheduleInventoryAction(() => _warmingEggIndex = index);
        }

        private void CompleteEggWarming()
        {
            int index = _warmingEggIndex;
            _warmingEggIndex = -1;
            if (index < 0 || _runInventory == null) return;
            var items = _runInventory.DungeonItems;
            if (index >= items.Count || items[index].Kind != MonsterEgg.Kind) return;
            var egg = MonsterEgg.Warm(items[index], 2 + RollSpawnRange(7));
            _runInventory.ReplaceDungeonItem(index, MonsterEgg.Kind, egg);
            HatchWarmedEgg(egg, () => _runInventory.ConsumeDungeonItem(index, MonsterEgg.Kind));
        }

        private void HatchWarmedEgg(DungeonItemEntry egg, Action consumeEgg)
        {
            int heat = MonsterEgg.HeatOf(egg);
            _message = DescribeEgg(egg);
            if (heat < 100) return;
            if (FindEggSpecies(egg.Subtype) == null)
            { _message = T("This egg's species cannot hatch here.", "O ovo desta espécie não pode chocar aqui."); return; }
            if (!TryFindHatchCell(out var cell))
            { _message = T("The egg is ready. Warm it again with an empty tile beside Koh.", "O ovo está pronto. Aqueça-o de novo com um espaço livre ao lado de Koh."); return; }
            if (!CanRemoveFamiliarEquipment()) return;

            if (_parkedFamiliarActor == null)
            {
                _parkedFamiliarActor = _familiarActor;
                _parkedFamiliarDefinition = _familiarDefinition;
                _parkedFamiliarSpecies = _hatchedSpecies;
                ExposeParkedFamiliar();
            }
            EquipHatchedFamiliar(egg.Subtype, _floor);
            _runInventory?.ClearEquipped(FamiliarEquipped);
            _towerHatchedNickname = "";
            _familiarInBag = _familiarDefeated = false;
            consumeEgg();
            if (_familiar) { _familiar.SetActive(false); RemoveGenerated(_familiar); }
            _familiarCell = cell;
            _familiar = SpawnFamiliar();
            _familiarLocomotion = ConfigureLocomotion(_familiar);
            _familiarAnimation = _familiar.GetComponentInChildren<AzureDreamsReimagined.Rendering.PsxSpriteRenderer>();
            Animate(_familiarAnimation, false);
            _message = T($"{_familiarDefinition.DisplayName} hatched at level {_floor}! It will stay until you leave the tower.", $"{_familiarDefinition.DisplayName} nasceu no nível {_floor}! Vai acompanhar você até a saída da torre.");
        }

        private bool TryFindHatchCell(out Vector2Int cell)
        {
            foreach (var delta in new[] { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down })
            {
                var candidate = _playerCell + delta;
                if (!CanTowerStep(_playerCell, delta) || candidate == _stairCell || CreatureAt(candidate) >= 0 ||
                    (_familiar && candidate == _familiarCell) ||
                    _thrownEggs.Exists(ally => ally.View && ally.Cell == candidate)) continue;
                cell = candidate;
                return true;
            }
            cell = default;
            return false;
        }

        private static string EncodeHutEgg(DungeonItemEntry egg)
            => $"{StoredEggPrefix}{egg.Subtype}:{egg.Param}:{egg.Flags}";

        private static bool TryReadHutEgg(string slot, out DungeonItemEntry egg)
        {
            egg = null;
            if (!slot.StartsWith(StoredEggPrefix, StringComparison.Ordinal)) return false;
            var data = slot.Split(':');
            if (data.Length != 4 || !byte.TryParse(data[1], out var id) ||
                !sbyte.TryParse(data[2], out var heat) || !byte.TryParse(data[3], out var flags)) return false;
            egg = new DungeonItemEntry(id, MonsterEgg.Kind, heat, flags);
            return true;
        }

        private static string FamiliarSlotId(string slot)
        {
            int separator = slot.IndexOf('@');
            return separator < 0 ? slot : slot.Substring(0, separator);
        }

        private static int FamiliarSlotExperience(string slot)
        {
            var fields = slot.Split('@');
            return fields.Length >= 2 && int.TryParse(fields[1], out int experience)
                ? Mathf.Max(0, experience) : 0;
        }

        private static int ReadHatchedSlot(string slot)
        {
            slot = FamiliarSlotId(slot);
            return slot.StartsWith(HatchedPrefix, StringComparison.Ordinal) &&
                   int.TryParse(slot.Substring(HatchedPrefix.Length), out int id) ? id : 0;
        }
    }
}
