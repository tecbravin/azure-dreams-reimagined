using UnityEngine;
using AzureDreamsReimagined.Rendering;

namespace AzureDreamsReimagined
{
    internal sealed class SalamanderSpriteView : MonoBehaviour
    {
        private const string Art = "Monsters/Salamander/";
        private SpriteRenderer _body, _fire, _attack;
        private Sprite[] _bodyFrames, _fireFrames, _attackFrames;
        private float _attackTime = -1f;

        internal bool Initialize()
        {
            var body = AssetMode.Load<Texture2D>(Art + "Body");
            var fire = AssetMode.Load<Texture2D>(Art + "Fire");
            var attack = AssetMode.Load<Texture2D>(Art + "Attack");
            if (!body || !fire || !attack) return false;
            body.filterMode = fire.filterMode = attack.filterMode = FilterMode.Point;

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;

            var visual = new GameObject("Salamander sprite");
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = Vector3.down * 0.5f;
            visual.AddComponent<CameraFacingSprite>();
            _bodyFrames = Slice(body, 24, 24, 1, 4);
            _fireFrames = Slice(fire, 16, 32, 2, 4);
            _attackFrames = Slice(attack, 24, 32, 2, 5, 2);
            _fire = AddLayer(visual.transform, "Flames", Vector3.zero, 0);
            _body = AddLayer(visual.transform, "Body", Vector3.up * 0.16f, 1);
            _attack = AddLayer(visual.transform, "Attack", Vector3.zero, 2);
            visual.AddComponent<UnityEngine.Rendering.SortingGroup>();
            _attack.enabled = false;
            return true;
        }

        private static SpriteRenderer AddLayer(Transform parent, string label, Vector3 position, int order)
        {
            var layer = new GameObject(label);
            layer.transform.SetParent(parent, false);
            layer.transform.localPosition = position;
            var renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = order;
            return renderer;
        }

        private static Sprite[] Slice(Texture2D texture, int width, int height, int rows, int columns, int startRow = 0)
        {
            var frames = new Sprite[rows * columns];
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < columns; col++)
                    frames[row * columns + col] = Sprite.Create(texture,
                        new Rect(col * width, texture.height - (startRow + row + 1) * height, width, height),
                        new Vector2(0.5f, 0f), 24f, 0, SpriteMeshType.FullRect);
            return frames;
        }

        internal void PresentAttack(Vector2Int direction)
        {
            if (!_attack) return;
            _attackTime = Time.time;
            _attack.transform.position = transform.position + new Vector3(direction.x, -0.5f, direction.y);
        }

        private void LateUpdate()
        {
            if (!_body) return;
            _body.sprite = _bodyFrames[(int)(Time.time * 6f) % _bodyFrames.Length];
            _fire.sprite = _fireFrames[(int)(Time.time * 10f) % _fireFrames.Length];
            int frame = (int)((Time.time - _attackTime) * 14f);
            _attack.enabled = _attackTime >= 0f && frame < _attackFrames.Length;
            if (_attack.enabled) _attack.sprite = _attackFrames[frame];
        }

        private void OnDestroy()
        {
            foreach (var frames in new[] { _bodyFrames, _fireFrames, _attackFrames })
                if (frames != null)
                    foreach (var frame in frames)
                        if (frame) Destroy(frame);
        }
    }
}

