using System;
using System.Collections;
using System.Linq;
using KMA.Gameplay;
using KMA.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KMA.Gameplay.UI
{
    /// The shared result screen of every minigame. Tiếp tục raises the preview route; Chơi lại
    /// (only when a retry is configured) raises ResultPanelActions.Retry.
    public sealed class ResultPanel : MonoBehaviour, IRetryResultPreviewPanel, IChallengeResultPanel,
        IFrogJumpResultPanel
    {
        const int SortingOrder = 500;

        /// Caption above the score number; the prefab styler bakes the same text.
        public const string ScoreCaptionText = "ĐIỂM";
        static float ScrimDuration => Mathf.Max(0f, UITheme.Shared.Motion.resultScrim);
        static float ModalDuration => Mathf.Max(0f, UITheme.Shared.Motion.resultModal);
        static float TitleDuration => Mathf.Max(0f, UITheme.Shared.Motion.resultTitle);
        static float ScoreDuration => Mathf.Max(0f, UITheme.Shared.Motion.resultScore);
        static float RankDuration => Mathf.Max(0f, UITheme.Shared.Motion.resultRank);

        [SerializeField] GameObject contentRoot;
        [SerializeField] TMP_Text statusLabel;
        [SerializeField] TMP_Text scoreLabel;
        [SerializeField] TMP_Text rankLabel;
        [SerializeField] TMP_Text detailLabel;
        [SerializeField] TMP_Text livesLabel;
        [SerializeField] TMP_Text errorLabel;
        [SerializeField] Button actionButton;
        [SerializeField] Button retryButton;

        [SerializeField] GameplayDialogueHost dialogueHost;
        [SerializeField] GameplayDialogueView dialogueView;
        [SerializeField] ScriptableObject frogBalance;
        readonly System.Collections.Generic.HashSet<string> spokenAttempts=new System.Collections.Generic.HashSet<string>();
        bool frogSpoken;
        public void BindDialogue(GameplayDialogueHost host)
        {
            dialogueHost=host;
            var panelCanvas=UiKit.GetOrAdd<Canvas>(gameObject);panelCanvas.overrideSorting=true;panelCanvas.sortingOrder=SortingOrder;
            if(dialogueView==null && transform is RectTransform)
            {
                var root=UiKit.Rect(transform,"ResultDialogue");UiKit.Stretch(root);
                dialogueView=root.gameObject.AddComponent<GameplayDialogueView>();
                dialogueView.SetSlot(DialogueHudSlot.Result);
                dialogueView.Configure(JourneyDialogueLibrary.LoadDefault(),root);
            }
            if(dialogueView!=null)
            {
                var canvas=UiKit.GetOrAdd<Canvas>(dialogueView.gameObject);
                canvas.overrideSorting=true;canvas.sortingOrder=SortingOrder+100;
            }
        }
        float PenaltyTimeLimit => frogBalance is IFrogJumpTimingSource timing ? timing.TimeLimitSeconds : 30f;
        void SpeakResult(bool pass,ChallengeKind kind,string attemptId,JourneyCommitOutcome? outcome,string error)
        {
            if(dialogueHost==null)return;
            if(dialogueView==null)BindDialogue(dialogueHost);
            dialogueHost.BeginResult();
            dialogueHost.SetView(dialogueView);
            if(!string.IsNullOrEmpty(error)){dialogueHost.Clear();return;}
            if(attemptId!=null && spokenAttempts.Contains(attemptId))return;
            string id=ResultDialoguePolicy.Select(pass,kind,dialogueHost.PreferredResultEventId,outcome,error);
            if(id!=null && dialogueHost.Emit(id) && attemptId!=null)spokenAttempts.Add(attemptId);
        }

        string successTitle = "CHIẾN THẮNG";
        string failureTitle = "THẤT BẠI";
        int remainingLives;
        bool retryConfigured;
        bool retryAvailable;
        bool actionPending;
        bool listenersBound;
        bool frogMode;
        string finalScoreText = string.Empty;
        ChallengeAttemptContext challengeContext;
        string challengeSaveError;
        JourneyCommitOutcome? challengeOutcome;
        ChallengeAttemptResult challengeResult;

        public event Action<string> ActionRequested;
        public event Action<JourneyResultAction> JourneyActionRequested;
        public event Action FrogJumpContinueRequested;

        public MinigameResult CurrentResult { get; private set; }
        public string PreviewRoute { get; private set; } = string.Empty;
        public bool HasContinued { get; private set; }
        public bool RetryAvailable => retryAvailable;
        public bool IsActionPending => actionPending;
        public bool IsVisible => contentRoot ? contentRoot.activeInHierarchy : gameObject.activeInHierarchy;
        public bool ContinueInteractable => actionButton && actionButton.interactable;
        public string StatusText => statusLabel ? statusLabel.text : null;
        public bool SupportsRetry => retryButton && detailLabel && livesLabel;
        /// The main button's caption exactly as SetButtonLabel wrote it (VietText-normalized).
        public string ContinueLabel => actionButton != null
            ? actionButton.GetComponentInChildren<TMP_Text>(true)?.text : null;

        /// Wires a panel built in code; prefab instances are wired by MinigamePrefabStyler.
        public void Configure(GameObject content, TMP_Text status, TMP_Text detail, TMP_Text score, TMP_Text rank,
            TMP_Text lives, Button continueAction, Button retryAction, TMP_Text error = null)
        {
            contentRoot = content;
            statusLabel = status;
            detailLabel = detail;
            scoreLabel = score;
            rankLabel = rank;
            livesLabel = lives;
            actionButton = continueAction;
            retryButton = retryAction;
            errorLabel = error;
            BindButtons();
            RefreshButtons();
        }

        public bool ValidateReferences() => contentRoot && statusLabel && scoreLabel && rankLabel && actionButton;

        public void SetTitles(string success, string failure)
        {
            successTitle = success ?? successTitle;
            failureTitle = failure ?? failureTitle;
        }

        public void SetDetail(string text)
        {
            if (detailLabel == null)
                return;
            detailLabel.text = VietText.Fix(text ?? string.Empty);
            detailLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        public void ConfigureRetry(int livesAfterFailure)
        {
            retryConfigured = true;
            remainingLives = Mathf.Max(0, livesAfterFailure);
            retryAvailable = remainingLives > 0;
            RefreshButtons();
        }

        /// The HUD canvas renders through the camera at order 0, so world sprites with higher
        /// sorting orders (the runners) would draw over the panel. A nested canvas lifts it above them.
        void RaiseAboveWorldSprites()
        {
            var canvas = UiKit.GetOrAdd<Canvas>(gameObject);
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
            if(dialogueView!=null)
            {
                var speechCanvas=UiKit.GetOrAdd<Canvas>(dialogueView.gameObject);
                speechCanvas.overrideSorting=true;speechCanvas.sortingOrder=SortingOrder+100;
            }
            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();
        }

        public void Show(MinigameResult result, string previewRoute)
        {
            frogMode = false;
            ApplyFrogLayout(false);
            challengeContext = null;
            challengeSaveError = null;
            challengeOutcome = null;
            challengeResult = null;
            CurrentResult = result ?? throw new ArgumentNullException(nameof(result));
            PreviewRoute = previewRoute ?? string.Empty;
            HasContinued = false;
            actionPending = false;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            RaiseAboveWorldSprites();
            if (contentRoot != null)
                contentRoot.SetActive(true);
            ApplyTheme();
            if (errorLabel != null)
                errorLabel.text = VietText.Fix(string.Empty);
            if (statusLabel != null)
            {
                statusLabel.text = VietText.Fix(result.Pass ? successTitle : failureTitle);
                statusLabel.color = result.Pass ? MinigameUiTheme.Success : MinigameUiTheme.Energy;
            }
            finalScoreText = FormatScore(result.Score);
            if (scoreLabel != null)
                scoreLabel.text = VietText.Fix(finalScoreText);
            if (rankLabel != null)
                rankLabel.text = VietText.Fix($"XẾP HẠNG {result.Rank}");
            if (livesLabel != null)
            {
                livesLabel.text = VietText.Fix($"LƯỢT THI: {remainingLives}/{GameSession.MaxLives}");
                livesLabel.gameObject.SetActive(retryConfigured);
            }
            SetButtonLabel(actionButton, "TIẾP TỤC");
            SetButtonLabel(retryButton, "CHƠI LẠI");

            retryAvailable = !result.Pass && remainingLives > 0;
            RefreshButtons();
            SpeakResult(result.Pass,ChallengeKind.Exam,null,null,null);
            Reveal(result.Score);
        }

        public void Continue()
        {
            if (CurrentResult == null || HasContinued || actionPending)
                return;
            HasContinued = true;
            if (frogMode)
            {
                FrogJumpContinueRequested?.Invoke();
                return;
            }
            if (challengeContext != null)
            {
                JourneyActionRequested?.Invoke(challengeSaveError != null && !challengeOutcome.HasValue
                    ? JourneyResultAction.RetrySave
                    : challengeOutcome.HasValue && challengeOutcome.Value.FrogJumpRequired
                        ? JourneyResultAction.FrogJump : JourneyResultAction.Continue);
                return;
            }
            ActionRequested?.Invoke(PreviewRoute);
        }

        public void Retry()
        {
            if (CurrentResult == null || HasContinued || actionPending || !retryAvailable)
                return;
            HasContinued = true;
            if (challengeContext != null)
            {
                JourneyActionRequested?.Invoke(JourneyResultAction.Retry);
                return;
            }
            ActionRequested?.Invoke(ResultPanelActions.Retry);
        }

        public void ShowChallenge(ChallengeAttemptContext context, ChallengeAttemptResult result,
            JourneyCommitOutcome? outcome, string saveError)
        {
            frogMode = false;
            ApplyFrogLayout(false);
            challengeContext = context;
            challengeSaveError = outcome.HasValue ? null : saveError;
            challengeOutcome = outcome;
            challengeResult = result;
            var display = result.ExamResult ?? new MinigameResult(result.Pass,
                result.Metrics.CompletedTargets, result.Pass ? Rank.C : Rank.F);
            ShowChallengeCore(context, display, result, outcome, saveError);
        }

        void ShowChallengeCore(ChallengeAttemptContext context, MinigameResult display,
            ChallengeAttemptResult result, JourneyCommitOutcome? outcome, string saveError)
        {
            CurrentResult = display;
            PreviewRoute = string.Empty;
            HasContinued = false;
            actionPending = false;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            RaiseAboveWorldSprites();
            if (contentRoot != null) contentRoot.SetActive(true);
            ApplyTheme();
            if (statusLabel != null)
            {
                bool practice = context.Mode != ChallengeAttemptMode.Journey;
                statusLabel.text = VietText.Fix(result.Pass ? practice ? "LUYỆN TẬP HOÀN THÀNH" : successTitle
                    : failureTitle);
                statusLabel.color = result.Pass ? MinigameUiTheme.Success : MinigameUiTheme.Energy;
            }
            finalScoreText = result.ExamResult == null
                ? result.Metrics.CompletedTargets.ToString()
                : FormatScore(result.ExamResult.Score);
            if (scoreLabel != null) scoreLabel.text = VietText.Fix(finalScoreText);
            if (rankLabel != null)
            {
                rankLabel.text = result.ExamResult == null
                    ? VietText.Fix($"{result.Metrics.CompletedTargets} MỤC TIÊU")
                    : VietText.Fix($"XẾP HẠNG {result.ExamResult.Rank}");
                rankLabel.gameObject.SetActive(result.ExamResult != null);
            }
            bool frogJump = outcome.HasValue && outcome.Value.FrogJumpRequired;
            bool final = outcome.HasValue && outcome.Value.FinalChallenge;
            string learnDetail = LearnProgressDetail(context, result);
            SetDetail(saveError == null && !string.IsNullOrEmpty(learnDetail) ? learnDetail
                : saveError == null && final && !string.IsNullOrEmpty(result.Metrics.Detail)
                ? result.Metrics.Detail
                : saveError ?? (context.Mode == ChallengeAttemptMode.Journey
                ? outcome.HasValue
                    ? frogJump
                        ? outcome.Value.FrogJumpSavesLife
                            ? $"Bật cóc về đích trong {MinigameGuidePages.Number(PenaltyTimeLimit)} giây để giữ lượt thi."
                            : "Đã trừ 1 lượt thi. Hoàn thành lượt Bật cóc để kết thúc hình phạt."
                        : "Kết quả đã lưu."
                    : "Kết quả đang chờ lưu."
                : $"Thời gian: {MinigameGuidePages.Number(result.Metrics.Elapsed)} giây"));
            if (errorLabel != null) errorLabel.text = VietText.Fix(saveError ?? string.Empty);
            var definition = ChallengeCatalog.LoadDefault().Ordered.FirstOrDefault(d => d != null && d.Id == context.ChallengeId);
            string retryLabel = definition?.Kind switch
            {
                ChallengeKind.Learn => "HỌC LẠI",
                ChallengeKind.Practice => "LUYỆN LẠI",
                _ => "THI LẠI"
            };
            SetButtonLabel(actionButton, saveError == null ? frogJump ? "BẬT CÓC"
                : context.Mode == ChallengeAttemptMode.Journey && !result.Pass ? retryLabel : "TIẾP TỤC"
                : outcome.HasValue ? "THỬ LẠI" : "LƯU LẠI");
            retryAvailable = context.Mode != ChallengeAttemptMode.Journey && result.ExamResult != null &&
                !result.Pass && outcome.HasValue &&
                (final || outcome.Value.AttemptsRemaining > 0) && !frogJump;
            if (retryButton != null)
            {
                retryButton.gameObject.SetActive(retryAvailable);
                SetButtonLabel(retryButton, final ? "CHƠI LẠI" : "THI LẠI");
            }
            if (livesLabel != null)
            {
                int attemptsRemaining = outcome.HasValue ? outcome.Value.AttemptsRemaining : 0;
                livesLabel.text = VietText.Fix($"LƯỢT THI: {attemptsRemaining}/{GameSession.MaxLives}");
                livesLabel.gameObject.SetActive(!final && (result.ExamResult != null || frogJump));
            }
            RefreshButtons();
            SpeakResult(result.Pass,definition?.Kind ?? ChallengeKind.Exam,context.AttemptId,outcome,saveError);
            Reveal(result.ExamResult == null ? result.Metrics.CompletedTargets : result.ExamResult.Score);
        }

        static string LearnProgressDetail(ChallengeAttemptContext context, ChallengeAttemptResult result)
        {
            if (context == null || result == null || context.Mode != ChallengeAttemptMode.Journey)
                return null;
            ChallengeDefinition definition;
            try { definition = ChallengeCatalog.LoadDefault().Get(context.ChallengeId); }
            catch (Exception) { return null; }
            if (definition.Kind != ChallengeKind.Learn)
                return null;
            if (context.ChallengeId == "sprint_learn")
                return $"{MinigameGuidePages.Number(definition.TimeLimit)} GIÂY • CHUỖI ĐÚNG TỐT NHẤT: " +
                    $"{result.Metrics.CompletedTargets}/{definition.TargetCount}";
            if (context.ChallengeId == "volleyball_learn")
                return $"BÀI TẬP: {result.Metrics.CompletedTargets}/{definition.TargetCount}";
            return null;
        }

        // The frog card has no score or rank, so it closes that gap by using a shorter card.
        struct RectState { public Vector2 min, max, size; }
        readonly System.Collections.Generic.Dictionary<RectTransform, RectState> layoutOriginals =
            new System.Collections.Generic.Dictionary<RectTransform, RectState>();

        void SetRect(RectTransform rect, float minY, float maxY)
        {
            if (rect == null) return;
            if (!layoutOriginals.ContainsKey(rect))
                layoutOriginals[rect] = new RectState { min = rect.anchorMin, max = rect.anchorMax, size = rect.sizeDelta };
            rect.anchorMin = new Vector2(rect.anchorMin.x, minY);
            rect.anchorMax = new Vector2(rect.anchorMax.x, maxY);
        }

        void ApplyFrogLayout(bool frog)
        {
            if (!frog)
            {
                foreach (var pair in layoutOriginals)
                {
                    if (pair.Key == null) continue;
                    pair.Key.anchorMin = pair.Value.min;
                    pair.Key.anchorMax = pair.Value.max;
                    pair.Key.sizeDelta = pair.Value.size;
                }
                layoutOriginals.Clear();
                return;
            }
            RectTransform card = contentRoot != null ? contentRoot.GetComponent<RectTransform>() : null;
            if (card == null || layoutOriginals.ContainsKey(card)) return;
            layoutOriginals[card] = new RectState { min = card.anchorMin, max = card.anchorMax, size = card.sizeDelta };
            card.sizeDelta = new Vector2(card.sizeDelta.x, 420f);
            SetRect(statusLabel != null ? statusLabel.rectTransform : null, .74f, .95f);
            SetRect(detailLabel != null ? detailLabel.rectTransform : null, .51f, .63f);
            SetRect(livesLabel != null ? livesLabel.rectTransform : null, .38f, .48f);
            SetRect(actionButton != null ? (RectTransform)actionButton.transform : null, .06f, .27f);
        }

        public void ShowFrogJump(FrogJumpResultView view)
        {
            frogMode = true;
            ApplyFrogLayout(true);
            challengeContext = null;
            challengeSaveError = null;
            challengeOutcome = null;
            challengeResult = null;
            CurrentResult = new MinigameResult(view.ReachedFinish, 0f, view.ReachedFinish ? Rank.C : Rank.F);
            PreviewRoute = string.Empty;
            HasContinued = false;
            actionPending = false;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            RaiseAboveWorldSprites();
            if (contentRoot != null) contentRoot.SetActive(true);
            ApplyTheme();
            if (statusLabel != null)
            {
                statusLabel.text = VietText.Fix(view.ReachedFinish ? "VỀ ĐÍCH" : "HẾT GIỜ");
                statusLabel.color = view.ReachedFinish ? MinigameUiTheme.Success : MinigameUiTheme.Energy;
            }
            // The frog jump has no score: blank the score, hide the rank, and reveal without the
            // count-up so no stray "0" is ever written.
            finalScoreText = string.Empty;
            if (scoreLabel != null) scoreLabel.text = string.Empty;
            if (rankLabel != null) rankLabel.gameObject.SetActive(false);
            SetDetail(!view.SavesLife ? "Lượt thi đã bị trừ khi trượt bài; không trừ thêm."
                : view.ReachedFinish ? "Giữ được lượt thi." : "Đã trừ 1 lượt thi.");
            if (livesLabel != null)
            {
                livesLabel.text = VietText.Fix($"LƯỢT THI: {view.LivesRemaining}/{GameSession.MaxLives}");
                livesLabel.gameObject.SetActive(true);
            }
            if (errorLabel != null) errorLabel.text = VietText.Fix(view.Error ?? string.Empty);
            SetButtonLabel(actionButton, view.LivesRemaining <= 0 ? "VỀ BẢN ĐỒ"
                : view.RetryIsPractice ? "LUYỆN LẠI" : "THI LẠI");
            retryAvailable = false;
            RefreshButtons();
            if(dialogueHost!=null)
            {
                if(dialogueView==null)BindDialogue(dialogueHost);
                dialogueHost.BeginResult();dialogueHost.SetView(dialogueView);
                if(!string.IsNullOrEmpty(view.Error))dialogueHost.Clear();
                else if(!frogSpoken && dialogueHost.PreferredResultEventId==null)
                    dialogueHost.Emit(view.ReachedFinish?"frog.finish":"frog.timeout");
                frogSpoken=true;
            }
            Reveal(0f, scoreless: true);
        }

        public void SetActionPending(bool pending, string error)
        {
            actionPending = pending;
            if (errorLabel != null)
                errorLabel.text = VietText.Fix(error ?? string.Empty);
            if (!pending)
                HasContinued = false;
            RefreshButtons();
        }

        void Awake() => BindButtons();
        void OnEnable() => BindButtons();

        void OnDisable()
        {
            if (!listenersBound)
                return;
            if (actionButton != null) actionButton.onClick.RemoveListener(Continue);
            if (retryButton != null) retryButton.onClick.RemoveListener(Retry);
            listenersBound = false;
        }

        void Update()
        {
            if (IsVisible && CurrentResult != null && Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame)
                Continue();
        }

        void BindButtons()
        {
            if (listenersBound)
                return;
            if (actionButton != null) actionButton.onClick.AddListener(Continue);
            if (retryButton != null) retryButton.onClick.AddListener(Retry);
            listenersBound = actionButton != null || retryButton != null;
        }

        void ApplyTheme()
        {
            Image backdrop = transform.Find("Backdrop")?.GetComponent<Image>();
            if (backdrop != null) backdrop.color = MinigameUiTheme.Scrim;
            Image card = contentRoot != null ? contentRoot.GetComponent<Image>() : null;
            if (card != null) UiKit.StylePanel(card);
            if (scoreLabel != null) scoreLabel.color = MinigameUiTheme.Accent;
            if (rankLabel != null) rankLabel.color = MinigameUiTheme.TextPrimary;
            if (detailLabel != null) detailLabel.color = MinigameUiTheme.TextPrimary;
            if (livesLabel != null) livesLabel.color = MinigameUiTheme.TextPrimary;
            if (errorLabel != null) errorLabel.color = MinigameUiTheme.Energy;
            TMP_Text caption = contentRoot != null
                ? contentRoot.transform.Find("ScoreCaption")?.GetComponent<TMP_Text>() : null;
            if (caption != null)
            {
                caption.color = MinigameUiTheme.WithAlpha(MinigameUiTheme.TextPrimary, .7f);
                caption.text = VietText.Fix(ScoreCaptionText);
                // The frog jump card has no score, so its caption would float over nothing.
                caption.gameObject.SetActive(!frogMode);
            }
            if (actionButton != null) UiKit.StyleButton(actionButton, ButtonVariant.Primary);
            if (retryButton != null) UiKit.StyleButton(retryButton, ButtonVariant.Primary);
        }

        void RefreshButtons()
        {
            if (retryButton != null)
            {
                retryButton.gameObject.SetActive(retryAvailable);
                retryButton.interactable = !actionPending;
            }
            if (actionButton == null)
                return;
            actionButton.interactable = !actionPending;

            bool paired = retryAvailable && retryButton != null;
            var action = (RectTransform)actionButton.transform;
            action.anchorMin = new Vector2(paired ? .52f : .25f, action.anchorMin.y);
            action.anchorMax = new Vector2(paired ? .94f : .75f, action.anchorMax.y);
            if (actionButton.GetComponent<KitPressFeedback>() != null)
                UiKit.ApplyVariant(UiKit.ButtonParts(actionButton), paired ? ButtonVariant.Secondary : ButtonVariant.Primary);
        }

        static void SetButtonLabel(Button button, string value)
        {
            if (button == null)
                return;
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = VietText.Fix(value);
        }

        void Reveal(float finalScore, bool scoreless = false)
        {
            StopAllCoroutines();
            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                SnapRevealed();
                return;
            }
            PrepareReveal(scoreless);
            StartCoroutine(AnimateReveal(finalScore, scoreless));
        }

        /// Puts every animated element in its pre-stage state so nothing shows before its turn.
        void PrepareReveal(bool scoreless)
        {
            CanvasGroup scrim = ScrimGroup();
            if (scrim != null) scrim.alpha = 0f;
            CanvasGroup modal = ModalGroup();
            if (modal != null)
            {
                modal.alpha = 0f;
                contentRoot.transform.localScale = Vector3.one * .9f;
            }
            if (statusLabel != null)
            {
                Color c = statusLabel.color;
                statusLabel.color = new Color(c.r, c.g, c.b, 0f);
            }
            if (scoreLabel != null) scoreLabel.text = scoreless ? string.Empty : VietText.Fix("0");
            if (rankLabel != null) rankLabel.rectTransform.localScale = Vector3.one * .6f;
        }

        CanvasGroup ScrimGroup()
        {
            Transform backdrop = transform.Find("Backdrop");
            return backdrop == null ? null : UiKit.GetOrAdd<CanvasGroup>(backdrop.gameObject);
        }

        CanvasGroup ModalGroup() => contentRoot == null ? null : UiKit.GetOrAdd<CanvasGroup>(contentRoot);

        void SnapRevealed()
        {
            Transform backdrop = transform.Find("Backdrop");
            CanvasGroup scrim = backdrop == null ? null : backdrop.GetComponent<CanvasGroup>();
            if (scrim != null) scrim.alpha = 1f;
            if (contentRoot != null)
            {
                CanvasGroup modal = contentRoot.GetComponent<CanvasGroup>();
                if (modal != null) modal.alpha = 1f;
                contentRoot.transform.localScale = Vector3.one;
            }
            if (rankLabel != null) rankLabel.rectTransform.localScale = Vector3.one;
            if (scoreLabel != null) scoreLabel.text = VietText.Fix(finalScoreText);
        }

        IEnumerator AnimateReveal(float finalScore, bool scoreless)
        {
            yield return FadeGroup(ScrimGroup(), ScrimDuration);
            yield return ScaleAndFadeModal();
            yield return FadeText(statusLabel, TitleDuration);
            if (scoreless)
                yield break;
            yield return CountUpScore(finalScore);
            yield return PopRank();
        }

        static IEnumerator FadeGroup(CanvasGroup group, float duration)
        {
            if (group == null)
                yield break;
            float elapsed = 0f;
            group.alpha = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            group.alpha = 1f;
        }

        IEnumerator ScaleAndFadeModal()
        {
            CanvasGroup group = ModalGroup();
            if (group == null)
                yield break;
            Transform modal = contentRoot.transform;
            float elapsed = 0f;
            group.alpha = 0f;
            while (elapsed < ModalDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / ModalDuration);
                group.alpha = t;
                modal.localScale = Vector3.one * Mathf.Lerp(.9f, 1f, t);
                yield return null;
            }
            group.alpha = 1f;
            modal.localScale = Vector3.one;
        }

        static IEnumerator FadeText(TMP_Text label, float duration)
        {
            if (label == null)
                yield break;
            Color target = label.color;
            target.a = 1f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                label.color = new Color(target.r, target.g, target.b, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            label.color = target;
        }

        // One decimal, like the stored score, so the number never disagrees with the rank beside it.
        static string FormatScore(float score) =>
            score.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

        IEnumerator CountUpScore(float finalScore)
        {
            if (scoreLabel == null)
                yield break;
            float elapsed = 0f;
            while (elapsed < ScoreDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / ScoreDuration);
                float shown = Mathf.Lerp(0f, finalScore, t);
                scoreLabel.text = VietText.Fix(finalScoreText.Contains(".")
                    ? FormatScore(shown) : Mathf.RoundToInt(shown).ToString());
                yield return null;
            }
            scoreLabel.text = VietText.Fix(finalScoreText);
        }

        IEnumerator PopRank()
        {
            if (rankLabel == null)
                yield break;
            RectTransform rect = rankLabel.rectTransform;
            float elapsed = 0f;
            while (elapsed < RankDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / RankDuration);
                float scale = t < .5f ? Mathf.Lerp(.6f, 1.2f, t / .5f) : Mathf.Lerp(1.2f, 1f, (t - .5f) / .5f);
                rect.localScale = Vector3.one * scale;
                yield return null;
            }
            rect.localScale = Vector3.one;
        }
    }
}
