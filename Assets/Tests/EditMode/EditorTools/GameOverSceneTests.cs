#if UNITY_EDITOR
using System.Linq;
using KMA.Gameplay.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KMA.Tests.EditorTools
{
    public sealed class GameOverSceneTests
    {
        const string ScenePath = "Assets/_Project/Scenes/GameOver.unity";

        [TearDown]
        public void ReleaseScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [Test]
        public void GameOverSceneHasNoMinigameUiLeft()
        {
            EditorSceneManager.OpenScene(ScenePath);
            foreach (string name in new[] { "S2_HUD_Minigame", "S2_PhaseOverlay", "S2_ResultPanel" })
                Assert.That(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Any(t => t.name == name), Is.False, name);
        }

        [Test]
        public void GameOverSceneAssignsTheMenuBackdrop()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var screen = Object.FindFirstObjectByType<GameOverScreen>(FindObjectsInactive.Include);
            Assert.That(AssetDatabase.GetAssetPath(screen.Background), Does.EndWith("Art/UI/MenuKeyArtBackdrop.png"));
        }

        [Test]
        public void GameOverSceneIsBakedInTheMenuCardStyle()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var screen = Object.FindFirstObjectByType<GameOverScreen>(FindObjectsInactive.Include);
            Assert.That(screen.transform.Find("GameOverLayout/MenuPanel"), Is.Not.Null);
            Assert.That(screen.transform.Find("GameOverVeil/Wash"), Is.Null);
        }
    }
}
#endif
