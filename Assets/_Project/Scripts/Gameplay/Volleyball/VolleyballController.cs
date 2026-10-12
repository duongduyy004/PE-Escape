using System;
using System.Collections.Generic;
using KMA.Gameplay.UI;
using UnityEngine;

namespace KMA.Gameplay.Volleyball
{
    public sealed class VolleyballController : MinigameBase, IChallengeController, IMinigameGuideSource
    {
        public override bool UsesSharedTutorial => false;

        public const float MaxStep = .1f;

        [SerializeField] VolleyAthleteView playerView;
        [SerializeField] VolleyAthleteView opponentView;
        [SerializeField] VolleyBallView ballView;
        [SerializeField] VolleyballInputBridge input;
        [SerializeField] VolleyballHud hud;
        float stepDistance;
        VolleyballChallengeRules challengeRules;
        ChallengeDefinition challengeDefinition;
        ChallengeAttemptContext challengeContext;

        public VolleyballMatch Match { get; private set; }
        public MinigameResult LastResult { get; private set; }
        public VolleyAthleteView PlayerView => playerView;
        public VolleyAthleteView OpponentView => opponentView;
        public VolleyBallView BallView => ballView;
        public VolleyballInputBridge Input => input;
        public VolleyballHud Hud => hud;
        public bool HasAllReferences => playerView && opponentView && ballView && input && hud;
        public SubjectId Subject => SubjectId.Volleyball;
        public string GuideKey => VolleyballGuide.Key;
        public IReadOnlyList<TutorialStep> BuildGuide() => VolleyballGuide.Build(challengeDefinition?.Kind,
            challengeDefinition?.TargetCount ?? 0, Match.WinningPoints, Match.ClockLimit);
        public event Action<ChallengeAttemptResult> ChallengeCompleted;
        public ChallengeDefinition CurrentChallenge => challengeDefinition;

        public void Configure(VolleyAthleteView player, VolleyAthleteView opponent, VolleyBallView ball,
            VolleyballInputBridge inputBridge, VolleyballHud volleyballHud)
        {
            playerView = player;
            opponentView = opponent;
            ballView = ball;
            input = inputBridge;
            hud = volleyballHud;
        }

        protected override void Awake()
        {
            base.Awake();
            VolleyballCameraFraming.Apply(Camera.main);
            Match = new VolleyballMatch();
            SubscribeMatch(Match);
            PhaseChanged += OnPhaseChanged;
        }

        public void ConfigureChallenge(ChallengeDefinition definition, ChallengeAttemptContext context)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (definition.Subject != SubjectId.Volleyball || definition.Id != context.ChallengeId)
                throw new ArgumentException("Volleyball controller received a mismatched challenge context.");
            // PhaseOverlay may open the start gate before the router binds the scene (Awake/OnEnable
            // order is not fixed on device), so the countdown can already be running: still pre-play.
            if (Lifecycle != null && Lifecycle.Phase != MinigamePhase.Tutorial &&
                Lifecycle.Phase != MinigamePhase.Countdown)
                throw new InvalidOperationException("Volleyball challenge must be configured before play starts.");
            UnsubscribeMatch(Match);
            challengeDefinition = definition;
            challengeContext = context;
            challengeRules = new VolleyballChallengeRules(definition);
            Match = challengeRules.Match;
            LastResult = null;
            SubscribeMatch(Match);
            if (hud) hud.ConfigureChallenge(definition, challengeRules);
        }

        void SubscribeMatch(VolleyballMatch match)
        {
            match.Completed += OnMatchCompleted;
            match.PlayerActed += OnPlayerActed;
            match.PointScored += OnPointScored;
            match.PlayerBlocked += OnPlayerBlocked;
        }

        void UnsubscribeMatch(VolleyballMatch match)
        {
            if (match == null) return;
            match.Completed -= OnMatchCompleted;
            match.PlayerActed -= OnPlayerActed;
            match.PointScored -= OnPointScored;
            match.PlayerBlocked -= OnPlayerBlocked;
        }

        void Start()
        {
            if (HasAllReferences)
                return;

            Debug.LogError("[KMA] VolleyballController is missing a scene reference; disabling.", this);
            enabled = false;
        }

        void OnDestroy()
        {
            PhaseChanged -= OnPhaseChanged;
            if (Match == null)
                return;

            UnsubscribeMatch(Match);
        }

        protected override void TickPlay(float dt)
        {
            float step = Mathf.Min(dt, MaxStep);
            if (step <= 0f)
            {
                // Paused (timeScale 0): drop presses rather than replaying them on resume.
                input.ClearPresses();
                return;
            }

            BallFlight previousFlight = Match.Flight;
            Vector2 previousPosition = Match.Player.Position;
            if (challengeRules != null) challengeRules.SetMove(input.Move);
            else Match.SetMove(input.Move);
            // Jumps first: two thumbs landing in one frame mean "take off, then hit".
            for (int jumps = input.ConsumeJumps(); jumps > 0; jumps--)
            {
                if (challengeRules != null) challengeRules.PressJump();
                else Match.PressJump();
            }
            for (int presses = input.ConsumePresses(); presses > 0; presses--)
            {
                if (challengeRules != null) challengeRules.PressAction();
                else Match.PressAction();
            }
            if (challengeRules != null) challengeRules.Tick(step);
            else Match.Tick(step);
            if (challengeRules != null && challengeDefinition.Kind != ChallengeKind.Exam &&
                challengeRules.IsComplete && PresentationPhase == MinigamePhase.Play)
            {
                ChallengeAttemptResult completed = challengeRules.BuildResult(challengeContext);
                LastResult = completed.ExamResult ?? new MinigameResult(completed.Pass, 1f,
                    completed.Pass ? KMA.Gameplay.Rank.C : KMA.Gameplay.Rank.F);
                Finish(LastResult);
            }
            if (Match.Flight != previousFlight && Match.BallState == BallState.InPlay)
                GameAudio.Play(GameSound.VolleyHit);
            float moved = Vector2.Distance(previousPosition, Match.Player.Position);
            // Ignore the between-point reset that teleports the athlete to the serve spot.
            if (moved <= VolleyballMatch.PlayerSpeed * step * 1.5f)
                stepDistance += moved;
            if (stepDistance >= .85f)
            {
                stepDistance %= .85f;
                GameAudio.Play(GameSound.RunStep);
            }
        }

        void LateUpdate()
        {
            playerView.Render(Match.Player);
            opponentView.Render(Match.Opponent);
            ballView.Render(Match);
            hud.Render(Match, PresentationPhase, Time.deltaTime);
            bool controlsEnabled = PresentationPhase == MinigamePhase.Play && !Match.IsOver &&
                                   (challengeRules == null || !challengeRules.IsPresentationPending);
            if (input && input.ActionButton) input.ActionButton.SetInteractable(controlsEnabled);
            if (input && input.JumpButton)
            {
                input.JumpButton.SetInteractable(controlsEnabled);
                input.JumpButton.SetCue(controlsEnabled && Match.TryGetJumpCue(out _));
            }
        }

        protected override MinigameHudState BuildHudState() => new MinigameHudState(
            PresentationPhase.ToString(),
            Match == null ? 0f : Match.TimeRemaining,
            challengeRules == null || challengeDefinition.Kind == ChallengeKind.Exam ? 0f
                : Mathf.Clamp01(challengeRules.CompletedTargets / (float)Mathf.Max(1, challengeDefinition.TargetCount)),
            0f,
            Match == null ? 0f : Match.PlayerPoints,
            string.Empty);

        public void SkipToPlayForTest()
        {
            SetTutorialGate(false);
            Lifecycle.Tick(float.MaxValue);
        }

        void OnPhaseChanged(MinigamePhase phase)
        {
            if (phase == MinigamePhase.Play && input)
                input.ClearPresses();
        }

        void OnPlayerActed(ActionDecision decision)
        {
            if (hud)
                hud.ShowFeedback(decision);
        }

        void OnPlayerBlocked()
        {
            if (hud)
                hud.ShowBlock();
        }

        void OnPointScored(CourtSide winner)
        {
            if (Match.PlayerPoints < Match.WinningPoints && Match.OpponentPoints < Match.WinningPoints)
                GameAudio.Play(winner == CourtSide.Player ? GameSound.Point : GameSound.Miss);
            if (hud)
                hud.ShowPoint(winner);
        }

        void OnMatchCompleted()
        {
            if (challengeRules != null && challengeContext != null)
            {
                ChallengeAttemptResult result = challengeRules.BuildResult(challengeContext);
                LastResult = result.ExamResult ?? new MinigameResult(result.Pass, result.Pass ? 1f : 0f,
                    result.Pass ? KMA.Gameplay.Rank.C : KMA.Gameplay.Rank.F);
            }
            else LastResult = Match.BuildResult();
            Finish(LastResult);
        }

        protected override void OnResultResolved(MinigameResult result)
        {
            if (challengeRules == null || challengeContext == null)
            {
                base.OnResultResolved(result);
                return;
            }
            ChallengeCompleted?.Invoke(challengeRules.BuildResult(challengeContext));
        }
    }
}
