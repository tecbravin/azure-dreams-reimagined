using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private GameObject _fountainCollisionRoot;
        private readonly Dictionary<Vector2Int, List<FountainFace>> _fountainFaces = new();
        private struct FountainFace
        {
            public Vector3 A, B, C;
            public bool Height(Vector2 p, out float height)
            {
                float area = (B.x-A.x)*(C.z-A.z)-(B.z-A.z)*(C.x-A.x);
                if (Mathf.Abs(area)<.00001f) { height=0f; return false; }
                float u=((p.x-A.x)*(C.z-A.z)-(p.y-A.z)*(C.x-A.x))/area;
                float v=((B.x-A.x)*(p.y-A.z)-(B.z-A.z)*(p.x-A.x))/area;
                height=A.y+u*(B.y-A.y)+v*(C.y-A.y);
                return u>=-.0001f && v>=-.0001f && u+v<=1.0001f;
            }
        }

        private void SetupFountainCollision(GameObject root, Mesh mesh)
        {
            _fountainCollisionRoot=root;
            _fountainFaces.Clear();
            root.AddComponent<MeshCollider>().sharedMesh=mesh;
            var vertices=mesh.vertices;
            var indices=mesh.triangles;
            for(int i=0;i<indices.Length;i+=3)
            {
                var face=new FountainFace { A=root.transform.TransformPoint(vertices[indices[i]]), B=root.transform.TransformPoint(vertices[indices[i+1]]), C=root.transform.TransformPoint(vertices[indices[i+2]]) };
                float area=(face.B.x-face.A.x)*(face.C.z-face.A.z)-(face.B.z-face.A.z)*(face.C.x-face.A.x);
                if(Mathf.Abs(area)<.00001f)continue;
                int x0=Mathf.FloorToInt(Mathf.Min(face.A.x,Mathf.Min(face.B.x,face.C.x))), x1=Mathf.FloorToInt(Mathf.Max(face.A.x,Mathf.Max(face.B.x,face.C.x)));
                int z0=Mathf.FloorToInt(Mathf.Min(face.A.z,Mathf.Min(face.B.z,face.C.z))), z1=Mathf.FloorToInt(Mathf.Max(face.A.z,Mathf.Max(face.B.z,face.C.z)));
                for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
                {
                    var cell=new Vector2Int(x,z);
                    if(!_fountainFaces.TryGetValue(cell,out var faces))_fountainFaces[cell]=faces=new List<FountainFace>();
                    faces.Add(face);
                }
            }
        }

        private float FountainSurface(Vector3 point, float ceiling=float.PositiveInfinity)
        {
            if(_interior || !_fountainCollisionRoot)return float.NegativeInfinity;
            if(!_fountainFaces.TryGetValue(new Vector2Int(Mathf.FloorToInt(point.x),Mathf.FloorToInt(point.z)),out var faces))return float.NegativeInfinity;
            float top=float.NegativeInfinity;
            foreach(var face in faces)
                if(face.Height(new Vector2(point.x,point.z),out float y) && y<=ceiling+.001f)top=Mathf.Max(top,y);
            return top;
        }

        private float FountainObstacleTop(Vector3 point,float radius)
        {
            float top=FountainSurface(point);
            if(radius<=0f)return top <= 1.01f ? float.NegativeInfinity : top;
            for(int i=0;i<8;i++)
            {
                float angle=i*Mathf.PI*.25f;
                top=Mathf.Max(top,FountainSurface(point+new Vector3(Mathf.Cos(angle)*radius,0f,Mathf.Sin(angle)*radius)));
            }
            return top <= 1.01f ? float.NegativeInfinity : top;
        }

        private bool FountainWalkwayOpen(Vector3 point,float radius)
        {
            float ground=FountainSurface(point,1.01f);
            if(float.IsNegativeInfinity(ground)||ground<-.75f)return false;
            for(int i=0;i<4;i++)
            {
                float angle=i*Mathf.PI*.5f;
                float side=FountainSurface(point+new Vector3(Mathf.Cos(angle)*radius,0f,Mathf.Sin(angle)*radius),1.01f);
                if(float.IsNegativeInfinity(side)||side<-.75f)return false;
            }
            return true;
        }
    }
}
