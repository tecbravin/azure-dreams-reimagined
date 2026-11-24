using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private GameObject _auraBike;
        private AuraBikeSpriteView _auraBikeView;
        private AuraBikeDust _auraBikeDust;
        private bool _ridingAuraBike;
        private float _auraBikeSpeed;
        private float _auraBikeSoundTime;
        private AudioSource _auraBikeAudio;

        private void PresentAuraBikeSound(float dt, float maximum, bool moving)
        {
            if (!Application.isPlaying) return;
            if (!moving || _sfxVolume <= 0f)
            {
                if (_auraBikeAudio) _auraBikeAudio.Stop();
                _auraBikeSoundTime=0f;
                return;
            }
            if (!_auraBikeAudio)
            {
                var clip=AssetMode.Load<AudioClip>("Sfx/Retail/60A");
                if (!clip) return;
                _auraBikeAudio=_auraBike.AddComponent<AudioSource>();
                _auraBikeAudio.playOnAwake=false;_auraBikeAudio.spatialBlend=0f;
                _auraBikeAudio.clip=clip;
            }
            _auraBikeAudio.volume=_sfxVolume*.38f;
            _auraBikeSoundTime-=dt;
            if (_auraBikeSoundTime>0f) return;
            // Retrigger a single voice, as on SPU; do not overlap 30 one-shots/sec.
            _auraBikeAudio.Stop();_auraBikeAudio.Play();
            _auraBikeSoundTime=(2f+7f*(1f-Mathf.Clamp01(Mathf.Abs(_auraBikeSpeed)/maximum)))/60f;
        }
        private Vector2 _auraBikeRecoil;
        private float _auraBikeBumpRemaining, _auraBikeBumpHeight, _auraBikeBumpLength=AuraBikeBumpDuration, _auraBikeBumpStrength;
        private const float AuraBikeBumpDuration=.32f;

        private void ResetAuraBikeBump()
        {
            _auraBikeRecoil=Vector2.zero;
            _auraBikeBumpRemaining=0f;
            _auraBikeBumpHeight=0f;
            _auraBikeBumpStrength=0f;
        }

        private void BumpAuraBike(Vector2 travel)
        {
            float impact=Mathf.Abs(_auraBikeSpeed);
            _auraBikeSpeed=0f;
            _auraBikeBumpStrength=Mathf.Clamp01(impact/8f);
            _auraBikeRecoil=-travel.normalized*Mathf.Min(impact*.35f,8f);
            _auraBikeBumpLength=Mathf.Lerp(.07f,AuraBikeBumpDuration,_auraBikeBumpStrength);
            _auraBikeBumpRemaining=_auraBikeBumpLength;
        }
        private Vector2 _auraBikeHeading = Vector2.down;

        private const string AuraBikeReturnParkingKey = "aura_bike.return_parking";

        private bool AuraBikeParkingOpen(Vector3 position)
        {
            if (float.IsNaN(position.x) || float.IsNaN(position.z) || float.IsInfinity(position.x) || float.IsInfinity(position.z) ||
                position.x < 1f || position.z < 1f || position.x > RetailTownSize - 2f || position.z > RetailTownSize - 2f) return false;
            return TownPositionOpen(position, TownTerrainHeight(position), radius: .55f);
        }

        private bool TryAuraBikeHomeParking(out Vector3 position)
        {
            var home = BuiltKohHome();
            float left = (home?.Bounds.xMin ?? 48f) - 1.5f;
            float front = home?.Entrance.y ?? 56f;
            // Search the left-hand path, leaving clearance from the building and its plants.
            for (int distance = 0; distance <= 8; distance++)
                for (int side = 0; side < (distance == 0 ? 1 : 2); side++)
                    for (int lane = 0; lane < 4; lane++)
                    {
                        position = new Vector3(left - lane * .75f, 0f, front + distance * (side == 0 ? 1f : -1f));
                        if (AuraBikeHomeGroundOpen(position)) return true;
                    }
            Rect bounds = home?.Bounds ?? Rect.MinMaxRect(48f, 44f, 63f, 58f);
            for (int distance = 1; distance <= 6; distance++)
                for (float x = bounds.xMin; x <= bounds.xMax; x += 1f)
                {
                    position = new Vector3(x, 0f, bounds.yMax + distance);
                    if (AuraBikeHomeGroundOpen(position)) return true;
                }
            position = default;
            return false;
        }

        private bool AuraBikeHomeGroundOpen(Vector3 position)
        {
            const float radius = .55f;
            Rect bounds = BuiltKohHome()?.Bounds ?? Rect.MinMaxRect(48f, 44f, 63f, 58f);
            if (position.x > bounds.xMin - radius && position.x < bounds.xMax + radius &&
                position.z > bounds.yMin - radius && position.z < bounds.yMax + radius) return false;
            return AuraBikeParkingOpen(position) &&
                float.IsNegativeInfinity(HomeRoofGround(position)) &&
                float.IsNegativeInfinity(TownObstacleTop(position, radius));
        }

        private void EnsureAuraBike()
        {
            if (_interior || !UsingRetailTown || GeneralStoreOwned("aura_bike") == 0) return;
            if (_auraBike) return;
            _ridingAuraBike = false;
            _auraBikeSpeed = 0f;
            ResetAuraBikeBump();
            Vector3 position = new(TownSaveVariables.GetFloat("aura_bike.x"), 0f, TownSaveVariables.GetFloat("aura_bike.z"));
            Rect homeBounds = BuiltKohHome()?.Bounds ?? Rect.MinMaxRect(48f, 44f, 63f, 58f);
            bool relocate = TownSaveVariables.GetBool(AuraBikeReturnParkingKey) || !TownSaveVariables.HasValue("aura_bike.x") || !TownSaveVariables.HasValue("aura_bike.z") ||
                homeBounds.Contains(new Vector2(position.x, position.z)) || !AuraBikeParkingOpen(position);
            if (relocate)
            {
                if (!TryAuraBikeHomeParking(out position)) return;
                TownSaveVariables.SetFloat("aura_bike.x", position.x);
                TownSaveVariables.SetFloat("aura_bike.z", position.z);
                TownSaveVariables.SetBool(AuraBikeReturnParkingKey, false);
                TownSaveVariables.Save();
            }
            position.y = TownTerrainHeight(position) + .04f;
            _auraBike = new GameObject("Purchased Aura Bike");
            _auraBike.transform.SetParent(transform, false);
            _auraBike.transform.position = position;
            _generated.Add(_auraBike);
            _auraBikeView = _auraBike.AddComponent<AuraBikeSpriteView>();
            _auraBikeView.Initialize(GeneralStoreOwned("booster") > 0);
            var dust=new GameObject("Aura Bike acceleration dust");
            dust.transform.SetParent(transform,false);
            _generated.Add(dust);
            _auraBikeDust=dust.AddComponent<AuraBikeDust>();
            _auraBikeDust.Initialize();
        }

        private bool SummonAuraBike()
        {
            if(_area!=Area.Town || _interior || !_player || _ridingAuraBike || GeneralStoreOwned("aura_bike")==0) return false;
            EnsureAuraBike();
            if(!_auraBike) return false;
            var facing=_townFacing.sqrMagnitude>.001f ? _townFacing.normalized : Vector2.down;
            var position=_player.transform.position+new Vector3(facing.x,0f,facing.y)*1.25f;
            if(!AuraBikeParkingOpen(position)) position=_player.transform.position;
            position.y=TownTerrainHeight(position)+.04f;
            _auraBike.transform.position=position;
            _auraBikeHeading=facing;
            _auraBikeSpeed=0f;
            ResetAuraBikeBump();
            _auraBikeView.Present(facing,false);
            TownSaveVariables.SetFloat("aura_bike.x",position.x);
            TownSaveVariables.SetFloat("aura_bike.z",position.z);
            TownSaveVariables.Save();
            return true;
        }

        private bool TryAuraBikeInteraction()
        {
            EnsureAuraBike();
            if (!_auraBike || _interior) return false;
            if (_ridingAuraBike)
            {
                if(_townJumpHeight>TownGroundTolerance || _townJumpVelocity!=0f) return true;
                _ridingAuraBike = false;
                if (_kohSprite) _kohSprite.PresentBike(false);
                if (_auraBikeAudio) _auraBikeAudio.Stop();
                _auraBikeSpeed = 0f;
                _townMomentum = Vector2.zero;
                ResetAuraBikeBump();
                var grounded=_player.transform.position;
                grounded.y=TownTerrainHeight(grounded)+TownActorFootOffset;
                _player.transform.position=grounded;
                _auraBike.transform.position=grounded-Vector3.up*(TownActorFootOffset-.04f);
                _auraBikeView.Present(_auraBikeHeading, false);
                TownSaveVariables.SetFloat("aura_bike.x", _auraBike.transform.position.x);
                TownSaveVariables.SetFloat("aura_bike.z", _auraBike.transform.position.z);
                TownSaveVariables.Save();
                return true;
            }
            Vector3 delta = _auraBike.transform.position - _player.transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 2.25f || _carriedItem != null || _heldTownPot || _townJumpHeight > TownGroundTolerance) return false;
            _ridingAuraBike = true;
            if (_kohSprite) _kohSprite.PresentBike(true);
            _auraBikeSoundTime=0f;
            ResetAuraBikeBump();
            _auraBikeHeading = _townFacing;

            return true;
        }

        // TOWN 800A5A98: native caps 0x1c0000 / 0x260000, turn step (16 + speed) / 4096.
        // Native coordinates use 64 units/tile. Enhanced steering deliberately grows with speed.
        internal static float AuraBikeMaximumSpeed(bool booster) => (booster ? 38f : 28f) * 60f / 64f * .8f;
        internal static float AuraBikeTurnRate(float speed) => (16f + speed * 64f / 60f) * 360f / 4096f * 60f * 3f;

        private void DriveAuraBike(Vector2 input,float maximum,float dt,bool recoiling)
        {
            bool hasInput=input.sqrMagnitude>.001f;
            float alignment=hasInput ? Vector2.Dot(input.normalized,_auraBikeHeading) : 1f;
            bool reverse=hasInput && (alignment<-.65f || _auraBikeSpeed<-.1f && alignment<=.15f);
            if(!recoiling)
            {
                float target=hasInput ? (reverse ? -maximum*.5f : maximum) : 0f;
                bool braking=!hasInput || _auraBikeSpeed*target<0f;
                // Brake to a stop before applying acceleration in the other direction.
                float stepTarget=braking ? 0f : target;
                float acceleration=braking ? maximum*1.6f : maximum*.7f*(reverse ? .5f : 1f);
                _auraBikeSpeed=Mathf.MoveTowards(_auraBikeSpeed,stepTarget,acceleration*dt);
            }
            // Gentle contacts do not lock steering. Reverse keeps the bike's facing.
            if(hasInput && (!recoiling || _auraBikeBumpStrength<.35f))
            {
                float heading=Mathf.Atan2(_auraBikeHeading.x,_auraBikeHeading.y)*Mathf.Rad2Deg;
                // Reversing toward screen-left turns the nose right, and vice versa.
                Vector2 steering=reverse ? -input : input;
                float target=Mathf.Atan2(steering.x,steering.y)*Mathf.Rad2Deg;
                heading=Mathf.MoveTowardsAngle(heading,target,AuraBikeTurnRate(Mathf.Abs(_auraBikeSpeed))*dt)*Mathf.Deg2Rad;
                _auraBikeHeading=new Vector2(Mathf.Sin(heading),Mathf.Cos(heading));
            }
        }

        private bool AuraBikeStepOpen(Vector3 next,Vector3 origin,float feet,bool airborne)
        {
            if(next.x<.5f || next.z<.5f || next.x>RetailTownSize-1.5f || next.z>RetailTownSize-1.5f) return false;
            float support=Mathf.Max(TownTerrainHeight(origin),TownObstacleTop(origin,TownActorRadius));
            float nextSupport=Mathf.Max(TownTerrainHeight(next),TownObstacleTop(next,TownActorRadius));
            if(!airborne)
            {
                // Roll over low foliage and small terrain/bridge approaches. The
                // normal ground mask still excludes deep water and solid buildings.
                if(nextSupport-support>.65f || support-nextSupport>1.01f) return false;
                feet=Mathf.Max(feet,nextSupport);
            }
            return TownPlayerPositionOpen(next,origin,feet);
        }

        private bool MoveAuraBike(Vector2 input, float deltaTime)
        {
            EnsureAuraBike();
            if (!_ridingAuraBike || !_auraBike || _interior) { _ridingAuraBike = false; if(_kohSprite)_kohSprite.PresentBike(false); if(_auraBikeAudio)_auraBikeAudio.Stop(); ResetAuraBikeBump(); return false; }
            if(_kohSprite)_kohSprite.PresentBike(true);
            float dt = Mathf.Clamp(deltaTime,0f,.1f);
            bool recoiling=_auraBikeBumpRemaining>0f;
            float maximum = AuraBikeMaximumSpeed(GeneralStoreOwned("booster") > 0);
            float previousSpeed=Mathf.Abs(_auraBikeSpeed);
            DriveAuraBike(input,maximum,dt,recoiling);
            Vector3 before = _player.transform.position, position = before;
            float startFeet=before.y-TownActorFootOffset-_auraBikeBumpHeight;
            float startTerrain=TownTerrainHeight(before);
            float startGround=Mathf.Max(startTerrain,TownObstacleTop(before,TownActorRadius));
            bool grounded=_townJumpVelocity==0f && startFeet<=startGround+TownGroundTolerance;
            if(grounded && !recoiling && _wait?.WasPressedThisFrame()==true) BeginTownJump();
            bool airborne=!grounded || _townJumpVelocity!=0f;
            float feet=startFeet;
            if(airborne)
            {
                feet+=_townJumpVelocity*dt-.5f*TownGravity*dt*dt;
                _townJumpVelocity-=TownGravity*dt;
            }
            float clearance=Mathf.Max(startFeet,feet);
            Vector2 move = recoiling ? _auraBikeRecoil*dt : _auraBikeHeading*(_auraBikeSpeed*dt);
            int steps = Mathf.Max(1, Mathf.CeilToInt(move.magnitude / .1f));
            for (int i = 0; i < steps; i++)
            {
                Vector3 next = position + new Vector3(move.x, 0f, move.y) / steps;
                if(!AuraBikeStepOpen(next,position,clearance,airborne))
                {
                    if(recoiling){_auraBikeRecoil=Vector2.zero;_auraBikeSpeed=0f;}
                    else if(move.sqrMagnitude>.000001f) BumpAuraBike(move);
                    break;
                }
                position = next;
            }
            if(recoiling)
            {
                _auraBikeRecoil=Vector2.MoveTowards(_auraBikeRecoil,Vector2.zero,28f*dt);
                _auraBikeBumpRemaining=Mathf.Max(0f,_auraBikeBumpRemaining-dt);
                if(_auraBikeBumpRemaining==0f) _auraBikeRecoil=Vector2.zero;
            }
            float bump=_auraBikeBumpRemaining>0f ? Mathf.Sin(Mathf.PI*(1f-_auraBikeBumpRemaining/_auraBikeBumpLength))*.16f*_auraBikeBumpStrength : 0f;
            float landingGround=Mathf.Max(TownTerrainHeight(position),TownObstacleTop(position,TownActorRadius));
            bool rodeOffObstacle=!airborne && startGround>startTerrain+TownGroundTolerance && landingGround<startGround-TownGroundTolerance;
            if(!airborne && !rodeOffObstacle) feet=landingGround;
            if(feet<=landingGround && _townJumpVelocity<=0f)
            {
                feet=landingGround;
                _townJumpVelocity=0f;
            }
            _townJumpHeight=Mathf.Max(0f,feet-landingGround);
            _auraBikeBumpHeight=bump;
            if(_kohSprite) _kohSprite.SetAirborne(_townJumpHeight>TownGroundTolerance);
            position.y=feet+TownActorFootOffset+bump;
            _player.transform.position = position;
            _playerCell = new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z));
            _townFacing = _auraBikeHeading;
            if (_kohSprite) _kohSprite.SetFacing(_townFacing);
            if (_playerLocomotion) _playerLocomotion.Present(Vector3.zero, 0f, false, false);
            _auraBike.transform.position = position - Vector3.up * (TownActorFootOffset - .04f);
            _auraBikeView.Present(_auraBikeHeading, Mathf.Abs(_auraBikeSpeed) > .1f || _auraBikeRecoil.sqrMagnitude>.01f);
            float distance=new Vector2(position.x-before.x,position.z-before.z).magnitude;
            // Retail update_actor_movement_and_actions: SD_Call(0x60A), 2..9 ticks
            // between pulses as speed approaches the cap. Reverse uses absolute speed.
            PresentAuraBikeSound(dt,maximum,distance>.001f && !recoiling);
            bool accelerating=Mathf.Abs(_auraBikeSpeed)>previousSpeed+.001f;
            // Never emit from a blocked wheel, airborne bike, or shallow water.
            float dustIntensity=accelerating && !recoiling && _townJumpHeight<=TownGroundTolerance &&
                _townJumpVelocity==0f && distance>.001f && feet>=-.05f
                ? Mathf.Lerp(1f,.3f,Mathf.Clamp01(Mathf.Abs(_auraBikeSpeed)/maximum)) : 0f;
            if(_auraBikeDust) _auraBikeDust.Present(new Vector3(position.x,feet,position.z),
                _auraBikeHeading*(_auraBikeSpeed<0f ? -1f : 1f),dustIntensity,dt);
            return true;
        }
    }
}
