using System;
using System.Collections;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string NicoWakeUpSeen = StoryPrefix + "nico.wake_up_seen";
        private static readonly TownRetailSelectionTable WakeUpSelection = TownRetailSelectionTable.CreateForScene3E5000();
        // Entries 1-7 of the home module, including Cherrl's nurse visit at entry 7.
        private static readonly (string Id, string English, string Portuguese)[] WakeUpVisitors =
        {
            default,
            ("nico", "Good morning, {name}. Come see the fountain with me when you're ready.",
                "Bom dia, {name}. Quando estiver pronto, venha ver a fonte comigo."),
            ("selfi", "You're awake! Next time, tell me before you go climbing the Tower again.",
                "Você acordou! Da próxima vez, me avise antes de subir a Torre de novo."),
            ("fur", "Did you sleep well? Stop by the shop later. I'd like to hear about your trip.",
                "Dormiu bem? Passe na loja mais tarde. Quero saber como foi a sua aventura."),
            ("patty", "Good morning! Come by the restaurant when you get hungry.",
                "Bom dia! Passe no restaurante quando bater a fome."),
            ("vivian", "You're back just in time. Come see me dance when you've rested.",
                "Você voltou bem a tempo. Venha me ver dançar depois que descansar."),
            ("mia", "Oh, you're awake. Come find me at the library when you feel up to it.",
                "Ah, você acordou. Quando estiver se sentindo melhor, me procure na biblioteca."),
            ("cherrl", "I was worried about you. Chappy and I are glad you're home.",
                "Eu estava preocupada com você. O Chappy e eu estamos felizes que você voltou.")
        };

        // Mattress centers from the three captured bedroom meshes.
        private static readonly Vector2Int[] WakeUpBeds = { new(10, 12), new(10, 12), new(10, 14) };
        private const float MattressHeight = 0.5f;

        private bool _wakingInBed;
        private NPCController _wakeUpVisitor;
        private bool _wakeUpVisitorIsResident;
        private Vector3 _wakeUpResidentPosition;
        private Quaternion _wakeUpResidentRotation;
        private string _wakeUpReturnMessage;

        private Vector3 HomeReturnPosition()
        {
            var mansion = BuiltKohHome();
            if (mansion != null) return ConstructionReturnPosition(mansion);
            foreach (var (door, interior) in _facadeInteriors)
            {
                if (!door || (interior != "KohHome" && Array.IndexOf(HomeScenes, interior) < 0)) continue;
                var outside = door.Threshold + Vector3.forward;
                return RetailTownActorPosition(outside, TownActorFootOffset);
            }
            if (UsingRetailTown) return _player.transform.position;
            return CellPosition(HomeDoorCell + Vector2Int.up);
        }

        private IEnumerator PlayWakeUp(bool survived, string returnMessage)
        {
            var bed = WakeUpBeds[HouseIndex];
            var exit = FindWakeUpCell(bed, MattressHeight);
            if (exit.x < 0)
            {
                _message = returnMessage;
                yield break;
            }

            Vector3 bedPosition = CellPosition(bed);
            bedPosition.y = MattressHeight + TownActorFootOffset;
            var standingPosition = RetailTownActorPosition(CellPosition(exit), TownActorFootOffset);

            _message = string.Empty;
            _player.transform.position = bedPosition;
            _playerCell = bed;
            _wakingInBed = true;
            _townFacing = new Vector2(exit.x - bed.x, exit.y - bed.y);
            if (_kohSprite) _kohSprite.SetFacing(_townFacing);
            UpdateCameraFollow(snap: true);
            yield return new WaitForSecondsRealtime(0.85f);

            _wakingInBed = false;
            const float duration = 0.35f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float progress = elapsed / duration;
                _player.transform.position = Vector3.Lerp(bedPosition, standingPosition, progress) +
                    Vector3.up * (Mathf.Sin(progress * Mathf.PI) * 0.18f);
                yield return null;
            }
            _player.transform.position = standingPosition;
            _playerCell = exit;

            var visitorCell = FindWakeUpCell(exit, MattressHeight);
            if (visitorCell.x >= 0 && TryStartWakeUpVisit(visitorCell, survived, returnMessage)) yield break;
            _message = returnMessage;
        }

        private Vector2Int FindWakeUpCell(Vector2Int near, float maxGroundHeight)
        {
            for (int distance = 1; distance <= 4; distance++)
                for (int z = -distance; z <= distance; z++)
                    for (int x = -distance; x <= distance; x++)
                    {
                        if (Mathf.Abs(x) + Mathf.Abs(z) != distance) continue;
                        var candidate = near + new Vector2Int(x, z);
                        var position = CellPosition(candidate);
                        if (candidate == _playerCell || candidate.x >= BedroomDoors[HouseIndex].x ||
                            TownTerrainHeight(position) >= maxGroundHeight || !TownPositionOpen(position)) continue;
                        return candidate;
                    }
            return new Vector2Int(-1, -1);
        }

        private bool TryStartWakeUpVisit(Vector2Int cell, bool survived, string returnMessage)
        {
            var reputation = TownReputation.Read();
            bool nicoThanks = StoryStage("nico") == 2 && !TownSaveVariables.GetBool(NicoWakeUpSeen) &&
                !reputation.IsFlagSet(TownReputation.NicoWakeUpFlag);
            byte entry = 0;
            string id = nicoThanks ? "nico" : "wreath";
            string english = survived ? "Good morning, {name}. I'm glad you're home." :
                "{name}, you're awake. You gave me quite a scare. Take it easy today.";
            string portuguese = survived ? "Bom dia, {name}. Que bom que você voltou." :
                "{name}, você acordou. Que susto você me deu! Vá com calma hoje.";
            if (nicoThanks)
            {
                english = "Good morning, {name}! I came to thank you for donating to the fountain.";
                portuguese = "Bom dia, {name}! Vim agradecer pela sua doação para a fonte.";
            }
            else
            {
                var selection = new TownRetailStorySelectionState
                {
                    EntryIndex = (byte)TownSaveVariables.GetInt(TownReputation.WakeCursorKey, byte.MaxValue)
                };
                entry = TownRetailStoryEventSelector.SelectCompletedEntry(reputation, selection, WakeUpSelection);
                if (entry != 0)
                {
                    (id, english, portuguese) = WakeUpVisitors[entry];
                    TownSaveVariables.SetInt(TownReputation.WakeCursorKey, selection.EntryIndex);
                }
            }

            var npc = _townPeople.Find(candidate => candidate && candidate.npcId == id);
            _wakeUpVisitorIsResident = npc;
            if (!npc)
            {
                var prefab = FindStoryResidentPrefab(id);
                if (!prefab && _interior.residentPrefabs != null)
                    prefab = Array.Find(_interior.residentPrefabs, candidate =>
                        candidate && candidate.TryGetComponent<NPCController>(out var resident) && resident.npcId == id);
                if (!prefab) return false;
                npc = Spawn(prefab, cell, "Wake-up visitor").GetComponent<NPCController>();
            }
            _wakeUpVisitor = npc;
            _wakeUpResidentPosition = npc.transform.position;
            _wakeUpResidentRotation = npc.transform.rotation;
            _wakeUpReturnMessage = returnMessage;
            npc.transform.position = RetailTownActorPosition(CellPosition(cell), 0f);
            _townFacing = new Vector2(cell.x - _playerCell.x, cell.y - _playerCell.y);
            if (_kohSprite) _kohSprite.SetFacing(_townFacing);
            npc.transform.rotation = Quaternion.LookRotation(new Vector3(-_townFacing.x, 0f, -_townFacing.y));

            if (nicoThanks)
            {
                TownSaveVariables.SetBool(NicoWakeUpSeen, true);
                reputation.ApplyOneTimeFlagOperation(TownReputation.NicoWakeUpFlag);
            }
            TownSaveVariables.SetInt(TownReputation.WakeEntryKey, entry);
            TownSaveVariables.Save();
            return StoryTalk(npc, english, portuguese);
        }

        private void FinishWakeUpVisit()
        {
            if (!_wakeUpVisitor) return;
            if (_wakeUpVisitorIsResident)
                _wakeUpVisitor.transform.SetPositionAndRotation(_wakeUpResidentPosition, _wakeUpResidentRotation);
            else
            {
                _wakeUpVisitor.gameObject.SetActive(false);
                RemoveGenerated(_wakeUpVisitor.gameObject);
            }
            _wakeUpVisitor = null;
            _message = _wakeUpReturnMessage ?? string.Empty;
            _wakeUpReturnMessage = null;
        }
    }
}
