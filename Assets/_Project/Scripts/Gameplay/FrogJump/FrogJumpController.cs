using System.Collections.Generic;
using System.Globalization;
using KMA.Gameplay.Core;
using KMA.Gameplay.UI;
using TMPro;
using UnityEngine;

namespace KMA.Gameplay.FrogJump
{
    public sealed class FrogJumpController : MinigameBase, IMinigameGuideSource
    {
        /// The tutorial copy lives in the hint chip above the power bar, not the shared card.
        public override bool UsesSharedTutorial => false;

        [SerializeField] FrogJumpBalanceConfig balance;
        [SerializeField] FrogJumpView view;
        [SerializeField] FrogJumpPowerBar powerBar;
        [SerializeField] FrogJumpTapArea tapArea;
        [SerializeField] TMP_Text feedback;

        FrogJumpRules rules;
        float feedbackUntil;
        static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
        bool finished;

        static string Metres(float value) => value.ToString("0.0", Vi);

        public FrogJumpRules Rules => rules;
        public event System.Action<FrogJumpState> StatePresented;
        public bool? PendingSavesLife => PendingJumpSavesLife();
        public bool IsWired => view != null && powerBar != null && tapArea != null && feedback != null;
        public string GuideKey => FrogJumpGuide.Key;
        public IReadOnlyList<TutorialStep> BuildGuide() => FrogJumpGuide.Build(rules.Tuning, PendingJumpSavesLife());

        static bool? PendingJumpSavesLife()
        {
            GameManager manager = GameManager.Instance;
            return manager != null && manager.IsInitialized ? manager.Session?.PendingFrogJump?.SavesLife : null;
        }

        public void Configure(FrogJumpBalanceConfig config, FrogJumpView frogView, FrogJumpPowerBar bar,
            FrogJumpTapArea tap, TMP_Text feedbackLabel)
        {
            balance = config;
            view = frogView;
            powerBar = bar;
            tapArea = tap;
            feedback = feedbackLabel;
        }

        protected override void Awake()
        {
            base.Awake();
            rules = new FrogJumpRules(balance != null ? balance.Tuning : new FrogJumpTuning());
            rules.Landed += OnLanded;
        }

        void Start()
        {
            if (tapArea != null) tapArea.Tapped += OnTapped;
            var pause = FindFirstObjectByType<PausePanel>(FindObjectsInactive.Include);
            if (pause != null) pause.SetLeaveOptionsVisible(false);
            if (feedback != null) feedback.text = string.Empty;
        }

        void OnDestroy()
        {
            if (tapArea != null) tapArea.Tapped -= OnTapped;
            if (rules != null) rules.Landed -= OnLanded;
        }

        protected override void Update()
        {
            base.Update();
            bool playing = PresentationPhase == MinigamePhase.Play && !rules.IsOver;
            if (powerBar != null) powerBar.SetNeedle(rules.Needle01, playing && rules.State == FrogJumpState.Aiming);
            if (view != null)
                view.Present(rules, Mathf.Clamp01(rules.StateElapsed / Mathf.Max(.01f, rules.Tuning.jumpSeconds)));
            if (feedback != null && Time.time > feedbackUntil) feedback.text = string.Empty;
        }

        protected override void TickPlay(float dt)
        {
            rules.Tick(dt);
            StatePresented?.Invoke(rules.State);
            if (rules.IsOver && !finished)
            {
                finished = true;
                bool pass = rules.ReachedFinish;
                Finish(new MinigameResult(pass, 0f, pass ? Rank.C : Rank.F));
            }
        }

        protected override MinigameHudState BuildHudState() => rules == null
            ? MinigameHudState.Empty
            : new MinigameHudState("PLAY", rules.TimeRemaining, rules.Progress01, 0f, rules.Distance,
                VietText.Fix($"Còn {Metres(rules.Tuning.trackMetres - rules.Distance)} m"));

        /// Taps are ignored while paused (timeScale 0), including the Space shortcut.
        public static bool CanAcceptTap(MinigamePhase phase, bool isOver, float timeScale) =>
            phase == MinigamePhase.Play && !isOver && timeScale > 0f;

        void OnTapped()
        {
            if (!CanAcceptTap(PresentationPhase, rules.IsOver, Time.timeScale)) return;
            if (rules.Stop()) GameAudio.Play(GameSound.Click);
        }

        void OnLanded(float metres)
        {
            GameAudio.Play(metres <= 0f ? GameSound.Miss : GameSound.SandStep);
            var haptics = FindFirstObjectByType<HapticsService>();
            if (haptics != null) { if (metres <= 0f) haptics.Fail(); else haptics.Light(); }
            if (view != null && rules.State == FrogJumpState.Fallen) view.Present(rules,0f);
            if (feedback == null) return;
            feedback.text = VietText.Fix(metres <= 0f ? "NGÃ"
                : metres >= rules.Tuning.maxJumpMetres * .9f ? $"CHÍNH XÁC · {Metres(metres)} m" : $"{Metres(metres)} m");
            feedbackUntil = Time.time + .8f;
        }
    }
}
