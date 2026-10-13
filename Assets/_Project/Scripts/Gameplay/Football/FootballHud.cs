using System;
using KMA.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Gameplay
{
    public sealed class FootballHud : MonoBehaviour
    {
        const float OverPowerThreshold = .85f;

        [SerializeField] Slider directionSlider;
        [SerializeField] TMP_Text directionLabel;
        [SerializeField] TMP_Text shotFeedback;
        [SerializeField] FootballHoldButton shootButton;
        [SerializeField] KitBar powerBar;
        [SerializeField] GameObject overPowerWarning;
        [SerializeField] TMP_Text scoreLabel;
        [SerializeField] TMP_Text remainingLabel;
        [SerializeField] TMP_Text shotTimerLabel;
        [SerializeField] Image[] kickMarkers = new Image[5];
        [SerializeField] GameObject startPanel;
        [SerializeField] GameObject startScrim;
        [SerializeField] Button startButton;
        [SerializeField] Button easyButton;
        [SerializeField] Button normalButton;
        [SerializeField] Button hardButton;

        FootballDifficulty selectedDifficulty = FootballDifficulty.Normal;
        bool listenersBound;
        ChallengeDefinition challenge;

        public event Action<FootballDifficulty> StartRequested;
        public Slider DirectionSlider => directionSlider;
        public FootballHoldButton ShootButton => shootButton;
        public TMP_Text ShotTimerLabel => shotTimerLabel;

        void Awake()
        {
            // Keep the timer visible even in scenes authored before the timer field was added.
            if (shotTimerLabel == null)
            {
                shotTimerLabel = UiKit.Label(transform, "ShotTimer", string.Empty, MinigameUiTheme.Title,
                    MinigameUiTheme.TextPrimary, TextAlignmentOptions.Center, outline: true);
                UiKit.Anchor(shotTimerLabel.rectTransform, new Vector2(.39f, .82f), new Vector2(.61f, .92f));
                shotTimerLabel.gameObject.SetActive(false);
            }
        }

        public void Configure(Slider aim, FootballHoldButton shoot, KitBar power, GameObject warning, TMP_Text score,
            TMP_Text remaining, Image[] markers, GameObject start, Button startAction = null, Button easy = null,
            Button normal = null, Button hard = null, TMP_Text directionText = null, TMP_Text feedback = null,
            GameObject scrim = null, TMP_Text shotTimer = null)
        {
            directionSlider = aim;
            shootButton = shoot;
            powerBar = power;
            overPowerWarning = warning;
            scoreLabel = score;
            remainingLabel = remaining;
            kickMarkers = markers;
            startPanel = start;
            startScrim = scrim;
            startButton = startAction;
            easyButton = easy;
            normalButton = normal;
            hardButton = hard;
            directionLabel = directionText;
            shotFeedback = feedback;
            // Preserve the runtime fallback created in Awake when an older scene has no serialized field.
            if (shotTimer) shotTimerLabel = shotTimer;
            BindButtons();
            UpdateDifficultyButtons();
        }

        public bool ValidateReferences() => directionSlider && shootButton && powerBar && overPowerWarning &&
            scoreLabel && remainingLabel && kickMarkers != null && kickMarkers.Length == 5 &&
            Array.TrueForAll(kickMarkers, marker => marker) && startPanel;

        public void ShowStart(FootballDifficulty selected)
        {
            selectedDifficulty = FootballDifficulty.Normal;
            HideDifficultySelection();
            SetStartVisible(true);
            if (directionSlider) directionSlider.interactable = false;
            if (shootButton) shootButton.SetInteractable(false);
            UpdateDifficultyButtons();
        }

        public void HideStart()
        {
            SetStartVisible(false);
        }

        void SetStartVisible(bool visible)
        {
            if (startPanel) startPanel.SetActive(visible);
            if (startScrim) startScrim.SetActive(visible);
        }

        public void SetDifficulty(FootballDifficulty selected)
        {
            selectedDifficulty = FootballDifficulty.Normal;
            UpdateDifficultyButtons();
        }

        public void ConfigureChallenge(ChallengeDefinition definition, bool difficultySelectionEnabled)
        {
            challenge = definition;
            selectedDifficulty = FootballDifficulty.Normal;
            HideDifficultySelection();
            UpdateDifficultyButtons();
        }

        public void RequestStart() => StartRequested?.Invoke(selectedDifficulty);

        public void Render(FootballRules rules)
        {
            if (rules == null)
                return;
            bool overPower = rules.Power > OverPowerThreshold;
            int target = challenge != null ? challenge.TargetCount : rules.MaximumKicks ?? rules.RequiredGoals;
            if (scoreLabel) scoreLabel.text = VietText.Fix(challenge == null
                ? "BÀN: " + rules.Goals : $"BÀN: {rules.Goals} / {target}");
            if (remainingLabel) remainingLabel.text = VietText.Fix(rules.MaximumKicks.HasValue
                ? "CÒN " + Mathf.Max(0, rules.MaximumKicks.Value - rules.Kicks) + " LƯỢT"
                : "LUYỆN TẬP KHÔNG GIỚI HẠN LƯỢT");
            if (shotTimerLabel)
            {
                bool visible = rules.IsShotWindowOpen;
                shotTimerLabel.gameObject.SetActive(visible);
                if (visible)
                {
                    float seconds = rules.ShotTimeRemaining;
                    shotTimerLabel.text = VietText.Fix("SÚT TRONG " + seconds.ToString("0.0") + " GIÂY");
                    shotTimerLabel.color = seconds <= 1f ? MinigameUiTheme.Energy : MinigameUiTheme.TextPrimary;
                }
            }
            if (powerBar)
            {
                powerBar.SetValue(rules.Power);
                powerBar.SetFillColor(overPower ? MinigameUiTheme.Energy : MinigameUiTheme.Accent);
                if (powerBar.Label) powerBar.Label.text = VietText.Fix(Mathf.RoundToInt(rules.Power * 100f) + "%");
            }
            if (overPowerWarning) overPowerWarning.SetActive(overPower);
            for (int i = 0; kickMarkers != null && i < kickMarkers.Length; i++)
            {
                if (!kickMarkers[i]) continue;
                kickMarkers[i].color = i < rules.Outcomes.Count
                    ? (rules.Outcomes[i] == FootballOutcome.Goal ? MinigameUiTheme.Accent : MinigameUiTheme.Energy)
                    : MinigameUiTheme.Track;
            }
            if (startPanel && rules.State != FootballState.Start)
                SetStartVisible(false);
            if (directionSlider) directionSlider.SetValueWithoutNotify(rules.AimX);
            if (directionLabel) directionLabel.text = VietText.Fix(Mathf.Abs(rules.AimX) < .02f ? "GIỮA" :
                (rules.AimX < 0f ? "TRÁI " : "PHẢI ") + Mathf.RoundToInt(Mathf.Abs(rules.AimX) * 100f) + "%");
            if (shotFeedback) shotFeedback.text = VietText.Fix(OutcomeText(rules.Flight?.Outcome ?? rules.LastOutcome));
        }

        static string OutcomeText(FootballOutcome? outcome) => outcome switch
        {
            FootballOutcome.Goal => "VÀO!",
            FootballOutcome.Saved => "THỦ MÔN CẢN PHÁ",
            FootballOutcome.Post => "TRÚNG CỘT DỌC",
            FootballOutcome.Crossbar => "TRÚNG XÀ NGANG",
            FootballOutcome.Wide => "CHỆCH KHUNG THÀNH",
            FootballOutcome.High => "BÓNG VƯỢT XÀ",
            FootballOutcome.Short => "BÓNG DỪNG TRƯỚC GOAL",
            FootballOutcome.Timeout => "HẾT GIỜ",
            _ => string.Empty
        };

        void OnEnable() => BindButtons();
        void OnDisable()
        {
            if (!listenersBound) return;
            if (startButton) startButton.onClick.RemoveListener(RequestStart);
            if (easyButton) easyButton.onClick.RemoveListener(SelectEasy);
            if (normalButton) normalButton.onClick.RemoveListener(SelectNormal);
            if (hardButton) hardButton.onClick.RemoveListener(SelectHard);
            listenersBound = false;
        }

        void BindButtons()
        {
            if (listenersBound) return;
            if (startButton) startButton.onClick.AddListener(RequestStart);
            if (easyButton) easyButton.onClick.AddListener(SelectEasy);
            if (normalButton) normalButton.onClick.AddListener(SelectNormal);
            if (hardButton) hardButton.onClick.AddListener(SelectHard);
            listenersBound = startButton || easyButton || normalButton || hardButton;
        }

        void SelectEasy() => SetDifficulty(FootballDifficulty.Easy);
        void SelectNormal() => SetDifficulty(FootballDifficulty.Normal);
        void SelectHard() => SetDifficulty(FootballDifficulty.Hard);

        void UpdateDifficultyButtons()
        {
            SetDifficultyVariant(easyButton, selectedDifficulty == FootballDifficulty.Easy);
            SetDifficultyVariant(normalButton, selectedDifficulty == FootballDifficulty.Normal);
            SetDifficultyVariant(hardButton, selectedDifficulty == FootballDifficulty.Hard);
        }

        void HideDifficultySelection()
        {
            foreach (Button button in new[] { easyButton, normalButton, hardButton })
                if (button) button.gameObject.SetActive(false);
            Transform title = startPanel ? startPanel.transform.Find("DifficultyTitle") : null;
            if (title) title.gameObject.SetActive(false);
        }

        static void SetDifficultyVariant(Button button, bool selected)
        {
            if (button && button.GetComponent<KitPressFeedback>())
                UiKit.ApplyVariant(UiKit.ButtonParts(button), selected ? ButtonVariant.Primary : ButtonVariant.Secondary);
        }

        static void SetButtonAvailable(Button button, bool available)
        {
            if (button) button.interactable = available;
        }

        static FootballDifficulty ToFootballDifficulty(ChallengeDifficulty difficulty) => difficulty switch
        {
            ChallengeDifficulty.Easy => FootballDifficulty.Easy,
            ChallengeDifficulty.Normal => FootballDifficulty.Normal,
            ChallengeDifficulty.Hard => FootballDifficulty.Hard,
            _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, null)
        };
    }
}
