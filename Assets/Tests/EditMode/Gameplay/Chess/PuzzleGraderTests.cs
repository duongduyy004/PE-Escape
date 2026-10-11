using System;
using KMA.Gameplay.Chess;
using NUnit.Framework;
using static KMA.Tests.Gameplay.Chess.TestPuzzles;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class PuzzleGraderTests
    {
        [Test]
        public void TreeMoveIsAcceptedAndTheBossPlaysTheFirstReply()
        {
            PuzzleDefinition puzzle = ScholarTwoMover();
            var grader = new PuzzleGrader(puzzle);
            ChessPosition start = ChessPosition.FromFen(puzzle.startFen);

            GradeResult result = grader.Grade(start, 0, 0, Uci("d1h5"));

            Assert.That(result.Kind, Is.EqualTo(GradeKind.Accepted));
            Assert.That(result.BossReply.Value.ToUci(), Is.EqualTo("g8f6"));
            Assert.That(result.NextNode, Is.EqualTo(1));
            Assert.That(result.After.SideToMove, Is.EqualTo(PieceColor.Black));
        }

        [Test]
        public void LegalMoveOutsideTheTreeIsWrong()
        {
            PuzzleDefinition puzzle = ScholarTwoMover();
            GradeResult result = new PuzzleGrader(puzzle).Grade(ChessPosition.FromFen(puzzle.startFen), 0, 0, Uci("d1f3"));
            Assert.That(result.Kind, Is.EqualTo(GradeKind.Wrong));
        }

        [Test]
        public void IllegalMoveIsIllegal()
        {
            PuzzleDefinition puzzle = ScholarTwoMover();
            GradeResult result = new PuzzleGrader(puzzle).Grade(ChessPosition.FromFen(puzzle.startFen), 0, 0, Uci("e1e3"));
            Assert.That(result.Kind, Is.EqualTo(GradeKind.Illegal));
        }

        [Test]
        public void MateIsSolvedEvenWhenTheTreeListsAnotherMove()
        {
            PuzzleDefinition puzzle = ScholarTwoMover();
            puzzle.nodes[1] = Node(Move("h5e5", false));
            GradeResult result = new PuzzleGrader(puzzle)
                .Grade(ChessPosition.FromFen(ScholarMateInOne), 1, 1, Uci("h5f7"));
            Assert.That(result.Kind, Is.EqualTo(GradeKind.Solved));
        }

        [Test]
        public void PromotionPieceIsPartOfTheMove()
        {
            var puzzle = new PuzzleDefinition
            {
                startFen = "7k/P7/8/8/8/8/8/K7 w - - 0 1", playerColor = "w", maxPlayerMoves = 2,
                timeLimitSeconds = 90f, maxRecoverableMistakes = 2,
                nodes = new[] { Node(Move("a7a8q", false, Reply("h8g7", 0))) }
            };
            var grader = new PuzzleGrader(puzzle);
            ChessPosition start = ChessPosition.FromFen(puzzle.startFen);
            Assert.That(grader.Grade(start, 0, 0, Uci("a7a8n")).Kind, Is.EqualTo(GradeKind.Wrong));
            Assert.That(grader.Grade(start, 0, 0, Uci("a7a8q")).Kind, Is.EqualTo(GradeKind.Accepted));
        }

        [Test]
        public void InconsistentDataThrowsInsteadOfDeclaringAWin()
        {
            PuzzleDefinition puzzle = ScholarTwoMover();
            puzzle.nodes[1] = Node(Move("h5e5", true)); // flagged mate, but it is not
            Assert.Throws<InvalidOperationException>(() => new PuzzleGrader(puzzle)
                .Grade(ChessPosition.FromFen(ScholarMateInOne), 1, 1, Uci("h5e5")));

            PuzzleDefinition lastMove = ScholarTwoMover();
            lastMove.nodes[1] = Node(Move("h5e5", false, Reply("e8e7", 1)));
            Assert.Throws<InvalidOperationException>(() => new PuzzleGrader(lastMove)
                .Grade(ChessPosition.FromFen(ScholarMateInOne), 1, 1, Uci("h5e5")));
        }

        [Test]
        public void HintsRevealIdeaThenPieceThenMove()
        {
            PuzzleDefinition puzzle = ScholarTwoMover();
            ChessPosition start = ChessPosition.FromFen(puzzle.startFen);
            Assert.That(PuzzleHints.For(puzzle, 0, start, 1), Is.EqualTo("Nhắm vào ô f7."));
            Assert.That(PuzzleHints.For(puzzle, 0, start, 2), Is.EqualTo("Xem lại quân Hậu ở d1."));
            Assert.That(PuzzleHints.For(puzzle, 0, start, 3), Is.EqualTo("Đi Hậu từ d1 đến h5. Tôi đã chỉ rõ rồi."));
        }
    }
}
