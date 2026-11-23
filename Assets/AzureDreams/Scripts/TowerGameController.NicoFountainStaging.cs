using System;
using System.Collections;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private bool _nicoFountainAnimating;
        private bool _nicoGhoshMusic;
        private bool _nicoGhoshLeaving;
        private int _nicoFountainSceneVersion;
        private Vector3 _nicoGhoshDeparture;
        private const string NicoCheroArrivedKey = StoryPrefix + "nico.chero_arrived";

        private bool HandleNicoCheroArrival()
        {
            if (_area != Area.Town || _interior || !_player || _storySpeaker || _inventoryOpen || _settingsOpen ||
                StoryStage("nico") != NicoCheroIntroduction || !StoryNewDay("nico") ||
                TownSaveVariables.GetBool(NicoCheroArrivedKey)) return false;
            var chero = FindStoryActor("chero");
            if (!chero || Vector3.Distance(_player.transform.position, chero.transform.position) > 5f) return false;
            TownSaveVariables.SetBool(NicoCheroArrivedKey, true);
            TownSaveVariables.Save();
            StartCoroutine(PlayNicoCheroArrival(chero));
            return true;
        }

        private IEnumerator PlayNicoCheroArrival(NPCController chero)
        {
            _nicoFountainAnimating = true;
            try
            {
                var stop = chero.transform.position;
                chero.transform.position = stop + Vector3.left * 3f;
                yield return PlayStoryWalk(chero, stop, 1.2f);
            }
            finally { _nicoFountainAnimating = false; }
            if (chero && _area == Area.Town && !_interior) NicoStory(chero);
        }

        private void BeginNicoGhoshArrival(NPCController ghosh, Action dialogue)
        {
            if (!ghosh || _nicoFountainAnimating || _nicoGhoshLeaving) return;
            _nicoFountainGhoshGuest = ghosh;
            StartCoroutine(PlayNicoGhoshArrival(ghosh, dialogue));
        }

        private IEnumerator PlayNicoGhoshArrival(NPCController ghosh, Action dialogue)
        {
            int scene = _nicoFountainSceneVersion;
            _nicoFountainAnimating = true;
            try
            {
                Vector3 stop = ghosh.transform.position;
                // Enter from the street beside the square, rather than materializing beside Nico.
                _nicoGhoshDeparture = stop + Vector3.right * 4f;
                ghosh.transform.position = _nicoGhoshDeparture;
                RefreshTownNpcCells();
                yield return PlayStoryWalk(ghosh, stop, 1.4f);
                RefreshTownNpcCells();
            }
            finally { if (scene == _nicoFountainSceneVersion) _nicoFountainAnimating = false; }
            if (scene != _nicoFountainSceneVersion || !ghosh || _area != Area.Town || _interior) yield break;
            _nicoGhoshMusic = true;
            PlayAreaMusic("GhoshTheme.mp3");
            dialogue?.Invoke();
        }

        private IEnumerator PlayNicoGhoshDeparture(NPCController ghosh, Action done)
        {
            int scene = _nicoFountainSceneVersion;
            _nicoGhoshLeaving = true;
            _nicoFountainAnimating = true;
            try
            {
                if (ghosh) yield return PlayStoryWalk(ghosh, _nicoGhoshDeparture, 1.4f);
                if (ghosh)
                {
                    _townPeople.Remove(ghosh);
                    RemoveGenerated(ghosh.gameObject);
                    RefreshTownNpcCells();
                }
            }
            finally
            {
                if (scene == _nicoFountainSceneVersion)
                {
                    _nicoFountainAnimating = false;
                    _nicoGhoshMusic = false;
                    _nicoGhoshLeaving = false;
                    if (_area == Area.Town && !_interior) PlayAreaMusic("Town");
                }
            }
            if (scene == _nicoFountainSceneVersion && _area == Area.Town && !_interior) done?.Invoke();
        }

        private void FinishNicoGhoshVisit(Action done)
        {
            if (_nicoGhoshLeaving) return;
            var ghosh = _nicoFountainGhoshGuest;
            _nicoFountainGhoshGuest = null;
            if (ghosh) StartCoroutine(PlayNicoGhoshDeparture(ghosh, done));
            else { _nicoGhoshMusic = false; if (_area == Area.Town && !_interior) PlayAreaMusic("Town"); done?.Invoke(); }
        }

        private void SpawnNicoChero()
        {
            if (FindStoryActor("chero")) return;
            SpawnStoryResident("monster_shop_hunter", "chero", "Chero", TownResidentPlacementCatalog.GetTownCells("chero")[0]);
        }
    }
}
