using System;
using System.Collections.Generic;
using KMA.Gameplay.Chess;
using NUnit.Framework;
using static KMA.Tests.Gameplay.Chess.TestPuzzles;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessAttemptGraderTests
    {
        static ChessSearchEngine Engine() => new ChessSearchEngine(new SearchBudget(2_000_000, 60));

        [Test]
        public void EveryOptimalMoveScoresOne()
        {
            ChessPosition position = ChessPosition.FromFen(ChessSearchEngineTests.LadderMateIn2);
            foreach (string uci in new[] { "b1b7", "a2a7" })
            {
                MoveQuality q = ChessAttemptGrader.Evaluate(Engine(), position, Uci(uci), 3);
                Assert.That(q.Status, Is.EqualTo(SearchStatus.Complete), uci);
                Assert.That(q.Q, Is.EqualTo(1.0).Within(1e-9), uci);
            }
        }

        [Test]
        public void MoveQualityMatchesTheBruteForceChecker()
        {
            ChessPosition position = ChessPosition.FromFen(ChessSearchEngineTests.LadderMateIn2);
            int best = NaiveMate.AttackerDistance(position, 2);
            foreach (ChessMove move in MoveGenerator.LegalMoves(position))
            {
                int chosen = NaiveMate.DefenderDistance(position.Apply(move), 2);
                double expected = chosen == NaiveMate.None ? 0.0 : (double)best / chosen;
                MoveQuality q = ChessAttemptGrader.Evaluate(Engine(), position, move, 2);
                Assert.That(q.Q, Is.EqualTo(expected).Within(1e-9), move.ToUci());
            }
        }

        [Test]
        public void SlowerMateGetsAFraction()
        {
            // With three moves left the ladder still mates in 2; a waiting king move that keeps a mate in 3 scores 2/3.
            ChessPosition position = ChessPosition.FromFen(ChessSearchEngineTests.LadderMateIn2);
            ChessMove waiting = Uci("g1f1");
            int chosen = NaiveMate.DefenderDistance(position.Apply(waiting), 3);
            double expected = chosen == NaiveMate.None ? 0.0 : 2.0 / chosen;
            Assert.That(ChessAttemptGrader.Evaluate(Engine(), position, waiting, 3).Q, Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        public void StalematingMoveScoresZeroAndMatingMovesScoreOne()
        {
            ChessPosition position = ChessPosition.FromFen(ChessSearchEngineTests.StalemateTrap);
            Assert.That(ChessAttemptGrader.Evaluate(Engine(), position, Uci("g5g6"), 2).Q, Is.EqualTo(0.0));
            Assert.That(ChessAttemptGrader.Evaluate(Engine(), position, Uci("g5g7"), 2).Q, Is.EqualTo(1.0));
        }

        [Test]
        public void NoForcedMateLeftScoresZeroButIsComplete()
        {
            ChessPosition position = ChessPosition.FromFen(ChessSearchEngineTests.NoMateInTwo);
            MoveQuality q = ChessAttemptGrader.Evaluate(Engine(), position, Uci("h1h8"), 2);
            Assert.That(q.Status, Is.EqualTo(SearchStatus.Complete));
            Assert.That(q.Q, Is.EqualTo(0.0));
            Assert.That(q.BestDistance, Is.EqualTo(0));
        }

        [Test]
        public void UnknownIsNeverReportedAsZero()
        {
            var engine = new ChessSearchEngine(new SearchBudget(2, 60));
            MoveQuality q = ChessAttemptGrader.Evaluate(engine,
                ChessPosition.FromFen(ChessSearchEngineTests.LadderMateIn2), Uci("b1b7"), 3);
            Assert.That(q.Unknown, Is.True);
            Assert.That(q.Status, Is.EqualTo(SearchStatus.Unknown));
        }

        [TestCase(100, ChessGrade.APlus)]
        [TestCase(90, ChessGrade.APlus)]
        [TestCase(89, ChessGrade.A)]
        [TestCase(80, ChessGrade.A)]
        [TestCase(79, ChessGrade.B)]
        [TestCase(70, ChessGrade.B)]
        [TestCase(69, ChessGrade.C)]
        [TestCase(60, ChessGrade.C)]
        [TestCase(59, ChessGrade.D)]
        [TestCase(50, ChessGrade.D)]
        [TestCase(49, ChessGrade.F)]
        [TestCase(0, ChessGrade.F)]
        public void GradeBoundaries(int tenths, ChessGrade expected) =>
            Assert.That(ChessAttemptGrader.GradeFromTenths(tenths), Is.EqualTo(expected));

        [Test]
        public void LabelsUseAPlus()
        {
            Assert.That(ChessAttemptGrader.Label(ChessGrade.APlus), Is.EqualTo("A+"));
            Assert.That(ChessAttemptGrader.Label(ChessGrade.F), Is.EqualTo("F"));
        }

        [Test]
        public void PerfectPlayInOptimalMovesIsTenAndAPlus()
        {
            ChessScore s = ChessAttemptGrader.Score(true, new[] { 1.0, 1.0 }, 2, 2, false);
            Assert.That(s.Pass, Is.True);
            Assert.That(s.Score, Is.EqualTo(10f));
            Assert.That(s.Grade, Is.EqualTo(ChessGrade.APlus));
        }

        [Test]
        public void ExtraMovesLowerEfficiencyOnlyThirtyPercent()
        {
            ChessScore s = ChessAttemptGrader.Score(true, new[] { 1.0, 1.0, 1.0, 1.0 }, 2, 4, false);
            Assert.That(s.Score, Is.EqualTo(8.5f)); // 10 * (0.7*1 + 0.3*0.5)
            Assert.That(s.Grade, Is.EqualTo(ChessGrade.A));
        }

        [Test]
        public void FloatNoiseAtNineStillMakesAPlus()
        {
            // N=2, U=3 gives efficiency 0.6667 and exactly 9.0 mathematically.
            ChessScore s = ChessAttemptGrader.Score(true, new[] { 1.0, 1.0, 1.0 }, 2, 3, false);
            Assert.That(s.Score, Is.EqualTo(9.0f));
            Assert.That(s.Grade, Is.EqualTo(ChessGrade.APlus));
        }

        [Test]
        public void ExplicitHintCapsAtEightPointNine()
        {
            ChessScore s = ChessAttemptGrader.Score(true, new[] { 1.0, 1.0 }, 2, 2, true);
            Assert.That(s.Score, Is.EqualTo(8.9f));
            Assert.That(s.Grade, Is.EqualTo(ChessGrade.A));
            Assert.That(s.HintCapped, Is.True);
        }

        [Test]
        public void HintDoesNotChangeALowerScore()
        {
            ChessScore plain = ChessAttemptGrader.Score(true, new[] { 0.5, 1.0 }, 2, 2, false);
            ChessScore hinted = ChessAttemptGrader.Score(true, new[] { 0.5, 1.0 }, 2, 2, true);
            Assert.That(hinted.Score, Is.EqualTo(plain.Score));
            Assert.That(hinted.HintCapped, Is.False);
        }

        [Test]
        public void FailureIsZeroAndF()
        {
            ChessScore s = ChessAttemptGrader.Score(false, new[] { 1.0, 1.0 }, 2, 2, false);
            Assert.That(s.Pass, Is.False);
            Assert.That(s.Score, Is.EqualTo(0f));
            Assert.That(s.Grade, Is.EqualTo(ChessGrade.F));
        }

        [Test]
        public void OutOfRangeInputsAreClamped()
        {
            ChessScore s = ChessAttemptGrader.Score(true, new[] { 2.0, -1.0 }, 3, 0, false);
            Assert.That(s.Score, Is.InRange(0f, 10f));
            Assert.That(s.Efficiency, Is.EqualTo(0.0));
        }

        [Test]
        public void NoRecordedMovesCountsAsZeroQuality() =>
            Assert.That(ChessAttemptGrader.Score(true, new List<double>(), 1, 1, false).QualityMean, Is.EqualTo(0.0));
    }
}
