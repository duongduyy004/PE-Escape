using System.Collections.Generic;
using KMA.Gameplay.UI;

namespace KMA.Gameplay
{
    public static class SprintGuide
    {
        public const string Key = nameof(SubjectId.Sprint);
        public const string Controls =
            "Bấm TRÁI rồi PHẢI, luân phiên để chạy. Giữ nhịp đúng để tăng chuỗi và bứt tốc. Ngừng bấm thì tốc độ giảm.";
        public const string RaceRules =
            "Bấm nhầm bên sẽ mất chuỗi và tăng tốc rất ít. Hết giờ mà chưa về đích là chưa đạt. Điểm tính theo độ chính xác, thứ hạng và thời gian còn lại.";
        // The learn lesson has no clock: a wrong side only resets the streak it counts.
        public const string LearnRules =
            "Bấm nhầm bên sẽ đưa chuỗi về 0.";

        public static IReadOnlyList<TutorialStep> Build(ChallengeKind? kind, int targetCount, float distance,
            float timeLimit, int rivalCount)
        {
            bool learn = kind == ChallengeKind.Learn;
            string goal = learn
                ? $"Bấm TRÁI, PHẢI luân phiên đúng {targetCount} nhịp liên tiếp. Bài này không tính giờ."
                : $"Về đích {MinigameGuidePages.Number(distance)} m trong tối đa {MinigameGuidePages.Number(timeLimit)} giây." +
                  (rivalCount > 0 ? $" Có {rivalCount} sinh viên chạy cùng." : string.Empty);
            return MinigameGuidePages.Pages(
                new TutorialStep("MỤC TIÊU", goal),
                new TutorialStep("ĐIỀU KHIỂN", Controls),
                new TutorialStep("LUẬT", learn ? $"{LearnRules} Bạn cần thực hiện lại đủ {targetCount} nhịp liên tiếp." : RaceRules),
                MinigameGuidePages.FailurePage(kind));
        }
    }
}
