using KMA.Gameplay;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Progression
{
    public sealed class CelebrationSummaryTests
    {
        [Test]
        public void SummaryReadsRecordsAndTheChessBest()
        {
            var session = new GameSession();
            JourneyTestData.CompleteThrough(session, "chess_final");
            CelebrationSummary summary = CelebrationSummary.From(session);

            Assert.That(summary.IsSample, Is.False);
            Assert.That(summary.Subjects.Count, Is.EqualTo(3));
            Assert.That(summary.Subjects[0].Title, Is.EqualTo("Chạy nước rút"));
            Assert.That(summary.Subjects[2].Title, Is.EqualTo("Bóng đá"));
            foreach (CelebrationRow row in summary.Subjects)
            {
                Assert.That(row.Completed, Is.True);
                Assert.That(row.HasScore, Is.True);
                Assert.That(row.Score, Is.EqualTo(8f));
            }
            Assert.That(summary.ChessRecorded, Is.True);
            Assert.That(summary.ChessThinkSeconds, Is.EqualTo(90f));
            Assert.That(summary.ChessMistakes, Is.EqualTo(0));
        }

        [Test]
        public void LegacyChessRecordIsMarkedLegacyAndKeepsItsValues()
        {
            var session = new GameSession();
            JourneyTestData.CompleteThrough(session, "chess_final");
            CelebrationSummary summary = CelebrationSummary.From(session);
            Assert.That(summary.ChessScoringVersion, Is.EqualTo(0));
            Assert.That(summary.ChessThinkSeconds, Is.EqualTo(90f));
            Assert.That(summary.ChessMistakes, Is.EqualTo(0));
            Assert.That(summary.ChessGradeLabel, Is.Null.Or.Empty);
        }

        [Test]
        public void NewChessRecordExposesTheNewProperties()
        {
            var session = new GameSession();
            JourneyTestData.CompleteThrough(session, "soccer_exam");
            Assert.That(session.TryStartChallenge("chess_final", ChallengeAttemptMode.Journey,
                ChallengeDifficulty.Normal, out ChallengeAttemptContext context), Is.True);
            var chess = new ChessResultSummary
            {
                scoringVersion = ChessResultSummary.CurrentScoringVersion, movesUsed = 2, maxMoves = 3, optimalMoves = 2,
                allowedSeconds = 90, gradeLabel = "A+", score = 9.5f
            };
            session.SubmitChallengeResult(new ChallengeAttemptResult(context, true,
                new ChallengeMetrics(elapsed: 40f, completedTargets: 2, chess: chess), new MinigameResult(true, 9.5f, Rank.S)));
            CelebrationSummary summary = CelebrationSummary.From(session);
            Assert.That(summary.ChessScoringVersion, Is.EqualTo(ChessResultSummary.CurrentScoringVersion));
            Assert.That(summary.ChessMovesUsed, Is.EqualTo(2));
            Assert.That(summary.ChessMaxMoves, Is.EqualTo(3));
            Assert.That(summary.ChessOptimalMoves, Is.EqualTo(2));
            Assert.That(summary.ChessAllowedSeconds, Is.EqualTo(90));
            Assert.That(summary.ChessGradeLabel, Is.EqualTo("A+"));
        }

        [Test]
        public void SampleIsMarkedAndClockFormatsMinutes()
        {
            Assert.That(CelebrationSummary.Sample().IsSample, Is.True);
            Assert.That(CelebrationSummary.FormatClock(42.3f), Is.EqualTo("00:43"));
            Assert.That(CelebrationSummary.FormatClock(90f), Is.EqualTo("01:30"));
        }
    }
}
