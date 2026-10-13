using System;

namespace KMA.Gameplay.Chess
{
    /// Everything needed to rebuild a puzzle and its grading context (spec sections 5 and 9).
    /// Plain public fields so JsonUtility can store it in a save file.
    [Serializable]
    public sealed class ChessPuzzleSnapshot
    {
        public const string GeneratedSource = "Generated";
        public const string FallbackSource = "Fallback";

        public string fen;
        public string level;
        public int seed;
        public int optimalMoves;
        public int maxMoves;
        public int timeSeconds;
        public float branching;
        public int quietMoves;
        public int estimatorVersion;
        public string source;
        public string playerColor = "w";

        public ChessLevel Level => Enum.TryParse(level, out ChessLevel parsed) ? parsed : ChessLevel.Normal;
        public ChessPosition StartPosition => ChessPosition.FromFen(fen);
    }
}
