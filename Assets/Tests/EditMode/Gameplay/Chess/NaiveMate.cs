using System;
using KMA.Gameplay.Chess;

namespace KMA.Tests.Gameplay.Chess
{
    /// Brute-force mate distance. Attacker moves needed to force mate, int.MaxValue when not within `limit`.
    internal static class NaiveMate
    {
        public const int None = int.MaxValue;

        /// Attacker to move.
        public static int AttackerDistance(ChessPosition position, int limit)
        {
            int best = None;
            foreach (ChessMove move in MoveGenerator.LegalMoves(position))
            {
                int d = DefenderDistance(position.Apply(move), limit);
                if (d < best) best = d;
            }
            return best;
        }

        /// Defender to move; `limit` counts the attacker move that produced `after`.
        public static int DefenderDistance(ChessPosition after, int limit)
        {
            var defences = MoveGenerator.LegalMoves(after);
            if (defences.Count == 0) return MoveGenerator.IsInCheck(after) ? 1 : None;
            if (limit <= 1) return None;
            int worst = 0;
            foreach (ChessMove defence in defences)
            {
                int inner = AttackerDistance(after.Apply(defence), limit - 1);
                if (inner == None) return None;
                worst = Math.Max(worst, 1 + inner);
            }
            return worst;
        }
    }
}
