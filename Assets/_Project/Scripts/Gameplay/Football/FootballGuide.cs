using System.Collections.Generic;
using KMA.Gameplay.UI;

namespace KMA.Gameplay
{
    public static class FootballGuide
    {
        public const string Key = nameof(SubjectId.Football);
        public const string Aim =
            "Kéo thanh hướng sang TRÁI hoặc PHẢI để chọn góc sút. Đường bay dự kiến xuất hiện khi bạn lấy lực.";
        public const string Power =
            "Giữ nút SÚT để lấy lực; lực sẽ tăng rồi giảm liên tục. Thả nút để sút. Lực quá mạnh dễ làm bóng vọt xà; quá yếu có thể khiến bóng dừng trước khung thành.";
        public const string Rules =
            "Mỗi cú sút tính một lượt, kể cả khi bóng trúng cột, trúng xà, ra ngoài hoặc bị thủ môn cản phá. Bài làm quen không giới hạn lượt sút.";

        public static IReadOnlyList<TutorialStep> Build(ChallengeKind? kind, int requiredGoals, int? maxKicks,
            bool keeperEnabled)
        {
            string keeper = keeperEnabled ? "Có thủ môn" : "Không có thủ môn";
            string goal = maxKicks.HasValue
                ? kind == ChallengeKind.Exam ? $"Ghi ít nhất {requiredGoals} bàn sau {maxKicks.Value} lượt sút. {keeper}." : $"Ghi ít nhất {requiredGoals} bàn trong tối đa {maxKicks.Value} lượt sút. {keeper}."
                : $"Ghi {requiredGoals} bàn. {keeper} và không giới hạn lượt sút.";
            return MinigameGuidePages.Pages(
                new TutorialStep("MỤC TIÊU", goal),
                new TutorialStep("NGẮM", Aim),
                new TutorialStep("LỰC SÚT", Power),
                new TutorialStep("LUẬT", Rules),
                MinigameGuidePages.FailurePage(kind));
        }
    }
}
