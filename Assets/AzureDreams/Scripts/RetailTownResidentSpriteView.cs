using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AzureDreamsReimagined
{
    /// <summary>Assembled retail field sprites with their original timed sequences.</summary>
    [DefaultExecutionOrder(10), DisallowMultipleComponent]
    public sealed class RetailTownResidentSpriteView : MonoBehaviour
    {
        [Serializable] private sealed class Sequence
        {
            public int[] frames;
            public int[] ticks;
            public bool loop;
        }

        [Serializable] private sealed class Clip
        {
            public string key;
            public Sequence[] directions;
        }

        [Serializable] private sealed class Bank
        {
            public int cellSize, columns, frameCount;
            public float pivotX, pivotY, ticksPerSecond, groundingPixels;
            public Clip[] clips;
        }

        [SerializeField] private Texture2D _atlas;
        [SerializeField] private TextAsset _animationData;
        // Retail field cels are 1 px = 1/48 unit, but SpawnActor enlarges Koh's sprite by 1.8x
        // (KohVisualScale). Residents use the same on-screen pixel size by lowering the
        // pixels-per-unit instead of scaling or resampling the texture: every cel pixel stays square.
        internal const float KohVisualScale = 1.8f;
        internal const float TownPixelsPerUnit = 48f / KohVisualScale;
        [SerializeField, Min(1f)] private float _pixelsPerUnit = TownPixelsPerUnit;
        private Sprite[] _frames;
        private Bank _bank;
        private Clip _clip;
        private SpriteRenderer _renderer;
        private Camera _camera;
        private double _ticks;
        private bool _moving, _explicitClip;
        private Vector3 _lastPosition;
        internal bool UsesAtlas(Texture2D atlas) => _atlas == atlas && _frames != null;

        public void Configure(Texture2D atlas, TextAsset animations, float pixelsPerUnit = TownPixelsPerUnit)
        {
            // A resident can be reconfigured (e.g. a town bank replaced by a scene-specific
            // minigame bank in the casino or gym): drop the previous frames and visual.
            if (_frames != null)
            {
                foreach (var frame in _frames)
                    if (frame) Destroy(frame);
                if (_renderer) Destroy(_renderer.gameObject);
                _frames = null;
                _renderer = null;
                _clip = null;
                _bank = null;
            }
            _atlas = atlas;
            _animationData = animations;
            _pixelsPerUnit = pixelsPerUnit;
            Awake();
        }

        private void Awake()
        {
            if (_frames != null) return;
            if (!_atlas || !_animationData) return;
            _bank = JsonUtility.FromJson<Bank>(_animationData.text);
            if (!ValidBank())
            {
                Debug.LogError("Invalid retail town sprite animation bank.", this);
                return;
            }
            _frames = new Sprite[_bank.frameCount];
            for (int i = 0; i < _frames.Length; i++)
            {
                var rectangle = new Rect((i % _bank.columns) * _bank.cellSize,
                    _atlas.height - (i / _bank.columns + 1) * _bank.cellSize,
                    _bank.cellSize, _bank.cellSize);
                // Align the lowest standing/walking foot pixel with the actor's floor anchor.
                // One constant per bank preserves the original animation's bounce and scale.
                var pivot = new Vector2(_bank.pivotX, _bank.pivotY - _bank.groundingPixels / _bank.cellSize);
                _frames[i] = Sprite.Create(_atlas, rectangle, pivot,
                    _pixelsPerUnit, 0, SpriteMeshType.FullRect);
                _frames[i].name = name + " retail frame " + i;
            }
            var visual = new GameObject("Retail field sprite");
            visual.transform.SetParent(transform, false);
            _renderer = visual.AddComponent<SpriteRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _lastPosition = transform.position;
            if (!SelectClip("idle"))
            {
                Debug.LogError("Retail town sprite bank has no idle sequence.", this);
                return;
            }
            PresentFrame(_clip.directions.Length == 8 ? 2 : 1);
        }

        private bool ValidBank()
        {
            if (_bank == null || _bank.cellSize <= 0 || _bank.columns <= 0 || _bank.frameCount <= 0 ||
                _bank.ticksPerSecond <= 0 || _bank.clips == null || _bank.clips.Length == 0 ||
                _atlas.width < _bank.columns * _bank.cellSize ||
                _atlas.height < ((_bank.frameCount + _bank.columns - 1) / _bank.columns) * _bank.cellSize)
                return false;
            foreach (var clip in _bank.clips)
            {
                if (clip.directions == null || (clip.directions.Length != 4 && clip.directions.Length != 8)) return false;
                foreach (var sequence in clip.directions)
                {
                    if (sequence.frames == null || sequence.ticks == null || sequence.frames.Length == 0 ||
                        sequence.frames.Length != sequence.ticks.Length) return false;
                    for (int i = 0; i < sequence.frames.Length; i++)
                        if (sequence.frames[i] < 0 || sequence.frames[i] >= _bank.frameCount || sequence.ticks[i] <= 0)
                            return false;
                }
            }
            return true;
        }

        internal bool ShowFrontPose { get; set; }

        internal void Present(Vector3 direction, bool moving)
        {
            _moving = moving;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            // Choose the clip once in LateUpdate. NPC patrol can report idle
            // before a story coroutine reports movement in the same frame.
            // Selecting here would restart the walking sequence every frame.
        }

        /// <summary>Plays an extracted clip; loop/hold and per-frame duration come from the disc.</summary>
        public bool PlayClip(string key)
        {
            if (!SelectClip(key)) return false;
            _explicitClip = true;
            _ticks = 0;
            return true;
        }

        public void ResumeLocomotion()
        {
            _explicitClip = false;
            SelectClip(_moving ? "walk" : "idle");
        }

        private bool SelectClip(string key)
        {
            if (_bank?.clips == null) return false;
            if (_clip?.key == key) return true;
            foreach (var clip in _bank.clips)
                if (clip.key == key)
                {
                    _clip = clip;
                    _ticks = 0;
                    return true;
                }
            return false;
        }

        private bool _poolSwimming;
        private bool _seated;
        internal void SetSeated(bool seated)
        {
            _seated=seated;
            if(!_renderer)return;
            _renderer.transform.localScale = seated ? new Vector3(1f, .78f, 1f) : Vector3.one;
        }
        private static readonly int PoolWaterHeightId = Shader.PropertyToID("_PoolWaterHeight");
        private float _poolWaterHeight;
        private Material _poolSpriteMaterial, _drySpriteMaterial;
        private MaterialPropertyBlock _poolSpriteProperties;
        internal void PresentPoolSwimming(bool swimming, float waterHeight)
        {
            _poolSwimming = swimming;
            _poolWaterHeight = waterHeight;
        }

        private void LateUpdate()
        {
            if (!_renderer || _clip == null) return;
            UpdatePoolWaterline();
            // Scripted residents can move without a patrol/locomotion call.
            Vector3 displacement = transform.position - _lastPosition;
            _lastPosition = transform.position;
            displacement.y = 0;
            if (!_explicitClip)
            {
                bool displaced = displacement.sqrMagnitude > 0.000001f && displacement.sqrMagnitude < 1f;
                if (displaced && !_moving) transform.rotation = Quaternion.LookRotation(displacement, Vector3.up);
                SelectClip(_moving || displaced ? "walk" : "idle");
            }
            if (!_camera || !_camera.isActiveAndEnabled) _camera = Camera.main;
            int direction = _clip.directions.Length == 8 ? 2 : 1;
            if (_camera)
            {
                Vector3 forward = _camera.transform.forward;
                forward.y = 0;
                forward.Normalize();
                float side = Vector3.Dot(transform.forward, _camera.transform.right);
                float depth = Vector3.Dot(transform.forward, forward);
                if (!ShowFrontPose) direction = FieldSpriteDirection(side, depth, _clip.directions.Length);
                _renderer.transform.rotation = _camera.transform.rotation;
            }
            _ticks += Time.deltaTime * _bank.ticksPerSecond;
            PresentFrame(direction);
        }

        private void UpdatePoolWaterline()
        {
            if (!_poolSwimming)
            {
                if (_poolSpriteMaterial && _renderer.sharedMaterial == _poolSpriteMaterial)
                    _renderer.sharedMaterial = _drySpriteMaterial;
                _renderer.transform.localPosition = _seated ? Vector3.up * .06f : Vector3.zero;
                return;
            }

            // Put the waist at the surface. The shader hides the submerged
            // portion even when transparent sprite and water sorting disagree.
            const float waistHeight = .64f;
            float bob = Mathf.Sin(Time.time * 2.8f) * .02f;
            var position = transform.position;
            position.y = _poolWaterHeight - waistHeight + bob;
            _renderer.transform.position = position;
            if (!_poolSpriteMaterial)
            {
                var shader = AssetMode.Load<Shader>("Shaders/PoolResident");
                if (!shader) return;
                _poolSpriteMaterial = new Material(shader) { name = "Pool resident waterline" };
                _poolSpriteProperties = new MaterialPropertyBlock();
            }
            if (_renderer.sharedMaterial != _poolSpriteMaterial)
            {
                _drySpriteMaterial = _renderer.sharedMaterial;
                _renderer.sharedMaterial = _poolSpriteMaterial;
            }
            _renderer.GetPropertyBlock(_poolSpriteProperties);
            _poolSpriteProperties.SetFloat(PoolWaterHeightId, _poolWaterHeight);
            _renderer.SetPropertyBlock(_poolSpriteProperties);
        }

        private static int FieldSpriteDirection(float side, float depth, int directions)
        {
            if (directions == 8)
            {
                // Native eight-direction order: left, front-left, front, front-right,
                // right, back-right, back, back-left. Repeated side chains mirror.
                int octant = Mathf.RoundToInt(Mathf.Atan2(side, -depth) * Mathf.Rad2Deg / 45f) & 7;
                return (octant + 2) & 7;
            }
            return Mathf.Abs(side) > Mathf.Abs(depth) ? (side >= 0f ? 2 : 0) : (depth > 0f ? 3 : 1);
        }

        private void PresentFrame(int direction)
        {
            var sequence = _clip.directions[direction];
            int total = 0;
            foreach (int ticks in sequence.ticks) total += ticks;
            double time = sequence.loop ? _ticks % total : Math.Min(_ticks, total - 0.001);
            int entry = 0;
            while (entry + 1 < sequence.frames.Length && time >= sequence.ticks[entry])
                time -= sequence.ticks[entry++];
            _renderer.sprite = _frames[sequence.frames[entry]];
            // The source side chain faces left; the opposite direction mirrors it.
            _renderer.flipX = _clip.directions.Length == 8 ? direction is >= 3 and <= 5 : direction == 2;
        }

        private void OnDestroy()
        {
            if (_poolSpriteMaterial) Destroy(_poolSpriteMaterial);
            if (_frames == null) return;
            foreach (var frame in _frames)
                if (frame) Destroy(frame);
        }
    }
}
