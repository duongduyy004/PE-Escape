using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using KMA.Gameplay;
using KMA.Gameplay.Core;
using KMA.Gameplay.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KMA.Tests.Presentation
{
    public sealed class SplashLoadingFlowTests
    {
        const string BootstrapScenePath = "Assets/_Project/Scenes/Bootstrap.unity";
        const float SceneTimeoutSeconds = 8f;

        readonly List<GameObject> spawned = new List<GameObject>();
        float originalTimeScale;

        [SetUp]
        public void SetUp() => originalTimeScale = Time.timeScale;

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = originalTimeScale;
            foreach (GameObject gameObject in spawned)
                if (gameObject != null)
                    UnityEngine.Object.DestroyImmediate(gameObject);
            spawned.Clear();

            DestroyAll<GameManager>();
            DestroyAll<SceneRouter>();
            DestroyAll<SceneTransitionOverlay>();
            DestroyAll<SplashScreenPresenter>();
        }

        [TestCase(-0.1f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(0.45f, 0.5f)]
        [TestCase(0.9f, 1f)]
        [TestCase(1f, 1f)]
        public void LoadProgress_MapsUnityPreActivationRangeToNormalizedProgress(float source, float expected)
        {
            Assert.That(SceneRouter.NormalizeLoadProgress(source), Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void Overlay_BlocksRaycastsAndDisplaysRouterProgressOnlyWhileLoading()
        {
            SceneRouter router = Track(new GameObject("SplashLoadingFlowTests.Router"))
                .AddComponent<SceneRouter>();
            SceneTransitionOverlay overlay = SceneTransitionOverlay.EnsurePersistentInstance();
            overlay.Bind(router);

            Raise(router, "SceneLoadStarted");
            Raise(router, "SceneLoadProgressChanged", 0.625f);

            Assert.That(overlay.IsVisible, Is.True);
            Assert.That(overlay.IsBlockingInput, Is.True);
            Assert.That(overlay.Progress, Is.EqualTo(0.625f).Within(0.0001f));
            Slider visibleBar = overlay.GetComponentInChildren<Slider>(true);
            Graphic fill = visibleBar.transform.Find("FillMask/Fill").GetComponent<Graphic>();
            Assert.That(fill.gameObject.activeInHierarchy, Is.True);
            Assert.That(fill.color.a, Is.GreaterThan(0f));

            Raise(router, "SceneLoadCompleted");

            Assert.That(overlay.IsVisible, Is.False);
            Assert.That(overlay.IsBlockingInput, Is.False);
        }

        [UnityTest]
        public IEnumerator Splash_WaitsForMinimumUnscaledIntroAfterLoadCompletes()
        {
            SceneRouter router = Track(new GameObject("SplashLoadingFlowTests.Router"))
                .AddComponent<SceneRouter>();
            var splashRoot = Track(new GameObject("SplashLoadingFlowTests.Splash"));
            splashRoot.SetActive(false);
            CanvasGroup canvasGroup = splashRoot.AddComponent<CanvasGroup>();
            Slider slider = Track(new GameObject("LoadingBar")).AddComponent<Slider>();
            slider.transform.SetParent(splashRoot.transform, false);
            SplashScreenPresenter presenter = splashRoot.AddComponent<SplashScreenPresenter>();
            SetField(presenter, "canvasGroup", canvasGroup);
            SetField(presenter, "loadingBar", slider);
            SetField(presenter, "minimumIntroSeconds", 0.12f);
            SetField(presenter, "minimumIntroFrames", 1);
            splashRoot.SetActive(true);

            Time.timeScale = 0f;
            Raise(router, "SceneLoadStarted");
            Raise(router, "SceneLoadProgressChanged", 0.4f);
            Assert.That(presenter.IsVisible, Is.True);
            Assert.That(presenter.IsBlockingInput, Is.True);
            Assert.That(presenter.Progress, Is.EqualTo(0.4f).Within(0.0001f));

            Raise(router, "SceneLoadCompleted");
            Assert.That(presenter.Progress, Is.EqualTo(1f));

            Raise(router, "SceneLoadStarted");
            Raise(router, "SceneLoadProgressChanged", 0.2f);
            Assert.That(presenter.Progress, Is.EqualTo(1f),
                "The startup splash must detach after its first successful load.");

            float deadline = Time.realtimeSinceStartup + 3f;
            while (canvasGroup.alpha >= 1f && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(canvasGroup.alpha, Is.GreaterThan(0f).And.LessThan(1f),
                "The loaded menu must be revealed through a fade, not an abrupt hide.");
            Assert.That(presenter.IsBlockingInput, Is.True, "Input stays blocked during the fade.");
            while (presenter != null && presenter.IsVisible && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(presenter.IsVisible, Is.False);
            Assert.That(presenter.IsBlockingInput, Is.False);
            yield return null;
            Assert.That(presenter == null, Is.True,
                "The completed startup presentation must not persist into later routes.");
        }

        [UnityTest]
        public IEnumerator Splash_InterpolatesDisplayedProgressAtZeroTimeScale()
        {
            SceneRouter router = Track(new GameObject("SplashLoadingFlowTests.Router"))
                .AddComponent<SceneRouter>();
            var root = Track(new GameObject("SplashLoadingFlowTests.Splash"));
            root.SetActive(false);
            root.AddComponent<CanvasGroup>();
            Slider bar = Track(new GameObject("LoadingBar")).AddComponent<Slider>();
            bar.transform.SetParent(root.transform, false);
            SplashScreenPresenter presenter = root.AddComponent<SplashScreenPresenter>();
            SetField(presenter, "loadingBar", bar);
            root.SetActive(true);
            Time.timeScale = 0f;

            Raise(router, "SceneLoadStarted");
            Raise(router, "SceneLoadProgressChanged", .8f);
            Assert.That(bar.value, Is.LessThan(.8f), "The displayed fill must not jump to a router event.");
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(bar.value, Is.GreaterThan(0f).And.LessThan(.8f));
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(bar.value, Is.EqualTo(.8f).Within(.002f));
        }

        [UnityTest]
        public IEnumerator Splash_StaysVisibleUntilItHasBeenPresentedForMinimumFrames()
        {
            // On device, scene activation stalls the player inside one long frame: the
            // wall-clock hold expired after two presented frames and the intro was never seen.
            SceneRouter router = Track(new GameObject("SplashLoadingFlowTests.Router"))
                .AddComponent<SceneRouter>();
            var splashRoot = Track(new GameObject("SplashLoadingFlowTests.Splash"));
            splashRoot.SetActive(false);
            CanvasGroup canvasGroup = splashRoot.AddComponent<CanvasGroup>();
            SplashScreenPresenter presenter = splashRoot.AddComponent<SplashScreenPresenter>();
            SetField(presenter, "canvasGroup", canvasGroup);
            SetField(presenter, "minimumIntroSeconds", 0f);
            SetField(presenter, "minimumIntroFrames", 20);
            splashRoot.SetActive(true);

            Raise(router, "SceneLoadStarted");
            Raise(router, "SceneLoadCompleted");
            int completedFrame = Time.frameCount;

            float deadline = Time.realtimeSinceStartup + 3f;
            while (presenter != null && presenter.IsVisible)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    "The presented-frame hold must still end.");
                yield return null;
            }

            Assert.That(Time.frameCount - completedFrame, Is.GreaterThanOrEqualTo(20),
                "A zero-second hold must still present the intro for its minimum frame count.");
        }

        [UnityTest]
        public IEnumerator SplashPresentation_FitsLandscapeRatiosAndRendersVietnameseGlyphs()
        {
            var root = Track(new GameObject("SplashLoadingFlowTests.Layout", typeof(RectTransform), typeof(Canvas)));
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = (RectTransform)root.transform;
            var art = Track(new GameObject("Illustration", typeof(RectTransform), typeof(Image)));
            art.transform.SetParent(root.transform, false);
            SplashPresentationView.Build(root.transform);
            var safe = (RectTransform)root.transform.Find("SplashSafeArea");
            var corners = new Vector3[4];
            var elementCorners = new Vector3[4];
            foreach (Vector2 size in new[] { new Vector2(1920, 1080), new Vector2(1920, 1200),
                new Vector2(2160, 1080), new Vector2(1440, 1080) })
            {
                canvasRect.sizeDelta = size;
                yield return null;
                Canvas.ForceUpdateCanvases();
                safe.GetWorldCorners(corners);
                Transform badge = safe.Find("SplashLayout/SportBadge");
                Assert.That(badge.Find("NavyFace").position.x, Is.EqualTo(badge.position.x).Within(.01f),
                    "The reused badge artwork must share the centered title axis.");
                foreach (var name in new[] { "SportBadge", "TitleTop", "TitleKMA", "Slogan", "LoadingBar" })
                {
                    var element = (RectTransform)safe.Find("SplashLayout/" + name);
                    element.GetWorldCorners(elementCorners);
                    foreach (Vector3 point in elementCorners)
                    {
                        Assert.That(point.x, Is.InRange(corners[0].x, corners[2].x), name + " at " + size);
                        Assert.That(point.y, Is.InRange(corners[0].y, corners[2].y), name + " at " + size);
                    }
                }
                var imageRect = (RectTransform)art.transform;
                Assert.That(imageRect.rect.width, Is.GreaterThanOrEqualTo(size.x - .1f));
                Assert.That(imageRect.rect.height, Is.GreaterThanOrEqualTo(size.y - .1f));
            }
            foreach (var name in new[] { "Slogan", "LoadingStatus" })
            {
                var text = safe.Find("SplashLayout/" + name).GetComponent<TMP_Text>();
                text.ForceMeshUpdate();
                Assert.That(text.textInfo.characterCount, Is.GreaterThan(0));
                foreach (var character in text.textInfo.characterInfo)
                {
                    if (!character.isVisible) continue;
                    Assert.That(character.textElement.unicode, Is.EqualTo((uint)character.character),
                        "Missing Vietnamese glyph in " + text.text);
                }
            }
        }

        [Test]
        public void InvalidScene_ReportsFailureAndRestoresInteractionImmediately()
        {
            SceneRouter router = Track(new GameObject("SplashLoadingFlowTests.Router"))
                .AddComponent<SceneRouter>();
            var events = new List<string>();
            router.SceneLoadStarted += () => events.Add("started");
            router.SceneLoadFailed += _ => events.Add("failed");
            router.SceneLoadCompleted += () => events.Add("completed");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Missing_Task5_Scene"));

            bool accepted = router.TryLoadScene("Missing_Task5_Scene");

            Assert.That(accepted, Is.False);
            Assert.That(router.IsTransitioning, Is.False);
            Assert.That(events, Is.EqualTo(new[] { "started", "failed", "completed" }));
        }

        [Test]
        public void PostValidationLoaderRejection_RollsBackSubjectAndRestoresControls()
        {
            SceneRouter router = Track(new GameObject("SplashLoadingFlowTests.Router"))
                .AddComponent<SceneRouter>();
            int sessionChanges = 0;
            int failures = 0;
            router.SessionChanged += () => sessionChanges++;
            router.SceneLoadFailed += _ => failures++;
            router.ConfigureSceneLoaderForTests(_ => null);
            LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex("MG_Sprint.*did not create"));

            bool accepted = router.StartSubject(SubjectId.Sprint);

            Assert.That(accepted, Is.False);
            Assert.That(router.Session.ActiveSubject, Is.Null,
                "A rejected scene operation must not strand an active subject attempt.");
            Assert.That(router.IsTransitioning, Is.False);
            Assert.That(sessionChanges, Is.Zero,
                "Rejected state must never be published or persisted.");
            Assert.That(failures, Is.EqualTo(1));
        }

        [Test]
        public void PostValidationMenuRejection_PreservesActiveRouteBinding()
        {
            SceneRouter router = Track(new GameObject("SplashLoadingFlowTests.Router"))
                .AddComponent<SceneRouter>();
            router.Session.StartSubject(SubjectId.Sprint);
            SetField(router, "activeSubject", (SubjectId?)SubjectId.Sprint);
            SetField(router, "awaitingSubjectScene", true);
            router.ConfigureSceneLoaderForTests(_ => null);
            LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex("Menu.*did not create"));

            bool accepted = router.RouteToMenu();

            Assert.That(accepted, Is.False);
            Assert.That(router.Session.ActiveSubject, Is.EqualTo(SubjectId.Sprint));
            Assert.That(GetField<SubjectId?>(router, "activeSubject"),
                Is.EqualTo(SubjectId.Sprint));
            Assert.That(GetField<bool>(router, "awaitingSubjectScene"), Is.True);
            Assert.That(router.IsTransitioning, Is.False);
        }

        [UnityTest]
        public IEnumerator LoadingMenuAtZeroTimeScale_EmitsRealProgressAndCompletes()
        {
            SceneRouter router = Track(new GameObject("SplashLoadingFlowTests.Router"))
                .AddComponent<SceneRouter>();
            var progress = new List<float>();
            router.SceneLoadProgressChanged += progress.Add;
            Time.timeScale = 0f;

            Assert.That(router.RouteToMenu(), Is.True);
            Assert.That(router.RouteToMenu(), Is.False, "A second tap must not start another transition.");

            yield return WaitForSceneAndTransition(router, "Menu");

            Assert.That(progress, Is.Not.Empty);
            Assert.That(progress[0], Is.EqualTo(0f));
            Assert.That(progress[progress.Count - 1], Is.EqualTo(1f));
            for (int i = 1; i < progress.Count; i++)
                Assert.That(progress[i], Is.GreaterThanOrEqualTo(progress[i - 1]));
            for (int i = 0; i < progress.Count - 1; i++)
                Assert.That(progress[i], Is.LessThan(1f),
                    "Only an activated scene may publish 100% progress.");
        }

        [UnityTest]
        public IEnumerator Bootstrap_ReachesMenuOnceWithOneManagerAndOneRouter()
        {
            yield return DestroyPersistentRuntime();
            int menuLoads = 0;
            void CountMenu(Scene scene, LoadSceneMode _) { if (scene.name == "Menu") menuLoads++; }
            SceneManager.sceneLoaded += CountMenu;
            try
            {
                float introStartedAt = Time.realtimeSinceStartup;
                AsyncOperation load = SceneManager.LoadSceneAsync(BootstrapScenePath, LoadSceneMode.Single);
                Assert.That(load, Is.Not.Null);
                while (!load.isDone)
                    yield return null;
                yield return WaitForScene("Menu");

                Assert.That(menuLoads, Is.EqualTo(1));
                Assert.That(UnityEngine.Object.FindObjectsByType<GameManager>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(1));
                Assert.That(UnityEngine.Object.FindObjectsByType<SceneRouter>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(1));
                SplashScreenPresenter splash = UnityEngine.Object.FindFirstObjectByType<SplashScreenPresenter>();
                Assert.That(splash, Is.Not.Null);
                Assert.That(splash.GetComponent<EventSystem>(), Is.Not.Null);
                Assert.That(splash.GetComponent<BaseInputModule>(), Is.Not.Null);
                Assert.That(splash.transform.Find("SplashSafeArea/SplashLayout/SportBadge"), Is.Not.Null);
                Assert.That(splash.transform.Find("Illustration"), Is.Not.Null);
                Transform content = splash.transform.Find("SplashSafeArea/SplashLayout");
                Assert.That(content, Is.Not.Null, "Splash must build its presentation inside the safe area.");
                Assert.That(content.Find("TitleTop").GetComponent<TMP_Text>().text, Is.EqualTo("CHẠY TRỐN"));
                Assert.That(content.Find("TitleKMA").GetComponent<TMP_Text>().text, Is.EqualTo("THỂ CHẤT"));
                Assert.That(content.Find("Slogan").GetComponent<TMP_Text>().text,
                    Is.EqualTo("Hành trình rèn luyện thể chất"));
                Assert.That(content.Find("TitleKMA").GetComponent<TMP_Text>().fontSharedMaterial,
                    Is.SameAs(VietTypography.Library.titleMaterial));
                Assert.That(splash.transform.Find("Illustration").GetComponent<UnityEngine.UI.AspectRatioFitter>().aspectMode,
                    Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent));
                while (splash != null && splash.IsVisible)
                    yield return null;
                Assert.That(Time.realtimeSinceStartup - introStartedAt, Is.GreaterThanOrEqualTo(1.45f));
                yield return null;
                Assert.That(splash == null, Is.True);
            }
            finally
            {
                SceneManager.sceneLoaded -= CountMenu;
            }
        }

        [UnityTest]
        public IEnumerator Home_PreservesButtonLookupNamesAndUsesVietnamesePresentation()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("Menu", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone)
                yield return null;

            Assert.That(GameObject.Find("PLAYButton"), Is.Null);
            AssertLabel("CONTINUEButton", "TIẾP TỤC");
            AssertLabel("NEW GAMEButton", "CHƠI MỚI");
            AssertLabel("SETTINGSButton", "CÀI ĐẶT");
            AssertLabel("QUITButton", "THOÁT");
            var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuScreen>();
            Assert.That(menu.transform.Find("HomeMenuLayout/NEW GAMEButton"), Is.Not.Null);
            Assert.That(GameObject.Find("HomeIllustration"), Is.Not.Null);
            var art = GameObject.Find("HomeIllustration").GetComponent<UnityEngine.UI.Image>();
            Assert.That(art.sprite.name, Does.StartWith("MenuKeyArt"), "the painted menu key art is the background");
        }

        [UnityTest]
        public IEnumerator HomeNewGameButton_RoutesThroughTheRealRuntimeBinding()
        {
            // The button reaches Map through SceneRouter, which only exists once the
            // Bootstrap route has run, so Menu must be entered the way the game enters it.
            yield return DestroyPersistentRuntime();
            AsyncOperation load = SceneManager.LoadSceneAsync(BootstrapScenePath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone)
                yield return null;
            yield return WaitForScene("Menu");
            Assert.That(SceneRouter.Instance, Is.Not.Null,
                "The Bootstrap route must publish the runtime the menu buttons bind to.");

            Button play = GameObject.Find("NEW GAMEButton").GetComponent<Button>();
            Assert.That(play, Is.Not.Null);

            play.onClick.Invoke();
            UnityEngine.Object.FindFirstObjectByType<MainMenuScreen>()?.ConfirmNewGame();

            yield return WaitForScene("Map");
        }

        [Test]
        public void ContinueAndNewGameKeepTheirExistingGuards()
        {
            MainMenuScreen menu = Track(new GameObject("SplashLoadingFlowTests.Menu"))
                .AddComponent<MainMenuScreen>();
            int continues = 0;
            int newGames = 0;
            menu.ContinueRequested += () => continues++;
            menu.NewGameRequested += () => newGames++;

            menu.Configure(false);
            menu.Continue();
            Assert.That(continues, Is.Zero);
            menu.Configure(true);
            menu.NewGame();
            Assert.That(newGames, Is.Zero);

            menu.Configure(true);
            menu.Continue();
            menu.ConfirmNewGame();
            Assert.That(continues, Is.EqualTo(1));
            Assert.That(newGames, Is.EqualTo(1));
        }

        GameObject Track(GameObject gameObject)
        {
            spawned.Add(gameObject);
            return gameObject;
        }

        static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing serialized field {name}.");
            field.SetValue(target, value);
        }

        static T GetField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {name}.");
            return (T)field.GetValue(target);
        }

        static void Raise(SceneRouter router, string eventName, params object[] args)
        {
            FieldInfo field = typeof(SceneRouter).GetField(eventName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing event backing field {eventName}.");
            ((Delegate)field.GetValue(router))?.DynamicInvoke(args);
        }

        static IEnumerator WaitForScene(string sceneName)
        {
            float deadline = Time.realtimeSinceStartup + SceneTimeoutSeconds;
            while (!string.Equals(SceneManager.GetActiveScene().name, sceneName, StringComparison.Ordinal))
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                yield return null;
            }
        }

        static IEnumerator WaitForSceneAndTransition(SceneRouter router, string sceneName)
        {
            yield return WaitForScene(sceneName);
            float deadline = Time.realtimeSinceStartup + SceneTimeoutSeconds;
            while (router != null && router.IsTransitioning)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                yield return null;
            }
        }

        static IEnumerator DestroyPersistentRuntime()
        {
            foreach (GameManager manager in UnityEngine.Object.FindObjectsByType<GameManager>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.Destroy(manager.gameObject);
            foreach (SceneRouter router in UnityEngine.Object.FindObjectsByType<SceneRouter>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.Destroy(router.gameObject);
            foreach (SplashScreenPresenter splash in UnityEngine.Object.FindObjectsByType<SplashScreenPresenter>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.Destroy(splash.gameObject);
            foreach (SceneTransitionOverlay overlay in UnityEngine.Object.FindObjectsByType<SceneTransitionOverlay>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.Destroy(overlay.gameObject);
            yield return null;
        }

        static void DestroyAll<T>() where T : Component
        {
            foreach (T component in UnityEngine.Object.FindObjectsByType<T>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(component.gameObject);
        }

        static void AssertLabel(string buttonName, string expected)
        {
            GameObject button = GameObject.Find(buttonName);
            Assert.That(button, Is.Not.Null, $"Missing preserved menu object {buttonName}.");
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            Assert.That(label, Is.Not.Null);
            Assert.That(label.text, Is.EqualTo(expected));
        }
    }
}
