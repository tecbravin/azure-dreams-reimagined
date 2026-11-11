using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        // Values are SD_Call selectors in the original PS1 game.
        private enum RetailSfx
        {
            Cursor = 0x502,
            Confirm = 0x503,
            Pickup = 0x505,
            Equip = 0x508,
            Eat = 0x509,
            Cancel = 0x515,
            CoinPickup = 0x516,
            FeedFamiliar = 0x51E,
            Hit = 0x601,
            WeaponSwing = 0x605,
            Punch = 0x606,
            Rumble = 0x608,
            Trap = 0x611,
            FallingWeight = 0x612,
            Frog = 0x613,
            Restore = 0x702,
            Summon = 0x704,
            ReturnMagic = 0x802,
            MonsterAttack = 0x804,
            MonsterDefeat = 0x805,
            Spell = 0x816,
            TerrainShift = 0x818
        }

        private AudioSource _gameplayAudio;
        private readonly Dictionary<RetailSfx, AudioClip> _retailSounds = new();
        private readonly Dictionary<RetailSfx, int> _retailSoundFrames = new();

        private void PreloadCommonSounds()
        {
            foreach (var sound in new[] { RetailSfx.Cursor, RetailSfx.Confirm, RetailSfx.Cancel,
                         RetailSfx.Hit, RetailSfx.WeaponSwing, RetailSfx.MonsterAttack, RetailSfx.Pickup, RetailSfx.CoinPickup })
            {
                var clip = Resources.Load<AudioClip>($"Sfx/Retail/{(int)sound:X3}");
                if (!clip) continue;
                clip.LoadAudioData();
                _retailSounds[sound] = clip;
            }
        }

        private void PlayRetailSound(RetailSfx sound, float gain = 0.65f)
        {
            if (!Application.isPlaying || _sfxVolume <= 0f ||
                (_retailSoundFrames.TryGetValue(sound, out int frame) && frame == Time.frameCount))
                return;

            if (!_retailSounds.TryGetValue(sound, out var clip))
                _retailSounds[sound] = clip = Resources.Load<AudioClip>($"Sfx/Retail/{(int)sound:X3}");
            if (!clip) return;
            _retailSoundFrames[sound] = Time.frameCount;
            PlayRetailClip(clip, gain);
        }

        private void PlayRetailClip(AudioClip clip, float gain)
        {
            if (!Application.isPlaying || _sfxVolume <= 0f) return;
            if (!_gameplayAudio)
            {
                _gameplayAudio = gameObject.AddComponent<AudioSource>();
                _gameplayAudio.playOnAwake = false;
                _gameplayAudio.spatialBlend = 0f;
                _gameplayAudio.volume = _sfxVolume;
            }
            _gameplayAudio.PlayOneShot(clip, gain);
        }
    }
}
