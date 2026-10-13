using UnityEngine;
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

        const float Dt = 1f / 120f;
        // Raw tap speed is metres per second divided by DistanceScale.
        static float Raw(float metersPerSecond) => metersPerSecond / SprintBalanceParameters.Default.DistanceScale;

        [Test]
        public void Rival_RunsThroughTheLineAndPullsUpWithinItsStopDistance()
        {
            var rules = new SprintRules(rivalProfiles: new[] { new RivalPaceProfile("Steady", 9f, 9f) },
                goalDistance: 50f);
            for (int i = 0; i < 120 * 10; i++) rules.Tick(Dt);

            Assert.That(rules.HasRivalFinished(0), Is.True);
            Assert.That(rules.GetRivalDistance(0), Is.EqualTo(50f), "race distance stops on the line");
            Assert.That(rules.GetRivalPosition(0) - 50f, Is.InRange(3.8f, 4.05f), "but the body runs on ~4 m");
            Assert.That(rules.GetRivalSpeed(0), Is.Zero);
            Assert.That(rules.AllRivalsReached(50f), Is.True);
        }

        [Test]
        public void Player_CrossingFinishesTheRaceAtTheInterpolatedTimeAndRunsOn()
        {
            var rules = SprintRules.ForTest(distance: 49f, elapsed: 10f, rank: 1, speed: Raw(10f), goalDistance: 50f);
            rules.Tick(.2f);

            Assert.That(rules.IsFinished, Is.True);
            Assert.That(rules.Distance, Is.EqualTo(50f));
            Assert.That(rules.Elapsed, Is.EqualTo(10.1f).Within(.005f));
            Assert.That(rules.BuildResult(50f, 60f).Pass, Is.True);

            for (int i = 0; i < 120 * 3; i++) rules.Tick(Dt);
            Assert.That(rules.Position - 50f, Is.InRange(3.8f, 4.05f));
            Assert.That(rules.MetersPerSecond, Is.Zero);
            Assert.That(rules.Elapsed, Is.EqualTo(10.1f).Within(.005f), "the clock stopped on the line");
        }

        [TestCase(60f, 210f, 1)]   // player crosses at ~0.40 s, rival at ~0.48 s
        [TestCase(60f, 400f, 2)]   // player crosses at ~0.40 s, rival at 0.25 s
        public void Rank_UsesInterpolatedCrossingTimes_WhenBothCrossInOneTick(float playerStart, float rivalSpeed,
            int expectedRank)
        {
            // The rival starts from 0 and covers 100 m at rivalSpeed; the player runs 100 m/s from playerStart.
            var rules = SprintRules.ForTest(distance: playerStart, elapsed: 0f, rank: 1,
                rivalProfiles: new[] { new RivalPaceProfile("R", rivalSpeed, rivalSpeed) },
                speed: Raw(100f), goalDistance: 100f);
            rules.Tick(.5f);

            Assert.That(rules.IsFinished, Is.True);
            Assert.That(rules.HasRivalFinished(0), Is.True);
            Assert.That(rules.Rank, Is.EqualTo(expectedRank));
        }

        [Test]
        public void TickRivals_MovesTheFieldWhileThePlayerIsHeld()
        {
            var rules = SprintRules.ForTest(distance: 10f, elapsed: 5f, rank: 1,
                rivalProfiles: new[] { new RivalPaceProfile("R", 9f, 9f) }, speed: Raw(8f));

            rules.TickRivals(1f);

            Assert.That(rules.GetRivalPosition(0), Is.EqualTo(9f).Within(.001f));
            Assert.That(rules.Distance, Is.EqualTo(10f), "the held player does not move");
            Assert.That(rules.Elapsed, Is.EqualTo(5f), "the held player's clock does not run");
        }

        [Test]
        public void Rank_ComparesCrossingsOnTheRaceClockAfterThePlayerWasHeld()
        {
            // The rival crosses 100 m at race time 10 s. The player was held for 9.7 s, so on their
            // own clock they cross after ~0.4 s, but on the race clock at ~10.1 s: behind the rival.
            var rules = SprintRules.ForTest(distance: 60f, elapsed: 0f, rank: 1,
                rivalProfiles: new[] { new RivalPaceProfile("R", 10f, 10f) }, speed: Raw(100f), goalDistance: 100f);

            rules.TickRivals(9.7f);
            rules.Tick(1f);

            Assert.That(rules.IsFinished, Is.True);
            Assert.That(rules.HasRivalFinished(0), Is.True);
            Assert.That(rules.Elapsed, Is.LessThan(1f), "the player's own time excludes the hold");
            Assert.That(rules.Rank, Is.EqualTo(2));
        }

        [Test]
        public void TickAfterRace_PullsUpAnUnfinishedPlayerWithoutChangingTheRace()
        {
            var rules = SprintRules.ForTest(distance: 20f, elapsed: 22f, rank: 1,
                rivalProfiles: new[] { new RivalPaceProfile("R", 9f, 9f) }, speed: Raw(8f));
            float before = rules.GetRivalPosition(0);
            int rank = rules.Rank;

            rules.TickAfterRace(1f / 60f);
            Assert.That(rules.MetersPerSecond, Is.GreaterThan(7f).And.LessThan(8f), "slowing, not stopped dead");
            Assert.That(rules.Position, Is.GreaterThan(20f));
            for (int i = 1; i < 60; i++) rules.TickAfterRace(1f / 60f);
            rules.TickAfterRace(0f);
            Assert.That(rules.GetRivalPosition(0), Is.EqualTo(before + 9f).Within(.001f));

            for (int i = 0; i < 60 * 3; i++) rules.TickAfterRace(1f / 60f);
            Assert.That(rules.MetersPerSecond, Is.Zero);
            Assert.That(rules.Position - 20f, Is.InRange(3.8f, SprintRules.DefaultRunOutDistance + .05f));
            Assert.That(rules.Distance, Is.EqualTo(20f), "the race distance does not change");
            Assert.That(rules.Elapsed, Is.EqualTo(22f), "the race clock does not change");
            Assert.That(rules.Rank, Is.EqualTo(rank));
            Assert.That(rules.IsFinished, Is.False);
        }
    }
}
