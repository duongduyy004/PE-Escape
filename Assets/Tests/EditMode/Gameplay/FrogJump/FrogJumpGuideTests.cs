using KMA.Gameplay.FrogJump;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.FrogJump
{
    public sealed class FrogJumpGuideTests
    {
        [Test]
        public void ALifeSavingJumpSaysSo()
        {
            var pages = FrogJumpGuide.Build(new FrogJumpTuning(), true);
            Assert.That(pages.Count, Is.EqualTo(3));
            Assert.That(pages[0].Instruction, Is.EqualTo(
                "Bật cóc hết 60 m trong tối đa 40 giây. Đây là hình phạt sau khi trượt bài. Về đích để giữ lượt thi; hết giờ sẽ mất 1 lượt thi."));
            Assert.That(pages[1].Instruction, Is.EqualTo(
                "Kim di chuyển qua lại trên thanh lực. Chạm màn hình khi kim nằm trong vùng xanh. Chạm càng gần giữa, bạn bật càng xa."));
            Assert.That(pages[2].Instruction,
                Is.EqualTo("Chạm khi kim ở vùng đỏ sẽ bị ngã: không tiến thêm và mất 2,5 giây hồi phục. Đồng hồ vẫn chạy trong thời gian này."));
        }

        [Test]
        public void AJumpThatCannotSaveALifeOmitsTheSentence()
        {
            var pages = FrogJumpGuide.Build(new FrogJumpTuning(), false);
            Assert.That(pages[0].Instruction,
                Is.EqualTo("Bật cóc hết 60 m trong tối đa 40 giây. Bạn đã bị trừ 1 lượt thi khi trượt bài. Hình phạt này không trừ thêm lượt thi."));
        }

        [Test]
        public void KeyIsTheSavedFrogJumpKey() => Assert.That(FrogJumpGuide.Key, Is.EqualTo("FrogJump"));
    }
}
