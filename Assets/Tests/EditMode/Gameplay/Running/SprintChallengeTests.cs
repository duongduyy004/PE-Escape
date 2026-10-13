using System;
using KMA.Gameplay;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Running
{
    public sealed class SprintChallengeTests
    {
        ChallengeCatalog catalog;

        [SetUp]
        public void SetUp() => catalog = ChallengeCatalog.LoadDefault();

        [Test]
        public void BalanceDefaultsMatchTheAuthoredTuningContract()
        {
            SprintBalanceParameters value = SprintBalanceParameters.Default;
            Assert.That(new[] { value.CorrectImpulse, value.WrongImpulseFactor, value.SpeedCap,
                value.DragPerSecond, value.DistanceScale }, Is.EqualTo(new[] { 18f, .4f, 150f, 15f, .08f }));
            Assert.That(new[] { value.ComboBoostStartStreak, value.ComboBoostFullStreak },
                Is.EqualTo(new[] { 10, 30 }));
            Assert.That(new[] { value.ComboBoostMax, value.IdleGraceSeconds, value.IdleBrakeRampSeconds,
                value.IdleBrakePerSecond }, Is.EqualTo(new[] { .25f, .35f, .25f, 240f }));
            Assert.That(SprintBalanceConfig.LoadDefault().ToRuntime(), Is.EqualTo(value));
        }

        [Test]
        public void CatalogDefinesDistinctLearnPracticeAndExamGoals()
        {
            ChallengeDefinition learn = catalog.Get("sprint_learn");
            ChallengeDefinition practice = catalog.Get("sprint_practice");
            ChallengeDefinition exam = catalog.Get("sprint_exam");
            Assert.That(learn.Kind, Is.EqualTo(ChallengeKind.Learn));
            Assert.That(learn.TimeLimit, Is.EqualTo(50f));
            Assert.That(learn.TargetCount, Is.EqualTo(12));
            Assert.That(practice.Distance, Is.EqualTo(150f));
            Assert.That(practice.TimeLimit, Is.EqualTo(45f));
            Assert.That(exam.Distance, Is.EqualTo(150f));
            Assert.That(exam.TimeLimit, Is.EqualTo(40f));
        }

        [Test]
        public void LearnWaitsForTheTimedLessonAndKeepsBestStreakAfterAnError()
        {
            var challenge = new SprintChallengeRules(catalog.Get("sprint_learn"),
                SprintBalanceParameters.Default, Array.Empty<RivalPaceProfile>());
            for (int tap = 0; tap < 12; tap++) challenge.Tap(tap % 2 == 0 ? Side.Left : Side.Right);
            Assert.That(challenge.BestStreak, Is.EqualTo(12));
            Assert.That(challenge.IsComplete, Is.False, "A streak must not end Learn before its timer.");
            Assert.That(challenge.Race.Distance, Is.LessThan(150f));

            challenge.Tap(Side.Right);
            Assert.That(challenge.Race.CorrectStreak, Is.Zero);
            Assert.That(challenge.BestStreak, Is.EqualTo(12), "A later mistake must not erase the lesson's best streak.");

            challenge.Tick(49.99f);
            Assert.That(challenge.IsComplete, Is.False);
            challenge.Tick(1f);
            Assert.That(challenge.IsComplete, Is.True);
            ChallengeAttemptResult result = challenge.BuildResult(new ChallengeAttemptContext(
                "learn-1", "sprint_learn", ChallengeAttemptMode.Journey, ChallengeDifficulty.Normal));
            Assert.That(result.Pass, Is.True);
            Assert.That(result.Metrics.CompletedTargets, Is.EqualTo(12));
            Assert.That(result.Metrics.Elapsed, Is.EqualTo(50f).Within(.001f));
            Assert.That(result.ExamResult, Is.Null);
        }

        static SprintChallengeRules LearnWithRivals()
        {
            var authored = new[]
            {
                new RivalPaceProfile("A", 8.5f, 5.7f), new RivalPaceProfile("B", 5.8f, 5.7f),
                new RivalPaceProfile("C", 6f, 7.1f)
            };
            return new SprintChallengeRules(ChallengeCatalog.LoadDefault().Get("sprint_learn"),
                SprintBalanceParameters.Default,
                RivalPaceRandomizer.RollField(authored, 8.5f, 10f, new System.Random(3)));
        }

        [Test]
        public void LearnRivalsRaceToTheSameFinishLineAsPracticeAndRunThroughIt()
        {
            SprintChallengeRules challenge = LearnWithRivals();
            Assert.That(challenge.Race.GoalDistance, Is.EqualTo(150f));
            for (int i = 0; i < 60 * 50; i++) challenge.Tick(1f / 60f);
            challenge.Tick(1f); // closes any float drift left under the 50 s limit

            Assert.That(challenge.IsComplete, Is.True);
            for (int r = 0; r < challenge.Race.RivalCount; r++)
            {
                Assert.That(challenge.Race.HasRivalFinished(r), Is.True, $"rival {r} finished");
                Assert.That(challenge.Race.GetRivalPosition(r) - 150f,
                    Is.InRange(RivalMotionTraits.MinStopDistance - .05f, RivalMotionTraits.MaxStopDistance + .05f),
                    $"rival {r} pulled up past the line");
            }
        }

        [Test]
        public void LearnStillPassesOnTheStreakWhenEveryRivalBeatsThePlayer()
        {
            SprintChallengeRules challenge = LearnWithRivals();
            for (int tap = 0; tap < 12; tap++) challenge.Tap(tap % 2 == 0 ? Side.Left : Side.Right);
            for (int i = 0; i < 60 * 50; i++) challenge.Tick(1f / 60f);
            challenge.Tick(1f); // closes any float drift left under the 50 s limit

            ChallengeAttemptResult result = challenge.BuildResult(new ChallengeAttemptContext(
                "learn-last", "sprint_learn", ChallengeAttemptMode.Journey, ChallengeDifficulty.Normal));
            Assert.That(result.Metrics.Placement, Is.EqualTo(challenge.Race.RivalCount + 1), "last place");
            Assert.That(result.Pass, Is.True, "Learn is judged by the streak, not by placement.");
        }

        [Test]
        public void LearnWithoutTheMinimumStreakFailsAfterTheFullLessonAndClampsDelta()
        {
            var challenge = new SprintChallengeRules(catalog.Get("sprint_learn"),
                SprintBalanceParameters.Default, Array.Empty<RivalPaceProfile>());
            challenge.Tick(90f);
            Assert.That(challenge.IsComplete, Is.True);
            Assert.That(challenge.Race.Elapsed, Is.EqualTo(50f).Within(.001f));
            ChallengeAttemptResult result = challenge.BuildResult(new ChallengeAttemptContext(
                "idle-learn", "sprint_learn", ChallengeAttemptMode.Journey, ChallengeDifficulty.Normal));
            Assert.That(result.Pass, Is.False, "Waiting out the timer must not unlock Practice.");
            Assert.That(result.Metrics.CompletedTargets, Is.Zero);
        }

        [Test]
        public void SixHertzRaceFinishesExamAndPracticeHasLongerDeadline()
        {
            var practice = RunAtCadence("sprint_practice", 6f);
            var exam = RunAtCadence("sprint_exam", 6f);
            Assert.That(exam.Pass, Is.True);
            Assert.That(exam.Metrics.Distance, Is.GreaterThanOrEqualTo(150f));
            Assert.That(exam.Metrics.Elapsed, Is.LessThanOrEqualTo(40f));
            Assert.That(exam.ExamResult, Is.Not.Null);
            Assert.That(practice.Pass, Is.True);
            Assert.That(practice.ExamResult, Is.Not.Null);
            Assert.That(practice.ExamResult.Score, Is.GreaterThan(0f));
        }

        [Test]
        public void FourthPlaceDoesNotPreventAnOnTimeExamPass()
        {
            var rivals = new[]
            {
                new RivalPaceProfile("A", 100f, 100f), new RivalPaceProfile("B", 100f, 100f),
                new RivalPaceProfile("C", 100f, 100f)
            };
            var challenge = new SprintChallengeRules(catalog.Get("sprint_exam"),
                SprintBalanceParameters.Default, rivals);
            Simulate(challenge, 6f, 22f);
            Assert.That(challenge.Race.Rank, Is.EqualTo(4));
            ChallengeAttemptResult result = challenge.BuildResult(new ChallengeAttemptContext(
                "exam-rank", "sprint_exam", ChallengeAttemptMode.Journey, ChallengeDifficulty.Normal));
            Assert.That(result.Pass, Is.True);
        }

        static ChallengeAttemptResult RunAtCadence(string challengeId, float hertz)
        {
            var catalog = ChallengeCatalog.LoadDefault();
            var challenge = new SprintChallengeRules(catalog.Get(challengeId),
                SprintBalanceParameters.Default, Array.Empty<RivalPaceProfile>());
            Simulate(challenge, hertz, catalog.Get(challengeId).TimeLimit);
            return challenge.BuildResult(new ChallengeAttemptContext("attempt", challengeId,
                ChallengeAttemptMode.Journey, ChallengeDifficulty.Normal));
        }

        static ChallengeAttemptContext ContextFor(string id) =>
            new ChallengeAttemptContext("ctx-" + id, id, ChallengeAttemptMode.Journey, ChallengeDifficulty.Normal);

        [Test]
        public void PassingPracticeShowsTheEarnedScoreButFailingDoesNot()
        {
            ChallengeDefinition practice = catalog.Get("sprint_practice");
            var challenge = new SprintChallengeRules(practice, SprintBalanceParameters.Default,
                Array.Empty<RivalPaceProfile>());
            Assert.That(challenge.BuildResult(ContextFor(practice.Id)).ExamResult, Is.Null);

            typeof(SprintRules).GetField("distance", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).SetValue(challenge.Race, practice.Distance);
            ChallengeAttemptResult won = challenge.BuildResult(ContextFor(practice.Id));
            Assert.That(won.Pass, Is.True);
            Assert.That(won.ExamResult, Is.Not.Null);
            Assert.That(won.ExamResult.Score, Is.GreaterThan(0f));
        }

        [Test]
        public void FinishedRacerFreezesWhileRivalsKeepRunning()
        {
            var rival = new RivalPaceProfile("Steady", 6f, 6f);
            var rules = SprintRules.ForTest(150f, 20f, 1, rivalProfiles: new[] { rival });
            rules.FinishRace();
            int rank = rules.Rank;
            rules.Tap(Side.Left);
            rules.Tick(1f);
            Assert.That(rules.Elapsed, Is.EqualTo(20f));
            Assert.That(rules.Distance, Is.EqualTo(150f));
            Assert.That(rules.Speed, Is.Zero);
            Assert.That(rules.GetRivalDistance(0), Is.GreaterThan(0f));
            Assert.That(rules.AllRivalsReached(150f), Is.False);
            for (int i = 0; i < 40; i++) rules.Tick(1f);
            Assert.That(rules.AllRivalsReached(150f), Is.True);
            Assert.That(rules.Rank, Is.EqualTo(rank));
        }

        static void Simulate(SprintChallengeRules challenge, float hertz, float maxSeconds)
        {
            const float dt = 1f / 240f;
            int cadenceTicks = (int)Math.Round(240f / hertz);
            int tick = 0;
            Side expected = Side.Left;
            for (float elapsed = 0f; elapsed < maxSeconds && !challenge.IsComplete; elapsed += dt, tick++)
            {
                if (tick % cadenceTicks == 0)
                {
                    challenge.Tap(expected);
                    expected = expected == Side.Left ? Side.Right : Side.Left;
                }
                challenge.Tick(dt);
            }
        }
    }
}
