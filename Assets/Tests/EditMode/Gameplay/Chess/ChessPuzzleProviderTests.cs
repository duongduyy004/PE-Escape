using System.Diagnostics;
using KMA.Gameplay.Chess;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessPuzzleProviderTests
    {
        static ChessPuzzleResult WaitFor(ChessPuzzleProvider provider)
        {
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed.TotalSeconds < 15)
            {
                if (provider.TryTake(out ChessPuzzleResult result)) return result;
                System.Threading.Thread.Sleep(5);
            }
            Assert.Fail("provider produced no puzzle");
            return null;
        }

        [TestCase(ChessLevel.Easy)]
        [TestCase(ChessLevel.Normal)]
        [TestCase(ChessLevel.Hard)]
        public void EveryLevelYieldsAPuzzleOfThatLevel(ChessLevel level)
        {
            using var provider = new ChessPuzzleProvider();
            provider.Begin(level, 123, null);
            ChessPuzzleSnapshot s = WaitFor(provider).Snapshot;
            ChessLevelRules rules = ChessLevelRules.For(level);
            Assert.That(s.Level, Is.EqualTo(level));
            Assert.That(s.optimalMoves, Is.EqualTo(rules.OptimalMoves));
            Assert.That(s.maxMoves, Is.EqualTo(rules.MaxMoves));
            Assert.That(s.source, Is.EqualTo(ChessPuzzleSnapshot.GeneratedSource).Or.EqualTo(ChessPuzzleSnapshot.FallbackSource));
            Assert.That(s.timeSeconds, Is.InRange(60, 240));
        }

        [Test]
        public void AvoidFenIsHonouredEvenViaFallback()
        {
            using var provider = new ChessPuzzleProvider();
            provider.Begin(ChessLevel.Normal, 5, null);
            string first = WaitFor(provider).Snapshot.fen;
            provider.Begin(ChessLevel.Normal, 5, first);
            Assert.That(WaitFor(provider).Snapshot.fen, Is.Not.EqualTo(first));
        }

        [Test]
        public void SourceCountersTrackGeneratedAndFallback()
        {
            int before = ChessPuzzleProvider.GeneratedCount + ChessPuzzleProvider.FallbackCount;
            using var provider = new ChessPuzzleProvider();
            provider.Begin(ChessLevel.Easy, 8, null);
            WaitFor(provider);
            Assert.That(ChessPuzzleProvider.GeneratedCount + ChessPuzzleProvider.FallbackCount, Is.EqualTo(before + 1));
        }

        [Test]
        public void CancelBeforeFinishYieldsNoResult()
        {
            using var provider = new ChessPuzzleProvider();
            provider.Begin(ChessLevel.Hard, 1, null);
            provider.Cancel();
            System.Threading.Thread.Sleep(400);
            Assert.That(provider.TryTake(out _), Is.False);
        }
    }
}
