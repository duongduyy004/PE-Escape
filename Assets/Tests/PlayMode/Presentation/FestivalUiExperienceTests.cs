using System.Collections;
using System.Linq;
using KMA.Gameplay;
using KMA.Gameplay.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KMA.Tests.Presentation
{
    public sealed class FestivalUiExperienceTests
    {
        [UnityTest]
        public IEnumerator SplashShowsReadableProgressAndLoadingHint()
        {
            yield return SceneManager.LoadSceneAsync(
                "Assets/_Project/Scenes/Bootstrap.unity", LoadSceneMode.Single);

            Transform splash = GameObject.Find("SplashCanvas")?.transform;
            Assert.That(splash, Is.Not.Null);
            TMP_Text progress = splash.Find("SplashSafeArea/SplashLayout/ProgressPercent")?.GetComponent<TMP_Text>();
            Assert.That(progress, Is.Not.Null,
                "The loading state needs a numeric progress value, not only a fill bar.");
            Assert.That(progress.font, Is.Not.Null);
            TMP_Text hint = splash.Find("SplashSafeArea/SplashLayout/Slogan")?.GetComponent<TMP_Text>();
            Assert.That(hint, Is.Not.Null);
            Assert.That(hint.text, Is.EqualTo("Hành trình rèn luyện thể chất"));
            Assert.That(hint.font, Is.Not.Null);
            Assert.That(splash.Find("SplashNavyGradient"), Is.Null,
                "The splash shows the stadium art as is, without a navy overlay.");
        }

        [UnityTearDown]
        public IEnumerator RemoveStartupPresentation()
        {
            // Bootstrap's presentation is persistent: each fixture owns its cleanup
            // even when another test already created the singleton game runtime.
            foreach (var splash in Object.FindObjectsByType<SplashScreenPresenter>(FindObjectsSortMode.None))
                Object.Destroy(splash.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuBuildsOneClearPrimaryActionAndCompactUtilities()
        {
            yield return SceneManager.LoadSceneAsync("Menu", LoadSceneMode.Single);
            yield return null;

            Transform panel = GameObject.Find("HomeMenuLayout")?.transform;
            Assert.That(panel, Is.Not.Null);
            var buttons = panel.GetComponentsInChildren<Button>();
            Assert.That(buttons.Select(button => button.name), Is.EqualTo(new[]
            {
                "NEW GAMEButton", "CONTINUEButton", "SETTINGSButton", "QUITButton"
            }));

        }

        [UnityTest]
        public IEnumerator Settings_OpenAdjustAndReturn_UsesVisibleRuntimeControls()
        {
            yield return SceneManager.LoadSceneAsync("Menu", LoadSceneMode.Single);
            yield return null;
            var menu = Object.FindFirstObjectByType<MainMenuScreen>();
            FindButton("SETTINGSButton").onClick.Invoke();
            var settings = Object.FindFirstObjectByType<SettingsScreen>();
            Assert.That(settings, Is.Not.Null, "Settings must remain active when the menu is hidden.");
            Assert.That(menu.IsVisible, Is.False);
            var sliders = settings.GetComponentsInChildren<Slider>();
            Assert.That(sliders.Length, Is.EqualTo(2));
            sliders.Single(slider => slider.name == "MusicSlider").value = .25f;
            sliders.Single(slider => slider.name == "SfxSlider").value = .5f;
            var toggle = settings.GetComponentInChildren<Toggle>();
            Assert.That(toggle, Is.Not.Null);
            toggle.isOn = false;
            Assert.That(settings.CurrentSettings.musicVol, Is.EqualTo(.25f));
            Assert.That(settings.CurrentSettings.sfxVol, Is.EqualTo(.5f));
            Assert.That(settings.CurrentSettings.vibration, Is.False);
            Button settingsBack = settings.GetComponentsInChildren<Button>().Single(button => button.name == "BackButton");
            Assert.That(settingsBack.GetComponent<KMA.UI.Kit.ShellPressFeedback>(), Is.Not.Null);
            Assert.That(((Image)settingsBack.targetGraphic).sprite, Is.Not.Null);
            Assert.That(settingsBack.GetComponentInChildren<TMP_Text>().text, Is.Not.Empty, "back keeps its caption");
            settingsBack.onClick.Invoke();
            Assert.That(menu.IsVisible, Is.True);
            Assert.That(settings.gameObject.activeInHierarchy, Is.False);
        }

        [UnityTest]
        public IEnumerator MapUsesReadableLivesUniformCardsAndIntegratedProgress()
        {
            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            for (int frame = 0; frame < 4; frame++)
            {
                Canvas.ForceUpdateCanvases();
                yield return null;
            }

            Transform root = GameObject.Find("S5MapPresentation")?.transform;
            Assert.That(root, Is.Not.Null);
            Transform mapBack = root.Find("Content/Header/BackButton");
            Assert.That(mapBack, Is.Not.Null);
            Assert.That(mapBack.GetComponent<KMA.UI.Kit.ShellPressFeedback>(), Is.Not.Null);
            Assert.That(mapBack.GetComponentInChildren<TMP_Text>().text, Is.Not.Empty, "back arrow keeps its glyph");
            RectTransform header = root.Find("Content/Header").GetComponent<RectTransform>();
            Assert.That(header.rect.height, Is.InRange(76f, 92f));

            Transform hearts = root.Find("Content/Header/LivesPanel/HeartBar");
            Assert.That(hearts, Is.Not.Null);
            foreach (LayoutElement heart in hearts.GetComponentsInChildren<LayoutElement>(true)
                         .Where(element => element.name.StartsWith("Heart") && element.name != "HeartBar"))
                Assert.That(heart.preferredWidth, Is.InRange(32f, 40f));

            Transform grid = root.Find("Content/SelectionGrid");
            Assert.That(grid, Is.Not.Null);
            var nodes = grid.GetComponentsInChildren<MapNodeView>();
            Assert.That(nodes, Has.Length.EqualTo(4));
            // The connected route owns card placement; the legacy disabled grid's
            // cellSize no longer describes anything rendered on screen.
            Assert.That(nodes.Select(node => ((RectTransform)node.transform).rect.size).Distinct().Count(),
                Is.EqualTo(1), "All chapter cards must have the same rendered size.");
            foreach (TMP_Text label in nodes.SelectMany(node => node.GetComponentsInChildren<TMP_Text>()))
                Assert.That(label.preferredHeight,
                    Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1f),
                    $"{label.name} must remain readable inside the rendered chapter card.");
            Assert.That(nodes.Select(node => node.SubjectId), Is.EqualTo(new[]
                { SubjectId.Sprint, SubjectId.Volleyball, SubjectId.Football, SubjectId.Chess }));
            Assert.That(grid.Cast<Transform>().Count(child => child.name.StartsWith("PathTrack")), Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator SprintUsesAutomaticVietnameseStartBanner()
        {
            yield return SceneManager.LoadSceneAsync("MG_Sprint", LoadSceneMode.Single);
            yield return null;

            Transform chrome = GameObject.Find("SprintBroadcastChrome")?.transform;
            Assert.That(chrome, Is.Not.Null, "Automatic Sprint broadcast chrome install failed.");
            TMP_Text modeLabel = chrome.Find("ModeLabel/Label").GetComponent<TMP_Text>();
            Assert.That(modeLabel.text, Is.EqualTo("CHẠY NƯỚC RÚT · 150M"));
            Assert.That(modeLabel.font, Is.Not.Null);

            SprintStartPresentation start = Object.FindFirstObjectByType<SprintStartPresentation>();
            Assert.That(start, Is.Not.Null);
            Assert.That(start.InstructionText, Is.EqualTo(SprintStartPresentation.InstructionCopy));
        }

        static Button FindButton(string name) => Object.FindObjectsByType<Button>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(button => button.name == name);
    }
}
