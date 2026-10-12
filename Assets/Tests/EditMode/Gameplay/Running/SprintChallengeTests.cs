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
            Assert.That(learn.TargetCount, Is.EqualTo(50));
            Assert.That(practice.Distance, Is.EqualTo(150f));
            Assert.That(practice.TimeLimit, Is.EqualTo(20f));
            Assert.That(exam.Distance, Is.EqualTo(150f));
            Assert.That(exam.TimeLimit, Is.EqualTo(15f));
        }

        [Test]
        public void LearnRequiresFiftyAlternatingInputsAndResetsOnError()
        {
            var challenge = new SprintChallengeRules(catalog.Get("sprint_learn"),
                SprintBalanceParameters.Default, Array.Empty<RivalPaceProfile>());
            for (int tap = 0; tap < 8; tap++) challenge.Tap(tap % 2 == 0 ? Side.Left : Side.Right);
            challenge.Tap(Side.Right);
            Assert.That(challenge.Race.CorrectStreak, Is.Zero);
            for (int tap = 0; tap < 49; tap++) challenge.Tap(tap % 2 == 0 ? Side.Left : Side.Right);
            Assert.That(challenge.IsComplete, Is.False);
            challenge.Tap(Side.Right);
            Assert.That(challenge.IsComplete, Is.True);
            ChallengeAttemptResult result = challenge.BuildResult(new ChallengeAttemptContext(
                "learn-1", "sprint_learn", ChallengeAttemptMode.Journey, ChallengeDifficulty.Normal));
            Assert.That(result.Pass, Is.True);
            Assert.That(result.Metrics.CompletedTargets, Is.EqualTo(50));
            Assert.That(result.ExamResult, Is.Null);
        }

        [Test]
        public void SixHertzRaceFinishesExamAndPracticeHasLongerDeadline()
        {
            var practice = RunAtCadence("sprint_practice", 6f);
            var exam = RunAtCadence("sprint_exam", 6f);
            Assert.That(exam.Pass, Is.True);
            Assert.That(exam.Metrics.Distance, Is.GreaterThanOrEqualTo(150f));
            Assert.That(exam.Metrics.Elapsed, Is.LessThanOrEqualTo(15f));
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
