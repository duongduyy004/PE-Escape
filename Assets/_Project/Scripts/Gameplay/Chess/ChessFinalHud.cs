using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Gameplay.Chess
{
    public sealed class ChessFinalHud : MonoBehaviour
    {
        [SerializeField] TMP_Text objective;
        [SerializeField] TMP_Text clock;
        [SerializeField] TMP_Text mistakes;
        [SerializeField] TMP_Text turn;
        [SerializeField] TMP_Text toast;
        [SerializeField] GameObject introCard;
        [SerializeField] Button startButton;
        [SerializeField] Button hintButton;
        float toastUntil;

        public Button StartButton => startButton;
        public Button HintButton => hintButton;
        public string ClockText => clock.text;
        public string MistakesText => mistakes.text;
        public string TurnText => turn.text;

        public void Configure(TMP_Text objectiveLabel, TMP_Text clockLabel, TMP_Text mistakesLabel, TMP_Text turnLabel,
            TMP_Text toastLabel, GameObject intro, Button start, Button hint)
        {
            objective = objectiveLabel;
            clock = clockLabel;
            mistakes = mistakesLabel;
            turn = turnLabel;
            toast = toastLabel;
            introCard = intro;
            startButton = start;
            hintButton = hint;
        }

        public void ShowIntro(bool visible) => introCard.SetActive(visible);
        public void SetObjective(int moves) => objective.text = VietText.Fix($"Chiếu hết trong {moves} nước");
        public void SetClock(float remaining) => clock.text = FormatClock(remaining);
        public void SetMistakes(int made, int max) => mistakes.text = VietText.Fix($"Sai: {made}/{max}");
        public void SetTurnText(string text) => turn.text = VietText.Fix(text);
        public void SetHintAvailable(bool available) => hintButton.interactable = available;

        public void SetTurn(ChessFinalPhase phase) => SetTurnText(phase switch
        {
            ChessFinalPhase.PlayerTurn => "Lượt của bạn",
            ChessFinalPhase.Validating => "Lượt của bạn",
            ChessFinalPhase.BossTurn => "Lượt giảng viên",
            ChessFinalPhase.Completed => "Hoàn thành",
            ChessFinalPhase.Paused => "Tạm dừng",
            ChessFinalPhase.Failed => turn.text,
            _ => "Đọc đề, sau đó bấm Bắt đầu"
        });

        public void Toast(string text, float seconds = 1.6f)
        {
            toast.text = VietText.Fix(text);
            toast.gameObject.SetActive(true);
            toastUntil = Time.unscaledTime + seconds;
        }

        void Update()
        {
            if (toast == null || !toast.gameObject.activeSelf) return;
            if (Time.timeScale == 0f) toastUntil += Time.unscaledDeltaTime;
            else if (Time.unscaledTime > toastUntil) toast.gameObject.SetActive(false);
        }

        public static string FormatClock(float seconds) => CelebrationSummary.FormatClock(seconds);
    }
}
