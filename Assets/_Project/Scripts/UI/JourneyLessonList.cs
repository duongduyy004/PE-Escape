using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KMA.Gameplay;
using KMA.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KMA.Gameplay.UI
{
    public sealed class JourneyLessonList : MonoBehaviour
    {
        static readonly SubjectId[] CourseOrder =
            { SubjectId.Sprint, SubjectId.Volleyball, SubjectId.Football, SubjectId.Chess };
        static readonly string[] CourseTitles = { "Chạy nước rút", "Bóng chuyền", "Bóng đá", "Bài kiểm tra cuối" };
        readonly List<StageNode> stageNodes = new List<StageNode>(JourneyLessonPresentation.StageCount);
        readonly List<StageState> stages = new List<StageState>(JourneyLessonPresentation.StageCount);
        TMP_Text heading;
        TMP_Text progress;
        Image progressStar;
        TMP_Text objective;
        TMP_Text status;
        Button playButton;
        TMP_Text playLabel;
        Button closeButton;
        Button scrim;
        GameSession session;
        Action<string, ChallengeAttemptMode> onSelected;
        SubjectId selectedSubject;
        bool hasBound;
        string lastCheckpointId;
        Coroutine revealRoutine;
        int currentCardIndex = -1;

        readonly List<string> lessonIds = new List<string>(JourneyLessonPresentation.StageCount);
        public IReadOnlyList<string> LessonIds => lessonIds;
        public string CurrentChallengeId { get; private set; }
        public SubjectId SelectedSubject => selectedSubject;
        /// The stage whose objective and play button the detail card shows.
        public int SelectedLessonIndex { get; private set; }
        public bool IsOpen => gameObject.activeSelf;

        public static JourneyLessonList Create(Transform parent)
        {
            RectTransform panel = JourneyLessonPresentation.Create(parent);
            JourneyLessonList list = panel.gameObject.AddComponent<JourneyLessonList>();
            list.CacheChildReferences();
            return list;
        }

        public void Bind(GameSession session, Action<string, ChallengeAttemptMode> onSelected)
        {
            CacheChildReferences();
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.onSelected = onSelected;
            string checkpoint = session.Journey.CheckpointChallengeId;
            bool reveal = !hasBound || checkpoint != lastCheckpointId;
            if (reveal && !string.IsNullOrEmpty(checkpoint))
                selectedSubject = session.Journey.Catalog.Get(checkpoint).Subject;
            else if (!session.Journey.IsSubjectUnlocked(selectedSubject) && !string.IsNullOrEmpty(checkpoint))
                selectedSubject = session.Journey.Catalog.Get(checkpoint).Subject;
            hasBound = true;
            lastCheckpointId = checkpoint;
            Refresh(reveal);
        }

        public void ShowSubject(SubjectId subject)
        {
            if (session == null || !session.Journey.IsSubjectUnlocked(subject) || selectedSubject == subject)
                return;
            selectedSubject = subject;
            Refresh(true);
        }

        /// Pops the lesson menu up over the map for an unlocked subject; locked stops stay closed.
        public void Open(SubjectId subject)
        {
            if (session == null || !session.Journey.IsSubjectUnlocked(subject))
                return;
            selectedSubject = subject;
            SetOpen(true);
            Refresh(true);
        }

        public void Close() => SetOpen(false);

        public void ShowStartError(string error)
        {
            if (status != null) status.text = VietText.Fix(error);
            if (playLabel != null && playButton != null && playButton.interactable)
                playLabel.text = VietText.Fix("THỬ LẠI  ›");
        }

        /// Highlights a stage and shows its objective; it never starts the stage, the play button does.
        public void SelectLesson(int index)
        {
            if (index < 0 || index >= stages.Count) return;
            SelectedLessonIndex = index;
            ApplySelection();
        }

        void SetOpen(bool open)
        {
            CacheChildReferences();
            if (!open && revealRoutine != null)
            {
                StopCoroutine(revealRoutine);
                revealRoutine = null;
            }
            if (scrim != null) scrim.gameObject.SetActive(open);
            gameObject.SetActive(open);
        }

        public void Refresh() => Refresh(false);

        // A reveal (opening, switching subject, a new checkpoint) also moves the selection back to
        // the next lesson; a quiet refresh such as a regenerated life keeps what the player picked.
        void Refresh(bool reveal)
        {
            CacheChildReferences();
            if (session == null) return;
            int subjectIndex = Array.IndexOf(CourseOrder, selectedSubject);
            if (subjectIndex < 0) subjectIndex = 0;
            if (heading != null)
                heading.text = VietText.Fix(CourseTitles[subjectIndex]);
            Color chapterColor = ChapterColor(selectedSubject);
            transform.Find("ChapterAccent").GetComponent<Image>().color = chapterColor;
            RefreshCourseIcon(subjectIndex, chapterColor);

            ChallengeDefinition[] challenges = session.Journey.Catalog.Ordered
                .Where(challenge => challenge.Subject == selectedSubject).ToArray();
            int count = Mathf.Min(challenges.Length, stageNodes.Count);
            JourneyLessonPresentation.LayoutStages((RectTransform)transform, count);
            lessonIds.Clear();
            stages.Clear();
            currentCardIndex = -1;
            for (int index = 0; index < stageNodes.Count; index++)
            {
                StageNode node = stageNodes[index];
                node.Button.gameObject.SetActive(index < count);
                if (index >= count) continue;

                ChallengeDefinition challenge = challenges[index];
                lessonIds.Add(challenge.Id);
                bool complete = session.Journey.IsChallengeComplete(challenge.Id);
                bool checkpoint = challenge.Id == session.Journey.CheckpointChallengeId;
                bool unlocked = complete || checkpoint || session.Journey.CourseComplete;
                bool outOfLives = checkpoint && !complete && !session.Journey.CourseComplete &&
                    JourneyProgress.IsPenalizedKind(challenge.Kind) && session.Lives == 0;
                ChallengeAttemptMode mode = session.Journey.CourseComplete
                    ? ChallengeAttemptMode.FreePlay
                    : complete ? ChallengeAttemptMode.Review
                        : checkpoint ? ChallengeAttemptMode.Journey : ChallengeAttemptMode.Review;
                var state = new StageState(challenge, index, complete, checkpoint, unlocked, outOfLives, mode);
                stages.Add(state);
                ApplyNodeState(node, state, chapterColor);
                if (checkpoint) currentCardIndex = index;
                node.Button.onClick.RemoveAllListeners();
                int selectedIndex = index;
                node.Button.onClick.AddListener(() => SelectLesson(selectedIndex));
            }

            int completedCount = challenges.Count(challenge => session.Journey.IsChallengeComplete(challenge.Id));
            if (progress != null)
                progress.text = VietText.Fix($"{completedCount}/{challenges.Length} bài hoàn thành");
            for (int index = 0; index < stageNodes.Count - 1; index++)
            {
                bool passed = index < stages.Count && stages[index].Complete;
                Transform road = transform.Find($"LessonRoad{index + 1}");
                if (road == null) continue;
                foreach (Image dot in road.GetComponentsInChildren<Image>(true))
                    dot.color = passed ? chapterColor : UITheme.Shared.MapLockedBorder;
            }

            string current = session.Journey.CheckpointChallengeId;
            CurrentChallengeId = !string.IsNullOrEmpty(current) &&
                session.Journey.Catalog.Get(current).Subject == selectedSubject ? current : null;

            if (reveal || SelectedLessonIndex >= stages.Count)
                SelectedLessonIndex = currentCardIndex >= 0 ? currentCardIndex : 0;
            ApplySelection();

            if (reveal) PlayReveal();
        }

        static Color ChapterColor(SubjectId subject) => subject switch
        {
            SubjectId.Volleyball => UITheme.Shared.LessonJourney.volleyball,
            SubjectId.Football => UITheme.Shared.LessonJourney.football,
            SubjectId.Chess => UITheme.Shared.LessonJourney.chess,
            _ => UITheme.Shared.LessonJourney.sprint
        };

        void RefreshCourseIcon(int subjectIndex, Color chapterColor)
        {
            Transform courseIcon = transform.Find("CourseIcon");
            courseIcon.GetComponent<Image>().color = chapterColor;
            for (int i = 0; i < courseIcon.childCount; i++)
                courseIcon.GetChild(i).gameObject.SetActive(i == subjectIndex);
            // Runtime-drawn sprites do not survive a scene bake; hand them back on every refresh.
            if (progressStar != null) progressStar.sprite = MapStopBuilder.StarSprite();
        }

        static void ApplyNodeState(StageNode node, StageState state, Color chapterColor)
        {
            UITheme theme = UITheme.Shared;
            node.Disc.color = state.Unlocked ? chapterColor : theme.MapLockedCard;
            node.Disc.rectTransform.localScale = Vector3.one *
                (state.Checkpoint ? theme.LessonJourney.nodeCurrentScale : 1f);
            node.Glyph.color = theme.Surface;
            node.Glow.gameObject.SetActive(state.Checkpoint);
            node.Glow.color = MinigameUiTheme.WithAlpha(theme.Accent, theme.LessonJourney.glowAlpha.x);
            bool showBadge = (state.Complete && !state.Checkpoint) || !state.Unlocked;
            node.Badge.gameObject.SetActive(showBadge);
            node.Badge.color = state.Unlocked ? theme.Success : theme.MapLockedBorder;
            node.CompletedMark.SetActive(state.Unlocked);
            node.LockIcon.gameObject.SetActive(!state.Unlocked);
            node.LockIcon.sprite = MapPresentationBuilder.LockSprite();
            node.Title.text = VietText.Fix(StageName(state.Challenge.Kind));
            node.Title.color = state.Unlocked ? theme.TextPrimary : theme.MapHint;
        }

        static string StageName(ChallengeKind kind) => kind switch
        {
            ChallengeKind.Learn => "HỌC",
            ChallengeKind.Practice => "LUYỆN",
            ChallengeKind.Final => "CUỐI",
            _ => "THI"
        };

        void ApplySelection()
        {
            for (int index = 0; index < stageNodes.Count; index++)
                stageNodes[index].SelectedRing.gameObject.SetActive(index < stages.Count && index == SelectedLessonIndex);
            if (SelectedLessonIndex < 0 || SelectedLessonIndex >= stages.Count)
            {
                if (playButton != null) playButton.interactable = false;
                return;
            }

            StageState state = stages[SelectedLessonIndex];
            if (objective != null) objective.text = VietText.Fix(state.Challenge.Objective);
            if (status != null) status.text = VietText.Fix(StatusText(state));
            if (playLabel != null)
                playLabel.text = VietText.Fix(!state.Unlocked ? "KHÓA"
                    : state.OutOfLives ? "CHỜ HỒI LƯỢT"
                    : session.Journey.CourseComplete ? "CHƠI LẠI  ›"
                    : state.Complete ? "ÔN LẠI  ›" : "CHƠI  ›");
            if (playButton != null)
            {
                playButton.interactable = state.Unlocked && !state.OutOfLives;
                playButton.onClick.RemoveAllListeners();
                string id = state.Challenge.Id;
                ChallengeAttemptMode mode = state.Mode;
                playButton.onClick.AddListener(() => onSelected?.Invoke(id, mode));
            }
        }

        string StatusText(StageState state)
        {
            if (!state.Unlocked)
                return state.Challenge.Kind == ChallengeKind.Final ? "Đạt Bóng đá để mở"
                    : state.Index == 1 ? "Hoàn thành HỌC để mở" : "Hoàn thành LUYỆN để mở";
            if (state.OutOfLives) return "Hết lượt thi · Chờ hồi lượt để thi tiếp";
            if (session.Journey.CourseComplete) return "Đã hoàn thành khóa học · Chơi lại thoải mái";
            if (state.Complete) return "Đã hoàn thành · Ôn lại bất cứ lúc nào";
            return "Bài tiếp theo của bạn";
        }

        void PlayReveal()
        {
            if (revealRoutine != null) StopCoroutine(revealRoutine);
            // A closed popup can't run coroutines; Open replays the reveal anyway.
            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                foreach (StageNode node in stageNodes)
                {
                    node.Group.alpha = 1f;
                    node.Group.interactable = true;
                    node.Group.blocksRaycasts = true;
                    node.Button.transform.localScale = Vector3.one;
                }
                revealRoutine = null;
                return;
            }
            revealRoutine = StartCoroutine(RevealNodes());
        }

        IEnumerator RevealNodes()
        {
            UITheme.LessonJourneyStyle style = UITheme.Shared.LessonJourney;
            for (int i = 0; i < stageNodes.Count; i++)
            {
                StageNode node = stageNodes[i];
                if (!node.Button.gameObject.activeSelf) continue;
                node.Group.alpha = 0f;
                node.Group.interactable = false;
                node.Group.blocksRaycasts = false;
                node.Button.transform.localScale = Vector3.one * style.revealScale;
            }
            for (int i = 0; i < stageNodes.Count; i++)
            {
                StageNode node = stageNodes[i];
                if (!node.Button.gameObject.activeSelf) continue;
                yield return new WaitForSecondsRealtime(style.revealStagger);
                float elapsed = 0f;
                float duration = style.revealDuration;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    // Overshoot slightly so each stage pops onto the road.
                    float eased = 1f + 2.2f * Mathf.Pow(t - 1f, 3f) + 1.2f * Mathf.Pow(t - 1f, 2f);
                    node.Group.alpha = Mathf.Clamp01(t * 2f);
                    node.Button.transform.localScale = Vector3.one * Mathf.LerpUnclamped(style.revealScale, 1f, eased);
                    yield return null;
                }
                node.Group.alpha = 1f;
                node.Group.interactable = true;
                node.Group.blocksRaycasts = true;
                node.Button.transform.localScale = Vector3.one;
            }
            revealRoutine = null;
        }

        void Update()
        {
            // Android's Back button arrives as Escape; it closes the popup instead of leaving the map.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }
            if (currentCardIndex < 0 || currentCardIndex >= stageNodes.Count)
                return;
            UITheme.LessonJourneyStyle style = UITheme.Shared.LessonJourney;
            float pulse = (Mathf.Sin(Time.unscaledTime * style.glowSpeed) + 1f) * .5f;
            stageNodes[currentCardIndex].Glow.color = MinigameUiTheme.WithAlpha(UITheme.Shared.Accent,
                Mathf.Lerp(style.glowAlpha.x, style.glowAlpha.y, pulse));
        }

        void CacheChildReferences()
        {
            if (heading == null) heading = transform.Find("CourseTitle")?.GetComponent<TMP_Text>();
            if (progress == null) progress = transform.Find("CourseProgress")?.GetComponent<TMP_Text>();
            if (progressStar == null) progressStar = transform.Find("ProgressStar")?.GetComponent<Image>();
            if (objective == null) objective = transform.Find("DetailCard/Objective")?.GetComponent<TMP_Text>();
            if (status == null) status = transform.Find("DetailCard/Status")?.GetComponent<TMP_Text>();
            if (playButton == null)
            {
                playButton = transform.Find("DetailCard/PlayButton")?.GetComponent<Button>();
                playLabel = playButton != null ? playButton.transform.Find("Label")?.GetComponent<TMP_Text>() : null;
            }
            if (closeButton == null)
            {
                closeButton = transform.Find("CloseButton")?.GetComponent<Button>();
                if (closeButton != null)
                {
                    closeButton.onClick.RemoveListener(Close);
                    closeButton.onClick.AddListener(Close);
                }
            }
            if (scrim == null && transform.parent != null)
            {
                scrim = transform.parent.Find("LessonScrim")?.GetComponent<Button>();
                if (scrim != null)
                {
                    scrim.onClick.RemoveListener(Close);
                    scrim.onClick.AddListener(Close);
                }
            }
            if (stageNodes.Count == 0)
            {
                for (int index = 0; index < JourneyLessonPresentation.StageCount; index++)
                {
                    Transform item = transform.Find($"Lesson{index + 1}");
                    if (item == null) continue;
                    stageNodes.Add(new StageNode(item));
                }
            }
        }

        readonly struct StageState
        {
            public readonly ChallengeDefinition Challenge;
            public readonly int Index;
            public readonly bool Complete;
            public readonly bool Checkpoint;
            public readonly bool Unlocked;
            public readonly bool OutOfLives;
            public readonly ChallengeAttemptMode Mode;

            public StageState(ChallengeDefinition challenge, int index, bool complete, bool checkpoint,
                bool unlocked, bool outOfLives, ChallengeAttemptMode mode)
            {
                Challenge = challenge;
                Index = index;
                Complete = complete;
                Checkpoint = checkpoint;
                Unlocked = unlocked;
                OutOfLives = outOfLives;
                Mode = mode;
            }
        }

        sealed class StageNode
        {
            public readonly Button Button;
            public readonly CanvasGroup Group;
            public readonly Image Disc;
            public readonly Image Glow;
            public readonly Image SelectedRing;
            public readonly Image Glyph;
            public readonly Image Badge;
            public readonly GameObject CompletedMark;
            public readonly Image LockIcon;
            public readonly TMP_Text Title;

            public StageNode(Transform root)
            {
                Button = root.GetComponent<Button>();
                Group = root.GetComponent<CanvasGroup>();
                Disc = root.Find("StageIcon").GetComponent<Image>();
                Glow = root.Find("StageIcon/Glow").GetComponent<Image>();
                SelectedRing = root.Find("StageIcon/SelectedRing").GetComponent<Image>();
                Glyph = root.Find("StageIcon/Glyph").GetComponent<Image>();
                Badge = root.Find("StageIcon/StateBadge").GetComponent<Image>();
                CompletedMark = root.Find("StageIcon/StateBadge/CompletedMark").gameObject;
                LockIcon = root.Find("StageIcon/StateBadge/LockIcon").GetComponent<Image>();
                Title = root.Find("StageTitle").GetComponent<TMP_Text>();
            }
        }
    }
}
