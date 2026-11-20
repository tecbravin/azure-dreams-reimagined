using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Reacts to the Classic / Reimagined choice (see <see cref="AssetMode"/>): drops cached art so it is
    /// reloaded from the right set and restarts the current music track in the selected version.
    /// </summary>
    public sealed partial class TowerGameController
    {
        private readonly Dictionary<string, Sprite> _reimaginedItemSprites = new();

        private void SubscribeAssetMode() => AssetMode.Changed += OnAssetModeChanged;

        private void UnsubscribeAssetMode() => AssetMode.Changed -= OnAssetModeChanged;

        private void OnAssetModeChanged()
        {
            ReleaseStreamedPortraits();
            ReleaseStreamedTextures();
            _familiarStatusTextures.Clear();
            ReleaseReimaginedItemSprites();
            ReloadLevelUpMusic();
            RestartAreaMusicForAssetMode();
        }

        private static string AssetModeLabel(ArtStyle style) => style == ArtStyle.Classic
            ? T("Classic", "Clássico")
            : T("Reimagined", "Reimagined");

        /// <summary>Redesigned item icon from Resources/Reimagined/Items/&lt;name&gt;.png, or null.</summary>
        private Sprite ReimaginedItemSprite(string name)
        {
            if (!AssetMode.IsReimagined || string.IsNullOrEmpty(name)) return null;
            if (_reimaginedItemSprites.TryGetValue(name, out var sprite)) return sprite;
            var texture = Resources.Load<Texture2D>(AssetMode.OverlayFolder + "/Items/" + name);
            sprite = texture
                ? Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                    texture.width, 0, SpriteMeshType.FullRect)
                : null;
            if (sprite) sprite.name = name;
            _reimaginedItemSprites[name] = sprite;
            return sprite;
        }

        private void ReleaseReimaginedItemSprites()
        {
            foreach (var sprite in _reimaginedItemSprites.Values)
                if (sprite) Destroy(sprite);
            _reimaginedItemSprites.Clear();
        }

        private void ReloadLevelUpMusic()
        {
            if (_levelUpMusicActive || !_levelUpMusic) return;
            Destroy(_levelUpMusic);
            _levelUpMusic = null;
            if (_area == Area.Tower) PrepareLevelUpMusic();
        }

        private void RestartAreaMusicForAssetMode()
        {
            string cue = _playingMusicCue;
            if (cue == null) return;
            _playingMusicCue = null;
            PlayAreaMusic(cue, _playingMusicLoop);
        }
    }
}
