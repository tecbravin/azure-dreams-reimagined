using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    // Tile sheets of the managed (non-retail) floor mesh, loaded on demand from
    // Resources/Tower/FloorTiles. They used to be serialized scene references, which
    // loaded all 20 floor sheets (1024x1024 each) as soon as the scene opened even
    // though a floor only ever draws one of them - and only when it has no retail mesh.
    public sealed partial class TowerGameController
    {
        private const string TowerTilesPath = "Tower/FloorTiles/TowerFloor_Monster_Tower_";
        private static readonly string[] UpperTowerTileLabels =
        {
            "12F-13F", "14F-15F", "16F-17F", "18F-19F", "20F-21F", "22F-23F", "24F-25F",
            "26F-27F", "28F-29F", "30F-31F", "32F-33F", "34F-35F", "36F-37F", "38F-39F"
        };
        private readonly Dictionary<string, Material> _loadedTowerTiles = new();
        private readonly List<int> _towerTileTextureIds = new();

        /// <summary>Floors 1-2 sheet (tower entrance threshold, ice rocks).</summary>
        private Material EarlyTowerTiles => TowerTiles("1F-2F");

        /// <summary>Same floor-to-sheet mapping the serialized fields encoded.</summary>
        private static string TowerTileLabel(int floor)
        {
            if (floor <= 2) return "1F-2F";
            if (floor == 3) return "3F";
            if (floor <= 5) return "4F-5F";
            if (floor <= 7) return "6F-7F";
            if (floor <= 9) return "8F-9F";
            if (floor <= 11) return "10F-11F";
            if (floor == TowerFloorRules.BeldoFloor) return UpperTowerTileLabels[^1];
            int upperIndex = floor is >= 12 and <= 39 ? (floor - 12) / 2 : -1;
            return upperIndex >= 0 && upperIndex < UpperTowerTileLabels.Length ? UpperTowerTileLabels[upperIndex] : null;
        }

        private Material TowerTilesForFloor(int floor)
        {
            string label = TowerTileLabel(floor);
            return label == null ? null : TowerTiles(label);
        }

        private Material TowerTiles(string label)
        {
            if (_loadedTowerTiles.TryGetValue(label, out var material) && material) return material;
            material = AssetMode.Load<Material>(TowerTilesPath + label);
            if (material) _loadedTowerTiles[label] = material;
            return material;
        }

        /// <summary>
        /// Unloads every loaded sheet except <paramref name="keepLabel"/> (material and its
        /// textures). Called when generated geometry is cleared, so nothing should still draw
        /// with them; if something does, Unity reloads the asset from disk on access.
        /// </summary>
        private void ReleaseTowerTiles(string keepLabel = null)
        {
            if (_loadedTowerTiles.Count == 0) return;
            Material kept = null;
            foreach (var entry in _loadedTowerTiles)
            {
                var material = entry.Value;
                if (!material) continue;
                if (entry.Key == keepLabel)
                {
                    kept = material;
                    continue;
                }
                material.GetTexturePropertyNameIDs(_towerTileTextureIds);
                foreach (int id in _towerTileTextureIds)
                {
                    var texture = material.GetTexture(id);
                    if (texture) Resources.UnloadAsset(texture);
                }
                Resources.UnloadAsset(material);
            }
            _towerTileTextureIds.Clear();
            _loadedTowerTiles.Clear();
            if (kept) _loadedTowerTiles[keepLabel] = kept;
        }
    }
}
