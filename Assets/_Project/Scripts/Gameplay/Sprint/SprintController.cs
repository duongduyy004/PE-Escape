using System.Collections.Generic;
using System;
using KMA.Gameplay.Core;
using KMA.Gameplay.UI;
using KMA.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KMA.Gameplay
{
    public sealed class SprintController : MinigameBase, IChallengeController, IMinigameGuideSource
    {
        public override bool UsesSharedTutorial => false;
        public override bool UsesSharedCountdown => false;
        public override bool OwnsStartGate => true;

        // Fastest roll lets the quickest rival finish around 15 s, slowest keeps the last one near 18 s.
        public const float DefaultMinRivalSpeed = 8.5f;
        public const float DefaultMaxRivalSpeed = 10f;
        public const float MaxRivalWaitSeconds = 8f;
        public const string WaitForRivalsText = "VỀ ĐÍCH! ĐỢI CÁC BẠN VỀ ĐÍCH...";

        [SerializeField] RivalPaceProfileAsset[] rivalProfiles;
        [SerializeField, Min(0f)] float minRivalSpeed = DefaultMinRivalSpeed;
        [SerializeField, Min(0f)] float maxRivalSpeed = DefaultMaxRivalSpeed;
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] bool directInputEnabled = true;
        [SerializeField] GameplayInputRouter inputRouter;
        [SerializeField] string leftInputAction = "SprintLeft";
        [SerializeField] string rightInputAction = "SprintRight";

        SprintRules rules;
        SprintChallengeRules challengeRules;
        ChallengeDefinition challengeDefinition;
        ChallengeAttemptContext challengeContext;
        InputAction leftAction;
        InputAction rightAction;
        bool terminalResolved;
        bool inputRouterSubscribed;
        int cadenceCombo;
        float stepDistance;
        float rivalWaitElapsed;
        bool learnDialogueSuspended;

        public bool IsWaitingForRivals => rules != null && rules.IsFinished && !terminalResolved;
        public bool InputActionsReady => directInputEnabled && leftAction != null && rightAction != null;
        public Side ExpectedSide => rules == null ? Side.Left : rules.ExpectedSide;
        public SprintSnapshot Snapshot => rules == null ? default : rules.Snapshot;
        public MinigameResult LastResult { get; private set; }
        public MinigamePhase Phase => Lifecycle == null ? MinigamePhase.Tutorial : Lifecycle.Phase;
        public string LeftInputAction => leftInputAction;
        public string RightInputAction => rightInputAction;
        public int Rank => rules == null ? 1 : rules.Rank;
        public string RankText => "VỊ TRÍ " + Mathf.Clamp(Rank, 1, 4);
        public int CadenceCombo => cadenceCombo;
        public int CorrectStreak => rules == null ? 0 : rules.CorrectStreak;
        public int BestStreak => challengeRules == null ? CorrectStreak : challengeRules.BestStreak;
        public float LearnTimeRemaining => challengeRules == null ? 0f : challengeRules.LearnTimeRemaining;
        public float LearnProgress01 => challengeRules == null ? 0f : challengeRules.LearnProgress01;
        public bool IsComboBoosting => rules != null && rules.IsComboBoosting;
        public float[] RivalDistances => rules == null ? System.Array.Empty<float>() : rules.RivalDistances;
        public SubjectId Subject => SubjectId.Sprint;
        public float TargetDistance => challengeDefinition != null && challengeDefinition.Distance > 0f
            ? challengeDefinition.Distance : SprintRules.RaceDistance;
        public float TargetTime => challengeDefinition != null && challengeDefinition.TimeLimit > 0f
            ? challengeDefinition.TimeLimit : SprintRules.DefaultTimeLimit;
        public int TargetCount => challengeDefinition == null ? 0 : challengeDefinition.TargetCount;
        public bool IsLearnChallenge => challengeDefinition != null && challengeDefinition.Kind == ChallengeKind.Learn;
        public event Action<ChallengeAttemptResult> ChallengeCompleted;
        public event Action<bool> CadenceTapped;
        public ChallengeDefinition CurrentChallenge => challengeDefinition;
        public int RivalCount => rules == null ? 0 : rules.RivalCount;
        public string GuideKey => SprintGuide.Key;
        public IReadOnlyList<TutorialStep> BuildGuide() =>
            SprintGuide.Build(challengeDefinition?.Kind, TargetCount, TargetDistance, TargetTime, RivalCount);
        public float GetRivalDistance(int index) => rules == null ? 0f : rules.GetRivalDistance(index);
        public bool IsRivalSurging(int index) => rules != null && rules.IsRivalSurging(index);
        public bool IsRivalSlowing(int index) => rules != null && rules.IsRivalSlowing(index);

        protected override void Awake()
        {
            base.Awake();
            rules = CreateRulesFromAuthoredProfiles();
            ConfigureInputActions();
        }

        public void ConfigureChallenge(ChallengeDefinition definition, ChallengeAttemptContext context)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (definition.Subject != SubjectId.Sprint || definition.Id != context.ChallengeId)
                throw new ArgumentException("Sprint controller received a mismatched challenge context.");
            if (Lifecycle != null && Lifecycle.Phase != MinigamePhase.Tutorial)
                throw new InvalidOperationException("Sprint challenge must be configured before the start gate opens.");

            challengeDefinition = definition;
            challengeContext = context;
            challengeRules = new SprintChallengeRules(definition, SprintBalanceConfig.LoadDefault().ToRuntime(),
                CreateRuntimeProfiles());
            rules = challengeRules.Race;
            terminalResolved = false;
            rivalWaitElapsed = 0f;
            learnDialogueSuspended = false;
            cadenceCombo = 0;
            stepDistance = 0f;
            LastResult = null;
            SprintFestivalPresentation.ConfigureChallenge(definition);
            SprintStartPresentation presentation = FindFirstObjectByType<SprintStartPresentation>(
                FindObjectsInactive.Include);
            presentation?.ConfigureInstruction(definition.Kind == ChallengeKind.Learn
                ? $"TẬP LUÂN PHIÊN HAI CHÂN TRONG {definition.TimeLimit:0} GIÂY"
                : $"CHẠY {definition.Distance:0} M TRONG {definition.TimeLimit:0} GIÂY");
        }

        void OnEnable()
        {
            ConfigureInputActions();
            SubscribeInputRouter();
        }

        void OnDisable()
        {
            UnsubscribeInputActions();
            UnsubscribeInputRouter();
        }

        void OnDestroy()
        {
            UnsubscribeInputActions();
            UnsubscribeInputRouter();
        }

        void ConfigureInputActions()
        {
            UnsubscribeInputActions();
            if (!directInputEnabled || inputActions == null)
                return;

            leftAction = inputActions.FindAction(leftInputAction, false);
            rightAction = inputActions.FindAction(rightInputAction, false);
            if (leftAction == null || rightAction == null)
                return;

            leftAction.performed += OnLeftActionPerformed;
            rightAction.performed += OnRightActionPerformed;
            inputActions.Enable();
        }

        void UnsubscribeInputActions()
        {
            if (leftAction != null)
                leftAction.performed -= OnLeftActionPerformed;
            if (rightAction != null)
                rightAction.performed -= OnRightActionPerformed;
            leftAction = null;
            rightAction = null;
        }

        void OnLeftActionPerformed(InputAction.CallbackContext context)
        {
            if (context.performed && Lifecycle.Phase == MinigamePhase.Play)
                OnLeftTap();
        }

        void OnRightActionPerformed(InputAction.CallbackContext context)
        {
            if (context.performed && Lifecycle.Phase == MinigamePhase.Play)
                OnRightTap();
        }

        public void ConfigureInputForTest(InputActionAsset actions)
        {
            inputActions = actions;
            ConfigureInputActions();
        }

        public void ConfigureInputRouterForTest(GameplayInputRouter router)
        {
            UnsubscribeInputRouter();
            inputRouter = router;
            SubscribeInputRouter();
        }

        void SubscribeInputRouter()
        {
            if (inputRouter == null || inputRouterSubscribed)
                return;

            inputRouter.OnSprintTap += OnRouterSprintTap;
            inputRouterSubscribed = true;
        }

        void UnsubscribeInputRouter()
        {
            if (!inputRouterSubscribed)
                return;

            inputRouter.OnSprintTap -= OnRouterSprintTap;
            inputRouterSubscribed = false;
        }

        void OnRouterSprintTap(KMA.Input.Side side)
        {
            if (Lifecycle.Phase != MinigamePhase.Play)
                return;

            if (side == KMA.Input.Side.Left)
                OnLeftTap();
            else
                OnRightTap();
        }

        /// The Learn familiarity clock advances only while the learner can play; Sprint teacher lines
        /// are shown on unscaled time, so suspend the lesson simulation until the line has finished.
        public void SetLearnDialogueSuspended(bool suspended) => learnDialogueSuspended = suspended;

        public void ConfigureForTest()
        {
            Lifecycle = new MinigameLifecycle(0f, 0f);
            Lifecycle.Tick(0f);
            Lifecycle.Tick(0f);
            rules = SprintRules.ForTest(0f, 0f, 1);
            terminalResolved = false;
            learnDialogueSuspended = false;
            cadenceCombo = 0;
            LastResult = null;
            challengeRules = null;
            challengeDefinition = null;
            challengeContext = null;
        }

        public void AdvanceToDistance(float value)
        {
            rules = SprintRules.ForTest(value, rules == null ? 0f : rules.Elapsed, 1);
        }

        public void Simulate(float dt)
        {
            Lifecycle.Tick(dt);
            if (Lifecycle.Phase == MinigamePhase.Play)
                TickPlay(dt);
        }

        public void OnLeftTap() => OnTap(Side.Left);

        public void OnRightTap() => OnTap(Side.Right);

        public MinigameResult BuildResult()
        {
            if (challengeRules != null && challengeContext != null)
            {
                ChallengeAttemptResult result = challengeRules.BuildResult(challengeContext);
                LastResult = result.ExamResult ?? new MinigameResult(result.Pass,
                    result.Pass ? result.Metrics.CompletedTargets : 0f,
                    result.Pass ? KMA.Gameplay.Rank.C : KMA.Gameplay.Rank.F);
            }
            else
                LastResult = rules.BuildResult();
            return LastResult;
        }

        protected override bool TryBeginResolve(bool passed) => base.TryBeginResolve(passed);

        protected override void OnResultResolved(MinigameResult result)
        {
            LastResult = result;
            if (challengeRules == null || challengeContext == null)
            {
                base.OnResultResolved(result);
                return;
            }
            ChallengeCompleted?.Invoke(challengeRules.BuildResult(challengeContext));
        }

        protected override MinigameHudState BuildHudState() => new MinigameHudState(
            phase: Phase.ToString(),
            timeRemaining: Mathf.Max(0f, rules == null ? 0f : TargetTime - rules.Elapsed),
            progress01: challengeDefinition != null && challengeDefinition.Kind == ChallengeKind.Learn
                ? (challengeRules == null ? 0f : challengeRules.LearnProgress01)
                : Mathf.Clamp01((rules == null ? 0f : rules.Snapshot.Distance) / TargetDistance),
            stamina01: 0f,
            score: 0f,
            statusText: IsWaitingForRivals ? WaitForRivalsText : challengeDefinition == null ? "TAP LEFT / RIGHT"
                : challengeDefinition.Kind == ChallengeKind.Learn
                    ? $"CHUỖI TỐT NHẤT {BestStreak}/{TargetCount}" : $"{TargetDistance:0} M · {TargetTime:0} S");
        protected override void TickPlay(float dt)
        {
            if (IsLearnChallenge && learnDialogueSuspended) return;
            float before = rules.Snapshot.Distance;
            if (challengeRules != null) challengeRules.Tick(dt);
            else rules.Tick(dt);
            if (rules.IsFinished) rivalWaitElapsed += dt;
            stepDistance += Mathf.Max(0f, rules.Snapshot.Distance - before);
            if (dt > 0f && stepDistance >= 1.6f)
            {
                stepDistance %= 1.6f;
                GameAudio.Play(GameSound.RunStep);
            }
            EvaluateTerminalOutcome();
        }

        void OnTap(Side side)
        {
            if (Time.timeScale <= 0f || Lifecycle.Phase != MinigamePhase.Play || rules.IsFinished ||
                (IsLearnChallenge && learnDialogueSuspended))
                return;

            Side expected = rules.ExpectedSide;
            if (challengeRules != null) challengeRules.Tap(side);
            else rules.Tap(side);
            cadenceCombo = side == expected ? cadenceCombo + 1 : 0;
            CadenceTapped?.Invoke(side == expected);
            EvaluateTerminalOutcome();
        }

        SprintRules CreateRulesFromAuthoredProfiles()
        {
            RivalPaceProfile[] runtimeProfiles = CreateRuntimeProfiles();
            if (runtimeProfiles.Length == 0) return SprintRules.Default();
            return new SprintRules(SprintRules.DefaultTimeLimit, runtimeProfiles);
        }

        RivalPaceProfile[] CreateRuntimeProfiles()
        {
            if (rivalProfiles == null || rivalProfiles.Length == 0) return Array.Empty<RivalPaceProfile>();
            var authored = new RivalPaceProfile[rivalProfiles.Length];
            for (var i = 0; i < rivalProfiles.Length; i++)
                authored[i] = rivalProfiles[i] == null ? null : rivalProfiles[i].ToRuntime();
            return RivalPaceRandomizer.RollField(authored, minRivalSpeed, maxRivalSpeed, new System.Random());
        }

        void EvaluateTerminalOutcome()
        {
            if (terminalResolved || Lifecycle.Phase != MinigamePhase.Play)
                return;

            if (rules.IsFinished)
            {
                if (rivalWaitElapsed >= MaxRivalWaitSeconds || rules.AllRivalsReached(TargetDistance))
                    Resolve();
                return;
            }

            bool learn = IsLearnChallenge;
            bool finished = learn ? challengeRules.IsComplete : rules.Snapshot.Distance >= TargetDistance;
            bool timedOut = !learn && rules.Snapshot.Elapsed >= TargetTime;
            if (!finished && !timedOut)
                return;

            if (!learn && finished && rules.RivalCount > 0 && !rules.AllRivalsReached(TargetDistance))
            {
                // Lock the player's result and let the rivals run in before the result card opens.
                rules.FinishRace();
                rivalWaitElapsed = 0f;
                return;
            }

            Resolve();
        }

        void Resolve()
        {
            terminalResolved = true;
            Finish(BuildResult());
        }
    }
}
