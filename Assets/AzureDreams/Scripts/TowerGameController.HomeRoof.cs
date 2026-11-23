using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private bool _homeSliding;
        private float _homeSlideElapsed;
        private Vector3 _homeSlideStart;
        private bool HasHomeRoofAccess => _area == Area.Town && !_interior && UsingRetailTown && CurrentHomeRank >= 2;

        // Coordinates and heights from Construction/home.bytes: rear ramp X54..58/Z47..48,
        // opening at X54..55, and the original water plane X52..58/Z49..52 at Y3.25.
        private static float HomeRoofSurface(Vector3 point)
        {
            if (point.x >= 54.15f && point.x <= 54.85f && point.z >= 47.65f && point.z <= 49.2f)
                return point.z < 48.5f ? Mathf.Lerp(3f, 3.5f, Mathf.InverseLerp(47.65f, 48.5f, point.z)) :
                    Mathf.Lerp(3.5f, 3.25f, Mathf.InverseLerp(48.5f, 49f, point.z));
            if (point.x >= 54.15f && point.x <= 62.4f && point.z >= 47.1f && point.z <= 47.8f)
                return Mathf.Clamp(58f - point.x, 0f, 3f);
            if (point.x >= 57.8f && point.x <= 62.4f && point.z >= 46.55f && point.z <= 47.8f)
                return 0f;
            if (point.x >= 51.65f && point.x <= 58.35f && point.z >= 48.65f && point.z <= 52.35f)
                return 3.25f;
            if (point.x >= 58.1f && point.x <= 60.65f && point.z >= 50.65f && point.z <= 55.65f)
                return 3.5f;
            return float.NegativeInfinity;
        }

        private float HomeRoofGround(Vector3 point) => HasHomeRoofAccess ? HomeRoofSurface(point) : float.NegativeInfinity;

        private bool TickHomeSlide(Vector2 input, float deltaTime)
        {
            if (!HasHomeRoofAccess) { ResetHomeSlide(); return false; }
            var before = _player.transform.position;
            if (!_homeSliding)
            {
                if (input.x <= .2f || before.x < 54.2f || before.x > 55.1f ||
                    before.z < 47.1f || before.z > 47.8f || before.y - TownActorFootOffset < 2.7f ||
                    _townJumpVelocity != 0f || _carriedItem != null || _heldTownPot || _ridingAuraBike) return false;
                _homeSliding = true;
                _homeSlideElapsed = 0f;
                _homeSlideStart = before;
                _townJumpHeight = _townJumpVelocity = 0f;
                _townMomentum = Vector2.zero;
                _kohSprite?.PresentDining(true);
            }
            _homeSlideElapsed += Mathf.Min(deltaTime, .25f);
            float progress = Mathf.Clamp01(_homeSlideElapsed / 1.05f);
            var point = Vector3.Lerp(_homeSlideStart, new Vector3(58.35f, 0f, 47.45f), Mathf.Pow(progress, 1.35f));
            point.y = Mathf.Clamp(58f - point.x, 0f, 3f) + TownActorFootOffset;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(before, point) / .1f));
            for (int i = 1; i <= steps; i++)
                if (!TownPlayerPositionOpen(Vector3.Lerp(before, point, i / (float)steps), before, before.y - TownActorFootOffset))
                { ResetHomeSlide(); return true; }
            _player.transform.position = point;
            _playerCell = new Vector2Int(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.z));
            _townFacing = Vector2.right;
            _kohSprite?.SetFacing(Vector2.right);
            _playerLocomotion?.Present(point - before, progress, false);
            if (progress >= 1f) ResetHomeSlide();
            return true;
        }

        private void ResetHomeSlide()
        {
            if (_homeSliding) _kohSprite?.PresentDining(false);
            _homeSliding = false;
            _homeSlideElapsed = 0f;
        }
    }
}
