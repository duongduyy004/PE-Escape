using KMA.Gameplay.FrogJump;
using NUnit.Framework;
using UnityEngine;

namespace KMA.Tests.Gameplay.FrogJump
{
    public sealed class FrogJumpRulesTests
    {
        static readonly FrogJumpTuning Tuning = new FrogJumpTuning();
        const float Eps = 1e-3f;
        const float Step = 1f / 60f;

        [Test]
        public void DefaultTuningUsesTheThirtySecondNarrowSafeBalance()
        {
            var tuning = new FrogJumpTuning();
            Assert.That(tuning.trackMetres, Is.EqualTo(60f));
            Assert.That(tuning.timeLimitSeconds, Is.EqualTo(30f));
            Assert.That(tuning.safeZone, Is.EqualTo(.2f));
            Assert.That(tuning.minJumpMetres, Is.EqualTo(2.2f));
            Assert.That(tuning.maxJumpMetres, Is.EqualTo(3.2f));
            Assert.That(tuning.sweepSeconds, Is.EqualTo(.7f));
            Assert.That(tuning.jumpSeconds, Is.EqualTo(.6f));
            Assert.That(tuning.recoverSeconds, Is.EqualTo(2.5f));
        }

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

        [TestCase(.5f, 3.2f)]
        [TestCase(.4f, 2.2f)]
        [TestCase(.6f, 2.2f)]
        [TestCase(.45f, 2.7f)]
        public void JumpShrinksLinearlyFromCentreToTheSafeEdge(float needle, float metres)
        {
            Assert.That(FrogJumpRules.JumpMetres(needle, Tuning), Is.EqualTo(metres).Within(Eps));
        }

        [Test]
        public void SafeEdgeIsInclusiveAndOnlyItsSmallToleranceExtendsIt()
        {
            Assert.That(FrogJumpRules.IsFall(.4f, Tuning), Is.False);
            Assert.That(FrogJumpRules.IsFall(.6f, Tuning), Is.False);
            Assert.That(FrogJumpRules.IsFall(.399998f, Tuning), Is.False, "The rule keeps its documented numeric tolerance.");
            Assert.That(FrogJumpRules.IsFall(.39998f, Tuning), Is.True);
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
        public void OnlyTheMiddleTwentyPercentOfTheBarIsSafe()
        {
            int safe = 0;
            const int samples = 1000;
            for (int i = 0; i < samples; i++)
                if (!FrogJumpRules.IsFall((i + .5f) / samples, Tuning)) safe++;
            Assert.That(safe / (float)samples, Is.EqualTo(.2f).Within(.01f));
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
        public void CentreStopJumpsThreePointTwoMetresAfterTheJumpTime()
        {
            var rules = new FrogJumpRules(Tuning);
            rules.Tick(Tuning.sweepSeconds / 2f);
            Assert.That(rules.Stop(), Is.True);
            Assert.That(rules.State, Is.EqualTo(FrogJumpState.Jumping));
            Assert.That(rules.Stop(), Is.False);
            rules.Tick(.59f);
            Assert.That(rules.Distance, Is.Zero);
            rules.Tick(.02f);
            Assert.That(rules.Distance, Is.EqualTo(3.2f).Within(Eps));
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
        public void ReachingSixtyMetresWithCentreJumpsWinsAndClampsDistance()
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
        public void RunningOutOfThirtySecondsLoses()
        {
            var rules = new FrogJumpRules(Tuning);
            rules.Tick(29.9f);
            Assert.That(rules.IsOver, Is.False);
            rules.Tick(.2f);
            Assert.That(rules.State, Is.EqualTo(FrogJumpState.TimedOut));
            Assert.That(rules.ReachedFinish, Is.False);
            Assert.That(rules.Elapsed, Is.EqualTo(30f).Within(Eps));
            Assert.That(rules.TimeRemaining, Is.Zero);
            Assert.That(rules.Stop(), Is.False);
        }

        [Test]
        public void TimingOutDuringAJumpDoesNotCreditItsPendingDistance()
        {
            var tuning = new FrogJumpTuning { trackMetres = 3.2f, timeLimitSeconds = .7f };
            var rules = new FrogJumpRules(tuning);
            rules.Tick(tuning.sweepSeconds / 2f);
            Assert.That(rules.Stop(), Is.True);
            Assert.That(rules.State, Is.EqualTo(FrogJumpState.Jumping));
            rules.Tick(.4f);
            Assert.That(rules.State, Is.EqualTo(FrogJumpState.TimedOut));
            Assert.That(rules.Distance, Is.Zero);
            Assert.That(rules.Jumps, Is.Zero);
            Assert.That(rules.Elapsed, Is.EqualTo(tuning.timeLimitSeconds).Within(Eps));
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

        [Test]
        public void IdealCentreTimingIsAnEstimateAndSampledFirstGreenStrategyWinsAtThirtyAndSixtyHz()
        {
            var tuning = new FrogJumpTuning();
            int idealJumps = Mathf.CeilToInt(tuning.trackMetres / tuning.maxJumpMetres);
            float idealTime = idealJumps * (tuning.sweepSeconds * .5f + tuning.jumpSeconds);
            Assert.That(idealJumps, Is.EqualTo(19));
            Assert.That(idealTime, Is.EqualTo(18.05f).Within(Eps));

            foreach (int hz in new[] { 30, 60 })
            {
                var result = PlayFirstGreenSampledWindow(tuning, hz);
                Assert.That(result.State, Is.EqualTo(FrogJumpState.Finished), hz + " Hz sampled first-green strategy");
                Assert.That(result.Falls, Is.Zero, hz + " Hz first-green strategy should never fall");
                Assert.That(result.Distance, Is.EqualTo(tuning.trackMetres).Within(Eps));
                Assert.That(result.Elapsed, Is.LessThan(tuning.timeLimitSeconds));
                Assert.That(result.Jumps, Is.GreaterThan(idealJumps), "Frame sampling lands away from the ideal centre at " + hz + " Hz.");
                TestContext.WriteLine("{0} Hz: elapsed={1:F3}s, jumps={2}, falls={3}, metres={4:F2}; ideal centre={5:F2}s/{6} jumps",
                    hz, result.Elapsed, result.Jumps, result.Falls, result.Distance, idealTime, idealJumps);
            }
        }

        static FrogJumpRules PlayFirstGreenSampledWindow(FrogJumpTuning tuning, int hz)
        {
            var rules = new FrogJumpRules(tuning);
            float dt = 1f / hz;
            float previousNeedle = rules.Needle01;
            bool wasAiming = true;
            int frames = 0;
            while (!rules.IsOver && frames++ < hz * 30)
            {
                rules.Tick(dt);
                bool aiming = rules.State == FrogJumpState.Aiming;
                if (!aiming || !wasAiming)
                {
                    previousNeedle = rules.Needle01;
                    wasAiming = aiming;
                    continue;
                }

                float needle = rules.Needle01;
                // Poll once per rendered frame and tap as soon as the forward sweep first enters green.
                bool enteredGreen = previousNeedle < .4f && needle >= .4f;
                previousNeedle = needle;
                if (enteredGreen) rules.Stop();
            }
            return rules;
        }

        // Balance: consistent centre aim wins; consistently aiming near the safe edge is too slow.
        [Test]
        public void ACentredPlayerFinishes()
        {
            Assert.That(Play(offsets: new[] { -.05f, .02f, .05f, -.03f, 0f }), Is.True);
        }

        [Test]
        public void APlayerWhoMissesTheNarrowZoneAndFallsRunsOutOfTime()
        {
            Assert.That(Play(offsets: new[] { -.09f, .11f, .08f, -.12f }), Is.False);
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
