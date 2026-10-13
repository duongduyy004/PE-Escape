using System;
using KMA.Gameplay.Chess;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessPuzzleAnalyzerTests
    {
        static ChessSearchEngine Engine() => new ChessSearchEngine(new SearchBudget(1_000_000, 60));

        [Test]
        public void MateInOneHasOneProofStateAndNoQuietMoves()
        {
            // 10 rook moves + 5 king moves = 15 legal moves; the mating move leaves only a terminal state.
            PuzzleAnalysis result = ChessPuzzleAnalyzer.Analyze(Engine(),
                ChessPosition.FromFen(ChessSearchEngineTests.BackRankMateIn1), 3);
            Assert.That(result.Status, Is.EqualTo(SearchStatus.Complete));
            Assert.That(result.OptimalMoves, Is.EqualTo(1));
            Assert.That(result.QuietMoves, Is.EqualTo(0));
            Assert.That(result.Branching, Is.EqualTo(Math.Log(16, 2)).Within(1e-9));
        }

        [Test]
        public void LadderMateNeedsExactlyOneQuietMove()
        {
            PuzzleAnalysis result = ChessPuzzleAnalyzer.Analyze(Engine(),
                ChessPosition.FromFen(ChessSearchEngineTests.LadderMateIn2), 3);
            Assert.That(result.OptimalMoves, Is.EqualTo(2));
            Assert.That(result.QuietMoves, Is.EqualTo(1));
            Assert.That(result.Branching, Is.GreaterThan(1.0));
        }

        [Test]
        public void NoForcedMateReportsZeroOptimalMoves()
        {
            PuzzleAnalysis result = ChessPuzzleAnalyzer.Analyze(Engine(),
                ChessPosition.FromFen(ChessSearchEngineTests.NoMateInTwo), 2);
            Assert.That(result.Status, Is.EqualTo(SearchStatus.Complete));
            Assert.That(result.OptimalMoves, Is.EqualTo(0));
        }

        [Test]
        public void LimitBelowTheRealDistanceReportsNoMate()
        {
            PuzzleAnalysis result = ChessPuzzleAnalyzer.Analyze(Engine(),
                ChessPosition.FromFen(ChessSearchEngineTests.LadderMateIn2), 1);
            Assert.That(result.OptimalMoves, Is.EqualTo(0));
        }

        [Test]
        public void TinyBudgetIsUnknown()
        {
            var engine = new ChessSearchEngine(new SearchBudget(5, 60));
            PuzzleAnalysis result = ChessPuzzleAnalyzer.Analyze(engine,
                ChessPosition.FromFen(ChessSearchEngineTests.LadderMateIn2), 3);
            Assert.That(result.Status, Is.EqualTo(SearchStatus.Unknown));
        }

        [Test]
        public void AnalysisIsDeterministic()
        {
            ChessPosition position = ChessPosition.FromFen(ChessSearchEngineTests.LadderMateIn2);
            PuzzleAnalysis a = ChessPuzzleAnalyzer.Analyze(Engine(), position, 3);
            PuzzleAnalysis b = ChessPuzzleAnalyzer.Analyze(Engine(), position, 3);
            Assert.That(b.Branching, Is.EqualTo(a.Branching));
            Assert.That(b.QuietMoves, Is.EqualTo(a.QuietMoves));
        }
    }
}
