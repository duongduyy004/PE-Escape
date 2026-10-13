using System;
using UnityEngine;

namespace KMA.Gameplay.Core
{
    /// <summary>Bridges the existing minigame lifecycle into the persisted challenge contract.</summary>
    public sealed class JourneyControllerAdapter : MonoBehaviour, IChallengeController
    {
        MinigameBase source;
        ChallengeDefinition definition;
        ChallengeAttemptContext context;

        public ChallengeAttemptContext Context => context;
        public SubjectId Subject => definition == null ? default : definition.Subject;
        public event Action<ChallengeAttemptResult> ChallengeCompleted;
        public event Action Configured;

        void Awake()
        {
            source = GetComponent<MinigameBase>();
            if (source != null) source.Completed += OnCompleted;
        }

        void OnDestroy()
        {
            if (source != null) source.Completed -= OnCompleted;
        }

        public void ConfigureChallenge(ChallengeDefinition challenge, ChallengeAttemptContext attempt)
        {
            definition = challenge ?? throw new ArgumentNullException(nameof(challenge));
            context = attempt ?? throw new ArgumentNullException(nameof(attempt));
            Configured?.Invoke();
        }

        void OnCompleted(MinigameResult result)
        {
            if (context == null || definition == null || result == null) return;
            bool scored = ChallengeDefinition.IsScored(definition.Kind);
            ChallengeMetrics metrics = source is IChallengeMetricsSource rich
                ? rich.BuildMetrics(definition, result)
                : scored
                    ? new ChallengeMetrics(completedTargets: definition.TargetCount)
                    : new ChallengeMetrics(elapsed: Mathf.Max(0f, result.Score),
                        completedTargets: definition.TargetCount);
            ChallengeCompleted?.Invoke(new ChallengeAttemptResult(context, result.Pass, metrics,
                scored ? result : null));
        }
    }
}
