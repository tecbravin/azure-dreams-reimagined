using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private bool InFortuneHouse => _interior && _interior.interiorId == FortuneHouseInterior;
        private readonly List<TownInteriorScene.FurnitureSurface> _fortuneFloors = new();
        private readonly List<(Vector3 A, Vector3 B, Vector3 C)> _fortuneRamps = new();
        // The side stone stairs lead outside the room. Only the central exit
        // remains accessible; the internal wooden stairs are walking surfaces.
        private static readonly Rect[] FortuneSolids =
        {
            Rect.MinMaxRect(8.85f, 9f, 9f, 17f), Rect.MinMaxRect(23f, 9f, 23.15f, 17f),
            Rect.MinMaxRect(9f, 8.85f, 23f, 9f),
            Rect.MinMaxRect(10f, 16.95f, 15f, 17.05f), Rect.MinMaxRect(17f, 16.95f, 22f, 17.05f),
            Rect.MinMaxRect(14f, 9f, 18f, 11f),
            Rect.MinMaxRect(9f, 14f, 10f, 17.1f), Rect.MinMaxRect(22f, 14f, 23f, 17.1f)
        };
        private static bool TouchesFortuneRect(Rect rect, Vector3 p, float radius) =>
            p.x > rect.xMin - radius && p.x < rect.xMax + radius &&
            p.z > rect.yMin - radius && p.z < rect.yMax + radius;

        private bool FortuneBlocks(Vector3 position, float radius)
        {
            if (position.x < 8.75f + radius || position.x > 23.25f - radius ||
                position.z < 9f + radius || position.z > 17f - radius) return true;
            foreach (var rect in FortuneSolids)
                if (TouchesFortuneRect(rect, position, radius)) return true;
            return false;
        }

        private static bool FortuneStair(Vector3 p) => p.z >= 12f && p.z <= 14.05f &&
            (p.x >= 11f && p.x <= 13f || p.x >= 19f && p.x <= 21f);

        private float FortuneFurnitureTop(Vector3 p, float radius) =>
            TouchesFortuneRect(Rect.MinMaxRect(15.125f, 13f, 16.875f, 13.875f), p, radius)
                ? 2.7500032f : float.NegativeInfinity;

        private void LoadFortuneFloors()
        {
            if (_fortuneFloors.Count > 0) return;
            var source = Resources.Load<TextAsset>("Town/Interiors/" + FortuneHouseInterior + "/geometry");
            if (!source) return;
            var geometry = JsonUtility.FromJson<TownInteriorScene.Geometry>(source.text);
            foreach (var surface in geometry.surfaces)
            for (int i = 0; i + 2 < surface.triangles.Length; i += 3)
            {
                var a = geometry.vertices[surface.triangles[i]];
                var b = geometry.vertices[surface.triangles[i + 1]];
                var c = geometry.vertices[surface.triangles[i + 2]];
                var center = (a + b + c) / 3f;
                if (FortuneStair(center) && Mathf.Min(a.y,b.y,c.y) >= 1.999f &&
                    Mathf.Max(a.y,b.y,c.y) <= 4.001f && Mathf.Abs(Vector3.Cross(b-a,c-a).y) > .001f)
                    _fortuneRamps.Add((a,b,c));
                bool upperDeck = Mathf.Abs(a.y-4f) < .001f &&
                    center.z >= 9f && center.z <= 12.751f;
                if ((!upperDeck && a.y > 2.001f) || Mathf.Abs(a.y - b.y) > .001f || Mathf.Abs(a.y - c.y) > .001f) continue;
                // Native floor and stair treads are axis-aligned rectangular quads.
                var bounds = Rect.MinMaxRect(Mathf.Min(a.x, b.x, c.x), Mathf.Min(a.z, b.z, c.z),
                    Mathf.Max(a.x, b.x, c.x), Mathf.Max(a.z, b.z, c.z));
                if (bounds.width < .01f || bounds.height < .01f) continue;
                _fortuneFloors.Add(new TownInteriorScene.FurnitureSurface { footprint = bounds, top = a.y });
            }
        }

        private float FortuneGround(Vector3 position)
        {
            LoadFortuneFloors();
            float top = -128f;
            foreach (var floor in _fortuneFloors)
                if (floor.footprint.Contains(new Vector2(position.x, position.z))) top = Mathf.Max(top, floor.top);
            foreach (var ramp in _fortuneRamps)
            {
                float denominator = (ramp.B.z-ramp.C.z)*(ramp.A.x-ramp.C.x) +
                    (ramp.C.x-ramp.B.x)*(ramp.A.z-ramp.C.z);
                float a = ((ramp.B.z-ramp.C.z)*(position.x-ramp.C.x) +
                    (ramp.C.x-ramp.B.x)*(position.z-ramp.C.z)) / denominator;
                float b = ((ramp.C.z-ramp.A.z)*(position.x-ramp.C.x) +
                    (ramp.A.x-ramp.C.x)*(position.z-ramp.C.z)) / denominator;
                float c = 1f-a-b;
                if (a >= -.0001f && b >= -.0001f && c >= -.0001f)
                    top = Mathf.Max(top,a*ramp.A.y+b*ramp.B.y+c*ramp.C.y);
            }
            return top;
        }

        private void SetupFortuneHouse()
        {
            if (!InFortuneHouse) return;
            LoadFortuneFloors();
            foreach (var rect in FortuneSolids)
            {
                var body = new GameObject("Fortune house fixed wall or furnishing");
                body.transform.SetParent(transform, false);
                body.transform.position = new Vector3(rect.center.x, 4f, rect.center.y);
                body.AddComponent<BoxCollider>().size = new Vector3(rect.width, 8f, rect.height);
                _generated.Add(body);
            }
            // Decorative crystal ball: no inventory record, pot or lifting component.
            CreatePrimitive(PrimitiveType.Sphere, "Shiela fixed crystal ball",
                new Vector3(16f, 3.05f, 13.45f), Vector3.one * .6f, _accentMaterial, true);
        }
    }
}
