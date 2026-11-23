using UnityEngine;
using UnityEngine.Rendering;

namespace AzureDreamsReimagined
{
    /// <summary>World-space wheel dust: reused emitter, soft overlapping puffs, no FX per frame allocations.</summary>
    public sealed class AuraBikeDust : MonoBehaviour
    {
        private ParticleSystem _particles;
        private Material _material;
        private float _remainder;
        public void Initialize()
        {
            var shader=Resources.Load<Shader>("Shaders/AuraBikeDust");
            if(!shader) return;
            _material=new Material(shader){name="Aura Bike dense wheel dust"};
            _particles=gameObject.AddComponent<ParticleSystem>();
            _particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=_particles.main;main.loop=false;main.playOnAwake=false;
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=256;
            main.startLifetime=new ParticleSystem.MinMaxCurve(.5f,.95f);
            main.startSize=new ParticleSystem.MinMaxCurve(.24f,.48f);
            main.startSpeed=0f;main.gravityModifier=-.025f;
            var emission=_particles.emission;emission.enabled=false;
            var shape=_particles.shape;shape.enabled=false;
            var size=_particles.sizeOverLifetime;size.enabled=true;
            size.size=new ParticleSystem.MinMaxCurve(1f,AnimationCurve.EaseInOut(0,.45f,1,2.8f));
            var colors=_particles.colorOverLifetime;colors.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(.7f,.58f,.4f),0),new GradientColorKey(new Color(.84f,.76f,.6f),1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.65f,.09f),new GradientAlphaKey(.4f,.5f),new GradientAlphaKey(0,1)});
            colors.color=new ParticleSystem.MinMaxGradient(gradient);
            var renderer=GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial=_material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }

        public void Present(Vector3 feet,Vector2 travel,float intensity,float dt)
        {
            if(!_particles || intensity<=0f || dt<=0f){_remainder=0;return;}
            intensity=Mathf.Clamp01(intensity);
            var backward=-new Vector3(travel.x,0,travel.y).normalized;
            var side=new Vector3(-backward.z,0,backward.x);
            _remainder+=Mathf.Lerp(35f,110f,intensity)*Mathf.Min(dt,.1f);
            int count=Mathf.FloorToInt(_remainder);_remainder-=count;
            if(!_particles.isPlaying) _particles.Play();
            for(int i=0;i<count;i++)
            {
                var p=new ParticleSystem.EmitParams{
                    position=feet+backward*.6f+side*Random.Range(-.19f,.19f)+Vector3.up*Random.Range(.08f,.18f),
                    velocity=backward*Random.Range(.5f,1.5f)+side*Random.Range(-.4f,.4f)+Vector3.up*Random.Range(.12f,.5f),
                    startSize=Random.Range(.28f,.48f)*Mathf.Lerp(.8f,1.25f,intensity),
                    startLifetime=Random.Range(.5f,.95f),startColor=Color.white,
                    rotation=Random.Range(0f,360f)};
                _particles.Emit(p,1);
            }
        }
        private void OnDestroy(){if(_material) Destroy(_material);}
    }
}
