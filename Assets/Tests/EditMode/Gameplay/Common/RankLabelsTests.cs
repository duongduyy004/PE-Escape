using KMA.Gameplay;
using NUnit.Framework;

namespace KMA.Tests.Gameplay
{
    public sealed class RankLabelsTests
    {
        [TestCase(Rank.S, true, "A+")]
        [TestCase(Rank.A, true, "A")]
        [TestCase(Rank.B, true, "B")]
        [TestCase(Rank.C, true, "C")]
        [TestCase(Rank.D, true, "D")]
        [TestCase(Rank.F, true, "F")]
        [TestCase(Rank.S, false, "S")]
        [TestCase(Rank.A, false, "A")]
        public void ChessShowsAPlusAndEverythingElseIsUnchanged(Rank rank, bool chess, string expected) =>
            Assert.That(RankLabels.For(rank, chess), Is.EqualTo(expected));
    }
}
