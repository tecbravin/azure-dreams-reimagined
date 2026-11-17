using System;
using System.Collections;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string NicoWakeUpSeen = StoryPrefix + "nico.wake_up_seen";
        private const string NicoConfessionWakeUpSeen = StoryPrefix + "nico.confession_wake_up_seen";
        private static readonly TownRetailSelectionTable WakeUpSelection = TownRetailSelectionTable.CreateForScene3E5000();
        private readonly struct WakeUpVisitorDefinition
        {
            internal readonly string Id, English, Portuguese, PortraitPath;

            internal WakeUpVisitorDefinition(string id, string english, string portuguese, string portraitPath)
            {
                Id = id;
                English = english;
                Portuguese = portuguese;
                PortraitPath = portraitPath;
            }
        }

        // Entries 1-7 follow the order decoded from the retail home module.
        // WakeUpSelection retains its original priority/paired/cycling rules.
        private static readonly WakeUpVisitorDefinition[] WakeUpVisitors =
        {
            default,
            new WakeUpVisitorDefinition("nico", null, null, null),
            new WakeUpVisitorDefinition("selfi", null, null, null),
            new WakeUpVisitorDefinition("fur", null, null, null),
            new WakeUpVisitorDefinition("patty", null, null, null),
            new WakeUpVisitorDefinition("vivian", null, null, null),
            new WakeUpVisitorDefinition("mia", null, null, null),
            new WakeUpVisitorDefinition("cherrl", null, null, null)
        };

        private static readonly WakeUpVisitorDefinition WreathWakeUpVisitor = new("wreath", null, null, WreathPortrait);

        // Mattress centers from the three captured bedroom meshes.
        private static readonly Vector2Int[] WakeUpBeds = { new(10, 12), new(10, 12), new(10, 14) };
        private const float MattressHeight = 0.5f;

        private bool _wakingInBed;
        private NPCController _wakeUpVisitor;
        private bool _wakeUpVisitorIsResident;
        private Vector3 _wakeUpResidentPosition;
        private Quaternion _wakeUpResidentRotation;
        private string _wakeUpReturnMessage;
        private Vector3 _wakeUpApproachStart;
        private bool _wakeUpVisitorLeaving;

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
            var bed = HomeLayoutCell(HouseIndex, WakeUpBeds[HouseIndex]);
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
            if (_kohSprite)
            {
                _kohSprite.SetFacing(_townFacing);
                // The retail sleep sheet ends on the prone pose.  Start the wake-up
                // scene there instead of replaying the fall while Koh is already in bed.
                _kohSprite.PresentSleep(true, alreadyLying: true);
            }
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
                        if (candidate == _playerCell || !IsInHomeBedroom(HouseIndex, candidate) ||
                            TownTerrainHeight(position) >= maxGroundHeight || !TownPositionOpen(position)) continue;
                        return candidate;
                    }
            return new Vector2Int(-1, -1);
        }

        private bool TryStartWakeUpVisit(Vector2Int cell, bool survived, string returnMessage)
        {
            _wakeUpVisitorLeaving = false;
            var reputation = TownReputation.Read();
            bool nicoThanks = StoryStage("nico") == NicoSecondDonation && !TownSaveVariables.GetBool(NicoWakeUpSeen) &&
                !reputation.IsFlagSet(TownReputation.NicoWakeUpFlag);
            bool nicoConfession = StoryStage("nico") == NicoConfessionWake && StoryNewDay("nico") &&
                !TownSaveVariables.GetBool(NicoConfessionWakeUpSeen) && !reputation.IsFlagSet(NicoCheroMorningFlag);
            byte entry = 0;
            var visitor = WreathWakeUpVisitor;
            string english = visitor.English;
            string portuguese = visitor.Portuguese;
            string portraitPath = visitor.PortraitPath;
            if (nicoThanks || nicoConfession) visitor = WakeUpVisitors[1];
            else
            {
                var selection = new TownRetailStorySelectionState
                {
                    EntryIndex = (byte)TownSaveVariables.GetInt(TownReputation.WakeCursorKey, byte.MaxValue)
                };
                entry = TownRetailStoryEventSelector.SelectCompletedEntry(reputation, selection, WakeUpSelection);
                if (entry != 0) TownSaveVariables.SetInt(TownReputation.WakeCursorKey, selection.EntryIndex);
                if (entry != 0 && (survived || WakeUpVisitors[entry].Id != "vivian") &&
                    (WakeUpVisitors[entry].Id != "cherrl" || reputation.IsFlagSet(CherrlNurseFlag)))
                {
                    visitor = WakeUpVisitors[entry];
                    english = visitor.English;
                    portuguese = visitor.Portuguese;
                    portraitPath = visitor.PortraitPath;
                }
                else entry = 0;
            }

            var npc = _townPeople.Find(candidate => candidate && candidate.npcId == visitor.Id);
            _wakeUpVisitorIsResident = npc;
            if (!npc)
            {
                var prefab = FindStoryResidentPrefab(visitor.Id);
                if (!prefab && _interior.residentPrefabs != null)
                    prefab = Array.Find(_interior.residentPrefabs, candidate =>
                        candidate && candidate.TryGetComponent<NPCController>(out var resident) && resident.npcId == visitor.Id);
                if (!prefab) return false;
                npc = Spawn(prefab, cell, "Wake-up visitor").GetComponent<NPCController>();
            }
            _wakeUpVisitor = npc;
            _wakeUpResidentPosition = npc.transform.position;
            _wakeUpResidentRotation = npc.transform.rotation;
            _wakeUpReturnMessage = returnMessage;
            Vector3 destination = RetailTownActorPosition(CellPosition(cell), 0f);
            _townFacing = new Vector2(cell.x - _playerCell.x, cell.y - _playerCell.y);
            if (_kohSprite) _kohSprite.SetFacing(_townFacing);
            Vector3 approach = new(-_townFacing.x, 0f, -_townFacing.y);
            _wakeUpApproachStart = destination + approach * 0.72f;
            npc.transform.position = _wakeUpApproachStart;
            npc.transform.rotation = Quaternion.LookRotation(-approach);

            if (nicoThanks)
            {
                TownSaveVariables.SetBool(NicoWakeUpSeen, true);
                reputation.ApplyOneTimeFlagOperation(TownReputation.NicoWakeUpFlag);
            }
            else if (nicoConfession)
            {
                TownSaveVariables.SetBool(NicoConfessionWakeUpSeen, true);
                reputation.ApplyOneTimeFlagOperation(NicoCheroMorningFlag);
                AdvanceTownStory("nico", NicoHouseGuard);
            }
            TownSaveVariables.SetInt(TownReputation.WakeEntryKey, entry);
            TownSaveVariables.Save();
            StartCoroutine(PlayWakeUpVisitorArrival(npc, destination, english, portuguese, portraitPath, survived,
                nicoThanks, nicoConfession));
            return true;
        }

        private IEnumerator PlayWakeUpVisitorArrival(NPCController npc, Vector3 destination, string english,
            string portuguese, string portraitPath, bool survived, bool nicoThanks, bool nicoConfession)
        {
            const float duration = 0.32f;
            if (!npc) yield break;
            Vector3 origin = npc.transform.position;
            Vector3 direction = destination - origin;
            var locomotion = npc.GetComponent<ActorLocomotionView>();
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                if (!npc) yield break;
                npc.transform.position = Vector3.Lerp(origin, destination, elapsed / duration);
                locomotion?.Present(direction, elapsed * 7f, true);
                yield return null;
            }
            if (!npc) yield break;
            npc.transform.position = destination;
            locomotion?.Present(direction, 0f, false);
            if (direction.sqrMagnitude > 0.0001f) npc.transform.rotation = Quaternion.LookRotation(-direction.normalized);
            if (npc.npcId == "nico")
                NicoWakeUpStory(npc, survived, nicoThanks, nicoConfession);
            else if (npc.npcId == "selfi")
                SelfiWakeUpStory(npc, survived);
            else if (npc.npcId == "patty")
                PattyWakeUpStory(npc);
            else if (npc.npcId == "mia")
                MiaWakeUpStory(npc, survived);
            else if (npc.npcId == "vivian")
                VivianWakeUpStory(npc);
            else if (npc.npcId == "fur")
                FurWakeUpStory(npc, survived);
            else if (npc.npcId == "cherrl")
                CherrlWakeUpStory(npc);
            else if (npc.npcId == "wreath")
                WreathWakeUpStory(npc, survived);
            else StoryTalk(npc, english, portuguese, portraitPath);
        }

        private void FinishWakeUpVisit()
        {
            if (!_wakeUpVisitor || _wakeUpVisitorLeaving) return;
            StartCoroutine(PlayWakeUpVisitorDeparture(_wakeUpVisitor));
        }

        private IEnumerator PlayWakeUpVisitorDeparture(NPCController npc)
        {
            _wakeUpVisitorLeaving = true;
            const float duration = 0.28f;
            Vector3 origin = npc ? npc.transform.position : Vector3.zero;
            Vector3 direction = _wakeUpApproachStart - origin;
            var locomotion = npc ? npc.GetComponent<ActorLocomotionView>() : null;
            for (float elapsed = 0f; npc && elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                npc.transform.position = Vector3.Lerp(origin, _wakeUpApproachStart, elapsed / duration);
                locomotion?.Present(direction, elapsed * 7f, true);
                yield return null;
            }
            if (!_wakeUpVisitor)
            {
                _wakeUpVisitorLeaving = false;
                yield break;
            }
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
            _wakeUpVisitorLeaving = false;
        }
    }
}
