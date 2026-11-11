using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Stub for PSX-style sprite animation controller.
    /// TODO: implement frame playback, direction flipping, and event emission.
    /// </summary>
    public class PsxAnimationController : MonoBehaviour
    {
        public int framesPerSecond = 12;
        public bool loop = true;

        private SpriteRenderer _renderer;
        private Sprite[] _frames;
        private float _timer;
        private int _index;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        public void Play(Sprite[] frames)
        {
            _frames = frames;
            _index = 0;
            _timer = 0f;
            ApplyFrame();
        }

        private void Update()
        {
            if (_frames == null || _frames.Length == 0) return;

            _timer += Time.deltaTime;
            float interval = 1f / Mathf.Max(1, framesPerSecond);
            while (_timer >= interval)
            {
                _timer -= interval;
                _index++;
                if (_index >= _frames.Length)
                {
                    if (loop) _index = 0;
                    else { _index = _frames.Length - 1; break; }
                }
            }
            ApplyFrame();
        }

        private void ApplyFrame()
        {
            if (_renderer != null && _frames != null && _frames.Length > 0)
            {
                _renderer.sprite = _frames[_index];
            }
        }
    }
}
