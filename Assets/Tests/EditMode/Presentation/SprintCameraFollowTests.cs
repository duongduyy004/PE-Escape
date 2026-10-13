using KMA.Gameplay;
using NUnit.Framework;

namespace KMA.Tests.Presentation
{
    public sealed class SprintCameraFollowTests
    {
        [Test]
        public void HoldsStillUntilThePlayerReachesTheMiddleThenFollows()
        {
            Assert.That(SprintCameraFollow.TargetX(0f, -9.6f), Is.EqualTo(0f));
            Assert.That(SprintCameraFollow.TargetX(0f, -.1f), Is.EqualTo(0f));
            Assert.That(SprintCameraFollow.TargetX(0f, 0f), Is.EqualTo(0f), "continuous at the hand-off");
            Assert.That(SprintCameraFollow.TargetX(0f, 12f), Is.EqualTo(12f));
        }

        [Test]
        public void Step_LandsExactlyOnThePlayerSoTheyStayCentred()
        {
            Assert.That(SprintCameraFollow.Step(0f, 5f, 1f / 60f), Is.EqualTo(5f));
            Assert.That(SprintCameraFollow.Step(3f, 3.3f, 1f / 60f), Is.EqualTo(3.3f));
            Assert.That(SprintCameraFollow.Step(0f, 60f, 1f / 60f), Is.EqualTo(60f));
        }

        [Test]
        public void Step_HoldsWhenPausedEvenForALargeJump()
        {
            Assert.That(SprintCameraFollow.Step(1f, 3f, 0f), Is.EqualTo(1f));
            Assert.That(SprintCameraFollow.Step(1f, 200f, 0f), Is.EqualTo(1f));
        }

        [TestCase(4f / 3f)]
        [TestCase(16f / 9f)]
        [TestCase(20f / 9f)]
        [TestCase(21f / 9f)]
        public void StartX_PutsTheStartLineJustInsideTheLeftEdge(float aspect)
        {
            float halfWidth = 5.4f * aspect;
            float startX = SprintCameraFollow.StartXFor(halfWidth);
            float leftEdge = startX - halfWidth;
            Assert.That(leftEdge, Is.EqualTo(SprintTrackLayout.TrackStartX - SprintCameraFollow.StartMargin).Within(1e-4f));
            Assert.That(leftEdge, Is.GreaterThan(-12.8f), "the authored track tile starts at -12.8");
        }

        [Test]
        public void StartMargin_StaysInsideTheAuthoredTrackTile()
        {
            Assert.That(SprintCameraFollow.StartMargin, Is.GreaterThan(0f).And.LessThan(3.2f));
        }
    }
}
