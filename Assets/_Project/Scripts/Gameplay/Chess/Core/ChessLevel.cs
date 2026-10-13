namespace KMA.Gameplay.Chess
{
    public enum ChessLevel { Easy, Normal, Hard }

    /// N = player moves of the optimal mate, M = player moves allowed. Spec section 3.
    public readonly struct ChessLevelRules
    {
        ChessLevelRules(int optimalMoves, int maxMoves)
        {
            OptimalMoves = optimalMoves;
            MaxMoves = maxMoves;
        }

        public int OptimalMoves { get; }
        public int MaxMoves { get; }

        public static ChessLevelRules For(ChessLevel level) => level switch
        {
            ChessLevel.Easy => new ChessLevelRules(1, 2),
            ChessLevel.Normal => new ChessLevelRules(2, 3),
            _ => new ChessLevelRules(3, 5)
        };
    }
}
