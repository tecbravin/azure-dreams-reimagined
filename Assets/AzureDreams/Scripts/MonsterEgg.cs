using AzureDreams.Core;
using UnityEngine;
using AzureDreamsReimagined.Rendering;

namespace AzureDreamsReimagined
{
    // A prefab view and adapter over the four-byte inventory/save record.
    [RequireComponent(typeof(SpriteRenderer), typeof(CameraFacingSprite))]
    public sealed class MonsterEgg : MonoBehaviour
    {
        private const byte Format = 0x80;
        private const byte Identified = 0x40;
        public const byte Kind = 0x12;
        private static readonly string[] Colors = { "White", "Red", "Blue", "Green", "Yellow", "Purple", "Orange", "Pink" };
        [SerializeField, Range(0, 7)] private int _color;
        [SerializeField] private bool _identified;
        [SerializeField, Range(0, 100)] private int _heat = 15;
        [SerializeField] private byte _species;
        public string ColorName => Colors[_color];
        public bool IsIdentified => _identified;
        public int HeatPercent => _heat;
        public byte Species => _species;

        public static DungeonItemEntry Create(byte species, bool identified)
            => Encode(species, Random.Range(0, Colors.Length), identified, Random.Range(15, 99));

        internal static DungeonItemEntry Encode(byte species, int color, bool identified, int heat)
            => new(species, Kind, (sbyte)Mathf.Clamp(heat, 0, 100),
                (byte)(Format | (identified ? Identified : 0) | (color & 7)));

        public static bool IsManaged(DungeonItemEntry item) => (item.Flags & Format) != 0;
        public static bool IdentifiedInTower(DungeonItemEntry item) => IsManaged(item) && (item.Flags & Identified) != 0;
        public static string ColorOf(DungeonItemEntry item) => Colors[ColorIndex(item)];
        public static int HeatOf(DungeonItemEntry item) => IsManaged(item) ? Mathf.Clamp(item.Param, 0, 100) : Mathf.Clamp(item.Param, 15, 98);
        private static int ColorIndex(DungeonItemEntry item) => (IsManaged(item) ? item.Flags : item.Subtype) & 7;
        public static DungeonItemEntry Identify(DungeonItemEntry item) => Encode(item.Subtype, ColorIndex(item), true, HeatOf(item));
        public static DungeonItemEntry Warm(DungeonItemEntry item, int degrees)
            => Encode(item.Subtype, ColorIndex(item), IdentifiedInTower(item), HeatOf(item) + degrees);

        public void Bind(DungeonItemEntry item)
        {
            _color = ColorIndex(item);
            _identified = IdentifiedInTower(item);
            _heat = HeatOf(item);
            _species = item.Subtype;
        }
    }
}
