using UnityEngine;

namespace AzureDreamsReimagined.Editor
{
    public class TilemapData : ScriptableObject
    {
        public TowerFloorTheme theme;
        public Material floorMaterial;
        public Material wallMaterial;
        public Material backgroundMaterial;
        public Texture2D floorTexture;
        public Texture2D wallTexture;
        public float tileSize = 1.0f;
        public int mapWidth = 32;
        public int mapHeight = 32;
    }
}
