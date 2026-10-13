using KMA.Gameplay;
using NUnit.Framework;

namespace KMA.Tests.Presentation
{
    public sealed class SprintOffscreenRivalChipsTests
    {
        [TestCase(5f, SprintOffscreenRivalChips.Edge.None)]
        [TestCase(10.1f, SprintOffscreenRivalChips.Edge.None)]
        [TestCase(10.7f, SprintOffscreenRivalChips.Edge.Right)]
        [TestCase(-10.7f, SprintOffscreenRivalChips.Edge.Left)]
        public void ChipAppearsOnlyOnceTheRivalIsFullyOutOfView(float rivalX, SprintOffscreenRivalChips.Edge expected) =>
            Assert.That(SprintOffscreenRivalChips.EdgeFor(rivalX, 0f, 10f), Is.EqualTo(expected));

        [Test]
        public void GapIsSignedWithOneDecimal()
        {
            Assert.That(SprintOffscreenRivalChips.GapText(54.21f, 50f), Is.EqualTo("+4.2 m"));
            Assert.That(SprintOffscreenRivalChips.GapText(46.9f, 50f), Is.EqualTo("−3.1 m"));
        }
    }
}
