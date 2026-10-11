using KMA.Gameplay;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KMA.Gameplay.UI
{
    public interface ISprintStartPresentation
    {
        void Bind(MinigameBase source);
    }

    public sealed class PhaseOverlay : MonoBehaviour
    {
        const float CountdownDuration = 3f;

        [SerializeField] GameObject tutorialRoot;
        [SerializeField] GameObject countdownRoot;
        [SerializeField] GameObject playRoot;
        [SerializeField] GameObject resolveRoot;
        [SerializeField] TMP_Text phaseLabel;
        [SerializeField] TMP_Text countdownLabel;
        [SerializeField] MinigameBase minigameSource;

        MinigameBase source;
        bool subscribed;
        float countdownElapsed;

        public MinigamePhase DisplayedPhase { get; private set; } = MinigamePhase.Tutorial;
        public bool IsTutorialVisible => tutorialRoot != null && tutorialRoot.activeSelf;
        public bool IsPlayVisible => playRoot != null && playRoot.activeSelf;
        public string CountdownText => countdownLabel == null ? string.Empty : countdownLabel.text;

        public void Bind(MinigameBase minigame)
        {
            Unsubscribe();
            minigameSource = minigame;
            source = minigame;
            Subscribe();
            ConfigureTutorial();
            ApplyPhase(source == null ? MinigamePhase.Tutorial : source.PresentationPhase);
        }

        void OnEnable()
        {
            if (source == null && minigameSource != null)
                source = minigameSource;
            Subscribe();
            ConfigureTutorial();
            if (source != null)
                ApplyPhase(source.PresentationPhase);
        }

        void OnDisable() => Unsubscribe();

        void Update()
        {
            if ((source != null && !source.UsesSharedCountdown) || DisplayedPhase != MinigamePhase.Countdown)
                return;

            countdownElapsed += Time.deltaTime;
            RefreshCountdown();
        }

        void Subscribe()
        {
            if (source == null || subscribed)
                return;
            source.PhaseChanged += ApplyPhase;
            subscribed = true;
        }

        void Unsubscribe()
        {
            if (source != null && subscribed)
                source.PhaseChanged -= ApplyPhase;
            subscribed = false;
        }

        void ApplyPhase(MinigamePhase phase)
        {
            DisplayedPhase = phase;
            if (phase == MinigamePhase.Countdown)
                countdownElapsed = 0f;

            bool sharedTutorial = source == null || source.UsesSharedTutorial;
            bool sharedCountdown = source == null || source.UsesSharedCountdown;
            SetActive(tutorialRoot, sharedTutorial && phase == MinigamePhase.Tutorial);
            SetActive(countdownRoot, sharedCountdown && phase == MinigamePhase.Countdown);
            SetActive(playRoot, sharedTutorial && phase == MinigamePhase.Play);
            SetActive(resolveRoot, false); // ResultPanel owns the resolve headline.

            if (phaseLabel != null)
                phaseLabel.text = VietText.Fix(!sharedTutorial || phase == MinigamePhase.Resolve ? string.Empty : PhaseName(phase));
            if (sharedCountdown)
                RefreshCountdown();
        }

        // Minigames that own their start gate open it themselves; custom-tutorial minigames are released
        // at once; shared-tutorial minigames advance on the lifecycle timer (releasing their gate would
        // skip straight to the countdown). The how-to-play guide is MinigameGuideHost's job.
        void ConfigureTutorial()
        {
            if (source == null)
                return;
            if (source.OwnsStartGate)
                FindSprintStartPresentation()?.Bind(source);
            else if (!source.UsesSharedTutorial)
                source.RequestStart();
        }

        static ISprintStartPresentation FindSprintStartPresentation()
        {
            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < behaviours.Length; index++)
                if (behaviours[index] is ISprintStartPresentation presentation)
                    return presentation;
            return null;
        }

        void RefreshCountdown()
        {
            if (countdownLabel == null)
                return;
            var remaining = Mathf.Clamp(Mathf.CeilToInt(CountdownDuration - countdownElapsed), 1, 3);
            countdownLabel.text = VietText.Fix(DisplayedPhase == MinigamePhase.Countdown
                ? remaining.ToString()
                : string.Empty);
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }

        static string PhaseName(MinigamePhase phase) => phase switch
        {
            MinigamePhase.Tutorial => "HƯỚNG DẪN",
            MinigamePhase.Countdown => "CHUẨN BỊ",
            MinigamePhase.Play => "TĂNG TỐC!",
            MinigamePhase.Resolve => "KẾT QUẢ",
            _ => string.Empty
        };
    }
}
