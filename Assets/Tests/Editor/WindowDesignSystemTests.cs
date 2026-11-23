using AzureDreamsReimagined;
using NUnit.Framework;
using UnityEngine;

namespace AzureDreams.Reimagined.EditorTests
{
    public sealed class WindowDesignSystemTests
    {
        [TestCase(320f, 240f)]
        [TestCase(640f, 480f)]
        [TestCase(1280f, 720f)]
        public void FooterAndSpeakerRemainOutsideText(float width, float height)
        {
            var panel = new Rect(24f, height * .6f, width - 48f, height * .3f);
            var content = WindowDesignSystem.Content(panel, footer: true);
            var page = WindowDesignSystem.PageCorner(panel);
            var name = WindowDesignSystem.SpeakerWindow(panel, new Vector2(230f, 20f));
            Assert.That(content.yMax, Is.LessThanOrEqualTo(page.y));
            Assert.That(panel.Contains(page.min) && panel.Contains(page.max), Is.True);
            Assert.That(name.yMax, Is.LessThan(panel.y));
            Assert.That(name.x, Is.EqualTo(panel.x));
            Assert.That(name.width, Is.LessThanOrEqualTo(panel.width));
        }

        [Test]
        public void AllTextStatesUseSharedFontAndWhiteText()
        {
            var font = Resources.Load<Font>(WindowDesignSystem.FontResource);
            Assert.That(font, Is.Not.Null);
            Assert.That(System.Array.Exists(font.fontNames, name => name.IndexOf("Dyslexic", System.StringComparison.OrdinalIgnoreCase) >= 0), Is.True);
            var style = new GUIStyle { fixedHeight = 12f };
            WindowDesignSystem.ApplyText(style, font);
            Assert.That(style.font, Is.SameAs(font));
            Assert.That(style.fixedHeight, Is.Zero);
            foreach (var state in new[] { style.normal, style.hover, style.active, style.focused,
                style.onNormal, style.onHover, style.onActive, style.onFocused })
                Assert.That(state.textColor, Is.EqualTo(Color.white));
            Assert.That(WindowDesignSystem.Background.a, Is.EqualTo(.74f));
        }
    }
}
