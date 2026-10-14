using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using KMA.Gameplay.UI;
using KMA.Gameplay.Core;
using UnityEngine;

[assembly: InternalsVisibleTo("KMA.Gameplay.Football.PlayMode.Tests")]

namespace KMA.Gameplay
{
    public sealed class FootballController : MinigameBase, IChallengeController, IMinigameGuideSource
    {
        public override bool UsesSharedTutorial => false;
        public override bool OwnsStartGate => true;
        public override bool OwnsCameraBackground => true;

        static FootballDifficulty preferredDifficulty = FootballDifficulty.Normal;
        static bool retryRequested;

        [SerializeField] FootballDifficultyConfig difficultyConfig;
        [SerializeField] FootballInputBridge inputBridge;
        [SerializeField] FootballPresentation presentation;
        [SerializeField] FootballHud hud;
        [SerializeField] ResultPanel resultPanel;

        FootballRules rules;
        MinigameResult lastResult;
        FootballDifficulty selectedDifficulty = FootballDifficulty.Normal;
        bool ready;
        bool listenersBound;
        bool matchRequested;
        bool rulesStarted;
        bool resultSubmitted;
        int lastReportedKick = -1;
        int lastStartedKick = -1;
        bool appPaused;
        bool hasFocus = true;
        FootballFlightSimulation kickedFlight, soundedOutcomeFlight;
        ChallengeDefinition challengeDefinition;
        ChallengeAttemptContext challengeContext;

        public FootballRules Rules => rules;
        public float ShotTimeRemaining => rules?.ShotTimeRemaining ?? 0f;
        public string GuideKey => FootballGuide.Key;
        // Read from the live rules, because OptionsFor hardcodes the learn and exam numbers.
        public IReadOnlyList<TutorialStep> BuildGuide() => rules == null
            ? System.Array.Empty<TutorialStep>()
            : FootballGuide.Build(challengeDefinition?.Kind, rules.RequiredGoals, rules.MaximumKicks, rules.KeeperEnabled);
        public MinigameResult LastResult => lastResult;
        public SubjectId Subject => SubjectId.Football;
        public event Action<ChallengeAttemptResult> ChallengeCompleted;
        public event Action<FootballState> StatePresented;
        /// Raised when the whistle opens a new aiming/charging window.
        public event Action<int> ShotWindowStarted;
        /// Raised once for every completed shot, including timeout shots.
        public event Action<FootballOutcome> ShotResolved;
        public ChallengeDefinition CurrentChallenge => challengeDefinition;
        FootballState? presentedState;
        void NotifyPresentedState()
        {
            if (rules == null || presentedState == rules.State) return;
            presentedState=rules.State;
            StatePresented?.Invoke(rules.State);
        }

        public void Configure(FootballDifficultyConfig config, FootballInputBridge input,
            FootballPresentation view, FootballHud gameHud, ResultPanel result)
        {
            difficultyConfig = config;
            inputBridge = input;
            presentation = view;
            hud = gameHud;
            resultPanel = result;
        }

        public bool BeginMatch(FootballDifficulty difficulty)
        {
            if (!ready || matchRequested || rules == null || rules.State != FootballState.Start ||
                PresentationPhase != MinigamePhase.Tutorial)
                return false;

            difficulty = FootballDifficulty.Normal;
            selectedDifficulty = difficulty;
            preferredDifficulty = difficulty;
            rules = new FootballRules(difficultyConfig.Get(difficulty),
                challengeDefinition == null ? null : OptionsFor(challengeDefinition));
            matchRequested = true;
            rulesStarted = false;
            resultSubmitted = false;
            lastReportedKick = -1;
            lastStartedKick = -1;
            lastResult = null;
            hud.HideStart();
            SetTutorialGate(false); // Requests once; independent preparation holds remain closed.
            return true;
        }

        public void ConfigureChallenge(ChallengeDefinition definition, ChallengeAttemptContext context)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (definition.Subject != SubjectId.Football || definition.Id != context.ChallengeId)
                throw new ArgumentException("Football controller received a mismatched challenge context.");
            if (Lifecycle != null && Lifecycle.Phase != MinigamePhase.Tutorial)
                throw new InvalidOperationException("Football challenge must be configured before the start gate opens.");
            challengeDefinition = definition;
            challengeContext = context;
            selectedDifficulty = FootballDifficulty.Normal;
            rules = new FootballRules(difficultyConfig.Get(selectedDifficulty), OptionsFor(definition));
            matchRequested = false;
            rulesStarted = false;
            resultSubmitted = false;
            lastReportedKick = -1;
            lastStartedKick = -1;
            lastResult = null;
            hud.ConfigureChallenge(definition, false);
            hud.ShowStart(selectedDifficulty);
            inputBridge.SetEnabled(false, false);
            presentation.HidePreview();
            hud.Render(rules);
            presentation.Render(rules);
        }

        protected override void Awake()
        {
            base.Awake();
            SetTutorialGate(true);
            if (!ValidateReferences())
            {
                Debug.LogError("FootballController disabled: required scene references are missing", this);
                enabled = false;
                return;
            }

            selectedDifficulty = preferredDifficulty;
            rules = new FootballRules(difficultyConfig.Get(selectedDifficulty));
            inputBridge.Configure(hud.DirectionSlider, hud.ShootButton);
            ready = true;
            BindListeners();
            inputBridge.SetEnabled(false, false);

            bool startRetry = retryRequested;
            retryRequested = false;
            if (startRetry)
                BeginMatch(selectedDifficulty);
            else
                hud.ShowStart(selectedDifficulty);
            hud.Render(rules);
            presentation.Render(rules);
        }

        public bool ValidateReferences() => difficultyConfig && inputBridge && presentation && hud && resultPanel &&
            hud.ValidateReferences() && presentation.ValidateReferences() && resultPanel.ValidateReferences() &&
            resultPanel.SupportsRetry && hud.DirectionSlider && hud.ShootButton;

        protected override void Update()
        {
            if (!ready) return;
            if (IsSuspended)
            {
                inputBridge.CancelActivePointer();
                inputBridge.SetEnabled(false, false);
                presentation.Render(rules);
                hud.Render(rules);
                return;
            }
            base.Update();
            hud.Render(rules);
            presentation.Render(rules);
        }

        protected override void TickPlay(float deltaTime)
        {
            if (!ready || IsSuspended || !matchRequested || PresentationPhase != MinigamePhase.Play || resultSubmitted)
                return;

            if (!rulesStarted)
            {
                rulesStarted = rules.Start(openShotWindow: false);
                if (rulesStarted)
                    NotifyShotWindowStarted();
                RefreshInputAvailability();
            }
            if (!rulesStarted)
                return;

            FootballFlightSimulation previousFlight = rules.Flight;
            rules.Tick(deltaTime);
            NotifyPresentedState();
            NotifyShotResolved();
            NotifyShotWindowStarted();
            UpdateAudio(rules.Flight ?? previousFlight);
            RefreshInputAvailability();
            if (rules.State == FootballState.MatchResult)
            {
                resultSubmitted = true;
                inputBridge.SetEnabled(false, false);
                // Apply the terminal HUD state before opening ResultPanel so no gameplay control
                // survives for a frame and draws above the win/loss overlay.
                hud.Render(rules);
                presentation.Render(rules);
                if (challengeDefinition != null && challengeContext != null)
                {
                    ChallengeAttemptResult result = rules.BuildChallengeResult(challengeContext);
                    lastResult = result.ExamResult ?? new MinigameResult(result.Pass,
                        result.Pass ? result.Metrics.CompletedTargets * 2f : 0f,
                        result.Pass ? ScoreUtil.ToRank(result.Metrics.CompletedTargets * 2f) : Rank.F);
                }
                else lastResult = rules.BuildResult();
                resultPanel.SetDetail($"{rules.Goals}/{rules.RequiredGoals} BÀN");
                Finish(lastResult);
            }
        }

        protected override MinigameHudState BuildHudState() => MinigameHudState.Empty;

        void UpdateAudio(FootballFlightSimulation flight)
        {
            if (flight == null) return;
            if (flight.Time > 0f && flight != kickedFlight)
            {
                kickedFlight = flight;
                GameAudio.Play(GameSound.Kick);
            }
            if (!flight.Outcome.HasValue || flight == soundedOutcomeFlight) return;
            soundedOutcomeFlight = flight;
            GameAudio.Play(flight.Outcome.Value switch
            {
                FootballOutcome.Goal => GameSound.Cheer,
                FootballOutcome.Saved => GameSound.Save,
                FootballOutcome.Post or FootballOutcome.Crossbar => GameSound.Post,
                _ => GameSound.Miss
            });
        }

        void SetAim(float direction)
        {
            if (ready && matchRequested && PresentationPhase == MinigamePhase.Play && rules.SetAim(direction))
                RefreshInputAvailability();
        }

        void BeginCharge()
        {
            if (ready && matchRequested && PresentationPhase == MinigamePhase.Play && rules.BeginCharge())
                RefreshInputAvailability();
        }

        void ReleaseShot()
        {
            if (ready && matchRequested && PresentationPhase == MinigamePhase.Play && rules.ReleaseShot())
            {
                presentation.HidePreview();
                RefreshInputAvailability();
            }
        }

        void CancelCharge()
        {
            if (rules != null)
                rules.CancelCharge();
            RefreshInputAvailability();
            if (presentation && rules != null) presentation.Render(rules);
        }

        void RefreshInputAvailability()
        {
            if (inputBridge == null || !ready || IsSuspended || !matchRequested ||
                PresentationPhase != MinigamePhase.Play || resultSubmitted)
            {
                inputBridge?.SetEnabled(false, false);
                return;
            }
            inputBridge.SetEnabled(rules.State == FootballState.Aiming,
                rules.State == FootballState.Aiming || rules.State == FootballState.Charging);
        }

        void NotifyShotWindowStarted()
        {
            if (rules == null || rules.State != FootballState.Aiming || rules.IsShotWindowOpen ||
                rules.Kicks <= lastStartedKick)
                return;
            lastStartedKick = rules.Kicks;
            ShotWindowStarted?.Invoke(lastStartedKick);
            rules.BeginShotWindow();
        }

        void NotifyShotResolved()
        {
            if (rules == null || rules.Kicks <= lastReportedKick)
                return;
            while (lastReportedKick < rules.Kicks - 1)
            {
                lastReportedKick++;
                ShotResolved?.Invoke(rules.Outcomes[lastReportedKick]);
            }
        }

        void OnApplicationPause(bool paused)
        {
            appPaused = paused;
            if (paused)
            {
                inputBridge?.CancelActivePointer();
                inputBridge?.SetEnabled(false, false);
            }
            else
                RefreshInputAvailability();
        }

        void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            if (!focused)
            {
                inputBridge?.CancelActivePointer();
                inputBridge?.SetEnabled(false, false);
            }
            else
                RefreshInputAvailability();
        }

        void OnStartRequested(FootballDifficulty difficulty) => BeginMatch(difficulty);

        void OnResultAction(string action)
        {
            if (challengeContext != null)
                return;
            if (action == ResultPanelActions.Retry)
                retryRequested = true;
            else if (action == ResultPanelActions.Continue)
                retryRequested = false;
        }

        protected override void OnResultResolved(MinigameResult result)
        {
            if (challengeDefinition == null || challengeContext == null)
            {
                base.OnResultResolved(result);
                return;
            }
            ChallengeCompleted?.Invoke(rules.BuildChallengeResult(challengeContext));
        }

        static FootballMatchOptions OptionsFor(ChallengeDefinition definition) => definition.Kind switch
        {
            ChallengeKind.Learn => new FootballMatchOptions(null, 3, false),
            ChallengeKind.Practice => new FootballMatchOptions(
                definition.AttemptLimit > 0 ? definition.AttemptLimit : (int?)null, definition.TargetCount, true,
                stopAtRequiredGoals: true),
            _ => new FootballMatchOptions(5, 3, true)
        };

        static FootballDifficulty ToFootballDifficulty(ChallengeDifficulty difficulty) => difficulty switch
        {
            ChallengeDifficulty.Easy => FootballDifficulty.Easy,
            ChallengeDifficulty.Normal => FootballDifficulty.Normal,
            ChallengeDifficulty.Hard => FootballDifficulty.Hard,
            _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, null)
        };

        void OnEnable()
        {
            if (!ready) return;
            BindListeners();
            RefreshInputAvailability();
        }

        void BindListeners()
        {
            if (!ready || listenersBound) return;
            inputBridge.AimChanged += SetAim;
            inputBridge.ShootPressed += BeginCharge;
            inputBridge.ShootReleased += ReleaseShot;
            inputBridge.ShootCancelled += CancelCharge;
            hud.StartRequested += OnStartRequested;
            resultPanel.ActionRequested += OnResultAction;
            listenersBound = true;
        }

        void OnDisable()
        {
            if (inputBridge)
            {
                inputBridge.CancelActivePointer();
                inputBridge.SetEnabled(false, false);
                if (listenersBound)
                {
                    inputBridge.AimChanged -= SetAim;
                    inputBridge.ShootPressed -= BeginCharge;
                    inputBridge.ShootReleased -= ReleaseShot;
                    inputBridge.ShootCancelled -= CancelCharge;
                }
            }
            if (listenersBound && hud) hud.StartRequested -= OnStartRequested;
            if (listenersBound && resultPanel) resultPanel.ActionRequested -= OnResultAction;
            listenersBound = false;
            if (presentation) presentation.HidePreview();
        }

        bool IsSuspended => appPaused || !hasFocus || Time.timeScale == 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSessionPreference()
        {
            preferredDifficulty = FootballDifficulty.Normal;
            retryRequested = false;
        }

        internal static void ResetSessionPreferenceForTests() => ResetSessionPreference();
    }
}
