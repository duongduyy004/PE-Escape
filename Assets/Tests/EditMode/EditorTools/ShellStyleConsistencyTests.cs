#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using KMA.Gameplay.UI;
using KMA.UI.Kit;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KMA.Tests.EditorTools
{
    public sealed class ShellStyleConsistencyTests
    {
        [TearDown]
        public void ReleaseScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [Test]
        public void NewGameConfirmationFollowsTheShellStyle()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Menu.unity", OpenSceneMode.Single);
            MonoBehaviour controller = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                .First(b => b != null && b.GetType().Name == "S5ShellSceneController");
            controller.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(controller, null);
            controller.GetType().GetMethod("EnsureNewGameConfirmation", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);

            GameObject confirmation = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .First(t => t.name == "NewGameConfirmation").gameObject;
            Transform card = confirmation.transform.Find("ConfirmationCard");
            Assert.That(card.Find(ShellKit.FaceName).GetComponent<Image>().color, Is.EqualTo(ShellTheme.Cream));
            Assert.That(confirmation.GetComponent<Image>().color, Is.EqualTo(UITheme.Shared.Scrim));
            var problems = ShellStyleAudit.Audit(card.gameObject);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            Assert.That(card.GetComponentsInChildren<Button>(true).Select(b => b.name),
                Is.EquivalentTo(new[] { "XÁC NHẬNButton", "QUAY LẠIButton" }));
        }

        [Test]
        public void MapSceneHasNoMissingScripts()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Map.unity", OpenSceneMode.Single);
            int missing = scene.GetRootGameObjects().Sum(go => go.GetComponentsInChildren<Transform>(true)
                .Sum(t => UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
            Assert.That(missing, Is.Zero);
        }

        static GameObject Find(Scene scene, string name) =>
            scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .First(t => t.name == name).gameObject;

        [TestCase("Assets/_Project/Scenes/GameOver.unity", "GameOverLayout")]
        [TestCase("Assets/_Project/Scenes/Map.unity", "S5MapPresentation")]
        [TestCase("Assets/_Project/Scenes/Menu.unity", "HomeMenuLayout")]
        public void AuthoredShellScreensFollowTheShellStyle(string scenePath, string rootName)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var problems = ShellStyleAudit.Audit(Find(scene, rootName));
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void CelebrationCanvasesFollowTheShellStyle()
        {
            Scene scene = EditorSceneManager.OpenScene(KMA.EditorTools.CelebrationSceneConfigurator.ScenePath,
                OpenSceneMode.Single);
            var problems = scene.GetRootGameObjects().Where(go => go.GetComponent<Canvas>() != null)
                .SelectMany(ShellStyleAudit.Audit).ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void BootstrapSplashFollowsTheSplashRules()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            var problems = ShellStyleAudit.Audit(Find(scene, "SplashLayout"), ShellAuditOptions.Splash);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void ShellScreensHaveNoTextBelowTheFloor()
        {
            foreach (string path in new[] { "GameOver", "Map", "Menu" })
            {
                Scene scene = EditorSceneManager.OpenScene($"Assets/_Project/Scenes/{path}.unity", OpenSceneMode.Single);
                foreach (TMP_Text text in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)))
                {
                    if (text.GetComponentInParent<GameplayDialogueView>(true) != null || text.color.a < .01f) continue;
                    float smallest = text.enableAutoSizing ? text.fontSizeMin : text.fontSize;
                    Assert.That(smallest, Is.GreaterThanOrEqualTo(MinigameUiTheme.MinimumFontSize), $"{path}: {text.name}");
                }
            }
        }
    }
}
#endif
