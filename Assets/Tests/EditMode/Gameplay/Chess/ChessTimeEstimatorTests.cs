using KMA.Gameplay.Chess;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessTimeEstimatorTests
    {
        [TestCase(1, 1.0, 0, 60)]    // 58 clamps up to 60
        [TestCase(1, 4.0, 0, 85)]    // 25+25+32 = 82 -> 85
        [TestCase(2, 4.0, 0, 110)]   // 25+50+32 = 107 -> 110
        [TestCase(3, 5.0, 2, 170)]   // 25+75+40+30 = 170 exact
        [TestCase(3, 30.0, 9, 240)]  // far above the cap
        public void FormulaClampsAndRoundsUpToFive(int n, double b, int q, int expected) =>
            Assert.That(ChessTimeEstimator.Seconds(n, b, q), Is.EqualTo(expected));

        [Test]
        public void SameOptimalMovesCanGiveDifferentTimes() =>
            Assert.That(ChessTimeEstimator.Seconds(2, 3.0, 0), Is.Not.EqualTo(ChessTimeEstimator.Seconds(2, 5.5, 1)));

        [Test]
        public void ResultIsAlwaysMultipleOfFiveInRange()
        {
            for (int n = 1; n <= 3; n++)
            for (double b = 1; b <= 8; b += .37)
            for (int q = 0; q <= 4; q++)
            {
                int t = ChessTimeEstimator.Seconds(n, b, q);
                Assert.That(t % 5, Is.EqualTo(0));
                Assert.That(t, Is.InRange(60, 240));
            }
        }

        [Test]
        public void VersionIsOne() => Assert.That(ChessTimeEstimator.Version, Is.EqualTo(1));
    }
}
