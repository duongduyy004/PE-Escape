using KMA.Gameplay.Chess;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessDrawRulesTests
    {
        static ChessPosition P(string fen) => ChessPosition.FromFen(fen);

        [Test]
        public void StalemateIsADrawButCheckmateIsNot()
        {
            Assert.That(ChessDrawRules.Evaluate(P("7k/5K2/6Q1/8/8/8/8/8 b - - 0 1"), 1), Is.EqualTo(DrawReason.Stalemate));
            Assert.That(ChessDrawRules.Evaluate(P("7k/5KQ1/8/8/8/8/8/8 b - - 0 1"), 1), Is.EqualTo(DrawReason.None));
        }

        [TestCase("k7/8/8/8/8/8/8/K7 w - - 0 1", true)]            // K v K
        [TestCase("k7/8/8/8/8/8/8/KN6 w - - 0 1", true)]           // K+N v K
        [TestCase("k7/8/8/8/8/8/8/KB6 w - - 0 1", true)]           // K+B v K
        [TestCase("k7/8/8/8/8/8/8/KB1b4 w - - 0 1", true)]         // bishops on the same colour
        [TestCase("k7/8/8/8/8/8/8/KBb5 w - - 0 1", false)]         // bishops on opposite colours
        [TestCase("k7/8/8/8/8/8/8/KNN5 w - - 0 1", false)]         // two knights can still mate in theory
        [TestCase("k7/8/8/8/8/8/8/KR6 w - - 0 1", false)]
        [TestCase("k7/8/8/8/8/8/P7/K7 w - - 0 1", false)]
        public void InsufficientMaterial(string fen, bool expected)
        {
            Assert.That(ChessDrawRules.IsInsufficientMaterial(P(fen)), Is.EqualTo(expected), fen);
            Assert.That(ChessDrawRules.Evaluate(P(fen), 1) == DrawReason.InsufficientMaterial, Is.EqualTo(expected), fen);
        }

        [Test]
        public void FiftyMoveRuleStartsAtHundredHalfmoves()
        {
            Assert.That(ChessDrawRules.Evaluate(P("7k/8/8/8/8/8/8/R3K3 w - - 99 80"), 1), Is.EqualTo(DrawReason.None));
            Assert.That(ChessDrawRules.Evaluate(P("7k/8/8/8/8/8/8/R3K3 w - - 100 80"), 1), Is.EqualTo(DrawReason.FiftyMove));
        }

        [Test]
        public void ThirdOccurrenceIsRepetitionRegardlessOfMoveCounters()
        {
            var history = new PositionHistory();
            Assert.That(history.Record(P("7k/8/8/8/8/8/8/R3K3 w - - 0 1")), Is.EqualTo(1));
            Assert.That(history.Record(P("7k/8/8/8/8/8/8/R3K3 w - - 4 3")), Is.EqualTo(2));
            int third = history.Record(P("7k/8/8/8/8/8/8/R3K3 w - - 8 5"));
            Assert.That(third, Is.EqualTo(3));
            Assert.That(ChessDrawRules.Evaluate(P("7k/8/8/8/8/8/8/R3K3 w - - 8 5"), third), Is.EqualTo(DrawReason.Repetition));
        }

        [Test]
        public void SideToMoveDistinguishesPositions()
        {
            var history = new PositionHistory();
            history.Record(P("7k/8/8/8/8/8/8/R3K3 w - - 0 1"));
            Assert.That(history.Record(P("7k/8/8/8/8/8/8/R3K3 b - - 0 1")), Is.EqualTo(1));
        }

        [Test]
        public void ClearForgetsEverything()
        {
            var history = new PositionHistory();
            history.Record(P("7k/8/8/8/8/8/8/R3K3 w - - 0 1"));
            history.Clear();
            Assert.That(history.Record(P("7k/8/8/8/8/8/8/R3K3 w - - 0 1")), Is.EqualTo(1));
        }
    }
}
