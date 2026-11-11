using System;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public enum TowerFloorTheme
    {
        StoneDungeon,    // Floors 1-10
        CrystalCave,     // Floors 11-20
        LavaChamber,     // Floors 21-30
        IcePalace,       // Floors 31-40
        DarkTower        // Floors 41+
    }

    [CreateAssetMenu(fileName = "TowerFloorTheme", menuName = "Azure Dreams/Tower Floor Theme")]
    public class TowerFloorThemeData : ScriptableObject
    {
        public TowerFloorTheme theme;
        public Material floorMaterial;
        public Material wallMaterial;
        public Material backgroundMaterial;
        public Color ambientColor = new Color(0.3f, 0.35f, 0.42f);
        public Color fogColor = new Color(0.5f, 0.5f, 0.5f);
        public float fogDensity = 0.01f;
        public GameObject[] fxPrefabs;
        public AudioClip ambientSound;
        public int minFloor = 1;
        public int maxFloor = 10;
    }

    public static class TowerFloorThemeResolver
    {
        // Based on decomp evidence: floors grouped in 5-floor sets.
        // D_8008146C[0] / 5 → resource group + 1 (func_80019684 = dungeonLoadObjectIntoAlternateBuffer)
        // Audio/monsters/textures change at 5-floor boundaries (N=4,9,14,...)
        // Mode 1: preserve sequence (tutorial). Mode 2: random group (second tower, tile_type==4)
        public static TowerFloorTheme GetThemeForFloor(int floorNumber)
        {
            int group = (floorNumber - 1) / 5; // 0-based group
            switch (group)
            {
                case 0: return TowerFloorTheme.StoneDungeon;    // Floors 1-5
                case 1: return TowerFloorTheme.StoneDungeon;    // Floors 6-10
                case 2: return TowerFloorTheme.CrystalCave;     // Floors 11-15
                case 3: return TowerFloorTheme.CrystalCave;     // Floors 16-20
                case 4: return TowerFloorTheme.LavaChamber;     // Floors 21-25
                case 5: return TowerFloorTheme.LavaChamber;     // Floors 26-30
                case 6: return TowerFloorTheme.IcePalace;       // Floors 31-35
                case 7: return TowerFloorTheme.IcePalace;       // Floors 36-40
                default: return TowerFloorTheme.DarkTower;      // Floors 41+
            }
        }

        // Map floor number to Spriters Resource tower texture filename
        public static string GetTowerTextureName(int floorNumber)
        {
            if (floorNumber < 1 || floorNumber > 39) return null;
            if (floorNumber <= 2)
                return "PlayStation - Azure Dreams - Tower - Monster Tower 1F-2F.png";
            if (floorNumber == 3)
                return "PlayStation - Azure Dreams - Tower - Monster Tower 3F.png";
            int first = floorNumber - floorNumber % 2;
            return $"PlayStation - Azure Dreams - Tower - Monster Tower {first}F-{first + 1}F.png";
        }

        public static Color GetThemeAmbientColor(TowerFloorTheme theme)
        {
            switch (theme)
            {
                case TowerFloorTheme.StoneDungeon: return new Color(0.3f, 0.35f, 0.42f);
                case TowerFloorTheme.CrystalCave: return new Color(0.2f, 0.3f, 0.5f);
                case TowerFloorTheme.LavaChamber: return new Color(0.5f, 0.2f, 0.1f);
                case TowerFloorTheme.IcePalace: return new Color(0.4f, 0.5f, 0.6f);
                case TowerFloorTheme.DarkTower: return new Color(0.1f, 0.1f, 0.15f);
                default: return Color.gray;
            }
        }

        public static Color GetThemeFogColor(TowerFloorTheme theme)
        {
            switch (theme)
            {
                case TowerFloorTheme.StoneDungeon: return new Color(0.4f, 0.4f, 0.45f);
                case TowerFloorTheme.CrystalCave: return new Color(0.3f, 0.4f, 0.6f);
                case TowerFloorTheme.LavaChamber: return new Color(0.6f, 0.3f, 0.1f);
                case TowerFloorTheme.IcePalace: return new Color(0.7f, 0.8f, 0.9f);
                case TowerFloorTheme.DarkTower: return new Color(0.15f, 0.15f, 0.2f);
                default: return Color.gray;
            }
        }

        public static string GetThemeName(TowerFloorTheme theme)
        {
            return theme.ToString();
        }
    }
}
