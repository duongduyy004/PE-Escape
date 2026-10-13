namespace KMA.Gameplay
{
    public enum ChallengeAttemptMode
    {
        Journey,
        Supplementary,
        Review,
        FreePlay
    }

    public sealed class ChallengeAttemptContext
    {
        public string AttemptId { get; }
        public string ChallengeId { get; }
        public ChallengeAttemptMode Mode { get; }
        public ChallengeDifficulty Difficulty { get; }

        public ChallengeAttemptContext(string attemptId, string challengeId,
            ChallengeAttemptMode mode, ChallengeDifficulty difficulty)
        {
            AttemptId = attemptId;
            ChallengeId = challengeId;
            Mode = mode;
            Difficulty = difficulty;
        }
    }

    public sealed class ChallengeMetrics
    {
        public float Distance { get; }
        public float Elapsed { get; }
        public int CompletedTargets { get; }
        public int Kicks { get; }
        public int Placement { get; }
        public int Mistakes { get; }
        public bool HintUsed { get; }
        /// Short Vietnamese result line a controller wants shown, or null.
        public string Detail { get; }
        /// The chess attempt breakdown, or null for every other challenge and for results saved before it existed.
        public ChessResultSummary Chess { get; }

        public ChallengeMetrics(float distance = 0f, float elapsed = 0f, int completedTargets = 0,
            int kicks = 0, int placement = 0, int mistakes = 0, bool hintUsed = false,
            string detail = null, ChessResultSummary chess = null)
        {
            Distance = distance;
            Elapsed = elapsed;
            CompletedTargets = completedTargets;
            Kicks = kicks;
            Placement = placement;
            Mistakes = mistakes;
            HintUsed = hintUsed;
            Detail = detail;
            Chess = chess;
        }
    }

    /// A one-shot hint from the router to the next scene: "this start is a retry of that challenge".
    public static class ChallengeRetryIntent
    {
        static string armedChallengeId;

        public static void Arm(string challengeId) => armedChallengeId = challengeId;

        public static void Clear() => armedChallengeId = null;

        public static bool Consume(string challengeId)
        {
            if (string.IsNullOrEmpty(challengeId) || armedChallengeId != challengeId) return false;
            armedChallengeId = null;
            return true;
        }
    }

    public sealed class FrogJumpPending
    {
        public string Id { get; }
        public string FailedAttemptId { get; }
        public string FailedChallengeId { get; }
        public bool SavesLife { get; }

        public FrogJumpPending(string id, string failedAttemptId, string failedChallengeId, bool savesLife)
        {
            Id = id;
            FailedAttemptId = failedAttemptId;
            FailedChallengeId = failedChallengeId;
            SavesLife = savesLife;
        }
    }
}
