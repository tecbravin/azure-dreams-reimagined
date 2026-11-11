using AzureDreams.Core;
using System.Collections.Generic;
using UnityEngine;
namespace AzureDreamsReimagined
{
    /// <summary>Camera-relative locomotion using the recovered PS1 body frames.</summary>
    public sealed class KohSpriteView : MonoBehaviour
    {
        private const int CellSize = 64;
        private const int FramesPerCycle = 8;
        private const int AtlasColumns = 17;
        private const int AtlasRows = 5;
        private const float RetailPixelsPerUnit = 48f;
        private const int EquipmentSwords = 12;
        private const int EquipmentWands = 9;
        private const int EquipmentShields = 11;
        private const int EquipmentRows = EquipmentSwords + EquipmentWands + EquipmentShields;
        // Original groups 0–4 contain front, front diagonal, side, rear diagonal and rear.
        private static readonly int[] AtlasRowByOctant = { 4, 3, 2, 1, 0, 1, 2, 3 };

        // Pixel shifts of the cap relative to the idle frame, measured in the source atlas.
        // Matching a fixed body feature leaves the changing silhouette of the stride intact.
        private static readonly Vector2Int[,] RunPivotOffsets =
        {
            { new(0, 1), new(0, 0), new(0, 0), new(0, 1), new(1, 1), new(0, 0), new(0, 0), new(0, 1) },
            { new(-1, 1), new(-1, -1), new(0, 0), new(0, 0), new(0, 1), new(0, 0), new(0, 0), new(0, 0) },
            { new(0, 0), new(0, -2), new(0, -2), new(0, -1), new(-1, 0), new(0, -2), new(-1, -2), new(0, -1) },
            { new(0, 0), new(0, -2), new(0, -1), new(0, -1), new(-1, 1), new(-1, -2), new(-1, -1), new(0, -1) },
            { new(0, 1), new(0, -1), new(0, -1), new(0, -1), new(-1, 1), new(0, -2), new(0, -1), new(0, -1) }
        };

        internal bool HasRetailRun => _retailFrames != null;

        [SerializeField] private Texture2D _sheet;
        [SerializeField] private Texture2D _retailLocomotion;
        private Sprite[] _retailFrames;
        [System.Serializable]
        private sealed class RetailGrounding
        {
            public int[] locomotion;
            public int[] attack;
            public int[] lift;
            public int[] sleep;
        }

        private RetailGrounding _grounding;
        private readonly Dictionary<Sprite, float> _soleClearances = new();
        [SerializeField] private Texture2D _attackAtlas;
        private Sprite[] _attackFrames;
        private Texture2D _sleepAtlas;
        private Sprite[] _sleepFrames;
        private bool _sleeping;
        private float _sleepStartedAt;
        private const int SleepColumns = 8;
        private const int HitColumns = 9;
        private Texture2D _hitAtlas;
        private Sprite[] _hitFrames;
        private bool _hitFramesRequested;
        [SerializeField] private Texture2D _liftAtlas;
        private Sprite[] _liftFrames;
        private bool _lifting;
        private bool _inTown;
        private float PlaybackSpeed => _inTown ? 1f : TowerGameController.TowerAnimationSpeed;
        private float _framingScale = 1f;
        private float _uprightHeightScale = 1f;
        private float _visualUnitsPerPixel = 1f / RetailPixelsPerUnit;
        private Camera _gameplayCamera;
        private Sprite _bagPose;
        private bool _bagPoseFlip;

        internal void ConfigurePresentation(Camera camera, bool inTown, float framingScale, float cameraPitch)
        {
            _gameplayCamera = camera;
            _inTown = inTown;
            _framingScale = framingScale;
            _uprightHeightScale = 1f / Mathf.Cos(cameraPitch * Mathf.Deg2Rad);
            if (_renderer)
            {
                _baseVisualScale ??= _renderer.transform.localScale;
                _baseVisualPosition ??= _renderer.transform.localPosition;
                var scale = _baseVisualScale.Value * _framingScale;
                scale.y *= _uprightHeightScale;
                _renderer.transform.localScale = scale;
                _visualUnitsPerPixel = scale.y / RetailPixelsPerUnit;
            }
        }

        internal void SetBagPose(Sprite pose, bool flip) { _bagPose = pose; _bagPoseFlip = flip; }
        private float _liftStartedAt;
        private const int LiftColumns = 19;
        private const float LiftDuration = 0.28f;
        private const int HeldIdleColumn = 6;
        private const int HeldWalkColumn = 7;
        private const int PushColumn = 16;
        private static readonly int[,] HeldWalkHandRisePixels =
        {
            { -5, 1, 2, 1, -5 },
            { -4, 5, 6, 5, -4 },
            { -3, 5, 7, 5, -3 },
            { -3, 6, 7, 6, -3 },
            { -2, 7, 8, 7, -2 }
        };

        internal void PresentLift(bool lifting)
        {
            if (lifting && !_lifting) _liftStartedAt = Time.time;
            _lifting = lifting;
        }

        private float LiftProgress => Mathf.Clamp01((Time.time - _liftStartedAt) * PlaybackSpeed / LiftDuration);

        internal Vector3 LiftHandPosition(Camera camera, Vector2 direction)
        {
            Vector3 forward = new(direction.x, 0f, direction.y);
            Vector3 right = camera ? camera.transform.right : Vector3.right;
            Vector3 up = camera && !_inTown ? camera.transform.up : Vector3.up;
            if (_inTown) up *= _uprightHeightScale;
            float side = Vector3.Dot(right, forward.normalized);
            // The frontal and rear poses hold the object centered over the head.
            float offset = Mathf.Abs(side) < 0.35f ? 0f : side * 0.16f;
            Vector3 feet = _renderer ? _renderer.transform.position : transform.position - Vector3.up * 0.55f;
            float lift = LiftProgress;
            float height = Mathf.SmoothStep(0.67f, 1.38f, lift);
            if (_liftFrames != null && lift >= 1f && _locomotion && _locomotion.IsMoving)
            {
                AdvanceWalkClock();
                float angle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
                if (camera) angle -= camera.transform.eulerAngles.y;
                int row = AtlasRowByOctant[Mathf.RoundToInt(angle / 45f) & 7];
                height += HeldWalkHandRisePixels[row, HeldWalkStride] / 48f;
            }
            Vector3 hand = feet + (up * height + right * offset) * _framingScale;
            return camera ? hand - camera.transform.forward * 0.07f : hand;
        }

        private int HeldWalkStride
        {
            get
            {
                // Retail timing is 2, 4, 6, 4, 2 ticks across the five poses.
                float tick = _clock * (18f / FramesPerCycle);
                return tick < 2f ? 0 : tick < 6f ? 1 : tick < 12f ? 2 : tick < 16f ? 3 : 4;
            }
        }

        // Camera-relative steps for a celebration in place, using the retail body atlas.
        private static readonly int[] LevelUpDirections = { 4, 3, 2, 3, 4, 5, 6, 5, 4, 4, 4, 4 };
        private float _levelUpProgress = -1f;

        internal void PresentLevelUp(float progress) => _levelUpProgress = progress;

        private float _attackProgress = -1f;
        private float _pushEffort;
        private bool _airborne;
        private bool _paralyzed;

        internal void PresentParalysis(bool paralyzed) => _paralyzed = paralyzed;

        internal void PresentSleep(bool sleeping, bool alreadyLying = false)
        {
            if (_sleeping == sleeping) return;
            _sleeping = sleeping;
            if (!sleeping || _retailFrames == null) return;
            _sleepStartedAt = Time.unscaledTime - (alreadyLying ? 1f : 0f);
            if (_sleepFrames != null) return;
            var asset = Resources.Load<TextAsset>("Tower/koh-retail-sleep");
            if (!asset) return;
            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            if (!atlas.LoadImage(asset.bytes, markNonReadable: true) ||
                atlas.width != SleepColumns * CellSize || atlas.height != AtlasRows * CellSize)
            {
                Destroy(atlas);
                return;
            }
            _sleepAtlas = atlas;
            var frames = new Sprite[AtlasRows * SleepColumns];
            for (int row = 0; row < AtlasRows; row++)
            for (int column = 0; column < SleepColumns; column++)
            {
                int index = row * SleepColumns + column;
                frames[index] = CreateRetailBodyFrame(atlas, row, column, Vector2Int.zero, _grounding?.sleep, index);
            }
            _sleepFrames = frames;
        }

        private static int SleepFrameAt(int tick)
        {
            // Play the collapse once and hold the final pose until waking.
            if (tick < 2) return 0;
            if (tick < 4) return 1;
            if (tick < 8) return 2;
            if (tick < 14) return 3;
            return Mathf.Min(SleepColumns - 1, 4 + (tick - 14) / 2);
        }

        internal void SetAirborne(bool airborne) => _airborne = airborne;
        private Vector3? _baseVisualPosition;
        private Vector3? _baseVisualScale;

        public void PresentPush(Vector2 facing, float effort)
        {
            _pushEffort = Mathf.Clamp01(effort);
            if (_pushEffort > 0f) SetFacing(facing);
        }
        public void PresentAttack(Vector2 facing, float progress)
        {
            SetFacing(facing);
            _attackProgress = progress >= 1f ? -1f : Mathf.Clamp01(progress);
        }
        [SerializeField] private Material _material;
        // Clockwise from screen up. A missing cycle uses the supplied side walk.
        [SerializeField] private DirectionCycle[] _directionCycles = new DirectionCycle[8];

        [System.Serializable]
        private sealed class DirectionCycle
        {
            public Sprite idle;
            public Sprite[] walk;
        }

        private Sprite[] _frames;
        private Vector2 _facing = Vector2.down;

        public void SetFacing(Vector2 direction)
        {
            if (direction.sqrMagnitude > 0.0001f) _facing = direction;
        }
        private SpriteRenderer _renderer;
        private Color? _colorBeforeStatusTint;
        private ActorLocomotionView _locomotion;
        private float _clock;
        private int _clockFrame = -1;

        private void AdvanceWalkClock()
        {
            if (_clockFrame == Time.frameCount) return;
            _clockFrame = Time.frameCount;
            bool walking = !_sleeping && !_paralyzed && _locomotion && _locomotion.IsMoving;
            bool running = _locomotion && _locomotion.IsRunningNow;
            float rate = _retailFrames != null ? (running ? 15f : 10f) : (running ? 16f : 8f);
            _clock = walking ? (_clock + Time.deltaTime * PlaybackSpeed * rate) % FramesPerCycle : 0f;
        }
        public void Configure(Texture2D sheet, Material material) { _sheet = sheet; _material = material; }

        private Sprite CreateRetailBodyFrame(Texture2D atlas, int row, int column, Vector2Int offset, int[] bottoms, int index)
        {
            var rect = new Rect(column * CellSize, (AtlasRows - 1 - row) * CellSize, CellSize, CellSize);
            float pivotY = 11f - offset.y;
            var pivot = new Vector2(32f + offset.x, pivotY) / CellSize;
            var sprite = Sprite.Create(atlas, rect, pivot, RetailPixelsPerUnit, 0, SpriteMeshType.FullRect);
            // The retail anchor sometimes crosses the soles. Lift the whole visual just enough
            // to clear the floor so the gear still lines up and the collider stays in place.
            if (bottoms != null && index < bottoms.Length)
            {
                float clearance = bottoms[index] - (CellSize - pivotY) + 0.25f;
                if (clearance > 0f) _soleClearances.Add(sprite, clearance);
            }
            return sprite;
        }

        private void Awake()
        {
            if (!_retailLocomotion && (!_sheet || !_material)) { enabled = false; return; }
            foreach (var mesh in GetComponentsInChildren<MeshRenderer>(true)) mesh.enabled = false;
            var visual = new GameObject("Koh Sprite");
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = new Vector3(0f, -0.55f, 0f);
            _renderer = visual.AddComponent<SpriteRenderer>();
            if (_retailLocomotion && _retailLocomotion.width == AtlasColumns * CellSize &&
                _retailLocomotion.height == AtlasRows * CellSize)
            {
                var grounding = Resources.Load<TextAsset>("Tower/koh-retail-grounding");
                if (grounding) _grounding = JsonUtility.FromJson<RetailGrounding>(grounding.text);
                _retailFrames = new Sprite[AtlasRows * AtlasColumns];
                _equipmentAtlas = Resources.Load<Texture2D>("Tower/KohEquipment");
                float runClearance = 0f;
                for (int row = 0; row < AtlasRows; row++)
                {
                    for (int column = 0; column < AtlasColumns; column++)
                    {
                        var offset = column > FramesPerCycle ? RunPivotOffsets[row, column - FramesPerCycle - 1] : Vector2Int.zero;
                        int index = row * AtlasColumns + column;
                        var frame = CreateRetailBodyFrame(_retailLocomotion, row, column, offset, _grounding?.locomotion, index);
                        _retailFrames[index] = frame;
                        if (column > FramesPerCycle && _soleClearances.TryGetValue(frame, out float clearance))
                            runClearance = Mathf.Max(runClearance, clearance);
                    }
                }
                // Keep the stabilized running poses at one height throughout the stride.
                if (runClearance > 0f)
                    for (int row = 0; row < AtlasRows; row++)
                    for (int column = 1 + FramesPerCycle; column < AtlasColumns; column++)
                        _soleClearances[_retailFrames[row * AtlasColumns + column]] = runClearance;

                if (_attackAtlas)
                {
                    _attackFrames = new Sprite[15];
                    for (int row = 0; row < AtlasRows; row++)
                    for (int column = 0; column < 3; column++)
                    {
                        int index = row * 3 + column;
                        _attackFrames[index] = CreateRetailBodyFrame(_attackAtlas, row, column, Vector2Int.zero, _grounding?.attack, index);
                    }
                }
                if (_liftAtlas && _liftAtlas.width == LiftColumns * CellSize && _liftAtlas.height == AtlasRows * CellSize)
                {
                    _liftFrames = new Sprite[AtlasRows * LiftColumns];
                    for (int row = 0; row < AtlasRows; row++)
                    for (int column = 0; column < LiftColumns; column++)
                    {
                        if (column is >= 12 and < PushColumn) continue;
                        int index = row * LiftColumns + column;
                        _liftFrames[index] = CreateRetailBodyFrame(_liftAtlas, row, column, Vector2Int.zero, _grounding?.lift, index);
                    }
                }
                _renderer.sprite = _retailFrames[0];
                return;
            }
            if (!_sheet || !_material) { enabled = false; return; }
            _renderer.sharedMaterial = _material;
            var textureBinding = new MaterialPropertyBlock();
            textureBinding.SetTexture("_MainTex", _sheet);
            _renderer.SetPropertyBlock(textureBinding);
            _frames = new Sprite[8];
            for (int i = 0; i < _frames.Length; i++)
            {
                _frames[i] = Sprite.Create(_sheet, new Rect(i * 96, 0, 96, 96), new Vector2(0.52f, 10f / 96f), 48f, 0, SpriteMeshType.FullRect);
                _frames[i].name = "Koh walk " + i;
            }
            _renderer.sprite = _frames[0];
        }
        internal void PresentStatusTint(bool paralyzed, bool greenFlash)
        {
            if (!_renderer) return;
            if (paralyzed || greenFlash)
            {
                Color normal = _colorBeforeStatusTint ??= _renderer.color;
                float phase = Mathf.Repeat(Time.unscaledTime * 4f, 1f);
                _renderer.color = paralyzed ? normal * TowerStatusColors.Paralysis :
                    phase < 0.5f ? normal * new Color(0.45f, 1f, 0.42f, 1f) : normal;
            }
            else if (_colorBeforeStatusTint is { } original)
            {
                _renderer.color = original;
                _colorBeforeStatusTint = null;
            }
        }

        private SpriteRenderer _rightWeapon, _leftShield;
        private Texture2D _equipmentAtlas;
        private Sprite[,] _equipmentFrames;
        private Sprite _weaponIcon, _shieldIcon;
        private int _weaponRow = -1, _shieldRow = -1;
        private bool _equippedWeaponIsSword;

        internal void SetEquipment(byte weaponKind, byte weaponSubtype, Sprite weaponIcon, byte shieldSubtype, Sprite shieldIcon)
        {
            _weaponIcon = weaponIcon;
            _shieldIcon = shieldIcon;
            _equippedWeaponIsSword = weaponKind == ItemKind.Sword;
            _weaponRow = (weaponKind, weaponSubtype) switch
            {
                (ItemKind.Sword, >= 1 and <= EquipmentSwords) => weaponSubtype - 1,
                (ItemKind.Wand, >= 1 and <= EquipmentWands) => EquipmentSwords + weaponSubtype - 1,
                _ => -1
            };
            _shieldRow = shieldSubtype is >= 1 and <= EquipmentShields
                ? EquipmentSwords + EquipmentWands + shieldSubtype - 1 : -1;
        }

        private Sprite EquipmentFrame(int row, int facingRow)
        {
            if (row < 0 || !_equipmentAtlas || _equipmentAtlas.width != AtlasRows * CellSize ||
                _equipmentAtlas.height != EquipmentRows * CellSize) return null;
            _equipmentFrames ??= new Sprite[EquipmentRows, AtlasRows];
            return _equipmentFrames[row, facingRow] ??= Sprite.Create(_equipmentAtlas,
                new Rect(facingRow * CellSize, (EquipmentRows - 1 - row) * CellSize, CellSize, CellSize),
                new Vector2(0.5f, 11f / CellSize), 48f, 0, SpriteMeshType.FullRect);
        }

        private void SetHandSprite(ref SpriteRenderer hand, Sprite sprite, string name)
        {
            if (!hand)
            {
                if (!sprite) return;
                var view = new GameObject(name);
                view.transform.SetParent(_renderer.transform, false);
                hand = view.AddComponent<SpriteRenderer>();
                hand.sortingLayerID = _renderer.sortingLayerID;
            }
            if (hand.sprite != sprite) hand.sprite = sprite;
        }

        private void PresentEquipment(Vector3 facing, int octant, bool walking, Camera camera)
        {
            var right = camera ? _renderer.transform.right : Vector3.right;
            var forward = camera ? Vector3.Cross(right, Vector3.up) : Vector3.forward;
            float side = Vector3.Dot(facing, right);
            float depth = Vector3.Dot(facing, forward);
            float stride = walking ? Mathf.Sin(_clock * (2f * Mathf.PI / FramesPerCycle)) : 0f;
            float swing = _attackProgress < 0f ? 0f : Mathf.Sin(_attackProgress * Mathf.PI);
            bool handsFree = !_sleeping && !_bagPose && _pushEffort == 0f;
            int facingRow = AtlasRowByOctant[octant];
            Sprite weaponFrame = EquipmentFrame(_weaponRow, facingRow);
            Sprite shieldFrame = EquipmentFrame(_shieldRow, facingRow);
            SetHandSprite(ref _rightWeapon, weaponFrame ? weaponFrame : _weaponIcon, "Koh right-hand weapon");
            SetHandSprite(ref _leftShield, shieldFrame ? shieldFrame : _shieldIcon, "Koh left-arm shield");
            PositionHand(_rightWeapon, false, weaponFrame, 0.255f, 0.25f, 0.34f, handsFree, swing);
            PositionHand(_leftShield, true, shieldFrame, 0.195f, 0.22f, 0.39f, handsFree && !_lifting, 0f);

            void PositionHand(SpriteRenderer hand, bool shield, bool retail, float size, float width, float height,
                bool visible, float attack)
            {
                if (!hand) return;
                hand.enabled = visible && hand.sprite;
                if (!hand.enabled) return;
                hand.color = _renderer.color;
                float anatomicalSide = shield ? 1f : -1f;
                if (retail)
                {
                    // Each frame already includes the original hand offset relative to Koh's feet.
                    hand.transform.localScale = Vector3.one;
                    hand.transform.localPosition = new Vector3(side * (anatomicalSide * stride * 0.075f + attack * 0.23f),
                        anatomicalSide * stride * 0.045f + attack * 0.16f, 0f);
                    hand.transform.localRotation = Quaternion.identity;
                    hand.sortingOrder = _renderer.sortingOrder + (shield ? 2 : facingRow >= 2 ? -1 : 1);
                    hand.flipX = octant is >= 1 and <= 3;
                    return;
                }

                hand.transform.localScale = Vector3.one * (size / Mathf.Max(hand.sprite.bounds.size.x, hand.sprite.bounds.size.y));
                bool farHand = side * anatomicalSide > 0.25f;
                float x = -depth * anatomicalSide * width
                    + side * (0.08f + anatomicalSide * stride * 0.075f + attack * 0.23f);
                float y = height + anatomicalSide * stride * 0.045f + attack * 0.16f;
                hand.transform.localPosition = new Vector3(x, y, 0f);
                float yaw = shield ? 75f * depth : 0f;
                hand.transform.localRotation = Quaternion.Euler(0f, yaw, -side * attack * 65f);
                hand.sortingOrder = _renderer.sortingOrder + (farHand ? -1 : 1);
                bool sideProfile = Mathf.Abs(side) >= 0.92388f;
                hand.flipX = !shield && ((side < -0.25f) ^ (_equippedWeaponIsSword && sideProfile));
            }
        }

        private void OnDisable() => PresentStatusTint(false, false);

        private void LateUpdate()
        {
            if (!_renderer) return;
            if (!_locomotion) _locomotion = GetComponent<ActorLocomotionView>();
            bool walking = !_sleeping && !_paralyzed && _locomotion && _locomotion.IsMoving;
            bool running = _locomotion && _locomotion.IsRunningNow;
            AdvanceWalkClock();
            var camera = _gameplayCamera;
            var facing = new Vector3(_facing.x, 0f, _facing.y);
            float angle = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
            var visual = _renderer.transform;
            float idleBob = !_sleeping && !_paralyzed && !walking && !_airborne && !_lifting && _pushEffort == 0f && _attackProgress < 0f
                ? (1f - Mathf.Cos(Time.time * PlaybackSpeed * 4f)) * 0.015f : 0f;
            if (camera)
            {
                angle -= camera.transform.eulerAngles.y;
                float lean = -12f * _pushEffort * Vector3.Dot(facing.normalized, camera.transform.right);
                // Keep the sprite above its feet so the camera pitch cannot push its head behind a nearby wall.
                var billboard = Quaternion.Euler(0f, camera.transform.eulerAngles.y, 0f);
                visual.rotation = billboard * Quaternion.Euler(0f, 0f, lean);
            }
            int octant = Mathf.RoundToInt(angle / 45f) & 7;
            float jump = 0f;
            if (_levelUpProgress >= 0f)
            {
                int step = Mathf.Min(LevelUpDirections.Length - 1, (int)(_levelUpProgress * LevelUpDirections.Length));
                octant = LevelUpDirections[step];
                float heading = (octant * 45f + (camera ? camera.transform.eulerAngles.y : 0f)) * Mathf.Deg2Rad;
                facing = new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading));
                walking = running = false;
                idleBob = 0f;
                if (_levelUpProgress < 0.9f)
                    jump = Mathf.Abs(Mathf.Sin(_levelUpProgress / 0.9f * Mathf.PI * 3f)) * 0.26f;
            }
            PresentBody(octant, walking, running);
            var poseOffset = facing.normalized * (_pushEffort * 0.14f) + Vector3.up * (idleBob + jump);
            var offset = visual.parent.InverseTransformVector(poseOffset);
            if (_renderer.sprite && _soleClearances.TryGetValue(_renderer.sprite, out float clearance))
                offset.y += clearance * _visualUnitsPerPixel;
            visual.localPosition = (_baseVisualPosition ??= visual.localPosition) + offset;
            PresentEquipment(facing.normalized, octant, walking, camera);
        }

        private void PresentBody(int octant, bool walking, bool running)
        {
            if (_sleeping && _sleepFrames != null)
            {
                int tick = Mathf.FloorToInt((Time.unscaledTime - _sleepStartedAt) * 30f);
                int direction = octant & 7;
                int index = AtlasRowByOctant[direction] * SleepColumns + SleepFrameAt(tick);
                if ((uint)index < (uint)_sleepFrames.Length)
                {
                    _renderer.flipX = direction is >= 1 and <= 3;
                    _renderer.sprite = _sleepFrames[index];
                }
                return;
            }
            float hit = _locomotion ? _locomotion.HitProgress : 1f;
            if (hit < 1f)
            {
                if (!_hitFramesRequested) LoadHitFrames();
                if (_hitFrames != null)
                {
                    int row = AtlasRowByOctant[octant & 7];
                    // The first pose anticipates the impact and the last returns to idle.
                    int column = 1 + Mathf.Min(HitColumns - 3, (int)(hit * (HitColumns - 2)));
                    _renderer.flipX = octant is >= 1 and <= 3;
                    _renderer.sprite = _hitFrames[row * HitColumns + column];
                    return;
                }
            }
            if (_levelUpProgress >= 0f && _retailFrames != null)
            {
                int pose = _levelUpProgress >= 0.9f ? 0 : 1 + (int)(_levelUpProgress * 24f) % FramesPerCycle;
                _renderer.flipX = octant is >= 1 and <= 3;
                _renderer.sprite = _retailFrames[AtlasRowByOctant[octant] * AtlasColumns + pose];
                return;
            }
            if (_bagPose && _retailFrames != null)
            {
                _renderer.sprite = _bagPose;
                _renderer.flipX = _bagPoseFlip;
                return;
            }
            if (_retailFrames != null)
            {
                int column = _pushEffort > 0f ? 1 + Mathf.Min(FramesPerCycle / 2, (int)(_pushEffort * FramesPerCycle)) :
                    walking ? 1 + (running ? FramesPerCycle : 0) + (int)_clock : 0;
                _renderer.flipX = octant >= 1 && octant <= 3;
                if (_lifting && _liftFrames != null)
                {
                    float lift = _paralyzed ? 1f : LiftProgress;
                    int columnWhileLifting = lift < 1f ? (int)(lift * HeldIdleColumn) :
                        walking ? HeldWalkColumn + HeldWalkStride : HeldIdleColumn;
                    _renderer.sprite = _liftFrames[AtlasRowByOctant[octant] * LiftColumns + columnWhileLifting];
                    return;
                }
                if (_pushEffort > 0f && _liftFrames != null)
                {
                    int pushFrame = PushColumn + (_pushEffort < 0.25f ? 0 : _pushEffort < 0.5f ? 1 : 2);
                    _renderer.sprite = _liftFrames[AtlasRowByOctant[octant] * LiftColumns + pushFrame];
                    return;
                }
                _renderer.sprite = _attackFrames != null && _attackProgress >= 0f
                    ? _attackFrames[AtlasRowByOctant[octant] * 3 + Mathf.Min(2, Mathf.FloorToInt(_attackProgress * 3f))]
                    : _retailFrames[AtlasRowByOctant[octant] * AtlasColumns + column];
                return;
            }
            var cycle = _directionCycles != null && octant < _directionCycles.Length ? _directionCycles[octant] : null;
            var walk = cycle?.walk;
            Sprite sprite = walking && walk != null && walk.Length > 0 ? walk[(int)_clock % walk.Length] : cycle?.idle;
            _renderer.flipX = !sprite && octant >= 5;
            _renderer.sprite = sprite ? sprite : _frames[walking ? (int)_clock % _frames.Length : 0];
        }
        private void LoadHitFrames()
        {
            _hitFramesRequested = true;
            var asset = Resources.Load<TextAsset>("Tower/koh-retail-hit");
            if (!asset) return;

            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            if (!atlas.LoadImage(asset.bytes, markNonReadable: true) ||
                atlas.width != HitColumns * CellSize || atlas.height != AtlasRows * CellSize)
            {
                Destroy(atlas);
                return;
            }

            _hitAtlas = atlas;
            _hitFrames = new Sprite[AtlasRows * HitColumns];
            for (int row = 0; row < AtlasRows; row++)
            for (int column = 0; column < HitColumns; column++)
            {
                int index = row * HitColumns + column;
                _hitFrames[index] = CreateRetailBodyFrame(atlas, row, column, Vector2Int.zero, null, index);
            }
        }

        private void OnDestroy()
        {
            if (_hitFrames != null)
                foreach (var frame in _hitFrames) if (frame) Destroy(frame);
            if (_hitAtlas) Destroy(_hitAtlas);
            if (_equipmentFrames != null)
                foreach (var frame in _equipmentFrames) if (frame) Destroy(frame);
            if (_sleepFrames != null)
                foreach (var frame in _sleepFrames) if (frame) Destroy(frame);
            if (_sleepAtlas) Destroy(_sleepAtlas);
            if (_liftFrames != null)
                foreach (var frame in _liftFrames) if (frame) Destroy(frame);
            if (_attackFrames != null)
                foreach (var frame in _attackFrames) if (frame) Destroy(frame);
            var frames = _retailFrames ?? _frames;
            if (frames == null) return;
            foreach (var frame in frames) if (frame) Destroy(frame);
        }
    }
}
