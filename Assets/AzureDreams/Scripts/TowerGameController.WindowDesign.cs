using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private void DrawSpeakerWindow(Rect dialogue, string speaker)
        {
            if (string.IsNullOrWhiteSpace(speaker)) return; // Narration has no speaker.
            var measured = _bodyStyle.CalcSize(new GUIContent(speaker));
            var window = WindowDesignSystem.SpeakerWindow(dialogue, measured);
            DrawDialogueFrame(window, pageCorner: false);
            DrawInventoryFittedText(new Rect(window.x + WindowDesignSystem.Padding,
                window.y + 4f, window.width - WindowDesignSystem.Padding * 2f, window.height - 8f), speaker, _bodyStyle);
        }

        private static void DrawWindowPageCorner(Rect window)
        {
            if (Event.current.type != EventType.Repaint || window.width < 64f || window.height < 48f) return;
            if (!_pageCornerIcon) _pageCornerIcon = BuildPageCornerIcon();
            GUI.DrawTexture(WindowDesignSystem.PageCorner(window), _pageCornerIcon, ScaleMode.ScaleToFit, true);
        }
    }
}
