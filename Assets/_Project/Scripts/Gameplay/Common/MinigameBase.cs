using KMA.Gameplay.UI;
using UnityEngine;

namespace KMA.Gameplay
{
    public abstract class MinigameBase : MonoBehaviour, IMinigameHudStateSource
    {
        public event System.Action<MinigameResult> Completed;
        public event System.Action<MinigamePhase> PhaseChanged;
        /// Raised the moment the outcome is known, before any minigame-specific delay holds back Completed.
        public event System.Action<bool> ResultDecided;
        [SerializeField] float tutorialSeconds = 2f;
        [SerializeField] float countdownSeconds = 3f;
        MinigameLifecycle lifecycle;
        float nextCountdownSound;
        readonly MinigameStartGate startGate = new MinigameStartGate();
        System.IDisposable legacyStartHold;
        public bool StartPreparationReady => startGate.CanStart;
        public System.IDisposable AcquireStartHold(string owner)
        {
            Lifecycle?.SetTutorialGate(true);
            return startGate.Hold(owner);
        }
        public void RequestStart() => startGate.RequestStart();
        void OpenPreparedStart() => Lifecycle?.SetTutorialGate(false);

        protected MinigameLifecycle Lifecycle
        {
            get => lifecycle;
            set
            {
                if (lifecycle != null)
                    lifecycle.PhaseChanged -= RelayPhaseChanged;
                lifecycle = value;
                if (lifecycle != null)
                    lifecycle.PhaseChanged += RelayPhaseChanged;
            }
        }

        public MinigamePhase PresentationPhase => Lifecycle == null ? MinigamePhase.Tutorial : Lifecycle.Phase;

        /// PhaseOverlay shows the shared tutorial card and holds the start gate while it is open.
        public virtual bool UsesSharedTutorial => true;

        /// PhaseOverlay shows the shared 3-2-1 countdown.
        public virtual bool UsesSharedCountdown => true;

        /// The minigame opens its own start gate, so PhaseOverlay must leave the gate alone.
        public virtual bool OwnsStartGate => false;

        /// The scene configurator paints its own camera background, so the shared assembler keeps it.
        public virtual bool OwnsCameraBackground => false;

        public void SetTutorialGate(bool closed)
        {
            if (closed) legacyStartHold ??= AcquireStartHold("legacy intro");
            else
            {
                RequestStart();
                legacyStartHold?.Dispose();
                legacyStartHold = null;
            }
        }

        protected virtual void Awake()
        {
            Lifecycle = new MinigameLifecycle(tutorialSeconds, countdownSeconds);
            startGate.Ready += OpenPreparedStart;
            if (startGate.IsHeld) Lifecycle.SetTutorialGate(true);
            else if (startGate.CanStart) OpenPreparedStart();
        }

        protected virtual void Update()
        {
            Lifecycle.Tick(Time.deltaTime);
            if (Lifecycle.Phase == MinigamePhase.Countdown && Time.deltaTime > 0f)
            {
                nextCountdownSound -= Time.deltaTime;
                if (nextCountdownSound <= 0f)
                {
                    GameAudio.Play(GameSound.Countdown);
                    nextCountdownSound = 1f;
                }
            }
            if (Lifecycle.Phase == MinigamePhase.Play)
                TickPlay(Time.deltaTime);
        }

        public MinigameHudState ReadHudState() => BuildHudState();
        protected virtual MinigameHudState BuildHudState() => MinigameHudState.Empty;
        protected abstract void TickPlay(float dt);

        void RelayPhaseChanged(MinigamePhase phase)
        {
            if (phase == MinigamePhase.Countdown) nextCountdownSound = 0f;
            if (phase == MinigamePhase.Play) GameAudio.Play(GameSound.Whistle);
            PhaseChanged?.Invoke(phase);
        }

        protected void Finish(MinigameResult result)
        {
            if (TryBeginResolve(result.Pass))
            {
                GameAudio.Play(result.Pass ? GameSound.Win : GameSound.Lose);
                ResultDecided?.Invoke(result.Pass);
                OnResultResolved(result);
            }
        }

        protected virtual bool TryBeginResolve(bool passed) => Lifecycle.BeginResolve();

        protected virtual void OnResultResolved(MinigameResult result) => Completed?.Invoke(result);
    }
}
