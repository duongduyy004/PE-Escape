using KMA.Gameplay;
using NUnit.Framework;

namespace KMA.Tests.Gameplay
{
    public sealed class RankLabelsTests
    {
        [TestCase(Rank.S, "A+")]
        [TestCase(Rank.A, "A")]
        [TestCase(Rank.B, "B")]
        [TestCase(Rank.C, "C")]
        [TestCase(Rank.D, "D")]
        [TestCase(Rank.F, "F")]
        public void TopRankReadsAPlusAndEverythingElseIsUnchanged(Rank rank, string expected) =>
            Assert.That(RankLabels.For(rank), Is.EqualTo(expected));
    }
}
