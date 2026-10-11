using KMA.Gameplay.FrogJump;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.FrogJump
{
    public sealed class FrogJumpRulesTests
    {
        static readonly FrogJumpTuning Tuning = new FrogJumpTuning();
        const float Eps = 1e-3f;
        const float Step = 1f / 60f;

        [Test]
        public void FasterDefaultSweepsEachEdgeInPointSevenSeconds()
        {
            var rules = new FrogJumpRules(new FrogJumpTuning());
            foreach (float expected in new[] { .5f, 1f, .5f, 0f })
            {
                rules.Tick(.35f);
                Assert.That(rules.Needle01, Is.EqualTo(expected).Within(Eps));
            }
        }

        [Test]
        public void FasterNeedleKeepsFallRecovery()
        {
            var rules = new FrogJumpRules(new FrogJumpTuning());
            rules.Stop();
            rules.Tick(2.49f);
            Assert.That(rules.State, Is.EqualTo(FrogJumpState.Fallen));
            rules.Tick(.01f);
            Assert.That(rules.State, Is.EqualTo(FrogJumpState.Aiming));
            Assert.That(rules.Distance, Is.Zero);
        }

        [TestCase(.5f, 1.5f)]
        [TestCase(.3f, .25f)]
        [TestCase(.7f, .25f)]
        [TestCase(.4f, .875f)]
        public void JumpShrinksLinearlyFromCentreToTheSafeEdge(float needle, float metres)
        {
            Assert.That(FrogJumpRules.JumpMetres(needle, Tuning), Is.EqualTo(metres).Within(Eps));
        }

        [TestCase(.05f)]
        [TestCase(.25f)]
        [TestCase(.75f)]
        [TestCase(.97f)]
        [TestCase(0f)]
        [TestCase(1f)]
        public void OutsideTheSafeZoneIsAFall(float needle)
        {
            Assert.That(FrogJumpRules.IsFall(needle, Tuning), Is.True);
            Assert.That(FrogJumpRules.JumpMetres(needle, Tuning), Is.Zero);
        }

        [Test]
        public void OnlyTheMiddleFortyPercentOfTheBarIsSafe()
        {
            int safe = 0;
            const int samples = 1000;
            for (int i = 0; i < samples; i++)
                if (!FrogJumpRules.IsFall((i + .5f) / samples, Tuning)) safe++;
            Assert.That(safe / (float)samples, Is.EqualTo(.4f).Within(.01f));
        }

        [Test]
        public void NeedleSweepsEdgeToEdgeAndBack()
        {
            var rules = new FrogJumpRules(Tuning);
            Assert.That(rules.Needle01, Is.Zero);
            rules.Tick(Tuning.sweepSeconds / 2f);
            Assert.That(rules.Needle01, Is.EqualTo(.5f).Within(Eps));
            rules.Tick(Tuning.sweepSeconds / 2f);
            Assert.That(rules.Needle01, Is.EqualTo(1f).Within(Eps));
            rules.Tick(Tuning.sweepSeconds / 2f);
            Assert.That(rules.Needle01, Is.EqualTo(.5f).Within(Eps));
        }

        [Test]
        public void CentreStopJumpsOneAndAHalfMetresAfterTheJumpTime()
        {
            var rules = new FrogJumpRules(Tuning);
            rules.Tick(Tuning.sweepSeconds / 2f);
            Assert.That(rules.Stop(), Is.True);
            Assert.That(rules.State, Is.EqualTo(FrogJumpState.Jumping));
            Assert.That(rules.Stop(), Is.False);
            rules.Tick(.59f);
            Assert.That(rules.Distance, Is.Zero);
            rules.Tick(.02f);
            Assert.That(rules.Distance, Is.EqualTo(1.5f).Within(Eps));
            Assert.That(rules.State, Is.EqualTo(FrogJumpState.Aiming));
            Assert.That(rules.Needle01, Is.Zero);
            Assert.That(rules.Jumps, Is.EqualTo(1));
        }

        [Test]
        public void EdgeStopFallsAndBlocksInputWhileRecovering()
        {
            var rules = new FrogJumpRules(Tuning);
            Assert.That(rules.Stop(), Is.True);
            Assert.That(rules.State, Is.EqualTo(FrogJumpState.Fallen));
            Assert.That(rules.LastJumpMetres, Is.Zero);
            rules.Tick(2.4f);
            Assert.That(rules.Stop(), Is.False);
            rules.Tick(.2f);
            Assert.That(rules.State, Is.EqualTo(FrogJumpState.Aiming));
            Assert.That(rules.Distance, Is.Zero);
            Assert.That(rules.Falls, Is.EqualTo(1));
        }

        [Test]
        public void ReachingSixtyMetresWinsAndClampsDistance()
        {
            var rules = new FrogJumpRules(Tuning);
            for (int i = 0; i < 50 && !rules.IsOver; i++)
            {
                rules.Tick(Tuning.sweepSeconds / 2f);
                rules.Stop();
                rules.Tick(.6f);
            }
            Assert.That(rules.State, Is.EqualTo(FrogJumpState.Finished));
            Assert.That(rules.ReachedFinish, Is.True);
            Assert.That(rules.Distance, Is.EqualTo(60f));
            Assert.That(rules.Progress01, Is.EqualTo(1f));
        }

        [Test]
        public void RunningOutOfTimeLoses()
        {
            var rules = new FrogJumpRules(Tuning);
            rules.Tick(59.9f);
            Assert.That(rules.IsOver, Is.False);
            rules.Tick(.2f);
            Assert.That(rules.State, Is.EqualTo(FrogJumpState.TimedOut));
            Assert.That(rules.ReachedFinish, Is.False);
            Assert.That(rules.Elapsed, Is.EqualTo(60f).Within(Eps));
            Assert.That(rules.TimeRemaining, Is.Zero);
            Assert.That(rules.Stop(), Is.False);
        }

        [Test]
        public void LandingOnTheFinishExactlyAtTheLimitStillWins()
        {
            var tuning = new FrogJumpTuning { trackMetres = 1.5f };
            tuning.timeLimitSeconds = tuning.sweepSeconds / 2f + tuning.jumpSeconds;
            var rules = new FrogJumpRules(tuning);
            rules.Tick(tuning.sweepSeconds / 2f);
            rules.Stop();
            rules.Tick(.6f);
            Assert.That(rules.State, Is.EqualTo(FrogJumpState.Finished));
        }

        // Balance: a player who aims for the centre (stops within 5% of it) finishes in time;
        // one who taps as soon as the needle is anywhere near the green does not.
        [Test]
        public void ACentredPlayerFinishes()
        {
            Assert.That(Play(offsets: new[] { -.05f, .02f, .05f, -.03f, 0f }), Is.True);
        }

        [Test]
        public void ACarelessPlayerFailsEvenWithoutFalling()
        {
            // Every stop lands inside the green but near its edge.
            Assert.That(Play(offsets: new[] { -.18f, .17f, -.16f, .19f }), Is.False);
        }

        [Test]
        public void TappingAtRandomFails()
        {
            Assert.That(Play(offsets: new[] { -.45f, .1f, .35f, -.3f, .22f, -.15f, .48f, -.4f }), Is.False);
        }

        // Stops each sweep where the needle first reaches .5 + offset (cycling through offsets).
        static bool Play(float[] offsets)
        {
            var rules = new FrogJumpRules(Tuning);
            int next = 0;
            float previous = rules.Needle01;
            bool wasAiming = true;
            while (!rules.IsOver)
            {
                rules.Tick(Step);
                bool aiming = rules.State == FrogJumpState.Aiming;
                // A fresh sweep restarts at the left edge; don't read that reset as a crossing.
                if (!aiming || !wasAiming) { previous = rules.Needle01; wasAiming = aiming; continue; }
                float target = .5f + offsets[next % offsets.Length];
                float needle = rules.Needle01;
                bool crossed = (previous - target) * (needle - target) <= 0f && previous != needle;
                previous = needle;
                if (!crossed) continue;
                rules.Stop();
                next++;
            }
            return rules.ReachedFinish;
        }
    }
}
