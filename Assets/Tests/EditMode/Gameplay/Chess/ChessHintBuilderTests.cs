using KMA.Gameplay.Chess;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessHintBuilderTests
    {
        static ChessSearchEngine Engine(int nodes = 2_000_000) => new ChessSearchEngine(new SearchBudget(nodes, 60));
        static ChessHint Hint(string fen, int movesLeft, int level, ChessSearchEngine engine = null) =>
            ChessHintBuilder.Build(engine ?? Engine(), new HintRequest { Id = 4, Fen = fen, MovesLeft = movesLeft, Level = level });

        [Test]
        public void LevelOneDescribesTheIdeaWithoutNamingAMove()
        {
            ChessHint h = Hint(ChessSearchEngineTests.LadderMateIn2, 3, 1);
            Assert.That(h.Status, Is.EqualTo(SearchStatus.Complete));
            Assert.That(h.MateExists, Is.True);
            Assert.That(h.NamesMove, Is.False);
            Assert.That(h.Text, Is.Not.Empty);
            Assert.That(h.Text, Does.Not.Contain("b1"));
            Assert.That(h.Id, Is.EqualTo(4));
        }

        [Test]
        public void LevelTwoNamesThePieceAndSquareOfAnOptimalMove()
        {
            ChessHint h = Hint(ChessSearchEngineTests.LadderMateIn2, 3, 2);
            Assert.That(h.NamesMove, Is.False);
            Assert.That(h.Text, Does.Contain("Xe"));
            Assert.That(new[] { "b1", "a2" }, Does.Contain(Square.Name(h.Move.From)));
            Assert.That(h.Text, Does.Contain(Square.Name(h.Move.From)));
        }

        [Test]
        public void LevelThreeNamesAnOptimalMoveAndFlagsIt()
        {
            ChessHint h = Hint(ChessSearchEngineTests.LadderMateIn2, 3, 3);
            Assert.That(h.NamesMove, Is.True);
            var engine = Engine();
            ChessPosition position = ChessPosition.FromFen(ChessSearchEngineTests.LadderMateIn2);
            Assert.That(engine.MoveDistance(position.Apply(h.Move), 3).Distance, Is.EqualTo(2), "must be an optimal move");
            Assert.That(h.Text, Does.Contain(Square.Name(h.Move.From)).And.Contain(Square.Name(h.Move.To)));
        }

        [Test]
        public void HintIsDeterministic()
        {
            Assert.That(Hint(ChessSearchEngineTests.LadderMateIn2, 3, 3).Move,
                Is.EqualTo(Hint(ChessSearchEngineTests.LadderMateIn2, 3, 3).Move));
        }

        [Test]
        public void NoForcedMateIsStatedHonestlyAndLevelThreeStillNamesALegalNonDrawingMove()
        {
            const string fen = ChessSearchEngineTests.NoMateInTwo;
            ChessHint one = Hint(fen, 2, 1);
            Assert.That(one.MateExists, Is.False);
            Assert.That(one.Text, Does.Contain("không còn").IgnoreCase);

            ChessHint three = Hint(fen, 2, 3);
            Assert.That(three.NamesMove, Is.True);
            ChessPosition position = ChessPosition.FromFen(fen);
            Assert.That(MoveGenerator.LegalMoves(position).Contains(three.Move), Is.True);
            Assert.That(ChessDrawRules.Evaluate(position.Apply(three.Move), 1), Is.EqualTo(DrawReason.None));
        }

        [Test]
        public void StalematingMoveIsNeverSuggestedWhenThereIsNoMate()
        {
            // Qg6 stalemates here; it must not be offered as the "safe" waiting move.
            ChessHint h = Hint("7k/5K2/8/6Q1/8/8/8/8 w - - 0 1", 1, 3);
            Assert.That(h.MateExists, Is.True, "this position does have mate in one");
            Assert.That(h.Move.ToUci(), Is.Not.EqualTo("g5g6"));
        }

        [Test]
        public void UnknownWhenTheBudgetRunsOut()
        {
            ChessHint h = Hint(ChessSearchEngineTests.LadderMateIn2, 3, 3, Engine(2));
            Assert.That(h.Status, Is.EqualTo(SearchStatus.Unknown));
            Assert.That(h.Text, Is.Null.Or.Empty);
        }

        [Test, Timeout(300000)]
        public void HardHintsFinishWithinTheDefaultBudgetAtFullMovesLeft()
        {
            for (int seed = 1; seed <= 12; seed++)
            {
                ChessPuzzleSnapshot puzzle = ChessPuzzleGenerator.TryGenerate(ChessLevel.Hard, seed, ChessGeneratorLimits.Default, null, null, out _);
                Assert.That(puzzle, Is.Not.Null, "seed " + seed);
                ChessHint hint = ChessHintBuilder.Build(new ChessSearchEngine(SearchBudget.Default()),
                    new HintRequest { Id = 1, Fen = puzzle.fen, MovesLeft = puzzle.maxMoves, Level = 3 });
                Assert.That(hint.Status, Is.EqualTo(SearchStatus.Complete), "seed " + seed + " " + puzzle.fen);
                ChessPosition position = ChessPosition.FromFen(puzzle.fen);
                Assert.That(MoveGenerator.LegalMoves(position).Contains(hint.Move), Is.True, "seed " + seed);
                Assert.That(Engine().MoveDistance(position.Apply(hint.Move), puzzle.maxMoves).Distance, Is.EqualTo(puzzle.optimalMoves),
                    "the hinted move must be an optimal one, seed " + seed);
            }
        }

        [TestCase(Piece.Pawn, "Tốt")]
        [TestCase(Piece.Knight, "Mã")]
        [TestCase(Piece.Bishop, "Tượng")]
        [TestCase(Piece.Rook, "Xe")]
        [TestCase(Piece.Queen, "Hậu")]
        [TestCase(Piece.King, "Vua")]
        public void PieceNamesAreVietnamese(int type, string name) =>
            Assert.That(ChessHintBuilder.PieceName(type), Is.EqualTo(name));
    }
}
