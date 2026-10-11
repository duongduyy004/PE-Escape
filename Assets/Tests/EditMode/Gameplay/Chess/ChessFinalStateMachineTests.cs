using System.Collections.Generic;
using KMA.Gameplay.Chess;
using NUnit.Framework;
using static KMA.Tests.Gameplay.Chess.TestPuzzles;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessFinalStateMachineTests
    {
        ChessFinalStateMachine machine;
        readonly List<bool> finished = new List<bool>();

        [SetUp]
        public void SetUp()
        {
            machine = new ChessFinalStateMachine(ScholarTwoMover());
            finished.Clear();
            machine.Finished += solved => finished.Add(solved);
        }

        [Test]
        public void IntroIgnoresMovesAndTheClock()
        {
            Assert.That(machine.Phase, Is.EqualTo(ChessFinalPhase.Intro));
            Assert.That(machine.Submit(Uci("d1h5")), Is.Null);
            machine.Tick(200f);
            Assert.That(machine.Clock.Elapsed, Is.EqualTo(0f));
            machine.Begin();
            Assert.That(machine.Phase, Is.EqualTo(ChessFinalPhase.PlayerTurn));
            Assert.That(machine.Clock.Running, Is.True);
        }

        [Test]
        public void AcceptedMoveHandsTheTurnToTheBossWithTheClockStopped()
        {
            machine.Begin();
            machine.Tick(5f);
            Assert.That(machine.Submit(Uci("d1h5")), Is.EqualTo(GradeKind.Accepted));
            Assert.That(machine.Phase, Is.EqualTo(ChessFinalPhase.BossTurn));
            Assert.That(machine.PlayerMovesMade, Is.EqualTo(1));
            Assert.That(machine.PendingBossReply.Value.ToUci(), Is.EqualTo("g8f6"));
            Assert.That(machine.Submit(Uci("h5f7")), Is.Null, "no move while the boss is to play");
            machine.Tick(30f);
            Assert.That(machine.Clock.Elapsed, Is.EqualTo(5f));

            Assert.That(machine.CompleteBossMove(), Is.True);
            Assert.That(machine.CompleteBossMove(), Is.False);
            Assert.That(machine.Phase, Is.EqualTo(ChessFinalPhase.PlayerTurn));
            Assert.That(machine.Position.ToFen(), Is.EqualTo(ScholarMateInOne));
            Assert.That(machine.Submit(Uci("h5f7")), Is.EqualTo(GradeKind.Solved));
            Assert.That(machine.Phase, Is.EqualTo(ChessFinalPhase.Completed));
            Assert.That(finished, Is.EqualTo(new[] { true }));
        }

        [Test]
        public void WrongMovesRollBackAndTheThirdFails()
        {
            machine.Begin();
            string before = machine.Position.ToFen();
            Assert.That(machine.Submit(Uci("d1f3")), Is.EqualTo(GradeKind.Wrong));
            Assert.That(machine.Position.ToFen(), Is.EqualTo(before));
            Assert.That(machine.Mistakes, Is.EqualTo(1));
            Assert.That(machine.Phase, Is.EqualTo(ChessFinalPhase.PlayerTurn));
            Assert.That(machine.PlayerMovesMade, Is.EqualTo(0));
            machine.Submit(Uci("d1f3"));
            Assert.That(machine.Phase, Is.EqualTo(ChessFinalPhase.PlayerTurn));
            machine.Submit(Uci("d1f3"));
            Assert.That(machine.Phase, Is.EqualTo(ChessFinalPhase.Failed));
            Assert.That(machine.FailReason, Is.EqualTo(ChessFailReason.TooManyMistakes));
            Assert.That(machine.Submit(Uci("d1h5")), Is.Null);
            Assert.That(finished, Is.EqualTo(new[] { false }));
        }

        [Test]
        public void WrongSecondMoveKeepsTheCorrectFirstMove()
        {
            machine.Begin();
            machine.Submit(Uci("d1h5"));
            machine.CompleteBossMove();
            Assert.That(machine.Submit(Uci("h5h7")), Is.EqualTo(GradeKind.Wrong));
            Assert.That(machine.Position.ToFen(), Is.EqualTo(ScholarMateInOne));
            Assert.That(machine.PlayerMovesMade, Is.EqualTo(1));
        }

        [Test]
        public void IllegalMoveCostsNothing()
        {
            machine.Begin();
            Assert.That(machine.Submit(Uci("e1e3")), Is.EqualTo(GradeKind.Illegal));
            Assert.That(machine.Mistakes, Is.EqualTo(0));
            Assert.That(machine.Phase, Is.EqualTo(ChessFinalPhase.PlayerTurn));
        }

        [Test]
        public void TimeRunsOutOnlyOnThePlayersTurn()
        {
            machine.Begin();
            machine.Tick(89f);
            machine.Pause();
            machine.Tick(60f);
            Assert.That(machine.Phase, Is.EqualTo(ChessFinalPhase.Paused));
            machine.Resume();
            machine.Tick(1f);
            Assert.That(machine.Phase, Is.EqualTo(ChessFinalPhase.Failed));
            Assert.That(machine.FailReason, Is.EqualTo(ChessFailReason.TimeUp));
            machine.Tick(1f);
            Assert.That(finished, Is.EqualTo(new[] { false }));
        }

        [Test]
        public void PauseDuringTheBossTurnResumesTheBossTurn()
        {
            machine.Begin();
            machine.Submit(Uci("d1h5"));
            machine.Pause();
            machine.Resume();
            Assert.That(machine.Phase, Is.EqualTo(ChessFinalPhase.BossTurn));
            Assert.That(machine.Clock.Running, Is.False);
        }

        [Test]
        public void HintsOpenInOrderAndRestartResetsEverything()
        {
            machine.Begin();
            Assert.That(machine.RevealNextHint(), Is.EqualTo("Nhắm vào ô f7."));
            machine.RevealNextHint();
            Assert.That(machine.RevealNextHint(), Is.EqualTo("Đi Hậu từ d1 đến h5. Tôi đã chỉ rõ rồi."));
            Assert.That(machine.HintUsed, Is.True);
            machine.Submit(Uci("d1f3"));
            machine.Tick(20f);

            machine.Restart();
            Assert.That(machine.Phase, Is.EqualTo(ChessFinalPhase.Intro));
            Assert.That(machine.Mistakes, Is.EqualTo(0));
            Assert.That(machine.HintLevel, Is.EqualTo(0));
            Assert.That(machine.Clock.Remaining, Is.EqualTo(90f));
            Assert.That(machine.Position.ToFen(), Is.EqualTo(ScholarStart));
            Assert.That(machine.FailReason, Is.EqualTo(ChessFailReason.None));
        }

        [Test]
        public void HintLevelResetsWhenTheNodeChangesButHintUsedSticks()
        {
            machine.Begin();
            machine.RevealNextHint();
            machine.RevealNextHint();
            machine.RevealNextHint();
            Assert.That(machine.HintLevel, Is.EqualTo(3));
            machine.Submit(Uci("d1h5"));
            Assert.That(machine.CompleteBossMove(), Is.True);
            Assert.That(machine.HintLevel, Is.EqualTo(0));
            Assert.That(machine.HintUsed, Is.True);
            Assert.That(machine.RevealNextHint(), Is.EqualTo(PuzzleHints.For(machine.Puzzle, 1, machine.Position, 1)));
            machine.Restart();
            Assert.That(machine.HintUsed, Is.False);
        }

        [Test]
        public void DataErrorsReturnTheTurnWithoutAMistake()
        {
            PuzzleDefinition broken = ScholarTwoMover();
            broken.nodes[0] = Node(Move("d1f3", true));
            var faulty = new ChessFinalStateMachine(broken);
            string failure = null;
            faulty.GradingFailed += message => failure = message;
            faulty.Begin();
            Assert.That(faulty.Submit(Uci("d1f3")), Is.Null);
            Assert.That(failure, Is.Not.Null);
            Assert.That(faulty.Mistakes, Is.EqualTo(0));
            Assert.That(faulty.Phase, Is.EqualTo(ChessFinalPhase.PlayerTurn));
        }

        [Test]
        public void NonInvalidOperationExceptionErrorsAlsoCaughtAndReturnTurn()
        {
            PuzzleDefinition broken = ScholarTwoMover();
            var faulty = new ChessFinalStateMachine(broken);
            // Corrupt the puzzle after construction (TryValidateShape passed)
            broken.nodes[0].moves = null;
            string failure = null;
            faulty.GradingFailed += message => failure = message;
            faulty.Begin();
            // This should trigger a NullReferenceException (or similar) inside Grade
            Assert.That(faulty.Submit(Uci("d1h5")), Is.Null);
            Assert.That(failure, Is.Not.Null);
            Assert.That(faulty.Mistakes, Is.EqualTo(0));
            Assert.That(faulty.Phase, Is.EqualTo(ChessFinalPhase.PlayerTurn));
            Assert.That(faulty.Clock.Running, Is.True);
        }
    }
}
