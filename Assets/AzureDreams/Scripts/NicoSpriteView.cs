using UnityEngine;
using AzureDreamsReimagined.Rendering;

namespace AzureDreamsReimagined
{
    /// <summary>Renders town Nico from her original modular character sheet.</summary>
    [DisallowMultipleComponent]
    public sealed class NicoSpriteView : MonoBehaviour
    {
        [SerializeField] private Texture2D _sheet;
        [SerializeField, Min(1f)] private float _pixelsPerUnit = 30f;
        [SerializeField, Min(1f)] private float _walkFramesPerSecond = 6f;

        private Sprite[] _bodyFrames;
        private Sprite _head;
        private SpriteRenderer _bodyRenderer;
        private ActorLocomotionView _locomotion;
        private float _animationClock;
        private int _lastBodyFrame = -1;

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
            _head = CreateSprite(6, 4, 29, 31);

            _bodyRenderer = MakePart(billboard.transform, "Body", _bodyFrames[0], 0);
            MakePart(billboard.transform, "Head", _head, 1).transform.localPosition =
                Vector3.up * ((28f - 7f) / Mathf.Max(1f, _pixelsPerUnit));
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
                SetBodyFrame(0);
                return;
            }

            _animationClock += Time.deltaTime * _walkFramesPerSecond;
            SetBodyFrame(Mathf.FloorToInt(_animationClock) % _bodyFrames.Length);
        }

        private void SetBodyFrame(int index)
        {
            if (index == _lastBodyFrame) return;
            _lastBodyFrame = index;
            _bodyRenderer.sprite = _bodyFrames[index];
        }

        private void OnDestroy()
        {
            if (_bodyFrames != null)
                foreach (var sprite in _bodyFrames)
                    if (sprite) Destroy(sprite);
            if (_head) Destroy(_head);
        }
    }
}
