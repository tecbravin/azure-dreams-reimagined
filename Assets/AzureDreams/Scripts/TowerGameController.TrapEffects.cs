using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private readonly HashSet<Vector2Int> _sprungGoUpTraps = new();

        private static float GoUpSpringHeight(float progress)
        {
            float t = Mathf.Clamp01(progress);
            if (t < .18f) return Mathf.Lerp(.25f, .07f, t / .18f);
            if (t < .44f) return Mathf.Lerp(.07f, 1.15f, Mathf.Sin((t - .18f) / .26f * Mathf.PI * .5f));
            float settle = (t - .44f) / .56f;
            return Mathf.Max(.12f, .3f + .85f * Mathf.Cos(settle * Mathf.PI * 3f) * (1f - settle) * (1f - settle));
        }

        private IEnumerator PresentGoUpSpring(Vector2Int cell)
        {
            var root = new GameObject("Go Up trap - compressing spring");
            root.transform.SetParent(transform, false);
            _generated.Add(root);
            var line = root.AddComponent<LineRenderer>();
            var material = new Material(Shader.Find("Sprites/Default"));
            line.sharedMaterial = material;
            line.useWorldSpace = true; line.positionCount = 73;
            line.startWidth = line.endWidth = .045f;
            line.startColor = new Color(.42f,.34f,.20f); line.endColor = new Color(.95f,.94f,.75f);
            line.numCornerVertices = 2; line.numCapVertices = 2;
            var points = new Vector3[line.positionCount];
            var origin = new Vector3(cell.x, TowerGroundY(cell) + .045f, cell.y);
            try
            {
                for (float elapsed = 0f; elapsed < .65f; elapsed += Time.deltaTime * TowerAnimationSpeed * .7f)
                {
                    if (!line) yield break;
                    float height = GoUpSpringHeight(elapsed / .65f);
                    for (int i = 0; i < points.Length; i++)
                    {
                        float t = i / (float)(points.Length - 1), angle = t * Mathf.PI * 12f;
                        points[i] = origin + new Vector3(Mathf.Cos(angle)*.19f, t*height, Mathf.Sin(angle)*.19f);
                    }
                    line.SetPositions(points);
                    yield return null;
                }
            }
            finally
            {
                if (root) RemoveGenerated(root);
                if (material) Destroy(material);
            }
        }

        private static Color BlastTrapColor(float progress)
        {
            float t = Mathf.Clamp01(progress);
            var color = t < .16f ? Color.Lerp(new Color(1f,1f,.88f),new Color(1f,.65f,.06f),t/.16f)
                : t < .48f ? Color.Lerp(new Color(1f,.65f,.06f),new Color(.85f,.16f,.025f),(t-.16f)/.32f)
                : Color.Lerp(new Color(.40f,.28f,.20f),new Color(.18f,.18f,.18f),(t-.48f)/.52f);
            color.a = 1f - Mathf.InverseLerp(.45f,1f,t);
            return color;
        }

        private IEnumerator PresentBlastTrap(Vector2Int center)
        {
            var puffs = new List<(SpriteRenderer View, Vector3 Origin, float Angle)>();
            foreach (var offset in TrapCross)
            {
                var cell = center + offset;
                if (!InsideMap(cell) || _walls.Contains(cell)) continue;
                for (int i = 0; i < 6; i++)
                {
                    var puff = CreateSpellOrb(Color.white);
                    puff.name = "Blast trap - fire and smoke";
                    puff.sortingOrder = 8;
                    puffs.Add((puff, CellPosition(cell) - Vector3.up*.3f, i == 5 ? float.NaN : i*Mathf.PI*2f/5f));
                }
            }
            try
            {
                for (float elapsed = 0f; elapsed < .7f; elapsed += Time.deltaTime * TrapAnimationSpeed)
                {
                    if (puffs.Count == 0 || !puffs[0].View) yield break;
                    float t = elapsed/.7f;
                    var right = _gameCamera ? _gameCamera.transform.right : Vector3.right;
                    var up = _gameCamera ? _gameCamera.transform.up : Vector3.up;
                    foreach (var puff in puffs)
                    {
                        if (!puff.View) continue;
                        bool core = float.IsNaN(puff.Angle);
                        float angle = core ? 0f : puff.Angle;
                        float spread = core ? 0f : Mathf.Lerp(.025f,.48f,Mathf.Sqrt(t));
                        puff.View.transform.position = puff.Origin + right*(Mathf.Cos(angle)*spread) +
                            up*(Mathf.Sin(angle)*spread + t*.5f);
                        puff.View.transform.localScale = Vector3.one * Mathf.Lerp(.30f,.85f,t);
                        var color = BlastTrapColor(core ? t * .65f : t);
                        color.a = 1f - Mathf.InverseLerp(.45f, 1f, t);
                        puff.View.color = color;
                    }
                    yield return null;
                }
            }
            finally
            {
                foreach (var puff in puffs) if (puff.View) RemoveGenerated(puff.View.gameObject);
            }
        }
    }
}