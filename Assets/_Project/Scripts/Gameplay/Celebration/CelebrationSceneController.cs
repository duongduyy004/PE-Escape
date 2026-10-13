using System.Globalization;
using System.Linq;
using KMA.Gameplay.Core;
using KMA.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KMA.Gameplay.Celebration
{
    public sealed class CelebrationSceneController : MonoBehaviour
    {
        static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

        [SerializeField] Image student;
        [SerializeField] Image classmate;
        [SerializeField] Image teacher;
        [SerializeField] Sprite[] studentFrames;   // idle, happy, happy
        [SerializeField] Sprite[] classmateFrames; // idle, cheer0, cheer1
        [SerializeField] Sprite[] teacherFrames;   // strictLook, scoreHold, scoreWrite
        [SerializeField] GameObject bubble;
        [SerializeField] TMP_Text bubbleText;
        [SerializeField] UiConfetti confetti;
        [SerializeField] CanvasGroup summaryGroup;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text[] rows;
        [SerializeField] TMP_Text footnote;
        [SerializeField] Button skipButton;
        [SerializeField] Button menuButton;
        [SerializeField] Button replayButton;
        [SerializeField] Image fade;
        const float FadeSeconds = .6f;
        float frameClock;
        bool leaving;

        public CelebrationTimeline Timeline { get; } = new CelebrationTimeline();
        public CelebrationSummary Summary { get; private set; }
        public bool SummaryVisible => summaryGroup.alpha >= .99f;
        public string[] RowTexts => rows.Select(row => row.text).ToArray();

        public void Configure(Image studentImage, Image classmateImage, Image teacherImage, Sprite[] studentSet,
            Sprite[] classmateSet, Sprite[] teacherSet, GameObject speech, TMP_Text speechText, UiConfetti paper,
            CanvasGroup summaryPanel, TMP_Text titleLabel, TMP_Text[] rowLabels, TMP_Text note, Button skip,
            Button menu, Button replay)
        {
            student = studentImage; classmate = classmateImage; teacher = teacherImage;
            studentFrames = studentSet; classmateFrames = classmateSet; teacherFrames = teacherSet;
            bubble = speech; bubbleText = speechText; confetti = paper; summaryGroup = summaryPanel;
            title = titleLabel; rows = rowLabels; footnote = note;
            skipButton = skip; menuButton = menu; replayButton = replay;
        }

        public void SetFade(Image cover) => fade = cover;

        void Start()
        {
            GameSession session = SceneRouter.Instance != null ? SceneRouter.Instance.Session
                : GameManager.Instance != null ? GameManager.Instance.Session : null;
            bool real = session != null && session.Journey.CourseComplete;
            Summary = real ? CelebrationSummary.From(session) : CelebrationSummary.Sample();
            // Progress is already saved by the result commit; this only records that the first celebration ran.
            if (real && GameManager.Instance != null && !GameManager.Instance.TryMarkCelebrationSeen(out string error))
                Debug.LogWarning("[KMA] Could not save the celebration flag: " + error);

            Timeline.SummaryRequested += OnSummaryRequested;
            skipButton.onClick.AddListener(Skip);
            menuButton.onClick.AddListener(GoToMenu);
            replayButton.onClick.AddListener(GoToMap);
            FillSummary();
            summaryGroup.alpha = 0f;
            summaryGroup.interactable = summaryGroup.blocksRaycasts = false;
            bubble.SetActive(false);
        }

        public void Skip() => Timeline.Skip();

        void Update()
        {
            Timeline.Tick(Time.deltaTime);
            UpdateFade();
            frameClock += Time.deltaTime;
            int cheer = Mathf.FloorToInt(frameClock / .35f) % 2 == 0 ? 1 : 2;
            CelebrationBeat beat = Timeline.Beat;
            student.sprite = studentFrames[beat == CelebrationBeat.Arrive ? 0 : cheer];
            classmate.sprite = classmateFrames[beat == CelebrationBeat.Arrive ? 0 : 3 - cheer];
            teacher.sprite = teacherFrames[beat == CelebrationBeat.Arrive || beat == CelebrationBeat.Cheer ? 0
                : Timeline.Time < CelebrationTimeline.TeacherAt + 1f ? 1 : 2];
            if (beat != CelebrationBeat.Arrive) confetti.Play();
            if (beat == CelebrationBeat.Teacher && !bubble.activeSelf && !Timeline.Skipped)
            {
                bubble.SetActive(true);
                bubbleText.text = VietText.Fix("Tốt, cậu này được. Qua rồi thì về, đừng đứng chắn sân.");
            }
            if (Timeline.SummaryShown)
                summaryGroup.alpha = Mathf.MoveTowards(summaryGroup.alpha, 1f, Time.deltaTime * 3f);
        }

        void UpdateFade()
        {
            if (fade == null || !fade.gameObject.activeSelf) return;
            float alpha = Timeline.Skipped ? 0f : 1f - Mathf.Clamp01(Timeline.Time / FadeSeconds);
            fade.color = new Color(0f, 0f, 0f, alpha);
            if (alpha <= 0f) fade.gameObject.SetActive(false);
        }

        void OnSummaryRequested()
        {
            skipButton.gameObject.SetActive(false);
            bubble.SetActive(false);
            summaryGroup.interactable = summaryGroup.blocksRaycasts = true;
        }

        void FillSummary()
        {
            // The title is a Shell title: a gold drop copy sits behind the face text, so both change together.
            ShellKit.SetTitleText(title.transform.parent, "HOÀN THÀNH HỌC PHẦN THỂ CHẤT");
            for (int i = 0; i < Summary.Subjects.Count && i < 3; i++)
            {
                CelebrationRow row = Summary.Subjects[i];
                string score = row.HasScore
                    ? $"  ·  {row.Score.ToString("0.0", Vietnamese)} điểm  ·  Hạng {RankLabels.For(row.Rank)}" : string.Empty;
                rows[i].text = VietText.Fix((row.Completed ? "Đạt  " : "Chưa đạt  ") + row.Title + score);
            }
            rows[3].text = VietText.Fix(Summary.ChessLine);
            footnote.text = VietText.Fix(Summary.IsSample ? "Dữ liệu mẫu"
                : Summary.SupplementaryRounds > 0 ? $"Thi bổ sung: {Summary.SupplementaryRounds} đợt" : string.Empty);
        }

        public void GoToMenu()
        {
            if (leaving) return;
            leaving = true;
            if (SceneRouter.Instance != null && SceneRouter.Instance.RouteToMenu()) return;
            if (SceneRouter.Instance == null) SceneManager.LoadScene("Menu");
            else leaving = false;
        }

        public void GoToMap()
        {
            if (leaving) return;
            leaving = true;
            if (SceneRouter.Instance != null && SceneRouter.Instance.Route(SessionRoute.Map)) return;
            if (SceneRouter.Instance == null) SceneManager.LoadScene("Map");
            else leaving = false;
        }
    }
}
