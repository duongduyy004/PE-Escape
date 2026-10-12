using UnityEngine;

namespace KMA.Gameplay
{
    /// Pure responsive geometry for the Sprint HUD.
    /// Sizes are fractions of safe-area HEIGHT so every panel and button keeps the same
    /// physical size across landscape aspects; only the gaps between them stretch.
    public static class SprintUiLayout
    {
        public const float FinishRevealDistance = 70f;
        public const float FinishDistance = 100f;

        const float EdgeX = .02f;   // horizontal inset, fraction of width
        const float RailInsetX = .03f;
        const float RailTop = .985f;
        const float RailHeight = .03f;
        const float ClusterTop = .94f;
        const float ScoreboardWidth = .50f;
        const float ScoreboardHeight = .15f;
        const float ModeChipWidth = .36f;
        const float ModeChipHeight = .05f;
        const float ModeChipBottom = .895f;
        const float PauseSize = .089f;
        const float ControlWidth = .43f;
        const float ControlHeight = .26f;
        const float ControlEdgeInset = .0185f;
        const float ControlBottom = .0185f;
        const float CountdownWidth = .53f;
        const float CountdownHeight = .32f;
        const float CountdownCenterY = .62f;
        const float InstructionWidth = 1.07f;
        const float InstructionHeight = .10f;
        const float InstructionCenterY = .38f;

        public readonly struct NamedRect
        {
            public readonly string Name;
            public readonly Rect Rect;

            public NamedRect(string name, Rect rect)
            {
                Name = name;
                Rect = rect;
            }
        }

        public static float LaneCenter01(int laneIndex, int laneCount)
        {
            if (laneCount <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(laneCount));
            return (Mathf.Clamp(laneIndex, 0, laneCount - 1) + .5f) / laneCount;
        }

        public static bool FinishVisible(float distance) => distance >= FinishRevealDistance;

        /// How far the finish line has travelled in from the right edge: 0 the moment it is
        /// revealed, 1 when the runner reaches the line. See <see cref="SprintTrackLayout.FinishLineX"/>.
        public static float FinishReveal01(float distance) =>
            Mathf.Clamp01(Mathf.InverseLerp(FinishRevealDistance, FinishDistance, distance));

        // The one deliberate exception to height-only sizing: the rail spans the screen
        // because it maps 0-100 m onto the same left-right axis the runner moves along.
        public static Rect ProgressRailRect(Rect safe) => new Rect(
            safe.xMin + safe.width * RailInsetX,
            safe.yMin + safe.height * (RailTop - RailHeight),
            safe.width * (1f - RailInsetX * 2f),
            safe.height * RailHeight);

        public static Rect ScoreboardRect(Rect safe) => new Rect(
            safe.xMin + safe.width * EdgeX,
            safe.yMin + safe.height * (ClusterTop - ScoreboardHeight),
            safe.height * ScoreboardWidth,
            safe.height * ScoreboardHeight);

        public static Rect ModeChipRect(Rect safe) => new Rect(
            safe.center.x - safe.height * ModeChipWidth * .5f,
            safe.yMin + safe.height * ModeChipBottom,
            safe.height * ModeChipWidth,
            safe.height * ModeChipHeight);

        public static Rect PauseRect(Rect safe) => new Rect(
            safe.xMax - safe.width * EdgeX - safe.height * PauseSize,
            safe.yMin + safe.height * ClusterTop - safe.height * PauseSize,
            safe.height * PauseSize,
            safe.height * PauseSize);

        /// The visible button inside its tap area, in fractions of that tap area.
        /// The hit box remains full size while the 30% larger plate reaches the outer bottom corner
        /// inside the safe area.
        public const float ControlVisualWidthFraction = .676f;
        public const float ControlVisualHeightFraction = .65f;
        public const float ControlVisualBottomFraction = 0f;

        public static Rect ControlVisualRect01(bool left) => new Rect(
            left ? 0f : 1f - ControlVisualWidthFraction,
            ControlVisualBottomFraction,
            ControlVisualWidthFraction,
            ControlVisualHeightFraction);

        public static Rect ControlRect(Rect safe, bool left)
        {
            float width = safe.height * ControlWidth;
            float inset = safe.height * ControlEdgeInset;
            float x = left
                ? safe.xMin + inset
                : safe.xMax - inset - width;
            return new Rect(x, safe.yMin + safe.height * ControlBottom, width, safe.height * ControlHeight);
        }

        public static Rect CountdownRect(Rect safe) => Centered(safe, CountdownWidth, CountdownHeight, CountdownCenterY);

        public static Rect InstructionRect(Rect safe) =>
            Centered(safe, InstructionWidth, InstructionHeight, InstructionCenterY);

        public static NamedRect[] RaceRects(Rect safe) => new[]
        {
            new NamedRect("ProgressRail", ProgressRailRect(safe)),
            new NamedRect("Scoreboard", ScoreboardRect(safe)),
            new NamedRect("ModeChip", ModeChipRect(safe)),
            new NamedRect("Pause", PauseRect(safe)),
            new NamedRect("LeftControl", ControlRect(safe, true)),
            new NamedRect("RightControl", ControlRect(safe, false))
        };

        public static NamedRect[] StartStateRects(Rect safe)
        {
            NamedRect[] race = RaceRects(safe);
            var all = new NamedRect[race.Length + 2];
            System.Array.Copy(race, all, race.Length);
            all[race.Length] = new NamedRect("Countdown", CountdownRect(safe));
            all[race.Length + 1] = new NamedRect("Instruction", InstructionRect(safe));
            return all;
        }

        static Rect Centered(Rect safe, float width01H, float height01H, float centerY01H)
        {
            float width = safe.height * width01H;
            float height = safe.height * height01H;
            return new Rect(
                safe.center.x - width * .5f,
                safe.yMin + safe.height * centerY01H - height * .5f,
                width, height);
        }
    }
}
