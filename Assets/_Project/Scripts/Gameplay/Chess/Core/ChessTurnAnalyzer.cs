namespace KMA.Gameplay.Chess
{
    /// Immutable-by-convention data handed to a worker: only strings, ints and structs.
    public sealed class AnalysisRequest
    {
        public int Id;
        /// Position before the player's move.
        public string BeforeFen;
        public ChessMove Move;
        /// Player moves left before this move, this one included.
        public int MovesLeftBefore;
        public bool NeedDefence;
        public int Seed;
    }

    public sealed class AnalysisResult
    {
        public int Id;
        public SearchStatus Status;
        public MoveQuality Quality;
        public DefenceChoice Defence;
    }

    public static class ChessTurnAnalyzer
    {
        public static AnalysisResult Analyze(ChessSearchEngine engine, AnalysisRequest request)
        {
            ChessPosition before = ChessPosition.FromFen(request.BeforeFen);
            ChessPosition after = before.Apply(request.Move);

            // A mating move is the shortest possible mate, so it needs no proof and can never be blocked by a budget.
            MoveQuality quality = MoveGenerator.IsCheckmate(after)
                ? new MoveQuality(SearchStatus.Complete, 1.0, 1, 1)
                : ChessAttemptGrader.Evaluate(engine, before, request.Move, request.MovesLeftBefore);
            if (quality.Unknown) return Unknown(request.Id);

            DefenceChoice defence = default;
            if (request.NeedDefence)
            {
                defence = ChessOpponent.Choose(engine, after, request.MovesLeftBefore - 1, request.Seed);
                if (defence.Status == SearchStatus.Unknown) return Unknown(request.Id);
            }
            return new AnalysisResult { Id = request.Id, Status = SearchStatus.Complete, Quality = quality, Defence = defence };
        }

        static AnalysisResult Unknown(int id) => new AnalysisResult
        {
            Id = id,
            Status = SearchStatus.Unknown,
            Quality = new MoveQuality(SearchStatus.Unknown, 0, 0, 0)
        };
    }
}
