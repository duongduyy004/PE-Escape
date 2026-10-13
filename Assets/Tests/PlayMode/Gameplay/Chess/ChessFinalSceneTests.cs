using System;
using System.Collections;
using System.Linq;
using KMA.Gameplay;
using KMA.Gameplay.Chess;
using KMA.Gameplay.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessFinalSceneTests
    {
        ChessFinalController controller;
        ChessBoardView board;
        ChessFinalHud hud;
        MinigameResult result;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("MG_ChessFinal", LoadSceneMode.Single);
            yield return null;
            controller = UnityEngine.Object.FindFirstObjectByType<ChessFinalController>();
            board = UnityEngine.Object.FindFirstObjectByType<ChessBoardView>();
            hud = UnityEngine.Object.FindFirstObjectByType<ChessFinalHud>();
            yield return WaitFor(() => controller.PuzzleReady, "the puzzle was not generated", 20f);
            var opening = UnityEngine.Object.FindFirstObjectByType<KMA.Gameplay.UI.GameplayOpeningFlow>();
            if (opening != null) yield return new WaitUntil(() => opening.IsComplete);
            result = null;
            controller.Completed += r => result = r;
        }

        [UnityTearDown]
        public IEnumerator ResetTime()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        static IEnumerator WaitFor(Func<bool> condition, string what, float seconds = 10f)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(condition(), Is.True, what);
        }

        /// First optimal move of the live board, from the same builder the level-3 hint uses.
        ChessMove Optimal() => ChessHintBuilder.Build(new ChessSearchEngine(SearchBudget.Default()),
            new HintRequest
            {
                Id = 0, Fen = controller.Machine.Position.ToFen(),
                MovesLeft = controller.Machine.MaxMoves - controller.Machine.MovesUsed, Level = 3
            }).Move;

        /// A legal, non-mating move that loses quality; falls back to any legal non-mating move.
        ChessMove Suboptimal()
        {
            ChessAttemptMachine machine = controller.Machine;
            var engine = new ChessSearchEngine(SearchBudget.Default());
            int movesLeft = machine.MaxMoves - machine.MovesUsed;
            ChessMove fallback = default;
            foreach (ChessMove move in MoveGenerator.LegalMoves(machine.Position).Where(m => m.Promotion == 0))
            {
                ChessPosition after = machine.Position.Apply(move);
                if (MoveGenerator.IsCheckmate(after) || MoveGenerator.LegalMoves(after).Count == 0) continue;
                fallback = move;
                if (ChessAttemptGrader.Evaluate(engine, machine.Position, move, movesLeft).Q < .999) return move;
            }
            return fallback;
        }

        IEnumerator Play(ChessMove move)
        {
            board.ClickSquare(move.From);
            board.ClickSquare(move.To);
            yield return null;
        }

        IEnumerator Begin()
        {
            controller.BeginAttempt();
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator BoardIsLockedAndClockStillUntilStart()
        {
            Assert.That(controller.Machine.Phase, Is.EqualTo(ChessAttemptPhase.Intro));
            Assert.That(board.Interactable, Is.False);
            yield return new WaitForSeconds(.3f);
            Assert.That(controller.Machine.Clock.Elapsed, Is.EqualTo(0f));
            yield return Begin();
            Assert.That(board.Interactable, Is.True);
            Assert.That(controller.Machine.Clock.Running, Is.True);
        }

        [UnityTest]
        public IEnumerator DirectSceneUsesNormalDifficulty()
        {
            ChessAttemptMachine machine = controller.Machine;
            Assert.That(machine.OptimalMoves, Is.EqualTo(2));
            Assert.That(machine.MaxMoves, Is.EqualTo(3));
            Assert.That(machine.Clock.Limit, Is.InRange(60f, 240f));
            Assert.That(hud.MovesText, Does.Contain("0/3"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator OptimalLineSolvesAndReportsGrade()
        {
            yield return Begin();
            for (int i = 0; i < controller.Machine.MaxMoves && result == null; i++)
            {
                yield return WaitFor(() => controller.Machine.Phase == ChessAttemptPhase.PlayerTurn || result != null, "player turn");
                if (result != null) break;
                yield return Play(Optimal());
            }
            yield return WaitFor(() => result != null, "the result was not published");
            Assert.That(result.Pass, Is.True);
            Assert.That(result.Score, Is.GreaterThanOrEqualTo(8.9f));
            Assert.That(controller.Machine.Moves.All(m => m.Q > .999), Is.True);
            Assert.That(controller.BuildMetrics(null, result).Detail, Does.StartWith("Nước:"));
        }

        IEnumerator PlayOptimalLineToTheResult()
        {
            yield return Begin();
            for (int i = 0; i < controller.Machine.MaxMoves && result == null; i++)
            {
                yield return WaitFor(() => controller.Machine.Phase == ChessAttemptPhase.PlayerTurn || result != null, "player turn");
                if (result != null) break;
                yield return Play(Optimal());
            }
            yield return WaitFor(() => result != null, "the result was not published");
        }

        /// The standalone scene has no SceneRouter, so show the card the router would show for the Final.
        ResultPanel ShowChallengeCard(bool solved)
        {
            var panel = UnityEngine.Object.FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
            ChallengeDefinition definition = new GameSession().Journey.Catalog.Get("chess_final");
            var context = new ChallengeAttemptContext("t", "chess_final", ChallengeAttemptMode.Journey, ChallengeDifficulty.Normal);
            var challenge = new ChallengeAttemptResult(context, solved, controller.BuildMetrics(definition, result), result);
            panel.ShowChallenge(context, challenge, new JourneyCommitOutcome(true, null, 0, false, false, solved, true), null);
            return panel;
        }

        [UnityTest]
        public IEnumerator ResultDetailListsMovesAndTimeAfterAWin()
        {
            yield return PlayOptimalLineToTheResult();
            ResultPanel panel = ShowChallengeCard(true);
            Assert.That(panel.DetailText, Does.Contain("Nước:"));
            Assert.That(panel.DetailText, Does.Contain("Tối ưu:"));
            Assert.That(panel.DetailText, Does.Contain("Chiếu hết"));
            Assert.That(result.Score, Is.GreaterThanOrEqualTo(8.9f), "the score label counts up, so the result is checked");
        }

        [UnityTest]
        public IEnumerator ChessRankLabelShowsAPlus()
        {
            yield return PlayOptimalLineToTheResult();
            Assert.That(result.Rank, Is.EqualTo(Rank.S), "A+ is stored as the top rank");
            ResultPanel panel = ShowChallengeCard(true);
            Assert.That(panel.RankText, Does.Contain("A+"));
        }

        [UnityTest]
        public IEnumerator SuboptimalMoveIsPlayedNotUndone()
        {
            yield return Begin();
            string start = controller.Machine.Position.ToFen();
            yield return Play(Suboptimal());
            yield return WaitFor(() => controller.Machine.Phase != ChessAttemptPhase.Analyzing, "analysis finished");
            Assert.That(controller.Machine.MovesUsed, Is.EqualTo(1));
            Assert.That(controller.Machine.Position.ToFen(), Is.Not.EqualTo(start));
            Assert.That(hud.MovesText, Does.Contain("1/"));
        }

        [UnityTest]
        public IEnumerator IllegalMoveChangesNothingAndTheClockKeepsRunning()
        {
            yield return Begin();
            string start = controller.Machine.Position.ToFen();
            controller.RequestMove(0, 0);
            yield return null;
            Assert.That(controller.Machine.MovesUsed, Is.EqualTo(0));
            Assert.That(controller.Machine.Position.ToFen(), Is.EqualTo(start));
            Assert.That(controller.Machine.Phase, Is.EqualTo(ChessAttemptPhase.PlayerTurn));
            Assert.That(controller.Machine.Clock.Running, Is.True);
        }

        [UnityTest]
        public IEnumerator PauseFreezesTheClock()
        {
            yield return Begin();
            Time.timeScale = 0f;
            yield return null;
            float frozen = controller.Machine.Clock.Elapsed;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(controller.Machine.Clock.Elapsed, Is.EqualTo(frozen));
            Assert.That(controller.Machine.Phase, Is.EqualTo(ChessAttemptPhase.Paused));
            Time.timeScale = 1f;
            yield return null;
            Assert.That(controller.Machine.Phase, Is.EqualTo(ChessAttemptPhase.PlayerTurn));
        }

        [UnityTest]
        public IEnumerator AnalysisResultThatArrivesWhilePausedWaitsAndIsAppliedOnce()
        {
            yield return Begin();
            ChessMove move = Optimal();
            board.ClickSquare(move.From);
            board.ClickSquare(move.To);
            Time.timeScale = 0f;
            yield return null;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(controller.Machine.Phase, Is.EqualTo(ChessAttemptPhase.Paused));
            Assert.That(controller.Machine.PendingBossReply, Is.Null, "not applied while paused");
            Time.timeScale = 1f;
            yield return WaitFor(() => controller.Machine.Phase == ChessAttemptPhase.BossTurn ||
                                       controller.Machine.Phase == ChessAttemptPhase.PlayerTurn, "resumed");
            Assert.That(controller.Machine.Moves.Count, Is.EqualTo(1), "the move was recorded once");
        }

        [UnityTest]
        public IEnumerator HintStopsTheClockUntilShown()
        {
            yield return Begin();
            controller.RevealHint();
            Assert.That(controller.Machine.Phase, Is.EqualTo(ChessAttemptPhase.Analyzing));
            Assert.That(controller.Machine.Clock.Running, Is.False);
            yield return WaitFor(() => controller.Machine.Phase == ChessAttemptPhase.PlayerTurn, "hint shown");
            Assert.That(controller.Machine.Clock.Running, Is.True);
            Assert.That(controller.Machine.HintLevel, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RestartPuzzleKeepsTheSamePuzzle()
        {
            yield return Begin();
            string fen = controller.Machine.Snapshot.fen;
            ChessMove move = Optimal();
            board.ClickSquare(move.From);
            board.ClickSquare(move.To);
            // The result is applied on the next Update, so a failed analysis can be forced before it.
            ChessAttemptMachine machine = controller.Machine;
            machine.CompleteAnalysis(new AnalysisResult
            {
                Id = machine.PendingAnalysis.Id, Status = SearchStatus.Unknown,
                Quality = new MoveQuality(SearchStatus.Unknown, 0, 0, 0)
            });
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.AnalysisError));
            Assert.That(hud.AnalysisErrorVisible, Is.True);

            hud.RestartPuzzleButton.onClick.Invoke();
            yield return null;
            Assert.That(machine.Snapshot.fen, Is.EqualTo(fen));
            Assert.That(machine.Position.ToFen(), Is.EqualTo(fen));
            Assert.That(machine.MovesUsed, Is.EqualTo(0));
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.Intro));
            Assert.That(hud.AnalysisErrorVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator DisablingTheControllerDuringAnalysisIsQuiet()
        {
            yield return Begin();
            ChessMove move = Optimal();
            board.ClickSquare(move.From);
            board.ClickSquare(move.To);
            controller.enabled = false;
            yield return new WaitForSecondsRealtime(1f);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PauseDuringTheBossSlideStillPlaysTheBossMoveOnResume()
        {
            yield return Begin();
            yield return Play(Optimal());
            yield return WaitFor(() => controller.Machine.Phase == ChessAttemptPhase.BossTurn, "boss turn");
            ChessMove reply = controller.Machine.PendingBossReply.Value;
            ChessPosition beforeReply = controller.Machine.Position;
            yield return WaitFor(() => board.transform.Find("MovingPiece") != null, "boss slide");

            // Pause lands while the slide is still running. The controller is disabled so its Update
            // cannot auto-resume; its coroutines keep running, so the slide finishes while paused.
            controller.enabled = false;
            controller.Machine.Pause();
            yield return new WaitForSeconds(1.5f);
            Assert.That(controller.Machine.Phase, Is.EqualTo(ChessAttemptPhase.Paused));
            Assert.That(controller.Machine.Position.ToFen(), Is.EqualTo(beforeReply.ToFen()),
                "the boss move is not applied while paused");

            controller.Machine.Resume();
            controller.enabled = true;
            yield return WaitFor(() => controller.Machine.Phase == ChessAttemptPhase.PlayerTurn, "player turn again", 3f);
            Assert.That(controller.Machine.Position.ToFen(), Is.EqualTo(beforeReply.Apply(reply).ToFen()));
            Assert.That(board.Interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator PosesFollowTheTurn()
        {
            var cast = UnityEngine.Object.FindFirstObjectByType<ChessCastView>();
            yield return Begin();
            Assert.That(cast.TeacherPose, Is.EqualTo("strictLook"));
            yield return Play(Optimal());
            Assert.That(cast.StudentPose, Is.EqualTo("idle"), "the student settles once the move is played");
            yield return WaitFor(() => controller.Machine.Phase == ChessAttemptPhase.BossTurn, "boss turn");
            Assert.That(cast.TeacherPose, Is.EqualTo("chessThink").Or.EqualTo("chessMove"));
            yield return WaitFor(() => controller.Machine.Phase == ChessAttemptPhase.PlayerTurn, "player turn again", 5f);
            Assert.That(cast.TeacherPose, Is.EqualTo("strictLook"));
        }

        [UnityTest]
        public IEnumerator TheTeacherBubbleDoesNotExpireWhileTheGameIsFrozen()
        {
            var cast = UnityEngine.Object.FindFirstObjectByType<ChessCastView>();
            cast.Say("Đọc đề nhé.", .3f);
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(cast.BubbleVisible, Is.True, "the guide or pause menu is still open");
            Time.timeScale = 1f;
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(cast.BubbleVisible, Is.False);
        }
    }
}
