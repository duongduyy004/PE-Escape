using System.Globalization;
using System.Text;

namespace KMA.Gameplay
{
    public static class ChessResultText
    {
        /// Two lines so it fits the result card: the outcome and numbers, then the hint state.
        public static string Detail(ChessResultSummary s)
        {
            if (s == null) return string.Empty;
            var text = new StringBuilder();
            string reason = EndReasonText(s.endReason);
            if (reason.Length > 0) text.Append(reason).Append(" · ");
            text.Append("Nước: ").Append(s.movesUsed).Append('/').Append(s.maxMoves)
                .Append(" · Tối ưu: ").Append(s.optimalMoves)
                .Append(" · ").Append(FormatClock(s.usedSeconds)).Append('/').Append(FormatClock(s.allowedSeconds));
            text.Append('\n').Append(HintText(s));
            return text.ToString();
        }

        public static string EndReasonText(string reason) => reason switch
        {
            "Mate" => "Chiếu hết",
            "TimeUp" => "Hết giờ",
            "OutOfMoves" => "Hết số nước",
            "Mated" => "Bị chiếu hết",
            "Draw" => "Hòa cờ",
            _ => string.Empty
        };

        public static string MoveLines(ChessResultSummary s, int max)
        {
            if (s?.moves == null || s.moves.Length == 0) return string.Empty;
            var text = new StringBuilder();
            for (int i = 0; i < s.moves.Length && i < max; i++)
            {
                if (i > 0) text.Append('\n');
                text.Append(i + 1).Append(". ").Append(s.moves[i].uci).Append(" · ")
                    .Append(Percent(s.moves[i].q));
            }
            if (s.moves.Length > max) text.Append("\n…");
            return text.ToString();
        }

        public static string FormatClock(float seconds)
        {
            int total = seconds <= 0f ? 0 : (int)seconds;
            return $"{total / 60:00}:{total % 60:00}";
        }

        static string HintText(ChessResultSummary s) =>
            s.hintCapped ? "Gợi ý chỉ rõ nước: hạng tối đa A" :
            s.hintUsed ? "Có dùng gợi ý" : "Không dùng gợi ý";

        // The share of the best distance kept by that move; not a claim about engine-strength accuracy.
        static string Percent(float q) => ((int)System.Math.Round(q * 100f)).ToString(CultureInfo.InvariantCulture) + "%";
    }
}
