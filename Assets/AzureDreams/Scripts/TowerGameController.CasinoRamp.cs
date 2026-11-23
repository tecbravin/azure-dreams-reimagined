using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private readonly List<(Vector3 A,Vector3 B,Vector3 C,bool Sloped)> _casinoRampFaces = new();
        private bool _casinoRampSliding;
        private float _casinoRampSlideSpeed;

        private void LoadCasinoRamp()
        {
            if(_casinoRampFaces.Count>0 || !AtCasinoRoulette) return;
            var source=Resources.Load<TextAsset>("Town/Interiors/"+CasinoRouletteRoom+"/geometry");
            if(!source)return;
            var geometry=JsonUtility.FromJson<TownInteriorScene.Geometry>(source.text);
            var indices=geometry.surfaces[10].triangles;
            for(int i=0;i+2<indices.Length;i+=3)
            {
                var a=geometry.vertices[indices[i]];var b=geometry.vertices[indices[i+1]];var c=geometry.vertices[indices[i+2]];
                if(Mathf.Abs(Vector3.Cross(b-a,c-a).y)<.001f)continue;
                if(Mathf.Min(a.x,b.x,c.x)<12.99f || Mathf.Max(a.x,b.x,c.x)>18.01f ||
                    Mathf.Min(a.z,b.z,c.z)<8.99f || Mathf.Max(a.z,b.z,c.z)>11.01f ||
                    Mathf.Min(a.y,b.y,c.y)<-.01f || Mathf.Max(a.y,b.y,c.y)>4.51f)continue;
                bool sloped=Mathf.Max(a.y,b.y,c.y)-Mathf.Min(a.y,b.y,c.y)>.1f;
                if(!sloped && Mathf.Abs(a.y-4f)>.01f)continue;
                _casinoRampFaces.Add((a,b,c,sloped));
            }
        }

        private bool TryCasinoRampHeight(Vector3 position,out float height,out bool sloped)
        {
            height=float.NegativeInfinity;sloped=false;
            if(!AtCasinoRoulette)return false;
            LoadCasinoRamp();
            foreach(var face in _casinoRampFaces)
            {
                float ax=face.B.x-face.A.x,az=face.B.z-face.A.z,bx=face.C.x-face.A.x,bz=face.C.z-face.A.z;
                float det=ax*bz-az*bx;
                float u=((position.x-face.A.x)*bz-(position.z-face.A.z)*bx)/det;
                float v=(ax*(position.z-face.A.z)-az*(position.x-face.A.x))/det;
                if(u<-.0001f || v<-.0001f || u+v>1.0001f)continue;
                float y=face.A.y+u*(face.B.y-face.A.y)+v*(face.C.y-face.A.y);
                if(y>height){height=y;sloped=face.Sloped;}
            }
            return !float.IsNegativeInfinity(height);
        }

        private void BuildCasinoRampCollider()
        {
            ResetCasinoRampSlide();LoadCasinoRamp();
            var vertices=new List<Vector3>();var indices=new List<int>();
            void Triangle(Vector3 a,Vector3 b,Vector3 c)
            {int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);indices.Add(n);indices.Add(n+1);indices.Add(n+2);}
            foreach(var face in _casinoRampFaces)
            {
                var a=face.A;var b=face.B;var c=face.C;
                if(Vector3.Cross(b-a,c-a).y<0f)(b,c)=(c,b);
                var floorA=new Vector3(a.x,0f,a.z);var floorB=new Vector3(b.x,0f,b.z);var floorC=new Vector3(c.x,0f,c.z);
                Triangle(a,b,c);Triangle(floorA,floorC,floorB);
                Triangle(a,floorA,b);Triangle(b,floorA,floorB);
                Triangle(b,floorB,c);Triangle(c,floorB,floorC);
                Triangle(c,floorC,a);Triangle(a,floorC,floorA);
            }
            if(indices.Count==0)return;
            var mesh=new Mesh{name="Casino wooden ramp solid underside"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            _constructionGeometry.Add(mesh);
            var body=new GameObject("Casino wooden ramp collision");body.transform.SetParent(transform,false);body.AddComponent<MeshCollider>().sharedMesh=mesh;_generated.Add(body);
        }

        private bool TickCasinoRampSlide(Vector2 input,float deltaTime)
        {
            if(!AtCasinoRoulette || !_player || _rouletteMode is not (RouletteMode.Idle or RouletteMode.Paying))
            {ResetCasinoRampSlide();return false;}
            var before=_player.transform.position;
            if(!_casinoRampSliding)
            {
                if(input.x<=.2f || _townJumpVelocity!=0f || _carriedItem!=null || _heldTownPot ||
                    !TryCasinoRampHeight(before,out float top,out bool sloped) || !sloped ||
                    Mathf.Abs(before.y-TownActorFootOffset-top)>.15f)return false;
                _casinoRampSliding=true;_casinoRampSlideSpeed=2.5f;_townMomentum=Vector2.zero;
                _kohSprite?.PresentDining(true);
            }
            float dt=Mathf.Min(deltaTime,.1f);
            _casinoRampSlideSpeed=Mathf.MoveTowards(_casinoRampSlideSpeed,7f,9f*dt);
            float travel=_casinoRampSlideSpeed*dt;int steps=Mathf.Max(1,Mathf.CeilToInt(travel/.08f));
            var position=before;bool finished=false;
            for(int i=0;i<steps;i++)
            {
                var next=position+Vector3.right*(travel/steps);
                if(!TownPlayerPositionOpen(next,position,position.y-TownActorFootOffset)) {finished=true;break;}
                if(TryCasinoRampHeight(next,out float top,out _))next.y=top+TownActorFootOffset;
                else
                {
                    float ground=TownTerrainHeight(next);
                    next.y=Mathf.Max(ground,position.y-TownActorFootOffset)+TownActorFootOffset;
                    _townJumpHeight=Mathf.Max(0f,next.y-TownActorFootOffset-ground);
                    if(_townJumpHeight>TownGroundTolerance)_townJumpVelocity=-1f;
                    finished=true;
                }
                position=next;if(finished)break;
            }
            _player.transform.position=position;_playerCell=new Vector2Int(Mathf.RoundToInt(position.x),Mathf.RoundToInt(position.z));
            _townFacing=Vector2.right;_kohSprite?.SetFacing(Vector2.right);_playerLocomotion?.Present(position-before,0f,false);
            if(finished)ResetCasinoRampSlide();
            return true;
        }

        private void ResetCasinoRampSlide()
        {
            if(_casinoRampSliding)_kohSprite?.PresentDining(false);
            _casinoRampSliding=false;_casinoRampSlideSpeed=0f;
        }
    }
}
