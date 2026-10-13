using System.Collections.Generic;
using KMA.Gameplay.UI;
using UnityEngine;

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
                ? $"Có {MinigameGuidePages.Number(timeSeconds)} giây suy nghĩ cho thế cờ này."
                : "Thời gian suy nghĩ được tính theo độ khó của thế cờ.";
            string goal = $"Chiếu hết trong tối đa {maxMoves} nước; có cách chiếu hết trong {optimalMoves} nước. {time} " +
                          "Trượt bài này không bị trừ lượt thi và không phải Bật cóc.";
            string rules = "Mọi nước đi hợp lệ đều được tính và không thể đi lại. Nước không hợp lệ không tính nước. " +
                           "Đồng hồ chỉ chạy khi đến lượt bạn nghĩ. Hết giờ, hết nước, bị chiếu hết hoặc hòa là chưa đạt. " +
                           "Điểm gồm chất lượng từng nước (70%) và số nước đã dùng (30%); dùng gợi ý chỉ rõ nước đi giới hạn hạng tối đa ở A.";
            return MinigameGuidePages.Pages(
                new TutorialStep("MỤC TIÊU", goal),
                new TutorialStep("ĐIỀU KHIỂN", Controls),
                new TutorialStep("LUẬT", rules));
        }

        public static string TimeWords(float seconds) => Mathf.RoundToInt(seconds) switch
        {
            60 => "một phút",
            90 => "một phút rưỡi",
            120 => "hai phút",
            int s => $"{s} giây"
        };
    }
}
