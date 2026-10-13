using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using KMA.Gameplay;
using KMA.Gameplay.Core;
using KMA.Gameplay.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace KMA.Tests.Gameplay.Progression
{
    public sealed class FrogJumpRoutingTests
    {
        readonly List<SceneRouteTransition> routes = new List<SceneRouteTransition>();
        SceneRouter router;
        ResultPanel panel;

        [SetUp]
        public void SetUp()
        {
            // Same construction as JourneyRoutingTests: a router with an in-memory session,
            // route acceptance recorded into `routes`, persistence that always succeeds,
            // and a code-built ResultPanel in the active scene.
            routes.Clear();
            router = JourneyRoutingFixture.CreateRouter(routes);
            panel = JourneyRoutingFixture.CreateResultPanel();
        }

        [TearDown]
        public void TearDown() => JourneyRoutingFixture.Destroy(router, panel);

        [Test]
        public void FailedExamOffersOnlyTheFrogJumpAndRoutesToItsScene()
        {
            JourneyRoutingFixture.CompleteThrough(router.Session, "sprint_practice");
            JourneyRoutingFixture.FailActive(router, panel, "sprint_exam");

            Assert.That(panel.RetryAvailable, Is.False);
            Assert.That(panel.ContinueLabel, Is.EqualTo(VietText.Fix("BẬT CÓC")));
            panel.Continue();
            Assert.That(routes[routes.Count - 1].Route, Is.EqualTo(SessionRoute.FrogJump));
            Assert.That(routes[routes.Count - 1].SceneName, Is.EqualTo("MG_FrogJump"));
        }

        [Test]
        public void WinningTheFirstFrogJumpKeepsTheLifeAndRestartsTheChallenge()
        {
            JourneyRoutingFixture.CompleteThrough(router.Session, "sprint_practice");
            JourneyRoutingFixture.FailActive(router, panel, "sprint_exam");
            panel.Continue();
            router.CompleteFrogJumpForTests(true);

            Assert.That(router.Session.Lives, Is.EqualTo(5));
            Assert.That(router.Session.PendingFrogJump, Is.Null);
            Assert.That(panel.ContinueLabel, Is.EqualTo(VietText.Fix("THI LẠI")));
            panel.Continue();
            Assert.That(routes[routes.Count - 1].Route, Is.EqualTo(SessionRoute.Map));
            Assert.That(router.CompleteJourneyMapDialogue(router.Session), Is.True);
            Assert.That(routes[routes.Count - 1].Route, Is.EqualTo(SessionRoute.Subject));
            Assert.That(router.Session.Journey.ActiveAttempt.ChallengeId, Is.EqualTo("sprint_exam"));
        }

        [Test]
        public void DuplicateFrogCompletionAndDoubleContinueApplyOnce()
        {
            JourneyRoutingFixture.CompleteThrough(router.Session, "sprint_practice");
            JourneyRoutingFixture.FailActive(router, panel, "sprint_exam");
            panel.Continue();
            router.CompleteFrogJumpForTests(false);
            router.CompleteFrogJumpForTests(false);
            Assert.That(router.Session.Lives, Is.EqualTo(4));
            int before = routes.Count;
            panel.Continue();
            panel.Continue();
            Assert.That(routes.Count, Is.EqualTo(before + 1));
        }

        [Test]
        public void LastLifeLostLandsOnTheMap()
        {
            JourneyRoutingFixture.CompleteThrough(router.Session, "sprint_practice");
            JourneyRoutingFixture.FailActive(router, panel, "sprint_exam");
            panel.Continue();
            router.CompleteFrogJumpForTests(true);
            panel.Continue();
            router.Session.Journey.SetAttemptsRemaining(1);
            JourneyRoutingFixture.FailActive(router, panel, "sprint_exam");
            panel.Continue();
            router.CompleteFrogJumpForTests(true);

            Assert.That(router.Session.Lives, Is.Zero);
            Assert.That(panel.ContinueLabel, Is.EqualTo(VietText.Fix("VỀ BẢN ĐỒ")));
            panel.Continue();
            Assert.That(routes[routes.Count - 1].Route, Is.EqualTo(SessionRoute.Map));
            Assert.That(router.Session.ActiveSubject, Is.Null);
        }

        [Test]
        public void FailedPracticeFrogResultOffersPracticeAgain()
        {
            JourneyRoutingFixture.CompleteThrough(router.Session, "sprint_learn");
            JourneyRoutingFixture.FailActive(router, panel, "sprint_practice");
            panel.Continue();
            router.CompleteFrogJumpForTests(true);

            Assert.That(panel.ContinueLabel, Is.EqualTo(VietText.Fix("LUYỆN LẠI")));
            panel.Continue();
            Assert.That(router.CompleteJourneyMapDialogue(router.Session), Is.True);
            Assert.That(router.Session.Journey.ActiveAttempt.ChallengeId, Is.EqualTo("sprint_practice"));
        }

        [Test]
        public void StartingAChallengeWhileTheFrogJumpIsOwedRoutesToTheFrogJump()
        {
            JourneyRoutingFixture.CompleteThrough(router.Session, "sprint_practice");
            JourneyRoutingFixture.FailActive(router, panel, "sprint_exam");
            Assert.That(router.Route(SessionRoute.Map), Is.True);

            ChallengeDefinition exam = router.Session.Journey.Catalog.Get("sprint_exam");
            Assert.That(router.TryStartChallenge(exam.Id, ChallengeAttemptMode.Journey, exam.Difficulty), Is.True);
            Assert.That(routes[routes.Count - 1].Route, Is.EqualTo(SessionRoute.FrogJump));
            Assert.That(router.TryStartChallenge("sprint_learn", ChallengeAttemptMode.Review), Is.True);
            Assert.That(routes[routes.Count - 1].Route, Is.EqualTo(SessionRoute.FrogJump));
            Assert.That(router.Session.PendingFrogJump, Is.Not.Null);
            Assert.That(router.Session.Journey.ActiveAttempt, Is.Null);
        }

        [Test]
        public void FrogContinueLoadFailureShowsTheLoadErrorAfterTheChallengeSceneIsGone()
        {
            router.ConfigureJourneyPersistence((out string error) => { error = null; return true; });
            JourneyRoutingFixture.CompleteThrough(router.Session, "sprint_practice");
            JourneyRoutingFixture.FailActive(router, panel, "sprint_exam");
            panel.Continue();
            Assert.That(routes[routes.Count - 1].Route, Is.EqualTo(SessionRoute.FrogJump));

            // The challenge scene (and its result panel) unloads; the frog scene brings its own.
            Object.DestroyImmediate(panel.gameObject);
            panel = JourneyRoutingFixture.CreateResultPanel();
            router.CompleteFrogJumpForTests(true);

            router.ConfigureSceneLoaderForTests(_ => throw new InvalidOperationException("load refused"));
            LogAssert.Expect(LogType.Error, new Regex("Could not load scene"));
            panel.Continue();

            string error = panel.transform.Find("Error").GetComponent<TMP_Text>().text;
            Assert.That(error, Does.Contain("Could not load scene"));
            Assert.That(error, Does.Not.Contain("Missing"));
            Assert.That(panel.ContinueLabel, Is.EqualTo(VietText.Fix("THI LẠI")));
            Assert.That(router.Session.Journey.ActiveAttempt, Is.Null, "The rejected retry is rolled back.");
        }

        [Test]
        public void LearnFailureNeverOffersTheFrogJump()
        {
            JourneyRoutingFixture.FailActive(router, panel, "sprint_learn");

            Assert.That(router.Session.PendingFrogJump, Is.Null);
            Assert.That(panel.ContinueLabel, Is.EqualTo(VietText.Fix("HỌC LẠI")));
            panel.Continue();
            Assert.That(routes[routes.Count - 1].Route, Is.EqualTo(SessionRoute.Subject));
            Assert.That(router.Session.Journey.ActiveAttempt.ChallengeId, Is.EqualTo("sprint_learn"));
        }

        [TestCase(true, 5)]
        [TestCase(false, 4)]
        public void FrogResultBackReturnsToTheMapWithoutRetrying(bool reachedFinish, int lives)
        {
            JourneyRoutingFixture.CompleteThrough(router.Session, "sprint_practice");
            JourneyRoutingFixture.FailActive(router, panel, "sprint_exam");
            panel.Continue();
            router.CompleteFrogJumpForTests(reachedFinish);

            Assert.That(router.Session.Lives, Is.EqualTo(lives));
            Assert.That(panel.BackAvailable, Is.True);
            panel.Back();
            panel.Continue();
            Assert.That(routes[routes.Count - 1].Route, Is.EqualTo(SessionRoute.Map));
            Assert.That(routes.FindAll(route => route.Route == SessionRoute.Map), Has.Count.EqualTo(1));
            Assert.That(router.HasPendingJourneyMapContinuation, Is.False);
            Assert.That(router.CompleteJourneyMapDialogue(router.Session), Is.False);
            Assert.That(router.Session.Journey.ActiveAttempt, Is.Null);
            Assert.That(router.Session.PendingFrogJump, Is.Null);
            Assert.That(router.Session.Journey.CheckpointChallengeId, Is.EqualTo("sprint_exam"));
            Assert.That(router.Session.Lives, Is.EqualTo(lives));
        }

        [Test]
        public void FrogResultBackSavesThePendingResultBeforeLeaving()
        {
            bool allowSave = true;
            router.ConfigureJourneyPersistence((out string error) =>
            {
                error = allowSave ? null : "Save unavailable";
                return allowSave;
            });
            JourneyRoutingFixture.CompleteThrough(router.Session, "sprint_practice");
            JourneyRoutingFixture.FailActive(router, panel, "sprint_exam");
            panel.Continue();
            allowSave = false;
            router.CompleteFrogJumpForTests(true);
            int before = routes.Count;

            panel.Back();
            Assert.That(routes.Count, Is.EqualTo(before), "An unsaved frog result must not leave its scene.");
            Assert.That(panel.transform.Find("Error").GetComponent<TMP_Text>().text, Does.Contain("Save unavailable"));
            Assert.That(panel.BackAvailable, Is.True);

            allowSave = true;
            panel.Back();
            Assert.That(routes[routes.Count - 1].Route, Is.EqualTo(SessionRoute.Map));
            Assert.That(router.HasPendingJourneyMapContinuation, Is.False);
        }

        [Test]
        public void ResumeWithAPendingFrogJumpRoutesToItsScene()
        {
            JourneyRoutingFixture.CompleteThrough(router.Session, "sprint_practice");
            JourneyRoutingFixture.FailActive(router, panel, "sprint_exam");

            Assert.That(router.ResumeCampaign(), Is.True);
            Assert.That(routes[routes.Count - 1].Route, Is.EqualTo(SessionRoute.FrogJump));
            Assert.That(routes[routes.Count - 1].SceneName, Is.EqualTo("MG_FrogJump"));
        }
    }
}
