#if UNITY_EDITOR
using System;
using KMA.EditorTools;
using KMA.Gameplay;
using KMA.Gameplay.Volleyball;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KMA.Tests.EditorTools
{
    /// <summary>The player is the same project character in every minigame, untinted, and nobody else is.</summary>
    public sealed class HeroConsistencyTests
    {
        const string SprintScene = "Assets/_Project/Scenes/MG_Sprint.unity";
        const string PlayerRunnerPrefab = "Assets/_Project/Prefabs/Gameplay/PlayerRunnerVisual.prefab";

        [TearDown]
        public void ReleaseScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [Test]
        public void SprintPlayerAndEveryClipItPlaysAreTheHero()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRunnerPrefab);
            var renderer = prefab.GetComponentInChildren<SpriteRenderer>();
            AssertHero(renderer.sprite, "Sprint player prefab");
            Assert.That(renderer.color, Is.EqualTo(Color.white), "Sprint player tint");
            foreach (AnimationClip clip in prefab.GetComponent<Animator>().runtimeAnimatorController.animationClips)
                foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    foreach (ObjectReferenceKeyframe key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                        AssertHero(key.value as Sprite, "Sprint clip " + clip.name);

            EditorSceneManager.OpenScene(SprintScene, OpenSceneMode.Single);
            foreach (var rival in Object.FindObjectsByType<RivalRunnerAI>(FindObjectsSortMode.None))
                AssertNotHero(rival.Sprite.sprite, "Sprint rival lane " + rival.Lane);
        }

        [Test]
        public void FootballKickerIsTheHeroAndTheKeeperIsNot()
        {
            EditorSceneManager.OpenScene(FootballSceneConfigurator.ScenePath, OpenSceneMode.Single);
            FootballPoseSprites poses = Object.FindFirstObjectByType<FootballPresentation>().Poses;
            Assert.That(poses, Is.Not.Null, "Football poses are configured");
            foreach (Sprite sprite in new[] { poses.kickerReady, poses.kickerRunUp, poses.kickerStrike, poses.kickerCelebrate })
                AssertHero(sprite, "Football kicker pose");
            foreach (Sprite sprite in new[] { poses.keeperReady, poses.keeperReachLeft, poses.keeperReachRight,
                         poses.keeperDiveLeft, poses.keeperDiveRight, poses.keeperCheer })
                AssertNotHero(sprite, "Football keeper pose");
            var player = GameObject.Find("FootballWorld/Player").GetComponent<SpriteRenderer>();
            AssertHero(player.sprite, "Football player renderer");
            Assert.That(player.color, Is.EqualTo(Color.white), "Football player tint");
        }

        [Test]
        public void VolleyballPlayerIsTheHeroAndTheOpponentIsNot()
        {
            EditorSceneManager.OpenScene(VolleyballSceneConfigurator.ScenePath, OpenSceneMode.Single);
            var controller = Object.FindFirstObjectByType<VolleyballController>();
            foreach (AthleteAction action in Enum.GetValues(typeof(AthleteAction)))
            {
                foreach (Sprite sprite in controller.PlayerView.FramesFor(action)) AssertHero(sprite, "Volleyball player " + action);
                foreach (Sprite sprite in controller.OpponentView.FramesFor(action)) AssertNotHero(sprite, "Volleyball opponent " + action);
            }
            Assert.That(controller.PlayerView.GetComponent<SpriteRenderer>().color, Is.EqualTo(Color.white), "Volleyball player tint");
        }

        static void AssertHero(Sprite sprite, string where)
        {
            Assert.That(sprite, Is.Not.Null, where + ": sprite is missing");
            Assert.That(CharacterArt.IsPoseOf(sprite, CharacterArt.Hero), Is.True,
                where + " uses " + AssetDatabase.GetAssetPath(sprite));
        }

        static void AssertNotHero(Sprite sprite, string where)
        {
            Assert.That(sprite, Is.Not.Null, where + ": sprite is missing");
            Assert.That(CharacterArt.IsPoseOf(sprite, CharacterArt.Hero), Is.False, where + " must not be the hero");
        }
    }
}
#endif
