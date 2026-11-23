using System;
using System.Collections;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private bool _poolSceneAnimating, _poolEntrySceneChecked, _poolHecklersStarted;
        private const int VivianPoolEventFlag = 0x1200;
        private const int MiaPoolEventFlag = 0x1201;
        private const int PoolOpenFlag = 4604; // Fp_pool_open
        private const int VivianPoolPresentFlag = 4606; // Fp_bibian_pool
        private const int MiaPoolPresentFlag = 4607; // Fp_mia_pool
        private const int KohSwimsuitFlag = 4610; // F_player_mizugi

        private static string PoolSwimsuitBank(string id) => id switch
        {
            "pool_nico" => "pool_niko", "pool_selfi" => "pool_selfy",
            "pool_patty" => "pool_paty", "pool_fur" => "pool_far",
            "pool_cherrl" => "pool_cheril", "pool_ghosh" => "pool_gosh",
            "mia" => "pool_mia", "vivian" => "pool_vivian", _ => null
        };

        private bool WearingPoolSprite(NPCController npc)
        {
            if (!npc) return false;
            var bank = PoolSwimsuitBank(npc.npcId);
            var atlas = bank == null ? null : AssetMode.Load<Texture2D>(RetailResidentBanks.Folder + bank);
            return atlas && npc.GetComponent<RetailTownResidentSpriteView>() is { } view && view.UsesAtlas(atlas)
                || npc.TryGetComponent<PoolAttireView>(out _);
        }

        private void ConfigureRestoredPoolEventState()
        {
            _poolEntrySceneChecked = _poolHecklersStarted = false;
            var flags = TownRetailScriptFlags.CreatePersistent();
            if (TownSaveVariables.GetInt(VivianPoolDateKey) is 1 or 2) flags.SetBit(VivianPoolEventFlag);
            if (MiaStage() == MiaPoolDate) flags.SetBit(MiaPoolEventFlag);
        }

        private bool HandleRestoredPoolEvents()
        {
            if (!InRestoredPool || _storySpeaker || _poolSceneAnimating || _inventoryOpen || _settingsOpen) return false;
            if (!_poolEntrySceneChecked)
            {
                _poolEntrySceneChecked = true;
                if (MiaStage() == MiaPoolDate && FindStoryActor("mia") is { } mia) return MiaPoolConversation(mia);
                if (TownSaveVariables.GetInt(VivianPoolDateKey) == 1 && FindStoryActor("vivian") is { } vivian)
                    return VivianPoolStory(vivian);
            }
            if (!_poolHecklersStarted && !TownSaveVariables.GetBool(VivianPoolHecklersSeenKey) &&
                TownSaveVariables.GetInt(VivianPoolDateKey) == 2 && FindStoryActor("vivian") is { } date &&
                Vector3.Distance(date.transform.position, _player.transform.position) < 4f &&
                FindStoryActor("pool_heckler_big") is { } brother)
            {
                _poolHecklersStarted = true;
                StartCoroutine(PlayPoolHecklerApproach(brother, date));
                return true;
            }
            return false;
        }

        private void ChangePoolOutfit(NPCController npc, Action next)
        {
            if (_poolSceneAnimating || !npc) return;
            StartCoroutine(PlayPoolOutfitChange(npc, next));
        }

        private IEnumerator PoolDeckWalk(NPCController npc, Vector3 destination)
        {
            if (!npc) yield break;
            // The northern deck (Z=15) skirts the water rectangle X18..29/Z18..30.
            var lane = new Vector3(npc.transform.position.x, npc.transform.position.y, 15f);
            yield return PlayStoryWalk(npc, lane, Mathf.Max(.15f, Vector3.Distance(npc.transform.position, lane) / 3.5f));
            if (!npc) yield break;
            lane = new Vector3(destination.x, destination.y, 15f);
            yield return PlayStoryWalk(npc, lane, Mathf.Max(.15f, Vector3.Distance(npc.transform.position, lane) / 3.5f));
            if (npc) yield return PlayStoryWalk(npc, destination, Mathf.Max(.15f, Vector3.Distance(npc.transform.position, destination) / 3.5f));
        }

        private IEnumerator PlayPoolOutfitChange(NPCController npc, Action next)
        {
            _poolSceneAnimating = true;
            var home = npc.transform.position;
            var changing = new Vector3(PoolChangingRoomPosition.x, home.y, PoolChangingRoomPosition.z);
            try
            {
                yield return PoolDeckWalk(npc, changing);
                if (!npc || !InRestoredPool) yield break;
                npc.gameObject.SetActive(false);
                yield return new WaitForSecondsRealtime(.35f);
                if (!npc || !InRestoredPool) yield break;
                ApplyPoolPresentation(npc);
                npc.gameObject.SetActive(true);
                yield return PoolDeckWalk(npc, home);
                if (npc && _player)
                    npc.GetComponent<ActorLocomotionView>()?.Present(_player.transform.position - npc.transform.position, 0f, false);
            }
            finally
            {
                if (npc) npc.gameObject.SetActive(true);
                _poolSceneAnimating = false;
            }
            if (npc && InRestoredPool) next?.Invoke();
        }

        private void MiaPoolGhoshEntrance(NPCController mia)
        {
            if (!_poolSceneAnimating) StartCoroutine(PlayMiaPoolGhoshEntrance(mia));
        }

        private IEnumerator PlayMiaPoolGhoshEntrance(NPCController mia)
        {
            _poolSceneAnimating = true;
            try
            {
                var ghosh = FindStoryActor("pool_ghosh");
                if (ghosh && mia)
                {
                    var stop = mia.transform.position + Vector3.right * 1.5f;
                    yield return PoolDeckWalk(ghosh, stop);
                    if (ghosh) ghosh.GetComponent<ActorLocomotionView>()?.Present(mia.transform.position - ghosh.transform.position, 0f, false);
                }
            }
            finally { _poolSceneAnimating = false; }
            if (mia && InRestoredPool) MiaPoolGhoshConversation(mia);
        }

        private void FinishMiaPoolDate(NPCController mia)
        {
            var flags = TownRetailScriptFlags.CreatePersistent();
            flags.ClearBit(MiaPoolEventFlag);
            flags.SetBit(MiaPoolPresentFlag);
            AdvanceTownStory("mia", MiaLibraryFollowUpOne);
            if (mia && InRestoredPool) StartCoroutine(PlayPoolDeparture(mia));
        }

        private IEnumerator PlayPoolDeparture(NPCController npc)
        {
            _poolSceneAnimating = true;
            try
            {
                var exit = HomeEntry(_interior) + Vector3.forward * 2f;
                exit.y = npc.transform.position.y;
                yield return PoolDeckWalk(npc, exit);
                if (npc) npc.gameObject.SetActive(false);
            }
            finally { _poolSceneAnimating = false; }
        }

        private IEnumerator PlayPoolHecklerApproach(NPCController brother, NPCController vivian)
        {
            _poolSceneAnimating = true;
            try
            {
                var little = FindStoryActor("pool_heckler_little");
                if (brother && vivian)
                    yield return PlayStoryWalk(brother, vivian.transform.position + Vector3.right * 1.4f, .6f);
                if (little && brother)
                    yield return PlayStoryWalk(little, brother.transform.position + Vector3.forward, .6f);
            }
            finally { _poolSceneAnimating = false; }
            if (brother && InRestoredPool) VivianPoolHecklers(brother);
        }

        private void FinishVivianPoolTrip(NPCController vivian)
        {
            StartCoroutine(PlayVivianPoolReturn(vivian));
        }

        private IEnumerator PlayVivianPoolReturn(NPCController vivian)
        {
            if (vivian) yield return PlayPoolDeparture(vivian);
            SetVivianPoolVisit(3);
            if (InRestoredPool) yield return LoadInterior(BarRoom);
        }

        private IEnumerator PlayPoolHecklerDeparture()
        {
            foreach (string id in new[] { "pool_heckler_big", "pool_heckler_little" })
                if (FindStoryActor(id) is { } npc) yield return PlayPoolDeparture(npc);
        }
    }
}
