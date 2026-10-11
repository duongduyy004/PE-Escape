using System.Collections;
using System.Linq;
using KMA.Gameplay;
using KMA.Gameplay.Chess;
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
        MinigameResult result;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("MG_ChessFinal", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindFirstObjectByType<ChessFinalController>();
            board = Object.FindFirstObjectByType<ChessBoardView>();
            var opening=Object.FindFirstObjectByType<KMA.Gameplay.UI.GameplayOpeningFlow>();
            if(opening!=null)yield return new WaitUntil(()=>opening.IsComplete);
            result = null;
            controller.Completed += r => result = r;
        }

        [UnityTearDown]
        public IEnumerator ResetTime()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator BoardIsLockedAndClockStillUntilStart()
        {
            Assert.That(controller.Machine.Phase, Is.EqualTo(ChessFinalPhase.Intro));
            Assert.That(board.Interactable, Is.False);
            yield return new WaitForSeconds(.3f);
            Assert.That(controller.Machine.Clock.Elapsed, Is.EqualTo(0f));
            controller.BeginAttempt();
            yield return null;
            yield return null;
            Assert.That(board.Interactable, Is.True);
            Assert.That(controller.Machine.Clock.Running, Is.True);
        }

        [UnityTest]
        public IEnumerator PlayingTheMainLineSolvesAndReportsMetrics()
        {
            controller.BeginAttempt();
            yield return null;
            yield return null;
            PuzzleDefinition puzzle = controller.Machine.Puzzle;
            yield return Play(puzzle.nodes[0].moves[0].uci);
            if (controller.Machine.Phase != ChessFinalPhase.Completed)
            {
                Assert.That(board.Interactable, Is.False, "board locked during the boss turn");
                yield return new WaitUntil(() => controller.Machine.Phase == ChessFinalPhase.PlayerTurn);
                yield return Play(puzzle.nodes[controller.Machine.Node].moves[0].uci);
            }
            yield return new WaitUntil(() => result != null);
            Assert.That(result.Pass, Is.True);
            Assert.That(result.Score, Is.GreaterThanOrEqualTo(6f));
            ChallengeMetrics metrics = controller.BuildMetrics(null, result);
            Assert.That(metrics.Mistakes, Is.EqualTo(0));
            Assert.That(metrics.HintUsed, Is.False);
            Assert.That(metrics.Detail, Does.StartWith("Thời gian"));
        }

        [UnityTest]
        public IEnumerator ThreeWrongMovesFailAndPauseFreezesTheClock()
        {
            controller.BeginAttempt();
            yield return null;
            yield return null;
            ChessPosition start = controller.Machine.Position;
            string[] tree = controller.Machine.Puzzle.nodes[0].moves.Select(m => m.uci).ToArray();
            ChessMove wrong = MoveGenerator.LegalMoves(start).First(m => !tree.Contains(m.ToUci()) &&
                !MoveGenerator.IsCheckmate(start.Apply(m)) && m.Promotion == 0);

            Time.timeScale = 0f;
            yield return null;
            float frozen = controller.Machine.Clock.Elapsed;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(controller.Machine.Clock.Elapsed, Is.EqualTo(frozen));
            Assert.That(controller.Machine.Phase, Is.EqualTo(ChessFinalPhase.Paused));
            Time.timeScale = 1f;
            yield return null;

            for (int i = 0; i < 3; i++)
            {
                yield return Play(wrong.ToUci());
                Assert.That(controller.Machine.Position.ToFen(), Is.EqualTo(start.ToFen()));
            }
            yield return new WaitUntil(() => result != null);
            Assert.That(result.Pass, Is.False);
            Assert.That(controller.BuildMetrics(null, result).Detail, Is.EqualTo("Sai quá 2 lần"));
        }

        [UnityTest]
        public IEnumerator PauseDuringTheBossSlideStillPlaysTheBossMoveOnResume()
        {
            controller.BeginAttempt();
            yield return null;
            yield return null;
            PuzzleDefinition puzzle = controller.Machine.Puzzle;
            yield return Play(puzzle.nodes[0].moves[0].uci);
            Assert.That(controller.Machine.Phase, Is.EqualTo(ChessFinalPhase.BossTurn));
            ChessMove reply = controller.Machine.PendingBossReply.Value;
            ChessPosition beforeReply = controller.Machine.Position;
            yield return new WaitUntil(() => board.transform.Find("MovingPiece") != null);

            // Pause lands while the slide is still running. The controller is disabled so its Update
            // cannot auto-resume; its coroutines keep running, so the slide finishes while paused.
            controller.enabled = false;
            controller.Machine.Pause();
            yield return new WaitForSeconds(1.5f);
            Assert.That(controller.Machine.Phase, Is.EqualTo(ChessFinalPhase.Paused));
            Assert.That(controller.Machine.Position.ToFen(), Is.EqualTo(beforeReply.ToFen()),
                "the boss move is not applied while paused");

            controller.Machine.Resume();
            controller.enabled = true;
            float until = Time.realtimeSinceStartup + 1f;
            yield return new WaitUntil(() => controller.Machine.Phase == ChessFinalPhase.PlayerTurn ||
                Time.realtimeSinceStartup > until);
            Assert.That(controller.Machine.Phase, Is.EqualTo(ChessFinalPhase.PlayerTurn));
            Assert.That(controller.Machine.Position.ToFen(), Is.EqualTo(beforeReply.Apply(reply).ToFen()));
            Assert.That(board.Interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator PosesFollowTheTurnAndAMistakeDoesNotCutTheWhistle()
        {
            var cast = Object.FindFirstObjectByType<ChessCastView>();
            controller.BeginAttempt();
            yield return null;
            Assert.That(cast.TeacherPose, Is.EqualTo("strictLook"));
            ChessPosition start = controller.Machine.Position;
            string[] tree = controller.Machine.Puzzle.nodes[0].moves.Select(m => m.uci).ToArray();
            ChessMove wrong = MoveGenerator.LegalMoves(start).First(m => !tree.Contains(m.ToUci()) &&
                !MoveGenerator.IsCheckmate(start.Apply(m)) && m.Promotion == 0);
            yield return Play(wrong.ToUci());
            Assert.That(controller.Machine.Phase, Is.EqualTo(ChessFinalPhase.PlayerTurn));
            Assert.That(cast.StudentPose, Is.EqualTo("hurt"));
            Assert.That(cast.TeacherSequenceRunning, Is.True, "the whistle sequence is still playing");
            yield return new WaitForSeconds(.9f);
            Assert.That(cast.TeacherPose, Is.EqualTo("strictLook"));
            yield return Play(tree[0]);
            Assert.That(cast.StudentPose, Is.EqualTo("idle"));
        }

        [UnityTest]
        public IEnumerator TheTeacherBubbleDoesNotExpireWhileTheGameIsFrozen()
        {
            var cast = Object.FindFirstObjectByType<ChessCastView>();
            cast.Say("Đọc đề nhé.", .3f);
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(cast.BubbleVisible, Is.True, "the guide or pause menu is still open");
            Time.timeScale = 1f;
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(cast.BubbleVisible, Is.False);
        }

        IEnumerator Play(string uci)
        {
            ChessMove.TryParseUci(uci, out ChessMove move);
            board.ClickSquare(move.From);
            board.ClickSquare(move.To);
            yield return null;
        }
    }
}
