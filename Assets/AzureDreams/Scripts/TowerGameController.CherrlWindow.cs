using System.Collections;
using UnityEngine;
using AzureDreamsReimagined.Rendering;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string CherrlToyObjectId = "cherrl_dropped_chappy";
        private const string CherrlToyThrownKey = StoryPrefix + "cherrl.toyThrown";
        private const string CherrlToyReturnedKey = StoryPrefix + "cherrl.toyReturned";
        private bool _cherrlWindowAnimating;
        private GameObject _cherrlDroppedToy;
        private Sprite _cherrlToySprite;

        private static bool CherrlHouseUnlocked => TownSaveVariables.GetBool(CherrlToyReturnedKey) ||
            TownSaveVariables.GetBool(CherrlWindowMeetingKey) || TownSaveVariables.GetBool(CherrlInvitedInsideKey) ||
            StoryStage("cherrl") > CherrlFirstMeeting;

        private void OpenCherrlHouseDoor()
        {
            foreach (var (door, interior) in _facadeInteriors)
                if (door && interior == CherrlHouse) door.OpenImmediately();
        }

        private void SetupCherrlFirstMeetingVisual(NPCController npc)
        {
            if (!npc || npc.npcId != "cherrl" || StoryStage("cherrl") != CherrlFirstMeeting) return;
            RetailResidentBanks.ApplyBank(npc.gameObject, StoryRoom == CherrlHouse ? "cheril_bed" :
                TownSaveVariables.GetBool(CherrlToyThrownKey) ? "cheril_window" : "cheril_bed");
            if (StoryRoom.Length != 0) return;
            // Present her at the window instead of on the pavement by the door.
            foreach (var (door, interior) in _facadeInteriors)
            {
                if (!door || interior != CherrlHouse) continue;
                // The captured window is on the same Z plane as the actor's
                // old anchor. Separate it and keep depth parallel to the wall
                // so camera rotation cannot make the billboard cross it.
                var window = door.Threshold + new Vector3(1.5f, 0f, .04f);
                window.y = TownTerrainHeight(door.Threshold + Vector3.forward) + 1.05f;
                npc.transform.position = window;
                npc.transform.rotation = Quaternion.identity;
                npc.GetComponent<AzureDreamsReimagined.Rendering.ActorSpriteDepth>()?.SetFacadeNormal(Vector3.forward);
                break;
            }
            if (TownSaveVariables.GetBool(CherrlToyThrownKey) && !TownSaveVariables.GetBool(CherrlToyReturnedKey))
                SpawnCherrlDroppedToy(npc.transform.position + Vector3.forward);
        }

        private bool BeginCherrlWindowMeeting(NPCController npc)
        {
            if (TownSaveVariables.GetBool(CherrlWindowMeetingKey) || TownSaveVariables.GetBool(CherrlInvitedInsideKey))
            {
                TownSaveVariables.SetBool(CherrlToyReturnedKey, true); // Existing saves already returned Chappy.
                return false;
            }
            if (!TownSaveVariables.GetBool(CherrlToyThrownKey))
                return CherrlTalk(npc, 3, "What..?\nAhhh!!!!!", "O quê...?\nAhhh!!!!!",
                    () => StartCoroutine(PlayCherrlToyDrop(npc)));
            if (TownSaveVariables.GetBool(CherrlToyReturnedKey)) return false;
            return CherrlTalk(npc, 4, "Oh my god...\nI was so surprised, I threw my precious Chappy.",
                "Meu Deus...\nLevei um susto tão grande que joguei o meu querido Chappy.");
        }

        private IEnumerator PlayCherrlToyDrop(NPCController npc)
        {
            _cherrlWindowAnimating = true;
            try
            {
                if (!npc) yield break;
                RetailResidentBanks.ApplyBank(npc.gameObject, "cheril_window");
                var view = npc.GetComponent<RetailTownResidentSpriteView>();
                view?.PlayClip("action_00");
                SpawnCherrlDroppedToy(npc.transform.position + Vector3.forward);
                if (_cherrlDroppedToy)
                {
                    Vector3 target = _cherrlDroppedToy.transform.position;
                    Vector3 from = npc.transform.position + Vector3.up * .65f;
                    for (float time = 0; time < .65f; time += Time.unscaledDeltaTime)
                    {
                        if (!npc || !_cherrlDroppedToy) yield break;
                        float progress = time / .65f;
                        _cherrlDroppedToy.transform.position = Vector3.Lerp(from, target, progress) +
                            Vector3.up * (Mathf.Sin(progress * Mathf.PI) * .6f);
                        yield return null;
                    }
                    _cherrlDroppedToy.transform.position = target;
                }
                TownSaveVariables.SetBool(CherrlToyThrownKey, true);
                TownSaveVariables.Save();
                view?.ResumeLocomotion();
                if (npc) CherrlTalk(npc, 4, "Oh my god...\nI was so surprised, I threw my precious Chappy.",
                    "Meu Deus...\nLevei um susto tão grande que joguei o meu querido Chappy.");
            }
            finally { _cherrlWindowAnimating = false; }
        }

        private void SpawnCherrlDroppedToy(Vector3 position)
        {
            if (_cherrlDroppedToy) return;
            var texture = AssetMode.Load<Texture2D>("Town/Residents/Retail/Chappy");
            if (!texture) { Debug.LogError("Missing original Chappy sprite."); return; }
            if (!_cherrlToySprite) _cherrlToySprite = Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, 0f), RetailTownResidentSpriteView.TownPixelsPerUnit);
            var toy = new GameObject("Cherrl's dropped Chappy");
            toy.transform.SetParent(transform, false);
            toy.transform.position = RetailTownActorPosition(position, .04f);
            toy.AddComponent<SpriteRenderer>().sprite = _cherrlToySprite;
            toy.AddComponent<CameraFacingSprite>();
            var pickup = toy.AddComponent<TownObject>();
            pickup.objectId = CherrlToyObjectId;
            pickup.persistInteraction = false;
            pickup.repeatable = false;
            pickup.interactionRadius = 1.2f;
            _townObjects.Add(pickup);
            _generated.Add(toy);
            _cherrlDroppedToy = toy;
        }

        private void ReturnCherrlToy(TownObject source)
        {
            var cherrl = FindStoryActor("cherrl");
            if (!cherrl) return;
            TownSaveVariables.SetBool(CherrlToyReturnedKey, true);
            TownSaveVariables.Save();
            OpenCherrlHouseDoor();
            if (source) { _townObjects.Remove(source); RemoveGenerated(source.gameObject); }
            _cherrlDroppedToy = null;
            RetailResidentBanks.ApplyBank(cherrl.gameObject, "cheril_bed");
            CherrlFirstMeetingAtWindow(cherrl);
        }
    }
}
