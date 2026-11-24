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

        private Texture2D StreamImage(string path, bool exactPath = false)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string key = exactPath ? "gallery:" + path : path;
            if (_streamedPortraits.TryGetValue(key, out var portrait))
            {
                if (_portraitLru.Last?.Value != key)
                {
                    _portraitLru.Remove(key);
                    _portraitLru.AddLast(key);
                }
                return portrait;
            }

            if (_portraitRequests.Count < 2 && !_portraitRequests.ContainsKey(key) &&
                (!_portraitRetryAfter.TryGetValue(key, out float retry) || Time.unscaledTime >= retry))
            {
                // Classic has no original for redesigned-only images: show nothing instead of a broken request.
                string resolved = exactPath ? path : AssetMode.ResolveMedia(path);
                if (resolved == null) return null;
                var request = UnityWebRequestTexture.GetTexture(StreamedMediaUrl(resolved), nonReadable: true);
                request.timeout = 15;
                _portraitRequests.Add(key, request);
                StartCoroutine(LoadPortrait(key, request));
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

        // Cache generico (sem LRU) para texturas/sprites grandes usados raramente - tela de titulo,
        // prologo, icones de dialogo. Diferente de _streamedPortraits (que troca retrato a retrato e
        // pode descartar o mais antigo a qualquer momento), essas poucas imagens ficam na memoria ate
        // a cena/estilo mudar (ReleaseStreamedTextures), porque varias podem estar visiveis ao mesmo
        // tempo (ex.: tia + avo no flashback da chuva) e descartar uma no meio da cena a faria sumir.
        private readonly Dictionary<string, Texture2D> _streamedTextures = new();
        private readonly HashSet<string> _streamedTextureRequests = new();
        private readonly Dictionary<string, float> _streamedTextureRetryAfter = new();
        private readonly Dictionary<string, Sprite> _streamedSprites = new();

        private Texture2D StreamMediaTexture(string relativePath, FilterMode filterMode, TextureWrapMode wrapMode)
        {
            if (_streamedTextures.TryGetValue(relativePath, out var cached)) return cached;
            if (_streamedTextureRequests.Contains(relativePath) ||
                (_streamedTextureRetryAfter.TryGetValue(relativePath, out float retry) && Time.unscaledTime < retry))
                return null;
            // Classic pode nao ter um correspondente para um arquivo so-Reimagined: mostra nada em vez
            // de pedir um caminho que nao existe.
            // Callers pass the stem ("Title/Newborn"); the files on disk are PNGs.
            string file = Path.HasExtension(relativePath) ? relativePath : relativePath + ".png";
            string resolved = AssetMode.ResolveMedia(file);
            if (resolved == null) return null;
            _streamedTextureRequests.Add(relativePath);
            StartCoroutine(LoadMediaTexture(relativePath, resolved, filterMode, wrapMode));
            return null;
        }

        private IEnumerator LoadMediaTexture(string relativePath, string resolved, FilterMode filterMode, TextureWrapMode wrapMode)
        {
            var request = UnityWebRequestTexture.GetTexture(StreamedMediaUrl(resolved), nonReadable: true);
            request.timeout = 15;
            yield return request.SendWebRequest();
            _streamedTextureRequests.Remove(relativePath);
            if (request.result == UnityWebRequest.Result.Success)
            {
                var texture = DownloadHandlerTexture.GetContent(request);
                texture.filterMode = filterMode;
                texture.wrapMode = wrapMode;
                _streamedTextures[relativePath] = texture;
                _streamedTextureRetryAfter.Remove(relativePath);
            }
            else
            {
                _streamedTextureRetryAfter[relativePath] = Time.unscaledTime + 20f;
                Debug.LogWarning($"Media texture '{relativePath}' could not load: {request.error}", this);
            }
            request.Dispose();
        }

        // pixelsPerUnit deve bater com o spritePixelsToUnits que o PNG original tinha configurado no
        // importer (conferido em cada .meta antes de mover o arquivo), senao o sprite aparece com a
        // escala errada em cena. pivot sempre 0.5/0.5 para esses arquivos (idem ao original).
        private Sprite StreamMediaSprite(string relativePath, float pixelsPerUnit, FilterMode filterMode = FilterMode.Point)
        {
            if (_streamedSprites.TryGetValue(relativePath, out var cachedSprite)) return cachedSprite;
            var texture = StreamMediaTexture(relativePath, filterMode, TextureWrapMode.Clamp);
            if (!texture) return null;
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                Vector2.one * 0.5f, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            _streamedSprites[relativePath] = sprite;
            return sprite;
        }

        private void ReleaseStreamedTextures()
        {
            foreach (var texture in _streamedTextures.Values) if (texture) Destroy(texture);
            _streamedTextures.Clear();
            _streamedSprites.Clear();
            _streamedTextureRequests.Clear();
            _streamedTextureRetryAfter.Clear();
        }
    }
}
