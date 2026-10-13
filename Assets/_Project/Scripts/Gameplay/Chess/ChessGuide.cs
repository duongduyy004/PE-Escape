using System.Collections.Generic;
using KMA.Gameplay.UI;

namespace KMA.Gameplay.Chess
{
    public static class ChessGuide
    {
        public const string Key = nameof(SubjectId.Chess);
        // ChessBoardView.ClickSquare: tap a piece, then tap its destination.
        public const string Controls =
            "Chạm quân của bạn, sau đó chạm ô muốn đi. Nếu tốt được phong cấp, chọn quân thay thế. Nút GỢI Ý có 3 mức: ý tưởng, quân cần xem và nước đi cụ thể.";

        /// `timeSeconds` of 0 or less means the puzzle's time is not known yet.
        public static IReadOnlyList<TutorialStep> Build(int optimalMoves, int maxMoves, int timeSeconds)
        {
            string time = timeSeconds > 0
                ? $"Có {MinigameGuidePages.Number(timeSeconds)} giây suy nghĩ."
                : "Thời gian suy nghĩ được tính theo độ khó của thế cờ.";
            string goal = "Bạn nhận một thế cờ ngẫu nhiên theo độ khó đã chọn. " +
                          $"Chiếu hết trong tối đa {maxMoves} nước; có cách chiếu hết trong {optimalMoves} nước. {time} " +
                          "Trượt bài này không bị trừ lượt thi và không phải Bật cóc.";
            string rules = "Mỗi nước đi hợp lệ của bạn tính một nước và không thể hoàn tác. " +
                           "Nước không hợp lệ không tính nước; nước của giảng viên không trừ vào giới hạn của bạn. " +
                           $"Chiếu hết trong tối đa {maxMoves} nước là đạt, kể cả ở nước cuối cùng được phép đi. " +
                           "Hết giờ, dùng hết số nước mà chưa chiếu hết, bị chiếu hết hoặc hòa đều là chưa đạt. " +
                           "Đồng hồ chỉ chạy trong lượt suy nghĩ của bạn. " +
                           "Điểm gồm chất lượng từng nước (70%) và số nước đã dùng (30%); dùng gợi ý chỉ rõ nước đi giới hạn hạng tối đa ở A.";
            return MinigameGuidePages.Pages(
                new TutorialStep("MỤC TIÊU", goal),
                new TutorialStep("ĐIỀU KHIỂN", Controls),
                new TutorialStep("LUẬT", rules));
        }
    }
}
