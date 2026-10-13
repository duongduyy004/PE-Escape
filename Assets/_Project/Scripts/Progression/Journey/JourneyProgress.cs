using System;
using System.Collections.Generic;
using System.Linq;

namespace KMA.Gameplay
{
    public sealed class JourneyProgress
    {
        readonly ChallengeCatalog catalog;
        readonly HashSet<string> completedChallengeIds = new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string, int> failCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        ChallengeAttemptContext activeAttempt;
        string lastCommittedAttemptId;
        JourneyResultData lastCommittedResult;
        FrogJumpPending pendingFrogJump;
        string lastAppliedFrogJumpId;
        int attemptsRemaining = GameSession.MaxLives;
        int supplementaryRounds;
        List<string> seenDialogueIds = new List<string>();
        JourneyChessRecordData chessBest = new JourneyChessRecordData();
        bool celebrationSeen;

        public JourneyProgress(ChallengeCatalog catalog)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public string CheckpointChallengeId =>
            catalog.Ordered.FirstOrDefault(x => !completedChallengeIds.Contains(x.Id))?.Id;

        public int AttemptsRemaining => attemptsRemaining;
        public int SupplementaryRounds => supplementaryRounds;
        public bool CourseComplete => catalog.Ordered.All(x => completedChallengeIds.Contains(x.Id));
        public JourneyChessRecordData ChessBest => chessBest.Copy();
        public bool CelebrationSeen => celebrationSeen;
        public ChallengeAttemptContext ActiveAttempt => activeAttempt;
        public ChallengeCatalog Catalog => catalog;
        public FrogJumpPending PendingFrogJump => pendingFrogJump;

        public int FailCount(string id) =>
            !string.IsNullOrEmpty(id) && failCounts.TryGetValue(id, out int count) ? count : 0;

        public void SetAttemptsRemaining(int value) =>
            attemptsRemaining = Math.Max(0, Math.Min(GameSession.MaxLives, value));

        public bool TryApplyFrogJump(string frogJumpId, bool reachedFinish)
        {
            if (pendingFrogJump == null || string.IsNullOrEmpty(frogJumpId) ||
                !string.Equals(frogJumpId, pendingFrogJump.Id, StringComparison.Ordinal) ||
                string.Equals(frogJumpId, lastAppliedFrogJumpId, StringComparison.Ordinal))
                return false;
            if (pendingFrogJump.SavesLife && !reachedFinish)
                attemptsRemaining = Math.Max(0, attemptsRemaining - 1);
            lastAppliedFrogJumpId = frogJumpId;
            pendingFrogJump = null;
            return true;
        }

        /// An unfinished frog jump only ever means one thing: it was lost.
        public bool ForfeitPendingFrogJump() =>
            pendingFrogJump != null && TryApplyFrogJump(pendingFrogJump.Id, false);

        public bool IsDialogueSeen(string key) => !string.IsNullOrWhiteSpace(key) &&
            seenDialogueIds.Contains(key, StringComparer.Ordinal);

        public void MarkDialogueSeen(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A dialogue seen key is required.", nameof(key));
            if (!IsDialogueSeen(key)) seenDialogueIds.Add(key);
        }

        public void UnmarkDialogueSeen(string key)
        {
            if (!string.IsNullOrWhiteSpace(key)) seenDialogueIds.RemoveAll(item => item == key);
        }

        public bool IsChallengeComplete(string id) =>
            !string.IsNullOrWhiteSpace(id) && completedChallengeIds.Contains(id);

        public bool IsChallengeUnlocked(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;
            if (CourseComplete || IsChallengeComplete(id))
                return catalog.Ordered.Any(x => x.Id == id);
            return string.Equals(CheckpointChallengeId, id, StringComparison.Ordinal);
        }

        public bool IsSubjectUnlocked(SubjectId subject)
        {
            switch (subject)
            {
                case SubjectId.Sprint:
                    return true;
                case SubjectId.Volleyball:
                    return IsChallengeComplete("sprint_exam");
                case SubjectId.Football:
                    return IsChallengeComplete("volleyball_exam");
                case SubjectId.Chess:
                    return IsChallengeComplete("soccer_exam");
                default:
                    return false;
            }
        }

        public bool TryBegin(string id, ChallengeAttemptMode mode, ChallengeDifficulty difficulty,
            out ChallengeAttemptContext context)
        {
            context = null;
            if (activeAttempt != null || pendingFrogJump != null ||
                !Enum.IsDefined(typeof(ChallengeAttemptMode), mode) ||
                !Enum.IsDefined(typeof(ChallengeDifficulty), difficulty))
            {
                return false;
            }

            ChallengeDefinition definition;
            try
            {
                definition = catalog.Get(id);
            }
            catch (KeyNotFoundException)
            {
                return false;
            }

            bool allowed = mode switch
            {
                ChallengeAttemptMode.Journey => id == CheckpointChallengeId &&
                    (!IsPenalizedKind(definition.Kind) || attemptsRemaining > 0),
                ChallengeAttemptMode.Review => IsChallengeComplete(id) && IsSubjectUnlocked(definition.Subject),
                ChallengeAttemptMode.FreePlay => CourseComplete,
                _ => false
            };
            if (!allowed)
                return false;

            if (definition.Subject == SubjectId.Football || definition.Kind == ChallengeKind.Final)
                difficulty = ChallengeDifficulty.Normal;

            if (mode == ChallengeAttemptMode.Journey && difficulty != definition.Difficulty) return false;

            context = new ChallengeAttemptContext(Guid.NewGuid().ToString("N"), id, mode, difficulty);
            activeAttempt = context;
            return true;
        }

        public JourneyCommitOutcome Apply(ChallengeAttemptResult result)
        {
            if (result == null || result.Context == null || activeAttempt == null ||
                !SameContext(activeAttempt, result.Context) ||
                string.Equals(result.Context.AttemptId, lastCommittedAttemptId, StringComparison.Ordinal))
            {
                return Outcome(false);
            }

            ChallengeDefinition definition = catalog.Get(activeAttempt.ChallengeId);
            if (activeAttempt.Mode == ChallengeAttemptMode.Journey)
            {
                if (result.Pass)
                {
                    completedChallengeIds.Add(definition.Id);
                    failCounts.Remove(definition.Id);
                }
                else if (IsPenalizedKind(definition.Kind))
                {
                    int count = FailCount(definition.Id) + 1;
                    failCounts[definition.Id] = count;
                    if (count >= 2)
                        attemptsRemaining = Math.Max(0, attemptsRemaining - 1);
                    pendingFrogJump = new FrogJumpPending(Guid.NewGuid().ToString("N"),
                        activeAttempt.AttemptId, definition.Id, count == 1);
                }
            }

            if (definition.Kind == ChallengeKind.Final && result.Pass && result.ExamResult != null &&
                result.ExamResult.Pass)
                OfferChessResult(result.Metrics, result.ExamResult.Score);

            lastCommittedAttemptId = activeAttempt.AttemptId;
            lastCommittedResult = JourneyResultData.FromResult(result);
            activeAttempt = null;
            return Outcome(true, definition.Kind == ChallengeKind.Final);
        }

        public void AbandonAttempt() => activeAttempt = null;

        /// The first celebration has played (or was skipped); later wins go to the map.
        public bool MarkCelebrationSeen()
        {
            if (!CourseComplete) return false;
            celebrationSeen = true;
            return true;
        }

        public void UnmarkCelebrationSeen() => celebrationSeen = false;

        void OfferChessResult(ChallengeMetrics metrics, float score)
        {
            if (chessBest.recorded && score <= chessBest.score) return;
            chessBest = new JourneyChessRecordData
            {
                recorded = true,
                score = score,
                thinkSeconds = Math.Max(0f, metrics.Elapsed),
                mistakes = Math.Max(0, metrics.Mistakes),
                hintUsed = metrics.HintUsed,
                scoringVersion = metrics.Chess?.scoringVersion ?? 0,
                movesUsed = metrics.Chess?.movesUsed ?? 0,
                optimalMoves = metrics.Chess?.optimalMoves ?? 0,
                maxMoves = metrics.Chess?.maxMoves ?? 0,
                allowedSeconds = metrics.Chess?.allowedSeconds ?? 0,
                gradeLabel = metrics.Chess?.gradeLabel
            };
        }

        public JourneyStateData ToData() => new JourneyStateData
        {
            completedChallengeIds = catalog.Ordered.Where(x => completedChallengeIds.Contains(x.Id))
                .Select(x => x.Id).ToList(),
            activeAttempt = JourneyAttemptData.FromContext(activeAttempt),
            lastCommittedAttemptId = lastCommittedAttemptId,
            lastCommittedResult = lastCommittedResult?.Copy(),
            failCounts = catalog.Ordered.Where(x => FailCount(x.Id) > 0)
                .Select(x => new JourneyFailCountData { challengeId = x.Id, count = FailCount(x.Id) }).ToList(),
            pendingFrogJump = JourneyFrogJumpData.FromPending(pendingFrogJump),
            lastAppliedFrogJumpId = lastAppliedFrogJumpId,
            supplementaryRounds = supplementaryRounds,
            seenDialogueIds = new List<string>(seenDialogueIds),
            chessBest = chessBest.Copy(),
            celebrationSeen = celebrationSeen
        };

        public void Restore(JourneyStateData data, int attemptsRemaining)
        {
            completedChallengeIds.Clear();
            if (data?.completedChallengeIds != null)
            {
                foreach (string id in data.completedChallengeIds)
                {
                    if (catalog.Ordered.Any(x => x.Id == id))
                        completedChallengeIds.Add(id);
                }
            }

            this.attemptsRemaining = Math.Max(0, Math.Min(GameSession.MaxLives, attemptsRemaining));
            supplementaryRounds = Math.Max(0, data?.supplementaryRounds ?? 0);
            lastCommittedAttemptId = data?.lastCommittedAttemptId;
            lastCommittedResult = data?.lastCommittedResult?.Copy();
            seenDialogueIds = data?.seenDialogueIds == null
                ? new List<string>()
                : new List<string>(data.seenDialogueIds);

            failCounts.Clear();
            if (data?.failCounts != null)
            {
                foreach (JourneyFailCountData entry in data.failCounts)
                {
                    if (entry != null && entry.count > 0 && IsPenalized(entry.challengeId))
                        failCounts[entry.challengeId] = entry.count;
                }
            }
            lastAppliedFrogJumpId = data?.lastAppliedFrogJumpId;
            pendingFrogJump = data?.pendingFrogJump?.ToPending();
            if (pendingFrogJump != null && (!IsPenalized(pendingFrogJump.FailedChallengeId) ||
                pendingFrogJump.Id == lastAppliedFrogJumpId))
                pendingFrogJump = null;
            activeAttempt = data?.activeAttempt?.ToContext();
            if (activeAttempt != null && (string.IsNullOrWhiteSpace(activeAttempt.AttemptId) ||
                !catalog.Ordered.Any(x => x.Id == activeAttempt.ChallengeId)))
            {
                activeAttempt = null;
            }

            JourneyChessRecordData best = data?.chessBest;
            chessBest = best == null || !best.recorded ? new JourneyChessRecordData() : new JourneyChessRecordData
            {
                recorded = true,
                score = Math.Max(0f, best.score),
                thinkSeconds = Math.Max(0f, best.thinkSeconds),
                mistakes = Math.Max(0, best.mistakes),
                hintUsed = best.hintUsed,
                scoringVersion = Math.Max(0, best.scoringVersion),
                movesUsed = Math.Max(0, best.movesUsed),
                optimalMoves = Math.Max(0, best.optimalMoves),
                maxMoves = Math.Max(0, best.maxMoves),
                allowedSeconds = Math.Max(0, best.allowedSeconds),
                gradeLabel = best.gradeLabel
            };
            celebrationSeen = (data?.celebrationSeen ?? false) && CourseComplete;
        }

        JourneyCommitOutcome Outcome(bool accepted, bool finalChallenge = false) => new JourneyCommitOutcome(accepted,
            CheckpointChallengeId, attemptsRemaining, pendingFrogJump != null,
            pendingFrogJump?.SavesLife ?? false, CourseComplete, finalChallenge);

        public static bool IsPenalizedKind(ChallengeKind kind) =>
            kind == ChallengeKind.Practice || kind == ChallengeKind.Exam;

        bool IsPenalized(string id) => !string.IsNullOrEmpty(id) &&
            catalog.Ordered.Any(x => x.Id == id && IsPenalizedKind(x.Kind));

        static bool SameContext(ChallengeAttemptContext left, ChallengeAttemptContext right) =>
            left != null && right != null && left.AttemptId == right.AttemptId &&
            left.ChallengeId == right.ChallengeId && left.Mode == right.Mode &&
            left.Difficulty == right.Difficulty;
    }
}
