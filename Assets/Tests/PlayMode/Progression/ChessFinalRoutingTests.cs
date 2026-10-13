using System.Collections.Generic;
using System.Linq;
using KMA.Gameplay;
using KMA.Gameplay.Core;
using KMA.Gameplay.UI;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Progression
{
    public sealed class ChessFinalRoutingTests
    {
        readonly List<SceneRouteTransition> routes = new List<SceneRouteTransition>();
        SceneRouter router;
        ResultPanel panel;

        [SetUp]
        public void SetUp()
        {
            routes.Clear();
            ChallengeRetryIntent.Clear();
            router = JourneyRoutingFixture.CreateRouter(routes);
            panel = JourneyRoutingFixture.CreateResultPanel();
        }

        [TearDown]
        public void TearDown() => JourneyRoutingFixture.Destroy(router, panel);

        [Test]
        public void TheChessFinalLoadsItsScene()
        {
            JourneyRoutingFixture.CompleteThrough(router.Session, "soccer_exam");
            Assert.That(router.TryStartChallenge("chess_final"), Is.True);
            Assert.That(routes.Last().SceneName, Is.EqualTo("MG_ChessFinal"));
        }

        [Test]
        public void FailedFinalOffersReplayEvenAtZeroLivesAndNeverTheFrogJump()
        {
            JourneyRoutingFixture.CompleteThrough(router.Session, "soccer_exam");
            router.Session.Journey.SetAttemptsRemaining(0);
            JourneyRoutingFixture.FailActive(router, panel, "chess_final");

            Assert.That(router.Session.PendingFrogJump, Is.Null);
            Assert.That(panel.RetryAvailable, Is.False);
            Assert.That(panel.ContinueLabel, Is.EqualTo(VietText.Fix("THI LẠI")));
            panel.Continue();
            Assert.That(routes.Last().Route, Is.EqualTo(SessionRoute.Subject));
            Assert.That(router.Session.Journey.ActiveAttempt.ChallengeId, Is.EqualTo("chess_final"));
        }

        [Test]
        public void RetryingAFailedFinalArmsTheRetryIntentForTheNextScene()
        {
            JourneyRoutingFixture.CompleteThrough(router.Session, "soccer_exam");
            JourneyRoutingFixture.FailActive(router, panel, "chess_final");
            panel.Continue();
            Assert.That(routes.Last().SceneName, Is.EqualTo("MG_ChessFinal"));
            Assert.That(ChallengeRetryIntent.Consume("chess_final"), Is.True);
        }

        [Test]
        public void AFreshStartFromTheMapDoesNotArmTheRetryIntent()
        {
            JourneyRoutingFixture.CompleteThrough(router.Session, "soccer_exam");
            Assert.That(router.TryStartChallenge("chess_final"), Is.True);
            Assert.That(ChallengeRetryIntent.Consume("chess_final"), Is.False);
        }

        [Test]
        public void AFailedRetryStartDoesNotLeaveTheIntentArmed()
        {
            JourneyRoutingFixture.CompleteThrough(router.Session, "soccer_exam");
            JourneyRoutingFixture.FailActive(router, panel, "chess_final");
            router.ConfigureJourneyPersistence((out string error) => { error = "disk full"; return false; });
            panel.Continue();
            Assert.That(ChallengeRetryIntent.Consume("chess_final"), Is.False);
        }

        [Test]
        public void FirstWinRoutesToTheCelebrationOnce()
        {
            JourneyRoutingFixture.CompleteThrough(router.Session, "soccer_exam");
            JourneyRoutingFixture.PassActive(router, panel, "chess_final");
            panel.Continue();
            panel.Continue();
            Assert.That(routes.Count(r => r.Route == SessionRoute.Celebration), Is.EqualTo(1));
            Assert.That(routes.Last().SceneName, Is.EqualTo("Celebration"));
        }

        [Test]
        public void WinAfterTheCelebrationWasSeenGoesToTheMap()
        {
            JourneyRoutingFixture.CompleteThrough(router.Session, "chess_final");
            Assert.That(router.Session.Journey.MarkCelebrationSeen(), Is.True);
            JourneyRoutingFixture.PassActive(router, panel, "chess_final", ChallengeAttemptMode.FreePlay);
            panel.Continue();
            Assert.That(routes.Last().Route, Is.EqualTo(SessionRoute.Map));
        }

        [Test]
        public void CelebrationRouteIsRefusedBeforeTheCourseIsComplete()
        {
            JourneyRoutingFixture.CompleteThrough(router.Session, "soccer_exam");
            Assert.That(router.RouteToCelebration(), Is.False);
            JourneyRoutingFixture.CompleteThrough(router.Session, "chess_final");
            Assert.That(router.RouteToCelebration(), Is.True);
        }
    }
}
