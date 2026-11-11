using System;
using UnityEngine;
using CoreDungeon = AzureDreams.Core.DungeonGenerator;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Generates a visual tilemap from dungeon grid data using floor tileset prefab.
    /// Attach to an empty GameObject in the scene and assign dungeon data source.
    /// </summary>
    public class TowerTilemapGenerator : MonoBehaviour
    {
        [Header("Grid Settings")]
        public int gridSize = 32;
        public float tileSize = 1.0f;

        [Header("Prefabs")]
        public GameObject floorTilePrefab;
        public GameObject wallTilePrefab;
        public GameObject stairsUpPrefab;
        public GameObject stairsDownPrefab;
        public GameObject trapPrefab;

        [Header("Dungeon Data Source")]
        private TowerFloorManager floorManager;

        public void SetFloorManager(TowerFloorManager manager) => floorManager = manager;

        private GameObject _tilemapRoot;

        /// <summary>Generate tilemap from current floor manager state.</summary>
        public void GenerateTilemap()
        {
            if (floorManager == null || floorManager.Dungeon == null)
            {
                Debug.LogError("[TowerTilemapGenerator] No dungeon data available");
                return;
            }

            ClearTilemap();

            _tilemapRoot = new GameObject("Tilemap_Floor" + floorManager.CurrentFloor);
            _tilemapRoot.transform.parent = transform;

            var dungeon = floorManager.Dungeon;
            for (int y = 0; y < gridSize; y++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    var tileType = dungeon.Grid[y, x];
                    GameObject prefab = GetPrefabForTile(tileType);
                    if (prefab == null) continue;

                    var pos = new Vector3(x * tileSize, 0, y * tileSize);
                    var tile = Instantiate(prefab, pos, Quaternion.identity, _tilemapRoot.transform);
                    tile.name = $"Tile_{x}_{y}_{tileType}";

                    // Apply height
                    int height = floorManager.GroundHeight(x, y);
                    if (height != 0)
                        tile.transform.position = new Vector3(x * tileSize, height * 0.5f, y * tileSize);
                }
            }

            Debug.Log($"[TowerTilemapGenerator] Generated tilemap for floor {floorManager.CurrentFloor}");
        }

        private GameObject GetPrefabForTile(CoreDungeon.TileType tileType)
        {
            switch (tileType)
            {
                case CoreDungeon.TileType.Floor:
                case CoreDungeon.TileType.Corridor:
                case CoreDungeon.TileType.Trap:
                    return floorTilePrefab;
                case CoreDungeon.TileType.Wall:
                    return wallTilePrefab;
                case CoreDungeon.TileType.StairsUp:
                    return stairsUpPrefab;
                case CoreDungeon.TileType.StairsDown:
                    return stairsDownPrefab;
                default:
                    return null;
            }
        }

        public void ClearTilemap()
        {
            if (_tilemapRoot)
            {
                DestroyImmediate(_tilemapRoot);
                _tilemapRoot = null;
            }
        }
    }
}
