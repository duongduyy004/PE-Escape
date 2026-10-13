using System;
using System.Linq;
using KMA.Gameplay.Chess;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessPuzzleGeneratorTests
    {
        // Node-only limits: wall-clock must not change the outcome of a seeded run.
        static readonly ChessGeneratorLimits Generous = new ChessGeneratorLimits(2_000_000, 300_000, 5_000, 1e9);

        [TestCase(ChessLevel.Easy)]
        [TestCase(ChessLevel.Normal)]
        [TestCase(ChessLevel.Hard)]
        public void RandomPositionsAreLegalSetups(ChessLevel level)
        {
            var rng = new Random(11);
            int made = 0;
            for (int i = 0; i < 300; i++)
            {
                ChessPosition p = ChessPuzzleGenerator.RandomPosition(level, rng);
                if (p == null) continue;
                made++;
                int wk = p.KingSquare(PieceColor.White), bk = p.KingSquare(PieceColor.Black);
                Assert.That(Math.Abs(Square.File(wk) - Square.File(bk)) > 1 || Math.Abs(Square.Rank(wk) - Square.Rank(bk)) > 1,
                    "kings adjacent: " + p.ToFen());
                Assert.That(MoveGenerator.IsSquareAttacked(p, bk, PieceColor.White), Is.False, p.ToFen());
                Assert.That(MoveGenerator.LegalMoves(p).Count, Is.GreaterThan(0), p.ToFen());
                Assert.That(p.SideToMove, Is.EqualTo(PieceColor.White));
                Assert.That(p.Castling, Is.EqualTo(CastlingRights.None));
                Assert.That(p.EnPassant, Is.EqualTo(-1));
                Assert.That(p.HalfmoveClock, Is.EqualTo(0));
                int pieces = Enumerable.Range(0, 64).Count(s => p[s] != 0);
                Assert.That(pieces, Is.InRange(4, 6), p.ToFen());
                for (int s = 0; s < 64; s++)
                    if (p[s] != 0 && Piece.ColorOf(p[s]) == PieceColor.Black)
                        Assert.That(Piece.TypeOf(p[s]), Is.EqualTo(Piece.King), "black has only a king: " + p.ToFen());
            }
            Assert.That(made, Is.GreaterThan(150), "setup rejection rate is far too high");
        }

        [TestCase(ChessLevel.Easy)]
        [TestCase(ChessLevel.Normal)]
        public void GeneratedPuzzleMatchesItsLevelAndIndependentCheck(ChessLevel level)
        {
            ChessLevelRules rules = ChessLevelRules.For(level);
            for (int seed = 1; seed <= 4; seed++)
            {
                ChessPuzzleSnapshot s = ChessPuzzleGenerator.TryGenerate(level, seed, Generous, null, null, out GenerationStats stats);
                Assert.That(s, Is.Not.Null, $"seed {seed}");
                Assert.That(stats.Success, Is.True);
                Assert.That(s.optimalMoves, Is.EqualTo(rules.OptimalMoves));
                Assert.That(s.maxMoves, Is.EqualTo(rules.MaxMoves));
                Assert.That(s.source, Is.EqualTo(ChessPuzzleSnapshot.GeneratedSource));
                ChessPosition p = ChessPosition.FromFen(s.fen);
                Assert.That(NaiveMate.AttackerDistance(p, rules.OptimalMoves), Is.EqualTo(rules.OptimalMoves),
                    $"mate in exactly N: {s.fen}");
            }
        }

        [Test, Timeout(300000)]
        public void HardPuzzleHasNoShorterMateAndAMateInThree()
        {
            ChessPuzzleSnapshot s = ChessPuzzleGenerator.TryGenerate(ChessLevel.Hard, 3, Generous, null, null, out _);
            Assert.That(s, Is.Not.Null);
            ChessPosition p = ChessPosition.FromFen(s.fen);
            Assert.That(NaiveMate.AttackerDistance(p, 2), Is.EqualTo(NaiveMate.None), "a shorter mate exists: " + s.fen);
            Assert.That(NaiveMate.AttackerDistance(p, 3), Is.EqualTo(3), s.fen);
        }

        [Test]
        public void SnapshotCarriesDerivedMetrics()
        {
            ChessPuzzleSnapshot s = ChessPuzzleGenerator.TryGenerate(ChessLevel.Normal, 5, Generous, null, null, out _);
            Assert.That(s.estimatorVersion, Is.EqualTo(ChessTimeEstimator.Version));
            Assert.That(s.timeSeconds, Is.EqualTo(ChessTimeEstimator.Seconds(s.optimalMoves, s.branching, s.quietMoves)));
            Assert.That(s.timeSeconds, Is.InRange(60, 240));
            Assert.That(s.seed, Is.EqualTo(5));
            Assert.That(s.Level, Is.EqualTo(ChessLevel.Normal));
            Assert.That(s.StartPosition.ToFen(), Is.EqualTo(s.fen));
            Assert.That(s.playerColor, Is.EqualTo("w"));
        }

        [Test]
        public void SameSeedAndNodeBudgetReproducesThePuzzle()
        {
            ChessPuzzleSnapshot a = ChessPuzzleGenerator.TryGenerate(ChessLevel.Normal, 9, Generous, null, null, out _);
            ChessPuzzleSnapshot b = ChessPuzzleGenerator.TryGenerate(ChessLevel.Normal, 9, Generous, null, null, out _);
            Assert.That(b.fen, Is.EqualTo(a.fen));
            Assert.That(b.timeSeconds, Is.EqualTo(a.timeSeconds));
        }

        [Test]
        public void DifferentSeedsGiveDifferentPuzzles()
        {
            var fens = Enumerable.Range(20, 5).Select(seed =>
                ChessPuzzleGenerator.TryGenerate(ChessLevel.Easy, seed, Generous, null, null, out _).fen).Distinct();
            Assert.That(fens.Count(), Is.GreaterThan(1));
        }

        [Test]
        public void AvoidFenIsNotReturned()
        {
            ChessPuzzleSnapshot first = ChessPuzzleGenerator.TryGenerate(ChessLevel.Easy, 4, Generous, null, null, out _);
            ChessPuzzleSnapshot second = ChessPuzzleGenerator.TryGenerate(ChessLevel.Easy, 4, Generous, first.fen, null, out _);
            Assert.That(second, Is.Not.Null);
            Assert.That(second.fen, Is.Not.EqualTo(first.fen));
        }

        [Test]
        public void TinyNodeBudgetReturnsNullInsteadOfAnUnverifiedPuzzle()
        {
            var tiny = new ChessGeneratorLimits(10, 10, 5_000, 1e9);
            ChessPuzzleSnapshot s = ChessPuzzleGenerator.TryGenerate(ChessLevel.Hard, 1, tiny, null, null, out GenerationStats stats);
            Assert.That(s, Is.Null);
            Assert.That(stats.Success, Is.False);
            Assert.That(stats.Nodes, Is.LessThanOrEqualTo(10 + 1));
        }

        [Test]
        public void CancellationStopsGenerationQuickly()
        {
            ChessPuzzleSnapshot s = ChessPuzzleGenerator.TryGenerate(ChessLevel.Hard, 1, Generous, null, () => true, out GenerationStats stats);
            Assert.That(s, Is.Null);
            Assert.That(stats.ElapsedSeconds, Is.LessThan(1.0));
        }

        [Test]
        public void StatsReportCandidatesAndNodes()
        {
            ChessPuzzleGenerator.TryGenerate(ChessLevel.Easy, 2, Generous, null, null, out GenerationStats stats);
            Assert.That(stats.Candidates, Is.GreaterThan(0));
            Assert.That(stats.Nodes, Is.GreaterThan(0));
        }
    }
}
