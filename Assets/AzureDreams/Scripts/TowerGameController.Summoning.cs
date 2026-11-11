using System.Collections;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        [SerializeField] private Texture2D _summoningSheet;
        private Sprite[] _summoningFrames;
        private Sprite[] _pullBagFrames;
        private Sprite[] _closeBagFrames;
        private bool _pullingFamiliarFromBag;

        private IEnumerator PresentFamiliarSummoning(GameObject familiar, bool pullFromBag)
        {
            if (!familiar) yield break;
            if (!_summoningSheet || _summoningSheet.width != 512 || _summoningSheet.height != 512)
            {
                if (pullFromBag) PlayRetailSound(RetailSfx.Summon);
                yield break;
            }
            if (_summoningFrames?.Length != 4)
            {
                var frames = new Sprite[4];
                for (int i = 0; i < frames.Length; i++)
                    frames[i] = Sprite.Create(_summoningSheet,
                        new Rect(256 + i * 64, _summoningSheet.height - 338, 64, 178),
                        new Vector2(27f / 64f, 6f / 178f), 96f, 0, SpriteMeshType.FullRect);
                _summoningFrames = frames;
            }
            var effect = new GameObject("Familiar summoning light");
            effect.transform.SetParent(familiar.transform, false);
            effect.transform.position = new Vector3(familiar.transform.position.x,
                TowerGroundY(new Vector2Int(Mathf.RoundToInt(familiar.transform.position.x),
                    Mathf.RoundToInt(familiar.transform.position.z))), familiar.transform.position.z);
            var sprite = effect.AddComponent<SpriteRenderer>();
            effect.AddComponent<AzureDreamsReimagined.Rendering.CameraFacingSprite>();
            sprite.sortingOrder = 2;
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
                bool flip = false;
                if (koh)
                {
                    _pullBagFrames ??= CreateBagPoseFrames(70, 158);
                    _closeBagFrames ??= CreateBagPoseFrames(422, 512);
                    var right = _gameCamera ? _gameCamera.transform.right : Vector3.right;
                    flip = Vector3.Dot(right, familiar.transform.position - koh.transform.position) > 0f;
                    for (float elapsed = 0f; elapsed < 0.56f && koh && familiar; elapsed += TowerAnimationDelta)
                    {
                        koh.SetBagPose(_pullBagFrames[Mathf.Min((int)(elapsed / 0.07f), 7)], flip);
                        yield return null;
                    }
                }
                if (!familiar) yield break;
                PlayRetailSound(RetailSfx.Summon);
                for (float elapsed = 0f; elapsed < 0.48f && familiar && sprite; elapsed += TowerAnimationDelta)
                {
                    sprite.sprite = _summoningFrames[Mathf.Min((int)(elapsed / 0.12f), 3)];
                    yield return null;
                }
                if (sprite) sprite.enabled = false;
                RestoreFamiliarRenderers();
                if (koh)
                    for (float elapsed = 0f; elapsed < 0.48f && koh && familiar; elapsed += TowerAnimationDelta)
                    {
                        koh.SetBagPose(_closeBagFrames[Mathf.Min((int)(elapsed / 0.06f), 7)], flip);
                        yield return null;
                    }
            }
            finally
            {
                if (koh) koh.SetBagPose(null, false);
                RestoreFamiliarRenderers();
                if (effect) Destroy(effect);
                if (pullFromBag && ReferenceEquals(koh, _kohSprite)) _pullingFamiliarFromBag = false;
            }

            void RestoreFamiliarRenderers()
            {
                for (int i = 0; i < bodyRenderers.Length; i++)
                    if (bodyRenderers[i] && bodyRenderers[i] != sprite) bodyRenderers[i].enabled = wasEnabled[i];
            }
        }

        private Sprite[] CreateBagPoseFrames(int top, int bottom)
        {
            var frames = new Sprite[8];
            float height = bottom - top;
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Sprite.Create(_summoningSheet,
                    new Rect(i * 64, _summoningSheet.height - bottom, 64, height),
                    new Vector2(26f / 64f, 3f / height), 96f, 0, SpriteMeshType.FullRect);
            return frames;
        }
    }
}
