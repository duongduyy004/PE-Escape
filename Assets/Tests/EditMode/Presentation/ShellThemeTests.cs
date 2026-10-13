using KMA.Gameplay.UI;
using KMA.UI.Kit;
using NUnit.Framework;
using UnityEngine;

namespace KMA.Tests.Presentation
{
    public sealed class ShellThemeTests
    {
        [Test]
        public void ShellTokensMatchTheKeyArt()
        {
            Assert.That((Color32)ShellTheme.Cream, Is.EqualTo(new Color32(0xFB, 0xF4, 0xE4, 0xFF)));
            Assert.That((Color32)ShellTheme.Ink, Is.EqualTo(new Color32(0x0B, 0x2A, 0x4A, 0xFF)));
            Assert.That((Color32)ShellTheme.Gold, Is.EqualTo(new Color32(0xF9, 0xC8, 0x2B, 0xFF)));
            Assert.That(ShellTheme.AccentRed, Is.EqualTo(UITheme.Shared.Primary));
            Assert.That(ShellTheme.CardBorder, Is.EqualTo(6f));
            Assert.That(ShellTheme.ControlBorder, Is.EqualTo(4f));
            Assert.That(ShellTheme.CardRadius, Is.EqualTo(20f));
            Assert.That(ShellTheme.ControlRadius, Is.EqualTo(12f));
            Assert.That(ShellTheme.CardShadow, Is.EqualTo(new Vector2(8f, -8f)));
            Assert.That(ShellTheme.ControlShadow, Is.EqualTo(new Vector2(5f, -5f)));
            Assert.That(ShellTheme.PressedShift, Is.EqualTo(ShellTheme.ControlShadow));
        }

        [Test]
        public void ShellTextColoursReadOnTheirFaces()
        {
            Assert.That(MinigameUiTheme.ContrastRatio(ShellTheme.Ink, ShellTheme.Cream), Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(MinigameUiTheme.ContrastRatio(ShellTheme.Ink, ShellTheme.Gold), Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(MinigameUiTheme.ContrastRatio(ShellTheme.Ink, ShellTheme.AccentRed), Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(MinigameUiTheme.ContrastRatio(ShellTheme.BodyText, ShellTheme.Cream), Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(MinigameUiTheme.ContrastRatio(ShellTheme.TitleRed, ShellTheme.White), Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(MinigameUiTheme.ContrastRatio(ShellTheme.DisabledText, ShellTheme.DisabledFace), Is.GreaterThanOrEqualTo(2.5f),
                "disabled text stays legible, just quieter");
        }

        [Test]
        public void PaletteHoldsEveryShellToken()
        {
            Color[] palette = ShellTheme.Palette();
            foreach (Color token in new[] { ShellTheme.Cream, ShellTheme.Ink, ShellTheme.Gold, ShellTheme.TitleRed,
                         ShellTheme.AccentRed, ShellTheme.BodyText, ShellTheme.DisabledFace, ShellTheme.DisabledText,
                         ShellTheme.White, UITheme.Shared.Scrim, UITheme.Shared.LessonJourney.sprint })
                Assert.That(System.Array.Exists(palette, c => MinigameUiTheme.RgbEquals(c, token)), Is.True, token.ToString());
        }
    }
}
