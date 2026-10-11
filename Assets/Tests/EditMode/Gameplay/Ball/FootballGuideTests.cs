using KMA.Gameplay;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Ball
{
    public sealed class FootballGuideTests
    {
        [Test]
        public void LearnHasNoKeeperAndNoKickLimit()
        {
            var pages = FootballGuide.Build(ChallengeKind.Learn, 3, null, false);
            Assert.That(pages.Count, Is.EqualTo(4));
            Assert.That(pages[0].Instruction,
                Is.EqualTo("Ghi 3 bàn. Không có thủ môn và không giới hạn lượt sút."));
            Assert.That(pages[1].Title, Is.EqualTo("NGẮM"));
            Assert.That(pages[2].Title, Is.EqualTo("LỰC SÚT"));
            Assert.That(pages[3].Title, Is.EqualTo("LUẬT"));
        }

        [Test]
        public void PracticeCountsKicksAndAddsTheFailurePage()
        {
            var pages = FootballGuide.Build(ChallengeKind.Practice, 2, 6, true);
            Assert.That(pages.Count, Is.EqualTo(5));
            Assert.That(pages[0].Instruction, Is.EqualTo("Ghi ít nhất 2 bàn trong tối đa 6 lượt sút. Có thủ môn."));
            Assert.That(pages[1].Instruction, Is.EqualTo(
                "Kéo thanh hướng sang TRÁI hoặc PHẢI để chọn góc sút. Đường bay dự kiến xuất hiện khi bạn lấy lực."));
            Assert.That(pages[2].Instruction, Is.EqualTo(
                "Giữ nút SÚT để lấy lực; lực sẽ tăng rồi giảm liên tục. Thả nút để sút. " +
                "Lực quá mạnh dễ làm bóng vọt xà; quá yếu có thể khiến bóng dừng trước khung thành."));
            Assert.That(pages[3].Instruction,
                Is.EqualTo("Mỗi cú sút tính một lượt, kể cả khi bóng trúng cột, trúng xà, ra ngoài hoặc bị thủ môn cản phá. Bài làm quen không giới hạn lượt sút."));
            Assert.That(pages[4].Title, Is.EqualTo("NẾU CHƯA ĐẠT"));
        }

        [Test]
        public void FinalHasNoFailurePage() =>
            Assert.That(FootballGuide.Build(ChallengeKind.Final, 3, 5, true).Count, Is.EqualTo(4));
    }
}
