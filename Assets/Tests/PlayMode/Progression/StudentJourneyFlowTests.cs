using System.Collections;
using System.Linq;
using KMA.Gameplay;
using KMA.Gameplay.Celebration;
using KMA.Gameplay.Chess;
using KMA.Gameplay.Core;
using KMA.Gameplay.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KMA.Tests.Gameplay.Progression
{
    public sealed class StudentJourneyFlowTests
    {
        GameObject runtime;
        SceneRouter router;
        SaveData saved;
        int originalFrameRate, originalVsync;

        [UnityTest]
        public IEnumerator BootstrapToSummaryUsesTheRealControllersAndPersistsCompletion()
        {
            yield return StartRuntime();
            foreach (string id in new[] { "sprint_learn", "sprint_practice", "sprint_exam",
                         "volleyball_learn", "volleyball_practice", "volleyball_exam",
                         "soccer_learn", "soccer_practice", "soccer_exam" })
            {
                yield return PlayCheckpoint();
                Assert.That(router.Session.Journey.IsChallengeComplete(id), Is.True, id);
                if (id == "sprint_practice")
                    Assert.That(router.Session.Journey.IsSubjectUnlocked(SubjectId.Volleyball), Is.False);
            }
            Assert.That(router.Session.Journey.CheckpointChallengeId, Is.EqualTo("chess_final"));
            Assert.That(router.Session.Journey.CourseComplete, Is.False);
            yield return PlayChessFinal();
            Assert.That(saved.journey.completedChallengeIds, Does.Contain("chess_final"),
                "The final commit must autosave before navigation.");
            Assert.That(saved.journey.chessBest.recorded, Is.True);
            Assert.That(saved.journey.celebrationSeen, Is.False);

            // The first win of the final continues into the celebration, which saves only its seen flag.
            Object.FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include).Continue();
            yield return JourneyRuntimeDriver.WaitForScene(router, "Celebration");
            var celebration = Object.FindFirstObjectByType<CelebrationSceneController>();
            Assert.That(celebration.Summary.IsSample, Is.False);
            Assert.That(celebration.Summary.ChessRecorded, Is.True);
            Assert.That(saved.journey.celebrationSeen, Is.True);
            celebration.Skip();
            celebration.GoToMap();
            yield return JourneyRuntimeDriver.WaitForScene(router, "Map");
            Assert.That(router.Session.Journey.CourseComplete, Is.True);
            Assert.That(router.Session.Records.Values.Count(record => record.Passed), Is.EqualTo(4));
            var reloaded = new GameSession();
            reloaded.Restore(saved);
            Assert.That(reloaded.Journey.CourseComplete, Is.True);
            Assert.That(Object.FindFirstObjectByType<JourneyCourseSummary>().ScoreRows.Count, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator VolleyballExamFailuresGoThroughTheFrogJumpAndKeepSprintPassed()
        {
            yield return StartRuntime();
            for (int i = 0; i < 5; i++) yield return PlayCheckpoint();
            Assert.That(router.Session.Journey.CheckpointChallengeId, Is.EqualTo("volleyball_exam"));
            ChallengeDefinition exam = router.Session.Journey.Catalog.Get("volleyball_exam");
            yield return StartCheckpoint();

            // Failure #1 costs nothing at commit; losing its frog jump costs one life.
            yield return FailIntoFrogJump(exam);
            Assert.That(router.Session.Lives, Is.EqualTo(5));
            Assert.That(router.Session.PendingFrogJump.SavesLife, Is.True);
            yield return FinishFrogJump(false, "MG_Volleyball");
            Assert.That(router.Session.Lives, Is.EqualTo(4));
            Assert.That(router.Session.Journey.ActiveAttempt.ChallengeId, Is.EqualTo("volleyball_exam"));

            // Failure #2 costs a life at commit; its frog jump result cannot change lives.
            yield return FailIntoFrogJump(exam);
            Assert.That(router.Session.Lives, Is.EqualTo(3));
            Assert.That(router.Session.PendingFrogJump.SavesLife, Is.False);
            yield return FinishFrogJump(true, "MG_Volleyball");
            Assert.That(router.Session.Lives, Is.EqualTo(3));

            Assert.That(router.Session.GetRecord(SubjectId.Sprint).Passed, Is.True);
            Assert.That(router.Session.Journey.IsSubjectUnlocked(SubjectId.Football), Is.False);
            yield return JourneyRuntimeDriver.CompleteCurrent(exam, true);
            Assert.That(router.Session.GetRecord(SubjectId.Volleyball).Passed, Is.True);
            Assert.That(router.Session.Journey.IsSubjectUnlocked(SubjectId.Football), Is.True);
            Assert.That(router.Route(SessionRoute.Map), Is.True);
            yield return JourneyRuntimeDriver.WaitForScene(router, "Map");
        }

        [UnityTest]
        public IEnumerator TheRealFrogJumpSceneReportsAWinThroughItsControllerAndKeepsTheLife()
        {
            ChallengeDefinition practice = null;
            KMA.Gameplay.FrogJump.FrogJumpController frog = null;
            yield return EnterRealFrogJump(c => practice = c, f => frog = f);

            // Every jump is stopped at the needle centre (half a sweep) for the longest hop.
            float toCentre = frog.Rules.Tuning.sweepSeconds * .5f;
            int guard = 0;
            while (!frog.Rules.IsOver)
            {
                Assert.That(guard++, Is.LessThan(60), "The frog never reached the finish.");
                frog.Rules.Tick(toCentre);
                frog.Rules.Stop();
                frog.Rules.Tick(frog.Rules.Tuning.jumpSeconds);
            }
            Assert.That(frog.Rules.ReachedFinish, Is.True);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (router.Session.PendingFrogJump != null)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "The frog scene never reported.");
                yield return null;
            }

            Assert.That(router.Session.Lives, Is.EqualTo(5), "Winning the first-failure frog jump saves the life.");
            Assert.That(string.IsNullOrEmpty(saved.journey.pendingFrogJump?.id), Is.True);
            Assert.That(saved.lives, Is.EqualTo(5));
            ResultPanel panel = Object.FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
            Assert.That(panel.IsVisible, Is.True);
            Assert.That(panel.CurrentResult.Pass, Is.True);
            Assert.That(panel.StatusText, Is.EqualTo(VietText.Fix("VỀ ĐÍCH")));
            Assert.That(panel.ContinueLabel, Is.EqualTo(VietText.Fix("LUYỆN LẠI")));
            panel.Continue();
            yield return JourneyRuntimeDriver.WaitForScene(router, "MG_Sprint");
            Assert.That(router.Session.Journey.ActiveAttempt.ChallengeId, Is.EqualTo(practice.Id));
        }

        [UnityTest]
        public IEnumerator TheRealFrogJumpSceneEndsThroughItsControllerAndRetriesThePractice()
        {
            ChallengeDefinition practice = null;
            KMA.Gameplay.FrogJump.FrogJumpController frog = null;
            yield return EnterRealFrogJump(c => practice = c, f => frog = f);
            frog.Rules.Tick(61f);
            Assert.That(frog.Rules.IsOver, Is.True);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (router.Session.PendingFrogJump != null)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "The frog scene never reported.");
                yield return null;
            }

            Assert.That(router.Session.Lives, Is.EqualTo(4), "Losing the first-failure frog jump costs a life.");
            Assert.That(string.IsNullOrEmpty(saved.journey.pendingFrogJump?.id), Is.True,
                "The frog result persists.");
            Assert.That(saved.lives, Is.EqualTo(4));
            ResultPanel panel = Object.FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
            Assert.That(panel.IsVisible, Is.True);
            Assert.That(panel.CurrentResult.Pass, Is.False);
            Assert.That(panel.ContinueLabel, Is.EqualTo(VietText.Fix("LUYỆN LẠI")));
            panel.Continue();
            yield return JourneyRuntimeDriver.WaitForScene(router, "MG_Sprint");
            Assert.That(router.Session.Journey.ActiveAttempt.ChallengeId, Is.EqualTo("sprint_practice"));
        }

        IEnumerator EnterRealFrogJump(System.Action<ChallengeDefinition> practiceOut,
            System.Action<KMA.Gameplay.FrogJump.FrogJumpController> frogOut)
        {
            yield return StartRuntime();
            yield return PlayCheckpoint();
            ChallengeDefinition practice = router.Session.Journey.Catalog.Get("sprint_practice");
            yield return StartCheckpoint();
            yield return FailIntoFrogJump(practice);
            Assert.That(router.Session.PendingFrogJump.SavesLife, Is.True);

            // Drive the scene's own controller: no test hook completes the frog jump here.
            var frog = Object.FindFirstObjectByType<KMA.Gameplay.FrogJump.FrogJumpController>();
            Assert.That(frog, Is.Not.Null);
            foreach (Transform item in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                if (item.name == "HeartBar" && item.GetComponentInParent<Canvas>(true) != null &&
                    item.gameObject.scene.name == "MG_FrogJump")
                    Assert.That(item.gameObject.activeInHierarchy, Is.False, "Unbound frog HUD hearts are hidden.");
            yield return JourneyRuntimeDriver.WaitForOpening();
            frog.SetTutorialGate(false);
            var lifecycle = (MinigameLifecycle)typeof(MinigameBase).GetField("lifecycle",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(frog);
            lifecycle.Tick(10f);
            Assert.That(frog.PresentationPhase, Is.EqualTo(MinigamePhase.Play));
            practiceOut(practice);
            frogOut(frog);
        }

        IEnumerator FailIntoFrogJump(ChallengeDefinition challenge)
        {
            yield return JourneyRuntimeDriver.CompleteCurrent(challenge, false);
            ResultPanel panel = Object.FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
            Assert.That(panel.CurrentResult.Pass, Is.False, challenge.Id);
            Assert.That(panel.RetryAvailable, Is.False, "A failure that owes a frog jump offers no retry.");
            Assert.That(panel.ContinueLabel, Is.EqualTo(VietText.Fix("BẬT CÓC")));
            Assert.That(router.Session.PendingFrogJump, Is.Not.Null);
            panel.Continue();
            yield return JourneyRuntimeDriver.WaitForScene(router, "MG_FrogJump");
        }

        IEnumerator FinishFrogJump(bool reachedFinish, string retryScene)
        {
            router.CompleteFrogJumpForTests(reachedFinish);
            Assert.That(router.Session.PendingFrogJump, Is.Null);
            ResultPanel panel = Object.FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
            Assert.That(panel.IsVisible, Is.True, "The frog jump scene shows its own result.");
            Assert.That(panel.ContinueLabel, Is.EqualTo(VietText.Fix("THI LẠI")));
            panel.Continue();
            yield return JourneyRuntimeDriver.WaitForScene(router, retryScene);
        }

        [UnityTest]
        public IEnumerator VolleyballPauseExitTapReturnsToTheMap()
        {
            yield return StartRuntime();
            for (int i = 0; i < 3; i++) yield return PlayCheckpoint();
            yield return StartCheckpoint();
            yield return PauseAndExitVolleyballToTheMap();
        }

        [UnityTest]
        public IEnumerator VolleyballPauseExitAfterAFrogJumpRetryReturnsToTheMap()
        {
            yield return StartRuntime();
            for (int i = 0; i < 5; i++) yield return PlayCheckpoint();
            ChallengeDefinition exam = router.Session.Journey.Catalog.Get("volleyball_exam");
            yield return StartCheckpoint();
            yield return FailIntoFrogJump(exam);
            yield return FinishFrogJump(true, "MG_Volleyball");
            yield return PauseAndExitVolleyballToTheMap();
        }

        [UnityTest]
        public IEnumerator VolleyballPauseExitAfterARestartReturnsToTheMap()
        {
            yield return StartRuntime();
            for (int i = 0; i < 3; i++) yield return PlayCheckpoint();
            yield return StartCheckpoint();
            yield return WaitForVolleyballPlay();
            var pause = Object.FindFirstObjectByType<PausePanel>(FindObjectsInactive.Include);
            yield return Tap(pause.GetComponent<Button>(), 0f);
            Button restart = pause.transform.parent.GetComponentsInChildren<Button>(true)
                .First(button => button.name == "RestartButton");
            int loads = 0;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> counted = (_, _) => loads++;
            SceneManager.sceneLoaded += counted;
            yield return Tap(restart);
            float reloadBy = Time.realtimeSinceStartup + 12f;
            while ((loads == 0 || router.IsTransitioning) && Time.realtimeSinceStartup < reloadBy)
                yield return null;
            SceneManager.sceneLoaded -= counted;
            Assert.That(loads, Is.EqualTo(1), "Restart must reload the volleyball scene.");
            yield return null;
            yield return PauseAndExitVolleyballToTheMap();
        }

        IEnumerator WaitForVolleyballPlay()
        {
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MG_Volleyball"));
            var volley = Object.FindFirstObjectByType<KMA.Gameplay.Volleyball.VolleyballController>();
            yield return JourneyRuntimeDriver.WaitForOpening();
            volley.SetTutorialGate(false);
            float playBy = Time.realtimeSinceStartup + 8f;
            while (volley.PresentationPhase != MinigamePhase.Play && Time.realtimeSinceStartup < playBy)
                yield return null;
            Assert.That(volley.PresentationPhase, Is.EqualTo(MinigamePhase.Play));
            yield return new WaitForSeconds(1f);
        }

        IEnumerator PauseAndExitVolleyballToTheMap()
        {
            yield return WaitForVolleyballPlay();
            var pause = Object.FindFirstObjectByType<PausePanel>(FindObjectsInactive.Include);
            Assert.That(pause, Is.Not.Null);
            yield return Tap(pause.GetComponent<Button>(), 0f);
            Assert.That(pause.IsOpen, Is.True, "Tapping the pause button must open the menu.");

            Button exit = pause.transform.parent.GetComponentsInChildren<Button>(true)
                .First(button => button.name == "ExitButton");
            yield return Tap(exit);
            yield return JourneyRuntimeDriver.WaitForScene(router, "Map");
            float settle = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < settle) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Map"), "Exit must stay on the map.");
            Assert.That(router.Session.ActiveSubject, Is.Null);
            Assert.That(router.Session.Journey.ActiveAttempt, Is.Null);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        // A real touch through the Input System and the scene's UI input module, with the small
        // drift a finger has on a phone screen.
        static IEnumerator Tap(Button button, float drift = 20f)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.isActiveAndEnabled && button.interactable, Is.True, button.name + " is not clickable");
            Canvas canvas = button.GetComponentInParent<Canvas>().rootCanvas;
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(eventCamera,
                button.transform.TransformPoint(((RectTransform)button.transform).rect.center));
            Assert.That(EventSystem.current, Is.Not.Null, "No EventSystem in the scene");

            // Batch-mode players never have focus; let the simulated touch through anyway.
            InputSettings settings = InputSystem.settings;
            InputSettings.BackgroundBehavior background = settings.backgroundBehavior;
            InputSettings.EditorInputBehaviorInPlayMode editorBehaviour = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var touchscreen = InputSystem.AddDevice<Touchscreen>();
            try
            {
                yield return Touch(touchscreen, UnityEngine.InputSystem.TouchPhase.Began, screen);
                yield return Touch(touchscreen, UnityEngine.InputSystem.TouchPhase.Moved, screen + new Vector2(drift, 0f));
                yield return Touch(touchscreen, UnityEngine.InputSystem.TouchPhase.Ended, screen + new Vector2(drift, 0f));
            }
            finally
            {
                InputSystem.RemoveDevice(touchscreen);
                settings.backgroundBehavior = background;
                settings.editorInputBehaviorInPlayMode = editorBehaviour;
            }
        }

        static IEnumerator Touch(Touchscreen touchscreen, UnityEngine.InputSystem.TouchPhase phase, Vector2 position)
        {
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 1, phase = phase, position = position });
            yield return null;
            yield return null;
        }

        IEnumerator StartRuntime()
        {
            originalFrameRate = Application.targetFrameRate;
            originalVsync = QualitySettings.vSyncCount;
            foreach (var manager in Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None))
                Object.DestroyImmediate(manager.gameObject);
            foreach (var oldRouter in Object.FindObjectsByType<SceneRouter>(FindObjectsSortMode.None))
                Object.DestroyImmediate(oldRouter.gameObject);
            router = SceneRouter.EnsurePersistentInstance();
            runtime = new GameObject("StudentJourneyRuntime");
            runtime.SetActive(false);
            GameManager owner = runtime.AddComponent<GameManager>();
            owner.ConfigureStartup(() => SaveData.CreateDefault(), data =>
                saved = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), router, _ => { },
                configuredHasExistingSave: () => false);
            runtime.SetActive(true);
            yield return SceneManager.LoadSceneAsync("Bootstrap");
            Assert.That(router.RouteToMenu(), Is.True);
            yield return JourneyRuntimeDriver.WaitForScene(router, "Menu");
            var menu = Object.FindFirstObjectByType<MainMenuScreen>();
            menu.NewGame();
            yield return JourneyRuntimeDriver.WaitForScene(router, "Map");
            Assert.That(ChallengeCatalog.LoadDefault().Ordered.Count, Is.EqualTo(10));
            Assert.That(JourneyDialogueLibrary.LoadDefault(), Is.Not.Null);
            var emoji = Resources.Load<TMPro.TMP_SpriteAsset>("Journey/JourneyEmoji");
            Assert.That(emoji, Is.Not.Null);
            Assert.That(emoji.spriteCharacterTable.Count, Is.EqualTo(DialogueEmoji.KnownNames.Count));
            Assert.That(SprintBalanceConfig.LoadDefault(), Is.Not.Null);
        }

        IEnumerator StartCheckpoint()
        {
            yield return JourneyRuntimeDriver.SkipDialogues();
            string id = router.Session.Journey.CheckpointChallengeId;
            ChallengeDefinition challenge = router.Session.Journey.Catalog.Get(id);
            Assert.That(router.TryStartChallenge(id, ChallengeAttemptMode.Journey, challenge.Difficulty), Is.True, id);
            yield return JourneyRuntimeDriver.WaitForScene(router, challenge.Subject switch
            {
                SubjectId.Sprint => "MG_Sprint", SubjectId.Volleyball => "MG_Volleyball",
                SubjectId.Chess => "MG_ChessFinal", _ => "MG_Football"
            });
        }

        /// Plays the puzzle main line on the real board, like ChessFinalSceneTests, and waits for the
        /// committed result on the real result panel.
        IEnumerator PlayChessFinal()
        {
            yield return StartCheckpoint();
            var controller = Object.FindFirstObjectByType<ChessFinalController>();
            var board = Object.FindFirstObjectByType<ChessBoardView>();
            yield return JourneyRuntimeDriver.WaitForOpening();
            controller.BeginAttempt();
            yield return null;
            yield return null;
            PuzzleDefinition puzzle = controller.Machine.Puzzle;
            yield return PlayChessMove(board, puzzle.nodes[0].moves[0].uci);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (controller.Machine.Phase != ChessFinalPhase.Completed)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "The boss reply did not arrive.");
                if (controller.Machine.Phase == ChessFinalPhase.PlayerTurn)
                    yield return PlayChessMove(board, puzzle.nodes[controller.Machine.Node].moves[0].uci);
                else yield return null;
            }
            ResultPanel panel = null;
            deadline = Time.realtimeSinceStartup + 10f;
            while (panel == null || panel.CurrentResult == null)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "The final result was not shown.");
                panel = Object.FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
                yield return null;
            }
            Assert.That(panel.CurrentResult.Pass, Is.True);
            Assert.That(router.Session.Journey.CourseComplete, Is.True);
        }

        static IEnumerator PlayChessMove(ChessBoardView board, string uci)
        {
            Assert.That(ChessMove.TryParseUci(uci, out ChessMove move), Is.True, uci);
            board.ClickSquare(move.From);
            board.ClickSquare(move.To);
            yield return null;
        }

        IEnumerator PlayCheckpoint(bool pass = true)
        {
            string id = router.Session.Journey.CheckpointChallengeId;
            ChallengeDefinition challenge = router.Session.Journey.Catalog.Get(id);
            yield return StartCheckpoint();
            if (challenge.Subject == SubjectId.Football)
            {
                Assert.That(router.Session.Journey.ActiveAttempt.Difficulty, Is.EqualTo(ChallengeDifficulty.Normal));
                var hud = Object.FindFirstObjectByType<FootballHud>();
                foreach (Transform item in hud.GetComponentsInChildren<Transform>(true))
                    if (item.name == "Easy" || item.name == "Normal" || item.name == "Hard" || item.name == "DifficultyTitle")
                        Assert.That(item.gameObject.activeSelf, Is.False, item.name);
            }
            yield return JourneyRuntimeDriver.CompleteCurrent(challenge, pass);
            ResultPanel panel = Object.FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
            Assert.That(panel.CurrentResult, Is.Not.Null, id);
            Assert.That(panel.CurrentResult.Pass, Is.EqualTo(pass), id);
            Assert.That(saved.journey.lastCommittedAttemptId, Is.Not.Null, "The receipt must persist before navigation.");
            // Returning to Map also covers quitting at a result without replaying its receipt.
            Assert.That(router.Route(SessionRoute.Map), Is.True);
            yield return JourneyRuntimeDriver.WaitForScene(router, "Map");
        }

        [UnityTearDown]
        public IEnumerator CleanupRuntime()
        {
            if (runtime != null)
            {
                Object.DestroyImmediate(runtime);
                if (router != null) Object.DestroyImmediate(router.gameObject);
                foreach (var splash in Object.FindObjectsByType<SplashScreenPresenter>(FindObjectsSortMode.None))
                    Object.Destroy(splash.gameObject);
                Application.targetFrameRate = originalFrameRate;
                QualitySettings.vSyncCount = originalVsync;
            }
            yield return null;
        }

        [Test]
        public void NewStudent_CanCompleteEveryChallengeAndUnlockAllSubjects()
        {
            var session = new GameSession();
            Assert.That(session.Journey.CheckpointChallengeId, Is.EqualTo("sprint_learn"));

            foreach (ChallengeDefinition challenge in session.Journey.Catalog.Ordered)
            {
                Assert.That(session.Journey.CheckpointChallengeId, Is.EqualTo(challenge.Id));
                JourneyCommitOutcome outcome = JourneyGameplayDriver.CompleteActiveChallenge(session);
                Assert.That(outcome.Accepted, Is.True, challenge.Id);
                Assert.That(session.Journey.IsChallengeComplete(challenge.Id), Is.True, challenge.Id);
            }

            Assert.That(session.Journey.CourseComplete, Is.True);
            Assert.That(session.Journey.CheckpointChallengeId, Is.Null);
            Assert.That(session.Lives, Is.EqualTo(GameSession.MaxLives));
            Assert.That(session.GetRecord(SubjectId.Sprint).Passed, Is.True);
            Assert.That(session.GetRecord(SubjectId.Volleyball).Passed, Is.True);
            Assert.That(session.GetRecord(SubjectId.Football).Passed, Is.True);
        }

        [Test]
        public void FailedExam_IsRecoveredThroughTheFrogJumpAndRetry()
        {
            SceneRouter frogRouter = JourneyRoutingFixture.CreateRouter();
            ResultPanel panel = JourneyRoutingFixture.CreateResultPanel();
            try
            {
                GameSession session = frogRouter.Session;
                JourneyRoutingFixture.CompleteThrough(session, "sprint_practice");

                JourneyCommitOutcome failure = JourneyGameplayDriver.CompleteActiveChallenge(session, false);
                Assert.That(failure.Accepted, Is.True);
                Assert.That(failure.FrogJumpRequired, Is.True);
                Assert.That(failure.FrogJumpSavesLife, Is.True);
                Assert.That(session.Lives, Is.EqualTo(5));
                Assert.That(JourneyGameplayDriver.CompleteActiveChallenge(session).Accepted, Is.False,
                    "The exam cannot be retried before the frog jump.");

                frogRouter.CompleteFrogJumpForTests(true);
                panel.Continue();
                ChallengeAttemptContext retry = session.Journey.ActiveAttempt;
                Assert.That(retry.ChallengeId, Is.EqualTo("sprint_exam"));
                Assert.That(session.Lives, Is.EqualTo(5));

                Assert.That(session.SubmitChallengeResult(new ChallengeAttemptResult(retry, true,
                    new ChallengeMetrics(), new MinigameResult(true, 6f, ScoreUtil.ToRank(6f)))).Accepted, Is.True);
                Assert.That(session.Journey.IsChallengeComplete("sprint_exam"), Is.True);
                Assert.That(session.GetRecord(SubjectId.Sprint).Passed, Is.True);
            }
            finally
            {
                JourneyRoutingFixture.Destroy(frogRouter, panel);
            }
        }
    }
}
