using UnityEngine;
using System.Collections.Generic;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#else
using System.Collections;
using UnityEngine.Networking;
#endif

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string SelfiMusicCue = "SelfiTheme.mp3";
        // These cues are supplied by Redesigned assets/music.  Keeping this
        // mapping at the playback boundary lets all existing location and
        // event rules continue to use their original cue names.
        // The redesigned M4A files contain Opus-in-MP4.  FMOD cannot decode
        // that container, so the matching Vorbis files below are generated
        // from the same source tracks for every runtime, including WebGL.
        private static readonly HashSet<string> RedesignedOggMusicCues = new()
        {
            "AngelSong", "ArmShop", "SelfiTheme", "BeldoDuel", "Casino", "CherrlTheme",
            "DanceAtPub", "EntranceOfComedians", "ExitComedians", "GhoshTheme", "HeroFinale",
            "KohHouseTheme", "MiaTheme", "MonsterRaceCourse", "MonsterRacing", "Playroom",
            "SwimmingPool", "Temple1", "Theatre", "Tower1", "Tower2", "Tower3", "Tower4",
            "Tower5", "Tower6", "Tower7", "Tower8", "TowerFinale", "VeiledGenuine", "VivianTheme"
        };
        private static readonly HashSet<string> RedesignedMp3MusicCues = new() { "NicoTheme", "Town" };
        private AudioSource _backgroundMusic;
        private AudioSource _levelUpMusic;
        private bool _levelUpMusicActive;
        private string _playingMusicCue;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void AzureBgmPlay(string url, float volume, int loop);
        [DllImport("__Internal")] private static extern void AzureBgmSetVolume(float volume);
        [DllImport("__Internal")] private static extern void AzureBgmPause(int pause);
        [DllImport("__Internal")] private static extern void AzureBgmStop();
#else
        private Coroutine _musicLoad;
        private UnityWebRequest _musicRequest;
#endif

        private static string TowerMusicCue(int floor)
        {
            if (floor >= TowerFloorRules.BeldoFloor) return "VeiledGenuine.mp3";
            // Retail selects the normal music bank using integer division by five.
            return $"Tower{Mathf.Clamp(floor / 5, 0, 7) + 1}";
        }

        private static string MusicFileName(string cue)
        {
            string baseCue = cue.EndsWith(".mp3", System.StringComparison.Ordinal)
                ? cue[..^4] : cue;
            if (RedesignedOggMusicCues.Contains(baseCue)) return baseCue + ".redesigned.ogg";
            if (RedesignedMp3MusicCues.Contains(baseCue)) return baseCue + ".mp3";
            return cue.EndsWith(".mp3", System.StringComparison.Ordinal) ? cue : cue + ".ogg";
        }

        private static AudioType MusicAudioType(string file) =>
            file.EndsWith(".mp3", System.StringComparison.OrdinalIgnoreCase) ? AudioType.MPEG :
            AudioType.OGGVORBIS;

        private void PlayAreaMusic(string cue, bool loop = true)
        {
            if (_playingMusicCue == cue) return;
            StopLevelUpMusic(resumeBackground: false);
            _playingMusicCue = cue;
            string file = MusicFileName(cue);
            string url = StreamedMediaUrl($"Music/{file}");
#if UNITY_WEBGL && !UNITY_EDITOR
            AzureBgmPlay(url, DefaultMusicVolume * _bgmVolume, loop ? 1 : 0);
#else
            CancelMusicLoad();
            ReleaseMusicClip();
            _musicLoad = StartCoroutine(LoadAreaMusic(url, cue, MusicAudioType(file), loop));
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private IEnumerator LoadAreaMusic(string url, string cue, AudioType audioType, bool loop)
        {
            var request = UnityWebRequestMultimedia.GetAudioClip(url, audioType);
            _musicRequest = request;
            request.timeout = 20;
            ((DownloadHandlerAudioClip)request.downloadHandler).streamAudio = true;
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                if (!_backgroundMusic)
                {
                    _backgroundMusic = gameObject.AddComponent<AudioSource>();
                    _backgroundMusic.playOnAwake = false;
                    _backgroundMusic.loop = loop;
                    _backgroundMusic.spatialBlend = 0f;
                }
                _backgroundMusic.volume = DefaultMusicVolume * _bgmVolume;
                _backgroundMusic.loop = loop;
                _backgroundMusic.clip = DownloadHandlerAudioClip.GetContent(request);
                _backgroundMusic.Play();
                if (_levelUpMusicActive || _fusionPlaying) _backgroundMusic.Pause();
            }
            else
            {
                Debug.LogWarning($"Background music '{cue}' could not load: {request.error}", this);
                _playingMusicCue = null;
            }
            request.Dispose();
            _musicRequest = null;
            _musicLoad = null;
        }

        private void CancelMusicLoad()
        {
            if (_musicLoad != null) StopCoroutine(_musicLoad);
            _musicLoad = null;
            if (_musicRequest == null) return;
            _musicRequest.Abort();
            _musicRequest.Dispose();
            _musicRequest = null;
        }

        private void ReleaseMusicClip()
        {
            if (!_backgroundMusic) return;
            var clip = _backgroundMusic.clip;
            _backgroundMusic.Stop();
            _backgroundMusic.clip = null;
            if (clip) Destroy(clip);
        }
#endif

        private void SetAreaMusicPaused(bool paused)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            AzureBgmPause(paused ? 1 : 0);
#else
            if (!_backgroundMusic) return;
            if (paused) _backgroundMusic.Pause();
            else _backgroundMusic.UnPause();
#endif
        }

        private void SetAreaMusicVolume()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            AzureBgmSetVolume(DefaultMusicVolume * _bgmVolume);
#else
            if (_backgroundMusic) _backgroundMusic.volume = DefaultMusicVolume * _bgmVolume;
#endif
        }

        private void ReleaseAreaMusic()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_playingMusicCue != null) AzureBgmStop();
#else
            CancelMusicLoad();
            ReleaseMusicClip();
#endif
            _playingMusicCue = null;
        }

        private void PrepareLevelUpMusic()
        {
            if (_levelUpMusic) return;
            var clip = Resources.Load<AudioClip>("Music/LevelUp");
            if (!clip) return;
            _levelUpMusic = gameObject.AddComponent<AudioSource>();
            _levelUpMusic.playOnAwake = false;
            _levelUpMusic.spatialBlend = 0f;
            _levelUpMusic.clip = clip;
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        }

        private void PlayLevelUpMusic()
        {
            if (_area != Area.Tower || _levelUpMusicActive) return;
            if (!_levelUpMusic) PrepareLevelUpMusic();
            if (!_levelUpMusic || _levelUpMusic.clip.loadState != AudioDataLoadState.Loaded) return;
            _levelUpMusic.volume = DefaultMusicVolume * _bgmVolume;
            SetAreaMusicPaused(true);
            _levelUpMusicActive = true;
            _levelUpMusic.Play();
        }

        private void TickLevelUpMusic()
        {
            if (_levelUpMusicActive && !AudioListener.pause && !_fusionPlaying &&
                (!_levelUpMusic || !_levelUpMusic.isPlaying))
                StopLevelUpMusic(resumeBackground: true);
        }

        private void StopLevelUpMusic(bool resumeBackground)
        {
            if (!_levelUpMusicActive) return;
            _levelUpMusicActive = false;
            if (_levelUpMusic) _levelUpMusic.Stop();
            if (resumeBackground && !_fusionPlaying) SetAreaMusicPaused(false);
        }
    }
}

