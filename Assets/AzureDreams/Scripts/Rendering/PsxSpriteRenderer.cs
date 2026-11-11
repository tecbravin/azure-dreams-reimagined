using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace AzureDreamsReimagined.Rendering
{
    /// <summary>
    /// Runtime sprite renderer that assembles PS1 part-chain frames into Unity Sprites.
    /// Each frame is composited from multiple atlas parts (12-byte part chain entries)
    /// using palette lookup. Callback-only parts are skipped; this preview renderer
    /// does not implement retail callback events or PS1 ABR semi-transparency.
    ///
    /// Optimizations (Architect):
    ///  - Atlas is read with GetPixels32 (Color32, 4 bytes/pixel) instead of GetPixels
    ///    (Color, 16 bytes/pixel): ~4x less transient memory.
    ///  - Redundant per-pixel allocations removed; Color constants hoisted.
    ///  - BuildFrames runs as a coroutine spread over frames to avoid a Start() hitch.
    ///  - A static frame cache keyed by resource identity lets identical atlases share
    ///    one baked set of frames (and one set of textures) across instances.
    ///  - Optional StreamingAssets loading for WebGL (opt-in via streamingAtlasRelativePath).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PsxSpriteRenderer : MonoBehaviour
    {
        [Header("Resource Data")]
        [Tooltip("atlas_indices.png (readable, 256x256, 4bpp indices). Leave null to load from StreamingAssets.")]
        public Texture2D atlas;
        [Tooltip("Relative path inside StreamingAssets for atlas_index.png (e.g. \"atlas_index.png\"). Used only when 'atlas' is null.")]
        public string streamingAtlasRelativePath = string.Empty;
        [Tooltip("Relative path inside StreamingAssets for metadata.json. Empty = derived from atlas path's directory.")]
        public string streamingMetadataRelativePath = string.Empty;
        [Tooltip("Relative path inside StreamingAssets for palettes.json. Empty = derived from atlas path's directory.")]
        public string streamingPalettesRelativePath = string.Empty;
        [Tooltip("metadata.json TextAsset for this resource.")]
        public TextAsset metadataJson;
        [Tooltip("palettes.json TextAsset for this resource.")]
        public TextAsset palettesJson;

        [Header("Animation")]
        public float framesPerSecond = 8f;
        public float PlaybackSpeed { get; set; } = 1f;
        public bool playOnStart = true;
        [Tooltip("Number of frames to build per coroutine step before yielding.")]
        public int buildBatchSize = 4;

        private SpriteRenderer _sr;
        private List<Sprite> _frames = new List<Sprite>();
        private int _frameIndex;
        private float _timer;
        private bool _playing;
        private bool _building;
        private Coroutine _buildCoroutine;

        // Main-thread-only, active leases only: no idle retention.
        private const int MaxSharedResources = 32;
        private sealed class SharedFrames
        {
            public readonly List<Sprite> frames = new List<Sprite>();
            public int users;
        }
        private static readonly Dictionary<string, SharedFrames> _frameCache = new Dictionary<string, SharedFrames>();
        private static readonly Dictionary<string, PsxSpriteRenderer> _inflight = new Dictionary<string, PsxSpriteRenderer>();
        private string _buildingKey;
        private SharedFrames _lease;
        private string _leaseKey;
        private bool _started;
        private UnityWebRequest _request;
        private Texture2D _loadedAtlas;
        private TextAsset _loadedMetadata, _loadedPalettes;
        private List<Sprite> _pendingFrames;

        [Serializable] private class Part { public int x, y, u, v, width, height, flags, clut_raw; public bool is_callback; }
        [Serializable] private class Chain { public int pointer; public List<Part> parts; }
        [Serializable] private class Meta { public int animation_count; public List<Chain> chains; }
        [Serializable] private class Rgba { public int[] values; }
        [Serializable] private class PalEntry { public int index; public Rgba[] rgba_preview; public int[] words_rgb555_stp; }

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
        }

        private void Start()
        {
            _started = true;
            if (_building || _lease != null) return;

            if (atlas == null && !string.IsNullOrEmpty(streamingAtlasRelativePath))
            {
                _buildCoroutine = StartCoroutine(LoadAtlasThenBuild());
                return;
            }

            if (atlas == null)
            {
                Debug.LogWarning("PsxSpriteRenderer: missing atlas.");
                return;
            }

            if (metadataJson == null || palettesJson == null)
            {
                Debug.LogWarning("PsxSpriteRenderer: missing metadata/palettes.");
                return;
            }
            StartBuild();
        }

        private void OnEnable() { if (_started) Start(); }

        private IEnumerator LoadAtlasThenBuild()
        {
            _building = true;
            // Download indices as data, not a color-managed texture. This instance
            // owns the source texture and releases it on disable/destroy.
            using (var req = UnityWebRequest.Get(StreamingUrl(streamingAtlasRelativePath)))
            {
                _request = req;
                yield return req.SendWebRequest();
                _request = null;
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("PsxSpriteRenderer: failed to load atlas from StreamingAssets: " + req.error);
                    _building = false;
                    yield break;
                }
                _loadedAtlas = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                if (!_loadedAtlas.LoadImage(req.downloadHandler.data))
                {
                    Debug.LogError("PsxSpriteRenderer: invalid atlas PNG.");
                    _building = false;
                    ReleaseLoadedSources();
                    yield break;
                }
                atlas = _loadedAtlas;
                atlas.filterMode = FilterMode.Point;
                atlas.wrapMode = TextureWrapMode.Clamp;
            }

            // Load metadata & palettes from StreamingAssets if not assigned, so the
            // streaming path is self-contained (populates the same serialized fields).
            if (metadataJson == null)
            {
                string mpath = string.IsNullOrEmpty(streamingMetadataRelativePath)
                    ? DeriveSibling(streamingAtlasRelativePath, "metadata.json")
                    : streamingMetadataRelativePath;
                if (!string.IsNullOrEmpty(mpath))
                {
                    using (var mreq = UnityWebRequest.Get(StreamingUrl(mpath)))
                    {
                        _request = mreq;
                        yield return mreq.SendWebRequest();
                        _request = null;
                        if (mreq.result == UnityWebRequest.Result.Success)
                            metadataJson = _loadedMetadata = new TextAsset(mreq.downloadHandler.text);
                    }
                }
            }
            if (palettesJson == null)
            {
                string ppath = string.IsNullOrEmpty(streamingPalettesRelativePath)
                    ? DeriveSibling(streamingAtlasRelativePath, "palettes.json")
                    : streamingPalettesRelativePath;
                if (!string.IsNullOrEmpty(ppath))
                {
                    using (var preq = UnityWebRequest.Get(StreamingUrl(ppath)))
                    {
                        _request = preq;
                        yield return preq.SendWebRequest();
                        _request = null;
                        if (preq.result == UnityWebRequest.Result.Success)
                            palettesJson = _loadedPalettes = new TextAsset(preq.downloadHandler.text);
                    }
                }
            }

            _building = false;
            if (metadataJson == null || palettesJson == null)
            {
                Debug.LogError("PsxSpriteRenderer: could not load metadata/palettes.");
                ReleaseLoadedSources();
                yield break;
            }
            StartBuild();
        }

        private void StartBuild()
        {
            string key = ComputeResourceKey();
            var build = BuildFramesCoroutine(key);
            if (Application.isPlaying)
                _buildCoroutine = StartCoroutine(build);
            else
            {
                // EditMode has no player-loop coroutine scheduler.
                while (build.MoveNext()) { }
            }
        }

        private string ComputeResourceKey()
        {
            return (atlas != null ? atlas.GetEntityId().ToString() : "none") + "|" +
                (metadataJson != null ? metadataJson.GetEntityId().ToString() : "none") + "|" +
                (palettesJson != null ? palettesJson.GetEntityId().ToString() : "none");
        }

        private void Update()
        {
            if (!_playing || _frames.Count <= 1) return;
            _timer += Time.deltaTime * PlaybackSpeed;
            float frameTime = 1f / Mathf.Max(0.01f, framesPerSecond);
            if (_timer >= frameTime)
            {
                _timer -= frameTime;
                _frameIndex = (_frameIndex + 1) % _frames.Count;
                _sr.sprite = _frames[_frameIndex];
            }
        }

        private IEnumerator BuildFramesCoroutine(string cacheKey)
        {
            if (_lease != null) yield break;
            while (_inflight.TryGetValue(cacheKey, out var owner) && owner != null && owner != this)
                yield return null;
            _inflight[cacheKey] = this;
            _buildingKey = cacheKey;
            try
            {
                var build = BuildFramesCore(cacheKey);
                try { while (build.MoveNext()) yield return build.Current; }
                finally { (build as IDisposable)?.Dispose(); }
            }
            finally
            {
                if (_inflight.TryGetValue(cacheKey, out var currentOwner) && currentOwner == this) _inflight.Remove(cacheKey);
                _buildingKey = null;
            }
        }

        private IEnumerator BuildFramesCore(string cacheKey)
        {
            if (_lease != null) yield break;
            if (_frameCache.TryGetValue(cacheKey, out var shared))
            {
                Acquire(shared, cacheKey);
                yield break;
            }
            Meta meta = null;
            try { meta = JsonUtility.FromJson<Meta>(metadataJson.text); }
            catch (Exception e) { Debug.LogError("PsxSpriteRenderer: metadata parse error: " + e.Message); yield break; }
            if (meta == null || meta.chains == null || meta.chains.Count == 0)
            {
                Debug.LogWarning("PsxSpriteRenderer: no chains.");
                yield break;
            }

            // Parse palettes
            List<PalEntry> pals = null;
            try
            {
                var wrapped = JsonUtility.FromJson<Wrapper<PalEntry>>(palettesJson.text);
                pals = wrapped != null ? wrapped.items : null;
            }
            catch { }
            if (pals == null)
            {
                string wrappedStr = "{\"items\":" + NormalizePreviewArrays(palettesJson.text) + "}";
                try
                {
                    var wrapped = JsonUtility.FromJson<Wrapper<PalEntry>>(wrappedStr);
                    pals = wrapped != null ? wrapped.items : null;
                }
                catch (Exception e) { Debug.LogError("PsxSpriteRenderer: palette parse error: " + e.Message); }
            }

            Color[][] paletteColors = null;
            if (pals != null)
            {
                paletteColors = new Color[pals.Count][];
                for (int i = 0; i < pals.Count; i++)
                {
                    var pe = pals[i];
                    var colors = new Color[16];
                    if (pe != null && pe.words_rgb555_stp != null)
                    {
                        int limit = Mathf.Min(pe.words_rgb555_stp.Length, 16);
                        for (int c = 0; c < limit; c++)
                        {
                            int word = pe.words_rgb555_stp[c];
                            colors[c] = new Color((word & 31) / 31f, ((word >> 5) & 31) / 31f,
                                ((word >> 10) & 31) / 31f, word == 0 ? 0f : 1f);
                        }
                    }
                    else if (pe != null && pe.rgba_preview != null)
                    {
                        int limit = Mathf.Min(pe.rgba_preview.Length, 16);
                        for (int c = 0; c < limit; c++)
                        {
                            var rgba = pe.rgba_preview[c]?.values;
                            if (rgba != null && rgba.Length >= 4)
                                colors[c] = new Color(rgba[0] / 255f, rgba[1] / 255f, rgba[2] / 255f, rgba[3] / 255f);
                        }
                    }
                    paletteColors[i] = colors;
                }
            }

            // Read atlas pixels as Color32 (4 bytes/pixel) instead of Color (16 bytes/pixel).
            Color32[] atlasPixels;
            int aw = atlas.width, ah = atlas.height;
            try { atlasPixels = atlas.GetPixels32(); }
            catch (Exception e) { Debug.LogError("PsxSpriteRenderer: atlas not readable: " + e.Message); yield break; }

            // Hoisted constants (avoid per-pixel struct construction).
            var fallbackColor = new Color(1f, 1f, 1f, 1f);

            var newFrames = new List<Sprite>();
            _pendingFrames = newFrames;
            _building = true;
            int builtSinceYield = 0;
            int batchSize = Mathf.Max(1, buildBatchSize);

            foreach (var chain in meta.chains)
            {
                if (chain == null || chain.parts == null || chain.parts.Count == 0) continue;

                int minx = int.MaxValue, miny = int.MaxValue, maxx = int.MinValue, maxy = int.MinValue;
                for (int i = 0; i < chain.parts.Count; i++)
                {
                    var part = chain.parts[i];
                    if (part == null || part.is_callback) continue;
                    if (part.x < minx) minx = part.x;
                    if (part.y < miny) miny = part.y;
                    int px = part.x + part.width;
                    int py = part.y + part.height;
                    if (px > maxx) maxx = px;
                    if (py > maxy) maxy = py;
                }
                if (minx == int.MaxValue) continue;
                int W = maxx - minx, H = maxy - miny;
                if (W <= 0 || H <= 0) continue;

                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                var pixels = new Color32[W * H]; // already zeroed (default Color is transparent)

                for (int pi = 0; pi < chain.parts.Count; pi++)
                {
                    var part = chain.parts[pi];
                    if (part == null || part.is_callback) continue;

                    int palIdx = (part.clut_raw >> 7) & 0x1F;
                    if (paletteColors == null || palIdx >= paletteColors.Length) palIdx = 0;
                    var pal = (paletteColors != null) ? paletteColors[palIdx] : null;

                    for (int dy = 0; dy < part.height; dy++)
                    {
                        int ay = part.v + dy;
                        if (ay < 0 || ay >= ah) continue;
                        int rowBase = ay * aw;
                        for (int dx = 0; dx < part.width; dx++)
                        {
                            int ax = part.u + dx;
                            if (ax < 0 || ax >= aw) continue;

                            int atlasIdx = atlasPixels[rowBase + ax].r & 0x0F;
                            Color c = (pal != null && atlasIdx < pal.Length) ? pal[atlasIdx] : fallbackColor;
                            if (c.a <= 0f) continue; // Visibility comes from the CLUT, not the index.

                            int cx = part.x + dx - minx;
                            int cy = part.y + dy - miny;
                            int uy = (H - 1) - cy;
                            if (cx < 0 || cx >= W || uy < 0 || uy >= H) continue;
                            pixels[uy * W + cx] = c;
                        }
                    }
                }

                tex.SetPixels32(pixels);
                tex.Apply(false, true);
                var spr = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
                spr.name = "frame_" + chain.pointer;
                newFrames.Add(spr);

                builtSinceYield++;
                if (builtSinceYield >= batchSize)
                {
                    builtSinceYield = 0;
                    yield return null;
                }
            }

            // Another coroutine may have finished the same resource while this
            // one yielded. Reuse its lease rather than leaking a duplicate bake.
            if (_frameCache.TryGetValue(cacheKey, out shared))
            {
                DestroyFrames(newFrames);
                Acquire(shared, cacheKey);
            }
            else
            {
                shared = new SharedFrames();
                shared.frames.AddRange(newFrames);
                if (_frameCache.Count < MaxSharedResources)
                    _frameCache.Add(cacheKey, shared);
                Acquire(shared, cacheKey);
            }
            _pendingFrames = null;
            _building = false;

            Debug.Log("PsxSpriteRenderer: built " + _frames.Count + " frames.");
        }

        private void Acquire(SharedFrames shared, string key)
        {
            _lease = shared;
            _leaseKey = key;
            shared.users++;
            _frames = shared.frames;
            _frameIndex = 0;
            _timer = 0f;
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_frames.Count > 0) _sr.sprite = _frames[0];
            _playing = playOnStart;
        }

        private void OnDisable()
        {
            _playing = false;
            if (_buildingKey != null && _inflight.TryGetValue(_buildingKey, out var owner) && owner == this)
                _inflight.Remove(_buildingKey);
            _buildingKey = null;
            if (_request != null)
            {
                _request.Abort();
                _request.Dispose();
                _request = null;
            }
            if (_buildCoroutine != null)
            {
                StopCoroutine(_buildCoroutine);
                _buildCoroutine = null;
            }
            _building = false;
            if (_sr != null) _sr.sprite = null;
            if (_pendingFrames != null) DestroyFrames(_pendingFrames);
            _pendingFrames = null;
            if (_lease != null && --_lease.users == 0)
            {
                if (_frameCache.TryGetValue(_leaseKey, out var shared) && ReferenceEquals(shared, _lease))
                    _frameCache.Remove(_leaseKey);
                DestroyFrames(_lease.frames);
            }
            _lease = null;
            _leaseKey = null;
            _frames = new List<Sprite>();
            ReleaseLoadedSources();
        }

        private void OnDestroy() => OnDisable();

        private void ReleaseLoadedSources()
        {
            if (_loadedAtlas != null)
            {
                if (atlas == _loadedAtlas) atlas = null;
                DestroyOwned(_loadedAtlas);
            }
            if (_loadedMetadata != null)
            {
                if (metadataJson == _loadedMetadata) metadataJson = null;
                DestroyOwned(_loadedMetadata);
            }
            if (_loadedPalettes != null)
            {
                if (palettesJson == _loadedPalettes) palettesJson = null;
                DestroyOwned(_loadedPalettes);
            }
            _loadedAtlas = null;
            _loadedMetadata = _loadedPalettes = null;
        }

        private static void DestroyFrames(List<Sprite> frames)
        {
            foreach (var sprite in frames)
            {
                if (sprite == null) continue;
                var texture = sprite.texture;
                DestroyOwned(sprite);
                DestroyOwned(texture);
            }
            frames.Clear();
        }

        private static void DestroyOwned(UnityEngine.Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        private static string StreamingUrl(string relativePath)
        {
            string root = Application.streamingAssetsPath.Replace("\\", "/").TrimEnd('/');
            if (root.Contains("://")) return root + "/" + relativePath.Replace("\\", "/");
            return new Uri(System.IO.Path.Combine(root, relativePath)).AbsoluteUri;
        }

        // JsonUtility cannot deserialize arrays of arrays. Normalize only the
        // numeric RGBA rows to serializable objects; raw RGB555 words stay intact.
        private static string NormalizePreviewArrays(string json)
        {
            const string field = "\"rgba_preview\"";
            int search = 0;
            var output = new System.Text.StringBuilder(json.Length);
            while (true)
            {
                int key = json.IndexOf(field, search, StringComparison.Ordinal);
                if (key < 0) { output.Append(json, search, json.Length - search); break; }
                int start = json.IndexOf('[', key + field.Length);
                if (start < 0) throw new FormatException("Invalid RGBA preview array.");
                output.Append(json, search, start - search);
                int depth = 0, end = start;
                for (; end < json.Length; end++)
                {
                    char ch = json[end];
                    if (ch == '[') { depth++; if (depth == 2) output.Append("{\"values\":"); }
                    output.Append(ch);
                    if (ch == ']')
                    {
                        if (depth == 2) output.Append('}');
                        if (--depth == 0) { end++; break; }
                    }
                }
                if (depth != 0) throw new FormatException("Unterminated RGBA preview array.");
                search = end;
            }
            return output.ToString();
        }

        private static string DeriveSibling(string path, string siblingName)
        {
            if (string.IsNullOrEmpty(path)) return siblingName;
            int idx = path.LastIndexOf('/');
            int idx2 = path.LastIndexOf('\\');
            int sep = Math.Max(idx, idx2);
            if (sep < 0) return siblingName;
            return path.Substring(0, sep + 1) + siblingName;
        }

        public void Play() { _playing = true; }
        public void Stop() { _playing = false; }
        public void SetFrame(int index)
        {
            if (index < 0 || index >= _frames.Count) return;
            _frameIndex = index;
            _sr.sprite = _frames[index];
        }

        [Serializable] private class Wrapper<T> { public List<T> items; }
    }
}
