using System;
using KMA.Gameplay.Chess;
using NUnit.Framework;
using static KMA.Tests.Gameplay.Chess.TestPuzzles;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessSearchEngineTests
    {
        public const string BackRankMateIn1 = "6k1/5ppp/8/8/8/8/8/R3K3 w - - 0 1";
        public const string LadderMateIn2 = "7k/8/8/8/8/8/R7/1R4K1 w - - 0 1";
        public const string NoMateInTwo = "k7/8/8/8/8/8/8/K6R w - - 0 1";
        public const string StalemateTrap = "7k/5K2/8/6Q1/8/8/8/8 w - - 0 1";

        static ChessSearchEngine Engine() => new ChessSearchEngine(new SearchBudget(1_000_000, 60));

        [Test]
        public void LevelRulesMatchTheSpecTable()
        {
            Assert.That(ChessLevelRules.For(ChessLevel.Easy).OptimalMoves, Is.EqualTo(1));
            Assert.That(ChessLevelRules.For(ChessLevel.Easy).MaxMoves, Is.EqualTo(2));
            Assert.That(ChessLevelRules.For(ChessLevel.Normal).OptimalMoves, Is.EqualTo(2));
            Assert.That(ChessLevelRules.For(ChessLevel.Normal).MaxMoves, Is.EqualTo(3));
            Assert.That(ChessLevelRules.For(ChessLevel.Hard).OptimalMoves, Is.EqualTo(3));
            Assert.That(ChessLevelRules.For(ChessLevel.Hard).MaxMoves, Is.EqualTo(5));
        }

        [Test]
        public void FindsMateInOne()
        {
            MateSearch result = Engine().MateDistance(ChessPosition.FromFen(BackRankMateIn1), 3);
            Assert.That(result.Status, Is.EqualTo(SearchStatus.Complete));
            Assert.That(result.Distance, Is.EqualTo(1));
            Assert.That(result.Forced, Is.True);
        }

        [Test]
        public void MateInTwoCountsPlayerMovesNotPlies()
        {
            ChessPosition position = ChessPosition.FromFen(LadderMateIn2);
            Assert.That(Engine().MateDistance(position, 3).Distance, Is.EqualTo(2));
            MateSearch tooShort = Engine().MateDistance(position, 1);
            Assert.That(tooShort.Status, Is.EqualTo(SearchStatus.Complete));
            Assert.That(tooShort.Forced, Is.False);
        }

        [Test]
        public void ReportsNoForcedMateWhenNoneExists()
        {
            MateSearch result = Engine().MateDistance(ChessPosition.FromFen(NoMateInTwo), 2);
            Assert.That(result.Status, Is.EqualTo(SearchStatus.Complete));
            Assert.That(result.Distance, Is.EqualTo(0));
        }

        [Test]
        public void StalemateIsNotMate()
        {
            ChessPosition position = ChessPosition.FromFen(StalemateTrap);
            Assert.That(Engine().MoveDistance(position.Apply(Uci("g5g6")), 1).Forced, Is.False, "Qg6 stalemates");
            Assert.That(Engine().MoveDistance(position.Apply(Uci("g5g7")), 1).Distance, Is.EqualTo(1), "Qg7 mates");
            Assert.That(Engine().MoveDistance(position.Apply(Uci("g5h5")), 1).Distance, Is.EqualTo(1), "Qh5 mates");
        }

        [Test]
        public void AgreesWithTheBruteForceChecker()
        {
            foreach (string fen in new[] { BackRankMateIn1, LadderMateIn2, NoMateInTwo, StalemateTrap })
            {
                ChessPosition position = ChessPosition.FromFen(fen);
                int naive = NaiveMate.AttackerDistance(position, 2);
                MateSearch engine = Engine().MateDistance(position, 2);
                Assert.That(engine.Distance, Is.EqualTo(naive == NaiveMate.None ? 0 : naive), fen);
            }
        }

        [Test]
        public void ExhaustedNodeBudgetIsUnknownNotNoMate()
        {
            var engine = new ChessSearchEngine(new SearchBudget(3, 60));
            MateSearch result = engine.MateDistance(ChessPosition.FromFen(LadderMateIn2), 3);
            Assert.That(result.Status, Is.EqualTo(SearchStatus.Unknown));
            Assert.That(result.Forced, Is.False);
        }

        [Test]
        public void CancellationIsUnknown()
        {
            var engine = new ChessSearchEngine(new SearchBudget(1_000_000, 60, () => true));
            MateSearch result = engine.MateDistance(ChessPosition.FromFen(LadderMateIn2), 3);
            Assert.That(result.Status, Is.EqualTo(SearchStatus.Unknown));
        }

        [Test]
        public void CacheSurvivesBudgetSwapAndIsReusedWithoutNodes()
        {
            var engine = new ChessSearchEngine(new SearchBudget(1_000_000, 60));
            ChessPosition position = ChessPosition.FromFen(LadderMateIn2);
            Assert.That(engine.MateDistance(position, 3).Distance, Is.EqualTo(2));

            var tiny = new SearchBudget(1, 60);
            engine.UseBudget(tiny);
            Assert.That(engine.MateDistance(position, 3).Distance, Is.EqualTo(2));
            Assert.That(tiny.Stopped, Is.EqualTo(SearchStop.None));
        }

        [Test]
        public void PositionKeyIgnoresMoveCounters()
        {
            string a = ChessSearchEngine.PositionKey(ChessPosition.FromFen("6k1/8/8/8/8/8/8/R3K3 w - - 0 1"));
            string b = ChessSearchEngine.PositionKey(ChessPosition.FromFen("6k1/8/8/8/8/8/8/R3K3 w - - 7 40"));
            Assert.That(a, Is.EqualTo(b));
        }
    }
}
