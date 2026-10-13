using KMA.Gameplay.UI;
using UnityEngine;

namespace KMA.UI.Kit
{
    /// Design tokens shared by every minigame's UI. Reads the shared ScriptableObject; owns no scene state.
    public static class MinigameUiTheme
    {
        public static Color Surface => UITheme.Shared.Surface;
        public static Color VolleyballControlSurface => UITheme.Shared.VolleyballControlSurface;
        public static Color TextPrimary => UITheme.Shared.TextPrimary;
        public static Color Accent => UITheme.Shared.Accent;
        public static Color Player => UITheme.Shared.Player;
        public static Color Energy => UITheme.Shared.Primary;
        public static Color TextOutline => UITheme.Shared.TextOutline;
        public static Color Success => UITheme.Shared.ResultSuccess;
        public static Color Track => WithAlpha(UITheme.Shared.Card, .22f);
        public static Color Scrim => UITheme.Shared.Scrim;

        public const float SurfaceOpaque = .92f;
        public const float SurfaceSoft = .82f;
        public const float SurfaceControlActive = .95f;
        public const float SurfaceDisabled = .30f;
        public const float DisabledGroupAlpha = .58f;
        public const float BorderHint = .75f;
        public const float BorderActive = 1f;
        public const float BorderDisabled = .15f;

        public const float Display = 160f;
        public const float Title = 54f;
        public const float Headline = 48f;
        public const float BodyLarge = 40f;
        public const float Body = 32f;
        public const float Caption = 28f;
        public const float MinimumFontSize = 28f;

        public static float RadiusPanel => UITheme.Shared.CornerRadius;
        public static float RadiusControl => UITheme.Shared.CornerRadius * 1.5f;
        public static float RadiusPause => UITheme.Shared.CornerRadius * (5f / 6f);
        public static float BorderWidth => UITheme.Shared.BorderWidth * .75f;

        public const float SpaceXs = 8f;
        public const float SpaceSm = 16f;
        public const float SpaceMd = 24f;
        public const float SpaceLg = 32f;

        public static Color ShadowColor => UITheme.Shared.ShadowColor;
        public static Vector2 ShadowOffset => UITheme.Shared.ShadowOffset;
        public static Color DisabledSurface => UITheme.Shared.DisabledSurface;
        public static Color DisabledText => UITheme.Shared.DisabledText;

        public static float PressScale => UITheme.Shared.Motion.pressScale;
        public static float PressLighten => UITheme.Shared.Motion.pressLighten;
        public static float PressRestoreSeconds => Mathf.Max(.001f, UITheme.Shared.Motion.pressRestore);

        public const float ButtonHeight = 88f;
        public const float RoundButton = 220f;
        public const float JoystickBase = 224f;
        public const float JoystickKnob = 112f;
        public const float BarHeight = 32f;
        public const float SliderKnob = 56f;

        public static float[] AllFontSizes() =>
            new[] { Display, Title, Headline, BodyLarge, Body, Caption };

        public static Color[] TextColors() =>
            new[] { TextPrimary, Accent, Player, Energy, Success };

        /// Every colour a minigame UI may draw with, compared without alpha (Track is white).
        public static Color[] Palette() =>
            new[] { Surface, VolleyballControlSurface, TextPrimary, Accent, Player, Energy, TextOutline, Success, Track };

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        /// Moves a colour toward white by amount (0..1) and keeps its alpha.
        public static Color Lighten(Color color, float amount)
        {
            Color lighter = Color.Lerp(color, Color.white, Mathf.Clamp01(amount));
            lighter.a = color.a;
            return lighter;
        }

        /// True when both colours have the same 8-bit RGB, whatever their alpha.
        public static bool RgbEquals(Color a, Color b)
        {
            Color32 first = a;
            Color32 second = b;
            return first.r == second.r && first.g == second.g && first.b == second.b;
        }

        /// WCAG 2.1 relative-luminance contrast ratio, from 1 to 21.
        public static float ContrastRatio(Color a, Color b)
        {
            float first = RelativeLuminance(a);
            float second = RelativeLuminance(b);
            float lighter = Mathf.Max(first, second);
            float darker = Mathf.Min(first, second);
            return (lighter + .05f) / (darker + .05f);
        }

        static float RelativeLuminance(Color color) =>
            .2126f * Channel(color.r) + .7152f * Channel(color.g) + .0722f * Channel(color.b);

        static float Channel(float value) =>
            value <= .03928f ? value / 12.92f : Mathf.Pow((value + .055f) / 1.055f, 2.4f);
    }
}
