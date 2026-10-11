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

        public static IReadOnlyList<TutorialStep> Build(int maxPlayerMoves, float timeLimitSeconds, int maxMistakes)
        {
            string goal = $"Chiếu hết trong {maxPlayerMoves} nước. Có {MinigameGuidePages.Number(timeLimitSeconds)} giây suy nghĩ. " +
                          "Trượt bài này không bị trừ lượt thi và không phải Bật cóc.";
            string rules = "Nước không hợp lệ không tính lỗi. Nước hợp lệ nhưng sai lời giải tính 1 lỗi, bàn cờ giữ nguyên. " +
                           $"Được sửa sai {maxMistakes} lần; sai lần thứ {maxMistakes + 1} hoặc hết giờ là chưa đạt. Đồng hồ chỉ chạy trong lượt của bạn. " +
                           "Đi sai, dùng gợi ý và dùng nhiều thời gian sẽ làm giảm điểm.";
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
