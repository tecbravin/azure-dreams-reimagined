using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private enum MapView { Sidebar, Preview, Full }

        private const float MapHoldSeconds = 0.55f;
        private const float MapTransitionSeconds = 0.22f;

        [SerializeField] private string _mapBinding = "<Keyboard>/rightShift";

        private InputAction _mapAction;
        private Texture2D _mapTexture;
        private bool _seeAllItems;
        private bool _seeMonsters;
        private bool _seeElevator;
        private readonly Plane[] _itemVisibilityPlanes = new Plane[6];
        private readonly Dictionary<Vector2Int, GameObject> _revealedMapItems = new();
        public void ActivateSeeAllLoupe() => _seeAllItems = true;

        private void DiscoverMinimapItems()
        {
            if (_area != Area.Tower || KohIsBlind || (!_seeAllItems && !_gameCamera)) return;
            if (!_seeAllItems) GeometryUtility.CalculateFrustumPlanes(_gameCamera, _itemVisibilityPlanes);
            foreach (var pair in _itemMarkers)
                DiscoverItem(pair.Key, pair.Value);
            if (_hasLoot) DiscoverItem(_lootCell, _salvageMarker);

            void DiscoverItem(Vector2Int cell, GameObject view)
            {
                if (!view || !view.activeInHierarchy) return;
                if (_revealedMapItems.TryGetValue(cell, out var known) && known == view) return;
                if (!_seeAllItems)
                {
                    var renderer = view.GetComponentInChildren<Renderer>();
                    if (!renderer || !GeometryUtility.TestPlanesAABB(_itemVisibilityPlanes, renderer.bounds)) return;
                }
                _revealedMapItems[cell] = view;
            }
        }

        private void DrawVisibleMapItems(Rect map, float opacity)
        {
            var color = new Color(0.1f, 0.45f, 1f, opacity);
            foreach (var pair in _itemMarkers)
                if (IsKnown(pair.Key, pair.Value)) DrawMinimapMarker(map, pair.Key, color);
            if (_hasLoot && IsKnown(_lootCell, _salvageMarker)) DrawMinimapMarker(map, _lootCell, color);

            bool IsKnown(Vector2Int cell, GameObject view) => view && view.activeInHierarchy &&
                (_seeAllItems || (_revealedMapItems.TryGetValue(cell, out var known) && known == view));
        }

        private MapView _mapView;
        private float _mapHeldSeconds;
        private float _mapExpansion;
        private float _mapBlackout;

        private void UpdateMinimap()
        {
            if (_area != Area.Tower || !_player || _mapAction == null) return;
            if (KohIsBlind)
            {
                CloseMinimap();
                return;
            }

            if (_mapAction.WasPressedThisFrame())
            {
                _mapView = _mapView == MapView.Full ? MapView.Sidebar : MapView.Preview;
                _mapHeldSeconds = 0f;
            }

            float delta = Time.unscaledDeltaTime;
            if (_mapView == MapView.Preview)
            {
                if (!_mapAction.IsPressed())
                    _mapView = MapView.Sidebar;
                else if ((_mapHeldSeconds += delta) >= MapHoldSeconds)
                    _mapView = MapView.Full;
            }

            _mapExpansion = Mathf.MoveTowards(_mapExpansion, _mapView == MapView.Sidebar ? 0f : 1f,
                delta / MapTransitionSeconds);
            _mapBlackout = Mathf.MoveTowards(_mapBlackout, _mapView == MapView.Full ? 1f : 0f,
                delta / MapTransitionSeconds);
        }

        private const int CorridorRevealSteps = 6;
        private static readonly Vector2Int[] MinimapSteps =
            { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        private static readonly Color32 MapFloorColor = new Color32(65, 190, 94, 255);
        private readonly Queue<(Vector2Int Cell, int Steps)> _mapSearch = new();
        private readonly HashSet<Vector2Int> _mapSearchSeen = new();
        private readonly HashSet<int> _revealedMapRooms = new();
        private Vector2Int? _mapLastPlayerCell;
        private Color32[] _mapPixels;

        private bool IsMinimapWalkable(Vector2Int cell) =>
            UsesManagedTowerLayout && _towerFloorLayout != null
                ? _towerFloorLayout.IsWalkable(cell.x, cell.y)
                : InsideMap(cell) && !_walls.Contains(cell);

        private void UpdateMinimapTerrain()
        {
            if (_area != Area.Tower || !_player || KohIsBlind) return;
            int size = ActiveMapSize;
            bool fullFloor = _floor is 2 or TowerFloorRules.BeldoFloor;
            if (!_mapTexture)
            {
                _mapTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "Tower minimap",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                _mapPixels = new Color32[size * size];
                if (fullFloor)
                {
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                            if (IsMinimapWalkable(new Vector2Int(x, y)))
                                _mapPixels[y * size + x] = MapFloorColor;
                    _mapTexture.SetPixels32(_mapPixels);
                    _mapTexture.Apply(false, true);
                }
            }
            if (fullFloor || _mapLastPlayerCell == _playerCell) return;
            _mapLastPlayerCell = _playerCell;

            int roomIndex = UsesManagedTowerLayout ? BellRoomAt(_playerCell) : -1;
            if (roomIndex >= 0 && _revealedMapRooms.Add(roomIndex))
            {
                var room = _towerFloorLayout.Dungeon.Rooms[roomIndex];
                for (int y = room.Y; y < room.Y + room.Height; y++)
                    for (int x = room.X; x < room.X + room.Width; x++)
                        if (IsMinimapWalkable(new Vector2Int(x, y)))
                            _mapPixels[y * size + x] = MapFloorColor;
            }

            // Search along the floor so nearby corridors behind walls stay hidden.
            _mapSearch.Clear();
            _mapSearchSeen.Clear();
            _mapSearchSeen.Add(_playerCell);
            _mapSearch.Enqueue((_playerCell, 0));
            while (_mapSearch.Count > 0)
            {
                var (cell, steps) = _mapSearch.Dequeue();
                if (IsMinimapWalkable(cell) && (!UsesManagedTowerLayout || BellRoomAt(cell) < 0))
                    _mapPixels[cell.y * size + cell.x] = MapFloorColor;
                if (steps == CorridorRevealSteps) continue;
                foreach (var direction in MinimapSteps)
                {
                    var next = cell + direction;
                    if (IsMinimapWalkable(next) && _mapSearchSeen.Add(next))
                        _mapSearch.Enqueue((next, steps + 1));
                }
            }

            _mapTexture.SetPixels32(_mapPixels);
            _mapTexture.Apply(false, false);
        }


        private void ResetMinimap()
        {
            if (_mapTexture) Destroy(_mapTexture);
            _mapTexture = null;
            _mapPixels = null;
            _mapLastPlayerCell = null;
            _revealedMapRooms.Clear();
            _revealedMapItems.Clear();
            _seeAllItems = _seeMonsters = _seeElevator = false;
            CloseMinimap();
        }

        private void CloseMinimap()
        {
            _mapView = MapView.Sidebar;
            _mapHeldSeconds = _mapExpansion = _mapBlackout = 0f;
        }

        private void DrawMinimap()
        {
            if (_area != Area.Tower || KohIsBlind || !_mapTexture || Event.current.type != EventType.Repaint) return;

            float shortSide = Mathf.Min(Screen.width, Screen.height);
            float sidebarSize = shortSide * 0.20f;
            float mapSize = Mathf.Lerp(sidebarSize, shortSide * 0.76f, _mapExpansion);
            var center = Vector2.Lerp(new Vector2(Screen.width - shortSide * 0.035f - sidebarSize * 0.5f, Screen.height * 0.53f),
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), _mapExpansion);
            var map = new Rect(center.x - mapSize * 0.5f, center.y - mapSize * 0.5f, mapSize, mapSize);
            float opacity = Mathf.Lerp(Mathf.Lerp(0.78f, 0.42f, _mapExpansion), 1f, _mapBlackout);
            Color previousColor = GUI.color;
            GUI.color = Color.white;
            if (_mapBlackout > 0f)
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture,
                    ScaleMode.StretchToFill, true, 0f, new Color(0f, 0f, 0f, _mapBlackout), 0f, 0f);

            GUI.DrawTexture(new Rect(map.x - 5f, map.y - 5f, map.width + 10f, map.height + 10f),
                Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                new Color(0.02f, 0.10f, 0.04f, opacity * 0.58f), 0f, 0f);

            // Keep diagonal corners inside the panel and rotate world directions into screen space.
            float yaw = _gameCamera ? _gameCamera.transform.eulerAngles.y : _cameraFollowYaw;
            float radians = yaw * Mathf.Deg2Rad;
            float fittedSize = mapSize / (Mathf.Abs(Mathf.Cos(radians)) + Mathf.Abs(Mathf.Sin(radians)));
            map = new Rect(center.x - fittedSize * 0.5f, center.y - fittedSize * 0.5f, fittedSize, fittedSize);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(-yaw, center);
            GUI.DrawTexture(map, _mapTexture, ScaleMode.StretchToFill, true, 0f,
                new Color(1f, 1f, 1f, opacity), 0f, 0f);
            DrawVisibleMapItems(map, opacity);
            foreach (var cell in _trapMarkers.Keys)
                if (TrapAt(cell) is { IsRevealed: true } trap)
                    DrawMinimapMarker(map, cell, new Color(1f, 0.9f, 0.15f, opacity * (trap.IsActive ? 0.5f : 1f)));
            if (_seeMonsters)
                foreach (var enemy in _creatures) DrawMinimapMarker(map, enemy.Cell, new Color(0.9f, 0.2f, 0.7f, opacity));
            if (_seeElevator && _elevatorPlatform)
                DrawMinimapMarker(map, _stairCell, new Color(1f, 0.63f, 0.19f, opacity));
            DrawMinimapMarker(map, _playerCell, new Color(1f, 0.18f, 0.16f, opacity));
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        private void DrawMinimapMarker(Rect map, Vector2Int cell, Color color)
        {
            float tileSize = map.width / _mapTexture.width;
            float markerSize = Mathf.Max(3f, tileSize * 0.85f);
            float x = map.x + (cell.x + 0.5f) * tileSize;
            float y = map.yMax - (cell.y + 0.5f) * tileSize;
            GUI.DrawTexture(new Rect(x - markerSize * 0.5f, y - markerSize * 0.5f, markerSize, markerSize),
                Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, 0f);
        }
    }
}
