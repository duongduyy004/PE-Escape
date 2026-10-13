using KMA.Gameplay;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Running
{
    public sealed class SprintRulesTests
    {
        [Test]
        public void AuthoredSequence_StartsLeftAndInvalidInputDoesNotAdvanceIt()
        {
            var rules = SprintRules.Default();

            Assert.That(rules.ExpectedSide, Is.EqualTo(Side.Left));
            rules.Tap(Side.Right);
            Assert.That(rules.ExpectedSide, Is.EqualTo(Side.Left));
            Assert.That(rules.ValidTapRatio, Is.EqualTo(0f));
            rules.Tick(SprintRules.FallSeconds + .05f);

            rules.Tap(Side.Left);
            Assert.That(rules.ExpectedSide, Is.EqualTo(Side.Right));
            rules.Tap(Side.Left);
            Assert.That(rules.ExpectedSide, Is.EqualTo(Side.Right));
            rules.Tap(Side.Right);
            Assert.That(rules.ExpectedSide, Is.EqualTo(Side.Left));
            Assert.That(rules.ValidTapRatio, Is.EqualTo(.5f).Within(.001f));
        }

        [Test]
        public void SameSideTap_CountsAsAMistake()
        {
            var rules = SprintRules.Default();

            rules.Tap(Side.Left);
            rules.Tap(Side.Left);

            Assert.That(rules.IsFalling, Is.True);
            Assert.That(rules.Speed, Is.Zero);
        }

        [Test]
        public void LongCombo_RaisesTopSpeedWithinTheBoostLimit()
        {
            SprintBalanceParameters tuning = SprintBalanceParameters.Default;
            var plain = new SprintRules(60f, null, null, tuning);
            var combo = new SprintRules(60f, null, null, tuning);
            for (int tap = 0; tap < 60; tap++)
            {
                plain.Tap(tap % 3 == 0 ? Side.Right : Side.Left);
                combo.Tap(tap % 2 == 0 ? Side.Left : Side.Right);
                plain.Tick(.01f);
                combo.Tick(.01f);
            }

            Assert.That(combo.IsComboBoosting, Is.True);
            Assert.That(plain.IsComboBoosting, Is.False);
            Assert.That(combo.Speed, Is.GreaterThan(tuning.SpeedCap));
            Assert.That(combo.Speed, Is.LessThanOrEqualTo(tuning.SpeedCap * (1f + tuning.ComboBoostMax)));
        }
        [Test]

        public void WrongTap_StopsTheRunnerAndTripsThem()
        {
            var rules = new SprintRules(60f, null, null, SprintBalanceParameters.Default);
            rules.Tap(Side.Left);
            rules.Tap(Side.Right);
            Assert.That(rules.Speed, Is.GreaterThan(0f));

            rules.Tap(Side.Right);

            Assert.That(rules.Speed, Is.Zero);
            Assert.That(rules.IsFalling, Is.True);
            Assert.That(rules.CorrectStreak, Is.Zero);
            rules.Tick(.3f);
            rules.Tap(Side.Left);
            Assert.That(rules.Speed, Is.Zero, "taps while on the ground do nothing");
            float distance = rules.Distance;
            rules.Tick(.3f);
            Assert.That(rules.Distance, Is.EqualTo(distance), "a fallen runner does not slide forward");
        }

        [Test]
        public void FallenRunnerRecoversAndRunsAgain()
        {
            var rules = new SprintRules(60f, null, null, SprintBalanceParameters.Default);
            rules.Tap(Side.Right);
            Assert.That(rules.IsFalling, Is.True);

            rules.Tick(SprintRules.FallSeconds + .05f);

            Assert.That(rules.IsFalling, Is.False);
            rules.Tap(Side.Left);
            Assert.That(rules.Speed, Is.GreaterThan(0f));
        }

        [Test]
        public void FallHasACooldownSoAFumblingRunnerDoesNotTripNonStop()
        {
            var rules = new SprintRules(60f, null, null, SprintBalanceParameters.Default);
            rules.Tap(Side.Right);
            rules.Tick(SprintRules.FallSeconds + .05f);

            rules.Tap(Side.Right);
            Assert.That(rules.IsFalling, Is.False, "still inside the post-fall cooldown");

            rules.Tick(SprintRules.FallCooldownSeconds + .1f);
            rules.Tap(Side.Right);
            Assert.That(rules.IsFalling, Is.True);
        }

        [Test]
        public void WrongTap_EndsTheComboBoost()
        {
            var rules = new SprintRules(60f, null, null, SprintBalanceParameters.Default);
            for (int tap = 0; tap < 20; tap++) rules.Tap(tap % 2 == 0 ? Side.Left : Side.Right);
            Assert.That(rules.IsComboBoosting, Is.True);

            rules.Tap(Side.Right);

            Assert.That(rules.IsComboBoosting, Is.False);
        }

        [Test]
        public void StoppingTaps_LeavesTheRunnerStandingStill()
        {
            var rules = new SprintRules(60f, null, null, SprintBalanceParameters.Default);
            for (int tap = 0; tap < 12; tap++)
            {
                rules.Tap(tap % 2 == 0 ? Side.Left : Side.Right);
                rules.Tick(1f / 6f);
            }
            float top = rules.Speed;

            rules.Tick(.1f);
            Assert.That(rules.Speed, Is.GreaterThan(top * .9f), "a short pause between taps keeps momentum");
            rules.Tick(.3f);
            Assert.That(rules.Speed, Is.Zero, "no idle slide once the grace window has passed");
            float distance = rules.Distance;
            rules.Tick(2f);
            Assert.That(rules.Distance, Is.EqualTo(distance));
        }

        [Test]
        public void IdleStop_DoesNotDependOnFrameRate()
        {
            var smooth = new SprintRules(60f, null, null, SprintBalanceParameters.Default);
            var hitch = new SprintRules(60f, null, null, SprintBalanceParameters.Default);
            smooth.Tap(Side.Left);
            hitch.Tap(Side.Left);

            for (int i = 0; i < 60; i++) smooth.Tick(1f / 60f);
            hitch.Tick(1f);

            Assert.That(hitch.Speed, Is.Zero);
            Assert.That(smooth.Speed, Is.Zero);
            Assert.That(hitch.Distance, Is.EqualTo(smooth.Distance).Within(.5f));
        }

        [Test]
        public void TopTwoAfterTimeout_DoesNotPass()
        {
            var rules = SprintRules.ForTest(distance: 150f, elapsed: 22.1f, rank: 1);

            Assert.That(rules.BuildResult().Pass, Is.False);
        }

        [Test]
        public void EqualInputs_ProduceEqualSnapshots()
        {
            var a = SprintRules.Default();
            var b = SprintRules.Default();

            foreach (var side in new[] { Side.Left, Side.Right, Side.Left })
            {
                a.Tap(side);
                b.Tap(side);
                a.Tick(.1f);
                b.Tick(.1f);
            }

            Assert.That(a.Snapshot, Is.EqualTo(b.Snapshot));
        }

        [Test]
        public void FixedRivalProfiles_AreConsumedWithoutChangingPlayerSnapshot()
        {
            var profiles = new[]
            {
                new RivalPaceProfile("FastStart", 8f, 4f),
                new RivalPaceProfile("RunnerB", 5f, 6f)
            };
            var rules = new SprintRules(rivalProfiles: profiles);

            rules.Tick(.5f);

            Assert.That(rules.RivalProfiles, Is.EqualTo(profiles));
            Assert.That(rules.Snapshot.Distance, Is.EqualTo(0f));
        }

        [Test]
        public void RivalProfiles_DetermineDeterministicRankAndDistance()
        {
            var fast = new SprintRules(rivalProfiles: new[]
            {
                new RivalPaceProfile("Fast", 100f, 100f)
            });
            var slow = new SprintRules(rivalProfiles: new[]
            {
                new RivalPaceProfile("Slow", 0f, 0f)
            });
            var repeat = new SprintRules(rivalProfiles: new[]
            {
                new RivalPaceProfile("Fast", 100f, 100f)
            });

            fast.Tap(Side.Left);
            slow.Tap(Side.Left);
            repeat.Tap(Side.Left);
            fast.Tick(1f);
            slow.Tick(1f);
            repeat.Tick(1f);

            Assert.That(fast.RivalDistances, Is.EqualTo(repeat.RivalDistances));
            Assert.That(fast.Rank, Is.EqualTo(repeat.Rank));
            Assert.That(fast.Rank, Is.GreaterThan(slow.Rank));
            Assert.That(fast.RivalDistances[0], Is.GreaterThan(slow.RivalDistances[0]));
        }

        [Test]
        public void FinishingFirst_ScoresAboveFinishingLast_ForTheSameRun()
        {
            RivalPaceProfile[] Rivals(float speed) => new[]
            {
                new RivalPaceProfile("A", speed, speed),
                new RivalPaceProfile("B", speed, speed),
                new RivalPaceProfile("C", speed, speed)
            };
            var first = SprintRules.ForTest(distance: 150f, elapsed: 20f, rank: 1, rivalProfiles: Rivals(0f));
            var last = SprintRules.ForTest(distance: 150f, elapsed: 20f, rank: 4, rivalProfiles: Rivals(200f));
            first.Tick(1f);
            last.Tick(1f);

            Assert.That(first.Rank, Is.EqualTo(1));
            Assert.That(last.Rank, Is.EqualTo(4));
            Assert.That(first.BuildResult().Score, Is.EqualTo(7.3f).Within(.001f));
            Assert.That(last.BuildResult().Score, Is.EqualTo(6.3f).Within(.001f));
        }

        [Test]
        public void CompletionIsTheOnlyPrimaryObjective()
        {
            var rules = SprintRules.ForTest(distance: 99.9f, elapsed: 1f, rank: 1);

            Assert.That(rules.BuildResult().Pass, Is.False);
        }

        [Test]
        public void Rival_StopsAtTheFinishLineInsteadOfRunningOn()
        {
            var rules = new SprintRules(rivalProfiles: new[] { new RivalPaceProfile("Fast", 100f, 100f) },
                goalDistance: 50f);

            rules.Tick(.4f);
            Assert.That(rules.GetRivalDistance(0), Is.EqualTo(40f).Within(.001f));
            rules.Tick(.4f);
            Assert.That(rules.GetRivalDistance(0), Is.EqualTo(50f));
            rules.Tick(5f);
            Assert.That(rules.GetRivalDistance(0), Is.EqualTo(50f));
            Assert.That(rules.AllRivalsReached(50f), Is.True);
        }

        [Test]
        public void Rival_StaysAtTheLineWhileThePlayerRunsIn()
        {
            var rules = new SprintRules(rivalProfiles: new[] { new RivalPaceProfile("Fast", 100f, 100f) },
                goalDistance: 50f);
            rules.Tick(1f);
            rules.FinishRace();

            rules.Tick(3f);

            Assert.That(rules.GetRivalDistance(0), Is.EqualTo(50f));
        }
    }
}
