using UnityEngine;

namespace AzureDreamsReimagined
{
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MonsterTurnSpriteView : MonoBehaviour
    {
        internal float PlaybackSpeed { get; set; } = 1f;
        internal bool GroundedIdle { get; set; }
        private float AnimationTime => Time.time * PlaybackSpeed;
        private const int CellSize = 64;
        private const int IndexedCellSize = 96;
        private const int Rows = 5;
        private const int WalkStart = 2;
        private const int AttackStart = 7;
        private const int CastStart = 12;
        private const int SleepStart = 14;
        private const int IndexedSleepStart = 18;
        private const int IndexedHitColumn = 20;
        private ActorLocomotionView _locomotion;
        [SerializeField] private Texture2D _atlas;
        [SerializeField] private int _indexedSpecies;
        [SerializeField, Min(0f)] private float _attackLunge;
        private Texture2D _frameAtlas;
        private SpriteRenderer _renderer;
        private Sprite[] _frames;
        private int _columns;
        private bool UsesRetailLayout => _atlas &&
            (_atlas.width == IndexedHitColumn * IndexedCellSize || _atlas.width == (IndexedHitColumn + 1) * IndexedCellSize);
        private int FrameSize => UsesRetailLayout ? IndexedCellSize : CellSize;
        private int SleepColumn => UsesRetailLayout ? IndexedSleepStart : SleepStart;
        private Vector2 _direction = Vector2.down;
        private bool _walking;
        private float _walkProgress;
        private bool _casting;
        private float _progress = 1f;
        private Vector3 _previousLunge;
        private bool _sleeping, _exhausted, _tinted;
        private Color _awakeColor;
        private float _sleepStartedAt;

        private void Awake()
        {
            if (!_atlas || _atlas.height != Rows * FrameSize || _atlas.width % FrameSize != 0) return;
            _columns = _atlas.width / FrameSize;
            if (!UsesRetailLayout && _columns != CastStart && _columns != SleepStart && _columns != SleepStart + 2) return;
            _renderer = GetComponent<SpriteRenderer>();
            var position = transform.localPosition;
            position.y = -0.55f;
            transform.localPosition = position;
        }

        private void Start()
        {
            if (_frames != null) return;
            if (_indexedSpecies != 0) SetPalette(_indexedSpecies, MonsterPaletteVariant.Enemy);
            else BuildFrames(_atlas);
        }

        internal void SetActionAtlas(Texture2D atlas)
        {
            if (_atlas && atlas.width == _atlas.width && atlas.height == _atlas.height) _atlas = atlas;
        }

        internal void SetPalette(int species, MonsterPaletteVariant variant) =>
            BuildFrames(MonsterPalettes.Recolor(_atlas, _indexedSpecies == 0 ? species : _indexedSpecies, variant,
                indexed: _indexedSpecies != 0));

        private void BuildFrames(Texture2D atlas)
        {
            if (!_renderer || _frameAtlas == atlas) return;
            ClearFrames();
            _frameAtlas = atlas;
            int size = FrameSize;
            int pivotPixels = UsesRetailLayout ? 12 : 4;
            if (GroundedIdle && _atlas.isReadable) pivotPixels = GroundedIdlePivot(size, pivotPixels);
            float pivotY = (float)pivotPixels / size;
            _frames = new Sprite[Rows * _columns];
            for (int row = 0; row < Rows; row++)
                for (int column = 0; column < _columns; column++)
                    _frames[row * _columns + column] = Sprite.Create(atlas,
                        new Rect(column * size, (Rows - 1 - row) * size, size, size),
                        new Vector2(0.5f, pivotY), 64f, 0, SpriteMeshType.FullRect);
            _renderer.sprite = _frames[0];
        }

        private int GroundedIdlePivot(int size, int pivotPixels)
        {
            // Read the source alpha, which is shared by all genus palettes.
            // One pivot for all idle poses preserves their relative bobbing.
            for (int y = 0; y < pivotPixels; y++)
                for (int row = 0; row < Rows; row++)
                    for (int x = 0; x < 2 * size; x++)
                        if (_atlas.GetPixel(x, row * size + y).a > 0f) return y;
            return pivotPixels;
        }

        internal void PresentMovement(Vector3 direction, float progress, bool walking)
        {
            _walking = walking;
            _walkProgress = progress;
            if (walking && _progress >= 1f && direction.sqrMagnitude > 0.0001f)
                _direction = new Vector2(direction.x, direction.z);
        }

        internal void Face(Vector2Int direction) => _direction = direction;

        internal void BeginAction(Vector2Int direction, bool casting)
        {
            _direction = direction;
            _casting = casting;
            _progress = 0f;
        }

        internal void PresentAction(float progress) => _progress = progress;

        internal bool PresentSleep(bool sleeping, bool exhausted)
        {
            if (_frames == null || _columns <= SleepColumn) return false;
            if (sleeping && !_sleeping) _sleepStartedAt = AnimationTime;
            _sleeping = sleeping;
            _exhausted = exhausted;
            return true;
        }

        private void LateUpdate()
        {
            if (!_renderer || _frames == null) return;
            var camera = Camera.main;
            float angle = Mathf.Atan2(_direction.x, _direction.y) * Mathf.Rad2Deg;
            if (camera) angle -= camera.transform.eulerAngles.y;
            int octant = Mathf.RoundToInt(angle / 45f) & 7;
            int row = Mathf.Abs(4 - octant);
            _renderer.flipX = octant is >= 1 and <= 3;
            float sleepTime = AnimationTime - _sleepStartedAt;
            if (_exhausted && !_tinted)
            {
                _awakeColor = _renderer.color;
                _tinted = true;
            }
            if (_exhausted)
                _renderer.color = Color.Lerp(_awakeColor, new Color(0.28f, 0.55f, 1f, _awakeColor.a),
                    0.48f + 0.45f * Mathf.Sin(sleepTime * 12f));
            else if (_tinted)
            {
                _renderer.color = _awakeColor;
                _tinted = false;
            }
            if (!_locomotion) _locomotion = GetComponentInParent<ActorLocomotionView>();
            bool hit = _locomotion && _locomotion.HitProgress < 1f;
            int column = Mathf.FloorToInt(AnimationTime * 4f) & 1;
            if (hit && UsesRetailLayout && _columns > IndexedHitColumn)
                column = IndexedHitColumn;
            else if (_sleeping)
                column = SleepColumn + (Mathf.FloorToInt(sleepTime * 2f) & 1);
            else if (!hit && _progress < 1f)
                column = _casting && _columns > CastStart
                    ? CastColumn()
                    : AttackStart + Mathf.Clamp((int)(_progress * 5f), 0, 4);
            else if (!hit && _walking)
                column = WalkStart + Mathf.Clamp((int)(_walkProgress * 5f), 0, 4);
            _renderer.sprite = _frames[row * _columns + column];

            if (_attackLunge > 0f || UsesRetailLayout)
            {
                float distance = hit || _sleeping || _casting || _progress >= 1f ? 0f : _attackLunge * Mathf.Sin(_progress * Mathf.PI);
                Vector3 worldOffset = new Vector3(_direction.x, 0f, _direction.y).normalized * distance;
                // Some retail directions hold one pose. Keep the body alive during those holds.
                if (UsesRetailLayout)
                {
                    if (_sleeping) worldOffset.y = (1f - Mathf.Cos(sleepTime * 4f)) * 0.009f;
                    else if (_casting && _progress < 1f) worldOffset.y = Mathf.Sin(_progress * Mathf.PI) * 0.09f;
                    else if (!_walking && _progress >= 1f && _indexedSpecies == 28)
                        worldOffset.y = (1f - Mathf.Cos(AnimationTime * 4f)) * 0.012f;
                }
                Vector3 localOffset = transform.parent.InverseTransformVector(worldOffset);
                transform.localPosition += localOffset - _previousLunge;
                _previousLunge = localOffset;
            }
        }

        private int CastColumn()
        {
            if (!UsesRetailLayout) return CastStart + (_progress < 0.23f ? 0 : 1);
            int frames = IndexedSleepStart - CastStart;
            return CastStart + Mathf.Clamp((int)(_progress * frames), 0, frames - 1);
        }

        private void OnDestroy() => ClearFrames();

        private void ClearFrames()
        {
            if (_frames == null) return;
            foreach (var sprite in _frames) Destroy(sprite);
            _frames = null;
        }
    }
}
