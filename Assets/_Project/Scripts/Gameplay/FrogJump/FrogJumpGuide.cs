using System.Collections.Generic;
using KMA.Gameplay.UI;

namespace KMA.Gameplay.FrogJump
{
    public static class FrogJumpGuide
    {
        public const string Key = SaveDataTutorialSeenStore.FrogJumpKey;
        public const string Controls =
            "Kim di chuyển qua lại trên thanh lực. Chạm màn hình khi kim nằm trong vùng xanh. Chạm càng gần giữa, bạn bật càng xa.";

        /// savesLife: a finish keeps the life (JourneyProgress only charges SavesLife && !reachedFinish).
        public static IReadOnlyList<TutorialStep> Build(FrogJumpTuning tuning, bool savesLife)
        {
            string goal = $"Bật cóc hết {MinigameGuidePages.Number(tuning.trackMetres)} m trong tối đa {MinigameGuidePages.Number(tuning.timeLimitSeconds)} giây. " +
                (savesLife ? "Đây là hình phạt sau khi trượt bài. Về đích để giữ lượt thi; hết giờ sẽ mất 1 lượt thi."
                    : "Bạn đã bị trừ 1 lượt thi khi trượt bài. Hình phạt này không trừ thêm lượt thi.");
            string rules = $"Chạm khi kim ở vùng đỏ sẽ bị ngã: không tiến thêm và mất {MinigameGuidePages.Number(tuning.recoverSeconds)} giây hồi phục. Đồng hồ vẫn chạy trong thời gian này.";
            return MinigameGuidePages.Pages(
                new TutorialStep("MỤC TIÊU", goal),
                new TutorialStep("ĐIỀU KHIỂN", Controls),
                new TutorialStep("LUẬT", rules));
        }
    }
}
