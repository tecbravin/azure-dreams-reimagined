using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        [SerializeField] private Texture2D _elevatorSheet;
        private Sprite[] _elevatorFrames;
        private Sprite _elevatorSide;
        private Mesh _elevatorCrystalMesh;
        private readonly System.Collections.Generic.List<(Transform Familiar, Transform Crystal, Vector3 Start,
            Vector3 Scale, Vector3 Offset, float Spin)> _elevatorFamiliars = new(2);
        private const float ElevatorBoardingDuration = 0.32f;
        private const float ElevatorBlockDepth = 2.05f;
        private const float ElevatorBlockHalfWidth = 0.48f;
        private SpriteRenderer _elevatorBase, _elevatorGlow;
        private Transform _elevatorPlatform;
        private AudioSource _elevatorAudio;
        private Vector3 _elevatorStart, _elevatorRiderStart;
        private float _elevatorElapsed;
        private bool _elevatorAscending;
        private bool _quakeAscending;
        private int _floorStartTurn;
        private float _towerShakeStartedAt = float.NegativeInfinity;
        private Vector3 _quakeCameraStart;
        private GameObject _demoTowerGround;
        private const int QuakeWarningTurn = 0x401;
        private const int FloorCollapseTurn = 1220;

        private bool CanQuake => _floor < TowerFloorRules.BeldoFloor &&
            _towerRun?.CurrentState == TowerRunManager.State.InTower;

        private bool TryStartFloorCollapse()
        {
            if (!CanQuake || _completedTowerTurns - _floorStartTurn < FloorCollapseTurn || !_player)
                return false;

            _checkpointMenu = CheckpointMenu.Closed;
            _message = T("The floor is collapsing!", "O andar está desmoronando!");
            PlayRetailSound(RetailSfx.TerrainShift);
            if (_gameCamera) _quakeCameraStart = _gameCamera.transform.position - TowerQuakeShake();
            BeginElevatorAscent(fromQuake: true);
            return true;
        }

        private void ShowTowerQuakeWarning()
        {
            if (!CanQuake || _completedTowerTurns - _floorStartTurn != QuakeWarningTurn) return;
            _towerShakeStartedAt = Time.time;
            PlayRetailSound(RetailSfx.Rumble);
            int combatLines = _combatMessageLines;
            _message = JoinTowerMessages(
                T("The tower is shaking!", "A torre está tremendo!"), combatLines > 0 ? _message : null);
            if (combatLines > 0) _combatMessageLines = combatLines + 1;
        }

        private Vector3 TowerQuakeShake()
        {
            float elapsed = _quakeAscending ? _elevatorElapsed : (Time.time - _towerShakeStartedAt) * TowerAnimationSpeed;
            if (elapsed >= ElevatorAscentDuration) return Vector3.zero;
            float amplitude = (_quakeAscending ? 0.24f : 0.12f) * (1f - elapsed / ElevatorAscentDuration);
            return _gameCamera.transform.right * (Mathf.Sin(elapsed * 83f) * amplitude) +
                _gameCamera.transform.up * (Mathf.Sin(elapsed * 111f) * amplitude * 0.55f);
        }
        private const float ElevatorAscentDuration = 1.15f;
        private static readonly Vector2Int[] ElevatorColors =
        {
            new(0, 0), new(33, 0), new(66, 0), new(99, 0), new(132, 0),
            new(33, 33), new(66, 33), new(99, 33), new(132, 33), new(33, 66)
        };

        private void CreateTowerElevator()
        {
            if (!_elevatorSheet || _elevatorSheet.width < 164 || _elevatorSheet.height < 98)
            {
                _elevatorPlatform = CreateMarker(_stairCell, "Tower Stairs", _accentMaterial, PrimitiveType.Cylinder, 0.7f).transform;
                return;
            }
            if (_elevatorFrames == null)
            {
                _elevatorFrames = new Sprite[ElevatorColors.Length];
                for (int i = 0; i < ElevatorColors.Length; i++)
                {
                    var cell = ElevatorColors[i];
                    _elevatorFrames[i] = Sprite.Create(_elevatorSheet,
                        new Rect(cell.x, _elevatorSheet.height - cell.y - 32, 32, 32),
                        Vector2.one * 0.5f, 32f, 0, SpriteMeshType.FullRect);
                }
            }
            var root = new GameObject("Tower Stairs");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(_stairCell.x, TowerGroundY(_stairCell), _stairCell.y);
            _generated.Add(root);
            _elevatorPlatform = root.transform;
            AddElevatorBlockSides(root.transform);
            // Keep the glow above the orb and both layers behind actors (sorting order 0).
            _elevatorBase = CreateElevatorLayer(root.transform, "Elevator orb", 0.018f, -2);
            _elevatorGlow = CreateElevatorLayer(root.transform, "Elevator glow", 0.021f, -1);
            AnimateTowerElevator();
        }

        private void BeginElevatorAscent(bool fromQuake = false)
        {
            _elevatorAscending = true;
            _quakeAscending = fromQuake;
            _elevatorElapsed = 0f;
            _elevatorRiderStart = _player.transform.position;
            if (_elevatorPlatform) _elevatorStart = _elevatorPlatform.position;
            _playerLocomotion?.Present(Vector3.zero, 0f, false);
            Animate(_playerAnimation, false);
            if (fromQuake) return;
            Vector3 right = _gameCamera ? _gameCamera.transform.right : Vector3.right;
            right.y = 0f;
            right.Normalize();
            AddElevatorFamiliar(_familiar, right * 0.76f, new Color(1f, 0.21f, 0.25f), 1f);
            AddElevatorFamiliar(_blueCollarAlly?.View, -right * 0.76f, new Color(0.22f, 0.52f, 1f), -1f);
            PresentElevatorFamiliars(0f, Vector3.zero);
            var sound = Resources.Load<AudioClip>("Tower/ElevatorAscent");
            if (!sound) return;
            if (!_elevatorAudio)
            {
                _elevatorAudio = gameObject.AddComponent<AudioSource>();
                _elevatorAudio.playOnAwake = false;
                _elevatorAudio.spatialBlend = 0f;
                _elevatorAudio.volume = _sfxVolume;
            }
            _elevatorAudio.PlayOneShot(sound, 0.65f);
        }

        private void TickElevatorAscent(float deltaTime)
        {
            if (!_elevatorAscending) return;
            float previousProgress = Mathf.Clamp01(ElevatorTravelTime / ElevatorAscentDuration);
            _elevatorElapsed += deltaTime;
            float progress = Mathf.Clamp01(ElevatorTravelTime / ElevatorAscentDuration);
            var rise = Vector3.up * (3.4f * progress * progress);
            if (_quakeAscending)
            {
                var drop = Vector3.down * (2.5f * (progress * progress - previousProgress * previousProgress));
                if (_managedFloorMesh) _managedFloorMesh.transform.position += drop;
                if (_managedWallMesh) _managedWallMesh.transform.position += drop;
                if (_demoTowerGround) _demoTowerGround.transform.position += drop;
            }
            else if (_elevatorPlatform) _elevatorPlatform.position = _elevatorStart + rise;
            if (_player) _player.transform.position = _elevatorRiderStart + rise;
            PresentElevatorFamiliars(progress, rise);
            if (progress < 1f) return;
            _elevatorAscending = false;
            CompleteElevatorAscent();
        }

        private void ResetElevatorAscent()
        {
            if (_elevatorAscending && _player) _player.transform.position = _elevatorRiderStart;
            if (_elevatorAscending && !_quakeAscending && _elevatorPlatform) _elevatorPlatform.position = _elevatorStart;
            foreach (var rider in _elevatorFamiliars)
            {
                if (rider.Familiar)
                {
                    rider.Familiar.position = rider.Start;
                    rider.Familiar.localScale = rider.Scale;
                }
                if (rider.Crystal) RemoveGenerated(rider.Crystal.gameObject);
            }
            _elevatorFamiliars.Clear();
            _quakeAscending = false;
            _towerShakeStartedAt = float.NegativeInfinity;
            _demoTowerGround = null;
            _elevatorAscending = false;
            if (_elevatorAudio) _elevatorAudio.Stop();
        }

        private float ElevatorTravelTime => _elevatorElapsed -
            (_elevatorFamiliars.Count > 0 ? ElevatorBoardingDuration : 0f);

        private void AddElevatorBlockSides(Transform root)
        {
            if (!_elevatorSide)
                _elevatorSide = Sprite.Create(_elevatorSheet, new Rect(0f, _elevatorSheet.height - 98f, 32f, 65f),
                    new Vector2(0.5f, 1f), 32f, 0, SpriteMeshType.FullRect);
            for (int i = 0; i < 4; i++)
            {
                var face = new GameObject("Elevator block face");
                face.transform.SetParent(root, false);
                var rotation = Quaternion.Euler(0f, i * 90f, 0f);
                face.transform.localRotation = rotation;
                // The terrain's step faces sit at the tile boundary (0.5).
                // Keep the moving block inside that boundary to avoid coplanar faces.
                face.transform.localPosition = rotation * (Vector3.back * ElevatorBlockHalfWidth);
                face.transform.localScale = new Vector3(2f * ElevatorBlockHalfWidth, ElevatorBlockDepth * 32f / 65f, 1f);
                var renderer = face.AddComponent<SpriteRenderer>();
                renderer.sprite = _elevatorSide;
                renderer.sortingOrder = -3;
            }
        }

        private void AddElevatorFamiliar(GameObject familiar, Vector3 offset, Color tint, float spin)
        {
            if (!familiar) return;
            var crystal = new GameObject(spin > 0f ? "Red elevator crystal" : "Blue elevator crystal");
            crystal.transform.SetParent(transform, false);
            crystal.AddComponent<MeshFilter>().sharedMesh = ElevatorCrystalMesh();
            var renderer = crystal.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _accentMaterial;
            var colors = new MaterialPropertyBlock();
            colors.SetColor("_BaseColor", tint);
            colors.SetColor("_Color", tint);
            renderer.SetPropertyBlock(colors);
            _generated.Add(crystal);
            _elevatorFamiliars.Add((familiar.transform, crystal.transform, familiar.transform.position,
                familiar.transform.localScale, offset, spin));
        }

        private Mesh ElevatorCrystalMesh()
        {
            if (_elevatorCrystalMesh) return _elevatorCrystalMesh;
            var ring = new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
            var vertices = new Vector3[24];
            var indices = new int[24];
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = ring[i] * 0.24f, b = ring[(i + 1) % 4] * 0.24f;
                int start = i * 6;
                vertices[start] = a;
                vertices[start + 1] = b;
                vertices[start + 2] = Vector3.up * 0.43f;
                vertices[start + 3] = b;
                vertices[start + 4] = a;
                vertices[start + 5] = Vector3.down * 0.43f;
                for (int j = 0; j < 6; j++) indices[start + j] = start + j;
            }
            _elevatorCrystalMesh = new Mesh { name = "Elevator familiar crystal", vertices = vertices, triangles = indices };
            _elevatorCrystalMesh.RecalculateNormals();
            _elevatorCrystalMesh.RecalculateBounds();
            return _elevatorCrystalMesh;
        }

        private void PresentElevatorFamiliars(float ascent, Vector3 rise)
        {
            float boarding = Mathf.SmoothStep(0f, 1f, _elevatorElapsed / ElevatorBoardingDuration);
            foreach (var rider in _elevatorFamiliars)
            {
                if (!rider.Familiar || !rider.Crystal) continue;
                float angle = rider.Spin * ascent * 720f;
                Vector3 center = _elevatorRiderStart + rider.Offset + rise + Vector3.up * 0.34f;
                rider.Crystal.SetPositionAndRotation(center, Quaternion.Euler(0f, angle, 12f * rider.Spin));
                rider.Crystal.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, boarding);
                rider.Familiar.position = Vector3.Lerp(rider.Start, center, boarding);
                rider.Familiar.localScale = rider.Scale * Mathf.Lerp(1f, 0.04f, boarding);
            }
        }

        private SpriteRenderer CreateElevatorLayer(Transform parent, string name, float height, int sorting)
        {
            var layer = new GameObject(name);
            layer.transform.SetParent(parent, false);
            layer.transform.localPosition = Vector3.up * height;
            layer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sorting;
            return renderer;
        }

        private void AnimateTowerElevator()
        {
            if (!_elevatorBase || !_elevatorGlow) return;
            float cycle = Mathf.PingPong(Time.time * TowerAnimationSpeed * 3.5f, _elevatorFrames.Length - 1);
            int first = Mathf.FloorToInt(cycle);
            _elevatorBase.sprite = _elevatorFrames[first];
            _elevatorGlow.sprite = _elevatorFrames[Mathf.Min(first + 1, _elevatorFrames.Length - 1)];
            _elevatorGlow.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, cycle - first));
        }
    }
}
