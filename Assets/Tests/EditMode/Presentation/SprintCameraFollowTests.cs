using KMA.Gameplay;
using NUnit.Framework;

namespace KMA.Tests.Presentation
{
    public sealed class SprintCameraFollowTests
    {
        [Test]
        public void HoldsStillUntilThePlayerReachesTheMiddle()
        {
            Assert.That(SprintCameraFollow.TargetX(0f, -9.6f), Is.EqualTo(0f));
            Assert.That(SprintCameraFollow.TargetX(0f, -.1f), Is.EqualTo(0f));
            Assert.That(SprintCameraFollow.TargetX(0f, 12f), Is.EqualTo(12f));
        }

        [Test]
        public void Step_ClosesOnThePlayerWithoutOvershooting()
        {
            float x = 0f, velocity = 0f, previous = 0f;
            for (int i = 0; i < 120; i++)
            {
                x = SprintCameraFollow.Step(x, ref velocity, 5f, 1f / 60f);
                Assert.That(x, Is.GreaterThanOrEqualTo(previous));
                Assert.That(x, Is.LessThanOrEqualTo(5f + 1e-4f));
                previous = x;
            }
            Assert.That(x, Is.EqualTo(5f).Within(.01f));
        }

        [Test]
        public void Step_CutsStraightToATeleportedPlayer()
        {
            float velocity = 3f;
            Assert.That(SprintCameraFollow.Step(0f, ref velocity, 60f, 1f / 60f), Is.EqualTo(60f));
            Assert.That(velocity, Is.Zero);
        }

        [Test]
        public void Step_HoldsWhenPaused()
        {
            float velocity = 2f;
            Assert.That(SprintCameraFollow.Step(1f, ref velocity, 3f, 0f), Is.EqualTo(1f));
        }
    }
}
