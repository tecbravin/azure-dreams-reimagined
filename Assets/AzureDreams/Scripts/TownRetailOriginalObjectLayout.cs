using System;
using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Original Monsbaiya exterior lots.  The coordinates are the native
    /// 128x128 Town-grid records at TOWN.BIN / 0x800D2EA4; one Unity world
    /// unit corresponds to one Town grid cell here.
    ///
    /// This is deliberately a placement table, not an inferred building-name
    /// table for every slot.  Only entries whose exterior asset is used by the
    /// game are named.  It keeps construction meshes anchored to the same lot
    /// selected by the original game's building system.
    /// </summary>
    internal static class TownRetailOriginalObjectLayout
    {
        internal readonly struct Lot
        {
            internal readonly int Slot;
            internal readonly Rect RawBounds;
            // True for the five interchangeable 16x14 construction slots
            // (FlexibleConstructionLots in TowerGameController.Construction.cs).
            // Those slots are a shared generic parcel that can host library,
            // alley, gym, theater or casino; each building's hand-tuned
            // BuildingProject.Bounds is intentionally smaller than the full
            // parcel (sidewalk/margin around the structure), so checking that
            // the mesh "covers" the whole parcel does not apply here. Keep the
            // entry (for the lot-number label in ObjectName) but skip the
            // containment check.
            internal readonly bool FlexibleParcel;

            internal Lot(int slot, float x, float z, float width, float height, bool flexibleParcel = false)
            {
                Slot = slot;
                RawBounds = new Rect(x, z, width, height);
                FlexibleParcel = flexibleParcel;
            }

            internal bool IsContainedBy(Rect visualBounds) =>
                FlexibleParcel ||
                (visualBounds.xMin <= RawBounds.xMin && visualBounds.xMax >= RawBounds.xMax &&
                 visualBounds.yMin <= RawBounds.yMin && visualBounds.yMax >= RawBounds.yMax);
        }

        private static readonly Dictionary<string, Lot> Lots = new(StringComparer.Ordinal)
        {
            // id, original plot slot, plot x/z, width/height
            { "home_1",  new Lot(11, 50, 44, 12, 14) },
            { "home_2",  new Lot(11, 50, 44, 12, 14) },
            { "hospital",new Lot(17, 10, 62, 12, 8) },
            { "temple",  new Lot(2,  64, 28, 18, 14) },
            { "fountain",new Lot(16, 56, 64, 16, 16) },
            { "circuit", new Lot(1,   8, 24, 16, 18) },
            { "arcade",  new Lot(18,106, 62, 12, 8) },
            // The five interchangeable flexible-construction-slot buildings:
            // their lot is a shared generic parcel, not a dedicated footprint.
            { "library", new Lot(7,  88, 28, 16, 14, flexibleParcel: true) },
            { "alley",   new Lot(5,  40, 28, 16, 14, flexibleParcel: true) },
            { "gym",     new Lot(28, 24, 86, 16, 14, flexibleParcel: true) },
            { "theater", new Lot(4,  24, 28, 16, 14, flexibleParcel: true) },
            { "casino",  new Lot(31, 88, 86, 16, 14, flexibleParcel: true) }
        };

        internal static bool TryGet(string objectId, out Lot lot) => Lots.TryGetValue(objectId, out lot);

        internal static string ObjectName(string fallback, string objectId)
        {
            return TryGet(objectId, out Lot lot)
                ? $"{fallback} — retail lot {lot.Slot} ({lot.RawBounds.xMin:0},{lot.RawBounds.yMin:0})"
                : fallback;
        }

        internal static void ValidateVisualBounds(string objectId, Rect visualBounds)
        {
            if (TryGet(objectId, out Lot lot) && !lot.IsContainedBy(visualBounds))
                Debug.LogError($"Retail Town object '{objectId}' no longer covers original lot {lot.Slot}. " +
                    $"Lot {lot.RawBounds}, visual bounds {visualBounds}.");
        }
    }
}
