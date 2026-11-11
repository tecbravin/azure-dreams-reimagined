using UnityEngine;
using AzureDreamsReimagined.Rendering;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Presentation only. The controller owns logical/root positions and the
    /// shared turn clock. Authored Animators receive locomotion parameters;
    /// static demo meshes use a small procedural walk, not retail PS1 clips.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActorLocomotionView : MonoBehaviour
    {
        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int IsWalking = Animator.StringToHash("IsWalking");
        private static readonly int IsRunning = Animator.StringToHash("IsRunning");
        private Animator _animator;
        private CameraFacingSprite[] _monsterBillboards;
        private MonsterTurnSpriteView _monsterSprite;
        private Transform _visual;
        private KohSpriteView _kohSprite;
        private KewneSpriteView _kewneSprite;
        private Vector3 _walkOffset, _hitDirection;
        private float _hitStartedAt = float.NegativeInfinity;
        internal const float HitDuration = 0.22f;
        internal float HitProgress => Mathf.Clamp01((Time.time - _hitStartedAt) / HitDuration);
        internal Vector3 HitOffset
        {
            get
            {
                float progress = HitProgress;
                if (progress >= 1f) return Vector3.zero;
                float amount = Mathf.Sin(Mathf.Sqrt(progress) * Mathf.PI);
                return (_hitDirection * 0.17f + Vector3.up * 0.035f) * amount;
            }
        }

        internal void PresentHit(Vector3 away)
        {
            away.y = 0f;
            _hitDirection = away.sqrMagnitude > 0.0001f ? away.normalized : -transform.forward;
            _hitStartedAt = Time.time;
        }
        private bool _hasSpeed, _hasWalking, _hasRunning, _billboard, _initialized;
        public bool IsMoving { get; private set; }
        public bool IsRunningNow { get; private set; }

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _billboard = GetComponentInChildren<SpriteRenderer>() != null;
            _kohSprite = GetComponent<KohSpriteView>();
            _kewneSprite = GetComponent<KewneSpriteView>();
            _animator = GetComponentInChildren<Animator>();
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                foreach (var parameter in _animator.parameters)
                {
                    _hasSpeed |= parameter.nameHash == Speed && parameter.type == AnimatorControllerParameterType.Float;
                    _hasWalking |= parameter.nameHash == IsWalking && parameter.type == AnimatorControllerParameterType.Bool;
                    _hasRunning |= parameter.nameHash == IsRunning && parameter.type == AnimatorControllerParameterType.Bool;
                }
                return; // Preserve authored hierarchy/animation bindings.
            }

            int childCount = transform.childCount;
            _visual = new GameObject("Locomotion Visual").transform;
            _visual.SetParent(transform, false);
            for (int i = 0; i < childCount; i++)
                transform.GetChild(0).SetParent(_visual, true);

            // A primitive actor has its mesh on the logical root. Mirror just
            // its rendering under the visual pivot; leave the collider in place.
            var filter = GetComponent<MeshFilter>();
            var renderer = GetComponent<MeshRenderer>();
            if (filter != null && renderer != null && renderer.enabled)
            {
                var mesh = new GameObject("Static Actor Mesh");
                mesh.transform.SetParent(_visual, false);
                mesh.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var copy = mesh.AddComponent<MeshRenderer>();
                copy.sharedMaterials = renderer.sharedMaterials;
                copy.shadowCastingMode = renderer.shadowCastingMode;
                copy.receiveShadows = renderer.receiveShadows;
                renderer.enabled = false;
            }
        }

        internal void EnableMonsterFacing()
        {
            // The extracted fallback monsters put their texture on a quad named Sprite.
            foreach (var mesh in GetComponentsInChildren<MeshRenderer>())
                if (mesh.gameObject.name == "Sprite" && !mesh.TryGetComponent<CameraFacingSprite>(out _))
                    mesh.gameObject.AddComponent<CameraFacingSprite>();

            foreach (var sprite in GetComponentsInChildren<SpriteRenderer>())
                if (!sprite.TryGetComponent<CameraFacingSprite>(out _))
                    sprite.gameObject.AddComponent<CameraFacingSprite>();

            _monsterBillboards = GetComponentsInChildren<CameraFacingSprite>();
            _monsterSprite = GetComponentInChildren<MonsterTurnSpriteView>();
            _billboard |= _monsterBillboards.Length > 0;
        }

        internal void FaceMonster(Vector3 direction)
        {
            if (_monsterBillboards == null) return;
            foreach (var billboard in _monsterBillboards) billboard.FaceMonster(direction);
        }

        public void Present(Vector3 direction, float cycle, bool moving) => Present(direction, cycle, moving, false);

        public void Present(Vector3 direction, float cycle, bool moving, bool running)
        {
            Initialize();
            IsMoving = moving;
            IsRunningNow = moving && running;
            direction.y = 0f;
            if (_monsterSprite) _monsterSprite.PresentMovement(direction, cycle, moving);
            if (moving && direction.sqrMagnitude > 0.000001f)
            {
                if (_billboard) FaceMonster(direction);
                else transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                if (_hasSpeed) _animator.SetFloat(Speed, IsRunningNow ? 2f : moving ? 1f : 0f);
                if (_hasRunning) _animator.SetBool(IsRunning, IsRunningNow);
                if (_hasWalking) _animator.SetBool(IsWalking, moving && (!_hasRunning || !IsRunningNow));
            }
            if (_visual == null) return;
            bool proceduralRun = IsRunningNow && (!_kohSprite || !_kohSprite.HasRetailRun);
            float step = moving && !_monsterSprite && (!IsRunningNow || proceduralRun) ? Mathf.Sin(cycle * Mathf.PI * 2f) : 0f;
            _walkOffset = Vector3.up * (Mathf.Abs(step) * (proceduralRun ? 0.085f : 0.05f));
            _visual.localRotation = Quaternion.Euler(proceduralRun ? 10f : 0f, 0f, step * (proceduralRun ? 5f : 3f));
        }

        private void LateUpdate()
        {
            if (_visual)
                _visual.localPosition = _walkOffset + (_kewneSprite ? Vector3.zero : transform.InverseTransformVector(HitOffset));
        }

        private void OnDisable()
        {
            _hitStartedAt = float.NegativeInfinity;
            _walkOffset = Vector3.zero;
            IsMoving = false;
            IsRunningNow = false;
            if (_monsterSprite) _monsterSprite.PresentMovement(Vector3.zero, 0f, false);
            if (_visual == null) return;
            _visual.localPosition = Vector3.zero;
            _visual.localRotation = Quaternion.identity;
        }
    }
}
