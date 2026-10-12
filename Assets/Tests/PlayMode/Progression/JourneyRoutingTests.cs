using KMA.Gameplay;
using KMA.Gameplay.Core;
using KMA.Gameplay.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace KMA.Tests.Gameplay.Progression
{
    public sealed class JourneyRoutingTests
    {
        SceneRouter router;
        ResultPanel panel;

        [SetUp]
        public void SetUp() => router = JourneyRoutingFixture.CreateRouter();

        [TearDown]
        public void TearDown() => JourneyRoutingFixture.Destroy(router, panel);

        [Test]
        public void FutureChallengeIsRejectedWithoutChangingSessionOrLoadingScene()
        {
            int routes = 0;
            router.TransitionStarted += _ => routes++;

            Assert.That(router.TryStartChallenge("volleyball_learn"), Is.False);

            Assert.That(router.Session.Journey.ActiveAttempt, Is.Null);
            Assert.That(router.Session.ActiveSubject, Is.Null);
            Assert.That(routes, Is.Zero);
        }

        [Test]
        public void RestartPreservesReviewOfAPreviousSubject()
        {
            for (int i = 0; i < 3; i++) JourneyGameplayDriver.CompleteActiveChallenge(router.Session);
            Assert.That(router.TryStartChallenge("sprint_learn", ChallengeAttemptMode.Review), Is.True);
            string previousId = router.Session.Journey.ActiveAttempt.AttemptId;
            Assert.That(router.RestartActiveSubject(), Is.True);
            ChallengeAttemptContext restarted = router.Session.Journey.ActiveAttempt;
            Assert.That(restarted.ChallengeId, Is.EqualTo("sprint_learn"));
            Assert.That(restarted.Mode, Is.EqualTo(ChallengeAttemptMode.Review));
            Assert.That(restarted.AttemptId, Is.Not.EqualTo(previousId));
            Assert.That(router.Session.Journey.CheckpointChallengeId, Is.EqualTo("volleyball_learn"));
            Assert.That(router.Session.Lives, Is.EqualTo(5));
        }

        [TestCase(1, ChallengeAttemptMode.Review)]
        [TestCase(10, ChallengeAttemptMode.FreePlay)]
        public void RestartPreservesChallengeAndModeAwayFromCheckpoint(int completed, ChallengeAttemptMode mode)
        {
            for (int i = 0; i < completed; i++) JourneyGameplayDriver.CompleteActiveChallenge(router.Session);
            string checkpoint = router.Session.Journey.CheckpointChallengeId;
            Assert.That(router.TryStartChallenge("sprint_learn", mode), Is.True);
            Assert.That(router.RestartActiveSubject(), Is.True);
            Assert.That(router.Session.Journey.ActiveAttempt.ChallengeId, Is.EqualTo("sprint_learn"));
            Assert.That(router.Session.Journey.ActiveAttempt.Mode, Is.EqualTo(mode));
            Assert.That(router.Session.Journey.CheckpointChallengeId, Is.EqualTo(checkpoint));
            Assert.That(router.Session.Lives, Is.EqualTo(5));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailedRestartKeepsOriginalAttemptInMemoryAndSave(bool rejectRoute)
        {
            SaveData persisted = null;
            bool allowSave = true;
            router.ConfigureJourneyPersistence((out string error) =>
            {
                error = allowSave ? null : "Save unavailable";
                if (allowSave) persisted = router.Session.ToSaveData();
                return allowSave;
            });
            Assert.That(router.TryStartChallenge("sprint_learn"), Is.True);
            string original = router.Session.Journey.ActiveAttempt.AttemptId;
            if (rejectRoute)
                router.ConfigureRouteAcceptanceForTests((_, _) => false);
            else allowSave = false;
            Assert.That(router.RestartActiveSubject(), Is.False);
            Assert.That(router.Session.Journey.ActiveAttempt.AttemptId, Is.EqualTo(original));
            var reload = new GameSession();
            reload.RestoreSnapshot(persisted);
            Assert.That(reload.Journey.ActiveAttempt.AttemptId, Is.EqualTo(original));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailedStartFromResultCanBeRetriedAfterPersistenceRecovers(bool frogJump)
        {
            string id = frogJump ? "sprint_exam" : "sprint_learn";
            if (frogJump) JourneyRoutingFixture.CompleteThrough(router.Session, "sprint_practice");
            var routes = new System.Collections.Generic.List<SceneRouteTransition>();
            router.TransitionStarted += routes.Add;
            bool allowSave = true;
            router.ConfigureJourneyPersistence((out string error) =>
            {
                error = allowSave ? null : "Save unavailable";
                return allowSave;
            });
            panel = JourneyRoutingFixture.CreateResultPanel();
            if (!frogJump)
            {
                // Start while saves work; the commit of its failure is what cannot be saved.
                Assert.That(router.TryStartChallenge(id), Is.True);
                allowSave = false;
            }
            JourneyRoutingFixture.FailActive(router, panel, id);

            if (frogJump)
            {
                // The frog jump itself needs no save; the retry it leads to does.
                panel.Continue();
                Assert.That(routes[routes.Count - 1].Route, Is.EqualTo(SessionRoute.FrogJump));
                router.CompleteFrogJumpForTests(true);
                Assert.That(router.Session.Lives, Is.EqualTo(5));
                allowSave = false;
            }
            panel.Continue();
            Assert.That(panel.HasContinued, Is.False, "A failed start must leave the result usable.");
            Assert.That(panel.ContinueInteractable, Is.True);
            // A failed commit save keeps the attempt uncommitted; a failed retry start keeps none.
            Assert.That(router.Session.Journey.ActiveAttempt?.ChallengeId,
                frogJump ? Is.Null : Is.EqualTo("sprint_learn"));
            Assert.That(router.Session.Lives, Is.EqualTo(5));
            string shown = panel.transform.Find(frogJump ? "Error" : "Detail").GetComponent<TMP_Text>().text;
            Assert.That(shown, Is.Not.Null, "The fixture panel must receive the routing error.");
            Assert.That(shown, Does.Contain("Save unavailable"));

            allowSave = true;
            panel.Continue();
            if (frogJump)
            {
                Assert.That(routes[routes.Count - 1].Route, Is.EqualTo(SessionRoute.Subject));
                Assert.That(router.Session.Journey.ActiveAttempt.ChallengeId, Is.EqualTo("sprint_exam"));
            }
            else
            {
                Assert.That(routes[routes.Count - 1].Route, Is.EqualTo(SessionRoute.Map));
                Assert.That(router.Session.Journey.ActiveAttempt, Is.Null);
                Assert.That(router.Session.PendingFrogJump, Is.Null);
            }
        }

        [Test]
        public void ChallengeStartPersistsActiveAttemptBeforeRouting()
        {
            bool persisted = false;
            router.ConfigureJourneyPersistence((out string error) =>
            {
                error = null;
                persisted = router.Session.Journey.ActiveAttempt != null;
                return persisted;
            });
            bool persistedAtRoute = false;
            router.TransitionStarted += _ => persistedAtRoute = persisted;

            Assert.That(router.TryStartChallenge("sprint_learn"), Is.True);

            Assert.That(persisted, Is.True);
            Assert.That(persistedAtRoute, Is.True);
            Assert.That(router.Session.Journey.ActiveAttempt.ChallengeId, Is.EqualTo("sprint_learn"));
        }

        [Test]
        public void PassedLessonContinuesTheCheckpointOnlyOnce()
        {
            panel = JourneyRoutingFixture.CreateResultPanel();
            JourneyRoutingFixture.PassActive(router, panel, "sprint_learn");
            panel.Continue();
            Assert.That(router.PendingJourneyChallengeId, Is.EqualTo("sprint_practice"));
            Assert.That(router.ContinuePendingJourney(out _), Is.True);
            string attempt = router.Session.Journey.ActiveAttempt.AttemptId;
            Assert.That(router.Session.Journey.ActiveAttempt.ChallengeId, Is.EqualTo("sprint_practice"));
            Assert.That(router.ContinuePendingJourney(out _), Is.False);
            Assert.That(router.Session.Journey.ActiveAttempt.AttemptId, Is.EqualTo(attempt));
        }

        [Test]
        public void RejectedContinueRouteKeepsTheResultUsableAndCancelsAutomaticStart()
        {
            panel = JourneyRoutingFixture.CreateResultPanel();
            JourneyRoutingFixture.PassActive(router, panel, "sprint_learn");
            router.ConfigureRouteAcceptanceForTests((_, _) => false);
            panel.Continue();
            Assert.That(router.PendingJourneyChallengeId, Is.Null);
            Assert.That(panel.HasContinued, Is.False);
            Assert.That(panel.ContinueInteractable, Is.True);
        }

        [Test]
        public void FailedAutomaticStartCanRetryTheSameCheckpointAfterSavingRecovers()
        {
            panel = JourneyRoutingFixture.CreateResultPanel();
            JourneyRoutingFixture.PassActive(router, panel, "sprint_learn");
            panel.Continue();
            bool allowSave = false;
            router.ConfigureJourneyPersistence((out string error) =>
            {
                error = allowSave ? null : "Save unavailable";
                return allowSave;
            });
            Assert.That(router.ContinuePendingJourney(out string saveError), Is.False);
            Assert.That(saveError, Is.EqualTo("Save unavailable"));
            Assert.That(router.PendingJourneyChallengeId, Is.EqualTo("sprint_practice"));
            Assert.That(router.Session.Journey.ActiveAttempt, Is.Null);
            allowSave = true;
            Assert.That(router.ContinuePendingJourney(out _), Is.True);
            Assert.That(router.PendingJourneyChallengeId, Is.Null);
            Assert.That(router.Session.Journey.ActiveAttempt.ChallengeId, Is.EqualTo("sprint_practice"));
        }

        [TestCase(ChallengeAttemptMode.Review)]
        [TestCase(ChallengeAttemptMode.FreePlay)]
        public void ReviewAndFreePlayResultsDoNotStartTheJourneyCheckpoint(ChallengeAttemptMode mode)
        {
            JourneyRoutingFixture.CompleteThrough(router.Session,
                mode == ChallengeAttemptMode.FreePlay ? "chess_final" : "sprint_learn");
            string checkpoint = router.Session.Journey.CheckpointChallengeId;
            panel = JourneyRoutingFixture.CreateResultPanel();
            JourneyRoutingFixture.PassActive(router, panel, "sprint_learn", mode);
            panel.Continue();
            Assert.That(router.PendingJourneyChallengeId, Is.Null);
            Assert.That(router.Session.Journey.CheckpointChallengeId, Is.EqualTo(checkpoint));
            Assert.That(router.Session.Journey.ActiveAttempt, Is.Null);
        }
    }
}
