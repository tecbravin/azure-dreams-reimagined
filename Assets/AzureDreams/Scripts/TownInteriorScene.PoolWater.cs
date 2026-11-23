using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TownInteriorScene
    {
        private const float PoolBasinDepth = 1.2f;
        private Mesh _poolBasinMesh;
        private Material _poolBasinMaterial;
        private GameObject _poolBasinObject;
        private Vector3[] _poolWaterVertices;
        private int[] _poolWaterTriangles;
        private bool IsPoolWaterSurface(int index) => interiorId == "Interior_0d_pool_quest_done" && index == 15;

        internal bool TryGetPoolWaterHeight(Vector3 position, out float height)
        {
            height = 0f;
            if (_poolWaterTriangles == null || _poolWaterVertices == null) return false;
            var p=transform.InverseTransformPoint(position);
            for(int i=0;i+2<_poolWaterTriangles.Length;i+=3)
            {
                var a=_poolWaterVertices[_poolWaterTriangles[i]];var b=_poolWaterVertices[_poolWaterTriangles[i+1]];var c=_poolWaterVertices[_poolWaterTriangles[i+2]];
                float ax=b.x-a.x,az=b.z-a.z,bx=c.x-a.x,bz=c.z-a.z,det=ax*bz-az*bx;
                if(Mathf.Abs(det)<.000001f) continue;
                float u=((p.x-a.x)*bz-(p.z-a.z)*bx)/det;
                float v=(ax*(p.z-a.z)-az*(p.x-a.x))/det;
                if(u<-.0001f || v<-.0001f || u+v>1.0001f) continue;
                height=transform.TransformPoint(new Vector3(p.x,a.y+u*(b.y-a.y)+v*(c.y-a.y),p.z)).y;
                return true;
            }
            return false;
        }

        private void PreparePoolGeometry(Geometry data)
        {
            if(!IsPoolWaterSurface(15) || data.surfaces.Length<=15) return;
            _poolWaterVertices=data.vertices;_poolWaterTriangles=data.surfaces[15].triangles;
            // Remove the old flat floor underneath the surface; it would obscure
            // the deeper basin even though the translucent water itself is correct.
            for(int surface=0;surface<data.surfaces.Length;surface++)
            {
                if(surface==15)continue;
                var source=data.surfaces[surface].triangles;var kept=new List<int>(source.Length);
                for(int i=0;i+2<source.Length;i+=3)
                {
                    var a=data.vertices[source[i]];var b=data.vertices[source[i+1]];var c=data.vertices[source[i+2]];
                    var center=(a+b+c)/3f;
                    bool flat=Mathf.Max(a.y,b.y,c.y)-Mathf.Min(a.y,b.y,c.y)<.001f;
                    if(flat && TryGetPoolWaterHeight(transform.TransformPoint(center),out float water) && transform.TransformPoint(center).y<water-.05f)continue;
                    kept.Add(source[i]);kept.Add(source[i+1]);kept.Add(source[i+2]);
                }
                data.surfaces[surface].triangles=kept.ToArray();
            }
        }

        private void BuildPoolBasin(Geometry data)
        {
            if (!IsPoolWaterSurface(15) || data.surfaces.Length <= 15 || !_runtimeMaterials[15]) return;
            var shader = AssetMode.Load<Shader>("Shaders/PoolBasin");
            if (!shader) return;
            var water = data.surfaces[15].triangles;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var indices = new List<int>();
            var edges = new Dictionary<(Vector2Int, Vector2Int), (Vector3 A, Vector3 B, int Count)>();
            void Add(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                uv.Add(ua); uv.Add(ub); uv.Add(uc);
                indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
            }
            void Edge(Vector3 a, Vector3 b)
            {
                var x = new Vector2Int(Mathf.RoundToInt(a.x * 1024), Mathf.RoundToInt(a.z * 1024));
                var y = new Vector2Int(Mathf.RoundToInt(b.x * 1024), Mathf.RoundToInt(b.z * 1024));
                var key = x.x < y.x || (x.x == y.x && x.y < y.y) ? (x, y) : (y, x);
                edges.TryGetValue(key, out var edge);
                edges[key] = (a, b, edge.Count + 1);
            }
            Vector3 down = Vector3.down * PoolBasinDepth;
            Vector2 Tile(Vector3 p) => new Vector2(p.x, p.z);
            for (int i = 0; i < water.Length; i += 3)
            {
                var a = data.vertices[water[i]]; var b = data.vertices[water[i + 1]]; var c = data.vertices[water[i + 2]];
                Add(a + down, b + down, c + down, Tile(a), Tile(b), Tile(c));
                Edge(a, b); Edge(b, c); Edge(c, a);
            }
            foreach (var edge in edges.Values)
            {
                if (edge.Count != 1) continue;
                var a = edge.A; var b = edge.B; float length = Vector3.Distance(a, b);
                Add(a, a + down, b, Vector2.zero, new Vector2(0, PoolBasinDepth), new Vector2(length, 0));
                Add(b, a + down, b + down, new Vector2(length, 0), new Vector2(0, PoolBasinDepth), new Vector2(length, PoolBasinDepth));
            }
            _poolBasinMesh = new Mesh { name = "Original pool contour - submerged basin" };
            _poolBasinMesh.SetVertices(vertices); _poolBasinMesh.SetUVs(0, uv); _poolBasinMesh.SetTriangles(indices, 0);
            _poolBasinMesh.RecalculateNormals(); _poolBasinMesh.RecalculateBounds();
            _poolBasinMaterial = new Material(shader) { mainTexture = _runtimeMaterials[4].mainTexture };
            _poolBasinMaterial.SetFloat("_SurfaceHeight", data.vertices[water[0]].y);
            _poolBasinMaterial.SetFloat("_Depth", PoolBasinDepth);
            _poolBasinObject = new GameObject("Pool underwater floor and walls");
            _poolBasinObject.transform.SetParent(transform, false);
            _poolBasinObject.AddComponent<MeshFilter>().sharedMesh = _poolBasinMesh;
            _poolBasinObject.AddComponent<MeshRenderer>().sharedMaterial = _poolBasinMaterial;
        }

        private void ClearPoolBasin()
        {
            foreach (Object item in new Object[] { _poolBasinObject, _poolBasinMesh, _poolBasinMaterial })
                if (item) { if (Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
            _poolBasinObject = null; _poolBasinMesh = null; _poolBasinMaterial = null;
            _poolWaterVertices=null;_poolWaterTriangles=null;
        }
    }
}
