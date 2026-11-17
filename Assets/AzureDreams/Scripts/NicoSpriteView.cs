using UnityEngine;
using AzureDreamsReimagined.Rendering;

namespace AzureDreamsReimagined
{
    /// <summary>Renders town Nico from her original modular character sheet.</summary>
    [DisallowMultipleComponent]
    public sealed class NicoSpriteView : MonoBehaviour
    {
        // Coordinates are in the transparent retail-derived Nico/Weedy/Jorda
        // sheet. The next green cel starts at y=30 and is a separate pose; the
        // old rectangle captured both cels, which produced the twisted torso.
        private const float NicoHeadLeft = 6f;
        private const float NicoHeadTop = 4f;
        private const float NicoHeadWidth = 33f;
        private const float NicoHeadHeight = 25f;
        private const float NicoBackHeadTop = 28f;
        private const float NicoBackHeadHeight = 19f;
        private const float NicoSideHeadTop = 44f;
        private const float NicoSideHeadWidth = 28f;
        private const float NicoSideHeadHeight = 25f;
        private const float NicoHeadPivotCorrectionX = 2f;
        [SerializeField] private Texture2D _sheet;
        [SerializeField, Min(1f)] private float _pixelsPerUnit = 30f;
        [SerializeField, Min(1f)] private float _walkFramesPerSecond = 6f;

        private Sprite[] _bodyFrames;
        private Sprite[] _headFrames;
        private SpriteRenderer _bodyRenderer;
        private SpriteRenderer _headRenderer;
        private ActorLocomotionView _locomotion;
        private float _animationClock;
        private int _lastFrame = -1;
        private NicoFacing _facing = NicoFacing.Front;

        private enum NicoFacing { Front, Back, Side }

        private void Awake()
        {
            foreach (var model in GetComponentsInChildren<MeshRenderer>(true))
                model.enabled = false;

            if (!_sheet) return;
            _locomotion = GetComponent<ActorLocomotionView>();
            var billboard = new GameObject("Nico Sprite");
            billboard.transform.SetParent(transform, false);
            billboard.AddComponent<CameraFacingSprite>();

            // The source sheet stores Nico's body and front hair as separate
            // cel pieces. Overlay the front cel over the body's back-hair cel.
            _bodyFrames = new[]
            {
                CreateSprite(6, 72, 26, 28),
                CreateSprite(34, 72, 29, 28)
            };
            _headFrames = new[]
            {
                CreateSprite(NicoHeadLeft, NicoHeadTop, NicoHeadWidth, NicoHeadHeight),
                CreateSprite(NicoHeadLeft, NicoBackHeadTop, NicoHeadWidth, NicoBackHeadHeight),
                CreateSprite(NicoHeadLeft, NicoSideHeadTop, NicoSideHeadWidth, NicoSideHeadHeight)
            };

            _bodyRenderer = MakePart(billboard.transform, "Body", _bodyFrames[0], 0);
            _headRenderer = MakePart(billboard.transform, "Head", _headFrames[0], 1);
            var head = _headRenderer.transform;
            // Keep the head slightly overlapping the body so the neck has no
            // visible seam, while the unrelated lower cel stays excluded.
            head.localPosition = new Vector3(NicoHeadPivotCorrectionX / Mathf.Max(1f, _pixelsPerUnit),
                (28f - 7f) / Mathf.Max(1f, _pixelsPerUnit), 0f);
        }

        private Sprite CreateSprite(float x, float top, float width, float height)
        {
            var rect = new Rect(x, _sheet.height - top - height, width, height);
            return Sprite.Create(_sheet, rect, new Vector2(0.5f, 0f), _pixelsPerUnit,
                0, SpriteMeshType.FullRect);
        }

        private SpriteRenderer MakePart(Transform parent, string name, Sprite sprite, int order)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            var renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private void Update()
        {
            if (_bodyRenderer == null || _bodyFrames == null || _bodyFrames.Length == 0) return;
            bool moving = _locomotion && _locomotion.IsMoving;
            if (!moving)
            {
                _animationClock = 0f;
                SetFrame(0);
                return;
            }

            _animationClock += Time.deltaTime * _walkFramesPerSecond;
            SetFrame(Mathf.FloorToInt(_animationClock) % _bodyFrames.Length);
        }

        private void SetFrame(int index)
        {
            if (index == _lastFrame) return;
            _lastFrame = index;
            _bodyRenderer.sprite = _bodyFrames[index];
            ApplyHeadFacing();
        }

        internal void PresentMovement(Vector3 direction, bool moving)
        {
            if (!moving || direction.sqrMagnitude < 0.0001f) return;
            bool side = Mathf.Abs(direction.x) >= Mathf.Abs(direction.z);
            _facing = side ? NicoFacing.Side : direction.z > 0f ? NicoFacing.Back : NicoFacing.Front;
            bool faceLeft = side && direction.x < 0f;
            if (_bodyRenderer) _bodyRenderer.flipX = faceLeft;
            if (_headRenderer) _headRenderer.flipX = faceLeft;
            ApplyHeadFacing();
        }

        private void ApplyHeadFacing()
        {
            if (!_headRenderer || _headFrames == null || _headFrames.Length < 3) return;
            _headRenderer.sprite = _headFrames[(int)_facing];
        }

        private void OnDestroy()
        {
            if (_bodyFrames != null)
                foreach (var sprite in _bodyFrames)
                    if (sprite) Destroy(sprite);
            if (_headFrames != null)
                foreach (var sprite in _headFrames)
                    if (sprite) Destroy(sprite);
        }
    }
}
