using System.Collections.Generic;

namespace KMA.Gameplay.Chess
{
    public enum SearchStatus { Complete, Unknown }

    public readonly struct MateSearch
    {
        public MateSearch(SearchStatus status, int distance)
        {
            Status = status;
            Distance = distance;
        }

        public SearchStatus Status { get; }
        /// Attacker moves needed to force mate against best defence; 0 when none within the limit.
        public int Distance { get; }
        public bool Forced => Status == SearchStatus.Complete && Distance > 0;
        public bool Unknown => Status == SearchStatus.Unknown;
    }

    /// And/or proof search for forced mate. Attacker nodes need one winning move, defender nodes need every
    /// reply to lose. Only finished results enter the cache, so an aborted search never poisons it.
    /// Not thread-safe: use one engine per worker.
    public sealed class ChessSearchEngine
    {
        readonly Dictionary<string, bool> cache = new Dictionary<string, bool>();
        SearchBudget budget;

        public ChessSearchEngine(SearchBudget budget) => this.budget = budget;

        public void UseBudget(SearchBudget next) => budget = next;

        /// `position.SideToMove` is the attacker. Smallest k in 1..maxMoves with a forced mate in k attacker moves.
        public MateSearch MateDistance(ChessPosition position, int maxMoves)
        {
            try
            {
                for (int k = 1; k <= maxMoves; k++)
                    if (AttackerWins(position, k)) return new MateSearch(SearchStatus.Complete, k);
                return new MateSearch(SearchStatus.Complete, 0);
            }
            catch (SearchAbortedException)
            {
                return new MateSearch(SearchStatus.Unknown, 0);
            }
        }

        /// `after` is the position after an attacker move, defender to move. `maxMoves` counts that move.
        public MateSearch MoveDistance(ChessPosition after, int maxMoves)
        {
            try
            {
                for (int k = 1; k <= maxMoves; k++)
                    if (DefenderLoses(after, k)) return new MateSearch(SearchStatus.Complete, k);
                return new MateSearch(SearchStatus.Complete, 0);
            }
            catch (SearchAbortedException)
            {
                return new MateSearch(SearchStatus.Unknown, 0);
            }
        }

        /// Board, side, castling and en passant; move counters are left out so transpositions share entries.
        public static string PositionKey(ChessPosition position)
        {
            string fen = position.ToFen();
            int cut = fen.LastIndexOf(' ');
            cut = fen.LastIndexOf(' ', cut - 1);
            return fen.Substring(0, cut);
        }

        bool AttackerWins(ChessPosition position, int moves)
        {
            string key = PositionKey(position) + "|A" + moves;
            if (cache.TryGetValue(key, out bool known)) return known;
            budget.Charge();

            var quiet = new List<ChessPosition>();
            bool result = false;
            foreach (ChessMove move in MoveGenerator.LegalMoves(position))
            {
                ChessPosition after = position.Apply(move);
                if (MoveGenerator.IsInCheck(after))
                {
                    if (DefenderLoses(after, moves)) { result = true; break; }
                }
                else quiet.Add(after);
            }
            // Checkmate needs a check, so quiet moves are only useful with a move to spare.
            if (!result && moves > 1)
                foreach (ChessPosition after in quiet)
                    if (DefenderLoses(after, moves)) { result = true; break; }

            cache[key] = result;
            return result;
        }

        bool DefenderLoses(ChessPosition after, int moves)
        {
            string key = PositionKey(after) + "|D" + moves;
            if (cache.TryGetValue(key, out bool known)) return known;
            budget.Charge();

            List<ChessMove> defences = MoveGenerator.LegalMoves(after);
            bool result;
            if (defences.Count == 0) result = MoveGenerator.IsInCheck(after); // checkmate, not stalemate
            else if (moves <= 1) result = false;
            else
            {
                result = true;
                foreach (ChessMove defence in defences)
                    if (!AttackerWins(after.Apply(defence), moves - 1)) { result = false; break; }
            }

            cache[key] = result;
            return result;
        }
    }
}
