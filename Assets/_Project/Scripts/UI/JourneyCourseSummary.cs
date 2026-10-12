using System.Collections.Generic;
using System.Text;
using KMA.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Gameplay.UI
{
    public readonly struct JourneyScoreRow
    {
        public SubjectId Subject { get; }
        public float Score { get; }
        public Rank Rank { get; }

        public JourneyScoreRow(SubjectId subject, float score, Rank rank)
        {
            Subject = subject;
            Score = score;
            Rank = rank;
        }
    }

    public sealed class JourneyCourseSummary : MonoBehaviour
    {
        static readonly SubjectId[] CourseOrder =
            { SubjectId.Sprint, SubjectId.Volleyball, SubjectId.Football, SubjectId.Chess };
        static readonly string[] Labels = { "Chạy nước rút", "Bóng chuyền", "Bóng đá", "Bài kiểm tra cuối" };
        const float ReplayWidth = 300f;
        const float ReplayGap = 20f;
        const float TextInset = 16f;
        public static float TextColumnWidthForContent(float contentWidth) =>
            Mathf.Max(0f, contentWidth * .9f - TextInset - ReplayWidth - ReplayGap);
        [SerializeField] TMP_Text summaryText;
        [SerializeField] Button replayButton;
        readonly List<JourneyScoreRow> scoreRows = new List<JourneyScoreRow>();

        public IReadOnlyList<JourneyScoreRow> ScoreRows => scoreRows;
        public int SupplementaryRounds { get; private set; }
        public Button ReplayButton => EnsureReplayButton();
        public event System.Action ReplayRequested;

        public void Configure(TMP_Text text)
        {
            summaryText = text;
            ConfigureLayout();
        }

        public void Hide() => gameObject.SetActive(false);

        public void Show(GameSession session)
        {
            if (session == null) return;
            ConfigureLayout();
            scoreRows.Clear();
            var scores = new List<string>();
            for (int index = 0; index < CourseOrder.Length; index++)
            {
                SubjectRecord record = session.GetRecord(CourseOrder[index]);
                MinigameResult best = record.BestResult;
                float score = best == null ? record.BestScore : best.Score;
                Rank rank = best == null ? record.BestRank : best.Rank;
                scoreRows.Add(new JourneyScoreRow(CourseOrder[index], score, rank));
                scores.Add($"{Labels[index]}: {score.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} · {rank}");
            }
            SupplementaryRounds = session.Journey.SupplementaryRounds;
            if (summaryText != null)
            {
                summaryText.fontSize = UITheme.Shared.LessonJourney.captionSize - 16f;
                // This compact strip intentionally uses a smaller size than the initial kit label.
                summaryText.fontSizeMax = summaryText.fontSize;
                summaryText.fontSizeMin = Mathf.Min(summaryText.fontSizeMin, summaryText.fontSizeMax);
                summaryText.text = VietText.Fix("HOÀN TẤT HỌC PHẦN\n" + FormatScoreLines(scores));
            }
            gameObject.SetActive(true);
            EnsureReplayButton();
        }

        public static string FormatScoreLines(IReadOnlyList<string> scores)
        {
            if (scores == null || scores.Count == 0) return string.Empty;
            int breakAt = (scores.Count + 1) / 2;
            var text = new StringBuilder();
            for (int i = 0; i < scores.Count; i++)
            {
                if (i > 0) { if (i == breakAt) text.Append('\n'); else text.Append(" | "); }
                text.Append(scores[i]);
            }
            return text.ToString();
        }

        void ConfigureLayout()
        {
            RectTransform panel = transform as RectTransform;
            if (panel != null)
            {
                panel.anchorMin = UITheme.Shared.LessonJourney.summaryAnchorMin;
                panel.anchorMax = UITheme.Shared.LessonJourney.summaryAnchorMax;
                panel.offsetMin = panel.offsetMax = Vector2.zero;
            }
            if (summaryText == null) return;
            RectTransform textRect = summaryText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(TextInset, 6f);
            textRect.offsetMax = new Vector2(-(ReplayWidth + ReplayGap), -6f);
            summaryText.alignment = TextAlignmentOptions.MidlineLeft;
            summaryText.enableWordWrapping = true;
            summaryText.textWrappingMode = TextWrappingModes.Normal;
            summaryText.raycastTarget = false;
        }

        Button EnsureReplayButton()
        {
            if (replayButton == null)
            {
                KMA.UI.Kit.ButtonHandle handle = KMA.UI.Kit.UiKit.Button(transform, "ReplayCelebration",
                    "Xem lại lễ mừng", KMA.UI.Kit.ButtonVariant.Primary);
                replayButton = handle.Button;
            }
            // Fill the strip's height (it is shorter than a kit button) so it never covers the map.
            var rect = (RectTransform)replayButton.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(1f, .5f);
            rect.anchoredPosition = new Vector2(-8f, 0f);
            rect.sizeDelta = new Vector2(ReplayWidth, -8f);
            replayButton.onClick.RemoveListener(OnReplay);
            replayButton.onClick.AddListener(OnReplay);
            return replayButton;
        }

        void OnReplay() => ReplayRequested?.Invoke();
    }
}
