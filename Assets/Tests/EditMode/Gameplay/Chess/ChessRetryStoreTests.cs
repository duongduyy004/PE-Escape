using KMA.Gameplay;
using KMA.Gameplay.Chess;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessRetryStoreTests
    {
        static ChessPuzzleSnapshot Snap(string fen) => new ChessPuzzleSnapshot { fen = fen, level = "Normal", maxMoves = 3, optimalMoves = 2, timeSeconds = 90 };

        [SetUp]
        public void SetUp()
        {
            ChessRetryStore.Forget();
            ChallengeRetryIntent.Clear();
        }

        [Test]
        public void RetryReturnsTheRememberedSnapshotOnceThenForgetsIt()
        {
            ChessRetryStore.Remember(Snap("fen-1"));
            ChessPuzzleSnapshot first = ChessRetryStore.TakeIfRetry(true);
            Assert.That(first.fen, Is.EqualTo("fen-1"));
            Assert.That(ChessRetryStore.TakeIfRetry(true), Is.Null);
        }

        [Test]
        public void NotARetryIgnoresAndClearsTheStore()
        {
            ChessRetryStore.Remember(Snap("fen-1"));
            Assert.That(ChessRetryStore.TakeIfRetry(false), Is.Null);
            Assert.That(ChessRetryStore.HasSnapshot, Is.False);
        }

        [Test]
        public void ReturnedSnapshotIsACopyOfTheStoredOne()
        {
            var original = Snap("fen-1");
            ChessRetryStore.Remember(original);
            original.fen = "mutated";
            Assert.That(ChessRetryStore.TakeIfRetry(true).fen, Is.EqualTo("fen-1"));
        }

        [Test]
        public void IntentMatchesOnlyItsOwnChallengeAndIsConsumedOnce()
        {
            ChallengeRetryIntent.Arm("chess_final");
            Assert.That(ChallengeRetryIntent.Consume("sprint_exam"), Is.False);
            Assert.That(ChallengeRetryIntent.Consume("chess_final"), Is.True, "a mismatched consume must not clear it");
            Assert.That(ChallengeRetryIntent.Consume("chess_final"), Is.False);
        }

        [Test]
        public void ClearDisarms()
        {
            ChallengeRetryIntent.Arm("chess_final");
            ChallengeRetryIntent.Clear();
            Assert.That(ChallengeRetryIntent.Consume("chess_final"), Is.False);
        }

        [Test]
        public void RememberNullClears()
        {
            ChessRetryStore.Remember(Snap("fen-1"));
            ChessRetryStore.Remember(null);
            Assert.That(ChessRetryStore.HasSnapshot, Is.False);
        }
    }
}
