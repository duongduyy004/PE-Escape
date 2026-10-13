using System;
using System.Collections.Generic;
using System.Linq;

namespace KMA.Gameplay.Chess
{
    public readonly struct DefenceChoice
    {
        public DefenceChoice(SearchStatus status, bool hasMove, ChessMove move)
        {
            Status = status;
            HasMove = hasMove;
            Move = move;
        }

        public SearchStatus Status { get; }
        public bool HasMove { get; }
        public ChessMove Move { get; }
    }

    /// Picks the defender's reply (spec section 4): mate the attacker if possible; else a reply that escapes the
    /// mate; else the reply that delays it longest. Equal replies fall back to material, then a seeded tie-break.
    public static class ChessOpponent
    {
        static readonly int[] Values = { 0, 1, 3, 3, 5, 9, 0 };

        /// `attackerMovesLeft` is how many more moves the attacker may make (player moves left after the one just made).
        public static DefenceChoice Choose(ChessSearchEngine engine, ChessPosition position, int attackerMovesLeft,
            int seed)
        {
            List<ChessMove> legal = MoveGenerator.LegalMoves(position);
            if (legal.Count == 0) return new DefenceChoice(SearchStatus.Complete, false, default);

            var scored = new List<(ChessMove move, int rank, int material)>(legal.Count);
            foreach (ChessMove move in legal)
            {
                ChessPosition after = position.Apply(move);
                if (MoveGenerator.IsCheckmate(after)) return new DefenceChoice(SearchStatus.Complete, true, move);
                MateSearch search = engine.MateDistance(after, attackerMovesLeft);
                if (search.Unknown)
                {
                    // A reply already proven to escape the mate is as good as any, so play it rather than fail.
                    if (!scored.Any(s => s.rank == int.MaxValue)) return new DefenceChoice(SearchStatus.Unknown, false, default);
                    break;
                }
                int rank = search.Forced ? search.Distance : int.MaxValue; // escaping the mate is best
                scored.Add((move, rank, Material(after, position.SideToMove)));
            }

            int bestRank = scored.Max(s => s.rank);
            List<(ChessMove move, int rank, int material)> best = scored.Where(s => s.rank == bestRank).ToList();
            int bestMaterial = best.Max(s => s.material);
            List<ChessMove> ties = best.Where(s => s.material == bestMaterial).Select(s => s.move)
                .OrderBy(m => m.ToUci(), StringComparer.Ordinal).ToList();
            int pick = new Random(seed ^ Fnv(ChessSearchEngine.PositionKey(position))).Next(ties.Count);
            return new DefenceChoice(SearchStatus.Complete, true, ties[pick]);
        }

        static int Material(ChessPosition position, PieceColor side)
        {
            int total = 0;
            for (int square = 0; square < 64; square++)
            {
                sbyte piece = position[square];
                if (piece == 0) continue;
                int value = Values[Piece.TypeOf(piece)];
                total += Piece.ColorOf(piece) == side ? value : -value;
            }
            return total;
        }

        /// string.GetHashCode is not stable across runtimes; tests need a reproducible seed.
        static int Fnv(string text)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in text) hash = (hash ^ c) * 16777619;
                return (int)hash;
            }
        }
    }
}
