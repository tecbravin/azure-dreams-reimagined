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

        private void Awake()
        {
            if (!_sheet || _parts == null) return;
            float pixelsPerUnit = Mathf.Max(1f, _pixelsPerUnit);
            _sprites = new Sprite[_parts.Length];
            var visual = new GameObject("Character Sprite");
            visual.transform.SetParent(transform, false);
            visual.AddComponent<AzureDreamsReimagined.Rendering.CameraFacingSprite>();
            for (int i = 0; i < _parts.Length; i++)
            {
                var part = _parts[i];
                var rectangle = part.rectangle;
                rectangle.y = _sheet.height - rectangle.y - rectangle.height;
                _sprites[i] = Sprite.Create(_sheet, rectangle, new Vector2(0.5f, 0f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
                var child = new GameObject("Sprite Part " + i);
                child.transform.SetParent(visual.transform, false);
                child.transform.localPosition = new Vector3(part.offset.x / pixelsPerUnit, part.offset.y / pixelsPerUnit, -i * 0.001f);
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sprite = _sprites[i];
                if (_material) renderer.sharedMaterial = _material;
            }
        }

        private void OnDestroy()
        {
            if (_sprites == null) return;
            foreach (var sprite in _sprites) if (sprite) Destroy(sprite);
        }
    }
}
