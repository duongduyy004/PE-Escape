using System.Linq;
using KMA.Gameplay.Chess;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessFallbackLibraryTests
    {
        [TestCase(ChessLevel.Easy)]
        [TestCase(ChessLevel.Normal)]
        [TestCase(ChessLevel.Hard)]
        public void EveryLevelHasAtLeastSixPuzzlesOfItsOwnLevel(ChessLevel level)
        {
            var own = ChessFallbackLibrary.All().Where(p => p.Level == level).ToList();
            Assert.That(own.Count, Is.GreaterThanOrEqualTo(6));
            Assert.That(own.Select(p => p.fen).Distinct().Count(), Is.EqualTo(own.Count), "duplicate FEN");
            Assert.That(own.All(p => p.source == ChessPuzzleSnapshot.FallbackSource), Is.True);
        }

        [Test]
        public void EveryPuzzleIsReVerifiedByTheEngine()
        {
            foreach (ChessPuzzleSnapshot p in ChessFallbackLibrary.All())
            {
                ChessLevelRules rules = ChessLevelRules.For(p.Level);
                var engine = new ChessSearchEngine(new SearchBudget(2_000_000, 120));
                PuzzleAnalysis analysis = ChessPuzzleAnalyzer.Analyze(engine, p.StartPosition, rules.OptimalMoves);
                Assert.That(analysis.Status, Is.EqualTo(SearchStatus.Complete), p.fen);
                Assert.That(analysis.OptimalMoves, Is.EqualTo(rules.OptimalMoves), p.fen);
                Assert.That(p.optimalMoves, Is.EqualTo(rules.OptimalMoves), p.fen);
                Assert.That(p.maxMoves, Is.EqualTo(rules.MaxMoves), p.fen);
                Assert.That(p.timeSeconds, Is.EqualTo(ChessTimeEstimator.Seconds(p.optimalMoves, p.branching, p.quietMoves)), p.fen);
                Assert.That(p.estimatorVersion, Is.EqualTo(ChessTimeEstimator.Version), p.fen);
            }
        }

        [Test]
        public void EasyAndNormalPuzzlesPassTheIndependentChecker()
        {
            foreach (ChessPuzzleSnapshot p in ChessFallbackLibrary.All().Where(p => p.Level != ChessLevel.Hard))
                Assert.That(NaiveMate.AttackerDistance(p.StartPosition, p.optimalMoves), Is.EqualTo(p.optimalMoves), p.fen);
        }

        [Test, Timeout(300000)]
        public void TwoHardPuzzlesPassTheIndependentChecker()
        {
            foreach (ChessPuzzleSnapshot p in ChessFallbackLibrary.All().Where(p => p.Level == ChessLevel.Hard).Take(2))
            {
                Assert.That(NaiveMate.AttackerDistance(p.StartPosition, 2), Is.EqualTo(NaiveMate.None), p.fen);
                Assert.That(NaiveMate.AttackerDistance(p.StartPosition, 3), Is.EqualTo(3), p.fen);
            }
        }

        [Test]
        public void PickNeverCrossesLevelsAndIsSeedStable()
        {
            foreach (ChessLevel level in new[] { ChessLevel.Easy, ChessLevel.Normal, ChessLevel.Hard })
            {
                ChessPuzzleSnapshot a = ChessFallbackLibrary.Pick(level, 7, null);
                ChessPuzzleSnapshot b = ChessFallbackLibrary.Pick(level, 7, null);
                Assert.That(a.Level, Is.EqualTo(level));
                Assert.That(b.fen, Is.EqualTo(a.fen));
            }
        }

        [Test]
        public void PickAvoidsTheLastFenWhenAnotherExists()
        {
            ChessPuzzleSnapshot first = ChessFallbackLibrary.Pick(ChessLevel.Hard, 1, null);
            for (int seed = 0; seed < 20; seed++)
                Assert.That(ChessFallbackLibrary.Pick(ChessLevel.Hard, seed, first.fen).fen, Is.Not.EqualTo(first.fen));
        }
    }
}
