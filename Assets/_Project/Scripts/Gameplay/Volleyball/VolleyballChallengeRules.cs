using KMA.Gameplay;

namespace KMA.Gameplay.Volleyball
{
    public enum VolleyballLearnStage
    {
        Receive,
        Serve,
        JumpHit,
        Complete
    }

    public sealed class VolleyballChallengeRules
    {
        public const int LearnReceiveTarget = 3;
        public const int LearnServeTarget = 2;
        public const int LearnJumpHitTarget = 2;
        public const int LearnTargetTotal = LearnReceiveTarget + LearnServeTarget + LearnJumpHitTarget;
        // The model continues to simulate the ball during this window. It is intentionally
        // longer than the receive/serve lock and covers a full jump/landing on the final drill.
        public const float LearnPresentationSeconds = 1.05f;

        readonly ChallengeDefinition definition;
        int completedTargets;
        int stageTargets;
        float pendingSeconds;
        bool pendingReset;
        bool pendingStageAdvance;

        public VolleyballMatch Match { get; }
        public VolleyballLearnStage LearnStage { get; private set; } = VolleyballLearnStage.Receive;
        public int StageCompletedTargets => stageTargets;
        public int StageTarget => LearnStage switch
        {
            VolleyballLearnStage.Receive => LearnReceiveTarget,
            VolleyballLearnStage.Serve => LearnServeTarget,
            VolleyballLearnStage.JumpHit => LearnJumpHitTarget,
            _ => LearnJumpHitTarget
        };
        public bool IsPresentationPending => pendingReset;
        public string LearnProgressText => LearnStage switch
        {
            VolleyballLearnStage.Receive => $"ĐỠ BÓNG {stageTargets}/{LearnReceiveTarget}",
            VolleyballLearnStage.Serve => $"PHÁT BÓNG {stageTargets}/{LearnServeTarget}",
            VolleyballLearnStage.JumpHit => $"NHẢY & ĐÁNH {stageTargets}/{LearnJumpHitTarget}",
            _ => "HOÀN THÀNH HỌC BÓNG CHUYỀN"
        };
        public int CompletedTargets => definition.Kind == ChallengeKind.Practice ? Match.PlayerPoints : completedTargets;
        public bool IsComplete => definition.Kind == ChallengeKind.Learn
            ? LearnStage == VolleyballLearnStage.Complete && !pendingReset
            : Match.IsOver;

        public VolleyballChallengeRules(ChallengeDefinition definition)
        {
            this.definition = definition ?? throw new System.ArgumentNullException(nameof(definition));
            VolleyballMatchOptions options = definition.Kind switch
            {
                ChallengeKind.Learn => new VolleyballMatchOptions(0, 0f, false),
                // Practice is a race to its target against the clock; the opponent keeps the serve.
                ChallengeKind.Practice => new VolleyballMatchOptions(definition.TargetCount, definition.TimeLimit,
                    true, requirePointsToWin: true),
                _ => new VolleyballMatchOptions(definition.TargetCount, definition.TimeLimit,
                    requirePointsToWin: true)
            };
            Match = new VolleyballMatch(options: options);
            Match.TouchRegistered += OnTouchRegistered;
            if (definition.Kind == ChallengeKind.Learn)
                Match.ResetRally(CourtSide.Opponent);
        }

        public void SetMove(UnityEngine.Vector2 move) => Match.SetMove(move);
        public ActionDecision PressAction() => definition.Kind == ChallengeKind.Learn && pendingReset
            ? ActionDecision.None
            : Match.PressAction();
        public bool PressJump() => definition.Kind == ChallengeKind.Learn && pendingReset
            ? false
            : Match.PressJump();

        public void Tick(float dt)
        {
            if (dt <= 0f)
                return;

            Match.Tick(dt);
            if (!pendingReset)
                return;

            pendingSeconds -= dt;
            if (pendingSeconds > 0f)
                return;

            pendingReset = false;
            if (pendingStageAdvance && completedTargets >= LearnTargetTotal)
            {
                LearnStage = VolleyballLearnStage.Complete;
                return;
            }

            if (pendingStageAdvance)
            {
                if (LearnStage == VolleyballLearnStage.Receive)
                    LearnStage = VolleyballLearnStage.Serve;
                else if (LearnStage == VolleyballLearnStage.Serve)
                    LearnStage = VolleyballLearnStage.JumpHit;
                stageTargets = 0;
            }
            pendingStageAdvance = false;
            Match.ResetRally(LearnStage == VolleyballLearnStage.Serve ? CourtSide.Player : CourtSide.Opponent);
        }

        public ChallengeAttemptResult BuildResult(ChallengeAttemptContext context)
        {
            if (context == null || context.ChallengeId != definition.Id)
                throw new System.ArgumentException("Volleyball challenge context does not match its definition.");
            bool pass = definition.Kind == ChallengeKind.Learn
                ? IsComplete
                : Match.IsOver && Match.PlayerPoints >= Match.WinningPoints;
            MinigameResult score = definition.Kind == ChallengeKind.Exam ? Match.BuildResult() : null;
            return new ChallengeAttemptResult(context, pass, new ChallengeMetrics(elapsed: Match.Elapsed,
                completedTargets: CompletedTargets), score);
        }

        void OnTouchRegistered(CourtSide side, ActionDecision decision, int touchesBefore)
        {
            if (definition.Kind != ChallengeKind.Learn || side != CourtSide.Player || pendingReset)
                return;

            bool counted = LearnStage switch
            {
                VolleyballLearnStage.Receive => decision.Kind == ActionKind.Receive || decision.Kind == ActionKind.FreeBall,
                VolleyballLearnStage.Serve => decision.Kind == ActionKind.ServeHit,
                VolleyballLearnStage.JumpHit => decision.Kind == ActionKind.Smash,
                _ => false
            };
            if (!counted)
                return;

            stageTargets++;
            completedTargets++;
            pendingReset = true;
            pendingStageAdvance = stageTargets >= StageTarget;
            pendingSeconds = LearnPresentationSeconds;
        }
    }
}
