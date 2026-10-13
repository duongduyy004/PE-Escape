using UnityEngine;

namespace KMA.Gameplay
{
    /// <summary>
    /// Maps the lanes painted into <c>SprintTrack.png</c> onto world space.
    ///
    /// The artwork carries five painted lane lines; the four bands between them are the lanes a
    /// runner is meant to be standing in. Runner Y is derived from here rather than hand-tuned, so
    /// a runner cannot drift off the lane they are drawn to be standing in.
    ///
    /// The backdrop keeps its authored size — its bottom edge rests on the viewport floor, so the
    /// track fills the screen. The lowest lane therefore runs underneath the TRÁI/PHẢI buttons;
    /// those draw a deliberately small visual inside a full-size tap area so they sit on the lane
    /// without hiding the runner in it. See <see cref="SprintUiLayout.ControlVisualRect01"/>.
    /// </summary>
    public static class SprintTrackLayout
    {
        /// Row of the centre of each painted lane line, top-down, in SprintTrack.png's own pixels.
        /// Measured from the cream core of each line; identical at x = 10%, 50% and 90%.
        public static readonly float[] LaneLineRows = { 471.5f, 553.5f, 639f, 722f, 798f };

        public const int TextureHeight = 875;
        public const float TexturePixelsPerUnit = 100f;
        public const int LaneCount = 4;

        /// The backdrop as authored in MG_Sprint: its bottom edge sits on the viewport floor at
        /// ortho size 5.4, and it stands this tall in world units.
        public const float BackdropBottomY = -5.4f;
        public const float BackdropHeight = 15.4477f;

        /// Runners start here and stop on the finish line, in world units. The line sits where a 16:10
        /// view (the narrowest supported) still shows it; wider screens just show more track beyond it.
        public const float TrackStartX = -9.6f;

        /// The race runs along a real straight: one metre is one world unit from the start.
        public const float UnitsPerMeter = 1f;

        public static float WorldX(float meters) => TrackStartX + meters * UnitsPerMeter;

        /// The finish line stands where a runner who has covered the whole race distance stands.
        public static float FinishWorldX(float raceDistance) => WorldX(raceDistance);
        public const float FinishX = 6.5f;
        public const float TrackLength = FinishX - TrackStartX;
        public const float FinishWidth = .5f;

        /// The checkerboard spans the painted lanes, from the top lane line to the bottom one.
        public static float FinishTopY => WorldYForRow(LaneLineRows[0]);
        public static float FinishBottomY => WorldYForRow(LaneLineRows[LaneLineRows.Length - 1]);

        /// The line slides in from <paramref name="entryX"/> (just off the right edge) and rests on FinishX.
        public static float FinishLineX(float reveal01, float entryX) =>
            Mathf.Lerp(entryX, FinishX, Mathf.Clamp01(reveal01));

        public static float UnitsPerRow => BackdropHeight / TextureHeight;

        public static float WorldYForRow(float row) =>
            BackdropBottomY + (TextureHeight - row) * UnitsPerRow;

        /// Row midway between the two painted lines that bound this lane.
        public static float LaneCenterRow(int lane)
        {
            int clamped = Mathf.Clamp(lane, 0, LaneCount - 1);
            return (LaneLineRows[clamped] + LaneLineRows[clamped + 1]) * .5f;
        }

        /// Where a runner in this lane plants their feet. Lane 0 is the topmost lane.
        public static float LaneCenterY(int lane) => WorldYForRow(LaneCenterRow(lane));

        /// Lane as authored on the runners, which numbers them 1-4 from the top.
        public static float LaneCenterYForAuthoredLane(int authoredLane) => LaneCenterY(authoredLane - 1);

        public static float BackdropScaleY =>
            BackdropHeight / (TextureHeight / TexturePixelsPerUnit);

        public static float BackdropCenterY => BackdropBottomY + BackdropHeight * .5f;
    }
}
