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
