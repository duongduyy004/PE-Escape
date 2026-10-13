using KMA.Gameplay.UI;
using UnityEngine;

namespace KMA.UI.Kit
{
    /// Design tokens for every screen outside the minigames. Reads the shared ScriptableObject; owns no scene state.
    public static class ShellTheme
    {
        static UITheme.ShellStyle Style => UITheme.Shared.Shell;

        public static Color Cream => Style.cream;
        public static Color Ink => Style.ink;
        public static Color Gold => Style.gold;
        public static Color GoldLight => Style.goldLight;
        public static Color GoldDark => Style.goldDark;
        public static Color TitleRed => Style.titleRed;
        public static Color AccentRed => UITheme.Shared.Primary;
        public static Color BodyText => Style.bodyText;
        public static Color DisabledFace => Style.disabledFace;
        public static Color DisabledText => Style.disabledText;
        public static Color White => UITheme.Shared.Card;

        public static float CardBorder => Style.cardBorder;
        public static float ControlBorder => Style.controlBorder;
        public static float CardRadius => Style.cardRadius;
        public static float ControlRadius => Style.controlRadius;
        public static Vector2 CardShadow => Style.cardShadow;
        public static Vector2 ControlShadow => Style.controlShadow;
        public static Vector2 PressedShift => Style.controlShadow;
        public static Vector2 TitleDrop => Style.titleDrop;
        public static float TitleAngle => Style.titleAngle;
        public static float SlantAngle => Style.slantAngle;

        public const float ControlHeight = 84f;
        public const float IconButtonSize = 88f;
        public const float BadgeSize = 132f;
        public const float AccentSquare = 30f;

        /// Every colour a Shell screen may draw with, compared without alpha.
        public static Color[] Palette()
        {
            UITheme theme = UITheme.Shared;
            UITheme.LessonJourneyStyle journey = theme.LessonJourney;
            return new[]
            {
                Cream, Ink, Gold, GoldLight, GoldDark, TitleRed, AccentRed, BodyText, DisabledFace, DisabledText, White,
                theme.Scrim, theme.Accent, theme.Success, theme.MutedForeground, theme.MapHint, theme.MapLivesSurface,
                theme.MapLockedBorder, theme.MapReadyText, theme.MapActionText, theme.MapLockIcon, theme.MapButtonHighlight,
                theme.MapCompleteBorder, theme.MapLockedCard, theme.MapLockedIcon, theme.MapLockedText,
                journey.sprint, journey.volleyball, journey.football, journey.chess,
            };
        }
    }
}
