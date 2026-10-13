using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace KMA.Gameplay.Chess
{
    public sealed class ChessFinalHud : MonoBehaviour
    {
        [SerializeField] TMP_Text objective;
        [SerializeField] TMP_Text clock;
        [FormerlySerializedAs("mistakes")] [SerializeField] TMP_Text moves;
        [SerializeField] TMP_Text turn;
        [SerializeField] TMP_Text toast;
        [SerializeField] GameObject introCard;
        [SerializeField] TMP_Text introLabel;
        [SerializeField] GameObject analysisErrorCard;
        [SerializeField] Button startButton;
        [SerializeField] Button hintButton;
        [SerializeField] Button retryAnalysis;
        [SerializeField] Button restartPuzzle;
        float toastUntil;

        public Button StartButton => startButton;
        public Button HintButton => hintButton;
        public Button RetryAnalysisButton => retryAnalysis;
        public Button RestartPuzzleButton => restartPuzzle;
        public string ClockText => clock.text;
        public string MovesText => moves.text;
        public string TurnText => turn.text;
        public bool AnalysisErrorVisible => analysisErrorCard != null && analysisErrorCard.activeSelf;

        public void Configure(TMP_Text objectiveLabel, TMP_Text clockLabel, TMP_Text movesLabel, TMP_Text turnLabel,
            TMP_Text toastLabel, GameObject intro, Button start, Button hint, TMP_Text introText,
            GameObject analysisError, Button retry, Button restart)
        {
            objective = objectiveLabel;
            clock = clockLabel;
            moves = movesLabel;
            turn = turnLabel;
            toast = toastLabel;
            introCard = intro;
            startButton = start;
            hintButton = hint;
            introLabel = introText;
            analysisErrorCard = analysisError;
            retryAnalysis = retry;
            restartPuzzle = restart;
        }

        public void ShowIntro(bool visible) => introCard.SetActive(visible);
        public void SetObjective(int optimalMoves, int maxMoves) =>
            objective.text = VietText.Fix($"Chiếu hết trong tối đa {maxMoves} nước (tối ưu {optimalMoves} nước)");
        public void SetClock(float remaining) => clock.text = FormatClock(remaining);
        public void SetMoves(int used, int max) => moves.text = VietText.Fix($"Nước: {used}/{max}");
        public void SetIntro(string text) => introLabel.text = VietText.Fix(text);
        public void SetTurnText(string text) => turn.text = VietText.Fix(text);
        public void SetHintAvailable(bool available) => hintButton.interactable = available;
        public void ShowAnalysisError(bool visible) => analysisErrorCard.SetActive(visible);

        public void SetGenerating(bool generating)
        {
            if (!generating) return;
            SetTurnText("Đang tạo thế cờ…");
            introCard.SetActive(false);
        }

        public void SetTurn(ChessAttemptPhase phase) => SetTurnText(phase switch
        {
            ChessAttemptPhase.PlayerTurn => "Lượt của bạn",
            ChessAttemptPhase.Analyzing => "Đang phân tích…",
            ChessAttemptPhase.BossTurn => "Lượt giảng viên",
            ChessAttemptPhase.Completed => "Hoàn thành",
            ChessAttemptPhase.Paused => "Tạm dừng",
            ChessAttemptPhase.AnalysisError => "Chưa phân tích được",
            ChessAttemptPhase.Failed => turn.text,
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
