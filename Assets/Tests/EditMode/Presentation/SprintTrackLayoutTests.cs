using KMA.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace KMA.Tests.Presentation
{
    /// <summary>
    /// The painted track carries five lane lines; the four bands between them are the lanes a
    /// runner must stand in. These pin the mapping from those painted rows onto world space, so a
    /// runner can never drift into the sky the way the hand-tuned constants did.
    /// </summary>
    public sealed class SprintTrackLayoutTests
    {
        [Test]
        public void FourLanesSitBetweenFivePaintedLines() =>
            Assert.That(SprintTrackLayout.LaneLineRows.Length, Is.EqualTo(SprintTrackLayout.LaneCount + 1));

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void EachLaneCentreSitsHalfwayBetweenItsTwoPaintedLines(int lane)
        {
            float above = SprintTrackLayout.WorldYForRow(SprintTrackLayout.LaneLineRows[lane]);
            float below = SprintTrackLayout.WorldYForRow(SprintTrackLayout.LaneLineRows[lane + 1]);

            Assert.That(SprintTrackLayout.LaneCenterY(lane),
                Is.EqualTo((above + below) * .5f).Within(.0001f),
                "A runner must stand midway between the two lines that mark their lane.");
        }

        [Test]
        public void LanesRunTopToBottomWithoutCrossing()
        {
            for (int lane = 1; lane < SprintTrackLayout.LaneCount; lane++)
                Assert.That(SprintTrackLayout.LaneCenterY(lane),
                    Is.LessThan(SprintTrackLayout.LaneCenterY(lane - 1)),
                    "Lane 1 is the topmost lane; each following lane sits below it.");
        }

        [Test]
        public void EveryLaneIsOnThePaintedTrackRatherThanTheSky()
        {
            // Row 460 is the first opaque row of SprintTrack.png — the sky/track horizon.
            float horizonY = SprintTrackLayout.WorldYForRow(460f);
            for (int lane = 0; lane < SprintTrackLayout.LaneCount; lane++)
                Assert.That(SprintTrackLayout.LaneCenterY(lane), Is.LessThan(horizonY),
                    $"Lane {lane + 1} would put its runner above the track, in the sky.");
        }

        [Test]
        public void EveryRunnerClearsTheScoreboard()
        {
            // The scoreboard's underside sits at y = 3.13 and a runner sprite stands 1.28 tall.
            const float scoreboardBottomY = 3.13f;
            const float runnerHeight = 1.28f;
            for (int lane = 0; lane < SprintTrackLayout.LaneCount; lane++)
                Assert.That(SprintTrackLayout.LaneCenterY(lane) + runnerHeight,
                    Is.LessThan(scoreboardBottomY),
                    $"Lane {lane + 1}'s runner would poke into the scoreboard.");
        }

        [Test]
        public void BackdropCoversTheViewportFloorSoNoSkyShowsBeneathTheTrack()
        {
            float spriteLocalHeight = SprintTrackLayout.TextureHeight / SprintTrackLayout.TexturePixelsPerUnit;
            float renderedHeight = spriteLocalHeight * SprintTrackLayout.BackdropScaleY;

            Assert.That(renderedHeight, Is.EqualTo(SprintTrackLayout.BackdropHeight).Within(.0001f));
            Assert.That(SprintTrackLayout.BackdropCenterY - renderedHeight * .5f,
                Is.EqualTo(SprintTrackLayout.BackdropBottomY).Within(.0001f));
            Assert.That(SprintTrackLayout.BackdropBottomY, Is.LessThanOrEqualTo(-5.4f),
                "The backdrop must reach the viewport floor on its own — there is no apron.");
        }

        [Test]
        public void TheLowestLaneRunsUnderTheControlButtons()
        {
            // This is the arrangement the buttons are designed around: they sit on the lane and
            // draw a small visual inside a full-size tap area so they do not hide its runner.
            const float controlTopY = -2.16f;
            Assert.That(SprintTrackLayout.LaneCenterY(SprintTrackLayout.LaneCount - 1),
                Is.LessThan(controlTopY));
        }

        [Test]
        public void OneMetreOfRaceIsOneWorldUnitFromTheStart()
        {
            Assert.That(SprintTrackLayout.WorldX(0f), Is.EqualTo(SprintTrackLayout.TrackStartX).Within(.0001f));
            Assert.That(SprintTrackLayout.WorldX(10f) - SprintTrackLayout.WorldX(0f), Is.EqualTo(10f).Within(.0001f));
        }

        [Test]
        public void FinishLineSitsAtTheRaceDistanceInTheWorld() =>
            Assert.That(SprintTrackLayout.FinishWorldX(150f), Is.EqualTo(140.4f).Within(.0001f));

        [Test]
        public void FinishLineSpansThePaintedLanes() =>
            Assert.That(SprintTrackLayout.FinishTopY, Is.GreaterThan(SprintTrackLayout.FinishBottomY));
    }
}
