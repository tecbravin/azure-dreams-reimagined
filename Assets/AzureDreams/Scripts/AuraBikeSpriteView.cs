using System;
using UnityEngine;
using AzureDreamsReimagined.Rendering;

namespace AzureDreamsReimagined
{
    // Retail eight-facing Aura Bike commands, including the booster variant.
    public sealed class AuraBikeSpriteView : MonoBehaviour
    {
        [Serializable] private sealed class Sequence { public int[] frames, ticks; }
        [Serializable] private sealed class Clip { public Sequence[] directions; }
        [Serializable] private sealed class Bank { public int frameCount; public Clip[] clips; }
        private Bank _bank;
        private Sprite[] _sprites;
        private SpriteRenderer _renderer;
        private float _clock;
        private Vector2 _heading = Vector2.down;
        private bool _moving;
        public void Initialize(bool booster)
        {
            string path = "Town/AuraBike/" + (booster ? "Booster" : "Bike");
            var atlas = AssetMode.Load<Texture2D>(path);
            var data = AssetMode.Load<TextAsset>(path);
            if (!atlas || !data) return;
            _bank = JsonUtility.FromJson<Bank>(data.text);
            _sprites = new Sprite[_bank.frameCount];
            for (int i = 0; i < _sprites.Length; i++)
                _sprites[i] = Sprite.Create(atlas, new Rect(i % 8 * 128, atlas.height - (i / 8 + 1) * 128, 128, 128),
                    new Vector2(.5f, 28f / 128f), 48f, 0, SpriteMeshType.FullRect);
            _renderer = gameObject.AddComponent<SpriteRenderer>();
            gameObject.AddComponent<CameraFacingSprite>();
        }
        public void Present(Vector2 heading, bool moving) { _heading = heading; _moving = moving; }
        private void LateUpdate()
        {
            if (!_renderer || _bank == null) return;
            var camera = Camera.main;
            float yaw = camera ? camera.transform.eulerAngles.y : 0f;
            float angle = Mathf.Atan2(_heading.x, _heading.y) * Mathf.Rad2Deg - yaw;
            int direction = Mathf.RoundToInt(Mathf.Repeat(angle, 360f) / 45f) % 8;
            var seq = _bank.clips[_moving ? 1 : 0].directions[direction];
            _clock += Time.deltaTime * 60f;
            int total = 0; foreach (int tick in seq.ticks) total += Mathf.Max(1, tick);
            float phase = _clock % Mathf.Max(1, total);
            int index = 0;
            while (index + 1 < seq.frames.Length && phase >= Mathf.Max(1, seq.ticks[index])) phase -= Mathf.Max(1, seq.ticks[index++]);
            _renderer.sprite = _sprites[seq.frames[index]];
            _renderer.flipX = direction > 4;
        }
        private void OnDestroy() { if (_sprites != null) foreach (var sprite in _sprites) if (sprite) Destroy(sprite); }
    }
}
