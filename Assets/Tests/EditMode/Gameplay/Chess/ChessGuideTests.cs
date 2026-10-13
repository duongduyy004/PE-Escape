using System.Linq;
using KMA.Gameplay.Chess;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessGuideTests
    {
        [Test]
        public void GoalPageCarriesMovesAndTime()
        {
            var pages = ChessGuide.Build(2, 3, 90);
            Assert.That(pages.Count, Is.EqualTo(3));
            Assert.That(pages[0].Instruction, Does.Contain("tối đa 3 nước"));
            Assert.That(pages[0].Instruction, Does.Contain("trong 2 nước"));
            Assert.That(pages[0].Instruction, Does.Contain("90 giây"));
            Assert.That(pages[0].Instruction, Does.Contain("không bị trừ lượt thi"));
        }

        [Test]
        public void TimeIsOnlyMentionedWhenKnown()
        {
            Assert.That(ChessGuide.Build(2, 3, 0)[0].Instruction, Does.Not.Contain("giây"));
            Assert.That(ChessGuide.Build(2, 3, 0)[0].Instruction, Does.Contain("Thời gian suy nghĩ được tính theo độ khó"));
        }

        [Test]
        public void ControlsPageIsUnchanged() =>
            Assert.That(ChessGuide.Build(2, 3, 90)[1].Instruction, Is.EqualTo(
                "Chạm quân của bạn, sau đó chạm ô muốn đi. Nếu tốt được phong cấp, chọn quân thay thế. " +
                "Nút GỢI Ý có 3 mức: ý tưởng, quân cần xem và nước đi cụ thể."));

        [Test]
        public void RulesDescribeRealMovesAndScoring()
        {
            string rules = ChessGuide.Build(2, 3, 90)[2].Instruction;
            Assert.That(rules, Does.Contain("không thể hoàn tác"));
            Assert.That(rules, Does.Contain("70%"));
            Assert.That(rules, Does.Contain("30%"));
            Assert.That(rules, Does.Contain("hạng tối đa ở A"));
        }

        [Test]
        public void NoPageMentionsTheOldMistakeRule() =>
            Assert.That(ChessGuide.Build(3, 5, 150).Select(p => p.Instruction),
                Has.None.Contain("sửa sai").Or.None.Contain("lần sửa"));
    }
}
