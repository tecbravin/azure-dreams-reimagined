using System;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        // Threshold mats recovered from the house meshes, before House 1's X mirroring.
        // Bathroom staging is reconstructed; the separate retail cutaway is not captured.
        private static readonly Vector3[] HomeBathDoors = {
            new(21.5f,0f,13.5f), new(16.5f,0f,9.5f), new(19.5f,0f,9.5f)
        };
        private int _homeBathRank = -1, _homeBathPhase;
        private float _homeBathTime;
        private Vector3 _homeBathStart;
        private GameObject _homeBathRoom;
        private Vector3 _homeBathCenter;
        private float _homeBathWidth, _homeBathDepth;
        private readonly System.Collections.Generic.List<UnityEngine.Object> _homeBathAssets = new();
        private static readonly string[] BathFurnishings = { "washer", "plant", "light", "painting", "poster", "telephone", "tv", "sculpture" };

        private Vector3 BathDoorPosition(int rank) => HomeLayoutPoint(rank, HomeBathDoors[rank]);
        private Vector3 BathWaterPosition(int rank) => new(96f, 0f, 48f);

        private void SetupHomeBathScene(int rank)
        {
            _homeBathRoom = null; // The cutaway is built only when the door is used.
        }

        private void BeginHomeBath(int rank)
        {
            if (!_player || rank < 0 || rank > 2 || _homeBathRank >= 0) return;
            _homeBathStart = _player.transform.position;
            _homeBathRank = rank;
            _homeBathPhase = 0;
            _homeBathTime = 0f;
            _townMomentum = Vector2.zero;
            _townJumpHeight = _townJumpVelocity = 0f;
            _message = string.Empty;
            if (_playerLocomotion) _playerLocomotion.Present(Vector3.zero, 0f, false);
            if (_kohSprite) _kohSprite.SetAirborne(false);
        }

        private bool TickHomeBath(float deltaTime)
        {
            if (_homeBathRank < 0) return false;
            if (!_player || HouseIndex != _homeBathRank) { EndHomeBath(); return false; }
            _homeBathTime += deltaTime;
            // Cross the bathroom doorway under black, rather than moving through a sink or wall.
            if (_homeBathPhase == 0)
            {
                _fadeToBlack = Mathf.Clamp01(_homeBathTime / .25f);
                if (_homeBathTime < .25f) return true;
                BuildHomeBathroom(_homeBathRank);
                // The bath poses use the standing sprite pivot. Seat the torso above
                // the water instead of burying the entire billboard inside the vessel.
                _player.transform.position = _homeBathCenter + Vector3.up * (TownActorFootOffset + (_homeBathRank == 0 ? .48f : .34f));
                _playerCell = new Vector2Int(Mathf.RoundToInt(_homeBathCenter.x), Mathf.RoundToInt(_homeBathCenter.z));
                _homeBathPhase = 1; _homeBathTime = 0f;
            }
            if (_homeBathPhase is >= 1 and <= 3)
            {
                int animation = _homeBathPhase - 1;
                float duration = _kohSprite ? _kohSprite.PresentBath(animation, _homeBathTime) : 1f;
                if (animation == 1) duration = 3f;
                _fadeToBlack = _homeBathPhase == 1 ? 1f - Mathf.Clamp01(_homeBathTime / .25f) : 0f;
                // Fixed framing keeps the bath and purchased furnishings visible without orbiting through walls.
                SetEntranceCamera(_homeBathCenter + new Vector3(0f,.7f,0f), 0f, 48f, 7f + _homeBathRank * 1.8f);
                if (_homeBathTime < duration) return true;
                _homeBathPhase++; _homeBathTime = 0f;
            }
            if (_homeBathPhase == 4)
            {
                _fadeToBlack = Mathf.Clamp01(_homeBathTime / .25f);
                if (_homeBathTime < .25f) return true;
                if (_kohSprite) _kohSprite.PresentBath(-1,0f);
                _player.transform.position = _homeBathStart;
                ResetEntranceCamera(); UpdateCameraFollow(snap:true);
                if (_homeBathRoom) _homeBathRoom.SetActive(false);
                _homeBathPhase = 5; _homeBathTime = 0f;
            }
            if (_homeBathPhase == 5)
            {
                _fadeToBlack = 1f - Mathf.Clamp01(_homeBathTime / .25f);
                if (_homeBathTime < .25f) return true;
                EndHomeBath();
                _health = _playerDefinition ? _playerDefinition.MaxHealth : 20;
                if (_player.TryGetComponent<CharacterStats>(out var stats)) stats.currentHealth = _health;
            }
            return true;
        }

        private void EndHomeBath()
        {
            if (_homeBathRank < 0) return;
            _homeBathRank = -1;
            _fadeToBlack = 0f;
            if (_kohSprite) _kohSprite.PresentBath(-1,0f);
            if (_homeBathRoom) RemoveGenerated(_homeBathRoom);
            _homeBathRoom = null;
            foreach (var asset in _homeBathAssets) if (asset) Destroy(asset);
            _homeBathAssets.Clear();
            ResetEntranceCamera();
            if (!_player) return;
            _player.transform.position = _homeBathStart;
            _playerCell = new Vector2Int(Mathf.RoundToInt(_homeBathStart.x),Mathf.RoundToInt(_homeBathStart.z));
            UpdateCameraFollow(snap:true);
        }

        private void BuildHomeBathroom(int rank)
        {
            _homeBathCenter = BathWaterPosition(rank);
            _homeBathWidth = 4f + rank * 2f; _homeBathDepth = 4f + rank;
            _homeBathRoom = new GameObject("Koh bathroom " + (rank+1));
            _homeBathRoom.transform.SetParent(transform,false); _homeBathRoom.transform.position = _homeBathCenter;
            _generated.Add(_homeBathRoom);
            // The captured house meshes omit the separate bathroom cutaway. Reconstruct
            // its enclosure with the house's own floor/wall tiles, preserving original art.
            var source = AssetMode.Load<TextAsset>("Town/Interiors/" + HomeScenes[rank] + "/geometry");
            if (source)
            {
                var geometry = JsonUtility.FromJson<TownInteriorScene.Geometry>(source.text);
                var materials = _interior.GetComponent<MeshRenderer>()?.sharedMaterials;
                BuildBathTiles(geometry,materials,rank);
            }
            BuildHomeBathVessel(rank);
            int slot = 0;
            foreach (string id in BathFurnishings)
            {
                if (GeneralStoreOwned(id) <= 0) continue;
                float x = slot % 2 == 0 ? -_homeBathWidth*.36f : _homeBathWidth*.36f;
                float z = -_homeBathDepth*.3f + (slot/2)*.9f;
                var item = CreateHomeDecoration(id,id,_homeBathCenter+new Vector3(x,0f,z),0);
                item.transform.SetParent(_homeBathRoom.transform,true); slot++;
            }
        }

        // The captured interiors omit the bathroom props. Reconstructed hollow vessels,
        // rather than a solid cylinder or the unrelated pot used by the old animation.
        private void BuildHomeBathVessel(int rank)
        {
            var vessel = new GameObject(rank == 0 ? "Wooden bath barrel" : "Bathtub");
            vessel.transform.SetParent(_homeBathRoom.transform, false);
            const int segments = 32;
            bool barrel = rank == 0;
            float rx = barrel ? .66f : 1.06f, rz = barrel ? .66f : .72f;
            float height = barrel ? .94f : .78f;
            var vertices = new System.Collections.Generic.List<Vector3>();
            var uv = new System.Collections.Generic.List<Vector2>();
            var triangles = new System.Collections.Generic.List<int>[] {
                new(), new(), new(), new(), new() };
            var colors = barrel ? new[] {new Color(.43f,.24f,.11f),new Color(.54f,.33f,.16f),new Color(.35f,.18f,.08f),new Color(.23f,.24f,.22f),new Color(.18f,.52f,.66f)}
                : new[] {new Color(.77f,.83f,.83f),new Color(.86f,.90f,.87f),new Color(.65f,.74f,.76f),new Color(.94f,.94f,.84f),new Color(.18f,.52f,.66f)};
            Vector3 Point(int i, float y, float radius) { float a=i*Mathf.PI*2/segments; return new Vector3(Mathf.Cos(a)*rx*radius,y,Mathf.Sin(a)*rz*radius); }
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int material)
            {
                int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});
                uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});
                triangles[material].AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            for(int i=0;i<segments;i++)
            {
                int shade=barrel ? i%3 : 0;
                // Outside, rounded lip, inner wall and water; the center remains open.
                Quad(Point(i,0,.84f),Point(i+1,0,.84f),Point(i+1,height,1),Point(i,height,1),shade);
                Quad(Point(i,height,1),Point(i+1,height,1),Point(i+1,height+.04f,.94f),Point(i,height+.04f,.94f),barrel?1:3);
                Quad(Point(i,height+.04f,.94f),Point(i+1,height+.04f,.94f),Point(i+1,height-.04f,.83f),Point(i,height-.04f,.83f),barrel?1:3);
                Quad(Point(i,height-.04f,.83f),Point(i+1,height-.04f,.83f),Point(i+1,.28f,.72f),Point(i,.28f,.72f),2);
                float water=height-.20f;
                Quad(new Vector3(0,water,0),Point(i,water,.80f),Point(i+1,water,.80f),new Vector3(0,water,0),4);
                if(barrel) foreach(float y in new[]{.15f,.72f})
                    Quad(Point(i,y,.84f+.16f*y/height+.012f),Point(i+1,y,.84f+.16f*y/height+.012f),Point(i+1,y+.075f,.84f+.16f*(y+.075f)/height+.012f),Point(i,y+.075f,.84f+.16f*(y+.075f)/height+.012f),3);
            }
            var mesh=new Mesh{name=vessel.name};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.subMeshCount=triangles.Length;
            for(int i=0;i<triangles.Length;i++)mesh.SetTriangles(triangles[i],i);
            mesh.RecalculateNormals();mesh.RecalculateBounds();_homeBathAssets.Add(mesh);
            var materials=new Material[colors.Length];
            for(int i=0;i<materials.Length;i++)
            {
                materials[i]=new Material(Shader.Find("AzureDreams/TownInteriorUnlit"));
                materials[i].color=colors[i];_homeBathAssets.Add(materials[i]);
            }
            vessel.AddComponent<MeshFilter>().sharedMesh=mesh;
            vessel.AddComponent<MeshRenderer>().sharedMaterials=materials;
        }

        private void BuildBathTiles(TownInteriorScene.Geometry geometry, Material[] materials, int rank)
        {
            if (materials == null) return;
            int floor=-1,wall=-1; Rect floorUv=default,wallUv=default;
            // Select interior tiles (not the exterior pavement) from their native geometry.
            var interiorFloor = rank == 0 ? new Vector2(18f,13f) : new Vector2(14f,14f);
            float best=float.PositiveInfinity;
            for(int s=0;s<geometry.surfaces.Length;s++)
            for(int i=0;i+2<geometry.surfaces[s].triangles.Length;i+=3)
            {
                var ix=geometry.surfaces[s].triangles;
                var a=geometry.vertices[ix[i]];var b=geometry.vertices[ix[i+1]];var c=geometry.vertices[ix[i+2]];
                var ua=geometry.uv[ix[i]];var ub=geometry.uv[ix[i+1]];var uc=geometry.uv[ix[i+2]];
                var uv=Rect.MinMaxRect(Mathf.Min(ua.x,ub.x,uc.x),Mathf.Min(ua.y,ub.y,uc.y),Mathf.Max(ua.x,ub.x,uc.x),Mathf.Max(ua.y,ub.y,uc.y));
                if(Mathf.Max(a.y,b.y,c.y)<.01f && Mathf.Abs(Vector3.Cross(b-a,c-a).y)>.5f)
                {
                    var mid=(a+b+c)/3f;float distance=(new Vector2(mid.x,mid.z)-interiorFloor).sqrMagnitude;
                    if(distance<best){best=distance;floor=s;floorUv=uv;}
                }
                // Upper wall courses avoid selecting the black doorway opening.
                if(wall<0 && Mathf.Min(a.y,b.y,c.y)>=1.5f && Mathf.Max(a.y,b.y,c.y)-Mathf.Min(a.y,b.y,c.y)>=.5f && Mathf.Abs(Vector3.Cross(b-a,c-a).y)<.01f){wall=s;wallUv=uv;}
            }
            if(floor<0 || wall<0) return;
            var vertices=new System.Collections.Generic.List<Vector3>();var uvs=new System.Collections.Generic.List<Vector2>();
            var floorIndices=new System.Collections.Generic.List<int>();var wallIndices=new System.Collections.Generic.List<int>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Rect uv,System.Collections.Generic.List<int> indices)
            {int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});uvs.AddRange(new[]{new Vector2(uv.xMin,uv.yMin),new Vector2(uv.xMax,uv.yMin),new Vector2(uv.xMax,uv.yMax),new Vector2(uv.xMin,uv.yMax)});indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
            for(float x=-_homeBathWidth/2;x<_homeBathWidth/2;x++)
            for(float z=-_homeBathDepth/2;z<_homeBathDepth/2;z++) Quad(new(x,0,z),new(x+1,0,z),new(x+1,0,z+1),new(x,0,z+1),floorUv,floorIndices);
            // Camera yaw zero views from negative Z. Leave that front side open.
            for(float y=0;y<3;y++)
            for(float x=-_homeBathWidth/2;x<_homeBathWidth/2;x++) Quad(new(x,y,_homeBathDepth/2),new(x+1,y,_homeBathDepth/2),new(x+1,y+1,_homeBathDepth/2),new(x,y+1,_homeBathDepth/2),wallUv,wallIndices);
            foreach(float x in new[]{-_homeBathWidth/2,_homeBathWidth/2})
            for(float y=0;y<3;y++)
            for(float z=-_homeBathDepth/2;z<_homeBathDepth/2;z++) Quad(new(x,y,z),new(x,y,z+1),new(x,y+1,z+1),new(x,y+1,z),wallUv,wallIndices);
            var mesh=new Mesh{name="Bathroom native wall and floor tiles"};mesh.SetVertices(vertices);mesh.SetUVs(0,uvs);mesh.subMeshCount=2;mesh.SetTriangles(floorIndices,0);mesh.SetTriangles(wallIndices,1);mesh.RecalculateBounds();mesh.RecalculateNormals();
            _homeBathRoom.AddComponent<MeshFilter>().sharedMesh=mesh;_homeBathRoom.AddComponent<MeshRenderer>().sharedMaterials=new[]{materials[floor],materials[wall]};_ownedManagedFloorMeshes.Add(mesh);
        }
    }
}
