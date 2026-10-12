using KMA.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace KMA.Tests.Gameplay.Ball
{
    public sealed class FootballPosesTests
    {
        [Test]
        public void KickerRunsUpStrikesAndCelebratesOnlyGoals()
        {
            float kick = FootballRules.KickAnimationSeconds;
            Assert.That(FootballPoses.Kicker(FootballState.Start, 0f, null), Is.EqualTo(KickerPose.Ready));
            Assert.That(FootballPoses.Kicker(FootballState.Aiming, 0f, null), Is.EqualTo(KickerPose.Ready));
            Assert.That(FootballPoses.Kicker(FootballState.Charging, .4f, null), Is.EqualTo(KickerPose.Ready));
            Assert.That(FootballPoses.Kicker(FootballState.Kicking, kick * .25f, null), Is.EqualTo(KickerPose.RunUp));
            Assert.That(FootballPoses.Kicker(FootballState.Kicking, kick * .75f, null), Is.EqualTo(KickerPose.Strike));
            Assert.That(FootballPoses.Kicker(FootballState.Flying, .2f, null), Is.EqualTo(KickerPose.Strike));
            Assert.That(FootballPoses.Kicker(FootballState.Flying, .6f, FootballOutcome.Goal), Is.EqualTo(KickerPose.Celebrate));
            Assert.That(FootballPoses.Kicker(FootballState.ShotResult, .1f, FootballOutcome.Goal), Is.EqualTo(KickerPose.Celebrate));
            foreach (var miss in new[] { FootballOutcome.Saved, FootballOutcome.Wide, FootballOutcome.High,
                         FootballOutcome.Short, FootballOutcome.Post, FootballOutcome.Crossbar })
                Assert.That(FootballPoses.Kicker(FootballState.ShotResult, .1f, miss), Is.EqualTo(KickerPose.Ready), miss.ToString());
        }

        [Test]
        public void KeeperUsesDirectionalLandingThenCheersOnlyAfterASave()
        {
            const float diveAt = 1f, centreDeadZone = .08f, landingHold = .3f, cheer = .45f;
            KeeperPose At(FootballOutcome? outcome, float x, float elapsed) =>
                FootballPoses.Keeper(outcome, x, elapsed, diveAt, centreDeadZone, landingHold, cheer);

            Assert.That(At(null, -2f, 0f), Is.EqualTo(KeeperPose.Ready));
            Assert.That(At(FootballOutcome.Goal, -.5f, .1f), Is.EqualTo(KeeperPose.ReachLeft));
            Assert.That(At(FootballOutcome.Saved, .5f, .1f), Is.EqualTo(KeeperPose.ReachRight));
            Assert.That(At(FootballOutcome.Goal, -1f, .1f), Is.EqualTo(KeeperPose.DiveLeft));
            Assert.That(At(FootballOutcome.Saved, 1f, .1f), Is.EqualTo(KeeperPose.DiveRight));
            Assert.That(At(FootballOutcome.Saved, .04f, .1f), Is.EqualTo(KeeperPose.Ready), "A central save stays central.");
            Assert.That(At(FootballOutcome.Saved, -1.5f, .3f), Is.EqualTo(KeeperPose.Cheer));
            Assert.That(At(FootballOutcome.Saved, -1.5f, .74f), Is.EqualTo(KeeperPose.Cheer));
            Assert.That(At(FootballOutcome.Saved, -1.5f, .75f), Is.EqualTo(KeeperPose.Ready));
            Assert.That(At(FootballOutcome.Goal, -1.5f, .3f), Is.EqualTo(KeeperPose.Ready), "A goal never cheers.");
            Assert.That(At(FootballOutcome.Post, 1.5f, .4f), Is.EqualTo(KeeperPose.Ready), "Other outcomes never cheer.");
        }

        [Test]
        public void PoseSpritesMapEveryPose()
        {
            Sprite Make(string name) { var s = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero); s.name = name; return s; }
            var set = new FootballPoseSprites
            {
                kickerReady = Make("kr"), kickerRunUp = Make("ku"), kickerStrike = Make("ks"), kickerCelebrate = Make("kc"),
                keeperReady = Make("gr"), keeperReachLeft = Make("grl"), keeperReachRight = Make("grr"),
                keeperDiveLeft = Make("gdl"), keeperDiveRight = Make("gdr"), keeperCheer = Make("gc")
            };
            Assert.That(set.For(KickerPose.Ready), Is.SameAs(set.kickerReady));
            Assert.That(set.For(KickerPose.RunUp), Is.SameAs(set.kickerRunUp));
            Assert.That(set.For(KickerPose.Strike), Is.SameAs(set.kickerStrike));
            Assert.That(set.For(KickerPose.Celebrate), Is.SameAs(set.kickerCelebrate));
            Assert.That(set.For(KeeperPose.Ready), Is.SameAs(set.keeperReady));
            Assert.That(set.For(KeeperPose.ReachLeft), Is.SameAs(set.keeperReachLeft));
            Assert.That(set.For(KeeperPose.ReachRight), Is.SameAs(set.keeperReachRight));
            Assert.That(set.For(KeeperPose.DiveLeft), Is.SameAs(set.keeperDiveLeft));
            Assert.That(set.For(KeeperPose.DiveRight), Is.SameAs(set.keeperDiveRight));
            Assert.That(set.For(KeeperPose.Cheer), Is.SameAs(set.keeperCheer));
        }

        [Test]
        public void KeeperDiveRotatesAboutTheHip()
        {
            float drop = FootballPresentation.KeeperFeetDrop * FootballPresentation.PixelToWorld;
            Vector3 hip = FootballPresentation.ScreenToWorld(600f, FootballFlightSimulation.KeeperHipY);
            Vector3 upright = FootballPresentation.KeeperWorldPosition(0f, 0f);
            Assert.That(upright.x, Is.EqualTo(hip.x).Within(1e-5f));
            Assert.That(hip.y - upright.y, Is.EqualTo(drop).Within(1e-5f), "Feet stand below the hip.");
            foreach (float angle in new[] { -45f, -20f, 30f, 45f })
            {
                Vector3 feet = FootballPresentation.KeeperWorldPosition(0f, angle);
                Vector3 backToHip = feet + Quaternion.Euler(0f, 0f, -angle) * new Vector3(0f, drop, 0f);
                Assert.That(Vector3.Distance(backToHip, hip), Is.LessThan(1e-5f), "angle " + angle);
            }
            Assert.That(FootballPresentation.KeeperWorldPosition(1f, 0f).x, Is.GreaterThan(upright.x));
        }
    }
}
