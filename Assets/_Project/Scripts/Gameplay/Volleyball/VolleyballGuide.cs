using System.Collections.Generic;
using KMA.Gameplay.UI;

namespace KMA.Gameplay.Volleyball
{
    public static class VolleyballGuide
    {
        public const string Key = nameof(SubjectId.Volleyball);
        public const string Move =
            "Kéo joystick để di chuyển đến vị trí bóng rơi.";
        public const string HitAndJump =
            "Nút ĐÁNH tự chọn động tác giao, đỡ, chuyền, đập hoặc chắn theo tình huống. Khi nút NHẢY sáng, bấm NHẢY rồi kéo joystick để chọn hướng đập.";
        public const string Rules =
            "Thời điểm bấm được đánh giá là HOÀN HẢO, TỐT, SỚM hoặc MUỘN. Mỗi bên chạm bóng tối đa 3 lần. Để bóng rơi trong sân mình hoặc đánh không qua lưới sẽ mất điểm.";

        public static IReadOnlyList<TutorialStep> Build(ChallengeKind? kind, int learnTarget, int pointsToWin,
            float clockLimit)
        {
            string goal;
            if (kind == ChallengeKind.Learn)
            {
                goal = $"Đỡ bóng thành công {VolleyballChallengeRules.LearnReceiveTarget} lần, phát bóng {VolleyballChallengeRules.LearnServeTarget} lần, rồi nhảy và đánh trả bóng {VolleyballChallengeRules.LearnJumpHitTarget} lần. Di chuyển để làm quen; đỡ và phát bóng được đối thủ cấp bóng.";
            }
            else
            {
                goal = $"Ghi đủ {pointsToWin} điểm trước đối thủ" +
                       (clockLimit > 0f ? $" trong tối đa {MinigameGuidePages.Number(clockLimit)} giây." : ".");
                goal += " Người chơi giao bóng đầu tiên. Bên ghi điểm được giao bóng tiếp.";
                if (clockLimit > 0f)
                    goal += " Hết giờ chưa đủ điểm vẫn chưa đạt, kể cả đang dẫn điểm.";
            }

            return MinigameGuidePages.Pages(
                new TutorialStep("MỤC TIÊU", goal),
                new TutorialStep("DI CHUYỂN", Move),
                new TutorialStep("ĐÁNH VÀ NHẢY", HitAndJump),
                new TutorialStep("LUẬT", Rules),
                MinigameGuidePages.FailurePage(kind));
        }
    }
}
