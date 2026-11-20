using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Presents a resident with the original Aunt/Grandpa directional cels (Front/Back/Side).
    /// The cels live in StreamingAssets/Media/Title/RainCast, so they are fetched asynchronously
    /// (works on WebGL) and resolved through <see cref="AssetMode"/> like every other streamed image.
    /// Until they arrive, the prefab's own renderers stay hidden; if loading fails they are restored.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RacetrackResidentSpriteView : MonoBehaviour
    {
        private const float PixelsPerUnit = 48f;
        private static readonly string[] DirectionNames = { "Front", "Back", "Side" };

        [SerializeField] private string _spriteFamily = "Grandpa";
        private readonly Sprite[] _directions = new Sprite[3];
        private SpriteRenderer _renderer;
        private Camera _camera;
        private bool _loaded;

        private void Awake()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            StartCoroutine(LoadDirections());
        }

        private IEnumerator LoadDirections()
        {
            bool allLoaded = true;
            for (int i = 0; i < DirectionNames.Length; i++)
            {
                string resolved = AssetMode.ResolveMedia($"Title/RainCast/{_spriteFamily}{DirectionNames[i]}.png");
                if (resolved == null) { allLoaded = false; continue; }
                var request = UnityWebRequestTexture.GetTexture(MediaUrl(resolved), nonReadable: true);
                request.timeout = 15;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success && this)
                {
                    var texture = DownloadHandlerTexture.GetContent(request);
                    texture.filterMode = FilterMode.Point;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    _directions[i] = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                        Vector2.one * 0.5f, PixelsPerUnit, 0, SpriteMeshType.FullRect);
                }
                else allLoaded = false;
                request.Dispose();
            }

            if (!this) yield break;
            if (!_directions[0] || !allLoaded)
            {
                // Keep the prefab's own visuals when the cels are unavailable (e.g. Classic without them).
                foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
                yield break;
            }

            var visual = new GameObject("Racetrack resident sprite");
            visual.transform.SetParent(transform, false);
            _renderer = visual.AddComponent<SpriteRenderer>();
            _renderer.sprite = _directions[0];
            _loaded = true;
        }

        private static string MediaUrl(string relativePath)
        {
            string root = Application.streamingAssetsPath.TrimEnd('/');
            if (root.Contains("://")) return root + "/Media/" + relativePath;
            return new System.Uri(System.IO.Path.Combine(root, "Media", relativePath)).AbsoluteUri;
        }

        private void LateUpdate()
        {
            if (!_loaded || !_renderer) return;
            if (!_camera || !_camera.isActiveAndEnabled) _camera = Camera.main;
            if (!_camera) return;
            Vector3 forward = _camera.transform.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = _camera.transform.right;
            float side = Vector3.Dot(transform.forward, right);
            float depth = Vector3.Dot(transform.forward, forward);
            int direction = Mathf.Abs(side) > Mathf.Abs(depth) ? 2 : depth > 0f ? 1 : 0;
            _renderer.sprite = _directions[direction] ? _directions[direction] : _directions[0];
            _renderer.flipX = direction == 2 && side < 0f;
            _renderer.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            _renderer.transform.localScale = Vector3.one * (1.65f / _renderer.sprite.bounds.size.y);
        }
    }
}
