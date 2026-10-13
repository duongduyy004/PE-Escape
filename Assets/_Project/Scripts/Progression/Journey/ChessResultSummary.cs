using System;

namespace KMA.Gameplay
{
    [Serializable]
    public sealed class ChessMoveEntry
    {
        public string uci;
        public float q;
        public int best;
        public int chosen;
    }

    /// Everything the result screens and the save need about one chess attempt (spec sections 7 and 9).
    [Serializable]
    public sealed class ChessResultSummary
    {
        public const int CurrentScoringVersion = 1;

        public int scoringVersion;
        public int movesUsed;
        public int maxMoves;
        public int optimalMoves;
        public int allowedSeconds;
        public float usedSeconds;
        public bool hintUsed;
        public bool explicitHintUsed;
        public bool hintCapped;
        /// Mate, TimeUp, OutOfMoves, Mated or Draw.
        public string endReason;
        /// F, D, C, B, A or A+.
        public string gradeLabel;
        public float score;
        public string fen;
        public string source;
        public int seed;
        public int estimatorVersion;
        public ChessMoveEntry[] moves = Array.Empty<ChessMoveEntry>();

        public ChessResultSummary Copy()
        {
            var copy = (ChessResultSummary)MemberwiseClone();
            copy.moves = new ChessMoveEntry[moves?.Length ?? 0];
            for (int i = 0; i < copy.moves.Length; i++)
                copy.moves[i] = new ChessMoveEntry
                    { uci = moves[i].uci, q = moves[i].q, best = moves[i].best, chosen = moves[i].chosen };
            return copy;
        }
    }
}
