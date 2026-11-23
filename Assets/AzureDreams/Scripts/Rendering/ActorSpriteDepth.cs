using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined.Rendering
{
    /// <summary>Depth-correct billboards without changing their screen proportions or collisions.</summary>
    [DefaultExecutionOrder(10000), DisallowMultipleComponent]
    public sealed class ActorSpriteDepth : MonoBehaviour
    {
        private static readonly int Anchor=Shader.PropertyToID("_ActorDepthAnchor");
        private static readonly int DepthNormal=Shader.PropertyToID("_ActorDepthNormal");
        private static readonly Dictionary<Material,Material> Materials=new();
        private readonly List<SpriteRenderer> _sprites=new();
        private MaterialPropertyBlock _properties;
        private TownInteriorScene _interior;
        private Vector3 _facadeNormal;
        private bool _seated;
        private Vector3 _seatDepthOffset;
        internal void SetSeated(bool seated,Vector3 depthOffset) { _seated=seated;_seatDepthOffset=depthOffset; }
        internal void SetInterior(TownInteriorScene interior) => _interior=interior;
        // Window actors belong to the facade plane, even while their artwork
        // faces the camera. Ordinary actors keep their usual upright depth.
        internal void SetFacadeNormal(Vector3 normal) => _facadeNormal=normal.normalized;

        private Vector3 FurnitureDepthAnchor(Vector3 anchor)
        {
            if(!_interior) return anchor;
            var camera=Camera.main;
            if(!camera) return anchor;
            var forward=camera.transform.forward;forward.y=0f;
            if(forward.sqrMagnitude<.001f) return anchor;
            forward.Normalize();
            var side=camera.transform.right;side.y=0f;side.Normalize();
            float halfWidth=.25f;
            foreach(var sprite in _sprites)
                if(sprite && sprite.enabled && sprite.sprite)
                    halfWidth=Mathf.Max(halfWidth,sprite.sprite.bounds.extents.x*Mathf.Abs(sprite.transform.lossyScale.x));
            halfWidth=Mathf.Min(halfWidth,1.2f);
            float clearance=0f;
            var point=new Vector2(anchor.x,anchor.z);
            var ahead=new Vector2(forward.x,forward.z);
            var right=new Vector2(side.x,side.z);
            foreach(var surface in _interior.FurnitureSurfaces())
            {
                if(surface.top<=anchor.y+.1f) continue; // Standing on top of furniture.
                var rect=_interior.MapLayoutFootprint(surface.footprint);
                // Preserve occlusion when Koh is behind the table. Only protect
                // the near side of nearby furniture, never walls/whole scenery.
                if(rect.Contains(point) || Vector2.Dot(rect.center-point,ahead)<=0f) continue;
                var nearest=new Vector2(Mathf.Clamp(point.x,rect.xMin,rect.xMax),Mathf.Clamp(point.y,rect.yMin,rect.yMax));
                if((nearest-point).sqrMagnitude>(halfWidth+.15f)*(halfWidth+.15f)) continue;
                float minimum=float.PositiveInfinity;
                var previous=new Vector2(rect.xMin,rect.yMax)-point;
                for(int edge=0;edge<4;edge++)
                {
                    var current=new Vector2(edge==0 || edge==3 ? rect.xMin : rect.xMax,edge<2 ? rect.yMin : rect.yMax)-point;
                    float a=Vector2.Dot(previous,right),b=Vector2.Dot(current,right);
                    if(Mathf.Abs(b)<=halfWidth) minimum=Mathf.Min(minimum,Vector2.Dot(current,ahead));
                    if(Mathf.Abs(b-a)>.0001f)
                        for(int sign=-1;sign<=1;sign+=2)
                        {
                            float t=(sign*halfWidth-a)/(b-a);
                            if(t>=0f && t<=1f) minimum=Mathf.Min(minimum,Vector2.Dot(Vector2.Lerp(previous,current,t),ahead));
                        }
                    previous=current;
                }
                if(!float.IsPositiveInfinity(minimum)) clearance=Mathf.Max(clearance,Mathf.Clamp(.025f-minimum,0f,.75f));
            }
            return anchor-forward*clearance;
        }

        private void LateUpdate()
        {
            // Lists avoid garbage; include visuals created/replaced by animation banks.
            GetComponentsInChildren(true,_sprites);
            _properties ??= new MaterialPropertyBlock();
            // All body pieces share one physical depth plane. A separate head
            // origin would leave its entire card behind nearby furniture.
            var anchor=transform.position;
            foreach(var part in _sprites)
                if(part && part.enabled && part.sprite)
                    anchor.y=Mathf.Min(anchor.y,part.transform.position.y);
            anchor=_seated ? transform.position+_seatDepthOffset : FurnitureDepthAnchor(anchor);
            foreach(var sprite in _sprites)
            {
                if(!sprite || !sprite.enabled || !sprite.sprite) continue;
                var source=sprite.sharedMaterial;
                if(!source || !source.shader) continue;
                string shaderName=source.shader.name;
                if(shaderName!="AzureDreams/ActorSpriteDepth")
                {
                    if(shaderName!="Sprites/Default" && shaderName!="AzureDreams/KohColorKey") continue;
                    if(!Materials.TryGetValue(source,out var material) || !material)
                    {
                        var shader=Resources.Load<Shader>("Shaders/ActorSpriteDepth");
                        if(!shader) continue;
                        material=new Material(shader){name=source.name+" (upright depth)",hideFlags=HideFlags.HideAndDontSave};
                        if(source.HasProperty("_Color")) material.SetColor("_Color",source.GetColor("_Color"));
                        material.SetFloat("_ColorKey",shaderName=="AzureDreams/KohColorKey" ? 1f : 0f);
                        Materials[source]=material;
                    }
                    sprite.sharedMaterial=material;
                }
                sprite.GetPropertyBlock(_properties);
                _properties.SetVector(Anchor,anchor);
                _properties.SetVector(DepthNormal,_facadeNormal);
                sprite.SetPropertyBlock(_properties);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearMaterials()
        {
            foreach(var material in Materials.Values) if(material) Destroy(material);
            Materials.Clear();
        }
    }
}
