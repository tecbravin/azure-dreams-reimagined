using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string PoolSlideObjectId = "pool_slide";
        private static readonly Vector3 PoolSlideFoot = new(34.5f, 1f, 26.5f);
        private static readonly Vector3 PoolSlideLanding = new(22.5f, .75f, 24.5f);
        private const float PoolSlideClimbEnd = 1.55f, PoolSlideLaunchTime = 1.97f;
        private const float PoolSlideLandingTime = 3.77f;
        private Coroutine _poolSlideRoutine;
        private int _poolSlideVersion;
        private static readonly Vector2[] PoolSlideProfile =
        {
            new(26f,1.625f), new(27f,1.25f), new(28f,1.25f), new(29f,1.625f),
            new(29.75f,2.375f), new(30.25f,3.015625f), new(31f,4f), new(32f,4f),
            new(32.5f,3.875f), new(33f,3.5f), new(33.625f,2.875f), new(34f,2f), new(34.5f,1f)
        };

        private void SetupPoolSlide()
        {
            var entrance = new GameObject("Pool slide entrance");
            entrance.transform.SetParent(transform, false);
            entrance.transform.position = PoolSlideFoot;
            var action = entrance.AddComponent<TownObject>();
            action.objectId = PoolSlideObjectId;
            action.message = T("Ride the slide", "Usar o escorregador");
            action.interactionRadius = 1.6f;
            action.persistInteraction = false;
            _townObjects.Add(action);
            _generated.Add(entrance);
        }

        private static float PoolSlideHeight(float x)
        {
            for (int i = 1; i < PoolSlideProfile.Length; i++)
                if (x <= PoolSlideProfile[i].x)
                    return Mathf.Lerp(PoolSlideProfile[i-1].y, PoolSlideProfile[i].y,
                        Mathf.InverseLerp(PoolSlideProfile[i-1].x, PoolSlideProfile[i].x, x));
            return PoolSlideProfile[PoolSlideProfile.Length-1].y;
        }

        private static Vector3 PoolSlidePosition(Vector3 start, Vector3 landing, float elapsed)
        {
            if (elapsed < .25f) return Vector3.Lerp(start, PoolSlideFoot, Mathf.SmoothStep(0,1,elapsed/.25f));
            if (elapsed < PoolSlideClimbEnd)
            {
                float x = Mathf.Lerp(34.5f,31.5f,Mathf.InverseLerp(.25f,PoolSlideClimbEnd,elapsed));
                return new Vector3(x,PoolSlideHeight(x),26.5f);
            }
            if (elapsed < PoolSlideLaunchTime)
            {
                float x = Mathf.Lerp(31.5f,26f,Mathf.Pow(Mathf.InverseLerp(PoolSlideClimbEnd,PoolSlideLaunchTime,elapsed),1.3f));
                return new Vector3(x,PoolSlideHeight(x),26.5f);
            }
            float flight = Mathf.InverseLerp(PoolSlideLaunchTime,PoolSlideLandingTime,elapsed);
            var point = Vector3.Lerp(new Vector3(26f,1.625f,26.5f),landing,flight);
            point.y += 40f * flight * (1f-flight);
            return point;
        }

        private void BeginPoolSlide()
        {
            if (!InRestoredPool || _poolSceneAnimating || _poolSlideRoutine != null) return;
            if (!_poolKohWearingSwimsuit)
            {
                _message = T("Change into swimwear before using the slide.", "Troque para a roupa de banho antes de usar o escorregador.");
                return;
            }
            if (_carriedItem != null || _heldTownPot)
            {
                _message = T("Put down what you are holding first.", "Coloque no chão o que está segurando primeiro.");
                return;
            }
            if (!TryFindPoolSlideLanding(out var landing))
            {
                _message = T("The bottom of the slide is blocked.", "A saída do escorregador está bloqueada.");
                return;
            }
            _poolSceneAnimating = true;
            _townMomentum = Vector2.zero;
            _townJumpHeight = _townJumpVelocity = 0f;
            _poolSlideRoutine = StartCoroutine(PlayPoolSlide(landing));
        }

        private bool TryFindPoolSlideLanding(out Vector3 landing)
        {
            for (int radius = 0; radius <= 4; radius++)
            for (int z = -radius; z <= radius; z++)
            for (int x = -radius; x <= radius; x++)
            {
                if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != radius) continue;
                var candidate = PoolSlideLanding + new Vector3(x * .5f, 0f, z * .5f);
                if (!_interior.TryGetPoolWaterHeight(candidate, out float waterHeight)) continue;
                candidate.y = TownTerrainHeight(candidate);
                if (Mathf.Abs(candidate.y - waterHeight) > TownGroundTolerance ||
                    !TownPositionOpen(candidate, candidate.y)) continue;
                landing = candidate;
                return true;
            }
            landing = default;
            return false;
        }

        private IEnumerator PlayPoolSlide(Vector3 landing)
        {
            int version = _poolSlideVersion;
            var room = _interior;
            var actor = _player;
            var start = actor.transform.position - Vector3.up * TownActorFootOffset;
            bool launched = false, landed = false;
            try
            {
                for (float elapsed = 0f; ; elapsed += Mathf.Min(Time.deltaTime, .1f))
                {
                    if (!actor || _player != actor || _interior != room || version != _poolSlideVersion) yield break;
                    if (elapsed >= PoolSlideLandingTime)
                    {
                        if (TryFindPoolSlideLanding(out var openLanding))
                        {
                            landing = openLanding;
                            landed = true;
                        }
                        var feet = landed ? landing : start;
                        actor.transform.position = feet + Vector3.up * TownActorFootOffset;
                        _playerCell = new Vector2Int(Mathf.RoundToInt(feet.x), Mathf.RoundToInt(feet.z));
                        break;
                    }

                    var point = PoolSlidePosition(start, landing, elapsed);
                    var before = actor.transform.position;
                    actor.transform.position = point + Vector3.up * TownActorFootOffset;
                    _playerCell = new Vector2Int(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.z));
                    _townFacing = Vector2.left;
                    _kohSprite?.SetFacing(_townFacing);
                    _kohSprite?.SetAirborne(elapsed >= PoolSlideLaunchTime);
                    _playerLocomotion?.Present(actor.transform.position - before, elapsed * 3f, elapsed < PoolSlideClimbEnd);
                    if (!launched && elapsed >= PoolSlideLaunchTime)
                    {
                        launched = true;
                        PlayRetailSound(RetailSfx.Jump);
                    }
                    yield return null;
                }
            }
            finally
            {
                if (version == _poolSlideVersion)
                {
                    _poolSlideRoutine = null;
                    _poolSceneAnimating = false;
                    _townMomentum = Vector2.zero;
                    _townJumpHeight = _townJumpVelocity = 0f;
                    if (actor && _player == actor && _interior == room)
                    {
                        _kohSprite?.PresentDining(false);
                        _kohSprite?.SetAirborne(false);
                        _playerLocomotion?.Present(Vector3.zero, 0f, false);
                        UpdateKohPoolSwimming(actor.transform.position);
                    }
                }
            }

            // Splash continues on its own after control has returned.
            if (landed) StartCoroutine(PlayPoolSlideSplash(version, landing));
        }

        private IEnumerator PlayPoolSlideSplash(int version, Vector3 landing)
        {
            var objects = new List<GameObject>();
            var drops = new SpriteRenderer[12];
            try
            {
                for (int i=0;i<drops.Length;i++)
                {
                    drops[i]=SpawnFusionGlow(landing,.18f,Color.clear,objects);
                    drops[i].gameObject.layer=_player.layer;
                    _generated.Add(drops[i].gameObject);
                }
                for(float elapsed=0;elapsed<.65f;elapsed+=Time.deltaTime)
                {
                    if(version!=_poolSlideVersion || !_player)yield break;
                    for(int i=0;i<drops.Length;i++)
                    {
                        float angle=i*Mathf.PI*2/drops.Length;
                        drops[i].transform.position=landing+new Vector3(Mathf.Cos(angle)*elapsed*1.8f,
                            Mathf.Max(.02f,(2.3f+(i%3)*.35f)*elapsed-4f*elapsed*elapsed),Mathf.Sin(angle)*elapsed*1.8f);
                        drops[i].color=new Color(.65f,.9f,1f,1f-elapsed/.65f);
                    }
                    yield return null;
                }
            }
            finally { foreach(var item in objects)if(item)RemoveGenerated(item); }
        }

        private void ResetPoolSlide()
        {
            _poolSlideVersion++;
            if(_poolSlideRoutine!=null)
            {
                StopCoroutine(_poolSlideRoutine);
                _poolSceneAnimating=false;
            }
            _poolSlideRoutine=null;
            _kohSprite?.PresentDining(false);
            _kohSprite?.SetAirborne(false);
        }
    }
}
