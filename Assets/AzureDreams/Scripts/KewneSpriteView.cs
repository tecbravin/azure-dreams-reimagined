using UnityEngine;

namespace AzureDreamsReimagined
{
    [DefaultExecutionOrder(10)]
    public sealed class KewneSpriteView : MonoBehaviour
    {
        internal float PlaybackSpeed { get; set; } = 1f;
        private float AnimationTime => Time.time * PlaybackSpeed;
        private static readonly int[] Rows = { 4, 3, 2, 1, 0, 1, 2, 3 };
        // Register the side cycle on Kewne's body so the tail swing does not shift the sprite.
        private static readonly int[] SideBodyPivotPixels = { 27, 27, 26, 23, 29, 27, 25, 25 };
        [SerializeField] private Texture2D _atlas;
        [SerializeField] private Texture2D _actionsAtlas;
        [SerializeField] private Texture2D _retailActionsAtlas;
        private Texture2D _frameAtlas, _actionFrameAtlas, _retailFrameAtlas;
        private Sprite[] _retailActions;
        private float _castProgress = 1f;
        private bool _sleeping, _exhausted;

        internal void PresentCast(Vector2 direction, float progress)
        {
            Face(direction);
            _castProgress = progress;
        }

        internal bool PresentSleep(bool sleeping, bool exhausted)
        {
            if (_retailActions == null) return false;
            _sleeping = sleeping;
            _exhausted = exhausted;
            return true;
        }
        private Sprite[] _frames;
        private Sprite[] _actions;
        private bool _townFlight;

        internal void SetTownFlight() => _townFlight = true;
        private SpriteRenderer _renderer;
        private ActorLocomotionView _locomotion;
        private Vector3 _facing = Vector3.back;
        private Vector3 _previousPosition;
        private float _phase;
        private bool _carried;
        private Transform _visual;
        private float _attackProgress = -1f;
        private int _attackHeightOffset;

        public void PresentAttack(Vector2 direction, float progress, int targetHeightOffset = 0)
        {
            Face(direction);
            _attackHeightOffset = targetHeightOffset;
            _attackProgress = progress >= 1f ? -1f : Mathf.Clamp01(progress);
        }
        public void SetCarried(bool carried)
        {
            _carried = carried;
            _phase = 0f;
            _previousPosition = transform.position;
        }

        public void Face(Vector2 direction)
        {
            if (direction.sqrMagnitude > 0.0001f) _facing = new Vector3(direction.x, 0f, direction.y);
        }

        private void Awake()
        {
            if (!_atlas || _atlas.width != 512 || _atlas.height != 320) return;
            _previousPosition = transform.position;
            var visual = new GameObject("Kewne Sprite");
            visual.transform.SetParent(transform, false);
            _visual = visual.transform;
            visual.transform.localPosition = Vector3.down * 0.47f;
            _renderer = visual.AddComponent<SpriteRenderer>();
        }

        private void Start()
        {
            if (_frames == null) SetPalette(2, MonsterPaletteVariant.Enemy);
        }

        internal void SetPalette(int species, MonsterPaletteVariant variant)
        {
            BuildFrames(MonsterPalettes.Recolor(_atlas, species, variant), MonsterPalettes.Recolor(_actionsAtlas, species, variant));
            if (!_renderer || !_retailActionsAtlas || _retailActionsAtlas.width != 1920 || _retailActionsAtlas.height != 480) return;
            var atlas = MonsterPalettes.Recolor(_retailActionsAtlas, species, variant, indexed: true);
            if (_retailFrameAtlas == atlas) return;
            ClearRetailFrames();
            _retailFrameAtlas = atlas;
            _retailActions = new Sprite[100];
            for (int row = 0; row < 5; row++)
                for (int i = 0; i < 20; i++)
                    _retailActions[row * 20 + i] = Sprite.Create(atlas,
                        new Rect(i * 96, (4 - row) * 96, 96, 96),
                        new Vector2(0.5f, 0.125f), 54f, 0, SpriteMeshType.FullRect);
        }

        private void BuildFrames(Texture2D atlas, Texture2D actions)
        {
            if (!_renderer || (_frameAtlas == atlas && _actionFrameAtlas == actions)) return;
            ClearFrames();
            _frameAtlas = atlas;
            _actionFrameAtlas = actions;
            _frames = new Sprite[40];
            for (int row = 0; row < 5; row++)
                for (int i = 0; i < 8; i++)
                {
                    float pivotX = row == 2 ? SideBodyPivotPixels[i] / 64f : 0.5f;
                    _frames[row * 8 + i] = Sprite.Create(atlas, new Rect(i * 64, (4 - row) * 64, 64, 64),
                        new Vector2(pivotX, 0.125f), 54f, 0, SpriteMeshType.FullRect);
                }
            _renderer.sprite = _frames[0];
            if (!actions || actions.width != 576 || actions.height != 320) return;
            _actions = new Sprite[45];
            for (int row = 0; row < 5; row++)
                for (int i = 0; i < 9; i++)
                    _actions[row * 9 + i] = Sprite.Create(actions, new Rect(i * 64, (4 - row) * 64, 64, 64),
                        new Vector2(0.5f, 0.125f), 54f, 0, SpriteMeshType.FullRect);
        }

        private void LateUpdate()
        {
            if (!_renderer || _frames == null) return;
            if (!_locomotion) _locomotion = GetComponent<ActorLocomotionView>();
            Vector3 delta = transform.position - _previousPosition;
            _previousPosition = transform.position;
            delta.y = 0f;
            bool moving = !_carried && (_townFlight || (_locomotion && _locomotion.IsMoving)) && delta.sqrMagnitude > 0.000001f;
            if (moving && !_townFlight && _attackProgress < 0f) _facing = delta;
            _phase = moving ? (_phase + delta.magnitude * 8f) % 8f : 0f;
            var camera = Camera.main;
            float angle = Mathf.Atan2(_facing.x, _facing.z) * Mathf.Rad2Deg;
            if (camera)
            {
                _renderer.transform.rotation = camera.transform.rotation;
                angle -= camera.transform.eulerAngles.y;
            }
            int octant = Mathf.RoundToInt(angle / 45f) & 7;
            _renderer.flipX = octant >= 1 && octant <= 3;
            float lunge = !_carried && _attackProgress >= 0f ? Mathf.Sin(_attackProgress * Mathf.PI) : 0f;
            float idleBob = !moving && !_carried && _attackProgress < 0f
                ? (1f - Mathf.Cos(AnimationTime * 4f)) * 0.022f : 0f;
            // Retail switches uphill at +32 and downhill below -16.
            int heightAttackColumn = _attackHeightOffset >= 32 ? 0 : _attackHeightOffset < -16 ? 6 : -1;
            float attackRise = heightAttackColumn < 0 ? 0f : Mathf.Clamp(_attackHeightOffset, -64, 64) / 128f;
            if (_townFlight)
                _visual.position = transform.position + Vector3.up * (Mathf.Sin(AnimationTime * 6f) * 0.06f);
            else if (_visual)
                _visual.position = transform.position + Vector3.up * (-0.55f + 0.08f * _visual.localScale.y)
                    + _facing.normalized * lunge * 0.32f + Vector3.up * (lunge * (0.12f + attackRise) + idleBob)
                    + (_locomotion ? _locomotion.HitOffset : Vector3.zero);
            int row = Rows[octant];
            _renderer.color = !_townFlight && !_carried && _exhausted
                ? Color.Lerp(Color.white, new Color(0.28f, 0.55f, 1f), 0.48f + 0.45f * Mathf.Sin(AnimationTime * 12f))
                : Color.white;
            float hit = _locomotion ? _locomotion.HitProgress : 1f;
            if (!_carried && hit < 1f && _actions != null)
            {
                _renderer.sprite = _actions[row * 9 + 6 + Mathf.Min(2, (int)(hit * 3f))];
                return;
            }
            if (!_townFlight && !_carried && _retailActions != null)
            {
                if (_sleeping)
                {
                    _renderer.sprite = _retailActions[row * 20 + 18 + (Mathf.FloorToInt(AnimationTime * 2f) & 1)];
                    return;
                }
                if (_castProgress < 1f)
                {
                    _renderer.sprite = _retailActions[row * 20 + 12 + Mathf.Clamp((int)(_castProgress * 6f), 0, 5)];
                    return;
                }
            }
            if (!_townFlight && !_carried && _attackProgress >= 0f && heightAttackColumn >= 0 && _retailActions != null)
            {
                _renderer.sprite = _retailActions[row * 20 + heightAttackColumn + Mathf.Min(5, (int)(_attackProgress * 6f))];
                return;
            }
            if (!_carried && _attackProgress >= 0f && _actions != null)
            {
                _renderer.sprite = _actions[row * 9 + Mathf.Min(5, (int)(_attackProgress * 6f))];
                return;
            }
            int frame = _townFlight ? (int)(AnimationTime * (moving ? 12f : 8f)) % 8 :
                _attackProgress >= 0f ? Mathf.Min(7, (int)(_attackProgress * 8f)) : (int)_phase;
            _renderer.sprite = _frames[row * 8 + frame];
        }

        private void OnDestroy()
        {
            ClearFrames();
            ClearRetailFrames();
        }

        private void ClearRetailFrames()
        {
            if (_retailActions == null) return;
            foreach (var sprite in _retailActions) Destroy(sprite);
            _retailActions = null;
        }

        private void ClearFrames()
        {
            if (_actions != null)
                foreach (var sprite in _actions) if (sprite) Destroy(sprite);
            _actions = null;
            if (_frames == null) return;
            foreach (var sprite in _frames) if (sprite) Destroy(sprite);
            _frames = null;
        }
    }
}
