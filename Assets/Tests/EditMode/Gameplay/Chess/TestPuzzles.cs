using KMA.Gameplay.Chess;

namespace KMA.Tests.Gameplay.Chess
{
    internal static class TestPuzzles
    {
        public static ChessMove Uci(string text)
        {
            ChessMove.TryParseUci(text, out ChessMove move);
            return move;
        }
    }
}
