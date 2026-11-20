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

        // The sheet is the Spriters Resource "Tower - OpenBag" layout (Resources/Tower/SummoningSheet),
        // either at its native 256 px or upscaled to 512 px; rectangles are authored for 512.
        private float SummoningSheetScale
        {
            get
            {
                if (!_summoningSheet) _summoningSheet = AssetMode.Load<Texture2D>("Tower/SummoningSheet");
                if (!_summoningSheet || _summoningSheet.width != _summoningSheet.height ||
                    (_summoningSheet.width != 256 && _summoningSheet.width != 512)) return 0f;
                return _summoningSheet.width / 512f;
            }
        }

        private Sprite[] FamiliarLightFrames()
        {
            float scale = SummoningSheetScale;
            if (scale <= 0f) return null;
            if (_summoningFrames?.Length == 4) return _summoningFrames;

            var frames = new Sprite[4];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Sprite.Create(_summoningSheet,
                    new Rect((256 + i * 64) * scale, _summoningSheet.height - 338 * scale, 64 * scale, 178 * scale),
                    new Vector2(27f / 64f, 6f / 178f), 96f * scale, 0, SpriteMeshType.FullRect);
            _summoningFrames = frames;
            return _summoningFrames;
        }

        private Sprite _storedFamiliarBeamSprite;

        // Retail's Monster Hut shows a stored familiar (or egg) glowing under a plain light ray, with no
        // bag underneath it. frames[^1] from FamiliarLightFrames is the bag-pulling animation's last frame,
        // which has the brown pot/bag baked into its lower third (correct for PresentFamiliarSummoning, the
        // only other place that uses those frames) -- using it here left a floating pot under every stored
        // familiar and egg. Crop just the beam, stopping at the frame's own transparent gap above the pot.
        private Sprite StoredFamiliarBeamSprite()
        {
            float scale = SummoningSheetScale;
            if (scale <= 0f) return null;
            if (_storedFamiliarBeamSprite) return _storedFamiliarBeamSprite;
            const int frame = 3; // fully grown beam, same frame FamiliarLightFrames used for this.
            var rect = new Rect((256 + frame * 64) * scale, _summoningSheet.height - 314 * scale, 64 * scale, 90 * scale);
            _storedFamiliarBeamSprite = Sprite.Create(_summoningSheet, rect, new Vector2(27f / 64f, 0.05f),
                96f * scale, 0, SpriteMeshType.FullRect);
            return _storedFamiliarBeamSprite;
        }

        private void AddStoredFamiliarLight(GameObject target)
        {
            var beamSprite = StoredFamiliarBeamSprite();
            if (!target || !beamSprite) return;

            var beam = new GameObject("Stored familiar light");
            beam.transform.SetParent(target.transform, false);
            float parentScale = target.transform.localScale.x;
            if (parentScale != 0f) beam.transform.localScale = Vector3.one / parentScale;
            var sprite = beam.AddComponent<SpriteRenderer>();
            sprite.sprite = beamSprite;
            sprite.sortingOrder = -2;
            beam.AddComponent<AzureDreamsReimagined.Rendering.CameraFacingSprite>();
        }

        private IEnumerator PresentFamiliarSummoning(GameObject familiar, bool pullFromBag)
        {
            if (!familiar) yield break;
            var summoningFrames = FamiliarLightFrames();
            if (summoningFrames == null)
            {
                if (pullFromBag) PlayRetailSound(RetailSfx.Summon);
                yield break;
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
                    sprite.sprite = summoningFrames[Mathf.Min((int)(elapsed / 0.12f), 3)];
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
            float scale = Mathf.Max(0.5f, SummoningSheetScale);
            float height = bottom - top;
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Sprite.Create(_summoningSheet,
                    new Rect(i * 64 * scale, _summoningSheet.height - bottom * scale, 64 * scale, height * scale),
                    new Vector2(26f / 64f, 3f / height), 96f * scale, 0, SpriteMeshType.FullRect);
            return frames;
        }
    }
}
