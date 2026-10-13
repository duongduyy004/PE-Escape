using System.Reflection;
using KMA.Gameplay.Core;
using KMA.Gameplay.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Tests.Presentation
{
    public sealed class SceneTransitionOverlayTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (SceneTransitionOverlay overlay in Object.FindObjectsByType<SceneTransitionOverlay>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(overlay.gameObject);
            }
        }

        [Test]
        public void StartsHiddenThenShowAndHideToggleVisibility()
        {
            var root = new GameObject("scene-transition-overlay");
            try
            {
                var overlay = root.AddComponent<SceneTransitionOverlay>();
                overlay.InitializeForTest();

                Assert.That(overlay.IsVisible, Is.False);

                overlay.Show();
                Assert.That(overlay.IsVisible, Is.True);

                overlay.Hide();
                Assert.That(overlay.IsVisible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BindSubscribesToRouterSceneLoadEventsToShowAndHide()
        {
            var overlayRoot = new GameObject("scene-transition-overlay");
            var routerRoot = new GameObject("scene-router");
            try
            {
                var overlay = overlayRoot.AddComponent<SceneTransitionOverlay>();
                overlay.InitializeForTest();
                var router = routerRoot.AddComponent<SceneRouter>();

                overlay.Bind(router);

                RaiseSceneLoadStarted(router);
                Assert.That(overlay.IsVisible, Is.True);

                RaiseSceneLoadCompleted(router);
                Assert.That(overlay.IsVisible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(overlayRoot);
                Object.DestroyImmediate(routerRoot);
            }
        }

        [Test]
        public void UsesTheMenuLookAndShowsProgressOnTheSlantedBar()
        {
            var root = new GameObject("scene-transition-overlay");
            try
            {
                var overlay = root.AddComponent<SceneTransitionOverlay>();
                overlay.InitializeForTest();
                Transform panel = root.transform.Find("Panel");

                CanvasScaler scaler = panel.GetComponent<CanvasScaler>();
                Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
                Assert.That(panel.Find("Backdrop").GetComponent<Image>().color, Is.EqualTo(KMA.UI.Kit.ShellTheme.Ink));
                Assert.That(panel.Find("Card/" + KMA.UI.Kit.ShellKit.FaceName).GetComponent<Image>().color, Is.EqualTo(KMA.UI.Kit.ShellTheme.Cream));
                var problems = KMA.UI.Kit.ShellStyleAudit.Audit(panel.Find("Card").gameObject);
                Assert.That(problems, Is.Empty, string.Join("\n", problems));
                Assert.That(panel.Find("Card/SportBadge"), Is.Not.Null);
                Assert.That(panel.Find("Card/Title").GetComponent<TMP_Text>().text, Does.StartWith("ĐANG TẢI."));

                overlay.Show();
                RectTransform fill = (RectTransform)panel.Find("Card/LoadingBar/FillMask/Fill");
                TMP_Text percent = panel.Find("Card/Percent").GetComponent<TMP_Text>();
                Assert.That(fill.gameObject.activeSelf, Is.False, "an empty bar shows no fill");
                Assert.That(percent.text, Is.EqualTo("0%"));

                SetProgress(overlay, .5f);
                Assert.That(fill.gameObject.activeSelf, Is.True);
                Assert.That(fill.anchoredPosition.x, Is.LessThan(0f).And.GreaterThan(-fill.rect.width));
                Assert.That(percent.text, Is.EqualTo("50%"));
                Assert.That(overlay.Progress, Is.EqualTo(.5f).Within(.0001f));

                SetProgress(overlay, 1f);
                Assert.That(fill.anchoredPosition.x, Is.EqualTo(0f).Within(.0001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void SetProgress(SceneTransitionOverlay overlay, float value) =>
            typeof(SceneTransitionOverlay).GetMethod("SetProgress", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(overlay, new object[] { value });

        static void RaiseSceneLoadStarted(SceneRouter router) => InvokeEvent(router, "SceneLoadStarted");

        static void RaiseSceneLoadCompleted(SceneRouter router) => InvokeEvent(router, "SceneLoadCompleted");

        static void InvokeEvent(SceneRouter router, string eventName)
        {
            var field = typeof(SceneRouter).GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic);
            var handler = (System.Action)field.GetValue(router);
            handler?.Invoke();
        }
    }
}
