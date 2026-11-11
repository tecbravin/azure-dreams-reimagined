using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const int PortraitCacheSize = 8;
        private const long ImageCacheBytes = 32L * 1024 * 1024;
        private long _streamedImageBytes;
        private readonly Dictionary<string, Texture2D> _streamedPortraits = new();
        private readonly LinkedList<string> _portraitLru = new();
        private readonly Dictionary<string, float> _portraitRetryAfter = new();
        private readonly Dictionary<string, UnityWebRequest> _portraitRequests = new();

        private static string StreamedMediaUrl(string relativePath)
        {
            string root = Application.streamingAssetsPath.TrimEnd('/');
            if (root.Contains("://")) return root + "/Media/" + relativePath;
            return new System.Uri(Path.Combine(root, "Media", relativePath)).AbsoluteUri;
        }

        private Texture2D StreamPortrait(string path) => string.IsNullOrEmpty(path) ? null : StreamImage("Portraits/" + path + ".png");

        private Texture2D StreamImage(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (_streamedPortraits.TryGetValue(path, out var portrait))
            {
                if (_portraitLru.Last?.Value != path)
                {
                    _portraitLru.Remove(path);
                    _portraitLru.AddLast(path);
                }
                return portrait;
            }

            if (_portraitRequests.Count < 2 && !_portraitRequests.ContainsKey(path) &&
                (!_portraitRetryAfter.TryGetValue(path, out float retry) || Time.unscaledTime >= retry))
            {
                var request = UnityWebRequestTexture.GetTexture(StreamedMediaUrl(path), nonReadable: true);
                request.timeout = 15;
                _portraitRequests.Add(path, request);
                StartCoroutine(LoadPortrait(path, request));
            }
            return null;
        }

        private IEnumerator LoadPortrait(string path, UnityWebRequest request)
        {
            yield return request.SendWebRequest();
            _portraitRequests.Remove(path);
            if (request.result == UnityWebRequest.Result.Success)
            {
                var texture = DownloadHandlerTexture.GetContent(request);
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                _streamedPortraits[path] = texture;
                _portraitLru.AddLast(path);
                _streamedImageBytes += TextureBytes(texture);
                _portraitRetryAfter.Remove(path);
                while (_portraitLru.Count > 1 &&
                    (_portraitLru.Count > PortraitCacheSize || _streamedImageBytes > ImageCacheBytes))
                {
                    string oldest = _portraitLru.First.Value;
                    _portraitLru.RemoveFirst();
                    var stale = _streamedPortraits[oldest];
                    _streamedImageBytes -= TextureBytes(stale);
                    Destroy(stale);
                    _streamedPortraits.Remove(oldest);
                }
            }
            else
            {
                _portraitRetryAfter[path] = Time.unscaledTime + 20f;
                Debug.LogWarning($"Image '{path}' could not load: {request.error}", this);
            }
            request.Dispose();
        }

        private static long TextureBytes(Texture2D texture) => (long)texture.width * texture.height * 4;

        private void ReleaseStreamedPortraits()
        {
            foreach (var request in _portraitRequests.Values) { request.Abort(); request.Dispose(); }
            foreach (var texture in _streamedPortraits.Values) if (texture) Destroy(texture);
            _portraitRequests.Clear();
            _streamedPortraits.Clear();
            _portraitLru.Clear();
            _portraitRetryAfter.Clear();
            _streamedImageBytes = 0;
        }
    }
}
