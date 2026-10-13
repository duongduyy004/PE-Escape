using System;
using System.Collections.Generic;
using System.Linq;

namespace KMA.Gameplay.Chess
{
    public readonly struct PuzzleAnalysis
    {
        public PuzzleAnalysis(SearchStatus status, int optimalMoves, double branching, int quietMoves)
        {
            Status = status;
            OptimalMoves = optimalMoves;
            Branching = branching;
            QuietMoves = quietMoves;
        }

        public SearchStatus Status { get; }
        /// Player moves of the optimal forced mate; 0 when none exists within the limit.
        public int OptimalMoves { get; }
        /// B in spec section 5: mean log2(1 + legal moves) over non-terminal states of the optimal proof tree.
        public double Branching { get; }
        /// Q in spec section 5: most quiet (no capture, no check) attacker moves on one optimal line.
        public int QuietMoves { get; }
    }

    public static class ChessPuzzleAnalyzer
    {
        sealed class UnknownException : Exception { }

        public static PuzzleAnalysis Analyze(ChessSearchEngine engine, ChessPosition start, int maxMoves)
        {
            MateSearch root = engine.MateDistance(start, maxMoves);
            if (root.Unknown) return new PuzzleAnalysis(SearchStatus.Unknown, 0, 0, 0);
            if (!root.Forced) return new PuzzleAnalysis(SearchStatus.Complete, 0, 0, 0);

            var logs = new List<double>();
            var seen = new HashSet<string>();
            var memo = new Dictionary<string, int>();
            try
            {
                int quiet = Walk(engine, start, root.Distance, seen, logs, memo);
                return new PuzzleAnalysis(SearchStatus.Complete, root.Distance, logs.Average(), quiet);
            }
            catch (UnknownException)
            {
                return new PuzzleAnalysis(SearchStatus.Unknown, 0, 0, 0);
            }
        }

        /// Attacker to move with a known shortest mate `distance`; returns the most quiet attacker moves on a line.
        static int Walk(ChessSearchEngine engine, ChessPosition position, int distance, HashSet<string> seen,
            List<double> logs, Dictionary<string, int> memo)
        {
            string key = ChessSearchEngine.PositionKey(position);
            if (memo.TryGetValue(key, out int known)) return known;

            List<ChessMove> legal = MoveGenerator.LegalMoves(position);
            if (seen.Add("A" + key)) logs.Add(Math.Log(1 + legal.Count, 2));

            int best = 0;
            foreach (ChessMove move in legal)
            {
                ChessPosition after = position.Apply(move);
                MateSearch result = engine.MoveDistance(after, distance);
                if (result.Unknown) throw new UnknownException();
                if (result.Distance != distance) continue; // not an optimal move

                int line = IsQuiet(position, move, after) ? 1 : 0;
                if (!MoveGenerator.IsCheckmate(after))
                {
                    List<ChessMove> defences = MoveGenerator.LegalMoves(after);
                    if (seen.Add("D" + ChessSearchEngine.PositionKey(after))) logs.Add(Math.Log(1 + defences.Count, 2));
                    int deepest = 0;
                    foreach (ChessMove defence in defences)
                    {
                        ChessPosition next = after.Apply(defence);
                        MateSearch follow = engine.MateDistance(next, distance - 1);
                        if (follow.Unknown) throw new UnknownException();
                        if (!follow.Forced)
                            throw new InvalidOperationException("Optimal move has a defence that escapes: " + next.ToFen());
                        deepest = Math.Max(deepest, Walk(engine, next, follow.Distance, seen, logs, memo));
                    }
                    line += deepest;
                }
                best = Math.Max(best, line);
            }

            memo[key] = best;
            return best;
        }

        static bool IsQuiet(ChessPosition before, ChessMove move, ChessPosition after)
        {
            bool capture = before[move.To] != 0 ||
                (Piece.TypeOf(before[move.From]) == Piece.Pawn && move.To == before.EnPassant);
            return !capture && !MoveGenerator.IsInCheck(after);
        }
    }
}
