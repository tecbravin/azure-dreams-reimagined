using System;
using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const int RetailTownSize = 128;
        [SerializeField] private GameObject _retailTownModel;
        [SerializeField] private TextAsset _retailTownHeights;
        [SerializeField] private Material[] _retailTownMaterials;
        private byte[] _townHeights;
        private readonly List<TownHouseDoor> _townFacadeDoors = new();
        private const string FacadeDoorResources = "Town/Retail/Doors/";

        [Serializable] private sealed class FacadeDoorCatalog { public FacadeDoorPlacement[] doors; }
        [Serializable] private struct FacadeDoorPlacement
        {
            public float x, z, width, height;
            public string style, interior;
        }

        private readonly Dictionary<TownHouseDoor, string> _facadeInteriors = new();
        private TownWallSurface[] _townWallSurfaces;
        private TownWallSurface[] _townStepSurfaces;
        private TownWallSurface[] _townBridgeSurfaces;
        private TownRoundObstacle[] _townRoundObstacles;

        [Serializable] private struct TownRoundObstacle
        {
            public Vector2 center;
            public float radius, top;
        }
        private readonly Dictionary<int, float> _groundUnderTownWalls = new();

        [Serializable] private struct TownWallGround
        {
            public int index;
            public float height;
        }

        [Serializable] private sealed class TownWallData
        {
            public TownWallSurface[] surfaces;
            public TownWallSurface[] steps;
            public TownWallSurface[] bridge;
            public TownWallGround[] ground;
            public TownRoundObstacle[] props;
        }

        [Serializable] private struct TownWallSurface
        {
            public Vector2 a, b, c;
            public float top, heightB, heightC;
            private Rect _bounds;

            public void PrepareBounds()
            {
                Vector2 min = Vector2.Min(a, Vector2.Min(b, c));
                Vector2 max = Vector2.Max(a, Vector2.Max(b, c));
                _bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }

            public float HeightAt(Vector2 point)
            {
                float area = Cross(b - a, c - a);
                float weightB = Cross(point - a, c - a) / area;
                float weightC = Cross(b - a, point - a) / area;
                return top + weightB * (heightB - top) + weightC * (heightC - top);
            }

            public bool Touches(Vector2 point, float radius)
            {
                if (point.x < _bounds.xMin - radius || point.x > _bounds.xMax + radius ||
                    point.y < _bounds.yMin - radius || point.y > _bounds.yMax + radius) return false;
                float ab = Cross(b - a, point - a);
                float bc = Cross(c - b, point - b);
                float ca = Cross(a - c, point - c);
                if ((ab >= 0 && bc >= 0 && ca >= 0) || (ab <= 0 && bc <= 0 && ca <= 0)) return true;
                float radiusSquared = radius * radius;
                return SegmentDistanceSquared(point, a, b) <= radiusSquared ||
                    SegmentDistanceSquared(point, b, c) <= radiusSquared ||
                    SegmentDistanceSquared(point, c, a) <= radiusSquared;
            }

            private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

            private static float SegmentDistanceSquared(Vector2 point, Vector2 start, Vector2 end)
            {
                Vector2 edge = end - start;
                float fraction = Mathf.Clamp01(Vector2.Dot(point - start, edge) / edge.sqrMagnitude);
                return (point - start - edge * fraction).sqrMagnitude;
            }
        }
        private bool UsingRetailTown => _townHeights != null;

        private void BuildTownMap()
        {
            _townHeights = _retailTownHeights ? _retailTownHeights.bytes : null;
            if (!_retailTownModel || _townHeights == null || _townHeights.Length != RetailTownSize * RetailTownSize * 2)
            {
                _townHeights = null;
                _gateCell = new Vector2Int(10, 5);
                BuildBase();
                AddTownLandmarks();
                return;
            }

            LoadTownWallSurfaces();

            // The capture stores two horizontal axes in [-1,1]. Unity's OBJ
            // importer mirrors X; the remaining Z conversion follows the capture grid.
            var town = Instantiate(_retailTownModel, new Vector3(64f, 0f, 64f), Quaternion.identity, transform);
            town.name = "Monsbaiya - minimum development capture";
            town.transform.localScale = new Vector3(64f, 64f, -64f);
            if (_retailTownMaterials != null)
            {
                foreach (var renderer in town.GetComponentsInChildren<Renderer>())
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (!materials[i]) continue;
                        string name = materials[i].name.Replace(" (Instance)", "");
                        foreach (var material in _retailTownMaterials)
                            if (material && material.name == name) { materials[i] = material; break; }
                    }
                    renderer.sharedMaterials = materials;
                }
            }
            _generated.Add(town);
            ApplyTownConstruction(town);
            PlaceRetailFacadeDoors();
            _gateCell = new Vector2Int(64, 25);
        }

        private void PlaceRetailFacadeDoors()
        {
            var catalog = AssetMode.Load<TextAsset>(FacadeDoorResources + "placements");
            var material = AssetMode.Load<Material>(FacadeDoorResources + "FacadeDoor");
            if (!catalog || !material)
            {
                Debug.LogWarning("Town facade doors could not load their placement catalog or material.");
                return;
            }
            var doors = JsonUtility.FromJson<FacadeDoorCatalog>(catalog.text)?.doors;
            if (doors == null) return;

            var textures = new Dictionary<string, Texture2D>();
            foreach (var authoredPlacement in doors)
            {
                var placement = authoredPlacement;
                var upgradedFacade = placement.interior == "KohHome" ? BuiltKohHome() :
                    placement.interior == "Interior_0f_hospital" ? _builtTownLots.Find(project => project.Id == "hospital") : null;
                // Construction replaces static town geometry, but the opening door
                // is a separate actor. Keep it at the upgraded facade's opening
                // and route the clinic to its renovated interior.
                if (upgradedFacade != null)
                {
                    placement.x = upgradedFacade.Entrance.x;
                    placement.z = upgradedFacade.Entrance.y;
                    placement.interior = upgradedFacade.Interior;
                    if (upgradedFacade.Id == "home_2") placement.height = 1.5f;
                }
                else if (IsBuiltLot(new Vector2(placement.x, placement.z))) continue;
                if (!textures.TryGetValue(placement.style, out var texture))
                {
                    texture = AssetMode.Load<Texture2D>(FacadeDoorResources + placement.style);
                    textures.Add(placement.style, texture);
                }
                if (!texture) continue;
                var position = new Vector3(placement.x, 0f, placement.z + 0.035f);
                var door = TownHouseDoor.CreateFacadeLeaf(transform, position, placement.style + " door",
                    placement.width > 0f ? placement.width : 1f,
                    placement.height > 0f ? placement.height : 1.75f, material, texture);
                _generated.Add(door.gameObject);
                _townFacadeDoors.Add(door);
                if (!string.IsNullOrEmpty(placement.interior))
                    _facadeInteriors.Add(door, placement.interior);
                // Unlocking Cherrl's house permits interaction; it does not leave
                // the door physically open whenever the town is rebuilt.
            }
        }

        private void LoadTownWallSurfaces()
        {
            if (_townWallSurfaces != null) return;
            var asset = AssetMode.Load<TextAsset>("Town/Retail/town_stone_walls");
            if (!asset) return;
            var walls = JsonUtility.FromJson<TownWallData>(asset.text);
            _townWallSurfaces = walls.surfaces;
            _townStepSurfaces = walls.steps;
            _townBridgeSurfaces = walls.bridge;
            _townRoundObstacles = walls.props;
            for (int i = 0; i < _townWallSurfaces.Length; i++) _townWallSurfaces[i].PrepareBounds();
            if (_townStepSurfaces != null)
                for (int i = 0; i < _townStepSurfaces.Length; i++) _townStepSurfaces[i].PrepareBounds();
            if (_townBridgeSurfaces != null)
                for (int i = 0; i < _townBridgeSurfaces.Length; i++) _townBridgeSurfaces[i].PrepareBounds();
            foreach (var sample in walls.ground) _groundUnderTownWalls.Add(sample.index, sample.height);
        }

        private float TownWallTop(Vector3 position, float radius)
        {
            if (!UsingRetailTown || _interior || _townWallSurfaces == null) return float.NegativeInfinity;
            float top = float.NegativeInfinity;
            var point = new Vector2(position.x, position.z);
            if (IsBuiltLot(point)) return top;
            foreach (var wall in _townWallSurfaces)
                if (wall.top > top && wall.Touches(point, radius)) top = wall.top;
            return top;
        }

        private float TownRoundObstacleTop(Vector3 position, float radius)
        {
            if (!UsingRetailTown || _interior || _townRoundObstacles == null) return float.NegativeInfinity;
            float top = float.NegativeInfinity;
            var point = new Vector2(position.x, position.z);
            if (IsBuiltLot(point)) return top;
            foreach (var obstacle in _townRoundObstacles)
            {
                float reach = obstacle.radius + radius;
                if (obstacle.top > top && (point - obstacle.center).sqrMagnitude <= reach * reach)
                    top = obstacle.top;
            }
            return top;
        }

        private bool RetailTownGroundOpen(Vector3 position, float radius)
        {
            if (TryCasinoRampHeight(position,out _,out _)) return true;
            if (!float.IsNegativeInfinity(HomeRoofGround(position))) return true;
            if (InFortuneHouse) return FortuneGround(position) > -0.75f && !FortuneBlocks(position, radius);
            if (FountainWalkwayOpen(position, radius)) return true;
            // The captured height grid contains water beneath all three bridges.
            // A bridge deck is a valid walking surface even when that grid cell
            // is underwater; its own triangle bounds keep Koh on the walkway.
            if (!_interior && TownBridgeTop(position) > float.NegativeInfinity) return true;
            float height = RetailTownGround(position);
            float maxGroundHeight = _interior && _interior.interiorId == FortuneHouseInterior ? 2f :
                _interior && (_interior.interiorId==PoolOpenRoom || _interior.interiorId==PoolClosedRoom) ? 1.01f : .65f;
            if (height < -0.75f || height > maxGroundHeight) return false;
            if (_interior) return true;
            // The same radius used for props also keeps Koh's sides clear of
            // buildings represented by solid samples in the original height grid.
            return RetailTownGround(position + Vector3.left * radius) <= 0.65f &&
                RetailTownGround(position + Vector3.right * radius) <= 0.65f &&
                RetailTownGround(position + Vector3.forward * radius) <= 0.65f &&
                RetailTownGround(position + Vector3.back * radius) <= 0.65f;
        }

        private float TownBridgeTop(Vector3 position)
        {
            if (_interior || _townBridgeSurfaces == null) return float.NegativeInfinity;
            var point = new Vector2(position.x, position.z);
            if (IsBuiltLot(point)) return float.NegativeInfinity;
            float top = float.NegativeInfinity;
            foreach (var surface in _townBridgeSurfaces)
            {
                if (!surface.Touches(point, 0f)) continue;
                // Bridge meshes contain overlapping triangles for the deck and
                // their side faces. Use the upper walkable face independent of
                // triangle ordering so seams never send Koh under the bridge.
                top = Mathf.Max(top, surface.HeightAt(point));
            }
            return top;
        }

        private float TownSteppingStoneTop(Vector3 position)
        {
            if (!UsingRetailTown || _interior || _townStepSurfaces == null) return float.NegativeInfinity;
            var point = new Vector2(position.x, position.z);
            if (IsBuiltLot(point)) return float.NegativeInfinity;
            foreach (var surface in _townStepSurfaces)
                if (surface.Touches(point, TownActorRadius * 0.4f)) return surface.top;
            return float.NegativeInfinity;
        }

        private float RetailTownGround(Vector3 position)
        {
            if (TryCasinoRampHeight(position,out float casinoRamp,out _)) return casinoRamp;
            float homeRoof = HomeRoofGround(position);
            if (!float.IsNegativeInfinity(homeRoof)) return homeRoof;
            // The native height grid marks the changing-room roof as solid; its blue-door recess is deck-height.
            if (InRestoredPool && Mathf.Abs(position.x - PoolChangingRoomPosition.x) <= 0.45f &&
                position.z >= 13f && position.z <= 15f) return 1f;
            if (InFortuneHouse) return FortuneGround(position);
            float fountainGround = FountainSurface(position, 1.01f);
            if (!float.IsNegativeInfinity(fountainGround)) return fountainGround;
            int x = Mathf.RoundToInt(position.x);
            int z = Mathf.RoundToInt(position.z);
            if (!_interior && _constructionGround.TryGetValue(z * RetailTownSize + x, out float builtGround))
                return builtGround;
            if (!_interior && x >= 0 && z >= 0 && x < RetailTownSize && z < RetailTownSize &&
                _groundUnderTownWalls.TryGetValue(z * RetailTownSize + x, out float ground)) return ground;
            float height = RetailTownHeight(position);
            if (_interior || height <= 0.65f) return height;

            // The retail grid samples the roof at the doorway. The captured recess
            // has a ground-level floor. Restore it only within the linked doorway.
            foreach (var door in _facadeInteriors.Keys)
            {
                if (!door) continue;
                var threshold = door.Threshold;
                if (Mathf.Abs(position.x - threshold.x) <= 0.52f &&
                    position.z >= threshold.z - TownActorRadius && position.z <= threshold.z + 0.55f)
                    return 0f;
            }
            return height;
        }

        private float RetailTownHeight(Vector3 position)
        {
            int x = Mathf.RoundToInt(position.x);
            int z = Mathf.RoundToInt(position.z);
            if (x < 0 || z < 0 || x >= RetailTownSize || z >= RetailTownSize) return -128f;
            if (!_interior && _constructionGround.TryGetValue(z * RetailTownSize + x, out float builtGround))
                return builtGround;
            int index = (z * RetailTownSize + x) * 2;
            return unchecked((short)(_townHeights[index] | _townHeights[index + 1] << 8)) / 256f;
        }

        private Vector3 RetailTownActorPosition(Vector3 position, float offset)
        {
            float fountainGround = FountainSurface(position, 1.01f);
            position.y = (float.IsNegativeInfinity(fountainGround) ? RetailTownHeight(position) : fountainGround) + offset;
            return position;
        }
    }
}
