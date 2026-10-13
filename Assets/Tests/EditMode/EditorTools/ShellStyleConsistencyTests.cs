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
    }
}
#endif
