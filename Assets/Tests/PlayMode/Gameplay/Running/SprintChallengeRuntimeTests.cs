using System.Collections;
using System.Reflection;
using KMA.Gameplay;
using KMA.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KMA.Tests.Gameplay.Running
{
    public sealed class SprintChallengeRuntimeTests
    {
        GameObject controllerObject;
        GameObject routerObject;

        [SetUp]
        public void SetUp()
        {
            controllerObject = new GameObject("JourneySprintController");
            routerObject = new GameObject("JourneyInputRouter");
        }

        [TearDown]
        public void TearDown()
        {
            if (controllerObject != null) Object.DestroyImmediate(controllerObject);
            if (routerObject != null) Object.DestroyImmediate(routerObject);
        }

        [UnityTest]
        public IEnumerator LearnChallengeUsesTouchBridgeAndRaisesOneTypedCompletion()
        {
            var controller = controllerObject.AddComponent<SprintController>();
            controller.enabled = false; // Keep simulation deterministic before binding the test bridge.
            var router = routerObject.AddComponent<GameplayInputRouter>();
            router.ConfigureSprintForTest(null);
            controller.ConfigureInputRouterForTest(router);
            ChallengeDefinition definition = ChallengeCatalog.LoadDefault().Get("sprint_learn");
            var session = new GameSession();
            Assert.That(session.TryStartChallenge(definition.Id, ChallengeAttemptMode.Journey,
                definition.Difficulty, out ChallengeAttemptContext context), Is.True);
            controller.ConfigureChallenge(definition, context);
            int completions = 0;
            ChallengeAttemptResult received = null;
            controller.ChallengeCompleted += result => { completions++; received = result; };

            controller.SetTutorialGate(false);
            controller.Simulate(4f);
            for (int tap = 0; tap < 12; tap++)
            {
                router.FeedSprintTapForTest(tap % 2 == 0 ? KMA.Input.Side.Left : KMA.Input.Side.Right,
                    1d + tap * .2d);
                yield return null;
            }

            Assert.That(completions, Is.Zero, "Twelve valid taps must not resolve Learn early.");
            var rules = (SprintRules)typeof(SprintController).GetField("rules",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            typeof(SprintRules).GetField("distance", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(rules, definition.Distance + SprintRules.RaceDistance + 10f);
            controller.Simulate(.05f);
            Assert.That(completions, Is.Zero, "Race distance must not resolve the Learn lesson.");
            for (int frame = 0; frame < 500 && completions == 0; frame++) controller.Simulate(.1f);

            Assert.That(completions, Is.EqualTo(1));
            Assert.That(received.Pass, Is.True);
            Assert.That(received.Metrics.Elapsed, Is.EqualTo(definition.TimeLimit).Within(.001f));
            Assert.That(received.ExamResult, Is.Null);
            Assert.That(session.SubmitChallengeResult(received).Accepted, Is.True);
            Assert.That(session.Journey.CheckpointChallengeId, Is.EqualTo("sprint_practice"));
        }

        [Test]
        public void LearnClockAndTapProgressFreezeWhileTeacherDialogueIsVisible()
        {
            var controller = controllerObject.AddComponent<SprintController>();
            ChallengeDefinition definition = ChallengeCatalog.LoadDefault().Get("sprint_learn");
            controller.ConfigureChallenge(definition, new ChallengeAttemptContext(
                "dialogue-pause", definition.Id, ChallengeAttemptMode.Journey, ChallengeDifficulty.Normal));
            controller.enabled = false;
            controller.SetTutorialGate(false);
            controller.Simulate(4f);
            Assert.That(controller.Phase, Is.EqualTo(MinigamePhase.Play));
            float elapsed = controller.Snapshot.Elapsed;

            controller.SetLearnDialogueSuspended(true);
            controller.OnLeftTap();
            controller.Simulate(3f);
            Assert.That(controller.Snapshot.Elapsed, Is.EqualTo(elapsed).Within(.001f));
            Assert.That(controller.CorrectStreak, Is.Zero, "Taps do not progress during a spoken coaching line.");
            Assert.That(controller.LearnTimeRemaining,
                Is.EqualTo(definition.TimeLimit - elapsed).Within(.001f));

            controller.SetLearnDialogueSuspended(false);
            controller.Simulate(1f);
            Assert.That(controller.Snapshot.Elapsed, Is.EqualTo(elapsed + 1f).Within(.001f));
        }

        [UnityTest]
        public IEnumerator FinishingDuringDeadlineFramePassesAtTheExactLimit()
        {
            var controller = controllerObject.AddComponent<SprintController>();
            ChallengeDefinition definition = ChallengeCatalog.LoadDefault().Get("sprint_exam");
            controller.ConfigureChallenge(definition, new ChallengeAttemptContext(
                "deadline", definition.Id, ChallengeAttemptMode.Journey, ChallengeDifficulty.Normal));
            controller.SetTutorialGate(false);
            for (int frame = 0; frame < 301; frame++) controller.Simulate(.01f);
            var rules = (SprintRules)typeof(SprintController).GetField("rules",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            foreach (var value in new[] { ("elapsed", 39.99f), ("distance", 149.95f), ("speed", 100f) })
                typeof(SprintRules).GetField(value.Item1, BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(rules, value.Item2);
            ChallengeAttemptResult received = null;
            controller.ChallengeCompleted += result => received = result;
            controller.Simulate(.02f);
            Assert.That(received, Is.Not.Null);
            Assert.That(received.Pass, Is.True);
            Assert.That(received.Metrics.Elapsed, Is.EqualTo(40f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CrossingTheLineWaitsForRivalsThenResolvesWithTheFrozenRank()
        {
            var controller = controllerObject.AddComponent<SprintController>();
            ChallengeDefinition definition = ChallengeCatalog.LoadDefault().Get("sprint_practice");
            controller.ConfigureChallenge(definition, new ChallengeAttemptContext(
                "wait", definition.Id, ChallengeAttemptMode.Journey, ChallengeDifficulty.Normal));
            var challenge = new SprintChallengeRules(definition, SprintBalanceParameters.Default,
                new[] { new RivalPaceProfile("Slow", 5f, 5f) });
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(SprintController).GetField("challengeRules", flags).SetValue(controller, challenge);
            typeof(SprintController).GetField("rules", flags).SetValue(controller, challenge.Race);
            controller.SetTutorialGate(false);
            for (int frame = 0; frame < 301; frame++) controller.Simulate(.01f);
            typeof(SprintRules).GetField("distance", flags).SetValue(challenge.Race, 149.99f);
            typeof(SprintRules).GetField("speed", flags).SetValue(challenge.Race, 100f);
            ChallengeAttemptResult received = null;
            controller.ChallengeCompleted += result => received = result;

            controller.Simulate(.05f);
            Assert.That(controller.IsWaitingForRivals, Is.True);
            Assert.That(received, Is.Null);
            float frozen = controller.Snapshot.Distance;
            controller.OnLeftTap();
            controller.Simulate(.5f);
            Assert.That(controller.Snapshot.Distance, Is.EqualTo(frozen));
            Assert.That(controller.GetRivalDistance(0), Is.GreaterThan(0f));

            for (int i = 0; i < 100 && received == null; i++) controller.Simulate(.5f);
            Assert.That(received, Is.Not.Null);
            Assert.That(received.Pass, Is.True);
            Assert.That(received.ExamResult, Is.Not.Null);
            Assert.That(received.ExamResult.Score, Is.GreaterThan(0f));
            Assert.That(received.Metrics.Placement, Is.EqualTo(1));
            Assert.That(controller.IsWaitingForRivals, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PassingSprintExamRaisesScoreResultAndUnlocksVolleyball()
        {
            var session = new GameSession();
            Complete(session, "sprint_learn");
            Complete(session, "sprint_practice");
            var controller = controllerObject.AddComponent<SprintController>();
            var router = routerObject.AddComponent<GameplayInputRouter>();
            router.ConfigureSprintForTest(null);
            controller.ConfigureInputRouterForTest(router);
            ChallengeDefinition definition = session.Journey.Catalog.Get("sprint_exam");
            Assert.That(session.TryStartChallenge(definition.Id, ChallengeAttemptMode.Journey,
                definition.Difficulty, out ChallengeAttemptContext context), Is.True);
            controller.ConfigureChallenge(definition, context);
            ChallengeAttemptResult received = null;
            controller.ChallengeCompleted += result => received = result;

            controller.SetTutorialGate(false);
            for (int frame = 0; frame < 301; frame++) controller.Simulate(.01f);
            for (int frame = 0; frame < 22 * 240 && received == null; frame++)
            {
                if (frame % 40 == 0)
                    router.FeedSprintTapForTest((frame / 40) % 2 == 0
                        ? KMA.Input.Side.Left : KMA.Input.Side.Right, frame / 240d);
                controller.Simulate(1f / 240f);
            }

            Assert.That(received, Is.Not.Null);
            Assert.That(received.Pass, Is.True);
            Assert.That(received.ExamResult, Is.Not.Null);
            Assert.That(session.SubmitChallengeResult(received).Accepted, Is.True);
            Assert.That(session.Journey.CheckpointChallengeId, Is.EqualTo("volleyball_learn"));
            yield return null;
        }

        static void Complete(GameSession session, string id)
        {
            ChallengeDefinition definition = session.Journey.Catalog.Get(id);
            Assert.That(session.TryStartChallenge(id, ChallengeAttemptMode.Journey,
                definition.Difficulty, out ChallengeAttemptContext context), Is.True);
            session.SubmitChallengeResult(new ChallengeAttemptResult(context, true,
                new ChallengeMetrics(completedTargets: definition.TargetCount)));
        }
    }
}
