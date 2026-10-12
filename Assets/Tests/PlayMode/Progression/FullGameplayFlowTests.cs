using System;
using System.Collections;
using System.Collections.Generic;
using KMA.Gameplay;
using KMA.Gameplay.Core;
using KMA.Gameplay.Shell;
using KMA.Gameplay.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KMA.Tests.Gameplay.Progression
{
    public sealed class FullGameplayFlowTests
    {
        readonly List<GameObject> gameObjects = new List<GameObject>();
        int originalTargetFrameRate;
        int originalVSyncCount;

        [SetUp]
        public void SetUp()
        {
            originalTargetFrameRate = Application.targetFrameRate;
            originalVSyncCount = QualitySettings.vSyncCount;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in gameObjects)
            {
                if (gameObject != null)
                    UnityEngine.Object.DestroyImmediate(gameObject);
            }
            gameObjects.Clear();
            DestroyAll<GameManager>();
            DestroyAll<SceneRouter>();
            Application.targetFrameRate = originalTargetFrameRate;
            QualitySettings.vSyncCount = originalVSyncCount;
        }

        [UnityTest]
        public IEnumerator FullFlow_UsesAttemptsLivesAndNormalizedResults()
        {
            var harness = GameplayFlowHarness.Create();
            foreach (string id in new[] { "sprint_learn", "sprint_practice" })
            {
                harness.StartChallenge(id);
                harness.CompleteChallenge(true, 0f);
            }
            harness.StartChallenge("sprint_exam");
            harness.CompleteChallenge(false, 0f);
            // Failure #1 costs nothing at commit; losing its frog jump costs the life.
            Assert.That(harness.Session.Lives, Is.EqualTo(5));
            Assert.That(harness.Session.Journey.CheckpointChallengeId, Is.EqualTo("sprint_exam"));
            harness.FrogJump(false);
            Assert.That(harness.Session.Lives, Is.EqualTo(4));
            harness.StartChallenge("sprint_exam");
            harness.CompleteChallenge(false, 0f);
            // Failure #2 costs the life at commit; its frog jump cannot change lives.
            Assert.That(harness.Session.Lives, Is.EqualTo(3));
            harness.FrogJump(true);
            Assert.That(harness.Session.Lives, Is.EqualTo(3));
            harness.StartChallenge("sprint_exam");
            harness.CompleteChallenge(true, 6f);
            foreach (string id in new[] { "volleyball_learn", "volleyball_practice", "volleyball_exam",
                         "soccer_learn", "soccer_practice", "soccer_exam", "chess_final" })
            {
                harness.StartChallenge(id);
                harness.CompleteChallenge(true, 6f);
            }

            Assert.That(harness.Session.Records, Has.Count.EqualTo(4));
            Assert.That(harness.Session.Journey.CourseComplete, Is.True);
            Assert.That(harness.Session.Journey.CheckpointChallengeId, Is.Null);
            Assert.That(harness.Session.Lives, Is.EqualTo(3));
            foreach (var record in harness.Session.Records.Values)
            {
                Assert.That(record.BestResult.Pass, Is.True);
                Assert.That(record.BestResult.Score, Is.InRange(0f, 10f));
                Assert.That(record.BestResult.Rank, Is.EqualTo(ScoreUtil.ToRank(record.BestResult.Score)));
            }

            Assert.That(harness.Transitions.Count, Is.GreaterThanOrEqualTo(20));
            yield return null;
        }

        [Test]
        public void FailedVisit_DoesNotStoreNonNormalizedFailedScore()
        {
            var harness = GameplayFlowHarness.Create();
            harness.StartChallenge("sprint_learn");
            harness.CompleteChallenge(true, 0f);
            harness.StartChallenge("sprint_practice");
            harness.CompleteChallenge(true, 0f);
            harness.StartChallenge("sprint_exam");
            harness.CompleteChallenge(false, 10f);
            harness.FrogJump(true);
            harness.StartChallenge("sprint_exam");
            harness.CompleteChallenge(false, 10f);

            var record = harness.Session.GetRecord(SubjectId.Sprint);
            Assert.That(record.Passed, Is.False);
            Assert.That(record.BestScore, Is.Zero);
            Assert.That(record.BestRank, Is.EqualTo(Rank.F));
            Assert.That(record.FailedVisits, Is.EqualTo(2));
        }

        [Test]
        public void RepeatedCompletionWhileTransitionIsPending_RoutesExactlyOnce()
        {
            var harness = GameplayFlowHarness.Create();
            foreach (string id in new[] { "sprint_learn", "sprint_practice", "sprint_exam", "volleyball_learn",
                         "volleyball_practice", "volleyball_exam" })
            {
                harness.StartChallenge(id);
                harness.CompleteChallenge(true, id.EndsWith("exam", StringComparison.Ordinal) ? 6f : 0f);
            }
            harness.StartChallenge("soccer_learn");
            harness.CompleteChallenge(true, 0f);
            int transitions = harness.Transitions.Count;
            Assert.That(harness.RepeatLastCompletion(), Is.False);
            Assert.That(harness.Transitions.Count, Is.EqualTo(transitions));
            Assert.That(harness.Session.Journey.IsChallengeComplete("soccer_learn"), Is.True);
            Assert.That(harness.Session.Journey.IsChallengeComplete("soccer_practice"), Is.False);
        }

        [Test]
        public void RuntimeRouter_MapsBothProductionSubjects()
        {
            var router = SceneRouter.EnsurePersistentInstance();

            AssertRoute(router, SessionRoute.Map, null);
            AssertRoute(router, SessionRoute.Map, SubjectId.Sprint);
            AssertRoute(router, SessionRoute.GameOver, null);
            AssertRoute(router, SessionRoute.FrogJump, null);
            AssertRoute(router, SessionRoute.Subject, SubjectId.Sprint);
            AssertRoute(router, SessionRoute.Subject, SubjectId.Football);
        }

        [UnityTest]
        public IEnumerator RuntimeRouter_AutoBindsRealSubjectCompletionEvents()
        {
            var router = SceneRouter.EnsurePersistentInstance();
            var mapRouteCount = 0;
            router.TransitionStarted += CountMapRoutes;

            Assert.That(router.StartSubject(SubjectId.Sprint), Is.True);
            yield return WaitForScene("MG_Sprint");

            var sprint = UnityEngine.Object.FindFirstObjectByType<SprintController>();
            Assert.That(sprint, Is.Not.Null);
            yield return JourneyRuntimeDriver.WaitForOpening();
            sprint.SetTutorialGate(false);
            sprint.Simulate(4f);
            var input = UnityEngine.Object.FindFirstObjectByType<KMA.Input.GameplayInputRouter>();
            Assert.That(input, Is.Not.Null);
            for (int tap = 0; tap < 12; tap++)
            {
                input.FeedSprintTapForTest(tap % 2 == 0 ? KMA.Input.Side.Left : KMA.Input.Side.Right,
                    1d + tap * .2d);
                yield return null;
            }
            sprint.Simulate(sprint.LearnTimeRemaining + .1f);
            var resultPanel = UnityEngine.Object.FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
            Assert.That(resultPanel, Is.Not.Null);
            Assert.That(resultPanel.CurrentResult.Pass, Is.True);
            resultPanel.Continue();
            yield return WaitForScene("Map");

            Assert.That(router.Session.Journey.IsChallengeComplete("sprint_learn"), Is.True);
            Assert.That(router.Session.Journey.CheckpointChallengeId, Is.EqualTo("sprint_practice"));
            Assert.That(router.Session.GetRecord(SubjectId.Sprint).Passed, Is.False);
            Assert.That(mapRouteCount, Is.EqualTo(1));

            yield return null;

            Assert.That(mapRouteCount, Is.EqualTo(1));
            router.TransitionStarted -= CountMapRoutes;

            void CountMapRoutes(SceneRouteTransition transition)
            {
                if (transition.Route == SessionRoute.Map)
                    mapRouteCount++;
            }
        }

        [UnityTest]
        public IEnumerator MutationsWhileTransitionIsPending_AreRejectedWithoutChangingSession()
        {
            var router = SceneRouter.EnsurePersistentInstance();
            Assert.That(router.StartSubject(SubjectId.Sprint), Is.True);
            Assert.That(router.IsTransitioning, Is.True);

            GameSession session = router.Session;
            int livesBefore = session.Lives;
            var persistenceEvents = 0;
            router.SessionChanged += () => persistenceEvents++;

            Assert.That(router.StartSubject(SubjectId.Football), Is.False, "StartSubject must be rejected.");
            Assert.That(router.SubmitSubjectResult(SubjectId.Sprint, new MinigameResult(false, 0f, Rank.F)),
                Is.False, "SubmitSubjectResult must be rejected.");
            Assert.That(router.RestartActiveSubject(), Is.False, "Restart must be rejected.");
            Assert.That(router.ExitActiveSubjectToMap(), Is.False, "Exit must be rejected.");
            Assert.That(router.RouteToMenu(), Is.False, "Menu must be rejected.");
            Assert.That(router.ResumeCampaign(), Is.False, "Continue must be rejected.");

            Assert.That(session.ActiveSubject, Is.EqualTo(SubjectId.Sprint));
            Assert.That(session.Lives, Is.EqualTo(livesBefore));
            Assert.That(session.GetRecord(SubjectId.Sprint).FailedVisits, Is.Zero);
            Assert.That(session.GetRecord(SubjectId.Sprint).Passed, Is.False);
            Assert.That(persistenceEvents, Is.Zero, "A rejected route must not emit a persistence event.");

            yield return WaitForRoutedScene(router, "MG_Sprint");

            var sprint = UnityEngine.Object.FindFirstObjectByType<SprintController>();
            Assert.That(sprint, Is.Not.Null, "The original subject route must keep its pending binding.");
            sprint.ConfigureForTest();
            sprint.AdvanceToDistance(SprintRules.RaceDistance);
            sprint.Simulate(0f);
            var resultPanel = UnityEngine.Object.FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
            Assert.That(resultPanel.CurrentResult.Pass, Is.True,
                "The original subject controller must remain bound after rejected routes.");

            Assert.That(router.ExitActiveSubjectToMap(), Is.True, "Rejection must end with the transition.");
            Assert.That(persistenceEvents, Is.EqualTo(1));
            Assert.That(session.ActiveSubject, Is.Null);
            yield return WaitForRoutedScene(router, "Map");
        }

        [UnityTest]
        public IEnumerator Continue_AfterFirstFailure_ResumesAtSubjectSelectAcrossRelaunch()
        {
            SaveData persisted = SaveData.CreateDefault();
            persisted.journey.completedChallengeIds.Add("sprint_learn");
            persisted.journey.completedChallengeIds.Add("sprint_practice");
            SceneRouter router = SceneRouter.EnsurePersistentInstance();
            CreateManager(router, () => persisted, data => persisted = data);

            Assert.That(router.StartSubject(SubjectId.Sprint), Is.True);
            yield return WaitForRoutedScene(router, "MG_Sprint");
            Assert.That(router.SubmitSubjectResult(SubjectId.Sprint, new MinigameResult(false, 0f, Rank.F)),
                Is.True);
            yield return WaitForRoutedScene(router, "Map");

            Assert.That(persisted.hasActiveSubject, Is.False);
            Assert.That(persisted.visitAttempt, Is.EqualTo(1));
            Assert.That(persisted.awaitingPunishment, Is.False);
            // A first failure costs no life at commit; it owes a frog jump instead.
            Assert.That(persisted.lives, Is.EqualTo(5));
            Assert.That(persisted.journey.pendingFrogJump, Is.Not.Null);

            DestroyAll<GameManager>();
            DestroyAll<SceneRouter>();
            yield return null;

            SceneRouter relaunched = SceneRouter.EnsurePersistentInstance();
            GameManager relaunchedManager = CreateManager(relaunched, () => persisted, data => persisted = data);
            // Quitting before the frog jump forfeits it on relaunch, which costs the life.
            Assert.That(relaunchedManager.Session.ForfeitedFrogJumpOnRestore, Is.True);
            Assert.That(relaunchedManager.Session.ResumeRoute(), Is.EqualTo(SessionRoute.Map));

            var transitions = new List<SceneRouteTransition>();
            relaunched.TransitionStarted += transitions.Add;
            GameObject shell = CreateGameObject("Relaunched shell");
            shell.AddComponent<RectTransform>();
            MainMenuScreen menu = shell.AddComponent<MainMenuScreen>();
            menu.gameObject.AddComponent<S5ShellSceneController>();

            Assert.That(menu.CanContinue, Is.True);
            menu.Continue();

            Assert.That(transitions, Has.Count.EqualTo(1));
            Assert.That(transitions[0].Route, Is.EqualTo(SessionRoute.Map));
            Assert.That(transitions[0].Subject, Is.Null);
            Assert.That(relaunched.Session.ActiveSubject, Is.Null);
            Assert.That(relaunched.Session.VisitAttempt, Is.EqualTo(1));
            Assert.That(relaunched.Session.Lives, Is.EqualTo(4));

            yield return WaitForRoutedScene(relaunched, "Map");
        }

        [UnityTest]
        public IEnumerator MutationsWhileMenuLoadIsPending_AreRejectedWithoutChangingTheSession()
        {
            SceneRouter router = SceneRouter.EnsurePersistentInstance();
            Assert.That(router.StartSubject(SubjectId.Sprint), Is.True);
            yield return WaitForRoutedScene(router, "MG_Sprint");

            GameSession session = router.Session;
            var persistenceEvents = 0;
            router.SessionChanged += () => persistenceEvents++;

            Assert.That(router.RouteToMenu(), Is.True);
            Assert.That(router.IsTransitioning, Is.True);
            Assert.That(router.StartSubject(SubjectId.Football), Is.False);
            Assert.That(router.SubmitSubjectResult(SubjectId.Sprint, new MinigameResult(false, 0f, Rank.F)),
                Is.False);
            Assert.That(router.RestartActiveSubject(), Is.False);
            Assert.That(router.ExitActiveSubjectToMap(), Is.False);
            Assert.That(router.ResumeCampaign(), Is.False);
            Assert.That(router.ResetCampaign(), Is.False);

            Assert.That(session.ActiveSubject, Is.EqualTo(SubjectId.Sprint));
            Assert.That(session.VisitAttempt, Is.EqualTo(1));
            Assert.That(persistenceEvents, Is.Zero);

            yield return WaitForScene("Menu");
            Assert.That(router.IsTransitioning, Is.False);
        }

        GameManager CreateManager(SceneRouter router, Func<SaveData> load, Action<SaveData> save)
        {
            GameObject gameObject = CreateGameObject("FullGameplayFlowTests.Manager");
            gameObject.SetActive(false);
            var manager = gameObject.AddComponent<GameManager>();
            manager.ConfigureStartup(load, save, router, _ => { }, null, () => true);
            gameObject.SetActive(true);
            return manager;
        }

        static void DestroyAll<T>() where T : Component
        {
            foreach (T component in UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                UnityEngine.Object.DestroyImmediate(component.gameObject);
            }
        }

        static IEnumerator WaitForRoutedScene(SceneRouter router, string sceneName)
        {
            while (SceneManager.GetActiveScene().name != sceneName || router.IsTransitioning)
                yield return null;
            yield return null;
        }

        static void AssertRoute(SceneRouter router, SessionRoute route, SubjectId? subject)
        {
            Assert.That(router.TryGetSceneName(route, subject, out var sceneName), Is.True);
            Assert.That(Application.CanStreamedLevelBeLoaded(sceneName), Is.True,
                $"{route} must resolve to an enabled scene.");
        }

        static IEnumerator WaitForScene(string sceneName)
        {
            while (SceneManager.GetActiveScene().name != sceneName)
                yield return null;
            yield return null;
        }

        GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            gameObjects.Add(gameObject);
            return gameObject;
        }

        sealed class GameplayFlowHarness
        {
            readonly RecordingTransitionSink sink = new RecordingTransitionSink();
            SubjectId active;
            MinigameResult lastCompletion;

            GameplayFlowHarness()
            {
                Session = new GameSession();
                Router = new SessionRouteTransitioner(Session, sink);
            }

            public GameSession Session { get; }
            public SessionRouteTransitioner Router { get; }
            public SessionRoute Route { get; private set; }
            public IReadOnlyList<SceneRouteTransition> Transitions => sink.Transitions;
            ChallengeAttemptContext activeChallenge;
            ChallengeAttemptResult lastChallenge;

            public static GameplayFlowHarness Create() => new GameplayFlowHarness();

            public void StartChallenge(string id)
            {
                ChallengeDefinition definition = Session.Journey.Catalog.Get(id);
                Assert.That(Session.TryStartChallenge(id, ChallengeAttemptMode.Journey,
                    definition.Difficulty, out activeChallenge), Is.True, id);
                RouteSession(SessionRoute.Subject, definition.Subject);
            }

            public void CompleteChallenge(bool pass, float score)
            {
                ChallengeDefinition definition = Session.Journey.Catalog.Get(activeChallenge.ChallengeId);
                MinigameResult examResult = ChallengeDefinition.IsScored(definition.Kind)
                    ? new MinigameResult(pass, pass ? score : 0f,
                        pass ? ScoreUtil.ToRank(score) : Rank.F)
                    : null;
                lastChallenge = new ChallengeAttemptResult(activeChallenge, pass,
                    new ChallengeMetrics(completedTargets: pass ? definition.TargetCount : 0), examResult);
                Assert.That(Session.SubmitChallengeResult(lastChallenge).Accepted, Is.True);
                activeChallenge = null;
                RouteSession(SessionRoute.Map, null);
            }

            /// Plays the pending frog jump and returns to the map, as the router does.
            public void FrogJump(bool reachedFinish)
            {
                FrogJumpPending pending = Session.PendingFrogJump;
                Assert.That(pending, Is.Not.Null, "A failed practice/exam must owe a frog jump.");
                RouteSession(SessionRoute.FrogJump, null);
                Assert.That(Session.TryApplyFrogJump(pending.Id, reachedFinish), Is.True);
                Assert.That(Session.TryApplyFrogJump(pending.Id, reachedFinish), Is.False);
                RouteSession(SessionRoute.Map, null);
            }

            public void Start(SubjectId id)
            {
                active = id;
                RouteSession(Session.StartSubject(id), id);
            }

            public void Pass(float score)
            {
                lastCompletion = new MinigameResult(true, score, ScoreUtil.ToRank(score));
                RouteSession(Session.SubmitResult(active, lastCompletion), active);
            }

            public void Fail() => Fail(new MinigameResult(false, 0f, Rank.F));

            public void Fail(MinigameResult result)
            {
                lastCompletion = result;
                RouteSession(Session.SubmitResult(active, result), active);
            }

            public bool RepeatLastCompletion()
            {
                if (lastChallenge != null)
                    return Session.SubmitChallengeResult(lastChallenge).Accepted;
                if (lastCompletion == null)
                    throw new InvalidOperationException("No completion is available to repeat.");

                return Router.TryRoute(Route, active);
            }

            public void CompleteTransition() => sink.CompleteActiveTransition();

            void RouteSession(SessionRoute route, SubjectId? subject)
            {
                Route = route;
                Assert.That(Router.TryRoute(route, subject), Is.True);
                sink.CompleteActiveTransition();
            }
        }

        sealed class RecordingTransitionSink : ISceneRouteTransitionSink
        {
            readonly List<SceneRouteTransition> transitions = new List<SceneRouteTransition>();
            Action complete;

            public IReadOnlyList<SceneRouteTransition> Transitions => transitions;

            public bool Begin(SceneRouteTransition transition, Action onCompleted)
            {
                transitions.Add(transition);
                complete = onCompleted;
                return true;
            }

            public void CompleteActiveTransition()
            {
                var completed = complete;
                complete = null;
                completed?.Invoke();
            }
        }
    }
}
