using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        // Source coordinates come from the three captured house layouts.  The
        // first house is mirrored through HomeLayoutPoint together with all of
        // its other room features, so the bath stays in the correct left room.
        private static readonly (Vector3 Water, float Duration, float Distance, float Radius, float CameraPan)[] HomeBathLayouts =
        {
            (new Vector3(21.5f, 0f, 10.5f), 2.4f, 3.7f, 0.72f, 5f),
            (new Vector3(35.5f, 0f, 12.5f), 3.2f, 4.3f, 0.94f, -9f),
            (new Vector3(55.5f, 0f, 13.5f), 4.2f, 5.4f, 1.16f, 10f)
        };

        private int _homeBathRank = -1;
        private float _homeBathTime;
        private Vector3 _homeBathStart;
        private LineRenderer _homeBathRipple;
        private Material _homeBathRippleMaterial;

        private Vector3 BathWaterPosition(int rank)
        {
            var layout = HomeBathLayouts[rank];
            Vector3 water = HomeLayoutPoint(rank, layout.Water);
            // Keep the water tied to the captured height map instead of a fixed
            // world Y value.  The three house upgrades use different floor
            // elevations, which was why the old effect could float or sink.
            water.y = RetailTownHeight(water) + 0.075f;
            return water;
        }

        private void SetupHomeBathScene(int rank)
        {
            if (rank < 0 || rank >= HomeBathLayouts.Length) return;
            var layout = HomeBathLayouts[rank];
            Vector3 water = BathWaterPosition(rank);
            Color waterColor = rank switch
            {
                0 => new Color(0.26f, 0.64f, 0.83f, 1f),
                1 => new Color(0.30f, 0.72f, 0.88f, 1f),
                _ => new Color(0.38f, 0.78f, 0.90f, 1f)
            };

            // A shallow, non-blocking water surface gives each house level an
            // actual bath set piece.  The captured room mesh remains the basin
            // and walls; this is only the animated water layer.
            var waterView = CreatePrimitive(PrimitiveType.Cylinder, "Koh house bath water",
                water, new Vector3(layout.Radius * 2f, 0.028f, layout.Radius * 2f), null, false);
            if (waterView.TryGetComponent<Renderer>(out var renderer))
            {
                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                properties.SetColor("_Color", waterColor);
                properties.SetColor("_BaseColor", waterColor);
                renderer.SetPropertyBlock(properties);
            }

            var steamRoot = new GameObject("Koh house bath steam");
            steamRoot.transform.SetParent(transform, false);
            steamRoot.transform.position = water + Vector3.up * 0.04f;
            _generated.Add(steamRoot);
            var steam = steamRoot.AddComponent<ParticleSystem>();
            var main = steam.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.45f + rank * 0.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.20f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startColor = new Color(0.92f, 0.98f, 1f, 0.36f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = steam.emission;
            emission.rateOverTime = 3f + rank * 2f;
            var shape = steam.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = layout.Radius * 0.58f;
            var velocity = steam.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(0.24f + rank * 0.04f);
            steam.Play(true);
        }

        private void BeginHomeBath(int rank)
        {
            MoveThroughBathPassage(HomeLayoutCell(rank, BathPoints[rank]));
            _homeBathStart = _player.transform.position;
            _homeBathRank = rank;
            _homeBathTime = 0f;
            _message = T("Koh enters the bath.", "Koh entra no banho.");
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
            Vector3 water = BathWaterPosition(rank);
            _homeBathTime += deltaTime;
            float progress = Mathf.Clamp01(_homeBathTime / layout.Duration);
            float approach = Mathf.SmoothStep(0f, 1f, progress / 0.24f);
            float retreat = Mathf.SmoothStep(0f, 1f, (progress - 0.76f) / 0.24f);
            float immersion = approach * (1f - retreat);
            // Keep Koh's root at the same relative depth used by the retail
            // house scenes.  House 1 has a shallower basin; the larger homes
            // submerge him farther so the water line hides his lower body.
            Vector3 position = rank == 0
                ? new Vector3(water.x + 0.10f, _homeBathStart.y - 0.05f, water.z + 0.18f)
                : water + Vector3.up * (TownActorFootOffset - 0.53f);
            if (rank == 2) position.z -= 0.20f * Mathf.Sin(progress * Mathf.PI);
            position.y += Mathf.Sin(_homeBathTime * (rank == 0 ? 12f : 5f)) * 0.045f;
            _player.transform.position = Vector3.Lerp(_homeBathStart, position, immersion);

            var facing = water - _homeBathStart;
            _townFacing = new Vector2(facing.x, facing.z).normalized;
            if (_kohSprite) _kohSprite.SetFacing(_townFacing);
            float pan = Mathf.SmoothStep(-layout.CameraPan, layout.CameraPan, progress);
            SetEntranceCamera(Vector3.Lerp(_homeBathStart, water, 0.62f) + Vector3.up * 0.25f,
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
                    _homeBathRipple.SetPosition(i, water + new Vector3(Mathf.Cos(angle) * radius, 0.04f,
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
