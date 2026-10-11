using KMA.Gameplay.Chess;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessGuideTests
    {
        [Test]
        public void PagesCarryThePuzzleNumbers()
        {
            var pages = ChessGuide.Build(2, 90f, 2);
            Assert.That(pages.Count, Is.EqualTo(3));
            Assert.That(pages[0].Instruction, Is.EqualTo(
                "Chiếu hết trong 2 nước. Có 90 giây suy nghĩ. Trượt bài này không bị trừ lượt thi và không phải Bật cóc."));
            Assert.That(pages[1].Instruction, Is.EqualTo(
                "Chạm quân của bạn, sau đó chạm ô muốn đi. Nếu tốt được phong cấp, chọn quân thay thế. " +
                "Nút GỢI Ý có 3 mức: ý tưởng, quân cần xem và nước đi cụ thể."));
            Assert.That(pages[2].Instruction, Is.EqualTo(
                "Nước không hợp lệ không tính lỗi. Nước hợp lệ nhưng sai lời giải tính 1 lỗi, bàn cờ giữ nguyên. " +
                "Được sửa sai 2 lần; sai lần thứ 3 hoặc hết giờ là chưa đạt. Đồng hồ chỉ chạy trong lượt của bạn. " +
                "Đi sai, dùng gợi ý và dùng nhiều thời gian sẽ làm giảm điểm."));
        }

        [TestCase(60f, "một phút")]
        [TestCase(90f, "một phút rưỡi")]
        [TestCase(120f, "hai phút")]
        [TestCase(45f, "45 giây")]
        public void TimeWordsReadAloud(float seconds, string expected) =>
            Assert.That(ChessGuide.TimeWords(seconds), Is.EqualTo(expected));
    }
}
