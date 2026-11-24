using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private static readonly string[] GalleryFolders =
            { "Reimagined/Portraits/Dialogue", "Portraits/Dialogue", "Portraits/Book", "Fanart" };
        private static readonly string[] GalleryTabs =
            { "Redesigned Portraits", "Portraits", "Monster book", "Fanart" };
        private string[][] _galleryFiles;
        private int _gallerySelection;
        private bool _galleryDownloading;
        private string _galleryDownloadStatus;

        private void DrawGalleryCollection(Rect panel)
        {
            _galleryFiles ??= ReadGalleryFiles();
            int tab = GUILayout.Toolbar(_galleryTab, GalleryTabs);
            if (tab != _galleryTab)
            {
                _galleryTab = tab;
                _gallerySelection = 0;
                _galleryDownloadStatus = null;
            }

            var files = _galleryFiles[tab];
            if (files.Length == 0)
            {
                GUILayout.Label(T("No images in this folder.", "Nenhuma imagem nesta pasta."), _bodyStyle);
                return;
            }

            _gallerySelection = Mathf.Clamp(_gallerySelection, 0, files.Length - 1);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("‹", GUILayout.Width(35f)))
                _gallerySelection = (_gallerySelection + files.Length - 1) % files.Length;
            GUILayout.Label($"{_gallerySelection + 1} / {files.Length}", GUILayout.Width(84f));
            if (GUILayout.Button("›", GUILayout.Width(35f)))
                _gallerySelection = (_gallerySelection + 1) % files.Length;
            string path = files[_gallerySelection];
            GUILayout.Label(Path.GetFileName(path), _bodyStyle);
            using (new GalleryButtonState(_galleryDownloading))
                if (GUILayout.Button(T("Download", "Baixar"), GUILayout.Width(105f)))
                    StartCoroutine(DownloadGalleryFile(path));
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_galleryDownloadStatus))
                GUILayout.Label(_galleryDownloadStatus, _bodyStyle);
            DrawTitleImage(StreamImage(path, exactPath: true), panel.width - 34f, panel.height - 133f, 1f);
        }

        private static string[][] ReadGalleryFiles()
        {
            var index = Resources.Load<TextAsset>("GalleryFiles");
            var indexed = (index ? index.text : string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return GalleryFolders.Select(folder =>
            {
                string directory = Path.Combine(Application.streamingAssetsPath, "Media", folder);
                if (Directory.Exists(directory))
                    return Directory.EnumerateFiles(directory).Where(path =>
                            Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg")
                        .Select(path => folder + "/" + Path.GetFileName(path))
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
                return indexed.Where(path => path.StartsWith(folder + "/", StringComparison.Ordinal)).ToArray();
            }).ToArray();
        }

        private IEnumerator DownloadGalleryFile(string path)
        {
            _galleryDownloading = true;
            _galleryDownloadStatus = null;
            using var request = UnityWebRequest.Get(StreamedMediaUrl(path));
            request.timeout = 30;
            yield return request.SendWebRequest();
            try
            {
                if (request.result != UnityWebRequest.Result.Success)
                    throw new IOException(request.error);
                string name = Path.GetFileName(path);
                byte[] bytes = request.downloadHandler.data;
#if UNITY_WEBGL && !UNITY_EDITOR
                AzureDownloadGallery(bytes, bytes.Length, name);
#else
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                Directory.CreateDirectory(folder);
                string output = Path.Combine(folder, name);
                for (int i = 1; File.Exists(output); i++)
                    output = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(name)} ({i}){Path.GetExtension(name)}");
                File.WriteAllBytes(output, bytes);
#endif
                _galleryDownloadStatus = T($"Downloaded {name}", $"Arquivo baixado: {name}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.LogWarning($"Gallery download failed: {ex.Message}", this);
                _galleryDownloadStatus = T("Download failed.", "Não foi possível baixar o arquivo.");
            }
            finally { _galleryDownloading = false; }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void AzureDownloadGallery(byte[] bytes, int length, string name);
#endif

        private readonly struct GalleryButtonState : IDisposable
        {
            private readonly bool _previous;
            public GalleryButtonState(bool busy)
            {
                _previous = GUI.enabled;
                GUI.enabled = _previous && !busy;
            }
            public void Dispose() => GUI.enabled = _previous;
        }
    }
}
