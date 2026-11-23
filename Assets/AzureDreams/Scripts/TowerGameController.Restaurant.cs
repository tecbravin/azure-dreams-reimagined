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
        private GameObject _servedDish;
        private AzureDreamsReimagined.Rendering.ActorSpriteDepth _diningDepth;

        private void BeginRestaurantMeal(NPCController patty, int dish, Action after)
        {
            if (_restaurantMealActive || !_player || StoryRoom != RestaurantRoom) return;
            _restaurantMealActive = true;
            _restaurantMealOrigin = _player.transform.position;
            _restaurantMealFacing = _townFacing;
            _townMomentum = Vector2.zero;
            _townJumpHeight = _townJumpVelocity = 0f;
            _kohSprite?.SetAirborne(false);
            _restaurantMealAnimating = true;
            _restaurantMealRoutine = StartCoroutine(PlayRestaurantMeal(patty, dish, after));
        }

        private IEnumerator PlayRestaurantMeal(NPCController patty, int dish, Action after)
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
            _diningDepth = _player.GetComponent<AzureDreamsReimagined.Rendering.ActorSpriteDepth>();
            if (_diningDepth) _diningDepth.SetSeated(true, new Vector3(0f, -TownActorFootOffset, .24f));
            yield return PlayPattyServeAnimation(patty);
            ServeRestaurantDish(dish);
            _kohSprite?.PresentDining(true, eating: true);
            yield return new WaitForSecondsRealtime(1.2f);
            _restaurantMealAnimating = false;
            _restaurantMealRoutine = null;
            after?.Invoke();
        }

        private void ServeRestaurantDish(int dish)
        {
            if ((uint)dish >= RestaurantMeals.Length) return;
            _servedDish = new GameObject(RestaurantMeals[dish].English);
            _servedDish.transform.SetParent(transform, false);
            _servedDish.transform.position = new Vector3(19.55f, .78f, 13.5f);
            _generated.Add(_servedDish);

            var porcelain = new Color(.92f, .91f, .80f);
            var rice = new Color(.93f, .86f, .67f);
            var brown = new Color(.49f, .25f, .10f);
            var green = new Color(.31f, .52f, .22f);
            var orange = new Color(.87f, .47f, .20f);
            void Food(PrimitiveType shape, Vector3 position, Vector3 size, Color color) =>
                HomePart(_servedDish, shape, "food", position, size, color);

            Food(PrimitiveType.Cylinder, Vector3.zero, new Vector3(.56f,.025f,.44f), porcelain);
            switch (dish)
            {
                case 0: case 5: case 6: case 9:
                    Food(PrimitiveType.Sphere, new Vector3(0f,.055f,0f), new Vector3(.37f,.13f,.29f), dish == 6 ? orange : rice);
                    if (dish == 5 || dish == 9)
                        Food(PrimitiveType.Sphere, new Vector3(.08f,.08f,0f), new Vector3(.25f,.11f,.25f), dish == 5 ? orange : brown);
                    break;
                case 1:
                    for (int i=0;i<9;i++)
                        Food(PrimitiveType.Sphere, new Vector3((i%3-1)*.07f,.055f,(i/3-1)*.065f), Vector3.one*.095f, brown);
                    break;
                case 2:
                    Food(PrimitiveType.Cube, new Vector3(0f,.075f,0f), new Vector3(.22f,.12f,.19f), porcelain);
                    Food(PrimitiveType.Sphere, Vector3.up*.14f, new Vector3(.095f,.025f,.08f), green);
                    break;
                case 3: case 4: case 7: case 8: case 10:
                    int count = dish == 4 ? 1 : 3;
                    for (int i=0;i<count;i++)
                        Food(PrimitiveType.Sphere, new Vector3((i-(count-1)*.5f)*.105f,.055f,0f),
                            new Vector3(count==1?.30f:.10f,.085f,dish==3||dish==8?.28f:.17f), dish==8?orange:brown);
                    if (dish==10) Food(PrimitiveType.Sphere,new Vector3(0f,.06f,.11f),new Vector3(.23f,.09f,.10f),green);
                    break;
                default:
                    Color topping = dish switch { 11 => new Color(.72f,.17f,.15f), 12 => new Color(.24f,.50f,.75f), 13 => Color.yellow, 14 => green, _ => orange };
                    for (int i=0;i<2;i++)
                    {
                        float x=(i-.5f)*.17f;
                        Food(PrimitiveType.Cube,new Vector3(x,.052f,0f),new Vector3(.13f,.075f,.19f),rice);
                        Food(PrimitiveType.Cube,new Vector3(x,.10f,0f),new Vector3(.14f,.035f,.20f),topping);
                    }
                    break;
            }
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
            if (_diningDepth) _diningDepth.SetSeated(false, Vector3.zero);
            yield return WalkToRestaurantSeat(new Vector3(18f, seat.y, seat.z));
            yield return WalkToRestaurantSeat(_restaurantMealOrigin);
            _restaurantMealRoutine = null;
            ResetRestaurantMeal();
        }

        private void ResetRestaurantMeal()
        {
            if (_servedDish) RemoveGenerated(_servedDish);
            _servedDish = null;
            if (_diningDepth) _diningDepth.SetSeated(false, Vector3.zero);
            _diningDepth = null;
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
