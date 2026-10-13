using KMA.Gameplay;
using KMA.Gameplay.UI;
using NUnit.Framework;

namespace KMA.Tests.Presentation
{
    public sealed class JourneyCourseSummaryRankTests
    {
        [Test]
        public void OnlyANewlyScoredChessBestReadsAPlus()
        {
            Assert.That(JourneyCourseSummary.RankText(SubjectId.Chess, Rank.S, ChessResultSummary.CurrentScoringVersion), Is.EqualTo("A+"));
            Assert.That(JourneyCourseSummary.RankText(SubjectId.Chess, Rank.S, 0), Is.EqualTo("S"),
                "an old chess record was not graded with the new scale and must not become A+");
            Assert.That(JourneyCourseSummary.RankText(SubjectId.Chess, Rank.B, 0), Is.EqualTo("B"));
        }

        [TestCase(SubjectId.Sprint)]
        [TestCase(SubjectId.Volleyball)]
        [TestCase(SubjectId.Football)]
        public void OtherSubjectsKeepS(SubjectId subject) =>
            Assert.That(JourneyCourseSummary.RankText(subject, Rank.S, ChessResultSummary.CurrentScoringVersion), Is.EqualTo("S"));
    }
}
