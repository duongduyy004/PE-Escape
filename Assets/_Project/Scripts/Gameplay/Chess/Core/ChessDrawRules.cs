using System.Collections.Generic;

namespace KMA.Gameplay.Chess
{
    public enum DrawReason { None, Stalemate, Repetition, FiftyMove, InsufficientMaterial }

    public sealed class PositionHistory
    {
        readonly Dictionary<string, int> counts = new Dictionary<string, int>();

        /// Returns how many times this position (counters ignored) has occurred, this call included.
        public int Record(ChessPosition position)
        {
            string key = ChessSearchEngine.PositionKey(position);
            counts.TryGetValue(key, out int seen);
            counts[key] = ++seen;
            return seen;
        }

        public void Clear() => counts.Clear();
    }

    public static class ChessDrawRules
    {
        /// Checkmate is not a draw. `repetitions` is the count returned by PositionHistory.Record for this position.
        public static DrawReason Evaluate(ChessPosition position, int repetitions)
        {
            if (MoveGenerator.IsStalemate(position)) return DrawReason.Stalemate;
            if (repetitions >= 3) return DrawReason.Repetition;
            if (position.HalfmoveClock >= 100) return DrawReason.FiftyMove;
            if (IsInsufficientMaterial(position)) return DrawReason.InsufficientMaterial;
            return DrawReason.None;
        }

        public static bool IsInsufficientMaterial(ChessPosition position)
        {
            int minors = 0;
            int lightBishops = 0, darkBishops = 0, knights = 0;
            for (int square = 0; square < 64; square++)
            {
                sbyte piece = position[square];
                if (piece == 0) continue;
                int type = Piece.TypeOf(piece);
                if (type == Piece.Pawn || type == Piece.Rook || type == Piece.Queen) return false;
                if (type == Piece.Knight) { minors++; knights++; }
                else if (type == Piece.Bishop)
                {
                    minors++;
                    if ((Square.File(square) + Square.Rank(square)) % 2 == 0) darkBishops++; else lightBishops++;
                }
            }
            if (minors <= 1) return true;
            return knights == 0 && (lightBishops == 0 || darkBishops == 0); // only same-coloured bishops
        }
    }
}
