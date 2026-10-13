using System;
using System.Diagnostics;
using System.Text;

namespace KMA.Gameplay.Chess
{
    public readonly struct ChessGeneratorLimits
    {
        public ChessGeneratorLimits(int totalNodes, int perCandidateNodes, int maxCandidates, double totalSeconds)
        {
            TotalNodes = totalNodes;
            PerCandidateNodes = perCandidateNodes;
            MaxCandidates = maxCandidates;
            TotalSeconds = totalSeconds;
        }

        public int TotalNodes { get; }
        public int PerCandidateNodes { get; }
        public int MaxCandidates { get; }
        public double TotalSeconds { get; }

        /// Spec section 3.2: 2 seconds and 200,000 nodes in total.
        public static ChessGeneratorLimits Default =>
            new ChessGeneratorLimits(SearchBudget.DefaultNodes, 30_000, 5_000, SearchBudget.DefaultSeconds);
    }

    public sealed class GenerationStats
    {
        public int Candidates;
        public int Nodes;
        public double ElapsedSeconds;
        public bool Success;
    }

    public static class ChessPuzzleGenerator
    {
        // Piece letters for the player's extras. The black side is a lone king.
        static readonly string[] EasySets = { "QR", "RR", "QB", "QN", "RB" };
        static readonly string[] NormalSets = { "QN", "QB", "RR", "RN", "RB" };
        static readonly string[] HardSets = { "QN", "QB", "QR", "RN", "RB", "RR" };
        static readonly int[] EdgeSquares = BuildEdgeSquares();

        /// Null when the budget ran out, the run was cancelled, or nothing verified. A candidate is only returned when
        /// the proof search finished and the shortest mate is exactly N.
        public static ChessPuzzleSnapshot TryGenerate(ChessLevel level, int seed, ChessGeneratorLimits limits,
            string avoidFen, Func<bool> isCancelled, out GenerationStats stats)
        {
            ChessLevelRules rules = ChessLevelRules.For(level);
            var rng = new Random(seed);
            var clock = Stopwatch.StartNew();
            stats = new GenerationStats();

            while (stats.Candidates < limits.MaxCandidates && stats.Nodes < limits.TotalNodes &&
                   clock.Elapsed.TotalSeconds < limits.TotalSeconds && !(isCancelled?.Invoke() ?? false))
            {
                stats.Candidates++;
                ChessPosition position = RandomPosition(level, rng);
                if (position == null) continue;
                string fen = position.ToFen();
                if (fen == avoidFen) continue;

                var budget = new SearchBudget(Math.Min(limits.PerCandidateNodes, limits.TotalNodes - stats.Nodes),
                    Math.Max(0.001, limits.TotalSeconds - clock.Elapsed.TotalSeconds), isCancelled);
                PuzzleAnalysis analysis = ChessPuzzleAnalyzer.Analyze(new ChessSearchEngine(budget), position,
                    rules.OptimalMoves);
                stats.Nodes += budget.Nodes;
                if (analysis.Status != SearchStatus.Complete || analysis.OptimalMoves != rules.OptimalMoves) continue;

                float branching = (float)analysis.Branching;
                stats.Success = true;
                stats.ElapsedSeconds = clock.Elapsed.TotalSeconds;
                return new ChessPuzzleSnapshot
                {
                    fen = fen,
                    level = level.ToString(),
                    seed = seed,
                    optimalMoves = rules.OptimalMoves,
                    maxMoves = rules.MaxMoves,
                    branching = branching,
                    quietMoves = analysis.QuietMoves,
                    timeSeconds = ChessTimeEstimator.Seconds(rules.OptimalMoves, branching, analysis.QuietMoves),
                    estimatorVersion = ChessTimeEstimator.Version,
                    source = ChessPuzzleSnapshot.GeneratedSource,
                    playerColor = "w"
                };
            }

            stats.ElapsedSeconds = clock.Elapsed.TotalSeconds;
            return null;
        }

        /// A legal setup with white to move, or null when the random placement is unusable.
        public static ChessPosition RandomPosition(ChessLevel level, Random rng)
        {
            string[] sets = level == ChessLevel.Easy ? EasySets : level == ChessLevel.Normal ? NormalSets : HardSets;
            string extras = sets[rng.Next(sets.Length)];

            var board = new sbyte[64];
            // Mates are far more common against a king on the edge.
            int blackKing = rng.NextDouble() < .6 ? EdgeSquares[rng.Next(EdgeSquares.Length)] : rng.Next(64);
            board[blackKing] = Piece.Make(Piece.King, PieceColor.Black);
            int whiteKing = FreeSquare(board, rng);
            if (Adjacent(whiteKing, blackKing)) return null;
            board[whiteKing] = Piece.Make(Piece.King, PieceColor.White);
            foreach (char letter in extras)
                board[FreeSquare(board, rng)] = Piece.Make(TypeOf(letter), PieceColor.White);

            ChessPosition position = ChessPosition.FromFen(ToFen(board));
            if (MoveGenerator.IsSquareAttacked(position, blackKing, PieceColor.White)) return null;
            return MoveGenerator.LegalMoves(position).Count == 0 ? null : position;
        }

        static int TypeOf(char letter) => letter switch
        {
            'Q' => Piece.Queen,
            'R' => Piece.Rook,
            'B' => Piece.Bishop,
            _ => Piece.Knight
        };

        static int FreeSquare(sbyte[] board, Random rng)
        {
            int square;
            do square = rng.Next(64); while (board[square] != 0);
            return square;
        }

        static bool Adjacent(int a, int b) =>
            Math.Abs(Square.File(a) - Square.File(b)) <= 1 && Math.Abs(Square.Rank(a) - Square.Rank(b)) <= 1;

        static string ToFen(sbyte[] board)
        {
            var text = new StringBuilder(64);
            for (int rank = 7; rank >= 0; rank--)
            {
                int empty = 0;
                for (int file = 0; file < 8; file++)
                {
                    sbyte piece = board[Square.At(file, rank)];
                    if (piece == 0) { empty++; continue; }
                    if (empty > 0) { text.Append(empty); empty = 0; }
                    text.Append(Piece.ToFen(piece));
                }
                if (empty > 0) text.Append(empty);
                if (rank > 0) text.Append('/');
            }
            return text.Append(" w - - 0 1").ToString();
        }

        static int[] BuildEdgeSquares()
        {
            var edge = new System.Collections.Generic.List<int>();
            for (int s = 0; s < 64; s++)
            {
                int f = Square.File(s), r = Square.Rank(s);
                if (f == 0 || f == 7 || r == 0 || r == 7) edge.Add(s);
            }
            return edge.ToArray();
        }
    }
}
