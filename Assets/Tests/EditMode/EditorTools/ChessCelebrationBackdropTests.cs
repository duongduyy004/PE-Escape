using System.Linq;
using KMA.EditorTools;
using KMA.Gameplay.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Tests.EditorTools
{
    public sealed class ChessCelebrationBackdropTests
    {
        [Test]
        public void ChessUsesTheCanvasBackdropOutsideTheSafeArea()
        {
            ChessFinalSceneConfigurator.BuildScene();
            EditorSceneManager.OpenScene(ChessFinalSceneConfigurator.ScenePath, OpenSceneMode.Single);
            var backdrop = Object.FindFirstObjectByType<CampusBackdropUi>(FindObjectsInactive.Include);
            Assert.That(backdrop, Is.Not.Null);
            Assert.That(backdrop.transform.parent.GetComponent<Canvas>(), Is.Not.Null, "backdrop must sit on the canvas root, outside SafeAreaRoot");
            Assert.That(backdrop.transform.GetSiblingIndex(), Is.Zero);
            Assert.That(GameObject.Find("Campus"), Is.Null, "the stretched Sprint campus picture is gone");
        }

        [Test]
        public void ChessIntroCardIsOpaqueAndDoesNotRepeatTheObjective()
        {
            ChessFinalSceneConfigurator.BuildScene();
            EditorSceneManager.OpenScene(ChessFinalSceneConfigurator.ScenePath, OpenSceneMode.Single);
            var intro = Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(i => i.name == "IntroCard");
            Assert.That(intro.color.a, Is.EqualTo(1f).Within(1e-3f));
            string text = intro.GetComponentInChildren<TMP_Text>(true).text;
            Assert.That(text, Does.Not.Contain("Chiếu hết"));
            Assert.That(text, Does.Contain("Đang tạo"), "the card is filled with the puzzle limits once the puzzle is ready");
        }

        [Test]
        public void CelebrationCastStandsOnTheCampusGround()
        {
            CelebrationSceneConfigurator.BuildScene();
            EditorSceneManager.OpenScene(CelebrationSceneConfigurator.ScenePath, OpenSceneMode.Single);
            var backdrop = Object.FindFirstObjectByType<CampusBackdropUi>(FindObjectsInactive.Include);
            Assert.That(backdrop, Is.Not.Null);
            Assert.That(backdrop.Ground.enabled, Is.True);
            Assert.That(GameObject.Find("Track"), Is.Null);
        }
    }
}
