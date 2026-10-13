using KMA.Gameplay;
using KMA.Gameplay.UI;
using KMA.UI.Kit;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Tests.Presentation
{
    public sealed class ResultPanelRetryTests
    {
        GameObject root;
        ResultPanel panel;
        Button continueButton, retryButton;
        TMP_Text status, detail, score, rank, lives, error;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("ResultPanel", typeof(RectTransform));
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(root.transform, false);
            continueButton = UiKit.Button(content.transform, "ActionButton", "TIẾP TỤC", ButtonVariant.Primary).Button;
            retryButton = UiKit.Button(content.transform, "RetryButton", "CHƠI LẠI", ButtonVariant.Primary).Button;
            status = Text(content, "StatusLabel"); detail = Text(content, "DetailLabel");
            score = Text(content, "ScoreLabel"); rank = Text(content, "RankLabel");
            lives = Text(content, "LivesLabel"); error = Text(content, "ErrorLabel");
            panel = root.AddComponent<ResultPanel>();
            panel.Configure(content, status, detail, score, rank, lives, continueButton, retryButton, error);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void FailureShowsRetryOnlyWhenLivesRemainAndReportsDetailAndLives()
        {
            panel.SetDetail("2/5 BÀN");
            panel.ConfigureRetry(2);
            panel.Show(new MinigameResult(false, 0f, Rank.F), "Map");
            Assert.That(panel.IsVisible, Is.True);
            Assert.That(detail.text, Is.EqualTo("2/5 BÀN"));
            Assert.That(lives.text, Is.EqualTo("LƯỢT THI: 2/5"));
            Assert.That(retryButton.gameObject.activeSelf, Is.True);
            Assert.That(continueButton.gameObject.activeSelf, Is.True);
            Assert.That(panel.RetryAvailable, Is.True);
        }

        [Test]
        public void ExhaustedFailureHidesRetryAndPendingActionCanRecoverAfterError()
        {
            panel.ConfigureRetry(0);
            panel.Show(new MinigameResult(false, 0f, Rank.F), "GameOver");
            Assert.That(retryButton.gameObject.activeSelf, Is.False);

            int actions = 0;
            panel.ActionRequested += _ => actions++;
            panel.SetActionPending(true, null);
            panel.Retry();
            panel.Continue();
            Assert.That(actions, Is.Zero);
            Assert.That(panel.ContinueInteractable, Is.False);

            panel.SetActionPending(false, "Could not load scene");
            Assert.That(error.text, Is.EqualTo("Could not load scene"));
            Assert.That(panel.ContinueInteractable, Is.True);
            panel.Continue();
            Assert.That(actions, Is.EqualTo(1));
        }

        [Test]
        public void RetryRaisesTheRetryActionAndContinueRaisesThePreviewRouteOnlyOnce()
        {
            panel.ConfigureRetry(3);
            panel.Show(new MinigameResult(false, 0f, Rank.F), "Map");
            string action = null;
            panel.ActionRequested += requested => action = requested;
            panel.Retry();
            panel.Retry();
            Assert.That(action, Is.EqualTo(ResultPanelActions.Retry));

            panel.Show(new MinigameResult(true, 8f, Rank.A), "Map");
            action = null;
            int count = 0;
            panel.ActionRequested += _ => count++;
            panel.Continue();
            panel.Continue();
            Assert.That(action, Is.EqualTo("Map"));
            Assert.That(count, Is.EqualTo(1));
            Assert.That(retryButton.gameObject.activeSelf, Is.False, "a win never offers a retry");
        }

        [Test]
        public void PanelsWithoutRetryKeepTheirSingleContinueAction()
        {
            var routes = new System.Collections.Generic.List<string>();
            panel.ActionRequested += routes.Add;
            panel.Show(new MinigameResult(false, 0f, Rank.F), "Map");
            Assert.That(retryButton.gameObject.activeSelf, Is.False);
            Assert.That(lives.gameObject.activeSelf, Is.False, "lives only show for panels that offer a retry");
            panel.Continue();
            panel.Continue();
            Assert.That(routes, Is.EqualTo(new[] { "Map" }));
        }

        [Test]
        public void CustomTitlesUseTheSuccessAndEnergyTokens()
        {
            panel.SetTitles("HOÀN THÀNH!", "THẤT BẠI");
            panel.Show(new MinigameResult(true, 8.4f, Rank.A), "Map");
            Assert.That(status.font, Is.SameAs(VietTypography.Library.regular));
            Assert.That(status.fontStyle, Is.EqualTo(FontStyles.Normal));
            Assert.That(status.enableVertexGradient, Is.False);
            Assert.That(status.text, Is.EqualTo("HOÀN THÀNH!"));
            Assert.That(status.color, Is.EqualTo(MinigameUiTheme.Success));
            Assert.That(score.text, Is.EqualTo("8.4"));
            Assert.That(rank.text, Is.EqualTo("XẾP HẠNG A"));
            panel.Show(new MinigameResult(false, 0f, Rank.F), "Map");
            Assert.That(status.text, Is.EqualTo("THẤT BẠI"));
            Assert.That(status.color, Is.EqualTo(MinigameUiTheme.Energy));
        }

        [Test]
        public void LoneContinueIsCentredAndPrimaryWhilePairedContinueIsSecondary()
        {
            panel.Show(new MinigameResult(false, 0f, Rank.F), "Map");
            var action = (RectTransform)continueButton.transform;
            Assert.That(action.anchorMin.x, Is.EqualTo(.25f).Within(.001f));
            Assert.That(UiKit.ButtonParts(continueButton).Fill.gameObject.activeSelf, Is.False, "Primary");

            panel.ConfigureRetry(2);
            panel.Show(new MinigameResult(false, 0f, Rank.F), "Map");
            Assert.That(action.anchorMin.x, Is.EqualTo(.52f).Within(.001f));
            Assert.That(UiKit.ButtonParts(continueButton).Fill.gameObject.activeSelf, Is.True, "Secondary");
        }

        [Test]
        public void ShowOutsidePlayModeSnapsTheRevealToItsFinalState()
        {
            Transform content = root.transform.Find("Content");
            var group = content.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            panel.Show(new MinigameResult(true, 8.4f, Rank.A), "Map");
            Assert.That(content.localScale, Is.EqualTo(Vector3.one));
            Assert.That(group.alpha, Is.EqualTo(1f), "the snap path must leave the modal fully opaque");
            Assert.That(score.text, Is.EqualTo("8.4"));
        }

        [TestCase("sprint_learn", 4, "45 GIÂY", "4/", "HỌC LẠI")]
        [TestCase("volleyball_learn", 6, "BÀI TẬP:", "/7", "HỌC LẠI")]
        public void JourneyLearnResultShowsTimeOrDrillProgress(string id, int completed, string expectedA, string expectedB, string expectedAction)
        {
            var context = new ChallengeAttemptContext("attempt", id, ChallengeAttemptMode.Journey,
                ChallengeDifficulty.Normal);
            var result = new ChallengeAttemptResult(context, false,
                new ChallengeMetrics(elapsed: 45f, completedTargets: completed));
            panel.ShowChallenge(context, result,
                new JourneyCommitOutcome(true, id, 5, false, false, false), null);
            Assert.That(detail.text, Does.Contain(expectedA));
            Assert.That(detail.text, Does.Contain(expectedB));
            Assert.That(continueButton.GetComponentInChildren<TMP_Text>(true).text, Does.Contain(expectedAction));
        }

        // VietText.Fix (NFC) is not visible to this assembly; labels are compared the same way.
        static string Nfc(string value) => value.Normalize(System.Text.NormalizationForm.FormC);
        static string Label(Button button) => button.GetComponentInChildren<TMP_Text>(true).text;

        void ShowJourney(string id, bool pass, JourneyCommitOutcome? outcome, string saveError = null)
        {
            var context = new ChallengeAttemptContext("attempt-" + id, id, ChallengeAttemptMode.Journey,
                ChallengeDifficulty.Normal);
            ChallengeDefinition definition = ChallengeCatalog.LoadDefault().Get(id);
            var result = new ChallengeAttemptResult(context, pass,
                new ChallengeMetrics(elapsed: 40f, completedTargets: pass ? definition.TargetCount : 0),
                ChallengeDefinition.IsScored(definition.Kind)
                    ? new MinigameResult(pass, pass ? 8f : 0f, pass ? Rank.A : Rank.F) : null);
            panel.ShowChallenge(context, result, outcome, saveError);
        }

        void AssertBackPaired(string primary)
        {
            Assert.That(continueButton.gameObject.activeSelf, Is.True);
            Assert.That(retryButton.gameObject.activeSelf, Is.True, "QUAY VỀ takes the second slot");
            Assert.That(panel.BackAvailable, Is.True);
            Assert.That(panel.RetryAvailable, Is.False);
            Assert.That(Label(retryButton), Is.EqualTo(Nfc("QUAY VỀ")));
            Assert.That(panel.ContinueLabel, Is.EqualTo(Nfc(primary)));
            Assert.That(((RectTransform)continueButton.transform).anchorMin.x, Is.EqualTo(.52f).Within(.001f));
            Assert.That(UiKit.ButtonParts(continueButton).Fill.gameObject.activeSelf, Is.False, "the right button stays Primary");
            Assert.That(UiKit.ButtonParts(retryButton).Fill.gameObject.activeSelf, Is.True, "QUAY VỀ is Secondary");
        }

        void AssertLone(string primary)
        {
            Assert.That(continueButton.gameObject.activeSelf, Is.True);
            Assert.That(retryButton.gameObject.activeSelf, Is.False);
            Assert.That(panel.BackAvailable, Is.False);
            Assert.That(panel.ContinueLabel, Is.EqualTo(Nfc(primary)));
        }

        System.Collections.Generic.List<JourneyResultAction> RecordJourneyActions()
        {
            var actions = new System.Collections.Generic.List<JourneyResultAction>();
            panel.JourneyActionRequested += actions.Add;
            return actions;
        }

        [Test]
        public void ConfigureRetryTurnsTheSecondSlotBackIntoRetryAfterAJourneyCard()
        {
            ShowJourney("sprint_learn", true, new JourneyCommitOutcome(true, "sprint_practice", 5, false, false, false));
            Assert.That(panel.BackAvailable, Is.True);

            panel.ConfigureRetry(2);

            Assert.That(panel.BackAvailable, Is.False, "the legacy flow's second slot is Retry, never Back");
            Assert.That(panel.RetryAvailable, Is.True);
        }

        [Test]
        public void JourneyChapterWinOffersBackToMapBesideContinue()
        {
            var outcome = new JourneyCommitOutcome(true, "sprint_practice", 5, false, false, false);
            ShowJourney("sprint_learn", true, outcome);
            AssertBackPaired("TIẾP TỤC");
            var actions = RecordJourneyActions();
            retryButton.onClick.Invoke();
            retryButton.onClick.Invoke();
            Assert.That(actions, Is.EqualTo(new[] { JourneyResultAction.BackToMap }));

            ShowJourney("sprint_learn", true, outcome);
            actions.Clear();
            continueButton.onClick.Invoke();
            Assert.That(actions, Is.EqualTo(new[] { JourneyResultAction.Continue }));
        }

        [Test]
        public void JourneyExamWinAlsoOffersBackToMap()
        {
            ShowJourney("sprint_exam", true, new JourneyCommitOutcome(true, "volleyball_learn", 5, false, false, false));
            AssertBackPaired("TIẾP TỤC");
            var actions = RecordJourneyActions();
            panel.Back();
            Assert.That(actions, Is.EqualTo(new[] { JourneyResultAction.BackToMap }));
        }

        [Test]
        public void JourneyLearnFailureOffersBackToMapBesideLearnAgain()
        {
            var outcome = new JourneyCommitOutcome(true, "sprint_learn", 5, false, false, false);
            ShowJourney("sprint_learn", false, outcome);
            AssertBackPaired("HỌC LẠI");
            var actions = RecordJourneyActions();
            panel.Back();
            Assert.That(actions, Is.EqualTo(new[] { JourneyResultAction.BackToMap }));

            ShowJourney("sprint_learn", false, outcome);
            actions.Clear();
            panel.Continue();
            Assert.That(actions, Is.EqualTo(new[] { JourneyResultAction.Continue }), "HỌC LẠI keeps its retry action");
        }

        [TestCase("sprint_practice")]
        [TestCase("sprint_exam")]
        public void JourneyFailureThatOwesAFrogJumpOffersOnlyTheFrogJump(string id)
        {
            ShowJourney(id, false, new JourneyCommitOutcome(true, id, 5, true, true, false));
            AssertLone("BẬT CÓC");
            var actions = RecordJourneyActions();
            panel.Back();
            Assert.That(actions, Is.Empty, "the frog jump stays mandatory");
            panel.Continue();
            Assert.That(actions, Is.EqualTo(new[] { JourneyResultAction.FrogJump }));
        }

        [Test]
        public void CourseCompletingFinalWinOffersOnlyContinue()
        {
            ShowJourney("chess_final", true, new JourneyCommitOutcome(true, null, 5, false, false, true, true));
            AssertLone("TIẾP TỤC");
        }

        [Test]
        public void UnsavedResultOffersOnlyTheSaveRetry()
        {
            ShowJourney("sprint_learn", true, null, "Save unavailable");
            AssertLone("LƯU LẠI");
            ShowJourney("sprint_learn", false, null, "Save unavailable");
            AssertLone("LƯU LẠI");
        }

        [Test]
        public void SavedWinReShownWithARouteErrorKeepsBackToMap()
        {
            ShowJourney("sprint_learn", true, new JourneyCommitOutcome(true, "sprint_practice", 5, false, false, false),
                "Could not load scene");
            AssertBackPaired("THỬ LẠI");
            Assert.That(error.text, Does.Contain("Could not load scene"));
            var actions = RecordJourneyActions();
            panel.Back();
            Assert.That(actions, Is.EqualTo(new[] { JourneyResultAction.BackToMap }));
        }

        [Test]
        public void JourneyFinalFailureOffersBackToMapBesideItsRetry()
        {
            ShowJourney("chess_final", false, new JourneyCommitOutcome(true, "chess_final", 5, false, false, false, true));
            AssertBackPaired("THI LẠI");
            var actions = RecordJourneyActions();
            panel.Back();
            Assert.That(actions, Is.EqualTo(new[] { JourneyResultAction.BackToMap }));
        }

        [TestCase(false, "THI LẠI")]
        [TestCase(true, "LUYỆN LẠI")]
        public void FrogJumpResultWithLivesLeftOffersBackToMap(bool practice, string primary)
        {
            int backs = 0, continues = 0;
            panel.FrogJumpBackRequested += () => backs++;
            panel.FrogJumpContinueRequested += () => continues++;
            panel.ShowFrogJump(new FrogJumpResultView(true, true, 5, null, practice));
            AssertBackPaired(primary);
            retryButton.onClick.Invoke();
            retryButton.onClick.Invoke();
            Assert.That(backs, Is.EqualTo(1));
            Assert.That(continues, Is.Zero);

            panel.ShowFrogJump(new FrogJumpResultView(false, true, 4, null, practice));
            AssertBackPaired(primary);
            continueButton.onClick.Invoke();
            Assert.That(continues, Is.EqualTo(1));
            Assert.That(backs, Is.EqualTo(1));
        }

        [Test]
        public void FrogJumpResultWithNoLivesLeftOffersOnlyTheMap()
        {
            int backs = 0;
            panel.FrogJumpBackRequested += () => backs++;
            panel.ShowFrogJump(new FrogJumpResultView(true, false, 0, null));
            AssertLone("VỀ BẢN ĐỒ");
            panel.Back();
            Assert.That(backs, Is.Zero);
        }

        [Test]
        public void ReviewExamFailureKeepsItsRetryInTheSecondSlot()
        {
            var context = new ChallengeAttemptContext("review", "sprint_exam", ChallengeAttemptMode.Review,
                ChallengeDifficulty.Normal);
            var result = new ChallengeAttemptResult(context, false, new ChallengeMetrics(),
                new MinigameResult(false, 0f, Rank.F));
            panel.ShowChallenge(context, result, new JourneyCommitOutcome(true, null, 5, false, false, false), null);
            Assert.That(panel.RetryAvailable, Is.True);
            Assert.That(panel.BackAvailable, Is.False);
            Assert.That(retryButton.gameObject.activeSelf, Is.True);
            Assert.That(Label(retryButton), Is.EqualTo(Nfc("THI LẠI")));
            Assert.That(panel.ContinueLabel, Is.EqualTo(Nfc("TIẾP TỤC")));
            var actions = RecordJourneyActions();
            panel.Back();
            Assert.That(actions, Is.Empty);
            retryButton.onClick.Invoke();
            Assert.That(actions, Is.EqualTo(new[] { JourneyResultAction.Retry }));
        }

        [Test]
        public void ReviewWinOffersOnlyContinue()
        {
            var context = new ChallengeAttemptContext("review", "sprint_learn", ChallengeAttemptMode.Review,
                ChallengeDifficulty.Normal);
            var result = new ChallengeAttemptResult(context, true, new ChallengeMetrics(completedTargets: 3));
            panel.ShowChallenge(context, result, new JourneyCommitOutcome(true, null, 5, false, false, false), null);
            AssertLone("TIẾP TỤC");
        }

        static TMP_Text Text(GameObject parent, string name)
        {
            var text = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent.transform, false);
            return text;
        }

        [Test]
        public void ResultActionOnlyReturnsPreviewRoute()
        {
            var resultRoot = new GameObject("result-panel");
            try
            {
                var result = new MinigameResult(true, 1234.6f, Rank.A);
                var panel = resultRoot.AddComponent<ResultPanel>();
                string requestedRoute = null;
                panel.ActionRequested += route => requestedRoute = route;

                panel.Show(result, "MG_SprintPreview");
                panel.Continue();

                Assert.That(panel.CurrentResult, Is.SameAs(result));
                Assert.That(panel.PreviewRoute, Is.EqualTo("MG_SprintPreview"));
                Assert.That(requestedRoute, Is.EqualTo("MG_SprintPreview"));
            }
            finally
            {
                Object.DestroyImmediate(resultRoot);
            }
        }
    }
}
