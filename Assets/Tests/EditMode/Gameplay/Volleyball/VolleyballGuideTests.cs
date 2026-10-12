using KMA.Gameplay;
using KMA.Gameplay.Volleyball;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Volleyball
{
    public sealed class VolleyballGuideTests
    {
        [Test]
        public void LearnExplainsAllThreeDrills()
        {
            var pages = VolleyballGuide.Build(ChallengeKind.Learn, 3, 0, 0f);
            Assert.That(pages.Count, Is.EqualTo(4));
            Assert.That(pages[0].Instruction, Is.EqualTo("Đỡ bóng thành công 3 lần, phát bóng 2 lần, rồi nhảy và đánh trả bóng 2 lần. Di chuyển để làm quen; đỡ và phát bóng được đối thủ cấp bóng."));
            Assert.That(pages[1].Title, Is.EqualTo("DI CHUYỂN"));
            Assert.That(pages[1].Instruction, Is.EqualTo("Kéo joystick để di chuyển đến vị trí bóng rơi."));
            Assert.That(pages[2].Title, Is.EqualTo("ĐÁNH VÀ NHẢY"));
            Assert.That(pages[2].Instruction, Is.EqualTo(
                "Nút ĐÁNH tự chọn động tác giao, đỡ, chuyền, đập hoặc chắn theo tình huống. " +
                "Khi nút NHẢY sáng, bấm NHẢY rồi kéo joystick để chọn hướng đập."));
            Assert.That(pages[3].Instruction, Is.EqualTo(
                "Thời điểm bấm được đánh giá là HOÀN HẢO, TỐT, SỚM hoặc MUỘN. Mỗi bên chạm bóng tối đa 3 lần. " +
                "Để bóng rơi trong sân mình hoặc đánh không qua lưới sẽ mất điểm."));
        }

        [Test]
        public void PracticeIsARaceAgainstTheClock()
        {
            var pages = VolleyballGuide.Build(ChallengeKind.Practice, 5, 5, 120f);
            Assert.That(pages.Count, Is.EqualTo(5));
            Assert.That(pages[0].Instruction,
                Is.EqualTo("Ghi đủ 5 điểm trước đối thủ trong tối đa 120 giây. Đối thủ luôn giao bóng."));
            Assert.That(pages[4].Title, Is.EqualTo("NẾU CHƯA ĐẠT"));
        }

        [Test]
        public void ExamHasNoClockAndAlternatesServes()
        {
            var pages = VolleyballGuide.Build(ChallengeKind.Exam, 10, 10, 0f);
            Assert.That(pages[0].Instruction,
                Is.EqualTo("Ghi đủ 10 điểm trước đối thủ. Hai bên luân phiên giao bóng."));
        }
    }
}
