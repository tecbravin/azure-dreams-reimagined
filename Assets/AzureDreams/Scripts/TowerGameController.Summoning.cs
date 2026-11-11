using System.Collections;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        [SerializeField] private Texture2D _summoningSheet;
        private Sprite[] _summoningFrames;
        private Sprite[] _pullBagFrames;
        private bool _pullingFamiliarFromBag;

        private IEnumerator PresentFamiliarSummoning(GameObject familiar, bool pullFromBag)
        {
            if (!familiar) yield break;
            if (!_summoningSheet || _summoningSheet.width < 256 || _summoningSheet.height < 170)
            {
                if (pullFromBag) PlayRetailSound(RetailSfx.Summon);
                yield break;
            }
            if (_summoningFrames?.Length != 4)
            {
                var frames = new Sprite[4];
                for (int i = 0; i < frames.Length; i++)
                    frames[i] = Sprite.Create(_summoningSheet,
                        new Rect(128 + i * 32, _summoningSheet.height - 170, 32, 90),
                        new Vector2(0.5f, 0.1f), 48f, 0, SpriteMeshType.FullRect);
                _summoningFrames = frames;
            }
            // Delay until the familiar's renderer has been created by Awake.
            var effect = new GameObject("Familiar summoning light");
            effect.transform.SetParent(familiar.transform, false);
            effect.transform.localPosition = Vector3.down * 0.45f;
            var sprite = effect.AddComponent<SpriteRenderer>();
            effect.AddComponent<AzureDreamsReimagined.Rendering.CameraFacingSprite>();
            var bodyRenderers = familiar.GetComponentsInChildren<Renderer>();
            var wasEnabled = new bool[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                var renderer = bodyRenderers[i];
                wasEnabled[i] = renderer.enabled;
                if (renderer != sprite) renderer.enabled = false;
            }
            var koh = pullFromBag ? _kohSprite : null;
            if (pullFromBag) _pullingFamiliarFromBag = true;
            try
            {
                if (koh)
                {
                    var frames = GetPullBagFrames();
                    bool flip = _towerFacing.x > 0;
                    for (float elapsed = 0f; elapsed < 0.64f && koh && familiar; elapsed += TowerAnimationDelta)
                    {
                        koh.SetBagPose(frames[Mathf.Clamp((int)(elapsed / 0.08f), 0, frames.Length - 1)], flip);
                        yield return null;
                    }
                    if (koh) koh.SetBagPose(null, false);
                }
                var lightFrames = _summoningFrames;
                if (familiar) PlayRetailSound(RetailSfx.Summon);
                for (float elapsed = 0f; elapsed < 0.6f && familiar && sprite; elapsed += TowerAnimationDelta)
                {
                    sprite.sprite = lightFrames[Mathf.Clamp(Mathf.FloorToInt(elapsed * 8f), 0, lightFrames.Length - 1)];
                    yield return null;
                }
            }
            finally
            {
                if (koh) koh.SetBagPose(null, false);
                for (int i = 0; i < bodyRenderers.Length; i++)
                    if (bodyRenderers[i] && bodyRenderers[i] != sprite) bodyRenderers[i].enabled = wasEnabled[i];
                if (effect) Destroy(effect);
                if (pullFromBag && ReferenceEquals(koh, _kohSprite)) _pullingFamiliarFromBag = false;
            }
        }

        private Sprite[] GetPullBagFrames()
        {
            if (_pullBagFrames?.Length != 8)
            {
                var frames = new Sprite[8];
                for (int i = 0; i < frames.Length; i++)
                    frames[i] = Sprite.Create(_summoningSheet,
                        new Rect(i * 32, _summoningSheet.height - 80, 32, 48),
                        new Vector2(0.375f, 1f / 48f), 48f, 0, SpriteMeshType.FullRect);
                _pullBagFrames = frames;
            }
            return _pullBagFrames;
        }
    }
}
