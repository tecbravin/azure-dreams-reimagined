using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>Shared visual and layout contract for runtime menus, logs and dialogue.</summary>
    public static class WindowDesignSystem
    {
        public const string FontResource = "UI/OpenDyslexic-Regular";
        public const float Padding = 12f;
        public const float Gap = 8f;
        public const float Footer = 24f;
        public const float PageSize = 16f;
        public const float MinimumRowHeight = 30f;
        public const float BackgroundOpacity = 0.74f;
        public static Color Background => new(0.025f, 0.027f, 0.12f, BackgroundOpacity);

        public static Rect Content(Rect window, bool footer = false) => new(
            window.x + Padding, window.y + Padding,
            Mathf.Max(0f, window.width - Padding * 2f),
            Mathf.Max(0f, window.height - Padding * 2f - (footer ? Footer : 0f)));

        public static bool ShowContinuation(bool hasContinuation, bool textComplete, bool hasChoices = false) =>
            hasContinuation && textComplete && !hasChoices;

        public static Rect PageCorner(Rect window) => new(
            window.xMax - Gap - PageSize, window.yMax - Gap - PageSize, PageSize, PageSize);

        public static Rect SpeakerWindow(Rect dialogue, Vector2 measuredName) => new(
            dialogue.x, dialogue.y - Mathf.Max(30f, measuredName.y + Padding) - Gap,
            Mathf.Min(dialogue.width, Mathf.Max(112f, measuredName.x + Padding * 2f)),
            Mathf.Max(30f, measuredName.y + Padding));

        public static void ApplyText(GUIStyle style, Font font)
        {
            if (font) style.font = font;
            style.normal.textColor = style.hover.textColor = style.active.textColor = style.focused.textColor = Color.white;
            style.onNormal.textColor = style.onHover.textColor = style.onActive.textColor = style.onFocused.textColor = Color.white;
            style.fixedHeight = 0f;
            style.clipping = TextClipping.Clip;
        }
    }
}
