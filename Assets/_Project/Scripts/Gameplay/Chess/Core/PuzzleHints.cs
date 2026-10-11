namespace KMA.Gameplay.Chess
{
    public static class PuzzleHints
    {
        public const int MaxLevel = 3;

        public static string For(PuzzleDefinition puzzle, int node, ChessPosition position, int level)
        {
            if (level <= 1) return puzzle.ideaHint;
            ChessMove.TryParseUci(puzzle.nodes[node].moves[0].uci, out ChessMove move);
            string piece = PieceName(Piece.TypeOf(position[move.From]));
            return level == 2
                ? $"Xem lại quân {piece} ở {Square.Name(move.From)}."
                : $"Đi {piece} từ {Square.Name(move.From)} đến {Square.Name(move.To)}. Tôi đã chỉ rõ rồi.";
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
    }
}
