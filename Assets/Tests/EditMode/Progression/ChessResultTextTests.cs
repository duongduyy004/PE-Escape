using KMA.Gameplay;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Progression
{
    public sealed class ChessResultTextTests
    {
        static ChessResultSummary Win() => new ChessResultSummary
        {
            scoringVersion = 1, movesUsed = 2, maxMoves = 3, optimalMoves = 2, allowedSeconds = 95, usedSeconds = 41.2f,
            endReason = "Mate", gradeLabel = "A+", score = 10f,
            moves = new[]
            {
                new ChessMoveEntry { uci = "b1b7", q = 1f, best = 2, chosen = 2 },
                new ChessMoveEntry { uci = "a2a8", q = 1f, best = 1, chosen = 1 }
            }
        };

        [Test]
        public void SuccessLineShowsMovesOptimalTimeAndHintState()
        {
            string text = ChessResultText.Detail(Win());
            Assert.That(text, Does.Contain("Nước: 2/3"));
            Assert.That(text, Does.Contain("Tối ưu: 2"));
            Assert.That(text, Does.Contain("00:41/01:35"));
            Assert.That(text, Does.Contain("Không dùng gợi ý"));
            Assert.That(text, Does.Not.Contain("sửa"));
        }

        [Test]
        public void HintStatesAreDistinguished()
        {
            ChessResultSummary general = Win();
            general.hintUsed = true;
            Assert.That(ChessResultText.Detail(general), Does.Contain("Có dùng gợi ý"));
            ChessResultSummary capped = Win();
            capped.hintUsed = true; capped.explicitHintUsed = true; capped.hintCapped = true;
            Assert.That(ChessResultText.Detail(capped), Does.Contain("tối đa A"));
        }

        [TestCase("TimeUp", "Hết giờ")]
        [TestCase("OutOfMoves", "Hết số nước")]
        [TestCase("Mated", "Bị chiếu hết")]
        [TestCase("Draw", "Hòa cờ")]
        [TestCase("Mate", "Chiếu hết")]
        public void EndReasonsAreReadable(string reason, string expected) =>
            Assert.That(ChessResultText.EndReasonText(reason), Is.EqualTo(expected));

        [Test]
        public void FailureListsTheMovesPlayedWithTheirQualityAndNoScoreClaim()
        {
            ChessResultSummary s = Win();
            s.endReason = "OutOfMoves"; s.gradeLabel = "F"; s.score = 0f;
            s.moves = new[]
            {
                new ChessMoveEntry { uci = "g1f1", q = 0.5f, best = 2, chosen = 4 },
                new ChessMoveEntry { uci = "f1g1", q = 0f, best = 2, chosen = 0 }
            };
            string text = ChessResultText.Detail(s);
            Assert.That(text, Does.Contain("Hết số nước"));
            Assert.That(text, Does.Contain("g1f1"));
            Assert.That(text, Does.Contain("50%"));
            Assert.That(text, Does.Contain("0%"));
        }

        [Test]
        public void MoveLinesAreCappedAtTheRequestedCount()
        {
            var s = new ChessResultSummary { moves = new ChessMoveEntry[5] };
            for (int i = 0; i < 5; i++) s.moves[i] = new ChessMoveEntry { uci = "a1a" + (i + 2), q = 1f };
            Assert.That(ChessResultText.MoveLines(s, 3).Split('\n').Length, Is.EqualTo(4), "3 lines plus the ellipsis line");
        }

        [Test]
        public void TheBreakdownStaysShortEnoughForTheResultCard()
        {
            Assert.That(ChessResultText.Detail(Win()).Split('\n').Length, Is.LessThanOrEqualTo(2), "a win is two lines");
            ChessResultSummary failed = Win();
            failed.endReason = "OutOfMoves"; failed.gradeLabel = "F"; failed.score = 0f; failed.hintUsed = true;
            failed.moves = new ChessMoveEntry[7];
            for (int i = 0; i < failed.moves.Length; i++) failed.moves[i] = new ChessMoveEntry { uci = "a1a" + (i + 2), q = i / 10f };
            string text = ChessResultText.Detail(failed);
            Assert.That(text.Split('\n').Length, Is.LessThanOrEqualTo(3), "a failure is at most three lines: " + text);
            Assert.That(text, Does.Contain("a1a2"));
            Assert.That(text, Does.Contain("…"), "moves beyond the cap are marked");
        }

        [Test]
        public void NullSummaryYieldsEmptyText() => Assert.That(ChessResultText.Detail(null), Is.Empty);

        [TestCase(0f, "00:00")]
        [TestCase(59.4f, "00:59")]
        [TestCase(95f, "01:35")]
        public void ClockIsMinutesAndSeconds(float seconds, string expected) =>
            Assert.That(ChessResultText.FormatClock(seconds), Is.EqualTo(expected));
    }
}
