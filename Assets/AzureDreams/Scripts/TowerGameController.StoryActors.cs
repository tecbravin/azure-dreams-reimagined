using UnityEngine;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        // Use the ordinary NPC interaction, portrait and collision path for story actors.
        private void SpawnStoryResident(string prefabId, string id, string name, Vector2Int cell, Vector3? placedPosition = null)
        {
            var prefab = FindStoryResidentPrefab(prefabId);
            if (!prefab) return;
            if (_interior)
            {
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
            if (id == "wookie" && instance.GetComponentInChildren<SpriteRenderer>() is { } sprite)
            {
                // Match Koh's standing height while keeping the sprite anchored at the feet.
                const float standingScale = 1.6125f;
                float framingScale = _interior ? InteriorCameraDistance / _townCameraDistance : 1f;
                sprite.transform.localScale *= standingScale * framingScale;
            }
            npc.ConfigureConversation(_townFlow.State);
            if (stats)
            {
                stats.displayName = name;
                if (prefabId != id) stats.portraitTexture = null;
            }
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

        private GameObject FindStoryResidentPrefab(string id) =>
            System.Array.Find(_storyResidents ?? System.Array.Empty<GameObject>(), candidate =>
                candidate && candidate.TryGetComponent<NPCController>(out var controller) && controller.npcId == id);

        private bool StoryResidentCellOpen(Vector2Int cell) => cell != _playerCell &&
            !_npcCells.Contains(cell) && TownPositionOpen(CellPosition(cell));

        private void SpawnOutdoorStoryResidents()
        {
            if (!UsingRetailTown) return;
            if (StoryStage("nico") is 1 or 2)
                SpawnStoryResident("nico", "nico", "Nico", new Vector2Int(58, 72));
            if (StoryStage("nico") == 3)
                SpawnStoryResident("monster_shop_hunter", "chero", "Chero", new Vector2Int(58, 72));
            // The fountain rim is one unit above the town height grid.
            Vector3? fountainPerch = _builtTownLots.Exists(project => project.Id == "fountain")
                ? new Vector3(70.5f, 1f, 78.5f) : null;
            SpawnStoryResident("monster_shop_hunter", "wookie", "Wookie",
                fountainPerch.HasValue ? new Vector2Int(70, 78) : new Vector2Int(70, 79), fountainPerch);
            if (StoryStage("mia") == 4)
                SpawnStoryResident("mia", "mia", "Mia", new Vector2Int(96, 40));
        }

        private bool ShouldSpawnStoryResident(NPCController npc, string room)
        {
            if (npc.npcId != "vivian") return true;
            int stage = StoryStage("vivian");
            return room switch
            {
                BarRoom => stage is 3 or 7 || (stage == 4 && !StoryBuilding("casino")),
                TheaterRoom => stage >= 5,
                _ => true
            };
        }

        private void SpawnInteriorStoryResidents(string room)
        {
            switch (room)
            {
                case CasinoRoom when StoryStage("vivian") == 4:
                    SpawnStoryResident("vivian", "vivian", "Vivian", default);
                    break;
                case TheaterRoom:
                    SpawnStoryResident("dar", "theater_fon", "Fon", default);
                    SpawnStoryResident("monster_shop_hunter", "theater_audience", T("Spectator", "Espectador"), default);
                    SetRoomDialogue("theater_audience", T("The dancing is great, but I can't wait to hear the stand-up comedy.",
                        "A dança é ótima, mas mal posso esperar pelo número de comédia."));
                    break;
                case PoolClosedRoom:
                    SpawnStoryResident("monster_shop_hunter", "wookie", "Wotta", default);
                    break;
                case PoolOpenRoom:
                    SpawnStoryResident("monster_shop_hunter", "wookie", "Wotta", default);
                    SpawnStoryResident("nico", "pool_nico", "Nico", default);
                    SpawnStoryResident("fur", "pool_fur", "Fur", default);
                    SpawnStoryResident("dar", "pool_visitor", T("Swimmer", "Banhista"), default);
                    if (StoryStage("mia") == 3) SpawnStoryResident("mia", "mia", "Mia", default);
                    break;
                case "Interior_10_hospital_2" when StoryStage("cherrl") >= 5:
                    SpawnStoryResident("cherrl", "cherrl", "Cherrl", default);
                    break;
            }
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
                new(true, "So you came after all, {name}. Ghosh has been spreading some ridiculous rumors about you.",
                    "Então você veio mesmo, {name}. O Ghosh anda espalhando uns boatos ridículos sobre você.")
            },
            new VisitorLine[]
            {
                new(true, "{name}, watch out! That Pulunpa has been chasing me!", "{name}, cuidado! Aquele Pulunpa está me perseguindo!")
            },
            null,
            new VisitorLine[]
            {
                new(true, "{name}, get out of the way! There's a U-Boat right behind you!", "{name}, saia da frente! Tem um U-Boat logo atrás de você!"),
                new(true, "There, it's gone. Why did I run toward it without even thinking?", "Pronto, ele se foi. Por que corri na direção dele sem nem pensar?")
            }
        };

        private int _selfiVisitorStory;
        private ActorRecord _selfiEncounterMonster;

        private int PendingSelfiEncounter()
        {
            int stage = StoryStage("selfi");
            return StoryNewDay("selfi") && stage is 1 or 2 or 4 ? stage : 0;
        }

        private void SpawnSelfiEncounterMonster(MonsterSpecies species)
        {
            foreach (var step in VisitorSteps)
            {
                Vector2Int cell = FloorTwoVisitorCell + step;
                if (!CanTowerStep(FloorTwoVisitorCell, step) || BellCellOccupied(cell)) continue;
                SpawnManagedCreature(cell, species, levelOverride: 2, allowSpawnSleep: false);
                int index = CreatureAt(cell);
                if (index >= 0) _selfiEncounterMonster = _creatures[index].Actor;
                return;
            }
        }

        private void CompleteSelfiVisitor()
        {
            if (_selfiVisitorStory == 2)
                SpawnSelfiEncounterMonster(MonsterSpecies.PULUNPA);
            else if (_selfiVisitorStory == 1)
                SetSelfiTowerStage(2);
            else if (_selfiVisitorStory == 4)
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
                SetSelfiTowerStage(5);
            }
        }

        private void ResolveSelfiRescue()
        {
            if (!IsTowerEventFloor || StoryStage("selfi") != 2 || _selfiEncounterMonster == null ||
                _carriedMonster.Actor == _selfiEncounterMonster ||
                _creatures.Exists(creature => creature.Actor == _selfiEncounterMonster)) return;
            _selfiEncounterMonster = null;
            SetSelfiTowerStage(3);
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
