using System.Linq;
using KMA.EditorTools;
using KMA.Gameplay;
using KMA.Gameplay.FrogJump;
using KMA.Gameplay.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KMA.Tests.EditorTools
{
    public sealed class FrogJumpSceneTests
    {
        [Test]
        public void AuthoredBalanceUsesTheFasterSweepAndThirtySecondLimit()
        {
            var config = AssetDatabase.LoadAssetAtPath<FrogJumpBalanceConfig>(
                "Assets/_Project/ScriptableObjects/FrogJump/FrogJumpBalance.asset");
            Assert.That(config.Tuning.trackMetres, Is.EqualTo(60f));
            Assert.That(config.Tuning.timeLimitSeconds, Is.EqualTo(30f));
            Assert.That(config.Tuning.safeZone, Is.EqualTo(.2f));
            Assert.That(config.Tuning.minJumpMetres, Is.EqualTo(2.2f));
            Assert.That(config.Tuning.maxJumpMetres, Is.EqualTo(3.2f));
            Assert.That(config.Tuning.sweepSeconds, Is.EqualTo(.7f));
            Assert.That(config.Tuning.jumpSeconds, Is.EqualTo(.6f));
            Assert.That(config.Tuning.recoverSeconds, Is.EqualTo(2.5f));
            var rules = new FrogJumpRules(config.Tuning);
            rules.Tick(.35f);
            Assert.That(rules.Needle01, Is.EqualTo(.5f).Within(.001f));
            rules.Stop();
            rules.Tick(.6f);
            Assert.That(rules.Distance, Is.EqualTo(3.2f).Within(.001f));
            Assert.That(rules.TimeRemaining, Is.EqualTo(29.05f).Within(.001f));
        }

        [Test]
        public void SceneIsInBuildSettingsAndWired()
        {
            Assert.That(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == FrogJumpSceneConfigurator.ScenePath));
            EditorSceneManager.OpenScene(FrogJumpSceneConfigurator.ScenePath);
            var controller = Object.FindFirstObjectByType<FrogJumpController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.IsWired, Is.True);
            Assert.That(Object.FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<PausePanel>(FindObjectsInactive.Include), Is.Not.Null);
        }

        [Test]
        public void SharedTutorialCardIsNotUsed()
        {
            EditorSceneManager.OpenScene(FrogJumpSceneConfigurator.ScenePath);
            Assert.That(Object.FindFirstObjectByType<FrogJumpController>().UsesSharedTutorial, Is.False);
        }

        [Test]
        public void TapsAreIgnoredWhilePausedOrOver()
        {
            Assert.That(FrogJumpController.CanAcceptTap(MinigamePhase.Play, false, 1f), Is.True);
            Assert.That(FrogJumpController.CanAcceptTap(MinigamePhase.Play, false, 0f), Is.False);
            Assert.That(FrogJumpController.CanAcceptTap(MinigamePhase.Play, true, 1f), Is.False);
            Assert.That(FrogJumpController.CanAcceptTap(MinigamePhase.Tutorial, false, 1f), Is.False);
        }

        [Test]
        public void FrogJumpBuildsWithoutTheVolleyballArtAndShowsTheCampus()
        {
            FrogJumpSceneConfigurator.BuildScene();
            EditorSceneManager.OpenScene(FrogJumpSceneConfigurator.ScenePath, OpenSceneMode.Single);
            foreach (var renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                Assert.That(AssetDatabase.GetAssetPath(renderer.sprite), Does.Not.Contain("/Environments/Volleyball/"), renderer.name);
            var world = Object.FindFirstObjectByType<CampusBackdropWorld>();
            Assert.That(world, Is.Not.Null);
            foreach (string name in new[] { "Sky", "Grass" })
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == name))
                    Assert.That(t.IsChildOf(world.transform), Is.True, name + " is an old flat quad");
        }

        [Test]
        public void TimerProgressAndStatusSitOnOneKitCard()
        {
            FrogJumpSceneConfigurator.BuildScene();
            EditorSceneManager.OpenScene(FrogJumpSceneConfigurator.ScenePath, OpenSceneMode.Single);
            Transform card = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(t => t.name == "ProgressCard");
            foreach (string name in new[] { "Timer", "Progress", "Status" })
                Assert.That(card.GetComponentsInChildren<Transform>(true).Any(t => t.name == name), Is.True, name);
            Assert.That(card.GetComponent<UnityEngine.UI.Image>().color.a,
                Is.EqualTo(KMA.UI.Kit.MinigameUiTheme.SurfaceSoft).Within(.01f));
        }
    }
}
