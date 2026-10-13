using System;
using System.Collections.Generic;
using System.Linq;

namespace KMA.Gameplay.Chess
{
    public sealed class HintRequest
    {
        public int Id;
        public string Fen;
        /// Player moves left, the coming one included.
        public int MovesLeft;
        public int Level;
    }

    public readonly struct ChessHint
    {
        public ChessHint(int id, SearchStatus status, int level, bool mateExists, bool namesMove, ChessMove move,
            string text)
        {
            Id = id;
            Status = status;
            Level = level;
            MateExists = mateExists;
            NamesMove = namesMove;
            Move = move;
            Text = text;
        }

        public int Id { get; }
        public SearchStatus Status { get; }
        public int Level { get; }
        public bool MateExists { get; }
        /// True for level 3: the hint names an explicit move, which caps the final score.
        public bool NamesMove { get; }
        public ChessMove Move { get; }
        public string Text { get; }
    }

    public static class ChessHintBuilder
    {
        public const int MaxLevel = 3;

        public static ChessHint Build(ChessSearchEngine engine, HintRequest request)
        {
            int level = Math.Min(MaxLevel, Math.Max(1, request.Level));
            ChessPosition position = ChessPosition.FromFen(request.Fen);
            MateSearch best = engine.MateDistance(position, request.MovesLeft);
            if (best.Unknown) return Unknown(request.Id, level);

            ChessMove move;
            string idea;
            if (best.Forced)
            {
                if (!TryFindOptimal(engine, position, best.Distance, request.MovesLeft, out move))
                    return Unknown(request.Id, level);
                bool check = MoveGenerator.IsInCheck(position.Apply(move));
                idea = check
                    ? $"Có đường chiếu hết trong {best.Distance} nước. Nhìn các nước chiếu trước."
                    : $"Có đường chiếu hết trong {best.Distance} nước. Nước đầu không chiếu: hãy khóa đường thoát của vua đen.";
            }
            else
            {
                move = SafeWaitingMove(position);
                idea = "Không còn đường chiếu hết bắt buộc trong số nước còn lại. Hãy giữ quân an toàn và tránh thế hòa.";
            }

            string piece = PieceName(Piece.TypeOf(position[move.From]));
            string text = level == 1
                ? idea
                : level == 2
                    ? $"Xem lại quân {piece} ở {Square.Name(move.From)}."
                    : $"Đi {piece} từ {Square.Name(move.From)} đến {Square.Name(move.To)}" +
                      (move.Promotion != 0 ? $" và phong thành {PieceName(move.Promotion)}" : string.Empty) +
                      ". Tôi đã chỉ rõ rồi.";
            return new ChessHint(request.Id, SearchStatus.Complete, level, best.Forced, level == 3, move, text);
        }

        public static string PieceName(int type) => type switch
        {
            Piece.Pawn => "Tốt",
            Piece.Knight => "Mã",
            Piece.Bishop => "Tượng",
            Piece.Rook => "Xe",
            Piece.Queen => "Hậu",
            _ => "Vua"
        };

        /// First optimal move in UCI order, so the same board always gives the same hint.
        /// False when the budget ran out before one was found.
        static bool TryFindOptimal(ChessSearchEngine engine, ChessPosition position, int distance, int movesLeft,
            out ChessMove move)
        {
            move = default;
            foreach (ChessMove candidate in MoveGenerator.LegalMoves(position)
                         .OrderBy(m => m.ToUci(), StringComparer.Ordinal))
            {
                ChessPosition after = position.Apply(candidate);
                if (MoveGenerator.IsCheckmate(after)) { if (distance == 1) { move = candidate; return true; } continue; }
                MateSearch result = engine.MoveDistance(after, movesLeft);
                if (result.Unknown) return false;
                if (result.Forced && result.Distance == distance) { move = candidate; return true; }
            }
            return false;
        }

        /// A legal move that neither stalemates nor triggers an automatic draw; checks are preferred, then UCI order.
        static ChessMove SafeWaitingMove(ChessPosition position)
        {
            List<ChessMove> ordered = MoveGenerator.LegalMoves(position).OrderBy(m => m.ToUci(), StringComparer.Ordinal).ToList();
            List<ChessMove> safe = ordered.Where(m => ChessDrawRules.Evaluate(position.Apply(m), 1) == DrawReason.None).ToList();
            if (safe.Count == 0) safe = ordered;
            foreach (ChessMove candidate in safe)
                if (MoveGenerator.IsInCheck(position.Apply(candidate))) return candidate;
            return safe[0];
        }

        static ChessHint Unknown(int id, int level) =>
            new ChessHint(id, SearchStatus.Unknown, level, false, false, default, null);
    }
}
