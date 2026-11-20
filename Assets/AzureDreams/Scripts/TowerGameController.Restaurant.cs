using System;
using System.Collections;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        [Serializable] private sealed class RestaurantDishText
        {
            public string[] english;
            public string[] portuguese;
        }
        [Serializable] private sealed class RestaurantDishCatalog
        {
            public RestaurantDishText[] entries;
        }

        private RestaurantDishCatalog _restaurantDishes;
        private bool _restaurantMealActive;
        private bool _restaurantMealAnimating;
        private Coroutine _restaurantMealRoutine;
        private Vector3 _restaurantMealOrigin;
        private Vector2 _restaurantMealFacing;

        private void BeginRestaurantMeal(NPCController patty, Action after)
        {
            if (_restaurantMealActive || !_player || StoryRoom != RestaurantRoom) return;
            _restaurantMealActive = true;
            _restaurantMealOrigin = _player.transform.position;
            _restaurantMealFacing = _townFacing;
            _townMomentum = Vector2.zero;
            _townJumpHeight = _townJumpVelocity = 0f;
            _kohSprite?.SetAirborne(false);
            _restaurantMealAnimating = true;
            _restaurantMealRoutine = StartCoroutine(PlayRestaurantMeal(patty, after));
        }

        private IEnumerator PlayRestaurantMeal(NPCController patty, Action after)
        {
            // Empty chair at the left end of the right-hand table. Its seat is
            // x=19..20, z=12.25..13 and its back is at z=12 in the captured mesh.
            var seat = new Vector3(19.5f, 0f, 12.6f);
            seat.y = RetailTownHeight(new Vector3(18f, 0f, seat.z)) + TownActorFootOffset;
            var aisle = new Vector3(18f, seat.y, seat.z);
            yield return WalkToRestaurantSeat(aisle);
            yield return WalkToRestaurantSeat(seat);
            _townFacing = Vector2.up;
            _kohSprite?.SetFacing(_townFacing);
            _playerLocomotion?.Present(Vector3.forward, 0f, false);
            // Lower into the original seated cels and hold them during dialogue.
            for (float time = 0f; time < 0.35f; time += Time.unscaledDeltaTime)
            {
                float progress = time / 0.35f;
                if (progress >= 0.5f) _kohSprite?.PresentDining(true);
                _player.transform.position = seat + Vector3.up * (0.10f * (1f - progress));
                yield return null;
            }
            _player.transform.position = seat;
            _kohSprite?.PresentDining(true);
            yield return PlayPattyServeAnimation(patty);
            _kohSprite?.PresentDining(true, eating: true);
            yield return new WaitForSecondsRealtime(1.2f);
            _restaurantMealAnimating = false;
            _restaurantMealRoutine = null;
            after?.Invoke();
        }

        private IEnumerator WalkToRestaurantSeat(Vector3 target)
        {
            Vector3 start = _player.transform.position;
            Vector3 direction = target - start;
            direction.y = 0f;
            float duration = Mathf.Max(0.1f, direction.magnitude / _townMoveSpeed);
            for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                _player.transform.position = Vector3.Lerp(start, target, time / duration);
                _townFacing = new Vector2(direction.x, direction.z).normalized;
                _playerLocomotion?.Present(direction, time * 4f, true);
                _kohSprite?.SetFacing(_townFacing);
                yield return null;
            }
            _player.transform.position = target;
            _playerCell = new Vector2Int(Mathf.RoundToInt(target.x), Mathf.RoundToInt(target.z));
            _playerLocomotion?.Present(direction, 0f, false);
        }

        private void ShowRestaurantDishDescription(NPCController patty, int dish, Action after)
        {
            if (_restaurantDishes == null)
            {
                var asset = AssetMode.Load<TextAsset>("Town/Retail/restaurant_meals");
                if (asset) _restaurantDishes = JsonUtility.FromJson<RestaurantDishCatalog>(asset.text);
            }
            var entries = _restaurantDishes?.entries;
            if (!patty || entries == null || (uint)dish >= (uint)entries.Length)
            {
                _kohSprite?.PresentDining(true);
                after?.Invoke();
                return;
            }
            var text = entries[dish];
            var pages = GameLanguage.Portuguese ? text.portuguese : text.english;
            void ShowPage(int page)
            {
                if (pages == null || page >= pages.Length)
                {
                    _kohSprite?.PresentDining(true);
                    after?.Invoke();
                    return;
                }
                // These descriptions are Koh's thoughts in the original script.
                StoryTalk(patty, pages[page], pages[page], () => ShowPage(page + 1));
                _dialogueSpeaker = HeroName;
                _dialoguePortrait = null;
                _storyPortraitPath = null;
            }
            ShowPage(0);
        }

        private void FinishRestaurantMeal()
        {
            if (!_restaurantMealActive || _restaurantMealAnimating) return;
            _restaurantMealAnimating = true;
            _restaurantMealRoutine = StartCoroutine(LeaveRestaurantSeat());
        }

        private IEnumerator LeaveRestaurantSeat()
        {
            _kohSprite?.PresentDining(true);
            Vector3 seat = _player.transform.position;
            for (float time = 0f; time < 0.25f; time += Time.unscaledDeltaTime)
            {
                if (time >= 0.12f) _kohSprite?.PresentDining(false);
                _player.transform.position = seat + Vector3.up * (time * 0.4f);
                yield return null;
            }
            _kohSprite?.PresentDining(false);
            yield return WalkToRestaurantSeat(new Vector3(18f, seat.y, seat.z));
            yield return WalkToRestaurantSeat(_restaurantMealOrigin);
            _restaurantMealRoutine = null;
            ResetRestaurantMeal();
        }

        private void ResetRestaurantMeal()
        {
            if (!_restaurantMealActive) return;
            if (_restaurantMealRoutine != null) StopCoroutine(_restaurantMealRoutine);
            _restaurantMealRoutine = null;
            _restaurantMealActive = _restaurantMealAnimating = false;
            _kohSprite?.PresentDining(false);
            _townFacing = _restaurantMealFacing;
            _kohSprite?.SetFacing(_townFacing);
            _playerLocomotion?.Present(new Vector3(_townFacing.x, 0f, _townFacing.y), 0f, false);
            _townMomentum = Vector2.zero;
        }
    }
}
