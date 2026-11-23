using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const float DamageNumberFlightSeconds = .65f;
        private const float DamageNumberBounceSeconds = .28f;
        private const float DamageNumberHoldSeconds = 1f;
        private const float DamageNumberFadeSeconds = .15f;
        private const float DamageNumberSeconds = DamageNumberFlightSeconds + DamageNumberBounceSeconds +
            DamageNumberHoldSeconds + DamageNumberFadeSeconds;
        private sealed class DamageNumber
        {
            internal Vector3 Head, Feet;
            internal float Started, Side;
            internal GUIContent Label;
        }
        private readonly List<DamageNumber> _damageNumbers = new();
        private readonly List<SpriteRenderer> _damageNumberSprites = new();
        private GUIStyle _damageNumberStyle;

        private void ShowDamageNumber(GameObject unit, int damage)
        {
            if (!unit || damage <= 0 || !_gameCamera) return;
            var feet = unit.transform.position - Vector3.up * TownActorFootOffset;
            var projectedFeet = _gameCamera.WorldToScreenPoint(feet);
            if (projectedFeet.z <= 0f) return;
            var head = feet + _gameCamera.transform.up;
            float highest = float.NegativeInfinity;
            unit.GetComponentsInChildren(false, _damageNumberSprites);
            foreach (var sprite in _damageNumberSprites)
            {
                if (!sprite || !sprite.enabled || !sprite.sprite) continue;
                // Use the animated body outline, rather than a fixed height, so
                // short monsters and large familiars start at their own heads.
                var bounds = sprite.sprite.bounds;
                for (int corner = 0; corner < 4; corner++)
                {
                    var point = sprite.transform.TransformPoint(new Vector3(
                        (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (corner & 2) == 0 ? bounds.min.y : bounds.max.y, 0f));
                    var screen = _gameCamera.WorldToScreenPoint(point);
                    if (screen.z > 0f) highest = Mathf.Max(highest, screen.y);
                }
            }
            if (float.IsNegativeInfinity(highest)) highest = _gameCamera.WorldToScreenPoint(head).y;
            head = _gameCamera.ScreenToWorldPoint(new Vector3(projectedFeet.x, highest + 5f, projectedFeet.z));
            head = Vector3.Lerp(feet, head, .6f);
            int simultaneous = 0;
            foreach (var number in _damageNumbers)
                if (Time.unscaledTime - number.Started < DamageNumberSeconds &&
                    (number.Feet - feet).sqrMagnitude < .1f) simultaneous++;
            _damageNumbers.Add(new DamageNumber
            {
                Head = head, Feet = feet, Started = Time.unscaledTime,
                Side = simultaneous == 0 ? 0f : (simultaneous % 2 == 0 ? -1f : 1f) * .25f,
                Label = new GUIContent(damage.ToString())
            });
        }

        private static Vector3 DamageNumberPosition(Vector3 head, Vector3 feet, Vector3 up, Vector3 right, float side, float progress)
        {
            float elapsed = Mathf.Clamp01(progress) * DamageNumberSeconds;
            if (elapsed < DamageNumberFlightSeconds)
            {
                float t = elapsed / DamageNumberFlightSeconds;
                float arc = 4f * t * (1f - t);
                return Vector3.Lerp(head, feet, t * t) + up * (.3f * arc) + right * (side * arc);
            }
            if (elapsed < DamageNumberFlightSeconds + DamageNumberBounceSeconds)
            {
                float t = (elapsed - DamageNumberFlightSeconds) / DamageNumberBounceSeconds;
                return feet + up * (.2f * 4f * t * (1f - t));
            }
            return feet;
        }

        private void DrawDamageNumbers()
        {
            if (Event.current.type != EventType.Repaint) return;
            float now = Time.unscaledTime;
            for (int i = _damageNumbers.Count - 1; i >= 0; i--)
                if (now - _damageNumbers[i].Started >= DamageNumberSeconds) _damageNumbers.RemoveAt(i);
            if (_area != Area.Tower || !_gameCamera || _damageNumbers.Count == 0) return;
            // Do not inherit the window label's background or draw a black outline.
            _damageNumberStyle ??= new GUIStyle
            {
                font = Resources.Load<Font>(WindowDesignSystem.FontResource),
                fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                wordWrap = false, richText = false, clipping = TextClipping.Overflow,
                padding = new RectOffset(2, 2, 2, 2)
            };
            _damageNumberStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * .032f), 18, 38);
            var previousColor = GUI.color;
            GUI.color = Color.white;
            foreach (var number in _damageNumbers)
            {
                float progress = (now - number.Started) / DamageNumberSeconds;
                var point = DamageNumberPosition(number.Head, number.Feet,
                    _gameCamera.transform.up, _gameCamera.transform.right, number.Side, progress);
                var screen = _gameCamera.WorldToScreenPoint(point);
                if (screen.z <= 0f) continue;
                float elapsed = now - number.Started;
                float alpha = 1f - Mathf.InverseLerp(DamageNumberSeconds - DamageNumberFadeSeconds, DamageNumberSeconds, elapsed);
                var size = _damageNumberStyle.CalcSize(number.Label);
                var rect = new Rect(screen.x - size.x * .5f, Screen.height - screen.y - size.y * .5f, size.x, size.y);
                _damageNumberStyle.normal.textColor = new Color(1f, .12f, .12f, alpha);
                GUI.Label(rect, number.Label, _damageNumberStyle);
            }
            GUI.color = previousColor;
        }
    }
}
