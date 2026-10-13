using System;
using KMA.Gameplay;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Running
{
    public sealed class RivalMotionTests
    {
        const float Dt = 1f / 120f;

        static RivalPaceProfile Steady(float speed, RivalMotionTraits traits) =>
            new RivalPaceProfile("Steady", speed, speed, Array.Empty<PaceSegment>(), traits);

        static RivalMotionTraits Traits(float reaction = 0f, float tau = 1f, float style = 0f, float stop = 4f,
            float slot = 0f) => new RivalMotionTraits(reaction, tau, style, stop, slot);

        static float TimeToReach(RivalMotion rival, float speed, float goal = 1000f)
        {
            float clock = 0f;
            while (rival.Velocity < speed && clock < 20f) { rival.StepRace(clock, Dt, goal); clock += Dt; }
            return clock;
        }

        [Test]
        public void StandsStillUntilItsReactionTimeHasPassed()
        {
            var rival = new RivalMotion(Steady(9f, Traits(reaction: .2f)));
            float clock = 0f;
            for (; clock + Dt <= .2f; clock += Dt) rival.StepRace(clock, Dt, 150f);
            Assert.That(rival.Position, Is.Zero);
            Assert.That(rival.Velocity, Is.Zero);
            rival.StepRace(clock, Dt * 2f, 150f);
            Assert.That(rival.Velocity, Is.GreaterThan(0f));
        }

        [TestCase(.9f, 2.5f, 3.5f)]
        [TestCase(1.4f, 4f, 4.7f)]
        public void ReachesNinetyFivePercentOfPaceInARealisticTime(float tau, float earliest, float latest)
        {
            var rival = new RivalMotion(Steady(9f, Traits(tau: tau)));
            Assert.That(TimeToReach(rival, 9f * .95f), Is.InRange(earliest, latest));
        }

        [Test]
        public void NeverAcceleratesHarderThanAHumanCan()
        {
            var rival = new RivalMotion(Steady(10f, Traits(tau: .5f)));
            rival.StepRace(0f, Dt, 150f);
            Assert.That(rival.Acceleration, Is.LessThanOrEqualTo(RivalMotion.MaxAcceleration + 1e-4f));
        }

        [Test]
        public void CrossingTimeIsInterpolatedInsideTheTick()
        {
            var rival = new RivalMotion(Steady(10f, RivalMotionTraits.Instant));
            rival.StepRace(0f, 4.9f, 50f);
            Assert.That(rival.HasFinished, Is.False);
            rival.StepRace(4.9f, .2f, 50f);
            Assert.That(rival.HasFinished, Is.True);
            Assert.That(rival.FinishTime, Is.EqualTo(5f).Within(1e-4f));
        }

        [TestCase(3f)]
        [TestCase(5f)]
        public void RunsThroughTheLineAndPullsUpWithinItsStopDistance(float stop)
        {
            var rival = new RivalMotion(Steady(9f, Traits(tau: 0f, stop: stop)));
            float clock = 0f;
            for (int i = 0; i < 120 * 30; i++, clock += Dt) rival.StepRace(clock, Dt, 50f);
            Assert.That(rival.Velocity, Is.Zero);
            Assert.That(rival.Position - 50f, Is.InRange(stop - .2f, stop + .05f));
        }

        [Test]
        public void FinishStyleOnlyChangesPaceInTheLastFifthOfTheRace()
        {
            var closer = new RivalMotion(Steady(9f, Traits(tau: 0f, style: .08f)));
            var plain = new RivalMotion(Steady(9f, Traits(tau: 0f)));
            closer.StepRace(5f, Dt, 150f);
            plain.StepRace(5f, Dt, 150f);
            Assert.That(closer.Velocity, Is.EqualTo(plain.Velocity).Within(1e-4f), "no kick before 80 %");

            float clock = 5f;
            while (closer.Position < 125f) { closer.StepRace(clock, Dt, 150f); clock += Dt; }
            closer.StepRace(clock, Dt, 150f);
            Assert.That(closer.Velocity, Is.EqualTo(9f * 1.08f).Within(1e-3f));
        }

        [Test]
        public void TetheredRivalSettlesBesideTheRunningPlayer()
        {
            var rival = new RivalMotion(Steady(9f, Traits(slot: 3f)));
            float clock = 0f, player = 0f;
            for (int i = 0; i < 120 * 20; i++, clock += Dt)
            {
                player += 8f * Dt;
                rival.StepTethered(clock, Dt, player, 8f);
            }
            Assert.That(rival.Position - player, Is.EqualTo(3f).Within(.5f));
            Assert.That(rival.HasFinished, Is.False);
        }

        [Test]
        public void SameSeedRollsTheSameTraitsInsideTheirRanges()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                RivalMotionTraits a = RivalMotionTraits.Roll(new Random(seed));
                RivalMotionTraits b = RivalMotionTraits.Roll(new Random(seed));
                Assert.That(a.ReactionSeconds, Is.EqualTo(b.ReactionSeconds));
                Assert.That(a.ReactionSeconds, Is.InRange(RivalMotionTraits.MinReaction, RivalMotionTraits.MaxReaction));
                Assert.That(a.AccelerationTau, Is.InRange(RivalMotionTraits.MinTau, RivalMotionTraits.MaxTau));
                Assert.That(a.FinishStyle, Is.InRange(RivalMotionTraits.MinFinishStyle, RivalMotionTraits.MaxFinishStyle));
                Assert.That(a.StopDistance, Is.InRange(RivalMotionTraits.MinStopDistance, RivalMotionTraits.MaxStopDistance));
                Assert.That(Math.Abs(a.TetherSlot), Is.InRange(RivalMotionTraits.MinTetherSlot, RivalMotionTraits.MaxTetherSlot));
            }
        }

        [Test]
        public void HandBuiltProfilesMoveInstantly() =>
            Assert.That(new RivalPaceProfile("A", 6f, 6f).Traits.AccelerationTau, Is.Zero);
    }
}
