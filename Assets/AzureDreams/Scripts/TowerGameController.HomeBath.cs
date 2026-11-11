using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private static readonly (Vector3 Water, float Duration, float Distance, float Radius)[] HomeBathLayouts =
        {
            (new Vector3(21.5f, 0.625f, 10.5f), 2.4f, 3.7f, 0.30f),
            (new Vector3(35.5f, 0.625f, 12.5f), 3.2f, 4.3f, 0.34f),
            (new Vector3(55.5f, 0.625f, 13.5f), 4.2f, 5.4f, 0.35f)
        };

        private int _homeBathRank = -1;
        private float _homeBathTime;
        private Vector3 _homeBathStart;
        private LineRenderer _homeBathRipple;
        private Material _homeBathRippleMaterial;

        private void BeginHomeBath(int rank)
        {
            MoveThroughBathPassage(BathPoints[rank]);
            _homeBathStart = _player.transform.position;
            _homeBathRank = rank;
            _homeBathTime = 0f;
            _message = string.Empty;
            if (_playerLocomotion) _playerLocomotion.Present(Vector3.zero, 0f, false);

            var shader = Shader.Find("Sprites/Default");
            if (!shader) return;
            var ripple = new GameObject("Bath ripple");
            ripple.transform.SetParent(transform, false);
            _homeBathRippleMaterial = new Material(shader);
            _homeBathRipple = ripple.AddComponent<LineRenderer>();
            _homeBathRipple.sharedMaterial = _homeBathRippleMaterial;
            _homeBathRipple.loop = true;
            _homeBathRipple.positionCount = 20;
            _homeBathRipple.widthMultiplier = 0.035f;
            _homeBathRipple.enabled = false;
        }

        private bool TickHomeBath(float deltaTime)
        {
            if (_homeBathRank < 0) return false;
            if (!_player || HouseIndex != _homeBathRank)
            {
                EndHomeBath();
                return false;
            }

            int rank = _homeBathRank;
            var layout = HomeBathLayouts[rank];
            _homeBathTime += deltaTime;
            float progress = Mathf.Clamp01(_homeBathTime / layout.Duration);
            float approach = Mathf.SmoothStep(0f, 1f, progress / 0.24f);
            float retreat = Mathf.SmoothStep(0f, 1f, (progress - 0.76f) / 0.24f);
            float immersion = approach * (1f - retreat);
            Vector3 position = layout.Water;
            if (rank == 0)
                position = new Vector3(20.85f, _homeBathStart.y, 10.75f);
            else
                position.y += TownActorFootOffset - 0.53f;
            if (rank == 2) position.z -= 0.20f * Mathf.Sin(progress * Mathf.PI);
            position.y += Mathf.Sin(_homeBathTime * (rank == 0 ? 12f : 5f)) * 0.045f;
            _player.transform.position = Vector3.Lerp(_homeBathStart, position, immersion);

            var facing = layout.Water - _homeBathStart;
            _townFacing = new Vector2(facing.x, facing.z).normalized;
            if (_kohSprite) _kohSprite.SetFacing(_townFacing);
            float pan = rank == 2 ? Mathf.SmoothStep(-12f, 10f, progress) : rank == 1 ? -9f : 5f;
            SetEntranceCamera(Vector3.Lerp(_homeBathStart, layout.Water, 0.62f) + Vector3.up * 0.25f,
                CameraYaw() + pan, GameplayCameraPitch, layout.Distance);

            if (_homeBathRipple)
            {
                float phase = Mathf.Repeat(_homeBathTime * (rank == 0 ? 2.6f : 1.7f), 1f);
                _homeBathRipple.enabled = immersion > 0.85f;
                var tint = new Color(0.75f, 0.95f, 1f, (1f - phase) * 0.9f);
                _homeBathRipple.startColor = _homeBathRipple.endColor = tint;
                float radius = Mathf.Lerp(0.06f, layout.Radius, phase);
                for (int i = 0; i < _homeBathRipple.positionCount; i++)
                {
                    float angle = 2f * Mathf.PI * i / _homeBathRipple.positionCount;
                    _homeBathRipple.SetPosition(i, layout.Water + new Vector3(Mathf.Cos(angle) * radius, 0.04f,
                        Mathf.Sin(angle) * radius * (rank == 2 ? 1.35f : 1f)));
                }
            }

            if (progress < 1f) return true;
            EndHomeBath();
            _health = _playerDefinition ? _playerDefinition.MaxHealth : 20;
            if (_player.TryGetComponent<CharacterStats>(out var stats)) stats.currentHealth = _health;
            _message = T($"{HeroName} takes a bath and feels refreshed.", $"{HeroName} toma banho e se sente revigorado.");
            return true;
        }

        private void EndHomeBath()
        {
            if (_homeBathRank < 0) return;
            _homeBathRank = -1;
            if (_homeBathRipple) Destroy(_homeBathRipple.gameObject);
            if (_homeBathRippleMaterial) Destroy(_homeBathRippleMaterial);
            _homeBathRipple = null;
            _homeBathRippleMaterial = null;
            ResetEntranceCamera();
            if (!_player) return;
            _player.transform.position = _homeBathStart;
            UpdateCameraFollow(snap: true);
        }
    }
}
