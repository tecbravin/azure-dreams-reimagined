using UnityEngine;

namespace AzureDreamsReimagined
{
    [DisallowMultipleComponent]
    public sealed class TownCharacterSpriteView : MonoBehaviour
    {
        [System.Serializable]
        private struct Part
        {
            public Rect rectangle;
            public Vector2 offset;
        }

        [SerializeField] private Texture2D _sheet;
        [SerializeField] private Material _material;
        [SerializeField, Min(1f)] private float _pixelsPerUnit = 48f;
        [SerializeField] private Part[] _parts;
        private Sprite[] _sprites;
        private Sprite[] _childDirectionalFrames;
        private SpriteRenderer _childDirectionalRenderer;
        private bool _isYoungChild;
        private int _lastChildFrame = -1;

        private void Awake()
        {
            if (!_sheet || _parts == null) return;
            float pixelsPerUnit = Mathf.Max(1f, _pixelsPerUnit);
            var visual = new GameObject("Character Sprite");
            visual.transform.SetParent(transform, false);
            visual.AddComponent<AzureDreamsReimagined.Rendering.CameraFacingSprite>();

            // The prologue children share the normal town-character path, but
            // their original sheets contain directional walking cels instead of
            // a single assembled town pose.  Keep every other resident on the
            // existing modular path.
            string id = GetComponent<NPCController>()?.npcId;
            if (TryCreateYoungChildSprites(id, visual.transform, pixelsPerUnit)) return;

            _sprites = new Sprite[_parts.Length];
            for (int i = 0; i < _parts.Length; i++)
            {
                var part = _parts[i];
                _sprites[i] = CreateSprite(part.rectangle, pixelsPerUnit);
                var child = new GameObject("Sprite Part " + i);
                child.transform.SetParent(visual.transform, false);
                child.transform.localPosition = new Vector3(part.offset.x / pixelsPerUnit, part.offset.y / pixelsPerUnit, -i * 0.001f);
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sprite = _sprites[i];
                if (_material) renderer.sharedMaterial = _material;
            }
        }

        private Sprite CreateSprite(Rect rectangle, float pixelsPerUnit)
        {
            rectangle.y = _sheet.height - rectangle.y - rectangle.height;
            return Sprite.Create(_sheet, rectangle, new Vector2(0.5f, 0f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        private bool TryCreateYoungChildSprites(string id, Transform visual, float pixelsPerUnit)
        {
            Rect[] frames = null;
            if (id == "youngnico" && _sheet.width == 230 && _sheet.height == 244)
            {
                // Original Young Nico sheet: back, front and side walking cels.
                frames = new[]
                {
                    new Rect(6, 52, 25, 41), new Rect(39, 52, 25, 41),
                    new Rect(6, 6, 25, 41), new Rect(39, 6, 25, 41),
                    new Rect(72, 52, 25, 41), new Rect(105, 52, 25, 41)
                };
            }
            else if (id == "youngkoh" && _sheet.width == 399 && _sheet.height == 226)
            {
                // Original Young Koh cels.  The lower row contains the alternate
                // walking poses for the matching upper-row directions.
                frames = new[]
                {
                    new Rect(250, 140, 28, 39), new Rect(250, 181, 28, 39),
                    new Rect(280, 140, 28, 39), new Rect(280, 181, 28, 39),
                    new Rect(310, 140, 28, 39), new Rect(310, 181, 28, 39)
                };
            }
            if (frames == null) return false;

            _isYoungChild = true;
            _childDirectionalFrames = new Sprite[frames.Length];
            for (int i = 0; i < frames.Length; i++) _childDirectionalFrames[i] = CreateSprite(frames[i], pixelsPerUnit);
            var child = new GameObject("Directional Child Sprite");
            child.transform.SetParent(visual, false);
            _childDirectionalRenderer = child.AddComponent<SpriteRenderer>();
            if (_material) _childDirectionalRenderer.sharedMaterial = _material;
            PresentMovement(Vector3.back, false);
            return true;
        }

        internal void PresentMovement(Vector3 direction, bool moving)
        {
            if (!_isYoungChild || !_childDirectionalRenderer || _childDirectionalFrames == null) return;
            bool side = Mathf.Abs(direction.x) >= Mathf.Abs(direction.z);
            int directionIndex = side ? 2 : direction.z > 0f ? 1 : 0;
            int step = moving ? Mathf.FloorToInt(Time.time * 7f) & 1 : 0;
            int frame = directionIndex * 2 + step;
            frame = Mathf.Clamp(frame, 0, _childDirectionalFrames.Length - 1);
            if (frame != _lastChildFrame)
            {
                _lastChildFrame = frame;
                _childDirectionalRenderer.sprite = _childDirectionalFrames[frame];
            }
            _childDirectionalRenderer.flipX = side && direction.x < 0f;
        }

        private void OnDestroy()
        {
            if (_sprites != null)
                foreach (var sprite in _sprites) if (sprite) Destroy(sprite);
            if (_childDirectionalFrames != null)
                foreach (var sprite in _childDirectionalFrames) if (sprite) Destroy(sprite);
        }
    }
}
